# Öğrenci İlişki Modeli (Dilim C) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** M03'e çoklu öğretmen bağlantı tablosu (`TeacherStudentLink`), free limit=5, öğrenci bazlı ücret (B-07), arşivleme (B-04) ve davet/bağlanma (B-06) eklemek.

**Architecture:** Students modülüne yeni `TeacherStudentLink` aggregate. Manuel öğrenci oluşturma link üretir; listeleme link üzerinden yürür. Davet/kabul, Parents `children/link+approve` desenini izler. Mevcut `CreatedByTeacherUserId` alanı geriye-uyum için korunur ve migration'da link'e backfill edilir.

**Tech Stack:** .NET 9, EF Core (PostgreSQL, `students` şeması), xUnit (`tests/Unit`), CQRS + `Result`/`Error`, `IClock`/`IIdGenerator`, `IUserDirectory`/`Shared.Contracts` (kullanıcı arama için — mevcut kontrat doğrulanacak).

## Global Constraints
- Modüller birbirine referans veremez; kullanıcı arama (davet) `Shared/Contracts` kontratı üzerinden (Parents'ın kullandığı deseni izle).
- Migration komutu: `dotnet ef migrations add <Ad> --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
- Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` · Build: `dotnet build EgitimUssu.sln`
- `FreeStudentLimit = 5` sabiti; premium bypass yok (M17 gelince eklenecek — kodda `// TODO(M17)`).
- Zamanlar UTC + `IClock.UtcNow`. Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Doküman bakımı son task'ta.

## File Structure
- `src/Modules/Students/Domain/StudentsDomainModel.cs` — `TeacherStudentLink` aggregate + `TeacherStudentLinkStatus` enum + event'ler.
- `src/Modules/Students/Application/StudentProfileFeatures.cs` — create → link + limit; list → link + arşiv filtresi; response alanları.
- `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs` *(yeni)* — archive/unarchive/rate/invite/accept/reject command+handler+authorizer + repository arayüzü.
- `src/Modules/Students/API/StudentsModule.cs` — yeni endpoint'ler + DTO.
- `src/Modules/Students/Infrastructure/StudentsDbContext.cs` — `DbSet` + config.
- `src/Modules/Students/Infrastructure/TeacherStudentLinkRepository.cs` *(yeni)*.
- `src/Modules/Students/Infrastructure/DependencyInjection.cs` — DI.
- Migrations: `AddTeacherStudentLinks` (+ backfill SQL).
- `tests/Unit/TeacherStudentLinkTests.cs`, `tests/Unit/StudentFreeLimitTests.cs` *(yeni)*.

---

### Task 1: `TeacherStudentLink` aggregate + repository + migration (backfill)

**Files:**
- Modify: `src/Modules/Students/Domain/StudentsDomainModel.cs`
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs` (repository arayüzü — yeni dosyaya taşımak yerine burada `ITeacherStudentLinkRepository` ayrı tanımlanır: TeacherStudentLinkFeatures.cs'de)
- Create: `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs` (yalnız repository arayüzü + enum kullanımları bu task'ta)
- Create: `src/Modules/Students/Infrastructure/TeacherStudentLinkRepository.cs`
- Modify: `src/Modules/Students/Infrastructure/StudentsDbContext.cs`
- Modify: `src/Modules/Students/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/TeacherStudentLinkTests.cs` (create)

**Interfaces:**
- Produces: `enum TeacherStudentLinkStatus { Manual=1, InviteSent=2, Linked=3, Rejected=4, Disconnected=5 }`. `TeacherStudentLink` aggregate: ctor `(Guid id, Guid teacherUserId, Guid studentId, TeacherStudentLinkStatus status, DateTime createdOnUtc)`; property'ler `AgreedRateAmount? (decimal)`, `Currency (string="TRY")`, `IsArchived (bool)`, `Status`, `InviteTargetUserId? (Guid)`; metotlar `SetRate(decimal amount, string currency, DateTime)`, `Archive(DateTime)`, `Unarchive(DateTime)`, `MarkInviteSent(Guid targetUserId, DateTime)`, `Accept(DateTime)`, `Reject(DateTime)`. `ITeacherStudentLinkRepository`: `AddAsync`, `GetByIdAsync`, `GetByTeacherAndStudentAsync`, `CountByTeacherAsync(teacherUserId)`, `ListByTeacherAsync(teacherUserId, includeArchived)`, `SaveChangesAsync`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/TeacherStudentLinkTests.cs`:
```csharp
using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherStudentLinkTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherStudentLink New(TeacherStudentLinkStatus status = TeacherStudentLinkStatus.Manual)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status, Now);

    [Fact]
    public void SetRate_StoresAmountAndCurrency()
    {
        var link = New();
        link.SetRate(450m, "TRY", Now);
        Assert.Equal(450m, link.AgreedRateAmount);
        Assert.Equal("TRY", link.Currency);
    }

    [Fact]
    public void ArchiveUnarchive_TogglesFlag()
    {
        var link = New();
        link.Archive(Now);
        Assert.True(link.IsArchived);
        link.Unarchive(Now);
        Assert.False(link.IsArchived);
    }

    [Fact]
    public void InviteAcceptReject_TransitionsStatus()
    {
        var target = Guid.NewGuid();
        var link = New();
        link.MarkInviteSent(target, Now);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(target, link.InviteTargetUserId);

        link.Accept(Now);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);

        var link2 = New();
        link2.MarkInviteSent(target, Now);
        link2.Reject(Now);
        Assert.Equal(TeacherStudentLinkStatus.Rejected, link2.Status);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: FAIL — `TeacherStudentLink` yok.

- [ ] **Step 3: Add aggregate + enum + events**

`StudentsDomainModel.cs` sonuna:
```csharp
public sealed class TeacherStudentLink : AggregateRoot<Guid>
{
    private TeacherStudentLink() { }

    public TeacherStudentLink(Guid id, Guid teacherUserId, Guid studentId, TeacherStudentLinkStatus status, DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Status = status;
        Currency = "TRY";
        IsArchived = false;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    public Guid TeacherUserId { get; private set; }
    public Guid StudentId { get; private set; }
    public decimal? AgreedRateAmount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public TeacherStudentLinkStatus Status { get; private set; }
    public bool IsArchived { get; private set; }
    public Guid? InviteTargetUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime UpdatedOnUtc { get; private set; }

    public void SetRate(decimal amount, string currency, DateTime updatedOnUtc)
    {
        AgreedRateAmount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim();
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Archive(DateTime updatedOnUtc) { IsArchived = true; UpdatedOnUtc = updatedOnUtc; }
    public void Unarchive(DateTime updatedOnUtc) { IsArchived = false; UpdatedOnUtc = updatedOnUtc; }

    public void MarkInviteSent(Guid targetUserId, DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.InviteSent;
        InviteTargetUserId = targetUserId;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new TeacherStudentInviteSentDomainEvent(Id, TeacherUserId, StudentId, targetUserId, updatedOnUtc));
    }

    public void Accept(DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.Linked;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new TeacherStudentLinkAcceptedDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }

    public void Reject(DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.Rejected;
        UpdatedOnUtc = updatedOnUtc;
    }
}

public enum TeacherStudentLinkStatus
{
    Manual = 1,
    InviteSent = 2,
    Linked = 3,
    Rejected = 4,
    Disconnected = 5
}

public sealed record TeacherStudentInviteSentDomainEvent(
    Guid LinkId, Guid TeacherUserId, Guid StudentId, Guid TargetUserId, DateTime OnUtc) : DomainEvent;

public sealed record TeacherStudentLinkAcceptedDomainEvent(
    Guid LinkId, Guid TeacherUserId, Guid StudentId, DateTime OnUtc) : DomainEvent;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: PASS.

- [ ] **Step 5: Repository arayüzü + impl + config + DI**

`TeacherStudentLinkFeatures.cs` (yeni) — şimdilik yalnız arayüz:
```csharp
using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Modules.Students.Application;

public interface ITeacherStudentLinkRepository
{
    Task AddAsync(TeacherStudentLink link, CancellationToken cancellationToken);
    Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken cancellationToken);
    Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);
    Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

`TeacherStudentLinkRepository.cs` (yeni):
```csharp
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class TeacherStudentLinkRepository : ITeacherStudentLinkRepository
{
    private readonly StudentsDbContext _dbContext;
    public TeacherStudentLinkRepository(StudentsDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(TeacherStudentLink link, CancellationToken ct)
        => _dbContext.TeacherStudentLinks.AddAsync(link, ct).AsTask();

    public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct)
        => _dbContext.TeacherStudentLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);

    public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct)
        => _dbContext.TeacherStudentLinks.FirstOrDefaultAsync(l => l.TeacherUserId == teacherUserId && l.StudentId == studentId, ct);

    public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct)
        => _dbContext.TeacherStudentLinks.CountAsync(l => l.TeacherUserId == teacherUserId && l.Status != TeacherStudentLinkStatus.Rejected, ct);

    public async Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct)
        => await _dbContext.TeacherStudentLinks
            .Where(l => l.TeacherUserId == teacherUserId && l.Status != TeacherStudentLinkStatus.Rejected && (includeArchived || !l.IsArchived))
            .ToArrayAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _dbContext.SaveChangesAsync(ct);
}
```
> Limit sayımı `CountByTeacherAsync` reddedilenleri saymaz; arşivli sayılır (toplam limit, arşiv boşaltmaz).

`StudentsDbContext.cs`:
- `DbSet` ekle: `public DbSet<TeacherStudentLink> TeacherStudentLinks => Set<TeacherStudentLink>();`
- Config:
```csharp
internal sealed class TeacherStudentLinkConfiguration : IEntityTypeConfiguration<TeacherStudentLink>
{
    public void Configure(EntityTypeBuilder<TeacherStudentLink> builder)
    {
        builder.ToTable("teacher_student_links");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entity => entity.AgreedRateAmount).HasPrecision(12, 2);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StudentId }).IsUnique();
    }
}
```
> Namespace başında `using Microsoft.EntityFrameworkCore.Metadata.Builders;` mevcut (diğer config'ler kullanıyor).

`DependencyInjection.cs`:
```csharp
        services.AddScoped<ITeacherStudentLinkRepository, TeacherStudentLinkRepository>();
