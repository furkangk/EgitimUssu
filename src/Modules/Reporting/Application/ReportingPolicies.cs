using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Application;

/// <summary>M14: Rapor verisinin izleyiciye ne kadar açılacağı (M15-4 gizlilik seviyelerinin rapor karşılığı).</summary>
public enum ReportVisibility
{
    /// <summary>Tam veri (öğrencinin kendisi, Admin ya da paylaşımı açık Standard).</summary>
    Full = 1,

    /// <summary>Yalnız toplamlar; seans/test kırılımı gizli.</summary>
    Limited = 2,

    /// <summary>Hiçbir çalışma verisi paylaşılmaz.</summary>
    Hidden = 3
}

/// <summary>
/// M14 öğrenci raporlarının gizlilik kapısı. Yetkilendirici "bu kişi bakabilir mi?" sorusunu (403) yanıtlar;
/// bu kapı "ne kadarını görebilir?" sorusunu yanıtlar — gizlilik kapalıyken 403 değil <b>boş sonuç</b> döner
/// (P08 Task 2'deki <c>ProgressPrivacyFilter</c> ile aynı ayrım, Öğrenilenler #106).
/// <para>
/// Öğrencinin kendisi ve Admin filtreden geçmez. Diğerleri için M15-4 <see cref="StudentPrivacy"/> okunur;
/// veli ve öğretmen bayrakları ayrıdır, izleyicinin hangisi olduğu rolünden belirlenir.
/// </para>
/// </summary>
public sealed class StudentReportPrivacyGate
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _students;
    private readonly IStudentPrivacyDirectory _privacy;

    public StudentReportPrivacyGate(
        ICurrentUser currentUser, IStudentDirectory students, IStudentPrivacyDirectory privacy)
    {
        _currentUser = currentUser;
        _students = students;
        _privacy = privacy;
    }

    public async Task<ReportVisibility> ResolveAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (_currentUser.Roles.Contains("Admin"))
        {
            return ReportVisibility.Full;
        }

        var ownerUserId = await _students.GetOwnerUserIdAsync(studentId, cancellationToken);
        if (ownerUserId is not { } studentUserId)
        {
            // Bağlı kullanıcı yok → ayar kaydı da yok; sözleşme gereği paylaşım açık.
            return ReportVisibility.Full;
        }

        if (Guid.TryParse(_currentUser.UserId, out var userId) && userId == studentUserId)
        {
            return ReportVisibility.Full;   // öğrenci kendi verisini her zaman tam görür
        }

        var privacy = await _privacy.GetForUserAsync(studentUserId, cancellationToken);
        var isShared = _currentUser.Roles.Contains("Parent")
            ? privacy.ShareStudyDataWithParent
            : privacy.ShareStudyDataWithTeacher;

        if (!isShared || privacy.Level == StudyPrivacyLevel.Hidden)
        {
            return ReportVisibility.Hidden;
        }

        return privacy.Level == StudyPrivacyLevel.Limited ? ReportVisibility.Limited : ReportVisibility.Full;
    }
}

/// <summary>M14: Öğretmen-kapsamlı raporu yalnız öğretmenin kendisi veya Admin okuyabilir.</summary>
public sealed class TeacherScopedReportAuthorizer<TQuery> : IQueryAuthorizer<TQuery>
    where TQuery : ITeacherScopedReportRequest
{
    private readonly ICurrentUser _currentUser;

    public TeacherScopedReportAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(TQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(ReportingErrors.Forbidden));
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Task.FromResult(Result.Success());
        }

        var isOwner = Guid.TryParse(_currentUser.UserId, out var userId) && userId == query.TeacherUserId;
        return Task.FromResult(isOwner ? Result.Success() : Result.Failure(ReportingErrors.Forbidden));
    }
}

/// <summary>
/// M14: Öğrenci-kapsamlı raporu öğrencinin kendisi, <b>bağlı</b> öğretmeni, <b>onaylı</b> velisi veya Admin okur.
/// Bağ doğrulaması modül sınırını korur: <see cref="IStudentDirectory"/> ve <see cref="IParentAccessDirectory"/>
/// sözleşmeleri kullanılır, Reporting başka bir modülün DbContext'ine bakmaz.
/// </summary>
public sealed class StudentScopedReportAuthorizer<TQuery> : IQueryAuthorizer<TQuery>
    where TQuery : IStudentScopedReportRequest
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _students;
    private readonly IParentAccessDirectory _parents;

    public StudentScopedReportAuthorizer(
        ICurrentUser currentUser, IStudentDirectory students, IParentAccessDirectory parents)
    {
        _currentUser = currentUser;
        _students = students;
        _parents = parents;
    }

    public async Task<Result> Authorize(TQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(ReportingErrors.Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(ReportingErrors.Forbidden);
        }

        var ownerUserId = await _students.GetOwnerUserIdAsync(query.StudentId, cancellationToken);
        if (ownerUserId == userId)
        {
            return Result.Success();
        }

        if (await _students.IsLinkedTeacherOfStudentAsync(userId, query.StudentId, cancellationToken))
        {
            return Result.Success();
        }

        return await _parents.IsApprovedParentOfStudentAsync(userId, query.StudentId, cancellationToken)
            ? Result.Success()
            : Result.Failure(ReportingErrors.Forbidden);
    }
}
