using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Study.API;

public sealed class StudyModule : ModuleDefinition
{
    public override string Name => "Study";

    public override string RoutePrefix => "/api/study";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddStudyModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        // Çalışma seansları (kronometre)
        group.MapPost("/sessions/start", StartSessionAsync).WithSummary("Kronometreyle çalışma seansı başlatır");
        group.MapPost("/sessions/manual", CreateManualSessionAsync).WithSummary("Kronometresiz manuel seans girer");
        group.MapPost("/sessions/{sessionId:guid}/pause", PauseSessionAsync).WithSummary("Çalışma seansını molaya alır");
        group.MapPost("/sessions/{sessionId:guid}/resume", ResumeSessionAsync).WithSummary("Çalışma seansını sürdürür");
        group.MapPost("/sessions/{sessionId:guid}/complete", CompleteSessionAsync).WithSummary("Çalışma seansını tamamlar");
        group.MapPost("/sessions/{sessionId:guid}/discard", DiscardSessionAsync).WithSummary("Çalışma seansını iptal eder");
        group.MapGet("/sessions/{sessionId:guid}", GetSessionAsync).WithSummary("Seans detayını getirir");
        group.MapPut("/sessions/{sessionId:guid}", EditSessionAsync).WithSummary("Tamamlanmış seansı düzenler (konu rollup yeniden türetilir)");
        group.MapDelete("/sessions/{sessionId:guid}", DeleteSessionAsync).WithSummary("Çalışma seansını siler (konu rollup yeniden türetilir)");
        group.MapGet("/students/{studentId:guid}/sessions", ListSessionsAsync).WithSummary("Öğrencinin seans geçmişini listeler");
        group.MapGet("/students/{studentId:guid}/weekly-summary", WeeklySummaryAsync).WithSummary("Haftalık çalışma özetini getirir");

        // Deneme / test
        group.MapPost("/test-results", RecordTestAsync).WithSummary("Deneme/test sonucu kaydeder (net otomatik)");
        group.MapGet("/test-results/{testResultId:guid}", GetTestAsync).WithSummary("Deneme sonucunu getirir");
        group.MapPut("/test-results/{testResultId:guid}", EditTestAsync).WithSummary("Deneme sonucunu düzenler (net yeniden hesaplanır)");
        group.MapDelete("/test-results/{testResultId:guid}", DeleteTestAsync).WithSummary("Deneme sonucunu siler");
        group.MapGet("/students/{studentId:guid}/test-results", ListTestsAsync).WithSummary("Öğrencinin deneme sonuçlarını listeler");
        group.MapGet("/students/{studentId:guid}/net-trend", NetTrendAsync).WithSummary("Ders/konu bazlı net trendini getirir");

        // Hedef / streak / başarım / paylaşım / dashboard
        group.MapGet("/students/{studentId:guid}/goals", GetGoalsAsync).WithSummary("Aktif çalışma hedeflerini getirir");
        group.MapPut("/students/{studentId:guid}/goals", UpdateGoalsAsync).WithSummary("Çalışma hedeflerini belirler/günceller");
        group.MapGet("/students/{studentId:guid}/streak", GetStreakAsync).WithSummary("Çalışma serisini (streak) getirir");
        group.MapGet("/students/{studentId:guid}/achievements", GetAchievementsAsync).WithSummary("Başarım rozetlerini getirir");
        group.MapGet("/students/{studentId:guid}/dashboard", GetDashboardAsync).WithSummary("Öğrenci çalışma panosu özetini getirir");
        group.MapGet("/students/{studentId:guid}/sharing", GetSharingAsync).WithSummary("Paylaşım tercihlerini getirir");
        group.MapPut("/students/{studentId:guid}/sharing", UpdateSharingAsync).WithSummary("Veli/öğretmenle paylaşım tercihlerini günceller");

        // Ders/konu kataloğu
        group.MapGet("/students/{studentId:guid}/subjects", ListSubjectsAsync).WithSummary("Öğrencinin ders/konu kataloğunu listeler");
        group.MapPost("/students/{studentId:guid}/subjects", CreateSubjectAsync).WithSummary("Katalog dersi ekler");
        group.MapPut("/subjects/{subjectId:guid}", UpdateSubjectAsync).WithSummary("Katalog dersini günceller");
        group.MapDelete("/subjects/{subjectId:guid}", DeleteSubjectAsync).WithSummary("Katalog dersini siler");
        group.MapPost("/subjects/{subjectId:guid}/topics", AddTopicAsync).WithSummary("Katalog dersine konu ekler");
        group.MapPut("/topics/{topicId:guid}", UpdateTopicAsync).WithSummary("Katalog konusunu günceller");
        group.MapDelete("/topics/{topicId:guid}", DeleteTopicAsync).WithSummary("Katalog konusunu siler");