```

- [ ] **Step 6: Migration + backfill SQL**

Run: `dotnet ef migrations add AddTeacherStudentLinks --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
Üretilen migration'ın `Up` metodunun **sonuna** backfill ekle (mevcut manuel öğrenciler için `Manual` link):
```csharp
            migrationBuilder.Sql(@"
                INSERT INTO students.teacher_student_links
                    (""Id"", ""TeacherUserId"", ""StudentId"", ""Status"", ""Currency"", ""IsArchived"", ""CreatedOnUtc"", ""UpdatedOnUtc"")
                SELECT gen_random_uuid(), sp.""CreatedByTeacherUserId"", sp.""Id"", 'Manual', 'TRY', false, now(), now()
                FROM students.student_profiles sp
                WHERE sp.""CreatedByTeacherUserId"" IS NOT NULL;");
```
> Kolon adları `SchedulingDbContextModelSnapshot`/mevcut snapshot'taki gerçek adlarla (tırnaklı PascalCase) eşleşmeli; migration üretildikten sonra `student_profiles` tablosunun gerçek kolon adlarını migration snapshot'tan doğrula. `gen_random_uuid()` için pgcrypto; yoksa `uuid_generate_v4()` veya app-side backfill'e geç.

- [ ] **Step 7: Build + test**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(students): TeacherStudentLink bağlantı tablosu + backfill (çoklu öğretmen)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Manuel öğrenci oluşturma → link + free limit=5

