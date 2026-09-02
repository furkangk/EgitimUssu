# P09 — Üyelik, Entitlement ve Gelir (M17) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gelir modelini çalışır hale getirmek: yeni **Membership** modülü (plan + abonelik + entitlement), entitlement'ın tüm modüllere tek noktadan yayılması, ödeme sağlayıcı entegrasyonu (iyzico) + webhook, reklam yerleşimleri, ilk-ay-ücretsiz ve referans kampanyaları, mobilde paywall.

**Architecture:** `src/Modules/Membership` (4 katman, `membership` şeması) açılır. `SubscriptionPlan` (rol × tier × fiyat × periyot) ve `UserSubscription` (kullanıcı × plan × durum × dönem) aggregate'leri; entitlement `Shared/Contracts/IEntitlementDirectory` üzerinden okunur ve `MembershipChangedIntegrationEvent` ile yayılır. Mevcut dağınık üyelik kalıntıları (`Students.MembershipTier`, `Parents.MembershipTier`, `Study/MembershipGate`) bu tek kaynağa bağlanır. Ödeme `IPaymentGateway` arkasında; iyzico implementasyonu + imza doğrulamalı webhook. Reklam yerleşimi sunucudan yönetilir (`GET /ad-placements`), premium'da boş liste döner.

**Tech Stack:** .NET 9, EF Core, iyzico .NET SDK, xUnit; Flutter (google_mobile_ads, in-app paywall).

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (kararlar **K-05**, **K-06**, **K-09**; açık sorular **Q1–Q3**)

> ⚠️ **Yürütmeden önce Q1–Q3 cevaplanmalı:** iyzico ticari hesabı, nihai fiyat listesi, AdMob hesabı + yerleşim politikası. Cevap yoksa Task 1–3 ve 6 yapılabilir; Task 4 (ödeme) ve Task 5 (reklam) bloke.

## Global Constraints

- **Tek doğruluk kaynağı:** Entitlement yalnız Membership modülünde hesaplanır. Hiçbir modül kendi `MembershipTier` alanına göre karar vermez; `IEntitlementDirectory` çağırır.
- **Fail-closed değil, fail-safe:** Entitlement okunamazsa kullanıcı **Free** kabul edilir (premium özellik açılmaz), ama işlem hata vermez; `LogWarning`.
- **Para:** Tüm tutarlar `decimal` + `Currency` (varsayılan `TRY`). Kuruş yuvarlaması yok; sağlayıcıya gönderilen tutar birebir loglanır.
- **Webhook güvenliği:** İmza doğrulanmadan hiçbir abonelik durumu değişmez. Doğrulanamayan istek `401` ve `LogWarning`.
- **Idempotency:** Webhook aynı olayı iki kez gönderirse abonelik iki kez uzatılmaz (`ProviderEventId` benzersiz).
- **Migration:** `dotnet ef migrations add <Ad> --project src/Modules/Membership/Infrastructure --startup-project src/API.Host --context MembershipDbContext`
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Membership modül iskeleti + plan/abonelik domain'i

**Files:**
- Create: `src/Modules/Membership/Domain/EgitimUssu.Modules.Membership.Domain.csproj` + `MembershipDomainModel.cs` + `AssemblyReference.cs`
- Create: `src/Modules/Membership/Application/EgitimUssu.Modules.Membership.Application.csproj` + `AssemblyReference.cs`
- Create: `src/Modules/Membership/Infrastructure/EgitimUssu.Modules.Membership.Infrastructure.csproj` + `MembershipDbContext.cs` + `DependencyInjection.cs` + `MembershipDesignTimeDbContextFactory.cs`
- Create: `src/Modules/Membership/API/EgitimUssu.Modules.Membership.API.csproj` + `MembershipModule.cs`
- Modify: `EgitimUssu.slnx` (4 proje girdisi), `src/API.Host/ModuleAssemblies.cs`
- Test: `tests/Unit/SubscriptionTests.cs`

