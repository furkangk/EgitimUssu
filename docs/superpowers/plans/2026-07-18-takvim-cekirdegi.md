# Takvim Çekirdeği (Dilim A) Implementation Plan

> **✅ DURUM (2026-08-19): TAMAMLANDI ve `main`'e merge edildi.** Tüm görevler kodlandı, dokümanlar güncellendi (`cd5311e`), merge `ac2f606`. İlgili commit'ler: B-10 `e1e1d59` · B-02 `91a7716` · B-09 `004afbc`+`3267841` · B-08 `e507930` · B-01 `e9c6668` · B-03 `25b6d0c`+`55db87b`+`5fd80e3`. Doğrulama: `dotnet test tests/Unit` → 151/151 yeşil. Aşağıdaki checkbox'lar geriye dönük işaretlenmiştir.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğretmen takvim çekirdeğinin 6 boşluğunu kapatmak — online link (B-10), erteleme (B-02), iptal nedeni/silme (B-09), oturum ücretlendirme (B-08), tatil bloğu (B-01), tekrar eden ders occurrence yönetimi (B-03).

**Architecture:** Mevcut Scheduling (M04) + LessonSessions (M05) modüllerinin Clean Architecture + CQRS desenine additive olarak eklenir. Tekrar eden dersler tek satır + `RecurrenceRule` olarak kalır ve okuma anında `RecurrenceExpander` ile sanal genişletilir; tek-oturum işlemleri yeni `LessonOccurrenceException` tablosuyla (iCal `EXDATE`/`RECURRENCE-ID` deseni) çözülür. Her domain değişikliği için modül şemasına ayrı migration.

**Tech Stack:** .NET 9, C#, EF Core (PostgreSQL, modül başına ayrı şema), xUnit + Assert (`tests/Unit`), CQRS (`ICommandHandler`/`IQueryHandler` + `ICommandDispatcher`/`IQueryDispatcher`), `Result`/`Result<T>` + `Error`, `IClock`/`IIdGenerator`, Outbox domain events → Notifications.

## Global Constraints

- **Dil/derleyici:** .NET 9, C#. Modüller birbirine referans veremez (mimari test `Modules_Should_Not_Reference_Other_Modules` zorlar); modüller-arası okuma `Shared/Contracts` üzerinden.
- **Persistence:** Her modül ayrı Postgres şeması + kendi `DbContext` + migration. Migration eklerken startup project `src/API.Host`.
- **Migration komutu (Scheduling):** `dotnet ef migrations add <Ad> --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
- **Migration komutu (LessonSessions):** `dotnet ef migrations add <Ad> --project src/Modules/LessonSessions/Infrastructure --startup-project src/API.Host --context LessonSessionsDbContext`
- **Test:** xUnit (`[Fact]`/`[Theory]`), `Assert.*`. Birim testleri `tests/Unit/`, projesi `EgitimUssu.Tests.Unit`.
- **Build/test komutu:** `dotnet build EgitimUssu.sln` · `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- **DateTime:** Tüm zamanlar UTC (`DateTimeKind.Utc`); `IClock.UtcNow` kullanılır, `DateTime.UtcNow` doğrudan çağrılmaz.
- **Commit mesajı sonu:** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- **Doküman bakımı (KALICI):** Kod değişince aynı turda ilgili `doc/modules/m04_scheduling.md`, `m05_lesson_sessions.md`, `00_genel_bakis.md`, `veri_modeli.md`, `mimari_inceleme.md`, `doc/roles/ogretmen.md` §10 güncellenir. Bu, son doküman task'ında toplanmıştır.
- **Yetki:** Yeni endpoint'ler `AuthenticatedUser` politikasına + sahiplik authorizer'ına tabi (öğretmen yalnız kendi kaynağı; admin her zaman).

## File Structure

**Scheduling (M04) — `src/Modules/Scheduling/`:**
- `Domain/SchedulingDomainModel.cs` — `LessonSchedule` yeni alan/metotlar; yeni `LessonOccurrenceException` entity + `OccurrenceExceptionAction` enum; `CancellationReason` enum; `LessonScheduleDeletedDomainEvent`.
- `Application/LessonScheduleFeatures.cs` — yeni command/handler/response alanları; `RescheduleLessonScheduleCommand`, `DeleteLessonScheduleCommand`; repository arayüzü genişler.
- `Application/RecurrenceExpander.cs` — istisna uygulayan overload.
- `Application/LessonSchedulePolicies.cs` — yeni command'lar için validator/authorizer.
- `Application/TimeOffFeatures.cs` *(yeni)* — `TimeOffBlock` command/query/handler/repository arayüzü/response.
- `Application/TimeOffPolicies.cs` *(yeni)* — validator + authorizer.
- `API/SchedulingModule.cs` — yeni endpoint'ler + request DTO'lar + error eşlemeleri.
- `Infrastructure/SchedulingDbContext.cs` — yeni `DbSet` + `IEntityTypeConfiguration`.
- `Infrastructure/LessonScheduleRepository.cs` — yeni metotlar.
- `Infrastructure/TimeOffBlockRepository.cs` *(yeni)*.
- `Infrastructure/DependencyInjection.cs` — yeni servis kayıtları.
- `Infrastructure/Migrations/` — EF migration'ları (üretilir).

**LessonSessions (M05) — `src/Modules/LessonSessions/`:**
- `Domain/LessonSessionsDomainModel.cs` — `IsChargeable` alanı + `Complete` imzası.
- `Application/*` — complete command/response alanı.
- `API/*` — complete request alanı.
- `Infrastructure/*` — config + migration.

**Tests — `tests/Unit/`:**
- `LessonScheduleTests.cs` *(yeni)* — domain davranışları (reschedule, cancel reason, delete guard, occurrence exception uygulama).
- `RecurrenceExpanderTests.cs` *(mevcut)* — istisna senaryoları eklenir.
- `TimeOffBlockTests.cs` *(yeni)* — çakışma taraması.
- `LessonSessionTests.cs` *(yeni)* — chargeable.

---

### Task 1: B-10 — Online ders linki (`MeetingUrl`)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs`
- Test: `tests/Unit/LessonScheduleTests.cs` (create)

**Interfaces:**
- Produces: `LessonSchedule` ctor ve `UpdateDetails` sonuna `string? meetingUrl` parametresi eklenir; `LessonSchedule.MeetingUrl` (string?) get. `LessonScheduleResponse`'a `string? MeetingUrl` alanı eklenir (kayıt sonuna). `CreateLessonScheduleCommand`/`UpdateLessonScheduleCommand` ve `CreateLessonScheduleRequest`/`UpdateLessonScheduleRequest` sonuna `string? MeetingUrl`.

- [x] **Step 1: Write the failing test**

`tests/Unit/LessonScheduleTests.cs`:
```csharp
using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonScheduleTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 20, 14, 0, 0, DateTimeKind.Utc);

    private static LessonSchedule NewLesson(string? meetingUrl = null, string? recurrenceRule = null)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, End, "Europe/Istanbul",
            recurrenceRule, LessonScheduleStatus.Planned, 60, "adres", meetingUrl, null, Start);

    [Fact]
    public void Ctor_StoresMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://meet.example/abc");
        Assert.Equal("https://meet.example/abc", lesson.MeetingUrl);
    }

    [Fact]
    public void UpdateDetails_ChangesMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://old");
        lesson.UpdateDetails("Matematik", ScheduledLessonFormat.Online, Start, End,
            "Europe/Istanbul", null, 60, "adres", "https://new", null, Start.AddMinutes(1));
        Assert.Equal("https://new", lesson.MeetingUrl);
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — derleme hatası: `LessonSchedule` ctor 13 argüman almıyor / `MeetingUrl` yok.

- [x] **Step 3: Add `MeetingUrl` to domain**

`SchedulingDomainModel.cs` — ctor imzasına `string? locationLabel`'dan **sonra** `string? meetingUrl` ekle; atama ve property ekle; `UpdateDetails` imzasına da aynı konuma ekle.

Ctor değişikliği (parametre listesi ve gövde):
```csharp
    public LessonSchedule(
        Guid id,
        Guid teacherUserId,
        Guid studentId,
        string subject,
        ScheduledLessonFormat lessonFormat,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        LessonScheduleStatus status,
        int reminderOffsetMinutes,
        string? locationLabel,
        string? meetingUrl,
        string? notes,
        DateTime createdOnUtc)
    {
        // ... mevcut atamalar ...
        LocationLabel = locationLabel;
        MeetingUrl = meetingUrl;
        Notes = notes;
        // ... geri kalanı aynı ...
    }
