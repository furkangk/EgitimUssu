# P17 — Ders Oluşumu Zinciri (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Özel öğretmenin günlük akışı uçtan uca doğru çalışsın: haftalık dersin **her haftası** kendi oturumunu, ders notunu, ücretini ve hatırlatmasını üretsin; "bu haftayı iptal et" yalnız o haftayı etkilesin; geç iptal politikası ücret üretsin; öğrenci ve veli kendi ders oturumlarını görsün.

**Architecture:** K-20: oluşum `(LessonScheduleId, OccurrenceStartUtc)` birinci sınıf anahtardır. Tekrarlı derste iptal/erteleme kapsam zorunlu; "bu ve sonrakiler" seriyi **böler**. K-21: tamamlama yalnız LessonSessions'ta; Scheduling durumu olayla senkronize olur. İptal olayı Payments ve LessonSessions'ta işlenir. Hatırlatmalar oluşum bazlı ve kayan pencereyle üretilir.

**Tech Stack:** .NET 9, EF Core 9 / Npgsql 9, Flutter (T1, T3), xUnit, Testcontainers.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — K-20, K-21, §3.1 S-2/S-3. **Kaynak:** rapor §1.1 (N-01, N-02, N-03, N-06, N-12, N-13, N-15, N-16), §1.3 (N-48 iptal kısmı, N-49).

**Dal:** `feat/p17-ders-olusumu` · **Ön koşul:** P16 merge edilmiş (özellikle T3 — S-2).

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Önceki tüm sözleşmeler; `occurrenceStartAtUtc` daima serinin **orijinal** oluşum zamanı (master §4).
- Tek seferlik (tekrarsız) derslerde `OccurrenceStartUtc == StartAtUtc`; kapsam alanı yok sayılır.
- **S-3 göç kuralı** T3'te harfiyen uygulanır.
- Mobil değişen her ekran ARB'ye taşınır (K-26).
- Her görevde en az bir Testcontainers senaryosu (master §1).

---

### Task 1: Tekrarlı derste kapsam zorunlu + mobil seçim (N-01)

**Files:**
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (cancel/reschedule: tekrarlı derste `Scope` ve `OccurrenceStartAtUtc` zorunlu; varsayılan `All` kaldırılır)
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (istek DTO'ları)
- Modify: `src/Modules/Scheduling/Application/LessonChangeRequestFeatures.cs` (öğrenci erteleme talebi kabulü yalnız ilgili oluşuma `Single` uygulanır)
- Modify (mobil): `mobile/lib/features/scheduling/data/models/lesson_schedule_model.dart` (`toCancelPayload`/`toReschedulePayload` scope + occurrence), `.../data/repositories/scheduling_repository_impl.dart`, ders detay/iptal UI (`scheduling_page.dart` ve `lesson_detail_page.dart` içindeki iptal/erteleme akışı)
- Create (mobil): `mobile/lib/features/scheduling/presentation/widgets/occurrence_scope_sheet.dart`
- Test: `tests/Unit/OccurrenceScopeRequiredTests.cs`, `mobile/test/features/scheduling/occurrence_scope_sheet_test.dart`

**Interfaces:**
- `CancelLessonScheduleCommand(..., OccurrenceScope? Scope, DateTime? OccurrenceStartAtUtc)` — tekrarlı derste `Scope == null` → `400 scheduling.scope_required`.
- Mobil: tekrarlı derste iptal/erteleme öncesi alt sayfa: **"Yalnız bu ders" / "Bu ve sonrakiler" / "Tüm seri"** (varsayılan seçili: "Yalnız bu ders").

- [ ] **Step 1: Kırmızı testler** — tekrarlı derste kapsamsız iptal → 400; `Single` iptal → yalnız o oluşum `Cancelled`, seri ve geçmiş oluşumlar dokunulmaz; erteleme talebi kabulü → yalnız ilgili oluşum taşınır; mobil sheet varsayılanı `single` ve yük `scope` + `occurrenceStartAtUtc` içerir.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Backend.** **Step 4: Mobil.**
- [ ] **Step 5: Yeşil gör** — `dotnet test EgitimUssu.slnx`, `cd mobile && flutter test && flutter analyze --fatal-infos`
- [ ] **Step 6: Doküman + commit** — `m04`, `doc/pages/scheduling.md`, `doc/pages/lesson_detail.md`; rapor N-01 ✅.
  ```bash
  git commit -m "fix(scheduling): tekrarli derste kapsam zorunlu + mobil 'yalniz bu ders' secimi (N-01)"
  ```

---

### Task 2: "Bu ve sonrakiler" seriyi doğru böler

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`SplitSeriesAt(DateTime cutoffUtc, ...)` → yeni seri)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (reschedule/cancel `ThisAndFuture`)
- Test: `tests/Unit/SeriesSplitTests.cs`

