using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// M06-3 → M11: Ödev son teslim uyarılarının ortak tabanı. Olay yalnız <c>StudentId</c> (öğrenci
/// <b>profili</b>) taşır; bildirim alıcısı kullanıcı kimliğidir — eşleme <see cref="IStudentDirectory"/>
/// sözleşmesinden okunur (modül sınırı korunur, Notifications Assignments/Students DbContext'ine bakmaz).
/// Push burada gönderilmez — <c>NotificationDispatcher</c> turu <c>PushedOnUtc</c>'si boş kayıtları
/// toplu gönderir ve "ödev hatırlatması" tercih kapısını orada uygular (Öğrenilenler #40/#55).
/// </summary>
public abstract class AssignmentDueNotificationHandlerBase : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IStudentDirectory _students;
    private readonly IUserNotificationRepository _userNotifications;

    protected AssignmentDueNotificationHandlerBase(
        NotificationsDbContext dbContext,
        IStudentDirectory students,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock,
        ILocalCalendar calendar)
        : base(dbContext, clock)
    {
        _students = students;
        _userNotifications = userNotifications;
        IdGenerator = idGenerator;
        Calendar = calendar;
    }

    protected IIdGenerator IdGenerator { get; }

    /// <summary>P16 T5 / N-50: metindeki son teslim saati yerel (K-19).</summary>
    protected ILocalCalendar Calendar { get; }

    /// <summary>Tüketilecek olayın adı (<c>Assignments</c> modülünden).</summary>
    protected abstract string EventName { get; }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Assignments" && integrationEvent.Name == EventName;

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<AssignmentDuePayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            throw new PoisonMessageException("Olay yükü null olamaz.");
        }

        var dueDate = NotificationTimeText.LocalDateTime(Calendar, payload.DueDateUtc);
        await NotifyAsync(payload, dueDate, cancellationToken);
        return true;
    }

    /// <summary>Olaya özgü alıcı/metin kurgusu.</summary>
    protected abstract Task NotifyAsync(AssignmentDuePayload payload, string dueDateText, CancellationToken cancellationToken);

    /// <summary>Öğrenci profilinden kullanıcı kimliğini çözer; bağlı kullanıcı yoksa <c>null</c>.</summary>
    protected Task<Guid?> ResolveStudentUserIdAsync(Guid studentId, CancellationToken cancellationToken)
        => _students.GetOwnerUserIdAsync(studentId, cancellationToken);

    protected Task AddAsync(
        Guid userId,
        UserNotificationKind kind,
        string title,
        string body,
        string route,
        CancellationToken cancellationToken)
        => _userNotifications.AddAsync(
            new UserNotification(IdGenerator.New(), userId, kind, title, body, route, Clock.UtcNow),
            cancellationToken);

    /// <summary>M06-3 olaylarının ortak gövdesi (DueSoon ve Missed aynı alanları taşır).</summary>
    protected sealed record AssignmentDuePayload(
        Guid AssignmentId,
        Guid StudentId,
        Guid TeacherUserId,
        string Title,
        DateTime DueDateUtc,
        DateTime NotifiedOnUtc);
}

/// <summary>M06-3: "Ödev teslimine 24 saat kaldı" — yalnız öğrenciye.</summary>
public sealed class AssignmentDueSoonNotificationHandler : AssignmentDueNotificationHandlerBase
{
    public AssignmentDueSoonNotificationHandler(
        NotificationsDbContext dbContext,
        IStudentDirectory students,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock,
        ILocalCalendar calendar)
        : base(dbContext, students, userNotifications, idGenerator, clock, calendar)
    {
    }

    protected override string EventName => "AssignmentDueSoonDomainEvent";

    protected override async Task NotifyAsync(
        AssignmentDuePayload payload,
        string dueDateText,
        CancellationToken cancellationToken)
    {
        var studentUserId = await ResolveStudentUserIdAsync(payload.StudentId, cancellationToken);
        if (studentUserId is null)
        {
            return;
        }

        await AddAsync(
            studentUserId.Value,
            UserNotificationKind.AssignmentDue,
            "Ödev teslimine 24 saat kaldı",
            $"\"{payload.Title}\" ödevinin son teslimi {dueDateText}.",
            "/student/assignments",
            cancellationToken);
    }
}

/// <summary>
/// M06-3 (+M09-3): "Ödev teslim edilmedi" — öğrenciye ve onaylı Premium velilerine
/// ("kaçırılan ödev" tercihi açıksa; PRD 9.3).
/// </summary>
public sealed class AssignmentMissedNotificationHandler : AssignmentDueNotificationHandlerBase
{
    private readonly IParentNotificationDirectory _parents;
    private readonly IParentNotificationRepository _parentNotifications;

    public AssignmentMissedNotificationHandler(
        NotificationsDbContext dbContext,
        IStudentDirectory students,
        IParentNotificationDirectory parents,
        IParentNotificationRepository parentNotifications,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock,
        ILocalCalendar calendar)
        : base(dbContext, students, userNotifications, idGenerator, clock, calendar)
    {
        _parents = parents;
        _parentNotifications = parentNotifications;
    }

    protected override string EventName => "AssignmentMissedDomainEvent";

    protected override async Task NotifyAsync(
        AssignmentDuePayload payload,
        string dueDateText,
        CancellationToken cancellationToken)
    {
        const string Title = "Ödev teslim edilmedi";
        var body = $"\"{payload.Title}\" ödevinin son teslimi {dueDateText} olarak geçti; teslim alınmadı.";

        var studentUserId = await ResolveStudentUserIdAsync(payload.StudentId, cancellationToken);
        if (studentUserId is not null)
        {
            await AddAsync(
                studentUserId.Value,
                UserNotificationKind.AssignmentMissed,
                Title,
                body,
                "/student/assignments",
                cancellationToken);
        }

        var targets = await _parents.GetApprovedParentsForStudentAsync(payload.StudentId, cancellationToken);
        foreach (var target in targets)
        {
            // Premium kapısı (PRD 9.3) + "kaçırılan ödev" tercih kapısı.
            if (target.Tier != MembershipTier.Premium || !target.Prefs.MissedAssignment)
            {
                continue;
            }

            await _parentNotifications.AddAsync(
                new ParentNotification(
                    IdGenerator.New(),
                    target.ParentUserId,
                    payload.StudentId,
                    ParentNotificationType.NewAssignment,
                    Title,
                    body,
                    Clock.UtcNow),
                cancellationToken);

            await AddAsync(
                target.ParentUserId,
                UserNotificationKind.AssignmentMissed,
                Title,
                body,
                "/parent/assignments",
                cancellationToken);
        }
    }
}
