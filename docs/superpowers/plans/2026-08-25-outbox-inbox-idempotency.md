# Outbox Tüketici Idempotency (Ortak Inbox) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tüm integration-event tüketici handler'larını tek kanonik, EventId-tabanlı, atomik idempotency desenine (`inbox_messages` tablosu + `IdempotentIntegrationEventHandler` base) taşımak.

**Architecture:** `Shared/Infrastructure/Persistence`'a `InboxMessage` entity'si eklenir ve `ModuleDbContext` tabanına bağlanır → her modül DbContext'i otomatik `inbox_messages` tablosunu alır. `Shared/Infrastructure/Messaging`'a `IdempotentIntegrationEventHandler` base sınıfı eklenir: `(EventId, Handler)` guard'ı + `ApplyAsync` (iş yazımını stage eder, SaveChanges çağırmaz) + tek transaction'da commit. 11 tüketici handler bu base'e taşınır; 3 eski `processed_*` tablosu kaldırılır.

**Tech Stack:** .NET 9, EF Core (Npgsql + InMemory), xUnit, modüler monolit (`src/Modules/*`), Shared kernel/infrastructure.

**Spec:** `docs/superpowers/specs/2026-08-25-outbox-inbox-idempotency-design.md`

## Global Constraints

- **Modül izolasyonu:** Her modül kendi şemasında ayrı `inbox_messages` tablosuna sahip olur. Modüller birbirinin DbContext'ini/tablosunu okumaz (mimari test `Modules_Should_Not_Reference_Other_Modules` bunu zorlar).
- **Atomiklik sözleşmesi:** `ApplyAsync` **asla** kendi `SaveChanges`'ini çağırmaz; yalnız `DbContext` üzerinde değişiklik stage eder (veya aynı transaction içinde `ExecuteUpdate`). Tek commit base'e aittir.
- **Dedup anahtarı:** Bileşik `(EventId Guid, Handler string)`. `Handler = GetType().Name`.
- **Zaman kaynağı:** Her yerde `IClock.UtcNow` — asla `DateTime.UtcNow`.
- **Serileştirme:** Payload çözümlemesinde daima `IntegrationEventSerialization.Options`.
- **Namespace kökü:** `EgitimUssu.*`. Dosya/kod tanımlayıcıları `EgitimUssu` (Türkçe karaktersiz).
- **Test komutu:** `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` (Docker'sız birim testleri). Migration komutları `--startup-project src/API.Host`.
- **Migration adı:** `AddInboxMessages` (eski tablo kaldıran modüllerde `AddInboxMessagesReplaceProcessed`).

---

### Task 1: `InboxMessage` entity + `IdempotentIntegrationEventHandler` base (çekirdek, TDD)

**Files:**
- Create: `src/Shared/Infrastructure/Persistence/InboxMessage.cs`
- Create: `src/Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs`
- Test: `tests/Unit/IdempotentIntegrationEventHandlerTests.cs`
- Test helper (create): `tests/Unit/TestDoubles/TestModuleDbContext.cs`

**Interfaces:**
- Consumes: `IIntegrationEvent`/`IntegrationEvent` (`Shared.Contracts`), `IClock` (`Shared.Kernel`), `ModuleDbContext` (`Shared.Infrastructure.Persistence`).
- Produces:
  - `InboxMessage(Guid eventId, string handler, string eventName, DateTime processedOnUtc)` — properties `EventId`, `Handler`, `EventName`, `ProcessedOnUtc` (private set).
  - `abstract class IdempotentIntegrationEventHandler : IIntegrationEventHandler` — ctor `(ModuleDbContext dbContext, IClock clock)`; `protected ModuleDbContext DbContext`; `protected IClock Clock`; `protected virtual string HandlerName => GetType().Name`; abstract `Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken)`.

- [ ] **Step 1: `InboxMessage` entity'sini yaz**

`src/Shared/Infrastructure/Persistence/InboxMessage.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>
/// "Bu handler bu event'i işledi" kaydı (tüketici idempotency). <see cref="OutboxMessage"/>'ın kardeşi.
/// Bileşik anahtar (EventId, Handler): tek event birden çok handler tarafından tüketilebilir.
/// </summary>
public sealed class InboxMessage
{
    public InboxMessage(Guid eventId, string handler, string eventName, DateTime processedOnUtc)
    {
        EventId = eventId;
        Handler = handler;
        EventName = eventName;
        ProcessedOnUtc = processedOnUtc;
    }

    private InboxMessage()
    {
    }

    public Guid EventId { get; private set; }

    public string Handler { get; private set; } = string.Empty;

    public string EventName { get; private set; } = string.Empty;

    public DateTime ProcessedOnUtc { get; private set; }
}
```

- [ ] **Step 2: Test helper `TestModuleDbContext`'i yaz**

`ModuleDbContext` soyut ve `IDomainEventMapper` ister. Birim testleri için InMemory türev + no-op mapper.

`tests/Unit/TestDoubles/TestModuleDbContext.cs`:
```csharp
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit.TestDoubles;

public sealed class TestModuleDbContext : ModuleDbContext
{
    public TestModuleDbContext(DbContextOptions options)
        : base(options, new NoOpDomainEventMapper)
    {
    }

    protected override string Schema => "test";

    protected override string ModuleName => "Test";
}

public sealed class NoOpDomainEventMapper : IDomainEventMapper
{
    public IEnumerable<IIntegrationEvent> Map(string moduleName, DomainEvent domainEvent)
        => Array.Empty<IIntegrationEvent>();
}
```
> Not: `IDomainEventMapper`/`DomainEvent`'in gerçek imzasını doğrula (`src/Shared/Infrastructure/Messaging/IDomainEventMapper.cs`). İmza farklıysa `Map` metodunu ona göre uyarla; amaç boş dizi döndüren no-op.

- [ ] **Step 3: Başarısız testleri yaz**

`tests/Unit/IdempotentIntegrationEventHandlerTests.cs`:
```csharp
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class IdempotentIntegrationEventHandlerTests
{
    private static TestModuleDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IntegrationEvent Event(Guid id) =>
        new(id, DateTime.UnixEpoch, "SampleDomainEvent", "Study", "{}");

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => DateTime.UnixEpoch;
    }

    private sealed class CountingHandler : IdempotentIntegrationEventHandler
    {
        public CountingHandler(ModuleDbContext db, IClock clock, bool applyResult = true) : base(db, clock)
            => _applyResult = applyResult;

        private readonly bool _applyResult;
        public int ApplyCount { get; private set; }
        public bool Throw { get; set; }

        public override bool CanHandle(IIntegrationEvent e) => true;

        protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken ct)
        {
            ApplyCount++;
            if (Throw) throw new InvalidOperationException("boom");
            return Task.FromResult(_applyResult);
        }
    }

    [Fact]
    public async Task Same_event_processed_once()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock);
        var e = Event(Guid.NewGuid());

        await handler.HandleAsync(e);
        await handler.HandleAsync(e);

        Assert.Equal(1, handler.ApplyCount);
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Same_event_different_handlers_both_run()
    {
        await using var db = NewContext();
        var e = Event(Guid.NewGuid());
        var a = new NamedHandler(db, new FixedClock, "A");
        var b = new NamedHandler(db, new FixedClock, "B");

        await a.HandleAsync(e);
        await b.HandleAsync(e);

        Assert.Equal(1, a.ApplyCount);
        Assert.Equal(1, b.ApplyCount);
        Assert.Equal(2, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Apply_returns_false_writes_no_inbox_row()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock, applyResult: false);

        await handler.HandleAsync(Event(Guid.NewGuid()));

        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Apply_throws_writes_no_inbox_row()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock) { Throw = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Event(Guid.NewGuid())));

        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
    }

    private sealed class NamedHandler : IdempotentIntegrationEventHandler
    {
        public NamedHandler(ModuleDbContext db, IClock clock, string name) : base(db, clock) => _name = name;
        private readonly string _name;
        public int ApplyCount { get; private set; }
        protected override string HandlerName => _name;
        public override bool CanHandle(IIntegrationEvent e) => true;
        protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken ct)
        {
            ApplyCount++;
            return Task.FromResult(true);
        }
    }
}
```
> Not: `InboxMessage` DbSet'i henüz `ModuleDbContext`'te tanımlı değil; `Set<InboxMessage>()` çalışması için Step 5 (model config) gerekli. Bu yüzden bu testler Step 5'e kadar derlenmez/geçmez — kabul; Task 1 çekirdeği ile Task 2 modeli aynı derleme birimine bağlı. **İstisna:** Testleri çalıştırmadan önce Task 2 Step'lerini de tamamla (Task 1 ve Task 2 tek commit döngüsü paylaşır).

- [ ] **Step 4: Base sınıfı yaz**

`src/Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs`:
```csharp
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

/// <summary>
/// Integration event tüketicileri için ortak idempotency tabanı. Outbox en-az-bir-kez teslim
/// ettiğinden her (EventId, Handler) çifti en fazla bir kez işlenir. İş-yazımı + inbox-mark
/// tek transaction'da commit olur (sıkı atomik). <see cref="ApplyAsync"/> SaveChanges ÇAĞIRMAZ.
/// </summary>
public abstract class IdempotentIntegrationEventHandler : IIntegrationEventHandler
{
    protected IdempotentIntegrationEventHandler(ModuleDbContext dbContext, IClock clock)
    {
        DbContext = dbContext;
        Clock = clock;
    }

    protected ModuleDbContext DbContext { get; }

    protected IClock Clock { get; }

    /// <summary>Dedup anahtarının handler bileşeni. Varsayılan tip adı; override edilebilir.</summary>
    protected virtual string HandlerName => GetType().Name;

    public abstract bool CanHandle(IIntegrationEvent integrationEvent);

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var relational = DbContext.Database.IsRelational();
        await using var transaction = relational
            ? await DbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var handlerName = HandlerName;
        var alreadyProcessed = await DbContext.Set<InboxMessage>()
            .AnyAsync(item => item.EventId == envelope.EventId && item.Handler == handlerName, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var applied = await ApplyAsync(envelope, cancellationToken);
        if (!applied)
        {
            return;
        }

        DbContext.Set<InboxMessage>().Add(new InboxMessage(envelope.EventId, handlerName, envelope.Name, Clock.UtcNow));
        await DbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>İş etkisini <see cref="DbContext"/> üzerinde STAGE eder (SaveChanges YOK). İşlenecek
    /// bir şey yoksa false döner → inbox'a yazılmaz.</summary>
    protected abstract Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Task 2'yi tamamla (model config), sonra testleri çalıştır — geçmeli**

Task 2 Step'lerini uygula. Ardından:
Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~IdempotentIntegrationEventHandlerTests"`
Expected: 4 PASS.

- [ ] **Step 6: Commit**
```bash
git add src/Shared/Infrastructure/Persistence/InboxMessage.cs \
        src/Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs \
        src/Shared/Infrastructure/Persistence/ModuleDbContext.cs \
        tests/Unit/IdempotentIntegrationEventHandlerTests.cs \
        tests/Unit/TestDoubles/TestModuleDbContext.cs
git commit -m "feat(shared): ortak inbox_messages + IdempotentIntegrationEventHandler base (TDD)"
```

---

### Task 2: `InboxMessage`'ı `ModuleDbContext`'e bağla

> Task 1 ile aynı commit döngüsünde tamamlanır (testler buna bağımlı). Ayrı task tutulur çünkü Shared model değişikliği tüm modül context'lerini etkiler.

**Files:**
- Modify: `src/Shared/Infrastructure/Persistence/ModuleDbContext.cs:23-46`

**Interfaces:**
- Produces: `ModuleDbContext.InboxMessages` (`DbSet<InboxMessage>`), her türev context'te `inbox_messages` tablosu.

- [ ] **Step 1: DbSet ekle**

`ModuleDbContext.cs`'te `ModuleStates` satırının altına:
```csharp
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
```

- [ ] **Step 2: `OnModelCreating`'e config ekle**

`module_states` entity bloğunun hemen ardına (`base.OnModelCreating` çağrısından önce):
```csharp
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(item => new { item.EventId, item.Handler });
            builder.Property(item => item.Handler).HasMaxLength(256).IsRequired();
            builder.Property(item => item.EventName).HasMaxLength(256).IsRequired();
        });
```

- [ ] **Step 3: Derle**

Run: `dotnet build src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj --nologo -v q`
Expected: 0 hata. (Csproj adını `find src/Shared/Infrastructure -name "*.csproj"` ile doğrula.)

- [ ] **Step 4:** Task 1 Step 5 (testler) + Step 6 (commit) bu iki task'ı birlikte kapatır.

---

### Task 3: ProgressTracking handler'larını ortak base'e taşı

**Files:**
- Modify: `src/Modules/ProgressTracking/Infrastructure/StudyProgressIntegrationEventHandlers.cs`
- Modify: `src/Modules/ProgressTracking/Infrastructure/ProgressTrackingDbContext.cs` (processed_events kaldır)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` (`ProcessedEvent` kaldır)
- Modify: `src/Modules/ProgressTracking/Application/*Repository*.cs` + impl (`HasProcessedAsync`/`AddProcessedAsync` kaldır)
- Modify: `src/Modules/ProgressTracking/Infrastructure/DependencyInjection.cs` (handler kaydı — ctor değişince gerekiyorsa)

**Interfaces:**
- Consumes: `IdempotentIntegrationEventHandler` (Task 1), `ProgressTrackingDbContext` (bir `ModuleDbContext`).
- Produces: (yok — modül-içi.)

- [ ] **Step 1: Önce oku ve doğrula**

Şu dosyaları oku ve `MasteryService.ApplyStudyAsync`/`ApplyTestAsync`'in DbContext'e nasıl yazdığını tespit et:
`MasteryService`, `IProgressRepository` + impl, `ProgressTrackingDbContext`.
**Kritik soru:** `MasteryService.ApplyAsync` içinde `SaveChanges` çağrılıyor mu? Çağrılıyorsa → `ApplyAsync` içinde tek commit garantisi bozulur. Bu durumda MasteryService'i "stage-only" yap: repository'nin `SaveChangesAsync`'ini MasteryService'ten çıkar, çağrıyı base'in tek `SaveChangesAsync`'ine bırak. (MasteryService başka yerden de çağrılıyorsa, o çağıranlar kendi SaveChanges'ini yapmaya devam eder; yalnız handler yolunda stage-only olmalı.)

- [ ] **Step 2: Handler'ları dönüştür**

`StudySessionCompletedProgressHandler`'ı base'e taşı (kardeş `TestResultRecordedProgressHandler` için aynısını uygula — payload/servis metodu farkıyla):
```csharp
internal sealed class StudySessionCompletedProgressHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly MasteryService _masteryService;

    public StudySessionCompletedProgressHandler(ProgressTrackingDbContext dbContext, MasteryService masteryService, IClock clock)
        : base(dbContext, clock)
        => _masteryService = masteryService;

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Study" && integrationEvent.Name == "StudySessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudySessionCompletedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject) || payload.EffectiveMinutes <= 0)
        {
            return true; // işlenecek bir şey yok ama dedup kaydı yazılsın (eski davranış: MarkProcessed'di)
        }

        await _masteryService.ApplyStudyAsync(payload.StudentId, payload.Subject, payload.Topic, payload.EffectiveMinutes, cancellationToken);
        return true;
    }

    private sealed record StudySessionCompletedPayload(
        Guid SessionId, Guid StudentId, string Subject, string? Topic,
        int EffectiveMinutes, int BreakMinutes, DateTime EndedAtUtc);
}
```
> `MasteryService.ApplyStudyAsync` artık SaveChanges yapmamalı (Step 1). Payload boş/geçersizse eski kod da `MarkProcessed` yapıyordu → `return true` ile inbox'a yazıp bir daha denememesini koru.

- [ ] **Step 3: Eski dedup altyapısını kaldır**

- `ProgressTrackingDbContext.cs:74-82` → `processed_events` entity config'ini ve `ProcessedEvents` DbSet'ini sil.
- `ProgressTrackingDomainModel.cs:217` → `ProcessedEvent` sınıfını sil.
- `IProgressRepository` + impl → `HasProcessedAsync`, `AddProcessedAsync` metotlarını sil (başka kullanan yoksa). `SaveChangesAsync` kalabilir (MasteryService başka yollarda kullanıyorsa).

- [ ] **Step 4: Derle + test**

Run: `dotnet build src/Modules/ProgressTracking/Infrastructure/*.csproj --nologo -v q` ardından
`dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: 0 hata; mevcut ProgressTracking testleri (varsa) yeşil. Yeşil değilse dur ve incele.

- [ ] **Step 5: Commit**
```bash
git add src/Modules/ProgressTracking
git commit -m "refactor(progress-tracking): idempotency ortak inbox'a taşındı (processed_events kaldırıldı)"
```

---

### Task 4: Parents projeksiyon handler'larını ortak base'e taşı

**Files:**
- Modify: `src/Modules/Parents/Infrastructure/ParentReadModelProjections.cs`
- Modify: `src/Modules/Parents/Infrastructure/ParentsDbContext.cs` (processed_integration_events kaldır)
- Modify: `src/Modules/Parents/Domain/ParentsReadModels.cs` (`ProcessedIntegrationEvent` kaldır)

**Interfaces:**
- Consumes: `IdempotentIntegrationEventHandler`, `ParentsDbContext`.

- [ ] **Step 1: `ParentReadModelProjectionHandler`'ı ortak base'e devret**

`ParentReadModelProjectionHandler`'ı `IIntegrationEventHandler` yerine `IdempotentIntegrationEventHandler`'dan türet. Kendi `HandleAsync`'ini ve `ProcessedIntegrationEvents` guard/mark bloğunu (satır 34-56) **sil** — base bunu yapıyor. `GetOrCreateSnapshotAsync`/`Deserialize` yardımcıları ve `ApplyAsync` alt sınıflarda aynen kalır. Ctor'u base'e bağla:
```csharp
internal abstract class ParentReadModelProjectionHandler : IdempotentIntegrationEventHandler
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected ParentReadModelProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, clock)
        => IdGenerator = idGenerator;

    protected IIdGenerator IdGenerator { get; }
    protected ParentsDbContext ParentsDb => (ParentsDbContext)DbContext;

    // CanHandle + ApplyAsync alt sınıflarda; GetOrCreateSnapshotAsync/Deserialize KORUNUR.
}
```
> `GetOrCreateSnapshotAsync` içinde `DbContext.ChildProgressSnapshots` kullanımını `ParentsDb.ChildProgressSnapshots`'a çevir (base `DbContext` artık `ModuleDbContext` tipinde). Aynı şekilde `ParentStudentDirectoryProjectionHandler.ApplyAsync` içindeki `DbContext.KnownStudents` → `ParentsDb.KnownStudents`.
> `ApplyAsync` zaten SaveChanges çağırmıyordu (base yapıyordu) → iş yazımı davranışı değişmez.

- [ ] **Step 2: Eski dedup kaldır**

- `ParentsDbContext.cs:95` → `processed_integration_events` config + `ProcessedIntegrationEvents` DbSet'ini sil.
- `ParentsReadModels.cs:165` → `ProcessedIntegrationEvent` sınıfını sil.
- Guard artık `Set<InboxMessage>()` üzerinden base'te.

- [ ] **Step 3: Derle + test**

Run: `dotnet build` (Parents.Infrastructure) + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: 0 hata; Parents projeksiyon testleri (varsa) yeşil.

- [ ] **Step 4: Commit**
```bash
git add src/Modules/Parents
git commit -m "refactor(parents): projeksiyon idempotency'si ortak inbox'a taşındı"
```

---

### Task 5: Notifications handler'ları — event dedup ortak inbox'a, haftalık-özet KORUNUR

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/ParentEventNotificationHandler.cs`
- Modify: `src/Modules/Notifications/Infrastructure/LessonScheduleNotificationIntegrationEventHandler.cs`
- Modify: `src/Modules/Notifications/Infrastructure/NotificationsDbContext.cs`
- Read (dokunma): `src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs`
- Modify: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs`

**Interfaces:**
- Consumes: `IdempotentIntegrationEventHandler`, `NotificationsDbContext`.

- [ ] **Step 1: Önce oku ve haftalık-özet bağımlılığını sınırla**

`ParentWeeklySummaryService`'in `processed_integration_events` tablosunu `weekly:{parentId}:{weekStart}` anahtarıyla nasıl kullandığını oku. **Karar:** Bu tabloyu SİLME. İki handler'ın (`ParentEventNotificationHandler`, `LessonScheduleNotificationIntegrationEventHandler`) **event dedup** kullanımını ortak inbox'a taşı; `processed_integration_events` tablosu + entity yalnız `ParentWeeklySummaryService` için kalsın. (İsteğe bağlı netlik: entity/tabloyu `SummaryDedup`/`summary_dedup` olarak yeniden adlandır — yalnız haftalık servis referans veriyorsa güvenli; adlandırmayı yapıyorsan tüm referansları güncelle, yapmıyorsan olduğu gibi bırak.)

- [ ] **Step 2: `LessonScheduleNotificationIntegrationEventHandler`'ı base'e taşı**

Oku; mevcut iş-verisi dedup'ını (`GetByLessonScheduleIdAsync` kontrolü) EventId dedup'ıyla değiştir. Handler'ı `IdempotentIntegrationEventHandler`'dan türet; ctor'a `NotificationsDbContext` + `IClock` geç; mevcut yazım mantığını `ApplyAsync`'e taşı, içindeki `SaveChanges`'i kaldır; iş-verisi "zaten var mı" guard'ını kaldır (base EventId guard'ı yeterli). İş kaydı hâlâ upsert semantiği istiyorsa (aynı LessonScheduleId için tek hatırlatma) o kontrolü koru ama SaveChanges base'e bırak.

- [ ] **Step 3: `ParentEventNotificationHandler`'ı base'e taşı**

Bu handler 4 event tipini tüketiyor ve kendi `processed_integration_events` guard'ını kullanıyor. `IdempotentIntegrationEventHandler`'dan türet; guard/mark bloğunu sil; 4 event'in işleme mantığını `ApplyAsync`'te `switch (envelope.Name)` ile topla; `SaveChanges`'i kaldır (base yapar). Premium/tercih kapısı mantığı `ApplyAsync` içinde kalır; işlenmeyecekse `return false` (Premium değil vb. → inbox'a yazma, çünkü tercih sonradan değişebilir) — **dikkat:** eski davranış neyse onu koru; eğer eski kod tercih kapalıyken de MarkProcessed yapıyorsa `return true`, yapmıyorsa `return false`. Oku ve eşle.

- [ ] **Step 4: DbContext temizliği**

`NotificationsDbContext.cs` — `processed_integration_events` config'i **yalnız haftalık servis kullanıyorsa koru**. İki handler artık kullanmıyor. Entity'yi (`NotificationsDomainModel.cs:149`) haftalık servis kullanıyorsa koru, kullanmıyorsa sil.

- [ ] **Step 5: Derle + test**

Run: `dotnet build` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: 0 hata; Notifications testleri (özellikle veli bildirim + haftalık özet) yeşil.

- [ ] **Step 6: Commit**
```bash
git add src/Modules/Notifications
git commit -m "refactor(notifications): event handler dedup ortak inbox'a; haftalık-özet dedup korundu"
```

---

### Task 6: Assignments `LessonSessionCompletedIntegrationEventHandler`

**Files:**
- Modify: `src/Modules/Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs` (gerçek yolu `find` ile doğrula)
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentsDbContext.cs` (gerekiyorsa)

- [ ] **Step 1: Oku ve dönüştür**

Handler'ı oku. Mevcut dedup'ı iş-verisi anahtarı (`GetLessonNoteByLessonSessionIdAsync` "zaten var mı"). `IdempotentIntegrationEventHandler`'dan türet; ctor `AssignmentsDbContext` + `IClock`; iş mantığını `ApplyAsync`'e taşı, `SaveChanges` kaldır. İş kaydının benzersizliği (aynı LessonSessionId için tek follow-up) domain kuralıysa o kontrolü koru; ama tekrar-teslimat koruması artık EventId guard'ından geliyor.

- [ ] **Step 2: Derle + test**

Run: `dotnet build` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` → 0 hata, yeşil.

- [ ] **Step 3: Commit**
```bash
git add src/Modules/Assignments
git commit -m "refactor(assignments): LessonSessionCompleted handler EventId-tabanlı idempotency"
```

---

### Task 7: Students `ParentChildLinkApprovedIntegrationEventHandler`

**Files:**
- Modify: `src/Modules/Students/Infrastructure/ParentChildLinkApprovedIntegrationEventHandler.cs` (yolu doğrula)

- [ ] **Step 1: Oku ve dönüştür**

Mevcut dedup: iş-verisi (`student.ParentUserId == payload.ParentUserId` ise çık). `IdempotentIntegrationEventHandler`'dan türet; ctor `StudentsDbContext` + `IClock`; `ApplyAsync`'te öğrenciyi yükle + `SetParent`; `SaveChanges` kaldır. İş guard'ı (zaten aynı parent bağlıysa) korunabilir ama artık gereksiz — koru ki `RegisterParent` domain kuralı bozulmasın; base EventId guard'ı asıl korumayı verir. İşlenecek öğrenci yoksa `return false`.

- [ ] **Step 2: Derle + test** → 0 hata, yeşil.

- [ ] **Step 3: Commit**
```bash
git add src/Modules/Students
git commit -m "refactor(students): ParentChildLinkApproved handler EventId-tabanlı idempotency"
```

---

### Task 8: 5× `*StudentMergedHandler` (Payments/Study/Assignments/Scheduling/LessonSessions)

**Files:**
- Modify: her modülde `*StudentMergedHandler.cs` (yolları `grep -rl "StudentMergedHandler" src/Modules --include=*.cs` ile bul)

**Interfaces:**
- Consumes: `IdempotentIntegrationEventHandler`, her modülün kendi `ModuleDbContext`'i.

- [ ] **Step 1: Bir handler'ı örnek dönüştür (Payments)**

`PaymentsStudentMergedHandler`'ı oku. `ExecuteUpdateAsync` ile reassign yapıyor (doğal idempotent). `IdempotentIntegrationEventHandler`'dan türet; ctor `PaymentsDbContext` + `IClock`; reassign'ları `ApplyAsync` içine taşı. **Önemli:** `ExecuteUpdateAsync` açık transaction içinde (base BeginTransaction açtı) çalışır → inbox insert ile atomik. `ApplyAsync` sonunda `return true`. Base zaten `SaveChangesAsync` çağıracak (inbox satırı için); `ExecuteUpdate` değişiklikleri change-tracker'da değil ama transaction'a dahil.
> InMemory (dev): `ExecuteUpdateAsync` InMemory provider'da desteklenmeyebilir — mevcut testler bunu nasıl çalıştırıyor kontrol et. Destekleniyorsa sorun yok; değilse mevcut testlerin bu handler'ları relational olmayan yolda test etmediğini doğrula.

- [ ] **Step 2: Diğer 4 handler'ı aynı reçeteyle dönüştür**

Study (11× ExecuteUpdate), Assignments, Scheduling, LessonSessions StudentMerged handler'larını Step 1 reçetesiyle dönüştür: base'den türet, ctor modül context + IClock, reassign'ları `ApplyAsync`'e taşı, `return true`. Her biri kendi `<Modül>DbContext`'ini kullanır.

- [ ] **Step 3: Derle + tüm birim testleri**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: 0 hata; tüm mevcut testler yeşil (özellikle StudentMerged birleştirme testleri).

- [ ] **Step 4: Commit**
```bash
git add src/Modules/Payments src/Modules/Study src/Modules/Assignments src/Modules/Scheduling src/Modules/LessonSessions
git commit -m "refactor(merge-handlers): 5 StudentMerged handler ortak inbox idempotency'sine taşındı"
```

---

### Task 9: DI kayıtlarını doğrula + startup guard geçişi

**Files:**
- Modify: ilgili `src/Modules/*/Infrastructure/DependencyInjection.cs` (ctor imzası değişen handler'lar)

- [ ] **Step 1: Handler kayıtlarını kontrol et**

`grep -rn "IIntegrationEventHandler" src/Modules/*/Infrastructure/DependencyInjection.cs`. Her dönüştürülen handler hâlâ `AddScoped<IIntegrationEventHandler, XHandler>()` ile kayıtlı olmalı. Ctor artık `<Modül>DbContext` istiyor — bu context DI'da zaten scoped kayıtlı (mevcut). Ek kayıt gerekmiyorsa dokunma.

- [ ] **Step 2: Uygulamayı başlat — startup guard geçmeli**

`ASPNETCORE_ENVIRONMENT=Development` ile uygulamayı arka planda başlat (bkz. bu repodaki çalıştırma deseni: `dotnet run --no-build --project src/API.Host/EgitimUssu.API.Host.csproj`). Logda `AuthorizationCoverageValidator`/DI hatası OLMAMALI; `Now listening on` görünmeli. Sonra süreci kapat.
Expected: temiz başlangıç, hata yok.

- [ ] **Step 3: Commit (değişiklik varsa)**
```bash
git add -A && git commit -m "chore(di): inbox'a taşınan handler kayıtlarını doğrula"
```

---

### Task 10: Migration'ları üret (tüm kayıtlı modül context'leri)

**Files:**
- Create: her modülde `Infrastructure/Migrations/*_AddInboxMessages*.cs`

**Interfaces:**
- Consumes: tamamlanmış model değişiklikleri (Task 1-8).

- [ ] **Step 1: Kayıtlı context listesini çıkar**

`grep -rn "AddModuleDbContext\|AddDbContext" src/Modules/*/Infrastructure/DependencyInjection.cs` ile migrate edilen context'leri bul (beklenen: Identity, Teachers, Students, Scheduling, LessonSessions, Assignments, Payments, Study, Parents, ProgressTracking, Notifications, Settings). Matching/Reviews/Reporting **kayıtsız** → migration YOK.

- [ ] **Step 2: Her context için migration üret**

Her modül için (context adını doğrula):
```bash
dotnet ef migrations add AddInboxMessages \
  --project src/Modules/<Modül>/Infrastructure \
  --startup-project src/API.Host \
  --context <Modül>DbContext
```
Eski `processed_*` tablosu kaldırılan modüllerde (ProgressTracking, Parents, ve Notifications-kısmi) migration adı `AddInboxMessagesReplaceProcessed`; migration'ın hem `inbox_messages` create hem eski tablo drop içerdiğini gözle doğrula.
> `dotnet ef` yoksa: `dotnet tool restore` veya `dotnet tool install --global dotnet-ef`. Provider Npgsql; migration üretimi canlı DB gerektirmez.

- [ ] **Step 3: Migration drift kontrolü**

Run: `dotnet build --nologo -v q` (tüm çözüm) — 0 hata.
Varsa CI drift script'ini çalıştır: `.github/workflows/backend-ci.yml` içindeki migration-drift adımını yerelde tekrarla (genelde `dotnet ef migrations has-pending-model-changes` her context için). Hiçbir context'te bekleyen model değişikliği KALMAMALI.

- [ ] **Step 4: Commit**
```bash
git add src/Modules/*/Infrastructure/Migrations
git commit -m "chore(migrations): tüm modül context'lerine inbox_messages (+eski processed drop)"
```

---

### Task 11: Doğrulama + doküman güncellemeleri

**Files:**
- Modify: `doc/modules/mimari_inceleme.md` (Y4 ✅)
- Modify: `doc/modules/00_genel_bakis.md` (idempotent notları birleştir)
- Modify: `doc/modules/veri_modeli.md` (inbox_messages ER)

- [ ] **Step 1: Tam test paketi**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: TÜM birim testleri yeşil. (Docker varsa `tests/Integration` de çalıştır; yoksa Skip.)

- [ ] **Step 2: Uçtan uca duman testi**

Uygulamayı Development'ta başlat; bir domain event üreten akışı tetikle (örn. ders tamamla) — outbox → handler → inbox satırı oluşmalı. En azından temiz başlangıç + `Outbox dispatcher enabled` + hata yok doğrula.

- [ ] **Step 3: Dokümanları güncelle (KALICI KURAL)**

- `doc/modules/mimari_inceleme.md`: **Y4** başlığını `### ✅ Y4 — ... — Düzeltildi 2026-08-25` yap; çözüm notu ekle (ortak `inbox_messages` + `IdempotentIntegrationEventHandler`, sıkı atomik). Footer + frontmatter `updated` tarihini güncelle.
- `doc/modules/00_genel_bakis.md`: handler `[consume]` notlarındaki "idempotent" ifadelerini "idempotent (ortak inbox)" olarak birleştir; footer tarihi.
- `doc/modules/veri_modeli.md`: her modül şemasına `inbox_messages (EventId, Handler, EventName, ProcessedOnUtc)` ekle; kaldırılan `processed_events`/`processed_integration_events`'i güncelle; footer tarihi.

- [ ] **Step 4: Commit**
```bash
git add doc/
git commit -m "docs: Y4 idempotency kapatıldı — ortak inbox; veri modeli + envanter güncellendi"
```

---

## Self-Review Notları
- **Spec kapsamı:** §3.1 InboxMessage → Task 1; §3.2 ModuleDbContext → Task 2; §3.3 base → Task 1; §4 handler dönüşümü → Task 3-8; §5 migration → Task 10; §6 riskler (haftalık-özet, InMemory, ExecuteUpdate) → Task 5 Step 1, base `IsRelational` guard, Task 8 Step 1; §7 test → Task 1 (birim) + Task 11 (entegrasyon/e2e); §8 doküman → Task 11.
- **Atomiklik:** ApplyAsync-SaveChanges yasağı her handler task'ında tekrarlanıyor; Global Constraints'te de var.
- **Kritik okuma noktaları:** MasteryService (Task 3 Step 1), ParentWeeklySummaryService (Task 5 Step 1), ExecuteUpdate+InMemory (Task 8 Step 1). Bunlar executor'ın körlemesine değiştirmemesi gereken yerler.
