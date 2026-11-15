# P19 — Çalışma, Ödev ve Arka Plan Doğruluğu (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrencinin "planlı çalışma" motivasyonu güvenilir olsun (seri doğru sayılsın, silinen kayıt istatistikte kalmasın), ödev akışı tutarlı olsun (puanlanmış teslim kaybolmasın, geç teslim görünsün, uyarılar aksamasın) ve arka plan altyapısı bir modülün hatasıyla diğerlerini aç bırakmasın.

**Architecture:** Seri hesabı mevcut seansı dahil ederek ve `ILocalCalendar` tabanlı 04:00 gün sınırıyla yapılır; okumada kırılmış seri sıfır döner. `DispatchingEventBus` handler'ları birbirinden yalıtır; inbox idempotency'si sayesinde yeniden denemede başarılı handler'lar atlanır. Ödev durumları domain'de geçiş tablosuyla korunur; teslimler sürümlenir. Düzenleme/silme olayları tüketicilerde yeniden hesaplamayı tetikler.

**Tech Stack:** .NET 9, EF Core 9 / Npgsql 9, Flutter (T7), xUnit, Testcontainers.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — §1 (sahte depo yasağı, senaryo testi). **Kaynak:** rapor §1.3 (N-40, N-42, N-44, N-45, N-47, N-48 trend, N-51…N-55).

**Dal:** `feat/p19-calisma-odev` · **Ön koşul:** P15 T8 (kira düzeltmesi), P16 T1 (`ILocalCalendar`).

