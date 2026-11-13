# P15 — Güvenlik Acil (Dalga 0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Veli ↔ öğrenci ↔ öğretmen bağlarının ele geçirilememesi, bağı biten tarafın erişiminin gerçekten kesilmesi ve üretim yapılandırmasının sır sızdıramaması. Ayrıca P22'nin (Sınıflar) üzerine kurulacağı iki ortak parçayı kurmak: `ISecureCodeGenerator` ve `ITeacherStudentAccess`.

**Architecture:** Kod üretimi ve erişim kapsamı `Shared/Kernel` + `Shared/Contracts` arayüzleri arkasına alınır, Students modülü uygular. Yazma uçlarının authorizer'ları kapsam sorgusuna geçer. Davet/claim/merge kuralları domain metotlarında `Result` ile zorlanır. Token tarafında iptal nedeni saklanır, refresh koşullu güncelleme ile tek kazanana indirgenir; mobil interceptor aynı görevde tek-uçuş refresh'e geçer (S-1).

**Tech Stack:** .NET 9, EF Core 9 / Npgsql 9, Redis, xUnit, Testcontainers, Flutter (yalnız T7).

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — K-13, K-24, K-28, §3.1 S-1/S-5/S-6, §4. **Kaynak:** [`doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`](../../../doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md) §1.2.

**Dal:** `feat/p15-guvenlik`

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- 2026-09-02 master §4 ve 2026-11-13 master §1/§4 sözleşmeleri geçerli.
- **Her görevde kırmızı test önce.** Raporun "Senaryo → yanlış sonuç" sütunu o görevin kırmızı testidir.
- **Yetki testi kuralı:** değişen her yazma ucu için "başka öğretmen", "bağı arşivlenmiş öğretmen", "bağsız kullanıcı" → 403 testi.
- **Hata kodları:** yeni hatalar `<modul>.<hata>` biçiminde; aşağıda adı verilenler aynen kullanılır.
- **Geriye uyumluluk:** mobil bu görevlerde değişmiyorsa (T1–T6, T8) mevcut mobil çağrıları kırılmamalı; değişen yanıt alanları yalnız eklenir.
- **Doküman:** her görev sonunda ilgili `doc/modules/mNN_*.md` + rapordaki N-ID satırına `✅ (P15 Tn)`.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Güvenli kod üreteci + davet kodlarının sertleştirilmesi (N-20) ✅

