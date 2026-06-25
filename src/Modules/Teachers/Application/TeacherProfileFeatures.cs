using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Teachers.Application;

public sealed record TeacherAvailabilityRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

public sealed record CreateTeacherProfileCommand(
    Guid UserId,
    string FullName,
    string Subject,
    string City,
    string District,
    string? Biography,
    string? Headline,
    TeacherLessonFormat LessonFormat,
    int ExperienceYears,
    string EducationLevel,
    decimal HourlyRateAmount,
    string Currency,
    string? ProfilePhotoUrl,
    IReadOnlyCollection<TeacherAvailabilityRequest> AvailabilitySlots) : ICommand<Result<TeacherProfileResponse>>;

public sealed record UpdateTeacherProfileCommand(
    Guid UserId,
    string FullName,
    string Subject,
    string City,
    string District,
    string? Biography,
    string? Headline,
    TeacherLessonFormat LessonFormat,
    int ExperienceYears,
    string EducationLevel,
    decimal HourlyRateAmount,
    string Currency,
    string? ProfilePhotoUrl,
    IReadOnlyCollection<TeacherAvailabilityRequest> AvailabilitySlots) : ICommand<Result<TeacherProfileResponse>>;

// IAllowAnonymous: HTTP katmanı "AuthenticatedUser" politikasıyla auth'u sağlıyor;
// dispatcher düzeyinde ek rol/sahiplik kontrolü yok (K3 — ilerleyen sürümde authorizer eklenecek).
public sealed record GetTeacherProfileByUserIdQuery(Guid UserId) : IQuery<Result<TeacherProfileResponse>>, IAllowAnonymous;

