---
title: "Eksik / Yapılmamış İşler Analizi (2026-09-02)"
summary: "Backend (15 modül) + mobil (41 ekran) kod gerçeğine karşı çıkarılmış, madde madde eksik iş envanteri; her madde için kanıt, etki, efor ve karar sütunu"
tags: [denetim, eksik-analizi, envanter, planlama]
authority: derived
updated: 2026-09-02
---

# 🔍 EğitimÜssü — Eksik / Yapılmamış İşler Analizi

> **Tarih:** 2026-09-02
> **Kapsam:** Backend (`src/` — 15 modül + Shared + API.Host), Mobil (`mobile/` — 41 ekran), testler (`tests/`, `mobile/test/`), altyapı (`.github/`, `infra/`, `render.yaml`), dokümanlar (`doc/`).
> **Yöntem:** Her modülün API/Application/Domain/Infrastructure katmanı okundu; endpoint envanteri `*Module.cs` dosyalarından **koddan** çıkarıldı; backend + mobil test paketleri **çalıştırıldı**; en kritik bulgu (A-01) API ayağa kaldırılarak **canlı yeniden üretildi**. Doküman iddiaları koda karşı doğrulandı; çelişkilerde kod esas alındı.
> **İlgili:** [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) (K/Y/O kodları) · [`2026-06-30_kapsamli_kod_denetimi.md`](2026-06-30_kapsamli_kod_denetimi.md) (önceki denetim) · [`../yol_haritasi.md`](../yol_haritasi.md)

---

## 0. Bu dokümanı nasıl kullanacaksın

Her madde şu alanlara sahip:

| Alan | Anlamı |
|------|--------|
| **ID** | Sabit kimlik (A-01, M02-3 …). Plan çıkarırken bu ID'lere atıf yapacağız. |
| **Kanıt** | Kodda nerede doğrulandı (`dosya:satır`) veya hangi komut/çıktı ile görüldü. |
| **Etki** | Bu eksik yüzünden bugün ne çalışmıyor / hangi risk var. |
| **Efor** | Kaba tahmin: **S** ≤1 gün · **M** 2–4 gün · **L** 1–2 hafta · **XL** 2+ hafta. |
| **Karar** | **SEN DOLDURACAKSIN.** Öneri: `✅ Yap` · `⏳ Sonra` · `❌ İptal` · `❓ Konuşalım` |
| **Not** | Senin notun (kapsam daraltma, farklı yaklaşım, bağımlılık vb.) |

**Akış:** (1) Karar + Not sütunlarını doldur → (2) bana "planı çıkar" de → (3) kararladığın maddelerden dilimlere bölünmüş, bağımlılık sıralı uygulama planı (`docs/superpowers/plans/`) üretilir.

**Lejant (etki şiddeti):** 🔴 Bozuk/çalışmıyor · 🟠 Eksik, ana akışı kısıtlıyor · 🟡 Eksik, ikincil · ⚪ İyileştirme/hijyen

---

## 1. Mevcut Durum Fotoğrafı (koddan sayılmış)

| Ölçüm | Değer |
|-------|-------|
| Backend modül klasörü | 15 (+`docs`) |
| Gerçek modül (domain + endpoint + migration) | **12** |
| Boş iskelet (yalnız `GET /status`, 0 entity, 0 migration) | **3** — Matching, Reviews, Reporting |
| Hiç kodu olmayan planlı modül | **3** — M16 Messaging, M17 Membership, M18 Feedback |
| Toplam endpoint (koddan) | ~150 · Study 38, Scheduling 20, Students 13, Payments 11, Parents 11, ProgressTracking 8, Assignments 7, Identity 10, LessonSessions 4, Teachers 3, Notifications 2, Settings 2, iskeletler 3 |
| Backend test | Unit **156 ✅** · Architecture **4 ✅** · Integration **12 ✅ / 1 ❌ / 4 atlandı (Docker yok)** |
| Mobil test | **41 ✅**, **5 test dosyası derlenmiyor** |
| Mobil ekran (`*_page.dart`) | 41 |
| Web (Angular) | Yok |
| Admin arayüzü / API grubu | Yok |

---

## 2. 🔴 A — Acil: Kodda doğrulanmış bozukluklar

> Bunlar "eksik özellik" değil, **şu an bozuk olan** şeyler. Yeni özellikten önce gelmeli.

### A-01 — `PUT /api/teachers/profiles/{userId}` 500 veriyor (öğretmen profilini kaydedemiyor) ✅ Düzeltildi 2026-09-02

- **Kanıt:** `dotnet test` → `TeacherWorkflowIntegrationTests.UpdateTeacherProfile_Should_Not_Change_IsVerified` **FAIL** (500). API ayağa kaldırılıp elle yeniden üretildi: `create=200`, `update=500`, gövde `{"code":"shared.unexpected", ...}`. Sunucu log'u:
  `DbUpdateConcurrencyException: Attempted to update or delete an entity that does not exist in the store` → `UpdateTeacherProfileCommandHandler.Handle`
