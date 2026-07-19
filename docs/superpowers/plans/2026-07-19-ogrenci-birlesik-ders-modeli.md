# Öğrenci Birleşik Ders Modeli (Ç-06) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrencinin kendi dersini öğretmen dersiyle **tek `LessonSchedule` entity**'sinde (nullable `TeacherUserId`) tutmak, çalışma seansını bu derse bağlamak ve "planlandı → çalışıldı → atlandı" durumunu backend contract ile üretmek — böylece öğretmensiz "programla → çalış → analiz" döngüsü uçtan uca kapanır.

**Architecture:** Scheduling modülünde `LessonSchedule.TeacherUserId` nullable yapılır; ayrı `StudyScheduleEntry` entity'si buraya taşınıp kaldırılır (kendi ders = `TeacherUserId is null`). `Study.StudySession`'a nullable `LessonId` eklenir. Plan tamamlanma durumu, Study modülünün yayınladığı `IStudyPlanCompletionReader` contract'ıyla Scheduling'in birleşik takvim sorgusuna doldurulur. Mobil taraf katalog seçiciyi forma/kronometreye bağlar ve tamamlanma rozetini gösterir.

**Tech Stack:** .NET 9 modüler monolit (Clean Architecture + DDD + CQRS + Outbox), EF Core + PostgreSQL (şema-per-modül), FluentValidation; Flutter (flutter_bloc/Cubit, go_router, dio, get_it).

## Global Constraints

- **Görünen ad** her zaman **EğitimÜssü**; kod/dosya adı **EgitimUssu** (çift-t `EgittimUssu` yanlıştır).
- **.NET 9** (`global.json` SDK 9.0.311). Yeni NuGet paketi ekleme; mevcut yapıyı kullan.
- **Modüller birbirine doğrudan referans VERMEZ.** Modüller arası okuma yalnızca `src/Shared/Contracts/*` arayüzleriyle; yazma/iletişim domain event → Outbox → integration event.
- **Modüller arası FK YOK.** Diğer modülün kimliği daima **gevşek `Guid?`** olarak tutulur (mevcut desen: `LessonSession.LessonScheduleId`).
- **Enum'lar** DbContext'te `HasConversion<string>()` ile string sütun olarak saklanır.
- **Migration konumu:** `src/Modules/<Module>/Infrastructure/Migrations/`; her modülün `<Module>DesignTimeDbContextFactory.cs`'i vardır.
- **Ana renk** `0xFF082B4F`. Öğrenci ekranlarında `AppColors` token'ları kullanılır; sabit hex yazılmaz.
- **Mobil DI:** `mobile/lib/core/di/injector.dart` (get_it, lazy singleton). Repolar `AppConfig.isMockFallbackEnabled(feature)` ile mock/gerçek arası geçer; her yeni alan mock verisinde de taşınmalı.

## Test Stratejisi (kod gerçeği)

- **Backend:** Depoda **xUnit test projesi yok**. Bu plan Scheduling domain'i için tek bir minimal test projesi (`tests/EgitimUssu.Modules.Scheduling.Tests`) kurar (Görev A1) ve saf domain mantığını (self-lesson fabrikası, yetki, completion eşleme) TDD ile doğrular. **API/persistence katmanı** için doğrulama: `dotnet build` + `dotnet ef migrations` uygula + Swagger/curl smoke.
- **Mobil:** `mobile/test/` mevcut (widget/unit). Yeni davranışlar `flutter test` ile doğrulanır; ekranlar `flutter run` ile manuel teyit edilir (test cihazı: Honor 400 Pro, kablosuz ADB).

## Scope (üç bağımsız sevk edilebilir alt-plan)

Bu spec üç alt sistem içerir; **sırayla** uygulanır, her biri kendi başına derlenir/çalışır:

- **Plan A — Backend/Scheduling:** `LessonSchedule` birleşimi (nullable teacher) + `StudyScheduleEntry` göçü. (Görev A1–A9)
- **Plan B — Backend/Study+Scheduling:** `StudySession.LessonId` + `IStudyPlanCompletionReader` + takvim occurrence `Completed`. (Görev B1–B5)
- **Plan C — Mobil:** Katalog seçici + plandan-başlat + tamamlanma UI. (Görev C1–C7)
- **Faz 0 — Doküman uzlaştırma:** Diyagram/dokümanı bu fiziksel modele (birleşik `LessonSchedule`) çek. (Görev D1)

---

## File Structure

**Plan A (Scheduling):**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` — `LessonSchedule` içinde `TeacherUserId` → `Guid?`, `Topic`/`ColorHex` ekle, `ScheduledLessonFormat?`, `CreateSelfLesson` fabrikası, `IsSelfPlanned`.
- Delete: `src/Modules/Scheduling/Domain/StudyScheduleModel.cs` — entity+event'ler birleşince kaldırılır (son görevde).
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs` — `LessonScheduleConfiguration` yeni alanlar; `StudyScheduleEntries` DbSet + config kaldır.
- Create: `src/Modules/Scheduling/Infrastructure/Migrations/<ts>_UnifyLessonSchedule.cs` — alanlar + veri göçü + tablo drop.
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`, `LessonSchedulePolicies.cs` — self-lesson create/update/delete komut yolu + validasyon.
- Modify: `src/Modules/Scheduling/Application/StudyScheduleFeatures.cs`, `StudySchedulePolicies.cs` — birleşik create'e köprü, sonra kaldır.
- Modify: `src/Modules/Scheduling/Application/RecurrenceExpander.cs` çağıran birleşik takvim sorgusu (aynı dosyada `GetStudentCalendarQuery`).
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` — `/students/{id}/study-entries` rotalarını birleşik create'e yönlendir; occurrence `source`/`isEditable` türet.
- Modify: `src/Modules/Scheduling/Application/*Authorizer*.cs` — öğrenci self-lesson yetkisi.
- Create: `tests/EgitimUssu.Modules.Scheduling.Tests/EgitimUssu.Modules.Scheduling.Tests.csproj` + test dosyaları.

