# P06 — Öğretmen MVP Kapanışı (Beta'ya Hazırlık) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğretmenin günlük iş akışındaki son boşlukları kapatmak: doğrulama rozetini verebilme, öğretmen arama/listeleme ucu, ders oturumu yaşam döngüsü (başlat/iptal + planlı dersten türetme), ödeme otomasyonu (gecikme + ders ücreti), takvimde tekrar açılımı, ödev son teslim uyarısı ve mobil uygunluk düzenleyici.

**Architecture:** Mevcut modüllere ekleme yapılır, yeni modül açılmaz. Ödeme otomasyonu için Payments'a bir `BackgroundService` (`PaymentOverdueScanner`) eklenir; gecikme tespiti `PaymentBecameOverdueDomainEvent` üretir ve outbox üzerinden Notifications'a düşer. Ders tamamlanınca ücret kaydı, `LessonSessionCompleted` integration event'inin Payments'ta idempotent tüketilmesiyle oluşur (`IdempotentIntegrationEventHandler`). Takvim tekrar açılımı, öğrenci birleşik takviminde zaten kullanılan `RecurrenceExpander`'ın öğretmen sorgusuna uygulanmasıdır.

**Tech Stack:** .NET 9, EF Core, xUnit; Flutter (syncfusion_flutter_calendar, flutter_bloc).

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md`

## Global Constraints

- **Yetki:** Doğrulama yalnız `Admin`; ders/ödeme işlemleri yalnız ilgili öğretmen (veya Admin). Her yeni command/query için authorizer.
- **Idempotency:** Event tüketen her yeni handler `IdempotentIntegrationEventHandler` tabanını kullanır (`inbox_messages`).
- **Modül sınırı:** Payments, LessonSessions'ın DbContext'ini okumaz; yalnız integration event + `Shared/Contracts`.
- **Zaman:** `IClock.UtcNow`. **Kimlik:** `IIdGenerator.New()`. **Sonuç:** `Result`/`Result<T>`.
- **Migration:** `dotnet ef migrations add <Ad> --project src/Modules/<M>/Infrastructure --startup-project src/API.Host --context <M>DbContext`
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Öğretmen doğrulama ucu (M02-1)

**Files:**
- Modify: `src/Modules/Teachers/Domain/TeachersDomainModel.cs` (`SetVerification` + `TeacherVerifiedDomainEvent`)
- Modify: `src/Modules/Teachers/Application/TeacherProfileFeatures.cs` (command + handler)
- Modify: `src/Modules/Teachers/Application/TeacherProfilePolicies.cs` (Admin-only authorizer)
- Modify: `src/Modules/Teachers/API/TeachersModule.cs`
- Modify: `src/Modules/Teachers/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/TeacherVerificationTests.cs`

**Interfaces:**
- Produces:
  - `TeacherProfile.SetVerification(bool isVerified, DateTime updatedOnUtc)` — `TeacherVerifiedDomainEvent(Guid ProfileId, Guid UserId, bool IsVerified, DateTime OccurredOnUtc)` yayar.
  - `sealed record SetTeacherVerificationCommand(Guid UserId, bool IsVerified) : ICommand<Result<TeacherProfileResponse>>`
  - `PUT /api/teachers/profiles/{userId}/verification` (auth, **yalnız Admin**)

- [ ] **Step 1: Testi yaz (kırmızı)**

```csharp
[Fact]
public void SetVerification_Should_Raise_Event_And_Update_Flag()
{
    var profile = NewProfile();                      // TeacherProfileTests içindeki yardımcıyı kullan
    profile.SetVerification(true, Now);

    Assert.True(profile.IsVerified);
    Assert.Contains(profile.DomainEvents, e => e is TeacherVerifiedDomainEvent);
}

