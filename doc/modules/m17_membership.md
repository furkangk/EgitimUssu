---
title: "M17 — Üyelik ve Para Kazanma"
summary: "Üyelik/gelir modülü: 4 katmanlı iskelet + SubscriptionPlan/UserSubscription/ReferralCode domain'i + membership şeması + IEntitlementDirectory (tek hak kaynağı) + plan kataloğu, ücretsiz deneme ve referans kampanyası uçları + mobil üyelik/paywall ekranları kodda; deneme→kilit açma→süre dolunca yeniden kilit zinciri gerçek Postgres'te uçtan uca doğrulandı (P09 Task 1-3, 6, 7 kısmi kapanış); ödeme (Q1) ve reklam (Q3) hâlâ yok"
tags: [modul, membership, uyelik, planlanan, faz-5]
status: "🟡"
authority: code
code_refs:
  - src/Modules/Membership/API/MembershipModule.cs
  - src/Modules/Membership/Application/MembershipFeatures.cs
  - src/Modules/Membership/Domain/MembershipDomainModel.cs
  - src/Modules/Membership/Infrastructure/EntitlementDirectory.cs
  - src/Modules/Membership/Infrastructure/MembershipPlanSeeder.cs
  - src/Shared/Contracts/EntitlementContract.cs
  - mobile/lib/features/membership/presentation/cubit/membership_cubit.dart
  - mobile/lib/shared/widgets/premium_lock.dart
  - tests/Integration/RealMembershipEntitlementTests.cs
updated: 2026-11-15
---

# 💳 Üyelik ve Para Kazanma Modülü (M17) — Detaylı Tasarım Dokümanı

> **PRD: M17 (YENİ)** · **Faz 5 — Premium & Analitik (gelir)** · **Durum: 🟡 Domain + entitlement + plan/kampanya uçları + mobil üyelik/paywall kodda ve üyelik zinciri gerçek Postgres'te uçtan uca doğrulanmış (P09 Task 1-3, 6 ve 7 kısmi kapanış, 2026-09-04); ödeme (Task 4/Q1) ve reklam (Task 5/Q3) yok — bu yüzden 🟢 değil**
> — `src/Modules/Membership` (4 katman) + `membership` şeması + `SubscriptionPlan`/`UserSubscription`/`ReferralCode`/`ReferralRedemption`
> aggregate'leri, `MembershipChangedDomainEvent`, **K-09 entitlement sözleşmesi** (`IEntitlementDirectory` + `GET /api/membership/me`)
> ve **plan kataloğu + ücretsiz deneme + referans kampanyası uçları** **kodda**; Study/Students/Reporting/Parents kapıları
> bu tek kaynağa devredildi; mobil üyelik ekranı ve paywall (Task 6) da kodda. **Ödeme (Task 4 / Q1) ve
> reklam (Task 5 / Q3) hâlâ planlanan** — para tahsil edilemiyor, premium'a yalnız deneme/referans/admin
> köprüsüyle çıkılıyor.
>
> **Amaç:** Platformun gelir modelini taşımak. PRD: **ücretsiz ve ücretli (premium) üyelik** vardır; gelir
> **reklam + üyelik** ikilisinden gelir. **Ücretli üyeler reklam görmez, kısıtlamalara takılmaz ve ekstra
> özelliklere erişir.** Kullanıcı çekmek için **ilk ay ücretsiz** ve **arkadaşını getir → 1 ay ücretsiz (referans)**
> kampanyaları planlanır (bkz. [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) §9 ve Faz 5).

> Rol bazlı premium özellik setleri PRD §9 ile hizalıdır:
> öğretmen (gelir analizi, PDF rapor, profil öne çıkarma, sınırsız öğrenci), öğrenci (**sınırsız geçmiş**,
> aylık analiz, hedef net/puan takibi, konu zayıflık analizi, streak **dondurma**), veli (detaylı grafik,
> haftalık rapor, geçmiş, zengin bildirim).
> ⚠️ **Streak'in kendisi ve rozetler Free'dir** — Premium olan yalnız *seri dondurma hakkı*
> (`PremiumFeature.StreakFreeze`). PRD §9.2 bunu 2026-09-05'te koda göre düzeltti; gerekçe:
> [`../denetim/2026-09-05_rakip_paritesi_analizi.md`](../denetim/2026-09-05_rakip_paritesi_analizi.md) §3.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### 🟢 Kodda olan (P09 Task 1-3 + Task 6, 2026-09-04)

- **Backend:** `src/Modules/Membership/{Domain,Application,Infrastructure,API}` — 4 csproj, `EgitimUssu.slnx`
  girdileri, `ModuleAssemblies.cs` kaydı, `MembershipModule` (`/api/membership`: `/status`, `GET /me`,
  `GET /plans`, `POST /subscriptions/start-trial`, `POST /subscriptions/cancel`, `GET /referral-code`,
  `POST /referral/redeem`, Admin `POST /plans` + `PUT /plans/{planId}`).
- **Şema:** `membership` — `subscription_plans`, `user_subscriptions`, `referral_codes`,
  `referral_redemptions` + ortak `outbox_messages` / `inbox_messages` / `module_states`
  (migration `InitialCreate` + `AddReferralAndTrial`).
- **Domain:** `SubscriptionPlan` (kod/rol/tier/periyot/fiyat/deneme + `NextPeriodEnd` takvim-ayı hesabı),
  `UserSubscription` (yaşam döngüsü + erişim kuralı), `SubscriptionStatus`, `BillingPeriod`,
  `MembershipChangedDomainEvent`. 12 birim testi (`tests/Unit/SubscriptionTests.cs`).
  ⚠️ `UserSubscription.PlanId` **nullable**'dır (P09 Task 3): kampanya ödülüyle açılan abonelik
  satın alınmış bir plana bağlı değildir.
