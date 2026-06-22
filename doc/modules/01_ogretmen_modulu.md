# 👨‍🏫 Öğretmen Modülü — Detaylı Tasarım Dokümanı

> **Öncelik: 1️⃣ (İlk hedef)** · **Faz 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Büyük ölçüde yazıldı**
>
> **Amaç:** Öğretmenin uygulamayı **her gün** kullanmasını sağlayan günlük operasyon aracı.
> Öğretmen; öğrencilerini ekler, derslerini takvimde planlar, dersleri işler/kaydeder,
> not ve ödev verir, ödemeleri takip eder.

> **Tasarım ilkesi (PRD §1):** Bu modül, öğretmeni "öğrenci bulduğu bir pazar yeri"ne değil,
> "derslerini düzenli yönettiği bir günlük çalışma aracı"na bağlar. Eşleştirme (Faz 4) sonradan gelir;
> önce yönetim tarafı eksiksiz çalışmalıdır.

---

## 1. Kapsam — Öğretmen Modülü Hangi Backend Modüllerini Kullanır?

"Öğretmen modülü" tek bir teknik modül değil; öğretmenin günlük iş akışını oluşturan **6 backend modülünün** birleşimidir:

| Adım | Yetenek | Backend Modülü | Mobil Feature | PRD |
|------|---------|----------------|---------------|-----|
| 0 | Giriş / kayıt / rol | `Identity` | `auth` | M01 |
| 1 | Öğretmen profili oluştur/düzenle | `Teachers` | `teacher_profile` | M02 |
| 2 | Öğrenci ekle ve listele | `Students` | `students` | M03 |
| 3 | Takvimde ders planla | `Scheduling` | `scheduling` | M04 |
| 4 | Dersi işle/tamamla, katılım & not | `LessonSessions` | `lesson_sessions` | M05 |
| 5 | Ders notu + ödev ver/takip et | `Assignments` | `assignments` | M06 |
| 6 | Ödeme/bakiye takibi | `Payments` | `payments` | M07 |
| 7 | Yaklaşan ders hatırlatması | `Notifications` | _(server-side)_ | M11 |

### Öğretmenin "altın akışı" (Golden Path)
```
Kayıt ol (Teacher rolü)
   → Öğretmen profilini doldur (branş, şehir, ücret, uygunluk saatleri)
      → Öğrenci ekle (manuel)
         → Takvime ders ekle (tek seferlik veya tekrar eden)
            → [Ders günü] Push hatırlatma al
               → Dersi tamamla (süre, konu, katılım, öğretmen notu)
                  → Ders notu yaz + ödev ver
                     → Ödeme kaydını işaretle (tahsil edildi / bekliyor)
                        → Aylık gelir özetini gör
```

---

## 2. Domain Modeli (Koddan Doğrulanmış)

### 2.1 Teachers — `TeacherProfile` (AggregateRoot)
`src/Modules/Teachers/Domain/TeachersDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Profil kimliği |
| `UserId` | Guid | Identity'deki kullanıcı (1 kullanıcı = 1 öğretmen profili) |
| `FullName` | string | Ad soyad |
| `Subject` | string | Branş |
| `City`, `District` | string | Şehir / ilçe |
| `Biography`, `Headline` | string? | Tanıtım metni / başlık |
| `LessonFormat` | enum `TeacherLessonFormat` | `InPerson=1`, `Online=2`, `Hybrid=3` |
| `ExperienceYears` | int | Deneyim yılı |
| `EducationLevel` | string | Eğitim seviyesi |
| `HourlyRateAmount` + `Currency` | decimal + string | Saatlik ücret (varsayılan `TRY`) |
| `IsVerified` | bool | Doğrulama durumu (rozet) |
| `ProfilePhotoUrl` | string? | Profil fotoğrafı |
| `AvailabilitySlots` | List<`TeacherAvailabilitySlot`> | Haftalık uygunluk |

**`TeacherAvailabilitySlot` (Entity):** `DayOfWeek`, `StartTime` (TimeOnly), `EndTime`, `IsOnlineAvailable`, `IsInPersonAvailable`.

**Domain Events:** `TeacherProfileCreatedDomainEvent`, `TeacherProfileUpdatedDomainEvent`.

**İş kuralı:** Uygunluk slotunda `EndTime > StartTime` olmalıdır (aksi halde `teachers.invalid_availability`).

### 2.2 Students — `StudentProfile` (AggregateRoot)
`src/Modules/Students/Domain/StudentsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Öğrenci profil kimliği |
| `UserId` | Guid? | Öğrenci kendi hesabıyla bağlıysa (self-registered) |
| `CreatedByTeacherUserId` | Guid? | Öğretmen ekledi ise öğretmenin kullanıcı kimliği |
| `ParentUserId` | Guid? | Bağlı veli |
| `FullName`, `GradeLevel` | string | Ad soyad, sınıf seviyesi |
| `ContactEmail`, `ContactPhone` | string? | İletişim |
| `GoalSummary`, `LevelNotes` | string? | Hedef / seviye notları |
| `Origin` | enum `StudentOrigin` | `TeacherManaged=1`, `SelfRegistered=2` |
| `IsActive` | bool | Aktif/pasif |
| `Subjects` | List<`StudentSubject`> | Branş + hedef seviye |

