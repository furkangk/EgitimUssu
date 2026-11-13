namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge) Students modülünün Outbox üzerinden yaydığı
/// integration event yükü (payload). Tüketen modüller (Scheduling, Assignments, Payments, LessonSessions,
/// Study, Parents, ProgressTracking, Messaging) kendi kayıtlarındaki <c>StudentId = FromStudentId</c> satırlarını kanonik
/// <c>ToStudentId</c>'ye yeniden atar. Domain event adı <c>StudentProfilesMergedDomainEvent</c> ile eşleşir.
/// <para>
/// P15 / N-28: Öğrenci kimliğiyle <b>benzersiz</b> satırı olan tüketici körü körüne yeniden atayamaz — kanonikte
/// de satır varsa benzersiz indeks ihlali, outbox'ta sonsuza dek yeniden denenen mesaj demektir. Kural: çakışmayan
/// satır taşınır, çakışanda kanonik kazanır (toplanabilir sayaçlar toplanır), kaynak satır silinir. Öğretmen
/// bağları ve bekleyen veli davetleri Students'ta, birleşmeyle <b>aynı transaction'da</b> taşınır.
/// </para>
/// </summary>
public sealed record StudentProfilesMergedIntegrationEvent(Guid FromStudentId, Guid ToStudentId);
