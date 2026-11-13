# P12 — Admin API, Moderasyon ve Geri Bildirim (M18) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Platformu yönetilebilir kılmak: `/api/admin/*` yönetim yüzeyi, kullanıcı durumu (askıya alma/kapatma), ortak moderasyon kuyruğu (yorum + mesaj + ilan şikayetleri), kullanıcı geri bildirim/şikayet modülü (M18) ve moderasyon kararlarının ilgili modüllere event ile yayılması.

**Architecture:** Yeni **Feedback** modülü (`feedback` şeması) iki aggregate taşır: `FeedbackReport` (bug/öneri) ve `AbuseReport` (şikayet — hedef türü + hedef kimliği + durum). M13 `ReviewFlag` ve M16 mesaj şikayeti buraya **event ile** akar; tek kuyrukta toplanır. Admin kararı `ModerationDecidedIntegrationEvent` yayar; Reviews yorumu kaldırır, Messaging mesajı yumuşak siler, Matching ilanı kapatır, Identity kullanıcıyı askıya alır — hepsi idempotent tüketicilerle. Admin uçları `src/API.Host/Admin/` altında **compose-root** seviyesinde toplanır (BFF deseni; modül sınırı ihlal edilmez, her uç ilgili modülün command/query'sini dispatch eder).

**Tech Stack:** .NET 9, EF Core, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-08**)

## Global Constraints

- **Yetki:** `/api/admin/*` altındaki her uç `RequireAuthorization("Admin")` politikasıyla korunur **ve** ilgili command/query'nin kendi authorizer'ı Admin kontrolünü tekrar yapar (savunma katmanı). Politika `Program.cs`'te tanımlanır.
- **Denetim izi:** Her admin eylemi `AdminAuditLog` satırı yazar (kim, ne zaman, hangi hedef, gerekçe). Gerekçesiz karar kabul edilmez.
- **Raporlayan gizliliği:** Şikayet edenin kimliği moderasyon kuyruğunda **Admin'e görünür**, hedef kullanıcıya asla dönülmez.
- **KVKK:** Şikayet kayıtları 2 yıl saklanır; `FeedbackRetentionService` süresi dolanları anonimleştirir (P13 ile hizalı).
- **Idempotency:** Karar tüketicileri `IdempotentIntegrationEventHandler` tabanını kullanır.
- **Migration:** `dotnet ef migrations add <Ad> --project src/Modules/Feedback/Infrastructure --startup-project src/API.Host --context FeedbackDbContext`
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Kullanıcı durumu yönetimi (M01-2/M01-3)

**Files:**
- Modify: `src/Modules/Identity/Domain/IdentityDomainModel.cs` (`Suspend`/`Close`/`Reactivate`)
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs`, `IdentityPolicies.cs`
- Modify: `src/Modules/Identity/API/IdentityModule.cs`
- Modify: `src/Modules/Identity/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/UserStatusTransitionTests.cs`

**Interfaces:**
- Produces:
  - `UserAccount.Suspend(string reason, DateTime nowUtc)` / `Close(string reason, DateTime nowUtc)` / `Reactivate(DateTime nowUtc)` + `UserStatusChangedDomainEvent`.
  - `PUT /api/identity/users/{userId}/status` (Admin) — `{ status: "Active|Suspended|Closed", reason }`
  - `DELETE /api/identity/users/{userId}/roles/{role}` (Admin)
  - Askıya alma **tüm refresh oturumlarını iptal eder** ve erişim token'ını kara listeye alır (`ITokenBlacklist`).

- [ ] **Step 1: Testleri yaz (kırmızı)** — `Closed` hesap `Active`'e dönemez (`identity.invalid_status_transition`); askıya alınan kullanıcı login olamıyor; gerekçesiz askı reddediliyor; son Admin rolü kaldırılamıyor (`identity.last_admin`).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserStatusTransitionTests"`
- [ ] **Step 3: Domain + command + authorizer + uçlar + oturum iptali.**
- [ ] **Step 4: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 5: Doküman + commit**

`doc/modules/m01_identity.md` (§3.3 maddeleri `- [x]`), `doc/roles/admin.md`.
```bash
git add src/Modules/Identity tests doc
git commit -m "feat(identity): admin kullanici durumu + rol kaldirma (M01-2/M01-3)"
```

---

