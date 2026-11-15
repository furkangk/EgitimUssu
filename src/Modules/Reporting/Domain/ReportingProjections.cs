using System.Globalization;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Domain;

/// <summary>
/// M14 raporlama projeksiyonlarının dönem kovaları. P16 T5 / N-50 (K-19): sınırlar <b>Türkiye yerel günüdür</b> — bir
/// an önce <see cref="ILocalCalendar"/> ile yerel tarihe çevrilir, kova o tarihten alınır (Pazartesi 01:30 TR seansı bu
/// haftaya, 1 Eylül 00:30 TR dersi Eylül'e düşer; eskiden UTC'ydi). Study'nin haftalık özetiyle aynı kural (düz yerel
/// tarih; Study'nin 04:00 seri günü yalnız seri sayacına aittir). Hafta numarası ISO-8601'dir — hafta Pazartesi başlar
/// ve yıl sınırında takvim yılından ayrışabilir (1 Ocak 2027 Cuma, ISO'da hâlâ 2026'nın 53. haftasıdır). M10-1 anlık
/// görüntüleriyle aynı hafta tanımı. UTC <c>DateTime</c> alan aşırı yükleme bilerek yok: takvimsiz çağrı derlenmez.
/// </summary>
public static class ReportingPeriods
{
    public static (int Year, int Month) MonthOf(DateOnly localDate) => (localDate.Year, localDate.Month);

    public static (int Year, int Week) WeekOf(DateOnly localDate)
    {
        var date = localDate.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }

    public static (int Year, int Month) MonthOf(DateTime utc, ILocalCalendar calendar) => MonthOf(calendar.ToLocalDate(utc));

    public static (int Year, int Week) WeekOf(DateTime utc, ILocalCalendar calendar) => WeekOf(calendar.ToLocalDate(utc));
}

// ---------------------------------------------------------------------------------------------
// M14-1 — Öğretmen aylık özeti
// ---------------------------------------------------------------------------------------------

/// <summary>
/// M14-1: Bir öğretmenin bir aydaki ders/tahsilat özeti (K-04 read-model). Yalnız integration event'lerden
/// beslenir.
/// <para>
/// Sayaçlar <b>kümeden türetilir</b> (P07 Task 2 deseni): her tamamlanan ders, iptal edilen ders ve ödeme
/// beklentisi bir <see cref="TeacherMonthlyItem"/> satırıdır, ekleme upsert'tir. Böylece aynı iş olayı iki
/// kez yayınlanırsa (ders yeniden tamamlanır) sayaç bozulmaz.
/// </para>
/// <para>Tahsilat/iade satırları hareket kimliğiyle upsert edilir; net tahsilat bu kümeden türetilir.
/// Farklı EventId ile tekrar teslim de aynı hareketi ikinci kez sayamaz.</para>
/// </summary>
public sealed class TeacherMonthlyProjection : ProjectionEntity
{
    private readonly List<TeacherMonthlyItem> _items = [];

    private TeacherMonthlyProjection()
    {
    }

    public TeacherMonthlyProjection(Guid teacherUserId, int year, int month)
    {
        TeacherUserId = teacherUserId;
        Year = year;
        Month = month;
    }