**Interfaces:**
- `ThisAndFuture` erteleme: eski seri `UNTIL = cutoff - 1 oluşum` ile biter; cutoff'tan itibaren **yeni** `LessonSchedule` (yeni saat, aynı öğrenci/konu/kural) oluşur; cutoff sonrası istisnalar yeni seriye **taşınır** (zaman farkı uygulanarak) veya açıkça düşürülür — karar: taşınır, `Cancelled` istisnalar korunur. Olay: `LessonSeriesSplitDomainEvent(OldId, NewId, CutoffUtc)`.
- `ThisAndFuture` iptal: mevcut `EndSeriesBefore` (davranış korunur).

- [ ] **Step 1: Kırmızı testler** — 10 haftalık serinin 4. haftasından itibaren saat değişimi → ilk 3 hafta eski saatte, 4–10 yeni saatte; geçmiş oluşumların oturum/ödeme kayıtları eski seride kalır; 6. haftadaki iptal istisnası yeni seride korunur.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m04`.
  ```bash
  git commit -m "feat(scheduling): 'bu ve sonrakiler' seriyi boler, gecmis olusumlar korunur"
  ```

---

### Task 3: Oluşum anahtarlı ders oturumu (N-02, N-12, N-13)

**Files:**
- Modify: `src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs` (`OccurrenceStartUtc`; yeniden tamamlama olay **yaymaz**)
- Modify: `src/Modules/LessonSessions/Application/LessonSessionFeatures.cs` (`CreateSessionFromScheduleCommand(LessonScheduleId, OccurrenceStartUtc)`; listeleme öğrenci/veli için `IStudentDirectory.GetStudentIdsForUserAsync` + onaylı veli bağı)
- Modify: `src/Modules/LessonSessions/Application/LessonSessionPolicies.cs` (detay: öğrencinin profil kimlikleri / onaylı veli)
- Modify: `src/Modules/LessonSessions/Infrastructure/LessonSessionsDbContext.cs` (benzersiz `(LessonScheduleId, OccurrenceStartUtc)` filtre `LessonScheduleId IS NOT NULL`)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` + tüketicisi (`LessonSessionId` bazlı idempotency; `Absent` katılım sayılmaz)
- Modify (mobil): ders başlatma çağrısı oluşum zamanını gönderir (`lesson_sessions` repository + çağıran ekran)
- Migration: `OccurrenceKeyedSessions` (LessonSessions)
- Test: `tests/Unit/OccurrenceKeyedSessionTests.cs`, `tests/Integration/RealWeeklyLessonSessionTests.cs`