**Domain Event:** `StudentProfileCreatedDomainEvent`.

> **Öğretmen-öğrenci ilişkisi burada başlar:** Öğretmen bir öğrenci eklediğinde `Origin = TeacherManaged`
> ve `CreatedByTeacherUserId = <öğretmenin userId>` olur. Bu, öğretmenin "Öğrencilerim" listesini besler
> (`GET /api/students/profiles/by-teacher/{teacherUserId}`).

### 2.3 Scheduling — `LessonSchedule` (AggregateRoot)
`src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `TeacherUserId`, `StudentId` | Guid | Dersin tarafları |
| `Subject` | string | Konu/branş |
| `LessonFormat` | enum `ScheduledLessonFormat` | InPerson/Online/Hybrid |
| `StartAtUtc`, `EndAtUtc`, `TimeZone` | DateTime + string | Zaman dilimi farkındalıklı |
| `RecurrenceRule` | string? | Tekrar kuralı (RRULE benzeri) |
| `Status` | enum `LessonScheduleStatus` | `Draft=1`, `Planned=2`, `Cancelled=3`, `Completed=4` |
| `ReminderOffsetMinutes` | int | Hatırlatma kaç dk önce |
| `LocationLabel`, `Notes` | string? | Konum / not |

**Davranış:** `Cancel(note, updatedOnUtc)` → durum `Cancelled`, not eklenir, `LessonScheduleCancelledDomainEvent` yayılır.
**Events:** `LessonScheduledDomainEvent`, `LessonScheduleCancelledDomainEvent` (→ Notifications modülü dinler).

### 2.4 LessonSessions — `LessonSession` (AggregateRoot)
`src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `LessonScheduleId` | Guid? | Hangi planlı dersten doğdu (opsiyonel) |
| `TeacherUserId`, `StudentId`, `Subject` | — | Taraflar + konu |
| `PlannedStartAtUtc` | DateTime | Planlanan başlangıç |
| `ActualStartAtUtc`, `ActualEndAtUtc`, `DurationMinutes` | DateTime?, int? | Gerçekleşen |
| `AttendanceStatus` | enum | `Unknown`, `Attended`, `Late`, `Absent` |
| `Status` | enum `LessonSessionStatus` | `Planned`, `InProgress`, `Completed`, `Cancelled` |
| `TopicTitle`, `CoveredContent`, `TeacherNotes` | string | İşlenen içerik + not |

**Davranış:** `Complete(...)` → süreyi gerçek başlangıç/bitişten hesaplar (`Math.Ceiling`), durum `Completed`,
`LessonSessionCompletedDomainEvent` yayılır → **Assignments modülü dinler** (ödev/not ekleme akışını tetikler).

### 2.5 Assignments — `Assignment` + `LessonNote` (2 AggregateRoot)
`src/Modules/Assignments/Domain/AssignmentsDomainModel.cs`

