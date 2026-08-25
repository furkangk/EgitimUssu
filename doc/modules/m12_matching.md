---
title: "M12 — Eşleştirme ve Keşif (Matching)"
summary: "Öğretmen-öğrenci eşleştirme/ilan modülü iskelet seviyesinde; kodda yalnızca placeholder status endpoint'i ve boş DbContext var"
tags: [modul, matching, eslestirme, iskelet, faz-4]
status: "🔴"
authority: code
code_refs:
  - src/Modules/Matching/**
updated: 2026-08-19
---

# 🔍 M12 — Eşleştirme ve Keşif Modülü (Matching)

> **PRD Modülü:** M12 Eşleştirme · **Backend Modülü:** `Matching` · **Route Prefix:** `/api/matching`
> **Faz:** 4️⃣ (Eşleştirme & Pazar Yeri) · **Öncelik:** Son · **Durum:** 🔴 İskelet
>
> **Amaç:** Öğrenci ve öğretmenin birbirini bulmasını sağlayan **iki taraflı ilan ve keşif** sistemi.
> Öğretmen sunduğu dersin ilanını verir; öğrenci aradığı dersin ilanını verir. Sistem; konuma göre yakınlık,
> yüksek yıldız puanı ve ücretli üyelik önceliği ile en uygun eşleşmeleri öne çıkarır. Talep kabul edildiğinde
> öğretmen-öğrenci ilişkisi kurulur ve özel ders öğrencinin programına otomatik eklenir.

> **Tasarım ilkesi (EğitimÜssü / PRD §10.1):** Bu modül **en son** açılır. "Sık yapılan hata: pazar yeri
> fonksiyonunu erken açmak, yönetim tarafını yarım bırakmaktır." Eşleştirmeye yalnızca Faz 1-2-3'ün gerçek
> kullanıcılarda çalıştığı doğrulandıktan ve her iki tarafta (öğretmen + öğrenci) yeterli kullanıcı havuzu
> oluştuktan sonra geçilir. Buna rağmen **sistem baştan bu özelliğe uygun** tasarlanmıştır (promp.txt son not).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

`Matching` modülü şu anda **iskelet** seviyesindedir. Kodda yalnızca aşağıdakiler mevcuttur:

| Katman | Dosya | İçerik |
|--------|-------|--------|
| API | `src/Modules/Matching/API/MatchingModule.cs` | `ModuleDefinition`; `Name = "Matching"`, `RoutePrefix = "/api/matching"`, tek endpoint `GET /api/matching/status` → `{ module, route, state = "placeholder" }` |
| Infrastructure | `src/Modules/Matching/Infrastructure/MatchingDbContext.cs` | `MatchingDbContext : ModuleDbContext`, `SchemaName = "matching"` (ayrı PostgreSQL şeması) |
| Infrastructure | `src/Modules/Matching/Infrastructure/DependencyInjection.cs` | `AddMatchingModule(...)` DI kaydı |
| Domain / Application | `AssemblyReference.cs` | Boş — aggregate, command/query, handler **yok** |

**Henüz olmayanlar:** Domain aggregate'leri, CQRS command/query + handler, EF entity konfigürasyonu, migration, integration event handler, mobil/web ekranları.

**Hazır altyapı (bu modülü besleyen, kodda mevcut olan zemin):**

- **`TeacherProfile`** (`Teachers` modülü) keşif/filtreleme için gereken alanları **zaten içeriyor**:
  `Subject` (branş), `City`/`District` (şehir/ilçe), `LessonFormat` (`InPerson`/`Online`/`Hybrid`),
  `HourlyRateAmount` + `Currency`, `ExperienceYears`, `EducationLevel`, `IsVerified` (doğrulama rozeti),
  `ProfilePhotoUrl`, `Headline`/`Biography` ve haftalık `AvailabilitySlots` (gün + saat aralığı + online/yüz yüze).
  Bkz. [`m02_teachers.md`](m02_teachers.md).
- **`TeacherProfileCreatedDomainEvent` / `TeacherProfileUpdatedDomainEvent`** olayları yayılıyor — `Matching`
  modülü bunları dinleyerek kendi arama projeksiyonunu besleyebilir (modül sınırını ihlal etmeden).
- **`StudentProfile`** (`Students` modülü) `Origin = TeacherManaged | SelfRegistered` ve `CreatedByTeacherUserId`
  ile öğretmen-öğrenci ilişkisini zaten modelliyor; eşleşme kabul akışı bu ilişkiyi kurar. Bkz. [`m03_students.md`](m03_students.md).
- **`Scheduling`** modülü (`POST /api/scheduling/lessons`) eşleşme kabul edildiğinde özel dersin öğrencinin
  programına eklenmesi için hazır.

> **Modül sınırı kuralı (kritik):** `Matching`, `Teachers`/`Students` veritabanını **doğrudan okumaz**.
> Integration event'leri dinleyip kendi `matching` şemasındaki **okuma modelini (read-model)** günceller (CQRS).
> Bu, modüler monolitin veri izolasyonunu korur.

---

## 2. Domain Modeli (⚠️ Önerilen — Henüz Kodda Yok)

> Aşağıdaki tablolar **önerilen** tasarımdır. `matching` PostgreSQL şemasında, modül kendi DbContext'i ile yönetir.

### 2.1 `TeacherListing` (AggregateRoot) — Öğretmen İlanı

Öğretmenin "sunduğu ders" ilanı. `TeacherProfile`'dan türetilen ama **yayın/keşif odaklı** bir kayıttır
(profil her zaman herkese açık değildir; ilan bilinçli olarak yayınlanır).

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | İlan kimliği |
| `TeacherUserId` | Guid | İlanı veren öğretmenin Identity kullanıcı kimliği |
| `Subject` | string | Branş (ör. Matematik, İngilizce) |
| `GradeLevels` | string[] | Hedef sınıf seviyeleri (ör. 11, 12, Mezun) |
| `City`, `District` | string | Şehir / ilçe (konum yakınlığı sıralaması için) |
| `Latitude`, `Longitude` | double? | Opsiyonel hassas konum (yakınlık skoru için) |
| `LessonFormat` | enum `ListingLessonFormat` | `InPerson=1`, `Online=2`, `Hybrid=3` |
| `HourlyRateAmount` + `Currency` | decimal + string | Saatlik ücret (varsayılan `TRY`) |
| `Headline`, `Description` | string? | İlan başlığı / tanıtım metni |
| `AvailabilitySummary` | string? | Uygunluk özeti (haftalık slotlardan türetilir) |
| `IsVerifiedTeacher` | bool | Öğretmen profili doğrulanmış mı (rozet — `TeacherProfile.IsVerified`'dan beslenir) |
| `IsFeatured` | bool | Öne çıkarma bayrağı (ücretli üyelik / premium — bkz. [`m17_membership.md`](m17_membership.md)) |
| `Status` | enum `ListingStatus` | `Draft=1`, `Published=2`, `Paused=3`, `Closed=4` |
| `PublishedOnUtc`, `CreatedOnUtc`, `UpdatedOnUtc` | DateTime? | Yaşam döngüsü zaman damgaları |

**Davranışlar:** `Publish()`, `Pause()`, `Close()`, `UpdateDetails(...)`, `SetFeatured(bool)`.
**Domain Events:** `TeacherListingPublishedDomainEvent`, `TeacherListingUpdatedDomainEvent`, `TeacherListingClosedDomainEvent`.

### 2.2 `StudentRequestListing` (AggregateRoot) — Öğrenci Ders Talebi İlanı

Öğrencinin "aradığı ders" ilanı (promp.txt: *"Öğrenciler aradıkları dersin ilanını verebilmeli"*).
Öğretmenler bu ilanları görüp öğrenciye teklif/talep gönderebilir (ters yön eşleşme).

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Talep ilanı kimliği |
| `StudentUserId` | Guid | İlanı veren öğrencinin Identity kullanıcı kimliği |
| `Subject` | string | Aranan branş |
| `GradeLevel` | string | Öğrencinin sınıf seviyesi |
| `City`, `District` | string | Konum |
| `PreferredFormat` | enum `ListingLessonFormat` | Tercih edilen ders şekli |
| `BudgetMinAmount`, `BudgetMaxAmount`, `Currency` | decimal? | Bütçe aralığı |
| `PreferredDays` | DayOfWeek[] | Tercih edilen günler |
| `Goal`, `Description` | string? | Hedef / açıklama (ör. "TYT matematik net artışı") |
| `Status` | enum `ListingStatus` | `Draft`, `Published`, `Paused`, `Closed` |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime? | Zaman damgaları |

**Davranışlar:** `Publish()`, `Pause()`, `Close()`, `UpdateDetails(...)`.
**Domain Events:** `StudentRequestListingPublishedDomainEvent`, `StudentRequestListingClosedDomainEvent`.

### 2.3 `MatchRequest` (AggregateRoot) — Eşleşme Talebi

İki yönlü talep akışının çekirdeği. Öğrenci bir öğretmene (ya da öğretmen bir öğrenci talebine) başvurur.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Talep kimliği |
| `StudentUserId` | Guid | Öğrenci tarafı |
| `TeacherUserId` | Guid | Öğretmen tarafı |
| `InitiatedBy` | enum `MatchInitiator` | `Student=1`, `Teacher=2` (talebi kim başlattı) |
| `SourceListingId` | Guid? | Hangi ilandan doğdu (`TeacherListing` veya `StudentRequestListing`) |
| `Subject` | string | İlgili branş |
| `Message` | string? | İlk mesaj / not (iletişim m16 üzerinden devam eder) |
| `Status` | enum `MatchRequestStatus` | `Pending=1`, `Accepted=2`, `Declined=3`, `Expired=4`, `Cancelled=5` |
| `CreatedOnUtc` | DateTime | Oluşturulma |
| `RespondedOnUtc`, `ExpiresOnUtc` | DateTime? | Yanıt / son geçerlilik tarihi |

**Davranışlar:**
- `Accept()` → `Status = Accepted`, `RespondedOnUtc` set, `MatchRequestAcceptedDomainEvent` yayılır.
- `Decline()` → `Status = Declined`, `MatchRequestDeclinedDomainEvent` yayılır.
- `Cancel()` → talebi başlatan taraf iptal eder (`Cancelled`).
- `Expire()` → süre dolduğunda zamanlanmış iş (`Notifications`/job) tarafından `Expired`'a çekilir.

**Domain Events:** `MatchRequestCreatedDomainEvent`, `MatchRequestAcceptedDomainEvent`, `MatchRequestDeclinedDomainEvent`.

### 2.4 `TeacherSearchProjection` (Read-Model) — Arama Projeksiyonu

`Teachers`, `Reviews` ve `Membership` modüllerinin **integration event'lerinden beslenen**, aramaya/filtreye/
sıralamaya optimize edilmiş salt-okunur model. `Matching` bunu kendi şemasında tutar; canlı sorgular bunun
üzerinden çalışır.

| Alan | Tip | Kaynak (Event) |
|------|-----|----------------|
| `TeacherUserId` | Guid | `Teachers` (`TeacherProfileCreated/Updated`) |
| `FullName`, `Subject`, `City`, `District` | string | `Teachers` |
| `Latitude`, `Longitude` | double? | `Teachers` (konum) |
| `LessonFormat` | enum | `Teachers` |
| `HourlyRateAmount`, `Currency` | decimal | `Teachers` |
| `IsVerified` | bool | `Teachers` (doğrulama rozeti) |
| `ProfilePhotoUrl`, `Headline` | string? | `Teachers` |
| `AverageRating` | decimal | `Reviews` (`TeacherReviewPublished` → ortalama güncellenir) — bkz. [`m13_reviews.md`](m13_reviews.md) |
| `ReviewCount` | int | `Reviews` |
| `IsPremium` / `FeaturedUntilUtc` | bool / DateTime? | `Membership` (ücretli üyelik / öne çıkarma) — bkz. [`m17_membership.md`](m17_membership.md) |
| `HasActiveListing` | bool | `Matching` (`TeacherListingPublished`) |
| `LastUpdatedUtc` | DateTime | Projeksiyon tazeliği |

> Projeksiyon **idempotent** güncellenmelidir (aynı event iki kez gelse de sonuç değişmez). Bir teacher hiç
> ilan yayınlamamışsa `HasActiveListing = false` olur ve genel keşifte gösterilmez.

### 2.5 Sıralama / Öne Çıkarma Skoru (promp.txt'in kalbi)

Keşif sonuçları **tek bir bileşik skorla** sıralanır (promp.txt son cümle:
*"Konumuna göre yakınlıkta bulunan ilanlar ve yıldızı yüksek ücretli üyelikli ilanlar ön planda olmalı"*):

```
Skor = w1 * KonumYakınlığı(arayan, ilan)         // mesafe ↓ → skor ↑ (city/district eşleşmesi + lat/long)
     + w2 * YıldızPuanı(AverageRating, ReviewCount) // m13: Bayesian/ağırlıklı ortalama
     + w3 * PremiumBoost(IsPremium, IsFeatured)      // m17: ücretli üyelik / öne çıkarma
     + w4 * Tazelik/Uygunluk                          // son aktiflik, uygun slot örtüşmesi
```

- **Konum yakınlığı:** Aynı ilçe > aynı şehir > yakın şehir; `Latitude/Longitude` varsa Haversine mesafesi.
- **Yıldız:** Az sayıda 5 yıldızlı ilanın haksız öne çıkmasını önlemek için **ağırlıklı (Bayesian) ortalama**
  kullanılır (`ReviewCount` düşükse global ortalamaya yaklaşır).
- **Premium boost:** Ücretli üyelik/öne çıkarma sıralamada öne taşır **ama** doğrulama rozeti ve gerçek puanın
  yerini almaz (reklam etiği — premium ilanlar "Öne Çıkan" etiketiyle şeffaf gösterilir).
- Ağırlıklar (`w1..w4`) konfigüre edilebilir; başlangıç önerisi: yakınlık ve puan baskın, premium ikincil itki.

---

## 3. API Sözleşmesi (⚠️ Önerilen — Henüz Yok)

Mevcut: yalnızca `GET /api/matching/status`. Aşağıdaki uçlar **önerilir**. Tüm yazma uçları
`RequireAuthorization("AuthenticatedUser")` ile korunur ve `Result<T>` döner; hata kodları HTTP statüsüne
eşlenir (`404`, `409`, `403 shared.forbidden`, varsayılan `400`).

### 3.1 Keşif & Arama (öğrenci tarafı)
```
GET  /api/matching/teachers
       ?subject=&city=&district=&minRate=&maxRate=&format=&availableDay=&verifiedOnly=
       &lat=&lng=&sort=relevance|rating|priceAsc|priceDesc&page=&pageSize=
                                       → sıralı/sayfalı öğretmen ilanı listesi (TeacherSearchProjection)
GET  /api/matching/teachers/{teacherUserId}
                                       → herkese açık öğretmen profil sayfası (+ puan/yorum özeti, ilan, uygunluk)
```

### 3.2 Öğretmen İlanı (öğretmen tarafı)
```
POST /api/matching/listings                      → öğretmen ilanı oluştur (Draft)
PUT  /api/matching/listings/{id}                 → ilan güncelle
POST /api/matching/listings/{id}/publish         → yayınla
POST /api/matching/listings/{id}/pause           → duraklat
POST /api/matching/listings/{id}/close           → kapat
GET  /api/matching/listings/mine                 → öğretmenin kendi ilanları
```

### 3.3 Öğrenci Ders Talebi İlanı (öğrenci tarafı)
```
POST /api/matching/requests-listings             → öğrenci "ders arıyorum" ilanı oluştur
PUT  /api/matching/requests-listings/{id}        → güncelle
POST /api/matching/requests-listings/{id}/close  → kapat
GET  /api/matching/requests-listings             → (öğretmen) yayındaki öğrenci talep ilanlarını ara/filtrele
```

### 3.4 Eşleşme Talebi Akışı (iki yönlü)
```
POST /api/matching/match-requests                → talep/mesaj gönder (Pending)
GET  /api/matching/match-requests/incoming       → bana gelen talepler (öğretmen veya öğrenci)
GET  /api/matching/match-requests/outgoing       → gönderdiğim talepler
POST /api/matching/match-requests/{id}/accept    → kabul (→ öğretmen-öğrenci ilişkisi + program)
POST /api/matching/match-requests/{id}/decline   → reddet
POST /api/matching/match-requests/{id}/cancel    → (başlatan) iptal
```

> **Eşleşme tamamlanınca (Accept):** Talep kabul edildiğinde sistem zincirleme şunları yapar:
> 1. `Students` modülünde öğretmen-öğrenci bağı kurulur (`StudentProfile` öğretmene bağlanır; self-registered
>    öğrenci ise `CreatedByTeacherUserId`/ilişki kaydı eklenir — bkz. [`m03_students.md`](m03_students.md)).
> 2. `Scheduling` modülünde özel ders öğrencinin programına otomatik eklenir
>    (promp.txt: *"o ders de otomatik olarak ders programına eklenecektir"*).
> 3. İletişim `m16_messaging.md` üzerinden devam eder (talepteki ilk `Message` mesaj kanalını başlatır).

---

## 4. İş Kuralları (Business Rules)

1. **İki taraflı ilan:** Hem öğretmen (sunduğu ders) hem öğrenci (aradığı ders) ilan yayınlayabilir.
   Eşleşme her iki yönden de başlatılabilir (`InitiatedBy`).
2. **Yalnızca yayınlanmış ilan keşifte görünür:** `Status = Published` olmayan ilanlar (Draft/Paused/Closed)
   arama sonuçlarında çıkmaz.
3. **Modül sınırı:** `Matching`, öğretmen/öğrenci/puan/üyelik verisini **doğrudan başka modülün DB'sinden okumaz**;
   yalnızca integration event'lerle beslenen `TeacherSearchProjection`'ı kullanır.
4. **Doğrulama rozeti güvenilir kaynaktan gelir:** `IsVerified` yalnızca `Teachers`/admin doğrulama akışından
   beslenir; `Matching` bu bayrağı **değiştiremez**, yalnızca yansıtır.
5. **Premium şeffaflığı:** Öne çıkan (featured/premium) ilanlar sıralamada öne taşınsa da **"Öne Çıkan" etiketiyle**
   açıkça işaretlenir; premium, gerçek yıldız puanını veya doğrulamayı taklit edemez (etik kural).
6. **Sıralama bütünlüğü:** Bileşik skor = konum yakınlığı + ağırlıklı yıldız + premium boost + tazelik
   (bkz. §2.5). Az yorumlu ilanlar Bayesian ortalama ile dengelenir.
7. **Talep yaşam döngüsü:** Bir `MatchRequest` `Pending` durumunda sınırlı süre (ör. 7 gün) bekler; yanıtlanmazsa
   zamanlanmış iş ile `Expired` olur. Aynı öğrenci-öğretmen-branş üçlüsü için aktif `Pending` talep tekrar açılamaz
   (`matching.duplicate_request`).
8. **Yetki:** İlanı yalnızca sahibi düzenleyebilir/kapatabilir; talebi yalnızca karşı taraf kabul/ret edebilir,
   yalnızca başlatan iptal edebilir (`shared.forbidden`).
9. **Otomatik ilişki + program:** Kabul, öğretmen-öğrenci ilişkisini kurar ve özel dersi öğrencinin programına
   ekler (Scheduling). Öğrencinin mevcut planıyla çakışırsa öncelik özel derstedir; öğrenci uyarılır
   (promp.txt: çakışmada öncelik özel ders — bkz. [`m03_students.md`](m03_students.md)).
10. **Engelleme/şikayet:** Kötüye kullanım (spam talep, taciz) `m18_feedback.md` üzerinden bildirilir; admin
    ilanı/kullanıcıyı askıya alabilir.

---

## 5. Olay Akışı (Event-Driven)

```
[Beslenen — diğer modüllerden gelen integration event'ler]
TeacherProfileCreated/Updated   → Matching: TeacherSearchProjection upsert (branş, şehir, ücret, rozet, konum)
TeacherReviewPublished          → Matching: projeksiyonda AverageRating + ReviewCount güncelle  (m13)
MembershipActivated/Expired     → Matching: projeksiyonda IsPremium / FeaturedUntilUtc güncelle  (m17)

[Üretilen — Matching'in yaydığı domain/integration event'ler]
TeacherListingPublished         → projeksiyon HasActiveListing = true; (m11) ilgi gruplarına bildirim (ops.)
StudentRequestListingPublished  → uygun öğretmenlere "yeni talep" bildirimi (ops., m11)
MatchRequestCreated             → karşı tarafa bildirim (m11) + mesaj kanalı açılışı (m16)
MatchRequestAccepted            → Students: öğretmen-öğrenci ilişkisi kur (m03)
                                → Scheduling: özel dersi öğrencinin programına ekle (m04)
                                → Messaging: kalıcı sohbet aç (m16)
                                → Notifications: her iki tarafa "eşleştiniz" bildirimi (m11)
MatchRequestDeclined / Expired  → başlatan tarafa bilgi bildirimi (m11)
```

> Olaylar **Outbox pattern** ile güvenilir yayılır (`Shared/Infrastructure/Messaging`). `Matching` hem **tüketici**
> (projeksiyon besleme) hem **üretici** (eşleşme sonucu) rolündedir. Mevcut bir tüketici örneği için
> `Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs` desen olarak alınabilir.

---

## 6. Mobil + Web Ekranlar (Planlanan)

### 6.1 Mobil (Flutter — `mobile/lib/features/matching/`)

Mimari ([`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §9): `matching` feature mobilde planlanan feature klasörlerindendir (öğretmen keşfi/ilan).

| Route (öneri) | Sayfa | Açıklama |
|---------------|-------|----------|
| `/discovery` | `TeacherDiscoveryPage` | Arama + filtre (branş, şehir/ilçe, ücret, ders şekli, uygun gün, doğrulanmış) + sıralama (alaka/puan/ücret) |
| `/discovery/teacher/:id` | `TeacherPublicProfilePage` | Herkese açık profil: puan/yorum özeti, ilan, uygunluk, "Talep Gönder" |
| `/listings/mine` | `MyTeacherListingsPage` | (Öğretmen) ilanlarım: oluştur/yayınla/duraklat/kapat |
| `/requests-listing/new` | `StudentRequestListingFormPage` | (Öğrenci) "ders arıyorum" ilanı oluştur |
| `/match-requests` | `MatchRequestsPage` | Gelen/giden talepler; kabul/ret |

- **Tema:** Birincil marka rengi `0xFF082B4F` (EğitimÜssü lacivert); "Öne Çıkan" ve "Doğrulanmış" rozetleri
  bu palette ile vurgulanır.
- **Durum yönetimi:** `flutter_bloc` (Cubit), her sayfa `*_cubit.dart` + `*_state.dart`; ağ `dio`, DI `get_it`,
  yönlendirme `go_router`.

### 6.2 Web (Angular — `src/app/features/matching/`)

Mimari ([`../architecture/web_angular.md`](../architecture/web_angular.md) §1): *"M12: Öğretmen Listeleme ve Detaylı Filtreleme"* web tarafında **öncelikli**; eşleştirme her iki
platformda da aktif edilecek. Büyük ekranda gelişmiş filtre paneli, harita üzerinde konuma göre keşif ve
karşılaştırma tablosu hedeflenir (Tailwind CSS, responsive `sm/md/lg`).

---

## 7. Kabul Kriterleri (Faz 4 Çıktısı)

- [ ] Öğretmen ilan oluşturup yayınlayabilir (branş, şehir/ilçe, ücret, ders şekli, uygunluk).
- [ ] Öğrenci "ders arıyorum" ilanı oluşturabilir; öğretmenler bu talepleri görebilir.
- [ ] Öğrenci öğretmenleri arayıp filtreleyebilir (branş, şehir, ücret, format, gün, doğrulanmış).
- [ ] Sonuçlar **konum yakınlığı + yıldız puanı + premium** bileşik skoruyla sıralanır; "Öne Çıkan" şeffaf etiketli.
- [ ] Herkese açık öğretmen profil sayfası (puan + yorum + ilan + uygunluk).
- [ ] Talep/mesaj gönderme; karşı taraf kabul/ret eder.
- [ ] Kabulde öğretmen-öğrenci ilişkisi kurulur **ve** özel ders öğrencinin programına otomatik eklenir.
- [ ] Doğrulama rozeti güvenilir kaynaktan (Teachers/admin) yansıtılır.
- [ ] `TeacherSearchProjection` integration event'lerle beslenir (doğrudan DB okuma yok — modül sınırı).
- [ ] Mobil + web keşif/talep ekranları çalışır.

---

## 8. Eksikler ve Yapılacaklar Listesi

> ⚠️ **Önkoşul (PRD §10.1):** Faz 1-2-3 (öğretmen çekirdeği, öğrenci bireysel çalışma, veli paneli) **gerçek
> kullanıcılarda doğrulanmadan** ve öğretmen+öğrenci havuzu oluşmadan bu modüle **başlanmamalıdır**. Aksi halde
> "boş pazar yeri" problemi (ilan var, talep yok) yaşanır.

**Önkoşul doğrulama listesi:**
- [ ] Faz 1 doğrulandı — öğretmenler her gün ders/öğrenci/ödeme yönetiyor ([`m02_teachers.md`](m02_teachers.md)).
- [ ] Faz 2 doğrulandı — öğrenciler bireysel çalışmayı kullanıyor ([`m03_students.md`](m03_students.md)).
- [ ] Faz 3 doğrulandı — veli paneli gerçek kullanımda.
- [ ] Yeterli öğretmen **ve** öğrenci kullanıcı havuzu oluştu.

**Yapılacaklar (sıra):**
1. **`TeacherSearchProjection` read-model'i** — `Teachers`/`Reviews`/`Membership` event'lerinden besleme + idempotent upsert.
2. **`TeacherListing` domain'i + CQRS** — oluştur/yayınla/duraklat/kapat + EF konfig + migration (`matching` şeması).
3. **`StudentRequestListing` domain'i + CQRS** — öğrenci talep ilanı.
4. **Keşif/arama endpoint'i** — filtre + bileşik skor sıralaması (§2.5) + sayfalama.
5. **`MatchRequest` akışı** — talep → kabul/ret → ilişki kurma (m03) + programa ekleme (m04) + mesaj (m16).
6. **Talep süre dolumu (Expire) zamanlanmış iş** — `Pending` taleplerin otomatik kapanışı.
7. **Premium/öne çıkarma entegrasyonu** — `Membership` (m17) ile featured boost ve "Öne Çıkan" etiketi.
8. **Mobil + web ekranları** — discovery, profil, ilan yönetimi, talep yönetimi.
9. **Kötüye kullanım/şikayet** — `m18_feedback.md` ile spam/taciz bildirimi + admin askıya alma.

---

## 9. İlişkili Dokümanlar

- **Roller:** [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md) · [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md)
- **Eşleştirilecek öğretmen profili (kaynak veri + rozet):** [`m02_teachers.md`](m02_teachers.md)
- **Eşleştirilecek öğrenci havuzu + öğretmen-öğrenci ilişkisi:** [`m03_students.md`](m03_students.md)
- **Ders oturumu (eşleşme sonrası ders işleme):** [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- **Güven altyapısı (yıldız puanı, sıralama girdisi):** [`m13_reviews.md`](m13_reviews.md)
- **Eşleşme sonrası iletişim:** [`m16_messaging.md`](m16_messaging.md)
- **Ücretli üyelik / öne çıkarma (premium boost):** [`m17_membership.md`](m17_membership.md)
- **Şikayet / kötüye kullanım moderasyonu:** [`m18_feedback.md`](m18_feedback.md)
- **Veri modeli (ER + modüller arası referans):** [`veri_modeli.md`](veri_modeli.md)
- **Genel durum & eşleme tablosu:** [`00_genel_bakis.md`](00_genel_bakis.md)
- **Ürün gereksinimleri:** [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*EğitimÜssü — M12 Eşleştirme ve Keşif Modülü · Detaylı Tasarım | Faz 4 | Durum: 🔴 İskelet | Güncelleme: 2026-08-19 (kod-senkron: yalnızca `GET /api/matching/status` placeholder + `MatchingDbContext` + DI kaydı kodda; Domain/Application boş (`AssemblyReference.cs`). Tüm §2/§3 içerik "önerilen", kodda yok — doğrulandı)*