- **Entitlement (K-09 / M17-5 / M03-4 — P09 Task 2):**
  - `Shared/Contracts/EntitlementContract.cs` → `record Entitlement(Tier, EffectiveUntilUtc, MaxStudents,
    CanSeeAds, DetailedAnalytics, PdfReports, RichNotifications)` + `IEntitlementDirectory`
    (`GetAsync`, toplu tarama için `GetManyAsync`).
  - `Membership.Application/MembershipFeatures.cs` → `EntitlementPolicy` (seviye → hak eşlemesinin **tek**
    yeri: Free = 5 öğrenci + reklam, Premium = sınırsız + reklamsız + PDF/analiz/zengin bildirim),
    `IMembershipReader`, `GetMyMembershipQuery` + handler + authorizer.
  - `Membership.Infrastructure/EntitlementDirectory.cs` → aboneliğe bakar; `HasAccess` dönemi açıksa Premium.
    **Fail-safe:** kaynak okunamazsa `LogWarning` + Free (fail-closed değil).
  - `GET /api/membership/me` (auth) → `{ tier, effectiveUntilUtc, planCode, status, entitlement }`.
  - **Devralınan kapılar:** `Study/StudyMembershipResolver`, `Students` öğretmen öğrenci limiti
    (`entitlement.MaxStudents`), `Reporting/ReportEntitlementResolver` (PDF), `Parents`
    `ParentNotificationDirectory` (veli bildirim seviyesi). 11 birim testi
    (`tests/Unit/EntitlementDirectoryTests.cs`).
- **Plan kataloğu, deneme ve kampanya (M17-1 / M17-3 — P09 Task 3):**
  - Domain: `ReferralCode` (kullanıcı başına tek, kod kimlikten **deterministik** türetilir —
    `ReferralCodeFactory`, karıştırılabilir `0/O/1/I/L` karakterleri alfabede yok),
    `ReferralRedemption` (`RedeemerUserId` benzersiz → kullanıcı ömrü boyunca **tek** ödül),
    `UserSubscription.GrantCampaignMonths` (ödül mevcut dönemi **ezmez, uzatır**),
    `SubscriptionPlan.UpdatePricing` (kod/rol/periyot değişmez — kararlı anahtarlar).
  - Uçlar: `GET /plans?role=` (**anonim** — paywall giriş yapmadan fiyat gösterebilmeli),
    `POST /subscriptions/start-trial`, `POST /subscriptions/cancel`, `GET /referral-code`
    (GET ama komut gönderir: kod yoksa üretilir), `POST /referral/redeem`,
    Admin `POST /plans` + `PUT /plans/{planId}` (`AdminOnlyMembershipAuthorizer`).
  - Kampanya kuralı tek yerde: `MembershipCampaigns.ReferralRewardMonths = 1` — davet edene **ve**
    gelene 1 ay premium.
  - **Plan tohumlama:** `MembershipPlanSeeder` idempotenttir (yalnız eksik kodları ekler) ve
    `Program.cs`'te **yalnız `Development`** ortamında koşar. Fiyatlar
    `codex/03_paket_fiyatlandirma.md` §4 lansman aralığının **alt sınırıdır** ve **hipotezdir**;
    nihai liste **Q2** ile onaylanmadan üretime çıkmaz (üretimde planlar Admin ucundan girilir).
  - 23 birim testi (`tests/Unit/TrialTests.cs`, `ReferralTests.cs`, `SubscriptionPlanTests.cs`).
- **Mobil üyelik ekranı ve paywall (D-10 / D-15 — P09 Task 6):**
  - `mobile/lib/features/membership/{domain,data,presentation}` + `shared/widgets/premium_lock.dart`.
  - `MembershipCubit` **uygulama kökünde** (`app/app.dart`) tek örnektir ve oturum değişince
    `GET /me` + `GET /plans?role=` çağırır. K-09'un mobil karşılığı: premium kararı tek yerde.
  - `MembershipRepository`: `me()`, `listPlans(role)`, `startTrial(code)`, `cancel()`,
    `referralCode()`, `redeemReferral(code)`. ⚠️ `checkout()` **bilinçli olarak yok** — uç Task 4'te
    gelecek, olmayan uca istek atan metot "sessiz başarısızlık yok" ilkesini çiğnerdi.
  - Ekranlar: `/membership` (durum, iptal, referans kodu, kod kullan) ve `/membership/paywall`
    (rol bazlı karşılaştırma, plan kartları, deneme, kampanyalar). `/more` "Uyelik" satırı aktif;
    profil kartındaki "Plus üye" rozeti artık gerçek üyelikten geliyor (sabit `true` sahtesi silindi).
  - `PremiumLock` / `PremiumLockCard` sunucunun **gerçekten uyguladığı** kapıların önünde:
    hedef net alanı (`study.premium_required`) ve PDF rapor kartı (`reporting.premium_required`).
  - 24 mobil test (`membership_cubit_test.dart` 12, `premium_lock_test.dart` 4,
    `membership_pages_test.dart` 8).
