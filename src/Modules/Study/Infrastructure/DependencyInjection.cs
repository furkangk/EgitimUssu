using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Study.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStudyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudyDbContext>(configuration, "Study", StudyDbContext.SchemaName);
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddScoped<EgitimUssu.Shared.Contracts.IStudyDigestDirectory, StudyDigestDirectory>();
        services.AddScoped<EgitimUssu.Shared.Contracts.IStudyPlanCompletionReader, StudyPlanCompletionReader>();

        // Uygulama servisleri
        services.AddScoped<StudyOwnershipGuard>();
        services.AddScoped<StudyLinkResolver>();
        services.AddScoped<StudyMembershipResolver>();
        services.AddScoped<AchievementEvaluator>();
        services.AddScoped<StudyCompletionService>();

        // Öğrenci-kapsamlı istekleri koruyan kapalı-generik sahiplik yetkilendiricileri
        // (AuthorizationCoverageValidator kapalı servis tipi bekler).
        AddStudentScopedCommandAuthorizer<StartStudySessionCommand>(services);
        AddStudentScopedCommandAuthorizer<CreateManualStudySessionCommand>(services);
        AddStudentScopedCommandAuthorizer<RecordTestResultCommand>(services);
        AddStudentScopedCommandAuthorizer<CreateMockExamCommand>(services);
        AddStudentScopedCommandAuthorizer<UpdateStudyGoalsCommand>(services);
        AddStudentScopedCommandAuthorizer<UpdateStudySharingCommand>(services);
        AddStudentScopedCommandAuthorizer<CreateSubjectCatalogCommand>(services);
        AddStudentScopedCommandAuthorizer<CreateStudyNoteCommand>(services);

        AddStudentScopedQueryAuthorizer<ListSubjectCatalogQuery>(services);
        AddStudentScopedQueryAuthorizer<ListStudyNotesQuery>(services);
        AddStudentScopedQueryAuthorizer<GetActiveSessionQuery>(services);
        AddStudentScopedQueryAuthorizer<ListStudySessionsQuery>(services);
        AddStudentScopedQueryAuthorizer<WeeklySummaryQuery>(services);
        AddStudentScopedQueryAuthorizer<ListTestResultsQuery>(services);
        AddStudentScopedQueryAuthorizer<NetTrendQuery>(services);
        AddStudentScopedQueryAuthorizer<GetStudyGoalsQuery>(services);
        AddStudentScopedQueryAuthorizer<GetStreakQuery>(services);
        AddStudentScopedQueryAuthorizer<GetAchievementsQuery>(services);
        AddStudentScopedQueryAuthorizer<GetStudySharingQuery>(services);
        AddStudentScopedQueryAuthorizer<GetStudyDashboardQuery>(services);

        // Kimliği sessionId/testResultId olan istekler için özel sahiplik yetkilendiricileri
        services.AddScoped<ICommandAuthorizer<PauseStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ResumeStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CompleteStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RecoverStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DiscardStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<EditStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteStudySessionCommand>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudySessionQuery>, StudySessionOwnershipAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetTestResultQuery>, StudyTestOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<EditTestResultCommand>, StudyTestOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteTestResultCommand>, StudyTestOwnershipAuthorizer>();

        // Katalog: kimliği subjectId/topicId olan istekler için sahiplik yetkilendiricileri
        services.AddScoped<ICommandAuthorizer<UpdateSubjectCatalogCommand>, StudyCatalogSubjectOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteSubjectCatalogCommand>, StudyCatalogSubjectOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<AddTopicCatalogCommand>, StudyCatalogSubjectOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateTopicCatalogCommand>, StudyCatalogTopicOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteTopicCatalogCommand>, StudyCatalogTopicOwnershipAuthorizer>();

        // Not: güncelleme/silme noteId üzerinden sahiplik
        services.AddScoped<ICommandAuthorizer<UpdateStudyNoteCommand>, StudyNoteOwnershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteStudyNoteCommand>, StudyNoteOwnershipAuthorizer>();

        // Validator'lar
        services.AddScoped<ICommandValidator<StartStudySessionCommand>, StartStudySessionCommandValidator>();
        services.AddScoped<ICommandValidator<CreateManualStudySessionCommand>, CreateManualStudySessionCommandValidator>();
        services.AddScoped<ICommandValidator<RecordTestResultCommand>, RecordTestResultCommandValidator>();

        // Seans komutları
        services.AddScoped<ICommandHandler<StartStudySessionCommand, Result<StudySessionResponse>>, StartStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<CreateManualStudySessionCommand, Result<StudySessionResponse>>, CreateManualStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<PauseStudySessionCommand, Result<StudySessionResponse>>, PauseStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<ResumeStudySessionCommand, Result<StudySessionResponse>>, ResumeStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteStudySessionCommand, Result<StudySessionResponse>>, CompleteStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<RecoverStudySessionCommand, Result<StudySessionResponse>>, RecoverStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<DiscardStudySessionCommand, Result<StudySessionResponse>>, DiscardStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<EditStudySessionCommand, Result<StudySessionResponse>>, EditStudySessionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteStudySessionCommand, Result<bool>>, DeleteStudySessionCommandHandler>();

        // Seans sorguları
        services.AddScoped<IQueryHandler<GetStudySessionQuery, Result<StudySessionResponse>>, GetStudySessionQueryHandler>();
        services.AddScoped<IQueryHandler<GetActiveSessionQuery, Result<ActiveSessionResponse?>>, GetActiveSessionQueryHandler>();
        services.AddScoped<IQueryHandler<ListStudySessionsQuery, Result<IReadOnlyCollection<StudySessionResponse>>>, ListStudySessionsQueryHandler>();
        services.AddScoped<IQueryHandler<WeeklySummaryQuery, Result<WeeklySummaryResponse>>, WeeklySummaryQueryHandler>();

        // Test komut/sorguları
        services.AddScoped<ICommandHandler<RecordTestResultCommand, Result<TestResultResponse>>, RecordTestResultCommandHandler>();
        services.AddScoped<ICommandHandler<EditTestResultCommand, Result<TestResultResponse>>, EditTestResultCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTestResultCommand, Result<bool>>, DeleteTestResultCommandHandler>();
        services.AddScoped<ICommandHandler<CreateMockExamCommand, Result<MockExamResponse>>, CreateMockExamCommandHandler>();
        services.AddScoped<IQueryHandler<GetTestResultQuery, Result<TestResultResponse>>, GetTestResultQueryHandler>();
        services.AddScoped<IQueryHandler<ListTestResultsQuery, Result<IReadOnlyCollection<TestResultResponse>>>, ListTestResultsQueryHandler>();
        services.AddScoped<IQueryHandler<NetTrendQuery, Result<NetTrendResponse>>, NetTrendQueryHandler>();

        // Hedef / streak / başarım / paylaşım / dashboard
        services.AddScoped<IQueryHandler<GetStudyGoalsQuery, Result<StudyGoalResponse?>>, GetStudyGoalsQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateStudyGoalsCommand, Result<StudyGoalResponse>>, UpdateStudyGoalsCommandHandler>();
        services.AddScoped<IQueryHandler<GetStreakQuery, Result<StreakResponse>>, GetStreakQueryHandler>();
        services.AddScoped<IQueryHandler<GetAchievementsQuery, Result<IReadOnlyCollection<AchievementResponse>>>, GetAchievementsQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudySharingQuery, Result<StudySharingResponse>>, GetStudySharingQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateStudySharingCommand, Result<StudySharingResponse>>, UpdateStudySharingCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudyDashboardQuery, Result<StudyDashboardResponse>>, GetStudyDashboardQueryHandler>();

        // Ders/konu kataloğu
        services.AddScoped<IQueryHandler<ListSubjectCatalogQuery, Result<IReadOnlyCollection<SubjectCatalogResponse>>>, ListSubjectCatalogQueryHandler>();
        services.AddScoped<ICommandHandler<CreateSubjectCatalogCommand, Result<SubjectCatalogResponse>>, CreateSubjectCatalogCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateSubjectCatalogCommand, Result<SubjectCatalogResponse>>, UpdateSubjectCatalogCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteSubjectCatalogCommand, Result<bool>>, DeleteSubjectCatalogCommandHandler>();
        services.AddScoped<ICommandHandler<AddTopicCatalogCommand, Result<TopicCatalogResponse>>, AddTopicCatalogCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTopicCatalogCommand, Result<TopicCatalogResponse>>, UpdateTopicCatalogCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTopicCatalogCommand, Result<bool>>, DeleteTopicCatalogCommandHandler>();

        // Öğrenci ders notları
        services.AddScoped<IQueryHandler<ListStudyNotesQuery, Result<IReadOnlyCollection<StudyNoteResponse>>>, ListStudyNotesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateStudyNoteCommand, Result<StudyNoteResponse>>, CreateStudyNoteCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateStudyNoteCommand, Result<StudyNoteResponse>>, UpdateStudyNoteCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteStudyNoteCommand, Result<bool>>, DeleteStudyNoteCommandHandler>();

        // Ö-C: profil birleştirmede kaynak öğrenciye ait tüm çalışma verisini kanonik öğrenciye taşır.
        services.AddScoped<IIntegrationEventHandler, StudyStudentMergedHandler>();

        return services;
    }

    private static void AddStudentScopedCommandAuthorizer<TCommand>(IServiceCollection services)
        where TCommand : IStudentScopedRequest =>
        services.AddScoped<ICommandAuthorizer<TCommand>, StudyOwnershipCommandAuthorizer<TCommand>>();

    private static void AddStudentScopedQueryAuthorizer<TQuery>(IServiceCollection services)
        where TQuery : IStudentScopedRequest =>
        services.AddScoped<IQueryAuthorizer<TQuery>, StudyOwnershipQueryAuthorizer<TQuery>>();
}