**Interfaces:**
- Produces:
  - `enum MembershipTier { Free = 1, Premium = 2 }`
  - `enum SubscriptionStatus { Trialing = 1, Active = 2, PastDue = 3, Cancelled = 4, Expired = 5 }`
  - `enum BillingPeriod { Monthly = 1, Yearly = 2 }`
  - `sealed class SubscriptionPlan : AggregateRoot<Guid>` — `string Code` (ör. `teacher-premium-monthly`), `string Role` (`Teacher|Student|Parent`), `MembershipTier Tier`, `BillingPeriod Period`, `decimal Price`, `string Currency`, `bool IsActive`, `int TrialDays`.
  - `sealed class UserSubscription : AggregateRoot<Guid>` — `Guid UserId`, `Guid PlanId`, `SubscriptionStatus Status`, `DateTime StartedOnUtc`, `DateTime CurrentPeriodEndUtc`, `bool AutoRenew`, `string? ProviderSubscriptionId`, `DateTime? CancelledOnUtc`; metotlar `StartTrial(int days, DateTime now)`, `Activate(DateTime periodEnd, DateTime now)`, `Renew(DateTime newPeriodEnd, DateTime now)`, `MarkPastDue(DateTime now)`, `Cancel(DateTime now)`, `ExpireIfDue(DateTime now)`.
  - Domain event: `MembershipChangedDomainEvent(Guid UserId, MembershipTier Tier, DateTime EffectiveUntilUtc, DateTime OccurredOnUtc)`.

- [ ] **Step 1: Domain testlerini yaz (kırmızı)**
```csharp
[Fact] public void StartTrial_Should_Set_Trialing_And_PeriodEnd() { }
[Fact] public void Activate_After_Trial_Should_Extend_Period() { }
[Fact] public void Cancel_Should_Keep_Access_Until_PeriodEnd() { }          // iptal ≠ anında kapanma
[Fact] public void ExpireIfDue_Should_Expire_Only_After_PeriodEnd() { }
[Fact] public void Renew_Should_Raise_MembershipChanged() { }
[Fact] public void Trial_Should_Be_Allowed_Once_Per_User() { }               // ikinci deneme reddedilir
```
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~SubscriptionTests"`
- [ ] **Step 3: 4 projeyi oluştur**
  Mevcut bir modülü şablon al (`Settings` en küçüğü): csproj referans zinciri `API → Application → Domain`, `Infrastructure → Application`, `Infrastructure → Shared.Infrastructure`.
  Run: `dotnet sln EgitimUssu.slnx add src/Modules/Membership/*/*.csproj` (veya `.slnx` içine Settings bloğunun eşleniğini elle ekle)
- [ ] **Step 4: Domain'i yaz** (yukarıdaki imzalar).
- [ ] **Step 5: DbContext + migration**
  `MembershipDbContext` — `SchemaName = "membership"`, `subscription_plans`, `user_subscriptions` (+ `HasIndex(UserId, Status)`).
  `ModuleAssemblies.cs` → `typeof(MembershipModule).Assembly`.
  Run: `dotnet ef migrations add InitialCreate --project src/Modules/Membership/Infrastructure --startup-project src/API.Host --context MembershipDbContext`
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx` (mimari testler yeni modülü de denetler; ihlal varsa referansları düzelt).
- [ ] **Step 7: Doküman + commit**

`doc/modules/m17_membership.md` (🔴 → 🟡, domain + şema), `doc/modules/00_genel_bakis.md` (modül tablosu + backend sütunu artık `Membership`), `doc/INDEX.md`, `doc/modules/veri_modeli.md`.
```bash
git add src/Modules/Membership EgitimUssu.slnx src/API.Host tests doc
git commit -m "feat(membership): modul iskeleti + plan/abonelik domain (M17-1)"
```

---

### Task 2: Entitlement sözleşmesi ve yayılımı (D-09)

**Files:**
- Create: `src/Shared/Contracts/EntitlementContract.cs`
- Create: `src/Modules/Membership/Infrastructure/EntitlementDirectory.cs`
- Create: `src/Modules/Membership/Application/MembershipFeatures.cs` (query + handler)
- Modify: `src/Modules/Study/Application/MembershipGate.cs` (yeni sözleşmeye devret)
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs:9` (`FreeStudentLimit` → entitlement)
- Modify: `src/Modules/Parents/**`, `src/Modules/Notifications/**` (tier okumaları)
- Test: `tests/Unit/EntitlementDirectoryTests.cs`, `tests/Unit/MembershipGateTests.cs` (mevcut dosyayı güncelle)

