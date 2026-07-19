namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// Ç-06: Bir planın (LessonSchedule/self ders) belirli bir tarihte çalışılmış olduğunu belirtir.
/// Study modülü, tamamlanmış ve bir derse bağlı seanslardan üretir; Scheduling takvim occurrence'ının
/// <c>Completed</c> alanını doldurmak için okur.
/// </summary>
public sealed record PlanCompletion(Guid LessonId, DateOnly Date);

/// <summary>
/// Study modülünün yayınladığı okuma sözleşmesi: verilen öğrenci ve tarih aralığında tamamlanmış
/// (bir plana bağlı) çalışma seanslarından (LessonId, tarih) kümesi. Scheduling bunu takvimde
/// "çalışıldı" rozeti için tüketir (modül izolasyonu: doğrudan referans yok).
/// </summary>
public interface IStudyPlanCompletionReader
{
    Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(
        Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