[Fact]
public async Task Non_Admin_Should_Be_Forbidden()
{
    var authorizer = new SetTeacherVerificationCommandAuthorizer(new FakeCurrentUser(roles: ["Teacher"]));
    var result = await authorizer.Authorize(new SetTeacherVerificationCommand(Guid.NewGuid(), true), default);
    Assert.True(result.IsFailure);
    Assert.Equal("shared.forbidden", result.Error.Code);
}
```
> `FakeCurrentUser` yoksa `tests/Unit/TestDoubles/` altına ekle (`ICurrentUser` uygulayan basit kayıt).

- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~TeacherVerificationTests"`
- [ ] **Step 3: Domain + command + handler + authorizer + endpoint + DI'yı yaz.**
- [ ] **Step 4: Yeşil gör** — aynı komut → PASS.
- [ ] **Step 5: Doküman + commit**

`doc/modules/m02_teachers.md` (madde `- [x]`), `doc/modules/00_genel_bakis.md`, `doc/roles/admin.md` (yeni yetenek).
```bash
git add src/Modules/Teachers tests doc
git commit -m "feat(teachers): admin dogrulama ucu + TeacherVerified event (M02-1)"
```

---

### Task 2: Öğretmen arama/listeleme ucu (M02-3) ve pasifleştirme (M02-4)

**Files:**
- Modify: `src/Modules/Teachers/Application/TeacherProfileFeatures.cs` (query + handler)
- Modify: `src/Modules/Teachers/Application/TeacherProfilePolicies.cs`
- Modify: `src/Modules/Teachers/Infrastructure/TeacherProfileRepository.cs`
- Modify: `src/Modules/Teachers/API/TeachersModule.cs`
- Test: `tests/Unit/TeacherSearchTests.cs`

**Interfaces:**
- Produces:
  - `sealed record SearchTeacherProfilesQuery(string? City, string? District, string? Subject, TeacherLessonFormat? LessonFormat, decimal? MinRate, decimal? MaxRate, bool? OnlyVerified, int Skip, int Take) : IQuery<Result<PagedResult<TeacherSummaryResponse>>>`
  - `sealed record TeacherSummaryResponse(Guid UserId, string FullName, string Subject, IReadOnlyCollection<string> Subjects, string City, string District, TeacherLessonFormat LessonFormat, decimal HourlyRateAmount, string Currency, bool IsVerified, bool IsActive)`
  - `GET /api/teachers/profiles?city=&district=&subject=&lessonFormat=&minRate=&maxRate=&onlyVerified=&skip=&take=` (auth)
  - `TeacherProfile.Deactivate(DateTime)` / `Activate(DateTime)` + `bool IsActive` alanı
  - `POST /api/teachers/profiles/{userId}/deactivate` · `/activate` (sahibi veya Admin)

- [ ] **Step 1: Testleri yaz (kırmızı)** — filtre kombinasyonları (şehir+branş, ücret aralığı, yalnız doğrulanmış), sayfalama (`Take` en fazla 50), pasif öğretmenin sonuçlarda çıkmaması.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~TeacherSearchTests"`
- [ ] **Step 3: `IsActive` alanı + migration**
  Run: `dotnet ef migrations add AddTeacherIsActive --project src/Modules/Teachers/Infrastructure --startup-project src/API.Host --context TeachersDbContext`
- [ ] **Step 4: Repository sorgusu + query handler + authorizer (kimliği doğrulanmış herkes arayabilir) + endpoint'ler.**
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

> Not: Bu uç M12'nin (eşleştirme) **geçici** okuma yüzeyidir; P11'de `TeacherSearchProjection` devreye girince bu handler projeksiyonu okuyacak şekilde değiştirilir, dış sözleşme (URL + yanıt) korunur.
```bash
git add src/Modules/Teachers tests doc
git commit -m "feat(teachers): profil arama/listeleme + pasiflestirme (M02-3/M02-4)"
```

---

### Task 3: Ders oturumu yaşam döngüsü (M05-1/M05-3/M05-4)

**Files:**
- Modify: `src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs`
- Modify: `src/Modules/LessonSessions/Application/LessonSessionFeatures.cs`
- Modify: `src/Modules/LessonSessions/Application/LessonSessionPolicies.cs`
- Modify: `src/Modules/LessonSessions/API/LessonSessionsModule.cs`
- Modify: `src/Modules/LessonSessions/Infrastructure/DependencyInjection.cs` + migration
- Test: `tests/Unit/LessonSessionTests.cs` (mevcut dosyaya ekleme)

