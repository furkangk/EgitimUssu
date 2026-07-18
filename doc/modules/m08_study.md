# 📚 M08 — Bireysel Çalışma (Study) Modülü — Detaylı Tasarım Dokümanı

> **Kod Modülü:** `src/Modules/Study` · **Route Prefix:** `/api/study` · **Şema:** `study`
> **PRD Modülü:** M08 Bireysel Çalışma · **Faz:** 2 (Öğrenci Bireysel Çalışma)
> **Durum:** 🟢 **Uçtan uca çalışır** (Domain + Application/CQRS + Infrastructure + migration + API + mobil `study` feature)
> **Platform:** EğitimÜssü (EgitimUssu) — .NET 9 modüler monolit · PostgreSQL · Redis · Flutter
>
> **Amaç:** Öğrencinin **öğretmensiz, tam işlevsel** bireysel çalışmasını takip etmek: kronometreyle
> çalışma seansları, deneme/sınav (test) performansı, günlük hedefler, çalışma serisi (streak),
> başarım rozetleri ve konu bazlı çalışma kaydı. Bu modül platformun **büyüme motorudur** — öğretmen
> gerektirmeden değer üretir ve eşleştirmeye (M12) hazır bir öğrenci havuzu besler.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Var olan (uçtan uca — 2026-07-04'te inşa edildi)
- **Domain** (`Domain/StudyDomainModel.cs`): `StudySession` (kronometre; start/pause/resume/complete/discard, mola muhasebesi), `TestResult` (net doğrulama + hesabı), `StudyGoal`, `StudyStreak` (`RegisterStudyDay`), `Achievement` (katalog) + `StudentAchievement` (kazanım), `StudyTopic` (konu rollup), `StudentSubjectCatalog` + `StudentTopicCatalog` (öğrencinin tanımladığı ders/konu kataloğu), `StudyStudent` (öğrenci↔kullanıcı bağı + paylaşım tercihleri). Enum'lar: `StudySessionStatus`, `StudySessionSource`, `TestType`, `AchievementCategory`. Domain olayları: `StudySessionStarted/Completed`, `TestResultRecorded`, `StudyGoalUpdated`, `StreakMilestoneReached`, `StreakBroken`, `AchievementEarned` (Outbox'a düşer).
- **Application (CQRS)** (`StudyContracts/SessionFeatures/TestFeatures/ProgressFeatures/Policies`): Start/Pause/Resume/Complete/Discard/Manual seans komutları; RecordTest; UpdateGoals; UpdateSharing. Sorgular: session, list-sessions, weekly-summary, test, list-tests, net-trend, goals, streak, achievements, sharing, dashboard. `StudyCompletionService` (konu rollup + streak + başarım değerlendirme), `AchievementEvaluator`, `StudyOwnershipGuard`/`StudyLinkResolver`.
- **Infrastructure**: `StudyDbContext` 8 `DbSet` + EF config'ler (snake_case, enum→string), `StudyRepository` (tek unit-of-work), `AddStudyModule` DI, `StudyDesignTimeDbContextFactory`, **`InitialStudy` migration** (`study` şeması + achievement katalog seed'i 10 rozet).
- **API** (`API/StudyModule.cs`): §3'teki tüm uçlar, `AuthenticatedUser` politikası, sahiplik yetkilendirmesi, hata→HTTP eşlemesi.
- **Mobil** (`mobile/lib/features/study/`): `student-home` (dashboard), `study/timer`, `study/test`, `study/goals` (+paylaşım), `study/history` (seans/deneme/haftalık + manuel), `study/achievements`. Rol bazlı `redirect` (öğrenci → `/student-home`). Self-register: profil yoksa `SelfRegistered` olarak otomatik oluşturulur.

