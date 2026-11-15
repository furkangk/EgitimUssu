using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel.Storage;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Assignments.API;

public sealed class AssignmentsModule : ModuleDefinition
{
    public override string Name => "Assignments";

    public override string RoutePrefix => "/api/assignments";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAssignmentsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/lesson-sessions/{lessonSessionId:guid}/follow-up", CreateLessonSessionFollowUpAsync)
        .WithSummary("Ders oturumu takip notu ve ödevlerini oluşturur");

        group.MapGet("/lesson-sessions/{lessonSessionId:guid}/follow-up", GetLessonSessionFollowUpAsync)
        .WithSummary("Ders oturumu takip notunu getirir");

        group.MapGet(string.Empty, ListAssignmentsAsync)
        .WithSummary("Ödevleri listeler");

        group.MapPost("/{assignmentId:guid}/complete", CompleteAssignmentAsync)
        .WithSummary("Öğrenci ödevi tamamlandı olarak işaretler");

        group.MapPost("/{assignmentId:guid}/submission", SubmitAssignmentAsync)
        .WithSummary("Öğrenci ödev çözümünü (dosya) yükler")
        .DisableAntiforgery();

        group.MapGet("/{assignmentId:guid}/attachment", DownloadAttachmentAsync)
        .WithSummary("Ödev ekini (öğrenci teslimi) indirir");

        group.MapPost("/{assignmentId:guid}/approve", ApproveAssignmentAsync)
        .WithSummary("Öğretmen ödevi onaylar");

        group.MapPost("/{assignmentId:guid}/return", ReturnAssignmentAsync)
        .WithSummary("Öğretmen ödevi geri bildirimle geri gönderir");

        group.MapPost("/{assignmentId:guid}/grade", GradeAssignmentAsync)
        .WithSummary("Öğretmen onayladığı ödeve puan verir (M06-4)");

        group.MapPost("/lesson-resources", CreateLessonResourceAsync)
        .WithSummary("Ders kaynağı ekler — dosya veya bağlantı (M06-1)")
        .DisableAntiforgery();

        group.MapGet("/lesson-resources", ListLessonResourcesAsync)
        .WithSummary("Ders kaynaklarını listeler (öğretmen kendi, öğrenci kendisine açık olanlar)");

        group.MapGet("/lesson-resources/{resourceId:guid}/file", DownloadLessonResourceAsync)
        .WithSummary("Ders kaynağı dosyasını yetkiyle indirir");