**Interfaces:**
- Produces:
  - `LessonSession.Start(DateTime actualStartUtc)` — yalnız `Planned` → `InProgress`; aksi halde `lesson_sessions.invalid_transition`.
  - `LessonSession.Cancel(string? reason, DateTime nowUtc)` — `Planned`/`InProgress` → `Cancelled`.
  - `LessonSession.Complete(...)` içinde **`ActualEnd > ActualStart` doğrulaması** (`lesson_sessions.invalid_time_range`).
  - Yeni alanlar: `string? MeetingUrl`, `string? RecordingUrl`, `string? CancellationReason`.
  - `POST /api/lesson-sessions/{id}/start` · `POST /api/lesson-sessions/{id}/cancel`

- [ ] **Step 1: Domain testlerini yaz (kırmızı)** — 5 test: geçerli start; ikinci start hata; complete'te ters zaman aralığı hata; cancel sonrası complete hata; `MeetingUrl` set edilebiliyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~LessonSessionTests"`
- [ ] **Step 3: Domain + alanlar + migration**
  Run: `dotnet ef migrations add AddSessionLifecycleFields --project src/Modules/LessonSessions/Infrastructure --startup-project src/API.Host --context LessonSessionsDbContext`
- [ ] **Step 4: Command/handler/authorizer + 2 endpoint.**
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Mobil** — `lesson_detail_page.dart`'a "Dersi başlat" / "İptal et" eylemleri; `MeetingUrl` varsa "Derse katıl" butonu (harici tarayıcı).
- [ ] **Step 7: Doküman + commit**

`doc/modules/m05_lesson_sessions.md` (durum geçiş diyagramı + yeni uçlar), `doc/pages/lesson_detail.md`.
```bash
git add src/Modules/LessonSessions mobile tests doc
git commit -m "feat(lesson-sessions): baslat/iptal gecisleri + zaman dogrulamasi + MeetingUrl (M05-1/3/4)"
```

---

### Task 4: Planlı dersten oturum türetme (M05-2)

**Files:**
- Modify: `src/Modules/LessonSessions/Application/LessonSessionFeatures.cs`
- Modify: `src/Modules/LessonSessions/API/LessonSessionsModule.cs`
- Modify: `src/Shared/Contracts/LessonScheduleReadContract.cs` (yeni)
- Modify: `src/Modules/Scheduling/Infrastructure/*` (contract implementasyonu)
- Test: `tests/Unit/CreateSessionFromScheduleTests.cs`

**Interfaces:**
- Produces:
  - ```csharp
    namespace EgitimUssu.Shared.Contracts;

    public sealed record LessonScheduleDetails(
        Guid LessonScheduleId, Guid TeacherUserId, Guid StudentId,
        DateTime StartAtUtc, DateTime EndAtUtc, string Subject, string? MeetingUrl, string Status);

    /// <summary>Scheduling'in dışa açtığı okuma sözleşmesi (LessonSessions tüketir).</summary>
    public interface ILessonScheduleDirectory
    {
        Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken);
    }
    ```
  - `sealed record CreateSessionFromScheduleCommand(Guid LessonScheduleId) : ICommand<Result<LessonSessionResponse>>`
  - `POST /api/lesson-sessions/from-schedule/{lessonScheduleId}` (öğretmen)

- [ ] **Step 1: Testleri yaz (kırmızı)** — plandan oturum üretilir (öğrenci/öğretmen/saat/`MeetingUrl` kopyalanır); aynı plan için ikinci çağrı **yeni oturum açmaz**, mevcut oturumu döndürür (idempotent); iptal edilmiş plandan oturum üretilemez.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~CreateSessionFromScheduleTests"`
- [ ] **Step 3: Sözleşmeyi + Scheduling implementasyonunu + handler'ı yaz.**
  > `LessonSessions` **Scheduling'e proje referansı vermez**; yalnız `Shared.Contracts`'ı kullanır (mimari test bunu zorlar).
