namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge) Students modülünün Outbox üzerinden yaydığı
/// integration event yükü (payload). Tüketen modüller (Scheduling, Assignments, Payments, LessonSessions,
/// Study) kendi kayıtlarındaki <c>StudentId = FromStudentId</c> satırlarını kanonik <c>ToStudentId</c>'ye
/// yeniden atar. Domain event adı <c>StudentProfilesMergedDomainEvent</c> ile eşleşir.
/// </summary>
public sealed record StudentProfilesMergedIntegrationEvent(Guid FromStudentId, Guid ToStudentId);