- ⚠️ **Geçiş köprüsü (P12'de silinecek):** abonelik kaydı yokken eski profil alanları
  (`StudentProfile.MembershipTier`, `ParentProfile.MembershipTier`) hâlâ okunur — `IMembershipDirectory`
  artık **yalnız** bu köprünün sözleşmesidir (Students + Parents uygular, tek tüketicisi
  `EntitlementDirectory`). Köprü olmasaydı admin'in `PUT .../membership-tier` ile verdiği premium,
  abonelik akışı (Task 3-4) gelene kadar sessizce kaybolurdu. Karar noktası yine tek: hiçbir modül
  o alanları kendisi okumaz.
- ⚠️ **`MembershipTier` burada yeniden tanımlanmadı:** kanonik tanım `Shared/Contracts`'tadır
  (`MembershipDirectoryContract.cs`) ve Students/Parents/Study/Reporting onu kullanır. Aynı gerekçeyle
  plandaki `MembershipTierContract` enum'u da **yazılmadı**.

### 🔴 Hâlâ kodda olmayan

- **Olay yayılımı:** `MembershipChangedIntegrationEvent` (outbox) — bugün entitlement her okumada
  taze hesaplanıyor, tüketici-tarafı önbellek/tazeleme yok (Task 4).
- **Uçlar:** ücretli abonelik **başlatma** (ödeme jetonuyla) — deneme/iptal/referans kodda,
  satın alma Task 4'te.
- **Mobil:** reklam yerleşimi (`AdSlot`) **yok** (Task 5 — Q3 bloke). Üyelik ekranı ve paywall
  Task 6'da geldi (yukarı bak); mobilde eksik kalan tek şey **satın alma akışı** (Task 4).
- **Ödeme:** Herhangi bir ödeme sağlayıcısı entegrasyonu (iyzico vb.) **yok** (Task 4 — **Q1 bloke**).
- **Reklam:** `ad-placements` ucu **yok** (Task 5 — **Q3 bloke**).
- **Kısıtlama uygulaması (kısmen):** Kapılar tek kaynağa bağlandı (yukarı), ama uygulanan kısıt kümesi
  hâlâ dar: Free geçmiş 30 güne kısılı + hedef net/puan Premium (`study.premium_required` → 402),
  öğretmen öğrenci limiti 5, PDF raporu Premium (402), veli zengin bildirimleri. PRD §9'daki tam
  özellik matrisi (reklam, mesajlaşma limitleri, profil öne çıkarma, streak/motivasyon) hâlâ
  uygulanmadı. **Bu yüzden mobil paywall tablosu PRD §9'un tamamını göstermez**: uygulanmayan
  satırlar P20 T1’den itibaren paywall’da gösterilmez (bkz. [`../pages/paywall.md`](../pages/paywall.md) §2).

### ✅ Uçtan uca doğrulanan (P09 Task 7 — **kısmi kapanış**, 2026-09-04)

Zincirin bugün çalışan kısmı **gerçek Postgres'e karşı** otomatik regresyon testine bağlandı
(`tests/Integration/RealMembershipEntitlementTests.cs`, 2 test). Birim testiyle görülemeyen şey burada
kanıtlanır: sınırın `membership` şemasındaki abonelik kaydından okunup `students` şemasındaki uca kadar
taşınması — yani iki ayrı şemanın tek istekte doğru bağlanması.

| Adım | Beklenen | Doğrulandı |
|------|----------|------------|
| Free öğretmen `GET /me` | `Free` · `maxStudents = 5` · `canSeeAds = true` | ✅ |
| 1–5. öğrenci | `200 OK` | ✅ |
| 6. öğrenci | `409` + `students.free_limit_reached` (paywall tetikleyicisi) | ✅ |
| `POST /subscriptions/start-trial` | `200` · `Trialing` · `planCode` dolu | ✅ |
| Deneme sonrası `GET /me` | `Premium` · `maxStudents = int.MaxValue` · `canSeeAds = false` | ✅ |
| 6. öğrenci (deneme açıkken) | `200 OK` — kilit gerçekten aboneliğe bağlı | ✅ |
| Dönem kapanınca `GET /me` | `Free` · `effectiveUntilUtc = null` | ✅ |
| 7. öğrenci (deneme bitince) | yeniden `409` | ✅ |
| Aynı kullanıcıya ikinci deneme | `membership.trial_already_used` — dönem kapansa bile verilmez | ✅ |

> ⚠️ **Kapanış kısmidir; M17 bilerek 🟡 bırakıldı.** Kabul kriterlerinden ikisi **doğrulanamadı** çünkü
> ilgili görevler açık soruya bloke: **"premium kullanıcı hiç reklam görmüyor"** (`ad-placements` ucu yok
> — Task 5 / **Q3**) ve **"webhook imzasız istekte 401, aynı olay iki kez gelince abonelik iki kez
> uzamıyor"** (ödeme sağlayıcısı yok — Task 4 / **Q1**). Yani **para hâlâ tahsil edilemiyor**: bugünkü
> premium'a yalnız ücretsiz deneme, referans ödülü ve admin köprüsüyle çıkılabilir. Bu iki kalem
> P09'un devamına bırakıldı; 🟢'ye geçiş onlara bağlıdır.

> ⚠️ §2'den itibaren içerik **önerilen / planlanan**dır; kodda olan kısım yukarıda §1'de listelenmiştir.
> §2.1/§2.2'deki alan adları ile kodun **birebir aynı olmadığına** dikkat: kodda `Tier` doğrudan
> `Shared.Contracts.MembershipTier`'dır (ayrı `PlanTier` yok) ve rol alanı `string Role`'dur
> (`UserRole` enum'una bağlanması Task 3'e bırakıldı). PRD §10.1'e göre bu modül **en geç açılan**
> bileşenlerdendir: Faz 1-3 gerçek kullanıcıda doğrulanmadan premium'a geçilmez.

---

## 2. Domain Modeli (⚠️ Önerilen)

**P18 T1 — eşzamanlılık (K-22):** `UserSubscription` `IVersioned` uygular; `uint Version { get; private set; }` PostgreSQL’in ürettiği `xmin` (`xid`) sistem kolonuna eşlenir. `ModuleDbContext` tip modele eklenirken concurrency + OnAddOrUpdate konvansiyonunu uygular. Aynı sürümü okuyan iki yazmadan kaybeden `409 shared.concurrency_conflict` alır: “Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.” `AddVersionToken` yalnız model metadatasını kaydeder; yeni kolon/veri göçü yoktur. Outbox hatayı normal backoff ile yeniden teslim eder; yeni scope güncel veriyi yükler, başarısız iş ve inbox aynı transaction’da geri alınır.


**Şema:** `membership` · **DbContext:** `MembershipDbContext` · **Route prefix:** `/api/membership`
**Aggregate'ler:** `SubscriptionPlan`, `UserSubscription`, `Campaign`, `ReferralCode`, `AdPlacement`.

> Modül sınırı kuralı: `Membership` diğer modüllerin tablolarına erişmez. Kullanıcı kimliği `Identity`
> integration event'leriyle bilinir; bir kullanıcının premium olup olmadığını diğer modüller
> `UserSubscriptionChangedIntegrationEvent` ile öğrenir (yerel "yetki/entitlement" projeksiyonu).

### 2.1 `SubscriptionPlan` (AggregateRoot)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `Code` | string | Benzersiz plan kodu (örn. `teacher_premium_monthly`) |
| `Name` | string | Görünen ad |
| `Tier` | enum `PlanTier` | `Free=1`, `Premium=2` |
| `TargetRole` | enum `UserRole` | `Teacher=2`, `Student=3`, `Parent=4` (rol bazlı paket) |
| `BillingPeriod` | enum `BillingPeriod` | `None=0`, `Monthly=1`, `Yearly=2` |
| `Price` | decimal | Dönem ücreti (Free için 0) |
| `Currency` | string | `TRY` (varsayılan) |
| `FeatureFlags` | jsonb | Özellik/limit seti (bkz. §2.6 entitlement) |
| `IsActive` | bool | Satışta mı |
| `CreatedOnUtc` | DateTime | |

**Davranışlar:** `Activate()/Deactivate()`, `UpdatePricing()`. **Event:** `SubscriptionPlanPublishedDomainEvent`.
**DB:** tablo `subscription_plans`; `Code` UNIQUE; index `(TargetRole, Tier, IsActive)`.

### 2.2 `UserSubscription` (AggregateRoot)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `UserId` | Guid | Identity kullanıcısı |
| `PlanId` | Guid | Seçili plan |
| `Status` | enum `SubscriptionStatus` | `Trialing=1`, `Active=2`, `PastDue=3`, `Expired=4`, `Cancelled=5` |
| `StartedOnUtc` | DateTime | Başlangıç |
| `CurrentPeriodEndUtc` | DateTime | Dönem bitişi / yenileme tarihi |
| `TrialEndsOnUtc` | DateTime? | Deneme/ilk ay ücretsiz bitişi |
| `AutoRenew` | bool | Otomatik yenileme |
| `Source` | enum `SubscriptionSource` | `Direct=1`, `Trial=2`, `Referral=3`, `Campaign=4` |
| `AppliedCampaignId` | Guid? | Uygulanan kampanya |
| `AppliedReferralCodeId` | Guid? | Kullanılan referans kodu |
| `ExternalSubscriptionId` | string? | Ödeme sağlayıcı abonelik kimliği (store) |
| `CancelledOnUtc` | DateTime? | |
| `CreatedOnUtc` / `UpdatedOnUtc` | DateTime | |

**Davranışlar:** `StartTrial()`, `Activate()`, `Renew(periodEnd)`, `MarkPastDue()`, `Expire()`, `Cancel()`, `ApplyReferralReward(months)`.
**Event'ler:** `SubscriptionStartedDomainEvent`, `SubscriptionRenewedDomainEvent`, `SubscriptionExpiredDomainEvent`, `SubscriptionCancelledDomainEvent`.
**DB:** tablo `user_subscriptions`; index `(UserId, Status)` (aktif aboneliği bulma), `(Status, CurrentPeriodEndUtc)` (yenileme/expire tarayıcısı), `(UserId)` aktif için kısmi UNIQUE.

### 2.3 `Campaign` (AggregateRoot)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `Code` | string | Kampanya kodu (UNIQUE) |
| `Name` | string | |
| `Type` | enum `CampaignType` | `FirstMonthFree=1`, `ReferralReward=2`, `Discount=3`, `ExtendedTrial=4` |
| `RewardMonths` | int | Ödül süresi (ay) — örn. ilk ay = 1 |
| `DiscountPercent` | int? | İndirimli kampanyalarda |
| `TargetRole` | enum `UserRole`? | Sadece belirli role |
| `StartsOnUtc` / `EndsOnUtc` | DateTime | Geçerlilik penceresi |
| `MaxRedemptions` | int? | Toplam kullanım limiti |
| `RedeemedCount` | int | Kullanım sayacı |
| `IsActive` | bool | |

**Davranışlar:** `Redeem()`, `Activate()/Deactivate()`. **Event:** `CampaignRedeemedDomainEvent`.
**DB:** tablo `campaigns`; `Code` UNIQUE.

### 2.4 `ReferralCode` (AggregateRoot)

> ✅ **Kodda olan (P09 Task 3) aşağıdaki öneriden sadeleşti:** `ReferralCode`
> (`OwnerUserId` benzersiz, `Code` benzersiz, `IsActive`, `RedemptionCount`) **çok kullanımlıdır** —
> tek davetlik `InvitedUserId`/`Status` alanları yerine, her kullanım ayrı bir
> **`ReferralRedemption`** satırıdır (`ReferralCodeId`, `ReferrerUserId`, `RedeemerUserId`,
> `RewardMonths`, `RedeemedOnUtc`; `RedeemerUserId` **benzersiz** → bir kullanıcı ömrü boyunca tek ödül).
> Ödül kodun kullanıldığı anda **her iki tarafa** yazılır (ayrı `Rewarded` durumu yok); kod
> kullanıcı kimliğinden deterministik türetilir (`ReferralCodeFactory`).


| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `Code` | string | Paylaşılabilir kod (UNIQUE) |
| `OwnerUserId` | Guid | **Davet eden** kullanıcı |
| `InvitedUserId` | Guid? | **Davet edilen** (kullanılınca dolar) |
| `Status` | enum `ReferralStatus` | `Issued=1`, `Redeemed=2`, `Rewarded=3`, `Expired=4` |
| `RewardMonths` | int | Her iki tarafa verilecek ücretsiz ay (örn. 1) |
| `RedeemedOnUtc` | DateTime? | |
| `RewardedOnUtc` | DateTime? | |
| `CreatedOnUtc` | DateTime | |

**Davranışlar:** `Redeem(invitedUserId, utcNow)`, `GrantReward(utcNow)`.
**Event'ler:** `ReferralRedeemedDomainEvent`, `ReferralRewardedDomainEvent`.
**Kural:** Davet edilen kullanıcı **gerçekten kayıt olup** geçerlilik şartını sağlayınca (örn. e-posta doğrulama / ilk ay) hem davet eden hem edilen **1 ay ücretsiz** alır.
**DB:** tablo `referral_codes`; `Code` UNIQUE; index `(OwnerUserId)`, `(InvitedUserId)`.

### 2.5 `AdPlacement` (AggregateRoot)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `Key` | string | Yerleşim anahtarı (örn. `dashboard_banner`, `inbox_native`) — UNIQUE |
| `Surface` | enum `AdSurface` | `Banner=1`, `Native=2`, `Interstitial=3` |
| `Provider` | enum `AdProvider` | `AdMob=1`, `Custom=2` |
| `IsEnabled` | bool | Genel açık/kapalı |
| `ShownToTiers` | enum `PlanTier` (flags) | Kimlere gösterilir — **varsayılan yalnızca `Free`** |
| `Config` | jsonb | Sağlayıcı/birim kimlikleri (istemci tüketir) |
| `UpdatedOnUtc` | DateTime | |

> **Reklam politikası (PRD kuralı):** `AdPlacement` reklamın **nerede ve kime** gösterileceğini tanımlar.
> **Premium kullanıcı reklam GÖRMEZ** (`ShownToTiers = Free`). Reklam gösterimi/yükleme **istemci (mobil)
> tarafında** yapılır; backend yalnızca yerleşim politikası/konfigürasyonunu verir.

### 2.6 Entitlement (yetki) projeksiyonu

Her modülün "bu kullanıcı premium mu / limiti ne?" sorusunu ucuz yanıtlaması için, `Membership`
`UserSubscriptionChangedIntegrationEvent` yayınlar; tüketen modüller yerel bir `entitlement` görünümü tutar
(örn. `IsPremium`, `MaxStudents`, `CanExportPdf`). Bu, modüller arası **doğrudan DB erişimini** önler.

### 2.7 Enum'lar (⚠️ Önerilen)

| Enum | Değerler |
|------|----------|
| `PlanTier` | `Free=1`, `Premium=2` |
| `BillingPeriod` | `None=0`, `Monthly=1`, `Yearly=2` |
| `SubscriptionStatus` | `Trialing=1`, `Active=2`, `PastDue=3`, `Expired=4`, `Cancelled=5` |
| `SubscriptionSource` | `Direct=1`, `Trial=2`, `Referral=3`, `Campaign=4` |
| `CampaignType` | `FirstMonthFree=1`, `ReferralReward=2`, `Discount=3`, `ExtendedTrial=4` |
| `ReferralStatus` | `Issued=1`, `Redeemed=2`, `Rewarded=3`, `Expired=4` |
| `AdSurface` | `Banner=1`, `Native=2`, `Interstitial=3` |
| `AdProvider` | `AdMob=1`, `Custom=2` |

---

## 3. API Sözleşmesi (`/api/membership`)

**Kodda olan (P09 Task 2-3):**

```
GET  /api/membership/me                                 → { tier, effectiveUntilUtc, planCode, status,
     (auth)                                                 entitlement: { maxStudents, canSeeAds,
                                                             detailedAnalytics, pdfReports,
                                                             richNotifications } }

GET  /api/membership/plans?role={role}                  → satıştaki planlar (rol filtresi opsiyonel)
     (ANONİM — IAllowAnonymous)                            [{ id, code, role, tier, period, price,
                                                              currency, trialDays, isActive }]

POST /api/membership/subscriptions/start-trial          → { planCode } · kullanıcı başına BİR KEZ
     (auth)                                                → üyelik durumu (Trialing)
                                                          409 membership.trial_already_used
                                                          404 membership.plan_not_found
                                                          400 membership.trial_not_available

POST /api/membership/subscriptions/cancel               → iptal; erişim dönem sonuna kadar sürer
     (auth)                                                404 membership.subscription_not_found

GET  /api/membership/referral-code                      → { code, redemptionCount, rewardMonths }
     (auth)                                                (kod yoksa üretilir — GET ama komut)

POST /api/membership/referral/redeem                    → { code } · HER İKİ tarafa 1 ay premium
     (auth)                                                400 membership.invalid_referral
                                                          400 membership.self_referral
                                                          409 membership.referral_already_used

POST /api/membership/plans                              → yeni plan (YALNIZ Admin)
     (auth, Admin)                                         409 membership.plan_code_conflict
                                                          400 membership.invalid_plan
PUT  /api/membership/plans/{planId}                     → fiyat/deneme/satış durumu (YALNIZ Admin)
     (auth, Admin)                                         404 membership.plan_not_found
```

**⚠️ Önerilen (henüz kodda değil):**

```
# Ücretli abonelik (Task 4 — Q1 bloke)
POST /api/membership/subscriptions/subscribe            → premium başlat (plan + ödeme jetonu)
POST /api/membership/webhooks/payment                   → sağlayıcı yenileme/iptal bildirimi (imzalı)

# Reklam politikası (Task 5 — Q3 bloke)
GET  /api/membership/ad-placements                      → kullanıcıya uygun yerleşimler
                                                          (premium → boş liste)
```

> **Yetki:** Kullanıcı yalnızca **kendi** aboneliğini yönetir — hedef kullanıcı gövdeden değil
> **oturumdan** gelir (`AuthenticatedMembershipAuthorizer`), bu yüzden uçlarda `{userId}` yoktur.
> Plan yazma uçları **yalnız Admin**'e açıktır (`AdminOnlyMembershipAuthorizer`; bkz.
> [`../roles/admin.md`](../roles/admin.md)). `GET /plans` bilinçli olarak anonimdir: paywall ekranı
> kullanıcı giriş yapmadan da fiyatları gösterebilmelidir. Webhook'lar imza ile doğrulanır (Task 4).

