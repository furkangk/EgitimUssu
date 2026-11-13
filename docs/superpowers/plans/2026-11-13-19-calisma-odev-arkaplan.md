# P19 — Çalışma, Ödev ve Arka Plan Doğruluğu (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrencinin "planlı çalışma" motivasyonu güvenilir olsun (seri doğru sayılsın, silinen kayıt istatistikte kalmasın), ödev akışı tutarlı olsun (puanlanmış teslim kaybolmasın, geç teslim görünsün, uyarılar aksamasın) ve arka plan altyapısı bir modülün hatasıyla diğerlerini aç bırakmasın.

**Architecture:** Seri hesabı mevcut seansı dahil ederek ve `ILocalCalendar` tabanlı 04:00 gün sınırıyla yapılır; okumada kırılmış seri sıfır döner. `DispatchingEventBus` handler'ları birbirinden yalıtır; inbox idempotency'si sayesinde yeniden denemede başarılı handler'lar atlanır. Ödev durumları domain'de geçiş tablosuyla korunur; teslimler sürümlenir. Düzenleme/silme olayları tüketicilerde yeniden hesaplamayı tetikler.

**Tech Stack:** .NET 9, EF Core 9 / Npgsql 9, Flutter (T7), xUnit, Testcontainers.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — §1 (sahte depo yasağı, senaryo testi). **Kaynak:** rapor §1.3 (N-40, N-42, N-44, N-45, N-47, N-48 trend, N-51…N-55).

**Dal:** `feat/p19-calisma-odev` · **Ön koşul:** P15 T8 (kira düzeltmesi), P16 T1 (`ILocalCalendar`).

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Önceki tüm sözleşmeler.
- **N-40 dersi:** persistence sırasına bağlı davranışlar sahte depoyla değil EF InMemory veya Testcontainers ile test edilir.
- Olay sözleşmesi değişiklikleri **ekleme** biçiminde (yeni olay türü), mevcut olay şekilleri bozulmaz.
- Yeniden hesaplama yapan tüketiciler idempotent tabandan türer.

---

### Task 1: Çalışma serisi (streak) düzeltmesi (N-40)