**Interfaces:**
- `POST /api/lesson-sessions/from-schedule` gövdesi `{ lessonScheduleId, occurrenceStartAtUtc }`; tekrarlı derste `occurrenceStartAtUtc` zorunlu (`400 lesson_sessions.occurrence_required`), tekil derste opsiyonel.
- `PlannedStartAtUtc = occurrence` (ertelenmişse override'daki yeni zaman).

- [ ] **Step 1: Kırmızı testler** — haftalık derste 1. hafta oturum aç+tamamla, 2. hafta "başlat" → **yeni** oturum, `PlannedStartAtUtc` = 2. hafta; aynı oluşuma ikinci çağrı → aynı oturum (idempotent); öğrenci kullanıcısı kendi oturumlarını listeler (**boş değil**); veli onaylı çocuğunun oturumlarını görür, onaysız göremez; yeniden tamamlama `AttendedLessonCount`'u artırmaz; `Absent` oturum katılım sayılmaz.
- [ ] **Step 2: Kırmızı gör.**
- [ ] **Step 3: Migration (S-3)** — `Up` sırası: (1) kolon nullable ekle, (2) `UPDATE ... SET "OccurrenceStartUtc" = "PlannedStartAtUtc" WHERE "LessonScheduleId" IS NOT NULL`, (3) aynı `(LessonScheduleId, OccurrenceStartUtc)` tekrarlarında **en erken oluşturulan hariç** satırları `Cancelled` + `OccurrenceStartUtc = NULL` yap ve sayısını `RAISE NOTICE` ile yaz, (4) filtreli benzersiz indeks. `Down` tersini yapar.
  `dotnet ef migrations add OccurrenceKeyedSessions --project src/Modules/LessonSessions/Infrastructure --startup-project src/API.Host --context LessonSessionsDbContext` → `./scripts/check-migration-down.sh`
- [ ] **Step 4: Göç tatbikatı** — `scripts/seed-demo-data.sh` ile staging profili doldurulmuş DB'de migration uygula; tekrar sayısı ve örnek satırlar ILERLEME'ye.
- [ ] **Step 5: Uygula (backend + mobil).** **Step 6: Yeşil gör** — `./scripts/test-with-docker.sh`, `flutter test`
- [ ] **Step 7: Doküman + commit** — `m05`, `m10`, `veri_modeli.md`; rapor N-02/N-12/N-13 ✅.
  ```bash
  git commit -m "fix(lesson-sessions): oturum olusum anahtarli, ogrenci/veli kendi oturumunu gorur, cift sayim yok (N-02, N-12, N-13)"
  ```

---

### Task 4: Tek tamamlama yolu (N-03, K-21)

**Files:**
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (`/complete` ucu kaldırılır)
- Modify (mobil — **ucu kullanan iki ekran var**): `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart` (`:212`), `mobile/lib/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart` (`:150`) → "Tamamla" artık oluşumun oturumunu türetir (`from-schedule`, P17 T3) ve oturumu tamamlar; `mobile/lib/features/scheduling/presentation/cubit/scheduling_cubit.dart`, `.../data/repositories/scheduling_repository_impl.dart`, `.../domain/scheduling_contracts.dart` → `completeLesson` kaldırılır
- Create: `src/Modules/Scheduling/Infrastructure/LessonSessionCompletedSchedulingHandler.cs` (oluşumu `Completed` işaretler: tekil derste ders durumu, tekrarlıda `OccurrenceExceptionAction.Completed`)
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`Completed` oluşum iptal/ertelenemez → `scheduling.occurrence_completed`)
- Modify: `src/Modules/Payments/Infrastructure/LessonSessionCompletedPaymentHandler.cs` ve diğer tüketicilerin `SourceModule` filtresi (yalnız LessonSessions kaynağı kalır — artık tek kaynak)
- Test: `tests/Unit/SingleCompletionPathTests.cs`, `tests/Integration/RealCompletionSyncTests.cs`

