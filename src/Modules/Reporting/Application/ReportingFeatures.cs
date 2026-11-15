using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Application;

/// <summary>M14: Öğretmen panelinin sayaçlarını read-model'den (projeksiyon) okur.</summary>
public sealed record GetTeacherDashboardQuery(Guid TeacherUserId) : IQuery<Result<TeacherDashboardResponse>>;

public sealed record TeacherDashboardResponse(
    Guid TeacherUserId,
    int TodayLessonCount,
    int PendingAssignmentCount,
    int OverduePaymentCount,
    decimal OverdueAmountTotal,
    string OverdueCurrency,
    int ActiveStudentCount,
    DateTime UpdatedOnUtc);

public interface ITeacherDashboardProjectionReader
{
    Task<TeacherDashboardProjection?> GetAsync(Guid teacherUserId, CancellationToken cancellationToken);
}

public sealed class GetTeacherDashboardQueryHandler
    : IQueryHandler<GetTeacherDashboardQuery, Result<TeacherDashboardResponse>>
{
    /// <summary>Projeksiyon henüz beslenmemiş: sıfır döndürmek yalancı-yeşil olurdu, açıkça "hazır değil" denir.</summary>
    private static readonly Error NotReady = new(
        "reporting.dashboard_not_ready",
        "Bu öğretmen için panel verisi henüz oluşmadı.");

    private readonly ITeacherDashboardProjectionReader _reader;
    private readonly ILocalCalendar _calendar;

    public GetTeacherDashboardQueryHandler(ITeacherDashboardProjectionReader reader, ILocalCalendar calendar)
    {
        _reader = reader;
        _calendar = calendar;
    }

    public async Task<Result<TeacherDashboardResponse>> Handle(
        GetTeacherDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var projection = await _reader.GetAsync(query.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return Result<TeacherDashboardResponse>.Failure(NotReady);
        }

        var (startUtc, endUtc) = _calendar.DayRangeUtc(_calendar.Today());

        return Result<TeacherDashboardResponse>.Success(new TeacherDashboardResponse(
            projection.TeacherUserId,
            projection.CountLessonsBetween(startUtc, endUtc),
            projection.PendingAssignmentCount,
            projection.OverduePaymentCount,
            projection.OverdueAmountTotal,
            projection.OverdueCurrency,
            projection.ActiveStudentCount,
            projection.UpdatedOnUtc));
    }
}

/// <summary>Panel verisini yalnız öğretmenin kendisi veya Admin okuyabilir.</summary>
public sealed class ReportingAuthorizer : IQueryAuthorizer<GetTeacherDashboardQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu panel verisini görme yetkiniz yok.");

    private readonly ICurrentUser _currentUser;

    public ReportingAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(GetTeacherDashboardQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Task.FromResult(Result.Success());
        }

        var isOwner = Guid.TryParse(_currentUser.UserId, out var userId) && userId == query.TeacherUserId;
        return Task.FromResult(isOwner ? Result.Success() : Result.Failure(Forbidden));
    }
}
