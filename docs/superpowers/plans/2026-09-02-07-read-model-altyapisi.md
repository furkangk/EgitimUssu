# P07 — Modüller Arası Okuma (Read-Model) Altyapısı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modül sınırını bozmadan çapraz-modül okuma yapabilmek için tek bir kanonik **projeksiyon deseni** kurmak (O5/C-03) ve dispatcher katmanının çift-sorgu (O2) ile reflection maliyetini (O4) gidermek. Desenin doğruluğu, ilk gerçek tüketici olan öğretmen dashboard'unun projeksiyona taşınmasıyla kanıtlanır.

**Architecture:** `Shared/Infrastructure/Projections` altında `ProjectionEntity` (temel: `LastAppliedEventId`, `UpdatedOnUtc`) ve `ProjectionHandler<TEvent, TProjection>` (mevcut `IdempotentIntegrationEventHandler` üzerine kurulu) gelir. Bir projeksiyon **tüketen modülün kendi şemasında** yaşar; kaynak modül yalnız integration event yayar. Dispatcher'da tip başına derlenmiş delegate cache eklenir; authorizer'ların yüklediği varlık `IRequestEntityCache` (scoped) ile handler'a taşınır, ikinci sorgu ortadan kalkar.

**Tech Stack:** .NET 9, EF Core, `System.Linq.Expressions` (delegate cache), xUnit, BenchmarkDotNet (opsiyonel ölçüm).

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-04**)

## Global Constraints

- **Tek yön:** Projeksiyon yalnız event'ten beslenir. Çapraz-modül **senkron** DB okuması yasak; mimari test `Modules_Should_Not_Reference_Other_Modules` korumayı sürdürür.
- **Idempotency:** Her projeksiyon handler'ı `inbox_messages` guard'ını kullanır; aynı event iki kez gelse sayaç bozulmaz.
- **Yeniden kurulabilirlik:** Her projeksiyon, outbox geçmişinden **yeniden inşa edilebilir** olmalı; bunun için `POST /api/admin/projections/{name}/rebuild` ucu P12'de eklenecek, bu planda `IProjectionRebuilder` arayüzü ve modül-içi implementasyon hazırlanır.
- **Kapsam kilidi:** Bu plan **yeni iş özelliği eklemez**. Yalnız desen + bir taşıma + iki performans düzeltmesi. Kapsam büyütmesi yasak.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Projeksiyon deseni (`ProjectionEntity` + `ProjectionHandler`) ✅

