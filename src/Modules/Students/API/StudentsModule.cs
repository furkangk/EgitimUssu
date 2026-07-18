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

        group.MapPut("/profiles/{studentId:guid}", UpdateStudentProfileAsync)
        .WithSummary("Öğrenci profilini günceller veya pasifleştirir");

        group.MapPost("/teachers/{teacherUserId:guid}/students/{studentId:guid}/archive", ArchiveStudentAsync)
        .WithSummary("Öğretmen-öğrenci bağlantısını arşivler");

        group.MapPost("/teachers/{teacherUserId:guid}/students/{studentId:guid}/unarchive", UnarchiveStudentAsync)
        .WithSummary("Öğretmen-öğrenci bağlantısını arşivden çıkarır");

        group.MapPut("/teachers/{teacherUserId:guid}/students/{studentId:guid}/rate", SetStudentRateAsync)
        .WithSummary("Öğrenci bazlı anlaşılan ücreti günceller");

        group.MapPost("/teachers/{teacherUserId:guid}/students/{studentId:guid}/invite", InviteStudentAsync)
        .WithSummary("Öğrenciyi gerçek kullanıcı hesabına bağlanmaya davet eder");

        group.MapPost("/links/{linkId:guid}/accept", AcceptLinkAsync)
        .WithSummary("Öğretmen davetini kabul eder (öğrenci)");

        group.MapPost("/links/{linkId:guid}/reject", RejectLinkAsync)
        .WithSummary("Öğretmen davetini reddeder (öğrenci)");
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
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var result = await dispatcher.Dispatch(new ListStudentsByTeacherQuery(teacherUserId, includeArchived), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Mevcut öğrenci profilinin bilgilerini ve aktiflik durumunu günceller.
    /// </summary>
    private static async Task<IResult> UpdateStudentProfileAsync(
        HttpContext context,
        Guid studentId,
        UpdateStudentProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(studentId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen-öğrenci bağlantısını arşivler (öğrenci listeden gizlenir, limit sayımını etkilemez).
    /// </summary>
    private static async Task<IResult> ArchiveStudentAsync(
        HttpContext context,
        Guid teacherUserId,
        Guid studentId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ArchiveTeacherStudentLinkCommand(teacherUserId, studentId, true), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    /// <summary>
    /// Öğretmen-öğrenci bağlantısını arşivden çıkarır.
    /// </summary>
    private static async Task<IResult> UnarchiveStudentAsync(
        HttpContext context,
        Guid teacherUserId,
        Guid studentId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ArchiveTeacherStudentLinkCommand(teacherUserId, studentId, false), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    /// <summary>
    /// Öğretmenin belirli bir öğrenci için anlaştığı ders ücretini günceller (B-07).
    /// </summary>
    private static async Task<IResult> SetStudentRateAsync(
        HttpContext context,
        Guid teacherUserId,
        Guid studentId,
        SetStudentRateRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new SetTeacherStudentRateCommand(teacherUserId, studentId, request.AgreedRateAmount, request.Currency),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    /// <summary>
    /// Öğrenciyi gerçek kullanıcı hesabına bağlanmaya davet eder (B-06). İsteğe bağlı hedef kullanıcı belirtilir.
    /// </summary>
    private static async Task<IResult> InviteStudentAsync(
        HttpContext context,
        Guid teacherUserId,
        Guid studentId,
        InviteStudentRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new InviteStudentCommand(teacherUserId, studentId, request.TargetUserId),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    /// <summary>
    /// Oturum açmış öğrenci kullanıcısı öğretmen davetini kabul eder ve profiline bağlanır.
    /// </summary>
    private static async Task<IResult> AcceptLinkAsync(
        HttpContext context,
        Guid linkId,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var acceptingUserId))
        {
            return ApiErrorHttpResults.Unauthorized(context, "Daveti yanıtlayan kullanıcı belirlenemedi.");
        }

        var result = await dispatcher.Dispatch(new AcceptTeacherStudentLinkCommand(linkId, acceptingUserId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    /// <summary>
    /// Oturum açmış öğrenci kullanıcısı öğretmen davetini reddeder.
    /// </summary>
    private static async Task<IResult> RejectLinkAsync(
        HttpContext context,
        Guid linkId,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var rejectingUserId))
        {
            return ApiErrorHttpResults.Unauthorized(context, "Daveti yanıtlayan kullanıcı belirlenemedi.");
        }

        var result = await dispatcher.Dispatch(new RejectTeacherStudentLinkCommand(linkId, rejectingUserId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : MapLinkError(context, result);
    }

    private static IResult MapLinkError(HttpContext context, Result result)
        => result.Error.Code switch
        {
            "students.link_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "students.user_profile_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "students.free_limit_reached" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
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
/// Öğretmen-öğrenci bağlantısı için anlaşılan ders ücretini taşır (B-07).
/// </summary>
public sealed record SetStudentRateRequest(decimal AgreedRateAmount, string Currency);

/// <summary>
/// Öğrenci davetini taşır (B-06). Hedef kullanıcı isteğe bağlı; belirtilmezse açık davet olur.
/// </summary>
public sealed record InviteStudentRequest(Guid? TargetUserId);

/// <summary>
/// Mevcut öğrenci profilini güncellemek için gerekli alanları ve aktiflik durumunu taşır.
/// </summary>
public sealed record UpdateStudentProfileRequest(
    string FullName,
    string GradeLevel,
    string? ContactEmail,
    string? ContactPhone,
    string? GoalSummary,
    string? LevelNotes,
    bool IsActive,
    IReadOnlyCollection<StudentSubjectItem> Subjects)
{
    public UpdateStudentProfileCommand ToCommand(Guid studentId)
    {
        return new UpdateStudentProfileCommand(
            studentId,
            FullName,
            GradeLevel,
            ContactEmail,
            ContactPhone,
            GoalSummary,
            LevelNotes,
            IsActive,
            Subjects.Select(s => new StudentSubjectRequest(s.Subject, s.TargetLevel)).ToArray());
    }
}

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
