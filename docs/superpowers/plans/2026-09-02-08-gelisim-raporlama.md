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
- Modify: `src/Modules/Reporting/Application/*` (query + handler + authorizer)
- Modify: `src/Modules/Reporting/API/ReportingModule.cs`
- Modify: `src/Modules/Reporting/Infrastructure/*` + migration
- Test: `tests/Unit/ReportingProjectionTests.cs`

**Interfaces:**
- Produces:
  - `TeacherMonthlyProjection : ProjectionEntity` — `Guid TeacherUserId`, `int Year`, `int Month`, `int CompletedLessonCount`, `decimal CollectedAmount`, `decimal ExpectedAmount`, `int ActiveStudentCount`, `int CancelledLessonCount`.
  - `StudentStudyProjection : ProjectionEntity` — `Guid StudentId`, `int Year`, `int WeekOfYear`, `int StudyMinutes`, `int SessionCount`, `int TestCount`.
  - `StudentPerformanceProjection : ProjectionEntity` — `Guid StudentId`, `string Subject`, `int Year`, `int Month`, `decimal AverageNet`, `int MasteryScore`, `int Delta`.
  - Uçlar:
    - `GET /api/reporting/teachers/{teacherUserId}/monthly?year=&month=` (M07-3 dönemsel gelir raporunun karşılığı)
    - `GET /api/reporting/teachers/{teacherUserId}/free-slots?from=&to=` (boş zaman analizi — M14-5; uygunluk slotları eksi planlı dersler)
    - `GET /api/reporting/students/{studentId}/study-analysis?from=&to=`
    - `GET /api/reporting/students/{studentId}/performance?subject=&from=&to=`

- [ ] **Step 1: Testleri yaz (kırmızı)** — her projeksiyon için event → alan eşlemesi; tekrar eden event'te sayaç bozulmaması; ay sınırında doğru bucket'a düşme (UTC).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ReportingProjectionTests"`
- [ ] **Step 3: Projeksiyonlar + handler'lar + migration**
  Run: `dotnet ef migrations add AddReportingProjections --project src/Modules/Reporting/Infrastructure --startup-project src/API.Host --context ReportingDbContext`
- [ ] **Step 4: Sorgu uçları + authorizer'lar** (öğretmen kendi verisi; öğrenci kendi verisi; veli onaylı çocuk + gizlilik; Admin serbest).
- [ ] **Step 5: Boş zaman analizi** — öğretmenin `TeacherAvailabilitySlot`'ları `Shared/Contracts` üzerinden okunur (Teachers modülü `ITeacherAvailabilityDirectory` açar), planlı dersler `ILessonScheduleDirectory` (P06 Task 4) ile alınır; fark hesaplanır.
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Doküman + commit**

```bash
git add src/Modules/Reporting src/Shared/Contracts tests doc
git commit -m "feat(reporting): ogretmen aylik + ogrenci calisma/performans + bos zaman projeksiyonlari (M14-1/2/3/5)"
```

---

### Task 4: PDF öğrenci raporu (M14-4)

**Files:**
- Modify: `src/Modules/Reporting/Infrastructure/EgitimUssu.Modules.Reporting.Infrastructure.csproj` (QuestPDF)
- Create: `src/Modules/Reporting/Infrastructure/StudentReportPdfGenerator.cs`
- Modify: `src/Modules/Reporting/Application/*` (command + handler)
- Modify: `src/Modules/Reporting/API/ReportingModule.cs` (2 endpoint)
- Test: `tests/Unit/StudentReportPdfGeneratorTests.cs`

**Interfaces:**
- Produces:
  - `interface IStudentReportPdfGenerator { byte[] Generate(StudentReportData data); }`
  - `sealed record StudentReportData(string StudentName, string TeacherName, DateOnly PeriodStart, DateOnly PeriodEnd, int StudyMinutes, int LessonCount, int AssignmentCompletionPercent, IReadOnlyList<SubjectPerformanceRow> Subjects, string? Note)`
  - `POST /api/reporting/students/{studentId}/report` → raporu üretir, `IFileStorage`'a `reporting/{studentId}/{reportId}.pdf` olarak yazar, `{ reportId, createdOnUtc }` döner.
  - `GET /api/reporting/reports/{reportId}/file` → yetkili indirme.

- [ ] **Step 1: Testi yaz (kırmızı)** — üretilen bayt dizisi `%PDF` ile başlıyor; öğrenci adı ve dönem PDF metninde geçiyor (QuestPDF'in `Document.GeneratePdf()` çıktısını `PdfPig` gibi bir okuyucu olmadan doğrulamak için: en az boyut > 1 KB + `%PDF-` başlığı + üretimin exception atmaması yeterlidir; metin doğrulaması için `StudentReportData`'yı doğrudan biçimlendiren saf fonksiyonu ayrı test et).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentReportPdfGeneratorTests"`
- [ ] **Step 3: QuestPDF ekle ve üreticiyi yaz**
  Run: `dotnet add src/Modules/Reporting/Infrastructure/EgitimUssu.Modules.Reporting.Infrastructure.csproj package QuestPDF`
  Lisans: `QuestPDF.Settings.License = LicenseType.Community;` uygulama başlangıcında bir kez ayarlanır (`DependencyInjection` içinde).
  Rapor düzeni: başlık (EğitimÜssü logosu yerine metin), öğrenci/dönem bilgisi, özet kutuları (çalışma süresi, ders sayısı, ödev tamamlama), ders bazlı tablo, öğretmen notu.
