# Veli V-E — Veli Bildirim Motoru (Premium Kapılı) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Veliye bildirim üret: olay bazlı (yeni ödev, ders tamamlandı özeti, ödeme güncellemesi, bağlantı bildirimi [V-C], ödeme beyanı [V-G]) + haftalık özet. Bildirimler **yalnız Premium veliye** gider (PRD 9.3 kararı) ve velinin tercih anahtarlarına saygılıdır.

**Architecture:** Notifications modülünde bugün veli kavramı yok. Bu plan: (1) `ParentProfile`'a `MembershipTier` alanı + set komutu ekler (satın alma altyapısı V-Premium'da; şimdilik admin/test seti); (2) Notifications'a yeni `ParentNotification` aggregate + `ProcessedIntegrationEvents` dedup tablosu + veli-bildirim listeleme endpoint'i; (3) `IParentNotificationDirectory` (`Shared.Contracts`, Parents uygular) ile bir öğrencinin **onaylı velilerini + tier + tercihlerini** döndürür; (4) Notifications'ta bir entegrasyon-olay işleyicisi + haftalık özet için `BackgroundService`. Premium kapısı ve tercih kontrolü teslim anında uygulanır. **Karar (2026-07-19):** bildirimler Premium.

**Tech Stack:** .NET 9, EF Core (`parents`/`notifications` şemaları), CQRS, Outbox, `BackgroundService`, xUnit. Cross-module: `IParentNotificationDirectory`.