**Interfaces:**
- Produces:
  - ```csharp
    namespace EgitimUssu.Shared.Contracts;

    public sealed record Entitlement(
        MembershipTierContract Tier,
        DateTime? EffectiveUntilUtc,
        int MaxStudents,          // Free: 5, Premium: int.MaxValue
        bool CanSeeAds,           // Free: true, Premium: false
        bool DetailedAnalytics,
        bool PdfReports,
        bool RichNotifications);

    public enum MembershipTierContract { Free = 1, Premium = 2 }

    /// <summary>Kullanıcının hak ettiği özellikler. Tek kaynak: Membership modülü.</summary>
    public interface IEntitlementDirectory
    {
        Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken);
    }
    ```
  - `GET /api/membership/me` → `{ tier, effectiveUntilUtc, plan, entitlement }`
  - `MembershipChangedIntegrationEvent` (outbox) — tüketiciler kendi önbelleklerini tazeler.

- [ ] **Step 1: Testleri yaz (kırmızı)** — abonesi olmayan kullanıcı `Free` + `MaxStudents = 5`; aktif premium `MaxStudents = int.MaxValue` + `CanSeeAds = false`; süresi geçmiş abonelik `Free`; `Cancelled` ama dönem sonu gelmemiş abonelik **hâlâ Premium**.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EntitlementDirectoryTests"`
- [ ] **Step 3: Sözleşme + directory + `GET /me` ucunu yaz.**
- [ ] **Step 4: Tüketicileri devret**
  - `Study/MembershipGate.cs` artık `IEntitlementDirectory` kullanır (imza korunur, iç gövde değişir).
  - `Students`: `FreeStudentLimit` sabiti kaldırılır; öğrenci ekleme handler'ı `entitlement.MaxStudents` ile karşılaştırır.
  - `Parents`/`Notifications`: `MembershipTier` alanına bakan yerler sözleşmeye geçer. **Alanlar hemen silinmez**; `[Obsolete]` işaretlenir ve P12'de temizlenir (veri göçü riski).
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

```bash
git add src/Shared/Contracts src/Modules tests doc
git commit -m "feat(membership): entitlement sozlesmesi + tum modullerin devri (D-09/M03-4)"
```

---

### Task 3: Plan yönetimi, deneme ve referans kampanyası (M17-1/M17-3)

**Files:**
- Modify: `src/Modules/Membership/Domain/MembershipDomainModel.cs` (`ReferralCode`, `ReferralRedemption`)
- Modify: `src/Modules/Membership/Application/MembershipFeatures.cs`
- Modify: `src/Modules/Membership/API/MembershipModule.cs`
- Modify: `src/Modules/Membership/Infrastructure/*` + migration
- Test: `tests/Unit/ReferralTests.cs`, `tests/Unit/TrialTests.cs`

**Interfaces:**
- Produces:
  - `GET /api/membership/plans?role=` (anonim erişilebilir — paywall ekranı için `IAllowAnonymous`)
  - `POST /api/membership/subscriptions/start-trial` (auth) — kullanıcı başına **bir kez**
  - `POST /api/membership/subscriptions/cancel` (auth)
  - `GET /api/membership/referral-code` (auth) — kullanıcıya ait kod (yoksa üretir)
  - `POST /api/membership/referral/redeem` (auth) — kayıt sonrası kod girer; **her iki tarafa** 1 ay premium
  - Admin: `POST /api/membership/plans`, `PUT /api/membership/plans/{planId}` (yalnız Admin)

- [ ] **Step 1: Testleri yaz (kırmızı)** — kendi kodunu kullanamama; aynı kodu iki kez kullanamama; kod geçersizse `membership.invalid_referral`; ödül iki tarafa da 1 ay ekliyor; deneme ikinci kez başlatılamıyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ReferralTests|FullyQualifiedName~TrialTests"`
- [ ] **Step 3: Domain + handler + endpoint + migration**
  Run: `dotnet ef migrations add AddReferralAndTrial --project src/Modules/Membership/Infrastructure --startup-project src/API.Host --context MembershipDbContext`
- [ ] **Step 4: Plan tohumlama (seed)** — `codex/03_paket_fiyatlandirma.md`'deki fiyatlarla 6 plan (3 rol × aylık/yıllık) `MembershipDbContext` seed'i veya idempotent startup görevi olarak eklenir. **Fiyatlar Q2 ile doğrulanmadan prod'a çıkmaz** — seed yalnız Development'ta koşar.
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

```bash
git add src/Modules/Membership tests doc
git commit -m "feat(membership): plan yonetimi + deneme + referans kampanyasi (M17-1/M17-3)"
```

---

### Task 4: Ödeme sağlayıcı (iyzico) + webhook (M17-4)

> **Bloklayıcı:** Q1 (iyzico hesabı) cevaplanmadan başlama.

