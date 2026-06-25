using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EgitimUssu.API.Host;

public static class TeacherDashboardEndpoints
{
    public static IEndpointRouteBuilder MapTeacherDashboard(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/teachers/profiles/{teacherUserId:guid}/dashboard-summary",
                GetTeacherDashboardSummaryAsync)
            .RequireAuthorization("AuthenticatedUser")
            .WithSummary("Öğretmen paneli özeti: bugünkü dersler, bekleyen ödevler ve geciken ödemeler");
        return endpoints;
    }

    private static async Task<IResult> GetTeacherDashboardSummaryAsync(
        HttpContext context,
        Guid teacherUserId,
        IQueryDispatcher queryDispatcher,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return ApiErrorHttpResults.Unauthorized(context, "Bu kaynağa erişmek için kimlik doğrulama gerekiyor.");
        }

        var isAdmin = currentUser.Roles.Contains("Admin");
        var isOwner = Guid.TryParse(currentUser.UserId, out var currentUserId) && currentUserId == teacherUserId;
        if (!isAdmin && !isOwner)
        {
            return ApiErrorHttpResults.Forbidden(context, "Bu işlemi yapma yetkiniz yok.");
        }

        var todayUtc = DateTime.SpecifyKind(clock.UtcNow.Date, DateTimeKind.Utc);

        var todayLessonsTask = queryDispatcher.Dispatch(
            new ListLessonSchedulesForTeacherQuery(teacherUserId, todayUtc, todayUtc.AddDays(1)),
            cancellationToken);
        var assignmentsTask = queryDispatcher.Dispatch(
            new ListAssignmentsQuery(teacherUserId, null, null),
            cancellationToken);
        var overduePaymentsTask = queryDispatcher.Dispatch(
            new ListFilteredPaymentRecordsForTeacherQuery(teacherUserId, false, true, false, null, null),
            cancellationToken);

        await Task.WhenAll(todayLessonsTask, assignmentsTask, overduePaymentsTask);

        var lessonsResult = todayLessonsTask.Result;
        var assignmentsResult = assignmentsTask.Result;
        var paymentsResult = overduePaymentsTask.Result;

        if (lessonsResult.IsFailure)
        {
            return ApiErrorHttpResults.FromError(context, StatusCodes.Status502BadGateway, lessonsResult.Error);
        }
        if (assignmentsResult.IsFailure)
        {
            return ApiErrorHttpResults.FromError(context, StatusCodes.Status502BadGateway, assignmentsResult.Error);
        }
        if (paymentsResult.IsFailure)
        {
            return ApiErrorHttpResults.FromError(context, StatusCodes.Status502BadGateway, paymentsResult.Error);
        }

        var todayLessons = lessonsResult.Value!
            .Where(l => l.Status != "Cancelled")
            .OrderBy(l => l.StartAtUtc)
            .Select(l => new DashboardTodayLessonItem(l.Id, l.StudentId, l.Subject, l.LessonFormat, l.StartAtUtc, l.EndAtUtc, l.LocationLabel))
            .ToArray();

        var pendingAssignments = assignmentsResult.Value!
            .Where(a => a.Status == "Pending")
            .OrderBy(a => a.DueDateUtc)
            .Select(a => new DashboardPendingAssignmentItem(a.Id, a.StudentId, a.TeacherUserId, a.Title, a.DueDateUtc))
            .ToArray();

        var overduePayments = paymentsResult.Value!
            .OrderBy(p => p.DueDateUtc)
            .Select(p => new DashboardOverduePaymentItem(p.Id, p.StudentId, p.Description, p.Currency, p.OutstandingAmount, p.DueDateUtc))
            .ToArray();

        var firstCurrency = overduePayments.FirstOrDefault()?.Currency ?? "TRY";
        var overdueTotal = overduePayments.Sum(p => p.OutstandingAmount);

        return Results.Ok(new TeacherDashboardSummaryResponse(
            TodayLessons: todayLessons,
            PendingAssignmentsCount: pendingAssignments.Length,
            PendingAssignments: pendingAssignments,
            OverduePaymentsCount: overduePayments.Length,
            OverduePaymentsCurrency: firstCurrency,
            OverduePaymentsTotal: overdueTotal,
            OverduePayments: overduePayments));
    }
}

public sealed record TeacherDashboardSummaryResponse(
    IReadOnlyCollection<DashboardTodayLessonItem> TodayLessons,
    int PendingAssignmentsCount,
    IReadOnlyCollection<DashboardPendingAssignmentItem> PendingAssignments,
    int OverduePaymentsCount,
    string OverduePaymentsCurrency,
    decimal OverduePaymentsTotal,
    IReadOnlyCollection<DashboardOverduePaymentItem> OverduePayments);

public sealed record DashboardTodayLessonItem(
    Guid Id,
    Guid StudentId,
    string Subject,
    string LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? LocationLabel);

public sealed record DashboardPendingAssignmentItem(
    Guid Id,
    Guid StudentId,
    Guid TeacherUserId,
    string Title,
    DateTime? DueDateUtc);

public sealed record DashboardOverduePaymentItem(
    Guid Id,
    Guid StudentId,
    string Description,
    string Currency,
    decimal OutstandingAmount,
    DateTime DueDateUtc);
