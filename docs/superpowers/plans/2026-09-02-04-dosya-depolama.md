# P04 — Ortak Dosya Depolama, Profil Fotoğrafı ve Ders Kaynağı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dosyaları kalıcı ve ölçeklenebilir şekilde saklamak: `Shared`'da S3-uyumlu `IFileStorage` soyutlaması, ödev tesliminin bu soyutlamaya taşınması, öğretmen profil fotoğrafı yükleme ve ders kaynağı (`LessonResource`) paylaşımı — backend + mobil.

**Architecture:** `Shared/Infrastructure/Storage` altında `IFileStorage` (`SaveAsync`/`OpenReadAsync`/`DeleteAsync`/`Exists`) ve iki implementasyon: `LocalFileStorage` (geliştirme, disk) ve `S3FileStorage` (AWS SDK; AWS S3 / Cloudflare R2 / MinIO ile uyumlu). Dosyalar `{module}/{ownerId}/{fileId}{ext}` anahtarıyla saklanır; indirme daima **uygulama üzerinden yetki kontrolüyle** yapılır (public bucket yok). Assignments modülündeki `IAssignmentFileStorage` kaldırılıp ortak soyutlamaya geçilir. `LessonResource` yeni bir aggregate olarak Assignments modülüne eklenir (ders materyali = ödevle aynı sınır).

**Tech Stack:** .NET 9, `AWSSDK.S3`, EF Core, xUnit; Flutter `image_picker`/`file_picker`, `cached_network_image`.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-03**, açık soru **Q4**)

## Global Constraints

- **Public bucket yok:** Hiçbir dosya doğrudan URL ile erişilebilir olmaz; indirme `GET /api/.../attachment` üzerinden yetki kontrolüyle akıtılır.
- **Boyut ve tür sınırı:** Yükleme başına **10 MB**; izinli türler: `pdf, png, jpg, jpeg, webp, doc, docx, xls, xlsx, ppt, pptx, txt, zip`. İhlalde `storage.file_too_large` / `storage.unsupported_type`.
- **Fail-fast:** `Storage:Provider=S3` iken bucket/endpoint/kimlik eksikse uygulama açılmaz.
- **Geriye uyum:** Mevcut ödev teslim dosyaları kaybolmaz; Task 2'de göç adımı vardır.
- **Zaman:** `IClock.UtcNow`. **Kimlik:** `IIdGenerator.New()`. **Sonuç:** `Result`/`Result<T>`.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: `IFileStorage` + yerel ve S3 implementasyonları

