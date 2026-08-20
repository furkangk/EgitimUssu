# 💳 Üyelik ve Para Kazanma Modülü (M17) — Detaylı Tasarım Dokümanı

> **PRD: M17 (YENİ)** · **Faz 5 — Premium & Analitik (gelir)** · **Durum: 🔴 YENİ — kodda HİÇ YOK (tüm domain ⚠️ Önerilen, planlanan)**
>
> **Amaç:** Platformun gelir modelini taşımak. PRD: **ücretsiz ve ücretli (premium) üyelik** vardır; gelir
> **reklam + üyelik** ikilisinden gelir. **Ücretli üyeler reklam görmez, kısıtlamalara takılmaz ve ekstra
> özelliklere erişir.** Kullanıcı çekmek için **ilk ay ücretsiz** ve **arkadaşını getir → 1 ay ücretsiz (referans)**
> kampanyaları planlanır (bkz. [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) §9 ve Faz 5).

> Rol bazlı premium özellik setleri PRD §9 (Free vs. Premium karşılaştırması) ile birebir uyumludur:
> öğretmen (gelir analizi, PDF rapor, profil öne çıkarma), öğrenci (geçmiş, haftalık/aylık analiz, streak,
> hedef, motivasyon), veli (detaylı grafik, haftalık rapor, geçmiş, bildirimler).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

🔴 **Kodda hiçbir karşılığı yoktur.** Tamamen planlama aşamasındadır:

- **Backend:** `src/Modules/Membership/` **yok** — `MembershipDbContext`, `ModuleDefinition`, DI, migration, `/status` endpoint'i **yok**.
- **Mobil:** abonelik/paywall/reklam feature'ı **yok**.
- **Şema:** PostgreSQL'de `membership` şeması **yok**.
- **Ödeme:** Herhangi bir ödeme sağlayıcısı entegrasyonu (App Store/Google Play/Iyzico vb.) **yok**.
- **Kısıtlama uygulaması:** Tam entitlement altyapısı henüz yok. **İstisna (Ö-D, 2026-07-19):** Study modülü hafif bir Free/Premium kapısı uygular — öğrencinin `MembershipTier`'ı (Free/Premium) M03 `StudentProfile`'da tutulur ve `Shared/Contracts` `IMembershipDirectory` sözleşmesinden okunur. Bugün zorlanan: Free geçmiş/net-trend son 30 güne kısılı, hedef net/puan takibi Premium'a özel (`study.premium_required` → 402). Bu, M17 tam modülü gelene kadar geçici bir çekirdektir; M17 açılınca tier depolama + entitlement bu modüle taşınır (bkz. [`m08_study.md`](m08_study.md) §4.7).

> ⚠️ Bu dokümandaki **tüm** içerik **önerilen / planlanan**dır. PRD §10.1'e göre bu modül **en geç açılan**
> bileşenlerdendir: Faz 1-3 gerçek kullanıcıda doğrulanmadan premium'a geçilmez.

---

## 2. Domain Modeli (⚠️ Önerilen)

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

## 3. API Sözleşmesi (⚠️ Önerilen — `/api/membership`)

```
# Planlar & abonelik
GET  /api/membership/plans?role={role}                  → role uygun plan listesi (free + premium)
GET  /api/membership/users/{userId}/subscription        → kullanıcının aktif aboneliği + entitlement
POST /api/membership/users/{userId}/subscribe           → premium başlat (plan + ödeme jetonu)
POST /api/membership/users/{userId}/start-trial         → ilk ay ücretsiz (deneme) başlat
POST /api/membership/users/{userId}/cancel              → otomatik yenilemeyi durdur / iptal
GET  /api/membership/users/{userId}/entitlements        → premium bayrakları + limitler

# Kampanya & referans
POST /api/membership/campaigns/redeem                   → kampanya kodu uygula
GET  /api/membership/users/{userId}/referral-code       → kullanıcının davet kodu (yoksa üretir)
POST /api/membership/referrals/redeem                   → davet kodunu kullan (kayıt sırasında)

# Reklam politikası (istemci tüketir)
GET  /api/membership/ad-placements?userId={userId}      → kullanıcıya uygun reklam yerleşimleri
                                                          (premium → boş liste)

# Ödeme sağlayıcı webhook'ları (Faz 5)
POST /api/membership/webhooks/payment                   → store/sağlayıcı yenileme/iptal bildirimi
```