    public Guid TeacherUserId { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    /// <summary>Bu ayda tahsil edilen net toplam (hareket kümesinden türetilir).</summary>
    public decimal CollectedAmount { get; private set; }

    public IReadOnlyCollection<TeacherMonthlyItem> Items => _items;

    public int CompletedLessonCount => _items.Count(item => item.Kind == TeacherMonthlyItemKind.CompletedLesson);

    public int CancelledLessonCount => _items.Count(item => item.Kind == TeacherMonthlyItemKind.CancelledLesson);

    /// <summary>Bu ayda en az bir dersi tamamlanan <b>farklı</b> öğrenci sayısı.</summary>
    public int ActiveStudentCount => _items
        .Where(item => item.Kind == TeacherMonthlyItemKind.CompletedLesson && item.StudentId is not null)
        .Select(item => item.StudentId!.Value)
        .Distinct()
        .Count();

    /// <summary>Bu ayda açılan ödeme kayıtlarının beklenen toplamı.</summary>
    public decimal ExpectedAmount => _items
        .Where(item => item.Kind == TeacherMonthlyItemKind.ExpectedPayment)
        .Sum(item => item.Amount ?? 0m);

    public void TrackCompletedLesson(Guid lessonSessionId, Guid studentId)
        => Upsert(TeacherMonthlyItemKind.CompletedLesson, lessonSessionId, studentId: studentId);

    public void TrackCancelledLesson(Guid lessonScheduleId)
        => Upsert(TeacherMonthlyItemKind.CancelledLesson, lessonScheduleId);

    public void TrackCancelledOccurrence(Guid lessonScheduleId, DateTime occurrenceStartUtc)
        => Upsert(TeacherMonthlyItemKind.CancelledLesson, lessonScheduleId, occurrenceStartUtc: occurrenceStartUtc);

    public void TrackExpectedPayment(Guid paymentRecordId, decimal amount)
        => Upsert(TeacherMonthlyItemKind.ExpectedPayment, paymentRecordId, amount: amount);

    public void AddCollected(decimal delta) => CollectedAmount += delta;

    public void TrackPaymentTransaction(Guid transactionId, bool isRefund, decimal amount)
    {
        Upsert(isRefund ? TeacherMonthlyItemKind.Refund : TeacherMonthlyItemKind.Collection, transactionId, amount: amount);
        CollectedAmount = _items.Where(x => x.Kind == TeacherMonthlyItemKind.Collection).Sum(x => x.Amount ?? 0m)
            - _items.Where(x => x.Kind == TeacherMonthlyItemKind.Refund).Sum(x => x.Amount ?? 0m);
    }

    public void ClearPaymentTransactions()
    {
        _items.RemoveAll(x => x.Kind is TeacherMonthlyItemKind.Collection or TeacherMonthlyItemKind.Refund);
        CollectedAmount = 0m;
    }

    public decimal RefundedAmount => _items.Where(x => x.Kind == TeacherMonthlyItemKind.Refund).Sum(x => x.Amount ?? 0m);

    private void Upsert(TeacherMonthlyItemKind kind, Guid itemId, Guid? studentId = null, decimal? amount = null, DateTime? occurrenceStartUtc = null)
    {
        var key = occurrenceStartUtc ?? DateTime.UnixEpoch;
        var existing = _items.FirstOrDefault(item => item.Kind == kind && item.ItemId == itemId && item.OccurrenceStartUtc == key);
        if (existing is null)
        {
            _items.Add(new TeacherMonthlyItem(TeacherUserId, Year, Month, kind, itemId, studentId, amount, key));
            return;
        }

        existing.Update(studentId, amount);
    }
}

public sealed class TeacherMonthlyItem
{
    private TeacherMonthlyItem()
    {
    }

    public TeacherMonthlyItem(
        Guid teacherUserId,
        int year,
        int month,
        TeacherMonthlyItemKind kind,
        Guid itemId,
        Guid? studentId,
        decimal? amount,
        DateTime? occurrenceStartUtc = null)
    {
        OccurrenceStartUtc = occurrenceStartUtc ?? DateTime.UnixEpoch;
        TeacherUserId = teacherUserId;
        Year = year;
        Month = month;
        Kind = kind;
        ItemId = itemId;
        StudentId = studentId;
        Amount = amount;
    }

