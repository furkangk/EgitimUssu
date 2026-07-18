# Öğretmen Profili Olgunluk (Dilim D) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** M02'ye çoklu branş (`Subjects`) ve sertifika (`TeacherCertificate`) eklemek; tek `Subject` birincil branş olarak korunur.

**Architecture:** `TeacherAvailabilitySlot` koleksiyon deseninin (BuildAvailabilitySlots + Clear/AddRange) aynısı iki yeni child entity için. Additive; her değişiklik için `teachers` şemasına migration.

**Tech Stack:** .NET 9, EF Core (PostgreSQL, `teachers` şeması), xUnit (`tests/Unit`), CQRS + `Result`, `IIdGenerator`.

## Global Constraints
- Migration komutu: `dotnet ef migrations add <Ad> --project src/Modules/Teachers/Infrastructure --startup-project src/API.Host --context TeachersDbContext`
- Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` · Build: `dotnet build EgitimUssu.sln`
- Birincil `Subject` alanı korunur (domain event + Matching kırılmaz).
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` · Doküman bakımı son task'ta.

## File Structure
- `src/Modules/Teachers/Domain/TeachersDomainModel.cs` — `TeacherSubject`, `TeacherCertificate` entity'leri + `TeacherProfile.Subjects`/`Certificates` + `Update` imzası.
- `src/Modules/Teachers/Application/TeacherProfileFeatures.cs` — command/request/response + build helper'ları.
- `src/Modules/Teachers/API/TeacherProfileModule.cs` (gerçek ad: `Teachers/API/*Module*.cs`) — DTO.
- `src/Modules/Teachers/Infrastructure/*DbContext*.cs` — config.
- `tests/Unit/TeacherProfileTests.cs` *(yeni)*.

---

### Task 1: Çoklu branş (`Subjects`)

**Files:**
- Modify: `src/Modules/Teachers/Domain/TeachersDomainModel.cs`
- Modify: `src/Modules/Teachers/Application/TeacherProfileFeatures.cs`
- Modify: `src/Modules/Teachers/API/` (module dosyası)
- Modify: `src/Modules/Teachers/Infrastructure/` (DbContext)
- Test: `tests/Unit/TeacherProfileTests.cs` (create)

**Interfaces:**
- Produces: `TeacherSubject(Guid id, Guid teacherProfileId, string subject)` entity. `TeacherProfile.Subjects` (List). `TeacherProfile` ctor + `Update` sonuna `IReadOnlyCollection<TeacherSubject> subjects`. Create/Update command + request + response'a `IReadOnlyCollection<string> Subjects`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/TeacherProfileTests.cs`:
```csharp
using EgitimUssu.Modules.Teachers.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherProfileTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherProfile New()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Ahmet", "Matematik", "İstanbul", "Kadıköy",
            null, null, TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", false, null, Now);

    [Fact]
    public void Update_ReplacesSubjects()
    {
        var profile = New();
        var subjects = new[]
        {
            new TeacherSubject(Guid.NewGuid(), profile.Id, "Matematik"),
            new TeacherSubject(Guid.NewGuid(), profile.Id, "Fizik")
        };

        profile.Update("Ahmet", "Matematik", "İstanbul", "Kadıköy", null, null,
            TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", null,
            Array.Empty<TeacherAvailabilitySlot>(), subjects, Array.Empty<TeacherCertificate>(), Now);

        Assert.Equal(2, profile.Subjects.Count);
        Assert.Contains(profile.Subjects, s => s.Subject == "Fizik");
    }
}
```
> Not: `Update` imzası Task 2 ile birlikte `certificates` de alacak. Bu testi Task 2 sonrası derlenecek şekilde yazdık (Certificates parametresi dahil). Task 1'i uygularken `Update` imzasına **hem** `subjects` **hem** `certificates` parametrelerini ekleyip Certificates'i Task 2'de doldurmak yerine, iki koleksiyonu da Task 1'de imzaya ekle ve Certificates entity'sini Task 1'de tanımla (aşağıda). Böylece tek imza değişikliği olur.

- [ ] **Step 2: Add entities (Subjects + Certificates iskeleti) + Update imzası**

