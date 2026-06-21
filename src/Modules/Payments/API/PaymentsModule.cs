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

        group.MapPost("/records", async (
            HttpContext context,
            UpsertPaymentRecordRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToCreateCommand(), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPut("/records/{paymentRecordId:guid}", async (
            HttpContext context,
            Guid paymentRecordId,
            UpsertPaymentRecordRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToUpdateCommand(paymentRecordId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/records/{paymentRecordId:guid}", async (
            HttpContext context,
            Guid paymentRecordId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetPaymentRecordByIdQuery(paymentRecordId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/teachers/{teacherUserId:guid}/records", async (
            HttpContext context,
            Guid teacherUserId,
            bool outstandingOnly,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new ListPaymentRecordsForTeacherQuery(teacherUserId, outstandingOnly), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/teachers/{teacherUserId:guid}/summary", async (
            HttpContext context,
            Guid teacherUserId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetTeacherPaymentSummaryQuery(teacherUserId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/teachers/{teacherUserId:guid}/records/filter", async (
            HttpContext context,
            Guid teacherUserId,
            bool outstanding,
            bool overdue,
            bool paid,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
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
        });
    }

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "payments.record_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

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