    public Guid TeacherUserId { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public TeacherMonthlyItemKind Kind { get; private set; }

    /// <summary>Kaynak modüldeki kaydın kimliği (ders oturumu / ders planı / ödeme kaydı).</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Single iptalin orijinal oluşumu; eski/toplu satırlar ve diğer item türleri UTC epoch.</summary>
    public DateTime OccurrenceStartUtc { get; private set; } = DateTime.UnixEpoch;

    public Guid? StudentId { get; private set; }

    public decimal? Amount { get; private set; }

    internal void Update(Guid? studentId, decimal? amount)
    {
        if (studentId is not null)
        {
            StudentId = studentId;
        }

        if (amount is not null)
        {
            Amount = amount;
        }
    }
}

public enum TeacherMonthlyItemKind
{
    CompletedLesson = 1,
    CancelledLesson = 2,
    ExpectedPayment = 3,
    Collection = 4,
    Refund = 5
}

// ---------------------------------------------------------------------------------------------
// M14-2 — Öğrenci haftalık çalışma analizi
// ---------------------------------------------------------------------------------------------

/// <summary>
/// M14-2: Bir öğrencinin bir ISO haftasındaki çalışma özeti. Seans ve test satır olarak tutulur; süre
/// düzeltilirse (seans kurtarma / düzenleme) satır güncellenir, sayaç çoğalmaz.
/// </summary>
public sealed class StudentStudyProjection : ProjectionEntity
{
    private readonly List<StudentStudyItem> _items = [];

    private StudentStudyProjection()
    {
    }

    public StudentStudyProjection(Guid studentId, int year, int weekOfYear)
    {
        StudentId = studentId;
        Year = year;
        WeekOfYear = weekOfYear;
    }

    public Guid StudentId { get; private set; }

    public int Year { get; private set; }

    public int WeekOfYear { get; private set; }

    public IReadOnlyCollection<StudentStudyItem> Items => _items;

    public int StudyMinutes => _items
        .Where(item => item.Kind == StudentStudyItemKind.Session)
        .Sum(item => item.Minutes);

    public int SessionCount => _items.Count(item => item.Kind == StudentStudyItemKind.Session);

    public int TestCount => _items.Count(item => item.Kind == StudentStudyItemKind.Test);

    public void TrackSession(Guid sessionId, int minutes)
        => Upsert(StudentStudyItemKind.Session, sessionId, Math.Max(0, minutes));

    public void TrackTest(Guid testResultId) => Upsert(StudentStudyItemKind.Test, testResultId, 0);

    private void Upsert(StudentStudyItemKind kind, Guid itemId, int minutes)
    {
        var existing = _items.FirstOrDefault(item => item.Kind == kind && item.ItemId == itemId);
        if (existing is null)
        {
            _items.Add(new StudentStudyItem(StudentId, Year, WeekOfYear, kind, itemId, minutes));
            return;
        }

        existing.UpdateMinutes(minutes);
    }
}

public sealed class StudentStudyItem
{
    private StudentStudyItem()
    {
    }

    public StudentStudyItem(Guid studentId, int year, int weekOfYear, StudentStudyItemKind kind, Guid itemId, int minutes)
    {
        StudentId = studentId;
        Year = year;
        WeekOfYear = weekOfYear;
        Kind = kind;
        ItemId = itemId;
        Minutes = minutes;
    }

    public Guid StudentId { get; private set; }

    public int Year { get; private set; }

    public int WeekOfYear { get; private set; }

    public StudentStudyItemKind Kind { get; private set; }

    public Guid ItemId { get; private set; }

    public int Minutes { get; private set; }

    internal void UpdateMinutes(int minutes) => Minutes = minutes;
}

public enum StudentStudyItemKind
{
    Session = 1,
    Test = 2
}

// ---------------------------------------------------------------------------------------------
// M14-3 — Öğrenci aylık performansı (ders bazlı)
// ---------------------------------------------------------------------------------------------

/// <summary>
/// M14-3: Bir öğrencinin bir ders için bir aydaki performansı. İki kaynaktan beslenir: konu hâkimiyeti
/// (M10 <c>TopicMasteryChanged</c>) ve test netleri (M08 <c>TestResultRecorded</c>).
/// <para>
/// <see cref="MasteryScore"/> dersin konularının <b>ortalamasıdır</b> (M10-1 anlık görüntüsüyle aynı kural);
/// her konu tek satır tutar ve yeniden değerlendirmede güncellenir. <see cref="AverageNet"/> aydaki test
/// netlerinin ortalamasıdır.
/// </para>
/// <para>
/// ⚠️ Aya göre <b>değişim (delta)</b> burada saklanmaz: komşuya bağlı bir değerdir ve sorgu penceresi
/// kırpıldığında yanlışa döner (Öğrenilenler #102). Sorgu anında <see cref="ReportingDelta"/> ile üretilir.
/// </para>
/// </summary>
public sealed class StudentPerformanceProjection : ProjectionEntity
{
    private readonly List<StudentPerformanceItem> _items = [];

