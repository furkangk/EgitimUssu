using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

internal static class StudentLimits
{
    public const int FreeStudentLimit = 5; // TODO(M17): premium sınırsız
}

public sealed record StudentSubjectRequest(string Subject, string? TargetLevel);

public sealed record CreateStudentProfileCommand(
    Guid? UserId,
    Guid? CreatedByTeacherUserId,
    Guid? ParentUserId,
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    StudentOrigin Origin,
    IReadOnlyCollection<StudentSubjectRequest> Subjects,
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null) : ICommand<Result<StudentProfileResponse>>;

public sealed record GetStudentProfileByIdQuery(Guid StudentId) : IQuery<Result<StudentProfileResponse>>;

public sealed record GetStudentProfileByUserIdQuery(Guid UserId) : IQuery<Result<StudentProfileResponse>>;

public sealed record ListStudentsByTeacherQuery(Guid TeacherUserId, bool IncludeArchived = false) : IQuery<Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>;

public sealed record UpdateStudentProfileCommand(
    Guid StudentId,
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    bool IsActive,
    IReadOnlyCollection<StudentSubjectRequest> Subjects,
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null) : ICommand<Result<StudentProfileResponse>>;

/// <summary>M03-2: Profili baştan güncellemeden tek branş ekler.</summary>
public sealed record AddStudentSubjectCommand(Guid StudentId, string Subject, string? TargetLevel)
    : ICommand<Result<StudentSubjectResponse>>;

/// <summary>M03-2: Tek branş siler.</summary>
public sealed record RemoveStudentSubjectCommand(Guid StudentId, Guid SubjectId) : ICommand<Result>;

/// <summary>M03-1: Mevcut bir veli kullanıcısını öğrenciye bağlama talebi (bağ Pending başlar).</summary>
public sealed record LinkStudentParentCommand(Guid StudentId, Guid ParentUserId)
    : ICommand<Result<StudentParentLinkResponse>>;

/// <summary>M03-3: Velinin onaylı çocuklarını listeler.</summary>
public sealed record ListStudentsByParentQuery(Guid ParentUserId)
    : IQuery<Result<IReadOnlyCollection<ParentChildSummaryResponse>>>;

/// <summary>Veli bağlama talebinin sonucu. <c>Status</c> her zaman <c>Pending</c>'dir — onay Parents'ta verilir.</summary>
public sealed record StudentParentLinkResponse(Guid StudentId, Guid ParentUserId, string Status);

/// <summary>Velinin çocuk listesi için hafif görünüm (öğretmen bağı alanlarını taşımaz).</summary>
public sealed record ParentChildSummaryResponse(
    Guid Id,
    string FullName,
    string GradeLevel,
    string TargetExam,
    bool IsActive,
    DateTime CreatedOnUtc);

public sealed record StudentSubjectResponse(Guid Id, string Subject, string? TargetLevel);

public sealed record StudentProfileSummaryResponse(
    Guid Id,
    string FullName,
    string GradeLevel,
    string Origin,
    bool IsActive,
    DateTime CreatedOnUtc,
    bool IsArchived,
    decimal? AgreedRateAmount,
    string LinkStatus);

