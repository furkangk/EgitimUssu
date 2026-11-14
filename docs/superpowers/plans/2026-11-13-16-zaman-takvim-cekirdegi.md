# P16 — Zaman ve Takvim Çekirdeği (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** "Bugün", "bu hafta", "bu ay" ve kullanıcıya gösterilen her saat Türkiye saatine göre doğru olsun; tekrarlayan dersler doğru güne düşsün; aynı oluşuma ikinci istisna sistemi çökertmesin; çakışma kontrolü tekrarı ve eşzamanlılığı görsün.

**Architecture:** `Shared/Kernel/ILocalCalendar` tek zaman kaynağıdır (K-19). `StudyLocalTime` sabit `+3` ofset yerine bu arayüzün üzerine kurulur. `RecurrenceExpander` serinin `TimeZone`'unda yerel saatle genişletir ve UTC'ye çevirir. Oluşum istisnaları `(SeriesLessonScheduleId, OriginalStartAtUtc)` üzerinde benzersizdir ve upsert edilir. Çakışma kontrolü pencere içindeki oluşumları genişletir, öğretmen başına `pg_advisory_xact_lock` ile serileştirilir.

**Tech Stack:** .NET 9 (`TimeZoneInfo` IANA), EF Core 9 / Npgsql 9, xUnit, Testcontainers.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — K-19, §3.1 S-2, §4 `ILocalCalendar`. **Kaynak:** rapor §1.1 (N-04, N-05, N-07, N-11, N-14), §1.3 (N-43, N-50).

**Dal:** `feat/p16-zaman-takvim`

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Önceki tüm sözleşmeler. **Bu plandan sonra** iş kuralında `DateOnly.FromDateTime(<utc>)`, `<utc>.Date`, UTC `.DayOfWeek` yasaktır; T6'da kaynak taraması bunu kilitler.
- Testlerde sabit saat: `FakeClock` + gerçek `LocalCalendar("Europe/Istanbul")`. Kritik sınır anları: **TR 00:30 (= UTC 21:30 önceki gün)**, **TR 02:59**, **TR 03:00**, ayın son günü 23:30 TR, ayın 1'i 01:00 TR.
- **S-2:** T3 (istisna benzersizliği) P17 T1'den (mobil scope) **önce** merge edilmelidir.
- **Commit:** görev başına bir commit.

---

### Task 1: `ILocalCalendar` + `StudyLocalTime` taşıması ✅

