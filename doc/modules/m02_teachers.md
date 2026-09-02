---
title: "M02 — Öğretmen Profili (Teachers)"
summary: "Öğretmen profili CRUD + haftalık uygunluk + çoklu branş/sertifika çalışır durumda; profil fotoğrafı depolama ve öğretmen arama/listeleme eksik"
tags: [modul, teachers, profil, uygunluk, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Teachers/**
updated: 2026-09-02
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
| Profil fotoğrafı için dosya-depolama altyapısı | 🔴 eksik | `ProfilePhotoUrl` string; yükleme/saklama servisi yok |
| Öğretmen listeleme / arama (şehir/branş filtresi) | 🔴 eksik | Yalnızca tekil `GET by userId` var (eşleştirme M12'de) |
| Tatil/izin istisnaları (ScheduleException) | 🔴 eksik | Modellenmedi → **M04 Scheduling**'de ele alınacak |

> **Özet:** Profil CRUD + haftalık uygunluk **çalışır durumdadır**. Y1 ve K3 kapatıldı: `IsVerified` update akışından çıkarıldı; profil okuma ucu (`GET /profiles/{userId}`) artık `TeacherProfileQueryAuthorizer` ile yetkilendirilir (Application `TeacherProfilePolicies.cs`, Infrastructure DI kaydı mevcut). Kalan açık noktalar profil fotoğrafı depolama + listeleme/arama gibi genişletmelerdir.

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
| `ProfilePhotoUrl` | string? | Profil fotoğrafı URL'i |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |
| `AvailabilitySlots` | List&lt;`TeacherAvailabilitySlot`&gt; | Haftalık uygunluk |
| `Subjects` | List&lt;`TeacherSubject`&gt; | Çoklu branş (§2.5) |
| `Certificates` | List&lt;`TeacherCertificate`&gt; | Sertifika/deneyim (§2.6) |

**Davranışlar:** Yapıcı `TeacherProfileCreatedDomainEvent` yayar. `Update(...)` tüm düzenlenebilir alanları (uygunluk slotları, **`Subjects` ve `Certificates`** koleksiyonlarını `Clear` + `AddRange` ile) günceller ve `TeacherProfileUpdatedDomainEvent` yayar. `Update()` imzası `isVerified` parametresi **içermez**; doğrulama durumu yalnızca admin/doğrulama akışıyla değişebilir.

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
| `IsVerified` için ayrı `VerifyTeacher(...)` davranışı + `TeacherVerifiedDomainEvent` | Doğrulamayı sıradan güncellemeden ayırıp yalnız admin akışına bağlamak (Y1 çözümü) |
| `ScheduleException` (tatil/izin istisnası) | Haftalık uygunluğun belirli tarihlerde geçersiz kılınması → **M04 Scheduling**'de modellenecek |
| `AverageRating` / `ReviewCount` (okuma modeli) | Puanlama (M13) entegrasyonu için profil özetinde gösterim |
| `ProfilePhoto` yükleme entegrasyonu | `ProfilePhotoUrl` yerine güvenli dosya-depolama (blob/object storage) + boyut/format doğrulama |

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

**İstek/yanıt sözleşmeleri (koddan):**

```
TeacherAvailabilityItem(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
                        bool IsOnlineAvailable, bool IsInPersonAvailable)

TeacherCertificateItem(string Title, string? Institution, int? Year, string? FileUrl)

UpsertTeacherProfileRequest(Guid UserId, string FullName, string Subject, string City, string District,
                            string? Biography, string? Headline, TeacherLessonFormat LessonFormat,
                            int ExperienceYears, string EducationLevel, decimal HourlyRateAmount,
                            string Currency, string? ProfilePhotoUrl,
                            IReadOnlyCollection<TeacherAvailabilityItem> AvailabilitySlots,
                            IReadOnlyCollection<string> Subjects,
                            IReadOnlyCollection<TeacherCertificateItem> Certificates)

TeacherProfileResponse(Guid Id, Guid UserId, string FullName, string Subject, string City, string District,
                       string? Biography, string? Headline, string LessonFormat, int ExperienceYears,
                       string EducationLevel, decimal HourlyRateAmount, string Currency, bool IsVerified,
                       string? ProfilePhotoUrl, IReadOnlyCollection<TeacherAvailabilityResponse> AvailabilitySlots,
                       IReadOnlyCollection<string> Subjects,
                       IReadOnlyCollection<TeacherCertificateResponse> Certificates,
                       DateTime CreatedOnUtc, DateTime UpdatedOnUtc)

TeacherCertificateResponse(Guid Id, string Title, string? Institution, int? Year, string? FileUrl)
```
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

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [x] **`GET /profiles/{userId}` için authorizer** — `TeacherProfileQueryAuthorizer` eklendi (K3 kapandı); kimlik doğrulanmış tüm roller okuyabilir. (Profilin herkese açık kısmı için ayrı "public" projeksiyon hâlâ düşünülebilir.)
- [ ] **`PUT /profiles/{userId}/verification`** — yalnız **admin**; `IsVerified`'i set edecek ayrı endpoint + `TeacherVerifiedDomainEvent` (Y1 kısmen kapatıldı — upsert'ten çıkarıldı; admin endpoint henüz yok).
- [ ] **`POST /profiles/{userId}/photo`** — dosya yükleme + güvenli depolama; `ProfilePhotoUrl` sunucuda set edilir.
- [ ] **`GET /profiles` (arama/listeleme)** — şehir/ilçe/branş/format/ücret aralığı filtreleri (eşleştirme M12 ile koordineli).
- [ ] **`DELETE /profiles/{userId}` / pasifleştirme** — öğretmen ayrılışı senaryosu.

---

## 4. İş Kuralları

1. **Tek profil:** Bir kullanıcının yalnızca **bir** öğretmen profili olabilir (`teachers.profile_exists`, 409).
2. **Uygunluk geçerliliği:** Her slotta `EndTime > StartTime` zorunludur; aksi halde `teachers.invalid_availability` (400). Hem create hem update'te kontrol edilir.
3. **Doğrulama rozeti (`IsVerified`):**
   - Create'te kod **her zaman `false`** atar ✅.
   - Update'te `IsVerified` **hiç güncellenmez** (`UpsertTeacherProfileRequest` ve `TeacherProfile.Update()` imzasından çıkarıldı) ✅ — yalnız gelecekteki admin-only endpoint değiştirebilir.
4. **Para birimi normalizasyonu:** `Currency.Trim().ToUpperInvariant()` (örn. `try` → `TRY`).
5. **Metin temizliği:** `FullName`, `Subject`, `City`, `District`, `EducationLevel`, `Biography`, `Headline`, `ProfilePhotoUrl` `Trim()` edilir.
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
| `/teacher-profile` | `TeacherProfilePage` | ✅ | Profil oluştur/düzenle (branş, şehir, ücret, biyografi, uygunluk) |
| `/teacher-panel-preview` | önizleme | ✅ | Giriş yapmadan öğretmen paneli önizlemesi |
| `/` (dashboard) | `DashboardPage` | ✅ | Bugünkü dersler + bekleyen ödevler + geciken ödemeler (BFF endpoint; `dashboard` feature) |

### Eksik / planlanan mobil ekranlar ⚠️
- [ ] **Uygunluk düzenleyici** — haftalık ızgara üzerinde slot ekle/sil + online/yüz yüze işaretleme (görsel).
- [ ] **Profil fotoğrafı yükleme** — kamera/galeri seçimi + sunucu yükleme akışı.
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
- [ ] **Profil fotoğrafı** güvenli depolamaya yüklenebilir.

---

## 8. Eksikler ve Yapılacaklar (öncelik sırasıyla)

1. ✅ ~~**`IsVerified` yazma yolunu kısıtla (Y1)**~~ — upsert'ten çıkarıldı. Kalan: admin-only `PUT /profiles/{userId}/verification` endpoint + `TeacherVerifiedDomainEvent`.
2. ✅ ~~**`GET /profiles/{userId}` yetkilendirmesi (K3)**~~ — `TeacherProfileQueryAuthorizer` eklendi (K3 kapandı). Kalan: herkese açık "public" projeksiyon ayrımı (opsiyonel).
3. **Profil fotoğrafı dosya-depolama altyapısı** — yükleme ucu + güvenli saklama + format/boyut doğrulama.
4. ✅ ~~**Çoklu branş** — `TeacherSubject` koleksiyonu~~ — **yapıldı (Dilim D)**; birincil `Subject` korunur. Kalan: branş bazlı seviye (`Level`).
5. **Tatil/izin istisnaları (ScheduleException)** — **M04 Scheduling** ile koordineli modelleme.
6. **Öğretmen listeleme/arama** — şehir/branş/format/ücret filtreleri (M12 ile).
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

*Öğretmen Profili (Teachers) Modülü (M02) — Detaylı Tasarım | Güncelleme: 2026-09-02 (A-01: çocuk koleksiyon merge + `ValueGeneratedNever` + açık cascade) · 2026-08-19 (kod-senkron: K3 `TeacherProfileQueryAuthorizer` kodda doğrulandı, GET yetkilendirmesi açık iddiaları düzeltildi) · 2026-07-18 (çoklu branş + sertifika — Dilim D)*
<!-- Y1 (IsVerified update akışına yazılabiliyor) 2026-06-24'te kapatıldı. -->
