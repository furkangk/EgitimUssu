using System.Globalization;
using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T5 / N-43, N-50 (K-19) — Reporting'in hafta/ay kovası ve boş zaman şablonu Türkiye saatine göre.
/// Fikstürü başka dosyada olan sınır testleri yanlarında durur: ödeme aylık kırılımı
/// (<see cref="PaymentSummaryOutstandingTests"/>), takvimde "çalışıldı" eşleşmesi (<see cref="StudentCalendarQueryTests"/>),
/// kapanmış dönem anlık görüntüsü (<see cref="ProgressSnapshotTests"/>), rapor varsayılan dönemi
/// (<see cref="StudentReportPdfGeneratorTests"/>), bildirim metinleri (<see cref="NotificationLocalTimeTextTests"/>).
/// </summary>
public sealed class LocalBucketTests
{
    /// <summary>Pazartesi 16 Kasım 2026 01:30 TR = Pazar 15 Kasım 22:30Z.</summary>
    private static readonly DateTime MondayNightUtc = new(2026, 11, 15, 22, 30, 0, DateTimeKind.Utc);

    private static readonly LocalCalendar Istanbul = new("Europe/Istanbul", new FixedClock(MondayNightUtc));

    [Fact]
    public void Monday_0130_session_belongs_to_this_week_not_the_previous_one()
    {
        var thisWeek = (ISOWeek.GetYear(new DateTime(2026, 11, 16)), ISOWeek.GetWeekOfYear(new DateTime(2026, 11, 16)));

        Assert.Equal(thisWeek, ReportingPeriods.WeekOf(MondayNightUtc, Istanbul));
        Assert.NotEqual(thisWeek, ReportingPeriods.WeekOf(new DateOnly(2026, 11, 15)));   // UTC günü Pazar: geçen hafta
    }

    [Fact]
    public async Task Study_session_projection_puts_a_monday_0130_session_into_this_week()
    {
        await using var dbContext = NewDbContext();
        var handler = new StudentStudySessionProjectionHandler(dbContext, new FixedClock(MondayNightUtc), Istanbul);

        await handler.HandleAsync(Envelope("Study", "StudySessionCompletedDomainEvent", new
        {
            sessionId = Guid.NewGuid(),
            studentId = Guid.NewGuid(),
            subject = "Matematik",
            topic = (string?)null,
            effectiveMinutes = 40,
            breakMinutes = 0,
            endedAtUtc = MondayNightUtc,
        }), CancellationToken.None);

        var week = await dbContext.StudentStudyProjections.SingleAsync();
        Assert.Equal(ISOWeek.GetWeekOfYear(new DateTime(2026, 11, 16)), week.WeekOfYear);
        Assert.Equal(40, week.StudyMinutes);
    }

    [Fact]
    public void Monday_evening_availability_is_read_as_turkey_time()
    {
        // "Pzt 18:00–21:00" şablonu 15:00Z–18:00Z'dir; 16:00–17:00Z dersi ortadan düşer.
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(21, 0)) };
        var busy = new[]
        {
            new TeacherBusyInterval(
                new DateTime(2026, 11, 16, 16, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc),
                "Lesson"),
        };

        var free = FreeSlotCalculator.Compute(
            slots, busy,
            new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc),   // Pazartesi 00:00 TR
            new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc),   // Salı 00:00 TR
            Istanbul);

        Assert.Equal(
            new[]
            {
                (new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 16, 16, 0, 0, DateTimeKind.Utc)),
                (new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc)),
            },
            free.Select(slot => (slot.StartAtUtc, slot.EndAtUtc)));
    }

    [Fact]
    public void Late_night_availability_falls_on_its_local_day()
    {
        // Pazartesi 01:00–02:00 TR = Pazar 22:00–23:00Z: UTC gününe göre yayılsaydı Pazar'a hiç düşmez, Pazartesi
        // 01:00Z'ye (04:00 TR) kayardı.
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(1, 0), new TimeOnly(2, 0)) };

        var free = FreeSlotCalculator.Compute(
            slots, Array.Empty<TeacherBusyInterval>(),
            new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc),
            Istanbul);

        var slot = Assert.Single(free);
        Assert.Equal(new DateTime(2026, 11, 15, 22, 0, 0, DateTimeKind.Utc), slot.StartAtUtc);
        Assert.Equal(new DateTime(2026, 11, 15, 23, 0, 0, DateTimeKind.Utc), slot.EndAtUtc);
    }

    private static IntegrationEvent Envelope(string sourceModule, string name, object payload)
        => new(
            Guid.NewGuid(),
            MondayNightUtc,
            name,
            sourceModule,
            System.Text.Json.JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    private static ReportingDbContext NewDbContext()
        => new(
            new DbContextOptionsBuilder<ReportingDbContext>()
                .UseInMemoryDatabase($"local-bucket-{Guid.NewGuid():N}")
                .Options,
            new NoOpDomainEventMapper());

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }
}
