using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Scheduling.API;

public sealed class SchedulingModule : ModuleDefinition
{
    public override string Name => "Scheduling";

    public override string RoutePrefix => "/api/scheduling";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSchedulingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/lessons", CreateLessonScheduleAsync)
        .WithSummary("Ders planı oluşturur");

        group.MapPut("/lessons/{lessonId:guid}", UpdateLessonScheduleAsync)
        .WithSummary("Ders planını günceller");

        group.MapPost("/lessons/{lessonId:guid}/cancel", CancelLessonScheduleAsync)
        .WithSummary("Ders planını iptal eder");

        group.MapPost("/lessons/{lessonId:guid}/complete", CompleteLessonScheduleAsync)
        .WithSummary("Ders planını tamamlandı olarak işaretler");

        group.MapGet("/lessons/{lessonId:guid}", GetLessonScheduleByIdAsync)
        .WithSummary("Ders planı detayını getirir");

        group.MapGet("/teachers/{teacherUserId:guid}/lessons", ListLessonSchedulesForTeacherAsync)
        .WithSummary("Öğretmenin ders planlarını listeler");

        group.MapGet("/students/{studentId:guid}/lessons", ListLessonSchedulesForStudentAsync)
        .WithSummary("Öğrencinin ders planlarını listeler");

        // Öğrenci-sahipli kişisel program (StudyScheduleEntry) + birleşik takvim.
        group.MapGet("/students/{studentId:guid}/calendar", GetStudentCalendarAsync)
        .WithSummary("Öğrencinin takvimini (öğretmen dersleri + kendi programı, tekrarlar genişletilmiş) getirir");

        group.MapPost("/students/{studentId:guid}/study-entries", CreateStudyScheduleEntryAsync)
        .WithSummary("Öğrencinin kendi program girdisini oluşturur");

        group.MapPut("/study-entries/{entryId:guid}", UpdateStudyScheduleEntryAsync)
        .WithSummary("Öğrencinin kendi program girdisini günceller");