**Files:**
- Create: `src/Shared/Kernel/ISecureCodeGenerator.cs`, `src/Shared/Infrastructure/SecureCodeGenerator.cs`
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (kayıt)
- Modify: `src/Modules/Students/Domain/StudentsDomainModel.cs` (`GenerateInviteCode` kaldır; `TeacherStudentLink` ve `StudentParentInvite`'a `InviteExpiresOnUtc`, `IsExpired(now)`)
- Modify: `src/Modules/Students/Application/StudentParentInviteFeatures.cs`, `TeacherStudentLinkFeatures.cs`
- Modify: `src/Modules/Students/Infrastructure/ParentInviteDirectory.cs` (süre + normalize)
- Modify: `src/Modules/Students/Infrastructure/StudentsDbContext.cs` (aktif davet kodu için filtreli benzersiz indeks)
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs`, `ParentPolicies.cs` (claim: `Parent` rolü + deneme limiti)
- Create: `src/Shared/Infrastructure/Caching/InviteClaimAttemptLimiter.cs` (Redis; kullanıcı başına 10 başarısız/saat)
- Migration: `HardenInviteCodes` (Students)
- Test: `tests/Unit/SecureCodeGeneratorTests.cs`, `tests/Unit/InviteCodeHardeningTests.cs`, `tests/Integration/RealInviteClaimTests.cs`
- **Yürütmede eklenen (2026-11-13):** `src/Shared/Application/IInviteClaimAttemptLimiter.cs` (arayüz Students ve Parents'ın ortak bağımlılığı olduğu için Shared/Application'da), `src/Shared/Contracts/ParentInviteContract.cs` (`ParentInviteInfo` + `InviterUserId`, `ExpiresOnUtc`), `src/Modules/Students/Infrastructure/TeacherStudentLinkRepository.cs` (bekleyen davet öncelikli arama), `src/Modules/Students/API/StudentsModule.cs` + `src/Modules/Parents/API/ParentsModule.cs` (410/403/429 eşlemeleri), mevcut testler `ClaimParentInviteTests`, `StudentClaimTests`, `TeacherStudentLinkTests`, `StudentParentInviteTests` (yeni imzalar; veli claim sertleştirme testleri `ClaimParentInviteTests`'e eklendi). `InviteOptions.ExpiryDays` yerine domain sabiti `InvitePolicy.ExpiryDays = 7` (YAGNI). Kendi kodunu kullanma hatası iki modülde ayrı: `students.cannot_claim_own_invite`, `parents.cannot_claim_own_invite`; öğrenci claim'i için ayrıca `students.claim_requires_student_role`.

**Interfaces:**
- Produces:
  - `public interface ISecureCodeGenerator { string NewCode(int length = 8); }` — alfabe `23456789ABCDEFGHJKMNPQRSTUVWXYZ` (karışan `0 O 1 I L` yok), `RandomNumberGenerator.GetInt32`.
  - `TeacherStudentLink.MarkInviteSent(string code, Guid? targetUserId, DateTime expiresOnUtc, DateTime nowUtc)` → `Result`
  - `StudentParentInvite(..., DateTime expiresOnUtc, ...)`
  - Hatalar: `students.invite_expired`, `parents.invite_expired`, `shared.too_many_attempts` (429), `parents.claim_requires_parent_role`, `students.cannot_claim_own_invite`.
- Consumes: `IClock`, `IRedisConnection` (mevcut).

- [x] **Step 1: Kırmızı testleri yaz**
  - `SecureCodeGeneratorTests`: 8 karakter, yalnız alfabe, 10.000 üretimde çakışma yok, `Random.Shared` kullanılmıyor (kaynak taraması: `grep "Random.Shared" src/Modules/Students` boş).
  - `InviteCodeHardeningTests`: süresi geçmiş veli daveti claim → `parents.invite_expired`; `Teacher` rolündeki kullanıcı veli kodu claim → `parents.claim_requires_parent_role`; daveti üreten kullanıcı kendi kodunu claim → `students.cannot_claim_own_invite`; küçük harfli kod normalize edilip kabul.
  - `RealInviteClaimTests` (Testcontainers): 10 yanlış kod denemesinden sonra 11. deneme **429**; doğru kod da o saat içinde 429 (kilit).
- [x] **Step 2: Kırmızı gör** — `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~SecureCodeGenerator|FullyQualifiedName~InviteCodeHardening"`
- [x] **Step 3: Üreteci yaz + DI kaydı; iki handler'ı üreteci kullanacak şekilde değiştir.** Davet süresi 7 gün (`InviteOptions.ExpiryDays`, varsayılan 7).
- [x] **Step 4: Migration** — `InviteExpiresOnUtc` kolonları; **mevcut bekleyen davetler için `Up`: `UPDATE ... SET "InviteExpiresOnUtc" = now() + interval '7 days' WHERE status = Pending`**; filtreli benzersiz indeks `(InviteCode) WHERE Status = Pending`. Önce aynı kodlu bekleyen tekrarları SQL ile en yenisi hariç `Expired` yap.
  `dotnet ef migrations add HardenInviteCodes --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext` → `./scripts/check-migration-down.sh`
- [x] **Step 5: Claim yolları** — `ClaimParentInvite` ve öğrenci `links/claim`: rol kontrolü, süre, kendi kodu, limiter (başarısızda artır, başarıda sıfırla).
- [x] **Step 6: Yeşil gör** — `dotnet test EgitimUssu.slnx` + `./scripts/test-with-docker.sh` (integration atlanmamalı)
- [x] **Step 7: Doküman + commit** — `doc/modules/m03_students.md`, `m09_parents.md` (kod biçimi, süre, limit), rapor N-20 ✅.
  ```bash
  git commit -m "fix(security): davet kodlari CSPRNG + sureli + deneme limitli + rol kontrollu (N-20)"
  ```

---

### Task 2: Erişim kapsamı sözleşmesi + yazma uçlarında bağ kontrolü (N-08, N-21) ✅

**Files:**
- Create: `src/Shared/Contracts/TeacherStudentAccessContract.cs`
- Create: `src/Modules/Students/Infrastructure/TeacherStudentAccess.cs` (+ DI)
- Modify: `src/Modules/Students/Application/StudentParentInviteFeatures.cs` (authorizer)
- Modify: `src/Modules/Scheduling/Application/LessonSchedulePolicies.cs` (create/update/reschedule)
- Modify: `src/Modules/Payments/Application/PaymentPolicies.cs` (create/update)
- Modify: `src/Modules/LessonSessions/Application/LessonSessionPolicies.cs` (manuel create + `LessonScheduleId` sahipliği)
- Modify: `src/Shared/Contracts/LessonScheduleReadContract.cs` (sahiplik okuması gerekiyorsa) — **gerekmedi**: `LessonScheduleDetails` zaten `TeacherUserId` + `StudentId` taşıyor
- Test: `tests/Unit/TeacherStudentAccessTests.cs`, `tests/Unit/WriteEndpointLinkAuthorizationTests.cs`
- **Yürütmede eklenen (2026-11-13):** `src/Modules/Students/Infrastructure/StudentDirectory.cs` (delegasyon) + `DependencyInjection.cs` (kayıt), `src/Shared/Contracts/StudentDirectoryContract.cs` (açıklama), `src/Modules/LessonSessions/API/LessonSessionsModule.cs` (`lesson_sessions.schedule_not_owned` → **403**; eşleme yoksa varsayılan 400'e düşüyordu, authorizer birim testi bunu göremez), `src/API.Host/Seeding/DemoDataSeeder.cs` (**demo tohumlaması N-08 açığına dayanıyordu**: kendi hesabı olan iki öğrenci `SelfRegistered` profille, öğretmenle **hiç bağ olmadan** açılıp onlara ders/ödeme yazılıyordu → artık öğretmen profili öğrencinin hesabıyla birlikte açar; T3'te davet + claim'e geçer, Öğrenilenler #179), `src/Modules/Students/Infrastructure/*.csproj` (`InternalsVisibleTo`), `tests/Unit/EgitimUssu.Tests.Unit.csproj` (Students.Infrastructure referansı — kapsam kuralı gerçek sorguya karşı test edilsin), `tests/Unit/TestDoubles/FakeTeacherStudentAccess.cs`, `tests/Unit/TestDoubles/FakeLessonScheduleDirectory.cs`, `tests/Unit/LessonSessionTests.cs` + `tests/Unit/PaymentRequestCacheTests.cs` (kurucu imzaları), `tests/Integration/RealWriteLinkAuthorizationTests.cs` (DI kaydı + HTTP eşlemesi + arşiv sonrası 403 / iptal 200 gerçek Postgres'te).
- **Formül netleştirmesi (yürütmede):** ikinci kol "öğretmen bağı arşivli değil" **ve yaşıyor** (`Manual`/`InviteSent`) okunur — `Rejected`/`Disconnected` bağ `None` (eski `IsLinkedTeacherOfStudentAsync` de `false` dönüyordu; "mevcut çağıranlar davranış değiştirmez" ve fail-closed). Profil var ama bağ satırı yoksa `None`. Yazma uçları `!= None` değil **`== Private`** karşılaştırır (P22'de `Class` özel ders yetkisi vermemeli — K-17/K-18). Manuel oturumda plan sahipliği öğretmen **ve öğrenci** eşleşmesi ister ve **Admin'e de** uygulanır (veri bütünlüğü); bilinmeyen plan da `schedule_not_owned`.

**Interfaces:**
- Produces (master §4):
  ```csharp
  public enum TeacherAccessScope { None = 0, Private = 1, Class = 2 }
  public interface ITeacherStudentAccess
  { Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken ct); }
  ```
  `Private` ⇔ (`TeacherStudentLink.Status == Linked` ve arşivli değil) **veya** (profil `TeacherManaged` ve `CreatedByTeacherUserId == teacher` ve öğretmen bağı arşivli değil). Aksi `None`. `Class` P22'ye kadar dönmez.
- Consumes: `IStudentDirectory.IsLinkedTeacherOfStudentAsync` artık `GetScopeAsync == Private`'a delege eder (tek kaynak).

- [x] **Step 1: Kırmızı testler** — her uç için üç senaryo (başka öğretmen / bağı arşivlenmiş öğretmen / bağsız öğretmen) → 403:
  `CreateLessonSchedule`, `CreatePayment`, `CreateLessonSession` (manuel), `CreateParentInvite` (N-21: bağsız öğretmen davet üretemez). Ek: manuel oturumda başka öğretmenin `LessonScheduleId`'si → 403 `lesson_sessions.schedule_not_owned`. **Yürütmede genişletildi** (Global Constraints "değişen her yazma ucu"): `UpdateLessonSchedule`, `RescheduleLessonSchedule`, `UpdatePayment` için de üç senaryo; arşivli öğretmenin iptal/silmesi serbest; Admin muafiyeti; bilinmeyen / başka öğrencinin planı.
- [x] **Step 2: Kırmızı gör** — `--filter "FullyQualifiedName~TeacherStudentAccess|FullyQualifiedName~WriteEndpointLinkAuthorization"`
- [x] **Step 3: Sözleşme + Students implementasyonu**; `IsLinkedTeacherOfStudentAsync`'i delege et (mevcut çağıranlar — Messaging, ProgressTracking, Reporting — davranış değiştirmez).
- [x] **Step 4: Dört authorizer'ı kapsam sorgusuna geçir.** Admin istisnası korunur.
- [x] **Step 5: Yeşil gör** — `dotnet test EgitimUssu.slnx`
- [x] **Step 6: Doküman + commit** — `doc/architecture/backend.md` (erişim kapsamı bölümü), `m03`, `m04`, `m05`, `m07`; rapor N-08, N-21 ✅.
  ```bash
  git commit -m "fix(security): ITeacherStudentAccess + yazma uclarinda bag kontrolu (N-08, N-21)"
  ```

---

### Task 3: Davet kabul/ret, profil bağlama ve merge kuralları (N-22, N-23, N-28)

**Files:**
- Modify: `src/Modules/Students/Domain/StudentsDomainModel.cs` (`Accept`, `Reject`, `MarkInviteSent`, `StudentProfile` kurucu kuralları, `LinkUser` olayı)
- Modify: `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs` (accept/reject/claim/merge)
- Modify: `src/Modules/Students/Application/StudentProfileFeatures.cs`, `StudentProfilePolicies.cs`
- Modify: `src/Modules/Students/API/StudentsModule.cs` (istek gövdesinden `UserId`/`ParentUserId` almayı bırak)
- Create: `src/Modules/Parents/Infrastructure/StudentProfilesMergedParentsHandler.cs`, `src/Modules/ProgressTracking/Infrastructure/StudentProfilesMergedProgressHandler.cs`
- Test: `tests/Unit/TeacherStudentLinkTransitionTests.cs`, `tests/Unit/StudentProfileOwnershipTests.cs`, `tests/Integration/RealStudentMergeIntegrationTests.cs` (genişlet)
- **T2'den devreden (2026-11-13, Öğrenilenler #179–#181):** `src/API.Host/Seeding/DemoDataSeeder.cs` (`EnsureStudentWithOwnAccountAsync` gövdeye `UserId` yazıyor → davet + claim'e geçmeli; veli bağı onayı `KnownStudent.UserId` boş kalacağı için veli davet kodu claim'iyle kurulmalı ya da T4 sırası gözetilmeli), `tests/Integration/RealMessagingChainTests.cs` + `tests/Integration/RealAccountDeletionTests.cs` (aynı `userId` yardımcıları). Merge kabul testi: birleşme sonrası kanonik profilde `ITeacherStudentAccess.GetScopeAsync == Private`.

**Interfaces:**
- Produces:
  - `TeacherStudentLink.Accept(Guid actingUserId, string inviteCode, DateTime now)` → `Result` — yalnız `InviteSent`, kod eşleşmeli, `InviteTargetUserId` doluysa `actingUserId` eşit olmalı. Hata: `students.invalid_link_transition`, `students.invite_code_mismatch`.
  - `MarkInviteSent` → `Linked` bağda `students.link_already_active`.
  - `TeacherManaged` profil: `UserId` ve `ParentUserId` **sunucuda null**; `SelfRegistered`: `CreatedByTeacherUserId` ve `ParentUserId` null.
  - `StudentProfileLinkedToUserIntegrationEvent(Guid StudentId, Guid UserId, DateTime OccurredOnUtc)` (T4 tüketir).
  - Merge: `TeacherStudentLink`'ler aynı transaction'da kanonik ID'ye taşınır (çakışan bağda arşivli olan düşer); Parents (`ParentChildLink`, `KnownStudent`) ve ProgressTracking `StudentProfilesMerged` olayını tüketir.

- [ ] **Step 1: Kırmızı testler** — link ID'si bilinen ama kodu bilmeyen kullanıcı `accept` → hata; `Linked` bağa ikinci `MarkInviteSent` → hata; öğretmen `TeacherManaged` profil oluştururken gövdeye başka kullanıcının `UserId`'sini yazar → profil `UserId == null` ve o kullanıcı kendi profilini açabilir; merge sonrası kanonik profilde öğretmen kapsamı `Private`, veli paneli dolu.
- [ ] **Step 2: Kırmızı gör.**
- [ ] **Step 3: Domain kuralları + handler'lar + uç sözleşmesi** (gövde alanları kabul edilip yok sayılmaz → doğrulayıcı `400 students.field_not_allowed` döner; mobil zaten göndermiyorsa kırılma yok — `grep -rn "userId\|parentUserId" mobile/lib/features/students/data` ile kontrol et).
- [ ] **Step 4: Merge tüketicileri** (idempotent taban).
- [ ] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 6: Doküman + commit** — `m03`, `m09`, `m10`; rapor N-22/23/28 ✅.
  ```bash
  git commit -m "fix(students): davet kabul/merge kurallari + profil baglama sunucu tarafinda (N-22, N-23, N-28)"
  ```

---

### Task 4: Veli erişiminin iptali ve gizlilik varsayılanı (N-26, N-27, N-32 birincil veli)

**Files:**
- Modify: `src/Modules/Parents/Domain/ParentsDomainModel.cs` (`Approve` yalnız `Pending`'den)
- Modify: `src/Modules/Students/Infrastructure/ParentChildLinkApprovedIntegrationEventHandler.cs` + Create `ParentChildLinkEndedIntegrationEventHandler.cs` (Revoked/Rejected → `ParentUserId` temizle)
- Modify: `src/Modules/Students/Application/StudentProfilePolicies.cs` (veli erişimi `IParentAccessDirectory` üzerinden)
- Modify: `src/Modules/Parents/Infrastructure/ParentReadModelProjections.cs` (`StudentProfileLinkedToUser` tüketimi; `UserId` bilinmiyorsa gizlilik `Hidden`)
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs`, `ParentPolicies.cs` (öğrenci kullanıcı onay/ret yetkisi `KnownStudent.UserId` ile)
- Modify: `src/Modules/Parents/Infrastructure/ParentsDbContext.cs` (birincil veli için kısmi benzersiz indeks `(StudentId) WHERE IsPrimaryContact AND Status = Approved`)
- Migration: `ParentLinkIntegrity` (önce tekrar birincilleri en eskisi hariç ikincile çevir)
- Test: `tests/Unit/ParentAccessRevocationTests.cs`, `tests/Integration/RealParentAccessRevocationTests.cs`

**Interfaces:**
- Produces: `ParentChildLink.Approve(...)` → `Result` (`parents.invalid_link_transition`); `ParentChildLinkEndedIntegrationEvent(LinkId, ParentUserId, StudentId, Reason)`.

- [ ] **Step 1: Kırmızı testler** — onaylı veli → bağ iptal → profil okuma **403**; reddedilmiş bağ yeniden `Approve` → hata; davetle bağlanmış öğrenci "Gizli" seçer → veli paneli gizli alanları görmez; davetle bağlanmış öğrenci veli talebini onaylayabilir; aynı öğrenciye eşzamanlı iki birincil veli → biri reddedilir.
- [ ] **Step 2: Kırmızı gör.**
- [ ] **Step 3: Uygula** (fail-closed: `UserId` bilinmiyorsa `Hidden`).
- [ ] **Step 4: Migration + `check-migration-down.sh`.**
- [ ] **Step 5: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 6: Doküman + commit** — `m09`, `roles/veli.md`, `roles/ogrenci.md`; rapor N-26/27 ✅.
  ```bash
  git commit -m "fix(parents): bag iptalinde erisim kesilir + gizlilik fail-closed + tek birincil veli (N-26, N-27)"
  ```

---

### Task 5: Mesajlaşmada süregelen ilişki kontrolü (N-46, K-28)

**Files:**
- Modify: `src/Modules/Messaging/Application/MessagingFeatures.cs` (gönderimde ilişki kontrolü), `MessagingPolicies.cs`
- Modify: `src/Modules/Messaging/Domain/MessagingDomainModel.cs` (`Conversation.IsReadOnly` hesaplanan alanı yanıtta)
- Test: `tests/Unit/MessagingRelationshipTests.cs`

**Interfaces:**
- Produces: gönderim → `403 messaging.relationship_ended`; `ConversationSummaryResponse.IsReadOnly` (yalnız ekleme).
- Consumes: `ITeacherStudentAccess` (T2), `IParentAccessDirectory`.

- [ ] **Step 1: Kırmızı testler** — bağı arşivlenen öğretmen–öğrenci sohbetinde gönderim 403; geçmiş listeleme 200 ve `isReadOnly: true`; veli bağı iptal edilen veli gönderemez; ilişki sürüyorsa davranış değişmez.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Uygula.** **Step 4: Yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m16_messaging.md`; rapor N-46 ✅.
  ```bash
  git commit -m "fix(messaging): iliski bitince gonderim kapanir, gecmis salt-okunur (N-46)"
  ```

---

### Task 6: Üretim sağlayıcı guard'ı + e-posta log maskeleme (N-24, P-01)

**Files:**
- Modify: `src/Shared/Infrastructure/Configuration/EmailOptionsGuard.cs`, `StorageOptionsGuard.cs`, `PushOptionsGuard.cs`
- Modify: `src/Shared/Infrastructure/Email/LoggingEmailSender.cs`
- Modify: `src/API.Host/Program.cs` (guard'lara `IHostEnvironment` geçir)
- Modify: `render.yaml` (üretim servisinde `Email__Provider`, `Storage__Provider` açık tanım, `sync: false`)
- Test: `tests/Unit/ProductionProviderGuardTests.cs`, `tests/Unit/LoggingEmailSenderTests.cs`

**Interfaces:**
- ✅ **Kullanıcı cevabı (2026-11-13):** canlı üretim servisi **yok**, önce proje tamamlanacak → bu guard'ın merge'ü hiçbir şeyi bloklamaz. Üretim ilk kez açılırken Q7 (SMTP) ve Q4 (depolama) cevaplanmış olmalı.
- Kural: `Production`'da `Email:Provider=Logging` → **fail-fast** (`Email:Provider üretimde Logging olamaz`); `Storage:Provider=Local` → fail-fast. `Push:Provider=Logging` **izinli + uyarı** (yük sır taşımaz; Firebase Q8 beklenirken yayın yapılabilsin). `Staging`/`Development` serbest.
- `LoggingEmailSender` gövdeyi **hiçbir ortamda** loglamaz: alıcı (maskeli `a***@d***.com`), konu, şablon adı; Development'ta gövde yalnız `Email:Logging:IncludeBodyInDevelopment=true` ise.

- [ ] **Step 1: Kırmızı testler** — Production + Logging e-posta → guard hata; Production + Local depolama → hata; Production + Logging push → başarı + uyarı; maskeleme biçimi; gövdede `token`/kod geçen e-posta log çıktısında **yok**.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Uygula.**
- [ ] **Step 4: Canlı doğrulama** — `ASPNETCORE_ENVIRONMENT=Production` + varsayılan config ile `dotnet run --project src/API.Host` → açılışta guard mesajıyla çıkış (exit ≠ 0). Çıktıyı ILERLEME'ye yaz.
- [ ] **Step 5: `render.yaml`** — üretim servisine `Email__Provider` (`sync: false`), `Email__Smtp__*` (`sync: false`), `Storage__Provider` (`sync: false`) anahtarları; yorum: "Q7/Q4 cevaplanmadan üretim servisi açılmaz — bilinçli".
- [ ] **Step 6: Yeşil gör + doküman + commit** — `doc/architecture/environments.md`; rapor N-24, P-01 ✅.
  ```bash
  git commit -m "fix(config): uretimde Logging e-posta ve Local depolama reddedilir, e-posta govdesi loglanmaz (N-24, P-01)"
  ```

---

### Task 7: Token ve oturum sertleştirme + mobil tek-uçuş refresh (N-29, N-30, N-31, N-32, N-63, N-67)

**Files:**
- Modify: `src/Modules/Identity/Domain/IdentityDomainModel.cs` (`RefreshTokenSession.RevocationReason`)
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs` (refresh: koşullu güncelleme, reuse yalnız `Rotated`; login: sabit-süreli doğrulama), `DeviceSessionFeatures.cs`
- Modify: `src/Modules/Identity/Infrastructure/IdentityRepositoryAndSecurity.cs` (`TryRotateAsync`: `UPDATE ... WHERE "Id"=@id AND "RevokedOnUtc" IS NULL` → etkilenen satır 1)
- Modify: `src/Modules/Identity/Infrastructure/IdentityDbContext.cs` (bekleyen silme talebi **gerçekten** benzersiz)
- Modify: `src/API.Host/Program.cs` (`OnTokenValidated`: `sid` iptal kümesi + hesap durumu; HSTS)
- Modify: `src/Shared/Infrastructure/Caching/RedisTokenBlacklist.cs` (ms çözünürlük; `sid` kümesi)
- Modify: `src/Shared/Infrastructure/Middleware/DistributedRateLimitMiddleware.cs` (auth uçları IP+hesap sıkı; `/me`, `/refresh`, `/sessions` kullanıcı başına)
- Modify (mobil): `mobile/lib/core/network/token_refresh_interceptor.dart`, `mobile/lib/core/di/injector.dart`, `mobile/lib/features/auth/data/repositories/auth_repository_impl.dart`
- Migration: `SessionRevocationReason` (Identity)
- Test: `tests/Unit/RefreshTokenRevocationReasonTests.cs`, `tests/Integration/RealConcurrentRefreshTests.cs`, `tests/Integration/RealSessionRevocationAccessTests.cs`, `mobile/test/core/network/token_refresh_interceptor_test.dart` (genişlet)

**Interfaces:**
- Produces:
  - `enum SessionRevocationReason { Rotated = 1, UserLogout = 2, DeviceRevoked = 3, OtherSessionsRevoked = 4, PasswordReset = 5, AccountClosed = 6, ReuseDetected = 7 }`
  - Reuse tespiti **yalnız** `Rotated` nedenli token tekrar sunulursa **ve** rotasyonun üzerinden **30 sn'den fazla** geçmişse (`Identity:RefreshReuseGraceSeconds=30`). Bekleme penceresi içinde (eşzamanlı refresh'in kaybedeni, zaman aşımı sonrası yeniden deneme, arka plandan dönen uygulama) düz `401 identity.invalid_refresh_token` — **toplu iptal yok**. Diğer nedenlerde de düz 401.
  - Cihaz iptali / "diğerlerini kapat" / şifre sıfırlama / hesap kapanışı → ilgili `sid`'ler Redis iptal kümesine (TTL = access token ömrü); `OnTokenValidated` `sid` kümesini ve `UserAccountStatus`'u (Redis'te önbellekli) kontrol eder.
  - Mobil: tek-uçuş refresh (`Completer` paylaşımı); 401 alan istek, gönderildiği token depodakinden farklıysa **refresh etmeden** yeni token'la tekrarlanır; refresh yalnız **400/401** dönerse çıkış; ağ hatası/timeout → istek hatası olarak yukarı iletilir, oturum korunur.

- [ ] **Step 1: Kırmızı testler (backend)** — telefon girişi "diğerlerini kapat" ile tableti iptal eder → tablet refresh **401** ama telefon oturumu **yaşar** (N-29); aynı refresh token'la iki paralel istek → **yalnız biri** yeni token alır **ve kazananın yeni token'ı çalışmaya devam eder** (kaybeden toplu iptal tetiklemez) (N-30, Testcontainers); rotasyondan 31 sn sonra eski token sunulursa toplu iptal (A-15 korunur); cihazı iptal edilen oturumun access token'ı sonraki istekte **401** (N-31); aynı saniyede verilen token iptal edilir (N-32); var olmayan kullanıcıyla giriş süresi var olanla aynı mertebe (hash yine çalışır).
- [ ] **Step 2: Kırmızı testler (mobil)** — 5 paralel 401 → **tek** refresh çağrısı; refresh timeout → `logout` çağrılmaz, istek `ApiException(network)` ile döner; refresh 401 → çıkış.
- [ ] **Step 3: Kırmızı gör** — `dotnet test ... --filter "FullyQualifiedName~RefreshTokenRevocationReason"`, `cd mobile && flutter test test/core/network`
- [ ] **Step 4: Backend uygula + migration** (`Up`: mevcut iptal edilmiş oturumlar `Rotated` kabul edilir).
- [ ] **Step 5: Mobil uygula.**
- [ ] **Step 6: Yeşil gör** — `./scripts/test-with-docker.sh`, `cd mobile && flutter test && flutter analyze --fatal-infos`
- [ ] **Step 7: Doküman + commit** — `m01_identity.md`, `doc/architecture/mobile_flutter.md` (refresh akışı), `mimari_inceleme.md` (A-15/A-06 notu); rapor N-29/30/31/32(kısmi)/63/67 ✅.
  ```bash
  git commit -m "fix(auth): iptal nedeni, kosullu refresh, sid iptali + mobil tek-ucus refresh (N-29..N-31, N-63, N-67)"
  ```

---

### Task 8: İş kirası yenileme + kirasız arka plan servisleri (N-41, A-07)

**Files:**
- Modify: `src/Shared/Infrastructure/Persistence/JobLeaseStore.cs` (sahip yenileyebilir)
- Modify: `src/Shared/Infrastructure/Messaging/LeasedBackgroundService.cs`
- Modify: `src/Modules/Identity/Infrastructure/AccountDeletionService.cs`, `src/Shared/Infrastructure/Persistence/DataRetentionService.cs` → `LeasedBackgroundService`
- Modify: `tests/Integration/RealJobLeaseTests.cs` (yanlış davranışı kilitleyen satırlar düzeltilir)
- Test: `tests/Integration/RealJobLeaseRenewalTests.cs`

**Interfaces:**
- SQL koşulu: `WHERE {table}."ExpiresOnUtc" <= {now} OR {table}."OwnerId" = EXCLUDED."OwnerId"`; InMemory dalında da aynı kural.

- [ ] **Step 1: Kırmızı test** — aynı instance 15 sn aralıkla 3 kez `TryAcquireAsync` → **3'ü de true**; başka instance kira süresi dolmadan → false. `OutgoingEmailDispatcher` için "e-posta 15 sn içinde gönderilir" senaryosu.
- [ ] **Step 2: Kırmızı gör.** **Step 3: Uygula** (S-6: önce kira düzeltmesi, sonra iki servisin kiraya alınması). **Step 4: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 5: Doküman + commit** — `doc/architecture/backend.md` (kira), `mimari_inceleme.md` A-07 notu; rapor N-41 ✅.
  ```bash
  git commit -m "fix(jobs): kira sahibince yenilenir, silme ve saklama servisleri kiraya alindi (N-41, A-07)"
  ```

---

### Task 9: Kapanış — çapraz-kiracı yetki paketi + doküman

**Files:**
- Create: `tests/Integration/RealCrossTenantAccessTests.cs`
- Modify: `tests/Integration/AuthorizationCoverageTests.cs` (yüklenemeyen assembly → **test başarısız**, sessiz atlama yok)
- Modify: `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md` (§1.2, §1.6 satırları ✅), `doc/modules/mimari_inceleme.md`

- [ ] **Step 1: Çapraz-kiracı paketi** — iki öğretmen, iki öğrenci, iki veli ile gerçek Postgres'te: öğretmen B, öğretmen A'nın öğrencisine ders/ödeme/oturum/veli daveti/mesaj **yazamaz** ve profil/ödeme/oturum **okuyamaz**; veli B, veli A'nın çocuğunu göremez; iptal edilen veli artık göremez. **T2'den devreden (Öğrenilenler #185):** bağı arşivlenmiş öğretmen eski planından `from-schedule` → ücretli tamamlama yaptığında **otomatik ders ücreti oluşmaz** (bugün oluşuyor — `GetAgreedLessonRateAsync` arşivi elemiyor); `link-parent` ve ödev uçlarının arşiv davranışı karara bağlanır. T2'nin `RealWriteLinkAuthorizationTests`'i bu pakete taşınabilir/genişletilebilir.
- [ ] **Step 2: Koş** — `./scripts/test-with-docker.sh` → başarısız 0, atlanan 0.
- [ ] **Step 3: Doküman kapanışı** + ILERLEME "Öğrenilenler".
  ```bash
  git commit -m "test(security): capraz-kiraci yetki paketi + P15 kapanisi"
  ```