**Files:**
- Create: `src/Modules/Membership/Application/IPaymentGateway.cs`
- Create: `src/Modules/Membership/Infrastructure/IyzicoPaymentGateway.cs`
- Create: `src/Modules/Membership/Infrastructure/WebhookSignatureValidator.cs`
- Modify: `src/Modules/Membership/API/MembershipModule.cs` (checkout + webhook uçları)
- Modify: `src/Modules/Membership/Domain/MembershipDomainModel.cs` (`ProviderEventId` benzersizliği)
- Modify: `src/Shared/Infrastructure/Configuration/` (`PaymentOptions` + guard)
- Test: `tests/Unit/WebhookSignatureValidatorTests.cs`, `tests/Unit/SubscriptionWebhookHandlerTests.cs`

**Interfaces:**
- Produces:
  - `interface IPaymentGateway { Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct); Task<bool> CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken ct); }`
  - `sealed record CheckoutRequest(Guid UserId, string Email, string PlanCode, decimal Price, string Currency, string CallbackUrl)`
  - `sealed record CheckoutSession(string ProviderSessionId, string RedirectUrl)`
  - `POST /api/membership/checkout` (auth) → `{ redirectUrl }`
  - `POST /api/membership/webhooks/iyzico` (**anonim**, imza doğrulamalı) → abonelik durumunu günceller

