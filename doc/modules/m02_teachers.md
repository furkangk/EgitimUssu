---
title: "M02 — Öğretmen Profili (Teachers)"
summary: "Öğretmen profili CRUD + haftalık uygunluk + çoklu branş/sertifika + profil fotoğrafı depolama + admin doğrulama + filtreli arama/pasifleştirme çalışır durumda"
tags: [modul, teachers, profil, uygunluk, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Teachers/**
updated: 2026-11-15
---

# 👨‍🏫 Öğretmen Profili (Teachers) Modülü (M02) — Detaylı Tasarım Dokümanı

> **PRD: M02 Öğretmen Profili** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (CRUD + uygunluk + çoklu branş/sertifika çalışıyor; GET yetkilendirmesi de eklendi — K3 kapandı)**
>
> **Amaç:** Öğretmenin kendini platformda **tanıttığı, ücretlendirdiği ve haftalık uygunluğunu** ortaya koyduğu
> profil katmanını yönetmek. Bu profil; takvimde ders planlama (M04), eşleştirme (M12) ve öğrenciye görünürlük için
> temel veri kaynağıdır. Öğretmenin "günlük çalışma aracı" yolculuğunun ilk adımıdır.
>
> İlgili: [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`m01_identity.md`](m01_identity.md) · [`m03_students.md`](m03_students.md) · [`m04_scheduling.md`](m04_scheduling.md) · [`m05_lesson_sessions.md`](m05_lesson_sessions.md) · [`m07_payments.md`](m07_payments.md) · [`m15_settings.md`](m15_settings.md) · [`mimari_inceleme.md`](mimari_inceleme.md) · [`veri_modeli.md`](veri_modeli.md) · [`00_genel_bakis.md`](00_genel_bakis.md) · [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

### İptal politikası (P17 T5)

`PUT /api/teachers/profiles/{userId}/cancellation-policy` gövdesi `{ noticeHours, chargePercent }`; yalnız profil sahibi Teacher yazabilir (başka öğretmen/bağsız kullanıcı 403). Yanıt `TeacherProfileResponse` içindeki `cancellationNoticeHours` ve `cancellationChargePercent` alanlarıdır; GET profil de bunları döndürür. Geçersiz değer 400 `teachers.invalid_cancellation_policy`, profil yoksa 404.

`CancellationPolicy` değer kaydı NoticeHours 0–720, ChargePercent 0–100 aralığını doğrular. `teacher_profiles.CancellationNoticeHours int` ve `CancellationChargePercent numeric(5,2)` varsayılan 0/0 (kapalı); `TeacherCancellationPolicy` migration’ı mevcut profilleri de bu varsayılanla taşır. Genel profil güncellemesi politikayı sıfırlamaz. Scheduling politikayı `ICancellationPolicyDirectory` salt-okunur sözleşmesinden okur. Mobil politika düzenleyicisi bu görevde eklenmedi; API kullanılabilir.


## 1. Mevcut Durum (Koddan Doğrulanmış)

`src/Modules/Teachers/` katmanları incelenerek çıkarılmıştır.

| Yetenek | Durum | Kanıt (kod) |
|---------|-------|-------------|
| Öğretmen profili oluşturma | ✅ var | `CreateTeacherProfileCommandHandler` |
| Profil güncelleme | ✅ var | `UpdateTeacherProfileCommandHandler` |
| Profil getirme (userId ile) | ✅ var | `GetTeacherProfileByUserIdQueryHandler` |
| Haftalık uygunluk slotları | ✅ var | `TeacherAvailabilitySlot` + upsert akışı |
| **Çoklu branş** (birincil `Subject` korunur) | ✅ var (Dilim D) | `TeacherSubject` alt-koleksiyonu + `teacher_subjects` tablosu; upsert `Subjects` listesi (boşsa `Subject`'ten türetilir) |
| **Sertifika/deneyim** | ✅ var (Dilim D) | `TeacherCertificate` alt-koleksiyonu + `teacher_certificates` tablosu; upsert `Certificates` listesi |
| Çevrimiçi/yüz yüze ayrımı (slot bazında) | ✅ var | `IsOnlineAvailable`, `IsInPersonAvailable` |
| Ders biçimi (yüz yüze/online/hibrit) | ✅ var | enum `TeacherLessonFormat` |
| Saatlik ücret + para birimi | ✅ var | `HourlyRateAmount`, `Currency` (vars. `TRY`) |
| Tek profil garantisi (kullanıcı başına) | ✅ var | `teachers.profile_exists` (409) |
| Uygunluk geçerlilik kontrolü | ✅ var | `EndTime <= StartTime` → `teachers.invalid_availability` |
| Komut yetkilendirmesi (create/update) | ✅ var | `TeacherProfileCommandAuthorizer` |
| Domain event yayını | ✅ var | `TeacherProfileCreatedDomainEvent`, `TeacherProfileUpdatedDomainEvent` |
| **`IsVerified`'in yalnız admin tarafından yazılması** | ✅ kapatıldı | `UpdateTeacherProfileCommand`/`UpsertTeacherProfileRequest`/`TeacherProfile.Update()` metodundan çıkarıldı; update akışı `IsVerified`'e dokunmuyor — bkz. mimari_inceleme **Y1** |
| **`GET /profiles/{userId}` yetkilendirici** | ✅ kapatıldı (2026-06-26) | `TeacherProfileQueryAuthorizer` eklendi — kimlik doğrulanmış tüm roller okuyabilir; bkz. mimari_inceleme **K3** |
| Profil fotoğrafı için dosya-depolama altyapısı | 🟢 var (2026-09-03, P04) | `ProfilePhotoKey` + `IFileStorage`; `POST`/`GET /profiles/{userId}/photo` |
| Öğretmen listeleme / arama (şehir/ilçe/branş/format/ücret filtresi) | ✅ var (2026-09-04, P06/M02-3) | `SearchTeacherProfilesQueryHandler` + `TeacherSearchFilter`; `GET /profiles` sayfalı (`take` en fazla 50) |
| **Profil pasifleştirme/aktifleştirme** | ✅ var (2026-09-04, P06/M02-4) | `TeacherProfile.IsActive` + `Deactivate()`/`Activate()`; `POST /profiles/{userId}/deactivate` · `/activate` |
| **Admin doğrulama rozeti** | ✅ var (2026-09-03, P06/M02-1) | `SetTeacherVerificationCommandHandler` + `TeacherVerifiedDomainEvent` |
| Tatil/izin istisnaları (ScheduleException) | 🔴 eksik | Modellenmedi → **M04 Scheduling**'de ele alınacak |

> **Özet:** Profil CRUD + haftalık uygunluk + **Admin doğrulama ucu** (P06/M02-1) **çalışır durumdadır**. Y1 ve K3 kapatıldı: `IsVerified` update akışından çıkarıldı; profil okuma ucu (`GET /profiles/{userId}`) artık `TeacherProfileQueryAuthorizer` ile yetkilendirilir (Application `TeacherProfilePolicies.cs`, Infrastructure DI kaydı mevcut). Profil fotoğrafı depolama 2026-09-03'te (P04/M02-2), **filtreli arama (`GET /profiles`) ve pasifleştirme** 2026-09-04'te (P06/M02-3, M02-4) eklendi. Arama yalnız **aktif** profilleri döndürür ve sayfa boyutu 50 ile sınırlıdır. Kalan açık nokta tatil/izin istisnalarıdır (M04).

---

## 2. Domain Modeli

Kaynak: `src/Modules/Teachers/Domain/TeachersDomainModel.cs`. Şema: **`teachers`**.
Tablolar: `teacher_profiles`, `teacher_availability_slots`, `teacher_subjects`, `teacher_certificates`.

### 2.1 🟢 Mevcut (koddan) — `TeacherProfile` (AggregateRoot&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Profil kimliği |
| `UserId` | Guid | Identity'deki kullanıcı (1 kullanıcı = 1 öğretmen profili) |
| `FullName` | string | Ad soyad |
| `Subject` | string | **Birincil branş** (korunur — domain event + eşleştirme bu alana bağlı). Çoklu branş için §2.5 `Subjects` |
| `City` | string | Şehir |
| `District` | string | İlçe |
| `Biography` | string? | Tanıtım metni |
| `Headline` | string? | Kısa başlık/slogan |
| `LessonFormat` | enum `TeacherLessonFormat` | Ders biçimi |
| `ExperienceYears` | int | Deneyim yılı |
| `EducationLevel` | string | Eğitim seviyesi |
| `HourlyRateAmount` | decimal | Saatlik ücret tutarı |
| `Currency` | string | Para birimi (varsayılan `"TRY"`, `ToUpperInvariant` ile normalize) |
| `IsVerified` | bool | Doğrulama rozeti (yalnızca admin akışıyla `true` olmalı — bkz. §4) |
| `IsActive` | bool | P06/M02-4: profil aramada görünür mü (varsayılan `true`). Pasif profil **listelenmez ama silinmez** — ders/öğrenci geçmişi korunur |
| `ProfilePhotoKey` | string? | P04: profil fotoğrafının `IFileStorage` anahtarı, **maks. 512** (public URL değil). Yanıtta `profilePhotoUrl` olarak `/api/teachers/profiles/{userId}/photo` rotası döner |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |
| `AvailabilitySlots` | List&lt;`TeacherAvailabilitySlot`&gt; | Haftalık uygunluk |
| `Subjects` | List&lt;`TeacherSubject`&gt; | Çoklu branş (§2.5) |
| `Certificates` | List&lt;`TeacherCertificate`&gt; | Sertifika/deneyim (§2.6) |

**Davranışlar:** Yapıcı `TeacherProfileCreatedDomainEvent` yayar. `Update(...)` tüm düzenlenebilir alanları (uygunluk slotları, **`Subjects` ve `Certificates`** koleksiyonlarını `Clear` + `AddRange` ile) günceller ve `TeacherProfileUpdatedDomainEvent` yayar. `Update()` imzası `isVerified` parametresi **içermez**; doğrulama durumu yalnızca admin/doğrulama akışıyla (`SetVerification()`) değişebilir. `SetVerification()` değer değişmediğinde olay yaymaz (idempotent). `Deactivate()`/`Activate()` yalnız `IsActive` + `UpdatedOnUtc` günceller, olay yaymaz.

### 2.2 🟢 Mevcut (koddan) — `TeacherAvailabilitySlot` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Slot kimliği |
| `TeacherProfileId` | Guid | Bağlı profil |
| `DayOfWeek` | `System.DayOfWeek` | Haftanın günü |
| `StartTime` | TimeOnly | Başlangıç saati |
| `EndTime` | TimeOnly | Bitiş saati |
| `IsOnlineAvailable` | bool | Bu slotta çevrimiçi ders verilebilir mi |
| `IsInPersonAvailable` | bool | Bu slotta yüz yüze ders verilebilir mi |

> Yanıtta slotlar `DayOfWeek` sonra `StartTime` ile sıralı döner (`TeacherProfileMappings`).

### 2.3 🟢 Mevcut (koddan) — Enum & Domain Events

| Enum | Değerler |
|------|----------|
| `TeacherLessonFormat` | `InPerson = 1`, `Online = 2`, `Hybrid = 3` |

```
TeacherProfileCreatedDomainEvent(Guid TeacherProfileId, Guid UserId, string Subject,
                                 string City, string District, DateTime CreatedOnUtc)
TeacherProfileUpdatedDomainEvent(Guid TeacherProfileId, Guid UserId, string Subject, DateTime UpdatedOnUtc)
```

### 2.4 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Gerekçe |
|-------|---------|
| ✅ ~~`TeacherSubject` alt-koleksiyonu (çoklu branş)~~ | **Yapıldı (Dilim D)** — §2.5. Birincil `Subject` korunur. `TeacherSubject.Level` (seviye) hâlâ önerilebilir. |
| ~~`IsVerified` için ayrı davranış + `TeacherVerifiedDomainEvent`~~ ✅ **(P06)** | `SetVerification(bool, DateTime)` eklendi; **değer değişmediyse olay yayılmaz** (idempotent, gereksiz bildirim yok) |
| `ScheduleException` (tatil/izin istisnası) | Haftalık uygunluğun belirli tarihlerde geçersiz kılınması → **M04 Scheduling**'de modellenecek |
| `AverageRating` / `ReviewCount` (okuma modeli) | Puanlama (M13) entegrasyonu için profil özetinde gösterim |
| ~~`ProfilePhoto` yükleme entegrasyonu~~ **✅ (2026-09-03, P04)** | `ProfilePhotoKey` + ortak `IFileStorage`; `FileValidation` ile 10 MB + uzantı beyaz listesi; dosya yalnız yetkili uçtan akıtılır |

### 2.5 🟢 Mevcut (koddan) — `TeacherSubject` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Kayıt kimliği |
| `TeacherProfileId` | Guid | Bağlı profil |
| `Subject` | string | Branş adı (`HasMaxLength(120)`) |

> Çoklu branş. Birincil `TeacherProfile.Subject` korunur; upsert'te `Subjects` listesi boş gelirse birincil `Subject`'ten tek satır türetilir. Update'te tam değiştirme (`Clear` + `AddRange`). Mevcut profiller için birincil branş migration backfill'i ile `teacher_subjects`'e taşındı.

### 2.6 🟢 Mevcut (koddan) — `TeacherCertificate` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Kayıt kimliği |
| `TeacherProfileId` | Guid | Bağlı profil |
| `Title` | string | Sertifika/deneyim başlığı (`HasMaxLength(200)`, zorunlu) |
| `Institution` | string? | Veren kurum (`HasMaxLength(200)`) |
| `Year` | int? | Alınan yıl |
| `FileUrl` | string? | Belge bağlantısı (`HasMaxLength(512)`) |

> Sertifika/deneyim listesi. Upsert'te `Title` boş olan kayıtlar filtrelenir. Update'te tam değiştirme (`Clear` + `AddRange`).

---

## 3. API Sözleşmesi

Tüm uçlar `RoutePrefix = /api/teachers` altında ve grup **`RequireAuthorization("AuthenticatedUser")`** ile korunur.
`Result<T>` döner; hata kodları `TeachersModule.ToHttpResult` ile HTTP'ye eşlenir.

### 3.1 Mevcut Endpoint'ler ✅

| Yetenek | Method + Route | Yetki kontrolü | İstek | Yanıt |
|---------|----------------|----------------|-------|-------|
| Profil oluştur | `POST /profiles` | `TeacherProfileCommandAuthorizer` (create) | `UpsertTeacherProfileRequest` | `TeacherProfileResponse` |
| Profil güncelle | `PUT /profiles/{userId:guid}` | `TeacherProfileCommandAuthorizer` (update) | `UpsertTeacherProfileRequest` | `TeacherProfileResponse` |
| Profil getir | `GET /profiles/{userId:guid}` | `TeacherProfileQueryAuthorizer` (K3 kapandı) | — | `TeacherProfileResponse` |
| **Doğrulama rozeti ver/geri al** (M02-1) | `PUT /profiles/{userId:guid}/verification` | `SetTeacherVerificationCommandAuthorizer` (**yalnız Admin**) | `{ isVerified }` | `TeacherProfileResponse` |
| **Profil ara/listele** (M02-3) | `GET /profiles?city=&district=&subject=&lessonFormat=&minRate=&maxRate=&onlyVerified=&skip=&take=` | `TeacherSearchQueryAuthorizer` (kimliği doğrulanmış her rol) | — | `PagedResult<TeacherSummaryResponse>` |
| **Profili pasifleştir** (M02-4) | `POST /profiles/{userId:guid}/deactivate` | `TeacherActivationCommandAuthorizer` (sahibi veya Admin) | — | `TeacherProfileResponse` |
| **Profili aktifleştir** (M02-4) | `POST /profiles/{userId:guid}/activate` | `TeacherActivationCommandAuthorizer` (sahibi veya Admin) | — | `TeacherProfileResponse` |

**Modüller-arası sözleşme (Shared.Contracts):** M02, öğretmenin **haftalık uygunluk şablonunun** otoritesidir ve
`ITeacherAvailabilityDirectory` sözleşmesini `TeacherAvailabilityDirectory` ile uygular (M14-5, 2026-09-04):
`ListWeeklySlotsAsync(teacherUserId) → IReadOnlyCollection<TeacherWeeklySlot>` (gün + başlangıç/bitiş saati).
Reporting'in boş zaman analizi bu sözleşmeyle okur — Teachers'ın `DbContext`'ine erişmeden, proje referansı olmadan.
Saatler **UTC duvar saati** kabul edilir (repo genelinde tüm hesaplar UTC).

**İstek/yanıt sözleşmeleri (koddan):**

```
TeacherAvailabilityItem(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
                        bool IsOnlineAvailable, bool IsInPersonAvailable)

TeacherCertificateItem(string Title, string? Institution, int? Year, string? FileUrl)

UpsertTeacherProfileRequest(Guid UserId, string FullName, string Subject, string City, string District,
                            string? Biography, string? Headline, TeacherLessonFormat LessonFormat,
                            int ExperienceYears, string EducationLevel, decimal HourlyRateAmount,
                            string Currency,
                            IReadOnlyCollection<TeacherAvailabilityItem> AvailabilitySlots,
                            IReadOnlyCollection<string> Subjects,
                            IReadOnlyCollection<TeacherCertificateItem> Certificates)

TeacherProfileResponse(Guid Id, Guid UserId, string FullName, string Subject, string City, string District,
                       string? Biography, string? Headline, string LessonFormat, int ExperienceYears,
                       string EducationLevel, decimal HourlyRateAmount, string Currency, bool IsVerified,
                       string? ProfilePhotoUrl /* indirme rotasi */, IReadOnlyCollection<TeacherAvailabilityResponse> AvailabilitySlots,
                       IReadOnlyCollection<string> Subjects,
                       IReadOnlyCollection<TeacherCertificateResponse> Certificates,
                       DateTime CreatedOnUtc, DateTime UpdatedOnUtc)

