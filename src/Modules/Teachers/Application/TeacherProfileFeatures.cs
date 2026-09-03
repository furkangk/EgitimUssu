using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Modules.Teachers.Application;

public sealed record TeacherAvailabilityRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

public sealed record TeacherCertificateRequest(
    string Title,
    string? Institution,
    int? Year,
    string? FileUrl);

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
    IReadOnlyCollection<TeacherAvailabilityRequest> AvailabilitySlots,
    IReadOnlyCollection<string> Subjects,
    IReadOnlyCollection<TeacherCertificateRequest> Certificates) : ICommand<Result<TeacherProfileResponse>>;

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
    IReadOnlyCollection<TeacherAvailabilityRequest> AvailabilitySlots,
    IReadOnlyCollection<string> Subjects,
    IReadOnlyCollection<TeacherCertificateRequest> Certificates) : ICommand<Result<TeacherProfileResponse>>;

public sealed record GetTeacherProfileByUserIdQuery(Guid UserId) : IQuery<Result<TeacherProfileResponse>>;

/// <summary>M02-1: Öğretmenin doğrulama rozetini verir/geri alır (yalnız Admin).</summary>
/// <param name="UserId">Öğretmenin kullanıcı kimliği.</param>
/// <param name="IsVerified">Yeni doğrulama durumu.</param>
public sealed record SetTeacherVerificationCommand(Guid UserId, bool IsVerified)
    : ICommand<Result<TeacherProfileResponse>>;

// ---- P04 (M02-2/D-05): Profil fotografi ----

/// <summary>
/// Profil fotografi yukleme. Dosya <see cref="Content"/> ile tasinir; handler dogrular, depolar ve
/// anahtari profile baglar. <see cref="SizeBytes"/> ayrica tasinir — akis ileri-sarilamayabilir.
/// </summary>
public sealed record UploadTeacherPhotoCommand(
    Guid UserId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content) : ICommand<Result<TeacherPhotoResponse>>;

/// <summary>Yanit ic anahtari degil, yetkili indirme rotasini tasir.</summary>
public sealed record TeacherPhotoResponse(string PhotoUrl);

/// <summary>Indirme icin fotografin depolama anahtarini ve MIME turunu cozer (yetki authorizer'da).</summary>
public sealed record GetTeacherPhotoQuery(Guid UserId) : IQuery<Result<TeacherPhotoFileResponse>>;

public sealed record TeacherPhotoFileResponse(string StorageKey, string ContentType);

public sealed record TeacherAvailabilityResponse(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

public sealed record TeacherCertificateResponse(
    Guid Id,
    string Title,
    string? Institution,
    int? Year,
    string? FileUrl);

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
    IReadOnlyCollection<string> Subjects,
    IReadOnlyCollection<TeacherCertificateResponse> Certificates,
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
            null,
            now);

        profile.AvailabilitySlots.AddRange(slots);

        var subjectNames = command.Subjects is { Count: > 0 } ? command.Subjects : new[] { command.Subject };
        var teacherSubjects = subjectNames
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new TeacherSubject(_idGenerator.New(), profileId, s.Trim()))
            .ToArray();
        profile.Subjects.AddRange(teacherSubjects);

        var teacherCertificates = command.Certificates
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Select(c => new TeacherCertificate(_idGenerator.New(), profileId, c.Title.Trim(), c.Institution?.Trim(), c.Year, c.FileUrl?.Trim()))
            .ToArray();
        profile.Certificates.AddRange(teacherCertificates);

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

        var subjectNames = command.Subjects is { Count: > 0 } ? command.Subjects : new[] { command.Subject };
        var teacherSubjects = subjectNames
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new TeacherSubject(_idGenerator.New(), profile.Id, s.Trim()))
            .ToArray();

        var teacherCertificates = command.Certificates
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Select(c => new TeacherCertificate(_idGenerator.New(), profile.Id, c.Title.Trim(), c.Institution?.Trim(), c.Year, c.FileUrl?.Trim()))
            .ToArray();

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
            slots,
            teacherSubjects,
            teacherCertificates,
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
            // P04: istemciye iç anahtar değil, yetkili indirme rotası verilir.
            profile.ProfilePhotoKey is null ? null : $"/api/teachers/profiles/{profile.UserId}/photo",
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
            profile.Subjects
                .Select(s => s.Subject)
                .ToArray(),
            profile.Certificates
                .Select(c => new TeacherCertificateResponse(
                    c.Id,
                    c.Title,
                    c.Institution,
                    c.Year,
                    c.FileUrl))
                .ToArray(),
            profile.CreatedOnUtc,
            profile.UpdatedOnUtc);
    }
}