public sealed record TeacherAvailabilityResponse(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

public sealed record TeacherProfileResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Subject,
    string City,
    string District,
    string? Biography,
    string? Headline,
    string LessonFormat,
    int ExperienceYears,
    string EducationLevel,
    decimal HourlyRateAmount,
    string Currency,
    bool IsVerified,
    string? ProfilePhotoUrl,
    IReadOnlyCollection<TeacherAvailabilityResponse> AvailabilitySlots,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public interface ITeacherProfileRepository
{
    Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(TeacherProfile profile, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateTeacherProfileCommandHandler : ICommandHandler<CreateTeacherProfileCommand, Result<TeacherProfileResponse>>
{
    private static readonly Error DuplicateProfile = new("teachers.profile_exists", "Bu kullanıcı için öğretmen profili zaten oluşturulmuş.");
    private static readonly Error InvalidAvailability = new("teachers.invalid_availability", "Uygunluk saatleri geçersiz.");
    private readonly ITeacherProfileRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateTeacherProfileCommandHandler(ITeacherProfileRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TeacherProfileResponse>> Handle(CreateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        var existingProfile = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (existingProfile is not null)
        {
            return Result<TeacherProfileResponse>.Failure(DuplicateProfile);
        }

        var slotsResult = BuildAvailabilitySlots(Guid.Empty, command.AvailabilitySlots, _idGenerator);
        if (slotsResult.IsFailure)
        {
            return Result<TeacherProfileResponse>.Failure(slotsResult.Error);
        }

        var now = _clock.UtcNow;
        var profileId = _idGenerator.New();
        var slots = slotsResult.Value!.Select(slot => new TeacherAvailabilitySlot(
            _idGenerator.New(),
            profileId,
            slot.DayOfWeek,
            slot.StartTime,
            slot.EndTime,
            slot.IsOnlineAvailable,
            slot.IsInPersonAvailable)).ToArray();

        var profile = new TeacherProfile(
            profileId,
            command.UserId,
            command.FullName.Trim(),
            command.Subject.Trim(),
            command.City.Trim(),
            command.District.Trim(),
            command.Biography?.Trim(),
            command.Headline?.Trim(),
            command.LessonFormat,
            command.ExperienceYears,
            command.EducationLevel.Trim(),
            command.HourlyRateAmount,
            command.Currency.Trim().ToUpperInvariant(),
            false,
            command.ProfilePhotoUrl?.Trim(),
            now);

        profile.AvailabilitySlots.AddRange(slots);

        await _repository.AddAsync(profile, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }

    private static Result<IReadOnlyCollection<TeacherAvailabilityRequest>> BuildAvailabilitySlots(
        Guid teacherProfileId,
        IReadOnlyCollection<TeacherAvailabilityRequest> requests,
        IIdGenerator idGenerator)
    {
        foreach (var request in requests)
        {
            if (request.EndTime <= request.StartTime)
            {
                return Result<IReadOnlyCollection<TeacherAvailabilityRequest>>.Failure(InvalidAvailability);
            }
        }

        return Result<IReadOnlyCollection<TeacherAvailabilityRequest>>.Success(requests);
    }
}

public sealed class UpdateTeacherProfileCommandHandler : ICommandHandler<UpdateTeacherProfileCommand, Result<TeacherProfileResponse>>
{
    private static readonly Error NotFound = new("teachers.profile_not_found", "Öğretmen profili bulunamadı.");
    private static readonly Error InvalidAvailability = new("teachers.invalid_availability", "Uygunluk saatleri geçersiz.");
    private readonly ITeacherProfileRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public UpdateTeacherProfileCommandHandler(ITeacherProfileRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TeacherProfileResponse>> Handle(UpdateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is null)
        {
            return Result<TeacherProfileResponse>.Failure(NotFound);
        }

        foreach (var request in command.AvailabilitySlots)
        {
            if (request.EndTime <= request.StartTime)
            {
                return Result<TeacherProfileResponse>.Failure(InvalidAvailability);
            }
        }

        var slots = command.AvailabilitySlots.Select(request => new TeacherAvailabilitySlot(
            _idGenerator.New(),
            profile.Id,
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            request.IsOnlineAvailable,
            request.IsInPersonAvailable)).ToArray();

        profile.Update(
            command.FullName.Trim(),
            command.Subject.Trim(),
            command.City.Trim(),
            command.District.Trim(),
            command.Biography?.Trim(),
            command.Headline?.Trim(),
            command.LessonFormat,
            command.ExperienceYears,
            command.EducationLevel.Trim(),
            command.HourlyRateAmount,
            command.Currency.Trim().ToUpperInvariant(),
            command.ProfilePhotoUrl?.Trim(),
            slots,
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }
}

public sealed class GetTeacherProfileByUserIdQueryHandler : IQueryHandler<GetTeacherProfileByUserIdQuery, Result<TeacherProfileResponse>>
{
    private static readonly Error NotFound = new("teachers.profile_not_found", "Öğretmen profili bulunamadı.");
    private readonly ITeacherProfileRepository _repository;

    public GetTeacherProfileByUserIdQueryHandler(ITeacherProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TeacherProfileResponse>> Handle(GetTeacherProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(query.UserId, cancellationToken);
        return profile is null
            ? Result<TeacherProfileResponse>.Failure(NotFound)
            : Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }
}

internal static class TeacherProfileMappings
{
    public static TeacherProfileResponse ToResponse(this TeacherProfile profile)
    {
        return new TeacherProfileResponse(
            profile.Id,
            profile.UserId,
            profile.FullName,
            profile.Subject,
            profile.City,
            profile.District,
            profile.Biography,
            profile.Headline,
            profile.LessonFormat.ToString(),
            profile.ExperienceYears,
            profile.EducationLevel,
            profile.HourlyRateAmount,
            profile.Currency,
            profile.IsVerified,
            profile.ProfilePhotoUrl,
            profile.AvailabilitySlots
                .OrderBy(slot => slot.DayOfWeek)
                .ThenBy(slot => slot.StartTime)
                .Select(slot => new TeacherAvailabilityResponse(
                    slot.DayOfWeek,
                    slot.StartTime,
                    slot.EndTime,
                    slot.IsOnlineAvailable,
                    slot.IsInPersonAvailable))
                .ToArray(),
            profile.CreatedOnUtc,
            profile.UpdatedOnUtc);
    }
}