- [ ] **Step 1: Kırmızı testler** — oturum tamamlanınca planın o oluşumu `Completed`; tamamlanmış oluşum iptal → 409; rapor aynı dersi hem tamamlanmış hem iptal saymaz; mobil: ders kartında "Tamamla" `from-schedule` + oturum `complete` çağırır, `SchedulingCubit.completeLesson` artık yok (`grep -rn completeLesson mobile/lib` boş).
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m04`, `m05`, `00_genel_bakis.md` (uç envanteri: `/complete` kaldırıldı); rapor N-03 ✅.
  ```bash
  git commit -m "refactor(scheduling): tamamlama yalniz LessonSessions'ta, plan olayla senkron (N-03, K-21)"
  ```

---

### Task 5: İptal politikası ve iptal sonrası temizlik (N-15, N-16)

**Files:**
- Modify: `src/Modules/Teachers/Domain/TeachersDomainModel.cs` (`CancellationPolicy { NoticeHours, ChargePercent }`, varsayılan 0/0 = kapalı) + profil uçları
- Create: `src/Modules/Payments/Infrastructure/LessonCancelledPaymentHandler.cs` (geç iptal → ücret kaydı; aksi → o oluşumun bekleyen ücret kaydını iptal)
- Create: `src/Modules/LessonSessions/Infrastructure/LessonCancelledSessionHandler.cs` (planlanmış oturumu iptal)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (iptal nedeni doğrulaması; `IsChargeable` politikadan hesaplanır, istemci yalnız "ücretsiz iptal et" ile **düşürebilir**; silme olayı `LessonScheduleDeletedDomainEvent` yayılır; gelecek oluşum tamamlanamaz)
- Modify: `src/Modules/Scheduling/Application/LessonChangeRequestFeatures.cs` (oluşum başına tek bekleyen talep; yalnız gelecek ve aktif oluşum; kabul **tek transaction**)
- Modify: `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` — *dokunma* (P18 T3)
- Test: `tests/Unit/CancellationPolicyTests.cs`, `tests/Integration/RealLateCancellationChargeTests.cs`

**Interfaces:**
- `LessonOccurrenceCancelledIntegrationEvent(LessonScheduleId, OccurrenceStartUtc, TeacherUserId, StudentId, IsChargeable, ChargeAmount?, Currency?)`.

- [ ] **Step 1: Kırmızı testler** — politika 24 saat / %100; dersten 3 saat önce iptal → ücret kaydı oluşur; 48 saat önce → bekleyen ücret kaydı iptal; öğretmen "ücretsiz iptal" seçerse ücret yok; geçersiz iptal nedeni → 400; aynı oluşuma ikinci erteleme talebi → 409; geçmiş/iptal edilmiş oluşuma talep → 400; silinen ders için hatırlatma/oturum kalmaz.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör** (`./scripts/test-with-docker.sh`).
- [ ] **Step 5: Doküman + commit** — `m02`, `m04`, `m05`, `m07`, `roles/ogretmen.md`; rapor N-15/N-16 ✅.
  ```bash
  git commit -m "feat(scheduling): iptal politikasi + gec iptal ucreti + iptal sonrasi temizlik (N-15, N-16)"
  ```

---

### Task 6: Oluşum bazlı hatırlatma (N-06)

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/LessonScheduleNotificationIntegrationEventHandler.cs` (Rescheduled / OccurrenceCancelled / Deleted / SeriesSplit tüketimi)
- Modify: `src/Modules/Notifications/Infrastructure/LessonReminderRepository.cs`, `NotificationsDbContext.cs` (anahtar `(LessonScheduleId, OccurrenceStartUtc, RecipientUserId)` benzersiz; `(Status, RemindAtUtc)` kısmi indeks)
- Create: `src/Modules/Notifications/Infrastructure/LessonReminderPlanner.cs` (`LeasedBackgroundService`; her tur önümüzdeki 7 günün oluşumları için eksik hatırlatmaları üretir — `ILessonScheduleReader` sözleşmesiyle)
- Modify: `src/Shared/Contracts/LessonScheduleReadContract.cs` (pencere için oluşum okuma)
- Migration: `OccurrenceReminders` (Notifications) — mevcut bekleyen hatırlatmalar `OccurrenceStartUtc = ders StartAtUtc` ile geri doldurulur
- Test: `tests/Unit/LessonReminderPlannerTests.cs`, `tests/Integration/RealOccurrenceReminderTests.cs`

