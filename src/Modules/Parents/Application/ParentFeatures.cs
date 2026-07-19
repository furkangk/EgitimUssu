using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
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

public sealed record ClaimParentInviteCommand(Guid ParentUserId, string InviteCode) : ICommand<Result<ChildLinkResponse>>;

public sealed record SetParentMembershipTierCommand(Guid ParentUserId, MembershipTier Tier) : ICommand<Result<ParentProfileResponse>>;

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
    string MembershipTier,
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
    IReadOnlyCollection<UpcomingLesson> UpcomingLessons,
    LastLessonSummary? LastLesson,
    DateTime? UpdatedOnUtc);

public sealed record StudySummaryResponse(
    int WeeklyStudyMinutes,
    int StreakDays,
    bool HasData,
    bool IsShared,
    IReadOnlyCollection<StudySubjectMinutes> SubjectBreakdown);

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

    Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken cancellationToken);

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

        var approvedLinks = await _repository.ListApprovedLinksForStudentAsync(link.StudentId, cancellationToken);
        var existingPrimary = approvedLinks.FirstOrDefault(l => l.IsPrimaryContact && l.Id != link.Id);

        // Birincil-veli tekilliği: bu bağ birincil olacaksa ve zaten bir birincil veli varsa,
        // onaylayan kişi mevcut birincil veli değilse reddet (Admin authorizer'da zaten geçebilir).
        if (link.IsPrimaryContact && existingPrimary is not null && existingPrimary.ParentUserId != command.ApprovedByUserId)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.PrimaryExists);
        }

        link.Approve(command.ApprovedByUserId, existingPrimary?.ParentUserId, _clock.UtcNow);
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