- [ ] **Step 4: Endpoint + DI + yeşil test** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 5: Mobil** — Takvimde bir dersi tamamlarken "Oturum aç ve not gir" tek adımda: `scheduling_page.dart`'taki ders kartına "Ders notu gir" eylemi → `POST /from-schedule/{id}` → dönen `lessonSessionId` ile `/lesson-notes/new`.
- [ ] **Step 6: Doküman + commit**

```bash
git add src/Shared/Contracts src/Modules mobile tests doc
git commit -m "feat(lesson-sessions): planli dersten oturum turetme (M05-2)"
```

---

### Task 5: Ödeme otomasyonu — gecikme taraması ve ders ücreti (M07-1/M07-2)

**Files:**
- Modify: `src/Modules/Payments/Domain/PaymentsDomainModel.cs` (`MarkOverdue` + `PaymentBecameOverdueDomainEvent`)
- Create: `src/Modules/Payments/Infrastructure/PaymentOverdueScanner.cs` (BackgroundService)
- Create: `src/Modules/Payments/Infrastructure/LessonSessionCompletedPaymentHandler.cs`
- Modify: `src/Modules/Payments/Infrastructure/DependencyInjection.cs` + migration
- Modify: `src/Modules/Notifications/Infrastructure/*` (yeni event tüketimi → `UserNotification`)
- Test: `tests/Unit/PaymentOverdueScannerTests.cs`, `tests/Unit/LessonSessionCompletedPaymentHandlerTests.cs`

**Interfaces:**
- Produces:
  - `PaymentRecord.MarkOverdue(DateTime nowUtc)` — yalnız `Pending`/`PartiallyPaid` ve `DueDate < now` iken; `Status = Overdue` + `PaymentBecameOverdueDomainEvent`.
  - `PaymentOverdueScanner : BackgroundService` — saatte bir tarar, `MarkOverdue` uygular (idempotent: zaten `Overdue` olanı atlar).
  - `LessonSessionCompletedPaymentHandler : IdempotentIntegrationEventHandler` — `IsChargeable == true` olan tamamlanan oturum için `PaymentRecord` (tutar = öğrenci bazlı ders ücreti) oluşturur.

- [ ] **Step 1: Testleri yaz (kırmızı)**
```csharp
[Fact]
public void MarkOverdue_Should_Only_Apply_To_Outstanding_Past_Due()
{
    // Paid kayıt → durum değişmez; vadesi gelmemiş Pending → değişmez;
    // vadesi geçmiş Pending → Overdue + event
}

[Fact]
public async Task Scanner_Should_Be_Idempotent()
{
    // İki tur çalıştır → ikinci turda yeni event üretilmemeli
}

[Fact]
public async Task Completed_Chargeable_Session_Should_Create_Payment_Record()
{
    // Aynı EventId ikinci kez gelirse ikinci kayıt oluşmamalı (inbox guard)
}

[Fact]
public async Task Non_Chargeable_Session_Should_Not_Create_Payment()
{
}
```
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~Payment"`
- [ ] **Step 3: Domain + migration** (`Status` zaten enum; `OverdueMarkedOnUtc` alanı ekle)
  Run: `dotnet ef migrations add AddOverdueTracking --project src/Modules/Payments/Infrastructure --startup-project src/API.Host --context PaymentsDbContext`
- [ ] **Step 4: Scanner + handler'ı yaz, DI'ya `AddHostedService<PaymentOverdueScanner>()` ekle.**
  > Ders ücreti: `Shared/Contracts/IStudentDirectory` üzerinden öğrenci bazlı ücret (`B-07` ile eklenen `rate`) okunur; yoksa öğretmenin `HourlyRateAmount`'u kullanılmaz — ücret yoksa kayıt **oluşturulmaz** ve `LogInformation` düşülür (sessiz yanlış tutar üretme).
