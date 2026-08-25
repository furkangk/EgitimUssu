---
title: "M14 — Raporlama ve Analiz (Reporting)"
summary: "Diğer modüllerin verisini birleştiren read-model raporlama modülü iskelet seviyesinde; hiçbir DbSet yok, yalnızca placeholder endpoint var"
tags: [modul, reporting, raporlama, read-model, iskelet, faz-5]
status: "🔴"
authority: code
code_refs:
  - src/Modules/Reporting/**
updated: 2026-08-19
---

# 📊 Raporlama ve Analiz Modülü (M14) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M14 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Reporting`) + Flutter mobil
> **PRD:** M14 · **Faz:** 5 (Premium & Analitik) · **Durum:** 🔴 İskelet — boş DbContext, yalnızca `GET /api/reporting/status` placeholder
> **Mimari:** CQRS + Outbox + Integration Event; önerilen **read-model / projeksiyon** yaklaşımı, PostgreSQL (`reporting` şeması), Redis (rapor cache)
> **Marka rengi (mobil):** `0xFF082B4F`

> Bu modül **kendi iş verisi üretmez**; Payments, Scheduling, Study, Assignments, ProgressTracking gibi modüllerin verisini **rapor ve analiz** olarak birleştiren bir **okuma (read-model)** modülüdür.
>
> ⚠️ **Mimari kısıt:** [`mimari_inceleme.md`](mimari_inceleme.md) **O5** — modüller arası senkron okuma mekanizması (read-model/contract) **henüz yok**. Anlamlı rapor için bu önce çözülmelidir.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### 🔴 Var olan (iskelet)

| Bileşen | Konum | Durum |
|---------|-------|-------|
| Module tanımı | `src/Modules/Reporting/API/ReportingModule.cs` | Yalnızca `GET /api/reporting/status` (`state = "placeholder"`) |
| DbContext | `src/Modules/Reporting/Infrastructure/ReportingDbContext.cs` | **Hiçbir `DbSet` yok** — tümüyle boş, şema `reporting` |
| DI | `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` | `AddReportingModule` (boş kayıt) |

**Doğrulanmış gerçekler:**
- `ReportingDbContext` yalnızca `Schema => "reporting"` ve `ModuleName => "Reporting"` tanımlar; **`OnModelCreating` override'ı, entity konfigürasyonu, hiçbir tablo yok**.
- Domain modeli, CQRS feature (query/handler), migration, integration event handler, mobil ekran **yok**.
- API yüzeyi tek placeholder endpoint'ten ibaret.

### ⚠️ Önkoşullar
- Anlamlı rapor için kaynak modüllerin **dolu** olması gerekir: özellikle Study (M08, [`m08_study.md`](m08_study.md)) ve gerçek veriyle çalışan Payments (M07, [`m07_payments.md`](m07_payments.md)), Scheduling (M04, [`m04_scheduling.md`](m04_scheduling.md)), ProgressTracking (M10, [`m10_progress_tracking.md`](m10_progress_tracking.md)).
- Modüller arası okuma kararı (O5) verilmeden projeksiyon tabloları beslenemez.

---

## 2. Domain / Projeksiyon Modeli

### 🟢 Mevcut
**Yok.** `ReportingDbContext` boş; hiçbir aggregate veya projeksiyon tablosu tanımlı değil.

### ⚠️ Önerilen — Read-model projeksiyonları

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

```
Payments:        PaymentRecordUpdated / PaymentCollected ──▶ TeacherMonthlyReportProjection güncelle
Scheduling:      LessonScheduled / LessonSessionCompleted ─▶ LessonCount / ActiveStudent / Availability güncelle
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

- [ ] Modüller arası okuma kararı (O5) verildi; projeksiyonlar integration event'lerle besleniyor.
- [ ] Öğretmen aylık özeti doğru ders sayısı + tahsil/beklenen gelir + aktif öğrenci gösteriyor.
- [ ] Öğrenci çalışma analizi Study (M08) verisinden haftalık/aylık doğru toplamları üretiyor.
- [ ] Öğrenci performans raporu konu bazlı değişimi ProgressTracking'ten gösteriyor.
- [ ] Veli yalnızca bağlı çocuklarının özetini görebiliyor; gizlilik bayrakları uygulanıyor (M15).
- [ ] PDF öğrenci raporu üretilip indirilebiliyor.
- [ ] Premium özellikler üyelik kontrolünden geçiyor (M17); free kullanıcıda kilitli.
- [ ] Aynı event tekrar gelse de projeksiyon sayaçları bozulmuyor (idempotent).
- [ ] Sahiplik/rol guard'ı yetkisiz erişimi reddediyor (K3).

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul:** Faz 5 modülü. Kaynak veri (Study M08 dolu, gerçek Payments) ve analiz ihtiyacı oluşmadan başlanmamalı.

1. **Modüller arası read mekanizması** kararı (projeksiyon vs. read-contract — O5).
2. **`reporting` şemasına projeksiyon tabloları** + migration.
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

*Raporlama ve Analiz Modülü (M14) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-08-19*
