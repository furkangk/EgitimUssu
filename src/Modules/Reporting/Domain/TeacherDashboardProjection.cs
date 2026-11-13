using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Domain;

/// <summary>
/// M14 — öğretmen paneli read-model'i (K-04). Yalnız diğer modüllerin integration event'lerinden beslenir;
/// Reporting hiçbir modülün veritabanını okumaz.
/// <para>
/// Sayaçlar <b>saklanmaz, kümeden türetilir</b>: her ders/ödev/gecikmiş ödeme/öğrenci bir <see cref="TeacherDashboardItem"/>
/// satırıdır. Böylece "+1/-1" sayaç deseninin kırıldığı yerler (aynı ödev hem Completed hem Approved olur, aynı ödeme
/// iki kez Overdue işaretlenir) sayacı bozmaz: ekleme upsert, çıkarma "varsa sil"dir — yani doğal olarak idempotenttir.
/// </para>
/// </summary>
public sealed class TeacherDashboardProjection : ProjectionEntity
{
    private readonly List<TeacherDashboardItem> _items = [];

    private TeacherDashboardProjection()
    {
    }

    public TeacherDashboardProjection(Guid teacherUserId) => TeacherUserId = teacherUserId;

    public Guid TeacherUserId { get; private set; }

    public IReadOnlyCollection<TeacherDashboardItem> Items => _items;

    /// <summary>Planlanan ders (yeniden planlamada aynı satırın saati güncellenir).</summary>
    public void TrackLesson(Guid lessonScheduleId, DateTime startAtUtc)
        => Upsert(TeacherDashboardItemKind.Lesson, lessonScheduleId, occursAtUtc: startAtUtc);

    public void UntrackLesson(Guid lessonScheduleId)
        => Remove(TeacherDashboardItemKind.Lesson, lessonScheduleId);

    public void TrackOpenAssignment(Guid assignmentId)
        => Upsert(TeacherDashboardItemKind.OpenAssignment, assignmentId);

    public void UntrackAssignment(Guid assignmentId)
        => Remove(TeacherDashboardItemKind.OpenAssignment, assignmentId);

    public void TrackOverduePayment(Guid paymentRecordId, decimal outstandingAmount, string currency)
        => Upsert(TeacherDashboardItemKind.OverduePayment, paymentRecordId, amount: outstandingAmount, currency: currency);

    public void UntrackOverduePayment(Guid paymentRecordId)
        => Remove(TeacherDashboardItemKind.OverduePayment, paymentRecordId);

    public void TrackStudent(Guid studentId)
        => Upsert(TeacherDashboardItemKind.ActiveStudent, studentId);

    /// <summary>Verilen UTC gününde (00:00–24:00) planlanmış ders sayısı. Gün sınırı çağıranın saatinden gelir.</summary>
    public int CountLessonsOn(DateTime dayUtc)
    {
        var start = DateTime.SpecifyKind(dayUtc.Date, DateTimeKind.Utc);
        var end = start.AddDays(1);
        return _items.Count(item =>
            item.Kind == TeacherDashboardItemKind.Lesson
            && item.OccursAtUtc >= start
            && item.OccursAtUtc < end);
    }

    public int PendingAssignmentCount => _items.Count(item => item.Kind == TeacherDashboardItemKind.OpenAssignment);

    public int OverduePaymentCount => _items.Count(item => item.Kind == TeacherDashboardItemKind.OverduePayment);

    public decimal OverdueAmountTotal =>
        _items.Where(item => item.Kind == TeacherDashboardItemKind.OverduePayment).Sum(item => item.Amount ?? 0m);

    /// <summary>Gecikmiş ödemelerin para birimi; kayıt yoksa varsayılan TRY (çoklu para birimi karışımı v1'de yok).</summary>
    public string OverdueCurrency =>
        _items.FirstOrDefault(item => item.Kind == TeacherDashboardItemKind.OverduePayment && item.Currency is not null)?.Currency
        ?? "TRY";

    public int ActiveStudentCount => _items.Count(item => item.Kind == TeacherDashboardItemKind.ActiveStudent);

    private void Upsert(
        TeacherDashboardItemKind kind,
        Guid itemId,
        DateTime? occursAtUtc = null,
        decimal? amount = null,
        string? currency = null)
    {
        var existing = Find(kind, itemId);
        if (existing is null)
        {
            _items.Add(new TeacherDashboardItem(TeacherUserId, kind, itemId, occursAtUtc, amount, currency));
            return;
        }

        existing.Update(occursAtUtc, amount, currency);
    }

    private void Remove(TeacherDashboardItemKind kind, Guid itemId)
    {
        var existing = Find(kind, itemId);
        if (existing is not null)
        {
            _items.Remove(existing);
        }
    }

    private TeacherDashboardItem? Find(TeacherDashboardItemKind kind, Guid itemId)
        => _items.FirstOrDefault(item => item.Kind == kind && item.ItemId == itemId);
}

/// <summary>Projeksiyonun tek bir kaynağı (ders, açık ödev, gecikmiş ödeme veya aktif öğrenci).</summary>
public sealed class TeacherDashboardItem
{
    private TeacherDashboardItem()
    {
    }

    public TeacherDashboardItem(
        Guid teacherUserId,
        TeacherDashboardItemKind kind,
        Guid itemId,
        DateTime? occursAtUtc,
        decimal? amount,
        string? currency)
    {
        TeacherUserId = teacherUserId;
        Kind = kind;
        ItemId = itemId;
        OccursAtUtc = occursAtUtc;
        Amount = amount;
        Currency = currency;
    }

    public Guid TeacherUserId { get; private set; }

    public TeacherDashboardItemKind Kind { get; private set; }

    /// <summary>Kaynak modüldeki kaydın kimliği (ders planı / ödev / ödeme kaydı / öğrenci).</summary>
    public Guid ItemId { get; private set; }

    public DateTime? OccursAtUtc { get; private set; }

    public decimal? Amount { get; private set; }

    public string? Currency { get; private set; }

    internal void Update(DateTime? occursAtUtc, decimal? amount, string? currency)
    {
        if (occursAtUtc is not null)
        {
            OccursAtUtc = occursAtUtc;
        }

        if (amount is not null)
        {
            Amount = amount;
        }

        if (currency is not null)
        {
            Currency = currency;
        }
    }
}

public enum TeacherDashboardItemKind
{
    Lesson = 1,
    OpenAssignment = 2,
    OverduePayment = 3,
    ActiveStudent = 4
}