/// <summary>
/// P04 (M02-2): Profil fotografini ortak <see cref="IFileStorage"/> uzerinden saklar.
/// Sira onemlidir: once dogrulama ve profilin varligi, sonra yazma — yetkisiz/hatali istek
/// depoya hicbir sey birakmaz. Yeni fotograf baglaninca eskisi silinir.
/// </summary>
public sealed class UploadTeacherPhotoCommandHandler
    : ICommandHandler<UploadTeacherPhotoCommand, Result<TeacherPhotoResponse>>
{
    private static readonly Error NotFound = new("teachers.profile_not_found", "Ogretmen profili bulunamadi.");
    private static readonly Error InvalidRequest = new("teachers.invalid_request", "Yuklenecek dosya bilgisi eksik.");

    private readonly ITeacherProfileRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly IClock _clock;

    public UploadTeacherPhotoCommandHandler(
        ITeacherProfileRepository repository,
        IFileStorage fileStorage,
        IClock clock)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _clock = clock;
    }

    public async Task<Result<TeacherPhotoResponse>> Handle(
        UploadTeacherPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            return Result<TeacherPhotoResponse>.Failure(InvalidRequest);
        }

        var validation = FileValidation.Validate(command.FileName, command.SizeBytes);
        if (validation.IsFailure)
        {
            return Result<TeacherPhotoResponse>.Failure(validation.Error);
        }

        var profile = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is null)
        {
            return Result<TeacherPhotoResponse>.Failure(NotFound);
        }

        var stored = await _fileStorage.SaveAsync(
            $"teachers/{command.UserId}",
            command.FileName,
            command.ContentType,
            command.Content,
            cancellationToken);

        var previousKey = profile.SetProfilePhoto(stored.Key, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousKey))
        {
            await _fileStorage.DeleteAsync(previousKey, cancellationToken);
        }

        return Result<TeacherPhotoResponse>.Success(
            new TeacherPhotoResponse($"/api/teachers/profiles/{command.UserId}/photo"));
    }
}

/// <summary>P04: Fotograf indirme oncesi anahtari cozer; fotograf yoksa 404 uretilir.</summary>
public sealed class GetTeacherPhotoQueryHandler
    : IQueryHandler<GetTeacherPhotoQuery, Result<TeacherPhotoFileResponse>>
{
    private static readonly Error NotFound = new("teachers.profile_not_found", "Ogretmen profili bulunamadi.");
    private static readonly Error PhotoNotFound = new("teachers.photo_not_found", "Profil fotografi bulunamadi.");

    private readonly ITeacherProfileRepository _repository;

    public GetTeacherPhotoQueryHandler(ITeacherProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TeacherPhotoFileResponse>> Handle(
        GetTeacherPhotoQuery query,
        CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(query.UserId, cancellationToken);
        if (profile is null)
        {
            return Result<TeacherPhotoFileResponse>.Failure(NotFound);
        }

        if (string.IsNullOrWhiteSpace(profile.ProfilePhotoKey))
        {
            return Result<TeacherPhotoFileResponse>.Failure(PhotoNotFound);
        }

        var contentType = Path.GetExtension(profile.ProfilePhotoKey).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return Result<TeacherPhotoFileResponse>.Success(
            new TeacherPhotoFileResponse(profile.ProfilePhotoKey, contentType));
    }
}

/// <summary>
/// M02-1: Doğrulama rozetini uygular. Yetki <see cref="EgitimUssu.Modules.Teachers.Application.SetTeacherVerificationCommandAuthorizer"/>
/// tarafından Admin'e sınırlanır; bu handler yalnız profili bulup domain metodunu çağırır.
/// </summary>
public sealed class SetTeacherVerificationCommandHandler
    : ICommandHandler<SetTeacherVerificationCommand, Result<TeacherProfileResponse>>
{
    private static readonly Error ProfileNotFound = new("teachers.profile_not_found", "Öğretmen profili bulunamadı.");

    private readonly ITeacherProfileRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Öğretmen profili deposu.</param>
    /// <param name="clock">Saat.</param>
    public SetTeacherVerificationCommandHandler(ITeacherProfileRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş profil.</returns>
    public async Task<Result<TeacherProfileResponse>> Handle(SetTeacherVerificationCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is null)
        {
            return Result<TeacherProfileResponse>.Failure(ProfileNotFound);
        }

        profile.SetVerification(command.IsVerified, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }
}