---

## 4. İş Kuralları

1. **İki katman:** Her rol için en az bir `Free` ve bir `Premium` plan vardır (PRD §9.1/9.2/9.3 paketleri).
2. **Premium = reklamsız + sınırsız + ekstra:** `Premium` abonelikte `AdPlacement` hiçbir reklam döndürmez, free limitleri kalkar ve ekstra özellikler açılır.
3. **İlk ay ücretsiz:** `start-trial` → `Status=Trialing`, `TrialEndsOnUtc = now + 1 ay`. Deneme süresince premium yetkileri açık; bitince `AutoRenew` ise ücret alınır, değilse `Expired` (free'ye düşer). Kullanıcı başına bir kez.
4. **Arkadaşını getir (referans):** Davet edilen kullanıcı kayıt olup şartı sağlayınca **hem davet eden hem davet edilen 1 ay ücretsiz** alır (`ReferralCode.GrantReward`). Kod tekil; kendi kodunu kullanamaz; her davet edilen tek kullanır.
5. **Yenileme & süre:** `CurrentPeriodEndUtc` geçtiğinde arka plan tarayıcısı `AutoRenew` ise yeniler (ödeme başarısızsa `PastDue` → tolerans sonra `Expired`), değilse `Expired`.
6. **Free'ye düşüş:** `Expired/Cancelled` olunca entitlement free'ye döner; modüller (öğretmen öğrenci limiti, geçmiş erişimi vb.) kısıtlamaları yeniden uygular.
7. **Rol bazlı özellikler (PRD §9):**
   - **Öğretmen:** aylık kazanç/gelir analizi, geciken ödeme listesi, otomatik ödeme hesaplama, PDF öğrenci raporu, öğrenci performans analizi, boş zaman analizi, **profil öne çıkarma**, sınırsız öğrenci, ders/ödev + WhatsApp/SMS hatırlatma.
   - **Öğrenci:** **sınırsız** geçmiş (Free 30 gün), aylık analiz, hedef net/puan takibi, konu bazlı zayıflık analizi, **streak dondurma**, PDF gelişim raporu. ⚠️ Kronometre, streak, rozet, günlük hedef ve temel haftalık analiz **Free**'dir (`MembershipGate`).
   - **Veli:** detaylı gelişim grafikleri, haftalık rapor, çalışma süresi geçmişi, bildirimler.
8. **Kısıtlamanın tek noktası:** Limitler `Membership` entitlement'ında tanımlanır; her modül kendi sınırını bu projeksiyondan okur (örn. M03 öğretmen öğrenci sayısı, M16 mesaj eki/hız, M14 PDF rapor).
9. **Ödeme/KVKK & mağaza:** Tahsilat App Store/Google Play (mobil) ve/veya yerel sağlayıcı üzerinden; uygulama mağazası abonelik kuralları ve KVKK uyumu gözetilir (PRD §10.3). Backend **kart verisi tutmaz**, yalnızca `ExternalSubscriptionId`.
10. **Reklam istemci tarafında:** Backend reklam içeriği sunmaz; yalnızca yerleşim politikası/konfig döner, gösterimi mobil SDK yapar.

---

## 5. Olay Akışı (⚠️ Önerilen)

```
[İlk ay ücretsiz]
POST /start-trial → UserSubscription.StartTrial()  → SubscriptionStartedDomainEvent (Source=Trial)
   → (Outbox) UserSubscriptionChangedIntegrationEvent (IsPremium=true, trial)
      → tüm modüller entitlement projeksiyonunu günceller (reklam kapanır, limitler kalkar)

[Referans]
Davet edilen kayıt olur (Identity: UserRegistered) + referral kodu ile geldi
   → ReferralCode.Redeem() → ReferralRedeemedDomainEvent
   → şart sağlanınca ReferralCode.GrantReward()
      → her iki UserSubscription'a +1 ay → ReferralRewardedDomainEvent

[Yenileme tarayıcısı (BackgroundService)]
[periyodik] CurrentPeriodEndUtc <= now olanları tara
   → AutoRenew + ödeme başarılı → Renew()  → SubscriptionRenewedDomainEvent
   → ödeme başarısız → MarkPastDue() → (tolerans) → Expire()
   → AutoRenew=false → Expire() → SubscriptionExpiredDomainEvent
      → (Outbox) UserSubscriptionChangedIntegrationEvent (IsPremium=false)
         → modüller free kısıtlamalarını yeniden uygular

[Ödeme webhook'u (store)]
POST /webhooks/payment (imza doğrulanır)
   → ilgili UserSubscription: Renew / Cancel / PastDue güncellemesi

[Reklam]
İstemci → GET /ad-placements?userId
   → premium ise [] döner; free ise aktif AdPlacement listesi (istemci SDK gösterir)
```

---

## 6. Mobil Ekranlar

`mobile/lib/features/membership/` — **P09 Task 6'da yazıldı** (D-10/D-15).
Ayrıntı: [`../pages/membership.md`](../pages/membership.md) · [`../pages/paywall.md`](../pages/paywall.md).

| Ekran / bileşen | Rota | Durum |
|-----------------|------|-------|
| **Paywall** — rol bazlı karşılaştırma, plan kartları, "ilk ay ücretsiz" rozeti, kampanyalar | `/membership/paywall` | 🟡 **kodda** — satın alma düğmesi gizli (P20 T1) (checkout ucu Task 4) |
| **Üyelik yönetimi** — mevcut plan, dönem bitişi, iptal | `/membership` | 🟡 **kodda** — otomatik yenileme anahtarı yok (`AutoRenew` ne dönülüyor ne değiştirilebiliyor) |
| **Referans** — kod, kopyala, kaç kişi kullandı | `/membership` içinde bölüm | 🟢 **kodda** |
| **Kod kullan** — referans/kampanya kodu girişi | `/membership` içinde bölüm | 🟢 **kodda** (kayıt akışına henüz bağlı değil) |
| **`PremiumLock` / `PremiumLockCard`** — premium kilidi + paywall köprüsü | `shared/widgets/premium_lock.dart` | 🟢 **kodda** |
| **`AdSlot`** — free kullanıcıda banner reklam | — | 🔴 **yok** (Task 5, Q3 bloke) |

> Premium vitrini ve reklam dışı yüzeyler kurumsal renk `0xFF082B4F` ile. Reklam SDK entegrasyonu (örn. AdMob) istemci tarafındadır.

---

## 7. Kabul Kriterleri

P09 Task 7 (kısmi kapanış, 2026-09-04) itibarıyla durum. ✅ olanlar koşulmuş test çıktısına dayanır
(`tests/Integration/RealMembershipEntitlementTests.cs` + birim/mobil paket).

- [x] Her rol için free + premium plan tanımlı; kullanıcı planını ve aktif aboneliğini görebiliyor
      (`GET /plans?role=` + `GET /me`). ⚠️ Fiyat değerleri **Q2** ile onaylanmadı; tohumlama yalnız Development.
- [ ] Premium kullanıcı **hiç reklam görmüyor** (`ad-placements` boş dönüyor) — **doğrulanamadı**, uç yok (Task 5 / **Q3**).
- [x] Premium kullanıcıda free limitleri kalkıyor — öğretmen 6. öğrenciyi ekleyebiliyor (uçtan uca §1).
- [x] İlk ay ücretsiz: deneme başlatma çalışıyor, **kullanıcı başına bir kez**, süre sonunda Free'ye geçiş.
- [x] Referans: kod üretimi + davet edilen kayıt → her iki tarafa 1 ay ücretsiz (`ReferralTests`, 23 birim testi).
- [x] İptal ve süre sonu free'ye düşüş çalışıyor (iptalde erişim dönem sonuna kadar sürer).
      ⚠️ **Otomatik yenileme yok** — yenileme ödemeye bağlı (Task 4).
- [ ] Entitlement değişimi **integration event** ile yayılıyor — **yok**; bugün her okumada taze hesaplanıyor (Task 4).
- [x] Rol bazlı premium özellikler ilgili modüllerde entitlement'a bağlı — ⚠️ **kısmen**: PRD §9'un
      uygulanan alt kümesi (limit, geçmiş/hedef, PDF, veli bildirim seviyesi); reklam / mesajlaşma
      limitleri / profil öne çıkarma hâlâ yok.
- [ ] Ödeme sağlayıcı webhook'u imza doğrulamalı ve aboneliği güncelliyor — **doğrulanamadı**, sağlayıcı yok (Task 4 / **Q1**).
- [ ] Admin plan/kampanya tanımlayıp gelir/dönüşüm raporu görebiliyor — plan CRUD ✅, kampanya yönetimi ve
      gelir/dönüşüm raporu **yok**.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul (PRD §10.1):** Faz 1-3 gerçek kullanıcıda doğrulanmadan açılmaz; "kısıtlamaları uygulama"
> (Faz 5.2) ve "abonelik altyapısı + ödeme" (Faz 5.1) kritik kalemlerdir.

1. ✅ **Modül iskeleti** (P09 Task 1) — `Membership` modülü, `MembershipDbContext`, `membership` şeması, DI + `ModuleDefinition` + `InitialCreate`.
2. 🟡 **Domain** — `SubscriptionPlan`, `UserSubscription`, `ReferralCode`, `ReferralRedemption` + enum'lar + `MembershipChangedDomainEvent` **kodda** (Task 1, 3); `Campaign` ve `AdPlacement` **yok** (Task 5 / sonrası).
3. 🟡 **Entitlement** (P09 Task 2 / K-09) — `IEntitlementDirectory` + `EntitlementPolicy` + `GET /me` **kodda**; `MembershipChangedIntegrationEvent` (outbox) hâlâ yok (Task 4).
4. 🟡 **Free/Premium kısıtlama uygulaması (Faz 5.2)** — M03 öğrenci limiti, M08 geçmiş/hedef, M14 PDF, M09/M11 bildirim seviyesi entitlement'a bağlandı; reklam ve mesajlaşma limitleri kaldı.
5. ✅ **İlk ay ücretsiz + referans kampanyaları** (P09 Task 3) — `POST /subscriptions/start-trial` (kullanıcı başına bir kez) + `GET /referral-code` / `POST /referral/redeem` (iki tarafa 1 ay). Plan tohumlaması yalnız Development'ta; **fiyatlar Q2 ile onaylanmadı**.
6. **Yenileme/expire BackgroundService.** — `ExpireIfDue` domain'de var ama **onu çağıran zamanlanmış iş yok**: dönemi geçmiş abonelik kaydı `Expired`'a yazılmaz, yalnız okuma anında Free görünür (`HasAccess`). Kullanıcıya etkisi doğru, veri tarafı tembel.
7. **Ödeme sağlayıcı entegrasyonu (Faz 5.1)** — store abonelikleri + imzalı webhook; backend kart verisi tutmaz.
8. **Reklam politikası + mobil SDK** — `AdPlacement` + istemci reklam gösterimi (premium gizli).
9. 🟡 **Mobil paywall/abonelik/referans ekranları** (P09 Task 6) — üyelik ekranı, paywall, referans akışları ve `PremiumLock` **kodda**; satın alma (Task 4) ve reklam yerleşimi (Task 5) kaldı.
10. 🟡 **Admin paneli** — plan CRUD uçları (`POST/PUT /plans`) **kodda** (Task 3); kampanya yönetimi ve gelir/dönüşüm raporu (M14 ile) kaldı.
11. **KVKK + mağaza uyumu** — abonelik şeffaflığı, iptal kolaylığı, veri saklama.

---

## 9. İlişkili Dokümanlar

- Reklam/premium farkı (mesajlaşma) → [`m16_messaging.md`](m16_messaging.md)
- Şikayet/kötüye kullanım (premium istismarı dahil) → [`m18_feedback.md`](m18_feedback.md)
- Premium hatırlatma/bildirim (WhatsApp/SMS) → [`m11_notifications.md`](m11_notifications.md)
- Profil öne çıkarma (öğretmen premium) → [`m12_matching.md`](m12_matching.md)
- Gelir/analiz/PDF rapor (premium çıktılar) → [`m14_reporting.md`](m14_reporting.md)
- Öğrenci limiti (öğretmen free/premium) → [`m03_students.md`](m03_students.md)
- Kullanıcı kimliği/rol → [`m01_identity.md`](m01_identity.md)
- Veli premium paketi → [`m09_parents.md`](m09_parents.md)
- Tercih/gizlilik → [`m15_settings.md`](m15_settings.md)
- Yetki guard'ı → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli bağlamı → [`veri_modeli.md`](veri_modeli.md)
- Rol perspektifleri → [`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../roles/admin.md`](../roles/admin.md), [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md)
- Ürün gereksinimleri (Free vs. Premium §9, Faz 5) → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Genel durum & strateji → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Üyelik ve Para Kazanma Modülü (M17) — Detaylı Tasarım | Faz 5 | Durum: 🟡 İskelet + domain + entitlement + plan/kampanya + mobil paywall, uçtan uca doğrulanmış | Güncelleme: 2026-11-15 (**doküman düzeltmesi**: PRD §9 kopyası koda göre güncellendi — streak ve rozetler **Free**, Premium olan yalnız `StreakFreeze`; ayrıntı `../denetim/2026-09-05_rakip_paritesi_analizi.md` §3. Önceki not — 2026-09-04 (**P09 Task 7 — kısmi kapanış**: üyelik zinciri gerçek Postgres'e karşı otomatik regresyona bağlandı (`tests/Integration/RealMembershipEntitlementTests.cs`, 2 test): Free öğretmen 6. öğrenciyi ekleyemiyor (409 `students.free_limit_reached`) → deneme başlıyor (`Trialing`, `maxStudents` sınırsız, `canSeeAds=false`) → 6. öğrenci eklenebiliyor → dönem kapanınca `GET /me` Free dönüyor ve 7. öğrenci yeniden 409; ikinci deneme `membership.trial_already_used`. §7 kabul kriterleri gerçeğe göre işaretlendi. **M17 bilerek 🟡 kaldı** — reklam (Task 5 / Q3) ve ödeme+webhook (Task 4 / Q1) doğrulanamadı, para tahsil edilemiyor; ayrıca `ExpireIfDue`'yu çağıran zamanlanmış iş yok (dönem sonu yalnız okuma anında Free görünür). Önceki not — P09 Task 3 / M17-1+M17-3: `ReferralCode`/`ReferralRedemption` domain'i (kod kimlikten deterministik türetilir), `UserSubscription.PlanId` **nullable** (kampanya ödülü plana bağlı değil) + `GrantCampaignMonths` (ezmez, uzatır), `SubscriptionPlan.UpdatePricing`; uçlar: anonim `GET /plans`, `POST /subscriptions/start-trial`, `POST /subscriptions/cancel`, `GET /referral-code`, `POST /referral/redeem`, Admin `POST/PUT /plans`; migration `AddReferralAndTrial` (`referral_codes`, `referral_redemptions`, `PlanId` nullable); `MembershipPlanSeeder` **yalnız Development** — fiyatlar `codex/03_paket_fiyatlandirma.md` alt sınırı ve **Q2 ile onaylanmadı**. Önceki not — P09 Task 2 / K-09+M17-5+M03-4: `IEntitlementDirectory` + `Entitlement` sözleşmesi (`Shared.Contracts`), `EntitlementPolicy` (seviye→hak eşlemesinin tek yeri), `EntitlementDirectory` (fail-safe, aboneliğe bakar), `GET /api/membership/me`; Study/Students/Reporting/Parents kapıları devralındı; eski `MembershipTier` alanları **geçici köprüye** indirildi (`IMembershipDirectory` artık yalnız köprü sözleşmesi, P12'de silinecek). Plandaki `MembershipTierContract` enum'u yazılmadı; plandaki `D-09` etiketi yanlıştı (doğrusu K-09/M17-5/M03-4). Önceki not — P09 Task 1 / M17-1: `src/Modules/Membership` 4 katman + `membership` şeması (`subscription_plans`, `user_subscriptions`, `InitialCreate`) + `SubscriptionPlan`/`UserSubscription` domain'i + `MembershipChangedDomainEvent`; `MembershipTier` **yeniden tanımlanmadı**, `Shared.Contracts`'taki kanonik enum kullanıldı. İptal ≠ anında kapanma: erişim dönem sonuna kadar sürer, Free'ye düşüş yalnız `ExpireIfDue`'da. Önceki not — 2026-08-19 (Ö-D: Study'de hafif Free/Premium çekirdeği — `StudentProfile.MembershipTier` + `IMembershipDirectory` — kalıcı; M17 modül klasörü hâlâ kodda yok))*

*Güncelleme: 2026-11-15 — P18 Task 1: xmin iyimser eşzamanlılık koruması ve 409 sözleşmesi.*

## P20 T1 — mobil sahte veri temizliği

Paywall ve ortak plan kartlarında işlevsiz satın alma düğmesi ile henüz kapısı olmayan Premium vaatlerinin bölümü gizlidir. Gerçek plan kataloğu, ücretsiz deneme, abonelik iptali ve referans akışları korunur. Mobil ödeme P28 kapsamındadır; bu görev ödeme entegrasyonu eklemez.
