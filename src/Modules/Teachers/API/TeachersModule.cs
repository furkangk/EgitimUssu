using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.Teachers.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Teachers.API;

public sealed class TeachersModule : ModuleDefinition
{
    public override string Name => "Teachers";

    public override string RoutePrefix => "/api/teachers";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTeachersModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/profiles", CreateTeacherProfileAsync)
        .WithSummary("Öğretmen profili oluşturur");

        group.MapPut("/profiles/{userId:guid}", UpdateTeacherProfileAsync)
        .WithSummary("Öğretmen profilini günceller");

        group.MapGet("/profiles/{userId:guid}", GetTeacherProfileAsync)
        .WithSummary("Öğretmen profilini getirir");

        group.MapPost("/profiles/{userId:guid}/photo", UploadTeacherPhotoAsync)
        .WithSummary("Öğretmen profil fotoğrafını yükler (M02-2)")
        .DisableAntiforgery();

        group.MapGet("/profiles/{userId:guid}/photo", DownloadTeacherPhotoAsync)
        .WithSummary("Öğretmen profil fotoğrafını yetkiyle akıtır (M02-2)");

        group.MapPut("/profiles/{userId:guid}/verification", SetTeacherVerificationAsync)
        .WithSummary("Öğretmenin doğrulama rozetini verir/geri alır — yalnız Admin (M02-1)");
    }

    /// <summary>
    /// Profil fotoğrafını yükler (P04/M02-2). Dosya belleğe alınır ve komuta verilir; doğrulama, yetki ve
    /// depolama yazımı handler'da yapılır. Boyut/tür kuralı `FileValidation` (10 MB + beyaz liste).
    /// </summary>
    private static async Task<IResult> UploadTeacherPhotoAsync(
        HttpContext context,
        Guid userId,
        IFormFile? file,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status400BadRequest,
                new Error("teachers.invalid_request", "Yüklenecek dosya bulunamadı."));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await dispatcher.Dispatch(
            new UploadTeacherPhotoCommand(userId, file.FileName, contentType, buffer.Length, buffer),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Profil fotoğrafını akıtır (P04/M02-2). Dosya doğrudan URL ile sunulmaz; anahtar yetki kontrolünden
    /// **sonra** `IFileStorage`'tan okunur.
    /// </summary>
    private static async Task<IResult> DownloadTeacherPhotoAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetTeacherPhotoQuery(userId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                "teachers.profile_not_found" or "teachers.photo_not_found"
                    => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
        }

        var photo = result.Value!;
        var content = await fileStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        if (content is null)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status404NotFound,
                new Error("teachers.photo_not_found", "Profil fotoğrafı bulunamadı."));
        }

        return Results.File(content, photo.ContentType);
    }

    /// <summary>
    /// Öğretmenin uzmanlık, ücret, konum ve müsaitlik bilgilerini içeren yeni profil kaydını oluşturur.
    /// </summary>
    private static async Task<IResult> CreateTeacherProfileAsync(
        HttpContext context,
        UpsertTeacherProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCreateCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Var olan öğretmen profilinin ders, ücret, doğrulama ve müsaitlik bilgilerini günceller.
    /// </summary>
    private static async Task<IResult> UpdateTeacherProfileAsync(
        HttpContext context,
        Guid userId,
        UpsertTeacherProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToUpdateCommand(userId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Kullanıcı kimliğine göre öğretmen profil detayını getirir.
    /// </summary>
    private static async Task<IResult> GetTeacherProfileAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetTeacherProfileByUserIdQuery(userId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// M02-1: Öğretmenin doğrulama rozetini verir veya geri alır. Yetki yalnız Admin'dedir
    /// (<c>SetTeacherVerificationCommandAuthorizer</c>); öğretmen kendini doğrulayamaz.
    /// </summary>
    private static async Task<IResult> SetTeacherVerificationAsync(
        HttpContext context,
        Guid userId,
        SetTeacherVerificationRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new SetTeacherVerificationCommand(userId, request.IsVerified), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "teachers.profile_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "teachers.profile_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// M02-1: Doğrulama rozeti isteği.
/// </summary>
/// <param name="IsVerified">Rozet verilsin mi (false = geri al).</param>
public sealed record SetTeacherVerificationRequest(bool IsVerified);

/// <summary>
/// Öğretmenin haftalık tekrar eden müsaitlik aralığını ve ders formatı tercihlerini taşır.
/// </summary>
public sealed record TeacherAvailabilityItem(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

/// <summary>
/// Öğretmenin sertifika/deneyim kaydını (başlık, kurum, yıl, belge bağlantısı) taşır.
/// </summary>
public sealed record TeacherCertificateItem(
    string Title,
    string? Institution,
    int? Year,
    string? FileUrl);

/// <summary>
/// Öğretmen profili oluşturma ve güncelleme işlemlerinde kullanılan profil, ücret ve müsaitlik verilerini taşır.
/// </summary>
public sealed record UpsertTeacherProfileRequest(
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
    IReadOnlyCollection<TeacherAvailabilityItem> AvailabilitySlots,
    IReadOnlyCollection<string> Subjects,
    IReadOnlyCollection<TeacherCertificateItem> Certificates)
{
    public CreateTeacherProfileCommand ToCreateCommand()
    {
        return new CreateTeacherProfileCommand(
            UserId,
            FullName,
            Subject,
            City,
            District,
            Biography,
            Headline,
            LessonFormat,
            ExperienceYears,
            EducationLevel,
            HourlyRateAmount,
            Currency,
            AvailabilitySlots
                .Select(slot => new TeacherAvailabilityRequest(
                    slot.DayOfWeek,
                    slot.StartTime,
                    slot.EndTime,
                    slot.IsOnlineAvailable,
                    slot.IsInPersonAvailable))
                .ToArray(),
            Subjects ?? Array.Empty<string>(),
            (Certificates ?? Array.Empty<TeacherCertificateItem>())
                .Select(certificate => new TeacherCertificateRequest(
                    certificate.Title,
                    certificate.Institution,
                    certificate.Year,
                    certificate.FileUrl))
                .ToArray());
    }

    public UpdateTeacherProfileCommand ToUpdateCommand(Guid userId)
    {
        return new UpdateTeacherProfileCommand(
            userId,
            FullName,
            Subject,
            City,
            District,
            Biography,
            Headline,
            LessonFormat,
            ExperienceYears,
            EducationLevel,
            HourlyRateAmount,
            Currency,
            AvailabilitySlots
                .Select(slot => new TeacherAvailabilityRequest(
                    slot.DayOfWeek,
                    slot.StartTime,
                    slot.EndTime,
                    slot.IsOnlineAvailable,
                    slot.IsInPersonAvailable))
                .ToArray(),
            Subjects ?? Array.Empty<string>(),
            (Certificates ?? Array.Empty<TeacherCertificateItem>())
                .Select(certificate => new TeacherCertificateRequest(
                    certificate.Title,
                    certificate.Institution,
                    certificate.Year,
                    certificate.FileUrl))
                .ToArray());
    }
}