### Task 2: Feedback modülü — bildirim ve şikayet

**Files:**
- Create: `src/Modules/Feedback/{Domain,Application,Infrastructure,API}/*`
- Modify: `EgitimUssu.slnx`, `src/API.Host/ModuleAssemblies.cs`
- Test: `tests/Unit/AbuseReportTests.cs`, `tests/Unit/FeedbackReportTests.cs`

**Interfaces:**
- Produces:
  - `enum FeedbackKind { Bug = 1, Suggestion = 2, Question = 3 }`
  - `sealed class FeedbackReport : AggregateRoot<Guid>` — `Guid UserId`, `FeedbackKind Kind`, `string Title`, `string Body`, `string Platform` (`android|ios|web`), `string AppVersion`, `string? ScreenRoute`, `FeedbackStatus Status` (`Open|InReview|Resolved|Closed`), `string? AdminNote`, `DateTime CreatedOnUtc`.
  - `enum AbuseTargetType { User = 1, Review = 2, Message = 3, Listing = 4 }`
  - `enum AbuseReportStatus { Open = 1, InReview = 2, ActionTaken = 3, Dismissed = 4 }`
  - `sealed class AbuseReport : AggregateRoot<Guid>` — `Guid ReporterUserId`, `AbuseTargetType TargetType`, `Guid TargetId`, `Guid? TargetOwnerUserId`, `string Reason`, `string? Details`, `AbuseReportStatus Status`, `string? Decision`, `Guid? DecidedByUserId`, `DateTime? DecidedOnUtc`, `DateTime CreatedOnUtc`; metotlar `StartReview(...)`, `TakeAction(string decision, Guid adminUserId, DateTime now)`, `Dismiss(string reason, Guid adminUserId, DateTime now)`.
  - Uçlar (kullanıcı): `POST /api/feedback/reports`, `GET /api/feedback/reports/mine`, `POST /api/feedback/abuse-reports`, `GET /api/feedback/abuse-reports/mine`.

- [ ] **Step 1: Testleri yaz (kırmızı)** — aynı hedefe aynı kullanıcıdan **açık** ikinci şikayet reddedilir (`feedback.duplicate_report`); kendi içeriğini şikayet edemez; karar gerekçesi zorunlu; `Dismissed` şikayet tekrar karara bağlanamaz.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~AbuseReportTests|FullyQualifiedName~FeedbackReportTests"`
- [ ] **Step 3: 4 projeyi oluştur + domain'i yaz** (P09 Task 1 deseni).
- [ ] **Step 4: DbContext + migration** — `feedback_reports`, `abuse_reports` (index `(Status, CreatedOnUtc)`, unique filtreli index `(ReporterUserId, TargetType, TargetId)` yalnız `Status IN (Open, InReview)` için).
  Run: `dotnet ef migrations add InitialCreate --project src/Modules/Feedback/Infrastructure --startup-project src/API.Host --context FeedbackDbContext`
- [ ] **Step 5: Kullanıcı uçları + authorizer'lar.**
- [ ] **Step 6: Yeşil gör + commit**

```bash
dotnet test EgitimUssu.slnx
git add src/Modules/Feedback EgitimUssu.slnx src/API.Host tests doc
git commit -m "feat(feedback): bildirim ve sikayet modulu (M18-1)"
```

---

### Task 3: Ortak moderasyon kuyruğu ve karar yayılımı (M18-2)

**Files:**
- Create: `src/Modules/Feedback/Infrastructure/ReviewFlaggedAbuseHandler.cs`
- Create: `src/Modules/Feedback/Infrastructure/MessageReportedAbuseHandler.cs`
- Modify: `src/Modules/Reviews/**`, `src/Modules/Messaging/**` (şikayet uçları → event)
- Create: `src/Modules/Reviews/Infrastructure/ModerationDecisionHandler.cs`
- Create: `src/Modules/Messaging/Infrastructure/ModerationDecisionHandler.cs`
- Create: `src/Modules/Matching/Infrastructure/ModerationDecisionHandler.cs`
- Create: `src/Modules/Identity/Infrastructure/ModerationDecisionHandler.cs`
- Test: `tests/Unit/ModerationFlowTests.cs`

