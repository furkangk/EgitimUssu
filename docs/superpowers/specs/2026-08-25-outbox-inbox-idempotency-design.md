# Tasarım — Outbox Tüketici Idempotency (Ortak Inbox)

> **Tarih:** 2026-08-25 · **Kapsam:** Backend (.NET 9 modüler monolit) · **Faz:** 0 madde 2 (Y4)
> **İlgili:** `doc/modules/mimari_inceleme.md` Y4 · K2/K5 (outbox dayanıklılık) · `doc/modules/00_genel_bakis.md`

## 1. Problem

Outbox **at-least-once** teslimat garantisi veriyor: bir integration event, tüketici handler'lara birden çok kez ulaşabilir. Sebepler:

- Bir event birden çok modül tarafından tüketiliyor (ör. `LessonSessionCompleted` → 3 modül). Outbox mesajı tek satır; handler'lardan biri exception fırlatırsa **tüm mesaj** retry'a girer → başarılı olan handler'lar **yeniden** çağrılır.
- Bir handler'ın iş-yazımı commit olduktan sonra, "işlendi" işareti yazılmadan süreç çökerse → retry'da iş **tekrar** uygulanır (çift kayıt).

Bugünkü durumda **aktif çift-yazım bug'ı yok** — her handler'ın bir tür dedup'ı var — ama **desen tutarsız ve bir kısmı kırılgan**:

| Desen | Handler'lar | Değerlendirme |
|-------|-------------|---------------|
| EventId tabanlı `processed` tablosu | ProgressTracking (`processed_events`), Notifications `ParentEventNotificationHandler` + Parents projeksiyonları (`processed_integration_events`, iki ayrı entity) | ✅ Sağlam ama **3 farklı tablo/entity** |
| İş-verisi anahtarı ("bu kayıt zaten var mı") | `LessonScheduleNotificationIntegrationEventHandler`, Assignments `LessonSessionCompletedIntegrationEventHandler`, Students `ParentChildLinkApprovedIntegrationEventHandler` | ⚠️ Kırılgan (kayıt meşru silinip yeniden gelirse / hiç oluşmuyorsa dedup zayıflar) |
| Yan-etki idempotency'si (`ExecuteUpdate` reassign 0 satır) | 5× `*StudentMergedHandler` (Payments/Study/Assignments/Scheduling/LessonSessions) | ⚠️ Açık dedup kaydı yok; semantik örtük |

**Hedef:** Tek kanonik, EventId tabanlı, atomik idempotency deseni.

## 2. Kararlar (onaylandı)

- **Kapsam:** Tam birleştirme — tüm tüketici handler'lar tek desene geçer; 3 mevcut `processed_*` tablosu ortak `inbox_messages` ile değişir. (Prod veri yok → göç riski düşük.)
- **Atomiklik:** Sıkı — guard + iş-yazımı + inbox-mark **tek transaction**'da commit olur.

## 3. Bileşenler

### 3.1 `InboxMessage` entity — `Shared/Infrastructure/Persistence/InboxMessage.cs`
"Bu handler bu event'i işledi" kaydı. `OutboxMessage`'ın kardeşi.

| Alan | Tip | Not |
|------|-----|-----|
| `EventId` | `Guid` | = `IIntegrationEvent.EventId` (= `OutboxMessage.Id`) |
| `Handler` | `string` | Handler kimliği (`GetType().Name`) |
| `EventName` | `string` | Teşhis/gözlem için (`IIntegrationEvent.Name`) |
| `ProcessedOnUtc` | `DateTime` | `IClock.UtcNow` |

**Bileşik birincil anahtar: `(EventId, Handler)`** — tek EventId birden çok handler tarafından tüketilebilir; her handler kendi dedup kaydını tutar. Tablo adı `inbox_messages`.

