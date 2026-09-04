using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
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
    }

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


