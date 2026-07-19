using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentsDbContext>(configuration, "Payments", PaymentsDbContext.SchemaName);
        services.AddScoped<IPaymentRecordRepository, PaymentRecordRepository>();
        services.AddScoped<ICommandHandler<CreatePaymentRecordCommand, Result<PaymentRecordResponse>>, CreatePaymentRecordCommandHandler>();
        services.AddScoped<ICommandHandler<UpdatePaymentRecordCommand, Result<PaymentRecordResponse>>, UpdatePaymentRecordCommandHandler>();
        services.AddScoped<IQueryHandler<GetPaymentRecordByIdQuery, Result<PaymentRecordResponse>>, GetPaymentRecordByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>, ListPaymentRecordsForTeacherQueryHandler>();
        services.AddScoped<IQueryHandler<ListFilteredPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>, ListFilteredPaymentRecordsForTeacherQueryHandler>();
        services.AddScoped<IQueryHandler<GetTeacherPaymentSummaryQuery, Result<TeacherPaymentSummaryResponse>>, GetTeacherPaymentSummaryQueryHandler>();
        services.AddScoped<IQueryHandler<SearchPaymentRecordsForTeacherQuery, Result<PagedPaymentRecordsResponse>>, SearchPaymentRecordsForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreatePaymentRecordCommand>, CreatePaymentRecordCommandValidator>();
        services.AddScoped<ICommandValidator<UpdatePaymentRecordCommand>, UpdatePaymentRecordCommandValidator>();
        services.AddScoped<IQueryValidator<GetPaymentRecordByIdQuery>, PaymentQueryValidator>();
        services.AddScoped<IQueryValidator<ListPaymentRecordsForTeacherQuery>, PaymentQueryValidator>();
        services.AddScoped<IQueryValidator<ListFilteredPaymentRecordsForTeacherQuery>, PaymentQueryValidator>();
        services.AddScoped<IQueryValidator<SearchPaymentRecordsForTeacherQuery>, PaymentQueryValidator>();
        services.AddScoped<IQueryValidator<GetTeacherPaymentSummaryQuery>, PaymentQueryValidator>();
        services.AddScoped<ICommandAuthorizer<CreatePaymentRecordCommand>, PaymentRecordAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdatePaymentRecordCommand>, PaymentRecordAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetPaymentRecordByIdQuery>, PaymentRecordAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListPaymentRecordsForTeacherQuery>, PaymentRecordAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListFilteredPaymentRecordsForTeacherQuery>, PaymentRecordAuthorizer>();
        services.AddScoped<IQueryAuthorizer<SearchPaymentRecordsForTeacherQuery>, PaymentRecordAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetTeacherPaymentSummaryQuery>, PaymentRecordAuthorizer>();

        // Ö-C: profil birleştirmede kaynak öğrenciye ait ödeme kayıtlarını kanonik öğrenciye taşır.
        services.AddScoped<IIntegrationEventHandler, PaymentsStudentMergedHandler>();

        // Veli "ödedim" beyanı (Veli V-G)
        services.AddScoped<IParentPaymentDeclarationRepository, ParentPaymentDeclarationRepository>();
        services.AddScoped<ICommandHandler<DeclarePaymentPaidCommand, Result<ParentPaymentDeclarationResponse>>, DeclarePaymentPaidCommandHandler>();
        services.AddScoped<ICommandHandler<ConfirmPaymentDeclarationCommand, Result<ParentPaymentDeclarationResponse>>, ConfirmPaymentDeclarationCommandHandler>();
        services.AddScoped<ICommandHandler<RejectPaymentDeclarationCommand, Result<ParentPaymentDeclarationResponse>>, RejectPaymentDeclarationCommandHandler>();
        services.AddScoped<IQueryHandler<ListPaymentDeclarationsForTeacherQuery, Result<IReadOnlyCollection<ParentPaymentDeclarationResponse>>>, ListPaymentDeclarationsForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<DeclarePaymentPaidCommand>, DeclarePaymentPaidCommandValidator>();
        services.AddScoped<ICommandAuthorizer<DeclarePaymentPaidCommand>, DeclarePaymentPaidCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ConfirmPaymentDeclarationCommand>, PaymentDeclarationResolveAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RejectPaymentDeclarationCommand>, PaymentDeclarationResolveAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListPaymentDeclarationsForTeacherQuery>, PaymentDeclarationResolveAuthorizer>();

        return services;
    }
}
