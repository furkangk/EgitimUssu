using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Application;

/// <summary>M14-5: Öğretmenin bir aralıkta gerçekten boş olduğu zaman parçası.</summary>
/// <param name="StartAtUtc">Başlangıç (UTC).</param>
/// <param name="EndAtUtc">Bitiş (UTC).</param>
public sealed record FreeSlot(DateTime StartAtUtc, DateTime EndAtUtc);

/// <summary>
/// M14-5 boş zaman analizi — <b>saf fonksiyon</b>: haftalık uygunluk şablonunu verilen aralığa yayar,
/// dolu aralıkları (ders + izin) çıkarır, kalan parçaları döndürür.
/// <para>
/// Saf tutulmasının sebebi Öğrenilenler #57 ile aynı: kural tek yerde durur, birim testi ile üretim
/// aynı hesabı çalıştırır. Çakışan dolu aralıklar önce birleştirilir (aynı saatte hem ders hem izin
/// olabilir); eşikten kısa artık parçalar atılır — 10 dakikalık bir boşluk ders için kullanılamaz.
/// </para>
/// <para>
/// P16 T5 / N-43 (K-19): uygunluk şablonu ("Pzt 18:00–21:00") öğretmenin <b>yerel</b> saatidir. Şablon yerel günlere
/// yayılır, her parça <see cref="ILocalCalendar.ToUtc"/> ile UTC'ye çevrilir; dolu aralıklar zaten gerçek UTC'dir.
/// Eskiden şablon UTC günlerine yayılıyordu (18:00 TR, 18:00Z sayılıyordu → sonuç 3 saat kayık). Öğretmenin kendi
/// saat dilimi P24'te; şimdilik platform takvimi.
/// </para>
/// </summary>
public static class FreeSlotCalculator
{
    /// <summary>Bu süreden kısa boşluklar sonuçta yer almaz.</summary>
    public const int DefaultMinimumMinutes = 30;

    /// <summary>Güvenlik sınırı: aralık en fazla bir yıl olabilir (uygunluk şablonu her güne yayılır).</summary>
    private const int MaxDays = 366;

    /// <summary>Şablonu <c>[fromUtc, toUtc)</c> penceresine yerel günlerle yayar (<c>calendar</c>: şablonun okunduğu takvim).</summary>
    public static IReadOnlyList<FreeSlot> Compute(
        IReadOnlyCollection<TeacherWeeklySlot> weeklySlots,
        IReadOnlyCollection<TeacherBusyInterval> busyIntervals,
        DateTime fromUtc,
        DateTime toUtc,
        ILocalCalendar calendar,
        int minimumMinutes = DefaultMinimumMinutes)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (weeklySlots.Count == 0 || toUtc <= fromUtc)
        {
            return Array.Empty<FreeSlot>();
        }

        var busy = Merge(busyIntervals);
        var minimum = TimeSpan.FromMinutes(Math.Max(1, minimumMinutes));
        var free = new List<FreeSlot>();

        // Yerel günler: pencerenin ilk anının yerel gününden son anının yerel gününe (iki uç dahil; kırpma aşağıda).
        var lastDay = calendar.ToLocalDate(toUtc);
        var day = calendar.ToLocalDate(fromUtc);
        for (var index = 0; day <= lastDay && index < MaxDays; index++, day = day.AddDays(1))
        {
            foreach (var slot in weeklySlots.Where(item => item.DayOfWeek == day.DayOfWeek))
            {
                var slotStart = calendar.ToUtc(day, slot.StartTime);
                var slotEnd = calendar.ToUtc(day, slot.EndTime);

                // Sorgu penceresine kırp.
                var windowStart = slotStart < fromUtc ? fromUtc : slotStart;
                var windowEnd = slotEnd > toUtc ? toUtc : slotEnd;
                if (windowEnd <= windowStart)
                {
                    continue;
                }

                free.AddRange(Subtract(windowStart, windowEnd, busy, minimum));
            }
        }

        return free.OrderBy(item => item.StartAtUtc).ToArray();
    }

    /// <summary>Çakışan/bitişik dolu aralıkları tek parçaya indirger, başlangıca göre sıralar.</summary>
    private static List<TeacherBusyInterval> Merge(IReadOnlyCollection<TeacherBusyInterval> intervals)
    {
        var merged = new List<TeacherBusyInterval>();
        foreach (var interval in intervals.Where(item => item.EndAtUtc > item.StartAtUtc).OrderBy(item => item.StartAtUtc))
        {
            var last = merged.Count == 0 ? null : merged[^1];
            if (last is not null && interval.StartAtUtc <= last.EndAtUtc)
            {
                if (interval.EndAtUtc > last.EndAtUtc)
                {
                    merged[^1] = last with { EndAtUtc = interval.EndAtUtc };
                }

                continue;
            }

            merged.Add(interval);
        }

        return merged;
    }

    /// <summary>Bir uygunluk penceresinden dolu aralıkları düşer; eşiği geçen parçaları döndürür.</summary>
    private static IEnumerable<FreeSlot> Subtract(
        DateTime windowStart,
        DateTime windowEnd,
        IReadOnlyList<TeacherBusyInterval> busy,
        TimeSpan minimum)
    {
        var cursor = windowStart;
        foreach (var interval in busy)
        {
            if (interval.EndAtUtc <= cursor)
            {
                continue;
            }

            if (interval.StartAtUtc >= windowEnd)
            {
                break;
            }

            if (interval.StartAtUtc > cursor && interval.StartAtUtc - cursor >= minimum)
            {
                yield return new FreeSlot(cursor, interval.StartAtUtc);
            }

            if (interval.EndAtUtc > cursor)
            {
                cursor = interval.EndAtUtc;
            }

            if (cursor >= windowEnd)
            {
                yield break;
            }
        }

        if (windowEnd - cursor >= minimum)
        {
            yield return new FreeSlot(cursor, windowEnd);
        }
    }
}
