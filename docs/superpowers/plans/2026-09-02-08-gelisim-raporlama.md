# P08 — Gelişim Takibi ve Raporlama Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** M10'u zaman serisiyle tamamlamak (snapshot + trend + öğretmen/veli görünümü) ve M14'ü gerçek bir raporlama modülüne dönüştürmek (öğretmen aylık özeti, öğrenci çalışma/performans analizi, PDF rapor, boş zaman analizi) — mobilde grafiklerle.

**Architecture:** M10'a `ProgressSnapshot` (öğrenci × dönem × ders) eklenir; haftalık `ProgressSnapshotService` (BackgroundService) üretir, trend iki ardışık snapshot farkından hesaplanır. M14, P07'de kurulan projeksiyon deseniyle beslenen üç projeksiyon üzerine kurulur: `TeacherMonthlyProjection`, `StudentStudyProjection`, `StudentPerformanceProjection`. PDF, sunucuda QuestPDF ile üretilir ve `IFileStorage`'a (P04) yazılıp yetkili indirme ucundan sunulur. Mobilde `fl_chart` ile zaman serisi ve dağılım grafikleri.

**Tech Stack:** .NET 9, EF Core, QuestPDF (Community lisansı), xUnit; Flutter `fl_chart`.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-04**)

## Global Constraints

- **Önkoşul:** P07 (projeksiyon deseni) ve P04 (dosya depolama) tamamlanmış olmalı.
- **Gizlilik:** Veli ve öğretmen görünümü daima `IStudentPrivacyDirectory` + `PrivacyLevel` (P05 Task 4) filtresinden geçer. Filtre atlanırsa test kırmızıya döner.
- **Premium kapısı:** PDF rapor ve detaylı analiz premium'dur; bu planda `IEntitlementDirectory` (P09) henüz yoksa **geçici** olarak `MembershipGate` genelleştirilmiş hali kullanılır ve P09'da tek satırla değiştirilir. Kapı **kaldırılmaz**, ertelenmez.
- **Idempotency:** Tüm projeksiyon handler'ları `inbox_messages` guard'ını kullanır.
- **Zaman:** `IClock.UtcNow` — hafta başlangıcı **Pazartesi**, tüm hesaplar UTC.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: `ProgressSnapshot` + trend (M10-1)

**Files:**
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs`
- Create: `src/Modules/ProgressTracking/Infrastructure/ProgressSnapshotService.cs` (BackgroundService)
- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingFeatures.cs` (query + handler)
- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingContracts.cs` (response + repo arayüzü) *(plan dışı, zorunlu: yanıt kayıtları ve `IProgressRepository` bu dosyada)*
- Modify: `src/Modules/ProgressTracking/API/ProgressTrackingModule.cs`
- Modify: `src/Modules/ProgressTracking/Infrastructure/*` (DbContext + repository + DI + migration)
- Modify: `tests/Unit/EgitimUssu.Tests.Unit.csproj` *(plan dışı, zorunlu — Öğrenilenler #72: ProgressTracking Domain/Application/Infrastructure referansı yoktu)*
- Test: `tests/Unit/ProgressSnapshotTests.cs`
- Test: `tests/Integration/RealProgressSnapshotTests.cs` *(plan dışı, zorunlu — Öğrenilenler #81: yeni depo/sorgu/uç yazan görev en az bir Testcontainers testi bırakır)*

**Interfaces:**
- Produces:
  - `enum SnapshotPeriod { Weekly = 1, Monthly = 2 }`
  - `sealed class ProgressSnapshot : AggregateRoot<Guid>` — `Guid StudentId`, `string Subject`, `SnapshotPeriod Period`, `DateOnly PeriodStart`, `int MasteryScore` (0–100), `int StudyMinutes`, `int TestCount`, `decimal AverageNet`, `DateTime CreatedOnUtc`.
  - ⚠️ **Plan düzeltmesi (2026-09-04):** `enum ProgressTrend { Rising, Flat, Falling }` **yazılamaz** — `ProgressTracking.Domain`'de aynı adlı enum zaten var (`Improving = 1, Stable = 2, Declining = 3`, `TopicMastery.Trend` kullanıyor). İkinci bir tanım derlenmez, yenidenadlandırma mevcut API yanıtını kırardı. Mevcut enum yeniden kullanıldı; eşik mantığı `ProgressTrendCalculator.FromScores(int? previous, int current)` içinde: fark `>= +5` → `Improving`, `<= -5` → `Declining`, aksi → `Stable`, önceki dönem yoksa `Stable`.
  - `static class SnapshotPeriods` — `StartOf(period, date)` (haftalık: Pazartesi, aylık: ayın 1'i) + `Previous(period, periodStart)`.
  - `ProgressSnapshot.MasteryScore` **`int`** (0–100), `AverageNet` 0–1 net **oranı** — `TopicMastery.AverageNetRatio` ile aynı ölçek.
  - `GET /api/progress-tracking/students/{studentId}/snapshots?subject=&period=&from=&to=` → `IReadOnlyList<ProgressSnapshotResponse>` (her öğede `Trend`).

- [x] **Step 1: Testleri yaz (kırmızı)** — 9 birim testi (adlar mevcut enum'a göre `Improving`/`Stable`/`Declining`):
```csharp
[Fact] public void Trend_Should_Be_Improving_When_Score_Increases_By_Five_Or_More() { }
[Fact] public void Trend_Should_Be_Stable_Within_Threshold() { }
[Fact] public void Trend_Should_Be_Declining_When_Score_Drops() { }
[Fact] public void Week_Start_Should_Be_Monday_In_Utc() { }
[Fact] public void Month_Start_Should_Be_First_Day_And_Previous_Should_Step_One_Period_Back() { }
[Fact] public async Task Snapshot_Service_Should_Be_Idempotent_For_Same_Period() { /* iki tur → tek satır */ }
[Fact] public async Task Snapshot_Should_Cover_The_Last_Closed_Period() { }
[Fact] public async Task Snapshot_Should_Aggregate_Topics_Of_The_Same_Subject() { }
[Fact] public async Task Query_Should_Attach_Trend_Per_Subject_Series() { }
```
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ProgressSnapshotTests"` → **CS0246** (`ProgressSnapshot`/`SnapshotPeriod`/`ProgressTrendCalculator`/`SnapshotPeriods` yok)
- [x] **Step 3: Domain + migration**
  Run: `dotnet ef migrations add AddProgressSnapshots --project src/Modules/ProgressTracking/Infrastructure --startup-project src/API.Host --context ProgressTrackingDbContext`
  Benzersizlik: `(StudentId, Subject, Period, PeriodStart)` unique index. ✅ Üretildi (`progress_snapshots` tablosu + 2 indeks).