public sealed class SetParentMembershipTierCommandHandler : ICommandHandler<SetParentMembershipTierCommand, Result<ParentProfileResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IClock _clock;

    public SetParentMembershipTierCommandHandler(IParentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ParentProfileResponse>> Handle(SetParentMembershipTierCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetProfileByUserIdAsync(command.ParentUserId, cancellationToken);
        if (profile is null)
        {
            return Result<ParentProfileResponse>.Failure(ParentErrors.ProfileNotFound);
        }

        profile.SetMembershipTier(command.Tier, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ParentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class ClaimParentInviteCommandHandler : ICommandHandler<ClaimParentInviteCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IParentInviteDirectory _inviteDirectory;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public ClaimParentInviteCommandHandler(IParentRepository repository, IParentInviteDirectory inviteDirectory, IClock clock, IIdGenerator idGenerator)
    {
        _repository = repository;
        _inviteDirectory = inviteDirectory;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<Result<ChildLinkResponse>> Handle(ClaimParentInviteCommand command, CancellationToken cancellationToken)
    {
        var info = await _inviteDirectory.ResolveAsync(command.InviteCode.Trim(), cancellationToken);
        if (info is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.InviteNotFound);
        }

        var existing = await _repository.GetActiveLinkAsync(command.ParentUserId, info.StudentId, cancellationToken);
        if (existing is not null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkAlreadyExists);
        }

        var now = _clock.UtcNow;
        // Bu çocuğa halihazırda birincil veli var mı? (V-C birincil tekilliği)
        var approved = await _repository.ListApprovedLinksForStudentAsync(info.StudentId, cancellationToken);
        var existingPrimary = approved.FirstOrDefault(l => l.IsPrimaryContact);
        var isPrimary = existingPrimary is null; // ilk veli birincil olur; ikinci veli birincil olmaz

        var link = new ParentChildLink(_idGenerator.New(), command.ParentUserId, info.StudentId, info.ChildDisplayName, null, command.InviteCode.Trim(), isPrimary, now);
        await _repository.AddLinkAsync(link, cancellationToken);
        // Öğretmen kodu = öğretmen onayı; veli kodu girdi = veli onayı → doğrudan Approved.
        link.Approve(command.ParentUserId, existingPrimary?.ParentUserId, now);
        await _repository.SaveChangesAsync(cancellationToken);
        await _inviteDirectory.MarkClaimedAsync(info.InviteId, command.ParentUserId, cancellationToken);

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
    private const int UpcomingTake = 5;
    private static readonly IReadOnlyCollection<StudySubjectMinutes> NoSubjects = Array.Empty<StudySubjectMinutes>();
    private readonly IParentRepository _repository;
    private readonly IStudentPrivacyDirectory _privacy;
    private readonly IStudyDigestDirectory _studyDigest;
    private readonly IStudentUpcomingLessonsDirectory _upcomingLessons;
    private readonly IStudentLastLessonDirectory _lastLesson;
    private readonly IClock _clock;

    public GetChildDashboardQueryHandler(
        IParentRepository repository,
        IStudentPrivacyDirectory privacy,
        IStudyDigestDirectory studyDigest,
        IStudentUpcomingLessonsDirectory upcomingLessons,
        IStudentLastLessonDirectory lastLesson,
        IClock clock)
    {
        _repository = repository;
        _privacy = privacy;
        _studyDigest = studyDigest;
        _upcomingLessons = upcomingLessons;
        _lastLesson = lastLesson;
        _clock = clock;
    }

    public async Task<Result<ChildDashboardResponse>> Handle(GetChildDashboardQuery query, CancellationToken cancellationToken)
    {
        var link = await _repository.GetActiveLinkAsync(query.ParentUserId, query.StudentId, cancellationToken);
        if (link is null || !link.IsApproved)
        {
            // Veli yalnızca onaylı (Approved) bağlı çocuğunun verisini görebilir (İş Kuralları 4.4).
            return Result<ChildDashboardResponse>.Failure(ParentErrors.LinkNotApproved);
        }

        // Gizlilik: öğrenci çalışma verisini veli ile paylaşmıyorsa çalışma alanları maskelenir (Veli V-B).
        // Ayar kaydı yoksa (KnownStudent/UserId çözülemezse) paylaşım açık varsayılır.
        var isStudyShared = true;
        var known = await _repository.GetKnownStudentAsync(query.StudentId, cancellationToken);
        if (known?.UserId is { } studentUserId)
        {
            var privacy = await _privacy.GetForUserAsync(studentUserId, cancellationToken);
            isStudyShared = privacy.ShareStudyDataWithParent;
        }

        // Çalışma verisi canlı digest'ten gelir (Veli V-F): ChildProgressSnapshot'taki WeeklyStudyMinutes/StudyStreakDays
        // hiç yazılmıyordu (bug). Paylaşım kapalıysa digest hiç çağrılmaz; maskeli 0/boş döner (V-B davranışı).
        StudySummaryResponse study;
        if (isStudyShared)
        {
            var digest = await _studyDigest.GetWeeklyDigestAsync(query.StudentId, _clock.UtcNow, cancellationToken);
            var hasData = digest.WeeklyStudyMinutes > 0 || digest.StreakDays > 0 || digest.SubjectBreakdown.Count > 0;
            study = new StudySummaryResponse(digest.WeeklyStudyMinutes, digest.StreakDays, hasData, true, digest.SubjectBreakdown);
        }
        else
        {
            study = new StudySummaryResponse(0, 0, false, false, NoSubjects);
        }

        // Ders verisi (öğretmen bağlı): yaklaşan dersler + son tamamlanan ders özeti — canlı okuma (Veli V-F).
        var upcoming = await _upcomingLessons.GetUpcomingAsync(query.StudentId, _clock.UtcNow, UpcomingTake, cancellationToken);
        var lastLesson = await _lastLesson.GetLastCompletedAsync(query.StudentId, cancellationToken);

        var snapshot = await _repository.GetSnapshotAsync(query.StudentId, cancellationToken);
        return Result<ChildDashboardResponse>.Success(snapshot.ToDashboard(query.StudentId, link, study, upcoming, lastLesson));
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
    public static readonly Error PrimaryExists = new("parents.primary_exists", "Bu çocuğun zaten bir birincil velisi var; birincil bağ için mevcut birincil velinin (veya yöneticinin) onayı gerekir.");
    public static readonly Error InviteNotFound = new("parents.invite_not_found", "Davet kodu bulunamadı veya kullanılmış.");
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
            profile.MembershipTier.ToString(),
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

    public static ChildDashboardResponse ToDashboard(
        this ChildProgressSnapshot? snapshot,
        Guid studentId,
        ParentChildLink link,
        StudySummaryResponse study,
        IReadOnlyCollection<UpcomingLesson> upcomingLessons,
        LastLessonSummary? lastLesson)
    {
        if (snapshot is null)
        {
            return new ChildDashboardResponse(
                studentId,
                link.ChildDisplayName,
                link.Status.ToString(),
                study,
                new LessonSummaryResponse(0, 0, null),
                new AssignmentSummaryResponse(0, 0, 0),
                new PaymentSummaryResponse("TRY", 0m, 0m, 0m, null),
                upcomingLessons,
                lastLesson,
                null);
        }

        return new ChildDashboardResponse(
            studentId,
            link.ChildDisplayName,
            link.Status.ToString(),
            study,
            new LessonSummaryResponse(snapshot.CompletedLessonCount, snapshot.PlannedLessonCount, snapshot.LastLessonCompletedAtUtc),
            new AssignmentSummaryResponse(snapshot.TotalAssignmentCount, snapshot.OpenAssignmentCount, snapshot.CompletedAssignmentCount),
            new PaymentSummaryResponse(
                snapshot.Currency,
                snapshot.ExpectedPaymentTotal,
                snapshot.CollectedPaymentTotal,
                snapshot.OutstandingPaymentTotal,
                snapshot.LastPaymentUpdatedAtUtc),
            upcomingLessons,
            lastLesson,
            snapshot.UpdatedOnUtc);
    }
}
