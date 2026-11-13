using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Membership.API;

public sealed class MembershipModule : ModuleDefinition
{
    public override string Name => "Membership";

    public override string RoutePrefix => "/api/membership";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMembershipModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
            .WithSummary("Üyelik modül durumunu getirir");

        group.MapGet("/me", GetMyMembershipAsync)
            .WithSummary("Oturumdaki kullanıcının üyelik durumunu ve haklarını getirir")
            .RequireAuthorization("AuthenticatedUser");

        // M17-1: Paywall ekranı, kullanıcı giriş yapmadan da fiyatları göstermek zorundadır → anonim.
        group.MapGet("/plans", GetPlansAsync)
            .WithSummary("Satıştaki üyelik planlarını listeler (anonim)");

        var secure = CreateModuleGroup(endpoints);
        secure.RequireAuthorization("AuthenticatedUser");

        secure.MapPost("/subscriptions/start-trial", StartTrialAsync)
            .WithSummary("Seçilen planın ücretsiz denemesini başlatır (kullanıcı başına bir kez, M17-3)");
        secure.MapPost("/subscriptions/cancel", CancelSubscriptionAsync)
            .WithSummary("Aboneliği iptal eder; erişim dönem sonuna kadar sürer (M17-3)");
        secure.MapGet("/referral-code", GetReferralCodeAsync)
            .WithSummary("Kullanıcının referans kodunu getirir, yoksa üretir (M17-3)");
        secure.MapPost("/referral/redeem", RedeemReferralAsync)
            .WithSummary("Referans kodunu kullanır; her iki tarafa bir ay premium yazar (M17-3)");

        secure.MapPost("/plans", CreatePlanAsync)
            .WithSummary("Yeni üyelik planı tanımlar (yalnız Admin)");
        secure.MapPut("/plans/{planId:guid}", UpdatePlanAsync)
            .WithSummary("Planın fiyat/deneme/satış durumunu günceller (yalnız Admin)");
    }

    private static async Task<IResult> GetPlansAsync(
        HttpContext context, string? role, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(new ListPlansQuery(role), cancellationToken));

    private static async Task<IResult> StartTrialAsync(
        HttpContext context,
        StartTrialRequest? request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new StartTrialCommand(request?.PlanCode ?? string.Empty), cancellationToken));

    private static async Task<IResult> CancelSubscriptionAsync(
        HttpContext context, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new CancelSubscriptionCommand(), cancellationToken));

    /// <summary>
    /// Uç <c>GET</c>'tir ama bir <b>komut</b> gönderir: kod yoksa üretilip kaydedilir. İstemci için
    /// "kodumu getir" tek bir çağrıdır; ikinci bir "kod oluştur" adımı gereksiz karmaşa olurdu.
    /// </summary>
    private static async Task<IResult> GetReferralCodeAsync(
        HttpContext context, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new EnsureReferralCodeCommand(), cancellationToken));

    private static async Task<IResult> RedeemReferralAsync(
        HttpContext context,
        RedeemReferralRequest? request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new RedeemReferralCommand(request?.Code ?? string.Empty), cancellationToken));

    private static async Task<IResult> CreatePlanAsync(
        HttpContext context,
        CreatePlanRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new CreateSubscriptionPlanCommand(
                request.Code, request.Role, request.Tier, request.Period,
                request.Price, request.Currency, request.TrialDays, request.IsActive),
            cancellationToken));

    private static async Task<IResult> UpdatePlanAsync(
        HttpContext context,
        Guid planId,
        UpdatePlanRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new UpdateSubscriptionPlanCommand(
                planId, request.Price, request.Currency, request.TrialDays, request.IsActive),
            cancellationToken));

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                // Kaynak gerçekten yok: plan kodu tanınmıyor ya da iptal edilecek abonelik yok.
                "membership.plan_not_found" or "membership.subscription_not_found"
                    => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                // Kural çakışması (kod çift, hak zaten kullanılmış): 409.
                "membership.plan_code_conflict" or "membership.trial_already_used"
                    or "membership.referral_already_used"
                    => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };

    /// <summary>
    /// Üyelik modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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

    /// <summary>
    /// K-09: Oturumdaki kullanıcının etkin üyelik seviyesini, dönemini ve haklarını döner.
    /// İstemci (mobil paywall / reklam yerleşimi) bu ucu tek doğruluk kaynağı olarak okur.
    /// </summary>
    private static async Task<IResult> GetMyMembershipAsync(
        HttpContext context, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(new GetMyMembershipQuery(), cancellationToken));
}

/// <summary>Deneme başlatma isteği.</summary>
/// <param name="PlanCode">Denenecek planın kararlı kodu.</param>
public sealed record StartTrialRequest(string PlanCode);

/// <summary>Referans kodu kullanma isteği.</summary>
/// <param name="Code">Girilen kod.</param>
public sealed record RedeemReferralRequest(string Code);

/// <summary>Admin — yeni plan isteği.</summary>
/// <param name="Code">Kararlı plan kodu.</param>
/// <param name="Role">Hitap ettiği rol (<c>Teacher</c> | <c>Student</c> | <c>Parent</c>).</param>
/// <param name="Tier">Üyelik seviyesi (<c>Premium</c>).</param>
/// <param name="Period">Faturalama periyodu (<c>Monthly</c> | <c>Yearly</c>).</param>
/// <param name="Price">Fiyat.</param>
/// <param name="Currency">Para birimi; boşsa <c>TRY</c>.</param>
/// <param name="TrialDays">Ücretsiz deneme günü.</param>
/// <param name="IsActive">Satışta mı.</param>
public sealed record CreatePlanRequest(
    string Code, string Role, string Tier, string Period,
    decimal Price, string? Currency, int TrialDays, bool IsActive);

/// <summary>Admin — plan güncelleme isteği (kod, rol ve periyot değiştirilemez).</summary>
/// <param name="Price">Fiyat.</param>
/// <param name="Currency">Para birimi; boşsa mevcut korunur.</param>
/// <param name="TrialDays">Ücretsiz deneme günü.</param>
/// <param name="IsActive">Satışta mı.</param>
public sealed record UpdatePlanRequest(decimal Price, string? Currency, int TrialDays, bool IsActive);
