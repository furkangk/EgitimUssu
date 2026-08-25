---
title: "M10 — Gelişim Takibi (ProgressTracking)"
summary: "Konu bazlı hâkimiyet skoru + hedef + mobil 'Gelişimim' ekranı çalışır çekirdek; ProgressSnapshot zaman serisi ve öğretmen/veli görünümü henüz yok"
tags: [modul, progress-tracking, gelisim, faz-3]
status: "🟡"
authority: code
code_refs:
  - src/Modules/ProgressTracking/**
updated: 2026-08-26
---

# 📈 M10 — Gelişim Takibi (ProgressTracking) Modülü — Detaylı Tasarım Dokümanı

> **Kod Modülü:** `src/Modules/ProgressTracking` · **Route Prefix:** `/api/progress-tracking` · **Şema:** `progress_tracking`
> **PRD Modülü:** M10 Gelişim Takibi · **Faz:** 3
> **Durum:** 🟡 **Çalışır çekirdek** (TopicMastery + TopicGoal domain, M08 çalışma/test olay tüketicileri,
> hâkimiyet skoru motoru, mastery/weak-spots/strengths/overview + konu hedefi API'leri, mobil "Gelişimim" ekranı).
> ProgressSnapshot (zaman serisi) + öğretmen/veli görünümü + M14/M09 entegrasyonu henüz yok.
> **Platform:** EğitimÜssü (EgitimUssu) — .NET 9 modüler monolit · PostgreSQL · Redis · Flutter
>
> **Amaç:** Öğrencinin **konu bazlı gelişimini** ölçmek: hangi konularda eksik/güçlü, konu hedeflerine
> ne kadar yaklaştı, zaman içinde performansı nasıl değişti. Modül kendi ölçüm verisini **üretmez**;
> M08 (bireysel çalışma + test) ve M05 (ders oturumu) verilerinden **beslenen** bir analiz/zaman-serisi
> modülüdür. Çıktısı hem **öğretmenin** hem **velinin** gelişim takibini ve M14 raporlarını besler.

> ⚠️ Bu dokümandaki **ProgressSnapshot**, öğretmen/veli görünümü ve M14/M09 entegrasyonu bölümleri henüz **önerilen tasarımdır**.
> TopicMastery + TopicGoal + besleme + API + mobil ekran **kodda mevcuttur** (2026-07-09). Bölüm 1 koddan doğrulanmıştır.

---

## 1. Mevcut Durum (Koddan Doğrulanmış — 2026-07-09)

### ✅ Uygulanan (çalışır çekirdek)
- **Domain** (`src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs`): `TopicMastery`
  (`RegisterStudy`/`RegisterTest` → skor/seviye/trend/eksik-güçlü yeniden hesaplama), `TopicGoal`
  (`MarkAchieved`/`Cancel`). Enum'lar: `MasteryLevel`, `ProgressTrend`,
  `MasterySource`, `TopicGoalStatus`, `TopicGoalSetterRole`. Olaylar: `TopicMasteryChanged`, `TopicGoalAchieved`.
- **Besleme (idempotent consumer'lar)** (`Infrastructure/StudyProgressIntegrationEventHandlers.cs`):
  M08 `StudySessionCompletedDomainEvent` → süre; `TestResultRecordedDomainEvent` → net oranı. Tekrar-koruma artık
  modüle özel bir tablo yerine paylaşılan `IdempotentIntegrationEventHandler` tabanı + `inbox_messages` tablosu
  (composite PK `(EventId, Handler)`) ile sağlanır; eski `ProcessedEvent` entity'si ve `processed_events` tablosu
  **kaldırıldı** (2026-08-26, bkz. `mimari_inceleme.md` Y4).
  (M08 `TestResultRecordedDomainEvent` bu iş kapsamında `Topic`/`TotalQuestions`/D-Y-B alanlarıyla zenginleştirildi.)
- **Skor motoru** (`TopicMastery.Recalculate`): çalışma bileşeni (maks 30, 3 saatte doyar) + test net oranı (maks 70);
  seviye bantları 0–20 Weak / 20–45 Developing / 45–75 Proficient / 75–100 Mastered; veri yoksa NotStarted.
  Trend son iki net oranından; `IsWeakSpot`/`IsStrength` §4.2'ye göre.
- **API** (`API/ProgressTrackingModule.cs`): mastery / weak-spots / strengths / overview + topic-goals (liste/oluştur/iptal). Sahiplik `IStudentDirectory` ile.
- **Persistence**: `ProgressTrackingDbContext` (TopicMastery/TopicGoal + paylaşılan `InboxMessage`) + `progress_tracking` şeması migration'ı.
- **Mobil**: `mobile/lib/features/progress/` — "Gelişimim" ekranı (dağılım + eksik/güçlü + tüm konular).

### 🟢 Hazır besleme kaynakları (henüz tüketilmeyen)
- M05 LessonSessions: **`LessonSessionCompletedDomainEvent`** mevcut — ders tamamlanmasından besleme **ileride** eklenebilir (şu an yalnızca M08 tüketiliyor).

### 🔴 Kalan (önerilen)
- **ProgressSnapshot** (haftalık/aylık zaman serisi + zamanlayıcı) — gelişim grafiği için.
- **Öğretmen/veli görünümü** (bağlı öğrenci / paylaşıma açık çocuk) ve **M14/M09 entegrasyonu**.
- **M05 ders tamamlanması** beslemesi.

---

## 2. Domain Modeli (⚠️ Önerilen)

> `AggregateRoot<Guid>` / `Entity<Guid>` deseni (private ctor, `private set`, enum `1`'den, `Raise(...)`).
> `StudentId`, M03 `StudentProfile.Id`'ye mantıksal referanstır. Tablolar `progress_tracking` şemasında.
> Bu modül **türetilmiş veri** tutar; kaynak olaylar yeniden işlenerek (replay) yeniden hesaplanabilir olmalıdır.

### 2.1 `TopicMastery` (AggregateRoot) — Konu hâkimiyeti (eksik/güçlü)

Bir öğrencinin belirli bir **ders+konu** için güncel hâkimiyet seviyesi. Çalışma süresi ve test netlerinden türetilir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | M03 `StudentProfile.Id` |
| `Subject` | `string` | ✓ | Ders |
| `Topic` | `string` | ✓ | Konu (ileride M15 müfredat sözlüğüne bağlı) |
| `MasteryLevel` | `MasteryLevel` | ✓ | `NotStarted`/`Weak`/`Developing`/`Proficient`/`Mastered` |
| `MasteryScore` | `decimal` | ✓ | 0–100 normalize skor (seviye bunun bandından türer) |
| `TotalStudyMinutes` | `int` | ✓ | Bu konuya ayrılan toplam net süre (M08'den rollup) |
| `TestAttemptCount` | `int` | ✓ | Bu konuda çözülen test sayısı |
| `AverageNetRatio` | `decimal?` | — | Ortalama net oranı (net / soru) |
| `Trend` | `ProgressTrend` | ✓ | `Improving`/`Stable`/`Declining` |
| `IsWeakSpot` | `bool` | ✓ | Eksik konu işareti (öğretmen/veli için vurgulanır) |
| `IsStrength` | `bool` | ✓ | Güçlü konu işareti |
| `LastEvaluatedOnUtc` | `DateTime` | ✓ | Son hesaplama anı |
| `Source` | `MasterySource` | ✓ | `StudyOnly`/`LessonOnly`/`Combined` (verinin geldiği kaynak) |

```csharp
public enum MasteryLevel { NotStarted = 1, Weak = 2, Developing = 3, Proficient = 4, Mastered = 5 }
public enum ProgressTrend { Improving = 1, Stable = 2, Declining = 3 }
public enum MasterySource { StudyOnly = 1, LessonOnly = 2, Combined = 3 }
```

**Davranış:** `Recalculate(...)` → yeni çalışma/test/ders verisiyle `MasteryScore`, `MasteryLevel`, `Trend`,
`IsWeakSpot`/`IsStrength` yeniden hesaplanır. Seviye değişiminde `TopicMasteryChangedDomainEvent` yükseltir.

### 2.2 `TopicGoal` (AggregateRoot) — Konu gelişim hedefi

Bir konu için belirlenen hedef seviye/net. Öğrenci kendisi koyabilir veya öğretmen önerebilir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | |
| `Subject` | `string` | ✓ | |
| `Topic` | `string` | ✓ | |
| `TargetMasteryLevel` | `MasteryLevel` | ✓ | Ulaşılmak istenen seviye |
| `TargetNetRatio` | `decimal?` | — | Hedef net oranı |
| `SetByUserId` | `Guid` | ✓ | Hedefi koyan (öğrenci/öğretmen) |
| `SetByRole` | `enum` | ✓ | `Student`/`Teacher` |
| `TargetDate` | `DateOnly?` | — | Hedef tarihi |
| `Status` | `TopicGoalStatus` | ✓ | `Active`/`Achieved`/`Missed`/`Cancelled` |
| `AchievedOnUtc` | `DateTime?` | — | Ulaşıldığında dolar |
| `CreatedOnUtc` | `DateTime` | ✓ | |

```csharp
public enum TopicGoalStatus { Active = 1, Achieved = 2, Missed = 3, Cancelled = 4 }
```

**Davranış:** `MarkAchieved(nowUtc)` (mastery hedefe ulaşınca) → `TopicGoalAchievedDomainEvent` (M11 kutlama).

### 2.3 `ProgressSnapshot` (AggregateRoot) — Zaman serisi performans

Periyodik (haftalık/aylık) veya olay-tetikli performans fotoğrafı. Gelişim grafiğinin ham zaman serisidir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | |
| `Subject` | `string?` | — | Ders (genel snapshot ise null) |
| `Topic` | `string?` | — | Konu (ders/genel ise null) |
| `PeriodType` | `SnapshotPeriod` | ✓ | `Daily`/`Weekly`/`Monthly` |
| `PeriodStartUtc` | `DateTime` | ✓ | Dönem başı |
| `PeriodEndUtc` | `DateTime` | ✓ | Dönem sonu |
| `TotalStudyMinutes` | `int` | ✓ | Dönemdeki net çalışma (M08) |
| `LessonCount` | `int` | ✓ | Dönemdeki tamamlanan ders (M05) |
| `TestCount` | `int` | ✓ | Dönemdeki test sayısı (M08) |
| `AverageNet` | `decimal?` | — | Dönem ortalama net |
| `AverageNetRatio` | `decimal?` | — | Ortalama net oranı |
| `MasteryScoreSnapshot` | `decimal?` | — | Dönem sonu hâkimiyet skoru |
| `CreatedOnUtc` | `DateTime` | ✓ | |

```csharp
public enum SnapshotPeriod { Daily = 1, Weekly = 2, Monthly = 3 }
```

**Davranış:** `ProgressSnapshotCreatedDomainEvent` (M14 Reporting için).

### 2.4 Domain Olayları (özet)

| Olay | Tetikleyen | Tüketen |
|------|-----------|---------|
| `TopicMasteryChangedDomainEvent` | `TopicMastery.Recalculate` (seviye değişti) | M09 Parents, M11 (öğretmen/veli bilgilendirme), M14 |
| `TopicGoalAchievedDomainEvent` | `TopicGoal.MarkAchieved` | M11 Notifications |
| `ProgressSnapshotCreatedDomainEvent` | snapshot oluşturma | M14 Reporting |

---

## 3. API Sözleşmesi — `/api/progress-tracking`

> Auth gerektirir (`status` hariç). Erişim: öğrenci kendi verisi; **öğretmen** bağlı öğrencisi; **veli** onaylı+paylaşıma açık çocuğu
> (gizlilik bayrakları M08/M09 ile, bkz. İş Kuralları 4.5). Sahiplik `IStudentDirectory` ile çözülür.
> **Koddan doğrulanmış (2026-08-19):** 8 endpoint — `status` (açık) + 7 korumalı (`mastery`, `weak-spots`, `strengths`, `overview`, `topic-goals` liste/oluştur/iptal).

### 3.1 Konu hâkimiyeti (🟢 Kodda)
```
GET /api/progress-tracking/status                                          → 200 modül durumu (açık uç)
GET /api/progress-tracking/students/{studentId}/mastery?subject=
    → 200 konu listesi { subject, topic, masteryLevel, score, trend, isWeakSpot, isStrength }
GET /api/progress-tracking/students/{studentId}/weak-spots                 → 200 eksik konular (öncelikli)
GET /api/progress-tracking/students/{studentId}/strengths                  → 200 güçlü konular
GET /api/progress-tracking/students/{studentId}/overview                   → 200 birleşik gelişim genel bakışı
```
> **⚠️ Önerilen (kodda yok):** `GET .../mastery/{subject}/{topic}` (konu detayı + geçmiş).

### 3.2 Konu hedefleri (🟢 Kodda)
```
POST /api/progress-tracking/students/{studentId}/topic-goals
     body: { subject, topic, targetMasteryLevel, targetNetRatio?, targetDate? }   → 201
GET  /api/progress-tracking/students/{studentId}/topic-goals?status=Active        → 200
POST /api/progress-tracking/topic-goals/{goalId}/cancel                           → 200
```

### 3.3 Zaman serisi / gelişim (⚠️ Önerilen — kodda yok)
```
GET /api/progress-tracking/students/{studentId}/snapshots?period=Weekly&from=&to=
    → 200 zaman serisi (grafik için) — ProgressSnapshot henüz kodda yok
```
> Not: birleşik özet **`overview`** uç adıyla §3.1'de **kodda mevcuttur** (önceki dokümandaki `progress-overview` yanlıştı).

### 3.4 İç besleme (event consumer — endpoint değil)
```
[consume] LessonSessionCompletedDomainEvent (M05)   → ilgili konu mastery güncelle
[consume] StudySessionCompletedDomainEvent  (M08)   → çalışma süresi rollup + mastery güncelle
[consume] TestResultRecordedDomainEvent     (M08)   → net oranı + trend güncelle
[scheduler] periyodik snapshot üretimi (haftalık/aylık)
```

---

## 4. İş Kuralları

### 4.1 Türetilmiş veri (kendi ölçümü yok)
- ProgressTracking **kendi ham ölçümünü üretmez**; tüm girdi M08 (çalışma/test) ve M05 (ders) olaylarından gelir.
- Veri kaynak olayların **yeniden oynatılmasıyla (replay)** yeniden hesaplanabilir olmalı (idempotent handler'lar).

### 4.2 Hâkimiyet skoru (öneri)
- `MasteryScore` (0–100), ağırlıklı bir formülle hesaplanır: test net oranı (ana sinyal) + çalışma süresi + ders kapsamı.
- Seviye bantları (öneri): `0–20 Weak`, `20–45 Developing`, `45–75 Proficient`, `75–100 Mastered`; `NotStarted` = hiç veri yok.
- **Eksik konu (`IsWeakSpot`):** seviye `Weak`/`Developing` **veya** trend `Declining`.
- **Güçlü konu (`IsStrength`):** seviye `Proficient`/`Mastered` **ve** trend `Stable`/`Improving`.
- Skor formülü ve bantlar **M15 (Settings)** ile konfigüre edilebilir olmalı.

### 4.3 Trend (artış/azalış)
- `Trend`, son N test/snapshot'ın net oranı eğiminden hesaplanır; yeterli veri yoksa `Stable`.
- Net **artış/azalış analizi** M08 `TestResult` zaman serisiyle hizalıdır (aynı `Subject/Topic`).

### 4.4 Hedef değerlendirme
- `TopicGoal` aktifken her mastery güncellemesinde kontrol edilir; hedefe ulaşılınca `Achieved`, `TargetDate` geçilip ulaşılamazsa `Missed`.

### 4.5 Erişim & gizlilik
- **Öğrenci** kendi gelişimini görür.
- **Öğretmen** yalnızca **bağlı** öğrencisinin, paylaşıma açık verisinden türetilen gelişimini görür.
- **Veli** yalnızca **onaylı bağlı (`Approved`)** ve paylaşıma açık (`IsSharedWithParent`) çocuğunun gelişimini görür (M09 üzerinden).

### 4.6 Konu normalizasyonu
- `Subject/Topic` değerleri M08 `StudyTopic` ve (ileride) M15 müfredat sözlüğüyle **aynı** olmalı ki rollup tutarlı olsun.

---

## 5. Olay Akışı

### 5.1 Beslenme (ders + çalışma → gelişim)
```
M05 LessonSessionCompletedDomainEvent ──┐
M08 StudySessionCompletedDomainEvent  ──┤ (Integration Event)
M08 TestResultRecordedDomainEvent     ──┘
        │
        ▼  (idempotent consumer)
  TopicMastery.Recalculate(StudentId, Subject, Topic)
        ├─► seviye değişti → TopicMasteryChangedDomainEvent
        │        ├─► M09 Parents (veli paneli güncel)
        │        ├─► M11 Notifications (öğretmen/veli "gelişim/gerileme" bilgisi)
        │        └─► M14 Reporting
        ├─► aktif TopicGoal kontrol → ulaşıldıysa TopicGoalAchievedDomainEvent → M11
        └─► (zamanlayıcı) ProgressSnapshot üret → ProgressSnapshotCreatedDomainEvent → M14
```

### 5.2 Hedef
```
[Öğrenci/Öğretmen hedef koyar] → POST /topic-goals (Active)
  → mastery hedefi karşıladığında MarkAchieved → TopicGoalAchievedDomainEvent → M11 kutlama
```

---

## 6. Mobil Ekranlar (Planlanan)

> Birincil renk `0xFF082B4F`. Gelişim ekranları **öğrenci**, **öğretmen** ve **veli** rollerinde farklı bağlamla
> kullanılır (rol bazlı navigasyon; bkz. `../roles/ogretmen.md`, `../roles/veli.md`, `../roles/ogrenci.md`).
> Mobil tarafta bağımsız bir feature yerine ilgili rollerin gelişim sekmesinde gömülü olabilir
> (`mobile/lib/features/progress/` önerilir).

- `progress_overview` — hâkimiyet dağılımı (mastered/proficient/...), genel trend kartı.
- `topic_mastery_list` — ders/konu bazlı seviye listesi; eksik konular (`weak-spots`) vurgulu, güçlü konular ayrı.
- `topic_detail` — seçili konunun zaman içindeki gelişim grafiği (snapshot serisi) + ilgili testler.
- `topic_goals` — konu hedefleri belirleme/izleme (öğrenci veya öğretmen).
- `progress_chart` — haftalık/aylık net & çalışma süresi zaman serisi grafiği.
- (Öğretmen) `student_progress` — bağlı öğrencinin eksik/güçlü konuları, hedef önerme.
- (Veli) `child_progress` — çocuğun gelişim grafikleri (M09 paneli içinden).

---

## 7. Kabul Kriterleri (Faz 3)

- [ ] M05 ders tamamlanması ve M08 çalışma/test olayları **tüketilerek** `TopicMastery` otomatik güncellenir (idempotent).
- [ ] Her ders+konu için **hâkimiyet seviyesi** ve **0–100 skor** hesaplanır.
- [ ] **Eksik** (`weak-spots`) ve **güçlü** konular listelenir.
- [ ] Konu bazlı **trend** (artış/sabit/azalış) hesaplanır ve gösterilir.
- [ ] Öğrenci/öğretmen **konu hedefi** koyabilir; hedefe ulaşınca otomatik `Achieved` + bildirim.
- [ ] Haftalık/aylık **ProgressSnapshot** üretilir; gelişim **zaman serisi grafiği** sunulur.
- [ ] **Öğretmen** bağlı öğrencisinin, **veli** onaylı+paylaşıma açık çocuğunun gelişimini görür.
- [ ] Çıktı M14 (Raporlama) ve M09 (Veli paneli) tarafından tüketilebilir.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşullar:** Bu modül **M08 (Study)** çalışma/test olaylarına ve **M05 (LessonSessions)** tamamlanma
> olayına bağımlıdır. M05 `LessonSessionCompletedDomainEvent` **hazırdır (🟢)**; M08 olayları ise **henüz yok**
> (M08 iskelet — önce inşa edilmeli, bkz. [`m08_study.md`](m08_study.md)).

1. **M08'i tamamla** (çalışma/test olayları — ana besleme kaynağı; önkoşul).
2. **ProgressTracking domain'i** — `TopicMastery`, `TopicGoal`, `ProgressSnapshot` + enum + olaylar.
3. **Besleme handler'ları** — `LessonSessionCompleted`, `StudySessionCompleted`, `TestResultRecorded` integration event consumer'ları (idempotent).
4. **Hâkimiyet skoru motoru** — ağırlıklı skor + seviye bantları + trend hesaplama (M15'ten konfigüre edilebilir).
5. **Application (CQRS)** — mastery/weak-spots/strengths/snapshots/topic-goals sorgu ve komutları + erişim politikası.
6. **Infrastructure** — `ProgressTrackingDbContext` `DbSet`'leri, **`progress_tracking` şeması migration'ı**, snapshot zamanlayıcısı.
7. **Konu sözlüğü hizalama** — M08/M15 ile `Subject/Topic` normalizasyonu.
8. **Erişim/gizlilik** — öğretmen (bağlı öğrenci) ve veli (M09, paylaşıma açık) görünümleri.
9. **M14/M09 entegrasyonu** — rapor ve veli paneli tüketimi.
10. **Mobil `progress` ekranları** (öğrenci/öğretmen/veli bağlamları).

---

## 9. İlişkili Dokümanlar

- Öğretmen rolü (gelişim takibinin birincil kullanıcısı) → [`../roles/ogretmen.md`](../roles/ogretmen.md)
- Veli rolü (gelişim grafiklerini görür) → [`../roles/veli.md`](../roles/veli.md)
- Öğrenci rolü (kendi gelişimini görür, veriyi üretir) → [`../roles/ogrenci.md`](../roles/ogrenci.md)
- Besleme kaynağı: bireysel çalışma + test (önkoşul) → [`m08_study.md`](m08_study.md)
- Besleme kaynağı: tamamlanan ders → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Öğrenci profili / sahiplik → [`m03_students.md`](m03_students.md)
- Planlama → [`m04_scheduling.md`](m04_scheduling.md)
- Ödevler (kapsam/eksik sinyali) → [`m06_assignments.md`](m06_assignments.md)
- Ödeme → [`m07_payments.md`](m07_payments.md)
- Veli paneli (gelişimi tüketir) → [`m09_parents.md`](m09_parents.md)
- Gelişim/gerileme bildirimleri → [`m11_notifications.md`](m11_notifications.md)
- Raporlama (gelişim verisini tüketir) → [`m14_reporting.md`](m14_reporting.md)
- Skor formülü / seviye bantları / konu sözlüğü → [`m15_settings.md`](m15_settings.md)
- Veri modeli → [`veri_modeli.md`](veri_modeli.md)
- Genel durum tablosu → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

*M10 Gelişim Takibi (ProgressTracking) Modülü — Detaylı Tasarım | Faz 3 | Durum: 🟡 Çalışır çekirdek (TopicMastery+TopicGoal+besleme+API+mobil) | Güncelleme: 2026-08-26 (idempotency doküman drift'i giderildi: eski `ProcessedEvent`/`processed_events` kaldırıldı, yerine paylaşılan `IdempotentIntegrationEventHandler` + `inbox_messages`; önceki not — 2026-08-19 kod-senkron: API 8 endpoint doğrulandı — status + mastery/weak-spots/strengths/overview + topic-goals liste/oluştur/iptal; `progress-overview` → gerçek route `overview`; `mastery/{subject}/{topic}` detayı ve `snapshots` kodda yok, "önerilen" olarak işaretlendi)*
