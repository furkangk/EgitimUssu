using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

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
    IReadOnlyCollection<StudentSubjectRequest> Subjects) : ICommand<Result<StudentProfileResponse>>;

public sealed record GetStudentProfileByIdQuery(Guid StudentId) : IQuery<Result<StudentProfileResponse>>;

public sealed record GetStudentProfileByUserIdQuery(Guid UserId) : IQuery<Result<StudentProfileResponse>>;

public sealed record ListStudentsByTeacherQuery(Guid TeacherUserId) : IQuery<Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>;

public sealed record UpdateStudentProfileCommand(
    Guid StudentId,
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    bool IsActive,
    IReadOnlyCollection<StudentSubjectRequest> Subjects) : ICommand<Result<StudentProfileResponse>>;

public sealed record StudentSubjectResponse(string Subject, string? TargetLevel);

public sealed record StudentProfileSummaryResponse(
    Guid Id,
    string FullName,
    string GradeLevel,
    string Origin,
    bool IsActive,
    DateTime CreatedOnUtc);

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
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public interface IStudentProfileRepository
{
    Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken);

    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task AddAsync(StudentProfile profile, CancellationToken cancellationToken);

    Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateStudentProfileCommandHandler : ICommandHandler<CreateStudentProfileCommand, Result<StudentProfileResponse>>
{
    private static readonly Error InvalidOrigin = new("students.invalid_origin", "Öğrenci profili kaynağı ile kimlik bilgileri uyumsuz.");
    private static readonly Error DuplicateUserProfile = new("students.user_profile_exists", "Bu kullanıcı için öğrenci profili zaten var.");
    private readonly IStudentProfileRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateStudentProfileCommandHandler(IStudentProfileRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
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
            now);

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

        return Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class GetStudentProfileByIdQueryHandler : IQueryHandler<GetStudentProfileByIdQuery, Result<StudentProfileResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;

    public GetStudentProfileByIdQueryHandler(IStudentProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudentProfileResponse>> Handle(GetStudentProfileByIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(query.StudentId, cancellationToken);
        return profile is null
            ? Result<StudentProfileResponse>.Failure(NotFound)
            : Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class GetStudentProfileByUserIdQueryHandler : IQueryHandler<GetStudentProfileByUserIdQuery, Result<StudentProfileResponse>>
{
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly IStudentProfileRepository _repository;

    public GetStudentProfileByUserIdQueryHandler(IStudentProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudentProfileResponse>> Handle(GetStudentProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(query.UserId, cancellationToken);
        return profile is null
            ? Result<StudentProfileResponse>.Failure(NotFound)
            : Result<StudentProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class ListStudentsByTeacherQueryHandler : IQueryHandler<ListStudentsByTeacherQuery, Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>
{
    private readonly IStudentProfileRepository _repository;

    public ListStudentsByTeacherQueryHandler(IStudentProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<StudentProfileSummaryResponse>>> Handle(ListStudentsByTeacherQuery query, CancellationToken cancellationToken)
    {
        var students = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var payload = students
            .OrderBy(student => student.FullName)
            .Select(student => new StudentProfileSummaryResponse(
                student.Id,
                student.FullName,
                student.GradeLevel,
                student.Origin.ToString(),
                student.IsActive,
                student.CreatedOnUtc))
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
            _clock.UtcNow);

        var newSubjects = command.Subjects
            .Where(s => !string.IsNullOrWhiteSpace(s.Subject))
            .Select(s => new StudentSubject(_idGenerator.New(), profile.Id, s.Subject.Trim(), s.TargetLevel?.Trim()))
            .ToList();

        await _repository.ReplaceSubjectsAsync(profile.Id, newSubjects, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudentProfileResponse>.Success(profile.ToResponseWithSubjects(newSubjects));
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
            subjects.Select(subject => new StudentSubjectResponse(subject.Subject, subject.TargetLevel)).ToArray(),
            profile.CreatedOnUtc,
            profile.UpdatedOnUtc);
    }
}