```
Property (LocationLabel'ın altına):
```csharp
    public string? MeetingUrl { get; private set; }
```
`UpdateDetails` — `string? locationLabel`'dan sonra `string? meetingUrl` parametresi ve gövdeye `MeetingUrl = meetingUrl;` ekle.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 5: Thread `MeetingUrl` through Application + API + config**

`LessonScheduleFeatures.cs`:
- `CreateLessonScheduleCommand` ve `UpdateLessonScheduleCommand` kayıtlarına `string? LocationLabel`'dan sonra `string? MeetingUrl` ekle.
- `LessonScheduleResponse` kaydına `string? LocationLabel`'dan sonra `string? MeetingUrl` ekle.
- `CreateLessonScheduleCommandHandler.Handle` içindeki `new LessonSchedule(...)` çağrısına `command.LocationLabel?.Trim()`'ten sonra `command.MeetingUrl?.Trim()` ekle.
- `UpdateLessonScheduleCommandHandler.Handle` içindeki `lesson.UpdateDetails(...)` çağrısına `command.LocationLabel?.Trim()`'ten sonra `command.MeetingUrl?.Trim()` ekle.
- `LessonScheduleMappings.ToResponse` içine `lesson.LocationLabel`'dan sonra `lesson.MeetingUrl` ekle.

`SchedulingModule.cs`:
- `CreateLessonScheduleRequest` ve `UpdateLessonScheduleRequest` kayıtlarına `string? LocationLabel`'dan sonra `string? MeetingUrl` ekle ve `ToCommand(...)` içinde ilgili konuma `MeetingUrl` geçir.

`SchedulingDbContext.cs` — `LessonScheduleConfiguration.Configure` içine:
```csharp
        builder.Property(entity => entity.MeetingUrl).HasMaxLength(512);
```

- [x] **Step 6: Create migration**

Run: `dotnet ef migrations add AddLessonMeetingUrl --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
Expected: `Migrations/<ts>_AddLessonMeetingUrl.cs` üretilir; `AddColumn<string>("MeetingUrl", ...)` içerir.

- [x] **Step 7: Build + test**

Run: `dotnet build EgitimUssu.sln` sonra `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS (derleme + testler).

- [x] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(scheduling): ders planına online MeetingUrl alanı (B-10)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: B-02 — Ders erteleme (`Reschedule`)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`
- Modify: `src/Modules/Scheduling/Application/LessonSchedulePolicies.cs`
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/LessonScheduleTests.cs`

**Interfaces:**
- Produces: `LessonSchedule.Reschedule(DateTime newStartAtUtc, DateTime newEndAtUtc, string? note, DateTime updatedOnUtc)` — statüyü `Planned` bırakır, `OriginalStartAtUtc` yalnız ilk çağrıda set eder, `RescheduleNote` günceller, `LessonScheduleRescheduledDomainEvent` yayar. Yeni property: `DateTime? OriginalStartAtUtc`, `string? RescheduleNote`. `RescheduleLessonScheduleCommand(Guid LessonId, DateTime NewStartAtUtc, DateTime NewEndAtUtc, string? Note)`. Endpoint `POST /api/scheduling/lessons/{lessonId}/reschedule`. `LessonScheduleResponse`'a `DateTime? OriginalStartAtUtc` eklenir (sonuna, `MeetingUrl` sonrası uygun konuma).

- [x] **Step 1: Write the failing test**

`tests/Unit/LessonScheduleTests.cs` içine ekle:
```csharp
    [Fact]
    public void Reschedule_KeepsPlanned_SetsOriginalStartOnce_RaisesEvent()
    {
        var lesson = NewLesson();
        var newStart = Start.AddDays(2);
        var newEnd = End.AddDays(2);

        lesson.Reschedule(newStart, newEnd, "Öğrenci hasta", Start.AddHours(1));

        Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
        Assert.Equal(newStart, lesson.StartAtUtc);
        Assert.Equal(Start, lesson.OriginalStartAtUtc);
        Assert.Equal("Öğrenci hasta", lesson.RescheduleNote);
        Assert.Contains(lesson.DomainEvents, e => e is LessonScheduleRescheduledDomainEvent);

        // İkinci erteleme OriginalStart'ı değiştirmez
        lesson.Reschedule(newStart.AddDays(1), newEnd.AddDays(1), null, Start.AddHours(2));
        Assert.Equal(Start, lesson.OriginalStartAtUtc);
    }
```
> Not: `AggregateRoot<Guid>` `DomainEvents` koleksiyonunu expose ediyor (mevcut event testleri bu şekilde okuyor). Erişilemiyorsa aynı dosyadaki diğer domain testlerinin event okuma desenini birebir izle.

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — `Reschedule` metodu yok.

- [x] **Step 3: Add `Reschedule` to domain**

`SchedulingDomainModel.cs` — property'ler ekle (UpdatedOnUtc altına):
```csharp
    public DateTime? OriginalStartAtUtc { get; private set; }

    public string? RescheduleNote { get; private set; }
```
Metot (`UpdateDetails`'ten sonra):
```csharp
    /// <summary>
    /// Dersi yeni tarih/saate taşır. Statü Planned kalır (ERTELENDİ geçici bir işaret değil, kayıtlı bir taşımadır).
    /// İlk ertelemede özgün başlangıç saklanır; öğrenci/veli bildirimi için Rescheduled olayı yayılır.
    /// </summary>
    public void Reschedule(DateTime newStartAtUtc, DateTime newEndAtUtc, string? note, DateTime updatedOnUtc)
    {
        OriginalStartAtUtc ??= StartAtUtc;
        StartAtUtc = newStartAtUtc;
        EndAtUtc = newEndAtUtc;
        RescheduleNote = note;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonScheduleRescheduledDomainEvent(Id, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, updatedOnUtc));
    }
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 5: Add command + handler**

`LessonScheduleFeatures.cs` — yeni kayıt (CancelLessonScheduleCommand yakınına):
```csharp
public sealed record RescheduleLessonScheduleCommand(
    Guid LessonId,
    DateTime NewStartAtUtc,
    DateTime NewEndAtUtc,
    string? Note) : ICommand<Result<LessonScheduleResponse>>;
```
`LessonScheduleResponse` kaydına `string? MeetingUrl` sonrasına `DateTime? OriginalStartAtUtc` ekle; `ToResponse` içine `lesson.OriginalStartAtUtc` ekle (aynı konum).

Handler (CancelLessonScheduleCommandHandler yakınına):
```csharp
public sealed class RescheduleLessonScheduleCommandHandler : ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error Conflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli ders ertelenebilir.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public RescheduleLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(RescheduleLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.NewEndAtUtc <= command.NewStartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        var hasConflict = await _repository.HasTeacherConflictAsync(
            lesson.TeacherUserId, command.NewStartAtUtc, command.NewEndAtUtc, lesson.Id, cancellationToken);
        if (hasConflict)
        {
            return Result<LessonScheduleResponse>.Failure(Conflict);
        }

        lesson.Reschedule(command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}
```

- [x] **Step 6: Validator + authorizer + config + DI**

`LessonSchedulePolicies.cs`:
- `LessonScheduleCommandAuthorizer` sınıf bildirimine `ICommandAuthorizer<RescheduleLessonScheduleCommand>` ekle ve metodu ekle:
```csharp
    public async Task<Result> Authorize(RescheduleLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(lesson.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }
```

`SchedulingDbContext.cs` — `LessonScheduleConfiguration` içine:
```csharp
        builder.Property(entity => entity.RescheduleNote).HasMaxLength(500);
```

`DependencyInjection.cs` — ekle:
```csharp
        services.AddScoped<ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>, RescheduleLessonScheduleCommandHandler>();
        services.AddScoped<ICommandAuthorizer<RescheduleLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
```

- [x] **Step 7: Add endpoint**