- [ ] **Step 5: Notifications tarafında vade bildirimi** — `PaymentBecameOverdue` integration event'i tüketilip öğretmene (ve veliye, tercihe bağlı) `UserNotification` + push üretir.
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Doküman + commit**

`doc/modules/m07_payments.md` (otomasyon + yeni event), `doc/modules/m11_notifications.md` (yeni tüketici), `doc/modules/veri_modeli.md`.
```bash
git add src/Modules tests doc
git commit -m "feat(payments): gecikme taramasi + ders tamamlaninca ucret kaydi (M07-1/M07-2)"
```

---

### Task 6: Takvimde tekrar açılımı ve çakışma uyarısı (M04-1, M04-2/M08-1)

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (`GetTeacherLessonsQuery` handler)
- Modify: `src/Modules/Scheduling/Application/StudentCalendarFeatures.cs` (çakışma bayrağı)
- Test: `tests/Unit/RecurrenceExpanderTests.cs` (ekleme), `tests/Unit/StudentCalendarQueryTests.cs` (ekleme)
- Modify: `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart`

**Interfaces:**
- `GET /api/scheduling/teachers/{teacherUserId}/lessons` yanıtı artık **açılmış occurrence'lar** döner (aynı DTO, `occurrenceStartAtUtc` alanı eklenir); `LessonOccurrenceException` istisnaları uygulanır.
- Öğrenci takvim yanıtındaki bireysel plan öğelerine `bool ConflictsWithLesson` alanı eklenir.

- [ ] **Step 1: Testleri yaz (kırmızı)** — haftalık tekrar eden ders 4 haftalık aralıkta 4 occurrence döner; iptal edilen occurrence çıkmaz; bireysel plan özel dersle çakışıyorsa `ConflictsWithLesson = true`.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~RecurrenceExpanderTests|FullyQualifiedName~StudentCalendarQueryTests"`
- [ ] **Step 3: Öğretmen sorgusuna `RecurrenceExpander`'ı uygula** (öğrenci birleşik takviminde kullanılan kodun aynısı; kopyalama değil, ortak yardımcıyı çağır).
- [ ] **Step 4: Çakışma bayrağını hesapla ve yanıta ekle.**
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Mobil** — Öğretmen takviminde tekrar eden dersler görünür; öğrenci takviminde çakışan bireysel plan kartı uyarı rengiyle ve "Bu saatte özel dersin var" etiketiyle gösterilir.
- [ ] **Step 7: Doküman + commit**

`doc/modules/m04_scheduling.md` (iki açık madde `- [x]`), `doc/modules/m08_study.md` (çakışma maddesi), `doc/pages/scheduling.md`, `doc/pages/study_student.md`.
```bash
git add src/Modules/Scheduling mobile tests doc
git commit -m "feat(scheduling): ogretmen takviminde tekrar acilimi + cakisma uyarisi (M04-1/M04-2/M08-1)"
```

---

### Task 7: Ödev son teslim uyarısı ve değerlendirme (M06-3/M06-4)

**Files:**
- Create: `src/Modules/Assignments/Infrastructure/AssignmentDueScanner.cs` (BackgroundService)
- Modify: `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs` (`Grade` + `AssignmentDueSoonDomainEvent` + `AssignmentMissedDomainEvent`)
- Modify: `src/Modules/Assignments/Application/AssignmentTeacherFeatures.cs` (puanlama komutu)
- Modify: `src/Modules/Assignments/API/AssignmentsModule.cs`
- Modify: `src/Modules/Notifications/Infrastructure/*` (iki yeni tüketici)
- Test: `tests/Unit/AssignmentDueScannerTests.cs`, `tests/Unit/AssignmentGradeTests.cs`

**Interfaces:**
- Produces:
  - `Assignment.Grade(int score, string? feedback, DateTime nowUtc)` — 0–100; `Approved` durumundayken geçerli.
  - `AssignmentDueScanner` — saatte bir: son teslime **24 saat** kalan ve teslim edilmemiş ödevler için `AssignmentDueSoonDomainEvent` (ödev başına bir kez, `DueSoonNotifiedOnUtc` alanıyla); vadesi geçmiş ve teslim edilmemişler için `AssignmentMissedDomainEvent` (bir kez).
  - `POST /api/assignments/{assignmentId}/grade` (öğretmen)