`TeachersDomainModel.cs`:
```csharp
public sealed class TeacherSubject : Entity<Guid>
{
    private TeacherSubject() { }
    public TeacherSubject(Guid id, Guid teacherProfileId, string subject)
    {
        Id = id;
        TeacherProfileId = teacherProfileId;
        Subject = subject;
    }
    public Guid TeacherProfileId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
}

public sealed class TeacherCertificate : Entity<Guid>
{
    private TeacherCertificate() { }
    public TeacherCertificate(Guid id, Guid teacherProfileId, string title, string? institution, int? year, string? fileUrl)
    {
        Id = id;
        TeacherProfileId = teacherProfileId;
        Title = title;
        Institution = institution;
        Year = year;
        FileUrl = fileUrl;
    }
    public Guid TeacherProfileId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Institution { get; private set; }
    public int? Year { get; private set; }
    public string? FileUrl { get; private set; }
}
```
`TeacherProfile`'a koleksiyonlar (AvailabilitySlots yanına):
```csharp
    public List<TeacherSubject> Subjects { get; private set; } = [];
    public List<TeacherCertificate> Certificates { get; private set; } = [];
```
`Update` imzasına `IReadOnlyCollection<TeacherAvailabilitySlot> availabilitySlots`'tan sonra ekle: `IReadOnlyCollection<TeacherSubject> subjects, IReadOnlyCollection<TeacherCertificate> certificates`; gövdeye:
```csharp
        Subjects.Clear();
        Subjects.AddRange(subjects);
        Certificates.Clear();
        Certificates.AddRange(certificates);
```

- [ ] **Step 3: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherProfileTests`
Expected: PASS.

- [ ] **Step 4: Thread `Subjects` through Application + API + config**

`TeacherProfileFeatures.cs`:
- `CreateTeacherProfileCommand`/`UpdateTeacherProfileCommand`'e `IReadOnlyCollection<string> Subjects` ekle.
- `TeacherProfileResponse`'a `IReadOnlyCollection<string> Subjects` ekle.
- Create/Update handler'larında `Subjects`'ten `TeacherSubject` listesi kur (boşsa `command.Subject`'ten tek satır türet):
```csharp
var subjectNames = command.Subjects is { Count: > 0 } ? command.Subjects : new[] { command.Subject };
var teacherSubjects = subjectNames
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .Select(s => new TeacherSubject(_idGenerator.New(), profileId, s.Trim()))
    .ToArray();
