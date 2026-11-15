---
title: "Modüller — Genel Bakış ve Durum (İndeks)"
summary: "doc/modules/ klasörünün indeks dokümanı: M01–M18 modül listesi, backend eşlemesi ve durum tablosu"
tags: [modul, indeks, genel-bakis]
authority: code
code_refs:
  - src/Modules/*/API/*Module.cs
updated: 2026-11-15
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
> **Güncelleme:** 2026-11-15

---

**P17 T7 / M14:** Öğretmen pano dersleri `(TeacherUserId, LessonScheduleId, OccurrenceStartUtc)` anahtarlıdır. `ITeacherDashboardLessonSource` güncel Scheduling oluşumlarını, iptal/tamamlanmış dahil, modül sınırını koruyarak sağlar. Yerel dün → +14 gün penceresi olayla ve kiralı günlük işçiyle yenilenir; iptal tombstone tutulur, pencere dışı yalnız ders satırları budanır. `dashboard` yerel gün sayar; API yanıtı değişmedi.


**P17 T8 (2026-11-15):** Haftalık ders yaşam döngüsü gerçek PostgreSQL + outbox ile zincirlenir. Single iptal aylık raporda oluşum anahtarıyla sayılır (`MonthlyCancellationOccurrences`); iki tamamlama + bir geç iptal toplam 300 TRY borç üretir. Mikro-saniyeli seri sonlandırma önceki oluşumu korur. Yeni HTTP ucu yoktur; mobil üç haftanın orijinal anahtarlarını ve iptal kapsamını korur.


**P18 T1 / K-22:** M03, M04, M06, M07, M08, M09, M10 ve M17’de toplam 10 kritik tip `IVersioned`/PostgreSQL `xmin` taşır; ortak middleware çakışmayı `409 shared.concurrency_conflict` döner. Sekiz `AddVersionToken` migration’ı yalnız model metadatasıdır. Ödeme niyet komutları T2’de; yeni endpoint yoktur.

**P18 T2 (2026-11-15):** M07’ye `POST /records/{id}/collections`, `/refunds`, `/cancel` eklendi (14 uç); PUT yalnız ayrıntıları günceller. Net tahsilat/durum hareketlerden türetilir, eski istemci toplam göndererek tahsilatı ezemez. `PaymentTransactions` göçü eski tahsilatı tek `Migrated` harekete taşır; `IPaymentTransactionDirectory` ve `reporting.payment-collections` yeniden kurucusu aylık raporu besler. Mobil geçiş T4’tedir.


## 1. Modül İndeksi (M01–M18)

| M | Modül | Dosya | Backend (`src/Modules`) | Route | Durum |
|---|-------|-------|-------------------------|-------|-------|
| M01 | Kullanıcı & Rol | [`m01_identity.md`](m01_identity.md) | `Identity` | `/api/identity` | 🟢 |
| M02 | Öğretmen Profili | [`m02_teachers.md`](m02_teachers.md) | `Teachers` | `/api/teachers` | 🟢 |
| M03 | Öğrenci Profili | [`m03_students.md`](m03_students.md) | `Students` | `/api/students` | 🟢 / 🟡 (self-register) |
| M04 | Takvim & Planlama | [`m04_scheduling.md`](m04_scheduling.md) | `Scheduling` | `/api/scheduling` | 🟢 (link+tatil+erteleme+occurrence+öğrenci erteleme talebi, 2026-07-18) |
| M05 | Ders Oturumu | [`m05_lesson_sessions.md`](m05_lesson_sessions.md) | `LessonSessions` | `/api/lesson-sessions` | 🟢 |
| M06 | Not, Ödev & Kaynak | [`m06_assignments.md`](m06_assignments.md) | `Assignments` | `/api/assignments` | 🟢 (kaynak paylaşımı + onay/geri gönder + puanlama + son teslim uyarısı) |
| M07 | Ödeme Takibi | [`m07_payments.md`](m07_payments.md) | `Payments` | `/api/payments` | 🟢 (veli paylaşımı ⚠️) |
| M08 | Bireysel Çalışma | [`m08_study.md`](m08_study.md) | `Study` | `/api/study` | 🟢 (mobil dahil) |
| M09 | Veli Paneli | [`m09_parents.md`](m09_parents.md) | `Parents` | `/api/parents` | 🟢 |
| M10 | Gelişim Takibi | [`m10_progress_tracking.md`](m10_progress_tracking.md) | `ProgressTracking` | `/api/progress-tracking` | 🟢 (konu hâkimiyeti + hedef + snapshot & trend + öğretmen/veli görünümü + ders katılımı beslemesi + mobil çizgi grafiği; **P08 kapanışı 2026-09-04**) |
| M11 | Bildirim | [`m11_notifications.md`](m11_notifications.md) | `Notifications` | `/api/notifications` | 🟢 (cihaz token + FCM push + in-app bildirim merkezi/rozet — P03 2026-09-03) |
| M12 | Eşleştirme & İlan | [`m12_matching.md`](m12_matching.md) | `Matching` | `/api/matching` | 🔴 İskelet |
| M13 | Puanlama & Yorum | [`m13_reviews.md`](m13_reviews.md) | `Reviews` | `/api/reviews` | 🔴 İskelet |
| M14 | Raporlama & Analiz | [`m14_reporting.md`](m14_reporting.md) | `Reporting` | `/api/reporting` | 🟢 (panel + aylık özet + çalışma/performans analizi + boş zaman + premium kapılı PDF raporu + mobil ekranlar; **P08 kapanışı 2026-09-04**) · ⚠️ geçmiş dönem beslemesi `IProjectionRebuilder` ile gelecek (P12), premium kapısı geçici (P09) |
| M15 | Ayarlar & Güvenlik | [`m15_settings.md`](m15_settings.md) | `Settings` | `/api/settings` | 🟢 (ayar oku/yaz + çalışma paylaşımı + 3 sözleşme: gizlilik/bildirim tercihi/oturum politikası; rol bazlı profil düzenleme bekliyor) |
| M16 | Mesajlaşma | [`m16_messaging.md`](m16_messaging.md) | `Messaging` | `/api/messaging` | 🟡 Zincir uçtan uca çalışıyor, **gerçek Postgres+Redis'te doğrulandı** (P10 Task 1-5: domain + 8 uç + ilişki kapısı + keyset sayfalama + kullanıcı başına 30/dk kota + bildirim + mobil gelen kutusu/sohbet + `RealMessagingChainTests`); **bilerek 🟡** — karşı tarafın adı sunucudan gelmiyor, veli sohbet başlatamıyor, şikayet (M18/P12), cihaza push (Q8) ve gerçek zamanlı teslimat (v2) eksik |
| M17 | Üyelik & Para Kazanma | [`m17_membership.md`](m17_membership.md) | `Membership` | `/api/membership` | 🟡 Domain + entitlement + plan/kampanya + **mobil üyelik/paywall**, zincir **gerçek Postgres'te uçtan uca doğrulanmış** (P09 Task 1-3, 6 ve 7 kısmi kapanış: `SubscriptionPlan`/`UserSubscription`/`ReferralCode` + `membership` şeması + `IEntitlementDirectory` + `GET /me` + plan kataloğu/deneme/referans uçları + `features/membership` & `PremiumLock` + `RealMembershipEntitlementTests`; **ödeme (Task 4/Q1) ve reklam (Task 5/Q3) hâlâ yok → 🟢 değil**) |
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
| ProgressTracking | `progress` | `ProgressOverviewPage` (gelişim özeti + snapshot çizgi grafiği, `ProgressCubit`) — backend M10 🟢 |
| Reporting | `reporting` | `TeacherReportsPage` (`/teacher/reports`) + `StudentAnalysisPage` (`/student/reports`) + grafik widget'ları (`WeeklyStudyBarChart`, `SubjectNetChart`) — backend M14 🟢 |
| Membership | `membership` | `MembershipPage` (`/membership`) + `PaywallPage` (`/membership/paywall`) + kökteki `MembershipCubit` + ortak `PremiumLock`/`PremiumLockCard` — backend M17 🟡 (satın alma yok) |
| Messaging | `messaging` | `MessagingInboxPage` (`/messages`) + `ChatPage` (`/messages/:conversationId`) + `ConversationsCubit`/`ChatCubit` + `messaging_widgets.dart` balonları — backend M16 🟡 (şikayet/ad yok) |
| Matching/Reviews/Feedback | _(yok)_ | planlanan (yeni özellik ekranları) |

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
POST /account/deletion-request   DELETE /account/deletion-request   GET /account/deletion-request
  (auth; KVKK hesap silme — 30 gün bekleme, iptal edilebilir; E-07 2026-09-05)
GET /sessions   POST /sessions/{sessionId}/revoke   POST /sessions/revoke-all
  (auth; cihaz oturumları — yalnız aktifler, "bu cihaz" jetondaki `sid` ile; M01-4 2026-09-05)
[expose] IPersonalDataAnonymizer tüketicisi (Shared.Contracts) — süresi dolan talepte 11 modülü anonimleştirir
```
> Not (K1): `POST /register` yalnız `Teacher/Student/Parent` kabul eder; `Admin` reddedilir. Yükseltilmiş rol yalnız `POST /users/{id}/roles` ile atanır.
> Not (E-07): Silme talebi anında **hiçbir veri değişmez**; `AccountDeletionService` 6 saatte bir süresi dolanları uygular. Ticari kayıt (ödeme/ders) silinmez, kimliksizleşir. Ayrıntı: [`../architecture/kvkk.md`](../architecture/kvkk.md).
> **P17 T5:** Teachers iptal politikası + Scheduling kapsam/silme olayları → LessonSessions/Payments/Notifications temizliği; LateCancellation=4 oluşum borcu; tek Pending talep ve atomik kabul; gelecek tamamlama reddi. Ayrıntılar m02/m04/m05/m07/m11.

### Teachers — `/api/teachers`
```
POST /profiles   PUT /profiles/{userId}   GET /profiles/{userId}
POST /profiles/{userId}/photo   GET /profiles/{userId}/photo   (profil fotografi; self/Admin yukler, kimlik dogrulanmis herkes okur; M02-2 2026-09-03)
PUT /profiles/{userId}/cancellation-policy  (yalniz sahibi Teacher; noticeHours/chargePercent; P17 T5)
PUT /profiles/{userId}/verification   (dogrulama rozeti; YALNIZ Admin, TeacherVerifiedDomainEvent; M02-1 2026-09-03)
GET /profiles?city=&district=&subject=&lessonFormat=&minRate=&maxRate=&onlyVerified=&skip=&take=   (filtreli arama; auth, yalniz aktif profiller, take<=50; M02-3 2026-09-04)
POST /profiles/{userId}/deactivate   POST /profiles/{userId}/activate   (arama gorunurlugu; sahibi veya Admin; M02-4 2026-09-04)
[expose] ITeacherAvailabilityDirectory (Shared.Contracts) — Reporting boş zaman analizi (M14-5)
[expose] ITeacherDirectory (Shared.Contracts) — Reporting PDF rapor başlığında öğretmen adı (M14-4)
```
> Not (M02-3): `GET /profiles` M12 (eşleştirme) için **geçici okuma yüzeyi**; P11'de `TeacherSearchProjection` devreye girince handler projeksiyonu okuyacak, URL + yanıt sözleşmesi korunacak.
> Not (Dilim D): profil upsert artık **çoklu branş** (`Subjects`) ve **sertifika/deneyim** (`Certificates`) listelerini de taşır; birincil `Subject` korunur. Tablolar: `teacher_subjects`, `teacher_certificates`.
### API.Host BFF — (compose root, modül sınırları dışı)
```
GET /api/teachers/profiles/{teacherUserId}/dashboard-summary  (auth)
    → Scheduling + Assignments + Payments paralel sorgula; bugünkü dersler, bekleyen ödevler, geciken ödemeleri tek yanıtta döndür
      (P16 T5: "bugün" Türkiye günü; yeni alan overduePaymentTotals [{currency, count, total}] — eski overduePaymentsCurrency/Total listenin ilk satırı)
```
### Students — `/api/students`
```
POST /profiles   PUT /profiles/{studentId}   GET /profiles/{studentId}
GET /profiles/by-user/{userId}   GET /profiles/by-teacher/{teacherUserId}?includeArchived=   (liste link üzerinden)
POST /teachers/{teacherUserId}/students/{studentId}/archive|unarchive   PUT .../rate   (arşiv + öğrenci bazlı ücret, B-04/B-07 2026-07-18)
POST /teachers/{teacherUserId}/students/{studentId}/invite   POST /links/{linkId}/accept|reject   (çoklu öğretmen davet/kabul, B-06 2026-07-18; P15 T3 / N-22: accept/reject gövdede inviteCode ZORUNLU, yalnız InviteSent bağda, hedefe ve süresinde; Linked bağa yeniden davet 409)
POST /links/claim   (8 karakterli davet koduyla profili devral + mevcut profil varsa birleştir/merge, Ö-C 2026-07-19; P15 T3: link kabulüyle aynı çekirdek, birleşmede öğretmen bağları + bekleyen veli davetleri aynı transaction'da kanonik profile)
[emit] StudentProfileLinkedToUserDomainEvent (P15 T3 / N-23) — davetle hesaba bağlanan profil; Parents tüketir (KnownStudent.UserId, P15 T4)
[consume] Parents.ParentChildLinkEnded → StudentProfile.UnlinkParent (ParentUserId o veliyse temizlenir; P15 T4 / N-26, idempotent)
POST /profiles/{studentId}/parent-invite   (öğretmen veli davet kodu üretir → ParentInviteResponse, Veli V-D 2026-07-19)
POST /profiles/{studentId}/link-parent   (kayıtlı veli kullanıcısını bağlama talebi → bağ Pending; M03-1 2026-09-04; P15 T9: öğretmen için yaşayan özel ders bağı şart)
POST /profiles/{studentId}/subjects   DELETE /profiles/{studentId}/subjects/{subjectId}   (branş ekle/sil; aynı branş 409, M03-2 2026-09-04)
GET /profiles/by-parent/{parentUserId}   (velinin ONAYLI çocukları; yalnız o veli veya Admin, M03-3 2026-09-04)
[expose] IParentInviteDirectory (Shared.Contracts) — Parents claim bunu tüketir
[expose] IStudentDirectory.GetDisplayNameAsync (Shared.Contracts) — Reporting PDF rapor başlığı (M14-4)
[expose] ITeacherStudentAccess.GetScopeAsync (Shared.Contracts) — öğretmen–öğrenci erişim kapsamı None/Private/Class (K-13, P15 T2); Scheduling/Payments/LessonSessions yazma uçları + Students veli daveti ve profil okuma (P15 T3) tüketir, IsLinkedTeacherOfStudentAsync buna delege
[consume] IParentChildLinkRequestService + IParentAccessDirectory (Parents uygular) — veli bağı talebi + onaylı çocuk listesi + veli profil okuma yetkisi (P15 T4 / N-26: ParentUserId yerine onaylı bağ)
```
### Scheduling — `/api/scheduling`
```
POST /lessons   PUT /lessons/{lessonId}   POST /lessons/{lessonId}/cancel
POST /lessons/{lessonId}/reschedule   DELETE /lessons/{lessonId}   (B-02/B-09, 2026-07-18)
  (P16 T4: POST /lessons, PUT /lessons/{id} ve /reschedule gövdesinde allowTimeOffOverride; çakışma oluşum genişletmeli 409 teacher_conflict, tatil bloğu 409 time_off_conflict; yazımlar öğretmen başına advisory lock altında)
  (P17 T1: /cancel ve /reschedule tekrarlı derste scope zorunlu → 400 scope_required; occurrenceStartAtUtc orijinal oluşum olmalı → 400 occurrence_not_found)
GET /lessons/{lessonId}
[consume] LessonSessions.LessonSessionCompletedDomainEvent → tekil plan Completed / tekrarlı oluşum Completed istisnası (P17 T4, K-21; inbox + öğretmen advisory lock, tekrar olay üretmez)
  (Scheduling /complete kaldırıldı; tamamlanmış oluşumu içeren iptal/erteleme/seri güncelleme 409 occurrence_completed)
GET /teachers/{teacherUserId}/lessons?startAtUtc=&endAtUtc=   (tarih aralığı filtreli; M04-1: tekrarlar occurrence olarak açılır, OccurrenceStartAtUtc = RECURRENCE-ID)
POST /teachers/{teacherUserId}/time-off   GET /teachers/{teacherUserId}/time-off?startAtUtc=&endAtUtc=   DELETE /teachers/{teacherUserId}/time-off/{timeOffId}   (tatil bloğu, B-01 2026-07-18; P16 T4: conflictingLessons oluşum döner, bloğa taşan ders dahil)
GET /students/{studentId}/lessons?startAtUtc=&endAtUtc=       (öğrenci kendi dersleri, IDOR korumalı)
GET /students/{studentId}/calendar?startAtUtc=&endAtUtc=      (birleşik takvim: öğretmen dersleri + kendi programı, tekrarlar + occurrence istisnaları genişletilmiş; ConflictsWithLesson bayrağı)
POST /students/{studentId}/study-entries   PUT /study-entries/{entryId}   DELETE /study-entries/{entryId}   (öğrenci kişisel programı)
POST /students/{studentId}/lesson-requests   GET /teachers/{teacherUserId}/lesson-requests?onlyPending=   POST /lesson-requests/{requestId}/accept   POST /lesson-requests/{requestId}/reject   (öğrenci ders erteleme talebi, Ö-F 2026-07-18; P17 T1: gövde + yanıt occurrenceStartAtUtc, kabul yalnız o oluşumu taşır, eski tekrarlı talep 409 request_occurrence_unknown)
```
### LessonSessions — `/api/lesson-sessions`
```
POST /            (oturum oluştur)        GET / ?filtre   (liste)
POST /{lessonSessionId}/complete
POST /{lessonSessionId}/start   POST /{lessonSessionId}/cancel   (yasam dongusu gecisleri; sahibi ogretmen/Admin, gecersiz gecis 409; M05-1 2026-09-04)
POST /from-schedule   {lessonScheduleId, occurrenceStartAtUtc?}  (P17 T3: oluşum başına idempotent; tekrarlıda anahtar zorunlu, ertelemede orijinal anahtar)
POST /from-schedule/{lessonScheduleId}   (tekil ders uyumluluk köprüsü; tekrarlıda 400 occurrence_required)
GET /{lessonSessionId}   (öğrenci kendi profil kimlikleri; veli onaylı çocukları; liste de aynı kapsamda)
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
POST /{assignmentId}/grade           (öğretmen puanlar 0-100; yalniz Approved durumda; M06-4 2026-09-04)
[background] AssignmentDueScanner    (saatlik; teslime 24 saat kala AssignmentDueSoon, vade gecince AssignmentMissed — odev basina bir kez; M06-3 2026-09-04)
POST /lesson-resources   GET /lesson-resources?teacherUserId=&studentId=&lessonSessionId=
GET /lesson-resources/{resourceId}/file   DELETE /lesson-resources/{resourceId}   (ders kaynagi; ogretmen ekler/arsivler, ogrenci kendisine acik olanlari gorur; M06-1 2026-09-03)
[expose] IStudentAssignmentDigestDirectory (Shared.Contracts) — Reporting PDF raporunda ödev tamamlama oranı (M14-4)
[expose] IStudentNotesDirectory (Shared.Contracts) — Parents veliye görünür öğretmen notları
```
### Payments — `/api/payments`

`POST /records/{id}/collections`, `POST /records/{id}/refunds`, `POST /records/{id}/cancel`; `PUT /records/{id}` yalnız description/expectedAmount/dueDateUtc/notes.
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
[consume] Scheduling.Scheduled/Rescheduled/OccurrenceRescheduled/Cancelled/CancellationApplied/OccurrenceCancelled/Deleted/SeriesSplit → güncel kaynaktan hatırlatma uzlaştırması (inbox + ortak kilit, P17 T6)
[hosted] LessonReminderPlanner (1 dk, kiralı, ILessonScheduleReader ile gelecek 7 gün; üçlü oluşum/alıcı anahtarı; P17 T6)
[hosted] NotificationDispatcher (30 sn tur: vakti gelen LessonReminder + push edilmemiş UserNotification → IPushSender/FCM; P03 2026-09-03)
```
### Parents — `/api/parents`  (tümü auth "AuthenticatedUser")
```
POST /profiles   GET /profiles/{userId}   PUT /{parentUserId}/notification-preferences
POST /children/link   POST /children/{linkId}/approve   /reject   /revoke   (onay: öğrenci/Admin; P15 T4: approve yalnız Pending → aksi 409 parents.invalid_link_transition, eşzamanlı ikinci birincil 409 parents.primary_exists)
POST /children/claim-invite   (öğretmen davet kodunu girerek çocuğa bağlan → Approved, Veli V-D 2026-07-19)
PUT /{parentUserId}/membership-tier   (Admin: veli Free/Premium, Veli V-E 2026-07-19)
[expose] IParentNotificationDirectory (Shared.Contracts) — Notifications veli bildirim motoru bunu tüketir
[consume] LessonSessions.LessonSessionCreated|Completed / Assignments.AssignmentCreated|Completed / Payments.PaymentRecordCreated|Updated / Students.StudentProfileCreated → 4 read-model projeksiyonu (dashboard özeti, idempotent (ortak inbox_messages))
[consume] Students.StudentProfilesMerged → veli bağları tekilleşip kanonik kimliğe, known_students kaynağı silinir, panel özeti toplanır (P15 T3 / N-28, idempotent)
[consume] Students.StudentProfileLinkedToUser → known_students.UserId (davetle bağlanan öğrenci onay verebilir, gizlilik ayarı okunur; P15 T4 / N-27, idempotent)
[emit] ParentChildLinkEndedDomainEvent (P15 T4 / N-26) — yaşayan bağ reddedildi/iptal edildi; Students ParentUserId'yi temizler (eski Rejected/Revoked olayları kaldırıldı)
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
### ProgressTracking — `/api/progress-tracking`  (🟢)
```
GET  /status   (açık uç — modül durumu)
GET  /students/{studentId}/mastery?subject=   /weak-spots   /strengths   /overview
GET  /students/{studentId}/snapshots?subject=&period=&from=&to=   → haftalık/aylık zaman serisi + trend (M10-1)
GET  /teachers/{teacherUserId}/students/{studentId}/overview   → öğretmen gelişim görünümü (gizlilik filtreli, M10-2)
GET  /parents/{parentUserId}/children/{studentId}/overview     → veli gelişim görünümü (gizlilik filtreli, M10-2)
GET  /students/{studentId}/topic-goals?status=   POST /students/{studentId}/topic-goals
POST /topic-goals/{goalId}/cancel
[consume] Study.StudySessionCompletedDomainEvent / Study.TestResultRecordedDomainEvent (idempotent (ortak inbox_messages))
[consume] LessonSessions.LessonSessionCompletedDomainEvent → konuya ders katılımı katkısı (yalnız Attended/Late; LessonSessionId makbuzu + inbox ile idempotent, P17 T3)
[consume] Students.StudentProfilesMerged → hedefler taşınır; hâkimiyet/fotoğraf çakışmayan satır taşınır, çakışanda kanonik kazanır (P15 T3 / N-28, idempotent)
[scheduler] ProgressSnapshotService — günde bir tur, kapanmış son hafta/ay için ProgressSnapshot yazar (idempotent + unique index)
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
### M14 Reporting (`/api/reporting`) — 🟢
```
GET /teachers/{teacherUserId}/dashboard              (auth: öğretmenin kendisi veya Admin)
GET /teachers/{teacherUserId}/monthly?year=&month=   → aylık ders/tahsilat özeti (M14-1)
GET /teachers/{teacherUserId}/free-slots?from=&to=   → boş zaman analizi (M14-5)
GET /students/{studentId}/study-analysis?from=&to=   → haftalık çalışma analizi (M14-2, gizlilik filtreli)
GET /students/{studentId}/performance?subject=&from=&to= → ders bazlı aylık performans (M14-3, gizlilik filtreli)
POST /students/{studentId}/report                    → PDF gelişim raporu üret (M14-4; premium → 402, gizlilik kapalı → 403)
GET /reports/{reportId}/file                         → yetkili PDF indirme (M14-4; öğrenci/bağlı öğretmen/onaylı veli/Admin)
GET /status
[consume] Scheduling LessonScheduled/Rescheduled/Cancelled · Assignments Created/Completed/Approved ·
          Payments BecameOverdue/RecordUpdated · Students TeacherStudentLinkAccepted
          → teacher_dashboard_projections (read-model, idempotent — ortak inbox_messages)
[consume] LessonSessions LessonSessionCompleted · Scheduling LessonScheduleCancelled ·
          Payments PaymentRecordCreated/PaymentTransactionRecordedIntegrationEvent  → teacher_monthly_projections (M14-1)
[consume] Study StudySessionCompleted/TestResultRecorded      → student_study_projections (M14-2)
[consume] Study TestResultRecorded · ProgressTracking TopicMasteryChanged
                                                              → student_performance_projections (M14-3)
[use] IStudentDirectory.GetDisplayNameAsync · ITeacherDirectory · IStudentAssignmentDigestDirectory ·
      IEntitlementDirectory (Shared.Contracts, Membership uygular) — premium kapıları:
      PDF rapor (M14-4), Study geçmiş/hedef (Ö-D), öğretmen öğrenci limiti (M03-4), veli bildirimi (M11)
```

### M16 Messaging (`/api/messaging`) — 🟡
```
GET    /status
POST   /conversations                          (auth) → sohbeti aç; varsa mevcudu dön (get-or-create)
GET    /conversations?skip=&take=              (auth) → gelen kutusu + okunmamış sayısı + isReadOnly
GET    /conversations/{id}/messages?beforeUtc=&take=
                                               (auth) → mesaj geçmişi (keyset, yeniden eskiye; ilişki bitse de okunur)
POST   /conversations/{id}/messages            (auth) → mesaj gönder · dakikada 30 (kullanıcı başına)
                                                        · ilişki bittiyse 403 messaging.relationship_ended (P15 T5 / K-28)
POST   /conversations/{id}/read                (auth) → gelen mesajları okundu işaretle (204)
POST   /conversations/{id}/block · /unblock    (auth) → engelle / kendi engelini kaldır
DELETE /messages/{messageId}                   (auth) → yumuşak sil (gönderen veya Admin, 204)
```
[use] ITeacherStudentAccess.GetScopeAsync (== Private) + IStudentDirectory.GetOwnerUserIdAsync ·
      IParentAccessDirectory.IsApprovedParentOfStudentAsync — ilişki kapısı: açılışta `messaging.relationship_required`,
      her gönderimde `messaging.relationship_ended` (P15 T5); Messaging ilişki bilgisini kendi tablolarında **tutmaz**
[consume] Students.StudentProfilesMerged → sohbetler kanonik profile taşınır; kanonikte aynı üçlü varsa mesajlar
      kanonik sohbete katılır, kopya silinir (P15 T5 / #189, idempotent)
[emit] MessageSentDomainEvent → (Outbox) → Notifications MessageSentNotificationHandler [consume]
       → UserNotification(Kind=MessageReceived, Route="/messages/{conversationId}")
> P10 Task 1-3 kodda. Şikayet ucu (M18/P12), sohbet susturma ve mesaj düzenleme **yok**; mobil P10 Task 4'te.

### M17 Membership (`/api/membership`) — 🟡
```
GET  /status
GET  /me                              (auth)   → { tier, effectiveUntilUtc, planCode, status, entitlement }
GET  /plans?role=                     (ANONİM) → satıştaki planlar (paywall giriş yapmadan okur)
POST /subscriptions/start-trial       (auth)   → { planCode } · kullanıcı başına BİR KEZ (M17-3)
POST /subscriptions/cancel            (auth)   → iptal; erişim dönem sonuna kadar sürer
GET  /referral-code                   (auth)   → kod (yoksa üretir; GET ama komut gönderir)
POST /referral/redeem                 (auth)   → { code } · HER İKİ tarafa 1 ay premium
POST /plans                           (Admin)  → yeni plan
PUT  /plans/{planId}                  (Admin)  → fiyat / deneme / satış durumu
[impl] IEntitlementDirectory (Shared.Contracts) — üyelik haklarının TEK kaynağı (K-09)
[seed] MembershipPlanSeeder — idempotent, YALNIZ Development; fiyatlar hipotez (Q2 onaylanmadı)
[mobil] /membership + /membership/paywall (P09 Task 6) — checkout ucu YOK, satın alma düğmesi pasif
```

### İskelet modüller (sadece durum endpoint'i)
```
GET /api/matching/status   /api/reviews/status
```

---

## 5. Bu Doküman Setinin Kullanımı

- **Modül tarafı:** ilgili `mNN_*.md` → "Eksikler/Yapılacaklar" → domain + API + mobil katmanları tamamla.
- **Rol tarafı:** [`../roles/`](../roles/00_roller_genel_bakis.md) → rol yeteneği/akışı.
- **Durum güncelleme (KALICI KURAL):** Bir özellik tamamlanınca ilgili `mNN_*.md`, bu tablodaki durum ve [`../INDEX.md`](../INDEX.md) güncellenir — kullanıcı söylemese de (bkz. kökteki `CLAUDE.md`).

---

*Modüller Genel Bakış / İndeks | Güncelleme: 2026-11-15 (P17 T3: gövdeli oluşum oturumu ucu, öğrenci/veli görünürlüğü, çift katılım koruması) (P17 Task 1 / N-01: **yeni uç yok**; Scheduling iptal/erteleme gövdesinde `scope` tekrarlı derste zorunlu (`400 scope_required`, `400 occurrence_not_found`), erteleme talebi `occurrenceStartAtUtc` taşır (`409 request_occurrence_unknown`); migration `AddChangeRequestOccurrence`) · 2026-11-15 (P16 Task 5 / N-11, N-43, N-50: **yeni uç yok**; BFF `dashboard-summary` yanıtına `overduePaymentTotals` (para birimi bazında), "bugün" Türkiye günü; Reporting kovaları/boş zaman şablonu/varsayılan dönemler yerel; `IStudyPlanCompletionReader` tarihleri yerel; bildirim metinleri yerel saat) · 2026-11-14 (P16 Task 4 / N-07: **yeni uç yok**; Scheduling yazma gövdelerine `allowTimeOffOverride` (create/update/reschedule), yeni hata `409 scheduling.time_off_conflict`, çakışma oluşum genişletmeli + öğretmen başına `pg_advisory_xact_lock`; tatil oluşturmanın `conflictingLessons` listesi oluşum döner; config `Scheduling:ConflictHorizonWeeks`) · 2026-11-14 (P16 Task 2 / N-04: **yeni uç yok**, yanıt sözleşmesi aynı; Scheduling'in tekrar genişletmesi serinin yerel diliminde — öğretmen listesi, birleşik takvim ve `ILessonScheduleDirectory.ListTeacherBusyIntervalsAsync` (Reporting boş zaman) oluşumları yerel `BYDAY`/ay gününe göre üretir; m04 "Tekrar kuralı yerel saatte genişletilir") · 2026-11-14 (P15 Task 9: **yeni uç yok**; `link-parent` yaşayan özel ders bağı ister, otomatik ders ücreti yalnız yaşayan bağda — m03 kural 21, m07 kural 11) · 2026-11-14 (P15 Task 7: **yeni uç yok**; M01'de yeni enum `SessionRevocationReason` + `refresh_token_sessions.RevocationReason`; `/refresh` eşzamanlı isteklerde tek kazanan, kaybeden düz 401; cihaz/oturum iptali ve hesap kapanışı erişim token'ını da keser; `/api/identity/*` hız sınırı üç politikaya ayrıldı — `auth` IP, `refresh` IP, `session` kullanıcı) · 2026-11-14 (P15 Task 5: **yeni uç yok**; Messaging gönderimi ilişki bitince `403 messaging.relationship_ended`, gelen kutusu/açma/engel yanıtlarına `isReadOnly` (yalnız ekleme); kapı `ITeacherStudentAccess == Private`'a geçti; Messaging'e `StudentProfilesMerged` `[consume]`) · 2026-11-14 (P15 Task 4: **yeni uç yok**; Parents `approve` yalnız `Pending`'den (`409 parents.invalid_link_transition`), birincil veli yarışı `409 parents.primary_exists`; Parents `[emit] ParentChildLinkEnded` + `[consume] StudentProfileLinkedToUser`; Students `[consume] ParentChildLinkEnded`, veli profil okuması `IParentAccessDirectory`'den) · 2026-11-14 (P15 Task 3: **yeni uç yok**; Students `links/{id}/accept|reject` gövdede `inviteCode` ister (sözleşme değişikliği — mobil bu uçları çağırmıyor); yeni `[emit] StudentProfileLinkedToUser`; Parents + ProgressTracking'e `StudentProfilesMerged` `[consume]`) · 2026-11-13 (P15 Task 2: Students `[expose] ITeacherStudentAccess` — erişim kapsamı sözleşmesi; yeni uç yok) · 2026-09-04 (**P10 KAPANIŞI / M16-1 + M09-2 + D-08**: **yeni uç eklenmedi**; zincir gerçek Postgres + Redis'te uçtan uca doğrulandı (`RealMessagingChainTests`, 3 test / 12 adım) ve doğrulama **bir hata çıkardı** — kullanıcı başına mesaj kotası `UseAuthentication` sırası yüzünden sessizce IP'ye düşüyordu, `Program.cs`'te düzeltildi. M16 **bilerek 🟡**. Önceki not — **P10 Task 4 / M16-1 + D-08**: **yeni backend ucu yok** — tek sunucu değişikliği `POST /conversations` artık `counterpartUserId` boş bırakılınca öğrenciyi `studentId`'den çözüyor (istemci öğrencinin kullanıcı kimliğini bilmiyor); mobil feature tablosuna `messaging` satırı eklendi (`/messages` + `/messages/:conversationId`), `messaging_widgets.dart` widget kataloğuna girdi, `flutter_chat_ui` bağımlılığı **kaldırıldı** (D-18). Önceki not — **P10 Task 3 / M16-1+M09-2**: **yeni uç eklenmedi**; Messaging ilk olayını yayıyor (`MessageSentDomainEvent`) ve Notifications'a yeni `[consume]` tüketici `MessageSentNotificationHandler` eklendi. Önceki not — **P10 Task 2 / M16-1**: Messaging envanterine **8 uç** (sohbet aç / gelen kutusu / mesaj listele-gönder / okundu / engelle-kaldır / yumuşak sil; uç sayısı 1 → 9); iki mevcut sözleşme yeni tüketici kazandı — `IStudentDirectory` ve `IParentAccessDirectory` mesajlaşmanın ilişki kapısını besliyor; `RateLimiting` ayarına **kullanıcı başına** partition'lı `Messaging` politikası (30/dk) eklendi. Önceki not — **P10 Task 1 / M16-1**: **Messaging modülü açıldı** — modül indeksinde backend sütunu `_(yok — yeni)_` → `Messaging`, durum 🔴 → 🟡; `messaging` şeması (`conversations`, `messages`) + `/api/messaging/status`; gerçek uçlar Task 2'de. Önceki not — **P09 Task 7 — kısmi kapanış**: **yeni uç eklenmedi**; üyelik zinciri (Free limit → deneme → premium → dönem sonu → yeniden kilit) gerçek Postgres'e karşı iki integration testiyle doğrulandı; M17 bilerek 🟡 kaldı — `ad-placements` (Q3) ve ödeme webhook'u (Q1) doğrulanamadı. Önceki not — P09 Task 6 / D-10+D-15: **yeni backend ucu yok**; mobil feature tablosuna `membership` satırı eklendi (`/membership` + `/membership/paywall`), `PremiumLock`/`PremiumLockCard` ortak widget kataloğuna girdi, `/more` "Uyelik" satırı aktifleşti. Önceki not — P09 Task 3 / M17-1+M17-3: Membership envanterine **kendi bölümü** açıldı — 7 yeni uç (`GET /plans` anonim, `start-trial`, `cancel`, `referral-code`, `referral/redeem`, Admin `POST/PUT /plans`; uç sayısı 2 → 9) + Development-only plan tohumlaması. Önceki not — P09 Task 2 / K-09+M17-5+M03-4: Membership envanterine `GET /api/membership/me` (uç sayısı 1 → 2); **yeni modüller-arası sözleşme `IEntitlementDirectory`** (Membership uygular, Study/Students/Reporting/Parents tüketir) — üyelik kapılarının tek kaynağı; `IMembershipDirectory` **geçici köprüye** indirildi (Students + Parents uygular, tek tüketicisi `EntitlementDirectory`, P12'de silinecek). Önceki not — P09 Task 1 / M17-1: **Membership modülü açıldı** — backend sütunu `_(yok)_` → `Membership`, durum 🔴 → 🟡; `membership` şeması (`subscription_plans`, `user_subscriptions`) + `/api/membership/status`. Önceki not — P08 kapanış / Task 6: **M10 🟡 → 🟢** ve **M14 🟡 → 🟢**; mobil feature tablosuna `reporting` satırı eklendi (`/teacher/reports` + `/student/reports`). Önceki not — P08 Task 4 / M14-4: Reporting envanterine PDF rapor üretimi + yetkili indirme uçları (uç sayısı 6 → 8); üç yeni/genişleyen modüller-arası sözleşme — `IStudentDirectory.GetDisplayNameAsync` (Students), `ITeacherDirectory` (Teachers), `IStudentAssignmentDigestDirectory` (Assignments); `MembershipGate`/`PremiumFeature` Study.Application'dan `Shared.Contracts`'a taşındı (Reporting de aynı kapıyı kullanıyor). Önceki not — P08 Task 3 / M14-1+M14-2+M14-3+M14-5: Reporting envanterine 4 analitik uç (uç sayısı 2 → 6) + 8 projeksiyon tüketicisi + 3 yeni read-model tablosu ailesi; yeni modüller-arası sözleşme `ITeacherAvailabilityDirectory` (Teachers uygular) ve `ILessonScheduleDirectory.ListTeacherBusyIntervalsAsync` (Scheduling uygular). Önceki not — P08 Task 2 / M10-2+M10-3+M10-4: ProgressTracking envanterine öğretmen ve veli `overview` uçları (uç sayısı 9 → 11) + `LessonSessions.LessonSessionCompletedDomainEvent` tüketicisi; Notifications'a `TopicGoalAchievedNotificationHandler` [consume]; `IStudentDirectory` sözleşmesi `IsLinkedTeacherOfStudentAsync` ile büyüdü. Önceki not — P08 Task 1 / M10-1: ProgressTracking envanterine `GET /students/{id}/snapshots` ucu + günlük `ProgressSnapshotService` tarama akışı (ProgressTracking uç sayısı 8 → 9). Önceki not — 2026-09-04 (P07 Task 2 / C-03: Reporting envanterine ilk gerçek uç `GET /teachers/{id}/dashboard` + 7 projeksiyon tüketicisi; M14 durumu 🔴 → 🟡. Önceki not — 2026-09-04 (P06 Task 10 canlı doğrulama: **yeni uç eklenmedi**, dört hata düzeltildi — Students branş ekleme `ValueGeneratedNever`, Teachers arama Türkçe 'I' katlaması, Parents bildirim sorgusu SQL çevrilebilirliği (tüm outbox zinciri ölüydü), Notifications liste ucu parametre varsayılanları) · 2026-09-04 (P06 Task 9 / M03-1 + M03-2 + M03-3: Students envanterine 4 uç — `link-parent`, `subjects` ekle/sil, `by-parent` listesi; yeni modüller-arası sözleşme `IParentChildLinkRequestService` (Parents uygular) ve `IParentAccessDirectory` `ListApprovedStudentIdsAsync` ile büyüdü; `StudentSubjectResponse` artık `Id` taşır). Önceki not — 2026-09-04 (P06 Task 7 / M06-3 + M06-4: Assignments envanterine puanlama ucu `POST /{id}/grade` + saatlik `AssignmentDueScanner` arka plan akışı; Notifications'a iki `[consume]` tüketici (`AssignmentDueSoon`/`AssignmentMissed`). Önceki not — 2026-09-04 (P06 Task 6 / M04-1 + M04-2/M08-1: Scheduling'e **yeni uç eklenmedi**, iki yanıt sözleşmesi büyüdü — `GET /teachers/{id}/lessons` artık occurrence döner (`LessonScheduleResponse.OccurrenceStartAtUtc`), birleşik takvim `StudentCalendarOccurrenceResponse.ConflictsWithLesson` taşır; `RecurrenceExpander` COUNT destekler). Önceki not — 2026-09-04 (P06 Task 5 / M07-1 + M07-2: Payments'a **yeni uç eklenmedi**, iki arka plan akışı eklendi — saatlik `PaymentOverdueScanner` (kalıcı `Overdue` + `PaymentBecameOverdueDomainEvent`) ve `LessonSessionCompletedPaymentHandler` [consume] (tamamlanan ücretli dersten `LessonFee` kaydı); Notifications'a `PaymentOverdueNotificationHandler` [consume]; `IStudentDirectory` sözleşmesi `GetAgreedLessonRateAsync` ile, `LessonSessionDetails` `IsChargeable` ile büyüdü). Önceki not — 2026-09-04 (P06/M05-2: LessonSessions envanterine `from-schedule` ucu + yeni modüller-arası sözleşme `ILessonScheduleDirectory` (Scheduling uygular). P06/M05-1: LessonSessions envanterine baslat/iptal 2 ucu. P06/M02-3 + M02-4: Teachers envanterine filtreli arama `GET /profiles` + pasifleştirme/aktifleştirme 2 ucu). Önceki not — 2026-09-03 (P06/M02-1: Teachers envanterine Admin doğrulama ucu. P05: Settings envanterine ayar oku/yaz 2 ucu + 3 modüller-arası sözleşme; M15 durumu 🟡 → 🟢. P04/M06-1: Assignments envanterine ders kaynağı 4 ucu. P04/M02-2: Teachers envanterine profil fotoğrafı 2 ucu; P04/C-02: ödev teslimi ortak `IFileStorage`'a taşındı. P03/M11-3/M11-4: Notifications envanterine in-app bildirim merkezi 3 ucu + `NotificationDispatcher` push turu; M11 durumu 🟡 → 🟢. P03/M11-2: `POST`/`DELETE /device-tokens`). Önceki not — 2026-09-02 (P02/M01-1: Identity envanterine `GET /me (auth)` eklendi). Önceki not — 2026-09-02 (F-07: §3 mobil eşlemesi — app artık üç rolü de kapsıyor; `notifications` + `progress` feature satırları eklendi). Önceki not — 2026-08-26 (Y4 kapatıldı: tüm `[consume]` handler notları "idempotent (ortak inbox_messages)" olarak birleştirildi; Parents'ın 4 read-model projeksiyonu için `[consume]` satırı eklendi; §4 başına ortak `IdempotentIntegrationEventHandler` notu). Önceki not — 2026-08-19 (Geçiş 2 kod-senkron m07-m12: Payments envanterine `/records/search` + ProgressTracking `/status` eklendi; endpoint sayıları doğrulandı — Payments 11, Study 38, Parents 11, ProgressTracking 8, Notifications 2, Matching 1). Önceki not — 2026-07-19 (Ö-E: M08 sayaç güvenilirliği — `sessions/{id}/recover` takılı seans kurtarma + `students/{id}/active-session` (isStale 6 saat) + pause/complete opsiyonel `clientEffectiveMinutes` istemci-otoriter süre · Ö-C: M03 `POST /links/claim` davet kodu ile öğrenci claim + tam profil birleştirme merge → modüller-arası `StudentId` yeniden atama · Ö-B: M08 `POST /students/{id}/mock-exams` çok dersli deneme + sınav tipine göre net böleni (ExamPenalty) + M03 `TargetExam` · Ö-A2: M08 seans/test düzenle-sil endpoint'leri + konu rollup yeniden türetme) · 2026-07-18 (Dilim A takvim çekirdeği: M04 MeetingUrl/erteleme/iptal nedeni+sil/tatil bloğu/occurrence istisnaları; M05 oturum IsChargeable · Dilim B: M06 not görünürlüğü + ödev onay/geri gönder · Dilim D: M02 çoklu branş + sertifika))*

*Güncelleme: 2026-11-15 — P17 Task 4 / N-03, K-21: tek oturum tamamlama yolu ve oluşum durum senkronizasyonu.*

*Güncelleme: 2026-11-15 — P17 T6: oluşum/alıcı anahtarlı hatırlatma, kayan 7 gün, kaynak uzlaştırması ve süre/tercih kapıları.*

*Güncelleme: 2026-11-15 — P17 T8: haftalık yaşam döngüsü ve kapanış regresyonları.*

*Güncelleme: 2026-11-15 — P18 Task 1 eşzamanlılık altyapısı.*

*Güncelleme: 2026-11-15 — P18 T2: ödeme hareketleri ve niyet komutları.*