**Plan B (Study + Shared):**
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` — `StudySession.LessonId (Guid?)`, `StartStopwatch`/`CreateManual` imzasına ekle.
- Modify: `src/Modules/Study/Infrastructure/StudyDbContext.cs` — `LessonId` property + index.
- Create: `src/Modules/Study/Infrastructure/Migrations/<ts>_AddLessonIdToStudySession.cs`.
- Modify: `src/Modules/Study/API/StudyModule.cs` — `POST /sessions/start` + `/sessions/manual` payload'una `lessonId`,`subjectId`,`topicId`.
- Create: `src/Shared/Contracts/StudyPlanCompletionContract.cs` — `IStudyPlanCompletionReader` + `PlanCompletion` record.
- Create: `src/Modules/Study/Application/StudyPlanCompletionReader.cs` — contract implementasyonu (tamamlanmış seanslardan (LessonId, tarih)).
- Modify: `src/Modules/Study/API/StudyModule.cs` (DI kaydı) — reader'ı register et.
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (`GetStudentCalendarQuery`) — reader ile `Completed` doldur.
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` — occurrence DTO'suna `completed`.

**Plan C (Mobil):**
- Modify: `mobile/lib/features/scheduling/domain/scheduling_contracts.dart` — `CalendarOccurrence` + `bool completed`.
- Modify: `mobile/lib/features/scheduling/data/models/*calendar*` — `completed` map.
- Modify: `mobile/lib/features/study/domain/study_contracts.dart` — `startSession(... String? lessonId, String? subjectId, String? topicId)`.
- Modify: `mobile/lib/features/study/data/repositories/study_repository_impl.dart` — payload + mock.
- Modify: `mobile/lib/features/study/presentation/pages/study_timer_page.dart` — `_StartForm` katalog seçici + `lessonId`.
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart` — `StudyEntryFormSheet` katalog seçici + occurrence tamamlanma rozeti.
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart` — "Bugünün planı" kartı.
- Create/Modify: `mobile/test/features/study/*_test.dart`.

**Faz 0 (Doküman):**
- Modify: `doc/diagrams/rol_sayfa_mimarisi/ogrenci.md` §1.2 + veri modeli; SVG yeniden üret.
- Modify: `doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md` §5.2.

---

## Faz 0 — Doküman uzlaştırma

### Task D1: Diyagram/dokümanı birleşik `LessonSchedule` modeline çek

**Files:**
- Modify: `doc/diagrams/rol_sayfa_mimarisi/ogrenci.md` (§1.2 kavramsal model + veri modeli bloğu)
- Modify: `doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md` (§5.2)

- [ ] **Step 1:** `ogrenci.md` §1.2 veri modeli bloğunu gerçek fiziğe göre değiştir:

```
LessonSchedule            (Scheduling modülü — tek entity)
  ├─ TeacherUserId  Guid? NULLABLE   ← null = öğrencinin kendi dersi
  ├─ StudentId      required
  ├─ Subject, Topic?, Start/End, TimeZone, RecurrenceRule?, Status, ColorHex?
  └─ (öğretmenliyse) LessonFormat, LocationLabel, MeetingUrl
StudySession (Study modülü)
  └─ LessonId       Guid? NULLABLE   ← derse bağlı ya da serbest
CalendarOccurrence  (okuma modeli) → source = TeacherUserId is null ? "Self" : "Teacher"
```

- [ ] **Step 2:** `ogrenci_rolu_fonksiyonel_dokuman_v1.md` §5.2'de "iki-entity" ifadesini "birleşik `LessonSchedule` (nullable TeacherUserId)" olarak güncelle; StudyScheduleEntry'nin buraya taşındığını not düş.

- [ ] **Step 3:** SVG'leri yeniden üret ve doğrula:

Run:
```bash
cd doc/diagrams/rol_sayfa_mimarisi
echo '{"args":["--no-sandbox","--disable-setuid-sandbox"]}' > /tmp/_pptr.json
rm -f svg/ogrenci/*.svg
npx -y @mermaid-js/mermaid-cli -i ogrenci.md -o svg/ogrenci/d.svg -p /tmp/_pptr.json
ls svg/ogrenci
```
Expected: `d-1.svg … d-9.svg` üretilir; ardından §-önceki adlandırma şemasıyla yeniden adlandır (bkz. README).

- [ ] **Step 4: Commit**

```bash
git add doc/diagrams/rol_sayfa_mimarisi/ogrenci.md doc/diagrams/rol_sayfa_mimarisi/svg/ogrenci doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md
git commit -m "docs(ogrenci): birleşik LessonSchedule modeline uzlaştır (Ç-06)"
```

---

## Plan A — Backend / Scheduling: Birleşik LessonSchedule

### Task A1: Scheduling domain test projesini kur

**Files:**
- Create: `tests/EgitimUssu.Modules.Scheduling.Tests/EgitimUssu.Modules.Scheduling.Tests.csproj`
- Create: `tests/EgitimUssu.Modules.Scheduling.Tests/LessonScheduleTests.cs`

**Interfaces:**
- Produces: xUnit test host — sonraki A görevleri buraya test ekler.

- [ ] **Step 1: Test projesini oluştur ve domain'e referans ver**