### ✅ Doğrulama (2026-07-04, InMemory + gerçek uçlar)
Öğrenci kaydı → self profil → start/pause/resume/complete (mola muhasebesi), tek-aktif-seans **409**, manuel giriş, **test net=28** (30−8/4), günlük/haftalık özet, streak=1, dashboard, paylaşım güncelleme ve **sahiplik izolasyonu (başka öğrenci 403)** uçtan uca doğrulandı. Not: başarım kazanımı katalog seed'i migration ile geldiği için Postgres'te devrededir (InMemory'de seed uygulanmaz).

### ⚠️ Sınır / Gelecek işler
- **Sahiplik modeli:** Study, kendi sınırı içinde `StudyStudent` bağını **ilk yazımda oturum kullanıcısına** bağlar. Manuel öğrenci hijack'ini tümüyle kapatmak için M03 `StudentProfileCreated` integration event tüketimi eklenmeli.
- **Yerel gün (streak):** M15 zaman dilimi tercihi gelene kadar Türkiye saati (UTC+3) varsayılır (`StudyLocalTime`). Streak gün sınırı 04:00'tir (`StudyLocalTime.StreakDate`); istatistik/haftalık özet ise gece yarısı tabanlı `LocalDate` kullanır.
- **Konu sözlüğü:** `Subject/Topic` serbest metin; M15 müfredat sözlüğüne bağlanmalı.
- **Veli/öğretmen okuma yolu:** Paylaşım bayrakları (`IsSharedWith*`) kayıtlarda tutulur; M09/öğretmen görünümünün bunları okuması bağ + integration event ile tamamlanacak.

---

## 2. Domain Modeli (✅ Kodda — `Domain/StudyDomainModel.cs`)

> Tüm aggregate'ler `EgitimUssu.Shared.Kernel` içindeki `AggregateRoot<Guid>` / `Entity<Guid>` desenini izler:
> private parametresiz ctor (EF için), `private set` property'ler, enum değerleri `1`'den başlar,
> domain olayları `Raise(...)` ile yükseltilir ve `sealed record ...DomainEvent : DomainEvent` olarak tanımlanır.
> Tüm tablolar `study` şemasında oluşturulur. `StudentId`, M03 `Students` modülündeki `StudentProfile.Id`'ye
> mantıksal referanstır (modüller arası **doğrudan DB FK yok** — bkz. `00_genel_bakis.md` modül sınırı kuralı).

### 2.1 `StudySession` (AggregateRoot) — Çalışma seansı / kronometre

Öğrencinin bir konuya ayırdığı çalışma süresini ölçen sayaç. Mola süresi **net süreye dahil edilmez**.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | Aggregate kimliği |
| `StudentId` | `Guid` | ✓ | Çalışan öğrenci (M03 `StudentProfile.Id`) |
| `Subject` | `string` | ✓ | Ders (örn. "Matematik") |
| `Topic` | `string?` | — | Konu (örn. "Türev"); `StudyTopic` ile eşlenebilir |
| `StartedAtUtc` | `DateTime` | ✓ | Sayaç başlangıcı (UTC) |
| `EndedAtUtc` | `DateTime?` | — | Sayaç bitişi; tamamlanınca dolar |
| `EffectiveMinutes` | `int` | ✓ | **Mola hariç** net çalışma (dakika) |
| `BreakMinutes` | `int` | ✓ | Toplam mola süresi (dakika), varsayılan 0 |
| `Status` | `StudySessionStatus` | ✓ | `Running`/`Paused`/`Completed`/`Discarded` |
| `LastResumedAtUtc` | `DateTime?` | — | Son devam/başlama anı (aktif dilim ölçümü için) |
| `PersonalNote` | `string?` | — | Seans sonu kişisel not |
| `Source` | `StudySessionSource` | ✓ | `Stopwatch` (kronometre) / `Manual` (sonradan giriş) |
| `IsSharedWithParent` | `bool` | ✓ | Bu seans veliyle paylaşılsın mı (gizlilik) |
| `IsSharedWithTeacher` | `bool` | ✓ | Bu seans bağlı öğretmenle paylaşılsın mı (gizlilik) |
| `CreatedOnUtc` | `DateTime` | ✓ | Kayıt anı |
| `UpdatedOnUtc` | `DateTime` | ✓ | Son güncelleme |

**Davranışlar (metotlar):**
- `Start(...)` → ctor; `Status = Running`, `StartedAtUtc = LastResumedAtUtc = now`. `StudySessionStartedDomainEvent` yükseltir.
- `Pause(nowUtc)` → aktif dilimi `EffectiveMinutes`'e ekler, `Status = Paused`, mola sayacı başlar.
- `Resume(nowUtc)` → ara biten süreyi `BreakMinutes`'e ekler, `LastResumedAtUtc = now`, `Status = Running`.
- `Complete(nowUtc, note?)` → son aktif dilimi ekler, `EndedAtUtc` set, `Status = Completed`,
  **`StudySessionCompletedDomainEvent`** yükseltir (mimari dokümanında öngörülen `StudySessionEndedEvent`'in kanonik karşılığı).
- `Discard()` → yanlış başlatılan seansı iptal eder (`Status = Discarded`, istatistiğe dahil edilmez).

```csharp
public enum StudySessionStatus { Running = 1, Paused = 2, Completed = 3, Discarded = 4 }
public enum StudySessionSource { Stopwatch = 1, Manual = 2 }
```

### 2.2 `TestResult` (AggregateRoot) — Deneme / sınav performansı

Öğrencinin çözdüğü deneme/test sonucu. Zaman içindeki net **artış/azalış analizinin** ham verisidir (M10 besler).

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | M03 `StudentProfile.Id` |
| `Subject` | `string` | ✓ | Ders |
| `Topic` | `string?` | — | Konu (branş denemesi ise null olabilir) |
| `TestName` | `string?` | — | Deneme adı (örn. "3D Yayınları TYT-5") |
| `TestType` | `TestType` | ✓ | `Branch`/`General`/`Subject`/`Topic` |
| `TotalQuestions` | `int` | ✓ | Toplam soru |
| `Correct` | `int` | ✓ | Doğru |
| `Wrong` | `int` | ✓ | Yanlış |
| `Blank` | `int` | ✓ | Boş |
| `Net` | `decimal` | ✓ | Hesaplanan net (bkz. İş Kuralları 4.3) |
| `PenaltyDivisor` | `int` | ✓ | Net katsayısı (varsayılan 4; M15'ten konfigüre edilir) |
| `DurationMinutes` | `int?` | — | Testin çözüm süresi |
| `TakenOnUtc` | `DateTime` | ✓ | Testin çözüldüğü tarih |
| `IsSharedWithParent` | `bool` | ✓ | Gizlilik bayrağı |
| `IsSharedWithTeacher` | `bool` | ✓ | Gizlilik bayrağı |
| `CreatedOnUtc` | `DateTime` | ✓ | |

**Davranış:** ctor doğrulama — `Correct + Wrong + Blank == TotalQuestions` (aksi halde domain exception).
`Net = Correct - (Wrong / (decimal)PenaltyDivisor)` (yuvarlama M15 ayarına göre). Olay: `TestResultRecordedDomainEvent`.

```csharp
public enum TestType { Branch = 1, General = 2, Subject = 3, Topic = 4 }
```

### 2.3 `StudyGoal` (AggregateRoot) — Çalışma hedefleri

Öğrencinin kendine koyduğu hedefler. Streak ve günlük ilerleme bunlara göre değerlendirilir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | (bir öğrenci için tek aktif hedef seti) |
| `DailyGoalMinutes` | `int` | ✓ | Günlük çalışma hedefi (dakika) |
| `WeeklyGoalMinutes` | `int?` | — | Haftalık hedef (opsiyonel) |
| `TargetNet` | `decimal?` | — | Ders/genel için hedef net |
| `TargetScore` | `decimal?` | — | Hedef puan (örn. sıralama/puan) |
| `Subject` | `string?` | — | Hedef belirli bir derse özelse |
| `StreakThresholdPercent` | `int` | ✓ | Günün seriye sayılması için günlük hedefin tamamlanması gereken yüzdesi (1–100, varsayılan 60; `Math.Clamp`) |
| `EffectiveFromUtc` | `DateTime` | ✓ | Hedefin geçerlilik başlangıcı |
| `IsActive` | `bool` | ✓ | Aktif mi |
| `UpdatedOnUtc` | `DateTime` | ✓ | |

**Davranış:** `UpdateGoals(...)` → değerleri günceller, `StudyGoalUpdatedDomainEvent` yükseltir.

### 2.4 `StudyStreak` (AggregateRoot) — Çalışma serisi & rekor

Motivasyon çekirdeği: ardışık çalışılan gün sayısı ve kişisel rekor. Öğrenci başına tekildir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `StudentId` | `Guid` | ✓ | Tekil (öğrenci başına 1) |
| `CurrentStreakDays` | `int` | ✓ | Mevcut ardışık gün |
| `LongestStreakDays` | `int` | ✓ | Kişisel rekor |
| `LastStudiedOnDate` | `DateOnly?` | — | Son çalışılan gün (öğrenci yerel saatine göre) |
| `TotalStudyDays` | `int` | ✓ | Toplam çalışılan gün sayısı |
| `UpdatedOnUtc` | `DateTime` | ✓ | |

**Davranış:** `RegisterStudyDay(localDate)` →
- aynı gün ise no-op,
- dün ise `CurrentStreakDays++`,
- arada boşluk varsa `CurrentStreakDays = 1` (seri kırıldı, `StreakBrokenDomainEvent`),
- `LongestStreakDays` güncellenir; rekor kırılırsa `StreakMilestoneReachedDomainEvent`.

**Streak eşiği (B3):** `RegisterStudyDay` artık her tamamlanan seansta çağrılmaz. `StudyCompletionService`, o günün (04:00 tabanlı `StudyLocalTime.StreakDate`) toplam efektif dakikasını hesaplar ve yalnız eşik aşılınca günü seriye işler. Eşik saf `StreakRules` sınıfındadır: günlük hedef varsa `ceil(DailyGoalMinutes × StreakThresholdPercent / 100)`, hedef yoksa sabit **20 dk** (`MinFixedThresholdMinutes`). Gün sınırı 04:00'tir (gece geç çalışan öğrenci dünü korur).

### 2.5 `Achievement` / `StudentAchievement` — Başarım rozetleri

Öğrenciyi sistemde tutan oyunlaştırma. `Achievement` katalog (statik tanım), `StudentAchievement` kazanım kaydı.

`Achievement` (katalog):

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `Code` | `string` | Benzersiz kod (örn. `STREAK_7`, `FIRST_TEST`, `100H_MATH`) |
| `Title` | `string` | Görünen ad (örn. "7 Günlük Seri") |
| `Description` | `string` | Açıklama |
| `Category` | `AchievementCategory` | `Streak`/`StudyTime`/`TestPerformance`/`Goal`/`Consistency` |
| `Threshold` | `int` | Tetikleme eşiği (örn. 7 gün, 100 saat) |
| `IconKey` | `string?` | Mobil ikon anahtarı |

`StudentAchievement` (kazanım — `Entity<Guid>`):

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `StudentId` | `Guid` | |
| `AchievementId` | `Guid` | Katalog referansı |
| `EarnedOnUtc` | `DateTime` | Kazanım anı |
| `ProgressValue` | `int` | Kazanım anındaki değer |

**Olay:** kazanımda `AchievementEarnedDomainEvent` (M11 ile kutlama bildirimi tetikler).

```csharp
public enum AchievementCategory { Streak = 1, StudyTime = 2, TestPerformance = 3, Goal = 4, Consistency = 5 }
```

### 2.6 `StudyTopic` (Entity / referans) — Çalışılan konular

Öğrencinin çalıştığı konuları normalize eden hafif kayıt. Hem `StudySession`/`TestResult` etiketlemesinde
hem M10 `TopicMastery` ile hizalamada kullanılır.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `StudentId` | `Guid` | |
| `Subject` | `string` | Ders |
| `Topic` | `string` | Konu adı |
| `FirstStudiedOnUtc` | `DateTime` | İlk çalışma |
| `LastStudiedOnUtc` | `DateTime` | Son çalışma |
| `TotalEffectiveMinutes` | `int` | Bu konuya ayrılan toplam net süre (rollup) |
| `SessionCount` | `int` | Seans sayısı |

> Not: `Subject`/`Topic` serbest metin yerine ileride M15 (Settings) tarafından yönetilen bir
> **müfredat/konu sözlüğüne** bağlanmalıdır (bkz. Eksikler 8).

### 2.7 Domain Olayları (özet)

| Olay | Tetikleyen | Tüketen (öneri) |
|------|-----------|------------------|
| `StudySessionStartedDomainEvent` | `StudySession.Start` | (analytics) |
| `StudySessionCompletedDomainEvent` | `StudySession.Complete` | M10 ProgressTracking, M09 Parents, Streak güncelleme |
| `TestResultRecordedDomainEvent` | `TestResult` ctor | M10 ProgressTracking, M09 Parents, M14 Reporting |
| `StudyGoalUpdatedDomainEvent` | `StudyGoal.UpdateGoals` | M10 (hedef hizalama) |
| `StreakMilestoneReachedDomainEvent` | `StudyStreak` | M11 Notifications |
| `StreakBrokenDomainEvent` | `StudyStreak` | M11 Notifications (geri kazanma teşviki) |
| `AchievementEarnedDomainEvent` | `StudentAchievement` | M11 Notifications |

> Domain olayları Outbox üzerinden Integration Event'e dönüşür (bkz. `00_genel_bakis.md` Mesajlaşma satırı).

---

## 3. API Sözleşmesi (✅ Kodda) — `/api/study`

> Not (koddan): manuel seans `POST /sessions/manual`; seans iptali `POST /sessions/{id}/discard`; paylaşım `GET`/`PUT /students/{studentId}/sharing`. `by-user` yerine öğrenci StudentId'si M03 `GET /api/students/profiles/by-user/{userId}` ile çözülür (mobil self-register).

> Tümü auth gerektirir; öğrenci yalnızca **kendi** `StudentId`'sine ait verilere erişir (sahiplik politikası).
> Veli/öğretmen erişimi gizlilik bayraklarına ve onaylı bağa tabidir (bkz. İş Kuralları 4.5).

### 3.1 Çalışma seansları (kronometre)
```
POST   /api/study/sessions/start
       body: { studentId, subject, topic?, source }            → 201 { sessionId, status, startedAtUtc }
POST   /api/study/sessions/{id}/pause                           → 200 { status, effectiveMinutes, breakMinutes }
POST   /api/study/sessions/{id}/resume                          → 200 { status, lastResumedAtUtc }
POST   /api/study/sessions/{id}/complete
       body: { personalNote? }                                  → 200 { summary: süre, mola, konu }
POST   /api/study/sessions/{id}/discard                         → 204
POST   /api/study/sessions/manual
       body: { studentId, subject, topic?, effectiveMinutes,
               studiedOnUtc, personalNote? }                    → 201 (kronometresiz manuel giriş)
GET    /api/study/sessions/{id}                                 → 200 seans detayı
PUT    /api/study/sessions/{id}
       body: { subject, topic?, effectiveMinutes, personalNote? } → 200 (yalnız tamamlanmış seans; konu rollup yeniden türetilir)
DELETE /api/study/sessions/{id}                                 → 200 (konu rollup yeniden türetilir)
GET    /api/study/students/{studentId}/sessions?from=&to=&subject=
                                                                → 200 seans listesi (sayfalı)
GET    /api/study/students/{studentId}/weekly-summary?weekStart=
                                                                → 200 { totalMinutes, perSubject[], perDay[] }
```

### 3.2 Deneme / test sonuçları
```
POST   /api/study/test-results
       body: { studentId, subject, topic?, testType, testName?,
               totalQuestions, correct, wrong, blank,
               durationMinutes?, takenOnUtc }                   → 201 { testResultId, net }
GET    /api/study/test-results/{id}                             → 200
PUT    /api/study/test-results/{id}
       body: { subject, topic?, testType, testName?, totalQuestions,
               correct, wrong, blank, penaltyDivisor?, durationMinutes?,
               takenOnUtc }                                      → 200 (net yeniden hesaplanır)
DELETE /api/study/test-results/{id}                             → 200
GET    /api/study/students/{studentId}/test-results?subject=&from=&to=
                                                                → 200 liste
GET    /api/study/students/{studentId}/net-trend?subject=&topic=
                                                                → 200 zaman serisi (net artış/azalış)
```

### 3.3 Hedef, streak, başarım
```
GET    /api/study/students/{studentId}/goals                    → 200 aktif hedefler
PUT    /api/study/students/{studentId}/goals
       body: { dailyGoalMinutes, weeklyGoalMinutes?, targetNet?, targetScore?, subject? }
                                                                → 200
GET    /api/study/students/{studentId}/streak                   → 200 { current, longest, lastStudiedOn, todayProgress }
GET    /api/study/students/{studentId}/achievements             → 200 kazanılan + ilerleme
GET    /api/study/students/{studentId}/dashboard                → 200 birleşik özet (bugün/hafta/streak/son test)
```

### 3.4 Gizlilik (paylaşım kontrolü)
```
PUT    /api/study/students/{studentId}/sharing
       body: { shareStudyWithParent, shareTestsWithParent,
               shareStudyWithTeacher, shareTestsWithTeacher }   → 200
```
> Bu tercihler M15 (Settings) bayraklarıyla senkron tutulur; M09 (Parents) ve öğretmen görünümü bunları okur.

### 3.5 Ders/konu kataloğu (✅ Kodda)
Öğrencinin tanımladığı ders (`StudentSubjectCatalog`) ve konu (`StudentTopicCatalog`) kataloğu. Kronometre,
deneme girişi ve takvim formu tutarlı ders/konu adlarını bu katalogdan alır (M10 gelişim takibinin de konu temeli).
```
GET    /api/study/students/{studentId}/subjects            → 200 dersler + konuları
POST   /api/study/students/{studentId}/subjects
       body: { name, colorHex? }                           → 200 ders
PUT    /api/study/subjects/{subjectId}
       body: { name, colorHex?, isActive }                 → 200 ders (+konular)
DELETE /api/study/subjects/{subjectId}                     → 200 (ders + konuları silinir)
POST   /api/study/subjects/{subjectId}/topics  body: { name }             → 200 konu
PUT    /api/study/topics/{topicId}  body: { name, orderIndex, isActive }  → 200 konu
DELETE /api/study/topics/{topicId}                         → 200
```
> Sahiplik: `students/{studentId}/subjects` öğrenci-scoped; `subjects/{id}` ve `topics/{id}` işlemleri
> ders/konu üzerinden çözülen sahiplik yetkilendiricileriyle korunur. Ders/konu silmek geçmiş seans/test
> kayıtlarını etkilemez (bu kayıtlar ders/konu adını metin olarak kopyalar).

### 3.6 Öğrenci ders notları (✅ Kodda)
Öğrencinin kendi tuttuğu not (`StudyNote`). Öğretmenin ders oturumuna bağlı `LessonNote`'undan (M06) **ayrıdır**;
öğrencinin kendi çalışma dünyasına aittir, opsiyonel ders/konu ile ilişkilendirilir.
```
GET    /api/study/students/{studentId}/notes                          → 200 notlar (güncelleme sırasına göre)
POST   /api/study/students/{studentId}/notes
       body: { title, body, subject?, topic?, attachmentUrl? }        → 200 not
PUT    /api/study/notes/{noteId}   body: { title, body, subject?, topic?, attachmentUrl? }  → 200
DELETE /api/study/notes/{noteId}                                       → 200
```
> Sahiplik: liste/oluşturma öğrenci-scoped; güncelleme/silme not üzerinden çözülen yetkilendiriciyle korunur.

---

## 4. İş Kuralları

### 4.1 Kronometre & mola
- **Net süre = toplam aktif dilimlerin toplamı.** Mola süresi (`Paused` aralıkları) `EffectiveMinutes`'e **dahil edilmez**, ayrıca `BreakMinutes`'te tutulur (PRD §M08).
- Aynı anda bir öğrencinin yalnızca **bir `Running`/`Paused` seansı** olabilir (yeni başlatma öncekini engeller veya devralmayı önerir).
- `Discarded` seanslar hiçbir istatistiğe (süre, streak, konu rollup) **dahil edilmez**.
- Çok uzun (örn. > 8 saat) açık kalan seans için otomatik kapatma/uyarı önerilir (sayaç unutulması).
- **Seans düzenle/sil (S-08.10):** Yalnızca **tamamlanmış** seans düzenlenir (`EditCompleted`: ders/konu/süre/not; süre > 0). Düzenleme/silme sonrası ilgili `(Subject, Topic)` **konu rollup'ı** (`StudyTopic`) o öğrencinin tamamlanmış seanslarından **yeniden türetilir** (`StudyRecompute.RebuildTopicAsync`; konu değişirse hem eski hem yeni konu için). Kalan seans yoksa rollup silinir. **Streak zinciri v1'de retroaktif geri sarılmaz** (YAGNI): o günün streak-uygunluğu bir sonraki seans kaydında yeniden değerlendirilir.

### 4.2 Manuel giriş
- Kronometre kullanmadan geçmişe dönük seans eklenebilir (`Source = Manual`); `studiedOnUtc` bugünden ileri olamaz.

### 4.3 Test/net hesabı
- **Doğrulama:** `Correct + Wrong + Blank == TotalQuestions` (ihlalde komut reddedilir).
- **Net formülü:** `Net = Correct - (Wrong / PenaltyDivisor)`; varsayılan `PenaltyDivisor = 4` (4 yanlış 1 doğruyu götürür).
  Katsayı ve yuvarlama **M15 (Settings)** üzerinden konfigüre edilebilir (örn. LGS vs YKS).
- `Net` negatif olabilir; saklanır (ham veri).
- **Test düzenle/sil (S-08.18):** `TestResult.Edit` aynı doğrulamayı uygular ve **net'i D/Y/B'den yeniden hesaplar**; silme kaydı kaldırır. Sahiplik `testResultId` üzerinden (öğrenci yalnız kendi kaydı; admin serbest).

### 4.4 Streak & hedef
- Bir gün **en az 1 tamamlanmış seans** (veya günlük hedef dakikasının karşılanması — konfigüre edilebilir) o günü "çalışılmış" sayar.
- Seri, öğrencinin **yerel takvim günü** üzerinden hesaplanır (zaman dilimi M15'ten).
- Günlük hedef karşılandığında `dashboard.todayProgress` "tamamlandı" döner; ardışık günlerde rozet eşiği kontrol edilir.

### 4.5 Gizlilik & paylaşım (PRD §M08 "Veli ile Paylaşım")
- Öğrenci, her veri türü için (çalışma süresi / test) veli ve öğretmenle paylaşımı **ayrı ayrı** kontrol eder (`IsSharedWith*` bayrakları + M15 tercihleri).
- **Reşit olmayan öğrenci:** veli erişimi varsayılan **açık** (KVKK velayet ilkesi); reşit öğrencide varsayılan **kapalı**, paylaşım öğrenci onayına tabi (bkz. `m09_parents.md`).
- Öğretmen yalnızca **bağlı olduğu** öğrencinin, paylaşıma açık verisini görür.

### 4.6 Plan çakışması önceliği (M04 ile)
- Öğrencinin kendi çalışma planı ile öğretmenle yapılacak **özel ders** çakışırsa **özel ders önceliklidir**.
- Çakışma anında öğrenciye **uyarı** gösterilir ve bireysel plan ikinci plana atılır (planlama kuralı `m04_scheduling.md`'de işlenir; M08 yalnızca uyarıyı yüzeye çıkarır).

---

## 5. Olay Akışı

### 5.1 Çalışma seansı (kronometre) yaşam döngüsü
```
[Öğrenci konu seçer] → POST /sessions/start
     → StudySession(Status=Running) + StudySessionStartedDomainEvent
[Mola] → /pause (aktif dilim EffectiveMinutes'e eklenir, Status=Paused)
[Devam] → /resume (mola süresi BreakMinutes'e eklenir, Status=Running)
[Bitir] → /complete
     → EffectiveMinutes kesinleşir, Status=Completed
     → StudySessionCompletedDomainEvent  ──(Outbox→Integration)──┐
                                                                  ├─► StudyStreak.RegisterStudyDay → (rozet eşiği) AchievementEarnedDomainEvent → M11
                                                                  ├─► StudyTopic rollup güncelle
                                                                  ├─► M10 ProgressTracking (ProgressSnapshot/TopicMastery besle)
                                                                  └─► M09 Parents (paylaşıma açıksa veli paneli güncel)
```

### 5.2 Test sonucu
```
POST /test-results → doğrula (toplam=doğru+yanlış+boş) → Net hesapla
   → TestResultRecordedDomainEvent
        ├─► M10 ProgressTracking (net trend, TopicMastery)
        ├─► M14 Reporting (performans raporu)
        └─► M09 Parents (paylaşıma açıksa)
```

### 5.3 Streak/rozet
```
RegisterStudyDay → seri devam/kırılma → StreakMilestoneReached / StreakBroken → M11 Notifications
```

---

## 6. Mobil Ekranlar (Planlanan — Flutter `study` feature)

> Birincil renk `0xFF082B4F`. Öğrenci rolü için ayrı navigasyon gerekir (rol bazlı `redirect`, bkz. `../roles/ogrenci.md`).
> Feature klasörü: `mobile/lib/features/study/` (`data` / `domain` / `presentation`).

- `study_dashboard` — bugünkü çalışma, haftalık özet kartı, streak göstergesi, son test, aktif hedef.
- `study_timer` — konu seç → başlat / mola / devam / bitir; canlı kronometre + mola sayacı; seans sonu özet + not.
- `study_history` — geçmiş seanslar listesi + haftalık süre grafiği + konu dağılımı (pasta/çubuk).
- `manual_session` — kronometresiz geçmiş çalışma girişi.
- `test_entry` — deneme/test girişi (doğru/yanlış/boş → net otomatik).
- `test_performance` — konu/ders bazlı net trend grafiği (artış/azalış).
- `goals_streak` — günlük/haftalık hedef belirleme, streak ve kişisel rekor, motivasyon.
- `achievements` — kazanılan rozetler + bir sonraki rozete ilerleme.
- `study_privacy` — veli/öğretmenle paylaşım anahtarları (M15 ile senkron).

---

## 7. Kabul Kriterleri

PRD §Faz 2: "Öğrenci kendi çalışmalarını öğretmen olmadan takip eder."

- [x] Öğrenci konu seçip kronometre **başlatabilir / mola verebilir / devam edebilir / bitirebilir**.
- [x] Mola süresi net süreye **eklenmez**; seans özeti net süre + mola süresini ayrı gösterir.
- [x] Manuel (kronometresiz) seans girişi yapılabilir.
- [x] Seans geçmişi + **haftalık çalışma özeti** (toplam süre, ders dağılımı, gün dağılımı) görüntülenir.
- [x] Test girişi: `doğru+yanlış+boş = toplam` doğrulaması; **net otomatik** hesaplanır (varsayılan katsayı 4; M15'ten konfigüre edilecek).
- [x] Konu/ders bazlı **net trend** (zaman serisi) gösterilir (`GET .../net-trend`).
- [x] Günlük/haftalık **hedef** belirlenebilir; bugünkü ilerleme görünür.
- [x] **Streak** (mevcut + rekor) hesaplanır ve gösterilir; seri kırılınca `StreakBrokenDomainEvent` (Outbox) tetiklenir.
- [x] **Başarım rozeti** seti (10 rozet) tanımlı; kazanımda `AchievementEarnedDomainEvent` yükseltilir. (M11 kutlama bildirimi ⚠️ bekliyor.)
- [x] Öğrenci, çalışma/test verisinin veli ve öğretmenle **paylaşımını ayrı ayrı** açıp kapatabilir (`sharing`).
- [ ] Bireysel plan ile özel ders çakışmasında **özel ders öncelikli** ve öğrenciye uyarı gösterilir (M04 entegrasyonu ⚠️ bekliyor).

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> **Önkoşullar:** M01 Identity (`Student` rolü, self-register) ve M03 Students (`StudentProfile`) mevcut (🟢).
> Mobilde **rol bazlı navigasyon** (öğretmen/öğrenci ayrımı) eklenmeli — bu Faz 2'nin ortak önkoşuludur.

1. **Domain'i sıfırdan inşa et** — `StudySession`, `TestResult`, `StudyGoal`, `StudyStreak`, `Achievement`/`StudentAchievement`, `StudyTopic` + enum + domain olayları.
2. **Application (CQRS)** — Start/Pause/Resume/Complete/Discard, RecordTest, UpdateGoals komutları; weekly-summary, net-trend, dashboard, streak sorguları; repository arayüzleri; sahiplik politikası.
3. **Infrastructure** — `StudyDbContext` `DbSet`'leri, EF konfigürasyonları, **`study` şeması migration'ı**, repository implementasyonları, Integration event publish/handler.
4. **API endpoint'leri** — Bölüm 3'teki sözleşme + auth + sahiplik kontrolü.
5. **Gizlilik entegrasyonu** — M15 (Settings) paylaşım bayraklarıyla senkron `sharing` endpoint'i.
6. **Çakışma uyarısı** — M04 (Scheduling) ile özel ders önceliği kuralının yüzeye çıkarılması.
7. **Konu sözlüğü** — serbest metin `Subject/Topic` yerine M15 müfredat sözlüğüne bağlama (M10 ile hizalı).
8. **Mobil `study` feature** — Bölüm 6 ekranları + öğrenci dashboard navigasyonu.
9. **Olay yayınları** — M10 (ProgressTracking) ve M09 (Parents) için integration event'lerin doğrulanması.

---

## 9. İlişkili Dokümanlar

- Öğrenci rolü (bu modülün birincil kullanıcısı) → [`../roles/ogrenci.md`](../roles/ogrenci.md)
- Veli paneli (Study verisini tüketir) → [`../roles/veli.md`](../roles/veli.md) · [`m09_parents.md`](m09_parents.md)
- Öğretmen rolü (bağlıysa paylaşıma açık veriyi görür) → [`../roles/ogretmen.md`](../roles/ogretmen.md)
- Öğrenci profili / sahiplik → [`m03_students.md`](m03_students.md)
- Plan çakışması / özel ders önceliği → [`m04_scheduling.md`](m04_scheduling.md)
- Ders oturumu (öğretmen verisi) → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Ödevler → [`m06_assignments.md`](m06_assignments.md)
- Ödeme → [`m07_payments.md`](m07_payments.md)
- Streak/rozet/test bildirimleri → [`m11_notifications.md`](m11_notifications.md)
- Gelişim takibi (Study'den beslenir) → [`m10_progress_tracking.md`](m10_progress_tracking.md)
- Raporlama → [`m14_reporting.md`](m14_reporting.md)
- Net katsayısı, konu sözlüğü, gizlilik tercihleri → [`m15_settings.md`](m15_settings.md)
- Veri modeli (ER + modüller arası referans) → [`veri_modeli.md`](veri_modeli.md)
- Genel durum tablosu → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

*M08 Bireysel Çalışma (Study) Modülü — Detaylı Tasarım | Faz 2 | Durum: 🟢 Uçtan uca | Güncelleme: 2026-07-19*