- [ ] **Step 1: Testleri yaz (kırmızı)** — puan aralığı doğrulaması; scanner'ın aynı ödev için ikinci kez event üretmemesi; teslim edilmiş ödev için event üretmemesi.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~Assignment"`
- [ ] **Step 3: Domain + alanlar + migration**
  Run: `dotnet ef migrations add AddAssignmentGradeAndDueTracking --project src/Modules/Assignments/Infrastructure --startup-project src/API.Host --context AssignmentsDbContext`
- [ ] **Step 4: Scanner + puanlama komutu + endpoint + DI.**
- [ ] **Step 5: Notifications tüketicileri (M09-3)** — öğrenciye "Ödev teslimine 24 saat kaldı", öğrenci+veliye "Ödev teslim edilmedi" (tercih kapısı P05'ten geçer). Veli tarafı M09-3'ü kapatır.
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Mobil** — Öğretmen ödev detayında puan + geri bildirim alanı; öğrenci ödev kartında puan rozeti.
- [ ] **Step 8: Doküman + commit**

```bash
git add src/Modules mobile tests doc
git commit -m "feat(assignments): son teslim uyarisi + odev puanlama (M06-3/M06-4)"
```

---

### Task 8: Mobil — öğretmen uygunluk düzenleyici (D-04) ve M02-5

**Files:**
- Create: `mobile/lib/features/teacher_profile/presentation/widgets/availability_editor.dart`
- Modify: `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart`
- Modify: `mobile/lib/features/teacher_profile/data/repositories/teacher_repository_impl.dart`
- Test: `mobile/test/features/teacher_profile/availability_editor_test.dart`
- Modify: `doc/pages/teacher_profile.md`, `doc/architecture/widgets.md`

**Interfaces:**
- `AvailabilityEditor` widget'ı: haftanın 7 günü × saat aralıkları ızgarası; slot ekle/sil, her slotta "Online"/"Yüz yüze" anahtarları. Değişiklik `TeacherProfileCubit.saveAvailability(List<AvailabilitySlot>)` ile `PUT /api/teachers/profiles/{userId}` gövdesindeki `availabilitySlots` alanına yazılır (P01 Task 1'deki merge sayesinde tekrar kaydetme güvenli).

- [ ] **Step 1: Widget testini yaz (kırmızı)** — slot ekleme sonrası listede görünmesi, çakışan slot eklenememesi (aynı gün + kesişen saat), silme.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/teacher_profile/availability_editor_test.dart`
- [ ] **Step 3: Widget'ı yaz** — `doc/architecture/design_system.md` token'ları + mevcut ortak widget'lar; yeni renk/spacing icat etme.
- [ ] **Step 4: Profil sayfasına yerleştir + kaydetme akışını bağla.**
- [ ] **Step 5: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 6: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): ogretmen uygunluk duzenleyici (D-04/M02-5)"
```

---

### Task 9: Öğrenci profili tamamlayıcı uçlar (M03-1/M03-2/M03-3)