**Interfaces:**
- Produces:
  - `ReviewFlaggedIntegrationEvent`, `MessageReportedIntegrationEvent`, `ListingReportedIntegrationEvent` → `AbuseReport` oluşturur (idempotent).
  - `ModerationDecidedIntegrationEvent(Guid AbuseReportId, AbuseTargetTypeContract TargetType, Guid TargetId, string Action, string Reason, DateTime OccurredOnUtc)` — `Action`: `RemoveContent | SuspendUser | Dismiss`.
  - Tüketiciler: Reviews → `Review.Remove(reason)`; Messaging → `Message.SoftDelete()`; Matching → `Listing.Close()`; Identity → `UserAccount.Suspend(reason)`.

- [ ] **Step 1: Testleri yaz (kırmızı)** — flag event'i tek `AbuseReport` üretir (tekrar gelirse ikinci üretmez); `RemoveContent` kararı yorumu kaldırır; `SuspendUser` kullanıcıyı askıya alır; `Dismiss` hiçbir içeriği değiştirmez.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ModerationFlowTests"`
- [ ] **Step 3: Şikayet event'lerini yay** — Reviews `POST /{reviewId}/flag` (P11 Task 1) artık event üretir; Messaging'e `POST /api/messaging/messages/{messageId}/report` eklenir; Matching'e `POST /api/matching/listings/{listingId}/report`.
- [ ] **Step 4: 4 karar tüketicisini yaz** (hepsi idempotent taban).
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

`doc/modules/m18_feedback.md` (moderasyon akış diyagramı), `doc/modules/m13_reviews.md` + `m16_messaging.md` (şikayet uçları).
```bash
git add src/Modules tests doc
git commit -m "feat(feedback): ortak moderasyon kuyrugu + karar yayilimi (M18-2/M13-5)"
```

---

### Task 4: Admin API yüzeyi (C-01)

**Files:**
- Create: `src/API.Host/Admin/AdminEndpoints.cs`
- Create: `src/API.Host/Admin/AdminAuditLogging.cs`
- Create: `src/Modules/Feedback/Domain/AdminAuditLog.cs` (+ DbContext + migration)
- Modify: `src/API.Host/Program.cs` (`Admin` politikası + `MapAdminEndpoints()`)
- Test: `tests/Integration/AdminEndpointsTests.cs`