        group.MapDelete("/study-entries/{entryId:guid}", DeleteStudyScheduleEntryAsync)
        .WithSummary("Öğrencinin kendi program girdisini siler");
    }

    /// <summary>
    /// Öğretmen ve öğrenci için tarih, format, tekrar ve hatırlatma bilgileriyle ders planı oluşturur.
    /// </summary>
    private static async Task<IResult> CreateLessonScheduleAsync(
        HttpContext context,
        CreateLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı dersin konu, zaman, format, tekrar, hatırlatma ve konum bilgilerini günceller.
    /// </summary>
    private static async Task<IResult> UpdateLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        UpdateLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(lessonId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı dersi isteğe bağlı iptal notuyla iptal eder.
    /// </summary>
    private static async Task<IResult> CancelLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        CancelLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new CancelLessonScheduleCommand(lessonId, request.CancellationNote), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı dersi tamamlandı olarak işaretler.
    /// </summary>
    private static async Task<IResult> CompleteLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new CompleteLessonScheduleCommand(lessonId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı ders detayını ders planı kimliğiyle getirir.
    /// </summary>
    private static async Task<IResult> GetLessonScheduleByIdAsync(
        HttpContext context,
        Guid lessonId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetLessonScheduleByIdQuery(lessonId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin belirli UTC tarih aralığındaki ders planlarını listeler.
    /// </summary>
    private static async Task<IResult> ListLessonSchedulesForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListLessonSchedulesForTeacherQuery(teacherUserId, startAtUtc, endAtUtc),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrencinin belirli UTC tarih aralığındaki ders planlarını listeler. Yalnızca öğrencinin
    /// kendisi (veya admin) erişebilir; sahiplik <c>IStudentDirectory</c> üzerinden doğrulanır.
    /// </summary>
    private static async Task<IResult> ListLessonSchedulesForStudentAsync(
        HttpContext context,
        Guid studentId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListLessonSchedulesForStudentQuery(studentId, startAtUtc, endAtUtc),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrencinin takvimini (öğretmen dersleri + kendi programı) tekrar kuralları genişletilmiş olarak getirir.
    /// Yalnızca öğrencinin kendisi (veya admin) erişebilir; sahiplik <c>IStudentDirectory</c> ile doğrulanır.
    /// </summary>
    private static async Task<IResult> GetStudentCalendarAsync(
        HttpContext context,
        Guid studentId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new GetStudentCalendarQuery(studentId, startAtUtc, endAtUtc),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrencinin kendi program girdisini (tek/tekrarlı) oluşturur. Öğretmen dersiyle saat çakışması reddedilir.
    /// </summary>
    private static async Task<IResult> CreateStudyScheduleEntryAsync(
        HttpContext context,
        Guid studentId,
        CreateStudyScheduleEntryRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(studentId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrencinin kendi program girdisini günceller. Öğretmen dersiyle saat çakışması reddedilir.
    /// </summary>
    private static async Task<IResult> UpdateStudyScheduleEntryAsync(
        HttpContext context,
        Guid entryId,
        UpdateStudyScheduleEntryRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(entryId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrencinin kendi program girdisini siler (soft-cancel).
    /// </summary>
    private static async Task<IResult> DeleteStudyScheduleEntryAsync(
        HttpContext context,
        Guid entryId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new DeleteStudyScheduleEntryCommand(entryId), cancellationToken);
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
            "scheduling.teacher_conflict" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "scheduling.lesson_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "scheduling.entry_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "scheduling.already_completed" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "scheduling.not_editable" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Ders planı oluşturmak için öğretmen, öğrenci, zaman, format, tekrar ve hatırlatma verilerini taşır.
/// </summary>
public sealed record CreateLessonScheduleRequest(
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? MeetingUrl,
    string? Notes)
{
    public CreateLessonScheduleCommand ToCommand()
    {
        return new CreateLessonScheduleCommand(
            TeacherUserId,
            StudentId,
            Subject,
            LessonFormat,
            StartAtUtc,
            EndAtUtc,
            TimeZone,
            RecurrenceRule,
            ReminderOffsetMinutes,
            LocationLabel,
            MeetingUrl,
            Notes);
    }
}

/// <summary>
/// Planlı dersi güncellemek için yeni konu, zaman, format, tekrar, hatırlatma ve konum verilerini taşır.
/// </summary>
public sealed record UpdateLessonScheduleRequest(
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? MeetingUrl,
    string? Notes)
{
    public UpdateLessonScheduleCommand ToCommand(Guid lessonId)
    {
        return new UpdateLessonScheduleCommand(
            lessonId,
            Subject,
            LessonFormat,
            StartAtUtc,
            EndAtUtc,
            TimeZone,
            RecurrenceRule,
            ReminderOffsetMinutes,
            LocationLabel,
            MeetingUrl,
            Notes);
    }
}

/// <summary>
/// Planlı ders iptal edilirken tutulacak isteğe bağlı açıklamayı taşır.
/// </summary>
public sealed record CancelLessonScheduleRequest(string? CancellationNote);

/// <summary>
/// Öğrencinin kendi program girdisini oluşturmak için ders, konu, zaman, tekrar ve renk verilerini taşır.
/// </summary>
public sealed record CreateStudyScheduleEntryRequest(
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes)
{
    public CreateStudyScheduleEntryCommand ToCommand(Guid studentId)
    {
        return new CreateStudyScheduleEntryCommand(
            studentId,
            Subject,
            Topic,
            StartAtUtc,
            EndAtUtc,
            TimeZone,
            RecurrenceRule,
            ReminderOffsetMinutes,
            ColorHex,
            Notes);
    }
}

/// <summary>
/// Öğrencinin kendi program girdisini güncellemek için yeni ders, konu, zaman, tekrar ve renk verilerini taşır.
/// </summary>
public sealed record UpdateStudyScheduleEntryRequest(
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes)
{
    public UpdateStudyScheduleEntryCommand ToCommand(Guid entryId)
    {
        return new UpdateStudyScheduleEntryCommand(
            entryId,
            Subject,
            Topic,
            StartAtUtc,
            EndAtUtc,
            TimeZone,
            RecurrenceRule,
            ReminderOffsetMinutes,
            ColorHex,
            Notes);
    }
}