```bash
cd C:/Users/furkan.gokdemir/Projects/EgitimUssu
dotnet new xunit -n EgitimUssu.Modules.Scheduling.Tests -o tests/EgitimUssu.Modules.Scheduling.Tests
dotnet add tests/EgitimUssu.Modules.Scheduling.Tests reference src/Modules/Scheduling/Domain/EgitimUssu.Modules.Scheduling.Domain.csproj
dotnet sln add tests/EgitimUssu.Modules.Scheduling.Tests
```

- [ ] **Step 2: Geçici duman testi yaz** — `LessonScheduleTests.cs`:

```csharp
using EgitimUssu.Modules.Scheduling.Domain;
using Xunit;

public class LessonScheduleTests
{
    [Fact]
    public void Smoke() => Assert.True(true);
}
```

- [ ] **Step 3: Derle ve çalıştır**

Run: `dotnet test tests/EgitimUssu.Modules.Scheduling.Tests`
Expected: PASS (1 test).

- [ ] **Step 4: Commit**

```bash
git add tests/EgitimUssu.Modules.Scheduling.Tests EgitimUssu.slnx
git commit -m "test(scheduling): domain test projesi iskeleti"
```

### Task A2: `LessonSchedule`'a self-lesson yeteneği (TDD)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`LessonSchedule`)
- Test: `tests/EgitimUssu.Modules.Scheduling.Tests/LessonScheduleTests.cs`

**Interfaces:**
- Produces:
  - `LessonSchedule.TeacherUserId` → `Guid?`
  - `string? Topic`, `string? ColorHex`, `ScheduledLessonFormat? LessonFormat`
  - `bool IsSelfPlanned => TeacherUserId is null`
  - `static LessonSchedule CreateSelfLesson(Guid id, Guid studentId, string subject, string? topic, DateTime startAtUtc, DateTime endAtUtc, string timeZone, string? recurrenceRule, int reminderOffsetMinutes, string? colorHex, string? notes, DateTime createdOnUtc)` — `Status=Planned`, `TeacherUserId=null`, `Raise(LessonScheduledDomainEvent(...))`.

- [ ] **Step 1: Failing test yaz** — `LessonScheduleTests.cs` içine:

```csharp
[Fact]
public void CreateSelfLesson_sets_null_teacher_and_planned_status()
{
    var studentId = Guid.NewGuid();
    var start = new DateTime(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    var lesson = LessonSchedule.CreateSelfLesson(
        id: Guid.NewGuid(), studentId: studentId,
        subject: "Matematik", topic: "Türev",
        startAtUtc: start, endAtUtc: start.AddMinutes(60),
        timeZone: "Europe/Istanbul", recurrenceRule: null,
        reminderOffsetMinutes: 30, colorHex: "#20A4A9",
        notes: null, createdOnUtc: start);

    Assert.Null(lesson.TeacherUserId);
    Assert.True(lesson.IsSelfPlanned);
    Assert.Equal(studentId, lesson.StudentId);
    Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
    Assert.Equal("Türev", lesson.Topic);
    Assert.Contains(lesson.DomainEvents, e => e is LessonScheduledDomainEvent);
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `dotnet test tests/EgitimUssu.Modules.Scheduling.Tests --filter CreateSelfLesson_sets_null_teacher_and_planned_status`
Expected: FAIL (derleme hatası: `CreateSelfLesson` / `Topic` / `IsSelfPlanned` yok).

- [ ] **Step 3: `LessonSchedule`'ı güncelle** — `SchedulingDomainModel.cs`'te:
  1. `public Guid TeacherUserId` → `public Guid? TeacherUserId` yap.
  2. `public ScheduledLessonFormat LessonFormat` → `public ScheduledLessonFormat? LessonFormat`.
  3. Yeni property'ler ekle: `public string? Topic { get; private set; }` ve `public string? ColorHex { get; private set; }` ve `public bool IsSelfPlanned => TeacherUserId is null;`.
  4. Fabrikayı ekle (mevcut öğretmen constructor'ı bozmadan):

```csharp
public static LessonSchedule CreateSelfLesson(
    Guid id, Guid studentId, string subject, string? topic,
    DateTime startAtUtc, DateTime endAtUtc, string timeZone,
    string? recurrenceRule, int reminderOffsetMinutes,
    string? colorHex, string? notes, DateTime createdOnUtc)
{
    var lesson = new LessonSchedule
    {
        Id = id,
        TeacherUserId = null,
        StudentId = studentId,
        Subject = subject,
        Topic = topic,
        LessonFormat = null,
        StartAtUtc = startAtUtc,
        EndAtUtc = endAtUtc,
        TimeZone = timeZone,
        RecurrenceRule = recurrenceRule,
        ReminderOffsetMinutes = reminderOffsetMinutes,
        ColorHex = colorHex,
        Notes = notes,
        Status = LessonScheduleStatus.Planned,
        IsChargeable = false,
        CreatedOnUtc = createdOnUtc,
        UpdatedOnUtc = createdOnUtc,
    };
    lesson.Raise(new LessonScheduledDomainEvent(
        lesson.Id, null, lesson.StudentId, lesson.Subject,
        lesson.StartAtUtc, lesson.EndAtUtc, lesson.ReminderOffsetMinutes, createdOnUtc));
    return lesson;
}
```

  5. `LessonScheduledDomainEvent` kaydına nullable `Guid? TeacherUserId` parametresini ekle (öğretmen constructor'ındaki çağrıyı da güncelle: teacher id'yi geç).

> Not: `LessonSchedule`'ın private parametresiz ctor'u yoksa ekle (`private LessonSchedule() { }`); mevcut öğretmen ctor'u korunur.

- [ ] **Step 4: Testin geçtiğini gör**

Run: `dotnet test tests/EgitimUssu.Modules.Scheduling.Tests --filter CreateSelfLesson_sets_null_teacher_and_planned_status`
Expected: PASS.

- [ ] **Step 5: Tüm çözümün derlendiğini doğrula**

Run: `dotnet build`
Expected: 0 error (Notifications handler'ı `LessonScheduledDomainEvent`'in yeni imzasına göre güncellenene kadar hata verirse Task A6'ya kadar geçici olarak eski imzayı koruyup handler'ı A6'da güncelle — bu görevi küçük tutmak için event imzasını Step 3.5'te değiştirdiysen A6'yı hemen ardından yap).

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Scheduling/Domain/SchedulingDomainModel.cs tests/EgitimUssu.Modules.Scheduling.Tests/LessonScheduleTests.cs
git commit -m "feat(scheduling): LessonSchedule self-lesson (nullable teacher) fabrikası"
```

