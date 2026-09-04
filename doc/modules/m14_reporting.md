---
title: "M14 — Raporlama ve Analiz (Reporting)"
summary: "Read-model raporlama modülü: öğretmen dashboard projeksiyonu (event-beslemeli, idempotent) canlı; kalan analiz/rapor yüzeyi hâlâ eksik"
tags: [modul, reporting, raporlama, read-model, projeksiyon, faz-5]
status: "🟡"
authority: code
code_refs:
  - src/Modules/Reporting/**
updated: 2026-09-04
---

# 📊 Raporlama ve Analiz Modülü (M14) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M14 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Reporting`) + Flutter mobil
> **PRD:** M14 · **Faz:** 5 (Premium & Analitik) · **Durum:** 🟡 Kısmi — ilk gerçek read-model (öğretmen dashboard projeksiyonu) çalışıyor; analiz/PDF/premium rapor yüzeyi eksik
> **Mimari:** CQRS + Outbox + Integration Event; önerilen **read-model / projeksiyon** yaklaşımı, PostgreSQL (`reporting` şeması), Redis (rapor cache)
> **Marka rengi (mobil):** `0xFF082B4F`

> Bu modül **kendi iş verisi üretmez**; Payments, Scheduling, Study, Assignments, ProgressTracking gibi modüllerin verisini **rapor ve analiz** olarak birleştiren bir **okuma (read-model)** modülüdür.
>
> ✅ **Mimari kısıt kapandı (P07):** [`mimari_inceleme.md`](mimari_inceleme.md) **O5** — kanonik projeksiyon deseni kuruldu
> (`ProjectionEntity` · `ProjectionHandler<TEvent>` · `IProjectionRebuilder`, bkz. [`../architecture/backend.md`](../architecture/backend.md) §7.1).
> Bu modül desenin **ilk gerçek tüketicisi**dir.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### 🟡 Var olan (P07 Task 2 sonrası)

| Bileşen | Konum | Durum |
|---------|-------|-------|
| Module tanımı | `src/Modules/Reporting/API/ReportingModule.cs` | `GET /api/reporting/status` (placeholder) + `GET /api/reporting/teachers/{id}/dashboard` (gerçek) |
| Domain | `src/Modules/Reporting/Domain/TeacherDashboardProjection.cs` | `TeacherDashboardProjection` + `TeacherDashboardItem` + `TeacherDashboardItemKind` |
| DbContext | `src/Modules/Reporting/Infrastructure/ReportingDbContext.cs` | `teacher_dashboard_projections` + `teacher_dashboard_items` (`reporting` şeması) |
| Projeksiyon handler'ları | `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionHandlers.cs` | 7 handler (Scheduling/Assignments/Payments/Students event'leri) |
| Sorgu | `src/Modules/Reporting/Application/ReportingFeatures.cs` | `GetTeacherDashboardQuery` + handler + `ReportingAuthorizer` |
| Reader | `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionReader.cs` | `AsNoTracking` tek satır okuma |
| DI | `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` | `AddModuleDbContext<ReportingDbContext>` + handler/sorgu kayıtları |
| Migration | `.../Migrations/*_AddTeacherDashboardProjection.cs` | `reporting` şeması + outbox/inbox/module_states + 2 projeksiyon tablosu |

**Doğrulanmış gerçekler:**
- Projeksiyon **yalnız integration event'lerden** beslenir; Reporting hiçbir modülün DbContext'ini okumaz (`Modules_Should_Not_Reference_Other_Modules` yeşil).
- Idempotency ortak `inbox_messages` guard'ından gelir; ayrıca sayaçlar **küme üyeliğinden türetilir** (aşağıya bkz.), bu yüzden aynı olay iki kez gelse de bozulmaz.
- Canlı doğrulandı: ders planlandı → `todayLessonCount = 1`, ders iptal edildi → `todayLessonCount = 0`.

### ⚠️ Hâlâ eksik
- Aylık gelir/ders özeti, boş zaman analizi, öğrenci çalışma/performans analizi, veli özeti, PDF rapor, premium kapısı.
- `IProjectionRebuilder` implementasyonu ve `POST /api/admin/projections/{name}/rebuild` (P12).

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

`dashboard` yanıtı: `teacherUserId`, `todayLessonCount`, `pendingAssignmentCount`, `overduePaymentCount`,
`overdueAmountTotal`, `overdueCurrency`, `activeStudentCount`, `updatedOnUtc`.
Projeksiyon henüz beslenmediyse **404 `reporting.dashboard_not_ready`** döner — sıfır göstermek yalancı-yeşil olurdu.

> ℹ️ `GET /api/teachers/profiles/{id}/dashboard-summary` (BFF) **projeksiyona taşınmadı**: mobil panel ders/ödev/ödeme
> **listelerini** basıyor, bugünkü integration event'ler ise ders konusu/saati etiketi, ödev başlığı/son teslim tarihi ve
> ödeme açıklaması taşımıyor. Liste projeksiyondan üretilemediği için BFF üç modülü sorgulamayı sürdürüyor; taşımak
> üç modülün event sözleşmesini zenginleştirmeyi gerektirir (ayrı iş).

### Eksik / Önerilen ⚠️

```
GET  /api/reporting/teachers/{teacherUserId}/monthly?year=&month=    → aylık ders + gelir + aktif öğrenci özeti
GET  /api/reporting/teachers/{teacherUserId}/students-summary         → aktif / pasif öğrenci dağılımı
GET  /api/reporting/teachers/{teacherUserId}/availability-analysis    → boş zaman / doluluk analizi
GET  /api/reporting/students/{studentId}/study-analysis?range=        → haftalık/aylık çalışma süresi
GET  /api/reporting/students/{studentId}/performance                  → konu bazlı performans değişimi
GET  /api/reporting/parents/{parentUserId}/children-overview          → veli grafiği/özeti (m09)
POST /api/reporting/students/{studentId}/pdf                          → PDF öğrenci raporu (premium)
```

> Yetki: öğretmen yalnızca **kendi** öğrenci/gelir raporlarını; öğrenci kendi analizini; veli yalnızca **bağlı** çocuklarının özetini görür ([`m09_parents.md`](m09_parents.md)). Varsayılan reddet guard'ı (K3) tüm endpoint'lerde uygulanmalı.

---

## 4. İş Kuralları

1. **Türetilmiş veri:** Reporting kaynak doğruluğu (source of truth) tutmaz; tüm değerler diğer modüllerin event'lerinden **türetilir**. Çelişkide kaynak modül kazanır.
2. **Eventual consistency:** projeksiyonlar event geldikçe güncellenir; raporlarda "son hesaplama zamanı" (`LastRecalculatedOnUtc`) gösterilmeli.
3. **Modül izolasyonu (O5):** başka modülün tablosuna doğrudan SQL/EF erişimi **yasak**; yalnızca integration event veya tanımlı read-contract.
4. **İdempotent projeksiyon:** aynı event tekrar gelirse (Outbox at-least-once) sayaçlar bozulmamalı — event id ile tekilleştirme (inbox) önerilir.
5. **Premium kısıtı (Faz 5):** gelir analizi, boş zaman analizi, PDF rapor, öğrenci performans analizi **premium** özelliktir; üyelik kontrolü ([`m17_membership.md`](m17_membership.md)) ile kapı tutulur.
6. **Gizlilik:** öğrenci çalışma/performans raporunun veliye/öğretmene açılması, Settings'teki `ShareStudyDataWithParent` / `ShareStudyDataWithTeacher` bayraklarına saygı gösterir ([`m15_settings.md`](m15_settings.md)).
7. **PDF üretimi:** sunucu tarafında (ör. headless render) üretilip indirilebilir link/dosya döner; büyük raporlar için arka plan işi.

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

---

## 7. Kabul Kriterleri

- [x] Modüller arası okuma kararı (O5) verildi; projeksiyonlar integration event'lerle besleniyor. **(P07)**
- [ ] Öğretmen aylık özeti doğru ders sayısı + tahsil/beklenen gelir + aktif öğrenci gösteriyor.
- [ ] Öğrenci çalışma analizi Study (M08) verisinden haftalık/aylık doğru toplamları üretiyor.
- [ ] Öğrenci performans raporu konu bazlı değişimi ProgressTracking'ten gösteriyor.
- [ ] Veli yalnızca bağlı çocuklarının özetini görebiliyor; gizlilik bayrakları uygulanıyor (M15).
- [ ] PDF öğrenci raporu üretilip indirilebiliyor.
- [ ] Premium özellikler üyelik kontrolünden geçiyor (M17); free kullanıcıda kilitli.
- [x] Aynı event tekrar gelse de projeksiyon sayaçları bozulmuyor (idempotent). **(P07 — inbox guard + küme üyeliği)**
- [~] Sahiplik/rol guard'ı yetkisiz erişimi reddediyor (K3). **(P07 — dashboard ucunda var; kalan uçlar yok)**

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul:** Faz 5 modülü. Kaynak veri (Study M08 dolu, gerçek Payments) ve analiz ihtiyacı oluşmadan başlanmamalı.

1. ~~**Modüller arası read mekanizması** kararı (projeksiyon vs. read-contract — O5).~~ ✅ **(P07)**
2. ~~**`reporting` şemasına projeksiyon tabloları** + migration.~~ ✅ **(P07 — öğretmen dashboard'u; diğer projeksiyonlar hâlâ yok)**
3. **Öğretmen aylık özet projeksiyonu** (Payments + Scheduling/LessonSession event'lerinden).
4. **Öğrenci çalışma/performans analizi** (Study M08 + ProgressTracking M10 verisinden).
5. **Boş zaman analizi** (Scheduling + Teachers uygunluk verisinden).
6. **PDF rapor üretimi** (premium).
7. **Veli özet/grafik raporu** (m09) + gizlilik bayrakları (m15).
8. **Premium kısıtlama** entegrasyonu (m17).
9. **İdempotent event tüketimi** (inbox/event-id) + CQRS query'leri + mobil ekranlar.

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

*Raporlama ve Analiz Modülü (M14) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-09-04 (P07: öğretmen dashboard projeksiyonu — ilk gerçek read-model, 🔴 → 🟡)*
