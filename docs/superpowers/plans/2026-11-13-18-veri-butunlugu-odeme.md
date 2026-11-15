# P18 — Veri Bütünlüğü ve Ödeme (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Özel öğretmenin finansal kayıtları güvenilir olsun: eşzamanlı güncellemeler birbirini sessizce ezmesin, tahsilat bir hareket geçmişi olarak tutulsun, tutar girişi Türkçe biçimde doğru ayrıştırılsın, ağ tekrarında çift kayıt oluşmasın.

**Architecture:** K-22: kritik aggregate'lar `IVersioned` (`uint Version` → Npgsql `xmin`) taşır; çakışma `409 shared.concurrency_conflict`. Ödeme kaydında tam PUT yerine niyete dayalı komutlar (`AddCollection`, `Refund`, `Cancel`, `UpdateDetails`); durum tutarlardan **türetilir**. Mobil para `int` kuruş (K-23) ve her yazma isteği `Idempotency-Key` taşır; sunucu anahtarla birlikte gövde özetini saklar.

**Tech Stack:** .NET 9, EF Core 9 / Npgsql 9.0.4, Redis, Flutter, xUnit, Testcontainers.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — K-22, K-23, §3.1 S-4, §4 sürüm + idempotency. **Kaynak:** rapor §1.1 (N-09, N-10, N-11 para birimi), §1.4 (N-64, N-65), §1.3 (N-55 idempotency), §1.5.

**Dal:** `feat/p18-veri-butunlugu`

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Önceki tüm sözleşmeler.
- **T1 revizyonu (2026-11-15):** Npgsql 9.0.4 scaffold C# dosyasına sistem kolonu AddColumn/DropColumn yazabilir; açık xmin yedeği de aynıdır. Sağlayıcı SQL üreticisinde sistem kolonları atılır. Önce her modül için ileri/geri SQL’de DDL olmadığını kanıtla; sonra yalnız bu etkisiz sistem-kolonu işlemlerini boş Up/Down + açıklama yorumuna indir, Designer/Snapshot korunsun. Convention API’de `IsConcurrencyToken(true)` + `ValueGenerated(OnAddOrUpdate)` `IsRowVersion()` karşılığıdır; EF1001 üreten mutable builder kurucusu kullanılmaz.
- **xmin eşlemesi:** `public uint Version { get; private set; }` + `builder.Property(x => x.Version).IsRowVersion()`. Npgsql 9 bunu `xmin` sistem kolonuna eşler; migration **kolon eklememeli**. Ekliyorsa dur, ILERLEME'ye yaz, `HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` biçimini dene.
- Para alanları `decimal(18,2)`; para birimi ISO 4217 3 harf.
- Mobil değişen ekranlar ARB'ye taşınır.

---

### Task 1: Concurrency token altyapısı (§1.5) ✅