TeacherCertificateResponse(Guid Id, string Title, string? Institution, int? Year, string? FileUrl)

// M02-3 arama yanıtı (hafif görünüm — biyografi/sertifika/uygunluk taşımaz)
TeacherSummaryResponse(Guid UserId, string FullName, string Subject, IReadOnlyCollection<string> Subjects,
                       string City, string District, string LessonFormat, decimal HourlyRateAmount,
                       string Currency, bool IsVerified, bool IsActive, int ExperienceYears, string? Headline)
```

> **Arama filtreleri (`TeacherSearchFilter.Apply`):** şehir/ilçe/branş büyük-küçük harf duyarsız tam eşleşme; branş hem birincil `Subject`
> hem de çoklu `Subjects` listesinde aranır; `minRate`/`maxRate` sınırlar **dahil**; `onlyVerified=true` yalnız rozetlileri getirir.
> Sıralama: **doğrulanmışlar önce**, sonra ücret artan, sonra ad. `take` 1–50 arasına çekilir (verilmezse 20), `skip` negatifse 0 sayılır.
> **Pasif profiller hiçbir filtre kombinasyonunda dönmez.** Filtre ifadesi Application katmanında tek yerdedir; hem EF sorgusu hem birim testi
> aynı ifadeyi kullanır (testte geçip üretimde farklı davranan filtre riski yok).
> `Subjects` boş gönderilirse birincil `Subject`'ten tek elemanlı liste türetilir; `Certificates` içinde `Title` boş olanlar yok sayılır.

> ✅ **Y1 kapatıldı:** `UpsertTeacherProfileRequest` artık `IsVerified` alanı **içermez**. Client'tan gelen JSON'da bu alan olsa dahi yok sayılır. `TeacherProfile.Update()` da `isVerified` parametresi almaz; doğrulama durumu update akışında hiç dokunulmaz.

### 3.2 Hata Kodları → HTTP Eşleme (koddan)

| Hata kodu | HTTP | Mesaj |
|-----------|------|-------|
| `teachers.profile_exists` | **409** | Bu kullanıcı için öğretmen profili zaten oluşturulmuş. |
| `teachers.profile_not_found` | **404** | Öğretmen profili bulunamadı. |
| `shared.forbidden` | **403** | Bu işlemi yapma yetkiniz yok. |
| `teachers.invalid_availability` | 400 | Uygunluk saatleri geçersiz. |
| `teachers.invalid_request` | 400 | (Validator) Profil bilgileri eksik veya hatalı. |

> ⚠️ **Arama girdisi normalizasyonu (P06 Task 10, 2026-09-04):** `GET /profiles` filtrelerinde şehir/ilçe/branş
> karşılaştırmasının **sol tarafı SQL'e** (`lower(...)`) çevrilir, **sağ tarafı .NET'te** hesaplanır. `ToLower()`
> kültüre bağlı olduğu için tr-TR sunucuda `'I' → 'ı'` oluyor ve Postgres'in ürettiği `'i'` ile eşleşmiyordu —
> "Istanbul" araması **boş dönüyordu**. `ToLowerInvariant()` tek başına yetmez: `'İ'`'yi `"i" + birleşen nokta`
> (2 kod noktası) yapar, Postgres ise düz `"i"` üretir. Çözüm `TeacherSearchFilter.NormalizeForSearch`:
> Türkçe I varyantlarını (`'I'`, `'İ'`) düz `'i'`'ye katlayıp invariant küçültür.
> Regresyon: `RealTeacherSearchIntegrationTests` (gerçek Postgres, tr-TR kültürü altında 9 varyant).

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [x] **`GET /profiles/{userId}` için authorizer** — `TeacherProfileQueryAuthorizer` eklendi (K3 kapandı); kimlik doğrulanmış tüm roller okuyabilir. (Profilin herkese açık kısmı için ayrı "public" projeksiyon hâlâ düşünülebilir.)
- [x] **`PUT /profiles/{userId}/verification`** — ✅ **(P06/M02-1)** yalnız **Admin**; `SetTeacherVerificationCommand` + `SetTeacherVerificationCommandAuthorizer` + `TeacherProfile.SetVerification()` + `TeacherVerifiedDomainEvent`. **Y1 tamamen kapandı.**
- [x] **`POST /profiles/{userId}/photo`** (multipart, self/Admin) — dosya `IFileStorage`'a yazılır, `ProfilePhotoKey` **sunucuda** set edilir; yanıt `{ photoUrl }`. **`GET /profiles/{userId}/photo`** dosyayı yetkiyle akıtır. `UpsertTeacherProfileRequest`'ten `profilePhotoUrl` **kaldırıldı** — istemci fotoğrafı yazamaz (Y1 dersi). **(2026-09-03, P04/M02-2)**
- [x] **`GET /profiles` (arama/listeleme)** — ✅ **(2026-09-04, P06/M02-3)** şehir/ilçe/branş/format/ücret aralığı + `onlyVerified` filtreleri, sayfalı.
      Bu uç M12 (eşleştirme) için **geçici okuma yüzeyidir**: P11'de `TeacherSearchProjection` devreye girince handler projeksiyonu okuyacak, **dış sözleşme (URL + yanıt) korunacak**.
- [x] **Pasifleştirme** — ✅ **(2026-09-04, P06/M02-4)** `POST /profiles/{userId}/deactivate` · `/activate`. Kayıt **silinmez** (`DELETE` yok): pasif profil aramada görünmez, verisi ve ders geçmişi durur.

---

## 4. İş Kuralları

1. **Tek profil:** Bir kullanıcının yalnızca **bir** öğretmen profili olabilir (`teachers.profile_exists`, 409).
2. **Uygunluk geçerliliği:** Her slotta `EndTime > StartTime` zorunludur; aksi halde `teachers.invalid_availability` (400). Hem create hem update'te kontrol edilir.
3. **Doğrulama rozeti (`IsVerified`):**
   - Create'te kod **her zaman `false`** atar ✅.
   - Update'te `IsVerified` **hiç güncellenmez** (`UpsertTeacherProfileRequest` ve `TeacherProfile.Update()` imzasından çıkarıldı) ✅ — yalnız `PUT /profiles/{userId}/verification` (Admin) değiştirir ✅ **(P06/M02-1)**.
4. **Para birimi normalizasyonu:** `Currency.Trim().ToUpperInvariant()` (örn. `try` → `TRY`).
5. **Metin temizliği:** `FullName`, `Subject`, `City`, `District`, `EducationLevel`, `Biography`, `Headline` `Trim()` edilir.
6. **Validator (create & update):** `UserId != Guid.Empty`, ad/branş/şehir/ilçe/eğitim seviyesi/para birimi boş olamaz; `ExperienceYears >= 0`, `HourlyRateAmount >= 0` (`teachers.invalid_request`). Update validator aynı kuralları create üzerinden uygular.
7. **Komut yetkisi:** `TeacherProfileCommandAuthorizer` → **admin** her zaman; **öğretmen** yalnızca `command.UserId == kendi userId`'si için create/update yapabilir; aksi halde `shared.forbidden`.
8. **Çocuk koleksiyonlarda birleştirme (merge) semantiği:** Update'te uygunluk slotları, branşlar ve sertifikalar **doğal anahtara göre birleştirilir** (`TeacherProfile.MergeAvailabilitySlots` / `MergeSubjects` / `MergeCertificates`): istekte olmayan kayıt silinir, eşleşen kayıt **aynı PK ile korunur**, yeni kayıt eklenir. Dışarıdan görünen davranış yine **tam değiştirme** (replace) — istekteki liste nihai listedir — ama kimlikler korunduğu için EF "orphan update" hatası oluşmaz.
   - Doğal anahtarlar: branş → `Subject` (büyük/küçük harf duyarsız) · sertifika → `(Title, Institution, Year)` · slot → `(DayOfWeek, StartTime, EndTime)`; eşleşen slotta yalnız `IsOnlineAvailable` / `IsInPersonAvailable` güncellenir.

> **Güncelleme (2026-09-02, denetim A-01):** Eski "sil + yeniden ekle" (`Clear` + `AddRange`) deseni `PUT /api/teachers/profiles/{userId}` isteğinde **500** üretiyordu
> (`DbUpdateConcurrencyException: Attempted to update or delete an entity that does not exist in the store`). İki kök neden birlikte düzeltildi:
> 1. Çocuk koleksiyonlar artık doğal anahtara göre **birleştiriliyor** (yukarıdaki kural 8).
> 2. Çocuk entity'lerin `Id` alanı `ValueGeneratedNever()` olarak yapılandırıldı. Id'ler `IIdGenerator` ile **istemcide** atandığı hâlde EF onları
>    `ValueGenerated.OnAdd` sanıyor, bu yüzden izlenen bir profile eklenen yeni çocuğu `Added` yerine **`Modified`** olarak izliyor ve var olmayan satırı
>    UPDATE etmeye çalışıyordu. Bu kural, Id'si istemcide üretilen tüm çocuk entity'ler için geçerlidir.
>
> Ayrıca ilişkiler artık açık `OnDelete(DeleteBehavior.Cascade)` ile tanımlıdır.

---

## 5. Olay Akışı (Event-Driven)

```
Profil oluşturuldu  → TeacherProfileCreatedDomainEvent (TeacherProfileId, UserId, Subject, City, District)
                      → Outbox → (gelecek) Matching (M12): aday havuzunu günceller
                      → (gelecek) Search/okuma modeli: şehir/branş indeksini besler
