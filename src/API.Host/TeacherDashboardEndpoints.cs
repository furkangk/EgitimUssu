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
        ILocalCalendar calendar,
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

        // P16 T5 / N-11 (K-19): "bugün" Türkiye günüdür — 00:00–03:00 TR dersleri bugünde, ertesi günün dersleri değil.
        var (todayStartUtc, todayEndUtc) = calendar.DayRangeUtc(calendar.Today());

        // Liste penceresi iki ucu dahil sayar ve tekil dersi kesişimle eşler; o yüzden pencere bir tick erken kapanır ve
        // sonuç aşağıda başlangıca göre süzülür (dün başlayıp bugüne taşan ya da tam gece yarısı biten ders "bugün" değil).
        var todayLessonsTask = queryDispatcher.Dispatch(
            new ListLessonSchedulesForTeacherQuery(teacherUserId, todayStartUtc, todayEndUtc.AddTicks(-1)),
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
            .Where(l => l.Status != "Cancelled" && l.StartAtUtc >= todayStartUtc && l.StartAtUtc < todayEndUtc)
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

        // P16 T5 / N-11 (K-23): farklı para birimleri toplanmaz. Liste en erken vadeli para biriminden başlar; eski tekil
        // alanlar (mobil okuyor) listenin ilk satırını taşır — artık yalnız o para biriminin toplamı.
        var overdueTotals = overduePayments
            .GroupBy(p => p.Currency)
            .OrderBy(group => group.Min(p => p.DueDateUtc))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new DashboardOverdueCurrencyTotal(group.Key, group.Count(), group.Sum(p => p.OutstandingAmount)))
            .ToArray();
        var primary = overdueTotals.FirstOrDefault();

        return Results.Ok(new TeacherDashboardSummaryResponse(
            TodayLessons: todayLessons,
            PendingAssignmentsCount: pendingAssignments.Length,
            PendingAssignments: pendingAssignments,
            OverduePaymentsCount: overduePayments.Length,
            OverduePaymentsCurrency: primary?.Currency ?? "TRY",
            OverduePaymentsTotal: primary?.Total ?? 0m,
            OverduePayments: overduePayments,
            OverduePaymentTotals: overdueTotals));
    }
}

/// <summary>
/// Öğretmen paneli özeti. P16 T5 / N-11: <c>OverduePaymentTotals</c> para birimi bazında gecikmiş toplamlardır (en erken
/// vadeli para birimi önce). <c>OverduePaymentsCurrency</c> + <c>OverduePaymentsTotal</c> eski tekil alanlardır (geriye
/// uyum — mobil okuyor, kullanımdan kalkacak): listenin ilk satırını taşır, yani yalnız o para biriminin toplamı (P16
/// T5'ten önce farklı para birimlerini topluyordu).
/// </summary>
public sealed record TeacherDashboardSummaryResponse(
    IReadOnlyCollection<DashboardTodayLessonItem> TodayLessons,
    int PendingAssignmentsCount,
    IReadOnlyCollection<DashboardPendingAssignmentItem> PendingAssignments,
    int OverduePaymentsCount,
    string OverduePaymentsCurrency,
    decimal OverduePaymentsTotal,
    IReadOnlyCollection<DashboardOverduePaymentItem> OverduePayments,
    IReadOnlyCollection<DashboardOverdueCurrencyTotal> OverduePaymentTotals);

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

/// <summary>P16 T5 / N-11: bir para biriminin gecikmiş ödeme sayısı ve kalan toplamı.</summary>
public sealed record DashboardOverdueCurrencyTotal(
    string Currency,
    int Count,
    decimal Total);
