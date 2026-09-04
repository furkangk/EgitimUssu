---
title: "Modüller — Genel Bakış ve Durum (İndeks)"
summary: "doc/modules/ klasörünün indeks dokümanı: M01–M18 modül listesi, backend eşlemesi ve durum tablosu"
tags: [modul, indeks, genel-bakis]
authority: code
code_refs:
  - src/Modules/*/API/*Module.cs
updated: 2026-09-04
---

# 🗂️ Modüller — Genel Bakış ve Durum (Modül İndeksi)

> Bu klasör (`doc/modules/`), her **backend modülünün** saf teknik tasarım dokümanını içerir
> (domain modeli, API sözleşmesi, iş kuralları, durum, eksikler). **Rol perspektifi** (öğretmen/öğrenci/veli
> yetenekleri ve akışları) artık ayrı klasördedir → [`../roles/`](../roles/00_roller_genel_bakis.md).
>
> İlgili üst dokümanlar:
> - [`../INDEX.md`](../INDEX.md) — tüm doküman haritası (buradan başla)
> - [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) — Ürün gereksinimleri (v2.1)
> - [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md) — Roller
> - [`../architecture/00_genel_bakis.md`](../architecture/00_genel_bakis.md) — Mimari (backend/mobil/web + genel bakış)
> - [`mimari_inceleme.md`](mimari_inceleme.md) · [`veri_modeli.md`](veri_modeli.md)
>
> **Güncelleme:** 2026-07-04

---

## 1. Modül İndeksi (M01–M18)

| M | Modül | Dosya | Backend (`src/Modules`) | Route | Durum |
|---|-------|-------|-------------------------|-------|-------|
| M01 | Kullanıcı & Rol | [`m01_identity.md`](m01_identity.md) | `Identity` | `/api/identity` | 🟢 |
| M02 | Öğretmen Profili | [`m02_teachers.md`](m02_teachers.md) | `Teachers` | `/api/teachers` | 🟢 |
| M03 | Öğrenci Profili | [`m03_students.md`](m03_students.md) | `Students` | `/api/students` | 🟢 / 🟡 (self-register) |
| M04 | Takvim & Planlama | [`m04_scheduling.md`](m04_scheduling.md) | `Scheduling` | `/api/scheduling` | 🟢 (link+tatil+erteleme+occurrence+öğrenci erteleme talebi, 2026-07-18) |
| M05 | Ders Oturumu | [`m05_lesson_sessions.md`](m05_lesson_sessions.md) | `LessonSessions` | `/api/lesson-sessions` | 🟢 |
| M06 | Not, Ödev & Kaynak | [`m06_assignments.md`](m06_assignments.md) | `Assignments` | `/api/assignments` | 🟢 (Dilim B: not görünürlüğü + ödev onay/geri gönder) |
| M07 | Ödeme Takibi | [`m07_payments.md`](m07_payments.md) | `Payments` | `/api/payments` | 🟢 (veli paylaşımı ⚠️) |
| M08 | Bireysel Çalışma | [`m08_study.md`](m08_study.md) | `Study` | `/api/study` | 🟢 (mobil dahil) |
| M09 | Veli Paneli | [`m09_parents.md`](m09_parents.md) | `Parents` | `/api/parents` | 🟢 |
| M10 | Gelişim Takibi | [`m10_progress_tracking.md`](m10_progress_tracking.md) | `ProgressTracking` | `/api/progress-tracking` | 🟡 Çalışır çekirdek (konu hâkimiyeti + hedef + mobil) |
| M11 | Bildirim | [`m11_notifications.md`](m11_notifications.md) | `Notifications` | `/api/notifications` | 🟢 (cihaz token + FCM push + in-app bildirim merkezi/rozet — P03 2026-09-03) |
| M12 | Eşleştirme & İlan | [`m12_matching.md`](m12_matching.md) | `Matching` | `/api/matching` | 🔴 İskelet |
| M13 | Puanlama & Yorum | [`m13_reviews.md`](m13_reviews.md) | `Reviews` | `/api/reviews` | 🔴 İskelet |
| M14 | Raporlama & Analiz | [`m14_reporting.md`](m14_reporting.md) | `Reporting` | `/api/reporting` | 🔴 İskelet |
| M15 | Ayarlar & Güvenlik | [`m15_settings.md`](m15_settings.md) | `Settings` | `/api/settings` | 🟢 (ayar oku/yaz + çalışma paylaşımı + 3 sözleşme: gizlilik/bildirim tercihi/oturum politikası; rol bazlı profil düzenleme bekliyor) |
| M16 | Mesajlaşma | [`m16_messaging.md`](m16_messaging.md) | _(yok — yeni)_ | `/api/messaging` | 🔴 Planlanan |
| M17 | Üyelik & Para Kazanma | [`m17_membership.md`](m17_membership.md) | _(yok — yeni)_ | `/api/membership` | 🔴 Planlanan |
| M18 | Geri Bildirim & Şikayet | [`m18_feedback.md`](m18_feedback.md) | _(yok — yeni)_ | `/api/feedback` | 🔴 Planlanan |

**Çapraz-kesit dokümanlar:** [`mimari_inceleme.md`](mimari_inceleme.md) (hata/güvenlik/öncelik) · [`veri_modeli.md`](veri_modeli.md) (ER şeması).

**Durum açıklaması:** 🟢 Domain + Application (CQRS) + API + migration (+ mobil) mevcut · 🟡 kısmen · 🔴 iskelet (yalnız `/status`; **K4/2026-07-01'den beri entity'siz iskelet modüller DbContext kaydetmez** — boş DbContext outbox taramasını çökertiyordu).

---

## 2. Teknoloji Yığını (Kodda Doğrulanmış)

| Katman | Teknoloji | Not |
|--------|-----------|-----|
| Backend | **.NET 9** (Modüler Monolit) | `Directory.Build.props`, `global.json` (SDK 9.0.311) |
| Mimari | Clean Architecture + DDD + CQRS + Outbox | Her modülde `API / Application / Domain / Infrastructure` |
| Veritabanı | **PostgreSQL** (modül başına ayrı şema + `DbContext` + migration) | Veri izolasyonu modül sınırında |
| Cache | **Redis** (lazy bağlantı) | `Shared/Infrastructure/Caching` |
| Mesajlaşma | Domain Events → Integration Events (Outbox) | `Shared/Infrastructure/Messaging` |
| Mobil | **Flutter** (`flutter_bloc`/Cubit, `go_router`, `dio`, `get_it`) | Birincil platform |
| Web | Angular (planlandı, henüz yok) | İkincil — Faz 4-5 |

### Backend modül katman yapısı (her modül için aynı)
```
src/Modules/<ModulAdi>/
 ├── API/            → ModuleDefinition, endpoint mapping, request/response DTO
 ├── Application/    → Command/Query + Handler + Policy (CQRS), Repository interface
 ├── Domain/         → AggregateRoot, Entity, Enum, DomainEvent
 └── Infrastructure/ → DbContext, Repository impl, Migrations, DI, Integration event handler
```

### Mobil feature yapısı
```
mobile/lib/features/<ozellik>/
 ├── data/           → model (DTO), repository_impl
 ├── domain/         → contracts (entity + repository interface)
 └── presentation/   → cubit (state mgmt), pages (ekranlar), widgets
```

---

## 3. Mobil Feature Eşlemesi

> Bir kullanıcı rolü birden çok backend modülünü kullanır (bkz. [`../roles/`](../roles/00_roller_genel_bakis.md)). Mevcut mobil app **üç rolü de kapsıyor**:
> öğretmen (dashboard/öğrenci/takvim/ödev/ödeme), **öğrenci** (`/student-home` 4 sekme + kronometre/test/hedef/başarım) ve **veli** (`/parent` paneli).

| Modül | Mobil Feature | Not |
|-------|---------------|-----|
| Identity | `auth` | giriş/kayıt/rol |
| Teachers | `teacher_profile` | profil |
| Students | `students` | öğretmenin öğrenci yönetimi |
| Scheduling | `scheduling` | takvim (syncfusion) |
| LessonSessions | `lesson_sessions` | oturum + not |
| Assignments | `assignments` | ödev/takip |
| Payments | `payments` | ödeme liste/form |
| Settings | `more` | ayarlar/hesap |
| API.Host (BFF) | `dashboard` | öğretmen pano özeti (bugünkü ders + bekleyen ödev + geciken ödeme) |
| Parents | `parent` | veli paneli (home/children/child_detail/notifications/profile) + `ParentBottomNav` + `/parent` rol navigasyonu |
| Study | `study` | öğrenci paneli: `student-home` (dashboard) + `study/timer`, `study/test`, `study/goals`, `study/history`, `study/achievements` + `/student-home` rol navigasyonu + self-register |
| Notifications | `notifications` | bildirim listesi (`NotificationsPage` + `NotificationsCubit`) |
| ProgressTracking | `progress` | `ProgressOverviewPage` (gelişim özeti) — backend M10 🟡 |
| Matching/Reviews/Reporting/Messaging/Membership/Feedback | _(yok)_ | planlanan (yeni özellik ekranları) |

---

## 4. Mevcut API Endpoint Envanteri (Koddan Çıkarıldı)

> **Tüketici idempotency'si (2026-08-26):** Aşağıdaki tüm `[consume]` handler'ları ortak `IdempotentIntegrationEventHandler`
> taban sınıfını (`Shared/Infrastructure`) ve modül şemasındaki `inbox_messages` tablosunu kullanır — `(EventId, Handler)`
> anahtarıyla tekrar-işleme atlanır, iş yazımı + inbox-işaretleme tek transaction'da atomik commit edilir. Detay:
> [`mimari_inceleme.md`](mimari_inceleme.md) (Y4), [`veri_modeli.md`](veri_modeli.md).

### Identity — `/api/identity`
```
POST /register   POST /login   POST /refresh
POST /password-reset/request   POST /password-reset/confirm
POST /email-verification/request   POST /email-verification/confirm
POST /logout (auth)   GET /me (auth)   GET /users/{userId} (auth)
POST /users/{userId}/roles (auth, yalnız Admin — rol atama)
```
> Not (K1): `POST /register` yalnız `Teacher/Student/Parent` kabul eder; `Admin` reddedilir. Yükseltilmiş rol yalnız `POST /users/{id}/roles` ile atanır.
### Teachers — `/api/teachers`
```
POST /profiles   PUT /profiles/{userId}   GET /profiles/{userId}
POST /profiles/{userId}/photo   GET /profiles/{userId}/photo   (profil fotografi; self/Admin yukler, kimlik dogrulanmis herkes okur; M02-2 2026-09-03)
PUT /profiles/{userId}/verification   (dogrulama rozeti; YALNIZ Admin, TeacherVerifiedDomainEvent; M02-1 2026-09-03)
GET /profiles?city=&district=&subject=&lessonFormat=&minRate=&maxRate=&onlyVerified=&skip=&take=   (filtreli arama; auth, yalniz aktif profiller, take<=50; M02-3 2026-09-04)
POST /profiles/{userId}/deactivate   POST /profiles/{userId}/activate   (arama gorunurlugu; sahibi veya Admin; M02-4 2026-09-04)
```
> Not (M02-3): `GET /profiles` M12 (eşleştirme) için **geçici okuma yüzeyi**; P11'de `TeacherSearchProjection` devreye girince handler projeksiyonu okuyacak, URL + yanıt sözleşmesi korunacak.
> Not (Dilim D): profil upsert artık **çoklu branş** (`Subjects`) ve **sertifika/deneyim** (`Certificates`) listelerini de taşır; birincil `Subject` korunur. Tablolar: `teacher_subjects`, `teacher_certificates`.
### API.Host BFF — (compose root, modül sınırları dışı)
```
GET /api/teachers/profiles/{teacherUserId}/dashboard-summary  (auth)
    → Scheduling + Assignments + Payments paralel sorgula; bugünkü dersler, bekleyen ödevler, geciken ödemeleri tek yanıtta döndür
```
### Students — `/api/students`
```
POST /profiles   PUT /profiles/{studentId}   GET /profiles/{studentId}
GET /profiles/by-user/{userId}   GET /profiles/by-teacher/{teacherUserId}?includeArchived=   (liste link üzerinden)
POST /teachers/{teacherUserId}/students/{studentId}/archive|unarchive   PUT .../rate   (arşiv + öğrenci bazlı ücret, B-04/B-07 2026-07-18)
POST /teachers/{teacherUserId}/students/{studentId}/invite   POST /links/{linkId}/accept|reject   (çoklu öğretmen davet/kabul, B-06 2026-07-18)
POST /links/claim   (6 haneli davet koduyla profili devral + mevcut self-profil varsa birleştir/merge, Ö-C 2026-07-19)
POST /profiles/{studentId}/parent-invite   (öğretmen veli davet kodu üretir → ParentInviteResponse, Veli V-D 2026-07-19)
[expose] IParentInviteDirectory (Shared.Contracts) — Parents claim bunu tüketir
```
### Scheduling — `/api/scheduling`
```
POST /lessons   PUT /lessons/{lessonId}   POST /lessons/{lessonId}/cancel
POST /lessons/{lessonId}/reschedule   DELETE /lessons/{lessonId}   (B-02/B-09, 2026-07-18)
POST /lessons/{lessonId}/complete   GET /lessons/{lessonId}
GET /teachers/{teacherUserId}/lessons?startAtUtc=&endAtUtc=   (tarih aralığı filtreli)
POST /teachers/{teacherUserId}/time-off   GET /teachers/{teacherUserId}/time-off?startAtUtc=&endAtUtc=   DELETE /teachers/{teacherUserId}/time-off/{timeOffId}   (tatil bloğu, B-01 2026-07-18)
GET /students/{studentId}/lessons?startAtUtc=&endAtUtc=       (öğrenci kendi dersleri, IDOR korumalı)
GET /students/{studentId}/calendar?startAtUtc=&endAtUtc=      (birleşik takvim: öğretmen dersleri + kendi programı, tekrarlar + occurrence istisnaları genişletilmiş)
POST /students/{studentId}/study-entries   PUT /study-entries/{entryId}   DELETE /study-entries/{entryId}   (öğrenci kişisel programı)
POST /students/{studentId}/lesson-requests   GET /teachers/{teacherUserId}/lesson-requests?onlyPending=   POST /lesson-requests/{requestId}/accept   POST /lesson-requests/{requestId}/reject   (öğrenci ders erteleme talebi, Ö-F 2026-07-18)
```
### LessonSessions — `/api/lesson-sessions`
```
POST /            (oturum oluştur)        GET / ?filtre   (liste)
POST /{lessonSessionId}/complete
POST /{lessonSessionId}/start   POST /{lessonSessionId}/cancel   (yasam dongusu gecisleri; sahibi ogretmen/Admin, gecersiz gecis 409; M05-1 2026-09-04)
POST /from-schedule/{lessonScheduleId}   (planli dersten oturum turetme; idempotent, plan yoksa 404, iptal/tamamlanmis plan 409; M05-2 2026-09-04)          GET /{lessonSessionId}   (complete: IsChargeable, B-08 2026-07-18)
```
### Assignments — `/api/assignments`
```
GET  /            (liste)
POST /lesson-sessions/{lessonSessionId}/follow-up
GET  /lesson-sessions/{lessonSessionId}/follow-up
POST /{assignmentId}/complete        (öğrenci tamamlar)
POST /{assignmentId}/submission      (öğrenci dosya yükler — multipart)
GET  /{assignmentId}/attachment      (teslim dosyasını indir — öğrenci/öğretmen/admin)
POST /{assignmentId}/approve         (öğretmen onaylar + geri bildirim — T-06.7)
POST /{assignmentId}/return          (öğretmen geri gönderir + geri bildirim — T-06.8)
POST /lesson-resources   GET /lesson-resources?teacherUserId=&studentId=&lessonSessionId=
GET /lesson-resources/{resourceId}/file   DELETE /lesson-resources/{resourceId}   (ders kaynagi; ogretmen ekler/arsivler, ogrenci kendisine acik olanlari gorur; M06-1 2026-09-03)
```
### Payments — `/api/payments`
```
POST /records   PUT /records/{paymentRecordId}   GET /records/{paymentRecordId}
GET /teachers/{teacherUserId}/records   /summary   /records/filter   /records/search   (search: metin+durum+öğrenci+tarih, sayfalı)
POST /records/{paymentRecordId}/declare-paid   (veli "ödedim" beyanı, Veli V-G 2026-07-19)
GET /teachers/{teacherUserId}/payment-declarations?onlyPending=   POST /payment-declarations/{id}/confirm|reject   (öğretmen teyit/red)
[expose] IParentAccessDirectory (Shared.Contracts) — Parents uygular; veli-öğrenci onaylı bağ doğrulaması
[consume] LessonSessions.LessonSessionCompleted → tamamlanan ücretli dersten LessonFee PaymentRecord (M07-2 2026-09-04; ücret IStudentDirectory'den, oturum başına tek kayıt, idempotent (ortak inbox_messages))
[hosted] PaymentOverdueScanner (saatlik tur: vadesi geçmiş Pending/PartiallyPaid → Status=Overdue + PaymentBecameOverdueDomainEvent; M07-1 2026-09-04)
```
### Notifications — `/api/notifications`
```
GET /teachers/{teacherUserId}/lesson-reminders?activeOnly=
GET /parents/{parentUserId}/notifications   (veli bildirim listesi, Premium; Veli V-E 2026-07-19)
POST /device-tokens   DELETE /device-tokens/{token}   (cihaz push token kaydi/silme, auth; M11-2 2026-09-03)
GET /users/{userId}/notifications?onlyUnread=&skip=&take=   (in-app bildirim merkezi, self/Admin; M11-3 2026-09-03)
GET /users/{userId}/unread-count   (rozet sayisi, self/Admin; M11-4 2026-09-03)
POST /{notificationId}/read   (okundu isaretle, idempotent; M11-4 2026-09-03)
[consume] Assignments.AssignmentCreated / LessonSessions.LessonSessionCompleted / Payments.PaymentRecordUpdated / Parents.ParentLinkConnectionNotice → ParentNotification + UserNotification (Premium + tercih kapılı, idempotent (ortak inbox_messages))
[consume] Payments.PaymentBecameOverdue → UserNotification(Kind=PaymentOverdue): öğretmene koşulsuz + Premium & "ödemeler" tercihi açık veliye (M07-1 2026-09-04, idempotent (ortak inbox_messages))
[hosted] ParentWeeklySummaryService (haftalık özet, Premium)
[hosted] NotificationDispatcher (30 sn tur: vakti gelen LessonReminder + push edilmemiş UserNotification → IPushSender/FCM; P03 2026-09-03)
```
### Parents — `/api/parents`  (tümü auth "AuthenticatedUser")
```
POST /profiles   GET /profiles/{userId}   PUT /{parentUserId}/notification-preferences
POST /children/link   POST /children/{linkId}/approve   /reject   /revoke   (onay: öğrenci/öğretmen/Admin)
POST /children/claim-invite   (öğretmen davet kodunu girerek çocuğa bağlan → Approved, Veli V-D 2026-07-19)
PUT /{parentUserId}/membership-tier   (Admin: veli Free/Premium, Veli V-E 2026-07-19)
[expose] IParentNotificationDirectory (Shared.Contracts) — Notifications veli bildirim motoru bunu tüketir
[consume] LessonSessions.LessonSessionCreated|Completed / Assignments.AssignmentCreated|Completed / Payments.PaymentRecordCreated|Updated / Students.StudentProfileCreated → 4 read-model projeksiyonu (dashboard özeti, idempotent (ortak inbox_messages))
GET  /{parentUserId}/children
GET  /{parentUserId}/children/{studentId}/dashboard   (yalnız Approved bağda; değilse 403; study alanları gizlilik filtreli — Veli V-B)
```
### Study — `/api/study`  (tümü auth "AuthenticatedUser"; öğrenci kendi StudentId'sine erişir)
```
POST /sessions/start   /sessions/manual
POST /sessions/{id}/pause   /resume   /complete   /discard   /recover   (pause/complete: opsiyonel clientEffectiveMinutes; recover: takılı seansı kurtarır — Ö-E)
GET  /sessions/{id}   PUT /sessions/{id}   DELETE /sessions/{id}
GET  /students/{studentId}/active-session   (aktif seans + IsStale takılı bayrağı — Ö-E)
GET  /students/{studentId}/sessions?from=&to=&subject=   /weekly-summary?weekStart=
POST /test-results   GET /test-results/{id}   PUT /test-results/{id}   DELETE /test-results/{id}
GET  /students/{studentId}/test-results?subject=&topic=&from=&to=   /net-trend?subject=&topic=
POST /students/{studentId}/mock-exams   (çok dersli deneme; net ExamPenalty böleniyle)
GET  /students/{studentId}/goals   PUT /students/{studentId}/goals
GET  /students/{studentId}/streak   /achievements   /dashboard
GET  /students/{studentId}/sharing   PUT /students/{studentId}/sharing
GET  /students/{studentId}/subjects   POST /students/{studentId}/subjects
PUT  /subjects/{subjectId}   DELETE /subjects/{subjectId}
POST /subjects/{subjectId}/topics   PUT /topics/{topicId}   DELETE /topics/{topicId}
GET  /students/{studentId}/notes   POST /students/{studentId}/notes
PUT  /notes/{noteId}   DELETE /notes/{noteId}
```
### ProgressTracking — `/api/progress-tracking`  (🟡 çalışır çekirdek)
```
GET  /status   (açık uç — modül durumu)
GET  /students/{studentId}/mastery?subject=   /weak-spots   /strengths   /overview
GET  /students/{studentId}/topic-goals?status=   POST /students/{studentId}/topic-goals
POST /topic-goals/{goalId}/cancel
[consume] Study.StudySessionCompletedDomainEvent / Study.TestResultRecordedDomainEvent (idempotent (ortak inbox_messages))
```
### Settings (M15 — ayar oku/yaz + gizlilik paylaşımı)
```
GET /api/settings/status
GET /api/settings/users/{userId}                 → kullanıcı ayarları (kayıt yoksa varsayılan, 200)
PUT /api/settings/users/{userId}                 → bildirim + gizlilik seviyesi + oturum politikası (upsert)
PUT /api/settings/users/{userId}/study-sharing   → öğrenci çalışma verisi öğretmen/veli paylaşım tercihi (Veli V-B)
[expose] IStudentPrivacyDirectory (Shared.Contracts) — Parents dashboard gizlilik filtresi bunu tüketir (Level dahil, M15-4)
[expose] IUserNotificationPreferences (Shared.Contracts) — Notifications gönderim turu tercih kapısı (M11-5)
[expose] ISessionPolicyDirectory (Shared.Contracts) — Identity girişte "diğer oturumları sonlandır" (M15-3)
```
### İskelet modüller (sadece durum endpoint'i)
```
GET /api/matching/status
/api/reviews/status   /api/reporting/status
```

---

## 5. Bu Doküman Setinin Kullanımı

- **Modül tarafı:** ilgili `mNN_*.md` → "Eksikler/Yapılacaklar" → domain + API + mobil katmanları tamamla.
- **Rol tarafı:** [`../roles/`](../roles/00_roller_genel_bakis.md) → rol yeteneği/akışı.
- **Durum güncelleme (KALICI KURAL):** Bir özellik tamamlanınca ilgili `mNN_*.md`, bu tablodaki durum ve [`../INDEX.md`](../INDEX.md) güncellenir — kullanıcı söylemese de (bkz. kökteki `CLAUDE.md`).

---

*Modüller Genel Bakış / İndeks | Güncelleme: 2026-09-04 (P06 Task 5 / M07-1 + M07-2: Payments'a **yeni uç eklenmedi**, iki arka plan akışı eklendi — saatlik `PaymentOverdueScanner` (kalıcı `Overdue` + `PaymentBecameOverdueDomainEvent`) ve `LessonSessionCompletedPaymentHandler` [consume] (tamamlanan ücretli dersten `LessonFee` kaydı); Notifications'a `PaymentOverdueNotificationHandler` [consume]; `IStudentDirectory` sözleşmesi `GetAgreedLessonRateAsync` ile, `LessonSessionDetails` `IsChargeable` ile büyüdü). Önceki not — 2026-09-04 (P06/M05-2: LessonSessions envanterine `from-schedule` ucu + yeni modüller-arası sözleşme `ILessonScheduleDirectory` (Scheduling uygular). P06/M05-1: LessonSessions envanterine baslat/iptal 2 ucu. P06/M02-3 + M02-4: Teachers envanterine filtreli arama `GET /profiles` + pasifleştirme/aktifleştirme 2 ucu). Önceki not — 2026-09-03 (P06/M02-1: Teachers envanterine Admin doğrulama ucu. P05: Settings envanterine ayar oku/yaz 2 ucu + 3 modüller-arası sözleşme; M15 durumu 🟡 → 🟢. P04/M06-1: Assignments envanterine ders kaynağı 4 ucu. P04/M02-2: Teachers envanterine profil fotoğrafı 2 ucu; P04/C-02: ödev teslimi ortak `IFileStorage`'a taşındı. P03/M11-3/M11-4: Notifications envanterine in-app bildirim merkezi 3 ucu + `NotificationDispatcher` push turu; M11 durumu 🟡 → 🟢. P03/M11-2: `POST`/`DELETE /device-tokens`). Önceki not — 2026-09-02 (P02/M01-1: Identity envanterine `GET /me (auth)` eklendi). Önceki not — 2026-09-02 (F-07: §3 mobil eşlemesi — app artık üç rolü de kapsıyor; `notifications` + `progress` feature satırları eklendi). Önceki not — 2026-08-26 (Y4 kapatıldı: tüm `[consume]` handler notları "idempotent (ortak inbox_messages)" olarak birleştirildi; Parents'ın 4 read-model projeksiyonu için `[consume]` satırı eklendi; §4 başına ortak `IdempotentIntegrationEventHandler` notu). Önceki not — 2026-08-19 (Geçiş 2 kod-senkron m07-m12: Payments envanterine `/records/search` + ProgressTracking `/status` eklendi; endpoint sayıları doğrulandı — Payments 11, Study 38, Parents 11, ProgressTracking 8, Notifications 2, Matching 1). Önceki not — 2026-07-19 (Ö-E: M08 sayaç güvenilirliği — `sessions/{id}/recover` takılı seans kurtarma + `students/{id}/active-session` (isStale 6 saat) + pause/complete opsiyonel `clientEffectiveMinutes` istemci-otoriter süre · Ö-C: M03 `POST /links/claim` davet kodu ile öğrenci claim + tam profil birleştirme merge → modüller-arası `StudentId` yeniden atama · Ö-B: M08 `POST /students/{id}/mock-exams` çok dersli deneme + sınav tipine göre net böleni (ExamPenalty) + M03 `TargetExam` · Ö-A2: M08 seans/test düzenle-sil endpoint'leri + konu rollup yeniden türetme) · 2026-07-18 (Dilim A takvim çekirdeği: M04 MeetingUrl/erteleme/iptal nedeni+sil/tatil bloğu/occurrence istisnaları; M05 oturum IsChargeable · Dilim B: M06 not görünürlüğü + ödev onay/geri gönder · Dilim D: M02 çoklu branş + sertifika)*
