using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Infrastructure;
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

namespace EgitimUssu.Modules.Reporting.API;

public sealed class ReportingModule : ModuleDefinition
{
    public override string Name => "Reporting";

    public override string RoutePrefix => "/api/reporting";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddReportingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Raporlama modül durumunu getirir");

        group.MapGet("/teachers/{teacherUserId:guid}/dashboard", GetTeacherDashboardAsync)
            .WithSummary("Öğretmen paneli sayaçlarını read-model projeksiyonundan getirir")
            .RequireAuthorization("AuthenticatedUser");

        var secure = CreateModuleGroup(endpoints);
        secure.RequireAuthorization("AuthenticatedUser");

        secure.MapGet("/teachers/{teacherUserId:guid}/monthly", GetTeacherMonthlyAsync)
            .WithSummary("Öğretmenin aylık ders/tahsilat özetini getirir (M14-1)");
        secure.MapGet("/teachers/{teacherUserId:guid}/free-slots", GetTeacherFreeSlotsAsync)
            .WithSummary("Öğretmenin verilen aralıktaki boş zaman dilimlerini getirir (M14-5)");
        secure.MapGet("/students/{studentId:guid}/study-analysis", GetStudentStudyAnalysisAsync)
            .WithSummary("Öğrencinin haftalık çalışma analizini getirir (M14-2, gizlilik filtreli)");
        secure.MapGet("/students/{studentId:guid}/performance", GetStudentPerformanceAsync)
            .WithSummary("Öğrencinin ders bazlı aylık performansını getirir (M14-3, gizlilik filtreli)");

        secure.MapPost("/students/{studentId:guid}/report", GenerateStudentReportAsync)
            .WithSummary("Öğrenci için PDF gelişim raporu üretir (M14-4, premium)");
        secure.MapGet("/reports/{reportId:guid}/file", DownloadStudentReportAsync)
            .WithSummary("Üretilmiş PDF raporu yetkili kullanıcıya indirir (M14-4)");
    }

    /// <summary>M14-1: Yıl/ay verilmezse içinde bulunulan ay kullanılır (eksik parametre 500 üretmez).</summary>
    private static async Task<IResult> GetTeacherMonthlyAsync(
        HttpContext context,
        Guid teacherUserId,
        int? year,
        int? month,
        IQueryDispatcher dispatcher,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return ToHttpResult(context, await dispatcher.Dispatch(
            new GetTeacherMonthlyReportQuery(teacherUserId, year ?? now.Year, month ?? now.Month), cancellationToken));
    }

    /// <summary>M14-5: Aralık verilmezse bugünden itibaren bir hafta bakılır.</summary>
    private static async Task<IResult> GetTeacherFreeSlotsAsync(
        HttpContext context,
        Guid teacherUserId,
        DateTime? from,
        DateTime? to,
        IQueryDispatcher dispatcher,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind((from ?? clock.UtcNow).Date, DateTimeKind.Utc);
        var end = to is { } given ? DateTime.SpecifyKind(given, DateTimeKind.Utc) : start.AddDays(7);
        return ToHttpResult(context, await dispatcher.Dispatch(
            new GetTeacherFreeSlotsQuery(teacherUserId, start, end), cancellationToken));
    }

    private static async Task<IResult> GetStudentStudyAnalysisAsync(
        HttpContext context,
        Guid studentId,
        DateOnly? from,
        DateOnly? to,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new GetStudentStudyAnalysisQuery(studentId, from, to), cancellationToken));

    private static async Task<IResult> GetStudentPerformanceAsync(
        HttpContext context,
        Guid studentId,
        string? subject,
        DateOnly? from,
        DateOnly? to,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new GetStudentPerformanceQuery(studentId, subject, from, to), cancellationToken));

    /// <summary>
    /// M14-4: Raporu üretir. Dönem verilmezse son bir ay alınır; premium kapısı handler'ın başındadır
    /// (uç yalnız hata kodunu HTTP'ye çevirir).
    /// </summary>
    private static async Task<IResult> GenerateStudentReportAsync(
        HttpContext context,
        Guid studentId,
        GenerateStudentReportRequest? request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new GenerateStudentReportCommand(studentId, request?.From, request?.To, request?.Note),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToReportHttpError(context, result.Error);
    }

    /// <summary>
    /// M14-4: PDF'i akıtır. Dosya doğrudan URL ile sunulmaz; anahtar <c>IFileStorage</c>'tan
    /// yetki kontrolünden **sonra** okunur (P04 ödev eki deseni).
    /// </summary>
    private static async Task<IResult> DownloadStudentReportAsync(
        HttpContext context,
        Guid reportId,
        IQueryDispatcher dispatcher,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetStudentReportFileQuery(reportId), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToReportHttpError(context, result.Error);
        }

        var report = result.Value!;
        var content = await fileStorage.OpenReadAsync(report.StorageKey, cancellationToken);
        if (content is null)
        {
            // Kayıt var ama dosya yok (elle silinmiş / göç eksik): sessizce boş dönmek yerine 404.
            return ApiErrorHttpResults.FromError(
                context, StatusCodes.Status404NotFound,
                new Error("reporting.report_not_found", "Rapor dosyası bulunamadı."));
        }

        return Results.File(content, report.ContentType, report.FileName);
    }

    private static IResult ToReportHttpError(HttpContext context, Error error)
        => error.Code switch
        {
            "shared.forbidden" or "reporting.report_not_shared"
                => ApiErrorHttpResults.Forbidden(context, error.Message),
            // Premium kapısı: kaynak var, yetki var, eksik olan üyelik → 402.
            "reporting.premium_required"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status402PaymentRequired, error),
            "reporting.report_not_found"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, error),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, error)
        };

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };

    /// <summary>
    /// Öğretmen panelinin sayaçlarını (bugünkü ders, açık ödev, gecikmiş ödeme, aktif öğrenci) projeksiyondan
    /// tek satır okuyarak döndürür. Projeksiyon henüz beslenmediyse 404 döner — sıfır göstermek yalancı-yeşil olurdu.
    /// </summary>
    private static async Task<IResult> GetTeacherDashboardAsync(
        HttpContext context,
        Guid teacherUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch<Result<TeacherDashboardResponse>>(
            new GetTeacherDashboardQuery(teacherUserId), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                "reporting.dashboard_not_ready" =>
                    ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }

    /// <summary>
    /// Raporlama modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
    /// </summary>
    private IResult GetStatus()
    {
        return TypedResults.Ok(new
        {
            module = Name,
            route = RoutePrefix,
            state = "placeholder"
        });
    }
}

/// <summary>M14-4: Rapor üretim isteğinin gövdesi; üçü de isteğe bağlıdır.</summary>
/// <param name="From">Dönem başlangıcı (verilmezse bitişten bir ay geri).</param>
/// <param name="To">Dönem bitişi (verilmezse bugün).</param>
/// <param name="Note">PDF'e basılacak öğretmen notu.</param>
public sealed record GenerateStudentReportRequest(DateOnly? From, DateOnly? To, string? Note);