**Files:**
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs`
- Modify: `src/Modules/Students/Application/StudentProfilePolicies.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs`
- Modify: `src/Modules/Students/Infrastructure/{StudentProfileRepository,DependencyInjection}.cs`
- Modify: `mobile/lib/features/students/presentation/pages/student_detail_page.dart`
- Test: `tests/Unit/StudentProfileSubjectsTests.cs`, `tests/Unit/StudentParentLinkTests.cs`

**Interfaces:**
- Produces:
  - `POST /api/students/profiles/{studentId}/link-parent` — `{ parentUserId }`; **veli daima kayıtlı kullanıcıdır** (`students.parent_user_required`). Onay akışı Parents modülündeki `children/link` ile aynı kurala tabidir: bağ `Pending` başlar, öğrenci/öğretmen onaylar. (M03-1)
  - `POST /api/students/profiles/{studentId}/subjects` — `{ subject, targetLevel }` · `DELETE /api/students/profiles/{studentId}/subjects/{subjectId}` (M03-2)
  - `GET /api/students/profiles/by-parent/{parentUserId}` — velinin **onaylı** çocukları (`IParentAccessDirectory` ile doğrulanır) (M03-3)

- [ ] **Step 1: Testleri yaz (kırmızı)** — kayıtlı olmayan veli kimliğiyle bağlama reddediliyor; aynı branş iki kez eklenemiyor (`students.duplicate_subject`); `by-parent` yalnız **onaylı** bağları döndürüyor; başka velinin kimliğiyle sorgu 403.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentProfileSubjectsTests|FullyQualifiedName~StudentParentLinkTests"`
- [ ] **Step 3: Command/query + handler + authorizer + uçları yaz.**
- [ ] **Step 4: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 5: Mobil** — Öğrenci detayında "Branş ekle/çıkar" ve "Veli bağla" (veli e-postasıyla arama → bağlama) bölümleri.
- [ ] **Step 6: Doküman + commit**

`doc/modules/m03_students.md` (§ eksik uçlar `- [x]`), `doc/modules/00_genel_bakis.md` (Students bloğu 13 → 16 uç).
```bash
git add src/Modules/Students mobile tests doc
git commit -m "feat(students): veli baglama + brans yonetimi + veli listesi (M03-1/2/3)"
```

---

### Task 10: Beta hazırlığı — kapanış ve saha doğrulaması

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → başarısız 0, atlanan 0.
- [ ] **Step 2: Öğretmen günlük akış senaryosunu baştan sona elle koş**
  1. Kayıt → e-posta doğrulama (P02) → profil doldur + fotoğraf (P04) + uygunluk (Task 8)
  2. Öğrenci ekle → davet gönder → öğrenci kabul
  3. Haftalık tekrar eden ders planla → takvimde 4 hafta görünüyor (Task 6)
  4. Ders saatinde "Dersi başlat" → "Tamamla" → oturum notu + ödev (Task 3, Task 4)
  5. Ödev süresi geçince öğrenciye+veliye bildirim (Task 7 + P03)
  6. Ödeme kaydı otomatik oluştu (Task 5) → vade geçince "geciken" bildirimi
  Expected: her adım hatasız; kırılan adım varsa ilgili task'a geri dön.
- [ ] **Step 3: Dokümanlar** — `doc/roles/ogretmen.md` kontrol listesi; `doc/yol_haritasi.md` Faz 1 durumu; `doc/modules/00_genel_bakis.md` endpoint envanteri (Teachers 3 → 8, LessonSessions 4 → 7, Assignments → +1); `doc/denetim/2026-09-02_eksik_analizi.md` ilgili maddeler `✅ (P06)`.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P06 ogretmen MVP kapanisi"
```

---

## Kabul Kriterleri

- [ ] Admin öğretmeni doğrulayabiliyor; öğretmen kendini doğrulayamıyor
- [ ] `GET /api/teachers/profiles` filtreli, sayfalı ve yalnız aktif profilleri döndürüyor
- [ ] Oturum `Planned → InProgress → Completed` ve `→ Cancelled` geçişleri çalışıyor; ters zaman aralığı reddediliyor
- [ ] Planlı dersten tek çağrıyla oturum türetiliyor, ikinci çağrı yeni oturum açmıyor
- [ ] Vadesi geçen ödeme otomatik `Overdue` oluyor ve bildirim gidiyor
- [ ] Tamamlanan ücretli oturum için ödeme kaydı otomatik oluşuyor (tekrar eden event'te çift kayıt yok)
- [ ] Öğretmen takviminde tekrar eden dersler görünüyor
- [ ] Ödev son teslim uyarısı ve puanlama çalışıyor
- [ ] Mobilde uygunluk düzenlenip kaydedilebiliyor
- [ ] Tam test paketi (Docker'lı) yeşil