### Task A3: DbContext konfigürasyonu — yeni alanlar

**Files:**
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs` (`LessonScheduleConfiguration`)

- [ ] **Step 1:** `LessonScheduleConfiguration.Configure` içine ekle:

```csharp
builder.Property(entity => entity.Topic).HasMaxLength(160);
builder.Property(entity => entity.ColorHex).HasMaxLength(16);
// LessonFormat artık nullable — .IsRequired() KALDIR:
builder.Property(entity => entity.LessonFormat).HasConversion<string>().HasMaxLength(32);
// Kendi dersleri sorgulamak için:
builder.HasIndex(entity => new { entity.StudentId, entity.TeacherUserId, entity.StartAtUtc });
```

- [ ] **Step 2: Derle**

Run: `dotnet build src/Modules/Scheduling/Infrastructure`
Expected: 0 error.

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs
git commit -m "feat(scheduling): LessonSchedule Topic/ColorHex/nullable format konfig"
```

### Task A4: Self-lesson komut yolu (Application) + validasyon

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`
- Modify: `src/Modules/Scheduling/Application/LessonSchedulePolicies.cs`
- Test: `tests/EgitimUssu.Modules.Scheduling.Tests/SelfLessonPolicyTests.cs`

**Interfaces:**
- Produces: `CreateSelfLessonCommand(Guid StudentId, string Subject, string? Topic, DateTime StartAtUtc, DateTime EndAtUtc, string TimeZone, string? RecurrenceRule, int ReminderOffsetMinutes, string? ColorHex, string? Notes)` + handler → `LessonSchedule.CreateSelfLesson(...)` kaydeder. Öğretmen dersiyle **saat çakışması** aynı `StudentId` için kontrol edilir (mevcut `study-entries` davranışı korunur).

- [ ] **Step 1: Failing validation test** — `SelfLessonPolicyTests.cs`:

```csharp
using EgitimUssu.Modules.Scheduling.Application;
using Xunit;