        // Öğrenci ders notları
        group.MapGet("/students/{studentId:guid}/notes", ListNotesAsync).WithSummary("Öğrencinin kendi ders notlarını listeler");
        group.MapPost("/students/{studentId:guid}/notes", CreateNoteAsync).WithSummary("Öğrenci ders notu ekler");
        group.MapPut("/notes/{noteId:guid}", UpdateNoteAsync).WithSummary("Öğrenci ders notunu günceller");
        group.MapDelete("/notes/{noteId:guid}", DeleteNoteAsync).WithSummary("Öğrenci ders notunu siler");
    }

    private static async Task<IResult> StartSessionAsync(HttpContext ctx, StartStudySessionRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new StartStudySessionCommand(req.StudentId, req.Subject, req.Topic), ct));

    private static async Task<IResult> CreateManualSessionAsync(HttpContext ctx, CreateManualSessionRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(
            new CreateManualStudySessionCommand(req.StudentId, req.Subject, req.Topic, req.EffectiveMinutes, req.StudiedOnUtc, req.PersonalNote), ct));

    private static async Task<IResult> PauseSessionAsync(HttpContext ctx, Guid sessionId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new PauseStudySessionCommand(sessionId), ct));

    private static async Task<IResult> ResumeSessionAsync(HttpContext ctx, Guid sessionId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ResumeStudySessionCommand(sessionId), ct));

    private static async Task<IResult> CompleteSessionAsync(HttpContext ctx, Guid sessionId, CompleteSessionRequest? req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new CompleteStudySessionCommand(sessionId, req?.PersonalNote), ct));

    private static async Task<IResult> DiscardSessionAsync(HttpContext ctx, Guid sessionId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DiscardStudySessionCommand(sessionId), ct));

    private static async Task<IResult> GetSessionAsync(HttpContext ctx, Guid sessionId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetStudySessionQuery(sessionId), ct));

    private static async Task<IResult> EditSessionAsync(HttpContext ctx, Guid sessionId, EditSessionRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(
            new EditStudySessionCommand(sessionId, req.Subject, req.Topic, req.EffectiveMinutes, req.PersonalNote), ct));

    private static async Task<IResult> DeleteSessionAsync(HttpContext ctx, Guid sessionId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DeleteStudySessionCommand(sessionId), ct));

    private static async Task<IResult> ListSessionsAsync(HttpContext ctx, Guid studentId, DateTime? from, DateTime? to, string? subject, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListStudySessionsQuery(studentId, from, to, subject), ct));

    private static async Task<IResult> WeeklySummaryAsync(HttpContext ctx, Guid studentId, DateOnly? weekStart, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new WeeklySummaryQuery(studentId, weekStart), ct));

    private static async Task<IResult> RecordTestAsync(HttpContext ctx, RecordTestResultRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new RecordTestResultCommand(
            req.StudentId, req.Subject, req.Topic, req.TestType, req.TestName, req.TotalQuestions,
            req.Correct, req.Wrong, req.Blank, req.PenaltyDivisor, req.DurationMinutes, req.TakenOnUtc), ct));

    private static async Task<IResult> GetTestAsync(HttpContext ctx, Guid testResultId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetTestResultQuery(testResultId), ct));

    private static async Task<IResult> EditTestAsync(HttpContext ctx, Guid testResultId, RecordTestResultRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new EditTestResultCommand(
            testResultId, req.Subject, req.Topic, req.TestType, req.TestName, req.TotalQuestions,
            req.Correct, req.Wrong, req.Blank, req.PenaltyDivisor, req.DurationMinutes, req.TakenOnUtc), ct));

    private static async Task<IResult> DeleteTestAsync(HttpContext ctx, Guid testResultId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DeleteTestResultCommand(testResultId), ct));

    private static async Task<IResult> ListTestsAsync(HttpContext ctx, Guid studentId, string? subject, string? topic, DateTime? from, DateTime? to, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListTestResultsQuery(studentId, subject, topic, from, to), ct));

    private static async Task<IResult> NetTrendAsync(HttpContext ctx, Guid studentId, string? subject, string? topic, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new NetTrendQuery(studentId, subject, topic), ct));

    private static async Task<IResult> GetGoalsAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetStudyGoalsQuery(studentId), ct));

    private static async Task<IResult> UpdateGoalsAsync(HttpContext ctx, Guid studentId, UpdateGoalsRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new UpdateStudyGoalsCommand(
            studentId, req.DailyGoalMinutes, req.WeeklyGoalMinutes, req.TargetNet, req.TargetScore, req.Subject, req.StreakThresholdPercent), ct));

    private static async Task<IResult> GetStreakAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetStreakQuery(studentId), ct));

    private static async Task<IResult> GetAchievementsAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetAchievementsQuery(studentId), ct));

    private static async Task<IResult> GetDashboardAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetStudyDashboardQuery(studentId), ct));

    private static async Task<IResult> GetSharingAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetStudySharingQuery(studentId), ct));

    private static async Task<IResult> UpdateSharingAsync(HttpContext ctx, Guid studentId, UpdateSharingRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new UpdateStudySharingCommand(
            studentId, req.ShareStudyWithParent, req.ShareTestsWithParent, req.ShareStudyWithTeacher, req.ShareTestsWithTeacher), ct));

    private static async Task<IResult> ListSubjectsAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListSubjectCatalogQuery(studentId), ct));

    private static async Task<IResult> CreateSubjectAsync(HttpContext ctx, Guid studentId, CreateSubjectRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new CreateSubjectCatalogCommand(studentId, req.Name, req.ColorHex), ct));

    private static async Task<IResult> UpdateSubjectAsync(HttpContext ctx, Guid subjectId, UpdateSubjectRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new UpdateSubjectCatalogCommand(subjectId, req.Name, req.ColorHex, req.IsActive), ct));

    private static async Task<IResult> DeleteSubjectAsync(HttpContext ctx, Guid subjectId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DeleteSubjectCatalogCommand(subjectId), ct));

    private static async Task<IResult> AddTopicAsync(HttpContext ctx, Guid subjectId, AddTopicRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new AddTopicCatalogCommand(subjectId, req.Name), ct));

    private static async Task<IResult> UpdateTopicAsync(HttpContext ctx, Guid topicId, UpdateTopicRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new UpdateTopicCatalogCommand(topicId, req.Name, req.OrderIndex, req.IsActive), ct));

    private static async Task<IResult> DeleteTopicAsync(HttpContext ctx, Guid topicId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DeleteTopicCatalogCommand(topicId), ct));

    private static async Task<IResult> ListNotesAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListStudyNotesQuery(studentId), ct));

    private static async Task<IResult> CreateNoteAsync(HttpContext ctx, Guid studentId, StudyNoteRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(
            new CreateStudyNoteCommand(studentId, req.Title, req.Body, req.Subject, req.Topic, req.AttachmentUrl), ct));

    private static async Task<IResult> UpdateNoteAsync(HttpContext ctx, Guid noteId, StudyNoteRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(
            new UpdateStudyNoteCommand(noteId, req.Title, req.Body, req.Subject, req.Topic, req.AttachmentUrl), ct));

    private static async Task<IResult> DeleteNoteAsync(HttpContext ctx, Guid noteId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new DeleteStudyNoteCommand(noteId), ct));

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "study.session_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.test_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.goal_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.subject_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.topic_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.note_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "study.session_active" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record StartStudySessionRequest(Guid StudentId, string Subject, string? Topic);

