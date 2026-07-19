# Veli V-A — Öğrenci Doğum Tarihi Alanı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrenci profiline `DateOfBirth` (doğum tarihi) alanını uçtan uca ekle — ileride veli claim eşleşmesi ve yaş-bazlı politikaların temeli.

**Architecture:** M03 Students'a mevcut `TargetExam` alanının eklendiği desenin birebir aynısı: nullable `DateTime? DateOfBirth` domain alanı → create/update komut + request DTO + response + EF config + migration. **Yaş-bazlı onay/politika YOK, KVKK rızası YOK** (bunlar ayrı dilimlere ertelendi — kullanıcı kararı 2026-07-19). Alan yalnızca saklanır ve response'ta döner.

**Tech Stack:** .NET 9, EF Core (`students` şeması, PostgreSQL `date` tipi), xUnit, CQRS.

## Global Constraints
- Migration (Students): `dotnet ef migrations add <Ad> --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Mevcut `TargetExam` deseni izlenir (opsiyonel trailing parametre; ctor/Update sonuna eklenir; mevcut çağrılar bozulmaz).

## File Structure
- `src/Modules/Students/Domain/StudentsDomainModel.cs` — `DateOfBirth` property + ctor/`Update` trailing param.
- `src/Modules/Students/Application/StudentProfileFeatures.cs` — command'lara alan, handler'da geçiş, response + mapping.
- `src/Modules/Students/API/StudentsModule.cs` — request DTO'lara alan + `ToCommand`.
- `src/Modules/Students/Infrastructure/StudentsDbContext.cs` — EF config (`date` tipi).
- `src/Modules/Students/Infrastructure/Migrations/*` — `AddStudentDateOfBirth`.
- `tests/Unit/StudentProfileTests.cs` — domain testleri.
- Docs: `doc/modules/m03_students.md`, `doc/modules/veri_modeli.md`.

---

### Task 1: Domain — `DateOfBirth` alanı

**Files:**
- Modify: `src/Modules/Students/Domain/StudentsDomainModel.cs`
- Test: `tests/Unit/StudentProfileTests.cs`

**Interfaces:**
- Produces: `StudentProfile.DateOfBirth` (`DateTime?`, get; private set). Ctor kazanır trailing `DateTime? dateOfBirth = null`; `Update(...)` kazanır trailing `DateTime? dateOfBirth = null`.

- [ ] **Step 1: Write the failing tests** — `tests/Unit/StudentProfileTests.cs` içine, `SetTargetExam_UpdatesValueAndTimestamp` testinden sonra ekle:

```csharp
    [Fact]
    public void NewProfile_DefaultsToNullDateOfBirth()
    {
        var profile = NewProfile();
        Assert.Null(profile.DateOfBirth);
    }

    [Fact]
    public void Update_SetsDateOfBirth()
    {
        var profile = NewProfile();
        var dob = new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = Now.AddMinutes(5);

        profile.Update("Ali Veli", "8", null, null, null, null, true, later, TargetExam.None, dob);

        Assert.Equal(dob, profile.DateOfBirth);
        Assert.Equal(later, profile.UpdatedOnUtc);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentProfileTests"`
Expected: FAIL — derleme hatası `'StudentProfile' does not contain a definition for 'DateOfBirth'` ve `Update` çağrısı 10 argümanla eşleşmiyor.

- [ ] **Step 3: Add the property** — `StudentsDomainModel.cs`, `MembershipTier` property'sinden sonra (satır ~79 civarı), `CreatedOnUtc`'den önce ekle:

```csharp
    /// <summary>Öğrencinin doğum tarihi (opsiyonel). Yaş türetimi + veli claim eşleşmesi için temel (Veli V-A).</summary>
    public DateTime? DateOfBirth { get; private set; }
```

- [ ] **Step 4: Add ctor param + assignment** — Ctor imzasında son satırı değiştir:

```csharp
        DateTime createdOnUtc,
        TargetExam targetExam = TargetExam.None,
        DateTime? dateOfBirth = null)
```

Ctor gövdesinde `TargetExam = targetExam;` satırından sonra ekle:

```csharp
        DateOfBirth = dateOfBirth;
```

- [ ] **Step 5: Add `Update` param + assignment** — `Update(...)` imzasında son satırı değiştir:

```csharp
        DateTime updatedOnUtc,
        TargetExam targetExam = TargetExam.None,
        DateTime? dateOfBirth = null)
```

`Update` gövdesinde `TargetExam = targetExam;` satırından sonra ekle:

```csharp
        DateOfBirth = dateOfBirth;
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentProfileTests"`
Expected: PASS (dört test: TargetExam/Membership + iki yeni DateOfBirth testi).

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Students/Domain/StudentsDomainModel.cs tests/Unit/StudentProfileTests.cs
git commit -m "feat(students): öğrenci doğum tarihi domaini (Veli V-A)"
```

---

### Task 2: Application + API + Infra + migration

**Files:**
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs`
- Modify: `src/Modules/Students/Infrastructure/StudentsDbContext.cs`
- Create: migration `AddStudentDateOfBirth`

**Interfaces:**
- Consumes: `StudentProfile.DateOfBirth`, ctor/`Update` trailing `DateTime? dateOfBirth` (Task 1).
- Produces: `CreateStudentProfileCommand.DateOfBirth`, `UpdateStudentProfileCommand.DateOfBirth`, `StudentProfileResponse.DateOfBirth` (hepsi `DateTime?`).

- [ ] **Step 1: Add to commands** — `StudentProfileFeatures.cs`.

`CreateStudentProfileCommand`'ın son satırını değiştir:
```csharp
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null) : ICommand<Result<StudentProfileResponse>>;
```

`UpdateStudentProfileCommand`'ın son satırını değiştir:
```csharp
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null) : ICommand<Result<StudentProfileResponse>>;
```

- [ ] **Step 2: Add to response record** — `StudentProfileResponse` içinde `string TargetExam,` satırından sonra ekle:
```csharp
    DateTime? DateOfBirth,
```

- [ ] **Step 3: Thread through handlers** — `CreateStudentProfileCommandHandler.Handle` içindeki `new StudentProfile(...)` çağrısında son argümanı değiştir:
```csharp
            now,
            command.TargetExam,
            command.DateOfBirth);
```

`UpdateStudentProfileCommandHandler.Handle` içindeki `profile.Update(...)` çağrısında son argümanı değiştir:
```csharp
            _clock.UtcNow,
            command.TargetExam,
            command.DateOfBirth);
```

- [ ] **Step 4: Thread through mapping** — `StudentProfileMappings.ToResponseWithSubjects` içinde `profile.TargetExam.ToString(),` satırından sonra ekle:
```csharp
            profile.DateOfBirth,
```

- [ ] **Step 5: Add to request DTOs** — `StudentsModule.cs`.

`UpdateStudentProfileRequest`'in son parametre satırını değiştir ve `ToCommand`'a ekle:
```csharp
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null)
```
`ToCommand` içindeki `new UpdateStudentProfileCommand(...)` son argümanını değiştir:
```csharp
            TargetExam,
            DateOfBirth);
```

`CreateStudentProfileRequest`'in son parametre satırını değiştir ve `ToCommand`'a ekle:
```csharp
    TargetExam TargetExam = TargetExam.None,
    DateTime? DateOfBirth = null)
```
`ToCommand` içindeki `new CreateStudentProfileCommand(...)` son argümanını değiştir:
```csharp
            TargetExam,
            DateOfBirth);
```

- [ ] **Step 6: EF config** — `StudentsDbContext.cs`, `MembershipTier` property config satırından sonra ekle:
```csharp
        builder.Property(entity => entity.DateOfBirth).HasColumnType("date");
```

- [ ] **Step 7: Build**

Run: `dotnet build EgitimUssu.slnx`
Expected: `0 Hata`.

- [ ] **Step 8: Generate migration**

Run: `dotnet ef migrations add AddStudentDateOfBirth --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
Expected: `Done.` — üretilen `Up` içinde `AddColumn<DateTime>(name: "DateOfBirth", ... type: "date", nullable: true)`.

- [ ] **Step 9: Build + full test**

Run: `dotnet build EgitimUssu.slnx` then `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `0 Hata`; tüm testler PASS (yeni DateOfBirth testleri dahil).

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Students/
git commit -m "feat(students): doğum tarihi create/update/response + migration (Veli V-A)"
```

---

### Task 3: Dokümantasyon

**Files:**
- Modify: `doc/modules/m03_students.md`
- Modify: `doc/modules/veri_modeli.md`

- [ ] **Step 1: m03_students.md** — `StudentProfile` alan tablosuna satır ekle:
```
| `DateOfBirth` | `DateTime?` | **Doğum tarihi (opsiyonel, Veli V-A 2026-07-19).** DB `date`; yaş türetimi + veli claim eşleşmesi temeli. Yaş-bazlı politika/KVKK bu dilimde YOK. |
```
Ve API sözleşmesi bölümünde `CreateStudentProfileRequest`/`UpdateStudentProfileRequest`/`StudentProfileResponse` alan listelerine `DateOfBirth?` ekle. Alt tarihi 2026-07-19 yap.

- [ ] **Step 2: veri_modeli.md** — Bölüm 4 Students satırında `StudentProfile` alan parantezine `+ DateOfBirth (Veli V-A)` ekle. Footer güncelleme tarihine `Veli V-A: StudentProfile.DateOfBirth` notu ekle.

- [ ] **Step 3: Commit**

```bash
git add doc/
git commit -m "docs: öğrenci doğum tarihi alanı (Veli V-A)"
```

---

## Self-Review
- **Spec coverage:** Spec §3 V-A "öğrenci doğum tarihi/yaş alanı" → Task 1-2 karşılıyor. Yaş-bazlı politika + KVKK spec'te V-A kapsamındaydı ama kullanıcı kararıyla (2026-07-19) bu dilimden çıkarıldı → spec V-A maddesi buna göre güncellendi; ilgili işler ayrı dilim olarak kalır.
- **Placeholder:** Yok — tüm adımlar kesin kod/komut içerir.
- **Type consistency:** `DateOfBirth` her katmanda `DateTime?`; ctor/Update/command/DTO trailing opsiyonel parametre olarak eklenir (mevcut `TargetExam` deseniyle birebir); mevcut çağrılar (testler dahil) bozulmaz.
