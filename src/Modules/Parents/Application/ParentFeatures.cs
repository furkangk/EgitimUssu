using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Parents.Application;

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record CreateParentProfileCommand(
    Guid UserId,
    string FullName,
    string? ContactPhone,
    string? ContactEmail) : ICommand<Result<ParentProfileResponse>>;

public sealed record UpdateNotificationPreferencesCommand(
    Guid ParentUserId,
    bool NotifyMissedAssignment,
    bool NotifyWeeklyProgressSummary,
    bool NotifyLessonReminders,
    bool NotifyTestResults,
    bool NotifyPayments,
    ParentNotificationChannel Channel) : ICommand<Result<ParentProfileResponse>>;

public sealed record RequestChildLinkCommand(
    Guid ParentUserId,
    Guid StudentId,
    string? Relationship,
    string? ChildDisplayName,
    string? InviteCode,
    bool IsPrimaryContact) : ICommand<Result<ChildLinkResponse>>;

public sealed record ApproveChildLinkCommand(Guid LinkId, Guid ApprovedByUserId) : ICommand<Result<ChildLinkResponse>>;

public sealed record RejectChildLinkCommand(Guid LinkId, Guid RejectedByUserId) : ICommand<Result<ChildLinkResponse>>;

public sealed record RevokeChildLinkCommand(Guid LinkId) : ICommand<Result<ChildLinkResponse>>;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

public sealed record GetParentProfileQuery(Guid UserId) : IQuery<Result<ParentProfileResponse>>;

public sealed record ListChildrenQuery(Guid ParentUserId) : IQuery<Result<IReadOnlyCollection<ChildLinkResponse>>>;

public sealed record GetChildDashboardQuery(Guid ParentUserId, Guid StudentId) : IQuery<Result<ChildDashboardResponse>>;

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

public sealed record ParentProfileResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string? ContactPhone,
    string? ContactEmail,
    NotificationPreferencesResponse Preferences,
    bool IsActive,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public sealed record NotificationPreferencesResponse(
    bool MissedAssignment,
    bool WeeklyProgressSummary,
    bool LessonReminders,
    bool TestResults,
    bool Payments,
    string Channel);

public sealed record ChildLinkResponse(
    Guid Id,
    Guid ParentUserId,
    Guid StudentId,
    string? ChildDisplayName,
    string? Relationship,
    string Status,
    bool IsPrimaryContact,
    DateTime RequestedOnUtc,
    DateTime? LinkedOnUtc,
    ChildProgressSummaryResponse? Progress);

public sealed record ChildProgressSummaryResponse(
    int CompletedLessonCount,
    DateTime? LastLessonCompletedAtUtc,
    int OpenAssignmentCount,
    int WeeklyStudyMinutes);

public sealed record ChildDashboardResponse(
    Guid StudentId,
    string? ChildDisplayName,
    string LinkStatus,
    StudySummaryResponse Study,
    LessonSummaryResponse Lessons,
    AssignmentSummaryResponse Assignments,
    PaymentSummaryResponse Payments,
    DateTime? UpdatedOnUtc);

public sealed record StudySummaryResponse(int WeeklyStudyMinutes, int StreakDays, bool HasData);

public sealed record LessonSummaryResponse(int CompletedLessonCount, int PlannedLessonCount, DateTime? LastLessonCompletedAtUtc);

public sealed record AssignmentSummaryResponse(int TotalCount, int OpenCount, int CompletedCount);

public sealed record PaymentSummaryResponse(
    string Currency,
    decimal ExpectedTotal,
    decimal CollectedTotal,
    decimal OutstandingTotal,
    DateTime? LastUpdatedAtUtc);

// ---------------------------------------------------------------------------
// Repository abstraction
// ---------------------------------------------------------------------------