**Files:**
- Create: ~~`src/Shared/Infrastructure/Projections/ProjectionEntity.cs`~~ → **Task 2'de `src/Shared/Kernel/ProjectionEntity.cs`'e taşındı** (Domain katmanı Infrastructure'a referans veremez)
- Create: `src/Shared/Infrastructure/Projections/ProjectionHandler.cs`
- Create: `src/Shared/Infrastructure/Projections/IProjectionRebuilder.cs`
- Test: `tests/Unit/ProjectionHandlerTests.cs`
- Modify: `tests/Unit/TestDoubles/TestModuleDbContext.cs` — **plan dışı, gerekliydi:** `ProjectionHandler` testi için `ProjectionEntity` türeten bir test entity'si (`TestCounterProjection`) + `DbSet` gerekiyordu

**Interfaces:**
- Produces:
  - ```csharp
    public abstract class ProjectionEntity
    {
        public Guid LastAppliedEventId { get; protected set; }
        public DateTime UpdatedOnUtc { get; protected set; }
        public void MarkApplied(Guid eventId, DateTime nowUtc) { LastAppliedEventId = eventId; UpdatedOnUtc = nowUtc; }
    }
    ```
  - `abstract class ProjectionHandler<TEvent> : IdempotentIntegrationEventHandler where TEvent : class` — `ApplyAsync` içinde payload'ı `TEvent`'e çözer, `ProjectAsync(TEvent payload, IntegrationEvent envelope, CancellationToken)` çağırır; commit tabana aittir. **Plan düzeltmesi:** kısıt önce `where TEvent : IIntegrationEvent` yazıyordu; `TEvent` zarf değil **payload sözleşmesidir** (mevcut `ParentReadModelProjections` deseninde de payload'lar düz `record`'dur), o yüzden `class` kısıtı doğrudur.
  - `interface IProjectionRebuilder { string Name { get; } Task<int> RebuildAsync(CancellationToken cancellationToken); }`

- [x] **Step 1: Testi yaz (kırmızı)**

`tests/Unit/ProjectionHandlerTests.cs`:
```csharp
[Fact]
public async Task Same_Event_Twice_Should_Project_Once()
{
    // TestModuleDbContext + sahte projeksiyon handler (sayaç artıran)
    // Aynı EventId ile iki kez HandleAsync → sayaç 1 olmalı, inbox_messages'ta tek satır
}

[Fact]
public async Task Failed_Projection_Should_Not_Write_Inbox_Row()
{
    // ProjectAsync exception atsın → inbox_messages boş kalmalı (atomiklik)
}

[Fact]
public async Task MarkApplied_Should_Record_Event_Id()
{
}
```
> `tests/Unit/TestDoubles/TestModuleDbContext.cs` ve `IdempotentIntegrationEventHandlerTests` mevcut; onların kurulumunu yeniden kullan.

- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectionHandlerTests"`
- [x] **Step 3: Tipleri yaz** (yukarıdaki imzalar birebir).
- [x] **Step 4: Yeşil gör** — aynı komut → PASS.
- [x] **Step 5: Doküman + commit**

`doc/architecture/backend.md`'ye "Projeksiyon (read-model) deseni" başlığı: ne zaman kullanılır, nerede yaşar, nasıl yeniden inşa edilir, hangi kurallar geçerlidir.
```bash
git add src/Shared/Infrastructure/Projections tests doc/architecture/backend.md
git commit -m "feat(shared): projeksiyon (read-model) deseni (C-03)"
```

---

### Task 2: İlk tüketici — öğretmen dashboard projeksiyonu ✅

**Files:**
- Create: `src/API.Host/Projections/` **yerine** → Create: `src/Modules/Reporting/Domain/TeacherDashboardProjection.cs`
- Create: `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionHandlers.cs`
- Modify: `src/Modules/Reporting/Infrastructure/ReportingDbContext.cs` (ilk gerçek entity — iskeletten çıkıyor)
- Modify: `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` (artık `AddModuleDbContext` kaydı yapılır)
- Modify: `src/Modules/Reporting/API/ReportingModule.cs` (yeni sorgu ucu)
- ~~Modify: `src/API.Host/TeacherDashboardEndpoints.cs` (BFF → projeksiyondan okuma)~~ — **yapılmadı, bkz. Step 5 sapması**
- Test: `tests/Unit/TeacherDashboardProjectionTests.cs`
- **Plan dışı, gerekliydi:**
  - Move: `ProjectionEntity` → `src/Shared/Kernel/ProjectionEntity.cs` (Task 1'de Infrastructure'a konmuştu; `Reporting.Domain` Infrastructure'a referans veremez — `Domain_Projects_Should_Not_Take_Web_Or_EfCore_Dependencies` mimari testi zorluyor)
  - Create: `src/Modules/Reporting/Application/ReportingFeatures.cs` (CQRS sözleşmesi: query + handler + `ReportingAuthorizer`; `AuthorizationCoverageValidator` her sorgu için authorizer istiyor)
  - Create: `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionReader.cs`
  - Create: `src/Modules/Reporting/Infrastructure/ReportingDesignTimeDbContextFactory.cs` (`dotnet ef migrations add` için)
  - Modify: `tests/Unit/EgitimUssu.Tests.Unit.csproj` (Reporting Domain/Application/Infrastructure referansları)

**Interfaces:**
- Produces:
  - `sealed class TeacherDashboardProjection : ProjectionEntity` — `Guid TeacherUserId` (PK) + `TeacherDashboardItem` çocuk kümesi (composite PK `(TeacherUserId, Kind, ItemId)`).
    **Plan düzeltmesi — sayaçlar saklanmıyor, kümeden türetiliyor:** (a) `TodayLessonCount` tarihe bağlıdır, saklanan bir sayaç ertesi gün yanlışa döner — ders satırı `OccursAtUtc` ile tutulur, "bugün" sorgu anında `IClock` ile hesaplanır; (b) "+1/−1" sayaç deseni bu domain'de kırılıyor (aynı ödev hem `Completed` hem `Approved` olur, aynı ödeme iki kez `Overdue` işaretlenebilir, `MarkCompleted` guard'sız) — küme üyeliğinde ekleme upsert, çıkarma "varsa sil" olduğundan çift sayım imkânsız.
  - Handler'lar (7): `LessonScheduleUpsertProjectionHandler` (Scheduled **+ Rescheduled** — plan Rescheduled'ı atlıyordu, olmadan ders saati bayatlıyor), `LessonScheduleCancelledProjectionHandler`, `AssignmentOpenedProjectionHandler`, `AssignmentClosedProjectionHandler` (Completed **+ Approved**), `PaymentOverdueProjectionHandler`, `PaymentSettledProjectionHandler`, `TeacherStudentLinkProjectionHandler`.
  - `GET /api/reporting/teachers/{teacherUserId}/dashboard` (auth; öğretmen kendisi veya Admin) — projeksiyon beslenmemişse **404 `reporting.dashboard_not_ready`**