**Interfaces:**
- Produces (hepsi `RequireAuthorization("Admin")`):
  - `GET /api/admin/users?query=&status=&role=&skip=&take=`
  - `PUT /api/admin/users/{userId}/status` (Identity command'ini dispatch eder)
  - `POST /api/admin/users/{userId}/roles` · `DELETE /api/admin/users/{userId}/roles/{role}`
  - `PUT /api/admin/teachers/{userId}/verification` (P06 Task 1 command'i)
  - `GET /api/admin/moderation/queue?status=&targetType=&skip=&take=`
  - `POST /api/admin/moderation/{abuseReportId}/decide` — `{ action, reason }`
  - `GET /api/admin/feedback?status=&kind=&skip=&take=` · `POST /api/admin/feedback/{id}/resolve`
  - `GET /api/admin/membership/plans` · `POST` · `PUT` (P09 command'leri)
  - `POST /api/admin/projections/{name}/rebuild` (P07 `IProjectionRebuilder`)
  - `GET /api/admin/audit-logs?from=&to=&adminUserId=&skip=&take=`
- `AdminAuditLog` — `Guid AdminUserId`, `string Action`, `string TargetType`, `Guid? TargetId`, `string? Reason`, `DateTime OccurredOnUtc`.

- [ ] **Step 1: Politikayı ve testi yaz (kırmızı)**

`Program.cs`:
```csharp
builder.Services.AddAuthorization(options =>
{
    // mevcut "AuthenticatedUser" politikasının yanına:
    options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});
```
`tests/Integration/AdminEndpointsTests.cs`: Teacher token'ıyla `/api/admin/users` → **403**; Admin token'ıyla → **200**; kimliksiz → **401**. Her admin eyleminden sonra `audit_logs` satırı oluşuyor.

- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~AdminEndpointsTests"`
- [ ] **Step 3: `AdminAuditLog` + migration.**
- [ ] **Step 4: `AdminEndpoints.cs`'i yaz** — her uç ilgili modülün command/query'sini `ICommandDispatcher`/`IQueryDispatcher` ile dispatch eder; **hiçbir modülün DbContext'ine dokunmaz**. Her başarılı mutasyondan sonra `AdminAuditLogging.RecordAsync(...)`.
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: OpenAPI grubu** — admin uçları `.WithTags("Admin")` ile işaretlenir; Swagger'da ayrı grup.
- [ ] **Step 7: Doküman + commit**

`doc/roles/admin.md` (tam yetenek listesi + uç envanteri), `doc/modules/00_genel_bakis.md` (yeni "Admin (compose root)" bölümü), `doc/architecture/backend.md` (admin BFF deseni).
```bash
git add src/API.Host src/Modules/Feedback tests doc
git commit -m "feat(admin): /api/admin yuzeyi + denetim izi (C-01)"
```

---

### Task 5: Mobil — geri bildirim ve şikayet akışları (D-13)

**Files:**
- Create: `mobile/lib/features/feedback/**`
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` ("Bize ulaşın" gerçek forma bağlanır)
- Modify: `mobile/lib/features/messaging/presentation/pages/*` (mesaj şikayet menüsü — P10 Task 4 Step 5'te ertelenmişti)
- Modify: `mobile/lib/features/reviews/**` (yorum şikayet), `mobile/lib/features/discovery/**` (ilan/profil şikayet)
- Test: `mobile/test/features/feedback/feedback_cubit_test.dart`
- Create: `doc/pages/feedback_form.md`

**Interfaces:**
- `FeedbackRepository.submit({kind, title, body, platform, appVersion, screenRoute})`, `myReports()`, `reportAbuse({targetType, targetId, reason, details})`.
- Form platform/sürüm/ekran bilgisini **otomatik** doldurur (`package_info_plus` gerekirse eklenir).

- [ ] **Step 1: Cubit testini yaz (kırmızı)** — gönderim başarılı → `submitted`; ağ hatası → `failure` + tekrar deneme; mükerrer şikayet hatası kullanıcıya anlaşılır mesajla gösteriliyor.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/feedback/feedback_cubit_test.dart`
- [ ] **Step 3: Repository + cubit + form ekranını yaz.**
- [ ] **Step 4: Şikayet menülerini bağla** — mesaj balonunda uzun basma → "Şikayet et"; yorum kartında ⋮ → "Şikayet et"; public profilde ⋮ → "Şikayet et".
- [ ] **Step 5: "Bildirimlerim" listesi** — kullanıcı kendi bildirim/şikayetlerinin durumunu görüyor.
- [ ] **Step 6: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 7: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): geri bildirim ve sikayet akislari (D-13/M18-1)"
```

---

### Task 6: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → yeşil.
- [ ] **Step 2: Uçtan uca** — Öğrenci bir yorumu şikayet eder → Admin kuyruğunda görünür → Admin "İçeriği kaldır" kararı verir → yorum public profilde kaybolur → denetim izinde kayıt var → raporlayanın kimliği hedefe sızmamış.
- [ ] **Step 3: `[Obsolete]` temizliği** — P09 Task 2'de işaretlenen `Students.MembershipTier` / `Parents.MembershipTier` alanları artık kullanılmıyorsa kaldırılır + migration.
- [ ] **Step 4: Dokümanlar** — `doc/modules/m18_feedback.md` (🔴 → 🟢), `doc/roles/admin.md`, `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/denetim/2026-09-02_eksik_analizi.md` C-01, M18-*, M01-2/3, D-13 → `✅ (P12)`.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: P12 admin ve moderasyon kapanisi (C-01/M18-*/M01-2/3)"
```

---

## Kabul Kriterleri

- [ ] `/api/admin/*` Admin olmayan kimlikte 403, kimliksizde 401
- [ ] Her admin eylemi gerekçeli ve denetim izine yazılıyor
- [ ] Yorum/mesaj/ilan şikayetleri **tek** kuyrukta toplanıyor
- [ ] Karar ilgili modüle event ile yayılıyor ve içerik gerçekten kaldırılıyor
- [ ] Askıya alınan kullanıcı giriş yapamıyor; mevcut token'ı anında geçersiz
- [ ] Raporlayan kimliği hedef kullanıcıya gösterilmiyor
- [ ] Kullanıcı kendi bildirim/şikayetlerinin durumunu görebiliyor
- [ ] Projeksiyon yeniden inşa ucu çalışıyor
- [ ] Tam test paketi (Docker'lı) yeşil