**Files:**
- Create: `src/Shared/Kernel/ILocalCalendar.cs`, `src/Shared/Infrastructure/LocalCalendar.cs`
- Create: `src/Shared/Infrastructure/Configuration/LocalCalendarGuard.cs` (açılışta zaman dilimi çözülemezse fail-fast)
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (`Localization:TimeZone` burada okunur ve guard'dan geçer — diğer guard'lardan **önce**; ayrı options sınıfı yok, `LocalizationOptions` adı framework tipiyle çakışır)
- Modify: `src/API.Host/Program.cs` (açılış log'una "yerel takvim" satırı — Step 4'ün kanıtı) — *2026-11-14 eklendi*
- Modify: `src/Modules/Study/Application/StudyPolicies.cs` (`StudyLocalTime` → `ILocalCalendar` üzerine; 04:00 seri günü kuralı korunur)
- Modify: `src/Modules/Study/Application/StudySessionFeatures.cs`, `src/Modules/Study/Application/StudyProgressFeatures.cs` (çağıranlar `ILocalCalendar` enjekte eder — statik yardımcı DI'lı takvimi göremez) — *2026-11-14 eklendi*
- Modify: `tests/Unit/StreakRulesTests.cs`, `tests/Unit/StudyStreakTests.cs`, `tests/Unit/StudySessionRecoveryHandlerTests.cs` (imza değişikliği) — *2026-11-14 eklendi*
- Modify: `Dockerfile` (runtime imajında `tzdata` mevcut değilse ekle)
- Doc: `doc/architecture/backend.md` (§5.1 zaman), `doc/modules/m08_study.md` (sabit UTC+3 anlatımı) — *2026-11-14 eklendi*
- Test: `tests/Unit/LocalCalendarTests.cs`

**Interfaces:**
- Produces: master §4'teki `ILocalCalendar` imzası aynen. Ek: `DateOnly StudyDay(DateTime utc)` **Study'de** kalır (`ILocalCalendar` uzantısı; `ToLocalDate(utc.AddHours(-4))` — `DateOnly.FromDateTime(` modülde kullanılmaz, T6 taraması).
- Config: `Localization:TimeZone` (varsayılan `Europe/Istanbul`).
- `FormatForUser` Türkçe ay/gün adlarını **sabit tablodan** basar (ICU/kültür verisine bağlı değil — Linux imajında kültür eksikse sessizce İngilizce basmaz).

- [x] **Step 1: Kırmızı testler** — `ToLocalDate(2026-11-13T21:30Z)` = 2026-11-14; `DayRangeUtc(2026-11-14)` = `[2026-11-13T21:00Z, 2026-11-14T21:00Z)`; `WeekRangeUtc` Pazartesi 00:00 TR (Pazar girdisi dahil); `MonthRangeUtc(2026, 12)` = `[2026-11-30T21:00Z, 2026-12-31T21:00Z)`; her UTC çıktıda `Kind == Utc`; `FormatForUser(2026-11-13T07:00Z)` → `"13 Kasım Cuma 10:00"` (*düzeltme 2026-11-14: planın ilk hâlindeki "14 Kasım Cuma" 2026'da yanlış — 14 Kasım 2026 Cumartesi*); geçersiz zaman dilimi kimliği → guard hatası.
- [x] **Step 2: Kırmızı gör** — `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~LocalCalendarTests"`
- [x] **Step 3: Uygula** (`TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")`; Windows'ta ICU ile çözülür, çözülmezse guard açıkça söyler).
- [x] **Step 4: Docker doğrulaması** — `docker build -t eu-tz . && docker run --rm -e ASPNETCORE_ENVIRONMENT=Development eu-tz` açılış log'unda guard geçti satırı; geçmezse `apt-get install -y tzdata` ekle. *(2026-11-14: `aspnet:9.0` imajında `tzdata` kurulu — `dpkg -s tzdata` → `install ok installed`; log satırı `Yerel takvim: Europe/Istanbul (UTC+03:00) — LocalCalendarGuard geçti`. Dockerfile'a paket değil yalnız koruma yorumu eklendi.)*
- [x] **Step 5: `StudyLocalTime` taşıması** — mevcut seri testleri yeşil kalmalı (davranış aynı, yalnız kaynak değişiyor).
- [x] **Step 6: Yeşil gör + doküman + commit** — `doc/architecture/backend.md` §zaman (§5.1); master §1 "Saat" kuralına atıf.
  ```bash
  git commit -m "feat(time): ILocalCalendar tek zaman kaynagi + StudyLocalTime tasindi (K-19)"
  ```

---

### Task 2: Tekrar genişletmesi yerel saatte (N-04) ✅

**Files:**
- Modify: `src/Modules/Scheduling/Application/RecurrenceExpander.cs` (her iki `Expand` aşırı yüklemesi + aynı dosyada `SeriesTimeZone` çözücüsü)
- Modify: çağıranlar — `LessonScheduleFeatures.cs`, `StudentCalendarQuery.cs`, `SelfLessonFeatures.cs` (`SelfLessonConflict` + iki self-ders handler'ı; plan ilk hâlinde yanlışlıkla `SelfLessonPolicies.cs` diyordu) — *2026-11-14 düzeltildi*
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleDirectory.cs` (dördüncü çağıran: Reporting boş zaman analizi — `ListTeacherBusyIntervalsAsync`) — *2026-11-14 eklendi*
- Modify: `tests/Unit/RecurrenceExpanderTests.cs` (imza: dilim argümanı), `tests/Unit/TeacherLessonExpansionTests.cs`, `tests/Unit/StudentCalendarQueryTests.cs` (handler kurucusu `ILocalCalendar` alır) — *2026-11-14 eklendi*
- Doc: `doc/architecture/backend.md` §5.1, `doc/modules/m04_scheduling.md`, rapor N-04 — *2026-11-14 eklendi*
- Test: `tests/Unit/RecurrenceExpanderLocalTimeTests.cs`

**Interfaces:**
- `Expand(...)` artık serinin `TimeZone`'unu (boşsa `ILocalCalendar.Zone`) alır: çapa yerel saate çevrilir, yerel takvimde ilerlenir, her oluşum UTC'ye çevrilir. *(Uygulanan biçim: `Expand(anchorStartUtc, anchorEndUtc, rule, TimeZoneInfo zone, rangeStartUtc, rangeEndUtc[, exceptions])` saf/statik; çağıran `calendar.SeriesZone(lesson.TimeZone)` verir — boş **veya çözülemeyen** kimlik `ILocalCalendar.Zone`'a düşer, doğrulayıcı yalnız boş olmamayı istediği için.)*
- Haftalık: `BYDAY` **yerel** haftanın günüyle eşleşir.
- Aylık: ay günü **çapadan sabitlenir** (`anchorLocal.Day`), kısa ayda o ayın son gününe kırpılır ama **sonraki aya taşınmaz** (31 Ocak → 28 Şubat → **31 Mart**).
- `UNTIL` *(2026-11-14 netleştirildi)*: `Z` sonekli = UTC an (mobil + `EndSeriesBefore` biçimi); Z'siz tarih-saat = serinin yerel saati (RFC 5545 floating); yalnız tarih = o yerel günün tamamı.

- [x] **Step 1: Kırmızı testler** — Salı 01:30 TR haftalık (`BYDAY=TU`) → ilk oluşum üretilir ve tüm oluşumlar TR Salı 01:30; 31 Ocak aylık → 28 Şubat, 31 Mart, 30 Nisan; "her ayın 1'i 01:00 TR" → her ay 1'inde; `COUNT` ve `UNTIL` sınırları yerel saatle; mevcut tüm `RecurrenceExpander` testleri yeşil. *(Ek: DST'li seri dilimi — Berlin ekim sonu, duvar saati 18:00 korunur; istisnalar yerel oluşum anını hedefler; boş/geçersiz dilim → platform dilimi; handler düzeyinde Salı gecesi serisi.)*
- [x] **Step 2: Kırmızı gör.** **Step 3: Uygula.** **Step 4: Yeşil gör** — `dotnet test EgitimUssu.slnx` *(önce imza davranışsız geçirildi → 1042 yeşil; sonra yeni testler 11 iddia hatasıyla kırmızı; uygulama sonrası yeşil)*
- [x] **Step 5: Mobil tutarlılık kontrolü** — `mobile/lib/features/scheduling/presentation/widgets/lesson_form_sheet.dart` RRULE'u **yerel** günle mi üretiyor? (`grep -n "BYDAY\|weekday" lesson_form_sheet.dart`). Yerelse dokunma; UTC'den üretiyorsa bu görevde düzelt + widget testi. *(2026-11-14: yerel — `_weekdayFromDate(_selectedDate)` seçilen yerel tarihin `weekday`'i, `UNTIL` yerel 23:59 → `toUtc()` + `Z`; `study_entry_form_sheet.dart` aynı. Dokunulmadı.)*
- [x] **Step 6: Doküman + commit** — `m04_scheduling.md` (tekrar kuralı); rapor N-04 ✅.
  ```bash
  git commit -m "fix(scheduling): tekrar kurali serinin yerel saatinde genisletilir, ay gunu capadan sabit (N-04)"
  ```

---

### Task 3: Oluşum istisnaları — benzersizlik, upsert, pencere dışı erteleme (N-05, N-14) ✅

**Files:**
- Modify: `src/Modules/Scheduling/Infrastructure/SchedulingDbContext.cs` (`HasIndex(...).IsUnique()`)
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleRepository.cs` (`UpsertExceptionAsync`; *2026-11-14 eklendi:* `ListActiveForTeacher/StudentUntilAsync` pencereye öne alınmış oluşumu olan seriyi de yükler — çapası pencereden sonra olan serinin ilk dersi bir önceki haftaya alınınca)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (cancel/reschedule `Single` yolu upsert kullanır)
- Modify: `src/Modules/Scheduling/Application/RecurrenceExpander.cs` (`ToDictionary` yerine güvenli gruplama; pencereye **taşınan** override'lar ayrıca dahil)
- Modify: `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` (`LessonOccurrenceException` özel setter'lı, mutator yok → upsert için `Overwrite(...)` domain metodu) — *2026-11-14 eklendi*
- Modify: `ILessonScheduleRepository`'nin altı test sahtesi — `tests/Unit/LessonScheduleTests.cs`, `SchedulingMutationIntegrityTests.cs` (kayıt listesi upsert'e bağlanır; `Assert.Empty(AddedExceptions)` boşa düşmesin), `SelfLessonPolicyTests.cs`, `StudentCalendarQueryTests.cs`, `TeacherLessonExpansionTests.cs`, `WriteEndpointLinkAuthorizationTests.cs` (`AddExceptionAsync` → `UpsertExceptionAsync`) — *2026-11-14 eklendi*
- Migration: `UniqueOccurrenceExceptions` (Scheduling) + `.Designer.cs` + `SchedulingDbContextModelSnapshot.cs`
- Doc: `doc/modules/m04_scheduling.md`, `doc/modules/veri_modeli.md`, rapor N-05/N-14 — *2026-11-14 eklendi*
- Test: `tests/Unit/OccurrenceExceptionUpsertTests.cs`, `tests/Integration/RealOccurrenceExceptionTests.cs`

**Interfaces:**
- `ILessonScheduleRepository.UpsertExceptionAsync(LessonOccurrenceException exception, CancellationToken ct)` — aynı `(SeriesLessonScheduleId, OriginalStartAtUtc)` varsa günceller. `AddExceptionAsync` kaldırılır (tek yazma yolu upsert).
- Genişletme: pencere için seri oluşumları + `OverrideStartAtUtc` pencereyle çakışan **Rescheduled** override'lar (orijinali pencere dışında olsa da). *(2026-11-14 netleştirildi: pencere `[from, to)` değil, genişleticinin mevcut **iki ucu dahil** kuralı — `Tuesday_night_series_lists_local_tuesdays` pencere sonundaki oluşumu bekler; taşınan override aynı dalın çakışma yüklemini kullanır. Orijinal gerçek bir oluşum olmalı — nokta genişletmesiyle doğrulanır, yoksa bayat istisna pencere dışından "hayalet ders" olarak dirilirdi. Düzeltme dört çağıranın hiçbirine dokunmadan `RecurrenceExpander` içinde kalır: hepsi serinin tüm istisnalarını yüklüyor.)*

- [x] **Step 1: Kırmızı testler** — aynı oluşuma iki kez erteleme → tek istisna, son değer geçerli, liste **200**; ertelenmiş oluşum sonra iptal → tek istisna `Cancelled`; Pazartesi dersi gelecek haftanın Salı'sına ertelendi → **gelecek hafta** görünümünde görünür, bu hafta görünmez. *(Ek: tekrar satırlar genişleticiyi patlatmaz; pencere içi erteleme bir kez; pencere sonu dahil; bayat istisna ×3 ve iptal/atlama satırı pencereye sızmaz; ilk ders serinin çapasından önceki haftaya öne alınınca o haftada listelenir; göç testi ayrı veritabanında önceki migration'a kurup tekrar yazar, yükseltir, `Down`'ı dener.)*
- [x] **Step 2: Kırmızı gör.** *(Önce `AddExceptionAsync` → `UpsertExceptionAsync` davranışsız yeniden adlandırıldı → 1061 yeşil; sonra yeni testler: birim 5 kırmızı (`ArgumentException` + taşınan yok), integration 3 kırmızı (liste 500, gelecek haftada Salı yok, göçte 4 satır kaldı), öne alma uç testi ayrıca kırmızı.)*
- [x] **Step 3: Migration** — `Up`: aynı anahtarlı tekrarlarda **en son `CreatedOnUtc` hariç** sil (SQL `DELETE ... USING (SELECT ... ROW_NUMBER() ...)`), sonra benzersiz indeks. `Down`: indeksi normal indekse geri çevir.
  `dotnet ef migrations add UniqueOccurrenceExceptions --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext` → `./scripts/check-migration-down.sh`
- [x] **Step 4: Upsert + genişletme düzeltmesi.**
- [x] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [x] **Step 6: Doküman + commit** — `m04`, `veri_modeli.md`; rapor N-05/N-14 ✅.
  ```bash
  git commit -m "fix(scheduling): olusum istisnalari benzersiz + upsert, pencere disina ertelenen ders gorunur (N-05, N-14)"
  ```

---

### Task 4: Çakışma kontrolü — tekrar, tatil bloğu, eşzamanlılık (N-07) ✅

**Files:**
- Modify: `src/Modules/Scheduling/Infrastructure/LessonScheduleRepository.cs` (*2026-11-14 düzeltildi:* `HasTeacherConflictAsync` ve `ListForTeacherAsync` — yalnız çapa satırını karşılaştıran iki sorgu — **kaldırılır**; genişletme Application'da, mevcut `ListActiveForTeacherUntilAsync` + `ListExceptionsForTeacherAsync` üzerinden yapılır. Planın andığı `ListOverlappingAsync` kodda yoktu; o işi `ListForTeacherAsync` görüyordu)
- Modify: `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` (create/update/reschedule: oluşum genişletmeli çakışma + tatil bloğu kontrolü + kilit; iptal `Single` da kilide alınır — Öğrenilenler #223 g; saf `TeacherScheduleConflicts` yardımcısı, `ITeacherScheduleLock` arayüzü, `SchedulingConflictSettings`)
- Modify: `src/Modules/Scheduling/Application/TimeOffFeatures.cs` (çakışan dersleri seri genişleterek listele; bloktan önce başlayıp bloğa taşan ders dahil; kilit altında)
- Create: `src/Modules/Scheduling/Infrastructure/TeacherScheduleLock.cs` (`pg_advisory_xact_lock(hashtext('sched:' || teacherId))`; Npgsql değilse no-op)
- Modify: `src/Modules/Scheduling/Infrastructure/DependencyInjection.cs` (kilit + `Scheduling:ConflictHorizonWeeks` okunur, aralık dışıysa açılış durur) — *2026-11-14 eklendi*
- Modify: `src/Modules/Scheduling/API/SchedulingModule.cs` (üç istek gövdesine `AllowTimeOffOverride`, `scheduling.time_off_conflict` → 409; yoksa varsayılan 400'e düşerdi) — *2026-11-14 eklendi*
- Modify: `ILessonScheduleRepository` sahteleri (kaldırılan iki yöntem) + handler kurucusu değişenler — `tests/Unit/LessonScheduleTests.cs`, `SchedulingMutationIntegrityTests.cs`, `OccurrenceExceptionUpsertTests.cs`, `SelfLessonPolicyTests.cs`, `StudentCalendarQueryTests.cs`, `TeacherLessonExpansionTests.cs`, `WriteEndpointLinkAuthorizationTests.cs` — *2026-11-14 eklendi*
- Doc: `doc/modules/m04_scheduling.md`, `doc/modules/00_genel_bakis.md`, `doc/architecture/backend.md` (yeni persistence deseni: öğretmen başına advisory lock), `doc/roles/ogretmen.md`, rapor N-07 — *2026-11-14 eklendi*
- Test: `tests/Unit/LessonConflictRecurrenceTests.cs`, `tests/Integration/RealConcurrentLessonCreateTests.cs`

**Interfaces:**
- Hatalar: `scheduling.teacher_conflict` (mevcut — *2026-11-14 düzeltildi:* plan `lesson_conflict` diyordu, kodda ve mobil sözleşmede ad `teacher_conflict`; değiştirilmedi), `scheduling.time_off_conflict` (yeni, 409). İstek gövdesine `allowTimeOffOverride: bool` (varsayılan false; create/update/reschedule) — öğretmen bilerek tatil gününe ders koyabilsin. Ders çakışması tatilden **önce** bakılır ve override edilemez.
- Kontrol penceresi: yeni dersin ilk oluşumundan itibaren **12 hafta** (config `Scheduling:ConflictHorizonWeeks`, 1–104). *2026-11-14 netleştirildi:* tekrarlı adayda pencere `max(çapa, şimdi)`'den başlar (Eylül'de çapalanmış bir serinin notunu düzeltmek geçmişteki bir çakışma yüzünden 409 almasın); tekil aday her zaman kendi anında denetlenir (geçmişe kayıt girilebilir, eski davranış).
- Mevcut oluşumlar aday aralığından **ders süresi kadar geriden** genişletilir: genişletici seri oluşumunu başlangıcına göre süzer, 16:30–17:30 dersi 17:00 adayından kaybolmasın; çakışma kesin `<`/`>` (arka arkaya dersler serbest). İptal edilen oluşum yer tutmaz, ertelenen oluşum yeni saatinde yer tutar.
- Hariç tutma: update ve reschedule-All dersin kendisini; reschedule-Single yalnız hedef oluşumu (aynı serinin kardeş oluşumunun üstüne taşıma yakalanır). Update/reschedule-All adayına serinin kendi istisnaları uygulanır.
- `ITeacherScheduleLock.RunExclusiveAsync(teacherUserId, work, ct)`: execution strategy → transaction → `pg_advisory_xact_lock` → `work` (okuma + yazım + `SaveChanges`) → yalnız başarıda commit. İş birimi yeniden denenebilir yazılır (aggregate değişikliği bir kez uygulanır).

- [x] **Step 1: Kırmızı testler** — Eylül'de başlamış haftalık Salı 17:00 serisi varken Kasım'da Salı 17:00 tekil ders → 409; tatil bloğu içine ders → 409 `time_off_conflict`, override ile 200; bloktan önce başlayıp bloğa taşan ders tatil oluştururken listelenir; iki paralel POST (Testcontainers) → **biri** 409. *(Ek: kısmi kesişim (16:30–17:30 serisi 17:00 adayını bloklar) ve arka arkaya ders serbest; iptal edilen oluşum boşaltır, ertelenen yeni saatini tutar; tekrarlı aday 4 hafta sonraki tekil derse çarpar, ufuk dışı (13. hafta) serbest, ufuk config'le büyür; Eylül serisinin notunu düzeltmek geçmiş çakışmaya takılmaz, kendi iptal oluşumu yer tutmaz; tek oluşum kardeş oluşumun / başka serinin üstüne taşınamaz, kendi saatinin yarım saat ötesine taşınabilir; ders çakışması tatilden önce ve override edilemez; her yazma yolu okuma + kaydetmeyi kilit içinde yapar; **kilit testi** — test bağlantısı `sched:<id>` anahtarını tutarken oluştur/tek erteleme/tek iptal bekler, başka öğretmen beklemez; HTTP'de `time_off_conflict` 409 + override 200.)*
- [x] **Step 2: Kırmızı gör.** *(Önce imza davranışsız geçirildi — kilit arayüzü + geçişli uygulama, `SchedulingConflictSettings`, `AllowTimeOffOverride`, kurucular — 1072 yeşil; sonra 33 yeni birim testinden 17'si, 5 integration testinin 5'i iddia hatasıyla kırmızı — paralel testte yarış gerçekten oluştu.)* **Step 3: Uygula** (kilit + kontrol + insert **aynı transaction**). **Step 4: Yeşil gör** — `./scripts/test-with-docker.sh` *(mutasyon: `pg_advisory_xact_lock` çağrısı kapatılınca iki kilit testi kırmızı)*
- [x] **Step 5: Doküman + commit** — `m04`; rapor N-07 ✅.
  ```bash
  git commit -m "fix(scheduling): cakisma kontrolu olusum genisletir, tatil blogu ve es zamanli istek korunur (N-07)"
  ```

---

### Task 5: Gün/hafta/ay kovaları ve kullanıcıya gösterilen saatler (N-11, N-43, N-50, N-06 metin)

**Files:**
- Modify: `src/API.Host/TeacherDashboardEndpoints.cs` ("bugün")
- Modify: `src/Modules/Payments/Application/PaymentFeatures.cs` (aylık kırılım; gecikmiş toplam **para birimi bazında** — yanıt alanı listeye döner, eski tekil alan geriye uyumluluk için ilk para birimini taşımaya devam eder ve `[Obsolete]` notu dokümana)
- Modify: `src/Modules/Scheduling/Application/StudentCalendarQuery.cs` (çalışıldı eşleşmesi yerel gün)
- Modify: `src/Modules/Reporting/Domain/ReportingProjections.cs`, `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs` (hafta/ay kovası)
- Modify: `src/Modules/Reporting/Application/FreeSlotCalculator.cs` (uygunluk şablonu yerel güne kurulur → UTC)
- Modify: `src/Modules/ProgressTracking/Infrastructure/ProgressSnapshotService.cs`, `src/Modules/Reporting/Application/StudentReportFeatures.cs`
- Modify: bildirim metinleri — `src/Modules/Notifications/Infrastructure/AssignmentDueNotificationHandlers.cs`, `PaymentOverdueNotificationHandler.cs`, `LessonScheduleNotificationIntegrationEventHandler.cs` (yalnız metin biçimi; oluşum mantığı P17 T6)
- Test: `tests/Unit/LocalBucketTests.cs` (her dosya için en az bir sınır testi), `tests/Unit/NotificationLocalTimeTextTests.cs`

- [ ] **Step 1: Kırmızı testler** — TR 00:30'daki ders "bugün" listesinde; ertesi günün 00:30 dersi değil; ayın 1'i 01:00 TR vadeli ödeme o ayda; Pazartesi 01:30 TR seansı Reporting'de **bu haftada**; "Pzt 18:00–21:00" uygunluk şablonu 15:00Z–18:00Z ile çakışma hesaplar; ödev son teslim 23:59 TR → bildirim metninde **"23:59"**; TRY+USD gecikmiş ödemeler iki ayrı toplam.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Uygula** (hepsi `ILocalCalendar` üzerinden).
- [ ] **Step 4: Projeksiyon yeniden kurulumu** — kova kuralı değiştiği için Reporting projeksiyonları `IProjectionRebuilder` ile yeniden kurulur; `RealProjectionRebuildTests` yeşil kalmalı. Göç notu `doc/architecture/backend.md` §7.1'e.
- [ ] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 6: Doküman + commit** — `m07`, `m10`, `m11`, `m14`; rapor N-11/N-43/N-50 ✅.
  ```bash
  git commit -m "fix(time): gun/hafta/ay kovalari ve bildirim saatleri Turkiye saatine gore (N-11, N-43, N-50)"
  ```

---

### Task 6: Kapanış — kaynak kilidi + gece yarısı senaryosu

**Files:**
- Create: `tests/Architecture/LocalTimeRuleTests.cs` (kaynak taraması: `src/Modules/**/Application|Domain|Infrastructure` içinde `DateOnly.FromDateTime(`, `UtcNow.Date`, `.Date ==` kalıpları — izinli listedeki dosyalar hariç **0**)
- Create: `tests/Integration/RealMidnightScenarioTests.cs`

- [ ] **Step 1: Mimari test** — izinli liste yalnız `LocalCalendar.cs` ve göç kodu. Kırmızıysa kalan kullanımları düzelt.
- [ ] **Step 2: Senaryo** — gerçek Postgres: öğretmen Salı 00:30 TR haftalık ders açar, Salı 00:45 TR'de dashboard → ders "bugün"; öğrenci takviminde Salı; aylık rapor doğru ayda.
- [ ] **Step 3: Koş** — `./scripts/test-with-docker.sh` başarısız 0 / atlanan 0.
- [ ] **Step 4: Doküman + ILERLEME + commit**
  ```bash
  git commit -m "test(time): yerel saat kurali kilitlendi + gece yarisi senaryosu, P16 kapanisi"
  ```