- [ ] **Step 1: İmza doğrulama testini yaz (kırmızı)** — sağlayıcının dokümanındaki örnek imzayla doğru/yanlış senaryolar; imzasız istek reddedilir; zaman aşımı (replay) penceresi dışındaki istek reddedilir.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~WebhookSignatureValidatorTests"`
- [ ] **Step 3: SDK ekle + gateway'i yaz**
  Run: `dotnet add src/Modules/Membership/Infrastructure/EgitimUssu.Modules.Membership.Infrastructure.csproj package Iyzipay`
  Sırlar: `Payment__ApiKey`, `Payment__SecretKey`, `Payment__BaseUrl` (env; `appsettings.json`'da boş).
- [ ] **Step 4: Webhook handler'ı yaz** — `ProviderEventId` daha önce işlendiyse `200 OK` döner ve **hiçbir şey yapmaz** (idempotent). Durum eşlemesi: ödeme başarılı → `Activate`/`Renew`; başarısız → `MarkPastDue`; iptal → `Cancel`.
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Sandbox'ta uçtan uca dene** — iyzico sandbox kartıyla checkout → callback → abonelik `Active` → `GET /api/membership/me` premium döner.
- [ ] **Step 7: Doküman + commit**

`doc/modules/m17_membership.md` (ödeme akışı diyagramı + webhook sözleşmesi + sır listesi).
```bash
git add src/Modules/Membership src/Shared tests doc
git commit -m "feat(membership): iyzico odeme entegrasyonu + imzali webhook (M17-4)"
```

---

### Task 5: Reklam yerleşimleri (M17-2)

> **Bloklayıcı:** Q3 (AdMob hesabı + yerleşim politikası) cevaplanmadan başlama.

**Files:**
- Modify: `src/Modules/Membership/Application/MembershipFeatures.cs` (`GetAdPlacementsQuery`)
- Modify: `src/Modules/Membership/API/MembershipModule.cs`
- Modify: `mobile/pubspec.yaml` (`google_mobile_ads`)
- Create: `mobile/lib/features/membership/presentation/widgets/ad_slot.dart`
- Test: `tests/Unit/AdPlacementTests.cs`, `mobile/test/features/membership/ad_slot_test.dart`

**Interfaces:**
- Produces:
  - `GET /api/membership/ad-placements` (auth) → `[{ slot: "student_home_bottom", unitId: "...", format: "banner" }]`; **premium kullanıcıda boş dizi**.
  - `AdSlot(slot: 'student_home_bottom')` widget'ı — yerleşim listesinde yoksa `SizedBox.shrink()` döner.

- [ ] **Step 1: Testleri yaz (kırmızı)** — premium'da boş liste; free'de tanımlı yerleşimler; bilinmeyen slot için widget hiçbir şey çizmiyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~AdPlacementTests"`
- [ ] **Step 3: Sunucu ucunu yaz** (yerleşim tanımları konfigürasyondan: `Ads:Placements`).
- [ ] **Step 4: Mobil `AdSlot` widget'ı** — `google_mobile_ads` banner; yükleme başarısızsa sessizce gizlenir (layout zıplaması yok: sabit yükseklik rezervi).
- [ ] **Step 5: Yerleşimleri ekranlara koy** — Q3'te kararlaştırılan ekranlara; **ders/çalışma akışını kesen tam ekran reklam yok** (ürün kararı: yalnız banner + doğal yerleşim).
- [ ] **Step 6: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 7: Doküman + commit**

```bash
git add src/Modules/Membership mobile tests doc
git commit -m "feat(membership): reklam yerlesimleri, premium'da kapali (M17-2)"
```

---

### Task 6: Mobil — üyelik ekranı ve paywall (D-10/D-15)

**Files:**
- Create: `mobile/lib/features/membership/**` (data/domain/presentation)
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` ("Üyelik" satırı aktif — P05'te pasifleştirilmişti)
- Modify: `mobile/lib/core/routing/app_router.dart` (`/membership`, `/membership/paywall`)
- Create: `mobile/lib/shared/widgets/premium_lock.dart`
- Test: `mobile/test/features/membership/membership_cubit_test.dart`, `mobile/test/shared/premium_lock_test.dart`
- Create: `doc/pages/membership.md`, `doc/pages/paywall.md`

**Interfaces:**
- `MembershipRepository`: `me()`, `plans(role)`, `startTrial()`, `checkout(planCode)`, `cancel()`, `referralCode()`, `redeemReferral(code)`.
- `PremiumLock` widget'ı: `child` premium ise gösterilir; değilse bulanık/kilitli görünüm + "Premium'a geç" → `/membership/paywall`.

- [ ] **Step 1: Cubit ve widget testlerini yaz (kırmızı)** — premium kullanıcıda `PremiumLock` içeriği doğrudan gösteriyor; free'de kilit + yönlendirme; `startTrial` sonrası durum premium'a dönüyor.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/membership test/shared/premium_lock_test.dart`
- [ ] **Step 3: Repository + cubit'i yaz.**
- [ ] **Step 4: Üyelik ekranı** — mevcut plan, dönem sonu, otomatik yenileme anahtarı, iptal, referans kodu paylaşma.
- [ ] **Step 5: Paywall ekranı** — PRD §9 tablosundan rol bazlı özellik karşılaştırması, plan kartları, "İlk ay ücretsiz" rozeti, checkout yönlendirmesi (harici tarayıcı + geri dönüş derin bağlantısı `egitimussu://membership/callback`).
- [ ] **Step 6: `PremiumLock`'u premium özelliklere uygula** — PDF rapor (P08), detaylı analiz, veli zengin bildirim, öğrenci geçmiş/streak (PRD §9.1–9.3 tablosu esas alınır).
- [ ] **Step 7: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 8: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): uyelik ekrani + paywall + premium kilitleri (D-10/M17-5)"
```

---

### Task 7: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → yeşil.
- [ ] **Step 2: Uçtan uca** — Free öğretmen 6. öğrenciyi ekleyemiyor → paywall → deneme başlat → 6. öğrenci eklenebiliyor → deneme bitince tekrar kilit.
- [ ] **Step 3: Dokümanlar** — `doc/modules/m17_membership.md` (🔴 → 🟢), `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/roles/*.md` premium yetenekleri, PRD §9 ile kod arasındaki farklar, `doc/denetim/2026-09-02_eksik_analizi.md` M17-*, M03-4, D-10, D-15 → `✅ (P09)`.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P09 uyelik ve gelir kapanisi (M17-*/M03-4/D-10)"
```

---

## Kabul Kriterleri

- [ ] Her rol için free + premium plan tanımlı; kullanıcı planını `GET /api/membership/me` ile görüyor
- [ ] Premium kullanıcı hiç reklam görmüyor (`ad-placements` boş)
- [ ] Free limitleri entitlement'tan geliyor; hiçbir modülde sabit limit kalmadı (`grep -rn "FreeStudentLimit" src/` boş)
- [ ] İlk ay ücretsiz kullanıcı başına bir kez çalışıyor
- [ ] Referans kodu iki tarafa da 1 ay veriyor; kendi kodu kullanılamıyor
- [ ] Webhook imzasız istekte 401; aynı olay iki kez gelince abonelik iki kez uzamıyor
- [ ] İptal edilen abonelik dönem sonuna kadar premium kalıyor, sonra Free'ye düşüyor
- [ ] Mobilde paywall ve premium kilitleri PRD §9 ile birebir
- [ ] Tam test paketi (Docker'lı) yeşil