> **Yetki:** Kullanıcı yalnızca **kendi** aboneliğini yönetir (sahiplik authorizer'ı — bkz.
> [`mimari_inceleme.md`](mimari_inceleme.md)). Plan/kampanya tanımları ve raporlar **admin**'e açıktır
> (bkz. [`../roles/admin.md`](../roles/admin.md)). Webhook'lar imza ile doğrulanır.

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
   - **Öğrenci:** geçmiş çalışma kayıtları, haftalık/aylık analiz, hedef belirleme, **streak (seri)**, motivasyon sistemi, gelişmiş çalışma sayacı, öğretmenle detaylı veri paylaşımı.
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

## 6. Mobil Ekranlar (Planlanan)

`mobile/lib/features/membership/`:

- **paywall / upgrade** — rol bazlı premium özellik vitrini (PRD §9 tablolarından), fiyat, "ilk ay ücretsiz" rozeti, satın al.
- **subscription-manage** — mevcut plan, dönem bitişi, otomatik yenileme aç/kapa, iptal.
- **referral-invite** — kullanıcının davet kodu/linki, paylaş, "arkadaşını getir → 1 ay ücretsiz" durumu.
- **redeem-code** — kampanya/referans kodu girişi (kayıt akışında da).
- **ad-slot widget** — free kullanıcıda banner/native reklam (premium'da gizli) — `AdPlacement` konfigine göre.

> Premium vitrini ve reklam dışı yüzeyler kurumsal renk `0xFF082B4F` ile. Reklam SDK entegrasyonu (örn. AdMob) istemci tarafındadır.

---

## 7. Kabul Kriterleri (⚠️ Önerilen)

- [ ] Her rol için free + premium plan tanımlı; kullanıcı planını ve aktif aboneliğini görebiliyor.
- [ ] Premium kullanıcı **hiç reklam görmüyor** (`ad-placements` boş dönüyor).
- [ ] Premium kullanıcıda free limitleri kalkıyor (örn. öğretmen sınırsız öğrenci).
- [ ] İlk ay ücretsiz: deneme başlatma çalışıyor, kullanıcı başına bir kez, süre sonunda doğru geçiş.
- [ ] Referans: kod üretimi + davet edilen kayıt → her iki tarafa 1 ay ücretsiz.
- [ ] Otomatik yenileme/iptal ve süre sonu free'ye düşüş çalışıyor.
- [ ] Entitlement değişimi integration event ile diğer modüllere yayılıyor; kısıtlamalar tek noktadan uygulanıyor.
- [ ] Rol bazlı premium özellikler (PRD §9) ilgili modüllerde entitlement'a bağlı.
- [ ] Ödeme sağlayıcı webhook'u imza doğrulamalı ve aboneliği güncelliyor.
- [ ] Admin plan/kampanya tanımlayıp gelir/dönüşüm raporu görebiliyor.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul (PRD §10.1):** Faz 1-3 gerçek kullanıcıda doğrulanmadan açılmaz; "kısıtlamaları uygulama"
> (Faz 5.2) ve "abonelik altyapısı + ödeme" (Faz 5.1) kritik kalemlerdir.

1. **Modül iskeleti** — `Membership` modülü, `MembershipDbContext`, `membership` şeması, DI + `ModuleDefinition` + ilk migration.
2. **Domain** — `SubscriptionPlan`, `UserSubscription`, `Campaign`, `ReferralCode`, `AdPlacement` + enum'lar + event'ler.
3. **Entitlement projeksiyonu + integration event** — `UserSubscriptionChangedIntegrationEvent` (tüm modüllerin tükettiği).
4. **Free/Premium kısıtlama uygulaması (Faz 5.2)** — her modülün limit/özellik kapısını entitlement'a bağla (M03 öğrenci limiti, M14 PDF/analiz, M16 mesaj eki/hız).
5. **İlk ay ücretsiz + referans kampanyaları** — trial + referral ödül akışı.
6. **Yenileme/expire BackgroundService.**
7. **Ödeme sağlayıcı entegrasyonu (Faz 5.1)** — store abonelikleri + imzalı webhook; backend kart verisi tutmaz.
8. **Reklam politikası + mobil SDK** — `AdPlacement` + istemci reklam gösterimi (premium gizli).
9. **Mobil paywall/abonelik/referans/reklam ekranları.**
10. **Admin paneli** — plan/kampanya yönetimi + gelir/dönüşüm raporu (M14 ile).
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
- Ürün gereksinimleri (Free vs. Premium §9, Faz 5) → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)
- Genel durum & strateji → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Üyelik ve Para Kazanma Modülü (M17) — Detaylı Tasarım | Faz 5 | Güncelleme: 2026-08-19 (Ö-D: Study'de hafif Free/Premium çekirdeği — `StudentProfile.MembershipTier` + `IMembershipDirectory` — kalıcı; M17 modül klasörü hâlâ kodda yok)*
