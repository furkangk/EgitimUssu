using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Students.API;

public sealed class StudentsModule : ModuleDefinition
{
    public override string Name => "Students";

    public override string RoutePrefix => "/api/students";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddStudentsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/profiles", CreateStudentProfileAsync)
        .WithSummary("Öğrenci profili oluşturur");

        group.MapGet("/profiles/{studentId:guid}", GetStudentProfileByIdAsync)
        .WithSummary("Öğrenci profilini getirir");

        group.MapGet("/profiles/by-user/{userId:guid}", GetStudentProfileByUserIdAsync)
        .WithSummary("Kullanıcıya ait öğrenci profilini getirir");

        group.MapGet("/profiles/by-teacher/{teacherUserId:guid}", ListStudentsByTeacherAsync)
        .WithSummary("Öğretmene bağlı öğrencileri listeler");
    }

    /// <summary>
    /// Öğrenciye ait temel profil, hedef, seviye ve ders alanı bilgilerini oluşturur.
    /// </summary>
    private static async Task<IResult> CreateStudentProfileAsync(
        HttpContext context,
        CreateStudentProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğrenci profilini öğrenci profil kimliği üzerinden getirir.
    /// </summary>
    private static async Task<IResult> GetStudentProfileByIdAsync(
        HttpContext context,
        Guid studentId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetStudentProfileByIdQuery(studentId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Uygulama kullanıcısına bağlı öğrenci profilini getirir.
    /// </summary>
    private static async Task<IResult> GetStudentProfileByUserIdAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetStudentProfileByUserIdQuery(userId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Belirli bir öğretmen tarafından oluşturulan veya yönetilen öğrenci profillerini listeler.
    /// </summary>
    private static async Task<IResult> ListStudentsByTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ListStudentsByTeacherQuery(teacherUserId), cancellationToken);
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
            "students.user_profile_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "students.profile_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Öğrencinin takip ettiği ders alanını ve hedef seviyesini belirtir.
/// </summary>
public sealed record StudentSubjectItem(string Subject, string? TargetLevel);

/// <summary>
/// Öğrenci profili oluşturmak için gerekli kimlik, iletişim, hedef ve ders bilgilerini taşır.
/// </summary>
public sealed record CreateStudentProfileRequest(
    Guid? UserId,
    Guid? CreatedByTeacherUserId,
    Guid? ParentUserId,
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    StudentOrigin Origin,
    IReadOnlyCollection<StudentSubjectItem> Subjects)
{
    public CreateStudentProfileCommand ToCommand()
    {
        return new CreateStudentProfileCommand(
            UserId,
            CreatedByTeacherUserId,
            ParentUserId,
            FullName,
            GradeLevel,
            ContactEmail,
            ContactPhone,
            GoalSummary,
            LevelNotes,
            Origin,
            Subjects.Select(subject => new StudentSubjectRequest(subject.Subject, subject.TargetLevel)).ToArray());
    }
}