public sealed record StudentProfileResponse(
    Guid Id,
    Guid? UserId,
    Guid? CreatedByTeacherUserId,
    Guid? ParentUserId,
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    string Origin,
    bool IsActive,
    string TargetExam,
    DateTime? DateOfBirth,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public interface IStudentProfileRepository
{
    Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken);

    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task AddAsync(StudentProfile profile, CancellationToken cancellationToken);

    Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateStudentProfileCommandHandler : ICommandHandler<CreateStudentProfileCommand, Result<StudentProfileResponse>>
{
    private static readonly Error InvalidOrigin = new("students.invalid_origin", "Öğrenci profili kaynağı ile kimlik bilgileri uyumsuz.");
    private static readonly Error DuplicateUserProfile = new("students.user_profile_exists", "Bu kullanıcı için öğrenci profili zaten var.");
    private static readonly Error FreeLimitReached = new("students.free_limit_reached", "Free planda en fazla 5 ogrenci ekleyebilirsiniz. Premium'a gecin.");
    private readonly IStudentProfileRepository _repository;
    private readonly ITeacherStudentLinkRepository _linkRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateStudentProfileCommandHandler(
        IStudentProfileRepository repository,
        ITeacherStudentLinkRepository linkRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _linkRepository = linkRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudentProfileResponse>> Handle(CreateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        if (command.Origin == StudentOrigin.TeacherManaged && command.CreatedByTeacherUserId is null)
        {
            return Result<StudentProfileResponse>.Failure(InvalidOrigin);
        }

        if (command.Origin == StudentOrigin.SelfRegistered && command.UserId is null)
        {
            return Result<StudentProfileResponse>.Failure(InvalidOrigin);
        }

        if (command.UserId.HasValue)
        {
            var existingProfile = await _repository.GetByUserIdAsync(command.UserId.Value, cancellationToken);
            if (existingProfile is not null)
            {
                return Result<StudentProfileResponse>.Failure(DuplicateUserProfile);
            }
        }

        if (command.Origin == StudentOrigin.TeacherManaged && command.CreatedByTeacherUserId is { } teacherId)
        {
            var count = await _linkRepository.CountByTeacherAsync(teacherId, cancellationToken);
            if (count >= StudentLimits.FreeStudentLimit)
            {
                return Result<StudentProfileResponse>.Failure(FreeLimitReached);
            }
        }

        var now = _clock.UtcNow;
        var profileId = _idGenerator.New();
        var profile = new StudentProfile(
            profileId,
            command.UserId,
            command.CreatedByTeacherUserId,
            command.ParentUserId,
            command.FullName.Trim(),
            command.GradeLevel.Trim(),
            command.ContactEmail?.Trim(),
            command.ContactPhone?.Trim(),
            command.GoalSummary?.Trim(),
            command.LevelNotes?.Trim(),
            command.Origin,
            true,
            now,
            command.TargetExam,
            command.DateOfBirth);

        foreach (var subject in command.Subjects.Where(subject => !string.IsNullOrWhiteSpace(subject.Subject)))
        {
            profile.Subjects.Add(new StudentSubject(
                _idGenerator.New(),
                profileId,
                subject.Subject.Trim(),
                subject.TargetLevel?.Trim()));
        }

        await _repository.AddAsync(profile, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        if (command.Origin == StudentOrigin.TeacherManaged && command.CreatedByTeacherUserId is { } linkTeacherId)
        {
            var link = new TeacherStudentLink(_idGenerator.New(), linkTeacherId, profile.Id, TeacherStudentLinkStatus.Manual, now);
            await _linkRepository.AddAsync(link, cancellationToken);
            await _linkRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class GetStudentProfileByIdQueryHandler : IQueryHandler<GetStudentProfileByIdQuery, Result<StudentProfileResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;
    private readonly IRequestEntityCache _entityCache;

    public GetStudentProfileByIdQueryHandler(IStudentProfileRepository repository, IRequestEntityCache entityCache)
    {
        _repository = repository;
        _entityCache = entityCache;
    }

    /// <summary>O2: Authorizer profili yüklediyse istek kapsamından okur; yalnız yoksa DB'ye gider.</summary>
    public async Task<Result<StudentProfileResponse>> Handle(GetStudentProfileByIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ById(query.StudentId),
            ct => _repository.GetByIdAsync(query.StudentId, ct),
            cancellationToken);

        return profile is null
            ? Result<StudentProfileResponse>.Failure(NotFound)
            : Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class GetStudentProfileByUserIdQueryHandler : IQueryHandler<GetStudentProfileByUserIdQuery, Result<StudentProfileResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;
    private readonly IRequestEntityCache _entityCache;

    public GetStudentProfileByUserIdQueryHandler(IStudentProfileRepository repository, IRequestEntityCache entityCache)
    {
        _repository = repository;
        _entityCache = entityCache;
    }

    /// <summary>O2: Authorizer profili yüklediyse istek kapsamından okur; yalnız yoksa DB'ye gider.</summary>
    public async Task<Result<StudentProfileResponse>> Handle(GetStudentProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ByUserId(query.UserId),
            ct => _repository.GetByUserIdAsync(query.UserId, ct),
            cancellationToken);

        return profile is null
            ? Result<StudentProfileResponse>.Failure(NotFound)
            : Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class ListStudentsByTeacherQueryHandler : IQueryHandler<ListStudentsByTeacherQuery, Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>
{
    private readonly IStudentProfileRepository _repository;
    private readonly ITeacherStudentLinkRepository _linkRepository;

    public ListStudentsByTeacherQueryHandler(IStudentProfileRepository repository, ITeacherStudentLinkRepository linkRepository)
    {
        _repository = repository;
        _linkRepository = linkRepository;
    }

    public async Task<Result<IReadOnlyCollection<StudentProfileSummaryResponse>>> Handle(ListStudentsByTeacherQuery query, CancellationToken cancellationToken)
    {
        var links = await _linkRepository.ListByTeacherAsync(query.TeacherUserId, query.IncludeArchived, cancellationToken);
        var studentIds = links.Select(link => link.StudentId).ToArray();
        var profiles = await _repository.ListByIdsAsync(studentIds, cancellationToken);
        var profilesById = profiles.ToDictionary(profile => profile.Id);

        var payload = links
            .Where(link => profilesById.ContainsKey(link.StudentId))
            .Select(link =>
            {
                var profile = profilesById[link.StudentId];
                return new StudentProfileSummaryResponse(
                    profile.Id,
                    profile.FullName,
                    profile.GradeLevel,
                    profile.Origin.ToString(),
                    profile.IsActive,
                    profile.CreatedOnUtc,
                    link.IsArchived,
                    link.AgreedRateAmount,
                    link.Status.ToString());
            })
            .OrderBy(summary => summary.FullName)
            .ToArray();

        return Result<IReadOnlyCollection<StudentProfileSummaryResponse>>.Success(payload);
    }
}

public sealed class UpdateStudentProfileCommandHandler : ICommandHandler<UpdateStudentProfileCommand, Result<StudentProfileResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public UpdateStudentProfileCommandHandler(IStudentProfileRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudentProfileResponse>> Handle(UpdateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result<StudentProfileResponse>.Failure(NotFound);
        }

        profile.Update(
            command.FullName,
            command.GradeLevel,
            command.ContactEmail,
            command.ContactPhone,
            command.GoalSummary,
            command.LevelNotes,
            command.IsActive,
            _clock.UtcNow,
            command.TargetExam,
            command.DateOfBirth);

        var newSubjects = command.Subjects
            .Where(s => !string.IsNullOrWhiteSpace(s.Subject))
            .Select(s => new StudentSubject(_idGenerator.New(), profile.Id, s.Subject.Trim(), s.TargetLevel?.Trim()))
            .ToList();

        await _repository.ReplaceSubjectsAsync(profile.Id, newSubjects, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudentProfileResponse>.Success(profile.ToResponseWithSubjects(newSubjects));
    }
}

public sealed class AddStudentSubjectCommandHandler : ICommandHandler<AddStudentSubjectCommand, Result<StudentSubjectResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public AddStudentSubjectCommandHandler(IStudentProfileRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudentSubjectResponse>> Handle(AddStudentSubjectCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result<StudentSubjectResponse>.Failure(NotFound);
        }

        var added = profile.AddSubject(_idGenerator.New(), command.Subject, command.TargetLevel, _clock.UtcNow);
        if (added.IsFailure)
        {
            return Result<StudentSubjectResponse>.Failure(added.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        var subject = added.Value!;
        return Result<StudentSubjectResponse>.Success(
            new StudentSubjectResponse(subject.Id, subject.Subject, subject.TargetLevel));
    }
}

public sealed class RemoveStudentSubjectCommandHandler : ICommandHandler<RemoveStudentSubjectCommand, Result>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;
    private readonly IClock _clock;

    public RemoveStudentSubjectCommandHandler(IStudentProfileRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(RemoveStudentSubjectCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(NotFound);
        }

        var removed = profile.RemoveSubject(command.SubjectId, _clock.UtcNow);
        if (removed.IsFailure)
        {
            return removed;
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// M03-1: Öğretmen mevcut bir veli kullanıcısını öğrenciye bağlar. Bağın kendisi <b>Parents</b>
/// modülünde yaşar; burada yalnız onay bekleyen talep açılır. <c>StudentProfile.ParentUserId</c>
/// bu adımda doldurulmaz — onay sonrası <c>ParentChildLinkApproved</c> olayıyla dolar.
/// </summary>
public sealed class LinkStudentParentCommandHandler : ICommandHandler<LinkStudentParentCommand, Result<StudentParentLinkResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private static readonly Error ParentUserRequired = new("students.parent_user_required", "Veli kayıtlı bir kullanıcı olmalıdır.");
    private static readonly Error LinkExists = new("students.parent_link_exists", "Bu veli zaten bu öğrenciye bağlı veya onay bekliyor.");
    private readonly IStudentProfileRepository _repository;
    private readonly IParentChildLinkRequestService _linkRequestService;

    public LinkStudentParentCommandHandler(
        IStudentProfileRepository repository,
        IParentChildLinkRequestService linkRequestService)
    {
        _repository = repository;
        _linkRequestService = linkRequestService;
    }

    public async Task<Result<StudentParentLinkResponse>> Handle(LinkStudentParentCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result<StudentParentLinkResponse>.Failure(NotFound);
        }

        if (command.ParentUserId == Guid.Empty)
        {
            return Result<StudentParentLinkResponse>.Failure(ParentUserRequired);
        }

        var outcome = await _linkRequestService.RequestPendingLinkAsync(
            command.ParentUserId,
            profile.Id,
            profile.FullName,
            cancellationToken);

        return outcome switch
        {
            ParentLinkRequestOutcome.Created => Result<StudentParentLinkResponse>.Success(
                new StudentParentLinkResponse(profile.Id, command.ParentUserId, "Pending")),
            ParentLinkRequestOutcome.AlreadyLinked => Result<StudentParentLinkResponse>.Failure(LinkExists),
            _ => Result<StudentParentLinkResponse>.Failure(ParentUserRequired)
        };
    }
}

/// <summary>M03-3: Velinin <b>onaylı</b> çocuklarını listeler; onay bilgisi Parents'tan okunur.</summary>
public sealed class ListStudentsByParentQueryHandler : IQueryHandler<ListStudentsByParentQuery, Result<IReadOnlyCollection<ParentChildSummaryResponse>>>
{
    private readonly IStudentProfileRepository _repository;
    private readonly IParentAccessDirectory _parentAccess;

    public ListStudentsByParentQueryHandler(IStudentProfileRepository repository, IParentAccessDirectory parentAccess)
    {
        _repository = repository;
        _parentAccess = parentAccess;
    }

    public async Task<Result<IReadOnlyCollection<ParentChildSummaryResponse>>> Handle(ListStudentsByParentQuery query, CancellationToken cancellationToken)
    {
        var studentIds = await _parentAccess.ListApprovedStudentIdsAsync(query.ParentUserId, cancellationToken);
        if (studentIds.Count == 0)
        {
            return Result<IReadOnlyCollection<ParentChildSummaryResponse>>.Success([]);
        }

        var profiles = await _repository.ListByIdsAsync(studentIds, cancellationToken);
        var payload = profiles
            .OrderBy(profile => profile.FullName)
            .Select(profile => new ParentChildSummaryResponse(
                profile.Id,
                profile.FullName,
                profile.GradeLevel,
                profile.TargetExam.ToString(),
                profile.IsActive,
                profile.CreatedOnUtc))
            .ToArray();

        return Result<IReadOnlyCollection<ParentChildSummaryResponse>>.Success(payload);
    }
}

internal static class StudentProfileMappings
{
    public static StudentProfileResponse ToResponse(this StudentProfile profile)
    {
        return profile.ToResponseWithSubjects(profile.Subjects);
    }

    public static StudentProfileResponse ToResponseWithSubjects(this StudentProfile profile, IEnumerable<StudentSubject> subjects)
    {
        return new StudentProfileResponse(
            profile.Id,
            profile.UserId,
            profile.CreatedByTeacherUserId,
            profile.ParentUserId,
            profile.FullName,
            profile.GradeLevel,
            profile.ContactEmail,
            profile.ContactPhone,
            profile.GoalSummary,
            profile.LevelNotes,
            profile.Origin.ToString(),
            profile.IsActive,
            profile.TargetExam.ToString(),
            profile.DateOfBirth,
            subjects.Select(subject => new StudentSubjectResponse(subject.Id, subject.Subject, subject.TargetLevel)).ToArray(),
            profile.CreatedOnUtc,
            profile.UpdatedOnUtc);
    }
}