- [ ] **Step 4: Premium kapısı** — handler başında entitlement kontrolü; free kullanıcıda `reporting.premium_required` (402/403 eşlemesi `ApiErrorHttpResults` ile).
- [ ] **Step 5: Uçlar + yetkili indirme** (P04'teki ödev indirme deseni).
- [ ] **Step 6: Yeşil gör + elle doğrula** — Bir öğrenci için rapor üret, dosyayı indir, PDF açılıyor mu bak.
- [ ] **Step 7: Doküman + commit**

```bash
git add src/Modules/Reporting tests doc
git commit -m "feat(reporting): PDF ogrenci raporu (M14-4)"
```

---

### Task 5: Mobil — gelişim ve rapor ekranları

**Files:**
- Modify: `mobile/lib/features/progress/**` (repository + cubit + sayfa)
- Create: `mobile/lib/features/reporting/**` (data/domain/presentation)
- Modify: `mobile/lib/features/parent/presentation/pages/parent_child_detail_page.dart` (gelişim grafiği)
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` ("Raporlar" satırını aktifleştir — P05'te pasifleştirilmişti)
- Test: `mobile/test/features/reporting/reporting_cubit_test.dart`
- Create: `doc/pages/progress_overview.md`, `doc/pages/teacher_reports.md`

**Interfaces:**
- `ProgressRepository.snapshots({studentId, subject, period, from, to})`, `ReportingRepository.teacherMonthly({teacherUserId, year, month})`, `studentStudyAnalysis(...)`, `generateStudentReport(studentId)`, `downloadReport(reportId)`.

- [ ] **Step 1: Cubit testlerini yaz (kırmızı)** — yükleme/başarı/hata; premium gerektiren uçta `402/403` → `PremiumRequired` durumu.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/reporting/reporting_cubit_test.dart`
- [ ] **Step 3: Repository + cubit'leri yaz** (mock fallback yok).
- [ ] **Step 4: Grafikler** — `fl_chart` ile: gelişim çizgi grafiği (snapshot serisi), haftalık çalışma süresi çubuk grafiği, ders bazlı net dağılımı. Renkler `doc/architecture/design_system.md` token'larından; `doc/architecture/animations.md`'deki geçiş kuralları.
- [ ] **Step 5: PDF indirme** — "Rapor oluştur" → ilerleme → dosya indirilip cihazda açılır (`open_filex` benzeri bir paket gerekiyorsa önce `pubspec`'e ekle ve gerekçesini `doc/architecture/mobile_flutter.md`'ye yaz).
- [ ] **Step 6: Veli çocuğu detayına gelişim grafiği** (gizlilik seviyesine göre boş durum metni: "Veli paylaşımı kapalı").
- [ ] **Step 7: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 8: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): gelisim grafikleri + rapor ekranlari"
```

---

### Task 6: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → yeşil.
- [ ] **Step 2: Projeksiyon yeniden inşa doğrulaması** — Reporting projeksiyon tablolarını boşalt, `IProjectionRebuilder`'ı çalıştır, sayaçların aynı değerlere döndüğünü doğrula.
- [ ] **Step 3: Dokümanlar** — `doc/modules/m10_progress_tracking.md` (🟡 → 🟢), `doc/modules/m14_reporting.md` (🔴 → 🟢), `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/roles/veli.md` + `doc/roles/ogretmen.md`, `doc/denetim/2026-09-02_eksik_analizi.md` (M10-*, M14-*, M09-1 → `✅ (P08)`).
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P08 gelisim ve raporlama kapanisi (M10-*/M14-*/M09-1)"
```

---

## Kabul Kriterleri

- [ ] Haftalık snapshot otomatik üretiliyor, aynı dönem için tekrarlanmıyor
- [ ] Trend üç durumu doğru hesaplanıyor
- [ ] Öğretmen bağlı öğrencisinin, veli onaylı çocuğunun gelişimini görüyor; gizlilik seviyesi uygulanıyor
- [ ] Öğretmen aylık raporu doğru ders sayısı ve tahsil/beklenen tutarı gösteriyor
- [ ] Boş zaman analizi uygunluk − planlı ders farkını döndürüyor
- [ ] PDF rapor üretilip indirilebiliyor; free kullanıcıda kilitli
- [ ] Aynı event tekrar geldiğinde projeksiyon sayaçları bozulmuyor
- [ ] Mobilde gelişim ve rapor grafikleri gerçek veriyle çiziliyor
- [ ] Tam test paketi (Docker'lı) yeşil