**Files:**
- Create: `src/Shared/Kernel/IVersioned.cs`
- Modify: `src/Shared/Infrastructure/Persistence/ModuleDbContext.cs` (`IVersioned` uygulayan entity'lere `IsRowVersion()` konvansiyonu)
- Modify: `src/Shared/Infrastructure/Middleware/ProblemDetailsExceptionMiddleware.cs` (`DbUpdateConcurrencyException` → 409)
- Verify: `src/Shared/Infrastructure/Persistence/EfOutboxStore.cs`, `src/Shared/Infrastructure/Messaging/DispatchingEventBus.cs`, `src/Shared/Infrastructure/Messaging/IdempotentIntegrationEventHandler.cs` (mevcut retry/backoff + yeni publish scope davranışı gerçek çakışma, rollback ve tekrar teslimle kanıtlanır; yalnız test açığı çıkarsa değiştirilir)
- Modify: `src/Modules/Payments/Domain/PaymentsDomainModel.cs`, `src/Modules/Membership/Domain/MembershipDomainModel.cs`, `src/Modules/Study/Domain/StudyDomainModel.cs`, `src/Modules/ProgressTracking/Domain/ProgressTrackingDomainModel.cs`, `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs`, `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`, `src/Modules/Students/Domain/StudentsDomainModel.cs`, `src/Modules/Parents/Domain/ParentsDomainModel.cs` (yalnız listelenen 10 tipe `IVersioned` + `Version`)
- Migration: yalnız bu sekiz modülün `Infrastructure/Migrations/*AddVersionToken*.cs` + `*DbContextModelSnapshot.cs` (Up/Down boş; xmin sistem kolonudur)
- Docs: `doc/architecture/backend.md`, `doc/modules/mimari_inceleme.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/modules/m03_students.md`, `m04_scheduling.md`, `m06_assignments.md`, `m07_payments.md`, `m09_parents.md`, `m10_progress_tracking.md`, `m17_membership.md`, `m08_study.md` (modül dosya adları koddan/INDEX'ten doğrulanır), `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`, bu plan; ayrı ilerleme commit'i: `docs/superpowers/ILERLEME.md`
- Test: `tests/Unit/ConcurrencyConventionTests.cs`, `tests/Integration/RealConcurrencyTokenTests.cs`
- Modify: `tests/Integration/RealLateCancellationChargeTests.cs` (mevcut göç kanıtı sondan ikinci yerine PendingOccurrenceChangeRequest göçünden önceki kimliği seçer; yeni migration eklenince yanlış şemaya düşmemeli)
- Modify: `tests/Unit/EgitimUssu.Tests.Unit.csproj` (Parents/Study Infrastructure model testleri için eksik başvurular)

**Interfaces:**
- `public interface IVersioned { uint Version { get; } }`
- Hata gövdesi: `{ code: "shared.concurrency_conflict", message: "Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin." }`

- [x] **Step 0: İmza** — `IVersioned` ve 10 tipte davranışsız `uint Version { get; private set; }`; konvansiyon/409 eklemeden derle.
- [x] **Step 1: Kırmızı testler** — konvansiyon testi: listedeki her entity için model metadatasında `Version` `IsConcurrencyToken == true`; Testcontainers: aynı `PaymentRecord`'u iki context'te okuyup ikisini de kaydet → ikincisi `DbUpdateConcurrencyException`; uçtan: 409 + kod.
- [x] **Step 2: Kırmızı gör.**
- [x] **Step 3: Uygula** + her modülde migration üret ve **boş olduğunu doğrula** (`git diff --stat` yalnız Designer/Snapshot). Boş değilse Global Constraints'teki yedek biçim.
  `dotnet ef migrations add AddVersionToken --project <sekiz etkilenen modül> --startup-project src/API.Host/EgitimUssu.API.Host.csproj --no-build` (tüm modülleri gezen betik yerine yalnız etkilenenler) → `./scripts/check-migration-down.sh`
- [x] **Step 4: Yeşil gör** — `./scripts/test-with-docker.sh`
- [x] **Step 5: Doküman + commit** — `doc/architecture/backend.md` (eşzamanlılık), `mimari_inceleme.md`; rapor §1.5 ✅.
  ```bash
  git commit -m "feat(persistence): kritik aggregate'larda xmin concurrency token + 409 eslemesi (K-22)"
  ```

---

**Doğrulama (2026-11-15):** İmza Release 0/0. 11/12 unit ve 3/3 gerçek PostgreSQL kırmızı → 12/12 unit ve 3/3 gerçek PostgreSQL yeşil. İlk tam pakette yeni göçün ortaya çıkardığı positional eski migration testi kırmızı → adla hedeflenen sınırla yeşil; bağımsız incelemedeki yeni testin aynı varsayımı da düzeltildi. Son `bash scripts/test-with-docker.sh`: **19 mimari / 1208 birim / 157 integration**, başarısız **0**, atlanan **0**. Release `-warnaserror --no-incremental`: **0 uyarı / 0 hata**. Mobil değişmedi, Flutter koşulmadı. Sekiz göçün ileri/geri SQL’i DDL içermez; gerçek PostgreSQL başlangıç göçleri uygulandı. Migration drift **0**; **163 Down** kontrolü (9 no-op) yeşil. Doc health RED **0**, kapsam dışı 2 YELLOW korundu.

### Task 2: Tahsilat hareketleri ve niyete dayalı ödeme komutları (N-09, N-10)

**Files:**
- Modify: `src/Modules/Payments/Domain/PaymentsDomainModel.cs` (`PaymentTransaction` çocuk entity; `AddCollection`, `Refund`, `Cancel`, `UpdateDetails`; `CollectedAmount` ve `Status` türetilir; `UpdateManualTracking` kaldırılır)
- Modify: `src/Modules/Payments/Application/PaymentFeatures.cs` (yeni komutlar; `UpdatePaymentRecordCommand` yalnız not/vade/beklenen tutar)
- Modify: `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` (onay → `AddCollection(method: ParentDeclaration)`)
- Modify: `src/Modules/Payments/Application/PaymentPolicies.cs` (durum–tutar doğrulamaları kalkar, çünkü durum türetiliyor)
- Modify: `src/Modules/Payments/API/PaymentsModule.cs` (uçlar)
- Modify: `src/Modules/Payments/Infrastructure/PaymentsDbContext.cs`, `PaymentRecordRepository.cs`
- Modify: `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs` (fark yerine hareket olayı)
- Migration: `PaymentTransactions` — mevcut `CollectedAmount > 0` kayıtları için tek sentetik `Collection` hareketi (`Method = Migrated`, `OccurredOnUtc = UpdatedOnUtc`)
- Test: `tests/Unit/PaymentTransactionTests.cs`, `tests/Integration/RealPaymentCollectionConcurrencyTests.cs`

**Interfaces:**
- `enum PaymentTransactionKind { Collection = 1, Refund = 2 }`, `enum PaymentMethod { Cash = 1, BankTransfer = 2, Card = 3, ParentDeclaration = 4, Other = 5, Migrated = 99 }`
- Uçlar: `POST /api/payments/records/{id}/collections` `{ amount, method, occurredOnUtc, note }`, `POST /api/payments/records/{id}/refunds`, `POST /api/payments/records/{id}/cancel`, `PUT /api/payments/records/{id}` (yalnız ayrıntılar).
- Türetilmiş durum: `Cancelled` (iptal) · `Paid` (tahsil ≥ beklenen) · `PartiallyPaid` (0 < tahsil < beklenen) · `Overdue` (tahsil < beklenen ve vade geçmiş, yerel gün) · `Pending`.
- Olay: `PaymentTransactionRecordedIntegrationEvent(PaymentRecordId, TransactionId, Kind, Amount, Currency, OccurredOnUtc)`.

- [ ] **Step 1: Kırmızı testler** — veli beyanı onayı (500) sonrası öğretmen not düzeltmesi → tahsilat **500 kalır**, durum `Paid`; iki paralel 250 tahsilat (Testcontainers) → toplam 500 **veya** biri 409 — sessiz kayıp **yok**; iade → tahsil düşer, durum yeniden hesaplanır; iptal edilmiş kayıt özette beklenen toplama girmez; aylık rapor tahsilatı hareket toplamına eşit.
- [ ] **Step 2: Kırmızı gör.**
- [ ] **Step 3: Domain + komutlar + uçlar + migration + projeksiyon.** Projeksiyonu yeniden kur (`IProjectionRebuilder`).
- [ ] **Step 4: Yeşil gör** — `./scripts/test-with-docker.sh`
- [ ] **Step 5: Doküman + commit** — `m07_payments.md` (uç envanteri, durum türetme tablosu), `00_genel_bakis.md`, `veri_modeli.md`; rapor N-09/N-10 ✅.
  ```bash
  git commit -m "feat(payments): tahsilat hareketleri + niyete dayali komutlar, durum turetilir (N-09, N-10)"
  ```

---

### Task 3: Para birimi bazlı özetler ve veli beyanı tekliği (N-11 para birimi, N-16 beyan maddesi)

**Files:**
- Modify: `src/Modules/Payments/Application/PaymentFeatures.cs` (özet yanıtı `totals: [{ currency, expected, collected, overdue }]`)
- Modify: `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` (kayıt başına tek bekleyen beyan; onayda diğer bekleyenler otomatik `Rejected` + veliye bildirim)
- Modify: `src/Modules/Payments/Infrastructure/PaymentsDbContext.cs` (kısmi benzersiz indeks `(PaymentRecordId) WHERE Status = Declared`)
- Migration: `SinglePendingDeclaration` (önce fazla bekleyenleri en yenisi hariç `Rejected`)
- Test: `tests/Unit/PaymentSummaryCurrencyTests.cs`, `tests/Unit/ParentDeclarationUniquenessTests.cs`

- [ ] **Step 1: Kırmızı testler** — TRY + USD kayıtlar → iki ayrı toplam; aynı kayda ikinci bekleyen beyan → 409 `payments.declaration_pending`.
- [ ] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `m07`; rapor N-11 (para birimi), N-16 (beyan maddesi) ✅.
  ```bash
  git commit -m "fix(payments): ozetler para birimi bazinda, odeme basina tek bekleyen veli beyani (N-11, N-16)"
  ```

---

### Task 4: Mobil para ve idempotency (N-64, N-65, N-09 mobil)

**Files:**
- Create: `mobile/lib/core/money/money.dart` (`Money { int minor; String currency; }`, `parseTr(String)` → `"1.500"`=150000, `"750,50"`=75050, `"1.500,5"`=150050; `formatTr`)
- Modify: `mobile/lib/core/network/api_client.dart` (`idempotent: true` → `Idempotency-Key` UUID; yeniden denemede aynı anahtar)
- Modify: `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`, `.../widgets/collect_payment_sheet.dart`, `.../cubit/payments_cubit.dart` (tahsilat → `POST .../collections`; istemcide toplam hesabı **kaldırılır**)
- Modify: `mobile/lib/features/payments/data/**` (modeller `int` kuruş; JSON `decimal` ↔ kuruş dönüşümü tek yerde)
- Modify: ödev teslimi, mesaj gönderme, ödeme oluşturma, ders oluşturma repository'leri (`idempotent: true`)
- Test: `mobile/test/core/money/money_test.dart`, `mobile/test/core/network/idempotency_key_test.dart`, `mobile/test/features/payments/payment_form_parsing_test.dart`

- [ ] **Step 1: Kırmızı testler** — `parseTr` tablo testi (`"1.500"`, `"1500"`, `"750,50"`, `"0,5"`, `"1.500,50"`, `"abc"`→hata, `"-5"`→hata); aynı işlem yeniden denendiğinde aynı anahtar, yeni işlemde farklı anahtar; tahsilat sheet'i yeni ucu çağırır ve toplam göndermez; ödeme formu `"1.500"` girişini 1.500,00 TL olarak gönderir.
- [ ] **Step 2: Kırmızı gör** — `cd mobile && flutter test test/core/money test/core/network test/features/payments`
- [ ] **Step 3: Uygula.** **Step 4: Yeşil gör** — `flutter test && flutter analyze --fatal-infos`
- [ ] **Step 5: Doküman + commit** — `doc/architecture/mobile_flutter.md` (para + idempotency), `doc/pages/payment_form.md`, `payments_list.md`; rapor N-64/N-65 ✅.
  ```bash
  git commit -m "fix(mobile): para kurus olarak, TR bicimi dogru ayristirilir, yazma isteklerinde Idempotency-Key (N-64, N-65)"
  ```

---

### Task 5: Idempotency anahtarında gövde özeti (N-55 idempotency)

**Files:**
- Modify: `src/Shared/Infrastructure/Middleware/IdempotencyMiddleware.cs` (anahtar + SHA-256 gövde özeti; farklı gövde → `422 shared.idempotency_key_reused`)
- Test: `tests/Unit/IdempotencyMiddlewareTests.cs` (genişlet)

- [ ] **Step 1: Kırmızı test** — aynı anahtar + farklı gövde → 422; aynı anahtar + aynı gövde → önbellekli yanıt; mevcut `IdempotencyIdentityIsolationTests` yeşil.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Commit** — rapor N-55 (idempotency) ✅.
  ```bash
  git commit -m "fix(idempotency): anahtar govde ozetiyle eslesir, farkli govde 422 (N-55)"
  ```

---

### Task 6: Kapanış — finans senaryosu

**Files:**
- Create: `tests/Integration/RealTeacherFinanceScenarioTests.cs`

- [ ] **Step 1: Senaryo** — öğretmen ders tamamlar → ücret kaydı (P17 sonrası) → veli 300 TL "ödedim" → öğretmen onay → öğretmen 200 TL nakit tahsilat girer (paralel ikinci cihazdan eski ekranla not düzeltmesi → tahsilat korunur) → kayıt `Paid` → 100 TL iade → `PartiallyPaid` → aylık raporda tahsilat 400, iade 100.
- [ ] **Step 2: Koş** — `./scripts/test-with-docker.sh`, `flutter test`.
- [ ] **Step 3: Doküman + ILERLEME + commit**
  ```bash
  git commit -m "test(payments): ogretmen finans senaryosu, P18 kapanisi"
  ```

*Güncelleme: 2026-11-15 — P18 T1 kapsam doğrulaması: sekiz domain dosyası, boş migration/snapshot kapsamı, mevcut outbox retry kanıtı ve imza adımı.*
