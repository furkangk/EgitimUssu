using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStudentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudentsDbContext>(configuration, "Students", StudentsDbContext.SchemaName);
        services.AddScoped<IStudentProfileRepository, StudentProfileRepository>();
        services.AddScoped<ITeacherStudentLinkRepository, TeacherStudentLinkRepository>();
        services.AddScoped<IStudentDirectory, StudentDirectory>();
        // P15 T2 (K-13): öğretmen–öğrenci erişim kapsamının tek kaynağı.
        services.AddScoped<ITeacherStudentAccess, TeacherStudentAccess>();
        services.AddScoped<IMembershipDirectory, MembershipDirectory>();
        services.AddScoped<ICommandHandler<CreateStudentProfileCommand, Result<StudentProfileResponse>>, CreateStudentProfileCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByUserIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByUserIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListStudentsByTeacherQuery, Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>, ListStudentsByTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateStudentProfileCommand>, CreateStudentProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateStudentProfileCommand>, CreateStudentProfileCommandAuthorizer>();
        services.AddScoped<ICommandHandler<UpdateStudentProfileCommand, Result<StudentProfileResponse>>, UpdateStudentProfileCommandHandler>();
        services.AddScoped<ICommandValidator<UpdateStudentProfileCommand>, UpdateStudentProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<UpdateStudentProfileCommand>, UpdateStudentProfileCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByUserIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListStudentsByTeacherQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<ICommandHandler<ArchiveTeacherStudentLinkCommand, Result>, ArchiveTeacherStudentLinkCommandHandler>();
        services.AddScoped<ICommandHandler<SetTeacherStudentRateCommand, Result>, SetTeacherStudentRateCommandHandler>();
        services.AddScoped<ICommandAuthorizer<ArchiveTeacherStudentLinkCommand>, TeacherStudentLinkAuthorizer>();
        services.AddScoped<ICommandAuthorizer<SetTeacherStudentRateCommand>, TeacherStudentLinkAuthorizer>();
        services.AddScoped<ICommandHandler<InviteStudentCommand, Result>, InviteStudentCommandHandler>();
        services.AddScoped<ICommandHandler<AcceptTeacherStudentLinkCommand, Result>, AcceptTeacherStudentLinkCommandHandler>();
        services.AddScoped<ICommandHandler<RejectTeacherStudentLinkCommand, Result>, RejectTeacherStudentLinkCommandHandler>();
        services.AddScoped<ICommandHandler<ClaimStudentLinkCommand, Result>, ClaimStudentLinkCommandHandler>();
        services.AddScoped<ICommandAuthorizer<InviteStudentCommand>, TeacherStudentLinkAuthorizer>();
        services.AddScoped<ICommandAuthorizer<AcceptTeacherStudentLinkCommand>, TeacherStudentLinkResponseAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RejectTeacherStudentLinkCommand>, TeacherStudentLinkResponseAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ClaimStudentLinkCommand>, TeacherStudentLinkResponseAuthorizer>();
        // M03-1/M03-2/M03-3: branş yönetimi, veli bağlama ve veli çocuk listesi
        services.AddScoped<ICommandHandler<AddStudentSubjectCommand, Result<StudentSubjectResponse>>, AddStudentSubjectCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveStudentSubjectCommand, Result>, RemoveStudentSubjectCommandHandler>();
        services.AddScoped<ICommandAuthorizer<AddStudentSubjectCommand>, StudentSubjectCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RemoveStudentSubjectCommand>, StudentSubjectCommandAuthorizer>();
        services.AddScoped<ICommandHandler<LinkStudentParentCommand, Result<StudentParentLinkResponse>>, LinkStudentParentCommandHandler>();
        services.AddScoped<ICommandAuthorizer<LinkStudentParentCommand>, LinkStudentParentCommandAuthorizer>();
        services.AddScoped<IQueryHandler<ListStudentsByParentQuery, Result<IReadOnlyCollection<ParentChildSummaryResponse>>>, ListStudentsByParentQueryHandler>();
        services.AddScoped<IQueryAuthorizer<ListStudentsByParentQuery>, StudentProfileQueryAuthorizer>();

        services.AddScoped<EgitimUssu.Shared.Infrastructure.Messaging.IIntegrationEventHandler, ParentChildLinkApprovedIntegrationEventHandler>();

        // Veli davet kodu (Veli V-D)
        services.AddScoped<IStudentParentInviteRepository, StudentParentInviteRepository>();
        services.AddScoped<IParentInviteDirectory, ParentInviteDirectory>();
        services.AddScoped<ICommandHandler<CreateParentInviteCommand, Result<ParentInviteResponse>>, CreateParentInviteCommandHandler>();
        services.AddScoped<ICommandValidator<CreateParentInviteCommand>, CreateParentInviteCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateParentInviteCommand>, CreateParentInviteCommandAuthorizer>();
        // E-07: KVKK hesap silme — bu modüldeki kişisel veriyi anonimleştirir/siler.
        services.AddScoped<IPersonalDataAnonymizer, StudentsPersonalDataAnonymizer>();


        return services;
    }
}