**Interfaces:**
- Alıcılar: öğretmen + öğrenci kullanıcısı + (tercihi açık) onaylı veli. Tercih kapısı (M11-5) aynen.
- Başlangıcı geçmiş oluşum için hatırlatma **üretilmez**; geciken gönderimde `RemindAt + 30 dk` geçmişse `Expired`.

- [ ] **Step 1: Kırmızı testler** — 15:00 → 18:00 erteleme: 14:45 hatırlatması iptal, 17:45 üretilir; silinen ders → bekleyen hatırlatma iptal; haftalık seride 3 hafta boyunca her oluşum için hatırlatma; geçmiş tarihli ders oluşturma → hatırlatma yok; metin TR saatiyle (P16 T5).
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m11`; rapor N-06 ✅.
  ```bash
  git commit -m "fix(notifications): ders hatirlatmasi olusum bazli, erteleme/silme izlenir, ogrenci ve veliye de gider (N-06)"
  ```

---

### Task 7: Oluşum bazlı öğretmen panosu projeksiyonu (N-49, N-48 iptal sırası)

**Files:**
- Modify: `src/Modules/Reporting/Domain/TeacherDashboardProjection.cs`, `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionHandlers.cs`
- Modify: `src/Modules/Reporting/Application/ReportingFeatures.cs` (yerel "bugün")
- Modify: `tests/Architecture/LocalTimeRuleTests.cs` — *2026-11-15 eklendi (P16 T6):* izinli listedeki iki N-49 girişini (`ReportingFeatures.cs` `_clock.UtcNow.Date`, `TeacherDashboardProjection.cs` `dayUtc.Date`) **sil**; yerel "bugün" yazılınca bayat kalırlar ve test zaten kırılır
- Test: `tests/Unit/TeacherDashboardOccurrenceTests.cs`

**Interfaces:**
- Satır anahtarı `(TeacherUserId, LessonScheduleId, OccurrenceStartUtc)`; kayan pencere (dün → +14 gün); `Cancelled` **tombstone** (sonradan gelen `Scheduled` olayı tombstone'u ezmez); günlük budama.

- [ ] **Step 1: Kırmızı testler** — haftalık ders her hafta "bugünkü dersler"de; `Cancelled` önce, `Scheduled` sonra işlenirse ders panoya **dönmez**; 30 gün önceki satırlar budanır.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör** + `RealProjectionRebuildTests` yeşil.
- [ ] **Step 5: Doküman + commit** — `m14`; rapor N-49, N-48 (iptal kısmı) ✅.
  ```bash
  git commit -m "fix(reporting): ogretmen panosu olusum bazli + iptal tombstone (N-49, N-48)"
  ```

---

### Task 8: Kapanış — haftalık ders senaryosu

**Files:**
- Create: `tests/Integration/RealWeeklyLessonLifecycleScenarioTests.cs`
- Create: `mobile/test/features/scheduling/lesson_occurrence_flow_test.dart`

- [ ] **Step 1: Senaryo** (gerçek Postgres + outbox çalışır): öğretmen 24 saat/%100 iptal politikasıyla haftalık ders açar → 1. hafta oturum + tamamla → ücret kaydı 1 → 2. hafta **yalnız bu** iptal (3 saat kala) → geç iptal ücreti → 3. hafta 18:00'e ertele → hatırlatma 17:45 → 3. hafta oturum + tamamla → ücret kaydı 2 → panoda ve aylık raporda doğru sayılar; öğrenci ve veli 2 tamamlanmış oturum görür.
- [ ] **Step 2: Koş** — `./scripts/test-with-docker.sh`, `flutter test` → başarısız 0, atlanan 0.
- [ ] **Step 3: Doküman kapanışı** (`roles/ogretmen.md` akış diyagramı) + ILERLEME.
  ```bash
  git commit -m "test(lessons): haftalik ders yasam dongusu senaryosu, P17 kapanisi"
  ```