**Files:**
- Modify: `src/Modules/Study/Application/StudySessionFeatures.cs` (`RecordCompletedAsync`: günlük toplam = DB'deki tamamlanmış seanslar **+ mevcut seans** (zaten listede değilse); pencere 04:00 seri gününe hizalı)
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` (`StudyStreak.RegisterStudyDay`: aynı gün ikinci kez sayılmaz; geçmiş gün girişi `TotalStudyDays`'i yalnız yeni günse artırır; seri yeniden hesap metodu)
- Modify: `src/Modules/Study/Application/StudyProgressFeatures.cs` (okuma: son seri günü < dün → `CurrentStreakDays = 0`)
- Modify: `src/Modules/Study/Application/StudyPolicies.cs` (`StudyLocalTime` → P16 T1 sonrası `ILocalCalendar`)
- Modify: `tests/Unit/StudyStreakTests.cs` (sahte depo `Seed(session)` hilesi kaldırılır)
- Create: `src/Modules/Study/Infrastructure/StudyStreakRepairJob.cs` (tek seferlik: tüm öğrencilerin serisini seanslardan yeniden hesaplar; `Study:RepairStreaksOnStartup` bayrağıyla, idempotent)
- Test: `tests/Unit/StudyStreakEfTests.cs` (EF InMemory), `tests/Integration/RealStudyStreakTests.cs`

- [ ] **Step 1: Kırmızı testler** — günde tek 90 dk seans (hedef 60) → seri **1**; manuel seans aynı; TR 01:30'da biten seans **önceki seri gününe** sayılır; aynı geçmiş güne iki manuel giriş → `TotalStudyDays` +1; 10 gün önce bırakan öğrenci → okumada **0**; onarım işi iki kez koşunca aynı sonuç.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Uygula.** **Step 4: Onarım işini staging demo verisinde koş**, önce/sonra seri değerlerini ILERLEME'ye yaz.
- [ ] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 6: Doküman + commit** — `m08_study.md` (seri kuralı); rapor N-40 ✅.
  ```bash
  git commit -m "fix(study): seri tamamlanan seansi sayar, 04:00 gun siniri, kirilan seri sifirlanir + onarim isi (N-40)"
  ```

---

### Task 2: Outbox handler izolasyonu ve dönüş anlamı (N-42)

**Files:**
- Modify: `src/Shared/Infrastructure/Messaging/DispatchingEventBus.cs` (her handler ayrı `try/catch`; hepsi denenir; en az biri başarısızsa `AggregateException` → mesaj yeniden denenir)
- Modify: `src/Shared/Infrastructure/Persistence/EfOutboxStore.cs` (dead-letter kaydı hangi handler'ın düştüğünü yazar)
- Modify: `src/Shared/Infrastructure/Projections/ProjectionHandler.cs`, `src/Modules/Notifications/Infrastructure/MessageSentNotificationHandler.cs` (`return false` → bozuk yük için açık `PoisonMessageException`, geçici durum için istisna)
- Audit: tüm `IIntegrationEventHandler` implementasyonlarını listele (`grep -rln "IIntegrationEventHandler" src/Modules`) ve **inbox idempotency tabanı kullanmayanları** tabana geçir (yeniden denemede tekrar çalışmaları güvenli olmalı)
- Test: `tests/Unit/DispatchingEventBusIsolationTests.cs`, `tests/Integration/RealOutboxHandlerIsolationTests.cs`

- [ ] **Step 1: Kırmızı testler** — 4 handler'lı olayda 2. handler kalıcı hata verir → 1, 3, 4 **çalışır**; yeniden denemede 1, 3, 4 inbox sayesinde **tekrar işlemez**, 2 tekrar denenir; bozuk yük → dead-letter'da handler adı; `return false` kullanan handler kalmaz (kaynak taraması).
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör** (`./scripts/test-with-docker.sh`).
- [ ] **Step 5: Doküman + commit** — `doc/architecture/backend.md` (outbox teslim semantiği); rapor N-42 ✅.
  ```bash
  git commit -m "fix(outbox): handler'lar birbirinden yalitildi, dead-letter handler adini tasir (N-42)"
  ```

---

### Task 3: Ödev durum makinesi ve teslim sürümleri (N-44)

**Files:**
- Modify: `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs` (geçiş tablosu; `SubmittedOnUtc`, `IsLate`; `AssignmentSubmissionVersion` çocuk entity; puanlanmış dosya silinmez)
- Modify: `src/Modules/Assignments/Application/AssignmentStudentFeatures.cs`, `AssignmentFeatures.cs`
- Migration: `AssignmentStateAndSubmissions` (mevcut teslimler sürüm 1 olarak taşınır; `SubmittedOnUtc` = son teslim güncelleme zamanı)
- Test: `tests/Unit/AssignmentStateMachineTests.cs`

**Interfaces:**
- İzinli geçişler: `Assigned → Submitted | Cancelled`, `Submitted → Approved | ReturnedForRevision | Submitted (yeni sürüm)`, `ReturnedForRevision → Submitted`, `Approved → (son)`, `Cancelled → (son)`. Aksi `400 assignments.invalid_transition`.
- `MarkCompleted` (dosyasız ödev) yalnız `Assigned → Submitted` gibi davranır.
- `ReturnForRevision` puanı **sıfırlar**; puan geçmişi sürümde kalır.
- `IsLate = SubmittedOnUtc > DueAtUtc` (yerel gün sonu kuralıyla).

- [ ] **Step 1: Kırmızı testler** — onaylanmış ödeve yeni dosya → 400, eski dosya **duruyor**; `MarkCompleted` `ReturnedForRevision`'ı ezemez; teslimsiz ödev onaylanamaz; revizyona dönen ödevde `Score == null`; son tarihten sonra teslim `IsLate == true`.
- [ ] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m06_assignments.md`; rapor N-44 ✅.
  ```bash
  git commit -m "fix(assignments): durum makinesi, teslim surumleri, gec teslim isareti (N-44)"
  ```

---

### Task 4: Son tarih tarayıcısı (N-45)

**Files:**
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentDueScanner.cs` (sorgu: `(DueSoonNotifiedOnUtc IS NULL AND DueAtUtc > now AND DueAtUtc <= now+24h) OR (MissedNotifiedOnUtc IS NULL AND DueAtUtc <= now AND SubmittedOnUtc IS NULL)`; pencere dışı kalan eski "az kaldı" adayları **atlanır ve işaretlenir**)
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentsDbContext.cs` (iki kısmi indeks)
- Test: `tests/Unit/AssignmentDueScannerTests.cs` (genişlet), `tests/Integration/RealAssignmentDueScannerTests.cs`

- [ ] **Step 1: Kırmızı testler** — 250 eski bekleyen ödev varken yeni ödev için "az kaldı" uyarısı **gider**; zamanında teslim edilip son tarihten sonra revizyona dönen ödev için "teslim edilmedi" **gitmez**.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Commit** — rapor N-45 ✅.
  ```bash
  git commit -m "fix(assignments): son tarih taramasi aclik yasamaz, teslim edilmis odeve yanlis uyari gitmez (N-45)"
  ```

---

### Task 5: Düzenleme/silme olayları ve tarih sıralı trend (N-47, N-48 trend)

**Files:**
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` (`StudySessionEdited/Deleted`, `TestResultEdited/Deleted` domain olayları)
- Modify: `src/Modules/Study/Application/StudyTestFeatures.cs`, `StudySessionFeatures.cs` (olay yayımı; `MockExam.TotalNet` yeniden hesap)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` (+ tüketici: `TopicMastery` yeniden hesap; trend `TakenOnUtc` sırasına göre)
- Modify: `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs`, `src/Modules/Reporting/Domain/ReportingProjections.cs` (düzenleme/silme yansır; yanlış yorum düzeltilir)
- Test: `tests/Unit/StudyEditDeletePropagationTests.cs`, `tests/Integration/RealStudyEditPropagationTests.cs`

- [ ] **Step 1: Kırmızı testler** — yanlış girilen test silinince `TopicMastery`, Reporting haftalık toplamı ve deneme `TotalNet` düşer; düzenleme yeni değeri yansıtır; geriye dönük girilen eski test trendi tersine **çevirmez**.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör** + projeksiyon yeniden kurulumu testi yeşil.
- [ ] **Step 5: Doküman + commit** — `m08`, `m10`, `m14`; rapor N-47/N-48 (trend) ✅.
  ```bash
  git commit -m "fix(study): duzenleme/silme olaylari istatistiklere yansir, trend tarih sirali (N-47, N-48)"
  ```

---

### Task 6: Bildirim kuyruğu, özetler ve küçük doğruluk hataları (N-51, N-52, N-53, N-55)

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/UserNotificationRepository.cs`, `NotificationDispatching.cs` (`PushAttempts`, `NextAttemptOnUtc`, `ExpiresOnUtc`; üstel geri çekilme; süresi dolan `Expired`; tercih ve token'lar **toplu** okunur)
- Modify: `src/Modules/Notifications/Infrastructure/MessageSentNotificationHandler.cs` (gövde: "{gönderen} size mesaj gönderdi" — mesaj metni **yok**)
- Modify: `src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs` (`(veli, öğrenci)` başına özet; sayfalı okuma)
- Modify: `src/Modules/Study/Application/ExamPenalty.cs` (YDS bölen 0 = ceza yok)
- Modify: `src/Modules/Messaging/Application/MessagingFeatures.cs` (okundu: `upToMessageId` parametresi; yalnız o mesaja kadar)
- Modify: `src/Modules/Study/**` (TopicGoal: hedef tarihi geçen ve tamamlanmamış hedef `Missed` — mevcut bir tarayıcıya eklenir)
- Migration: `NotificationDeliveryBackoff` (Notifications)
- Test: `tests/Unit/NotificationBackoffTests.cs`, `tests/Unit/ParentWeeklySummaryPerChildTests.cs`, `tests/Unit/ExamPenaltyTests.cs` (genişlet), `tests/Unit/MessageReadMarkerTests.cs`, `tests/Unit/TopicGoalMissedTests.cs`

- [ ] **Step 1: Kırmızı testler** — en eski 100 bildirim kalıcı geçici-hata verirken yeni bildirim gönderilir; 2 saatlik "ders başlıyor" bildirimi `Expired`; tur başına sorgu sayısı sabit (N+1 yok — EF komut sayımı); silinmiş mesajın metni bildirim merkezinde **yok**; iki çocuklu veli iki özet alır; YDS'de yanlış doğruyu götürmez; okundu işareti verilen mesaj ID'sinden sonrasını okundu yapmaz; tarihi geçen hedef `Missed`.
- [ ] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m11`, `m08`, `m16`; rapor N-51/52/53/55 ✅.
  ```bash
  git commit -m "fix(notifications): push geri cekilme + sure, toplu okuma, mesaj metni sizmaz, cocuk basina ozet (N-51..N-53, N-55)"
  ```

---

### Task 7: Sayfalama ve indeksler (N-54, N-72 bildirim sayfalaması)

**Files:**
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentRepository.cs` + uçlar (keyset `?after=&limit=`)
- Modify: `src/Modules/Study/Infrastructure/StudyRepository.cs` + uçlar (seans, test, not listeleri)
- Modify: `src/Modules/ProgressTracking/Infrastructure/ProgressSnapshotService.cs` (öğrenci partileri halinde)
- Modify (mobil): ödev listesi, çalışma geçmişi, bildirim merkezi → sonsuz kaydırma (`notification_center_cubit.dart` dahil)
- Test: `tests/Unit/KeysetPaginationTests.cs`, ilgili mobil cubit testleri

**Interfaces:**
- Yanıt: `PagedResult<T> { items, nextCursor }` (mevcut `Shared/Kernel/PagedResult.cs` kullanılır). Sayfa parametresi verilmezse **geriye uyumlu** ilk sayfa (varsayılan 50).

- [ ] **Step 1–4: Kırmızı gör → uygula → yeşil gör** (`dotnet test`, `flutter test`).
- [ ] **Step 5: Doküman + commit** — ilgili `mNN` uç envanterleri, `doc/pages/notifications.md`; rapor N-54 ✅.
  ```bash
  git commit -m "perf: odev, calisma ve bildirim listelerinde keyset sayfalama (N-54)"
  ```

---

### Task 8: Kapanış — öğrenci haftası senaryosu

**Files:**
- Create: `tests/Integration/RealStudentWeekScenarioTests.cs`

- [ ] **Step 1: Senaryo** — öğrenci 3 gün çalışır (biri TR 01:30'da biter) → seri 3; bir test girer, siler → istatistik eski haline döner; ödev teslim eder, öğretmen revizyona döndürür, yeniden teslim eder, onaylar → puan ve iki sürüm; veli haftalık özette bunları görür; Payments handler'ı bilerek hata verdiğinde Reporting yine güncellenir.
- [ ] **Step 2: Koş** — `./scripts/test-with-docker.sh`, `flutter test`.
- [ ] **Step 3: Doküman + ILERLEME + commit**
  ```bash
  git commit -m "test(study): ogrenci haftasi senaryosu, P19 kapanisi"
  ```