Profil güncellendi  → TeacherProfileUpdatedDomainEvent (TeacherProfileId, UserId, Subject)
                      → (gelecek) Matching: ücret/uygunluk/branş değişimini yansıtır
```

> Olaylar **Outbox pattern** ile yayılır (`Shared/Infrastructure/Messaging`). Şu an aktif bir tüketici yok;
> eşleştirme ve arama modülleri (M12) için doğal entegrasyon noktasıdır.

---

## 6. Mobil Ekranlar (mevcut + planlanan)

`mobile/lib/features/teacher_profile/` (flutter_bloc/Cubit).

| Route | Sayfa | Durum | Açıklama |
|-------|-------|-------|----------|
| `/teacher-profile` | `TeacherProfilePage` | ✅ | Profil oluştur/düzenle (branş, şehir, ücret, biyografi, uygunluk ızgarası) |
| `/teacher-panel-preview` | önizleme | ✅ | Yalnız debug derlemesinde öğretmen paneli önizlemesi |
| `/` (dashboard) | `DashboardPage` | ✅ | Bugünkü dersler + bekleyen ödevler + geciken ödemeler (BFF endpoint; `dashboard` feature) |

**Uygunluk düzenleyici (D-04/M02-5, 2026-09-04):** `AvailabilityEditor` widget'ı haftanın 7 gününü ayrı satırlarda
listeler; her güne saat aralığı eklenir/silinir ve **her aralık kendi** `isOnlineAvailable` / `isInPersonAvailable`
bayrağını taşır (eskiden profil geneli iki anahtardan türetiliyordu). Kesişme kontrolü istemcide
(`availabilitySlotError`) yapılır; sunucudaki tek kural `EndTime > StartTime` olduğu için çakışma **yalnız** mobilde
engellenir. Kaydetme `TeacherProfileCubit.saveAvailability(...)` → `PUT /api/teachers/profiles/{userId}` (gövde profilin
tamamını taşır; §5 madde 8'deki merge semantiği sayesinde tekrar kaydetme güvenlidir).

### Eksik / planlanan mobil ekranlar ⚠️
- [x] **Uygunluk düzenleyici** — haftalık ızgara üzerinde slot ekle/sil + online/yüz yüze işaretleme (2026-09-04, P06/D-04/M02-5; `presentation/widgets/availability_editor.dart`).
- [x] **Profil fotoğrafı yükleme** — kamera/galeri seçimi + sunucu yükleme akışı (2026-09-03, P04/M02-2).
- [ ] **Doğrulama rozeti** göstergesi — yalnız okunur; sunucudan gelen `isVerified` değeri gösterilir, formda alan olmamalı ✅ (Y1 backend'de kapatıldı).
- [ ] **Önizleme:** öğrencinin profili nasıl gördüğü (public görünüm).

---

## 7. Kabul Kriterleri

- [x] Öğretmen kendi `UserId`'siyle profil oluşturabilir; ikinci profil 409 ile engellenir.
- [x] Profil güncellenebilir; uygunluk slotları tam değiştirme ile yenilenir.
- [x] `EndTime <= StartTime` olan slot reddedilir.
- [x] Yalnız admin veya profilin sahibi öğretmen create/update yapabilir.
- [x] Profil yanıtı uygunluk slotlarını gün+saat sıralı döndürür.
- [x] **`IsVerified` update akışından çıkarıldı** — client değeri artık yazılamaz; regresyon testi eklendi.
- [x] **`GET /profiles/{userId}` yetkilendirildi** — `TeacherProfileQueryAuthorizer` (K3 kapandı).
- [x] **Profil fotoğrafı** güvenli depolamaya yüklenebilir (2026-09-03, P04/M02-2).
- [x] **Doğrulama rozetini yalnız Admin verebilir/geri alabilir** (2026-09-03, P06/M02-1); aynı değer tekrar yazılırsa olay yayılmaz.
- [x] **Arama pasif profilleri döndürmez** ve `take` 50'nin üstüne çıkamaz (2026-09-04, P06/M02-3).
- [x] **Profili yalnız sahibi öğretmen veya Admin pasifleştirebilir** (2026-09-04, P06/M02-4).
- [x] **Uygunluk mobilden düzenlenebilir** (2026-09-04, P06/D-04/M02-5): haftanın 7 günü ızgarası, aralık ekle/sil, **slot bazlı** online/yüz yüze bayrağı; aynı gün içinde **kesişen** aralık istemcide engellenir (bitişik aralık — `18:00-20:00` + `20:00-21:00` — geçerlidir).

---

## 8. Eksikler ve Yapılacaklar (öncelik sırasıyla)

1. ✅ ~~**`IsVerified` yazma yolunu kısıtla (Y1)**~~ — upsert'ten çıkarıldı **ve** admin-only `PUT /profiles/{userId}/verification` + `TeacherVerifiedDomainEvent` eklendi (P06/M02-1). **Kalan yok.**
2. ✅ ~~**`GET /profiles/{userId}` yetkilendirmesi (K3)**~~ — `TeacherProfileQueryAuthorizer` eklendi (K3 kapandı). Kalan: herkese açık "public" projeksiyon ayrımı (opsiyonel).
3. ✅ ~~**Profil fotoğrafı dosya-depolama altyapısı**~~ — **yapıldı (2026-09-03, P04/M02-2)**: `IFileStorage` + format/boyut doğrulama.
4. ✅ ~~**Çoklu branş** — `TeacherSubject` koleksiyonu~~ — **yapıldı (Dilim D)**; birincil `Subject` korunur. Kalan: branş bazlı seviye (`Level`).
5. **Tatil/izin istisnaları (ScheduleException)** — **M04 Scheduling** ile koordineli modelleme.
6. ✅ ~~**Öğretmen listeleme/arama**~~ — **yapıldı (2026-09-04, P06/M02-3)**; kalıcı çözüm P11 `TeacherSearchProjection` (sözleşme korunur).
7. **Puanlama özeti** — `AverageRating`/`ReviewCount` okuma alanları (M13 ile).

---

## 9. İlişkili Dokümanlar

- Öğretmenin uçtan uca günlük yolculuğu → [`../roles/ogretmen.md`](../roles/ogretmen.md)
- Kimlik/oturum temeli → [`m01_identity.md`](m01_identity.md)
- Öğrenci tarafı ve öğretmen-öğrenci ilişkisi → [`m03_students.md`](m03_students.md)
- Takvim, ders planlama ve tatil/izin istisnaları → [`m04_scheduling.md`](m04_scheduling.md)
- Ders oturumları → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Ödeme/ücret takibi → [`m07_payments.md`](m07_payments.md)
- Ayarlar & güvenlik → [`m15_settings.md`](m15_settings.md)
- Güvenlik açıkları (Y1, K3) → [`mimari_inceleme.md`](mimari_inceleme.md)
- Aggregate ER şeması → [`veri_modeli.md`](veri_modeli.md)
- Genel durum ve endpoint envanteri → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*Öğretmen Profili (Teachers) Modülü (M02) — Detaylı Tasarım | Güncelleme: 2026-11-15 (P08 Task 4 / M14-4: `ITeacherDirectory` sözleşmesi eklendi — `TeacherDirectory.GetDisplayNameAsync` öğretmen adını Reporting'in PDF rapor başlığına açar; uygunluk sözleşmesinden ayrı tutuldu) · 2026-09-04 (P08 Task 3 / M14-5: `ITeacherAvailabilityDirectory` sözleşmesi eklendi — haftalık uygunluk şablonu Reporting'e açıldı) · 2026-09-04 (P06 Task 10 canlı doğrulama: **arama filtresi Türkçe 'I' hatası düzeltildi** — `TeacherSearchFilter.NormalizeForSearch` girdiyi Postgres'in `lower()` davranışıyla hizalar; tr-TR sunucuda "Istanbul" araması boş dönüyordu) · 2026-09-04 (P06/D-04/M02-5: mobil uygunluk düzenleyici — `AvailabilityEditor`, slot bazlı online/yüz yüze + çakışma engeli) · 2026-09-04 (P06/M02-3 + M02-4: filtreli arama `GET /profiles` + `IsActive` pasifleştirme) · 2026-09-03 (P06/M02-1: admin doğrulama ucu + `TeacherVerifiedDomainEvent`) · 2026-09-03 (P04/M02-2: profil fotoğrafı — `ProfilePhotoKey` + `POST`/`GET /profiles/{userId}/photo`; `profilePhotoUrl` istek gövdesinden kaldırıldı) · 2026-09-02 (A-01: çocuk koleksiyon merge + `ValueGeneratedNever` + açık cascade) · 2026-08-19 (kod-senkron: K3 `TeacherProfileQueryAuthorizer` kodda doğrulandı, GET yetkilendirmesi açık iddiaları düzeltildi) · 2026-07-18 (çoklu branş + sertifika — Dilim D)*
<!-- Y1 (IsVerified update akışına yazılabiliyor) 2026-06-24'te kapatıldı. -->

## P20 T1 — mobil sahte veri temizliği

Mobil öğretmen profilinde gerçek profil yüklenmeden örnek veri/düzenleyici gösterilmez. Sabit 4.8/126 yorum ve 18 aktif öğrenci metrikleri kaldırıldı; More özeti de puan üretmez. `/teacher-panel-preview` yalnız debug derlemesinde kayıtlıdır.

P20 T1 inceleme düzeltmesi: `TeacherRepositoryImpl.getProfile` 404 yanıtını normal modda aynen hata olarak iletir; profil yokluğu demo öğretmen üretmez. Demo yalnız `AppConfig.isMockFallbackEnabled("teacher_profile")` açıkken mümkündür; production/beta bu bayrağı engeller. Başarılı gerçek profil yanıtı korunur, yeni profil oluşturma akışı bu göreve eklenmez.