`SchedulingModule.cs` — `MapEndpoints` içine (cancel'dan sonra):
```csharp
        group.MapPost("/lessons/{lessonId:guid}/reschedule", RescheduleLessonScheduleAsync)
        .WithSummary("Dersi yeni tarih/saate erteler");
```
Handler metodu + request DTO ekle:
```csharp
    private static async Task<IResult> RescheduleLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        RescheduleLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new RescheduleLessonScheduleCommand(lessonId, request.NewStartAtUtc, request.NewEndAtUtc, request.Note),
            cancellationToken);
        return ToHttpResult(context, result);
    }
```
Dosyanın DTO bölümüne:
```csharp
public sealed record RescheduleLessonScheduleRequest(DateTime NewStartAtUtc, DateTime NewEndAtUtc, string? Note);
```
`ToHttpResult` switch'ine (zaten `scheduling.not_editable` ve `scheduling.invalid_range` yoksa) `scheduling.invalid_range` için 400 varsayılan zaten uygun — ek eşleme gerekmez.

- [x] **Step 8: Migration + build + test**

Run: `dotnet ef migrations add AddLessonRescheduleFields --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
Sonra `dotnet build EgitimUssu.sln` ve `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: migration `OriginalStartAtUtc` + `RescheduleNote` kolonlarını ekler; build + test PASS.

- [x] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(scheduling): ders erteleme aksiyonu + erteleme geçmişi (B-02)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: B-09a — İptal nedeni + ücretlendirme

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs`
- Test: `tests/Unit/LessonScheduleTests.cs`

**Interfaces:**
- Produces: `enum CancellationReason { TeacherCancelled = 1, StudentCancelled = 2, Holiday = 3, Other = 4 }`. `LessonSchedule.Cancel` imzası: `Cancel(CancellationReason reason, bool isChargeable, string? cancellationNote, DateTime updatedOnUtc)`. Yeni property: `CancellationReason? CancellationReason`, `bool IsChargeable`. `CancelLessonScheduleCommand(Guid LessonId, CancellationReason Reason, bool IsChargeable, string? CancellationNote)`. Response'a `string? CancellationReason`, `bool IsChargeable` eklenir.

- [x] **Step 1: Write the failing test**

`tests/Unit/LessonScheduleTests.cs`:
```csharp
    [Fact]
    public void Cancel_StoresReasonAndChargeable()
    {
        var lesson = NewLesson();
        lesson.Cancel(CancellationReason.StudentCancelled, isChargeable: true, "geç haber verdi", Start.AddHours(1));

        Assert.Equal(LessonScheduleStatus.Cancelled, lesson.Status);
        Assert.Equal(CancellationReason.StudentCancelled, lesson.CancellationReason);
        Assert.True(lesson.IsChargeable);
        Assert.Contains(lesson.DomainEvents, e => e is LessonScheduleCancelledDomainEvent);
    }
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — `Cancel` 4 argüman almıyor / `CancellationReason` yok.

- [x] **Step 3: Update domain**

`SchedulingDomainModel.cs`:
- Enum ekle (dosya sonundaki enum'lar yanına):
```csharp
public enum CancellationReason
{
    TeacherCancelled = 1,
    StudentCancelled = 2,
    Holiday = 3,
    Other = 4
}
```
- Property'ler ekle:
```csharp
    public CancellationReason? CancellationReason { get; private set; }

    public bool IsChargeable { get; private set; }
```
- `Cancel` metodunu değiştir:
```csharp
    public void Cancel(CancellationReason reason, bool isChargeable, string? cancellationNote, DateTime updatedOnUtc)
    {
        Status = LessonScheduleStatus.Cancelled;
        CancellationReason = reason;
        IsChargeable = isChargeable;
        UpdatedOnUtc = updatedOnUtc;

        if (!string.IsNullOrWhiteSpace(cancellationNote))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? cancellationNote
                : $"{Notes}{Environment.NewLine}{cancellationNote}";
        }

        Raise(new LessonScheduleCancelledDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 5: Update command/handler/response/request**

`LessonScheduleFeatures.cs`:
- `CancelLessonScheduleCommand` kaydını değiştir:
```csharp
public sealed record CancelLessonScheduleCommand(
    Guid LessonId,
    CancellationReason Reason,
    bool IsChargeable,
    string? CancellationNote) : ICommand<Result<LessonScheduleResponse>>;
```
- `CancelLessonScheduleCommandHandler.Handle` içindeki `lesson.Cancel(command.CancellationNote?.Trim(), _clock.UtcNow);` satırını değiştir:
```csharp
        lesson.Cancel(command.Reason, command.IsChargeable, command.CancellationNote?.Trim(), _clock.UtcNow);
```
- `LessonScheduleResponse`'a alanlar ekle (`OriginalStartAtUtc` sonrası): `string? CancellationReason`, `bool IsChargeable`.
- `ToResponse` içine ekle: `lesson.CancellationReason?.ToString()`, `lesson.IsChargeable`.

`SchedulingModule.cs`:
- `CancelLessonScheduleRequest`'i değiştir:
```csharp
public sealed record CancelLessonScheduleRequest(CancellationReason Reason, bool IsChargeable, string? CancellationNote);
```
- `CancelLessonScheduleAsync` içindeki dispatch'i değiştir:
```csharp
        var result = await dispatcher.Dispatch(
            new CancelLessonScheduleCommand(lessonId, request.Reason, request.IsChargeable, request.CancellationNote),
            cancellationToken);
```

`SchedulingDbContext.cs` — `LessonScheduleConfiguration` içine:
```csharp
        builder.Property(entity => entity.CancellationReason).HasConversion<string>().HasMaxLength(32);
```

- [x] **Step 6: Migration + build + test**

Run: `dotnet ef migrations add AddLessonCancellationReason --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: migration `CancellationReason` + `IsChargeable` ekler; PASS.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(scheduling): iptal nedeni + ücretlendirme kararı (B-09)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: B-09b — Ders silme (24 saat + gelecek kuralı)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (yeni event)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (command + handler + repo arayüzü)
- Modify: `src/Modules/Scheduling/Application/LessonSchedulePolicies.cs` (authorizer)
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleRepository.cs` (Remove)
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/LessonScheduleTests.cs`

**Interfaces:**
- Produces: `LessonSchedule.CanBeDeletedAt(DateTime nowUtc)` → `bool` (oluşturmadan sonra ≤24 saat **ve** başlangıç gelecekte). `DeleteLessonScheduleCommand(Guid LessonId)`. `ILessonScheduleRepository.Remove(LessonSchedule lesson)`. Endpoint `DELETE /api/scheduling/lessons/{lessonId}`. Yeni error `scheduling.delete_not_allowed` → 409.

- [x] **Step 1: Write the failing test**

`tests/Unit/LessonScheduleTests.cs`:
```csharp
    [Fact]
    public void CanBeDeletedAt_TrueWithin24hAndFuture_FalseOtherwise()
    {
        var lesson = NewLesson(); // Created = Start (2026-07-20 13:00), Start gelecekte varsay
        var justAfter = Start.AddHours(1);          // <24s, ders hâlâ gelecekte (Start 13:00 > ... hayır)
        // Start'ı gelecekte tutmak için "now" oluşturmadan hemen sonra ve dersten önce olmalı:
        var nowWithinAndBeforeLesson = Start.AddMinutes(-30).AddHours(0); // 12:30, oluşturma 13:00'dan önce olamaz

        // Net senaryo: ders başlangıcı oluşturmadan sonra; now oluşturma+1s, ders gelecekte
        var future = new LessonScheduleBuilderNow();
        Assert.True(lesson.CanBeDeletedAt(lesson.CreatedOnUtc.AddHours(1)) == (lesson.StartAtUtc > lesson.CreatedOnUtc.AddHours(1)));
        Assert.False(lesson.CanBeDeletedAt(lesson.CreatedOnUtc.AddHours(25))); // 24s aşıldı
    }
```
> Not: Yukarıdaki test okunması zor; **bunun yerine** aşağıdaki net sürümü yaz:
```csharp
    [Fact]
    public void CanBeDeletedAt_Rules()
    {
        var created = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var lessonStart = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc); // gelecekte
        var lesson = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, lessonStart, lessonStart.AddHours(1), "Europe/Istanbul",
            null, LessonScheduleStatus.Planned, 60, null, null, null, created);

        Assert.True(lesson.CanBeDeletedAt(created.AddHours(1)));    // <24s, ders gelecekte
        Assert.False(lesson.CanBeDeletedAt(created.AddHours(25)));  // 24s aşıldı
        Assert.False(lesson.CanBeDeletedAt(lessonStart.AddMinutes(1))); // ders geçmişte
    }
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — `CanBeDeletedAt` yok.

- [x] **Step 3: Add domain method + event**

`SchedulingDomainModel.cs`:
```csharp
    /// <summary>Silme yalnızca oluşturmadan sonraki 24 saat içinde ve ders gelecekteyse mümkündür; aksi halde iptal kullanılır.</summary>
    public bool CanBeDeletedAt(DateTime nowUtc)
        => nowUtc <= CreatedOnUtc.AddHours(24) && StartAtUtc > nowUtc;
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 5: Command + handler + repo + DI + endpoint**

`LessonScheduleFeatures.cs`:
```csharp
public sealed record DeleteLessonScheduleCommand(Guid LessonId) : ICommand<Result>;
```
Handler:
```csharp
public sealed class DeleteLessonScheduleCommandHandler : ICommandHandler<DeleteLessonScheduleCommand, Result>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error NotAllowed = new("scheduling.delete_not_allowed", "Ders silinemez; iptal edin. Silme yalnizca olusturmadan sonraki 24 saat icinde ve ders gelecekteyse mumkundur.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public DeleteLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(DeleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result.Failure(NotFound);
        }

        if (!lesson.CanBeDeletedAt(_clock.UtcNow))
        {
            return Result.Failure(NotAllowed);
        }

        _repository.Remove(lesson);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```
`ILessonScheduleRepository` arayüzüne ekle: `void Remove(LessonSchedule lessonSchedule);`

`LessonScheduleRepository.cs` ekle:
```csharp
    public void Remove(LessonSchedule lessonSchedule)
    {
        _dbContext.LessonSchedules.Remove(lessonSchedule);
    }
```

`LessonSchedulePolicies.cs` — `LessonScheduleCommandAuthorizer`'a `ICommandAuthorizer<DeleteLessonScheduleCommand>` ekle:
```csharp
    public async Task<Result> Authorize(DeleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(lesson.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }
```

`DependencyInjection.cs`:
```csharp
        services.AddScoped<ICommandHandler<DeleteLessonScheduleCommand, Result>, DeleteLessonScheduleCommandHandler>();
        services.AddScoped<ICommandAuthorizer<DeleteLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
```

`SchedulingModule.cs` — endpoint + handler:
```csharp
        group.MapDelete("/lessons/{lessonId:guid}", DeleteLessonScheduleAsync)
        .WithSummary("Yanlış eklenen dersi siler (24s + gelecek kuralı)");
```
```csharp
    private static async Task<IResult> DeleteLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new DeleteLessonScheduleCommand(lessonId), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.Code switch
            {
                "scheduling.lesson_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                "scheduling.delete_not_allowed" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }
```

- [x] **Step 6: Build + test (migration gerekmez — şema değişmedi)**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(scheduling): ders silme (24s+gelecek) iptalden ayrı (B-09)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: B-08 — Oturum ücretlendirme (`IsChargeable`)

**Files:**
- Modify: `src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs`
- Modify: `src/Modules/LessonSessions/Application/*` (complete command/handler/response — mevcut dosya adını keşfet)
- Modify: `src/Modules/LessonSessions/API/*` (complete request)
- Modify: `src/Modules/LessonSessions/Infrastructure/*` (config)
- Test: `tests/Unit/LessonSessionTests.cs` (create)

**Interfaces:**
- Produces: `LessonSession.IsChargeable` (bool). `LessonSession.Complete(...)` imzasının sonuna `bool isChargeable` eklenir. Complete command/request/response'a `bool IsChargeable`.

- [x] **Step 1: Discover LessonSessions Application/API file names**

Run: `find src/Modules/LessonSessions -name '*.cs' | grep -v obj`
Expected: `Application/*Features.cs`, `Application/*Policies.cs`, `API/*Module.cs`, `Infrastructure/*DbContext.cs` isimlerini not al (Scheduling'deki desenin aynısı).

- [x] **Step 2: Write the failing test**

`tests/Unit/LessonSessionTests.cs`:
```csharp
using EgitimUssu.Modules.LessonSessions.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonSessionTests
{
    private static readonly DateTime Planned = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Complete_AbsentChargeable_IsRecorded()
    {
        var session = new LessonSession(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            Planned, null, null, null,
            StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "Konu", null, null, Planned, null);

        session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Absent,
            "Konu", null, "gelmedi", isChargeable: true, Planned.AddHours(1));

        Assert.Equal(LessonSessionStatus.Completed, session.Status);
        Assert.Equal(StudentAttendanceStatus.Absent, session.AttendanceStatus);
        Assert.True(session.IsChargeable);
    }
}
```

- [x] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonSessionTests`
Expected: FAIL — `Complete` `isChargeable` almıyor / `IsChargeable` yok.

- [x] **Step 4: Update domain**

`LessonSessionsDomainModel.cs`:
- Property ekle (CompletedOnUtc altına): `public bool IsChargeable { get; private set; }`
- `Complete` imzasına `string? teacherNotes` sonrasına `bool isChargeable` ekle; gövdeye `IsChargeable = isChargeable;` ekle.

- [x] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonSessionTests`
Expected: PASS.

- [x] **Step 6: Thread through Application + API + config**

Step 1'de bulunan dosyalarda:
- Complete command kaydına `bool IsChargeable` ekle; handler'daki `session.Complete(...)` çağrısına `command.IsChargeable` geçir (attendance parametresi sonrası uygun konuma göre `teacherNotes`'tan sonra).
- Complete response kaydına `bool IsChargeable` ekle; mapping'e `session.IsChargeable` ekle.
- Complete request DTO'suna `bool IsChargeable` ekle; `ToCommand` içine geçir.
- LessonSessions DbContext config'ine `builder.Property(entity => entity.IsChargeable);` ekle (bool non-null, varsayılan false).

- [x] **Step 7: Migration + build + test**

Run: `dotnet ef migrations add AddLessonSessionIsChargeable --project src/Modules/LessonSessions/Infrastructure --startup-project src/API.Host --context LessonSessionsDbContext`
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: migration `IsChargeable bool NOT NULL DEFAULT false` ekler; PASS.
> `--context` adını Step 1'de bulunan gerçek DbContext ismiyle değiştir.

- [x] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(lesson-sessions): gelmedi→ücretlendirme kararı bayrağı (B-08)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: B-01 — Tatil / müsait değil bloğu (`TimeOffBlock`)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (yeni aggregate + enum)
- Create: `src/Modules/Scheduling/Application/TimeOffFeatures.cs`
- Create: `src/Modules/Scheduling/Application/TimeOffPolicies.cs`
- Create: `src/Modules/Scheduling/Infrastructure/TimeOffBlockRepository.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/DependencyInjection.cs`
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs`
- Test: `tests/Unit/TimeOffBlockTests.cs` (create)

**Interfaces:**
- Produces: `TimeOffBlock` aggregate (`Id, TeacherUserId, Type, Title, StartAtUtc, EndAtUtc, IsAllDay, CreatedOnUtc`). `enum TimeOffType { Holiday=1, Leave=2, Official=3, Other=4 }`. Repository: `AddAsync`, `ListForTeacherAsync(teacherUserId, startAtUtc, endAtUtc)`, `GetByIdAsync`, `Remove`, `SaveChangesAsync`. Endpoint'ler: `POST /api/scheduling/teachers/{teacherUserId}/time-off`, `GET .../time-off?startAtUtc=&endAtUtc=`, `DELETE .../time-off/{timeOffId}`. Create yanıtı `CreateTimeOffResponse(TimeOffBlockResponse Block, IReadOnlyCollection<LessonScheduleResponse> ConflictingLessons)`.

> **Kapsam notu (YAGNI):** Bu task'ta blok tam-gün/aralık tarih penceresiyle modellenir (`StartAtUtc`–`EndAtUtc`). Günlük saat aralığı ("her gün 09–13") ilk sürümde dışta; gerekirse sonraki dilimde eklenir. Çakışma taraması `LessonSchedule` zaman kesişimidir.

- [x] **Step 1: Write the failing test**

`tests/Unit/TimeOffBlockTests.cs`:
```csharp
using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TimeOffBlockTests
{
    [Fact]
    public void Ctor_StoresFields()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var block = new TimeOffBlock(Guid.NewGuid(), Guid.NewGuid(), TimeOffType.Holiday, "Yaz tatili", start, end, true, start);

        Assert.Equal("Yaz tatili", block.Title);
        Assert.Equal(TimeOffType.Holiday, block.Type);
        Assert.True(block.IsAllDay);
        Assert.Equal(start, block.StartAtUtc);
        Assert.Equal(end, block.EndAtUtc);
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TimeOffBlockTests`
Expected: FAIL — `TimeOffBlock` yok.

- [x] **Step 3: Add aggregate + enum**

`SchedulingDomainModel.cs` sonuna:
```csharp
public sealed class TimeOffBlock : AggregateRoot<Guid>
{
    private TimeOffBlock() { }

    public TimeOffBlock(
        Guid id,
        Guid teacherUserId,
        TimeOffType type,
        string title,
        DateTime startAtUtc,
        DateTime endAtUtc,
        bool isAllDay,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        Type = type;
        Title = title;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        IsAllDay = isAllDay;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid TeacherUserId { get; private set; }
    public TimeOffType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime StartAtUtc { get; private set; }
    public DateTime EndAtUtc { get; private set; }
    public bool IsAllDay { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}

public enum TimeOffType
{
    Holiday = 1,
    Leave = 2,
    Official = 3,
    Other = 4
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TimeOffBlockTests`
Expected: PASS.

- [x] **Step 5: Application (features + policies)**

`src/Modules/Scheduling/Application/TimeOffFeatures.cs` oluştur — command/query/handler/response/repository arayüzü. `CreateLessonScheduleCommandHandler` desenini birebir izle. İçerik:
```csharp
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed record CreateTimeOffBlockCommand(
    Guid TeacherUserId,
    TimeOffType Type,
    string Title,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    bool IsAllDay) : ICommand<Result<CreateTimeOffResponse>>;

public sealed record DeleteTimeOffBlockCommand(Guid TimeOffId) : ICommand<Result>;

public sealed record ListTimeOffBlocksForTeacherQuery(
    Guid TeacherUserId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<TimeOffBlockResponse>>>;

public sealed record TimeOffBlockResponse(
    Guid Id, Guid TeacherUserId, string Type, string Title,
    DateTime StartAtUtc, DateTime EndAtUtc, bool IsAllDay, DateTime CreatedOnUtc);

public sealed record CreateTimeOffResponse(
    TimeOffBlockResponse Block,
    IReadOnlyCollection<LessonScheduleResponse> ConflictingLessons);

public interface ITimeOffBlockRepository
{
    Task AddAsync(TimeOffBlock block, CancellationToken cancellationToken);
    Task<TimeOffBlock?> GetByIdAsync(Guid timeOffId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TimeOffBlock>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);
    void Remove(TimeOffBlock block);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateTimeOffBlockCommandHandler : ICommandHandler<CreateTimeOffBlockCommand, Result<CreateTimeOffResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Tatil baslangic ve bitis araligi gecersiz.");
    private readonly ITimeOffBlockRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateTimeOffBlockCommandHandler(
        ITimeOffBlockRepository repository,
        ILessonScheduleRepository lessonRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CreateTimeOffResponse>> Handle(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<CreateTimeOffResponse>.Failure(InvalidRange);
        }

        var block = new TimeOffBlock(
            _idGenerator.New(), command.TeacherUserId, command.Type, command.Title.Trim(),
            command.StartAtUtc, command.EndAtUtc, command.IsAllDay, _clock.UtcNow);

        await _repository.AddAsync(block, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var lessons = await _lessonRepository.ListForTeacherAsync(
            command.TeacherUserId, command.StartAtUtc, command.EndAtUtc, cancellationToken);
        var conflicting = lessons
            .Where(l => l.Status != LessonScheduleStatus.Cancelled && l.StartAtUtc < command.EndAtUtc && l.EndAtUtc > command.StartAtUtc)
            .OrderBy(l => l.StartAtUtc)
            .Select(l => l.ToResponse())
            .ToArray();

        return Result<CreateTimeOffResponse>.Success(new CreateTimeOffResponse(block.ToResponse(), conflicting));
    }
}

public sealed class DeleteTimeOffBlockCommandHandler : ICommandHandler<DeleteTimeOffBlockCommand, Result>
{
    private static readonly Error NotFound = new("scheduling.timeoff_not_found", "Tatil blogu bulunamadi.");
    private readonly ITimeOffBlockRepository _repository;

    public DeleteTimeOffBlockCommandHandler(ITimeOffBlockRepository repository) => _repository = repository;

    public async Task<Result> Handle(DeleteTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var block = await _repository.GetByIdAsync(command.TimeOffId, cancellationToken);
        if (block is null)
        {
            return Result.Failure(NotFound);
        }

        _repository.Remove(block);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListTimeOffBlocksForTeacherQueryHandler : IQueryHandler<ListTimeOffBlocksForTeacherQuery, Result<IReadOnlyCollection<TimeOffBlockResponse>>>
{
    private readonly ITimeOffBlockRepository _repository;

    public ListTimeOffBlocksForTeacherQueryHandler(ITimeOffBlockRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TimeOffBlockResponse>>> Handle(ListTimeOffBlocksForTeacherQuery query, CancellationToken cancellationToken)
    {
        var blocks = await _repository.ListForTeacherAsync(query.TeacherUserId, query.StartAtUtc, query.EndAtUtc, cancellationToken);
        var payload = blocks.OrderBy(b => b.StartAtUtc).Select(b => b.ToResponse()).ToArray();
        return Result<IReadOnlyCollection<TimeOffBlockResponse>>.Success(payload);
    }
}

internal static class TimeOffBlockMappings
{
    public static TimeOffBlockResponse ToResponse(this TimeOffBlock block)
        => new(block.Id, block.TeacherUserId, block.Type.ToString(), block.Title,
            block.StartAtUtc, block.EndAtUtc, block.IsAllDay, block.CreatedOnUtc);
}
```

`src/Modules/Scheduling/Application/TimeOffPolicies.cs` — validator + authorizer (`LessonScheduleCommandAuthorizer.CanManageTeacher` mantığını yinele; modül-içi private helper kopyala):
```csharp
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed class CreateTimeOffBlockCommandValidator : ICommandValidator<CreateTimeOffBlockCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Tatil bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.TeacherUserId != Guid.Empty && !string.IsNullOrWhiteSpace(command.Title);
        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class TimeOffBlockAuthorizer :
    ICommandAuthorizer<CreateTimeOffBlockCommand>,
    ICommandAuthorizer<DeleteTimeOffBlockCommand>,
    IQueryAuthorizer<ListTimeOffBlocksForTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.timeoff_not_found", "Tatil bloğu bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly ITimeOffBlockRepository _repository;

    public TimeOffBlockAuthorizer(ICurrentUser currentUser, ITimeOffBlockRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public async Task<Result> Authorize(DeleteTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var block = await _repository.GetByIdAsync(command.TimeOffId, cancellationToken);
        return block is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(block.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(ListTimeOffBlocksForTeacherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private bool CanManageTeacher(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated) return false;
        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var id) && id == teacherUserId);
    }
}
```

- [x] **Step 6: Infrastructure (repository + config + DI)**

`src/Modules/Scheduling/Infrastructure/TimeOffBlockRepository.cs`:
```csharp
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class TimeOffBlockRepository : ITimeOffBlockRepository
{
    private readonly SchedulingDbContext _dbContext;
    public TimeOffBlockRepository(SchedulingDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(TimeOffBlock block, CancellationToken cancellationToken)
        => _dbContext.TimeOffBlocks.AddAsync(block, cancellationToken).AsTask();

    public Task<TimeOffBlock?> GetByIdAsync(Guid timeOffId, CancellationToken cancellationToken)
        => _dbContext.TimeOffBlocks.FirstOrDefaultAsync(b => b.Id == timeOffId, cancellationToken);

    public async Task<IReadOnlyCollection<TimeOffBlock>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
        => await _dbContext.TimeOffBlocks
            .Where(b => b.TeacherUserId == teacherUserId && b.StartAtUtc < endAtUtc && b.EndAtUtc > startAtUtc)
            .ToArrayAsync(cancellationToken);

    public void Remove(TimeOffBlock block) => _dbContext.TimeOffBlocks.Remove(block);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
```

`SchedulingDbContext.cs`:
- `DbSet` ekle: `public DbSet<TimeOffBlock> TimeOffBlocks => Set<TimeOffBlock>();`
- Config sınıfı ekle:
```csharp
internal sealed class TimeOffBlockConfiguration : IEntityTypeConfiguration<TimeOffBlock>
{
    public void Configure(EntityTypeBuilder<TimeOffBlock> builder)
    {
        builder.ToTable("time_off_blocks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.StartAtUtc).IsRequired();
        builder.Property(entity => entity.EndAtUtc).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StartAtUtc });
    }
}
```

`DependencyInjection.cs` ekle:
```csharp
        services.AddScoped<ITimeOffBlockRepository, TimeOffBlockRepository>();
        services.AddScoped<ICommandHandler<CreateTimeOffBlockCommand, Result<CreateTimeOffResponse>>, CreateTimeOffBlockCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTimeOffBlockCommand, Result>, DeleteTimeOffBlockCommandHandler>();
        services.AddScoped<IQueryHandler<ListTimeOffBlocksForTeacherQuery, Result<IReadOnlyCollection<TimeOffBlockResponse>>>, ListTimeOffBlocksForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateTimeOffBlockCommand>, CreateTimeOffBlockCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateTimeOffBlockCommand>, TimeOffBlockAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteTimeOffBlockCommand>, TimeOffBlockAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListTimeOffBlocksForTeacherQuery>, TimeOffBlockAuthorizer>();
```

- [x] **Step 7: API endpoints**

`SchedulingModule.cs` — `MapEndpoints` içine:
```csharp
        group.MapPost("/teachers/{teacherUserId:guid}/time-off", CreateTimeOffBlockAsync)
        .WithSummary("Tatil / müsait değil bloğu oluşturur; çakışan dersleri döndürür");
        group.MapGet("/teachers/{teacherUserId:guid}/time-off", ListTimeOffBlocksAsync)
        .WithSummary("Öğretmenin tatil bloklarını listeler");
        group.MapDelete("/teachers/{teacherUserId:guid}/time-off/{timeOffId:guid}", DeleteTimeOffBlockAsync)
        .WithSummary("Tatil bloğunu siler");
```
Handler metotları + DTO:
```csharp
    private static async Task<IResult> CreateTimeOffBlockAsync(
        HttpContext context, Guid teacherUserId, CreateTimeOffBlockRequest request,
        ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new CreateTimeOffBlockCommand(teacherUserId, request.Type, request.Title, request.StartAtUtc, request.EndAtUtc, request.IsAllDay),
            cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> ListTimeOffBlocksAsync(
        HttpContext context, Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc,
        IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListTimeOffBlocksForTeacherQuery(teacherUserId, startAtUtc, endAtUtc), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> DeleteTimeOffBlockAsync(
        HttpContext context, Guid teacherUserId, Guid timeOffId,
        ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new DeleteTimeOffBlockCommand(timeOffId), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.Code switch
            {
                "scheduling.timeoff_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }
```
DTO:
```csharp
public sealed record CreateTimeOffBlockRequest(
    TimeOffType Type, string Title, DateTime StartAtUtc, DateTime EndAtUtc, bool IsAllDay);
```
`ToHttpResult` switch'ine `scheduling.timeoff_not_found` için 404 ekle.
> Not: `CreateTimeOffBlockRequest` `TimeOffType` kullandığı için dosya başında `using EgitimUssu.Modules.Scheduling.Domain;` zaten mevcut (SchedulingModule.cs bunu import ediyor).

- [x] **Step 8: Migration + build + test**

Run: `dotnet ef migrations add AddTimeOffBlocks --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `time_off_blocks` tablosu; PASS.

- [x] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(scheduling): tatil/müsait değil bloğu + çakışan ders taraması (B-01)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: B-03 — Occurrence exception modeli (entity + repository)

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (entity + enum)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (repository arayüzü)
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleRepository.cs`
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs`
- Test: `tests/Unit/LessonScheduleTests.cs`

**Interfaces:**
- Produces: `enum OccurrenceExceptionAction { Skipped = 1, Cancelled = 2, Rescheduled = 3 }`. `LessonOccurrenceException` entity (`Id, SeriesLessonScheduleId, OriginalStartAtUtc, Action, OverrideStartAtUtc?, OverrideEndAtUtc?, Note?, CreatedOnUtc`). `ILessonScheduleRepository`'ye: `Task AddExceptionAsync(LessonOccurrenceException ex, CancellationToken)`, `Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesId, CancellationToken)`, `Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken)`.

- [x] **Step 1: Write the failing test**

`tests/Unit/LessonScheduleTests.cs`:
```csharp
    [Fact]
    public void OccurrenceException_Ctor_StoresFields()
    {
        var seriesId = Guid.NewGuid();
        var original = new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc);
        var ex = new LessonOccurrenceException(
            Guid.NewGuid(), seriesId, original, OccurrenceExceptionAction.Rescheduled,
            original.AddDays(1), original.AddDays(1).AddHours(1), "bir hafta ertelendi", original);

        Assert.Equal(seriesId, ex.SeriesLessonScheduleId);
        Assert.Equal(OccurrenceExceptionAction.Rescheduled, ex.Action);
        Assert.Equal(original.AddDays(1), ex.OverrideStartAtUtc);
    }
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — `LessonOccurrenceException` yok.

- [x] **Step 3: Add entity + enum**

`SchedulingDomainModel.cs` sonuna:
```csharp
public sealed class LessonOccurrenceException : Entity<Guid>
{
    private LessonOccurrenceException() { }

    public LessonOccurrenceException(
        Guid id,
        Guid seriesLessonScheduleId,
        DateTime originalStartAtUtc,
        OccurrenceExceptionAction action,
        DateTime? overrideStartAtUtc,
        DateTime? overrideEndAtUtc,
        string? note,
        DateTime createdOnUtc)
    {
        Id = id;
        SeriesLessonScheduleId = seriesLessonScheduleId;
        OriginalStartAtUtc = originalStartAtUtc;
        Action = action;
        OverrideStartAtUtc = overrideStartAtUtc;
        OverrideEndAtUtc = overrideEndAtUtc;
        Note = note;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid SeriesLessonScheduleId { get; private set; }
    public DateTime OriginalStartAtUtc { get; private set; }
    public OccurrenceExceptionAction Action { get; private set; }
    public DateTime? OverrideStartAtUtc { get; private set; }
    public DateTime? OverrideEndAtUtc { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}

public enum OccurrenceExceptionAction
{
    Skipped = 1,
    Cancelled = 2,
    Rescheduled = 3
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 5: Repository + config**

`ILessonScheduleRepository` (LessonScheduleFeatures.cs) arayüzüne ekle:
```csharp
    Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken);
```

`LessonScheduleRepository.cs` ekle (teacher exceptions için join StartAtUtc bazlı değil; series bazlı toplama):
```csharp
    public Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        => _dbContext.LessonOccurrenceExceptions.AddAsync(occurrenceException, cancellationToken).AsTask();

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
        => await _dbContext.LessonOccurrenceExceptions
            .Where(x => x.SeriesLessonScheduleId == seriesLessonScheduleId)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
        => await (from x in _dbContext.LessonOccurrenceExceptions
                  join l in _dbContext.LessonSchedules on x.SeriesLessonScheduleId equals l.Id
                  where l.TeacherUserId == teacherUserId
                  select x).ToArrayAsync(cancellationToken);
```

`SchedulingDbContext.cs`:
- `DbSet` ekle: `public DbSet<LessonOccurrenceException> LessonOccurrenceExceptions => Set<LessonOccurrenceException>();`
- Config:
```csharp
internal sealed class LessonOccurrenceExceptionConfiguration : IEntityTypeConfiguration<LessonOccurrenceException>
{
    public void Configure(EntityTypeBuilder<LessonOccurrenceException> builder)
    {
        builder.ToTable("lesson_occurrence_exceptions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Note).HasMaxLength(500);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.SeriesLessonScheduleId, entity.OriginalStartAtUtc });
    }
}
```

- [x] **Step 6: Migration + build + test**

Run: `dotnet ef migrations add AddLessonOccurrenceExceptions --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `lesson_occurrence_exceptions` tablosu; PASS.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(scheduling): occurrence exception entity + repository (B-03 altyapı)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: B-03 — `RecurrenceExpander` istisnaları uygular

**Files:**
- Modify: `src/Modules/Scheduling/Application/RecurrenceExpander.cs`
- Modify: `tests/Unit/RecurrenceExpanderTests.cs`

**Interfaces:**
- Produces: `ScheduleOccurrence`'a `bool IsCancelled` alanı eklenir (varsayılan false). Yeni overload:
  `RecurrenceExpander.Expand(DateTime anchorStartUtc, DateTime anchorEndUtc, string? recurrenceRule, DateTime rangeStartUtc, DateTime rangeEndUtc, IReadOnlyCollection<OccurrenceOverride> exceptions)` — `OccurrenceOverride(DateTime OriginalStartAtUtc, OccurrenceExceptionAction Action, DateTime? OverrideStartAtUtc, DateTime? OverrideEndAtUtc)`. `Skipped` → occurrence atlanır; `Cancelled` → `IsCancelled=true` ile döner; `Rescheduled` → override tarih/saatle döner.

- [x] **Step 1: Write the failing test**

`tests/Unit/RecurrenceExpanderTests.cs` içine:
```csharp
    [Fact]
    public void Expand_WithSkipException_OmitsThatOccurrence()
    {
        // Her Pazartesi, 3 hafta
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var rangeStart = Monday.AddDays(-1);
        var rangeEnd = Monday.AddDays(21);
        var secondMonday = Monday.AddDays(7);

        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Skipped, null, null)
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, rangeStart, rangeEnd, exceptions)
            .ToArray();

        Assert.DoesNotContain(result, o => o.StartAtUtc == secondMonday);
        Assert.Contains(result, o => o.StartAtUtc == Monday);
    }

    [Fact]
    public void Expand_WithRescheduleException_MovesOccurrence()
    {
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var secondMonday = Monday.AddDays(7);
        var moved = secondMonday.AddDays(2); // Çarşamba
        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Rescheduled, moved, moved.AddHours(1))
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday.AddDays(-1), Monday.AddDays(21), exceptions)
            .ToArray();

        Assert.DoesNotContain(result, o => o.StartAtUtc == secondMonday);
        Assert.Contains(result, o => o.StartAtUtc == moved);
    }

    [Fact]
    public void Expand_WithCancelException_MarksCancelled()
    {
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var secondMonday = Monday.AddDays(7);
        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Cancelled, null, null)
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday.AddDays(-1), Monday.AddDays(21), exceptions)
            .ToArray();

        Assert.Contains(result, o => o.StartAtUtc == secondMonday && o.IsCancelled);
    }
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~RecurrenceExpanderTests`
Expected: FAIL — 6 argümanlı `Expand` overload'u ve `OccurrenceOverride`/`IsCancelled` yok.

- [x] **Step 3: Implement overload**

`RecurrenceExpander.cs`:
- `ScheduleOccurrence`'ı değiştir:
```csharp
public readonly record struct ScheduleOccurrence(DateTime StartAtUtc, DateTime EndAtUtc, bool IsCancelled = false);
```
- Yeni tip ekle:
```csharp
public readonly record struct OccurrenceOverride(
    DateTime OriginalStartAtUtc,
    OccurrenceExceptionAction Action,
    DateTime? OverrideStartAtUtc,
    DateTime? OverrideEndAtUtc);
```
> `OccurrenceExceptionAction` Domain'de; dosya başına `using EgitimUssu.Modules.Scheduling.Domain;` ekle.
- Yeni overload ekle (mevcut `Expand`'i çağırıp istisnaları uygular):
```csharp
    public static IEnumerable<ScheduleOccurrence> Expand(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<OccurrenceOverride> exceptions)
    {
        var byOriginal = exceptions.ToDictionary(e => e.OriginalStartAtUtc);

        foreach (var occurrence in Expand(anchorStartUtc, anchorEndUtc, recurrenceRule, rangeStartUtc, rangeEndUtc))
        {
            if (!byOriginal.TryGetValue(occurrence.StartAtUtc, out var ex))
            {
                yield return occurrence;
                continue;
            }

            switch (ex.Action)
            {
                case OccurrenceExceptionAction.Skipped:
                    continue;
                case OccurrenceExceptionAction.Cancelled:
                    yield return occurrence with { IsCancelled = true };
                    break;
                case OccurrenceExceptionAction.Rescheduled:
                    var start = ex.OverrideStartAtUtc ?? occurrence.StartAtUtc;
                    var end = ex.OverrideEndAtUtc ?? occurrence.EndAtUtc;
                    if (start <= rangeEndUtc && end >= rangeStartUtc)
                    {
                        yield return new ScheduleOccurrence(start, end);
                    }

                    break;
            }
        }
    }
```

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~RecurrenceExpanderTests`
Expected: PASS (yeni + mevcut testler).

- [x] **Step 5: Wire exceptions into calendar/list expansion**

`StudyScheduleFeatures.cs` içinde `RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, ...)` çağrılarını, ilgili serinin istisnalarını geçiren 6-argümanlı overload'a taşı. Her ders için:
```csharp
var lessonExceptions = allExceptions
    .Where(x => x.SeriesLessonScheduleId == lesson.Id)
    .Select(x => new OccurrenceOverride(x.OriginalStartAtUtc, x.Action, x.OverrideStartAtUtc, x.OverrideEndAtUtc))
    .ToArray();
foreach (var occurrence in RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, windowStart, windowEnd, lessonExceptions))
{
    if (occurrence.IsCancelled) { /* iptal işaretli occurrence: takvimde soluk göstermek için response'a taşınır veya atlanır */ }
    // ... mevcut occurrence işleme ...
}
```
`allExceptions`, ilgili handler'ın başında `_repository.ListExceptionsForTeacherAsync(teacherUserId, ...)` (öğretmen takvimi) veya öğrenci takviminde ilgili derslerin serilerinden `ListExceptionsForSeriesAsync` ile toplanır. Öğrenci takvimi handler'ında öğretmen id doğrudan yoksa, her benzersiz `lesson.Id` için `ListExceptionsForSeriesAsync` çağır ve birleştir.
> Bu adım mevcut genişletme çağrılarının **hepsini** kapsamalı (çakışma kontrolü dahil `OverlapsTeacherLesson` iptal edilen/atlanan occurrence'ları saymamalı). `StudyScheduleFeatures.cs`'deki tüm `RecurrenceExpander.Expand(...)` çağrılarını gözden geçir.

- [x] **Step 6: Build + test**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(scheduling): recurrence genişletmesi occurrence istisnalarını uygular (B-03)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: B-03 — Kapsam (scope) ile tek-oturum iptal/ertele

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (cancel + reschedule handler'larına scope)
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (request DTO'lara scope)
- Test: `tests/Unit/LessonScheduleTests.cs` (handler seviyesinde davranış — sahte repository ile)

**Interfaces:**
- Produces: `enum OccurrenceScope { Single = 1, ThisAndFuture = 2, All = 3 }`. `CancelLessonScheduleCommand` ve `RescheduleLessonScheduleCommand` sonuna `OccurrenceScope Scope` eklenir. Cancel/Reschedule handler'ları: `Scope==Single` **ve** ders tekrarlı ise → temel satırı değiştirmeden `LessonOccurrenceException` yazar (`OriginalStartAtUtc` = hedef occurrence). `All` veya tek-seferlik ders → mevcut davranış (temel satır). `ThisAndFuture` → temel satırın kuralını hedef tarihten önce `UNTIL` ile sınırlandırır (bu task'ta cancel için: hedef ve sonrasını istisna yerine `UNTIL` kısaltmasıyla durdurur).

> **Kapsam kararı (YAGNI):** `ThisAndFuture` için tam "yeni seri oluştur" davranışı (reschedule'da yeni ayarlarla) karmaşık. Bu task'ta `ThisAndFuture` yalnızca **seriyi hedeften önce sonlandırır** (`RecurrenceRule`'a `UNTIL` ekleyerek). Yeni-seri-ile-değiştir ihtiyacı doğarsa ayrı task açılır. Hedef occurrence tanımı: istek gövdesindeki `OccurrenceStartAtUtc`.

- [x] **Step 1: Add scope enum + command fields (compile-first)**

`LessonScheduleFeatures.cs`:
```csharp
public enum OccurrenceScope
{
    Single = 1,
    ThisAndFuture = 2,
    All = 3
}
```
`CancelLessonScheduleCommand` ve `RescheduleLessonScheduleCommand` sonuna ekle:
- Cancel: `OccurrenceScope Scope, DateTime? OccurrenceStartAtUtc`
- Reschedule: `OccurrenceScope Scope, DateTime? OccurrenceStartAtUtc`

- [x] **Step 2: Write the failing test (single-occurrence cancel writes exception)**

`tests/Unit/LessonScheduleTests.cs` — hafif bir sahte repository ile handler davranışı. `ILessonScheduleRepository`'yi test içinde minimal implemente et (yalnız gereken metotlar; diğerleri `NotImplementedException`), `IClock` için sabit saat:
```csharp
    private sealed class FakeClock : EgitimUssu.Shared.Kernel.IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Cancel_SingleScope_OnRecurringLesson_WritesExceptionInsteadOfCancellingSeries()
    {
        var series = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, End, "Europe/Istanbul",
            "FREQ=WEEKLY;BYDAY=MO", LessonScheduleStatus.Planned, 60, null, null, null, Start);

        var repo = new RecordingRepository(series);
        var handler = new CancelLessonScheduleCommandHandler(repo, new FakeClock());

        var occurrence = Start.AddDays(7);
        var result = await handler.Handle(
            new CancelLessonScheduleCommand(series.Id, CancellationReason.StudentCancelled, false, null, OccurrenceScope.Single, occurrence),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonScheduleStatus.Planned, series.Status); // seri bozulmadı
        Assert.Single(repo.AddedExceptions);
        Assert.Equal(OccurrenceExceptionAction.Cancelled, repo.AddedExceptions[0].Action);
        Assert.Equal(occurrence, repo.AddedExceptions[0].OriginalStartAtUtc);
    }
```
`RecordingRepository`, `ILessonScheduleRepository`'yi implemente eder: `GetByIdAsync` seriyi döndürür, `AddExceptionAsync` listeye ekler, `SaveChangesAsync` no-op, diğerleri `throw new NotImplementedException()`. (Sınıfı test dosyasının sonuna ekle.)

- [x] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: FAIL — handler henüz scope'u işlemiyor; `AddExceptions` boş.

- [x] **Step 4: Implement scope in Cancel handler**

`CancelLessonScheduleCommandHandler.Handle` — ders bulunduktan sonra, `Cancel` çağrısından önce:
```csharp
        var isRecurring = !string.IsNullOrWhiteSpace(lesson.RecurrenceRule);
        if (isRecurring && command.Scope == OccurrenceScope.Single && command.OccurrenceStartAtUtc is { } occStart)
        {
            var ex = new LessonOccurrenceException(
                _idGenerator.New(), lesson.Id, occStart,
                OccurrenceExceptionAction.Cancelled, null, null, command.CancellationNote?.Trim(), _clock.UtcNow);
            await _repository.AddExceptionAsync(ex, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }

        if (isRecurring && command.Scope == OccurrenceScope.ThisAndFuture && command.OccurrenceStartAtUtc is { } cutoff)
        {
            lesson.EndSeriesBefore(cutoff, _clock.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }
```
> `CancelLessonScheduleCommandHandler`'a `IIdGenerator` bağımlılığı ekle (ctor + alan), DI zaten `IIdGenerator` sağlıyor.

Domain'e `EndSeriesBefore` ekle (`SchedulingDomainModel.cs`, `LessonSchedule`):
```csharp
    /// <summary>Tekrar serisini verilen tarihten önce sonlandırır (RecurrenceRule'a UNTIL ekler). "Bu ve sonrakiler" iptali için.</summary>
    public void EndSeriesBefore(DateTime cutoffUtc, DateTime updatedOnUtc)
    {
        if (string.IsNullOrWhiteSpace(RecurrenceRule)) return;
        var until = cutoffUtc.AddDays(-1).ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        RecurrenceRule = RecurrenceRule.Contains("UNTIL=", StringComparison.OrdinalIgnoreCase)
            ? System.Text.RegularExpressions.Regex.Replace(RecurrenceRule, "UNTIL=[^;]*", $"UNTIL={until}")
            : $"{RecurrenceRule};UNTIL={until}";
        UpdatedOnUtc = updatedOnUtc;
    }
```

- [x] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~LessonScheduleTests`
Expected: PASS.

- [x] **Step 6: Apply same scope logic to Reschedule handler**

`RescheduleLessonScheduleCommandHandler.Handle` — `isRecurring && Scope==Single && OccurrenceStartAtUtc is {} occStart` durumunda, temel satırı değiştirmeden `Rescheduled` istisnası yaz:
```csharp
        if (!string.IsNullOrWhiteSpace(lesson.RecurrenceRule)
            && command.Scope == OccurrenceScope.Single
            && command.OccurrenceStartAtUtc is { } occStart)
        {
            var ex = new LessonOccurrenceException(
                _idGenerator.New(), lesson.Id, occStart,
                OccurrenceExceptionAction.Rescheduled, command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow);
            await _repository.AddExceptionAsync(ex, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }
```
> `RescheduleLessonScheduleCommandHandler`'a `IIdGenerator` ekle. `All`/tek-seferlik → mevcut `lesson.Reschedule(...)` yolu.

- [x] **Step 7: API request DTO'lara scope ekle**

`SchedulingModule.cs`:
- `CancelLessonScheduleRequest`'e `OccurrenceScope Scope = OccurrenceScope.All`, `DateTime? OccurrenceStartAtUtc = null` ekle; `CancelLessonScheduleAsync` dispatch'ine geçir.
- `RescheduleLessonScheduleRequest`'e `OccurrenceScope Scope = OccurrenceScope.All`, `DateTime? OccurrenceStartAtUtc = null` ekle; dispatch'e geçir.
> `using EgitimUssu.Modules.Scheduling.Application;` zaten mevcut (OccurrenceScope oradan gelir).

- [x] **Step 8: DI güncelle (IIdGenerator handler'lara zaten enjekte ediliyor mu doğrula)**

Cancel + Reschedule handler kayıtları değişmez (aynı arayüz), ancak ctor'a `IIdGenerator` eklendiği için DI container zaten `IIdGenerator`'ı çözer (Create handler kullanıyor). Ek kayıt gerekmez.

- [x] **Step 9: Build + test**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [x] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(scheduling): tekrar eden ders için scope (bu/bu+sonraki/tümü) iptal & ertele (B-03)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 10: Dokümantasyon güncellemesi (KALICI KURAL)

**Files:**
- Modify: `doc/modules/m04_scheduling.md`
- Modify: `doc/modules/m05_lesson_sessions.md`
- Modify: `doc/modules/00_genel_bakis.md`
- Modify: `doc/modules/veri_modeli.md`
- Modify: `doc/modules/mimari_inceleme.md`
- Modify: `doc/roles/ogretmen.md`

**Interfaces:** Yok (yalnız dokümantasyon).

- [x] **Step 1: m04_scheduling.md güncelle**

Yeni domain alanları/entity'ler (`MeetingUrl`, `OriginalStartAtUtc`, `RescheduleNote`, `CancellationReason`, `IsChargeable`, `TimeOffBlock`, `LessonOccurrenceException`, `OccurrenceScope`) + yeni endpoint'ler (`/lessons/{id}/reschedule`, `DELETE /lessons/{id}`, `/teachers/{id}/time-off` [POST/GET/DELETE]) + iş kuralları (24s silme, scope davranışı, tatil çakışma taraması). "Güncelleme: 2026-07-18".

- [x] **Step 2: m05_lesson_sessions.md güncelle**

`LessonSession.IsChargeable` + complete akışında ücretlendirme kararı. Tarih güncelle.

- [x] **Step 3: 00_genel_bakis.md endpoint envanteri + durum**

§4 Scheduling ve LessonSessions endpoint listelerine yeni satırları ekle. §1 modül tablosunda M04 durumunu `🟡 (link/tatil ⚠️)` → `🟢` yap (link + tatil + erteleme tamam). Alt tarih notunu güncelle.

- [x] **Step 4: veri_modeli.md ER güncelle**

`TimeOffBlock`, `LessonOccurrenceException` tabloları + `LessonSchedule`/`LessonSession` yeni alanları ER'a ekle.

- [x] **Step 5: mimari_inceleme.md**

Takvim boşluklarıyla (B-01/B-02/B-03/B-08/B-09/B-10) ilgili açık madde varsa "✅ Düzeltildi" işaretle.

- [x] **Step 6: ogretmen.md §10 durumları**

§10.1 tablosunda B-01/B-02/B-03/B-08/B-09/B-10 satırlarını "✅ yapıldı (Dilim A)" olarak işaretle; §10.3 Öncelik 1/2 ilgili maddeleri güncelle. Kabul Kriterleri (§9) ilgili maddeleri `[x]` yap. Tarih güncelle.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "docs: takvim çekirdeği (Dilim A) — modül/rol/ER/endpoint dokümanları güncellendi

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notu (plan yazarı)

- **Spec kapsamı:** B-01 (Task 6), B-02 (Task 2), B-03 (Task 7-8-9), B-08 (Task 5), B-09 (Task 3 iptal + Task 4 silme), B-10 (Task 1). Tümü karşılandı.
- **YAGNI kısıtları:** TimeOff günlük saat aralığı ve `ThisAndFuture` "yeni seri ile değiştir" bilinçli olarak ertelendi (Task 6 ve Task 9 notlarında belirtildi).
- **Tip tutarlılığı:** `LessonSchedule` ctor'una eklenen parametre sırası (`meetingUrl`, `locationLabel`'dan sonra) tüm çağrı yerlerinde (`CreateHandler`, testler) aynı sırada kullanılır. `OccurrenceScope`/`OccurrenceExceptionAction`/`CancellationReason`/`TimeOffType` enum adları Task'lar arası tutarlı.
- **Migration sırası:** Her şema-değişen task kendi migration'ını üretir; sıra Task 1→7 boyunca additive, çakışma yok. Task 4 ve Task 9 (EndSeriesBefore hariç) şema değiştirmez.
- **Bilinmeyen doğrulama:** Task 5, LessonSessions dosya adlarını Step 1'de keşfeder (Scheduling deseninin aynısı varsayıldı). `AggregateRoot.DomainEvents` erişimi Task 2 Step 1 notunda mevcut test desenine yönlendirildi.
