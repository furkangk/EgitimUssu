# P09 — Üyelik, Entitlement ve Gelir (M17) Implementation Plan

> **🔁 Revizyon 2026-11-13 (Olgunlaştırma programı, K-25):** **Task 4 (iyzico + webhook) iptal edildi → [P28 Gelir v2](2026-11-13-28-gelir-v2-iap.md)**: mobilde premium Apple 3.1.1 / Play Billing gereği mağaza içi satın almayla satılır; iyzico yalnız ileride özel ders ücreti tahsilatı için (K-05 daraltıldı). **Task 5 (AdMob)** P28'de opsiyonel Task 5 oldu (Q3). Bu plan **5/7 ile kapandı**; kalan iş P28'de.

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
- Create: `src/Modules/Membership/Infrastructure/Migrations/*_InitialCreate.*` + `MembershipDbContextModelSnapshot.cs`
- Modify: `EgitimUssu.slnx` (4 proje girdisi), `src/API.Host/ModuleAssemblies.cs`
- Modify: `src/API.Host/EgitimUssu.API.Host.csproj` *(plan dışı, zorunlu: `ModuleAssemblies.cs` yeni modülü ancak proje referansı varsa görebilir)*
- Modify: `tests/Unit/EgitimUssu.Tests.Unit.csproj` *(plan dışı, zorunlu — Öğrenilenler #72 ile aynı: yeni modülün Domain referansı test projesine eklenmeli)*
- Test: `tests/Unit/SubscriptionTests.cs`

**Interfaces:**
- Produces:
  - ⚠️ **Plan düzeltmesi (2026-09-04):** `enum MembershipTier { Free = 1, Premium = 2 }` **yazılmadı** —
    aynı adlı ve aynı değerli enum `Shared/Contracts/MembershipDirectoryContract.cs`'te **zaten var** ve
    Students/Parents/Study/Reporting hepsi onu kullanıyor. İkinci bir tanım, P09'un "üyeliği tek kaynağa
    bağla" amacının tam tersi olurdu (Öğrenilenler #100 ile aynı tuzak). `Membership.Domain` bunun yerine
    `Shared.Contracts`'a referans veriyor — `Students.Domain` ve `Parents.Domain` ile aynı desen,
    mimari testler yeşil.
  - `enum SubscriptionStatus { Trialing = 1, Active = 2, PastDue = 3, Cancelled = 4, Expired = 5 }`
  - `enum BillingPeriod { Monthly = 1, Yearly = 2 }`
  - `sealed class SubscriptionPlan : AggregateRoot<Guid>` — `string Code` (ör. `teacher-premium-monthly`), `string Role` (`Teacher|Student|Parent`), `MembershipTier Tier`, `BillingPeriod Period`, `decimal Price`, `string Currency`, `bool IsActive`, `int TrialDays`.
  - `sealed class UserSubscription : AggregateRoot<Guid>` — `Guid UserId`, `Guid PlanId`, `SubscriptionStatus Status`, `DateTime StartedOnUtc`, `DateTime CurrentPeriodEndUtc`, `bool AutoRenew`, `string? ProviderSubscriptionId`, `DateTime? CancelledOnUtc`; metotlar `StartTrial(int days, DateTime now)`, `Activate(DateTime periodEnd, DateTime now)`, `Renew(DateTime newPeriodEnd, DateTime now)`, `MarkPastDue(DateTime now)`, `Cancel(DateTime now)`, `ExpireIfDue(DateTime now)`.
  - Domain event: `MembershipChangedDomainEvent(Guid UserId, MembershipTier Tier, DateTime EffectiveUntilUtc, DateTime ChangedOnUtc)`.
    *(Son parametre `ChangedOnUtc` adını aldı: `DomainEvent` tabanı zaten bir `OccurredOnUtc` taşıyor, aynı adı
    gölgelemek karışıklık üretirdi.)*
  - Ek üretilenler *(plan listelemiyordu, kural için gerekli)*: `MembershipErrors` (hata kodları
    `membership.*`), `UserSubscription.HasUsedTrial`, `HasAccess(now)`, `EffectiveTier(now)`,
    `LinkProviderSubscription(id)`, `SubscriptionPlan.NextPeriodEnd(from)` (takvim ayı/yılı — 30/365 gün değil).

- [x] **Step 1: Domain testlerini yaz (kırmızı)** — planın 6 testi + 6 ek (geçmiş tarihli dönem reddi,
  `ExpireIfDue` idempotency'si, sıfır/negatif deneme günü, `MarkPastDue`'da erişimin sürmesi, süresi dolmuş
  aboneliğin ödemeyle canlanması, plan alanları + `NextPeriodEnd`) = **12 test**
```csharp
[Fact] public void StartTrial_Should_Set_Trialing_And_PeriodEnd() { }
[Fact] public void Activate_After_Trial_Should_Extend_Period() { }
[Fact] public void Cancel_Should_Keep_Access_Until_PeriodEnd() { }          // iptal ≠ anında kapanma
[Fact] public void ExpireIfDue_Should_Expire_Only_After_PeriodEnd() { }
[Fact] public void Renew_Should_Raise_MembershipChanged() { }
[Fact] public void Trial_Should_Be_Allowed_Once_Per_User() { }               // ikinci deneme reddedilir
```
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~SubscriptionTests"` → **CS0234** (`EgitimUssu.Modules.Membership` ad alanı yok) + **CS0246** (`UserSubscription` yok)
- [x] **Step 3: 4 projeyi oluştur** — `Settings` şablon alındı; `.slnx`'e Membership bloğu alfabetik yere (Matching ↔ Notifications arası) elle eklendi. `Domain` ek olarak `Shared.Contracts`'a referans verir (yukarıdaki plan düzeltmesi).
- [x] **Step 4: Domain'i yaz** — `SubscriptionPlan` + `UserSubscription` + iki enum + hata kodları + olay.
  ⚠️ **Tasarım notu:** **erişim ile durum aynı şey değildir.** `Cancelled` ve `PastDue` durumlarında da kullanıcı `CurrentPeriodEndUtc`'a kadar Premium'dur (ödediği süreyi kaybetmez); tier'ı Free'ye düşüren **tek** geçiş `ExpireIfDue`'dur ve olay orada yayılır. `Cancel` bilinçli olarak **olay yaymaz**.
- [x] **Step 5: DbContext + migration** — `MembershipDbContext` (`membership` şeması), `subscription_plans` (benzersiz `Code`, `(Role, IsActive)`), `user_subscriptions` (**benzersiz `UserId`**, `(Status, CurrentPeriodEndUtc)`, `ProviderSubscriptionId`).
  ⚠️ **Plan düzeltmesi:** plan `HasIndex(UserId, Status)` diyordu; doğrusu **benzersiz `UserId`**. "Deneme kullanıcı başına bir kez" kuralı `HasUsedTrial` bayrağıyla taşınıyor ve bu ancak kullanıcı başına **tek** abonelik satırı varken anlamlı — bileşik, benzersiz olmayan bir indeks ikinci bir satıra (dolayısıyla ikinci bir denemeye) izin verirdi.
  Run: `dotnet ef migrations add InitialCreate ...` ✅ **5 tablo** (2 domain + ortak outbox/inbox/module_states) + 6 indeks.
- [x] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx` → 4 mimari + **568 birim** + **39 integration**, Başarısız 0, Atlanan 0 (öncesi 556 birim; +12) · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata · Integration testleri host'u tüm modüllerle ayağa kaldırıp migration'ları **gerçek Postgres'e** uyguladığı için `membership` şemasının uygulanabilirliği de bu turda doğrulandı.
- [x] **Step 7: Doküman + commit**

`doc/modules/m17_membership.md` (🔴 → 🟡, §1 "kodda olan / hâlâ olmayan" ikiye ayrıldı), `doc/modules/00_genel_bakis.md` (backend sütunu `_(yok)_` → `Membership`, durum 🔴 → 🟡), `doc/INDEX.md`, `doc/modules/veri_modeli.md` (M17 satırı + iki yeni enum).
```bash
git add src/Modules/Membership EgitimUssu.slnx src/API.Host tests doc
git commit -m "feat(membership): modul iskeleti + plan/abonelik domain (M17-1)"
```

---

### Task 2: Entitlement sözleşmesi ve yayılımı (K-09 / M17-5 / M03-4)

> ⚠️ **Plan düzeltmesi (2026-09-04):** başlıktaki **`D-09` etiketi yanlıştı.** Eksik envanterinde
> (`doc/denetim/2026-09-02_eksik_analizi.md`) `D-09` = *"Puanlama / yorum ekranları"* ve **P11**'e ait.
> Bu görevin doğru kimlikleri: karar **K-09** (master tasarım §2) + envanter **M17-5** (entitlement yayılımı)
> ve **M03-4** (öğretmen öğrenci limiti). Kod yorumları ve dokümanlar K-09 ile etiketlendi.

**Files:**
- Create: `src/Shared/Contracts/EntitlementContract.cs`
- Create: `src/Modules/Membership/Infrastructure/EntitlementDirectory.cs`
- Create: `src/Modules/Membership/Application/MembershipFeatures.cs` (query + handler)
- Modify: `src/Modules/Membership/API/MembershipModule.cs` + `Infrastructure/DependencyInjection.cs` *(plan dışı, zorunlu: `GET /me` ucu ve DI kayıtları)*
- Modify: `src/Modules/Study/Application/StudyPolicies.cs` (`StudyMembershipResolver` — plan `MembershipGate.cs` diyordu; o dosya P08'de `Shared/Contracts`'a taşındı ve **saf** kaldı, devredilen yer resolver'dır)
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs` (`FreeStudentLimit` → entitlement)
- Modify: `src/Modules/Parents/Infrastructure/ParentNotificationDirectory.cs` + `DependencyInjection.cs`
- Create: `src/Modules/Parents/Infrastructure/ParentMembershipDirectory.cs` *(plan dışı — geçiş köprüsü, aşağıya bak)*
- Modify: `src/Modules/Reporting/Application/StudentReportFeatures.cs` *(plan dışı, zorunlu: Öğrenilenler #113'ün kapatılacağı tek yer burasıydı — aksi hâlde öğretmen PDF'i kalıcı 402 kalırdı)*
- Modify: `src/Shared/Contracts/MembershipDirectoryContract.cs` (LEGACY belgesi), `src/Modules/Students/{Domain,Infrastructure}` (LEGACY işaretleri)
- Modify: `tests/Unit/EgitimUssu.Tests.Unit.csproj` (+Membership Application/Infrastructure), `src/Modules/Membership/Infrastructure/*.csproj` (`InternalsVisibleTo`), `tests/Architecture/ProjectArchitectureTests.cs` (`Membership` modül listesine eklendi — Task 1'de unutulmuştu)
- Test: `tests/Unit/EntitlementDirectoryTests.cs`, `tests/Unit/TestDoubles/FakeEntitlementDirectory.cs` (ortak sahte), `StudentFreeLimitTests.cs`, `MembershipGateHandlerTests.cs`, `StudentReportPdfGeneratorTests.cs`
- **Notifications'a dokunulmadı:** handler'lar seviyeyi `ParentNotificationTarget.Tier` üzerinden okuyor; kaynağı Parents'ın directory'si doldurduğu için tek satır değişmeden doğru kaynağa bağlandı.

**Interfaces:**
- Produces:
  - ```csharp
    namespace EgitimUssu.Shared.Contracts;

    public sealed record Entitlement(
        MembershipTier Tier,        // ⚠️ plandaki MembershipTierContract YAZILMADI — bkz. aşağıdaki düzeltme
        DateTime? EffectiveUntilUtc,
        int MaxStudents,          // Free: 5, Premium: int.MaxValue
        bool CanSeeAds,           // Free: true, Premium: false
        bool DetailedAnalytics,
        bool PdfReports,
        bool RichNotifications);

    public interface IEntitlementDirectory
    {
        Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken);

        // ⚠️ Plan dışı, zorunlu: haftalık veli özeti TÜM onaylı velileri tarıyor; tekil çağrı N sorgu ederdi.
        Task<IReadOnlyDictionary<Guid, Entitlement>> GetManyAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    }
    ```
  - ⚠️ **Plan düzeltmesi:** `enum MembershipTierContract` **yazılmadı** — aynı anlamı taşıyan
    `Shared.Contracts.MembershipTier` zaten var ve tüm modüller onu kullanıyor (Task 1'deki aynı
    tuzak, Öğrenilenler #100). İkinci bir enum "tek kaynak" amacının tersi olurdu.
  - `GET /api/membership/me` (auth) → `{ tier, effectiveUntilUtc, planCode, status, entitlement }`
  - `MembershipChangedIntegrationEvent` (outbox) — **bu görevde yazılmadı**: entitlement her okumada
    taze hesaplandığı için tüketici-tarafı önbellek yok, dolayısıyla tazelenecek bir şey de yok.
    Olay, önbellek/projeksiyon gerektiğinde (Task 3-4) eklenecek.

- [x] **Step 1: Testleri yaz (kırmızı)** — plandaki 4 senaryo + 6 ek (abonelik legacy'yi yener, `GetManyAsync`
  her kimliği yanıtlar, fail-safe, `/me` anlık görüntüsü plan kodu + durum taşır, `/me` handler'ı) = **10 test**
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EntitlementDirectoryTests"` → **CS0246** (`EntitlementDirectory`, `IMembershipReader`, `UserMembershipSnapshot` yok)
- [x] **Step 3: Sözleşme + directory + `GET /me` ucunu yaz.** `EntitlementPolicy` (seviye→hak eşlemesinin tek yeri) `Membership.Application`'a kondu — `Shared.Contracts`'a konsaydı her modül entitlement'ı kendi hesaplayabilirdi.
- [x] **Step 4: Tüketicileri devret**
  - `StudyMembershipResolver` → `IEntitlementDirectory` (imza korundu, gövde değişti).
  - `Students`: `StudentLimits.FreeStudentLimit` **silindi**; handler öğretmenin `entitlement.MaxStudents`'ini okuyor.
  - `Reporting`: geçici `ReportEntitlement.EffectiveTier` ("taraflardan biri Premium") **silindi**; kural
    artık **istek sahibinin** `entitlement.PdfReports` hakkı (Admin muaf) — Öğrenilenler #113 kapandı.
  - `Parents`: `ParentNotificationDirectory` seviyeyi `GetManyAsync` ile okuyor.
  - ⚠️ **Plan düzeltmesi — `[Obsolete]` attribute'u kullanılmadı:** repo `-warnaserror` ile derleniyor;
    attribute, alanı **okumak zorunda olan geçiş köprüsünde** derleme hatası üretir ve her satıra
    `#pragma warning disable CS0618` gerektirirdi. Yerine XML doküman başlıklarına
    **⚠️ LEGACY (P12'de silinecek)** notu kondu. Gerçek koruma zaten attribute değil, kararın tek
    noktaya taşınmış olmasıdır.
  - ⚠️ **Plan dışı — geçiş köprüsü:** plan eski alanların "okunmaz" hâle gelmesini varsayıyordu; ama
    bugün Premium olmanın **tek** yolu admin'in `PUT .../membership-tier` çağrısı ve abonelik akışı
    Task 3-4'te geliyor. Köprüsüz bırakmak, Task 2 ile Task 4 arasında premium'u sessizce kapatırdı.
    Bu yüzden `IMembershipDirectory` **LEGACY köprü sözleşmesine** indirildi (Students + yeni
    `ParentMembershipDirectory`), tek tüketicisi `EntitlementDirectory` ve `IEnumerable<>` olarak çözülüyor.
- [x] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx` → 4 mimari + **579 birim** + **39 integration**, Başarısız 0, Atlanan 0 (öncesi 568 birim) · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata · migration **gerekmedi** (şema değişmedi)
- [x] **Step 6: Doküman + commit**

`doc/modules/m17_membership.md` (§1 entitlement bloğu + §3 kodda olan uç), `00_genel_bakis.md` (M17 satırı + sözleşme envanteri), `m03_students.md` (limit→entitlement), `m14_reporting.md` (kapı devri, #113 kapandı), `m08_study.md`, `m09_parents.md`, `m11_notifications.md`, `veri_modeli.md` (LEGACY alanlar), `doc/INDEX.md`.
```bash
git add src/Shared/Contracts src/Modules tests doc
git commit -m "feat(membership): entitlement sozlesmesi + tum modullerin devri (K-09/M17-5/M03-4)"
```

---

### Task 3: Plan yönetimi, deneme ve referans kampanyası (M17-1/M17-3)

**Files:**
- Modify: `src/Modules/Membership/Domain/MembershipDomainModel.cs` (`ReferralCode`, `ReferralRedemption`, `ReferralCodeFactory`, `UserSubscription.GrantCampaignMonths`/`SwitchPlan`, `SubscriptionPlan.UpdatePricing`, `PlanId` → nullable)
- Modify: `src/Modules/Membership/Application/MembershipFeatures.cs`
- Modify: `src/Modules/Membership/API/MembershipModule.cs`
- Modify: `src/Modules/Membership/Infrastructure/MembershipDbContext.cs` + `DependencyInjection.cs` + migration `AddReferralAndTrial`
- Create: `src/Modules/Membership/Infrastructure/MembershipRepository.cs`, `MembershipPlanSeeder.cs` *(plan dışı, zorunlu: plan yalnız "Infrastructure/*" diyordu; yazma yolu bir depoya, seed ayrı bir dosyaya ihtiyaç duydu)*
- Modify: `src/Modules/Membership/Infrastructure/EntitlementDirectory.cs` *(plan dışı, zorunlu: `PlanId` nullable olunca plan kodu sorgusu koşullu hâle geldi)*
- Modify: `src/API.Host/Program.cs` *(plan dışı, zorunlu: Step 4 "seed yalnız Development'ta koşar" diyor; bunu `HasData` ile yapmak fiyatları migration'a — dolayısıyla üretime — gömerdi, tek yol startup kancası)*
- Test: `tests/Unit/ReferralTests.cs`, `tests/Unit/TrialTests.cs`, `tests/Unit/SubscriptionPlanTests.cs` *(plan dışı: görev üç konu taşıyor (plan yönetimi / deneme / referans) ama plan iki test dosyası listeliyordu; plan kataloğu + Admin CRUD testlerini "TrialTests" içine tıkmak yerine kendi dosyasına konuldu)*

**Interfaces:**
- Produces:
  - `GET /api/membership/plans?role=` (anonim erişilebilir — paywall ekranı için `IAllowAnonymous`)
  - `POST /api/membership/subscriptions/start-trial` (auth) — kullanıcı başına **bir kez**
  - `POST /api/membership/subscriptions/cancel` (auth)
  - `GET /api/membership/referral-code` (auth) — kullanıcıya ait kod (yoksa üretir)
  - `POST /api/membership/referral/redeem` (auth) — kayıt sonrası kod girer; **her iki tarafa** 1 ay premium
  - Admin: `POST /api/membership/plans`, `PUT /api/membership/plans/{planId}` (yalnız Admin)
  - ⚠️ **Plan düzeltmesi (2026-09-04) — `UserSubscription.PlanId` nullable yapıldı.** Referans ödülü
    satın alınmış bir plana bağlı değildir; henüz hiç aboneliği olmayan bir kullanıcıya ödül yazmak
    için ona bir plan **atamak** gerekirdi. Rol başına plan seçmek, planların tohumlanmış olmasını
    şart koşardı — ama tohumlama yalnız Development'ta koşuyor, yani üretimde referans akışı sessizce
    kırılırdı. Bunun yerine kampanya aboneliği **plansız** açılıyor (`PlanId = null`); kullanıcı
    sonradan denemeye/satın almaya geçince `SwitchPlan` ile bağlanıyor.
  - Ek üretilenler *(plan listelemiyordu, kural için gerekli)*: `ReferralCodeFactory` (kod kullanıcı
    kimliğinden **deterministik** türetilir — rastgele üretim çakışmada yeniden-deneme döngüsü ister
    ve testte sabitlenemezdi; alfabede karıştırılabilir `0/O/1/I/L` yok), `MembershipCampaigns`
    (ödül ayı + satılabilir rol listesi), `IMembershipRepository`, `MembershipStatusFactory`,
    `AdminOnlyMembershipAuthorizer` + `AuthenticatedMembershipAuthorizer`
    (`AuthorizationCoverageValidator` her komut için authorizer zorunlu kılıyor),
    `UserSubscription.GrantCampaignMonths` (ödül mevcut dönemi **ezmez, uzatır**),
    `SubscriptionPlan.UpdatePricing` (kod/rol/periyot kararlı anahtar → değişmez).
  - ⚠️ **Uçlarda `{userId}` yok:** hedef kullanıcı gövdeden değil **oturumdan** gelir; başkasının
    aboneliğine dokunmanın yolu böylece sözleşme düzeyinde kapalı.

- [x] **Step 1: Testleri yaz (kırmızı)** — plandaki 5 senaryo + 18 ek (iptal sonrası deneme yine reddedilir,
  bilinmeyen plan, denemesiz plan, iptalde erişimin sürmesi, aboneliksiz iptal, ödülün mevcut premium'u
  **uzatması**, kodun deterministik ve karıştırılamaz olması, plan listeleme/filtre, Admin CRUD + çift kod
  + geçersiz rol + bilinmeyen plan, Admin-dışı reddi, seed kapsamı) = **23 test** (7 + 8 + 8)
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ReferralTests|FullyQualifiedName~TrialTests|FullyQualifiedName~SubscriptionPlanTests"` → **CS0246** (`StartTrialCommandHandler`, `CancelSubscriptionCommandHandler`, `EnsureReferralCodeCommandHandler`, `RedeemReferralCommandHandler` yok)
- [x] **Step 3: Domain + handler + endpoint + migration**
  Run: `dotnet ef migrations add AddReferralAndTrial ...` ✅ 2 yeni tablo (`referral_codes` benzersiz `Code` +
  benzersiz `OwnerUserId`; `referral_redemptions` **benzersiz `RedeemerUserId`**) + `user_subscriptions.PlanId`
  nullable'a alındı.
  ⚠️ **Tasarım notu — "bir kullanıcı ömrü boyunca tek referans ödülü"** kuralı hem handler'da hem
  **veritabanı indeksinde** duruyor: uygulama kontrolü eşzamanlı iki istekte yarışabilir, benzersiz indeks
  yarışı kapatır. Aynı gerekçeyle "deneme bir kez" `HasUsedTrial` + benzersiz `UserId` ikilisiyle taşınıyor.
- [x] **Step 4: Plan tohumlama (seed)** — `MembershipPlanSeeder` (idempotent: yalnız **eksik kodları** ekler,
  var olan planın fiyatına dokunmaz — Admin'in elle yaptığı değişikliği her açılışta geri almamak için),
  `Program.cs`'te `ApplyMigrationsOnStartup` + `IsDevelopment()` altında. 6 plan: öğretmen 249/2490,
  öğrenci 99/990, veli 99/990 TRY — `codex/03_paket_fiyatlandirma.md` §4 aralığının **alt sınırı**
  (aynı dokümanın 5. ilkesi). Aylık planlarda 30 günlük deneme = "ilk ay ücretsiz".
  ⚠️ **Q2 hâlâ açık:** fiyatlar hipotezdir; üretimde plan kataloğu Admin ucundan girilir.
- [x] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx` → 4 mimari + **602 birim** + **39 integration**, Başarısız 0, Atlanan 0 (öncesi 579 birim; +23) · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata
- [x] **Step 6: Doküman + commit**

`doc/modules/m17_membership.md` (§1 plan/kampanya bloğu, §2.4 kodda-olan sadeleşme notu, §3 uç sözleşmesi, §8 durum),
`00_genel_bakis.md` (**Membership kendi uç bölümünü aldı** — 2 → 9 uç), `veri_modeli.md` (iki yeni tablo + nullable `PlanId`),
`doc/INDEX.md`, `doc/roles/admin.md` (plan yönetimi yeteneği), `doc/roles/{ogretmen,ogrenci,veli}.md` (deneme/iptal/referans).

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
  ⚠️ **Plan düzeltmesi (2026-09-04, P09 Task 6 sırasında):** bu kutu yanlışlıkla işaretliydi ve satırın
  içeriği Task 3 Step 5'in kopyasıydı. Task 4 hiç başlamadı (Q1 bloke); kutu geri alındı.
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
- Create: `mobile/lib/features/membership/**` — `domain/membership_contracts.dart`, `domain/premium_features.dart` *(plan dışı: paywall karşılaştırma tablosunun verisi)*, `data/repositories/membership_repository_impl.dart`, `presentation/cubit/membership_cubit.dart`, `presentation/pages/{membership_page,paywall_page}.dart`, `presentation/widgets/membership_widgets.dart`
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` ("Üyelik" satırı aktif + "Plus üye" rozeti gerçek üyelikten; kullanılmaz kalan `_MenuTile.enabled`/`.badge` silindi)
- Modify: `mobile/lib/core/routing/app_router.dart` (`/membership`, `/membership/paywall`)
- Create: `mobile/lib/shared/widgets/premium_lock.dart` (`PremiumLock` + `PremiumLockCard` + `kPaywallRoute`)
- Modify: `mobile/lib/core/di/injector.dart` *(plan dışı, zorunlu: `MembershipRepository` kaydı)*
- Modify: `mobile/lib/app/app.dart` *(plan dışı, zorunlu: `MembershipCubit` uygulama kökünde tek örnek — `PremiumLock` kararını buradan okur; oturum değişince `load(role:)`)*
- Modify: `mobile/lib/features/study/presentation/pages/study_goals_page.dart` + `mobile/lib/features/reporting/presentation/pages/student_analysis_page.dart` *(plan dışı, Step 6'nın gereği: kilidin uygulanacağı iki gerçek sunucu kapısı)*
- Test: `mobile/test/features/membership/membership_cubit_test.dart`, `mobile/test/features/membership/membership_pages_test.dart` *(plan dışı: Step 4-5 ekranlarının sözleşmesi)*, `mobile/test/shared/premium_lock_test.dart`, `mobile/test/helpers/fake_membership_repository.dart` *(plan dışı, zorunlu — §17.1 "sahteler tek noktada")*, `mobile/test/widget_test.dart` *(plan dışı, zorunlu: kök cubit testte DI'ya gitmemeli)*
- Create: `doc/pages/membership.md`, `doc/pages/paywall.md`

**Interfaces:**
- `MembershipRepository`: `me()`, `listPlans(role)`, `startTrial(planCode)`, `cancel()`, `referralCode()`, `redeemReferral(code)`.
  - ⚠️ **Plan düzeltmesi — `checkout(planCode)` YAZILMADI.** `POST /api/membership/checkout` ucu yok
    (Task 4, Q1 bloke). Var olmayan bir uca istek atan metot "sessiz başarısızlık yok" ilkesini
    çiğnerdi; satın alma düğmesi `onPressed: null` ile "Satın al (yakında)" der.
  - ⚠️ Metot adı `plans(role)` değil **`listPlans({role})`**: `plans` aynı zamanda durumun alan adı,
    ikisi tek sınıfta çakışıyordu.
- `PremiumLock` widget'ı: premium ise `child`; değilse bulanık + `IgnorePointer` önizleme (`premiumLockPreviewKey`) + `PremiumLockCard`. Kararı **kökteki `MembershipCubit`** verir (K-09'un mobil karşılığı).
- `PremiumLockCard(message, title?)`: cubit'e bağlı **değil** — sunucudan `402 *_premium_required` alan ekranlar kendi mesajıyla gösterir.
- `membershipRoleFor(roles)`: rol filtresi; öncelik `AppRouter`'ın rol yönlendirmesiyle birebir (veli > yalnız-öğrenci > öğretmen).

- [x] **Step 1: Cubit ve widget testlerini yaz (kırmızı)** — 12 cubit + 4 widget testi
- [x] **Step 2: Kırmızı gör** — Run: `flutter test test/features/membership test/shared/premium_lock_test.dart` → `Error when reading 'lib/features/membership/domain/membership_contracts.dart': No such file or directory` + `Type 'MembershipRepository' not found` (+ `MembershipSnapshot`/`MembershipPlan`/`ReferralCode`)
- [x] **Step 3: Repository + cubit'i yaz.** Fail-safe: `/me` okunamazsa durum Free kalır **ama** `status = failure` olur (sessizce yutulmaz). Komut reddedilirse (409/400) mevcut üyelik durumu **korunur**, yalnız hata mesajı eklenir.
- [x] **Step 4: Üyelik ekranı** — mevcut plan, dönem sonu, iptal (onay diyaloğu), referans kodu paylaşma (`Clipboard`, yeni paket **yok**) + kod kullanma.
  ⚠️ **Plan düzeltmesi — "otomatik yenileme anahtarı" yapılmadı:** `UserSubscription.AutoRenew` domainde
  var ama `GET /me` onu **döndürmüyor** ve değiştiren bir uç **yok**. Anahtar koymak, hiçbir yere
  yazmayan bir toggle olurdu (P05'te temizlenen sahte anahtarların tekrarı). Yerine gerçek olan
  `status` gösteriliyor: "İptal edildi — erişimin \<tarih\> tarihine kadar sürüyor".
  ⚠️ İptal düğmesi **yalnız yürüyen abonelikte** çıkar; yoksa sunucudan `404 subscription_not_found` gelirdi.
- [x] **Step 5: Paywall ekranı** — rol bazlı karşılaştırma, plan kartları, "İlk ay ücretsiz" rozeti, kampanya bölümü.
  ⚠️ **Plan düzeltmesi — checkout yönlendirmesi ve `egitimussu://membership/callback` derin bağlantısı
  yapılmadı:** çağrılacak uç yok (Task 4). Geri dönülecek bir akış olmadan derin bağlantı kaydetmek
  ölü kod olurdu; ikisi de Task 4'e taşındı.
  ⚠️ **Plan düzeltmesi — karşılaştırma tablosu PRD §9'un tamamı değil.** Tablo yalnız sunucunun
  **gerçekten uyguladığı** farkları taşır (`MaxStudents`, `PdfReports`, `TargetTracking`,
  `HistoryWindowDays`, `RichNotifications`). PRD §9'un kalan satırları (reklamsız kullanım, gelir/boş
  zaman analizi, streak, motivasyon, profil öne çıkarma) kodda **kısıtlanmıyor**; bunları "Premium'a
  özel" diye satmak, kullanıcıya bedava kullandığı şeyi satmak olurdu. Ayrı bir **"Yakında Premium
  kapsamında"** bölümünde duruyorlar ve kapı yazıldıkça tabloya taşınacaklar. İki yerde PRD'den
  **kod gerçeği yönünde** sapıldı: öğrenci geçmişi Free'de "yok" değil **son 30 gün**; öğretmen limiti
  "5–10" değil, canlı `entitlement.maxStudents`.
- [x] **Step 6: `PremiumLock`'u premium özelliklere uygula** — hedef net alanı (`study.premium_required`) ve PDF rapor kartı (`reporting.premium_required`); ikisi de artık paywall'a çıkıyor.
  ⚠️ **Plan düzeltmesi — "veli zengin bildirim" ve "öğrenci geçmiş/streak" kilitlenmedi.** Veli
  bildirimi sunucuda **sessizce filtrelenir** (istemciye kilitlenecek bir yüzey dönmez); geçmiş
  penceresi sessizce kırpılır; streak ise kodda **hiç kısıtlanmıyor** (`MembershipGate` yorumu: "Free =
  streak tam"). Uygulanmayan bir kısıtı istemcide taklit etmek, olmayan bir kuralı uydurmak olurdu.
- [x] **Step 7: Testler** — Run: `cd mobile && flutter test` → **170 başarılı, başarısız 0** (öncesi 146; +24) · `flutter analyze` → **4 info** (baseline ile aynı, yeni uyarı yok) · Backend regresyonu: `dotnet test EgitimUssu.slnx` → 4 mimari + 602 birim + 39 integration, Başarısız 0
- [x] **Step 8: Doküman + commit**

`doc/pages/membership.md` + `doc/pages/paywall.md` (yeni), `doc/pages/00_pages_index.md`, `doc/pages/more.md`,
`doc/architecture/widgets.md` (§3 `PremiumLock`/`PremiumLockCard` + §3.4 üyelik bileşenleri),
`doc/architecture/mobile_flutter.md` (§13.19b + §17.1), `doc/modules/m17_membership.md` (§1 mobil bloğu + §6 yeniden yazıldı + §8),
`doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/roles/{ogretmen,ogrenci,veli}.md`,
`doc/denetim/2026-09-02_eksik_analizi.md` (D-15 ✅ tam, D-10 ✅ kısmi).

```bash
git add mobile doc docs
git commit -m "feat(mobile): uyelik ekrani + paywall + premium kilitleri (D-10/M17-5)"
```

---

### Task 7: Kapanış

> ⚠️ **Kısmi kapanış olarak yürütüldü (2026-09-04, kullanıcı kararı (b)).** Task 4 (ödeme, **Q1**) ve
> Task 5 (reklam, **Q3**) hâlâ bloke olduğu için kabul kriterlerinden ikisi (*"premium hiç reklam
> görmüyor"*, *webhook imza + idempotency*) **doğrulanamadı**. Bu yüzden Step 3'teki `🔴 → 🟢` hedefi
> **uygulanmadı: M17 bilerek 🟡 bırakıldı**. Reklam ve ödeme kalemleri P09'un devamına kaldı.

- [x] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → yeşil.
      Sonuç: **4 mimari / 602 birim / 41 integration**, başarısız 0, atlanan 0 · mobil **170** test yeşil.
- [x] **Step 2: Uçtan uca** — Free öğretmen 6. öğrenciyi ekleyemiyor → paywall → deneme başlat → 6. öğrenci eklenebiliyor → deneme bitince tekrar kilit.
      ⚠️ **Plan düzeltmesi:** adım "elle koşulacak senaryo" olarak yazılmıştı; P06 Task 10'un dersi gereği
      (elle koşum tekrarlanabilir değil, InMemory gerçek hataları göstermiyor) **gerçek Postgres regresyon
      testine** çevrildi → `tests/Integration/RealMembershipEntitlementTests.cs` (2 test, 9 adım).
      İkinci test "deneme kullanıcı başına bir kez" kuralını dönem kapandıktan sonra da doğrular.
- [x] **Step 3: Dokümanlar** — `doc/modules/m17_membership.md` (**🟡 kaldı**, §1'e uçtan uca doğrulama tablosu
      + §7 kabul kriterleri gerçeğe göre işaretlendi), `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`,
      `doc/roles/{ogretmen,ogrenci,veli}.md` premium kapanış kutusu,
      `doc/denetim/2026-09-02_eksik_analizi.md`: M17-1 ✅, M17-3 ✅ (M03-4/M17-5/D-10/D-15 zaten işaretliydi);
      **M17-2 ve M17-4 bilerek AÇIK**.
- [x] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P09 uyelik ve gelir kapanisi (M17-*/M03-4/D-10)"
```

---

## Kabul Kriterleri

Durum: **7/9 karşılandı** (P09 Task 7 kısmi kapanışı, 2026-09-04). İki kriter Task 4/5 blokesi yüzünden açık.

- [x] Her rol için free + premium plan tanımlı; kullanıcı planını `GET /api/membership/me` ile görüyor
      *(fiyat değerleri Q2 ile onaylanmadı; tohumlama yalnız Development)*
- [ ] Premium kullanıcı hiç reklam görmüyor (`ad-placements` boş) — **AÇIK: uç yok (Task 5 / Q3)**
- [x] Free limitleri entitlement'tan geliyor; hiçbir modülde sabit limit kalmadı (`grep -rn "FreeStudentLimit" src/` boş)
- [x] İlk ay ücretsiz kullanıcı başına bir kez çalışıyor *(dönem kapansa bile ikinci deneme reddediliyor)*
- [x] Referans kodu iki tarafa da 1 ay veriyor; kendi kodu kullanılamıyor
- [ ] Webhook imzasız istekte 401; aynı olay iki kez gelince abonelik iki kez uzamıyor — **AÇIK: sağlayıcı yok (Task 4 / Q1)**
- [x] İptal edilen abonelik dönem sonuna kadar premium kalıyor, sonra Free'ye düşüyor
- [x] Mobilde paywall ve premium kilitleri PRD §9 ile birebir *(uygulanan alt küme; uygulanmayan satırlar paywall'da "yakında" bölümünde)*
- [x] Tam test paketi (Docker'lı) yeşil — 4 mimari / 602 birim / 41 integration / 170 mobil, başarısız 0