**`Assignment`:** `StudentId`, `TeacherUserId`, `LessonSessionId?`, `Title`, `Description?`, `DueDateUtc?`,
`Status` (`Pending`, `InProgress`, `Completed`, `Cancelled`), `AttachmentUrl?`. → `MarkCompleted()`.

**`LessonNote`:** `LessonSessionId`, `TeacherUserId`, `StudentId`, `Summary`, `CoveredTopics?`, `Recommendations?`. → `Update()`.

> Mobil tarafta bu ikisi "ders sonrası takip" (follow-up) olarak tek ekranda birleşir:
> `POST/GET /api/assignments/lesson-sessions/{lessonSessionId}/follow-up`.

### 2.6 Payments — `PaymentRecord` (AggregateRoot)
`src/Modules/Payments/Domain/PaymentsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `TeacherUserId`, `StudentId` | Guid | Taraflar |
| `RelatedLessonSessionId` | Guid? | İlişkili ders (opsiyonel) |
| `ItemType` | enum `BillingItemType` | `LessonFee=1`, `MonthlyPackage=2`, `ManualAdjustment=3` |
| `ExpectedAmount`, `CollectedAmount`, `Currency` | decimal | Beklenen / tahsil edilen |
| `DueDateUtc`, `CollectedOnUtc?` | DateTime | Vade / tahsil tarihi |
| `Status` | enum `PaymentStatus` | `Pending`, `PartiallyPaid`, `Paid`, `Overdue`, `Cancelled` |
| `BillingPeriodStartUtc/EndUtc` | DateTime? | Aylık paket dönemi |

**Davranış:** `UpdateManualTracking(...)` → `PaymentRecordUpdatedDomainEvent` (eski/yeni durum ve tutarı taşır).

---

## 3. API Sözleşmesi (Mevcut + Eksik)

### 3.1 Mevcut Endpoint'ler ✅

| Yetenek | Method + Route | Not |
|---------|----------------|-----|
| Profil oluştur | `POST /api/teachers/profiles` | Aynı kullanıcıda 2. profil → `409 teachers.profile_exists` |
| Profil güncelle | `PUT /api/teachers/profiles/{userId}` | Yoksa `404 teachers.profile_not_found` |
| Profil getir | `GET /api/teachers/profiles/{userId}` | |
| Öğrenci ekle | `POST /api/students/profiles` | `Origin=TeacherManaged`, `CreatedByTeacherUserId` set |
| Öğrenci getir | `GET /api/students/profiles/{studentId}` | |
| Öğrencilerim | `GET /api/students/profiles/by-teacher/{teacherUserId}` | Öğretmenin listesi |
| Ders planla | `POST /api/scheduling/lessons` | |
| Ders iptal | `POST /api/scheduling/lessons/{lessonId}/cancel` | |
| Ders getir | `GET /api/scheduling/lessons/{lessonId}` | |
| Takvim | `GET /api/scheduling/teachers/{teacherUserId}/lessons` | Haftalık/aylık görünüm verisi |
| Dersi tamamla | `POST /api/lesson-sessions/{lessonSessionId}/complete` | |
| Oturum getir | `GET /api/lesson-sessions/{lessonSessionId}` | |
| Ders sonrası takip (not+ödev) | `POST /api/assignments/lesson-sessions/{lessonSessionId}/follow-up` | |
| Takip getir | `GET /api/assignments/lesson-sessions/{lessonSessionId}/follow-up` | |
| Ödeme kaydı oluştur | `POST /api/payments/records` | |
| Ödeme güncelle | `PUT /api/payments/records/{paymentRecordId}` | |
| Ödeme getir | `GET /api/payments/records/{paymentRecordId}` | |
| Ödeme listesi | `GET /api/payments/teachers/{teacherUserId}/records` | |
| Gelir özeti | `GET /api/payments/teachers/{teacherUserId}/summary` | Aylık toplam |
| Ödeme filtre | `GET /api/payments/teachers/{teacherUserId}/records/filter` | Geciken / bekleyen |

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")` ile korunur ve `Result<T>` döner;
> hata kodları HTTP statüsüne eşlenir (`409`, `404`, `403 shared.forbidden`, varsayılan `400`).

