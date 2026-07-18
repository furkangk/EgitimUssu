namespace EgitimUssu.Shared.Contracts;

// Modüller-arası salt-okunur (read) sözleşme. Students bu sözleşmeyi uygular; diğer modüller
// (ör. Scheduling) öğrenci↔kullanıcı sahipliğini doğrulamak için tüketir. Böylece modüller
// birbirine doğrudan proje referansı vermez (anti-corruption / paylaşılan kontrat).
public interface IStudentDirectory
{
    // Öğrenci profiline bağlı kullanıcının kimliği (Student.UserId).
    // Profil bulunamazsa veya bağlı kullanıcı yoksa null döner.
    Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken);
}