public sealed record CreateManualSessionRequest(
    Guid StudentId, string Subject, string? Topic, int EffectiveMinutes, DateTime StudiedOnUtc, string? PersonalNote);

public sealed record CompleteSessionRequest(string? PersonalNote);

public sealed record EditSessionRequest(string Subject, string? Topic, int EffectiveMinutes, string? PersonalNote);

public sealed record RecordTestResultRequest(
    Guid StudentId,
    string Subject,
    string? Topic,
    string TestType,
    string? TestName,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    int? PenaltyDivisor,
    int? DurationMinutes,
    DateTime TakenOnUtc);

public sealed record UpdateGoalsRequest(
    int DailyGoalMinutes, int? WeeklyGoalMinutes, decimal? TargetNet, decimal? TargetScore, string? Subject, int StreakThresholdPercent = 60);

public sealed record UpdateSharingRequest(
    bool ShareStudyWithParent, bool ShareTestsWithParent, bool ShareStudyWithTeacher, bool ShareTestsWithTeacher);

public sealed record CreateSubjectRequest(string Name, string? ColorHex);

public sealed record UpdateSubjectRequest(string Name, string? ColorHex, bool IsActive);

public sealed record AddTopicRequest(string Name);

public sealed record UpdateTopicRequest(string Name, int OrderIndex, bool IsActive);

public sealed record StudyNoteRequest(
    string Title, string Body, string? Subject, string? Topic, string? AttachmentUrl);
