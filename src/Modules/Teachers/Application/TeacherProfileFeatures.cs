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

// ---------------------------------------------------------------------------
// M02-3 / M02-4: Öğretmen arama-listeleme ve pasifleştirme
// ---------------------------------------------------------------------------

/// <summary>M02-3: Filtreli, sayfalı öğretmen arama. Yalnız **aktif** profiller döner.</summary>
/// <param name="City">Şehir (tam eşleşme, büyük/küçük harf duyarsız).</param>
/// <param name="District">İlçe (tam eşleşme, büyük/küçük harf duyarsız).</param>
/// <param name="Subject">Branş — birincil `Subject` veya `Subjects` listesinde aranır.</param>
/// <param name="LessonFormat">Ders biçimi filtresi.</param>
/// <param name="MinRate">Alt saatlik ücret sınırı (dahil).</param>
/// <param name="MaxRate">Üst saatlik ücret sınırı (dahil).</param>
/// <param name="OnlyVerified">Yalnız doğrulanmış öğretmenler.</param>
/// <param name="Skip">Atlanacak kayıt sayısı.</param>
/// <param name="Take">Sayfa boyutu (en fazla 50).</param>
public sealed record SearchTeacherProfilesQuery(
    string? City,
    string? District,
    string? Subject,
    TeacherLessonFormat? LessonFormat,
    decimal? MinRate,
    decimal? MaxRate,
    bool? OnlyVerified,
    int Skip,
    int Take) : IQuery<Result<PagedResult<TeacherSummaryResponse>>>;

/// <summary>M02-3: Arama sonucundaki hafif öğretmen görünümü (biyografi/sertifika taşımaz).</summary>
/// <param name="UserId">Öğretmenin kullanıcı kimliği.</param>
/// <param name="FullName">Görünen ad.</param>
/// <param name="Subject">Birincil branş.</param>
/// <param name="Subjects">Tüm branşlar.</param>
/// <param name="City">Şehir.</param>
/// <param name="District">İlçe.</param>
/// <param name="LessonFormat">Ders biçimi adı.</param>
/// <param name="HourlyRateAmount">Saatlik ücret.</param>
/// <param name="Currency">Para birimi.</param>
/// <param name="IsVerified">Doğrulama rozeti.</param>
/// <param name="IsActive">Profil aktif mi (aramada daima true).</param>
/// <param name="ExperienceYears">Deneyim yılı.</param>
/// <param name="Headline">Vitrin başlığı.</param>
public sealed record TeacherSummaryResponse(
    Guid UserId,
    string FullName,
    string Subject,
    IReadOnlyCollection<string> Subjects,
    string City,
    string District,
    string LessonFormat,
    decimal HourlyRateAmount,
    string Currency,
    bool IsVerified,
    bool IsActive,
    int ExperienceYears,
    string? Headline);

/// <summary>M02-4: Öğretmen profilini pasifleştirir/aktifleştirir.</summary>
/// <param name="UserId">Öğretmenin kullanıcı kimliği.</param>
/// <param name="IsActive">Yeni durum.</param>
public sealed record SetTeacherActivationCommand(Guid UserId, bool IsActive)
    : ICommand<Result<TeacherProfileResponse>>;

/// <summary>M02-3: Arama sorgusunun depo tarafı. EF ile `IQueryable` üzerinde çalışır.</summary>
public interface ITeacherProfileSearchRepository
{
    /// <summary>Filtreleri uygular, sayfalar ve toplam sayıyı döner.</summary>
    /// <param name="query">Arama filtreleri.</param>
    /// <param name="skip">Atlanacak kayıt sayısı (normalize edilmiş).</param>
    /// <param name="take">Sayfa boyutu (normalize edilmiş).</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Sayfa ve toplam kayıt sayısı.</returns>
    Task<(IReadOnlyList<TeacherProfile> Items, int TotalCount)> SearchAsync(
        SearchTeacherProfilesQuery query, int skip, int take, CancellationToken cancellationToken);
}

/// <summary>
/// M02-3: Filtre mantığı **tek yerde**. Hem EF sorgusu hem birim testi bu ifadeyi kullanır;
/// böylece "testte geçen, üretimde farklı davranan filtre" sınıfı hatalar oluşmaz.
/// </summary>
public static class TeacherSearchFilter
{
    /// <summary>
    /// Arama girdisini Postgres'in <c>lower()</c> davranışıyla hizalar.
    /// <para>
    /// İki tuzak birden var: (1) <see cref="string.ToLower()"/> <b>kültüre bağlıdır</b> — tr-TR altında
    /// <c>'I' → 'ı'</c> olur ve Postgres'in ürettiği <c>'i'</c> ile eşleşmez ("Istanbul" araması boş dönerdi).
    /// (2) <c>ToLowerInvariant()</c> ise <c>'İ'</c>'yi <c>"i" + birleşen nokta</c> (2 kod noktası) yapar,
    /// Postgres ise düz <c>"i"</c> üretir. Bu yüzden Türkçe I varyantları önce düz <c>'i'</c>'ye katlanır.
    /// </para>
    /// </summary>
    /// <param name="value">Kullanıcının yazdığı ham arama metni.</param>
    /// <returns>Postgres <c>lower()</c> ile aynı sonucu veren katlanmış metin.</returns>
    public static string NormalizeForSearch(string value)
        => value.Trim().Replace('İ', 'i').Replace('I', 'i').ToLowerInvariant();