public class SelfLessonPolicyTests
{
    [Fact]
    public void Validator_rejects_empty_student_and_reversed_time()
    {
        var v = new CreateSelfLessonCommandValidator();
        var start = System.DateTime.UtcNow;
        var cmd = new CreateSelfLessonCommand(
            System.Guid.Empty, "", null, start, start.AddMinutes(-10),
            "Europe/Istanbul", null, 30, null, null);
        var result = v.Validate(cmd);
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 2: FAIL gör** — Run: `dotnet test tests/EgitimUssu.Modules.Scheduling.Tests --filter Validator_rejects_empty_student_and_reversed_time` → FAIL (tip yok).

- [ ] **Step 3:** `LessonScheduleFeatures.cs`'te `CreateSelfLessonCommand` + handler ekle (mevcut `StudyScheduleFeatures.CreateStudyScheduleEntry` mantığını temel al: çakışma kontrolü + `SchedulingDbContext.LessonSchedules.Add(LessonSchedule.CreateSelfLesson(...))`). `LessonSchedulePolicies.cs`'e `CreateSelfLessonCommandValidator` ekle: `StudentId != Guid.Empty`, `Subject` boş değil (≤120), `EndAtUtc > StartAtUtc`, süre 15dk–8saat.

- [ ] **Step 4: PASS gör** — Run: aynı filtre → PASS. Sonra `dotnet build` → 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Scheduling/Application/LessonScheduleFeatures.cs src/Modules/Scheduling/Application/LessonSchedulePolicies.cs tests/EgitimUssu.Modules.Scheduling.Tests/SelfLessonPolicyTests.cs
git commit -m "feat(scheduling): CreateSelfLesson komutu + validasyon"
```

### Task A5: Yetkilendirme — öğrenci kendi dersini yönetebilir

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonSchedulePolicies.cs` (authorizer)
- Test: `tests/EgitimUssu.Modules.Scheduling.Tests/SelfLessonAuthorizationTests.cs`

**Interfaces:**
- Produces: `CreateSelfLessonCommand` için yetki kuralı — `IsAuthenticated && (Admin || (Student && currentUserId == command.StudentId'nin sahibi))`. Öğretmen dersi yolları (`CreateLessonScheduleCommand`) değişmez.

- [ ] **Step 1: Failing test** — kimliksiz kullanıcı reddedilir, sahip öğrenci kabul edilir (mevcut `CurrentUser` sahtesini kullanarak). FAIL gör.
- [ ] **Step 2:** Authorizer'a `Authorize(CreateSelfLessonCommand)` ekle; `CanManageStudent(studentId)` yardımcı metodu (Admin veya kendi öğrenci profili). `IStudentDirectory` ile sahiplik doğrula (mevcut IDOR koruması deseni).
- [ ] **Step 3: PASS gör** + `dotnet build`.
- [ ] **Step 4: Commit** — `git commit -m "feat(scheduling): self-lesson yetkilendirme (sahip öğrenci/admin)"`

### Task A6: Domain event birleşimi + Notifications handler

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/LessonScheduleNotificationIntegrationEventHandler.cs`
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (öğretmen ctor'ında `LessonScheduledDomainEvent` çağrısı yeni imzaya)

**Interfaces:**
- Consumes: `LessonScheduledDomainEvent(Guid LessonId, Guid? TeacherUserId, Guid StudentId, string Subject, DateTime StartAtUtc, DateTime EndAtUtc, int ReminderOffsetMinutes, DateTime CreatedOnUtc)`.

- [ ] **Step 1:** Öğretmen ders oluşturma yolunun `LessonScheduledDomainEvent`'i yeni imzayla (teacher id dolu) yaydığını doğrula/güncelle.
- [ ] **Step 2:** Notifications handler'ı derlensin diye yeni alanlara uyarla; kendi ders (teacher null) için de yaklaşan-ders hatırlatması üretsin (öğrenciye). `StudyScheduleEntryScheduledDomainEvent` dinleyen kod varsa `LessonScheduledDomainEvent`'e taşı.
- [ ] **Step 3:** `dotnet build` → 0 error.
- [ ] **Step 4: Commit** — `git commit -m "feat: kendi ders hatırlatmaları LessonScheduled üzerinden"`

### Task A7: Birleşik takvim sorgusu + occurrence source türetme

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (`GetStudentCalendarQuery` handler)
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (occurrence DTO map)

**Interfaces:**
- Produces: `GetStudentCalendarQuery` artık **yalnız `lesson_schedules`**'ten okur (öğretmen + self). Her occurrence: `Source = row.TeacherUserId is null ? "Self" : "Teacher"`, `IsEditable = row.TeacherUserId is null`, `ColorHex`, `Topic` aktarılır. Tekrarlar `RecurrenceExpander` ile genişletilir (mevcut mantık).

- [ ] **Step 1:** Sorguyu tek kaynağa indir: `StudyScheduleEntries` join'ini kaldır; `LessonSchedules.Where(l => l.StudentId == studentId && (aralık))`.
- [ ] **Step 2:** DTO map'inde `source`/`isEditable`/`colorHex`/`topic` türet.
- [ ] **Step 3:** `dotnet build` → 0 error.
- [ ] **Step 4: Smoke** — API'yi çalıştır, mevcut bir öğrenci için `GET /api/scheduling/students/{id}/calendar?startAtUtc=...&endAtUtc=...` çağır; öğretmen dersleri `Teacher`, (varsa göç edilmiş) kendi girdiler `Self` gelmeli.

Run: `dotnet run --project src/API.Host` (ayrı terminal) sonra:
```bash
curl -s "http://localhost:5296/api/scheduling/students/<id>/calendar?startAtUtc=2026-07-01T00:00:00Z&endAtUtc=2026-07-31T00:00:00Z" -H "Authorization: Bearer <token>"
```
Expected: JSON occurrence listesi; `source` alanları doğru.

- [ ] **Step 5: Commit** — `git commit -m "feat(scheduling): takvim tek kaynak (lesson_schedules) + source türetme"`

### Task A8: `/students/{id}/study-entries` rotalarını birleşik create'e köprüle

**Files:**
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`

**Interfaces:**
- Consumes: `CreateSelfLessonCommand`. Mobil geriye uyum için rota adları korunur:
  - `POST /students/{studentId}/study-entries` → `CreateSelfLessonCommand`
  - `PUT /study-entries/{entryId}` → mevcut `LessonSchedule` update (self, teacher null) yolu
  - `DELETE /study-entries/{entryId}` → self lesson cancel/delete

- [ ] **Step 1:** Bu üç endpoint'in handler'ını `StudyScheduleEntry` yerine `LessonSchedule` (self) komutlarına bağla.
- [ ] **Step 2:** `dotnet build` → 0 error.
- [ ] **Step 3: Smoke** — `POST /api/scheduling/students/{id}/study-entries` ile kendi ders oluştur; `GET .../calendar`'da `source=Self`, `isEditable=true` görün.
- [ ] **Step 4: Commit** — `git commit -m "feat(scheduling): study-entries rotaları birleşik self-lesson'a"`

### Task A9: `StudyScheduleEntry` göçü + kaldırma (migration)

**Files:**
- Create: `src/Modules/Scheduling/Infrastructure/Migrations/<ts>_UnifyLessonSchedule.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs` (DbSet + config kaldır)
- Delete: `src/Modules/Scheduling/Domain/StudyScheduleModel.cs`, `Application/StudyScheduleFeatures.cs`, `StudySchedulePolicies.cs`

- [ ] **Step 1:** DbContext'ten `StudyScheduleEntries` DbSet'i ve `StudyScheduleEntryConfiguration`'ı kaldır.
- [ ] **Step 2:** `lesson_schedules` yeni sütunları için migration üret:

```bash
dotnet ef migrations add UnifyLessonSchedule \
  --project src/Modules/Scheduling/Infrastructure \
  --startup-project src/API.Host \
  --context SchedulingDbContext \
  --output-dir Infrastructure/Migrations
```

- [ ] **Step 3:** Üretilen migration'ın `Up`'ına **veri göçü** SQL'i ekle (kolonlar eklendikten sonra, `study_schedule_entries` drop'undan önce):

```csharp
migrationBuilder.Sql(@"
INSERT INTO scheduling.lesson_schedules
  (""Id"",""TeacherUserId"",""StudentId"",""Subject"",""Topic"",""LessonFormat"",
   ""StartAtUtc"",""EndAtUtc"",""TimeZone"",""RecurrenceRule"",""Status"",
   ""ReminderOffsetMinutes"",""ColorHex"",""Notes"",""IsChargeable"",
   ""CreatedOnUtc"",""UpdatedOnUtc"")
SELECT ""Id"", NULL, ""StudentId"", ""Subject"", ""Topic"", NULL,
       ""StartAtUtc"", ""EndAtUtc"", ""TimeZone"", ""RecurrenceRule"",
       CASE WHEN ""Status""='Active' THEN 'Planned' ELSE 'Cancelled' END,
       ""ReminderOffsetMinutes"", ""ColorHex"", ""Notes"", false,
       ""CreatedOnUtc"", ""UpdatedOnUtc""
FROM scheduling.study_schedule_entries;");
```

- [ ] **Step 4:** Migration'ın sonunda tabloyu düşür: `migrationBuilder.DropTable(name: "study_schedule_entries", schema: "scheduling");` (EF bunu DbSet kaldırıldığı için zaten ekleyebilir — yoksa elle ekle; `Down`'da geri oluştur).
- [ ] **Step 5:** Domain/Application StudySchedule* dosyalarını sil; `dotnet build` → 0 error.
- [ ] **Step 6: Migration'ı uygula**

