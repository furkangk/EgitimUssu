using System.Net;
using System.Net.Http.Json;
using EgitimUssu.Modules.Students.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 3 / N-23: Öğretmenin açtığı profili bir öğrenci hesabına bağlamanın tek ürün yolu — hedefli davet +
/// öğrencinin kodu girmesi (claim). Eskiden yardımcılar profili açarken gövdeye öğrencinin <c>userId</c>'sini
/// yazıyordu; bu artık <c>400 students.field_not_allowed</c>.
/// </summary>
internal static class StudentLinkTestHelper
{
    /// <summary>
    /// Öğretmenin açtığı <paramref name="studentId"/> profiline öğrenciyi davet eder ve öğrenci kodu girer.
    /// Sonuç: profil öğrencinin, bağ <c>Linked</c>. Davet kodu bugün hiçbir API yanıtında yok (Öğrenilenler #184)
    /// — test onu salt-okunur DB'den alır.
    /// </summary>
    public static async Task LinkStudentAccountAsync(
        WebApplicationFactory<Program> factory,
        HttpClient teacher,
        HttpClient student,
        Guid teacherUserId,
        Guid studentId,
        Guid studentUserId)
    {
        var invite = await teacher.PostAsJsonAsync(
            $"/api/students/teachers/{teacherUserId}/students/{studentId}/invite", new { targetUserId = studentUserId });
        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);

        string inviteCode;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var link = await scope.ServiceProvider.GetRequiredService<StudentsDbContext>().TeacherStudentLinks.AsNoTracking()
                .SingleAsync(x => x.TeacherUserId == teacherUserId && x.StudentId == studentId);
            inviteCode = link.InviteCode!;
        }

        var claim = await student.PostAsJsonAsync("/api/students/links/claim", new { inviteCode });
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
    }
}
