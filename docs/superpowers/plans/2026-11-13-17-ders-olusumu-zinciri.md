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

### Task 1: Tekrarlı derste kapsam zorunlu + mobil seçim (N-01) ✅

**Files:** *(2026-11-15 koddan doğrulandı ve düzeltildi — Öğrenilenler #227)*
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (cancel/reschedule komutlarında `OccurrenceScope? Scope = null` — varsayılan `All` kaldırılır; tekrarlı derste kapsam zorunlu; `Single`/`ThisAndFuture` anahtar ister ve anahtar serinin **gerçek orijinal oluşumu** olmalı — #223 g)
- Modify: `src/Modules/Scheduling/Application/RecurrenceExpander.cs` (`IsOccurrence(...)` nokta genişletmesi — anahtar doğrulaması) — *eklendi*
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (istek DTO'larında `Scope` nullable; değişiklik talebi gövdesine `OccurrenceStartAtUtc`)
- Modify: `src/Modules/Scheduling/Application/LessonChangeRequestFeatures.cs` (doğrulayıcı da bu dosyada; `LessonChangeRequestPolicies.cs`'e dokunulmadı) (talep **hangi oluşumu** ertelediğini taşır — tekrarlı derste zorunlu ve gerçek oluşum, tekil derste dersin `StartAtUtc`'si; kabul o oluşuma `Single` uygular) — *planın ilk hâli talebin oluşum taşıdığını varsayıyordu; taşımıyordu*
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`LessonChangeRequest.OccurrenceStartAtUtc`) + Migration `AddChangeRequestOccurrence` (Scheduling; `Up` tekil derslerin satırlarını dersin `StartAtUtc`'siyle **geri doldurur**, tekrarlı derslerinkiler kurtarılamaz → NULL kalır ve kabulde açık hata; `Down` kolonu düşer) — *eklendi*
- Modify (mobil — **yalnız veri katmanı + yeni sayfa**): `mobile/lib/features/scheduling/domain/scheduling_contracts.dart` (`LessonSchedule.occurrenceStartAtUtc`, `OccurrenceScope`, `cancelLesson` imzası), `.../data/models/lesson_schedule_model.dart` (`occurrenceStartAtUtc` okunur; `toCancelPayload` scope + **orijinal** anahtar), `.../data/repositories/scheduling_repository_impl.dart`, `.../presentation/cubit/scheduling_cubit.dart`, `mobile/test/helpers/fake_scheduling_repository.dart`, ARB (`lib/l10n/app_tr.arb`, `app_en.arb`; üretilen `app_localizations*.dart` **gitignore'da** — commit'lenmez, `flutter gen-l10n` ile yeniden üretilir). *Düzeltme:* `scheduling_page.dart`/`lesson_detail_page.dart`'ta **takvim dersini iptal eden/erteleyen bir akış yok** — `cancelLesson` hiçbir ekrandan çağrılmıyor, mobil `/reschedule`'ı hiç çağırmıyor; ders detayındaki "Dersi İptal Et" **oturumu** (LessonSessions) iptal ediyor. Ekran bağlantısı bu görevde yapılmaz (K-26 iki büyük ekranı ARB'ye taşımayı zorlardı; oturum↔takvim senkronu T5'te): iptal girişi **T5**, erteleme girişi **P21 T4**
- Create (mobil): `mobile/lib/features/scheduling/presentation/widgets/occurrence_scope_sheet.dart` (doğuştan ARB)
- Test: `tests/Unit/OccurrenceScopeRequiredTests.cs`, `tests/Integration/RealOccurrenceScopeTests.cs` (HTTP: DTO varsayılanı kalmadı; `Single` iptal komşu oluşumları korur; talep + kabul tek oluşumu taşır; göç geri doldurması ayrı veritabanında — #223 e), `mobile/test/features/scheduling/presentation/widgets/occurrence_scope_sheet_test.dart`, `mobile/test/features/scheduling/data/lesson_schedule_model_test.dart` (**mevcut** dosya — eski `toCancelPayload` testi yeni imzaya taşındı) — *ikinci ve dördüncü eklendi; mobil yollar `lib` aynası*
- Modify (kurucu/imza): `tests/Unit/LessonChangeRequestTests.cs` (domain kurucusu), `LessonScheduleTests.cs`, `SchedulingMutationIntegrityTests.cs`, `LessonConflictRecurrenceTests.cs` (iptal handler'ı `ILocalCalendar` alır) — *eklendi*
- Doc: `doc/modules/m04_scheduling.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/pages/scheduling.md`, rapor N-01 (◐ — backend ✅, mobil giriş noktası T5/P21 T4) — *`doc/pages/lesson_detail.md` çıkarıldı: ekrana dokunulmuyor*

**Interfaces:**
- `CancelLessonScheduleCommand(..., OccurrenceScope? Scope = null, DateTime? OccurrenceStartAtUtc = null)` ve `RescheduleLessonScheduleCommand` aynı — tekrarlı derste `Scope == null` → `400 scheduling.scope_required`; `Single`/`ThisAndFuture` anahtarsız → `400 scheduling.invalid_request` (mevcut); anahtar serinin orijinal oluşumu değilse → `400 scheduling.occurrence_not_found`. `All` anahtar istemez (*düzeltme:* plan anahtarı her kapsamda zorunlu sayıyordu; K-20 kapsamı zorunlu kılar). Tekil derste kapsam yok sayılır (Global Constraints).
- `CreateLessonChangeRequestCommand(..., DateTime? OccurrenceStartAtUtc)` — tekrarlı derste zorunlu + gerçek oluşum, tekil derste verilmezse dersin `StartAtUtc`'si; yanıtta `occurrenceStartAtUtc`. Kabul: tekrarlı derste `Single` + o anahtar; anahtarı NULL kalmış eski talep (göçte kurtarılamayan) → `409 scheduling.request_occurrence_unknown` (seri kaymaz).
- Mobil: tekrarlı derste iptal/erteleme öncesi alt sayfa: **"Yalnız bu ders" / "Bu ve sonrakiler" / "Tüm seri"** (varsayılan seçili: "Yalnız bu ders"). `OccurrenceScope` değerleri backend enum'uyla aynı (`single=1`, `thisAndFuture=2`, `all=3`).

- [x] **Step 1: Kırmızı testler** — tekrarlı derste kapsamsız iptal → 400; `Single` iptal → yalnız o oluşum `Cancelled`, seri ve geçmiş oluşumlar dokunulmaz; erteleme talebi kabulü → yalnız ilgili oluşum taşınır; mobil sheet varsayılanı `single` ve yük `scope` + `occurrenceStartAtUtc` içerir. *(Ek: anahtar seride olmayan an / görünen ertelenmiş saat / seri öncesi → `occurrence_not_found`; `All` anahtar istemez; tekil derste kapsam yok sayılır; anahtarsız eski tekrarlı talebin kabulü seriyi kaydırmaz; HTTP: kapsamsız iptal 400, `Single` iptal komşu haftaları korur, talep + kabul tek haftayı taşır, göç geri doldurması ayrı veritabanında.)*
- [x] **Step 2: Kırmızı gör.** *(Önce imza davranışsız — nullable `Scope` = eski `All`, talebe kolon + yalnız kolon ekleyen migration, kurucular → 1122 yeşil; sonra 13 birim testinden 10'u, 3 integration testinin 3'ü iddia hatasıyla kırmızı.)* **Step 3: Backend.** **Step 4: Mobil.** *(Mobil testler kodla birlikte yazıldı; kırmızı mutasyonla doğrulandı — yük orijinal anahtar yerine görünen saati gönderince test kırmızı. Sayfa testi gerçek bir taşma yakaladı: üç seçenek varsayılan alt sayfa yüksekliğine sığmıyordu → kaydırılabilir.)*
- [x] **Step 5: Yeşil gör** — `dotnet test EgitimUssu.slnx`, `cd mobile && flutter test && flutter analyze --fatal-infos`
- [x] **Step 6: Doküman + commit** — `m04`, `doc/pages/scheduling.md`, ~~`doc/pages/lesson_detail.md`~~ (ekrana dokunulmadı); rapor N-01 ◐ (backend ✅, mobil giriş T5 / P21 T4).
  ```bash
  git commit -m "fix(scheduling): tekrarli derste kapsam zorunlu + mobil 'yalniz bu ders' secimi (N-01)"
  ```

---

### Task 2: "Bu ve sonrakiler" seriyi doğru böler ✅

**Files:**
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`SplitSeriesAt(DateTime cutoffUtc, ...)` → yeni seri; `LessonOccurrenceException.MoveToSeries(...)`; `LessonSeriesSplitDomainEvent`)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (reschedule/cancel `ThisAndFuture`; `COUNT` kuralında yeni seriye yalnız kalan oluşum sayısını aktar; yeni seri çakışmasında eski serinin yalnız cutoff ve sonraki oluşumlarını hariç tut)
- Test: `tests/Unit/SeriesSplitTests.cs`
- Test: `tests/Integration/RealSeriesSplitTests.cs` (Global Constraints'teki görev başına Testcontainers kanıtı: iki seri + taşınan istisna + outbox olayı aynı gerçek transaction'da)

**Interfaces:**
- `ThisAndFuture` erteleme: eski seri `UNTIL = cutoff - 1 oluşum` ile biter; cutoff'tan itibaren **yeni** `LessonSchedule` (yeni saat, aynı öğrenci/konu/kural) oluşur; `COUNT` varsa yeni serinin sayısı toplamdan cutoff öncesi oluşumlar düşülerek yazılır (10 haftalık serinin 4. haftadan bölünmesi 3 + 7 kalır). Cutoff sonrası istisnalar yeni seriye **taşınır** (orijinal anahtar ve varsa override saatlerine zaman farkı uygulanır); `Cancelled` eylemi korunur. Başarılı yanıt yeni seriyi döndürür. Olay: `LessonSeriesSplitDomainEvent(OldId, NewId, CutoffUtc)`.
- `ThisAndFuture` iptal: mevcut `EndSeriesBefore` (davranış korunur).

- [x] **Step 1: Kırmızı testler** — 10 haftalık serinin 4. haftasından itibaren saat değişimi → ilk 3 hafta eski saatte, 4–10 yeni saatte; geçmiş oluşumların oturum/ödeme kayıtları eski seride kalır; 6. haftadaki iptal istisnası yeni seride korunur. *(Ek: başka dersle çakışmada iki seri de değişmez; gerçek PostgreSQL'de eski+yeni seri, taşınan iptal istisnası ve split outbox olayı tek transaction'da.)*
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör.** *(İmza iskeleti 0 uyarı/0 hata derlendi; mevcut davranış yeni seri üretmediği için 1/2 kırmızı; uygulama sonrası 2/2 birim + 1/1 gerçek PostgreSQL yeşil. Tam paket: 19 mimari / 1137 birim / 130 integration, başarısız 0, atlanan 0.)*
- [x] **Step 5: Doküman + commit** — `m04`; rapor N-01 backend seri bölme ayağı güncellendi.
  ```bash
  git commit -m "feat(scheduling): 'bu ve sonrakiler' seriyi boler, gecmis olusumlar korunur"
  ```

---

### Task 3: Oluşum anahtarlı ders oturumu (N-02, N-12, N-13) ✅

**Files:**
- Modify: `src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs` (`OccurrenceStartUtc`; yeniden tamamlama olay **yaymaz**)
- Modify: `src/Modules/LessonSessions/Application/LessonSessionFeatures.cs` (`CreateSessionFromScheduleCommand(LessonScheduleId, OccurrenceStartUtc)`; listeleme öğrenci/veli için `IStudentDirectory.GetStudentIdsForUserAsync` + onaylı veli bağı)
- Modify: `src/Modules/LessonSessions/Application/LessonSessionPolicies.cs` (detay: öğrencinin profil kimlikleri / onaylı veli)
- Modify: `src/Modules/LessonSessions/Infrastructure/LessonSessionsDbContext.cs` (benzersiz `(LessonScheduleId, OccurrenceStartUtc)` filtre `LessonScheduleId IS NOT NULL`)
- Modify: `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs` + tüketicisi (`LessonSessionId` bazlı idempotency; `Absent` katılım sayılmaz)
- Modify (mobil): ders başlatma çağrısı oluşum zamanını gönderir (`lesson_sessions` repository + çağıran ekran)
- Migration: `OccurrenceKeyedSessions` (LessonSessions)
- Test: `tests/Unit/OccurrenceKeyedSessionTests.cs`, `tests/Integration/RealWeeklyLessonSessionTests.cs`

**Kapsam doğrulaması (2026-11-15, #230):** Mevcut uç `/from-schedule/{id}` ve gövdesiz; kanonik gövdeli `/from-schedule` eklenir, eski uç tekil dersler için uyumluluk köprüsü kalır (tekrarlıda anahtarsız 400). Ek dosyalar:
- Modify: `src/Modules/LessonSessions/API/LessonSessionsModule.cs`, `Infrastructure/LessonSessionRepository.cs`, `Infrastructure/LessonSessionAccessService.cs`
- Modify: `src/Shared/Contracts/LessonScheduleReadContract.cs`, `LessonSessionReadContract.cs`; `src/Modules/Scheduling/Infrastructure/LessonScheduleDirectory.cs` (gerçek orijinal oluşumu doğrulayıp istisnadan görünen saati okur; olay şeması değişmez)
- Modify: `src/Modules/ProgressTracking/Infrastructure/ProgressTrackingDbContext.cs`, `LessonSessionCompletedProgressHandler.cs`; Migration `LessonAttendanceReceipts` (oturum kimliğiyle kalıcı sayım koruması)
- Modify (sahteler/imzalar): `tests/Unit/CreateSessionFromScheduleTests.cs`, `LessonSessionTests.cs`, `WriteEndpointLinkAuthorizationTests.cs`, `ProgressAccessTests.cs`, `TestDoubles/FakeLessonScheduleDirectory.cs`, `TestDoubles/FakeStudentDirectory.cs`; `tests/Integration/LessonSessionListAuthorizationTests.cs`
- Modify (mobil tam yollar): `mobile/lib/features/lesson_sessions/domain/lesson_session_contracts.dart`, `data/repositories/lesson_session_repository_impl.dart`, `presentation/pages/lesson_detail_page.dart`; `mobile/lib/l10n/app_tr.arb`, `app_en.arb` (K-26: detay ekranının metinleri); `mobile/test/features/lesson_sessions/data/lesson_session_repository_impl_test.dart`; `mobile/test/features/lesson_sessions/presentation/lesson_occurrence_start_test.dart`
- Doc: `doc/modules/m05_lesson_sessions.md`, `m10_progress_tracking.md`, `00_genel_bakis.md`, `veri_modeli.md`, `doc/pages/lesson_detail.md`, `doc/roles/ogrenci.md`, `doc/roles/veli.md`, `doc/roles/ogretmen.md`, `doc/modules/m04_scheduling.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`
- Eşzamanlı aynı oluşum türetmede benzersiz indeksin kaybedeni kazanan oturumu döndürür; tekrar completion notları günceller, ilk tamamlanma damgasını korur ve yeni tamamlanma olayı yaymaz. Katılım yalnız `Attended`/`Late`; farklı olay kimliğiyle aynı oturum ikinci kez sayılmaz.

**İkinci kapsam bulgusu (N-13, 2026-11-15):** `mobile/lib/features/lesson_sessions/data/models/lesson_session_model.dart` yanlış yoklama değerleri gönderiyordu (`Present=1`, `Absent=2`; backend `Unknown=1`, `Attended=2`, `Late=3`, `Absent=4`). `Absent` backend’de katıldı sayılacağı için bu dosya ve `mobile/test/features/lesson_sessions/data/lesson_session_model_test.dart` aynı göreve eklendi. Mevcut repository testindeki `Present` beklentisi de 2 olmalı.

**Interfaces:**
- `POST /api/lesson-sessions/from-schedule` gövdesi `{ lessonScheduleId, occurrenceStartAtUtc }`; tekrarlı derste `occurrenceStartAtUtc` zorunlu (`400 lesson_sessions.occurrence_required`), tekil derste opsiyonel.
- `PlannedStartAtUtc = occurrence` (ertelenmişse override'daki yeni zaman).

- [x] **Step 1: Kırmızı testler** — haftalık derste 1. hafta oturum aç+tamamla, 2. hafta "başlat" → **yeni** oturum, `PlannedStartAtUtc` = 2. hafta; aynı oluşuma ikinci çağrı → aynı oturum (idempotent); öğrenci kullanıcısı kendi oturumlarını listeler (**boş değil**); veli onaylı çocuğunun oturumlarını görür, onaysız göremez; yeniden tamamlama `AttendedLessonCount`'u artırmaz; `Absent` oturum katılım sayılmaz.
- [x] **Step 2: Kırmızı gör.**
- [x] **Step 3: Migration (S-3)** — `Up` sırası: (1) kolon nullable ekle, (2) `UPDATE ... SET "OccurrenceStartUtc" = "PlannedStartAtUtc" WHERE "LessonScheduleId" IS NOT NULL`, (3) aynı `(LessonScheduleId, OccurrenceStartUtc)` tekrarlarında **en erken oluşturulan hariç** satırları `Cancelled` + `OccurrenceStartUtc = NULL` yap ve sayısını `RAISE NOTICE` ile yaz, (4) filtreli benzersiz indeks. `Down` tersini yapar.
  `dotnet ef migrations add OccurrenceKeyedSessions --project src/Modules/LessonSessions/Infrastructure --startup-project src/API.Host --context LessonSessionsDbContext` → `./scripts/check-migration-down.sh`
- [x] **Step 4: Göç tatbikatı** — `scripts/seed-demo-data.sh` ile staging profili doldurulmuş DB'de migration uygula; tekrar sayısı ve örnek satırlar ILERLEME'ye.
- [x] **Step 5: Uygula (backend + mobil).** **Step 6: Yeşil gör** — `./scripts/test-with-docker.sh`, `flutter test`
**Doğrulama kanıtı (2026-11-15):** Davranışsız imzalar Release `-warnaserror` ile derlendi. Kırmızı: 7 yeni birim testin 6’sı, HTTP öğrenci listesi, veri göçünde NULL anahtar, mobil yük/ekran anahtarı ve 5 yoklama eşlemesinin 4’ü iddia hatası verdi. Sonuç: **19 mimari / 1144 birim / 134 integration**, başarısız 0, atlanan 0 (`bash scripts/test-with-docker.sh`); Release `-warnaserror --no-incremental` 0 uyarı/0 hata; drift 0; **149 migration Down kontrolü** geçti; **286 Flutter testi** + `flutter analyze --fatal-infos` temiz. Staging seed: 2 öğretmen / 5 öğrenci / 2 veli / 15 ders / 15 oturum; eski şemaya dönülüp 2 tekrar eklendi (17 satır, 1 yinelenen grup). Up: en erken kayıt anahtarla kaldı, 2 tekrar `Cancelled` + NULL, NOTICE 2; Down satırları korudu, yeniden Up aynı sonucu verdi.

- [x] **Step 7: Doküman + commit** — `m05`, `m10`, `veri_modeli.md`; rapor N-02/N-12/N-13 ✅.
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
- Modify (mobil) — *2026-11-15 eklendi (P17 T1 devri):* takvim dersi **iptal girişi** — T1 veri katmanını (`SchedulingCubit.cancelLesson(lesson, scope)`) ve `showOccurrenceScopeSheet`'i hazırladı ama hiçbir ekran çağırmıyor. Giriş `lesson_detail_page.dart`'ta olur (K-26 ARB maliyeti T4'te zaten ödeniyor) ve takvim iptali artık kanonik yol olduğundan oturum iptali (`LessonCancelledSessionHandler`) onu izler. **İki tuzak:** (1) `scheduling_cubit.dart` iptal yanıtını satırlara **`id` ile** birleştiriyor (`item.id == cancelled.id`) — serinin bütün oluşumları aynı `id`'yi taşıdığı için `Single` iptal yanıtı (serinin çapa satırı, `Planned`) listedeki her haftanın üzerine yazılır → birleştirme kapsam/oluşum anahtarına göre yapılmalı ya da liste yeniden yüklenmeli; (2) `LessonScheduleModel.cancelPayload` `reason`/`isChargeable` göndermiyor — bu görevin iptal nedeni doğrulaması bu yükü 400'e çevirir, yük nedeni ve ücret kararını taşımalı. Test: `mobile/test/features/scheduling/...` (birleştirme + yük)
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

*Güncelleme: 2026-11-15 (P17 Task 3: kapsam koddan doğrulandı, TDD + gerçek PostgreSQL göçü + backend/mobil doğrulama).*
