# 🗂️ Modüller — Genel Bakış ve Durum (Modül İndeksi)

> Bu klasör (`doc/modules/`), her **backend modülünün** saf teknik tasarım dokümanını içerir
> (domain modeli, API sözleşmesi, iş kuralları, durum, eksikler). **Rol perspektifi** (öğretmen/öğrenci/veli
> yetenekleri ve akışları) artık ayrı klasördedir → [`../roles/`](../roles/00_roller_genel_bakis.md).
>
> İlgili üst dokümanlar:
> - [`../INDEX.md`](../INDEX.md) — tüm doküman haritası (buradan başla)
> - [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) — Ürün gereksinimleri (v2.1)
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
| M04 | Takvim & Planlama | [`m04_scheduling.md`](m04_scheduling.md) | `Scheduling` | `/api/scheduling` | 🟢 (link+tatil+erteleme+occurrence, 2026-07-18) |
| M05 | Ders Oturumu | [`m05_lesson_sessions.md`](m05_lesson_sessions.md) | `LessonSessions` | `/api/lesson-sessions` | 🟢 |
| M06 | Not, Ödev & Kaynak | [`m06_assignments.md`](m06_assignments.md) | `Assignments` | `/api/assignments` | 🟢 (Dilim B: not görünürlüğü + ödev onay/geri gönder) |
| M07 | Ödeme Takibi | [`m07_payments.md`](m07_payments.md) | `Payments` | `/api/payments` | 🟢 (veli paylaşımı ⚠️) |
| M08 | Bireysel Çalışma | [`m08_study.md`](m08_study.md) | `Study` | `/api/study` | 🟢 (mobil dahil) |
| M09 | Veli Paneli | [`m09_parents.md`](m09_parents.md) | `Parents` | `/api/parents` | 🟢 |
| M10 | Gelişim Takibi | [`m10_progress_tracking.md`](m10_progress_tracking.md) | `ProgressTracking` | `/api/progress-tracking` | 🟡 Çalışır çekirdek (konu hâkimiyeti + hedef + mobil) |
| M11 | Bildirim | [`m11_notifications.md`](m11_notifications.md) | `Notifications` | `/api/notifications` | 🟡 (gerçek push yok) |
| M12 | Eşleştirme & İlan | [`m12_matching.md`](m12_matching.md) | `Matching` | `/api/matching` | 🔴 İskelet |
| M13 | Puanlama & Yorum | [`m13_reviews.md`](m13_reviews.md) | `Reviews` | `/api/reviews` | 🔴 İskelet |
| M14 | Raporlama & Analiz | [`m14_reporting.md`](m14_reporting.md) | `Reporting` | `/api/reporting` | 🔴 İskelet |
| M15 | Ayarlar & Güvenlik | [`m15_settings.md`](m15_settings.md) | `Settings` | `/api/settings` | 🟡 (domain var, endpoint yok) |
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

> Bir kullanıcı rolü birden çok backend modülünü kullanır (bkz. [`../roles/`](../roles/00_roller_genel_bakis.md)). Mevcut mobil app **öğretmen odaklı**.

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
| ProgressTracking/Matching/Reviews/Reporting/Messaging/Membership/Feedback | _(yok)_ | planlanan (yeni özellik ekranları) |

---

## 4. Mevcut API Endpoint Envanteri (Koddan Çıkarıldı)

### Identity — `/api/identity`
```
POST /register   POST /login   POST /refresh
POST /password-reset/request   POST /password-reset/confirm
POST /email-verification/request   POST /email-verification/confirm
POST /logout (auth)   GET /users/{userId} (auth)
POST /users/{userId}/roles (auth, yalnız Admin — rol atama)
```
> Not (K1): `POST /register` yalnız `Teacher/Student/Parent` kabul eder; `Admin` reddedilir. Yükseltilmiş rol yalnız `POST /users/{id}/roles` ile atanır.
### Teachers — `/api/teachers`
```
POST /profiles   PUT /profiles/{userId}   GET /profiles/{userId}
```
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
```
### LessonSessions — `/api/lesson-sessions`
```
POST /            (oturum oluştur)        GET / ?filtre   (liste)
POST /{lessonSessionId}/complete          GET /{lessonSessionId}   (complete: IsChargeable, B-08 2026-07-18)
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
```
### Payments — `/api/payments`
```
POST /records   PUT /records/{paymentRecordId}   GET /records/{paymentRecordId}
GET /teachers/{teacherUserId}/records   /summary   /records/filter
```
### Notifications — `/api/notifications`
```
GET /teachers/{teacherUserId}/lesson-reminders?activeOnly=
```
### Parents — `/api/parents`  (tümü auth "AuthenticatedUser")
```
POST /profiles   GET /profiles/{userId}   PUT /{parentUserId}/notification-preferences
POST /children/link   POST /children/{linkId}/approve   /reject   /revoke   (onay: öğrenci/öğretmen/Admin)
GET  /{parentUserId}/children
GET  /{parentUserId}/children/{studentId}/dashboard   (yalnız Approved bağda; değilse 403)
```
### Study — `/api/study`  (tümü auth "AuthenticatedUser"; öğrenci kendi StudentId'sine erişir)
```
POST /sessions/start   /sessions/manual
POST /sessions/{id}/pause   /resume   /complete   /discard
GET  /sessions/{id}
GET  /students/{studentId}/sessions?from=&to=&subject=   /weekly-summary?weekStart=
POST /test-results   GET /test-results/{id}
GET  /students/{studentId}/test-results?subject=&topic=&from=&to=   /net-trend?subject=&topic=
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
GET  /students/{studentId}/mastery?subject=   /weak-spots   /strengths   /overview
GET  /students/{studentId}/topic-goals?status=   POST /students/{studentId}/topic-goals
POST /topic-goals/{goalId}/cancel
[consume] Study.StudySessionCompletedDomainEvent / Study.TestResultRecordedDomainEvent (idempotent)
```
### İskelet modüller (sadece durum endpoint'i)
```
GET /api/matching/status
/api/reviews/status   /api/reporting/status   /api/settings/status
```

---

## 5. Bu Doküman Setinin Kullanımı

- **Modül tarafı:** ilgili `mNN_*.md` → "Eksikler/Yapılacaklar" → domain + API + mobil katmanları tamamla.
- **Rol tarafı:** [`../roles/`](../roles/00_roller_genel_bakis.md) → rol yeteneği/akışı.
- **Durum güncelleme (KALICI KURAL):** Bir özellik tamamlanınca ilgili `mNN_*.md`, bu tablodaki durum ve [`../INDEX.md`](../INDEX.md) güncellenir — kullanıcı söylemese de (bkz. kökteki `CLAUDE.md`).

---

*Modüller Genel Bakış / İndeks | Güncelleme: 2026-07-18 (Dilim A takvim çekirdeği: M04 MeetingUrl/erteleme/iptal nedeni+sil/tatil bloğu/occurrence istisnaları; M05 oturum IsChargeable · Dilim B: M06 not görünürlüğü + ödev onay/geri gönder · Dilim D: M02 çoklu branş + sertifika)*