### 3.2 Eksik / İyileştirilmesi Gereken Endpoint'ler ⚠️

- [ ] `GET /api/scheduling/teachers/{teacherUserId}/lessons` için **tarih aralığı + status filtresi** (haftalık görünüm).
- [ ] Ders **güncelleme** (`PUT /lessons/{id}`) — şu an sadece oluştur + iptal var.
- [ ] **Ders çakışması kontrolü** (PRD M04) — aynı öğretmende zaman çakışan ders engellensin.
- [ ] `LessonSession` **oluşturma** akışı — şu an sadece `complete` + `get` var; planlı dersten oturum türetme netleştirilmeli.
- [ ] Öğrenci **güncelleme/pasifleştirme** (`PUT /api/students/profiles/{id}`, `IsActive`).
- [ ] Öğretmen **dashboard özeti** endpoint'i (bugünkü dersler, bekleyen ödevler, geciken ödemeler tek çağrıda).

---

## 4. Mobil Ekranlar (Flutter — Mevcut)

`mobile/lib/core/routing/app_router.dart` ve feature klasörlerinden doğrulanmıştır.

| Route | Sayfa | Feature | Açıklama |
|-------|-------|---------|----------|
| `/` | `WelcomePage` | auth | Karşılama |
| `/role-selection` | `RoleSelectionPage` | auth | Rol seçimi |
| `/login`, `/register` | Login/Register | auth | Giriş/kayıt |
| `/dashboard` | `DashboardPage` | dashboard | Öğretmen ana ekranı (bölümler) |
| `/teacher-profile` | `TeacherProfilePage` | teacher_profile | Profil oluştur/düzenle |
| `/teacher-panel-preview` | preview | dashboard | Giriş yapmadan önizleme |
| `/students` | `StudentsPage` | students | Öğrenci listesi |
| `/students/:studentId` | `StudentDetailPage` | students | Öğrenci detayı |
| `/scheduling` | `SchedulingPage` | scheduling | Takvim (syncfusion calendar) |
| `/lesson-sessions` | `LessonSessionsPage` | lesson_sessions | Ders oturumları (`?create=1` ile oluştur) |
| `/lesson-sessions/detail` | `LessonDetailPage` | lesson_sessions | Oturum detayı |
| `/lesson-notes/new` | `LessonNoteFormPage` | lesson_sessions | Ders notu formu |
| `/lesson-sessions/detail/note` | `LessonNoteViewPage` | lesson_sessions | Not görüntüleme |
| `/assignments/new`, `/assignments/:lessonSessionId` | `AssignmentFollowUpPage` | assignments | Ödev/takip |
| `/payments`, `/payments/new` | `PaymentsPage`, `PaymentFormPage` | payments | Ödeme listesi/formu |
| `/more`, `/account-info` | `MorePage`, `AccountInfoPage` | more | Ayarlar/hesap |

> **Not:** `syncfusion_flutter_calendar` paketi takvim için kullanılıyor (`pubspec.yaml`).
> Durum yönetimi `flutter_bloc` (Cubit) ile, her feature kendi `*_cubit.dart` + `*_state.dart` dosyalarına sahip.

### Eksik mobil ekranlar ⚠️
- [ ] Öğretmen **dashboard** zenginleştirme: bugünkü dersler, geciken ödeme uyarısı, bekleyen ödev sayısı.
- [ ] Takvimde **ders çakışması** görsel uyarısı.
- [ ] Öğrenci ekleme formunda **branş (StudentSubject)** ve **veli bağlama** alanları.

---

## 5. İş Kuralları (Business Rules)

1. **Tek profil:** Bir kullanıcının yalnızca bir öğretmen profili olabilir (`teachers.profile_exists`).
2. **Uygunluk geçerliliği:** `EndTime > StartTime` zorunlu.
3. **Doğrulama:** `IsVerified` yalnızca admin/doğrulama akışıyla `true` olmalı; profil oluştururken kod `false` set eder (güvenli varsayılan).
4. **Öğrenci sahipliği:** Öğretmen yalnızca kendi eklediği (`CreatedByTeacherUserId`) veya kendisine bağlı öğrencileri görebilmeli (yetki kontrolü — `*Policies.cs`).
5. **Ders tamamlama:** Süre, `ActualStart`/`ActualEnd` farkından otomatik hesaplanır; manuel girilmez.
6. **Ödeme durumu:** `CollectedAmount < ExpectedAmount` ise mantıken `PartiallyPaid`; vadesi geçmiş ve ödenmemişse `Overdue` (toplu hesaplama/job ile güncellenmeli — bkz. Eksikler).