public interface IParentRepository
{
    Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken cancellationToken);

    Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken cancellationToken);

    Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken cancellationToken);

    Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddProfileAsync(ParentProfile profile, CancellationToken cancellationToken);

    Task AddLinkAsync(ParentChildLink link, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---------------------------------------------------------------------------
// Command handlers
// ---------------------------------------------------------------------------

public sealed class CreateParentProfileCommandHandler : ICommandHandler<CreateParentProfileCommand, Result<ParentProfileResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateParentProfileCommandHandler(IParentRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ParentProfileResponse>> Handle(CreateParentProfileCommand command, CancellationToken cancellationToken)
    {
        // Idempotent: aynı kullanıcı için profil varsa mevcut olanı döndür (onboarding tekrarına dayanıklı).
        var existing = await _repository.GetProfileByUserIdAsync(command.UserId, cancellationToken);
        if (existing is not null)
        {
            return Result<ParentProfileResponse>.Success(existing.ToResponse());
        }

        var profile = new ParentProfile(
            _idGenerator.New(),
            command.UserId,
            command.FullName.Trim(),
            command.ContactPhone?.Trim(),
            command.ContactEmail?.Trim(),
            _clock.UtcNow);

        await _repository.AddProfileAsync(profile, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ParentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class UpdateNotificationPreferencesCommandHandler : ICommandHandler<UpdateNotificationPreferencesCommand, Result<ParentProfileResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IClock _clock;

    public UpdateNotificationPreferencesCommandHandler(IParentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ParentProfileResponse>> Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetProfileByUserIdAsync(command.ParentUserId, cancellationToken);
        if (profile is null)
        {
            return Result<ParentProfileResponse>.Failure(ParentErrors.ProfileNotFound);
        }

        profile.UpdateNotificationPreferences(
            command.NotifyMissedAssignment,
            command.NotifyWeeklyProgressSummary,
            command.NotifyLessonReminders,
            command.NotifyTestResults,
            command.NotifyPayments,
            command.Channel,
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ParentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class RequestChildLinkCommandHandler : ICommandHandler<RequestChildLinkCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RequestChildLinkCommandHandler(IParentRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ChildLinkResponse>> Handle(RequestChildLinkCommand command, CancellationToken cancellationToken)
    {
        var active = await _repository.GetActiveLinkAsync(command.ParentUserId, command.StudentId, cancellationToken);
        if (active is not null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkAlreadyExists);
        }

        var link = new ParentChildLink(
            _idGenerator.New(),
            command.ParentUserId,
            command.StudentId,
            command.ChildDisplayName,
            command.Relationship,
            command.InviteCode,
            command.IsPrimaryContact,
            _clock.UtcNow);

        await _repository.AddLinkAsync(link, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
}

public sealed class ApproveChildLinkCommandHandler : ICommandHandler<ApproveChildLinkCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IClock _clock;

    public ApproveChildLinkCommandHandler(IParentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ChildLinkResponse>> Handle(ApproveChildLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkNotFound);
        }

        link.Approve(command.ApprovedByUserId, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
}

public sealed class RejectChildLinkCommandHandler : ICommandHandler<RejectChildLinkCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IClock _clock;

    public RejectChildLinkCommandHandler(IParentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ChildLinkResponse>> Handle(RejectChildLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkNotFound);
        }

        link.Reject(command.RejectedByUserId, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
}

public sealed class RevokeChildLinkCommandHandler : ICommandHandler<RevokeChildLinkCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IClock _clock;

    public RevokeChildLinkCommandHandler(IParentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ChildLinkResponse>> Handle(RevokeChildLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkNotFound);
        }

        link.Revoke(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
}

// ---------------------------------------------------------------------------
// Query handlers
// ---------------------------------------------------------------------------

public sealed class GetParentProfileQueryHandler : IQueryHandler<GetParentProfileQuery, Result<ParentProfileResponse>>
{
    private readonly IParentRepository _repository;

    public GetParentProfileQueryHandler(IParentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ParentProfileResponse>> Handle(GetParentProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetProfileByUserIdAsync(query.UserId, cancellationToken);
        return profile is null
            ? Result<ParentProfileResponse>.Failure(ParentErrors.ProfileNotFound)
            : Result<ParentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class ListChildrenQueryHandler : IQueryHandler<ListChildrenQuery, Result<IReadOnlyCollection<ChildLinkResponse>>>
{
    private readonly IParentRepository _repository;

    public ListChildrenQueryHandler(IParentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<ChildLinkResponse>>> Handle(ListChildrenQuery query, CancellationToken cancellationToken)
    {
        var links = await _repository.ListLinksByParentAsync(query.ParentUserId, cancellationToken);
        var responses = new List<ChildLinkResponse>(links.Count);
        foreach (var link in links.OrderByDescending(item => item.RequestedOnUtc))
        {
            // Yalnızca onaylı bağlarda gelişim özeti gösterilir (salt-okunur + gizlilik: İş Kuralları 4.4).
            ChildProgressSummaryResponse? progress = null;
            if (link.IsApproved)
            {
                var snapshot = await _repository.GetSnapshotAsync(link.StudentId, cancellationToken);
                progress = snapshot.ToProgressSummary();
            }

            responses.Add(link.ToResponse(progress));
        }

        return Result<IReadOnlyCollection<ChildLinkResponse>>.Success(responses);
    }
}

public sealed class GetChildDashboardQueryHandler : IQueryHandler<GetChildDashboardQuery, Result<ChildDashboardResponse>>
{
    private readonly IParentRepository _repository;

    public GetChildDashboardQueryHandler(IParentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ChildDashboardResponse>> Handle(GetChildDashboardQuery query, CancellationToken cancellationToken)
    {
        var link = await _repository.GetActiveLinkAsync(query.ParentUserId, query.StudentId, cancellationToken);
        if (link is null || !link.IsApproved)
        {
            // Veli yalnızca onaylı (Approved) bağlı çocuğunun verisini görebilir (İş Kuralları 4.4).
            return Result<ChildDashboardResponse>.Failure(ParentErrors.LinkNotApproved);
        }

        var snapshot = await _repository.GetSnapshotAsync(query.StudentId, cancellationToken);
        return Result<ChildDashboardResponse>.Success(snapshot.ToDashboard(query.StudentId, link));
    }
}

// ---------------------------------------------------------------------------
// Errors + mappings
// ---------------------------------------------------------------------------

public static class ParentErrors
{
    public static readonly Error ProfileNotFound = new("parents.profile_not_found", "Veli profili bulunamadı.");
    public static readonly Error LinkNotFound = new("parents.link_not_found", "Veli–çocuk bağı bulunamadı.");
    public static readonly Error LinkAlreadyExists = new("parents.link_exists", "Bu çocuk için zaten aktif bir bağ talebi/onayı var.");
    public static readonly Error LinkNotApproved = new("parents.link_not_approved", "Bu çocuğun verilerine erişmek için bağın onaylı olması gerekir.");
    public static readonly Error InvalidRequest = new("parents.invalid_request", "Veli isteği bilgileri eksik veya hatalı.");
}

internal static class ParentMappings
{
    public static ParentProfileResponse ToResponse(this ParentProfile profile)
    {
        return new ParentProfileResponse(
            profile.Id,
            profile.UserId,
            profile.FullName,
            profile.ContactPhone,
            profile.ContactEmail,
            new NotificationPreferencesResponse(
                profile.NotifyMissedAssignment,
                profile.NotifyWeeklyProgressSummary,
                profile.NotifyLessonReminders,
                profile.NotifyTestResults,
                profile.NotifyPayments,
                profile.NotificationChannel.ToString()),
            profile.IsActive,
            profile.CreatedOnUtc,
            profile.UpdatedOnUtc);
    }

    public static ChildLinkResponse ToResponse(this ParentChildLink link, ChildProgressSummaryResponse? progress)
    {
        return new ChildLinkResponse(
            link.Id,
            link.ParentUserId,
            link.StudentId,
            link.ChildDisplayName,
            link.Relationship,
            link.Status.ToString(),
            link.IsPrimaryContact,
            link.RequestedOnUtc,
            link.LinkedOnUtc,
            progress);
    }

    public static ChildProgressSummaryResponse ToProgressSummary(this ChildProgressSnapshot? snapshot)
    {
        return snapshot is null
            ? new ChildProgressSummaryResponse(0, null, 0, 0)
            : new ChildProgressSummaryResponse(
                snapshot.CompletedLessonCount,
                snapshot.LastLessonCompletedAtUtc,
                snapshot.OpenAssignmentCount,
                snapshot.WeeklyStudyMinutes);
    }

    public static ChildDashboardResponse ToDashboard(this ChildProgressSnapshot? snapshot, Guid studentId, ParentChildLink link)
    {
        if (snapshot is null)
        {
            return new ChildDashboardResponse(
                studentId,
                link.ChildDisplayName,
                link.Status.ToString(),
                new StudySummaryResponse(0, 0, false),
                new LessonSummaryResponse(0, 0, null),
                new AssignmentSummaryResponse(0, 0, 0),
                new PaymentSummaryResponse("TRY", 0m, 0m, 0m, null),
                null);
        }

        var hasStudyData = snapshot.WeeklyStudyMinutes > 0 || snapshot.StudyStreakDays > 0;
        return new ChildDashboardResponse(
            studentId,
            link.ChildDisplayName,
            link.Status.ToString(),
            new StudySummaryResponse(snapshot.WeeklyStudyMinutes, snapshot.StudyStreakDays, hasStudyData),
            new LessonSummaryResponse(snapshot.CompletedLessonCount, snapshot.PlannedLessonCount, snapshot.LastLessonCompletedAtUtc),
            new AssignmentSummaryResponse(snapshot.TotalAssignmentCount, snapshot.OpenAssignmentCount, snapshot.CompletedAssignmentCount),
            new PaymentSummaryResponse(
                snapshot.Currency,
                snapshot.ExpectedPaymentTotal,
                snapshot.CollectedPaymentTotal,
                snapshot.OutstandingPaymentTotal,
                snapshot.LastPaymentUpdatedAtUtc),
            snapshot.UpdatedOnUtc);
    }
}
