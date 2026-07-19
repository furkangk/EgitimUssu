using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Payments.API;

public sealed class PaymentsModule : ModuleDefinition
{
    public override string Name => "Payments";

    public override string RoutePrefix => "/api/payments";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPaymentsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/records", CreatePaymentRecordAsync)
        .WithSummary("Ödeme kaydı oluşturur");

        group.MapPut("/records/{paymentRecordId:guid}", UpdatePaymentRecordAsync)
        .WithSummary("Ödeme kaydını günceller");

        group.MapGet("/records/{paymentRecordId:guid}", GetPaymentRecordByIdAsync)
        .WithSummary("Ödeme kaydı detayını getirir");

        group.MapGet("/teachers/{teacherUserId:guid}/records", ListPaymentRecordsForTeacherAsync)
        .WithSummary("Öğretmenin ödeme kayıtlarını listeler");

        group.MapGet("/teachers/{teacherUserId:guid}/summary", GetTeacherPaymentSummaryAsync)
        .WithSummary("Öğretmenin ödeme özetini getirir");

        group.MapGet("/teachers/{teacherUserId:guid}/records/filter", ListFilteredPaymentRecordsForTeacherAsync)
        .WithSummary("Öğretmenin ödeme kayıtlarını filtreler");

        group.MapGet("/teachers/{teacherUserId:guid}/records/search", SearchPaymentRecordsForTeacherAsync)
        .WithSummary("Öğretmenin ödemelerinde arama + filtre + sayfalama");

        // Veli "ödedim" beyanı (Veli V-G)
        group.MapPost("/records/{paymentRecordId:guid}/declare-paid", DeclarePaymentPaidAsync)
        .WithSummary("Veli bir ödeme kaydı için ödedim beyanı verir (öğretmen teyidine gider)");

        group.MapGet("/teachers/{teacherUserId:guid}/payment-declarations", ListPaymentDeclarationsForTeacherAsync)
        .WithSummary("Öğretmene gelen veli ödeme beyanlarını listeler");

        group.MapPost("/payment-declarations/{declarationId:guid}/confirm", ConfirmPaymentDeclarationAsync)
        .WithSummary("Öğretmen veli ödeme beyanını teyit eder → kayıt tahsil edildi");