    /// <summary>Sorgu filtrelerini uygular (sıralama: doğrulanmışlar önce, sonra ücret).</summary>
    /// <param name="source">Kaynak sorgu.</param>
    /// <param name="query">Filtreler.</param>
    /// <returns>Filtrelenmiş ve sıralanmış sorgu.</returns>
    public static IQueryable<TeacherProfile> Apply(IQueryable<TeacherProfile> source, SearchTeacherProfilesQuery query)
    {
        // Pasif profiller hiçbir koşulda aramada görünmez (M02-4).
        source = source.Where(p => p.IsActive);

        // ⚠️ Karşılaştırmanın sol tarafı SQL'e (`lower(...)`) çevrilir, sağ tarafı ise .NET'te
        // **istemci tarafında** hesaplanıp parametre olarak gider. İki tarafın aynı sonucu
        // vermesi için istemci tarafı `NormalizeForSearch` ile katlanır (bkz. metodun notu).
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = NormalizeForSearch(query.City);
            source = source.Where(p => p.City.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(query.District))
        {
            var district = NormalizeForSearch(query.District);
            source = source.Where(p => p.District.ToLower() == district);
        }

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            // Birincil branş VEYA çoklu branş listesi (Dilim D ile eklenen `teacher_subjects`).
            var subject = NormalizeForSearch(query.Subject);
            source = source.Where(p =>
                p.Subject.ToLower() == subject ||
                p.Subjects.Any(s => s.Subject.ToLower() == subject));
        }

        if (query.LessonFormat is { } format)
        {
            source = source.Where(p => p.LessonFormat == format);
        }

        if (query.MinRate is { } minRate)
        {
            source = source.Where(p => p.HourlyRateAmount >= minRate);
        }

        if (query.MaxRate is { } maxRate)
        {
            source = source.Where(p => p.HourlyRateAmount <= maxRate);
        }

        if (query.OnlyVerified == true)
        {
            source = source.Where(p => p.IsVerified);
        }

        return source
            .OrderByDescending(p => p.IsVerified)
            .ThenBy(p => p.HourlyRateAmount)
            .ThenBy(p => p.FullName);
    }

    /// <summary>Profili arama sonucuna eşler.</summary>
    /// <param name="profile">Öğretmen profili.</param>
    /// <returns>Hafif görünüm.</returns>
    public static TeacherSummaryResponse ToSummary(TeacherProfile profile)
        => new(
            profile.UserId,
            profile.FullName,
            profile.Subject,
            profile.Subjects.Select(s => s.Subject).ToArray(),
            profile.City,
            profile.District,
            profile.LessonFormat.ToString(),
            profile.HourlyRateAmount,
            profile.Currency,
            profile.IsVerified,
            profile.IsActive,
            profile.ExperienceYears,
            profile.Headline);
}

/// <summary>M02-3: Arama sorgusunu karşılar; sayfa boyutunu güvenli sınıra çeker.</summary>
public sealed class SearchTeacherProfilesQueryHandler
    : IQueryHandler<SearchTeacherProfilesQuery, Result<PagedResult<TeacherSummaryResponse>>>
{
    /// <summary>İstemcinin aşırı sayfa istemesini engelleyen üst sınır.</summary>
    public const int MaxTake = 50;

    /// <summary>Sayfa boyutu verilmediğinde kullanılan varsayılan.</summary>
    public const int DefaultTake = 20;

    private readonly ITeacherProfileSearchRepository _repository;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Arama deposu.</param>
    public SearchTeacherProfilesQueryHandler(ITeacherProfileSearchRepository repository)
        => _repository = repository;

    /// <summary>Sorguyu işler.</summary>
    /// <param name="query">Arama filtreleri.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Sayfalı öğretmen listesi.</returns>
    public async Task<Result<PagedResult<TeacherSummaryResponse>>> Handle(
        SearchTeacherProfilesQuery query, CancellationToken cancellationToken)
    {
        var skip = Math.Max(query.Skip, 0);
        var take = Math.Clamp(query.Take <= 0 ? DefaultTake : query.Take, 1, MaxTake);

        var (items, totalCount) = await _repository.SearchAsync(query, skip, take, cancellationToken);
        var payload = items.Select(TeacherSearchFilter.ToSummary).ToArray();

        return Result<PagedResult<TeacherSummaryResponse>>.Success(
            new PagedResult<TeacherSummaryResponse>(payload, (skip / take) + 1, take, totalCount));
    }
}

/// <summary>M02-4: Pasifleştirme/aktifleştirme komutunu karşılar.</summary>
public sealed class SetTeacherActivationCommandHandler
    : ICommandHandler<SetTeacherActivationCommand, Result<TeacherProfileResponse>>
{
    private static readonly Error ProfileNotFound = new("teachers.profile_not_found", "Öğretmen profili bulunamadı.");

    private readonly ITeacherProfileRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Öğretmen profili deposu.</param>
    /// <param name="clock">Saat.</param>
    public SetTeacherActivationCommandHandler(ITeacherProfileRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş profil.</returns>
    public async Task<Result<TeacherProfileResponse>> Handle(SetTeacherActivationCommand command, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is null)
        {
            return Result<TeacherProfileResponse>.Failure(ProfileNotFound);
        }

        if (command.IsActive)
        {
            profile.Activate(_clock.UtcNow);
        }
        else
        {
            profile.Deactivate(_clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TeacherProfileResponse>.Success(profile.ToResponse());
    }
}