- [x] **Step 1: Testleri yaz (kırmızı)** — her handler için "event → sayaç doğru değişti"; aynı event iki kez → sayaç bir kez; ilgisiz öğretmenin sayacı etkilenmiyor.
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~TeacherDashboardProjectionTests"`
- [x] **Step 3: Reporting modülünü iskeletten çıkar**
  - `ReportingDbContext`'e `DbSet<TeacherDashboardProjection>` + `teacher_dashboard_projections` tablosu.
  - `DependencyInjection`'a `AddModuleDbContext<ReportingDbContext>(configuration, "Reporting", ReportingDbContext.SchemaName)` (K4 notu: artık entity var, kayıt güvenli).
  Run: `dotnet ef migrations add AddTeacherDashboardProjection --project src/Modules/Reporting/Infrastructure --startup-project src/API.Host --context ReportingDbContext`
- [x] **Step 4: Handler'ları ve sorgu ucunu yaz.**
- [~] **Step 5: BFF'i projeksiyona çevir — YAPILMADI (plan hatası)**
  `src/API.Host/TeacherDashboardEndpoints.cs` yalnız sayaç değil, **liste** döndürüyor: `todayLessons` (konu, format, saat, konum), `pendingAssignments` (başlık, son teslim), `overduePayments` (açıklama, tutar, vade) — mobil `teacher_dashboard_summary_model.dart` bunları okuyor. Bugünkü integration event'ler bu alanları **taşımıyor** (`AssignmentCreatedDomainEvent`'te başlık/tarih yok, `LessonScheduledDomainEvent`'te konu/konum yok, `PaymentBecameOverdueDomainEvent`'te açıklama yok), dolayısıyla listeler projeksiyondan üretilemez. Sayaçları projeksiyondan, listeleri modüllerden almak ikisinin **birbirinden sapmasına** yol açardı — bugünkünden kötü. BFF olduğu gibi bırakıldı; taşıma üç modülün event sözleşmesini zenginleştirmeyi gerektirir → **ayrı iş** (aday: P08/P13).
  Ayrıca `IProjectionRebuilder` implementasyonu da yapılmadı: yeniden inşa kaynağı diğer modüllerin outbox'ı/verisi olurdu, Reporting bunları **okuyamaz** (modül sınırı) — rebuild host seviyesinde orkestre edilmeli (P12).
- [x] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [x] **Step 7: Doküman + commit**

`doc/modules/m14_reporting.md` (🔴 → 🟡: ilk projeksiyon), `doc/modules/00_genel_bakis.md` (Reporting artık gerçek endpoint), `doc/modules/veri_modeli.md`, `doc/INDEX.md` durum sütunu.
```bash
git add src/Modules/Reporting src/API.Host tests doc
git commit -m "feat(reporting): ogretmen dashboard projeksiyonu (ilk read-model)"
```

---

### Task 3: Authorizer/handler çift sorgusunu gider (O2)

**Files:**
- Create: `src/Shared/Application/IRequestEntityCache.cs`
- Create: `src/Shared/Infrastructure/Application/RequestEntityCache.cs`
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (scoped kayıt)
- Modify: `src/Modules/Students/Application/StudentProfilePolicies.cs` + `StudentProfileFeatures.cs` (ilk kullanım)
- Test: `tests/Unit/RequestEntityCacheTests.cs`

**Interfaces:**
- Produces:
  - ```csharp
    /// <summary>Bir istek kapsamında authorizer'ın yüklediği varlığı handler'a taşır (çift DB sorgusunu önler).</summary>
    public interface IRequestEntityCache
    {
        void Set<T>(object key, T entity) where T : class;
        bool TryGet<T>(object key, out T? entity) where T : class;
    }
    ```