### 3.2 `ModuleDbContext` entegrasyonu — `Shared/Infrastructure/Persistence/ModuleDbContext.cs`
`OnModelCreating` içinde, `OutboxMessages` / `ModuleStates` ile aynı yerde:
```csharp
modelBuilder.Entity<InboxMessage>(b =>
{
    b.ToTable("inbox_messages");
    b.HasKey(x => new { x.EventId, x.Handler });
    b.Property(x => x.Handler).HasMaxLength(256);
    b.Property(x => x.EventName).HasMaxLength(256);
});
```
`DbSet<InboxMessage> InboxMessages` eklenir. Böylece **her modül DbContext'i** tabloyu otomatik alır. (Her modül kendi şemasında ayrı `inbox_messages` tablosuna sahip olur — modül veri izolasyonu korunur.)

### 3.3 Ortak base handler — `Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs`
```csharp
public abstract class IdempotentIntegrationEventHandler : IIntegrationEventHandler
{
    protected abstract ModuleDbContext DbContext { get; }
    protected IClock Clock { get; }               // ctor ile enjekte
    protected virtual string HandlerName => GetType().Name;

    public abstract bool CanHandle(IIntegrationEvent integrationEvent);

    public async Task HandleAsync(IIntegrationEvent e, CancellationToken ct)
    {
        var relational = DbContext.Database.IsRelational();
        await using var tx = relational
            ? await DbContext.Database.BeginTransactionAsync(ct)
            : null;

        var already = await DbContext.Set<InboxMessage>()
            .AnyAsync(x => x.EventId == e.EventId && x.Handler == HandlerName, ct);
        if (already) return;                       // tx dispose → no-op

        var applied = await ApplyAsync(e, ct);     // iş yazımını STAGE eder / ExecuteUpdate
        if (applied)
        {
            DbContext.Set<InboxMessage>()
                .Add(new InboxMessage(e.EventId, HandlerName, e.Name, Clock.UtcNow));
            await DbContext.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
    }

    /// <summary>İş etkisini uygular. SaveChanges ÇAĞIRMAZ (base tek commit yapar).
    /// İşlenecek bir şey yoksa false döner → inbox'a yazılmaz.</summary>
    protected abstract Task<bool> ApplyAsync(IIntegrationEvent e, CancellationToken ct);
}
```
**Sözleşme:** `ApplyAsync` yalnız `DbContext` üzerinden çalışır ve **kendi `SaveChanges`'ini çağırmaz**; değişiklikleri change-tracker'a stage eder (veya aynı transaction içinde `ExecuteUpdate`). Böylece iş-yazımı + inbox-mark tek `SaveChanges` + `Commit` ile atomik olur. Exception → transaction rollback → ne iş ne mark yazılır → retry güvenli.

> **Not (InMemory dev):** InMemory provider transaction desteklemez; `IsRelational()` guard'ı ile tx atlanır. Bu ortamda outbox tek-iş-parçacıklı işlediğinden ve guard yine çalıştığından davranış korunur. Atomiklik garantisi prod (Postgres) yolunda geçerlidir.

## 4. Handler dönüşümü