## Global Constraints
- Migration (Parents): `dotnet ef migrations add AddParentMembershipTier --project src/Modules/Parents/Infrastructure --startup-project src/API.Host --context ParentsDbContext`
- Migration (Notifications): `dotnet ef migrations add AddParentNotifications --project src/Modules/Notifications/Infrastructure --startup-project src/API.Host --context NotificationsDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Reuse: `ParentReadModelProjectionHandler` dedup deseni (Parents); `NotificationDispatcher : BackgroundService` deseni (Notifications); `IMembershipGate`/Premium gate deseni (Study Ö-D).

## File Structure
- `src/Modules/Parents/Domain/ParentsDomainModel.cs` — `ParentProfile.MembershipTier` + `SetMembershipTier` + `ParentNotificationTier` (kontrat enum'u `MembershipTier` yeniden kullanılır).
- `src/Modules/Parents/Application/ParentFeatures.cs` — `SetParentMembershipTierCommand` + handler + response alanı; `IParentRepository` değişmez.
- `src/Modules/Parents/Infrastructure/ParentNotificationDirectory.cs` *(yeni)* + config + DI + migration.
- `src/Shared/Contracts/ParentNotificationContract.cs` *(yeni)* — `IParentNotificationDirectory` + `ParentNotificationTarget` + `ParentNotificationPrefs`.
- `src/Modules/Notifications/Domain/NotificationsDomainModel.cs` — `ParentNotification` aggregate + `enum ParentNotificationType`.
- `src/Modules/Notifications/Application/NotificationFeatures.cs` — `ListParentNotificationsQuery` + repo genişletme.
- `src/Modules/Notifications/Infrastructure/*` — `ParentNotification` DbSet/config, `ProcessedIntegrationEvents` tablosu, `ParentEventNotificationHandler`, `ParentWeeklySummaryService`, DI, migration.
- `src/Modules/Notifications/API/NotificationsModule.cs` — veli listeleme endpoint'i.
- Test: `tests/Unit/ParentNotificationTests.cs`, `tests/Unit/ParentMembershipTierTests.cs`.

---

### Task 1: `ParentProfile.MembershipTier` (+ set komutu)

**Files:** `ParentsDomainModel.cs`, `ParentFeatures.cs`, `ParentsDbContext.cs`, `ParentsModule.cs`, Parents `DependencyInjection.cs`, migration; Test: `tests/Unit/ParentMembershipTierTests.cs`.

**Interfaces:**
- Produces: `ParentProfile.MembershipTier` (`MembershipTier`, `Shared.Contracts` enum, default `Free`) + `SetMembershipTier(MembershipTier, DateTime)`; `SetParentMembershipTierCommand(Guid ParentUserId, MembershipTier Tier)` (Admin-yetkili).

- [ ] **Step 1: Failing test** — `ParentMembershipTierTests.cs`: yeni profil `Free`; `SetMembershipTier(Premium, later)` → `Premium` + timestamp. (`using EgitimUssu.Shared.Contracts;` for `MembershipTier`.)
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `ParentProfile`'a ekle (StudentProfile Ö-D deseni):
```csharp
    public MembershipTier MembershipTier { get; private set; } = MembershipTier.Free;

    public void SetMembershipTier(MembershipTier tier, DateTime updatedOnUtc)
    {
        MembershipTier = tier;
        UpdatedOnUtc = updatedOnUtc;
    }
```
(`using EgitimUssu.Shared.Contracts;` dosya başına.)
- [ ] **Step 4:** `SetParentMembershipTierCommand` + handler (profil bul → `SetMembershipTier` → kaydet; yoksa `ParentErrors.ProfileNotFound`) + Admin authorizer (`ParentAuthorizer`'a `ICommandAuthorizer<SetParentMembershipTierCommand>`: yalnız `Admin`) + DI. `ParentProfileResponse`'a `string MembershipTier` alanı + mapping.
- [ ] **Step 5:** EF config `HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(MembershipTier.Free)`; endpoint `PUT /api/parents/{parentUserId:guid}/membership-tier` (Admin).
- [ ] **Step 6:** Build + migration `AddParentMembershipTier` + test → PASS.
- [ ] **Step 7: Commit** `feat(parents): veli üyelik seviyesi (MembershipTier) + set (Veli V-E)`.

---

### Task 2: `IParentNotificationDirectory` (Parents uygular)

**Files:** `src/Shared/Contracts/ParentNotificationContract.cs` (yeni), `ParentNotificationDirectory.cs` (yeni, Parents.Infrastructure), Parents DI.

**Interfaces:**
- Produces:
```csharp
namespace EgitimUssu.Shared.Contracts;

public sealed record ParentNotificationPrefs(
    bool MissedAssignment, bool WeeklyProgressSummary, bool LessonReminders, bool TestResults, bool Payments);

public sealed record ParentNotificationTarget(Guid ParentUserId, MembershipTier Tier, ParentNotificationPrefs Prefs);

public interface IParentNotificationDirectory
{
    // Bir öğrencinin ONAYLI velileri + üyelik + tercihleri. Notifications teslim kararı için okur.
    Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    // Haftalık özet için: tüm onaylı (parent,student) çiftleri (üyelik + tercih ile).
    Task<IReadOnlyCollection<(Guid StudentId, ParentNotificationTarget Target)>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 1:** Contract dosyasını yaz.
- [ ] **Step 2:** `ParentNotificationDirectory.cs` (Parents): `ParentsDbContext`'te `ParentChildLinks` (Approved) join `ParentProfiles` (ParentUserId) → `ParentNotificationTarget` üret (tier + prefs profilden). `GetApprovedParentsForStudentAsync` `StudentId` filtreli; `ListAllApprovedTargetsAsync` tüm Approved bağlar.
- [ ] **Step 3:** Parents DI: `services.AddScoped<IParentNotificationDirectory, ParentNotificationDirectory>();`.
- [ ] **Step 4:** Build → 0 hata. **Commit** `feat(parents): IParentNotificationDirectory (onaylı veli + tier + tercih) (Veli V-E)`.

---

### Task 3: `ParentNotification` aggregate + dedup + repo

**Files:** `NotificationsDomainModel.cs`, `NotificationFeatures.cs`, `NotificationsDbContext.cs`, `LessonReminderRepository.cs` (veya yeni `ParentNotificationRepository.cs`), Notifications DI, migration; Test: `tests/Unit/ParentNotificationTests.cs`.

**Interfaces:**
- Produces: `ParentNotification : AggregateRoot<Guid>` (`Id, ParentUserId, StudentId, Type, Title, Message, CreatedOnUtc`). `enum ParentNotificationType { WeeklySummary=1, NewAssignment=2, LessonCompleted=3, PaymentUpdate=4, LinkConnected=5, PaymentDeclared=6 }`. `IParentNotificationRepository` (Add, ListByParent, dedup helpers, SaveChanges). `ProcessedIntegrationEvent` tablosu (Notifications'ta yok → eklenir; Parents'taki `ProcessedIntegrationEvent` deseni birebir).

- [ ] **Step 1: Failing test** — `ParentNotificationTests.cs`: `new ParentNotification(...)` alanları saklıyor mu; ctor bir `ParentNotificationCreatedDomainEvent` yayıyor mu (opsiyonel — teslim MVP'de yalnız kayıt). Basit alan/ctor testi.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** Aggregate + enum + (opsiyonel) event; `ProcessedIntegrationEvent : Entity<Guid>` (Parents deseni); DbSet'ler + config'ler (`parent_notifications`: Title max 200, Message max 1000, Type string max 32, index `{ParentUserId, CreatedOnUtc}`; `processed_integration_events`: EventName max 256); `IParentNotificationRepository` + impl; DI.
- [ ] **Step 4:** Build + migration `AddParentNotifications` + test → PASS.
- [ ] **Step 5: Commit** `feat(notifications): ParentNotification aggregate + dedup + repo (Veli V-E)`.

---

### Task 4: Olay bazlı veli bildirim işleyicisi (Premium kapılı)

**Files:** `src/Modules/Notifications/Infrastructure/ParentEventNotificationHandler.cs` (yeni), Notifications DI; Test: `tests/Unit/ParentNotificationTests.cs` (ekleme).

**Interfaces:**
- Consumes: `IParentNotificationDirectory` (Task 2), `IParentNotificationRepository` (Task 3).
- Produces: `internal sealed class ParentEventNotificationHandler : IIntegrationEventHandler`. `CanHandle`: `SourceModule`+`Name` ∈ { Assignments/`AssignmentCreatedDomainEvent`; LessonSessions/`LessonSessionCompletedDomainEvent`; Payments/`PaymentRecordUpdatedDomainEvent`; Parents/`ParentLinkConnectionNoticeDomainEvent`; Payments/`ParentPaymentDeclaredDomainEvent` }.

- [ ] **Step 1: Failing test** — handler'ı sahte directory (bir Premium veli + `MissedAssignment=true`) + sahte repo ile kur; bir `AssignmentCreatedDomainEvent` zarfı ver → tek `ParentNotification` (NewAssignment) yaratıldığını doğrula. Free veli veya ilgili pref kapalıysa **yaratılmadığını** doğrula (Premium + pref kapısı).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** Handler'ı yaz (Notifications'taki `LessonScheduleNotificationIntegrationEventHandler` + Parents dedup desenini izle):
  - `HandleAsync`: dedup (`ProcessedIntegrationEvents` EventId), olay adına göre payload'ı deserialize et (StudentId'yi çıkar), `directory.GetApprovedParentsForStudentAsync(studentId)` → her hedef için **Premium ve ilgili tercih açık** ise `ParentNotification` üret (tip + başlık/mesaj olaya göre). Pref eşlemesi: NewAssignment→`MissedAssignment`? (yeni ödev bildirimi) — doküman AKIŞ 6: "Yeni ödev verildi ✅ Açık"; en yakın anahtar `MissedAssignment` yerine yeni ödev için de aynı anahtar kullanılır ya da `NotifyMissedAssignment` "ödev olayları" olarak yorumlanır (dokümanla uyumlu). LessonCompleted→(genel, tercih koşulsuz veya `LessonReminders`); PaymentUpdate→`Payments`; LinkConnected→(koşulsuz, güvenlik bildirimi); PaymentDeclared→öğretmene gider, **veli hedefli değil** → bu olay burada atlanır (öğretmen bildirimi ayrı; V-G notu). Kaydet + `ProcessedIntegrationEvent`.
  - Premium kapısı: `target.Tier == MembershipTier.Premium` değilse atla.
- [ ] **Step 4:** DI: `services.AddScoped<IIntegrationEventHandler, ParentEventNotificationHandler>();`.
- [ ] **Step 5:** Build + test → PASS. **Commit** `feat(notifications): olay bazlı veli bildirimi (Premium + tercih kapılı) (Veli V-E)`.

---

### Task 5: Haftalık özet `BackgroundService`

**Files:** `src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs` (yeni), Notifications DI; Test: birim testi zor (zaman/DI) → işleyici mantığını saf bir `ParentWeeklySummaryProcessor` sınıfına çıkar ve onu test et.

**Interfaces:**
- Produces: `IParentWeeklySummaryProcessor.RunAsync(DateTime nowUtc, CancellationToken) → int` — tüm Premium + `WeeklyProgressSummary` açık hedefler için o hafta bir `ParentNotification(WeeklySummary)` üretir (haftada bir; idempotency: `ProcessedIntegrationEvent` yerine hafta-anahtarı `"weekly:{parentUserId}:{isoWeek}"` benzeri bir dedup anahtarı). Çalışma verisi `ChildProgressSnapshot` (Parents) — cross-module okuma yerine mesaj metni `IParentNotificationDirectory` hedefinden + (V-F sonrası) snapshot'tan beslenebilir; MVP'de sabit "haftalık özet hazır" mesajı, V-F study verisi geldiğinde zenginleşir.

- [ ] **Step 1: Failing test** — `RunAsync` iki Premium hedefe iki `WeeklySummary` üretir; aynı hafta ikinci çağrı **tekrar üretmez** (dedup). Sahte directory + repo.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `ParentWeeklySummaryProcessor` (saf sınıf) + `ParentWeeklySummaryService : BackgroundService` (günlük tetik; `NotificationDispatcher` deseni — poll aralığı büyük, ör. 6 saat; `IClock` ile "hafta değişti mi" kontrolü). Hafta dedup için `ProcessedIntegrationEvent`'e deterministik `Guid` (hafta+parent hash) yaz veya yeni `ParentWeeklySummaryLog` tablosu (basit). MVP: `ProcessedIntegrationEvent` yeniden kullan, `eventId = Deterministic(parentUserId, isoWeek)`.
- [ ] **Step 4:** DI: `AddScoped<IParentWeeklySummaryProcessor, ...>` + `AddHostedService<ParentWeeklySummaryService>()`.
- [ ] **Step 5:** Build + test → PASS. **Commit** `feat(notifications): veli haftalık özet servisi (Premium) (Veli V-E)`.

---

### Task 6: Veli bildirim listeleme endpoint'i

**Files:** `NotificationFeatures.cs` (`ListParentNotificationsQuery` + handler + authorizer), `NotificationsModule.cs`, DI.

- [ ] **Step 1:** `ListParentNotificationsQuery(Guid ParentUserId) → Result<IReadOnlyCollection<ParentNotificationResponse>>` + handler (repo `ListByParentAsync`, `CreatedOnUtc` azalan) + authorizer (self/Admin). Endpoint `GET /api/notifications/parents/{parentUserId:guid}/notifications`.
- [ ] **Step 2:** Build + test + commit `feat(notifications): veli bildirim listeleme endpoint'i (Veli V-E)`.

---

### Task 7: Dokümantasyon
- [ ] `doc/modules/m11_notifications.md`: `ParentNotification` + olay işleyicisi + haftalık özet servisi + **Premium kapısı** + tercih eşlemesi + `IParentNotificationDirectory`.
- [ ] `doc/modules/m09_parents.md`: `ParentProfile.MembershipTier` + `PUT /membership-tier`; bildirim tercihlerinin artık fiilen tüketildiği notu.
- [ ] `doc/modules/00_genel_bakis.md` endpoint envanteri; `doc/modules/veri_modeli.md` (`ParentNotification`, `ParentProfile.MembershipTier`, `ParentNotificationType` enum, kontrat); `doc/roles/veli.md` V-09.26/27/28 (Premium bildirim).
- [ ] commit `docs: veli bildirim motoru (Premium) (Veli V-E)`.

## Self-Review
- **Spec coverage:** Spec V-E "haftalık özet + olay bildirimleri, Premium kapılı" → Task 1-6. Premium kaynağı keşif forku: `ParentProfile.MembershipTier` eklendi (Task 1) çünkü `IMembershipDirectory` yalnız öğrenci biliyor — plan içi teknik karar, gerekçe yazıldı.
- **Bağımlılık:** V-C (`ParentLinkConnectionNoticeDomainEvent` teslimi burada), V-G (`ParentPaymentDeclaredDomainEvent` — öğretmen bildirimi; bu handler'da öğretmen hedefli genişletme ayrı ele alınır), V-F (haftalık özet metni study verisiyle zenginleşir). V-E çekirdeği (olay bildirimleri) bunlar olmadan da çalışır.
- **Placeholder riski:** Task 4/5 handler gövdeleri desen-referanslı (Notifications + Parents mevcut desenleri); novel çekirdek (aggregate, contract, tier, Premium+pref kapı mantığı, dedup) tam tanımlı. Uygulama sırasında her olay için payload record'u üreticinin alan adlarıyla yeniden bildirilir (kod tabanı deseni).
- **Uyarı (kullanıcıya):** Satın alma akışı olmadığından başlangıçta hiçbir veli Premium değildir → bildirimler yalnız Admin `PUT /membership-tier` ile Premium yapılan velilere gider. "Bildirimler Free" isteniyorsa (doküman önerisi) Premium kapısı Task 4/5'ten kaldırılır — tek satırlık değişiklik.