Run:
```bash
dotnet ef database update --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext
```
Expected: başarıyla uygulanır; `study_schedule_entries` satırları `lesson_schedules`'e taşınmış, tablo düşmüş.

- [ ] **Step 7: Smoke** — `GET .../calendar` göç edilmiş kendi dersleri `source=Self` döndürür.
- [ ] **Step 8: Commit** — `git commit -m "feat(scheduling)!: StudyScheduleEntry -> LessonSchedule göçü + tablo kaldırma"`

---

## Plan B — Backend: Seans↔Ders bağı + tamamlanma contract'ı

### Task B1: `StudySession.LessonId` ekle (domain + config + migration)

**Files:**
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` (`StudySession`)
- Modify: `src/Modules/Study/Infrastructure/StudyDbContext.cs`
- Create: `src/Modules/Study/Infrastructure/Migrations/<ts>_AddLessonIdToStudySession.cs`

**Interfaces:**
- Produces: `StudySession.LessonId (Guid?)`; `StartStopwatch(...)` ve `CreateManual(...)` fabrikalarına opsiyonel `Guid? lessonId` parametresi (gevşek referans; FK yok).

- [ ] **Step 1:** `StudySession`'a `public Guid? LessonId { get; private set; }` ekle; `StartStopwatch`/`CreateManual` imzalarına `Guid? lessonId = null` ekleyip ata.
- [ ] **Step 2:** `StudyDbContext` `StudySession` config'ine `builder.HasIndex(e => e.LessonId);` ekle.
- [ ] **Step 3:** Migration üret + uygula:

```bash
dotnet ef migrations add AddLessonIdToStudySession --project src/Modules/Study/Infrastructure --startup-project src/API.Host --context StudyDbContext --output-dir Infrastructure/Migrations
dotnet ef database update --project src/Modules/Study/Infrastructure --startup-project src/API.Host --context StudyDbContext
```
Expected: `study.study_sessions` tablosuna `LessonId uuid null` kolonu eklenir.

- [ ] **Step 4:** `dotnet build` → 0 error.
- [ ] **Step 5: Commit** — `git commit -m "feat(study): StudySession.LessonId (plana bağ)"`

### Task B2: Seans başlatma endpoint'lerine `lessonId`/`subjectId`/`topicId`

**Files:**
- Modify: `src/Modules/Study/API/StudyModule.cs` (`POST /sessions/start`, `/sessions/manual`)
- Modify: `src/Modules/Study/Application/*` (start/manual command + handler)

**Interfaces:**
- Produces: start/manual request DTO'ları opsiyonel `lessonId`, `subjectId`, `topicId` alır; handler `StudySession.StartStopwatch(..., lessonId)` çağırır; `subjectId/topicId` verilirse katalogdan ad çözülüp `Subject/Topic` denormalize edilir (isim tutarlılığı).

- [ ] **Step 1:** Start/manual command'lerine alanları ekle; handler'da `lessonId`'yi entity'ye geçir. `subjectId` verilirse `StudentSubjectCatalog`'tan `Name` oku, `Subject`'e yaz (yoksa gelen string subject).
- [ ] **Step 2:** `dotnet build` → 0 error.
- [ ] **Step 3: Smoke** — `POST /api/study/sessions/start` gövdesine `lessonId` koyup çağır; `GET /students/{id}/sessions`'da seansın `lessonId`'si dolu görün.
- [ ] **Step 4: Commit** — `git commit -m "feat(study): seans başlatmada lessonId/subjectId/topicId"`

### Task B3: `IStudyPlanCompletionReader` contract'ı (Shared)

**Files:**
- Create: `src/Shared/Contracts/StudyPlanCompletionContract.cs`

**Interfaces:**
- Produces:

```csharp
namespace EgitimUssu.Shared.Contracts;

public sealed record PlanCompletion(Guid LessonId, DateOnly Date);

public interface IStudyPlanCompletionReader
{
    Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(
        Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
```

- [ ] **Step 1:** Dosyayı oluştur (yukarıdaki içerik). `Shared/Contracts` csproj'una başka referans gerekmez.
- [ ] **Step 2:** `dotnet build src/Shared/Contracts` → 0 error.
- [ ] **Step 3: Commit** — `git commit -m "feat(contracts): IStudyPlanCompletionReader"`

### Task B4: Study tarafında reader implementasyonu + DI

**Files:**
- Create: `src/Modules/Study/Application/StudyPlanCompletionReader.cs`
- Modify: `src/Modules/Study/API/StudyModule.cs` (DI kaydı)

**Interfaces:**
- Consumes: `IStudyPlanCompletionReader`, `StudyDbContext`.
- Produces: tamamlanmış (`Status=Completed`) ve `LessonId != null` seanslardan `(LessonId, Date=StartedAtUtc.Date)` kümesi.

- [ ] **Step 1:** Reader'ı yaz:

```csharp
public sealed class StudyPlanCompletionReader : IStudyPlanCompletionReader
{
    private readonly StudyDbContext _db;
    public StudyPlanCompletionReader(StudyDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(
        Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rows = await _db.StudySessions
            .Where(s => s.StudentId == studentId
                && s.LessonId != null
                && s.Status == StudySessionStatus.Completed
                && s.StartedAtUtc >= from && s.StartedAtUtc < to)
            .Select(s => new { s.LessonId, s.StartedAtUtc })
            .ToListAsync(ct);
        return rows
            .Select(r => new PlanCompletion(r.LessonId!.Value, DateOnly.FromDateTime(r.StartedAtUtc)))
            .Distinct().ToList();
    }
}
```

- [ ] **Step 2:** `StudyModule` DI'ında `services.AddScoped<IStudyPlanCompletionReader, StudyPlanCompletionReader>();` kaydet.
- [ ] **Step 3:** `dotnet build` → 0 error.
- [ ] **Step 4: Commit** — `git commit -m "feat(study): StudyPlanCompletionReader + DI"`

### Task B5: Takvim occurrence'a `Completed` doldur

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (`GetStudentCalendarQuery` handler ctor'una `IStudyPlanCompletionReader`)
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (occurrence DTO `completed`)

**Interfaces:**
- Consumes: `IStudyPlanCompletionReader`.
- Produces: `CalendarOccurrence.Completed (bool)` — occurrence `(entryId==LessonId, occurrence tarihi)` completion kümesinde varsa `true`.

- [ ] **Step 1:** Handler ctor'una reader'ı enjekte et; sorgu aralığı için `GetCompletionsAsync` çağır; genişletilmiş her occurrence için `Completed = set.Contains((occ.EntryId, occ.Start.Date))`.
- [ ] **Step 2:** Occurrence DTO'suna `completed` alanı ekle (API map).
- [ ] **Step 3:** `dotnet build` → 0 error.
- [ ] **Step 4: Smoke** — Bir kendi dersine bağlı seansı `complete` et; `GET .../calendar`'da o günkü occurrence `completed:true`.
- [ ] **Step 5: Commit** — `git commit -m "feat(scheduling): occurrence.completed (StudyPlanCompletionReader ile)"`

---

## Plan C — Mobil: Katalog tutkalı + plandan-başlat + tamamlanma

### Task C1: `CalendarOccurrence`/`StudySession` sözleşmelerine yeni alanlar

**Files:**
- Modify: `mobile/lib/features/scheduling/domain/scheduling_contracts.dart`
- Modify: `mobile/lib/features/scheduling/data/models/` (calendar occurrence model)
- Modify: `mobile/lib/features/study/domain/study_contracts.dart` (`StudySession` + repo imzası)

- [ ] **Step 1:** `CalendarOccurrence`'a `final bool completed;` ekle (ctor + `fromJson`, `json['completed'] as bool? ?? false`).
- [ ] **Step 2:** `study_contracts.dart` `StudySession`'a `final String? lessonId;` ekle; `StudyRepository.startSession(...)` imzasına `String? lessonId, String? subjectId, String? topicId` ekle.
- [ ] **Step 3:** `flutter analyze` → 0 error (imza değişince çağıranlar geçici derlenmeyebilir; C2'de düzeltilir — bu görevde yalnız sözleşme+model).
- [ ] **Step 4: Commit** — `git commit -m "feat(mobile): occurrence.completed + startSession lessonId alanları"`

### Task C2: Repo — payload + mock güncelle

**Files:**
- Modify: `mobile/lib/features/study/data/repositories/study_repository_impl.dart`

- [ ] **Step 1:** `startSession`/`startManual` gövdesine `if (lessonId != null) 'lessonId': lessonId`, `subjectId`, `topicId` ekle. Mock dalında dönen `StudySession`'a `lessonId` taşınsın.
- [ ] **Step 2:** `flutter analyze` → 0 error.
- [ ] **Step 3: Commit** — `git commit -m "feat(mobile): startSession payload lessonId/subjectId/topicId"`

### Task C3: Kronometre başlatma formunda katalog seçici (TDD)

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/study_timer_page.dart` (`_StartForm`)
- Test: `mobile/test/features/study/start_form_catalog_test.dart`

**Interfaces:**
- Consumes: `StudyRepository.listSubjects(studentId)` → `List<SubjectCatalog>`.
- Produces: ders seçilince o dersin `topics`'i konu dropdown'ına dolar; başlatınca `subjectId`/`topicId` (+ varsa `lessonId`) geçilir.

- [ ] **Step 1:** Widget test yaz (`start_form_catalog_test.dart`): sahte repo iki ders + konu döndürsün; formu pump et, ders seç, konu dropdown'ının dolduğunu assert et. FAIL gör (`flutter test .../start_form_catalog_test.dart`).
- [ ] **Step 2:** `_StartForm`'da serbest metin `AppTextField` yerine `listSubjects`'ten beslenen `DropdownButtonFormField` (ders) + bağımlı konu dropdown'ı; "Serbest çalışma" seçeneği kalsın (subjectId null).
- [ ] **Step 3:** PASS gör.
- [ ] **Step 4: Commit** — `git commit -m "feat(mobile): kronometre başlatmada katalog seçici"`

### Task C4: Takvim formunda (StudyEntryFormSheet) katalog seçici

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart` (`StudyEntryFormSheet`)

- [ ] **Step 1:** `_subjectController`/`_topicController` serbest metnini katalog dropdown'ıyla değiştir (satır-içi "yeni ders/konu ekle" → `createSubject`/`addTopic`). Seçilen ismi `subject`/`topic` olarak `createStudyEntry`'ye geçir (backend denormalize saklar).
- [ ] **Step 2:** `flutter analyze` → 0 error; `flutter test` yeşil.
- [ ] **Step 3: Commit** — `git commit -m "feat(mobile): takvim ders formunda katalog seçici"`

### Task C5: "Bugünün planı" kartı (Çalış açılış)

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart`

**Interfaces:**
- Consumes: `SchedulingRepository.getStudentCalendar(studentId, bugün 00:00, 23:59)`.
- Produces: bugünkü occurrence listesi; her satır → dokun → `/study/timer?studentId=...&lessonId=<entryId>&subject=<subject>&topic=<topic>`.

- [ ] **Step 1:** Ana sayfaya `_TodayPlan` bölümü ekle: bugünkü occurrence'ları çek, `completed` ise ✓ göster, değilse "başlat" aksiyonu.
- [ ] **Step 2:** `flutter analyze` → 0 error.
- [ ] **Step 3: Commit** — `git commit -m "feat(mobile): Çalış açılışında 'Bugünün planı'"`

### Task C6: Timer'ı plandan başlatınca `lessonId` taşı

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/study_timer_page.dart`
- Modify: `mobile/lib/features/study/presentation/cubit/study_timer_cubit.dart`
- Modify: `mobile/lib/core/routing/app_router.dart` (`/study/timer` query `lessonId`)

- [ ] **Step 1:** `/study/timer` rotasına `lessonId` query paramı ekle; `StudyTimerCubit.start`'a `lessonId` geçir → `startSession(..., lessonId: ...)`.
- [ ] **Step 2:** `flutter analyze` → 0 error.
- [ ] **Step 3: Commit** — `git commit -m "feat(mobile): plandan başlatılan seans lessonId taşır"`

### Task C7: Takvimde tamamlanma rozeti

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart` (`_OccurrenceDataSource`, `_TimePill`)

- [ ] **Step 1:** `occ.completed == true` ise occurrence kartına ✓ "çalışıldı" rozeti; geçmiş + `!completed` + `source=='Self'` ise ○ "atlandı" rozeti.
- [ ] **Step 2:** `flutter analyze` + `flutter test` yeşil; `flutter run` ile manuel teyit (Honor 400 Pro).
- [ ] **Step 3: Commit** — `git commit -m "feat(mobile): takvim occurrence tamamlanma rozeti"`

---

## Kabul Kriterleri (uçtan uca — kullanıcı senaryosu)

- [ ] Öğrenci öğretmensiz kaydolur, **katalogdan** ders+konu ekler (`/study/catalog`).
- [ ] Takvimde **kendi dersini** katalog seçiciyle planlar (`source=Self`, düzenlenebilir).
- [ ] **Çalış açılışında "Bugünün planı"** görünür; dokununca sayaç o derse bağlı (`lessonId`) başlar.
- [ ] Seans tamamlanınca takvimde o occurrence **✓ çalışıldı** olur; çalışılmayan geçmiş plan **○ atlandı**.
- [ ] Haftalık analiz katalog adıyla tutarlı gruplanır.
- [ ] Öğretmen dersleri hâlâ salt-okunur (kilit), öğrenci onlara dokunamaz (`S-04.4`).
- [ ] `dotnet build` + tüm migration'lar temiz; `flutter test` yeşil.

## Riskler ve Azaltımlar

- **Yıkıcı göç (A9):** `study_schedule_entries` drop'u geri alınamaz. Azaltım: migration `Down`'ında tabloyu+verisini geri kur; prod öncesi yedek; `Up`'ı staging'de doğrula.
- **Event imza değişimi (A2/A6):** `LessonScheduledDomainEvent` nullable teacher — tüm tüketiciler (Notifications) aynı PR'da güncellenmeli; `dotnet build` bunu yakalar.
- **Mobil geriye uyum:** `study-entries` rotaları korunur (A8), böylece eski mobil sürüm kırılmaz.
- **Backend test boşluğu:** Yalnız domain birim testi var; API/persistence smoke ile doğrulanır — regresyon riski için A/B fazları ayrı ayrı sevk edilip smoke edilmeli.

## Self-Review notu

- Spec kapsamı: G1 (B1,B2,C6) · G2 (B2,C3,C4) · G3 (C3,C4) · G4 (B3,B4,B5,C7) · G5 (C5) — hepsi görevlere bağlandı.
- Tip tutarlılığı: `LessonScheduledDomainEvent` imzası A2'de değişip A6'da tüm çağıranlar hizalanır; `IStudyPlanCompletionReader`/`PlanCompletion` B3'te tanımlanıp B4/B5'te kullanılır; `startSession(... lessonId, subjectId, topicId)` C1'de tanımlanıp C2/C3/C6'da kullanılır.