---

## 6. Olay Akışı (Event-Driven)

```
LessonSchedule oluştu      → LessonScheduledDomainEvent
                              → Notifications: yaklaşan ders hatırlatması planlar (ReminderOffsetMinutes)
LessonSchedule iptal       → LessonScheduleCancelledDomainEvent → hatırlatma iptal
LessonSession tamamlandı   → LessonSessionCompletedDomainEvent
                              → Assignments: ders sonrası not/ödev akışını mümkün kılar
                              → (gelecek) ProgressTracking: gelişim verisi günceller
                              → (gelecek) Reviews: öğrenciye değerlendirme daveti
PaymentRecord değişti      → PaymentRecordUpdatedDomainEvent → (gelecek) gelir özeti yeniden hesaplanır
```

> Olaylar **Outbox pattern** ile güvenilir biçimde yayılır (`Shared/Infrastructure/Messaging`).
> Bir örnek mevcut tüketici: `Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs`.

---

## 7. Kabul Kriterleri (Faz 1 Çıktısı)

PRD §Faz 1: "Öğretmen kendi öğrencilerini ekleyip derslerini yönetebilir."

- [x] Öğretmen kayıt olup `Teacher` rolüyle giriş yapabilir.
- [x] Öğretmen profilini oluşturabilir/düzenleyebilir (branş, şehir, ücret, uygunluk).
- [x] Öğretmen manuel öğrenci ekleyebilir ve listeleyebilir.
- [x] Öğretmen takvime ders ekleyebilir (tek seferlik + tekrar kuralı alanı mevcut).
- [x] Ders oturumu tamamlanabilir (konu, süre, katılım, not).
- [x] Ders sonrası not + ödev eklenebilir.
- [x] Manuel ödeme kaydı + gelir özeti + geciken filtre.
- [ ] **Yaklaşan ders push bildirimi uçtan uca** (Notifications altyapısı var, mobil/FCM teslimatı doğrulanmalı).
- [ ] **Ders çakışması kontrolü.**
- [ ] **5–10 gerçek öğretmenle beta test** (PRD önerisi).

---

## 8. Eksikler ve Yapılacaklar Listesi (Öncelik Sırasıyla)

> Bunlar öğretmen modülünü Faz 1 için "tamam" saymadan önce kapatılması gerekenler.

1. **Dashboard özeti** — Tek endpoint + zengin mobil ana ekran (bugünkü dersler, bekleyen ödev, geciken ödeme).
2. **Ders güncelleme + çakışma kontrolü** (M04 eksikleri).
3. **LessonSession yaşam döngüsü netleştirme** — Planlı dersten oturum oluşturma akışı (`Planned → InProgress → Completed`).
4. **Öğrenci düzenleme/pasifleştirme** + branş ve veli bağlama alanları.
5. **Push bildirim uçtan uca** — FCM token kaydı (mobil), teslimat doğrulama.
6. **Ödeme durum otomasyonu** — Vade geçince `Overdue`'a çeken zamanlanmış iş.
7. **Yetkilendirme testleri** — Öğretmen yalnızca kendi verisine erişebilir (`*Policies.cs` kapsamı).

---

## 9. İlişkili Dokümanlar

- Öğrenci tarafı ve öğretmen-öğrenci ilişkisinin diğer yarısı → [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md)
- Genel durum ve eşleme tablosu → [`00_genel_bakis.md`](00_genel_bakis.md)
- Mimari kurallar → [`../ai_ready_architecture.md`](../ai_ready_architecture.md)

---

*Öğretmen Modülü — Detaylı Tasarım | Faz 1 | Güncelleme: 2026-06-21*
