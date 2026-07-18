using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Application;
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
    /// Öğrenci ödev çözümünü yükler. Dosya önce belleğe alınır; yetki/komut başarılıysa depolamaya yazılır
    /// (aksi halde başka bir öğrencinin dosyasının üzerine yazılmasını önlemek için hiç kaydedilmez).
    /// </summary>
    private static async Task<IResult> SubmitAssignmentAsync(
        HttpContext context,
        Guid assignmentId,
        IFormFile? file,
        ICommandDispatcher dispatcher,
        IAssignmentFileStorage storage,
        CancellationToken cancellationToken)
    {
        const long maxBytes = 20 * 1024 * 1024;
        if (file is null || file.Length == 0)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status400BadRequest,
                new EgitimUssu.Shared.Kernel.Error("assignments.invalid_request", "Yüklenecek dosya bulunamadı."));
        }

        if (file.Length > maxBytes)
        {
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status400BadRequest,
                new EgitimUssu.Shared.Kernel.Error("assignments.file_too_large", "Dosya boyutu 20 MB sınırını aşıyor."));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var attachmentUrl = $"/api/assignments/{assignmentId}/attachment";
        var result = await dispatcher.Dispatch(new SubmitAssignmentWorkCommand(assignmentId, attachmentUrl), cancellationToken);
        if (result.IsSuccess)
        {
            buffer.Position = 0;
            await storage.SaveAsync(assignmentId, file.FileName, buffer, cancellationToken);
        }

        return ToHttpResult(context, result);
    }

    /// <summary>Ödev ekini (öğrenci teslimi) yetkili kullanıcıya (öğrenci/öğretmen/admin) döner.</summary>
    private static async Task<IResult> DownloadAttachmentAsync(
        HttpContext context,
        Guid assignmentId,
        IQueryDispatcher dispatcher,
        IAssignmentFileStorage storage,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetAssignmentQuery(assignmentId), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToHttpResult(context, result);
        }

        var file = await storage.OpenAsync(assignmentId, cancellationToken);
        if (file is null)
        {
            return Results.NotFound();
        }

        return Results.File(file.Content, file.ContentType, file.FileName);
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