Her tüketici handler `IdempotentIntegrationEventHandler`'dan türer; mevcut mantığı `ApplyAsync`'e taşınır ve içindeki `SaveChanges` çağrıları kaldırılır (tek commit base'e devredilir). Repository'lerde gerekiyorsa "stage-only" (Add/Update, SaveChanges'siz) metotlar eklenir/kullanılır.

**Dönüştürülecekler (11 handler):**
1. `ProgressTracking` — StudySessionCompleted + TestResultRecorded (2)
2. `Notifications` — ParentEventNotificationHandler (1) · LessonScheduleNotificationIntegrationEventHandler (1)
3. `Parents` — 4 projeksiyon handler'ı (mevcut base sınıf `ParentReadModelProjectionHandler` ortak base'e devrolur)
4. `Assignments` — LessonSessionCompletedIntegrationEventHandler (1)
5. `Students` — ParentChildLinkApprovedIntegrationEventHandler (1)
6. `*StudentMergedHandler` × 5 (Payments/Study/Assignments/Scheduling/LessonSessions)

**Silinecek eski dedup altyapısı:**
- `ProgressTracking` — `ProcessedEvent` entity + `processed_events` config + repo metotları
- `Notifications` — event-handler dedup'ı ortak inbox'a taşınır. ⚠️ `ProcessedIntegrationEvent` entity + `processed_integration_events` tablosu tamamen SİLİNMEZ: `ParentWeeklySummaryService` bunu `weekly:...` anahtarıyla haftalık-özet dedup'ı için kullanıyor → tablo **korunur** (yeniden adlandırılabilir), yalnız event-handler kullanımı ortak inbox'a geçer. Bkz. §6.
- `Parents` — `ProcessedIntegrationEvent` entity + `processed_integration_events` config

## 5. Migration'lar
`inbox_messages` `ModuleDbContext` tabanına eklendiğinden **her kayıtlı modül DbContext'i** için migration üretilir:
```
dotnet ef migrations add AddInboxMessages \
  --project src/Modules/<Modül>/Infrastructure \
  --startup-project src/API.Host --context <Modül>DbContext
```
Eski `processed_*` tabloları olan 3 modülde migration ayrıca o tabloyu **DROP** eder (prod veri yok). CI migration-drift kontrolü (Y8) tüm context'lerin migration'ının modelle uyumlu olmasını zorunlu kılar → hepsi üretilmeli.

## 6. Riskler / Kenar durumlar
- **`ParentWeeklySummaryService` çakışması:** Bu servis `processed_integration_events` tablosunu event-dedup'ı için DEĞİL, "bu hafta için özet üretildi mi" (`weekly:{parentId}:{weekStart}`) anahtarıyla kullanıyor. Ortak `inbox_messages`'ın PK'si `(EventId Guid, Handler)`; haftalık anahtar Guid değil. **Karar:** Notifications'ın `processed_integration_events` tablosu haftalık-özet dedup'ı için **korunur** (yeniden adlandırılabilir: `summary_dedup`), yalnız event-handler dedup'ı ortak inbox'a taşınır. Spec bu ayrımı netleştirir; haftalık servis dokunulmaz.
- **InMemory atomiklik:** §3.3 notu — dev'de tx yok, garanti prod yolunda. Kabul edildi.
- **`ExecuteUpdate` + transaction:** StudentMerged handler'ları `ExecuteUpdateAsync` kullanıyor; açık transaction içinde çağrıldığında ambient tx'e katılır → inbox insert ile atomik. InMemory'de ExecuteUpdate + guard yeterli (zaten doğal idempotent).
- **Migration hacmi:** ~12 modül context'i. Mekanik ama geniş; her biri derlenip drift kontrolünden geçmeli.

## 7. Test (TDD)
`tests/Unit/` altında `IdempotentIntegrationEventHandlerTests` (InMemory `ModuleDbContext` türevi test double ile):
1. Aynı `(EventId, Handler)` iki kez → `ApplyAsync` **bir kez** çağrılır; ikinci çağrı guard'a takılır; inbox'ta tek satır.
2. Aynı EventId, farklı `Handler` → ikisi de çalışır; inbox'ta iki satır.
3. `ApplyAsync` `false` döner → inbox'a yazılmaz, tekrar denenebilir.
4. `ApplyAsync` exception fırlatır → inbox'a yazılmaz (mark yok); event yeniden işlenebilir.
5. (Entegrasyon, Docker'lı) Gerçek Postgres'te iş-yazımı + inbox-mark atomik commit; `ApplyAsync` ortasında hata → rollback (ne iş ne mark).

## 8. Doküman güncellemeleri (KALICI KURAL)
- `doc/modules/mimari_inceleme.md` — **Y4 ✅ Düzeltildi** işaretle.
- `doc/modules/00_genel_bakis.md` — ilgili handler notlarında "idempotent (ortak inbox)" ifadesini birleştir.
- `doc/modules/veri_modeli.md` — her modül şemasına `inbox_messages` ekle; eski `processed_events`/`processed_integration_events`'i güncelle.

## 9. Kapsam dışı (YAGNI)
- Merkezî (tek şema) global inbox — modül izolasyonu bozar; her modül kendi tablosunu tutar.
- Event bus seviyesinde decorator dedup — iş-yazımı ile atomik olamaz (farklı DbContext/tx); reddedildi.
- Inbox kayıtlarının temizlenmesi (retention/TTL) — ayrı, sonraki bir iş.