        group.MapPost("/payment-declarations/{declarationId:guid}/reject", RejectPaymentDeclarationAsync)
        .WithSummary("Öğretmen veli ödeme beyanını reddeder");
    }

    /// <summary>
    /// Veli, bir ödeme kaydı için "ödedim" beyanı verir (Veli V-G). Para transferi değil; öğretmen teyidine gider.
    /// </summary>
    private static async Task<IResult> DeclarePaymentPaidAsync(
        HttpContext context,
        Guid paymentRecordId,
        DeclarePaidRequest request,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var parentUserId))
        {
            return ApiErrorHttpResults.Unauthorized(context, "Beyanı veren veli belirlenemedi.");
        }

        var result = await dispatcher.Dispatch(
            new DeclarePaymentPaidCommand(parentUserId, paymentRecordId, request.DeclaredAmount, request.Note),
            cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmene gelen veli ödeme beyanlarını listeler (opsiyonel sadece bekleyenler).
    /// </summary>
    private static async Task<IResult> ListPaymentDeclarationsForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken,
        bool onlyPending = false)
    {
        var result = await dispatcher.Dispatch(new ListPaymentDeclarationsForTeacherQuery(teacherUserId, onlyPending), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen veli ödeme beyanını teyit eder; ödeme kaydı tam tahsil edilmiş işaretlenir (Veli V-G).
    /// </summary>
    private static async Task<IResult> ConfirmPaymentDeclarationAsync(
        HttpContext context,
        Guid declarationId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ConfirmPaymentDeclarationCommand(declarationId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen veli ödeme beyanını reddeder; ödeme kaydına dokunulmaz (Veli V-G).
    /// </summary>
    private static async Task<IResult> RejectPaymentDeclarationAsync(
        HttpContext context,
        Guid declarationId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new RejectPaymentDeclarationCommand(declarationId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen ve öğrenci ilişkisine bağlı beklenen, tahsil edilen ve vade bilgilerini içeren ödeme kaydı oluşturur.
    /// </summary>
    private static async Task<IResult> CreatePaymentRecordAsync(
        HttpContext context,
        UpsertPaymentRecordRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCreateCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Var olan ödeme kaydının tutar, durum, tahsilat tarihi ve dönem bilgilerini günceller.
    /// </summary>
    private static async Task<IResult> UpdatePaymentRecordAsync(
        HttpContext context,
        Guid paymentRecordId,
        UpsertPaymentRecordRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToUpdateCommand(paymentRecordId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Ödeme kaydı detayını ödeme kaydı kimliğiyle getirir.
    /// </summary>
    private static async Task<IResult> GetPaymentRecordByIdAsync(
        HttpContext context,
        Guid paymentRecordId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetPaymentRecordByIdQuery(paymentRecordId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmene ait ödeme kayıtlarını listeler; istenirse sadece açık alacakları döndürür.
    /// </summary>
    private static async Task<IResult> ListPaymentRecordsForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        bool outstandingOnly,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ListPaymentRecordsForTeacherQuery(teacherUserId, outstandingOnly), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin para birimi bazında tahsilat, açık alacak ve gecikmiş ödeme özetini getirir.
    /// </summary>
    private static async Task<IResult> GetTeacherPaymentSummaryAsync(
        HttpContext context,
        Guid teacherUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetTeacherPaymentSummaryQuery(teacherUserId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin ödeme kayıtlarını açık, gecikmiş, ödenmiş ve tarih aralığı kriterleriyle filtreler.
    /// </summary>
    private static async Task<IResult> ListFilteredPaymentRecordsForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        bool outstanding,
        bool overdue,
        bool paid,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListFilteredPaymentRecordsForTeacherQuery(
                teacherUserId,
                outstanding,
                overdue,
                paid,
                dateFromUtc,
                dateToUtc),
            cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin ödemelerinde metin araması, durum/öğrenci/tarih filtresi ve sayfalama (skip/take) uygular.
    /// </summary>
    private static async Task<IResult> SearchPaymentRecordsForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        string? q,
        string? status,
        Guid? studentId,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        int? skip,
        int? take,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new SearchPaymentRecordsForTeacherQuery(
                teacherUserId,
                q,
                status,
                studentId,
                dateFromUtc,
                dateToUtc,
                skip ?? 0,
                take ?? 20),
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
            "payments.record_not_found" or "payments.declaration_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "payments.declaration_not_pending" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>Velinin "ödedim" beyanını taşır (Veli V-G): beyan edilen tutar + opsiyonel not (ör. havale/elden).</summary>
public sealed record DeclarePaidRequest(decimal DeclaredAmount, string? Note);

/// <summary>
/// Ödeme kaydı oluşturma ve güncelleme işlemlerinde kullanılan tutar, vade, durum ve dönem bilgilerini taşır.
/// </summary>
public sealed record UpsertPaymentRecordRequest(
    Guid TeacherUserId,
    Guid StudentId,
    Guid? RelatedLessonSessionId,
    BillingItemType ItemType,
    string Description,
    string Currency,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    DateTime DueDateUtc,
    DateTime? CollectedOnUtc,
    PaymentStatus Status,
    DateTime? BillingPeriodStartUtc,
    DateTime? BillingPeriodEndUtc,
    string? Notes)
{
    public CreatePaymentRecordCommand ToCreateCommand()
    {
        return new CreatePaymentRecordCommand(
            TeacherUserId,
            StudentId,
            RelatedLessonSessionId,
            ItemType,
            Description,
            Currency,
            ExpectedAmount,
            CollectedAmount,
            DueDateUtc,
            CollectedOnUtc,
            Status,
            BillingPeriodStartUtc,
            BillingPeriodEndUtc,
            Notes);
    }

    public UpdatePaymentRecordCommand ToUpdateCommand(Guid paymentRecordId)
    {
        return new UpdatePaymentRecordCommand(
            paymentRecordId,
            RelatedLessonSessionId,
            ItemType,
            Description,
            Currency,
            ExpectedAmount,
            CollectedAmount,
            DueDateUtc,
            CollectedOnUtc,
            Status,
            BillingPeriodStartUtc,
            BillingPeriodEndUtc,
            Notes);
    }
}