    private StudentPerformanceProjection()
    {
    }

    public StudentPerformanceProjection(Guid studentId, string subject, int year, int month)
    {
        StudentId = studentId;
        Subject = subject.Trim();
        Year = year;
        Month = month;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int Month { get; private set; }

    public IReadOnlyCollection<StudentPerformanceItem> Items => _items;

    /// <summary>Dersin konularının hâkimiyet skoru ortalaması (0–100, tam sayı); konu yoksa 0.</summary>
    public int MasteryScore
    {
        get
        {
            var topics = _items.Where(item => item.Kind == StudentPerformanceItemKind.Topic).ToArray();
            return topics.Length == 0 ? 0 : (int)Math.Round(topics.Average(item => item.Score), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Aydaki testlerin ortalama net oranı (0–1); test yoksa 0.</summary>
    public decimal AverageNet
    {
        get
        {
            var tests = _items.Where(item => item.Kind == StudentPerformanceItemKind.Test).ToArray();
            return tests.Length == 0 ? 0m : Math.Round(tests.Average(item => item.Score), 4);
        }
    }

    public int TestCount => _items.Count(item => item.Kind == StudentPerformanceItemKind.Test);

    public void TrackTopicMastery(string topic, decimal masteryScore)
        => Upsert(StudentPerformanceItemKind.Topic, topic.Trim(), Math.Clamp(masteryScore, 0m, 100m));

    public void TrackTestNet(Guid testResultId, decimal netRatio)
        => Upsert(StudentPerformanceItemKind.Test, testResultId.ToString("N"), Math.Clamp(netRatio, 0m, 1m));

    private void Upsert(StudentPerformanceItemKind kind, string itemKey, decimal score)
    {
        var existing = _items.FirstOrDefault(item => item.Kind == kind && item.ItemKey == itemKey);
        if (existing is null)
        {
            _items.Add(new StudentPerformanceItem(StudentId, Subject, Year, Month, kind, itemKey, score));
            return;
        }

        existing.UpdateScore(score);
    }
}

public sealed class StudentPerformanceItem
{
    private StudentPerformanceItem()
    {
    }

    public StudentPerformanceItem(
        Guid studentId,
        string subject,
        int year,
        int month,
        StudentPerformanceItemKind kind,
        string itemKey,
        decimal score)
    {
        StudentId = studentId;
        Subject = subject;
        Year = year;
        Month = month;
        Kind = kind;
        ItemKey = itemKey;
        Score = score;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int Month { get; private set; }

    public StudentPerformanceItemKind Kind { get; private set; }

    /// <summary>Konu adı (Kind=Topic) ya da test sonucu kimliği (Kind=Test).</summary>
    public string ItemKey { get; private set; } = string.Empty;

    /// <summary>Konu için 0–100 hâkimiyet skoru; test için 0–1 net oranı.</summary>
    public decimal Score { get; private set; }

    internal void UpdateScore(decimal score) => Score = score;
}

public enum StudentPerformanceItemKind
{
    Topic = 1,
    Test = 2
}

/// <summary>
/// Aylar arası değişim. Saklanmaz, sorgu anında ardışık iki aydan üretilir (Öğrenilenler #102):
/// serinin ilk ayının karşılaştıracağı önceki ay yoktur → 0.
/// </summary>
public static class ReportingDelta
{
    public static int FromScores(int? previous, int current) => previous is null ? 0 : current - previous.Value;
}