- **Kök neden (aday):** `TeachersDomainModel.cs:120-125` — `AvailabilitySlots.Clear()/AddRange()`, `Subjects.Clear()/AddRange()`, `Certificates.Clear()/AddRange()` deseni; çocuk koleksiyonlar her güncellemede yeni `Id` ile baştan yaratılıyor (`TeacherProfileFeatures.cs:239-248`). Çoklu branş özelliğiyle (`351d8d6`) geldi. `TeachersDbContext.cs:60-62` ilişkileri `HasMany(...).WithOne().HasForeignKey(...)` — cascade/orphan davranışı açıkça tanımlı değil.
- **Etki:** Öğretmen mobil uygulamada profilini **güncelleyemez** (`mobile/lib/features/teacher_profile/data/repositories/teacher_repository_impl.dart:88`). CI (`backend-ci.yml`) `main` üzerinde **kırmızı**.
- **Not:** Doğrulama InMemory sağlayıcıyla yapıldı (Docker yok → Postgres testleri atlandı). Postgres'te de patlayıp patlamadığı **ayrıca doğrulanmalı**; ama en azından CI kırık ve desen risklidir (delete+insert yerine "diff/merge" olmalı).
- **Efor:** S (fix) + S (Postgres'te doğrulama testi)
- **Karar:** ` `
- **Not (senin):** ` `
- **Çözüm (P01 Task 1):** Kök neden ikiliydi. (1) `Clear`+`AddRange` yerine doğal anahtara göre **merge** (`MergeSubjects`/`MergeCertificates`/`MergeAvailabilitySlots`). (2) Asıl tetikleyici: çocuk entity `Id`'leri `IIdGenerator` ile istemcide atanıyor ama EF onları `ValueGenerated.OnAdd` sayıyordu; bu yüzden izlenen profile eklenen **yeni** çocuk `Added` değil **`Modified`** olarak izlenip var olmayan satırı UPDATE etmeye çalışıyordu → `ValueGeneratedNever()` ile çözüldü. İlişkiler açık `DeleteBehavior.Cascade` aldı. Testler: `tests/Unit/TeacherProfileUpdateMergeTests.cs` (2 test, EF InMemory + gerçek `SaveChanges`) + `RealDatabaseIntegrationTests.TeacherProfile_Update_Should_Merge_Subjects_On_Real_Postgres` (Testcontainers). `UpdateTeacherProfile_Should_Not_Change_IsVerified` yeşile döndü.

### A-02 — Mobilde 5 test dosyası derlenmiyor ✅ Düzeltildi 2026-09-02 (P01)

- **Kanıt:** `flutter test` → `41 +, 5 -`. Hata: `_DelayedAuthRepository.login has fewer named arguments than those of overridden method 'AuthRepository.login'` (`mobile/test/widget_test.dart:52`, `:63`). Etkilenen dosyalar: `widget_test.dart`, `core/routing/app_router_test.dart`, `features/auth/presentation/cubit/auth_cubit_test.dart`, `features/dashboard/presentation/cubit/dashboard_cubit_test.dart`, `features/scheduling/presentation/pages/scheduling_page_test.dart`.
- **Etki:** Auth, routing, dashboard ve takvim ekranı için **regresyon koruması fiilen yok**; testler "geçiyor" görünüyor ama yüklenmiyor.
- **Efor:** S
- **Karar:** Ortak sahteler `mobile/test/helpers/` altına alındı (tek nokta kuralı → `doc/architecture/mobile_flutter.md` §17.1).
- **Not:** Kök neden iki başlıydı: (1) 3 dosyada auth sahtesi `AuthRepository.login/register`'a eklenen `roleId` argümanını uygulamıyordu, (2) 2 dosyada `_FakeSchedulingRepository`, Ç-06 ile `SchedulingRepository`'ye eklenen 5 öğrenci metodunu uygulamıyordu. `FakeAuthRepository` + `FakeSchedulingRepository` yazıldı; `flutter test` → **47 başarılı, 0 başarısız**.

### A-03 — E-posta hiç gönderilmiyor (şifre sıfırlama + e-posta doğrulama uçtan uca çalışmıyor) 🔴

- **Kanıt:** `IdentityRepositoryAndSecurity.cs:132-135` → `NullIdentityNotificationService`: `SendPasswordResetAsync` ve `SendEmailVerificationAsync` gövdesi `Task.CompletedTask`. Projede hiç SMTP/MailKit/SendGrid bağımlılığı yok (`grep` ile doğrulandı).
- **Etki:** `POST /password-reset/request` ve `/email-verification/request` uçları 200 dönüyor ama kullanıcıya **token ulaşmıyor** → şifresini unutan kullanıcı hesabına giremez; e-posta doğrulama tamamlanamaz.
- **Kapsam:** Sağlayıcı seçimi (SMTP / Resend / SendGrid / Amazon SES), `IEmailSender` soyutlaması, şablonlar (doğrulama, şifre sıfırlama), dev ortamında "log'a yaz" implementasyonu.
- **Efor:** M
- **Karar:** ` `
- **Not:** ` `

### A-04 — Push bildirimi hiç yok (Y5 açık) 🔴

- **Kanıt:** `NotificationDispatching.cs:24-38` → `DispatchDueRemindersAsync` yalnız `reminder.MarkSent(...)` yapıyor, hiçbir yere göndermiyor. Mobilde `firebase_messaging` + `flutter_local_notifications` **pubspec'te tanımlı ama `mobile/lib/` içinde tek satır kullanım yok**; `google-services.json` / `GoogleService-Info.plist` **yok**. Cihaz token'ı kaydeden endpoint/tablo yok.
- **Etki:** Ders hatırlatması, ödev/ödeme/veli bildirimleri **kullanıcıya hiç ulaşmıyor** — sadece DB'de "gönderildi" işaretleniyor. M11'in tamamı ve veli/öğrenci bildirim değer önerisi bu maddeye bağlı.
- **Kapsam:** `DeviceToken` entity + `POST/DELETE /api/notifications/device-tokens`, FCM/APNs gönderici (`INotificationSender`), geçersiz token devre dışı bırakma, mobil izin akışı + foreground/background handler, deep link.
- **Efor:** L
- **Karar:** ` `
- **Not:** ` `

### A-05 — Mobilde mock fallback varsayılan olarak AÇIK 🟠

- **Kanıt:** `mobile/lib/core/config/app_config.dart:36-43` → `USE_MOCK_FALLBACK` default `true`, `MOCK_FALLBACK_FEATURES` default `'*'`. Kullanan repolar: payments, scheduling, students, study, assignments, lesson_sessions, dashboard, parent, teacher_profile, auth, notifications.
- **Etki:** Geliştirme ortamında API hata verse bile ekranlar **sahte veriyle** doluyor → gerçek entegrasyon hataları görünmez oluyor (bu denetimde A-01'in fark edilmemesinin muhtemel sebeplerinden). `isProductionLike` ile beta/prod'da kapanıyor, yani prod riski değil; **geliştirme disiplini** riski.
- **Öneri:** Varsayılanı `false` yap, açık niyetle `--dart-define=USE_MOCK_FALLBACK=true` ile aç; ya da mock aktifken ekranda görünür bir "MOCK" rozeti göster.
- **Efor:** S
- **Karar:** ` `
- **Not:** ` `

### A-06 — Postgres parolası hâlâ `appsettings.json` içinde 🟠

- **Kanıt:** `mimari_inceleme.md` Y2'nin kalan yarısı; JWT anahtarı çıkarıldı ama `Password=postgres` duruyor.
- **Etki:** Sır sızıntısı riski; prod'da yanlış konfigürasyonla açılma ihtimali (JWT'de olduğu gibi fail-fast guard yok).
- **Efor:** S
- **Karar:** ` `
- **Not:** ` `

---

## 3. 🟠 B — Backend: modül modül eksikler

### M01 — Identity (🟢 çekirdek tamam)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M01-1 | `GET /me` yok | `IdentityModule.cs` — yalnız `/users/{userId}` | Her istekte istemci `userId` taşıyor; token'dan çözülmeli | S | | |
| M01-2 | `PUT /users/{userId}/status` (admin) yok | Domain'de `Active/Suspended/Closed` var, uç yok | Admin kötüye kullanan hesabı **askıya alamıyor** (M18 moderasyonun önkoşulu) | S | | |
| M01-3 | `DELETE /users/{userId}/roles/{role}` yok | Rol atama var, geri alma yok | Yanlış verilen rol geri alınamıyor | S | | |
| M01-4 | `GET /sessions` + `POST /sessions/revoke-all` yok | `UserSession` entity'sinde `DeviceName` var | Kullanıcı cihaz oturumlarını göremiyor/kapatamıyor | M | | |
| M01-5 | Telefon doğrulama (SMS/OTP) yok | Hiç SMS sağlayıcı bağımlılığı yok | PRD'de var; şu an telefon alanı doğrulanmamış veri | M | | |

### M02 — Teachers (🟢 ama en zayıf halka: 3 endpoint)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M02-1 | **Admin doğrulama ucu yok** — `PUT /profiles/{userId}/verification` + `TeacherVerifiedDomainEvent` | Y1'de `IsVerified` upsert'ten çıkarıldı ama yerine admin ucu konmadı | **Hiç kimse doğrulama rozeti veremiyor**; güven altyapısı (M12/M13) bunsuz anlamsız | S | | |
| M02-2 | Profil fotoğrafı yükleme yok | `ProfilePhotoUrl` düz string, sunucu tarafı yükleme yok | Öğretmen vitrini eksik; C-04'e (dosya depolama) bağlı | M | | |
| M02-3 | `GET /profiles` arama/listeleme yok | Şehir/ilçe/branş/format/ücret filtresi yok | **M12 eşleştirmenin doğrudan önkoşulu**; ayrıca admin listesi de yok | M | | |
| M02-4 | Pasifleştirme / silme yok | Öğretmen ayrılış senaryosu tanımsız | Ayrılan öğretmen sistemde aktif görünüyor | S | | |
| M02-5 | Uygunluk (availability) yönetimi yalnız upsert içinde | Slot ekle/sil ayrı uç yok | Mobil uygunluk düzenleyici (D-05) bunu istiyor | S | | |

### M03 — Students (🟢)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M03-1 | `POST /profiles/{studentId}/link-parent` yok | Veli davet kodu akışı var (`parent-invite`), doğrudan bağlama yok | Öğretmen mevcut veli kullanıcısını doğrudan bağlayamıyor | S | | |
| M03-2 | Branş ekleme/çıkarma uçları yok | Yalnız create/update içinde | Öğrenci branşı sonradan yönetilemiyor | S | | |
| M03-3 | `GET /profiles/by-parent/{parentUserId}` yok | Veli tarafı `Parents` üzerinden dolaşıyor | M09/M14 için doğrudan liste yok | S | | |
| M03-4 | `FreeStudentLimit = 5` sabit, premium bağlantısı yok | `StudentProfileFeatures.cs:9` → `// TODO(M17): premium sınırsız` | Üyelik gelmeden limit kaldırılamıyor (M17'ye bağlı) | S | | |

### M04 — Scheduling (🟢 en olgun modül, 20 endpoint)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M04-1 | Öğretmen ders listesinde tekrar (recurrence) açılımı yok | `GET /teachers/{id}/lessons` tek örnek döndürüyor; `RecurrenceExpander` yalnız öğrenci birleşik takviminde | Öğretmen haftalık tekrar eden dersleri takviminde göremiyor | M | | |
| M04-2 | Bireysel plan ↔ özel ders çakışmasında uyarı akışı eksik | `StudyScheduleConflict` var; öğrenciye gösterim/öncelik akışı tamamlanmamış (m08 açık maddesi) | Öğrenci çakışmayı fark etmiyor | S | | |

### M05 — LessonSessions (🟢 ama dar: 4 endpoint)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M05-1 | `InProgress` (başlat) ve `Cancelled` (iptal) geçişleri yok | `LessonSessionsModule.cs` — yalnız create/list/get/complete | Ders "başladı" durumu izlenemiyor; iptal edilen oturum modellenemiyor | S | | |
| M05-2 | **Planlı dersten oturum türetme yok** | `LessonScheduleId` alanı var ama otomasyon/tek-tık yok | Öğretmen her ders için oturumu **elle** açıyor → günlük kullanımda sürtünme | M | | |
| M05-3 | `ActualEnd > ActualStart` doğrulaması yok | Domain'de invariant yok | Hatalı süre kaydı mümkün (ödeme/rapor bozar) | S | | |
| M05-4 | `MeetingUrl` / `RecordingUrl` yok | M04'te `MeetingUrl` var, oturumda yok | Online ders linki oturum tarafında taşınmıyor | S | | |

### M06 — Assignments (🟢)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M06-1 | **`LessonResource` (ders kaynağı paylaşımı) hiç yok** | Domain'de entity yok | Öğretmen ders materyali paylaşamıyor (PRD Faz 1 maddesi) | M | | |
| M06-2 | Dosya depolama modüle gömülü, ortak soyutlama yok | `LocalAssignmentFileStorage.cs` — yerel disk | Çok-instance/Render'da **yüklenen dosya kaybolur**; bkz. C-04 | — | | |
| M06-3 | Son teslim yaklaşıyor / kaçırıldı bildirimi yok | Zamanlanmış iş yok | Öğrenci+veli uyarılmıyor (A-04'e bağlı) | M | | |
| M06-4 | Ödev değerlendirme/puanlama yok | Onay/geri gönder var, not/puan yok | Gelişim takibi (M10) ödevden beslenemiyor | S | | |

### M07 — Payments (🟢, 11 endpoint)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M07-1 | Kalıcı `Overdue` otomasyonu yok | `PaymentFeatures.cs:275-283` — `IsOverdue(now)` **sorgu anında** hesaplanıyor, zamanlanmış iş yok | Vade bildirimi tetiklenemiyor; "geciken" durumu event üretmiyor | M | | |
| M07-2 | Ders tamamlanınca otomatik ücret kaydı yok | M05 `complete` → Payments akışı yok (`IsChargeable` alanı var) | Öğretmen her ödemeyi elle giriyor | M | | |
| M07-3 | Dönemsel gelir raporu/grafik yok | M14'e bağlı | Premium "gelir analizi" özelliği yok | — | | |

### M08 — Study (🟢 en zengin modül, 38 endpoint)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M08-1 | Özel ders çakışmasında öncelik + öğrenci uyarısı | m08 dokümanındaki tek açık madde; M04-2 ile aynı iş | Öğrenci planı ile dersi çakışabiliyor | S | | |

### M09 — Parents (🟢)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M09-1 | Gelişim grafiği/raporu veli panelinde yok | M10-1/M14'e bağlı | Premium veli değer önerisi eksik | — | | |
| M09-2 | Veli↔öğretmen mesajlaşma yok | M16'ya bağlı | Veli iletişim kuramıyor | — | | |
| M09-3 | Ödev kaçırma bildirimi yok | M06-3 + A-04'e bağlı | Velinin en çok istediği bildirim yok | — | | |

### M10 — ProgressTracking (🟡 çalışır çekirdek)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M10-1 | `ProgressSnapshot` (haftalık/aylık zaman serisi) yok | 8 endpoint var; snapshot üretimi ve trend hesabı yok | Gelişim **grafiği** çizilemiyor (veli/öğretmen/rapor hepsi buna bağlı) | M | | |
| M10-2 | Öğretmen ve veli gelişim görünümü uçları yok | Yalnız öğrenci kapsamlı sorgular | Öğretmen öğrencisinin konu hâkimiyetini göremiyor | M | | |
| M10-3 | Hedefe ulaşınca otomatik `Achieved` + bildirim yok | `topic-goals` var, otomatik kapanış yok | Hedef motivasyon döngüsü tamamlanmıyor | S | | |
| M10-4 | M05 ders tamamlanması tüketilmiyor | Yalnız Study event'leri tüketiliyor | Öğretmenli ders, konu hâkimiyetini beslemiyor | S | | |

### M11 — Notifications (🟡 → gerçekte en zayıf 🟢-olmayan)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M11-1 | Gerçek teslimat yok | A-04 | Tüm bildirim değeri ölü | L | | |
| M11-2 | Cihaz token kaydı yok | Entity/uç yok | A-04'ün parçası | — | | |
| M11-3 | **Öğrenci ve öğretmen in-app bildirim listesi yok** | Yalnız `teachers/{id}/lesson-reminders` + `parents/{id}/notifications` | Öğretmenin mobil `/notifications` ekranı yalnız ders hatırlatması gösteriyor; öğrencinin bildirimi hiç yok | M | | |
| M11-4 | Okundu/okunmadı + rozet sayacı yok | Domain'de yok | Bildirim merkezi UX'i eksik | S | | |
| M11-5 | M15 tercihlerine saygı kısmi | Yalnız veli tercihleri (`notification-preferences`) | Kullanıcı bildirim türünü kapatamıyor | M | | |

### M12 — Matching (🔴 iskelet — 0 entity, 0 migration, yalnız `/status`)

| ID | Eksik | Etki | Efor | Karar | Not |
|----|-------|------|------|-------|-----|
| M12-1 | Öğretmen ilanı (oluştur/yayınla/kapat) | Platformun pazar-yeri tarafı yok | L | | |
| M12-2 | Öğrenci "ders arıyorum" ilanı | İki taraflı ilan yok | M | | |
| M12-3 | Arama/filtre API + `TeacherSearchProjection` read-model | Keşif yok (M02-3 + C-03 önkoşul) | L | | |
| M12-4 | Konum + yıldız + premium bileşik sıralama, "Öne Çıkan" etiketi | Gelir modelinin bir ayağı | M | | |
| M12-5 | Herkese açık öğretmen profil sayfası | Dışarıdan gelen kullanıcı hiçbir şey göremiyor | M | | |
| M12-6 | Talep gönder → kabul/ret → ilişki + otomatik ders kurulumu | Eşleştirme döngüsü kapanmıyor | L | | |

### M13 — Reviews (🔴 iskelet)

| ID | Eksik | Etki | Efor | Karar | Not |
|----|-------|------|------|-------|-----|
| M13-1 | `Review` domain + `ReviewEligibility` (dersi tamamlamış öğrenci) | Güven altyapısı yok | M | | |
| M13-2 | Genel yıldız + 4 alt kategori puanlama | — | S | | |
| M13-3 | Öğretmen yanıtı (olumsuz yorumu silememe kuralı) | — | S | | |
| M13-4 | Flag → moderasyon kuyruğu (M18 ile ortak) | Kötüye kullanım yönetilemiyor | M | | |
| M13-5 | `TeacherReviewPublished` → M12 projeksiyon güncellemesi | Sıralama puanı beslenemiyor | S | | |
| M13-6 | **Erken açılış:** Faz 1-2'de "öğretmene özel geri bildirim" (TeacherOnly, ortalamaya girmez) | Düşük maliyetli erken değer | S | | |

### M14 — Reporting (🔴 iskelet)

| ID | Eksik | Etki | Efor | Karar | Not |
|----|-------|------|------|-------|-----|
| M14-1 | Öğretmen aylık özeti (ders sayısı, tahsil/beklenen gelir, aktif öğrenci) | Premium öğretmen özelliği yok | M | | |
| M14-2 | Öğrenci çalışma analizi (M08 verisinden haftalık/aylık) | — | M | | |
| M14-3 | Öğrenci performans raporu (M10'dan konu bazlı değişim) | M10-1'e bağlı | M | | |
| M14-4 | PDF rapor üretimi + indirme | PRD premium maddesi; PDF kütüphanesi de yok | M | | |
| M14-5 | Boş zaman analizi (öğretmen) | — | S | | |

### M15 — Settings (🟡 etiketli ama gerçekte neredeyse boş)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M15-1 | **`GET`/`PUT /users/{userId}` genel ayar ucu yok** | `SettingsModule.cs` — yalnız `/status` ve `PUT /users/{id}/study-sharing` | Bildirim/gizlilik/güvenlik tercihleri **hiçbir yerde saklanmıyor** | M | | |
| M15-2 | Sahiplik authorizer + admin istisnası testi yok | — | Yetki boşluğu riski | S | | |
| M15-3 | `TerminateOtherSessions` davranışı yok | Identity ile entegrasyon yok | Güvenlik ayarı işlevsiz | S | | |
| M15-4 | Rol bazlı profil/bildirim izin matrisi yok | — | Veli/öğrenci görünürlük ayarları eksik | M | | |

### M16 / M17 / M18 — Hiç kodu olmayan modüller (🔴)

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| M16-1 | **Mesajlaşma modülü hiç yok** (klasör bile) | `src/Modules/` altında `Messaging` yok | Öğretmen↔öğrenci↔veli iletişimi yok; mobilde `flutter_chat_ui` paketi boşuna duruyor | XL | | |
| M17-1 | **Üyelik modülü hiç yok**; kalıntılar dağınık | `Students`/`Parents`'ta `MembershipTier` alanı, `Study/MembershipGate.cs`, `Shared/Contracts/MembershipDirectoryContract.cs` | Plan/abonelik/entitlement/otomatik yenileme yok | XL | | |
| M17-2 | Reklam altyapısı yok (`ad-placements`) | — | Free gelir modeli yok | M | | |
| M17-3 | Kampanya (ilk ay ücretsiz) + referans (arkadaşını getir) yok | — | Büyüme mekanizması yok | M | | |
| M17-4 | Ödeme sağlayıcı entegrasyonu + webhook yok | Hiç sağlayıcı bağımlılığı yok (iyzico/Stripe) | Para tahsil edilemiyor | L | | |
| M17-5 | Rol bazlı paywall/entitlement yayılımı yok | PRD §9 tablosunun tamamı uygulanmamış | Premium/free ayrımı fiilen yok | L | | |
| M18-1 | **Geri bildirim/şikayet modülü hiç yok** | — | Kötüye kullanım yönetilemiyor; mobil "Bize ulaşın" sadece bilgi sayfası | L | | |
| M18-2 | Ortak admin moderasyon kuyruğu (M13 flag + M16 şikayet) | — | Moderasyon süreci yok | L | | |

---

## 4. 🟠 C — Backend çapraz kesit / altyapı

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| C-01 | **Admin API grubu yok** | Yalnız dağınık `Roles.Contains("Admin")` kontrolleri (Payments, Study, Parents, Identity…); `/api/admin/*` yok | Admin rolü tanımlı ama **yönetim yüzeyi yok**: kullanıcı listesi, doğrulama, askıya alma, moderasyon | L | | |
| C-02 | Ortak `IFileStorage` soyutlaması yok (O8) | Yalnız `Assignments/Infrastructure/LocalAssignmentFileStorage.cs` | Yerel disk → Render/çok-instance'ta **veri kaybı**; profil foto + ders kaynağı bunu bekliyor | M | | |
| C-03 | **Modüller arası read-model mekanizması yok (O5)** | `Shared/Contracts` sadece nokta atışı arayüzler; genel projeksiyon deseni yok | M12 arama, M14 rapor, M09 zenginleştirme bunsuz ilerlemez — **en önemli mimari önkoşul** | L | | |
| C-04 | Gözlemlenebilirlik yok | OpenTelemetry / Serilog / Sentry / App Insights bağımlılığı **hiç yok**; yalnız `RequestContextLoggingMiddleware` | Prod'da hata/performans görünürlüğü yok | M | | |
| C-05 | Sorgu authorizer'ları varlığı iki kez yüklüyor (O2) | Örn. `GetStudentProfileByIdQuery` | Her korumalı sorguda çift DB gidişi | S | | |
| C-06 | `CommandDispatcher`/`QueryDispatcher` `dynamic` + reflection, cache yok (O4) | `Shared/Infrastructure/Application/` | Ölçekte CPU maliyeti; pipeline (validation/logging/tx) kancası yok | M | | |
| C-07 | Integration testler Docker'sız atlanıyor | `dotnet test` → **4 atlandı** (Testcontainers) | Yerelde gerçek Postgres/Redis doğrulaması yapılmıyor; A-01 gibi hatalar geç fark ediliyor | S | | |
| C-08 | Domain'de value object yok, invariant'lar handler'a kaçmış | 2026-06-30 denetimi DDD skoru 4.5/10 | Para/e-posta/tarih aralığı gibi kavramlar primitive | L | | |
| C-09 | `docs`, `Class1.cs`, boş `AssemblyReference.cs` gibi placeholder'lar | `src/Modules/docs` klasörü, iskelet modüllerdeki boş dosyalar | Hijyen | S | | |
| C-10 | API sürümleme stratejisi yok | Yalnız `/api/meta/version` | Kırıcı değişiklikte istemci yönetimi zor | S | | |

---

## 5. 🟠 D — Mobil (Flutter)

### D.1 Hiç olmayan ekran / akışlar

| ID | Eksik ekran / akış | Kanıt | Etki | Efor | Karar | Not |
|----|--------------------|-------|------|------|-------|-----|
| D-01 | **Şifremi unuttum** ekranı | `app_router.dart`'ta `/password-reset` yok | Sunucu ucu var, kullanıcı erişemiyor (A-03 ile birlikte tamamen ölü akış) | S | | |
| D-02 | **E-posta doğrulama** ekranı / deep link | Rota yok | Doğrulama tamamlanamıyor | M | | |
| D-03 | Cihaz oturumları ekranı | M01-4'e bağlı | Güvenlik özelliği yok | S | | |
| D-04 | Öğretmen **uygunluk (haftalık slot) düzenleyici** | `teacher_profile_page.dart` içinde görsel ızgara yok | Uygunluk verisi girilemiyor → M12 eşleştirme beslenemez | M | | |
| D-05 | Profil fotoğrafı yükleme (kamera/galeri) | `image_picker` yalnız ödev/not ekranlarında | Öğretmen vitrini eksik (M02-2) | S | | |
| D-06 | Public öğretmen profili / "öğrenci nasıl görüyor" önizlemesi | Ekran yok | — | M | | |
| D-07 | **Keşif / öğretmen arama** ekranları | `/student/discover` rotası var ama M12 API'si yok | Öğrenci öğretmen bulamıyor | L | | |
| D-08 | **Mesajlaşma** ekranları | Feature klasörü yok | M16'ya bağlı | L | | |
| D-09 | **Puanlama / yorum** ekranları | Feature klasörü yok | M13'e bağlı | M | | |
| D-10 | **Üyelik / paywall / reklam** ekranları | Feature yok; `more_page`'de sahte "Plus" kartı var | M17'ye bağlı; gelir modeli yok | L | | |
| D-11 | **Öğrenci/öğretmen bildirim merkezi** (okundu, rozet) | `notifications_page.dart` yalnız öğretmen ders hatırlatmaları | M11-3/4'e bağlı | M | | |
| D-12 | Admin arayüzü | Yok | C-01'e bağlı | L | | |
| D-13 | Geri bildirim/şikayet akışı | `more_page` "Bize ulaşın" statik bilgi sayfası | M18'e bağlı | M | | |

### D.2 Sahte/bağlanmamış olan mevcut ekranlar

| ID | Sorun | Kanıt | Etki | Efor | Karar | Not |
|----|-------|-------|------|------|-------|-----|
| D-14 | **`more_page` ayar sayfalarının tamamı sahte** | `more_page.dart` — `_showGeneralSettingsSheet`, `_showNotificationSettingsSheet`, `_showWorkSettingsSheet`, `_showHolidaySettingsSheet` hepsi lokal `setState`; hiçbiri backend'e yazmıyor | Kullanıcı ayar yaptığını sanıyor, uygulama kapanınca kayboluyor | M (M15-1 sonrası) | | |
| D-15 | **Abonelik kartı hardcoded** | `more_page.dart` → "Paket: Plus / Yenileme: 15 Haziran 2026 / Durum: Aktif" | Yanıltıcı; gerçek üyelik yok (M17) | S | | |
| D-16 | **Raporlar kartı hardcoded** | `more_page.dart` → "42 ders / %94 devam / %81 tahsilat" | Yanıltıcı sahte veri | S | | |
| D-17 | Auth yokken `'mock-teacher-user'` / `'mock-parent-user'` sentinel | `notifications_page.dart:18`, `parent_home_page.dart:18` | Oturum yoksa sahte kullanıcıyla istek atılıyor | S | | |

### D.3 Teknik hijyen

| ID | Sorun | Kanıt | Etki | Efor | Karar | Not |
|----|-------|-------|------|------|-------|-----|
| D-18 | Kullanılmayan ağır bağımlılıklar | `pubspec.yaml`: `firebase_messaging`, `flutter_local_notifications`, `flutter_chat_ui` — `lib/` içinde **hiç kullanım yok** | Paket boyutu + yanıltıcı "var" algısı | S | | |
| D-19 | l10n altyapısı yok | `app.dart:75-80` `supportedLocales`'te `en_US` var ama ARB/`l10n` klasörü yok; metinler hardcoded | İngilizce fiilen desteklenmiyor | M | | |
| D-20 | Türkçe karakter kullanılmayan metinler | `more_page.dart`: "Cikis yap", "Odeme hatirlatmalari", "Abonelik ayarlari" … | Marka kalitesi (`EğitimÜssü` adlandırma kuralıyla da çelişiyor) | S | | |
| D-21 | Öğrenci/veli akışları için widget testi yok | `mobile/test/` — parent hiç yok, study kısmi | Regresyon riski | M | | |
| D-22 | Flutter CI'da test adımı etkisiz | `build-android.yml` var; A-02 yüzünden testler zaten kırık | Koruma yok | S | | A-02 ✅ (P01) ile testler yeşil; CI adımının kendisi P13'te ele alınacak |

---

## 6. ⚪ E — Platform / operasyon

| ID | Eksik | Kanıt / Not | Etki | Efor | Karar | Not |
|----|-------|-------------|------|------|-------|-----|
| E-01 | **Angular web hiç yok** | `src/` altında yalnız API.Host/Modules/Shared; `doc/architecture/web_angular.md` 🔴 planlanan | PRD Faz 4-5; SEO/keşif tarafı yok | XL | | |
| E-02 | E-posta/SMS sağlayıcı sözleşmesi yok | A-03, M01-5 | — | — | | |
| E-03 | Nesne depolama (S3/Blob) yok | C-02 | — | — | | |
| E-04 | Ödeme sağlayıcı yok | M17-4 | — | — | | |
| E-05 | Yedekleme/geri yükleme, migration rollback planı yok | `render.yaml` + `ApplyModuleMigrationsAsync` otomatik migrate | Prod'da riskli otomatik migrate | M | | |
| E-06 | Staging ortamı / seed verisi yok | — | Beta testi (5-10 öğretmen) için gerekli | M | | |
| E-07 | KVKK/veri saklama-silme politikası uygulanmamış | Hesap silme ucu bile yok | Yasal risk | M | | |

---

## 7. ⚪ F — Doküman driftleri (kod ≠ doküman)

> CLAUDE.md kuralı: **kod doğruluk kaynağıdır**, doküman koda göre düzeltilir. Aşağıdakiler dokümanda "açık/eksik" görünüyor ama **kodda yapılmış**:

| ID | Doküman | Yanlış iddia | Kod gerçeği | Karar | Not |
|----|---------|--------------|-------------|-------|-----|
| F-01 | `m01_identity.md:276-277` | Mobil refresh interceptor + secure storage eksik | `TokenRefreshInterceptor` + `flutter_secure_storage` **var** (Y3/Y7 kapandı) | | |
| F-02 | `m03_students.md:326-327` | Self-register + davet/kabul akışı eksik | `POST /links/claim`, `/invite`, `/accept`, `/reject` **var** | | |
| F-03 | `m06_assignments.md:295,297-298` | `AssignmentSubmission` + öğrenci görünümü + dosya depolama eksik | Submission + attachment + öğrenci uçları **var** (ortak `IFileStorage` hâlâ yok → C-02 doğru) | | |
| F-04 | `m04_scheduling.md:398` | `Planned → Completed` geçişi eksik | `POST /lessons/{id}/complete` **var** | | |
| F-05 | `yol_haritasi.md` faz tablosu | Faz 2 "🔴 İskelet", Faz 3 "🔴 İskelet" | Study 🟢 (38 endpoint), Parents 🟢 (11 endpoint), ProgressTracking 🟡 | | |
| F-06 | `m09_parents.md:314`, `m11_notifications.md:257` | Kontrol listeleri açık | Kısmen yapılmış (veli dashboard + bildirim motoru) — madde madde gözden geçirilmeli | | |
| F-07 | `modules/00_genel_bakis.md` §3 | "Mevcut mobil app öğretmen odaklı" | Öğrenci (4 sekme) + veli panelleri de kodda | | |

---

## 8. Bağımlılık haritası (plan için)

```
A-01, A-02  ── bağımsız, hemen ─────────────────────────────┐
A-03 (e-posta) ──► D-01 (şifremi unuttum), D-02 (doğrulama) │
A-04 (push)  ──► M11-* ──► M06-3, M07-1, M09-3, D-11        │  Faz 0
C-02 (IFileStorage) ──► M02-2/D-05 (foto), M06-1 (kaynak)   │
C-03 (read-model O5) ──► M12-3, M14-*, M09-1                │
M15-1 (Settings) ──► D-14 (ayar ekranları), M11-5           ┘

M02-1 (doğrulama) + M02-3 (arama) ──► M12 (eşleştirme) ──► M13 (yorum) ──► M18 (moderasyon)
M10-1 (snapshot)  ──► M14-3 (performans raporu) ──► M09-1 (veli grafiği)
M17 (üyelik)      ──► M03-4 (limit), D-10 (paywall), M17-2 (reklam)
C-01 (admin API)  ──► D-12 (admin arayüz), M18-2 (moderasyon kuyruğu)
```

---

## 8.1 Plan Seti (2026-09-02'de yazıldı)

Bu envanterden türetilen uygulama planları: **master tasarım** → [`docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md`](../../docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md) · **14 plan** → `docs/superpowers/plans/2026-09-02-01…14-*.md`.

| Plan | Kapsam |
|------|--------|
| P01 Onarım | A-01, A-02, A-05, A-06, C-07, C-09, F-01…F-07 |
| P02 E-posta altyapısı | A-03, M01-1, D-01, D-02 |
| P03 Push bildirim | A-04, M11-1…4, D-11 |
| P04 Dosya depolama | C-02, M06-1/2, M02-2, D-05 |
| P05 Ayarlar | M15-1…4, M11-5, D-14…D-17 |
| P06 Öğretmen MVP | M02-1/3/4/5, M03-1/2/3, M05-1…4, M06-3/4, M07-1/2, M04-1/2, M08-1, M09-3, D-04 |
| P07 Read-model | C-03, C-05, C-06 |
| P08 Gelişim & raporlama | M10-1…4, M14-1…5, M09-1, M07-3 |
| P09 Üyelik & gelir | M17-1…5, M03-4, D-10, D-15 |
| P10 Mesajlaşma | M16-1, M09-2, D-08 |
| P11 Eşleştirme & yorum | M12-1…6, M13-1…6, D-06/07/09 |
| P12 Admin & moderasyon | C-01, M18-1/2, M01-2/3, D-13 |
| P13 Operasyon & hijyen | C-04, C-10, E-05/06/07, M01-4, D-03, D-18…D-22 |
| P14 Web (Angular) | E-01, D-12 |

**Kapsam dışı (bilinçli):** C-08 (value object refactor), M01-5 (SMS/OTP) — gerekçeler master tasarım §5.1'de.

---

## 9. Ham öneri (sen değiştireceksin)

> Aşağıdaki sıra bir **öneri**; kararlarını yazdıktan sonra bunu birlikte yeniden dizeceğiz.

| Sıra | Dilim | İçerik | Gerekçe |
|------|-------|--------|---------|
| 1 | **Onarım** | A-01, A-02, A-05, A-06, C-07 | Bozuk olanı düzeltmeden yeni özellik anlamsız; CI yeşile döner |
| 2 | **İletişim omurgası** | A-03 (e-posta), A-04 (push) + M11-2/3/4, D-01, D-02, D-11 | Ürünün "geri dönüş" döngüsü bunsuz yok |
| 3 | **Ortak altyapı (Faz 0 kalanı)** | C-02 (IFileStorage), M15-1 (Settings) + D-14, C-03 (read-model kararı) | Üst fazların hepsi bunlara dayanıyor |
| 4 | **Öğretmen MVP kapanışı** | M02-1, M02-3, M05-1/2, M06-1, M07-1/2, M04-1, D-04, D-05 | Beta (5-10 öğretmen) hedefi |
| 5 | **Gelişim & veli derinleşmesi** | M10-1/2/3, M09-1, M14-1/2/3 | Veli premium değeri |
| 6 | **Üyelik & gelir** | M17-1..5, M03-4, D-10, D-15 | Gelir modeli |
| 7 | **Pazar yeri** | M12-*, M13-*, M18-*, C-01, D-07/09/12/13 | PRD §10.1: en son |
| 8 | **Web** | E-01 | — |

---

## 10. Karar özeti (senin dolduracağın hızlı tablo)

| Dilim | Karar | Not |
|-------|-------|-----|
| 1 — Onarım | | |
| 2 — İletişim omurgası | | |
| 3 — Ortak altyapı | | |
| 4 — Öğretmen MVP kapanışı | | |
| 5 — Gelişim & veli | | |
| 6 — Üyelik & gelir | | |
| 7 — Pazar yeri | | |
| 8 — Web | | |

---

*Eksik/Yapılmamış İşler Analizi | Güncelleme: 2026-09-02 | Toplam madde: **121** — A 6 · B 69 · C 10 · D 22 · E 7 · F 7*