- [ ] **Step 1: Testi yaz (kırmızı)** — `Set` sonrası `TryGet` aynı örneği döndürür; farklı tip/anahtar için `false`; kapsam (scope) dışına sızmaz.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~RequestEntityCacheTests"`
- [ ] **Step 3: Implementasyonu yaz** (`Dictionary<(Type, object), object>` + scoped DI).
- [ ] **Step 4: `GetStudentProfileByIdQuery` yolunda uygula**
  `StudentProfileQueryAuthorizer` profili yükledikten sonra `_cache.Set(query.StudentId, profile)`; handler önce `TryGet` dener, bulamazsa DB'ye gider.
- [ ] **Step 5: Ölçüm** — Aynı sorgu için EF log'unda **tek** `SELECT` göründüğünü doğrula:
  Run: `dotnet run --project src/API.Host` + `curl` ile bir profil çek + log'da `student_profiles` SELECT sayısını say.
  Expected: 1.
- [ ] **Step 6: Kalan çift-sorgu noktalarını listele ve dönüştür** — `grep -rn "Authorizer" src/Modules --include='*.cs' | grep -i "repository"` ile authorizer içinde repository kullanan tüm sınıfları çıkar; her biri için aynı deseni uygula (Payments, Assignments, Scheduling, Study).
- [ ] **Step 7: Yeşil gör + commit**

```bash
dotnet test EgitimUssu.slnx
git add src/Shared src/Modules tests doc
git commit -m "perf(shared): authorizer/handler cift sorgusunu gider (O2)"
```

---

### Task 4: Dispatcher reflection maliyetini gider (O4)

**Files:**
- Modify: `src/Shared/Infrastructure/Application/CommandDispatcher.cs`
- Modify: `src/Shared/Infrastructure/Application/QueryDispatcher.cs`
- Test: `tests/Unit/DispatcherCacheTests.cs`

**Interfaces:**
- Dış sözleşme **değişmez** (`ICommandDispatcher.Dispatch`, `IQueryDispatcher.Dispatch`). İçeride tip başına `Func<object, object, CancellationToken, Task<object>>` derlenip `ConcurrentDictionary` içinde önbelleklenir; `dynamic` kaldırılır.

- [ ] **Step 1: Testi yaz (kırmızı)** — aynı komut tipi 100 kez dispatch edildiğinde delegate cache 1 kez üretiliyor (sayaçlı sahte ile); validator → authorizer → handler sırası korunuyor; authorizer başarısızsa handler **hiç çağrılmıyor**.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~DispatcherCacheTests"`
- [ ] **Step 3: Dispatcher'ları derlenmiş delegate'e çevir.**
- [ ] **Step 4: Tüm paketi koştur** — Run: `dotnet test EgitimUssu.slnx` → yeşil (bu değişiklik tüm modülleri etkiler, tam paket şart).
- [ ] **Step 5: Commit**

```bash
git add src/Shared/Infrastructure/Application tests
git commit -m "perf(shared): dispatcher delegate cache, dynamic kaldirildi (O4)"
```

---

### Task 5: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh` → yeşil, atlanan 0.
- [ ] **Step 2: Dokümanlar**
  - `doc/architecture/backend.md`: projeksiyon deseni + `IRequestEntityCache` + dispatcher cache.
  - `doc/modules/mimari_inceleme.md`: O2, O4, O5 maddeleri `✅ Düzeltildi 2026-09-02 (P07)`.
  - `doc/modules/m14_reporting.md`: durum 🟡.
  - `doc/denetim/2026-09-02_eksik_analizi.md`: C-03, C-05, C-06 → `✅ (P07)`.
- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs: P07 read-model altyapisi kapanisi (C-03/C-05/C-06)"
```

---

## Kabul Kriterleri

- [x] Projeksiyon deseni testlerle sabitlendi (idempotent + atomik)
- [~] Öğretmen dashboard **sayaçları** projeksiyondan okunuyor (`GET /api/reporting/teachers/{id}/dashboard`); projeksiyon yoksa 404. BFF `dashboard-summary` **taşınmadı** (liste alanları event'lerde yok — Task 2 Step 5)
- [x] Aynı event iki kez geldiğinde dashboard sayaçları bozulmuyor
- [ ] Korumalı sorguda varlık **bir kez** yükleniyor (EF log ile doğrulandı)
- [ ] Dispatcher'da `dynamic` kullanımı kalmadı (`grep -rn "dynamic" src/Shared/Infrastructure/Application` boş)
- [x] `Modules_Should_Not_Reference_Other_Modules` mimari testi hâlâ yeşil
- [ ] Tam test paketi (Docker'lı) yeşil
