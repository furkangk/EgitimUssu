---
title: "M14 — Raporlama ve Analiz (Reporting)"
summary: "Read-model raporlama modülü: öğretmen dashboard + aylık özet, öğrenci haftalık çalışma ve aylık performans projeksiyonları, boş zaman analizi ve premium kapılı PDF öğrenci raporu canlı; mobil rapor ekranları da canlı; modül 🟢 (P08)"
tags: [modul, reporting, raporlama, read-model, projeksiyon, faz-5]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Reporting/**
updated: 2026-09-04
---

# 📊 Raporlama ve Analiz Modülü (M14) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M14 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Reporting`) + Flutter mobil
> **PRD:** M14 · **Faz:** 5 (Premium & Analitik) · **Durum:** 🟢 **Tamam** (P08 kapanışı, 2026-09-04) — dört read-model (öğretmen paneli, öğretmen aylık özeti, öğrenci haftalık çalışma, öğrenci aylık performansı) + boş zaman analizi + **premium kapılı PDF öğrenci raporu** + **mobil rapor ekranları** (öğretmen aylık + öğrenci analizi/PDF + veli gelişim bölümü). ⚠️ İki bilinen borç kapsam dışıdır: geçmiş dönem beslemesi `IProjectionRebuilder` ile gelecek (**P12**), premium kapısı P09 Task 2'de gerçek entitlement'a bağlandı ✅
> **Mimari:** CQRS + Outbox + Integration Event; önerilen **read-model / projeksiyon** yaklaşımı, PostgreSQL (`reporting` şeması), Redis (rapor cache)
> **Marka rengi (mobil):** `0xFF082B4F`

> Bu modül **kendi iş verisi üretmez**; Payments, Scheduling, Study, Assignments, ProgressTracking gibi modüllerin verisini **rapor ve analiz** olarak birleştiren bir **okuma (read-model)** modülüdür.
>
> ✅ **Mimari kısıt kapandı (P07):** [`mimari_inceleme.md`](mimari_inceleme.md) **O5** — kanonik projeksiyon deseni kuruldu
> (`ProjectionEntity` · `ProjectionHandler<TEvent>` · `IProjectionRebuilder`, bkz. [`../architecture/backend.md`](../architecture/backend.md) §7.1).
> Bu modül desenin **ilk gerçek tüketicisi**dir.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### 🟡 Var olan (P08 Task 4 sonrası)

| Bileşen | Konum | Durum |
|---------|-------|-------|
| Module tanımı | `src/Modules/Reporting/API/ReportingModule.cs` | `GET /status` (placeholder) + `dashboard` + `monthly`, `free-slots`, `study-analysis`, `performance` + **`POST students/{id}/report`**, **`GET reports/{id}/file`** |
| Domain (panel) | `src/Modules/Reporting/Domain/TeacherDashboardProjection.cs` | `TeacherDashboardProjection` + `TeacherDashboardItem` + `TeacherDashboardItemKind` |
| Domain (analitik) | `src/Modules/Reporting/Domain/ReportingProjections.cs` **(P08 Task 3)** | `TeacherMonthlyProjection`, `StudentStudyProjection`, `StudentPerformanceProjection` (+ item tipleri) · `ReportingPeriods` (UTC ay / ISO hafta) · `ReportingDelta` |
| Domain (rapor kaydı) | **`src/Modules/Reporting/Domain/StudentReport.cs`** (P08 Task 4) | `StudentReport` aggregate — üretilmiş PDF'in `IFileStorage` anahtarı + dönem + üreten kullanıcı |
| DbContext | `src/Modules/Reporting/Infrastructure/ReportingDbContext.cs` | 9 tablo: panel 2 + analitik 6 + **`student_reports`** (`reporting` şeması) |
| Projeksiyon handler'ları | `.../TeacherDashboardProjectionHandlers.cs` + **`.../ReportingProjectionHandlers.cs`** | 7 panel + **8 analitik** handler (Scheduling/Assignments/Payments/Students/LessonSessions/Study/ProgressTracking) |
| Sorgu | `.../Application/ReportingFeatures.cs` + `ReportingAnalyticsFeatures.cs` | `GetTeacherDashboardQuery` + 4 analitik sorgu/handler |
| PDF raporu | **`.../Application/StudentReportFeatures.cs`** (P08 Task 4) | `GenerateStudentReportCommand` + `GetStudentReportFileQuery` · `StudentReportComposer` (saf metin) · `StudentReportAssembler` · `ReportEntitlementResolver` (premium kapısı — `IEntitlementDirectory`, P09 Task 2) |
| PDF üretici | **`.../Infrastructure/StudentReportPdfGenerator.cs`** | QuestPDF (Community lisansı statik kurucuda ayarlanır) |
| Yetki + gizlilik | `.../Application/ReportingPolicies.cs` | `TeacherScopedReportAuthorizer<T>`, **`StudentReportAccessChecker`** (tek kural), `StudentScopedReportAuthorizer<T>`, **`StudentScopedReportCommandAuthorizer<T>`**, **`StudentReportFileAuthorizer`**, `StudentReportPrivacyGate` |
| Boş zaman | **`.../Application/FreeSlotCalculator.cs`** | Saf fonksiyon: haftalık uygunluk − dolu aralıklar |
| Reader | `.../TeacherDashboardProjectionReader.cs` + **`ReportingProjectionReader.cs`** | `AsNoTracking` okuma |
| DI | `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` | `AddModuleDbContext<ReportingDbContext>` + handler/sorgu/yetki kayıtları |
| Migration | `*_AddTeacherDashboardProjection.cs` + `*_AddReportingProjections.cs` + **`*_AddStudentReports.cs`** | `reporting` şeması + outbox/inbox/module_states + 8 projeksiyon tablosu + `student_reports` |

**Doğrulanmış gerçekler:**
- Projeksiyon **yalnız integration event'lerden** beslenir; Reporting hiçbir modülün DbContext'ini okumaz (`Modules_Should_Not_Reference_Other_Modules` yeşil).
- Idempotency ortak `inbox_messages` guard'ından gelir; ayrıca sayaçlar **küme üyeliğinden türetilir** (aşağıya bkz.), bu yüzden aynı olay iki kez gelse de bozulmaz.
- Canlı doğrulandı: ders planlandı → `todayLessonCount = 1`, ders iptal edildi → `todayLessonCount = 0`.

### ⚠️ Kapsam dışına bırakılanlar (P08 kapandı, bunlar başka planlara ait)
- **Veli özet/grafik raporu için ayrı `parents/...` ucu** — veli çocuğunun analizini `students/{id}/study-analysis`
  ve `students/{id}/performance` uçlarından (onaylı bağ + gizlilik kapısıyla) okuyor; mobil veli çocuk detayı da
  bunları kullanıyor. Ayrı uç **gereksiz**, isteğe bağlı.
- ~~**Mobil rapor ekranları**~~ ✅ (P08 Task 5) — [`../pages/teacher_reports.md`](../pages/teacher_reports.md).
- `IProjectionRebuilder` implementasyonu ve `POST /api/admin/projections/{name}/rebuild` (**P12**).
  Arayüz modül içinde uygulanamaz: yeniden inşa kaynağı diğer modüllerin outbox'ı, Reporting onları okuyamaz
  (modül sınırı) → host seviyesinde orkestre edilecek.
  **P08 Task 6'da uygulamanın dayandığı özellik kanıtlandı:** `RealProjectionRebuildTests` gerçek Postgres'te
  projeksiyon tablolarını **ve** Reporting inbox satırlarını boşaltıp aynı olay akışını (aynı `EventId`'lerle)
  yeniden oynatıyor; sayaçlar birebir aynı çıkıyor, üçüncü teslimde değişmiyor. Yani projeksiyonlar olay
  akışının saf bir katlaması — eksik olan yalnız geçmiş olayları çeken **plumbing**.
- ⚠️ **Geriye dönük besleme yok:** analitik projeksiyonlar yalnız kendilerinden **sonra** yayınlanan event'leri
  görür; geçmiş aylar rebuild yapılana kadar boş kalır (#93 → P12).
- ✅ **Premium kapısı gerçek entitlement'a bağlandı (P09 Task 2):** `ReportEntitlementResolver` artık
  `IEntitlementDirectory`'yi okuyor ve kural sadeleşti — **istek sahibinin** `entitlement.PdfReports`
  hakkı belirler, Admin daima geçer. P08'in geçici "taraflardan biri Premium ise üretilebilir" kuralı
  (`ReportEntitlement.EffectiveTier`) **kaldırıldı**: öğretmen üyeliği artık gerçekten var, dolayısıyla
  Free bir öğretmen premium öğrencisinin raporunu **üretemez**.

---

## 2. Domain / Projeksiyon Modeli

### 🟢 Mevcut — `TeacherDashboardProjection` (öğretmen paneli read-model'i)

Sayaçlar **saklanmaz, kümeden türetilir**: her ders / açık ödev / gecikmiş ödeme / aktif öğrenci bir
`TeacherDashboardItem` satırıdır. Ekleme upsert, çıkarma "varsa sil" olduğundan handler'lar doğal olarak idempotenttir —
"+1/−1 sayaç" deseninin kırıldığı yerler (aynı ödev hem `Completed` hem `Approved` olur, aynı ödeme iki kez `Overdue`
işaretlenir) sayacı bozmaz.

**`teacher_dashboard_projections`** (PK `TeacherUserId`): `LastAppliedEventId`, `UpdatedOnUtc` (`ProjectionEntity`'den).

**`teacher_dashboard_items`** (PK `TeacherUserId` + `Kind` + `ItemId`):

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Kind` | `TeacherDashboardItemKind` | `Lesson` (1) · `OpenAssignment` (2) · `OverduePayment` (3) · `ActiveStudent` (4) |
| `ItemId` | `Guid` | Kaynak modüldeki kaydın kimliği |
| `OccursAtUtc` | `DateTime?` | Ders başlangıcı (bugünkü ders sayımı bundan türer) |
| `Amount` / `Currency` | `decimal?` / `string?` | Gecikmiş ödeme tutarı ve para birimi |

**Türetilen değerler:** `CountLessonsOn(gün)` · `PendingAssignmentCount` · `OverduePaymentCount` ·
`OverdueAmountTotal` · `OverdueCurrency` · `ActiveStudentCount`.

> **Neden "bugünkü ders" saklanmıyor:** tarihe bağlı bir sayaç ertesi gün yanlışa döner. Ders satırı başlangıç saatiyle
> tutulur, "bugün" sınırı sorgu anında `IClock` ile hesaplanır.

### ⚠️ Önerilen — sonraki projeksiyonlar

Modül sınırı kuralı gereği **başka modülün DB'sine doğrudan erişilmez**. Önerilen yaklaşım: integration event'leri dinleyip Reporting kendi `reporting` şemasında **önceden hesaplanmış özet tabloları** tutar (eventual consistency). Sorgular hızlı olur ve premium analizler için uygundur.

**`TeacherMonthlyReportProjection` (öğretmen aylık özet):**

| Alan | Tip | Kaynak event |
|------|-----|--------------|
| `TeacherUserId` | `Guid` | — |
| `Year`, `Month` | `int` | — |
| `LessonCount` | `int` | Scheduling/LessonSession tamamlandı |
| `CompletedLessonCount` | `int` | LessonSessionCompleted |
| `ActiveStudentCount` | `int` | dönem içinde dersi olan farklı öğrenci |
| `CollectedAmount` | `decimal` | Payments (tahsil edilen) |
| `ExpectedAmount` | `decimal` | Payments (beklenen) |
| `LastRecalculatedOnUtc` | `DateTime` | projeksiyon güncelleme |

**`TeacherAvailabilityAnalysisProjection` (boş zaman analizi):** öğretmenin uygunluk slotları vs. dolu slotlar; haftalık doluluk oranı, en boş gün/saat dilimleri (Scheduling + Teachers uygunluk verisi).

**`StudentStudyProjection` (öğrenci çalışma analizi):**

| Alan | Tip | Kaynak |
|------|-----|--------|
| `StudentId` | `Guid` | — |
| dönem (`Year/Week` veya `Year/Month`) | — | — |
| `TotalStudyMinutes` | `int` | Study (M08) StudySessionEnded |
| `SessionCount` | `int` | Study |
| `SubjectBreakdown` | JSON/satır | konu bazlı dakika dağılımı |
| `PlannedVsActualMinutes` | (int, int) | hedef vs. gerçekleşen |

**`StudentPerformanceProjection` (performans değişimi):** konu bazlı net/başarı trendi, dönemler arası değişim (ProgressTracking M10 verisinden).

> Alternatif: senkron **read-contract** (Shared/Contracts üzerinden modüller arası okuma servisi). O5'te belirtildiği gibi şu an böyle bir mekanizma yok; projeksiyon yaklaşımı önerilir.

---

## 3. API Sözleşmesi

### Mevcut ✅

| Yetenek | Method + Route | Yetki |
|---------|----------------|-------|
| Sağlık/placeholder | `GET /api/reporting/status` | — (placeholder, `state="placeholder"`) |
| Öğretmen paneli sayaçları | `GET /api/reporting/teachers/{teacherUserId}/dashboard` | `AuthenticatedUser`; öğretmenin kendisi veya Admin (yoksa **403**) |
| Öğretmen aylık özeti (M14-1) | `GET /api/reporting/teachers/{teacherUserId}/monthly?year=&month=` | öğretmenin kendisi veya Admin |
| Boş zaman analizi (M14-5) | `GET /api/reporting/teachers/{teacherUserId}/free-slots?from=&to=` | öğretmenin kendisi veya Admin |
| Öğrenci çalışma analizi (M14-2) | `GET /api/reporting/students/{studentId}/study-analysis?from=&to=` | öğrenci kendisi · bağlı öğretmen · onaylı veli · Admin |
| Öğrenci performansı (M14-3) | `GET /api/reporting/students/{studentId}/performance?subject=&from=&to=` | aynı |
| **PDF rapor üretimi (M14-4)** | `POST /api/reporting/students/{studentId}/report` | aynı + **premium kapısı** (402) |
| **PDF rapor indirme (M14-4)** | `GET /api/reporting/reports/{reportId}/file` | raporun öğrencisi · bağlı öğretmen · onaylı veli · Admin |

`dashboard` yanıtı: `teacherUserId`, `todayLessonCount`, `pendingAssignmentCount`, `overduePaymentCount`,
`overdueAmountTotal`, `overdueCurrency`, `activeStudentCount`, `updatedOnUtc`.
Projeksiyon henüz beslenmediyse **404 `reporting.dashboard_not_ready`** döner — sıfır göstermek yalancı-yeşil olurdu.

> ℹ️ `GET /api/teachers/profiles/{id}/dashboard-summary` (BFF) **projeksiyona taşınmadı**: mobil panel ders/ödev/ödeme
> **listelerini** basıyor, bugünkü integration event'ler ise ders konusu/saati etiketi, ödev başlığı/son teslim tarihi ve
> ödeme açıklaması taşımıyor. Liste projeksiyondan üretilemediği için BFF üç modülü sorgulamayı sürdürüyor; taşımak
> üç modülün event sözleşmesini zenginleştirmeyi gerektirir (ayrı iş).
>
> **BFF yanıtı (P16 T5 / N-11, 2026-11-15):** `todayLessons` **Türkiye günüdür** (`ILocalCalendar.DayRangeUtc(Today())`;
> başlangıcı `[gün başı, gün sonu)` içinde olan oluşumlar — dün başlayıp bugüne taşan ya da tam gece yarısı biten ders
> "bugün" değildir; eskiden UTC günüydü, 00:00–03:00 TR dersleri düşüyordu). Yeni alan
> **`overduePaymentTotals: [{ currency, count, total }]`** — para birimi bazında, en erken vadeli para birimi önce.
> `overduePaymentsCurrency` + `overduePaymentsTotal` **eski tekil alanlardır** (geriye uyum — mobil okuyor; kullanımdan
> kalkacak): listenin ilk satırını taşır, yani **yalnız o para biriminin** toplamı (eskiden farklı para birimlerini
> toplayıp ilkinin etiketini basıyordu). `overduePaymentsCount` tüm para birimlerinin kayıt sayısıdır. Ödeme özetinin
> (`/payments/.../summary`) aylık kırılımındaki para birimi boyutu **P18 T3**'te.
> Projeksiyonlu `dashboard` ucunun "bugün"ü hâlâ UTC — oluşum bazlı yeniden yazım **P17 T7** (N-49).

**Yanıt sözleşmeleri (koddan, P08 Task 3):**

```
monthly         → { teacherUserId, year, month, completedLessonCount, cancelledLessonCount,
                    activeStudentCount, expectedAmount, collectedAmount, updatedOnUtc? }
free-slots      → [ { startAtUtc, endAtUtc, durationMinutes } ]
study-analysis  → [ { year, weekOfYear, weekStart, studyMinutes, sessionCount, testCount } ]
performance     → [ { subject, year, month, masteryScore, averageNet, testCount, delta } ]
report (POST)   → { reportId, studentId, periodStart, periodEnd, fileName, sizeBytes, createdOnUtc }
                  gövde: { from?, to?, note? }
report file     → application/pdf akışı (Content-Disposition: gelisim-raporu-<baslangic>_<bitis>.pdf)
```

- **Tüm sorgu parametreleri opsiyoneldir** (Öğrenilenler #85): `monthly` yıl/ay verilmezse içinde bulunulan ayı,
  `free-slots` aralık verilmezse bugünden itibaren bir haftayı kullanır.
- `monthly`, hiç aktivite olmayan ayda **404 değil** sıfırlı 200 döner: boş bir ay meşru bir cevaptır
  (panel ucundaki `dashboard_not_ready` durumundan farkı budur — orada satırın yokluğu "projeksiyon henüz
  beslenmedi" demekti).
- `free-slots` aralığı en fazla **92 gün**; daha uzunu `400 reporting.invalid_range` döner (haftalık şablon
  her güne yayıldığı için sınırsız pencere kabul edilmez).
- `performance` yanıtındaki **`delta` saklanmaz**, dönen serideki bir önceki aydan üretilir; kırpılmış serinin
  ilk ayı 0 alır (Öğrenilenler #102, M10-1 trendiyle aynı kural).
- **PDF raporu (M14-4):** dönem verilmezse son bir ay alınır, aralık en fazla **366 gün**
  (`400 reporting.invalid_range`). Sıra: aralık → premium kapısı (**402 `reporting.premium_required`**) →
  gizlilik kapısı (**403 `reporting.report_not_shared`**) → üretim → depo → kayıt; reddedilen istek depoya
  hiçbir şey bırakmaz. `Limited` gizlilikte ders bazlı kırılım düşer, toplamlar kalır.
- **İndirme yetkisi kayıttan çözülür:** rota yalnız `reportId` taşır, raporun hangi öğrenciye ait olduğu
  `student_reports` satırından okunur; bilinmeyen rapor **404 `reporting.report_not_found`**.
- PDF içeriği modülün projeksiyonlarından + üç okuma sözleşmesinden toplanır:
  `IStudentDirectory.GetDisplayNameAsync`, `ITeacherDirectory.GetDisplayNameAsync`,
  `IStudentAssignmentDigestDirectory.GetCompletionAsync` (ödev tamamlama oranı).

### Eksik / Önerilen ⚠️

```
GET  /api/reporting/teachers/{teacherUserId}/students-summary         → aktif / pasif öğrenci dağılımı
```

> Yetki: öğretmen yalnızca **kendi** gelir/boş zaman raporlarını; öğrenci kendi analizini; bağlı öğretmen ve
> **onaylı** veli öğrencinin analizini görür ([`m09_parents.md`](m09_parents.md)). Varsayılan reddet guard'ı (K3)
> tüm uçlarda uygulanır.

---

## 4. İş Kuralları

1. **Türetilmiş veri:** Reporting kaynak doğruluğu (source of truth) tutmaz; tüm değerler diğer modüllerin event'lerinden **türetilir**. Çelişkide kaynak modül kazanır.
2. **Eventual consistency:** projeksiyonlar event geldikçe güncellenir; raporlarda "son hesaplama zamanı" (`LastRecalculatedOnUtc`) gösterilmeli.
3. **Modül izolasyonu (O5):** başka modülün tablosuna doğrudan SQL/EF erişimi **yasak**; yalnızca integration event veya tanımlı read-contract.
4. **İdempotent projeksiyon:** aynı event tekrar gelirse (Outbox at-least-once) sayaçlar bozulmamalı — event id ile tekilleştirme (inbox) önerilir.
5. **Premium kısıtı (Faz 5):** gelir analizi, boş zaman analizi, PDF rapor, öğrenci performans analizi **premium** özelliktir; üyelik kontrolü ([`m17_membership.md`](m17_membership.md)) ile kapı tutulur.
6. **Gizlilik (🟢 kodda — `StudentReportPrivacyGate`):** öğrenci çalışma/performans raporunun veliye/öğretmene
   açılması Settings'teki `ShareStudyDataWithParent` / `ShareStudyDataWithTeacher` bayraklarına ve
   `PrivacyLevel`'a saygı gösterir ([`m15_settings.md`](m15_settings.md)). Yetki "bakabilir mi?" (403), gizlilik
   "ne kadarını görür?" sorusudur — kapalıyken **403 değil boş sonuç** döner (M10-2 ile aynı ayrım):

   | İzleyici / ayar | study-analysis | performance |
   |---|---|---|
   | Öğrencinin kendisi, Admin | tam | tam |
   | Paylaşım kapalı ya da `Hidden` | boş | boş |
   | `Limited` | yalnız `studyMinutes` (seans/test sayısı 0) | boş — net/hâkimiyet zaten gizlenen detaydır |
   | `Standard` | tam | tam |

7. **Dönem kovaları Türkiye yerel günüdür (P16 T5 / N-50, K-19 — 2026-11-15):** olay anı önce `ILocalCalendar` ile
   yerel tarihe çevrilir, kova o tarihten alınır — `ReportingPeriods.MonthOf/WeekOf(utc, calendar)` ya da doğrudan yerel
   tarih için `MonthOf/WeekOf(DateOnly)`; takvimsiz UTC aşırı yüklemesi **yok** (derlenmez). Pazartesi 01:30 TR seansı bu
   haftaya, 1 Eylül 00:30 TR dersi Eylül'e düşer (eskiden UTC: geçen hafta / Ağustos). Study'nin haftalık özetiyle aynı
   kural (düz yerel tarih; 04:00 seri günü yalnız Study'nin seri sayacına aittir). Hafta sınırı **ISO-8601**: 1 Ocak 2027
   Cuma, ISO'da 2026'nın 53. haftasıdır; M10-1 anlık görüntüleriyle aynı hafta tanımı (Pazartesi başlangıç).
   **Göç:** kova kuralı değişti; canlı veri yok. Canlı veri varken bu değişiklik dağıtılırsa üç analitik projeksiyon
   (`teacher_monthly`, `student_study`, `student_performance`) yeniden kurulmalıdır — `backend.md` §7.1.
   - **Boş zaman (M14-5):** öğretmenin haftalık uygunluk şablonu ("Pzt 18:00–21:00") **yerel** saattir; yerel günlere
     yayılır ve her parça UTC'ye çevrilir (18:00 TR = 15:00Z). Eskiden UTC günlerine yayılıyordu (sonuç 3 saat kayık).
     Öğretmen saat dilimi P24'te — şimdilik platform takvimi.
   - **Varsayılan dönemler:** `monthly` yıl/ay verilmezse **yerel** bugünün ayı; `free-slots` başlangıcı verilen anın
     (ya da şimdinin) yerel gün başı; PDF raporunun varsayılan dönemi yerel bugünden geriye bir ay, ödev penceresi bu
     yerel günlerin UTC karşılığı `[ilk gün 00:00 TR, son günün ertesi 00:00 TR)`.
8. **PDF üretimi:** sunucu tarafında (ör. headless render) üretilip indirilebilir link/dosya döner; büyük raporlar için arka plan işi.

---

## 5. Olay Akışı

**Mevcut (P07, çalışıyor):**

```
Scheduling:   LessonScheduledDomainEvent / LessonScheduleRescheduledDomainEvent ─▶ ders satırı upsert
Scheduling:   LessonScheduleCancelledDomainEvent                                ─▶ ders satırı sil
Assignments:  AssignmentCreatedDomainEvent                                      ─▶ açık ödev ekle
Assignments:  AssignmentCompletedDomainEvent / AssignmentApprovedDomainEvent    ─▶ açık ödev çıkar
Payments:     PaymentBecameOverdueDomainEvent                                   ─▶ gecikmiş ödeme ekle (tutar+kur)
Payments:     PaymentRecordUpdatedDomainEvent (CurrentStatus != Overdue)        ─▶ gecikmiş ödeme çıkar
Students:     TeacherStudentLinkAcceptedDomainEvent                             ─▶ aktif öğrenci ekle
```

> Öğretmensiz ders planı (öğrencinin bireysel planı) projeksiyon satırı **oluşturmaz**.

**Önerilen (sonraki projeksiyonlar):**

```
Payments:        PaymentRecordUpdated / PaymentCollected ──▶ TeacherMonthlyReportProjection güncelle
Study (M08):     StudySessionEnded                        ──▶ StudentStudyProjection güncelle
ProgressTracking:ProgressRecorded                         ──▶ StudentPerformanceProjection güncelle

   ↑ Tüm projeksiyon güncellemeleri Outbox/IntegrationEvent üzerinden (K1 açık olmalı)

Sorgu zamanı:
  GET /reporting/... ──▶ hazır projeksiyondan oku (hızlı) ──▶ premium ise üyelik kontrolü (m17)
                                                          └─▶ PDF talebinde render + indirme
```

> ⚠️ Outbox kapalıyken (K1) hiçbir projeksiyon beslenmez — modül boş rapor döner.

---

## 6. Mobil Ekranlar (Flutter)

- **Öğretmen — Aylık özet panosu:** ders sayısı, tahsil/beklenen gelir, aktif/pasif öğrenci kartları; ay seçici. Marka rengi `0xFF082B4F` ile grafik/başlık vurgusu.
- **Öğretmen — Boş zaman analizi:** haftalık doluluk ısı haritası / en uygun saat önerileri.
- **Öğretmen — PDF öğrenci raporu:** seç → üret → paylaş/indir (premium rozetli).
- **Öğrenci — Çalışma analizi:** haftalık/aylık süre grafiği, konu dağılımı, hedef vs. gerçekleşen.
- **Öğrenci — Performans:** konu bazlı trend çizgileri.
- **Veli — Çocuk özeti:** grafik + rapor görünümü, premium kapısı ([`../roles/veli.md`](../roles/veli.md)).
- Free vs. premium: premium ekranlar üyelik durumu ile kilitli/önizlemeli gösterilir ([`m17_membership.md`](m17_membership.md)).

### 6.1 Uygulanan (2026-09-04, P08 Task 5) — `mobile/lib/features/reporting/`

| Ekran / bileşen | Rota | Kaynak uç |
|-----------------|------|-----------|
| `TeacherReportsPage` — aylık ders/tahsilat + boş zamanlar | `/teacher/reports` (`/more` → "Raporlar") | `monthly` (M14-1) + `free-slots` (M14-5) |
| `StudentAnalysisPage` — haftalık çalışma çubuk + ders bazlı net + PDF | `/student/reports?studentId=` | `study-analysis` (M14-2) + `performance` (M14-3) + `report`/`file` (M14-4) |
| Veli çocuk detayı — `_ChildProgressSection` | `/parent/child-detail` | `study-analysis` + `performance` |

- **Premium kilidi istemcide:** `402` **ve** `403` → `ReportingStatus.premiumRequired`; ekran hata değil kilit
  kartı gösterir, grafikler görünmeye devam eder (analiz durumu ile rapor durumu ayrı taşınır).
- **PDF:** `POST .../report` → `GET /reports/{id}/file` (bayt) → geçici dizin → `open_filex` ile açılır
  (`DeviceReportFileOpener`; `ReportFileOpener` arayüzü sayesinde cubit dosya sistemine dokunmadan test edilir).
- **Boş zaman analizi mobilde her zaman bugünden itibaren 7 gün** sorgulanır — geçmiş bir ayın boş saatleri
  planlanamayacağı için anlamsızdır.
- Ekran dokümanı: [`../pages/teacher_reports.md`](../pages/teacher_reports.md).

---

## 7. Kabul Kriterleri

- [x] Modüller arası okuma kararı (O5) verildi; projeksiyonlar integration event'lerle besleniyor. **(P07)**
- [x] Öğretmen aylık özeti doğru ders sayısı + tahsil/beklenen gelir + aktif öğrenci gösteriyor. **(P08 Task 3)**
- [x] Öğrenci çalışma analizi Study (M08) verisinden haftalık doğru toplamları üretiyor. **(P08 Task 3)**
- [x] Öğrenci performans raporu ders bazlı aylık değişimi ProgressTracking + Study'den gösteriyor. **(P08 Task 3)**
- [x] Veli yalnızca **onaylı** bağlı çocuklarının analizini görebiliyor; gizlilik bayrakları uygulanıyor (M15). **(P08 Task 3)**
- [x] Boş zaman analizi uygunluk şablonundan ders + izin bloklarını düşerek üretiliyor. **(P08 Task 3 / M14-5)**
- [x] PDF öğrenci raporu üretilip indirilebiliyor. **(P08 Task 4 / M14-4)**
- [x] Premium özellikler üyelik kontrolünden geçiyor; free kullanıcıda **402** ile kilitli. **(P09 Task 2'de gerçek `IEntitlementDirectory`'ye devredildi — istek sahibinin hakkı belirler)**
- [x] Aynı event tekrar gelse de projeksiyon sayaçları bozulmuyor (idempotent). **(P07 — inbox guard + küme üyeliği)**
- [x] Sahiplik/rol guard'ı yetkisiz erişimi reddediyor (K3). **(P07 dashboard + P08 Task 3 dört analitik uç)**

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul:** Faz 5 modülü. Kaynak veri (Study M08 dolu, gerçek Payments) ve analiz ihtiyacı oluşmadan başlanmamalı.

1. ~~**Modüller arası read mekanizması** kararı (projeksiyon vs. read-contract — O5).~~ ✅ **(P07)**
2. ~~**`reporting` şemasına projeksiyon tabloları** + migration.~~ ✅ **(P07 — öğretmen dashboard'u; diğer projeksiyonlar hâlâ yok)**
3. ~~**Öğretmen aylık özet projeksiyonu** (Payments + Scheduling/LessonSession event'lerinden).~~ ✅ **(P08 Task 3)**
4. ~~**Öğrenci çalışma/performans analizi** (Study M08 + ProgressTracking M10 verisinden).~~ ✅ **(P08 Task 3)**
5. ~~**Boş zaman analizi** (Scheduling + Teachers uygunluk verisinden).~~ ✅ **(P08 Task 3 — `ITeacherAvailabilityDirectory` + `ILessonScheduleDirectory.ListTeacherBusyIntervalsAsync`)**
6. ~~**PDF rapor üretimi** (premium).~~ ✅ **(P08 Task 4 — QuestPDF + `IFileStorage` + yetkili indirme)**
7. **Veli özet/grafik raporu** (m09) — bugün öğrenci uçları üzerinden karşılanıyor; ayrı bir veli ucu isteğe bağlı.
8. ~~**Premium kısıtlama** entegrasyonu (m17).~~ ✅ **(P09 Task 2 — `IEntitlementDirectory`; istek sahibinin `PdfReports` hakkı)**
9. ~~**İdempotent event tüketimi** (inbox/event-id) + CQRS query'leri~~ ✅; ~~mobil ekranlar~~ ✅ (P08 Task 5).
10. **Projeksiyon yeniden inşası** (`IProjectionRebuilder`) — geçmiş dönemler ancak bununla dolar (**P12**).
    P08 Task 6'da **yeniden kurulabilirlik özelliği** integration testiyle doğrulandı (`RealProjectionRebuildTests`);
    kalan iş yalnız geçmiş olayları besleyen host seviyesi orkestrasyon.

> **P08 kapanışı (2026-09-04):** M14-1…M14-5 + mobil ekranlar bitti, modül 🟡 → 🟢. Kalan 7/8/10 maddeleri
> sırasıyla *isteğe bağlı*, **P09** ve **P12** kapsamındadır.

---

## 9. İlişkili Dokümanlar

- Gelir verisi kaynağı → [`m07_payments.md`](m07_payments.md)
- Ders/oturum verisi → [`m04_scheduling.md`](m04_scheduling.md)
- Çalışma verisi kaynağı → [`m08_study.md`](m08_study.md)
- Performans/ilerleme verisi → [`m10_progress_tracking.md`](m10_progress_tracking.md)
- Ödev verisi → [`m06_assignments.md`](m06_assignments.md)
- Veli grafik/özet bağlamı → [`m09_parents.md`](m09_parents.md)
- Gizlilik (veri paylaşımı bayrakları) → [`m15_settings.md`](m15_settings.md)
- Premium kapısı → [`m17_membership.md`](m17_membership.md)
- Modüller arası okuma sorunu (O5), Outbox (K1), yetki (K3) → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli / şema → [`veri_modeli.md`](veri_modeli.md)
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/admin.md`](../roles/admin.md)

---

*Raporlama ve Analiz Modülü (M14) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-11-15 (P16 Task 5 / N-11, N-43, N-50: §4 kural 7 — dönem kovaları Türkiye yerel günü (`ReportingPeriods` takvimli/tarih tabanlı, UTC aşırı yüklemesi kaldırıldı), boş zaman şablonu yerel güne kurulur → UTC, `monthly`/`free-slots`/PDF varsayılan dönemleri yerel; BFF `dashboard-summary` bugünü yerel + `overduePaymentTotals` para birimi bazında, eski tekil alanlar ilk satır) · 2026-09-04 (P09 Task 2 / K-09: PDF premium kapısı `IMembershipDirectory` + geçici `ReportEntitlement.EffectiveTier` ikilisinden `IEntitlementDirectory`'ye devredildi — kural artık "istek sahibinin `PdfReports` hakkı", Admin muaf. Önceki not — P08 kapanış / Task 6: durum 🟡 → 🟢; projeksiyon yeniden-kurulabilirliği `RealProjectionRebuildTests` ile gerçek Postgres'te kanıtlandı (boşalt → aynı akışı oynat → aynı sayaçlar → üçüncü teslimde değişmiyor); kalan borçlar P09/P12 olarak etiketlendi. Önceki not — P08 Task 5: mobil rapor ekranları — `TeacherReportsPage` (`/teacher/reports`), `StudentAnalysisPage` (`/student/reports`), veli çocuk detayına gelişim grafikleri; `402/403` → `premiumRequired` kilidi; PDF `open_filex` ile cihazda açılıyor. Önceki not — P08 Task 4 / M14-4: QuestPDF ile PDF öğrenci raporu — `StudentReport` aggregate + `student_reports` tablosu (`AddStudentReports`), `POST students/{id}/report` (premium kapılı, 402) + `GET reports/{id}/file` (yetkili indirme), saf `StudentReportComposer`, geçici `ReportEntitlement` kapısı ve üç yeni okuma sözleşmesi kullanımı (`IStudentDirectory.GetDisplayNameAsync`, `ITeacherDirectory`, `IStudentAssignmentDigestDirectory`). Önceki not — P08 Task 3 / M14-1+M14-2+M14-3+M14-5: üç analitik projeksiyon (`TeacherMonthlyProjection`, `StudentStudyProjection`, `StudentPerformanceProjection`) + 8 besleme handler'ı + 6 tablo (`AddReportingProjections`) + 4 uç (`monthly`, `free-slots`, `study-analysis`, `performance`) + `StudentReportPrivacyGate` gizlilik kapısı + saf `FreeSlotCalculator`; iki yeni modüller-arası sözleşme: `ITeacherAvailabilityDirectory` (Teachers) ve `ILessonScheduleDirectory.ListTeacherBusyIntervalsAsync` (Scheduling). `delta` bilinçli olarak **saklanmıyor**. Önceki not — P07: öğretmen dashboard projeksiyonu — ilk gerçek read-model, 🔴 → 🟡)*