**Files:**
- Create: `src/Shared/Infrastructure/Storage/IFileStorage.cs`
- Create: `src/Shared/Infrastructure/Storage/StoredFile.cs`
- Create: `src/Shared/Infrastructure/Storage/LocalFileStorage.cs`
- Create: `src/Shared/Infrastructure/Storage/S3FileStorage.cs`
- Create: `src/Shared/Infrastructure/Storage/FileValidation.cs`
- Create: `src/Shared/Infrastructure/Configuration/StorageOptions.cs`
- Create: `src/Shared/Infrastructure/Configuration/StorageOptionsGuard.cs`
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs`, `src/API.Host/Program.cs`, `appsettings.json`
- Modify: `src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj` (`AWSSDK.S3`)
- Test: `tests/Unit/LocalFileStorageTests.cs`, `tests/Unit/FileValidationTests.cs`, `tests/Unit/StorageOptionsGuardTests.cs`

**Interfaces:**
- Produces:
  - `sealed record StoredFile(string Key, string FileName, string ContentType, long SizeBytes)`
  - `interface IFileStorage { Task<StoredFile> SaveAsync(string keyPrefix, string fileName, string contentType, Stream content, CancellationToken ct = default); Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default); Task DeleteAsync(string key, CancellationToken ct = default); }`
  - `static class FileValidation { public const long MaxSizeBytes = 10 * 1024 * 1024; public static Result Validate(string fileName, long sizeBytes); }`
  - `sealed class StorageOptions { public string Provider = "Local"; public string LocalRootPath = "storage"; public string BucketName = ""; public string? ServiceUrl; public string Region = "auto"; public string AccessKeyId = ""; public string SecretAccessKey = ""; }`

- [ ] **Step 1: Doğrulama ve yerel depolama testlerini yaz (kırmızı)**

`tests/Unit/FileValidationTests.cs`:
```csharp
using EgitimUssu.Shared.Infrastructure.Storage;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class FileValidationTests
{
    [Fact]
    public void Should_Reject_Unsupported_Extension()
    {
        var result = FileValidation.Validate("virus.exe", 1024);
        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
    }

    [Fact]
    public void Should_Reject_Too_Large_File()
    {
        var result = FileValidation.Validate("odev.pdf", FileValidation.MaxSizeBytes + 1);
        Assert.True(result.IsFailure);
        Assert.Equal("storage.file_too_large", result.Error.Code);
    }

    [Fact]
    public void Should_Accept_Pdf_Within_Limit()
        => Assert.True(FileValidation.Validate("odev.pdf", 1024).IsSuccess);

    [Fact]
    public void Should_Be_Case_Insensitive()
        => Assert.True(FileValidation.Validate("FOTO.JPG", 1024).IsSuccess);
}
```

`tests/Unit/LocalFileStorageTests.cs`:
```csharp
[Fact]
public async Task Save_Then_OpenRead_Should_Return_Same_Content()
{
    var root = Path.Combine(Path.GetTempPath(), $"eu-storage-{Guid.NewGuid()}");
    var storage = new LocalFileStorage(Options.Create(new StorageOptions { LocalRootPath = root }));

    using var content = new MemoryStream("merhaba"u8.ToArray());
    var stored = await storage.SaveAsync("assignments/abc", "odev.txt", "text/plain", content);

    await using var read = await storage.OpenReadAsync(stored.Key);
    using var reader = new StreamReader(read!);
    Assert.Equal("merhaba", await reader.ReadToEndAsync());

    await storage.DeleteAsync(stored.Key);
    Assert.Null(await storage.OpenReadAsync(stored.Key));

    Directory.Delete(root, recursive: true);
}