- [x] **Step 4: `ProgressSnapshotService`'i yaz** — Veri kaynağı **modül içi** `TopicMastery` (çapraz-modül DB okuması yok); ders bazında `GROUP BY` → skor ortalaması, süre/test toplamı, net ortalaması.
  ⚠️ **Plan düzeltmesi:** plan "kapanmış (geçmiş) haftalar/aylar için eksik snapshot'ları üretir" diyordu — **geriye doldurma yapılmadı**. `TopicMastery` kümülatiftir; geçmiş bir haftanın o günkü değeri yeniden kurulamaz, bugünkü toplamı 3 eski haftaya yazmak **sahte düz trend** üretirdi. Tur yalnız **en son kapanmış** hafta ve ayı üretir; servis günde bir koştuğu için kapanan dönem en geç bir günde yakalanır. Yapı: `ProgressSnapshotProcessor` (test edilebilir tur) + `ProgressSnapshotService : BackgroundService` (5 dk açılış gecikmesi, 24 sa periyot) — `AssignmentDueScanner`/`PaymentOverdueScanner` deseni.
- [x] **Step 5: Sorgu ucu + authorizer** — `GET /students/{id}/snapshots`, mevcut açık-generik `ProgressOwnershipQueryAuthorizer` ile korundu (öğrenci kendisi + Admin).
  ⚠️ **Kapsam notu:** öğretmen/veli erişimi ve gizlilik filtresi bu görevde **yapılmadı** — `ProgressOwnershipGuard`'ın genişletilmesi ve `IStudentPrivacyDirectory` filtresi **Task 2'nin** konusudur (Task 2 `Files:` listesi `ProgressTrackingPolicies.cs`'i zaten kapsıyor).
- [x] **Step 6: Yeşil gör** — Run: `./scripts/test-with-docker.sh` → 4 mimari + **494 birim** + **31 integration**, Başarısız 0, Atlanan 0 · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata
- [x] **Step 7: Doküman + commit**

```bash
git add src/Modules/ProgressTracking tests doc
git commit -m "feat(progress): haftalik/aylik snapshot + trend (M10-1)"
```

---

### Task 2: Öğretmen ve veli gelişim görünümü (M10-2/M10-3/M10-4)

**Files:**
- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingFeatures.cs`
- **Create** (plan "Modify" diyordu, dosya yoktu): `src/Modules/ProgressTracking/Application/ProgressTrackingPolicies.cs`
- Modify: `src/Modules/ProgressTracking/API/ProgressTrackingModule.cs`
- Create: `src/Modules/ProgressTracking/Infrastructure/LessonSessionCompletedProgressHandler.cs`
- ~~Modify: `src/Modules/Parents/Application/ParentFeatures.cs`~~ **yapılmadı** — gerekçe Step 3 notunda
- Modify: `src/Modules/ProgressTracking/Infrastructure/DependencyInjection.cs` *(plan dışı, zorunlu: yeni handler/authorizer kayıtları)*
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` + migration `AddLessonAttendanceToTopicMastery` *(plan dışı, zorunlu: M10-4 katılım alanları)*
- Modify: `src/Shared/Contracts/StudentDirectoryContract.cs` + `src/Modules/Students/Infrastructure/StudentDirectory.cs` *(plan dışı, zorunlu: öğretmen–öğrenci bağı sözleşmesi)*
- Create: `src/Modules/Notifications/Infrastructure/TopicGoalAchievedNotificationHandler.cs` + Notifications DI *(plan dışı, zorunlu: "→ Notifications" gereği)*
- Modify: `src/Modules/ProgressTracking/Infrastructure/*.csproj` (`InternalsVisibleTo`, Öğrenilenler #101)
- Test: `tests/Unit/ProgressAccessTests.cs`
- Test: `tests/Integration/RealProgressAccessTests.cs` *(plan dışı, zorunlu — Öğrenilenler #81)*
- Modify: `tests/Unit/LessonSessionCompletedPaymentHandlerTests.cs`, `tests/Unit/SelfLessonPolicyTests.cs` *(sözleşme genişleyince `IStudentDirectory` sahteleri)*

**Interfaces:**
- Produces:
  - `GET /api/progress-tracking/teachers/{teacherUserId}/students/{studentId}/overview`
  - `GET /api/progress-tracking/parents/{parentUserId}/children/{studentId}/overview`
  - `LessonSessionCompletedProgressHandler : IdempotentIntegrationEventHandler` — tamamlanan ders, ilgili konuya **katılım** katkısı yazar (M10-4).
  - Hedefe ulaşınca otomatik `Achieved` + `TopicGoalAchievedDomainEvent` → Notifications (M10-3).

- [x] **Step 1: Yetki testlerini yaz (kırmızı)** — 20 test: bağlı olmayan öğretmen 403, başka öğretmenin rotası 403 (IDOR), onaysız veli 403, Admin serbest; `Hidden`/paylaşım kapalı → boş özet, `Limited` → yalnız sayaç, `Standard` → tam; ders katılımı + idempotency + plansız oturum; hedef otomatik kapanışı (seviye + net eşiği).
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test ... --filter "FullyQualifiedName~ProgressAccessTests"` → **CS0246** (`GetTeacherStudentProgressOverviewQuery`/`ParentChildProgressAuthorizer`/`RegisterLessonAttendance` yok)
- [x] **Step 3: Query + authorizer + endpoint'leri yaz.**
  Yetki **iki katmanlıdır**: rotadaki kimlik = oturumdaki kullanıcı (IDOR), sonra bağ (`IStudentDirectory.IsLinkedTeacherOfStudentAsync` / `IParentAccessDirectory.IsApprovedParentOfStudentAsync`). Gizlilik `ProgressPrivacyFilter` ile handler'da uygulanır — üç uç (öğrenci/öğretmen/veli) aynı `ProgressOverviewReader` gövdesini paylaşır.
  ⚠️ **Plan düzeltmesi:** `ParentFeatures.cs`'e gelişim özeti gömülmedi. Parents'ın ProgressTracking verisini okuması **yeni bir `Shared/Contracts` sözleşmesi** ister ve gizlilik kapısını iki yerde çoğaltırdı; veli aynı veriyi yeni `parents/.../overview` ucundan alıyor. Mobil (Task 5) bu ucu çağıracak. Not `m10_progress_tracking.md` §1 🔴 listesine yazıldı.
- [x] **Step 4: Ders tamamlanma tüketicisini yaz** (idempotent taban sınıf) — `LessonSessionCompletedProgressHandler`. Olay yükü branş/konu taşımadığı için branş+süre `ILessonScheduleDirectory`, konu `ILessonSessionAccessService` sözleşmesinden okunur. `TopicMastery.RegisterLessonAttendance` yeni `AttendedLessonCount`/`TotalLessonMinutes` alanlarını yazar; ders süresi **çalışma bileşenine** eklenir (ağırlıklar değişmedi, ders yoksa davranış aynı). Plansız oturum işlenmiş sayılır ama satır üretmez.
- [x] **Step 5: Hedef otomatik kapanışı + event + bildirim tüketicisi.** `MarkAchieved` + `TopicGoalAchievedDomainEvent` zaten vardı; eksik olan (a) `TargetNetRatio`'nun **hiç değerlendirilmemesi** — artık seviye **ve** net birlikte aranıyor, (b) olayın tüketicisi — M11 `TopicGoalAchievedNotificationHandler` eklendi.
- [x] **Step 6: Yeşil gör** — Run: `./scripts/test-with-docker.sh` → **514 birim** + **33 integration**, Başarısız 0, Atlanan 0 · `dotnet test tests/Architecture` → 4/4 · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata
- [x] **Step 7: Doküman + commit**

```bash
git add src/Modules tests doc
git commit -m "feat(progress): ogretmen/veli gelisim gorunumu + hedef otomatik kapanis (M10-2/3/4)"
```

---

### Task 3: Raporlama projeksiyonları (M14-1/M14-2/M14-3/M14-5)

**Files:**
- Create: `src/Modules/Reporting/Domain/ReportingProjections.cs`
- Create: `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs`
- Create: `src/Modules/Reporting/Application/ReportingAnalyticsFeatures.cs` + `ReportingPolicies.cs` + `FreeSlotCalculator.cs`
- Modify: `src/Modules/Reporting/API/ReportingModule.cs`
- Modify: `src/Modules/Reporting/Infrastructure/{ReportingDbContext,DependencyInjection}.cs` + `ReportingProjectionReader.cs` + migration `AddReportingProjections`
- Create: `src/Shared/Contracts/TeacherAvailabilityContract.cs` · Modify: `LessonScheduleReadContract.cs` *(M14-5 sözleşmeleri)*
- Create: `src/Modules/Teachers/Infrastructure/TeacherAvailabilityDirectory.cs` + Teachers DI *(sözleşmeyi uygular)*
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleDirectory.cs` *(dolu aralık listesi)*
- Test: `tests/Unit/ReportingProjectionTests.cs`
- Test: `tests/Integration/RealReportingAnalyticsTests.cs` *(plan dışı, zorunlu — Öğrenilenler #81)*
- Modify: `tests/Unit/{CreateSessionFromScheduleTests,ProgressAccessTests}.cs` *(sözleşme genişleyince `ILessonScheduleDirectory` sahteleri)*

**Interfaces:**
- Produces:
  - `TeacherMonthlyProjection : ProjectionEntity` — `Guid TeacherUserId`, `int Year`, `int Month`, `int CompletedLessonCount`, `decimal CollectedAmount`, `decimal ExpectedAmount`, `int ActiveStudentCount`, `int CancelledLessonCount`.
  - `StudentStudyProjection : ProjectionEntity` — `Guid StudentId`, `int Year`, `int WeekOfYear`, `int StudyMinutes`, `int SessionCount`, `int TestCount`.
  - `StudentPerformanceProjection : ProjectionEntity` — `Guid StudentId`, `string Subject`, `int Year`, `int Month`, `decimal AverageNet`, `int MasteryScore`.
  - ⚠️ **Plan düzeltmesi (2026-09-04):** `Delta` **saklanmaz**. Komşuya (bir önceki aya) bağlı bir değerdir;
    sorgu penceresi kırpıldığında satır kendi yazıldığı andaki komşusuna göre yanlış cevap verir — M10-1'de
    aynı hata trend için yakalanmıştı (Öğrenilenler #102). `ReportingDelta.FromScores(previous, current)` ile
    sorgu anında üretilir, serinin ilk ayı 0 alır.
  - ⚠️ **Plan düzeltmesi:** üç projeksiyonun da sayaçları **kümeden türetilir** (child item tabloları), düz
    `+1` alanlar değil. Gerekçe P07 Task 2 ile aynı: aynı iş olayı iki farklı event'le gelirse (ders yeniden
    tamamlanır, seans süresi düzeltilir) sayaç bozulur. Tek istisna `CollectedAmount` — kaynak olay mutlak
    tutar değil önceki/şimdiki çifti taşır, tahsilatın hangi aya düştüğü ancak **farkla** doğru çıkar.
  - Uçlar:
    - `GET /api/reporting/teachers/{teacherUserId}/monthly?year=&month=` (M07-3 dönemsel gelir raporunun karşılığı)
    - `GET /api/reporting/teachers/{teacherUserId}/free-slots?from=&to=` (boş zaman analizi — M14-5; uygunluk slotları eksi planlı dersler)
    - `GET /api/reporting/students/{studentId}/study-analysis?from=&to=`
    - `GET /api/reporting/students/{studentId}/performance?subject=&from=&to=`

- [x] **Step 1: Testleri yaz (kırmızı)** — 20 test: UTC ay sınırı + ISO hafta yıl sınırı; her projeksiyonda tekrar eden iş olayının sayacı bozmaması; tahsilat farkının birikmesi; event → alan eşlemesi (5 handler); boş zaman analizinin dört kuralı (çıkarma, eşik altı parça, çakışan aralık birleştirme, haftalık tekrar).
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test ... --filter "FullyQualifiedName~ReportingProjectionTests"` → **24 × CS0246 + 11 × CS0103 + 7 × CS1061** (projeksiyonlar, `ReportingPeriods`, `FreeSlotCalculator` yok)
- [x] **Step 3: Projeksiyonlar + handler'lar + migration**
  Run: `dotnet ef migrations add AddReportingProjections ...` ✅ **6 tablo** üretildi (3 projeksiyon + 3 item tablosu).
- [x] **Step 4: Sorgu uçları + authorizer'lar** — `TeacherScopedReportAuthorizer<T>` (öğretmen kendisi/Admin) ve `StudentScopedReportAuthorizer<T>` (öğrenci kendisi · bağlı öğretmen · onaylı veli · Admin). Gizlilik yetkilendiricide değil `StudentReportPrivacyGate`'te: kapalıysa **403 değil boş sonuç** (P08 Task 2 ile aynı ayrım, Öğrenilenler #106).
- [x] **Step 5: Boş zaman analizi**
  ⚠️ **Plan düzeltmesi:** plan "planlı dersler `ILessonScheduleDirectory` ile alınır" diyordu — ama o sözleşmede yalnız `GetAsync(lessonScheduleId)` vardı, **tek ders** döner. Bir aralıktaki oluşumlar tekrar kuralı açılımı + occurrence istisnası + izin bloğu ister; hepsi Scheduling'in içindedir. Sözleşme `ListTeacherBusyIntervalsAsync(teacherUserId, from, to)` ile genişletildi ve Scheduling bunu **öğretmen takvimi ucuyla aynı yoldan** uygular (Öğrenilenler #67). Hesap saf `FreeSlotCalculator`'da.
- [x] **Step 6: Yeşil gör** — Run: `./scripts/test-with-docker.sh` → 4 mimari + **534 birim** + **36 integration**, Başarısız 0, Atlanan 0 · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata
- [x] **Step 7: Doküman + commit**

```bash
git add src/Modules/Reporting src/Shared/Contracts tests doc
git commit -m "feat(reporting): ogretmen aylik + ogrenci calisma/performans + bos zaman projeksiyonlari (M14-1/2/3/5)"
```

---

### Task 4: PDF öğrenci raporu (M14-4)

**Files:**
- Modify: `src/Modules/Reporting/Infrastructure/EgitimUssu.Modules.Reporting.Infrastructure.csproj` (QuestPDF 2026.8.0)
- Create: `src/Modules/Reporting/Infrastructure/StudentReportPdfGenerator.cs`
- Create: `src/Modules/Reporting/Application/StudentReportFeatures.cs` *(plan "Modify Application/*" diyordu)*
- Modify: `src/Modules/Reporting/Application/ReportingPolicies.cs` + `ReportingAnalyticsFeatures.cs`
- Modify: `src/Modules/Reporting/API/ReportingModule.cs` (2 endpoint + istek gövdesi)
- Create: `src/Modules/Reporting/Domain/StudentReport.cs` + `Infrastructure/StudentReportRepository.cs` + migration `AddStudentReports` *(plan dışı, **zorunlu**: indirme ucu yalnız `reportId` taşır, raporun sahibi ancak kayıttan bilinir)*
- Move: `src/Modules/Study/Application/MembershipGate.cs` → `src/Shared/Contracts/MembershipGate.cs` *(plandaki "genelleştirilmiş MembershipGate" gereği)*
- Modify: `src/Shared/Contracts/StudentDirectoryContract.cs` (`GetDisplayNameAsync`) · Create: `TeacherDirectoryContract.cs`, `StudentAssignmentDigestContract.cs` + Students/Teachers/Assignments uygulamaları ve DI *(plan dışı, zorunlu: rapor içeriğinin kaynakları)*
- Test: `tests/Unit/StudentReportPdfGeneratorTests.cs`
- Test: `tests/Integration/RealStudentReportTests.cs` *(plan dışı, zorunlu — Öğrenilenler #81)*
- Modify: `tests/Unit/{LessonSessionCompletedPaymentHandlerTests,SelfLessonPolicyTests,ProgressAccessTests}.cs` *(sözleşme genişleyince `IStudentDirectory` sahteleri)*

**Interfaces:**
- Produces:
  - `interface IStudentReportPdfGenerator { byte[] Generate(StudentReportData data); }`
  - `sealed record StudentReportData(string StudentName, string TeacherName, DateOnly PeriodStart, DateOnly PeriodEnd, int StudyMinutes, int LessonCount, int AssignmentCompletionPercent, IReadOnlyList<SubjectPerformanceRow> Subjects, string? Note)`
  - `static class StudentReportComposer` — saf biçimlendirme (süre, net %, tamamlama %, başlık/özet satırları, dosya adı).
  - `static class ReportEntitlement` + `ReportEntitlementResolver` — premium kapısı.
  - `POST /api/reporting/students/{studentId}/report` → `{ reportId, studentId, periodStart, periodEnd, fileName, sizeBytes, createdOnUtc }`
  - `GET /api/reporting/reports/{reportId}/file` → yetkili indirme.
  - ⚠️ **Plan düzeltmesi (2026-09-04):** raporun **kalıcı bir kaydı** gerekiyor. Plan yalnız üretim + indirme
    ucundan söz ediyordu; ama `GET /reports/{reportId}/file` rotasında öğrenci kimliği yok — yetki ancak
    `student_reports` satırından çözülebilir. `StudentReport` aggregate + tablo + repository eklendi.
  - ⚠️ **Plan düzeltmesi:** premium kapısı **öğretmen üyeliği olmadığı için** istek sahibinin tier'ına
    bağlanamaz — `IMembershipDirectory` yalnız `StudentProfile`'a bakar, her öğretmene `Free` döner ve uç
    ölü doğardı. Geçici kural: `ReportEntitlement.EffectiveTier` = Admin → Premium, aksi hâlde
    **taraflardan biri (istek sahibi ya da rapor konusu öğrenci) Premium ise** Premium. P09'da
    `IEntitlementDirectory` gelince yalnız bu fonksiyon değişir.
  - ⚠️ **Plan düzeltmesi:** `LessonCount` ve `AssignmentCompletionPercent` Reporting'in projeksiyonlarında
    yoktu. Ders sayısı `teacher_monthly_items` satırlarından (öğrenci bazlı, tüm öğretmenler) sayılıyor;
    ödev tamamlama oranı yeni `IStudentAssignmentDigestDirectory` sözleşmesiyle Assignments'tan okunuyor.
    Öğrenci/öğretmen adı için de `IStudentDirectory.GetDisplayNameAsync` ve `ITeacherDirectory` eklendi.

- [x] **Step 1: Testi yaz (kırmızı)** — 22 test: saf biçimlendirme (süre/net/tamamlama/başlık/dosya adı), premium kapısının üç durumu, gerçek QuestPDF çıktısı (`%PDF-` + >1 KB), handler (depolama+kayıt sırası, premium reddi, gizlilik reddi, `Limited` kırılım gizleme, ters aralık, veri toplama), indirme sorgusu + üç yetki senaryosu.
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentReportPdfGeneratorTests"` → **17 × CS0246**
- [x] **Step 3: QuestPDF ekle ve üreticiyi yaz** — QuestPDF **2026.8.0**. Lisans `LicenseType.Community` olarak **statik kurucuda** ayarlandı (plan `DependencyInjection` diyordu; sınıf birim testinde DI'sız da kurulduğu için orası yetmezdi). Düzen: başlık + öğrenci/öğretmen/dönem, üç özet kutusu, ders tablosu (delta ile), öğretmen notu, sayfa altlığı.
- [x] **Step 4: Premium kapısı** — `ReportEntitlementResolver` + `MembershipGate.Allows(tier, PremiumFeature.PdfReport)`; free ikilide `reporting.premium_required` → **402**.
- [x] **Step 5: Uçlar + yetkili indirme** — `StudentReportAccessChecker` tek kural olarak çıkarıldı; sorgu, komut ve dosya yetkilendiricileri onu kullanıyor (P04 ödev eki deseni).
- [x] **Step 6: Yeşil gör + elle doğrula** — Run: `./scripts/test-with-docker.sh` → 4 mimari + **556 birim** + **38 integration**, Başarısız 0, Atlanan 0 · `dotnet build EgitimUssu.slnx -warnaserror --no-incremental` → 0 uyarı / 0 hata · elle: örnek PDF üretildi, `file` → "PDF document, version 1.4, 1 pages", `qlmanage` ile render edilip **gözle doğrulandı** (Türkçe karakterler dahil düzen doğru).
- [x] **Step 7: Doküman + commit**

```bash
git add src/Modules/Reporting tests doc
git commit -m "feat(reporting): PDF ogrenci raporu (M14-4)"
```

---

### Task 5: Mobil — gelişim ve rapor ekranları

**Files:**
- Modify: `mobile/lib/features/progress/**` (repository + **yeni** `presentation/cubit/progress_cubit.dart` + `presentation/widgets/progress_snapshot_chart.dart` + sayfa)
- Create: `mobile/lib/features/reporting/**` (domain sözleşmeleri, repository, `DeviceReportFileOpener`, iki cubit, iki sayfa, grafik widget'ları)
- Modify: `mobile/lib/features/parent/presentation/pages/parent_child_detail_page.dart` (gelişim grafiği)
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` ("Raporlar" satırını aktifleştir — P05'te pasifleştirilmişti)
- Modify: `mobile/lib/core/di/injector.dart` + `mobile/lib/core/routing/app_router.dart` *(plan dışı, zorunlu: yeni repository/opener kaydı + `/student/reports` ve `/teacher/reports` rotaları)*
- Modify: `mobile/pubspec.yaml` (`open_filex`) *(Step 5'te öngörülmüştü)*
- Test: `mobile/test/features/reporting/reporting_cubit_test.dart`
- Test: `mobile/test/features/progress/progress_cubit_test.dart` + `mobile/test/helpers/fake_{reporting,progress}_repository.dart` *(plan dışı, zorunlu — "testsiz kod yok" çıtası + Öğrenilenler #8: sahteler `test/helpers/` altında)*
- Create: `doc/pages/progress_overview.md`, `doc/pages/teacher_reports.md` · Modify: `doc/pages/00_pages_index.md`, `doc/architecture/mobile_flutter.md`, `doc/architecture/widgets.md`, `doc/modules/m10_progress_tracking.md`, `doc/modules/m14_reporting.md`

**Interfaces:**
- `ProgressRepository.listSnapshots(studentId, {subject, period, from, to})` + `getTeacherStudentOverview(...)` + `getParentChildOverview(...)`, `ReportingRepository.teacherMonthly({teacherUserId, year, month})`, `teacherFreeSlots(...)`, `studentStudyAnalysis(...)`, `studentPerformance(...)`, `generateStudentReport(studentId)`, `downloadReport(reportId)`.
- ⚠️ **Plan düzeltmesi (2026-09-04):** plan `ProgressRepository.snapshots(...)` diyordu; ad `listSnapshots` yapıldı (mevcut `listMastery` ile aynı desen).
- ⚠️ **Plan düzeltmesi:** **snapshot serisi öğretmen/veli kapsamında çekilemez.** `GET /students/{id}/snapshots`
  Task 1'de `ProgressOwnershipQueryAuthorizer` ile korundu (öğrenci + Admin). Bu yüzden `ProgressCubit`
  öğretmen/veli kapsamında snapshot isteği **atmıyor** ve çizgi grafiği gizleniyor; velinin "gelişim grafiği"
  M14-2/M14-3 uçlarından (haftalık çalışma + ders bazlı net) çiziliyor.
- ⚠️ **Plan düzeltmesi:** plandaki `"Veli paylaşımı kapalı"` boş durum metni **birebir yazılamaz.** Sunucu
  gizlilik kapalıyken 403 değil **boş liste/özet** döndürüyor (`StudentReportPrivacyGate`, `ProgressPrivacyFilter`);
  istemci "veri yok" ile "paylaşım kapalı"yı ayırt edemiyor. Metin iki olasılığı birden söylüyor
  ("… Öğrenci paylaşımını kapatmış da olabilir") — kesin bilgi uydurmak yerine.
- `ReportFileOpener` soyutlaması: cubit dosya sistemine dokunmadan test edilebilsin diye eklendi
  (`DeviceReportFileOpener` = geçici dizin + `open_filex`).

- [x] **Step 1: Cubit testlerini yaz (kırmızı)** — 19 test: `TeacherReportsCubit` (yükleme/hata/ay geçişi ×2), `StudentReportCubit` (yükleme/hata/boş, üret-indir-aç, `402` ve `403` → `premiumRequired`, boş dosya, açılamama, `isGenerating` akışı), `ProgressCubit` (üç kapsam, seri gruplama/sıralama, hata, boş).
- [x] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/reporting/reporting_cubit_test.dart test/features/progress/progress_cubit_test.dart` → **`Method not found` / `Undefined name`** (cubit'ler, `ProgressScope`, `ProgressStatus`, `ProgressSnapshot` yok)
- [x] **Step 3: Repository + cubit'leri yaz** (mock fallback yok) — `ProgressRepositoryImpl` üç uçla genişletildi, `ReportingRepositoryImpl` altı uç, `ProgressCubit` + `TeacherReportsCubit` + `StudentReportCubit`; DI'ya `ReportingRepository` + `ReportFileOpener` kaydedildi.
- [x] **Step 4: Grafikler** — `ProgressSnapshotChart` (ders başına çizgi, y 0–100), `WeeklyStudyBarChart` (son 12 hafta, tooltip'li), `SubjectNetChart` (ders başına en güncel ay, % + delta). Renkler `AppColors` token'ları; geçişler **250 ms / `Curves.easeOut`** (`animations.md`).
- [x] **Step 5: PDF indirme** — `open_filex ^4.7.0` eklendi; gerekçe `doc/architecture/mobile_flutter.md` §10'a yazıldı (`url_launcher` `file://` adresini Android/iOS'ta güvenilir açamıyor). Akış: `POST .../report` → yetkili `GET /reports/{id}/file` (bayt) → geçici dizin → sistem görüntüleyici. Üç adım ayrı ayrı hata verebilir; kısmi başarı korunuyor.
- [x] **Step 6: Veli çocuğu detayına gelişim grafiği** — `_ChildProgressSection` (`StudentReportCubit` + iki grafik); boş durum metni yukarıdaki plan düzeltmesine göre yazıldı.
- [x] **Step 7: Testler** — Run: `cd mobile && flutter test` → **146/146 geçti** (önce 127) · `flutter analyze` → **4 info** (önce 5; hepsi P08 öncesinden — 3× `deprecated_member_use` + 1× `directives_ordering`; biri import yerleşimiyle kapandı)
- [x] **Step 8: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): gelisim grafikleri + rapor ekranlari"
```

---

### Task 6: Kapanış

**Files:**
- Test: `tests/Integration/RealProjectionRebuildTests.cs` *(plan dışı, **zorunlu** — Step 2 aşağıdaki plan düzeltmesi gereği bir doğrulama testine dönüştü)*
- Modify: `doc/modules/m10_progress_tracking.md`, `doc/modules/m14_reporting.md`, `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/architecture/backend.md`, `doc/roles/{ogretmen,veli,ogrenci}.md`, `doc/denetim/2026-09-02_eksik_analizi.md`

- [x] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh` → **556 birim + 38 integration**, Başarısız 0, Atlanan 0 · `dotnet test tests/Architecture` → **4/4** · `cd mobile && flutter test` → **146/146**
- [x] **Step 2: Projeksiyon yeniden inşa doğrulaması**
  ⚠️ **Plan düzeltmesi (2026-09-04):** `IProjectionRebuilder`'ın **uygulaması yok** ve bu bilinçlidir — arayüz modül
  içinde uygulanamaz (yeniden inşa kaynağı diğer modüllerin outbox'ı; Reporting onları okuyamaz, modül sınırı),
  host seviyesinde orkestre edilmeli (Öğrenilenler #93 → **P12**). Bu yüzden adım, "arayüzü çalıştır"dan
  **arayüzün dayandığı özelliği kanıtla**ya çevrildi: `RealProjectionRebuildTests` gerçek Postgres'te üç faz
  koşuyor — (1) olay akışı işlenir → sayaçlar okunur, (2) projeksiyon tabloları **ve** Reporting `inbox_messages`
  boşaltılıp **aynı** akış **aynı `EventId`'lerle** yeniden oynatılır → sayaçlar birebir aynı, (3) akış üçüncü kez
  (inbox boşaltılmadan) teslim edilir → hiçbir sayaç değişmez. Yani projeksiyonlar olay akışının **saf bir
  katlaması**; P12'ye kalan yalnız geçmiş olayları çeken plumbing.
  Test ayırt ediciliği kanıtlandı: inbox temizliği kaldırılınca **kırmızıya döndü**.
  ⚠️ **İkinci düzeltme:** ilk sürüm akışı genel `IEventBus`'a yayınlıyordu; bu diğer modüllerin tüketicilerini de
  tetikleyip komşu testi (`RealProgressSnapshotTests`) kırdı. Doğrusu — ve rebuild'in gerçek modeli — **yalnız
  hedef modülün handler'larını** oynatmaktır. Kural `doc/architecture/backend.md` §7.1'e yazıldı.
- [x] **Step 3: Dokümanlar** — `m10_progress_tracking.md` (🟡 → **🟢**), `m14_reporting.md` (🟡 → **🟢**; plan "🔴 → 🟢" diyordu, P07 onu zaten 🟡'ye çıkarmıştı), `modules/00_genel_bakis.md` (durum tablosu + mobil feature tablosuna `reporting` satırı + ProgressTracking/Reporting envanter başlıkları), `doc/INDEX.md` (iki durum + iki yeni sayfa md'si), `architecture/backend.md` §7.1 (yeniden inşanın iki tuzağı), `roles/ogretmen.md` + `roles/veli.md` + `roles/ogrenci.md`, `denetim/2026-09-02_eksik_analizi.md` (M10-1…4, M14-1…5, M09-1, **M07-3**, D-16 → ✅).
- [x] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P08 gelisim ve raporlama kapanisi (M10-*/M14-*/M09-1)"
```

---

## Kabul Kriterleri

- [x] Haftalık snapshot otomatik üretiliyor, aynı dönem için tekrarlanmıyor *(Task 1; unique index + iki tur idempotency gerçek Postgres'te)*
- [x] Trend üç durumu doğru hesaplanıyor *(Task 1; `ProgressTrendCalculator` ±5 eşiği)*
- [x] Öğretmen bağlı öğrencisinin, veli onaylı çocuğunun gelişimini görüyor; gizlilik seviyesi uygulanıyor *(Task 2; bağ yoksa 403, `Hidden` → boş, `Limited` → yalnız sayaç)*
- [x] Öğretmen aylık raporu doğru ders sayısı ve tahsil/beklenen tutarı gösteriyor *(Task 3+5)*
- [x] Boş zaman analizi uygunluk − planlı ders farkını döndürüyor *(Task 3; gerçek Postgres'te 180 dk boşluk)*
- [x] PDF rapor üretilip indirilebiliyor; free kullanıcıda kilitli *(Task 4: 402 · Task 5: mobilde kilit kartı + cihazda açma)*
- [x] Aynı event tekrar geldiğinde projeksiyon sayaçları bozulmuyor *(Task 3 birim + **Task 6 faz 3** gerçek Postgres'te)*
- [x] Mobilde gelişim ve rapor grafikleri gerçek veriyle çiziliyor *(Task 5; mock fallback yok)*
- [x] Tam test paketi (Docker'lı) yeşil *(Task 6 Step 1)*

> **Kapsam dışı bırakılan, bilinçli borçlar:** `IProjectionRebuilder` host uygulaması + `POST /api/admin/projections/{name}/rebuild` → **P12**; premium kapısının gerçek `IEntitlementDirectory`'ye bağlanması → **P09**. İkisi de ilgili modül dokümanlarında etiketlendi.