        group.MapDelete("/lesson-resources/{resourceId:guid}", ArchiveLessonResourceAsync)
        .WithSummary("Ders kaynağını arşivler (yumuşak silme)");
    }

    /// <summary>
    /// Ders kaynağı ekler (M06-1). `file` alanı doluysa dosya, `url` alanı doluysa bağlantı kaynağı üretilir.
    /// Multipart form olarak gelir; doğrulama ve depolama yazımı handler'da yapılır.
    /// </summary>
    private static async Task<IResult> CreateLessonResourceAsync(
        HttpContext context,
        HttpRequest request,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var teacherUserId))
        {
            return ApiErrorHttpResults.Forbidden(context, "Bu işlemi yapma yetkiniz yok.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var title = form["title"].ToString();
        var description = form["description"].ToString();
        var url = form["url"].ToString();
        var studentId = Guid.TryParse(form["studentId"], out var parsedStudentId) ? parsedStudentId : (Guid?)null;
        var lessonSessionId = Guid.TryParse(form["lessonSessionId"], out var parsedSessionId) ? parsedSessionId : (Guid?)null;

        var file = form.Files.GetFile("file");
        MemoryStream? buffer = null;

        try
        {
            if (file is not null && file.Length > 0)
            {
                buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
            }

            var result = await dispatcher.Dispatch(
                new CreateLessonResourceCommand(
                    teacherUserId,
                    studentId,
                    lessonSessionId,
                    title,
                    string.IsNullOrWhiteSpace(description) ? null : description,
                    string.IsNullOrWhiteSpace(url) ? null : url,
                    file?.FileName,
                    file?.ContentType,
                    buffer?.Length ?? 0,
                    buffer),
                cancellationToken);

            return ToHttpResult(context, result);
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>Ders kaynaklarını listeler (M06-1). Yetki: öğretmen kendi, öğrenci kendisine açık olanlar.</summary>
    private static async Task<IResult> ListLessonResourcesAsync(
        HttpContext context,
        Guid? teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListLessonResourcesQuery(teacherUserId, studentId, lessonSessionId),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>Ders kaynağı dosyasını akıtır (M06-1). Public URL yok; anahtar yetkiden sonra okunur.</summary>
    private static async Task<IResult> DownloadLessonResourceAsync(
        HttpContext context,
        Guid resourceId,
        IQueryDispatcher dispatcher,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetLessonResourceFileQuery(resourceId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                "assignments.resource_not_found" or "assignments.resource_not_a_file"
                    => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
        }

        var resource = result.Value!;
        var content = await fileStorage.OpenReadAsync(resource.StorageKey, cancellationToken);
        if (content is null)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status404NotFound,
                new EgitimUssu.Shared.Kernel.Error("assignments.resource_not_found", "Ders kaynağı bulunamadı."));
        }

        return Results.File(content, resource.ContentType, resource.FileName);
    }

    /// <summary>Ders kaynağını arşivler — yumuşak silme (M06-1).</summary>
    private static async Task<IResult> ArchiveLessonResourceAsync(
        HttpContext context,
        Guid resourceId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ArchiveLessonResourceCommand(resourceId), cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        return result.Error.Code switch
        {
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            "assignments.resource_not_found"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }

    /// <summary>Öğretmen ödevi onaylar; isteğe bağlı geri bildirim ekler.</summary>
    private static async Task<IResult> ApproveAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        ApproveAssignmentRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ApproveAssignmentCommand(assignmentId, request.Feedback), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>Öğretmen ödevi geri bildirimle revizyona geri gönderir.</summary>
    private static async Task<IResult> ReturnAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        ReturnAssignmentRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ReturnAssignmentCommand(assignmentId, request.Feedback), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>Öğretmen onayladığı ödeve 0–100 arası puan verir (M06-4).</summary>
    private static async Task<IResult> GradeAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        GradeAssignmentRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new GradeAssignmentCommand(assignmentId, request.Score, request.Feedback),
            cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>Öğrenci kendi ödevini tamamlandı olarak işaretler.</summary>
    private static async Task<IResult> CompleteAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new MarkAssignmentCompletedCommand(assignmentId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrenci ödev çözümünü yükler (P04). Dosya belleğe alınır ve komuta verilir; doğrulama, yetki ve
    /// depolama yazımı handler'da yapılır — yetkisiz istek depoya hiçbir şey bırakmaz.
    /// Boyut/tür kuralı `FileValidation` (10 MB + beyaz liste).
    /// </summary>
    private static async Task<IResult> SubmitAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        IFormFile? file,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status400BadRequest,
                new EgitimUssu.Shared.Kernel.Error("assignments.invalid_request", "Yüklenecek dosya bulunamadı."));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await dispatcher.Dispatch(
            new SubmitAssignmentWorkCommand(assignmentId, file.FileName, contentType, buffer.Length, buffer),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Ödev ekini (öğrenci teslimi) yetkili kullanıcıya (öğrenci/öğretmen/admin) akıtır (P04).
    /// Dosya doğrudan URL ile sunulmaz; anahtar `IFileStorage`'tan yetki kontrolünden **sonra** okunur.
    /// </summary>
    private static async Task<IResult> DownloadAttachmentAsync(
        HttpContext context,
        Guid assignmentId,
        int? version,
        IQueryDispatcher dispatcher,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetAssignmentAttachmentQuery(assignmentId, version), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                "assignments.assignment_not_found" or "assignments.attachment_not_found"
                    => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
        }

        var attachment = result.Value!;
        var content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        if (content is null)
        {
            // Kayıt var ama dosya yok (elle silinmiş / göç eksik): sessizce boş dönmek yerine 404.
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status404NotFound,
                new EgitimUssu.Shared.Kernel.Error("assignments.attachment_not_found", "Ödev eki bulunamadı."));
        }

        return Results.File(content, attachment.ContentType, attachment.FileName);
    }

    /// <summary>
    /// Tamamlanan ders oturumu için takip notunu ve varsa öğrenciye verilen ödevleri oluşturur.
    /// </summary>
    private static async Task<IResult> CreateLessonSessionFollowUpAsync(
        HttpContext context,
        Guid lessonSessionId,
        CreateLessonSessionFollowUpRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(lessonSessionId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Ders oturumuna bağlı takip notunu ve bu takipten üretilen ödevleri getirir.
    /// </summary>
    private static async Task<IResult> GetLessonSessionFollowUpAsync(
        HttpContext context,
        Guid lessonSessionId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetLessonSessionFollowUpQuery(lessonSessionId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen, öğrenci veya ders oturumu filtresine göre ödevleri listeler.
    /// </summary>
    private static async Task<IResult> ListAssignmentsAsync(
        HttpContext context,
        Guid? teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListAssignmentsQuery(teacherUserId, studentId, lessonSessionId),
            cancellationToken);
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
            "assignments.follow_up_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "assignments.lesson_session_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "assignments.follow_up_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "assignments.assignment_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Ders sonrası takip kaydıyla birlikte oluşturulacak tekil ödev bilgisini taşır.
/// </summary>
public sealed record CreateLessonSessionFollowUpAssignmentRequest(
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    string? AttachmentUrl);

/// <summary>Öğretmen ödev onayı için isteğe bağlı geri bildirim.</summary>
public sealed record ApproveAssignmentRequest(string? Feedback);

/// <summary>Öğretmen ödevi geri gönderirken zorunlu geri bildirim.</summary>
public sealed record ReturnAssignmentRequest(string Feedback);

/// <summary>M06-4: Ödev puanı (0–100) ve isteğe bağlı geri bildirim.</summary>
public sealed record GradeAssignmentRequest(int Score, string? Feedback);

/// <summary>
/// Ders sonrası özet, işlenen konular, öneriler ve isteğe bağlı ödev listesini taşır.  
/// </summary>
public sealed record CreateLessonSessionFollowUpRequest(
    string Summary,
    string? CoveredTopics,
    string? Recommendations,
    IReadOnlyCollection<CreateLessonSessionFollowUpAssignmentRequest>? Assignments,
    LessonNoteVisibility Visibility = LessonNoteVisibility.Private)
{
    public CreateLessonSessionFollowUpCommand ToCommand(Guid lessonSessionId)
    {
        return new CreateLessonSessionFollowUpCommand(
            lessonSessionId,
            Summary,
            CoveredTopics,
            Recommendations,
            (Assignments ?? [])
                .Select(assignment => new AssignmentDraft(
                    assignment.Title,
                    assignment.Description,
                    assignment.DueDateUtc,
                    assignment.AttachmentUrl))
                .ToArray(),
            Visibility);
    }
}