**Files:**
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs` (error eşleme)
- Test: `tests/Unit/StudentFreeLimitTests.cs` (create)

**Interfaces:**
- Produces: `CreateStudentProfileCommandHandler` artık `ITeacherStudentLinkRepository` + `IIdGenerator` kullanır. Yeni error `students.free_limit_reached`. Sabit `StudentLimits.FreeStudentLimit = 5` (StudentProfileFeatures.cs içinde `internal static class`).

- [ ] **Step 1: Write the failing test (limit handler seviyesinde)**

`tests/Unit/StudentFreeLimitTests.cs` — sahte `ITeacherStudentLinkRepository` (yalnız `CountByTeacherAsync` + `AddAsync` + `SaveChangesAsync`) ve sahte `IStudentProfileRepository`. `CountByTeacherAsync` 5 döndürürse handler `students.free_limit_reached` döndürmeli.
```csharp
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentFreeLimitTests
{
    private sealed class FakeClock : IClock { public DateTime UtcNow => new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class FakeIds : IIdGenerator { public Guid New() => Guid.NewGuid(); }

    private sealed class FullLinkRepo : ITeacherStudentLinkRepository
    {
        public Task AddAsync(TeacherStudentLink l, CancellationToken ct) => Task.CompletedTask;
        public Task<TeacherStudentLink?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid t, Guid s, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountByTeacherAsync(Guid t, CancellationToken ct) => Task.FromResult(5);
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid t, bool inc, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    // FakeStudentProfileRepository: IStudentProfileRepository'yi minimal implemente et (AddAsync/SaveChangesAsync no-op, diğerleri NotImplemented).

    [Fact]
    public async Task Create_TeacherManaged_AtLimit_Fails()
    {
        // handler'ı FullLinkRepo + FakeStudentProfileRepository ile kur;
        // Origin=TeacherManaged, CreatedByTeacherUserId dolu bir CreateStudentProfileCommand gönder;
        // sonuç IsSuccess=false ve Error.Code == "students.free_limit_reached" olmalı.
    }
}
```
> `IStudentProfileRepository` üyelerini `StudentProfileFeatures.cs`'den kopyalayıp minimal sahte yaz. Test gövdesini bu arayüze göre tamamla.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudentFreeLimitTests`
Expected: FAIL — handler limit kontrolü yapmıyor / `students.free_limit_reached` yok.

- [ ] **Step 3: Update create handler**

`StudentProfileFeatures.cs`:
- Sabit + error:
```csharp
internal static class StudentLimits
{
    public const int FreeStudentLimit = 5; // TODO(M17): premium sınırsız
}
```
- `CreateStudentProfileCommandHandler` ctor'una `ITeacherStudentLinkRepository linkRepository` ve (yoksa) `IIdGenerator idGenerator` ekle (alanlar + atama).
- Error ekle: `private static readonly Error FreeLimitReached = new("students.free_limit_reached", "Free planda en fazla 5 ogrenci ekleyebilirsiniz. Premium'a gecin.");`
- `Handle` içinde, Origin=TeacherManaged doğrulamasından sonra, profil oluşturmadan önce:
```csharp
        if (command.Origin == StudentOrigin.TeacherManaged && command.CreatedByTeacherUserId is { } teacherId)
        {
            var count = await _linkRepository.CountByTeacherAsync(teacherId, cancellationToken);
            if (count >= StudentLimits.FreeStudentLimit)
            {
                return Result<StudentProfileResponse>.Failure(FreeLimitReached);
            }
        }
```
- Profil `SaveChangesAsync`'ten sonra (aynı akış), manuel ise link oluştur:
```csharp
        if (command.Origin == StudentOrigin.TeacherManaged && command.CreatedByTeacherUserId is { } linkTeacherId)
        {
            var link = new TeacherStudentLink(_idGenerator.New(), linkTeacherId, profile.Id, TeacherStudentLinkStatus.Manual, _clock.UtcNow);
            await _linkRepository.AddAsync(link, cancellationToken);
            await _linkRepository.SaveChangesAsync(cancellationToken);
        }
```
> `profile` değişken adını handler'daki gerçek adla eşle (mevcut kodda oluşturulan `StudentProfile`).

`StudentsModule.cs` — `ToHttpResult` switch'ine `students.free_limit_reached` → 409 (veya 400) ekle.

- [ ] **Step 4: DI güncelle**

`DependencyInjection.cs` — `CreateStudentProfileCommandHandler` kaydı zaten var; ctor'a eklenen bağımlılıklar (`ITeacherStudentLinkRepository`, `IIdGenerator`) DI'da mevcut (link repo Task 1'de eklendi, `IIdGenerator` shared). Ek kayıt gerekmez.

- [ ] **Step 5: Run test + build**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudentFreeLimitTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(students): manuel öğrenci → link + free limit 5 (B-04 temel)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Listeleme link üzerinden + arşiv filtresi + response alanları

**Files:**
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs`
- Test: `tests/Unit/StudentFreeLimitTests.cs` veya yeni (list davranışı — sahte repo ile)

**Interfaces:**
- Produces: `ListStudentsByTeacherQuery(Guid TeacherUserId, bool IncludeArchived = false)`. `StudentProfileSummaryResponse` sonuna `bool IsArchived`, `decimal? AgreedRateAmount`, `string LinkStatus`. Handler linkler → profiller birleşimini döndürür.

- [ ] **Step 1: Update query + handler**

`StudentProfileFeatures.cs`:
- `ListStudentsByTeacherQuery`'ye `bool IncludeArchived = false` ekle.
- `StudentProfileSummaryResponse` sonuna alanlar ekle: `bool IsArchived`, `decimal? AgreedRateAmount`, `string LinkStatus`.
- `ListStudentsByTeacherQueryHandler`: `ITeacherStudentLinkRepository`'yi enjekte et; `ListByTeacherAsync(teacherUserId, includeArchived)` ile linkleri al, `StudentId`'lerle profilleri getir (mevcut repo'da toplu getirici yoksa `GetByIdAsync` döngüsü veya yeni `ListByIdsAsync`), her özetde link alanlarını doldur. Sıralama ada göre.
> Mevcut handler `CreatedByTeacherUserId` filtresini kullanıyorsa, onu link tabanlı listeye çevir. `IStudentProfileRepository`'ye gerekiyorsa `Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken)` ekle + Infrastructure'da implemente et (`Where(p => ids.Contains(p.Id))`).

- [ ] **Step 2: Update endpoint**

`StudentsModule.cs` — `ListStudentsByTeacherAsync` handler'ına `bool includeArchived = false` query parametresi ekle; query'ye geçir.

- [ ] **Step 3: Build + test (mevcut testler + yeni liste testi)**

`tests/Unit/` içine arşivli linkin varsayılan listede görünmediğini, `includeArchived=true`'da göründüğünü doğrulayan bir handler testi ekle (sahte `ITeacherStudentLinkRepository.ListByTeacherAsync` iki farklı sonuç döndürür).
Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(students): öğrenci listesi link üzerinden + arşiv filtresi (B-04)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Arşivle / arşivden çıkar + öğrenci bazlı ücret (B-07)

**Files:**
- Modify: `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs` (command+handler+authorizer)
- Modify: `src/Modules/Students/API/StudentsModule.cs`
- Modify: `src/Modules/Students/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/TeacherStudentLinkTests.cs` (authorizer/handler)

**Interfaces:**
- Produces: `ArchiveTeacherStudentLinkCommand(Guid TeacherUserId, Guid StudentId, bool Archive)`, `SetTeacherStudentRateCommand(Guid TeacherUserId, Guid StudentId, decimal AgreedRateAmount, string Currency)`. Endpoint'ler `POST .../teachers/{teacherUserId}/students/{studentId}/archive`, `/unarchive`, `PUT .../teachers/{teacherUserId}/students/{studentId}/rate`. Authorizer: `currentUser == teacherUserId` (admin serbest).

- [ ] **Step 1: Write the failing test (authorizer reddi)**

`tests/Unit/TeacherStudentLinkTests.cs`'e ekle: başka öğretmenin `SetTeacherStudentRateCommand`'ı reddedilir (authorizer). Sahte `ICurrentUser` (Teacher, farklı id) → `Authorize` `IsSuccess=false`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: FAIL — command/authorizer yok.

- [ ] **Step 3: Implement commands + handlers + authorizer**

`TeacherStudentLinkFeatures.cs`'e ekle (arayüzün altına). Handler deseni: `GetByTeacherAndStudentAsync` → yoksa `students.link_not_found` → `Archive/Unarchive/SetRate` → `SaveChangesAsync`. Authorizer `CanManageTeacher(teacherUserId)` (Scheduling `LessonScheduleCommandAuthorizer.CanManageTeacher` mantığını kopyala).
```csharp
public sealed record ArchiveTeacherStudentLinkCommand(Guid TeacherUserId, Guid StudentId, bool Archive) : ICommand<Result>;
public sealed record SetTeacherStudentRateCommand(Guid TeacherUserId, Guid StudentId, decimal AgreedRateAmount, string Currency) : ICommand<Result>;

public sealed class ArchiveTeacherStudentLinkCommandHandler : ICommandHandler<ArchiveTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IClock _clock;
    public ArchiveTeacherStudentLinkCommandHandler(ITeacherStudentLinkRepository repository, IClock clock) { _repository = repository; _clock = clock; }

    public async Task<Result> Handle(ArchiveTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null) return Result.Failure(NotFound);
        if (command.Archive) link.Archive(_clock.UtcNow); else link.Unarchive(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetTeacherStudentRateCommandHandler : ICommandHandler<SetTeacherStudentRateCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IClock _clock;
    public SetTeacherStudentRateCommandHandler(ITeacherStudentLinkRepository repository, IClock clock) { _repository = repository; _clock = clock; }

    public async Task<Result> Handle(SetTeacherStudentRateCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null) return Result.Failure(NotFound);
        link.SetRate(command.AgreedRateAmount, command.Currency, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class TeacherStudentLinkAuthorizer :
    ICommandAuthorizer<ArchiveTeacherStudentLinkCommand>,
    ICommandAuthorizer<SetTeacherStudentRateCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ogrenci uzerinde islem yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    public TeacherStudentLinkAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(ArchiveTeacherStudentLinkCommand command, CancellationToken ct)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    public Task<Result> Authorize(SetTeacherStudentRateCommand command, CancellationToken ct)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private bool CanManage(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated) return false;
        if (_currentUser.Roles.Contains("Admin")) return true;
        return _currentUser.Roles.Contains("Teacher") && Guid.TryParse(_currentUser.UserId, out var id) && id == teacherUserId;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: PASS.

- [ ] **Step 5: Endpoints + DI**

`StudentsModule.cs`:
```csharp
        group.MapPost("/teachers/{teacherUserId:guid}/students/{studentId:guid}/archive", ArchiveStudentAsync);
        group.MapPost("/teachers/{teacherUserId:guid}/students/{studentId:guid}/unarchive", UnarchiveStudentAsync);
        group.MapPut("/teachers/{teacherUserId:guid}/students/{studentId:guid}/rate", SetStudentRateAsync);
```
Handler metotları — dispatch + `ToHttpResult`/`NoContent`:
```csharp
    private static async Task<IResult> ArchiveStudentAsync(HttpContext c, Guid teacherUserId, Guid studentId, ICommandDispatcher d, CancellationToken ct)
    {
        var r = await d.Dispatch(new ArchiveTeacherStudentLinkCommand(teacherUserId, studentId, true), ct);
        return r.IsSuccess ? Results.NoContent() : MapLinkError(c, r);
    }
    private static async Task<IResult> UnarchiveStudentAsync(HttpContext c, Guid teacherUserId, Guid studentId, ICommandDispatcher d, CancellationToken ct)
    {
        var r = await d.Dispatch(new ArchiveTeacherStudentLinkCommand(teacherUserId, studentId, false), ct);
        return r.IsSuccess ? Results.NoContent() : MapLinkError(c, r);
    }
    private static async Task<IResult> SetStudentRateAsync(HttpContext c, Guid teacherUserId, Guid studentId, SetStudentRateRequest request, ICommandDispatcher d, CancellationToken ct)
    {
        var r = await d.Dispatch(new SetTeacherStudentRateCommand(teacherUserId, studentId, request.AgreedRateAmount, request.Currency), ct);
        return r.IsSuccess ? Results.NoContent() : MapLinkError(c, r);
    }

    private static IResult MapLinkError(HttpContext context, Result result)
        => result.Error.Code switch
        {
            "students.link_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
```
DTO: `public sealed record SetStudentRateRequest(decimal AgreedRateAmount, string Currency);`

`DependencyInjection.cs`:
```csharp
        services.AddScoped<ICommandHandler<ArchiveTeacherStudentLinkCommand, Result>, ArchiveTeacherStudentLinkCommandHandler>();
        services.AddScoped<ICommandHandler<SetTeacherStudentRateCommand, Result>, SetTeacherStudentRateCommandHandler>();
        services.AddScoped<ICommandAuthorizer<ArchiveTeacherStudentLinkCommand>, TeacherStudentLinkAuthorizer>();
        services.AddScoped<ICommandAuthorizer<SetTeacherStudentRateCommand>, TeacherStudentLinkAuthorizer>();
```

- [ ] **Step 6: Build + test**

Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(students): arşivleme + öğrenci bazlı ücret (B-04/B-07)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Davet / kabul / red (B-06)

**Files:**
- Modify: `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs`
- Modify: `src/Modules/Students/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/TeacherStudentLinkTests.cs`

**Interfaces (KARAR 2026-07-18):** Kullanıcı-arama kontratı **yok**; Parents e-posta/telefonla değil `StudentId` ile bağlanıyor. Identity modülüne dokunulmaz. Davet, mevcut manuel öğrenci link'i üzerinden yürür; kabul eden `currentUser` öğrenci profiline bağlanır.
- Produces: `InviteStudentCommand(Guid TeacherUserId, Guid StudentId, Guid? TargetUserId)`, `AcceptTeacherStudentLinkCommand(Guid LinkId, Guid AcceptingUserId)`, `RejectTeacherStudentLinkCommand(Guid LinkId, Guid RejectingUserId)`. Endpoint'ler `POST .../teachers/{teacherUserId}/students/{studentId}/invite`, `POST .../links/{linkId}/accept`, `POST .../links/{linkId}/reject` (accept/reject `currentUser`'dan kimliği alır). `TeacherStudentLink.MarkInviteSent(Guid? targetUserId, ...)`; kabulde `StudentProfile.LinkUser(Guid userId, DateTime)`. Error'lar `students.link_not_found`. Davet authorizer'ı `CanManage(teacherUserId)`; accept/reject authorizer'ı link'in `InviteTargetUserId == currentUser` (belirli hedef varsa) veya admin.

- [ ] **Step 1: Discover user-directory contract**

Run: `grep -rnE 'IUserDirectory|IUserLookup|FindUserBy|GetUserByEmail|children/link' src/Shared/Contracts src/Modules/Parents --include='*.cs' | grep -v obj | head`
Expected: Parents'ın kullanıcıyı e-posta/telefonla nasıl bulup link kurduğu kontrat/servis adını not al. Aynı kontratı Students'ta kullan (yoksa Parents'ın kullandığı `Shared/Contracts` arayüzünü referansla).

- [ ] **Step 2: Write the failing test (invite→accept happy path, sahte repo + sahte directory)**

`tests/Unit/TeacherStudentLinkTests.cs`'e handler testi ekle: `InviteStudentCommand` → directory kullanıcı döndürür → link `InviteSent`; `AcceptTeacherStudentLinkCommand` → `Linked`. (Sahte `ITeacherStudentLinkRepository` + sahte kullanıcı directory.)

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: FAIL — invite/accept command yok.

- [ ] **Step 4: Implement invite/accept/reject handlers + authorizer**

`TeacherStudentLinkFeatures.cs`'e ekle:
- `InviteStudentCommandHandler`: `GetByTeacherAndStudentAsync` ile mevcut link'i al (manuel öğrenci) veya yeni oluştur; directory ile `Email`/`Phone`'dan `targetUserId` bul (yoksa `students.user_not_found`); `link.MarkInviteSent(targetUserId, now)`; kaydet.
- `AcceptTeacherStudentLinkCommandHandler`/`RejectTeacherStudentLinkCommandHandler`: `GetByIdAsync` → `Accept`/`Reject` → kaydet. Accept'te ilgili `StudentProfile.UserId` bağlama (mevcut `LinkParent` benzeri bir `LinkUser` metodu gerekebilir; yoksa StudentProfile'a `LinkUser(Guid userId, DateTime)` ekle — Task 1 kapsamı dışıysa burada ekle).
- Authorizer: invite → `CanManage(teacherUserId)`; accept/reject → link'in `InviteTargetUserId == currentUser` (öğrenci kendi davetini yanıtlar) veya admin.

Detay davranışı Step 1'de bulunan Parents deseniyle hizala (bildirim event'i outbox'a).

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TeacherStudentLinkTests`
Expected: PASS.

- [ ] **Step 6: Endpoints + DI + build + test**

`StudentsModule.cs` — invite/accept/reject endpoint'leri + DTO (`InviteStudentRequest(string? Email, string? Phone)`) + `MapLinkError`'a `students.user_not_found` → 404 ekle.
`DependencyInjection.cs` — 3 handler + authorizer kaydı.
Run: `dotnet build EgitimUssu.sln` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(students): öğrenci davet/kabul/red akışı (B-06)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: Dokümantasyon

**Files:** `doc/modules/m03_students.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/roles/ogretmen.md`, `doc/roles/00_roller_genel_bakis.md`

- [ ] **Step 1: m03_students.md** — `TeacherStudentLink`, çoklu öğretmen, free limit=5, arşiv, öğrenci bazlı ücret, davet akışı + yeni endpoint'ler. Tarih 2026-07-18.
- [ ] **Step 2: 00_genel_bakis.md** — Students endpoint envanteri + M03 durumu.
- [ ] **Step 3: veri_modeli.md** — `teacher_student_links` tablosu + ER (öğrenci↔öğretmen çoklu ilişki).
- [ ] **Step 4: ogretmen.md §10** — B-04/B-06/B-07 "✅ yapıldı (Dilim C)"; ilgili Kabul Kriterleri.
- [ ] **Step 5: 00_roller_genel_bakis.md** — "bir öğrenci birden fazla öğretmene bağlanabilir" kuralını ekle.
- [ ] **Step 6: Commit**
```bash
git add -A
git commit -m "docs: öğrenci ilişki modeli (Dilim C) doküman güncellemesi

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notu
- **Spec kapsamı:** C.1 link (Task 1), C.2/C.3 limit+create (Task 2), C.4 list+arşiv (Task 3), C.5 arşiv + C.6 ücret (Task 4), C.7 davet (Task 5). Karşılandı.
- **En büyük risk:** Task 1 backfill migration (kolon adları + `gen_random_uuid()`); Step 6 notu doğrulamayı zorunlu kılıyor.
- **Bilinmeyen doğrulama:** Task 5 Step 1'de kullanıcı-arama kontratı (Parents deseni) keşfedilir; `StudentProfile.LinkUser` gerekiyorsa Task 5 Step 4'te eklenir.
- **Tip tutarlılığı:** `TeacherStudentLinkStatus`, `ITeacherStudentLinkRepository` imzaları task'lar arası tutarlı; `students.link_not_found`/`students.free_limit_reached`/`students.user_not_found` error kodları tekil.
- **Karar (YAGNI):** Premium bypass (M17) yok; limit herkese 5. `Disconnected` durumu tanımlı ama bu dilimde endpoint'i yok (ileride bağlantı kesme).
