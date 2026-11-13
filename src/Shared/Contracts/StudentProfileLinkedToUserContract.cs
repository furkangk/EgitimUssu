namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// P15 / N-23: Öğretmenin açtığı öğrenci profili, davet kabulüyle (claim veya link kabulü) bir öğrenci hesabına
/// bağlandığında Students modülünün Outbox üzerinden yaydığı integration event yükü. Domain event adı
/// <c>StudentProfileLinkedToUserDomainEvent</c> ile eşleşir.
/// <para>
/// Profilin kullanıcısı yalnız bu yolla dolar (oluşturma gövdesinden gelmez). Parents'ın öğrenci→kullanıcı
/// eşlemesi (<c>KnownStudent.UserId</c>) bu olayı P15 Task 4'te tüketir; o zamana kadar davetle bağlanan
/// öğrencinin veli talebini onaylama yetkisi görünmez (fail-closed).
/// </para>
/// </summary>
/// <param name="StudentId">Bağlanan öğrenci profili.</param>
/// <param name="UserId">Profili devralan öğrenci kullanıcısı.</param>
/// <param name="OccurredOnUtc">Bağlama anı (domain event'in <c>OccurredOnUtc</c>'si).</param>
public sealed record StudentProfileLinkedToUserIntegrationEvent(Guid StudentId, Guid UserId, DateTime OccurredOnUtc);