> **Devralınan (P15 T9, 2026-11-14 — ILERLEME Öğrenilenler #189):** Reporting, Notifications ve Identity `StudentProfilesMerged` olayını tüketmiyor — birleşme sonrası eski öğrenci kimliğinde kalan projeksiyon/bildirim satırı olabilir (Messaging P15 T5'te kapandı). **Task 8'in Step 0'ı** olarak ele alınır: önce bu üç modülde öğrenci kimliğiyle anahtarlanan tablolar ölçülür; gerçekten varsa tüketici (çakışmayan taşınır, çakışanda kanonik kazanır — #187) + senaryoya birleşme adımı eklenir, yoksa ILERLEME'de "ölçüldü, gerek yok" diye kapanır.

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Önceki tüm sözleşmeler.
- **N-40 dersi:** persistence sırasına bağlı davranışlar sahte depoyla değil EF InMemory veya Testcontainers ile test edilir.
- Olay sözleşmesi değişiklikleri **ekleme** biçiminde (yeni olay türü), mevcut olay şekilleri bozulmaz.
- Yeniden hesaplama yapan tüketiciler idempotent tabandan türer.

---

### Task 1: Çalışma serisi (streak) düzeltmesi (N-40) ✅

**Files:**
- Modify: `src/Modules/Study/Application/StudySessionFeatures.cs` (`RecordCompletedAsync`: günlük toplam = DB'deki tamamlanmış seanslar **+ mevcut seans** (zaten listede değilse); pencere 04:00 seri gününe hizalı)
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` (`StudyStreak.RegisterStudyDay`: aynı gün ikinci kez sayılmaz; geçmiş gün girişi `TotalStudyDays`'i yalnız yeni günse artırır; seri yeniden hesap metodu)
- Modify: `src/Modules/Study/Application/StudyProgressFeatures.cs` (okuma: son seri günü < dün → `CurrentStreakDays = 0`)
- Modify: `src/Modules/Study/Application/StudyPolicies.cs` (`StudyLocalTime` → P16 T1 sonrası `ILocalCalendar`)
- Modify: `tests/Unit/StudyStreakTests.cs` (sahte depo `Seed(session)` hilesi kaldırılır)
- Create: `src/Modules/Study/Infrastructure/StudyStreakRepairJob.cs` (tek seferlik: tüm öğrencilerin serisini seanslardan yeniden hesaplar; `Study:RepairStreaksOnStartup` bayrağıyla, idempotent)
- Test: `tests/Unit/StudyStreakEfTests.cs` (EF InMemory), `tests/Integration/RealStudyStreakTests.cs`

- Create: `src/Modules/Study/Application/StudyStreakCalculator.cs` (seanslardan tekil uygun günleri türetme; yeni depo arayüzü yok)
- Modify: `src/Modules/Study/Infrastructure/DependencyInjection.cs` (onarım servisi ve tek seferlik startup işi)
- Modify: `src/Modules/Study/Infrastructure/StudyDigestDirectory.cs` (veli özeti de kırılmış seriyi sıfır okur)
- Docs: `doc/modules/m08_study.md`, `doc/modules/00_genel_bakis.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`

**Interfaces / koddan doğrulama (2026-11-15):** `IStudyRepository` değişmez; mevcut `ListCompletedSessionsAsync` EF sorgusu yalnız kaydedilmiş Completed satırları döndürür. Hesaplayıcı mevcut seansı kimliğiyle birleştirir ve 04:00 tabanlı günlere gruplar. Geçmiş günün tekilliği yalnız son-tarih sayacıyla ispatlanamaz: `RegisterStudyDay` uygun gün kümesini alır, `Recalculate` sıralı tekil günlerden sayaçları yeniden kurar. Mevcut hedef geçmişi tutulmadığından yeniden hesap güncel aktif hedefi (yoksa 20 dk) kullanır. `CurrentAt(studyDay)` tüm seri okumalarının ortak kuralıdır; günlük dakika istatistiği gece yarısını korur. Onarım `IHostedService` olarak seed/migration sonrası, HTTP öncesi ve yalnız bayrak açıkken çalışır; PostgreSQL transaction advisory lock + kalıcı `module_states` işareti, veri/sayaçlar atomik. Şema değişmez, migration gerekmez. Onarım eski rozetleri geri almaz ve geçmiş bildirim olaylarını yeniden üretmez. T1 düzenleme/silme akışına dokunmaz (T5).

- [x] **Step 1: Kırmızı testler** — günde tek 90 dk seans (hedef 60) → seri **1**; manuel seans aynı; TR 01:30'da biten seans **önceki seri gününe** sayılır; aynı geçmiş güne iki manuel giriş → `TotalStudyDays` +1; 10 gün önce bırakan öğrenci → okumada **0**; onarım işi iki kez koşunca aynı sonuç.
- [x] **Step 2: Kırmızı gör.** **Step 3: Uygula.** **Step 4: Onarım işini staging demo verisinde koş**, önce/sonra seri değerlerini ILERLEME'ye yaz.
- [x] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [x] **Step 6: Doküman + commit** — `m08_study.md` (seri kuralı); rapor N-40 ✅.
  ```bash
  git commit -m "fix(study): seri tamamlanan seansi sayar, 04:00 gun siniri, kirilan seri sifirlanir + onarim isi (N-40)"
  ```

---

**Kanıt (2026-11-15):** İmza Release derlemesi 0 uyarı/0 hata; EF testleri gerçek RED **7 başarısız/2 başarılı** → GREEN. Son `bash scripts/test-with-docker.sh`: **19 mimari/1238 birim/177 integration**, başarısız **0**, atlanan **0**. Release `-warnaserror --no-incremental` **0/0**, tool restore başarılı. Mobil/migration değişmedi. Ayrı yerel Staging PostgreSQL'de **12 demo seans/2 öğrenci**; kontrollü eski `99/99/99` → iki öğrenci de **0/1/6**. Marker yazımına yapay hata: ara kayıtlar rollback, marker yok; başarılı startup sonrası ikinci çağrı ve restart aynı değerleri/tarihleri korur. İlk tam turdaki global snapshot sayımı **2→4** test izolasyonuydu; yeni HTTP testi kendi bekleyen olaylarını temizler, eski test/üretim değiştirilmedi. Doc health RED0/mevcut2YELLOW+1BLUE. Loglar `/tmp/egitimussu-p19t1-{signature,red,green,real2,staging-proof,docker-final,release,tool-restore,doc-health}.log`.

### Task 2: Outbox handler izolasyonu ve dönüş anlamı (N-42) ✅

**Files:**
- Modify: `src/Shared/Infrastructure/Messaging/DispatchingEventBus.cs` (her handler ayrı `try/catch`; hepsi denenir; en az biri başarısızsa `AggregateException` → mesaj yeniden denenir)
- Modify: `src/Shared/Infrastructure/Persistence/EfOutboxStore.cs` (dead-letter kaydı hangi handler'ın düştüğünü yazar)
- Modify: `src/Shared/Infrastructure/Projections/ProjectionHandler.cs`, `src/Modules/Notifications/Infrastructure/MessageSentNotificationHandler.cs` (`return false` → bozuk yük için açık `PoisonMessageException`, geçici durum için istisna)
- Audit: tüm `IIntegrationEventHandler` implementasyonlarını listele (`grep -rln "IIntegrationEventHandler" src/Modules`) ve **inbox idempotency tabanı kullanmayanları** tabana geçir (yeniden denemede tekrar çalışmaları güvenli olmalı)
- Test: `tests/Unit/DispatchingEventBusIsolationTests.cs`, `tests/Integration/RealOutboxHandlerIsolationTests.cs`

**Kapsam doğrulaması / Interfaces (2026-11-15):** Kayıtlı tüm tüketiciler zaten `IdempotentIntegrationEventHandler` / `ProjectionHandler` tabanındadır; dönüşüm gerekmiyor. `ApplyAsync/ProjectAsync: Task<bool>` imzası korunur: `true` hem uygulanan işi hem geçerli etkisiz sonucu inbox'a yazar; `false` artık tabanda açık hata verir. Bozuk JSON/null/kimlik → `PoisonMessageException`, geçici kaynak yokluğu → istisna, ücret/tercih/öğretmensiz ders/zaten uygulanmış durum → `true`. Poison dahil tüm hatalar mevcut retry sınırına tabidir; bir poison kardeş geçici hatanın yeniden denenmesini erken kesmez. `DispatchingEventBus` mevcut scope'u korur, taban her başarısızlıkta tracker'ı temizler (rollback tek başına yeterli değil). Aggregate her başarısız handler adını taşır; dead-letter Error önce handler adlarını/türlerini yazar, yükü hata metnine kopyalamaz. İptal edilmiş tur derhal yayılır; token iptal edilmeden atılan OperationCanceledException handler hatasıdır.

**Files — doğrulamada eklenenler:**
- Create: `src/Shared/Infrastructure/Messaging/IntegrationEventExceptions.cs` (poison ve handler adı taşıyan hata)
- Modify: `src/Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs` (false açık hata, JSON hata sınıflaması, başarısız tracker temizliği)
- Modify: aşağıdaki mevcut handler dosyaları (false nedenlerinin tek tek sınıflanması):
  - `src/Modules/Assignments/Infrastructure/AssignmentsStudentMergedHandler.cs`
  - `src/Modules/Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs`
  - `src/Modules/LessonSessions/Infrastructure/LessonCancelledSessionHandler.cs`
  - `src/Modules/LessonSessions/Infrastructure/LessonSessionsStudentMergedHandler.cs`
  - `src/Modules/Messaging/Infrastructure/MessagingStudentMergedHandler.cs`
  - `src/Modules/Notifications/Infrastructure/AssignmentDueNotificationHandlers.cs`
  - `src/Modules/Notifications/Infrastructure/LessonScheduleNotificationIntegrationEventHandler.cs`
  - `src/Modules/Notifications/Infrastructure/MessageSentNotificationHandler.cs`
  - `src/Modules/Notifications/Infrastructure/ParentEventNotificationHandler.cs`
  - `src/Modules/Notifications/Infrastructure/PaymentDeclarationResolvedNotificationHandler.cs`
  - `src/Modules/Notifications/Infrastructure/PaymentOverdueNotificationHandler.cs`
  - `src/Modules/Notifications/Infrastructure/TopicGoalAchievedNotificationHandler.cs`
  - `src/Modules/Parents/Infrastructure/ParentReadModelProjections.cs`
  - `src/Modules/Parents/Infrastructure/StudentProfilesMergedParentsHandler.cs`
  - `src/Modules/Payments/Infrastructure/LessonCancelledPaymentHandler.cs`
  - `src/Modules/Payments/Infrastructure/LessonSessionCompletedPaymentHandler.cs`
  - `src/Modules/Payments/Infrastructure/PaymentsStudentMergedHandler.cs`
  - `src/Modules/ProgressTracking/Infrastructure/LessonSessionCompletedProgressHandler.cs`
  - `src/Modules/ProgressTracking/Infrastructure/StudentProfilesMergedProgressHandler.cs`
  - `src/Modules/ProgressTracking/Infrastructure/StudyProgressIntegrationEventHandlers.cs`
  - `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs`
  - `src/Modules/Reporting/Infrastructure/TeacherDashboardProjectionHandlers.cs`
  - `src/Modules/Scheduling/Infrastructure/LessonSessionCompletedSchedulingHandler.cs`
  - `src/Modules/Scheduling/Infrastructure/SchedulingStudentMergedHandler.cs`
  - `src/Modules/Students/Infrastructure/ParentChildLinkApprovedIntegrationEventHandler.cs`
  - `src/Modules/Students/Infrastructure/ParentChildLinkEndedIntegrationEventHandler.cs`
  - `src/Modules/Study/Infrastructure/StudyStudentMergedHandler.cs`
- Modify: `tests/Unit/ProjectionHandlerTests.cs`, `tests/Unit/IdempotentIntegrationEventHandlerTests.cs`, `tests/Unit/MessageSentNotificationHandlerTests.cs`, `tests/Unit/LessonSessionCompletedPaymentHandlerTests.cs` (artık sessiz başarı beklenmez)
- Docs: `doc/architecture/backend.md`, `doc/modules/00_genel_bakis.md` ve yukarıdaki dosyaların modül belgeleri (`m03`, `m04`, `m05`, `m06`, `m07`, `m08`, `m09`, `m10`, `m11`, `m14`, `m16`), N-42 denetim satırı.

- [x] **Step 1: Kırmızı testler** — 4 handler'lı olayda 2. handler kalıcı hata verir → 1, 3, 4 **çalışır**; yeniden denemede 1, 3, 4 inbox sayesinde **tekrar işlemez**, 2 tekrar denenir; bozuk yük → dead-letter'da handler adı; `return false` kullanan handler kalmaz (kaynak taraması).
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör** (`./scripts/test-with-docker.sh`).
- [x] **Step 5: Doküman + commit** — `doc/architecture/backend.md` (outbox teslim semantiği); rapor N-42 ✅.
  ```bash
  git commit -m "fix(outbox): handler'lar birbirinden yalitildi, dead-letter handler adini tasir (N-42)"
  ```

---

**Kanıt (2026-11-15):** İmza Release derlemesi 0 uyarı/0 hata; gerçek birim RED **7 başarısız/11 başarılı** → GREEN. Son `bash scripts/test-with-docker.sh` (exit 0): **19 mimari/1242 birim/183 integration**, başarısız **0**, atlanan **0**. Yeni 4 birim + 6 gerçek PostgreSQL vaka; 4 handler'da ikinci hata, SQL rollback + staged sızıntı yok, diğerleri bir kez; ikinci teslimde yalnız başarısız handler ve hem toparlanma hem dead-letter kanıtlandı. Null/bozuk JSON/boş alıcı gerçek MessageSent tüketicisinde handler adı + PoisonMessageException ile dead-letter, inbox yok, yük hata metnine sızmıyor. Tüm kayıtlı üretim tüketicileri inbox tabanında; kaynak taramasında belirsiz false yok. Ücretsiz veya ücreti tanımlanmamış ders inbox'a başarılı etkisiz kayıt, okunamayan kaynak oturum açık hata. Son Release `-warnaserror --no-incremental` 0/0; tool restore başarılı. Doc health RED0/mevcut2YELLOW+1BLUE (dokunulan M16 tarih uyumsuzluğu da giderildi). Mobil/migration yok; Flutter/drift/Down gerekmedi. Loglar `/tmp/egitimussu-p19t2-{baseline,signature,red,green,real-first,docker,docker-final,release,tool-restore,doc-health-final}.log`.

### Task 3: Ödev durum makinesi ve teslim sürümleri (N-44) ✅

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

**Koddan doğrulanan kapsam / kararlar (2026-11-15):**
- Modify: `src/Modules/Assignments/Application/AssignmentTeacherFeatures.cs` (Result geçiş hatalarının yayılması)
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentsDbContext.cs` (çocuk composite anahtar/FK + AutoInclude), `AssignmentsPersonalDataAnonymizer.cs` (tüm sürüm dosyaları ve geri bildirimleri de temizlenir), `AssignmentRepository.cs` (yalnız çocuk sürüm PK yarışını xmin gibi 409 yapar)
- Modify: `src/Modules/Assignments/API/AssignmentsModule.cs` (mevcut attachment ucuna isteğe bağlı `version` sorgusu)
- Modify: `tests/Unit/AssignmentTests.cs`, `AssignmentSubmissionStorageTests.cs` (takvim imzaları, eski dosya artık korunur)
- Create: `tests/Integration/RealAssignmentSubmissionTests.cs` (gerçek HTTP/dosya/PG, yetki, concurrency, anonimleştirme ve ayrı DB göç Up/Down/Up kanıtı)
- Migration files: `src/Modules/Assignments/Infrastructure/Migrations/*_AssignmentStateAndSubmissions.cs`, aynı `.Designer.cs` ve `AssignmentsDbContextModelSnapshot.cs`.
- Docs: `doc/modules/m06_assignments.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/roles/ogrenci.md`, `doc/roles/ogretmen.md`, N-44 denetim satırı.

**Interfaces — geriye uyum:** `IAssignmentRepository` değişmez (gerçek depo + iki test sahtesi doğrulandı). Durum adları/değerleri korunur: Assigned=`Pending`; Submitted=`InProgress` (dosya) veya `Completed` (dosyasız). İkisi de onay/revizyon/yeni dosya kabul eder; `MarkCompleted` yalnız Pending'den. Approved/Cancelled terminaldir; Cancel yalnız Pending'den domain'de, yeni iptal ucu yok. `MarkCompleted/SubmitWork/AttachSubmission/Approve/ReturnForRevision` Result döner; ilk üçü `ILocalCalendar` alır. Teslim anı/IsLate son sürümden; son tarih yerel günün son mikro-saniyesi (`DayRangeUtc.EndUtc - 10 tick`), gün sınırı yalnız takvimden. Çocuk `(AssignmentId, Number)` anahtarlı; tüm dosyalar korunur, aynı attachment ucunda `?version=N` ile yetkili indirilir; response `SubmissionVersions` dosya anahtarı sızdırmaz. Puan yalnız Approved'da, yeniden puanlama son sürüme yazılır; revizyon aggregate puanını temizler, tarihî sürüm puanını tutar. Approved'dan revizyon açılmaz. Eski DB'de teslim güncelleme zamanı **yok**: migration önce mevcut outbox AssignmentSubmitted/Completed olaylarından son teslim anını, yoksa CompletedOnUtc, yoksa CreatedOnUtc kullanır (tahmini fallback açıkça belgelenir). Öğretmenin Pending ek bağlantısı teslim sayılmaz. Şema göçü eski enum/olay şekillerini değiştirmez; yerel tarih dönüşümü mevcut varsayılan Europe/Istanbul. T4 tarayıcısı bu görevde değişmez.

- [x] **Step 1: Kırmızı testler** — onaylanmış ödeve yeni dosya → 400, eski dosya **duruyor**; `MarkCompleted` `ReturnedForRevision`'ı ezemez; teslimsiz ödev onaylanamaz; revizyona dönen ödevde `Score == null`; son tarihten sonra teslim `IsLate == true`.
- [x] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [x] **Step 5: Doküman + commit** — `m06_assignments.md`; rapor N-44 ✅.
  ```bash
  git commit -m "fix(assignments): durum makinesi, teslim surumleri, gec teslim isareti (N-44)"
  ```

**Kanıt (2026-11-15):** İmza Release 0 uyarı/0 hata; gerçek birim RED26başarısız/17başarılı → ilgili75yeşil. Dört gerçek PostgreSQL vaka ilk2kırmızı/2yeşil (göç0yerine4sürüm ve PKyarışı500), sonra4/4yeşil: HTTP revizyon/iki sürüm/onay/puan/korunan dosya+yetki; eşzamanlı yüklemede200/409 ve tek blob/sürüm/olay; tüm sürümlerin tekrarlanabilir anonimleştirmesi; ayrıDB’de6eski durumdan4sürüm1, doğru son olay zamanı/fallback/gecikme/puan ve Down/Up. Son `bash scripts/test-with-docker.sh`: **19 mimari / 1277 birim / 187 integration**, **başarısız0/atlanan0**, exit0. Release `-warnaserror --no-incremental`0/0, drift0,166Down, toolrestore başarılı. Mobil değişmedi (Flutter gerekmedi). Doc health RED0/mevcut2YELLOW+1BLUE. Mevcut profilId/UserId erişim açığı T3 kapsamı dışında P21'e devredildi (ILERLEME#267); tarayıcı T4'te. Loglar `/tmp/egitimussu-p19t3-{baseline,signature,red,green,real-red,real-green,docker,release,drift,down,doc-health}.log`.

---

### Task 4: Son tarih tarayıcısı (N-45) ✅

**Files:**
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentDueScanner.cs` (yerel gün sonuna göre birbirinden bağımsız, en fazla 200 yaklaşan + 200 kaçırılmış aday; SQL yalnız gerçekten işlenebilir satırları alır, AutoInclude kapalı)
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentsDbContext.cs` (iki kısmi indeks)
- Modify: `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs` (`MarkDueSoon/MarkMissed` takvim alır; SubmittedOnUtc doluysa iki uyarı da üretilmez; teslimle aynı yerel gün sonu)
- Migration: `AssignmentDueScanIndexes` (`.cs`, `.Designer.cs`, `AssignmentsDbContextModelSnapshot.cs`; yalnız iki kısmi indeks, Up/Down doğrulaması)
- Docs: `doc/modules/m06_assignments.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, N-45 denetim satırı.
- Test: `tests/Unit/AssignmentDueScannerTests.cs` (genişlet), `tests/Integration/RealAssignmentDueScannerTests.cs`

**Interfaces / koddan doğrulama (2026-11-15):** Kalıcı `DueAtUtc` yoktur; yeni kolon eklenmez. `AssignmentDueScanProcessor(db, clock, calendar)` ve domain `MarkDueSoon/MarkMissed(now, calendar)` imzaları önce davranış değiştirmeden derlenir. Gerçek depo + iki IAssignmentRepository sahtesi ve tek IAssignmentDueScanProcessor uygulaması doğrulandı; depo/işlemci arayüzleri değişmez. Son tarih teslimle aynı yerel günün son mikro-saniyesidir. Takvimden şimdi ve 24 saat ufkunun bitmiş gün sınırları çıkarılır; SQL DueDateUtc üzerinde UTC aralık karşılaştırır, istemcide filtreleme yoktur. İki akış ayrı 200 sınırı ve `(DueDateUtc,Id)` sırası kullanır (tur en fazla 400); biri diğerini aç bırakmaz. Her iki sorgu ve domain SubmittedOnUtc==null + Pending/ReturnedForRevision ister. Eski pencere dışı satırlar SQL ile dışlanır; gönderilmemiş DueSoon için sahte bildirim damgası yazılmaz, gerçek kaçırma MissedNotifiedOnUtc ile işaretlenir. İki kısmi indeks bu koşullara göre DueDateUtc/Id üzerindedir; mevcut genel indeks korunur. Olay şekli değişmez; yeni olayın DueDateUtc değeri gerçek yerel gün sonudur, mevcut tüketici doğru saati gösterir. Mobil/uç/sürüm davranışı kapsam dışıdır.

- [x] **Step 1: Kırmızı testler** — 250 eski bekleyen ödev varken yeni ödev için "az kaldı" uyarısı **gider**; zamanında teslim edilip son tarihten sonra revizyona dönen ödev için "teslim edilmedi" **gitmez**.
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [x] **Step 5: Commit** — rapor N-45 ✅.
  ```bash
  git commit -m "fix(assignments): son tarih taramasi aclik yasamaz, teslim edilmis odeve yanlis uyari gitmez (N-45)"
  ```

**Kanıt (2026-11-15):** İmza Release0uyarı/0hata; birim gerçek RED10başarısız/4başarılı →14/14yeşil (önceki8yerine14). Gerçek PostgreSQL ilk3/3kırmızı →3/3yeşil: ayrı DB 750 ödev (250 eski kaçırma damgalı +250 kaçırılmış aday +250 yaklaşan), ilk tur200+200 ve ikinci50+50; üçüncü0, iki SELECT ve teslim geçmişi yüklenmez; kalıcı zamanında teslim/revizyon yanlış uyarı üretmez, yerel son mikro-saniye ve tam tekrar; indeks Up/Down/Up veriyi ve eski damgayı korur. Birim Berlin23/25saat DST gününde24saat ufku dahil sınırlar yeşil. İlk yeşil denemede test outbox zarfını doğrudan olay sanıyordu (KeyNotFound); zarf→Payload çözümüyle düzeltildi. İlk tam paket19/1283/(189yeşil+1testhatası), ardından son **bash scripts/test-with-docker.sh:19mimari/1283birim/190integration**, **0başarısız/0atlanan**,exit0. Release `-warnaserror --no-incremental`0/0 (xUnit2031 test stili uyarısı giderildi), toolrestore başarılı; drift0,167Down. Doc health RED0/mevcut2YELLOW+1BLUE. Mobil değişmedi. Loglar `/tmp/egitimussu-p19t4-{baseline,signature,red,green,real-red,real-green2,docker,docker-final,release-final,drift,down,doc-health}.log`.

---

### Task 5: Düzenleme/silme olayları ve tarih sıralı trend (N-47, N-48 trend) ✅

**Files:**
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs` (`StudySessionEdited/Deleted`, `TestResultEdited/Deleted` domain olayları)
- Audit: `src/Modules/Study/Application/StudyTestFeatures.cs`, `StudySessionFeatures.cs` (mevcut komutlar korunur; olay domain'de ve silme repository'de, deneme toplamı gerçek repository SaveChanges sınırında hesaplanır, bu iki dosyada değişiklik gerekmedi)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` (+ tüketici: `TopicMastery` yeniden hesap; trend `TakenOnUtc` sırasına göre)
- Modify: `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs`, `src/Modules/Reporting/Domain/ReportingProjections.cs` (düzenleme/silme yansır; yanlış yorum düzeltilir)
- Test: `tests/Unit/StudyEditDeletePropagationTests.cs`, `tests/Integration/RealStudyEditPropagationTests.cs`

**Koddan doğrulanan Files / Interfaces (2026-11-15):**
- Create: `src/Shared/Contracts/StudyStatisticsContract.cs`, `src/Modules/Study/Infrastructure/StudyStatisticsReader.cs` (öğrencinin güncel tamamlanmış seans/test kayıtları; diğer modüller Study DbContext'ine erişmez).
- Modify: `src/Modules/Study/Infrastructure/StudyRepository.cs` (silme olayları aynı SaveChanges/outbox; değişen/silinen deneme bileşenlerinin TotalNet'i aynı transaction'da kaynak+tracker'dan hesaplanır).
- Modify: `src/Modules/Study/Infrastructure/DependencyInjection.cs`, `src/Modules/ProgressTracking/Infrastructure/DependencyInjection.cs`, `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` (okuyucu/tüketici kayıtları).
- Modify: `src/Modules/ProgressTracking/Infrastructure/StudyProgressIntegrationEventHandlers.cs`, `src/Modules/ProgressTracking/Application/ProgressTrackingFeatures.cs` (ekleme/düzenleme/silmede kaynak yeniden hesabı; eski konular sıfırlanır, ders katılımı korunur, aktif hedefler değerlendirilir).
- Modify: `src/Modules/ProgressTracking/Infrastructure/LessonSessionCompletedProgressHandler.cs` (aynı öğrenci kilidiyle ders katkısını korur).
- Modify: `tests/Unit/ReportingProjectionTests.cs`, `tests/Unit/LocalBucketTests.cs`, `tests/Integration/RealProjectionRebuildTests.cs` (mevcut olay testleri yeni kaynak okuyucusuna gerçek EF verisi sağlar).
- Docs: `doc/modules/m08_study.md`, `doc/modules/m10_progress_tracking.md`, `doc/modules/m14_reporting.md`, `doc/modules/00_genel_bakis.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`.

**Karar:** Mevcut olay şekilleri değişmez; dört yeni Edited/Deleted olay türü yalnız kaynak kimliğini/öğrenciyi bildirir. Yeni okuyucu tek uygulamalıdır; IStudyRepository/IProgressRepository değişmez, mevcut sahteleri etkilenmez. Kaynak yeniden hesap tüm mevcut + eski konu/hafta/ay kovalarını uzlaştırır; TakenOnUtc, eşitlikte Id sırası deterministiktir. Tüketiciler inbox tabanında, öğrenci bazlı transaction advisory lock içinde okur/yazar; geciken eski ekleme olayı da silinen kaydı diriltemez. Reporting test satırları güncel kaynaktan, mastery satırları mevcut M10 olayından beslenir. Şema/migration değişmez. Kapanmış ProgressSnapshot tarihî fotoğrafı ve kazanılmış rozet/hedefler geri alınmaz; seri onarımı T1'in mevcut ayrı mekanizmasıdır. Tam rebuild kanıtı bu görevde kaynak + olay akışını tekrar uygular; yeni admin ucu/startup göç işi eklenmez.

- [x] **Step 1: Kırmızı testler** — yanlış girilen test silinince `TopicMastery`, Reporting haftalık toplamı ve deneme `TotalNet` düşer; düzenleme yeni değeri yansıtır; geriye dönük girilen eski test trendi tersine **çevirmez**.
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör** + projeksiyon yeniden kurulumu testi yeşil.
- [x] **Step 5: Doküman + commit** — `m08`, `m10`, `m14`; rapor N-47/N-48 (trend) ✅.
  ```bash
  git commit -m "fix(study): duzenleme/silme olaylari istatistiklere yansir, trend tarih sirali (N-47, N-48)"
  ```

---

**Kanıt (2026-11-15):** İmza Release0uyarı/0hata; 7 yeni gerçek birim RED7/0→GREEN7/7; mevcut raporlama/yerel kova testleriyle31/31. Beş yeni PG vaka + mevcut yeniden kurulum6/6: HTTP→outbox→M10/M14, edit ders/konu/ay taşıması, silme ve 26→14→10→0 deneme toplamı, ters/tekrar/farklıEventId Study teslimi, kaynak+inbox temizliği sonrası aynı sayısal değerler, tarih sıralı geriye dönük test ve iki bileşenin eşzamanlı düzenlemesi12net, yabancıTeacher/Student/Parent edit/delete403. İlk PG derleme Array.Reverse void hatası ve testte yanlış Admin rolü (1 yerine Parent4) düzeltildi; bunlar özellik RED'i sayılmadı. İlk tam tur bilinen #194 Query_invoker aralıklı hatası (beklenen2/gerçek3); tekbaşına7/7, ardından son **bash scripts/test-with-docker.sh:19mimari/1290birim/195integration**, **0başarısız/0atlanan**,exit0. Son Release `-warnaserror --no-incremental`0/0, toolrestore başarılı. Şema/migration ve mobil değişmedi; drift/Down/Flutter gerekmedi. Doc health RED0/mevcut2YELLOW+1BLUE; diff-check temiz. Loglar `/tmp/egitimussu-p19t5-{baseline,signature,red,green,real,real2,flake-retry,docker,docker-final,release-final,tools,doc-health-final}.log`.

---

### Task 6: Bildirim kuyruğu, özetler ve küçük doğruluk hataları (N-51, N-52, N-53, N-55) ✅

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/UserNotificationRepository.cs`, `NotificationDispatching.cs` (`PushAttempts`, `NextAttemptOnUtc`, `ExpiresOnUtc`; üstel geri çekilme; süresi dolan `Expired`; tercih ve token'lar **toplu** okunur)
- Modify: `src/Modules/Notifications/Infrastructure/MessageSentNotificationHandler.cs` (gövde: "{gönderen} size mesaj gönderdi" — mesaj metni **yok**)
- Modify: `src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs` (`(veli, öğrenci)` başına özet; sayfalı okuma)
- Modify: `src/Modules/Study/Application/ExamPenalty.cs` (YDS bölen 0 = ceza yok)
- Modify: `src/Modules/Messaging/Application/MessagingFeatures.cs` (okundu: `upToMessageId` parametresi; yalnız o mesaja kadar)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs`, `src/Modules/ProgressTracking/Infrastructure/ProgressSnapshotService.cs` (TopicGoal.MarkMissed; mevcut günlük kiralı tarayıcıda 200 kayıtlık partiler)
- Migration: `NotificationDeliveryBackoff` (Notifications)
- Test: `tests/Unit/NotificationBackoffTests.cs`, `tests/Unit/ParentWeeklySummaryPerChildTests.cs`, `tests/Unit/ExamPenaltyTests.cs` (genişlet), `tests/Unit/MessageReadMarkerTests.cs`, `tests/Unit/TopicGoalMissedTests.cs`

**Koddan doğrulanan ek Files / Interfaces (2026-11-15):**
- Modify: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs`, `Application/NotificationFeatures.cs`, `Infrastructure/NotificationsDbContext.cs`, `Infrastructure/DeviceTokenRepository.cs`, `Infrastructure/ParentNotificationRepository.cs` (buradaki kısa yollar Notifications köküne göredir).
- Modify: `src/Shared/Contracts/UserNotificationPreferencesContract.cs`, `src/Modules/Settings/Infrastructure/UserNotificationPreferencesDirectory.cs` (GetManyAsync; gerçek + iki test sahtesi).
- Modify: `src/Shared/Contracts/ParentNotificationContract.cs`, `src/Modules/Parents/Infrastructure/ParentNotificationDirectory.cs` (LinkId keyset ile 200'lük onaylı bağ sayfaları; mevcut tam liste diğer tüketiciler için korunur). Beş IParentNotificationDirectory test sahtesi ve iki IParentNotificationRepository sahtesi; üç IDeviceTokenRepository sahtesi güncellenir.
- Modify: `src/Modules/Messaging/API/MessagingModule.cs`, `Infrastructure/MessagingRepository.cs`, `tests/Unit/MessagingSupport.cs`, `MessagePagingTests.cs`, `MessagingRelationshipTests.cs`, `tests/Integration/RealMessagingChainTests.cs` (görülen mesaj sınırı SQL'e iner; mevcut yetki kapısı korunur).
- Modify: `mobile/lib/features/messaging/domain/messaging_contracts.dart`, `data/repositories/messaging_repository_impl.dart`, `presentation/cubit/chat_cubit.dart`, `mobile/test/helpers/fake_messaging_repository.dart`, `mobile/test/features/messaging/messaging_cubit_test.dart`; Create: `mobile/test/features/messaging/message_read_marker_test.dart` (yüklenmiş en yeni sunucu mesajı; boş/taslak listede istek yok). Ekran widget'ı/metinleri değişmez, ARB taşıması tetiklenmez.
- Modify: `tests/Unit/UserNotificationTests.cs`, `NotificationDispatchProcessorTests.cs`, `NotificationPreferenceGateTests.cs`, `DeviceTokenTests.cs`, `ParentWeeklySummaryTests.cs`, `ParentNotificationTests.cs`, `PaymentOverdueNotificationHandlerTests.cs`, `NotificationLocalTimeTextTests.cs`, `LessonReminderPlannerTests.cs`, `MessageSentNotificationHandlerTests.cs` (imza sahteleri + eski önizleme beklentisi).
- Create: `tests/Integration/RealNotificationReliabilityTests.cs` (SQL sayımı, HTTP okundu yetkisi, gerçek kuyruk/özet/hedef + ayrı DB göç Up/Down/Up).
- Migration: Notifications `*_NotificationDeliveryBackoff.cs`, `.Designer.cs`, `NotificationsDbContextModelSnapshot.cs` (mevcut teslimler Sent, kalanlar Pending; expiry geriye doldurma; tüm eski MessageReceived Body sabit güvenli metne çevrilir, Down metni geri sızdırmaz).
- Docs: `doc/modules/m11_notifications.md`, `m08_study.md`, `m10_progress_tracking.md`, `m15_settings.md`, `m16_messaging.md`, `m09_parents.md`, `00_genel_bakis.md`, `veri_modeli.md`, `doc/pages/messaging_chat.md`, `doc/roles/veli.md`, N-51/52/53/55 denetim satırları.

- Modify: `src/Modules/ProgressTracking/Application/ProgressTrackingFeatures.cs`, `tests/Unit/ProgressAccessTests.cs` (MasteryService takvimi imzası: günlük taramadan önce gelen geç başarı da önce Missed kontrolünden geçer; altı elle kurucu güncellenir).

**Kararlar:** UserNotification PushStatus Pending/Sent/Expired/Failed; PushAttempts, NextAttemptOnUtc, ExpiresOnUtc. Geri çekilme 30sn × 2^(deneme−1), en fazla 1saat; 8 hatada Failed; ders hatırlatması 30dk, diğer push 24saat sonra Expired. In-app kayıt korunur; tercih kapalı/cihaz yok mevcut MarkPushed semantiğini korur. Pending sorgusu yalnız zamanı gelmiş veya süresi dolmuş satırları seçer, 100 sınırı ve deterministik sıra. Tercih/token okuması sayfa başına birer SQL; eski LessonReminder kaynağının oluşum/yetki uzlaştırması T17 sözleşmesidir, burada değiştirilmez. Mesaj olayı şekli korunur; gönderen-adı sözleşmesi olmadığı için güvenli metin “Bir kullanıcı size mesaj gönderdi.”, kişi adları P21'in sahibi. Özet anahtarı v2 `(veli,öğrenci,yerel ISO hafta)`, eski veli-hafta anahtarı yok sayılır; geçiş haftasında ilk çocuk bir ek özet alabilir, ikinci çocuk kaybolmaz. Sayfa başına toplu dedup + atomik SaveChanges; kira mevcut. MarkConversationReadCommand isteğe bağlı UpToMessageId alır; eski gövdesiz çağrı güvenli etkisiz başarıdır. Başka sohbete ait/yok sınır hata; yalnız sınır mesajı ve ondan kesin eski gelen mesajlar okunur (eşit zamanlı başka mesaj muhafazakâr biçimde okunmamış kalır). TopicGoal tarihi yerel gün sonuna kadar geçerli; günlük snapshot turu geçmiş Active hedefleri Missed yapar, mevcut xmin yarışta hatayı yayar/sonraki tur yeniden dener; kapanmış hedefler değişmez. YDS mevcut cezasız `null` sözleşmesini School ile paylaşır (0'a bölme yok). Master ve kullanıcı teknik karar yetkisiyle geri alınabilir seçimler; tek Task6 sınırı.

- [x] **Step 1: Kırmızı testler** — en eski 100 bildirim kalıcı geçici-hata verirken yeni bildirim gönderilir; 2 saatlik "ders başlıyor" bildirimi `Expired`; tur başına sorgu sayısı sabit (N+1 yok — EF komut sayımı); silinmiş mesajın metni bildirim merkezinde **yok**; iki çocuklu veli iki özet alır; YDS'de yanlış doğruyu götürmez; okundu işareti verilen mesaj ID'sinden sonrasını okundu yapmaz; tarihi geçen hedef `Missed`.
- [x] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [x] **Step 5: Doküman + commit** — `m11`, `m08`, `m16`; rapor N-51/52/53/55 ✅.
  ```bash
  git commit -m "fix(notifications): push geri cekilme + sure, toplu okuma, mesaj metni sizmaz, cocuk basina ozet (N-51..N-53, N-55)"
  ```

---

**Kanıt (2026-11-15):** İmza Release0uyarı/0hata; ilk gerçek birim RED12başarısız/13başarılı → yeşil; sonradan geç başarı/hedef tarihi RED1 (Achieved yerine Missed) →27ilgiliyeşil. Toplam12yeniunit. Mobil gerçekRED2→GREEN; ek gerçek repository JSON sınırıyla3yeni, tam334Flutteryeşil; fatal-infos temiz (ilk import sırası3info düzeltildi). Yeni5PG+mevcutmesajzinciri ilk1kırmızı/5yeşil: eski gövde göçte temizlenmiyordu; SQL backfill sonrası6/6. Ek HTTP sınır/üçyabancırol403 ile9ilgiliPGyeşil. 100hatalı→yenipush ve turbaşına3SELECT,2saatlikExpired/kapalıtercih,205çocuk/2sayfa/yerelhafta/dedup,205hedef/2parti,ayrıDBUp/Down/Up kanıtlı. Son Release `-warnaserror --no-incremental`0/0,drift0,168Down,toolrestore başarılı. Üç varsayılan nihai Docker turu bilinen #194 Query_invoker globalcache sayaç yarışında kırmızı (beklenen1/gerçek2); her birinde19mimari/201integrationyeşil,0atlanan. Test veya üretim kodu bu aralıklı hata için değiştirilmedi; sahibiP26. **Son `DOTNET_PROCESSOR_COUNT=1 bash scripts/test-with-docker.sh`:19mimari/1302birim/201integration,0başarısız/0atlanan,exit0** (hiçbir test filtrelenmedi; tek işlemci görünürlüğü). #194 ayrı7/7. Doc healthRED0/mevcut2YELLOW+1BLUE,diff-checktemiz. Loglar `/tmp/egitimussu-p19t6-{signature,red,deadline-red,deadline-green,mobile-red,mobile-final,analyze-final,real-red,real-final,release-final,drift,down,docker-final,docker-final2,docker-final3,docker-serial,flake-retry,doc-health-final}.log`.

---

### Task 7: Sayfalama ve indeksler (N-54, N-72 bildirim sayfalaması) ✅

**Files:**
- Modify: `src/Modules/Assignments/Infrastructure/AssignmentRepository.cs` + uçlar (keyset `?after=&limit=`)
- Modify: `src/Modules/Study/Infrastructure/StudyRepository.cs` + uçlar (seans, test, not listeleri)
- Modify: `src/Modules/ProgressTracking/Infrastructure/ProgressSnapshotService.cs` (öğrenci partileri halinde)
- Modify (mobil): ödev listesi, çalışma geçmişi, bildirim merkezi → sonsuz kaydırma (`notification_center_cubit.dart` dahil)
- Test: `tests/Unit/KeysetPaginationTests.cs`, ilgili mobil cubit testleri

**Interfaces:**
- Yanıt: `PagedResult<T> { items, nextCursor }` (mevcut `Shared/Kernel/PagedResult.cs` kullanılır). Sayfa parametresi verilmezse **geriye uyumlu** ilk sayfa (varsayılan 50).

**Koddan doğrulanan ek Files / Interfaces (2026-11-15):**
- Modify: `src/Shared/Kernel/PagedResult.cs`; Create: `src/Shared/Kernel/KeysetPage.cs`, `src/Shared/Infrastructure/Persistence/KeysetQuery.cs`.
- Modify: `src/Modules/Assignments/Application/AssignmentFeatures.cs`, `API/AssignmentsModule.cs`, `Infrastructure/AssignmentsDbContext.cs` (bu ve sonraki kısa yollar ilgili modül kökünden).
- Modify (Study): `Application/StudyContracts.cs`, `StudySessionFeatures.cs`, `StudyTestFeatures.cs`, `StudyNoteFeatures.cs`, `API/StudyModule.cs`, `Infrastructure/StudyDbContext.cs`.
- Modify (Notifications): `Application/NotificationFeatures.cs`, `Infrastructure/UserNotificationRepository.cs`, `NotificationsDbContext.cs`, `API/NotificationsModule.cs`.
- Modify: `src/API.Host/TeacherDashboardEndpoints.cs`, `src/API.Host/Seeding/DemoDataSeeder.cs` (liste tüketicileri: panel tüm sayfaları okur, seeder Items kullanır).
- Modify: Assignments/Study `Infrastructure/DependencyInjection.cs` (liste sorgularının PagedResult kayıtları).
- Modify (test sahteleri): `tests/Unit/AssignmentSubmissionStorageTests.cs`, `AssignmentTeacherAuthorizerTests.cs`, `MembershipGateHandlerTests.cs`, `StudySessionRecoveryHandlerTests.cs`, `TestResultScoringIntegrityTests.cs`, `UserNotificationTests.cs`.
- Create: `tests/Integration/RealKeysetPaginationTests.cs`; migration: Assignments/Study/Notifications `*_ListKeysetIndexes.cs`, Designer ve ilgili DbContextModelSnapshot dosyaları.
- Create (mobil): `mobile/lib/core/network/cursor_page.dart`, `mobile/lib/shared/widgets/paged_list_boundary.dart`; Modify: üç modülün domain sözleşmeleri/gerçek repository'leri; `assignments_list_cubit.dart`, `assignments_list_state.dart`, `notification_center_cubit.dart`, `notification_center_state.dart`.
- Modify (mobil ekranlar): `assignments_page.dart`, `student_assignments_page.dart`, `study_history_page.dart`, `study_notes_page.dart`, `notifications_page.dart`; K-26 gereği tr/en ARB. Diğer analiz tüketicilerinin tam liste gereksinimi repository adaptöründe tüm sayfaları okuyarak korunur.
- Modify (mobil sahteler): `mobile/test/helpers/fake_notification_repository.dart`, `mobile/test/features/notifications/notifications_page_test.dart` (ARB sonrası test MaterialApp yerelleştirmesi), `mobile/test/features/notifications/notification_center_cubit_test.dart`, `mobile/test/features/assignments/lesson_resources_cubit_test.dart`, `mobile/test/features/lesson_sessions/presentation/lesson_note_form_page_test.dart`. Create: `mobile/test/features/pagination/cursor_pagination_test.dart`.
- Docs: `doc/modules/m06_assignments.md`, `m08_study.md`, `m10_progress_tracking.md`, `m11_notifications.md`, `00_genel_bakis.md`, `veri_modeli.md`; `doc/pages/assignments.md` (iki mevcut ödev ekranının belgesi), `doc/pages/00_pages_index.md`, `doc/INDEX.md`, mevcut çalışma/bildirim sayfa belgeleri, `doc/architecture/widgets.md`, `ux_rules.md`, N-54/N-72 denetim satırları.

**Karar:** Mevcut listeler parametresiz çağrıda ilk50 için eski JSON dizi biçimini korur; `after` veya `limit` verilince PagedResult `items/nextCursor` döner. Bildirim eski skip/take sözleşmesi aynen korunur, keyset yalnız after/limit ile açılır. PostgreSQL cursor filtresi doğrudan row-value karşılaştırmasıdır; InMemory için eşdeğer OR filtresi kullanılır. Cursor sırası azalan `(zaman,Id)`; ödev/not CreatedOnUtc, seans StartedAtUtc, test TakenOnUtc, bildirim CreatedOnUtc. Not düzenlemesi sayfa anahtarını değiştirmez. Varsayılan50, üst100; bozuk cursor/sınır400. Keyset toplam sayımı yapmaz (TotalCount/TotalPages null); offset metadata korunur. Depo listelerine isteğe bağlı KeysetPage eklenir, iç istatistik/seri çağrıları tam kaynak okumayı korur. Snapshot 200 öğrenci keyset partisi, SQL grup toplama ve parti başına kalıcılık/detach. #272 okuyucunun tam geçmişi gerçek PG'de ölçülür; dar kaynak ve kesin yeniden hesap anlamı korunur, veri kesilmez. Kullanıcının teknik karar yetkisiyle geriye uyumlu ve geri alınabilir seçimler.

- [x] **Step 1–4: Kırmızı gör → uygula → yeşil gör** (`dotnet test`, `flutter test`).
- [x] **Step 5: Doküman + commit** — ilgili `mNN` uç envanterleri, `doc/pages/notifications.md`; rapor N-54 ✅.
  ```bash
  git commit -m "perf: odev, calisma ve bildirim listelerinde keyset sayfalama (N-54)"
  ```

---

**Kanıt (2026-11-15):** İmza Release0uyarı/0hata; cursor birim gerçekRED6başarısız/4başarılı→10/10; ilgili39birimyeşil. GerçekPG ilk liste testi105satır/limit51 ileRED→beşliste/üçsayfaGREEN; snapshot205öğrenci410satır trackerRED→parti/detachGREEN. Row-value SQL beklentisi ayrıcaRED→GREEN. Son dörtPG vaka: eşitzaman/filtre/izlemesizokuma, 205öğrenci/410snapshot/tekrar0, HTTPeskiilk50/yeni20/400sınırlar/Free30gün/üçyabancırol403/öğretmenpaneli105tam, ayrıDB’deüçmodülindeksUpDownUp ve5000seans+5000test2SELECT/28ms/4450664bayttahsis. Kaynakokuyucutamgeçmişi kesin yenidenhesap için korur; maliyetdoğrusaldır, P26aday. İlkHTTP yabancıöğretmenyalnızstudentIdfiltresi boş200: mevcut sahiplikdaraltmasıydı; test herikirakipfiltresini403ileölçer, üretimyetkisipolitikasıdeğişmedi.

Mobil imzadan sonra gerçekRED4başarısız/1başarılı→5/5; eküçvaka tamgeçmişadaptörü/dispose/kısailksayfa+hataotomatikdöngüyok. İlk tamFlutter338yeşil/4kırmızı yalnız yeniARBanahtarbiçimi ve eski3widgetfixture'ın localeeksikliğiydi; düzeltildi, **son342Flutteryeşil/fatal-infos temiz**. Analyze_options geri alındı. **Son varsayılan `bash scripts/test-with-docker.sh`:19mimari/1312birim/205integration,0başarısız/0atlanan,exit0**; bu tur#194görülmedi. Release `-warnaserror --no-incremental`0/0,toolrestore/drift0/171Down başarılı. Doc health RED0/mevcut1YELLOW+1BLUE, diff-check temiz. Task8başlamadı; merge/pushyok. Loglar `/tmp/egitimussu-p19t7-{baseline,signature,red,green,real-red,real-green,batch-red,batch-green,extended-red,extended-green,mobile-signature,mobile-red,mobile-green,mobile-full,mobile-final,analyze-final,release,drift,down,docker,doc-health}.log`.

---

### Task 8: Kapanış — öğrenci haftası senaryosu

**Files:**
- Create: `tests/Integration/RealStudentWeekScenarioTests.cs`

- [ ] **Step 0: #189 (P15'ten devralınan)** — Reporting/Notifications/Identity'de `StudentId` ile anahtarlanan tabloları ölç; varsa `StudentProfilesMerged` tüketicisi + birleşme senaryosu, yoksa ILERLEME'de kapat (plan başındaki not).
- [ ] **Step 1: Senaryo** — öğrenci 3 gün çalışır (biri TR 01:30'da biter) → seri 3; bir test girer, siler → istatistik eski haline döner; ödev teslim eder, öğretmen revizyona döndürür, yeniden teslim eder, onaylar → puan ve iki sürüm; veli haftalık özette bunları görür; Payments handler'ı bilerek hata verdiğinde Reporting yine güncellenir.
- [ ] **Step 2: Koş** — `./scripts/test-with-docker.sh`, `flutter test`.
- [ ] **Step 3: Doküman + ILERLEME + commit**
  ```bash
  git commit -m "test(study): ogrenci haftasi senaryosu, P19 kapanisi"
  ```

*Güncelleme: 2026-11-15 — P19 Task 7 tamamlandı (7/8); sırada yalnız Task 8 (öğrenci haftası senaryosu ve #189 birleşme ölçümü), başlamadı.*
