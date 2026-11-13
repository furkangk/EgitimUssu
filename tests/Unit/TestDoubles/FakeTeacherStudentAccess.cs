using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit.TestDoubles;

/// <summary>
/// P15 T2: <see cref="ITeacherStudentAccess"/>'in sabit cevaplı sahtesi. Kapsam cevabını yalnız
/// <b>tüketen</b> kodu (ör. ödeme önbelleği, oturum başlatma) test eden yerler içindir; kapsamın nasıl
/// hesaplandığı gerçek implementasyona karşı doğrulanır (<c>TeacherStudentAccessTests</c>).
/// </summary>
/// <param name="scope">Her soruya dönülecek kapsam.</param>
public sealed class FakeTeacherStudentAccess(TeacherAccessScope scope = TeacherAccessScope.Private) : ITeacherStudentAccess
{
    /// <inheritdoc />
    public Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(scope);
}