[Fact]
public async Task Key_Should_Not_Allow_Path_Traversal()
{
    var root = Path.Combine(Path.GetTempPath(), $"eu-storage-{Guid.NewGuid()}");
    var storage = new LocalFileStorage(Options.Create(new StorageOptions { LocalRootPath = root }));
    Assert.Null(await storage.OpenReadAsync("../../etc/passwd"));
    Directory.Delete(root, recursive: true);
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~FileValidationTests|FullyQualifiedName~LocalFileStorageTests"`
Expected: FAIL — tipler yok.

- [ ] **Step 3: Sözleşme + doğrulama + yerel implementasyon**

`src/Shared/Infrastructure/Storage/IFileStorage.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Storage;

/// <summary>Depoda saklanan bir dosyanın kimliği ve meta verisi.</summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Nesne depolama soyutlaması. Modüller sağlayıcıyı bilmez; anahtar (key) ile çalışır.
/// Dosyalar hiçbir zaman public URL ile sunulmaz; indirme uygulama üzerinden yetkiyle yapılır.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(string keyPrefix, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
```

`FileValidation.cs`:
```csharp
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Storage;

public static class FileValidation
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx",
        ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".zip"
    };

    private static readonly Error TooLarge = new("storage.file_too_large", "Dosya 10 MB sınırını aşıyor.");
    private static readonly Error Unsupported = new("storage.unsupported_type", "Bu dosya türü desteklenmiyor.");

    public static Result Validate(string fileName, long sizeBytes)
    {
        if (sizeBytes > MaxSizeBytes)
        {
            return Result.Failure(TooLarge);
        }

        var extension = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(extension) ? Result.Success() : Result.Failure(Unsupported);
    }
}
```

`LocalFileStorage.cs` — anahtarı `{keyPrefix}/{Guid}{ext}` üretir; `OpenReadAsync` içinde **kök dizin dışına çıkan yolları reddeder** (`Path.GetFullPath` + `StartsWith(rootFullPath)` kontrolü); dosya yoksa `null` döner.

`S3FileStorage.cs` — `AmazonS3Client` (`ServiceUrl` doluysa `ForcePathStyle = true`, R2/MinIO uyumu); `SaveAsync` → `PutObjectAsync`, `OpenReadAsync` → `GetObjectAsync` (404'te `null`), `DeleteAsync` → `DeleteObjectAsync`.

`StorageOptions.cs` / `StorageOptionsGuard.cs` — `EmailOptionsGuard` deseninin eşleniği: `Provider=S3` iken `BucketName`, `AccessKeyId`, `SecretAccessKey` zorunlu.

- [ ] **Step 4: Testleri çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~FileValidationTests|FullyQualifiedName~LocalFileStorageTests|FullyQualifiedName~StorageOptionsGuardTests"`
Expected: PASS.

- [ ] **Step 5: DI + konfigürasyon + guard**

`ServiceCollectionExtensions.cs`:
```csharp
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            return string.Equals(options.Provider, "S3", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<S3FileStorage>(provider)
                : ActivatorUtilities.CreateInstance<LocalFileStorage>(provider);
        });
```
`appsettings.json`:
```json
  "Storage": {
    "Provider": "Local",
    "LocalRootPath": "storage",
    "BucketName": "",
    "ServiceUrl": "",
    "Region": "auto",
    "AccessKeyId": "",
    "SecretAccessKey": ""
  },
```
`Program.cs` → `StorageOptionsGuard.Validate(...)`.
`.gitignore` → `storage/`.

- [ ] **Step 6: Commit**

```bash
git add src/Shared/Infrastructure/Storage src/Shared/Infrastructure/Configuration src/API.Host tests/Unit .gitignore
git commit -m "feat(storage): S3-uyumlu IFileStorage soyutlamasi + yerel implementasyon (C-02)"
```

---

### Task 2: Ödev teslimini ortak depolamaya taşı

**Files:**
- Delete: `src/Modules/Assignments/Application/IAssignmentFileStorage.cs`
- Delete: `src/Modules/Assignments/Infrastructure/LocalAssignmentFileStorage.cs`
- Modify: `src/Modules/Assignments/Application/AssignmentStudentFeatures.cs` (yükleme/indirme handler'ları)
- Modify: `src/Modules/Assignments/Infrastructure/DependencyInjection.cs`
- Modify: `src/Modules/Assignments/API/AssignmentsModule.cs` (boyut/tür hatalarının HTTP eşlemesi)
- Create: `scripts/migrate-assignment-files.sh`
- Test: `tests/Unit/AssignmentSubmissionStorageTests.cs`

**Interfaces:**
- Consumes: `IFileStorage`, `FileValidation`.
- Produces: `AssignmentSubmission.StorageKey` alanı (eski `FilePath` yerine) — migration gerekir.

- [ ] **Step 1: Testi yaz (kırmızı)**

`tests/Unit/AssignmentSubmissionStorageTests.cs`:
```csharp
[Fact]
public async Task Upload_Should_Reject_Oversized_File()
{
    // handler'a 11 MB'lık stream ver → Result.IsFailure && Error.Code == "storage.file_too_large"
}

[Fact]
public async Task Upload_Should_Save_With_Assignments_Key_Prefix()
{
    // sahte IFileStorage: SaveAsync çağrısındaki keyPrefix "assignments/{assignmentId}" olmalı
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~AssignmentSubmissionStorageTests"`

- [ ] **Step 3: Handler'ları ortak soyutlamaya çevir**

`AssignmentStudentFeatures.cs` içindeki yükleme handler'ı:
```csharp
        var validation = FileValidation.Validate(command.FileName, command.Content.Length);
        if (validation.IsFailure)
        {
            return Result<AssignmentSubmissionResponse>.Failure(validation.Error);
        }

        var stored = await _fileStorage.SaveAsync(
            $"assignments/{command.AssignmentId}",
            command.FileName,
            command.ContentType,
            command.Content,
            cancellationToken);

        assignment.AttachSubmission(stored.Key, stored.FileName, stored.ContentType, stored.SizeBytes, _clock.UtcNow);
```
İndirme handler'ı `_fileStorage.OpenReadAsync(submission.StorageKey, ct)` kullanır; `null` ise `assignments.attachment_not_found`.
`IAssignmentFileStorage` bağımlılıklarını `IFileStorage` ile değiştir, iki dosyayı sil, DI kaydını kaldır.

- [ ] **Step 4: Domain alan adını ve migration'ı güncelle**

`AssignmentSubmission` içindeki dosya yolu alanını `StorageKey` olarak yeniden adlandır (eski değer aynı formatta kalabilir).
Run: `dotnet ef migrations add RenameSubmissionFilePathToStorageKey --project src/Modules/Assignments/Infrastructure --startup-project src/API.Host --context AssignmentsDbContext`

- [ ] **Step 5: Mevcut dosyalar için göç script'i**

`scripts/migrate-assignment-files.sh` — eski yerel klasördeki dosyaları yeni `IFileStorage` kök yoluna (veya S3 bucket'ına `aws s3 sync` ile) taşır; script başında ne yapacağını yazar ve `--dry-run` destekler.

- [ ] **Step 6: Testler + commit**

Run: `dotnet test EgitimUssu.slnx`
```bash
git add src/Modules/Assignments scripts tests/Unit
git commit -m "refactor(assignments): ödev dosyalarini ortak IFileStorage'a tasi (M06-2)"
```

---

### Task 3: Öğretmen profil fotoğrafı (M02-2 + D-05)

**Files:**
- Modify: `src/Modules/Teachers/Application/TeacherProfileFeatures.cs` (upload command + handler, `ITeacherProfileRepository`)
- Modify: `src/Modules/Teachers/Application/TeacherProfilePolicies.cs` (authorizer)
- Modify: `src/Modules/Teachers/API/TeachersModule.cs` (2 endpoint)
- Modify: `src/Modules/Teachers/Domain/TeachersDomainModel.cs` (`SetProfilePhoto`)
- Modify: `src/Modules/Teachers/Infrastructure/DependencyInjection.cs`
- Modify: `mobile/lib/features/teacher_profile/**` (repository + cubit + sayfa)
- Test: `tests/Unit/TeacherProfilePhotoTests.cs`, `mobile/test/features/teacher_profile/photo_upload_test.dart`

**Interfaces:**
- Produces:
  - `POST /api/teachers/profiles/{userId}/photo` (multipart, auth) → `{ photoKey: string }`
  - `GET /api/teachers/profiles/{userId}/photo` (auth) → dosya akışı
  - `sealed record UploadTeacherPhotoCommand(Guid UserId, string FileName, string ContentType, Stream Content) : ICommand<Result<TeacherPhotoResponse>>`
  - `TeacherProfile.SetProfilePhoto(string storageKey, DateTime updatedOnUtc)` — `ProfilePhotoUrl` alanı artık **storage key** tutar (alan adı `ProfilePhotoKey` olarak yeniden adlandırılır).

- [ ] **Step 1: Domain + handler testini yaz (kırmızı)** — yalnız profil sahibi (veya Admin) yükleyebilir; 10 MB üstü ve `.exe` reddedilir; başarılı yüklemede `ProfilePhotoKey` set edilir.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~TeacherProfilePhotoTests"`
- [ ] **Step 3: Domain + command + handler + authorizer'ı yaz**, `ProfilePhotoUrl` → `ProfilePhotoKey` yeniden adlandırması ve migration:
  Run: `dotnet ef migrations add RenameProfilePhotoUrlToKey --project src/Modules/Teachers/Infrastructure --startup-project src/API.Host --context TeachersDbContext`
  > `UpsertTeacherProfileRequest` içindeki `profilePhotoUrl` alanı **kaldırılır** — foto yalnız yükleme ucundan set edilir (Y1 dersi: istemci yazamamalı).
- [ ] **Step 4: Endpoint'leri ekle** — multipart okuma için `AssignmentsModule`'deki `POST /{assignmentId}/submission` desenini birebir izle.
- [ ] **Step 5: Testleri yeşile al** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Mobil yükleme akışı** — `teacher_profile_page.dart`'a avatar + "Fotoğraf ekle" (kamera/galeri, `image_picker`); yükleme sırasında `CircularProgressIndicator`; başarıda `cached_network_image` ile `GET .../photo` gösterilir (Authorization header'lı `CachedNetworkImageProvider` veya `Image.memory` ile indirilen bayt).
- [ ] **Step 7: Mobil test + analiz** — Run: `cd mobile && flutter test && flutter analyze`
- [ ] **Step 8: Doküman + commit**

`doc/modules/m02_teachers.md` (yeni uçlar + `ProfilePhotoKey`), `doc/pages/teacher_profile.md`.
```bash
git add src/Modules/Teachers mobile doc
git commit -m "feat(teachers): profil fotografi yukleme (M02-2/D-05)"
```

---

### Task 4: Ders kaynağı — `LessonResource` (M06-1)

**Files:**
- Modify: `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs` (yeni aggregate)
- Modify: `src/Modules/Assignments/Application/*` (command/query + handler + authorizer + repository)
- Modify: `src/Modules/Assignments/Infrastructure/*` (DbContext + repository + DI + migration)
- Modify: `src/Modules/Assignments/API/AssignmentsModule.cs` (4 endpoint)
- Modify: `mobile/lib/features/assignments/**` + `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart`
- Test: `tests/Unit/LessonResourceTests.cs`

**Interfaces:**
- Produces:
  - `sealed class LessonResource : AggregateRoot<Guid>` — `Guid TeacherUserId`, `Guid? StudentId` (null = tüm öğrencilere açık), `Guid? LessonSessionId`, `string Title`, `string? Description`, `LessonResourceKind Kind` (`File = 1, Link = 2`), `string? StorageKey`, `string? FileName`, `string? Url`, `DateTime CreatedOnUtc`, `bool IsActive`; metotlar `Archive(DateTime nowUtc)`.
  - `POST /api/assignments/lesson-resources` (multipart veya JSON-link, öğretmen)
  - `GET /api/assignments/lesson-resources?studentId=&lessonSessionId=` (öğretmen kendi kaynakları / öğrenci kendisine açık olanlar)
  - `GET /api/assignments/lesson-resources/{resourceId}/file` (yetkili indirme)
  - `DELETE /api/assignments/lesson-resources/{resourceId}` (arşivle)

- [ ] **Step 1: Domain testini yaz (kırmızı)**

`tests/Unit/LessonResourceTests.cs`:
```csharp
[Fact]
public void File_Resource_Requires_StorageKey()
    => Assert.Throws<ArgumentException>(() => LessonResource.CreateFile(
        Guid.NewGuid(), Guid.NewGuid(), null, null, "Konu anlatimi", null, storageKey: "", fileName: "a.pdf", Now));

[Fact]
public void Link_Resource_Requires_Url()
    => Assert.Throws<ArgumentException>(() => LessonResource.CreateLink(
        Guid.NewGuid(), Guid.NewGuid(), null, null, "Video", null, url: "", Now));

[Fact]
public void Archive_Should_Deactivate()
{
    var resource = LessonResource.CreateLink(Guid.NewGuid(), Guid.NewGuid(), null, null, "Video", null,
        "https://ornek.com/ders", Now);
    resource.Archive(Now.AddDays(1));
    Assert.False(resource.IsActive);
}
```

- [ ] **Step 2: Kırmızı gör → domain'i yaz → yeşil**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~LessonResourceTests"`

- [ ] **Step 3: Application + Infrastructure + endpoint'ler**

Yetki kuralı (`LessonResourcePolicies`):
- Oluşturma/silme: yalnız `TeacherUserId` sahibi veya Admin.
- Listeleme: öğretmen kendi kaynaklarını; öğrenci yalnız `StudentId == kendi profili` **veya** `StudentId == null` **ve** öğretmenine bağlıysa (öğrenci-öğretmen bağı `Shared/Contracts` üzerinden doğrulanır — Assignments zaten `IStudentDirectory`/`ILessonSessionAccessService` kullanıyor, aynı yolu izle).
- İndirme: listeleme ile aynı kural.

Run: `dotnet ef migrations add AddLessonResources --project src/Modules/Assignments/Infrastructure --startup-project src/API.Host --context AssignmentsDbContext`

- [ ] **Step 4: Backend testleri** — Run: `dotnet test EgitimUssu.slnx` → yeşil.

- [ ] **Step 5: Mobil — öğretmen tarafı**

`lesson_detail_page.dart`'a "Kaynak ekle" eylemi: dosya seç (`file_picker`) veya bağlantı gir; liste halinde mevcut kaynaklar, uzun basınca sil.

- [ ] **Step 6: Mobil — öğrenci tarafı**

`student_lesson_detail_page.dart` ve `student_assignments_page.dart`'a "Ders kaynakları" bölümü: dosyayı indir/aç, bağlantıyı tarayıcıda aç.

- [ ] **Step 7: Mobil test + analiz** — Run: `cd mobile && flutter test && flutter analyze`

- [ ] **Step 8: Doküman + commit**

`doc/modules/m06_assignments.md` (yeni aggregate + 4 endpoint + yetki matrisi), `doc/modules/00_genel_bakis.md` (Assignments bloğu 7 → 11 endpoint), `doc/modules/veri_modeli.md` (`lesson_resources`), `doc/pages/lesson_detail.md`.
```bash
git add src/Modules/Assignments mobile doc
git commit -m "feat(assignments): ders kaynagi paylasimi (M06-1)"
```

---

### Task 5: Kapanış

- [ ] **Step 1: S3 sağlayıcısına karşı bir kez doğrula**

Seçilen sağlayıcıda (Q4: R2 veya S3) bucket aç, env ver:
```bash
Storage__Provider=S3 Storage__BucketName=<bucket> Storage__ServiceUrl=<endpoint> \
Storage__AccessKeyId=<id> Storage__SecretAccessKey=<secret> dotnet run --project src/API.Host
```
Ödev dosyası yükle → bucket'ta nesne oluştu → indirme ucu aynı dosyayı döndürüyor.

- [ ] **Step 2: Tam testler** — Run: `dotnet test EgitimUssu.slnx && cd mobile && flutter test` → yeşil.
- [ ] **Step 3: Dokümanlar** — `doc/architecture/backend.md` "Dosya depolama" başlığı; `doc/modules/mimari_inceleme.md` O8 maddesini `✅ Düzeltildi 2026-09-02 (P04)` yap; `doc/denetim/2026-09-02_eksik_analizi.md` C-02/M06-1/M06-2/M02-2/D-05 → `✅ (P04)`.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P04 dosya depolama kapanisi (C-02/M06-1/M02-2)"
```

---

## Kabul Kriterleri

- [ ] `Storage:Provider=S3` iken eksik konfigürasyonla uygulama açılmıyor
- [ ] 10 MB üstü ve izinsiz uzantılı dosya reddediliyor (`storage.*` hata kodları)
- [ ] Ödev teslimi S3'e yazılıyor, yetkisiz kullanıcı indiremiyor (403)
- [ ] Öğretmen profil fotoğrafı yükleyip görebiliyor; `profilePhotoUrl` istemciden set edilemiyor
- [ ] Öğretmen ders kaynağı (dosya + bağlantı) paylaşabiliyor, öğrenci görüp açabiliyor
- [ ] Yol dolaşımı (`../`) denemesi `null`/404 dönüyor
- [ ] `dotnet test EgitimUssu.slnx` ve `flutter test` yeşil