```
Create'te `profile.Subjects.AddRange(teacherSubjects)`; Update'te `profile.Update(..., teacherSubjects, teacherCertificates, ...)` (certificates Task 2). Response eşlemesine `profile.Subjects.Select(s => s.Subject).ToArray()`.
> `profileId`/`profile` değişken adlarını handler'daki gerçek adlarla eşle. Update handler `TeacherAvailabilitySlot` listesini nasıl kuruyorsa aynı yeri kullan.

`TeacherProfile` **ctor**'una da `IReadOnlyCollection<TeacherSubject>`... **gerekmez** — Create handler `AddRange` ile ekliyor (AvailabilitySlots deseni). Ctor değişmez.

API module: `UpsertTeacherProfileRequest`'e `IReadOnlyCollection<string> Subjects` ekle; `ToCommand` içine geçir.

DbContext config — yeni entity config'leri:
```csharp
internal sealed class TeacherSubjectConfiguration : IEntityTypeConfiguration<TeacherSubject>
{
    public void Configure(EntityTypeBuilder<TeacherSubject> builder)
    {
        builder.ToTable("teacher_subjects");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.TeacherProfileId);
    }
}
```
`TeacherProfile` config'inde koleksiyon ilişkisi: `TeacherAvailabilitySlot` nasıl bağlanıyorsa (`HasMany`/`OwnsMany` veya FK), `Subjects` için aynı deseni uygula. Mevcut availability ilişki tanımını bul ve birebir kopyala.

- [ ] **Step 5: Migration + build + test**

Run: `dotnet ef migrations add AddTeacherSubjects --project src/Modules/Teachers/Infrastructure --startup-project src/API.Host --context TeachersDbContext`
Üretilen migration `Up` sonuna backfill (mevcut profilin birincil branşını `teacher_subjects`'e ekle):
```csharp
            migrationBuilder.Sql(@"
                INSERT INTO teachers.teacher_subjects (""Id"", ""TeacherProfileId"", ""Subject"")
                SELECT gen_random_uuid(), tp.""Id"", tp.""Subject""
                FROM teachers.teacher_profiles tp
                WHERE tp.""Subject"" IS NOT NULL AND tp.""Subject"" <> '';");
```
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `teacher_subjects` tablosu + backfill; PASS.
> Kolon adlarını migration snapshot'tan doğrula; `gen_random_uuid()` yoksa alternatif.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(teachers): çoklu branş (Subjects) + backfill (T-02.3)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Sertifika (`Certificates`)

**Files:**
- Modify: `src/Modules/Teachers/Application/TeacherProfileFeatures.cs`
- Modify: `src/Modules/Teachers/API/` (module)
- Modify: `src/Modules/Teachers/Infrastructure/` (DbContext)
- Test: `tests/Unit/TeacherProfileTests.cs`

> `TeacherCertificate` entity'si + `Update` imzası Task 1 Step 2'de eklendi. Bu task Application/API/config + testi tamamlar.

**Interfaces:**
- Produces: `TeacherCertificateRequest(string Title, string? Institution, int? Year, string? FileUrl)`, `TeacherCertificateResponse(Guid Id, string Title, string? Institution, int? Year, string? FileUrl)`. Create/Update command + request + response'a `IReadOnlyCollection<...> Certificates`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/TeacherProfileTests.cs`'e ekle:
```csharp
    [Fact]
    public void Update_ReplacesCertificates()
    {
        var profile = New();
        var certs = new[] { new TeacherCertificate(Guid.NewGuid(), profile.Id, "ÖABT Başarı", "MEB", 2024, null) };

        profile.Update("Ahmet", "Matematik", "İstanbul", "Kadıköy", null, null,
            TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", null,
            Array.Empty<TeacherAvailabilitySlot>(), Array.Empty<TeacherSubject>(), certs, Now);

        Assert.Single(profile.Certificates);
        Assert.Equal("ÖABT Başarı", profile.Certificates[0].Title);
    }
```

- [ ] **Step 2: Run test to verify it fails/passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherProfileTests`
Expected: PASS (domain zaten Task 1'de eklendi). Değilse domain `Certificates` clear/addrange'i düzelt.

- [ ] **Step 3: Thread `Certificates` through Application + API + config**

`TeacherProfileFeatures.cs`:
- `TeacherCertificateRequest`/`TeacherCertificateResponse` kayıtlarını ekle.
- Create/Update command'lara `IReadOnlyCollection<TeacherCertificateRequest> Certificates`; response'a `IReadOnlyCollection<TeacherCertificateResponse> Certificates`.
- Handler'larda `TeacherCertificate` listesi kur:
```csharp
var teacherCertificates = command.Certificates
    .Where(c => !string.IsNullOrWhiteSpace(c.Title))
    .Select(c => new TeacherCertificate(_idGenerator.New(), profileId, c.Title.Trim(), c.Institution?.Trim(), c.Year, c.FileUrl?.Trim()))
    .ToArray();
```
Create'te `profile.Certificates.AddRange(teacherCertificates)`; Update'te `Update(..., teacherSubjects, teacherCertificates, ...)`. Response eşlemesine certificate listesi.

API module: `UpsertTeacherProfileRequest`'e `IReadOnlyCollection<TeacherCertificateRequest> Certificates` ekle; `ToCommand` içine geçir.

DbContext config:
```csharp
internal sealed class TeacherCertificateConfiguration : IEntityTypeConfiguration<TeacherCertificate>
{
    public void Configure(EntityTypeBuilder<TeacherCertificate> builder)
    {
        builder.ToTable("teacher_certificates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Institution).HasMaxLength(200);
        builder.Property(e => e.FileUrl).HasMaxLength(512);
        builder.HasIndex(e => e.TeacherProfileId);
    }
}
```
`TeacherProfile` config'inde `Certificates` ilişkisini `Subjects` ile aynı desende bağla.

- [ ] **Step 4: Migration + build + test**

Run: `dotnet ef migrations add AddTeacherCertificates --project src/Modules/Teachers/Infrastructure --startup-project src/API.Host --context TeachersDbContext`
Sonra `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `teacher_certificates` tablosu; PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(teachers): sertifika/deneyim ekleme (T-02.12)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Dokümantasyon

**Files:** `doc/modules/m02_teachers.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/roles/ogretmen.md`

- [ ] **Step 1: m02_teachers.md** — `Subjects` (çoklu branş, birincil korunur), `TeacherCertificate`; profil upsert'e branş listesi + sertifika. Tarih 2026-07-18.
- [ ] **Step 2: 00_genel_bakis.md** — Teachers bölümüne yeni alanlar notu.
- [ ] **Step 3: veri_modeli.md** — `teacher_subjects`, `teacher_certificates` tabloları + ER.
- [ ] **Step 4: ogretmen.md §10.2/§10.3** — M02 çoklu branş + sertifika "✅ yapıldı (Dilim D)"; §10.2 #1 çözüldü işaretle.
- [ ] **Step 5: Commit**
```bash
git add -A
git commit -m "docs: öğretmen profili olgunluk (Dilim D) doküman güncellemesi

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notu
- **Spec kapsamı:** D.1 çoklu branş (Task 1), D.2 sertifika (Task 2). Karşılandı.
- **Tek imza değişikliği:** `Update`'e hem `subjects` hem `certificates` Task 1'de eklenir (iki kez imza kırmamak için); Certificates Application tarafı Task 2'de doldurulur.
- **Geriye uyum:** Birincil `Subject` korunur; backfill mevcut profilleri `teacher_subjects`'e taşır.
- **Bilinmeyen doğrulama:** `TeacherProfile` içindeki `AvailabilitySlots` ilişki tanımı (HasMany/OwnsMany) bulunup `Subjects`/`Certificates` için birebir kopyalanır (Task 1 Step 4, Task 2 Step 3).
