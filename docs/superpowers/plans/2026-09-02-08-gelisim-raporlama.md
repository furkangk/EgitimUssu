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
- Modify: `src/Modules/ProgressTracking/API/ProgressTrackingModule.cs`
- Modify: `src/Modules/ProgressTracking/Infrastructure/*` (DbContext + repository + DI + migration)
- Test: `tests/Unit/ProgressSnapshotTests.cs`

**Interfaces:**
- Produces:
  - `enum SnapshotPeriod { Weekly = 1, Monthly = 2 }`
  - `sealed class ProgressSnapshot : AggregateRoot<Guid>` — `Guid StudentId`, `string Subject`, `SnapshotPeriod Period`, `DateOnly PeriodStart`, `int MasteryScore` (0–100), `int StudyMinutes`, `int TestCount`, `decimal AverageNet`, `DateTime CreatedOnUtc`.
  - `enum ProgressTrend { Rising = 1, Flat = 2, Falling = 3 }` — iki ardışık snapshot arası `MasteryScore` farkı: `>= +5` Rising, `<= -5` Falling, aksi Flat.
  - `GET /api/progress-tracking/students/{studentId}/snapshots?subject=&period=&from=&to=` → `IReadOnlyList<ProgressSnapshotResponse>` (her öğede `Trend`).

- [ ] **Step 1: Testleri yaz (kırmızı)**
```csharp
[Fact] public void Trend_Should_Be_Rising_When_Score_Increases_By_Five_Or_More() { }
[Fact] public void Trend_Should_Be_Flat_Within_Threshold() { }
[Fact] public void Trend_Should_Be_Falling_When_Score_Drops() { }
[Fact] public void Week_Start_Should_Be_Monday_In_Utc() { }
[Fact] public async Task Snapshot_Service_Should_Be_Idempotent_For_Same_Period() { /* iki tur → tek satır */ }
```
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ProgressSnapshotTests"`
- [ ] **Step 3: Domain + migration**
  Run: `dotnet ef migrations add AddProgressSnapshots --project src/Modules/ProgressTracking/Infrastructure --startup-project src/API.Host --context ProgressTrackingDbContext`
  Benzersizlik: `(StudentId, Subject, Period, PeriodStart)` unique index.
- [ ] **Step 4: `ProgressSnapshotService`'i yaz** — günde bir kez çalışır; kapanmış (geçmiş) haftalar/aylar için eksik snapshot'ları üretir. Veri kaynağı **modül içi** `TopicMastery` + Study'den gelen event'lerle beslenmiş sayaçlardır (çapraz-modül DB okuması yok).
- [ ] **Step 5: Sorgu ucu + authorizer** — öğrenci kendisi; öğretmen bağlı öğrencisi; veli onaylı+paylaşıma açık çocuğu (gizlilik filtresi zorunlu).
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Doküman + commit**

```bash
git add src/Modules/ProgressTracking tests doc
git commit -m "feat(progress): haftalik/aylik snapshot + trend (M10-1)"
```

---

### Task 2: Öğretmen ve veli gelişim görünümü (M10-2/M10-3/M10-4)

**Files:**
- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingFeatures.cs`
- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingPolicies.cs`
- Modify: `src/Modules/ProgressTracking/API/ProgressTrackingModule.cs`
- Create: `src/Modules/ProgressTracking/Infrastructure/LessonSessionCompletedProgressHandler.cs`
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs` (çocuk dashboard'una gelişim özeti)
- Test: `tests/Unit/ProgressAccessTests.cs`

**Interfaces:**
- Produces:
  - `GET /api/progress-tracking/teachers/{teacherUserId}/students/{studentId}/overview`
  - `GET /api/progress-tracking/parents/{parentUserId}/children/{studentId}/overview`
  - `LessonSessionCompletedProgressHandler : IdempotentIntegrationEventHandler` — tamamlanan ders, ilgili konuya **katılım** katkısı yazar (M10-4).
  - Hedefe ulaşınca otomatik `Achieved` + `TopicGoalAchievedDomainEvent` → Notifications (M10-3).

- [ ] **Step 1: Yetki testlerini yaz (kırmızı)** — bağlı olmayan öğretmen 403; onaysız veli 403; `PrivacyLevel.Hidden` iken veli boş özet alır; öğrenci kendisi 200.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ProgressAccessTests"`
- [ ] **Step 3: Query + authorizer + endpoint'leri yaz.**
- [ ] **Step 4: Ders tamamlanma tüketicisini yaz** (idempotent taban sınıf).
- [ ] **Step 5: Hedef otomatik kapanışı + event + bildirim tüketicisi.**
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Doküman + commit**

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
