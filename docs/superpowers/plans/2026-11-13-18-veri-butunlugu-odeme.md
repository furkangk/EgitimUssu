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

### Task 2: Tahsilat hareketleri ve niyete dayalı ödeme komutları (N-09, N-10) ✅

**Files:**
- Modify: `src/Modules/Payments/Domain/PaymentsDomainModel.cs` (`PaymentTransaction` çocuk entity; `AddCollection`, `Refund`, `Cancel`, `UpdateDetails`; `CollectedAmount` ve `Status` türetilir; `UpdateManualTracking` kaldırılır)
- Modify: `src/Modules/Payments/Application/PaymentFeatures.cs` (yeni komutlar; `UpdatePaymentRecordCommand` yalnız not/vade/beklenen tutar)
- Modify: `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` (onay → `AddCollection(method: ParentDeclaration)`)
- Modify: `src/Modules/Payments/Application/PaymentPolicies.cs` (durum–tutar doğrulamaları kalkar, çünkü durum türetiliyor)
- Modify: `src/Modules/Payments/API/PaymentsModule.cs` (uçlar)
- Modify: `src/Modules/Payments/Infrastructure/PaymentsDbContext.cs`, `PaymentRecordRepository.cs`
- Modify: `src/Modules/Reporting/Infrastructure/ReportingProjectionHandlers.cs` (fark yerine hareket olayı)
- Migration: `PaymentTransactions` — mevcut `CollectedAmount > 0` kayıtları için tek sentetik `Collection` hareketi (`Method = Migrated`, `OccurredOnUtc = COALESCE(CollectedOnUtc, DueDateUtc)` — kaynak tabloda UpdatedOnUtc yok; gerçek tahsil zamanı öncelikli, yoksa vade geri alınabilir tarihsel yedektir)
- Modify: `src/Modules/Payments/Infrastructure/DependencyInjection.cs`, `PaymentOverdueScanner.cs`, `PaymentsPersonalDataAnonymizer.cs` (DI, yerel vade, hareket notlarının temizliği)
- Create: `src/Shared/Contracts/PaymentTransactionContract.cs`, `src/Modules/Payments/Infrastructure/PaymentTransactionDirectory.cs` (yeniden kurulum için salt okunur kaynak)
- Modify: `src/Modules/Reporting/Domain/ReportingProjections.cs`, `src/Modules/Reporting/Infrastructure/DependencyInjection.cs` (hareket kimliğiyle upsert + tahsil/iade toplamı)
- Create: `src/Modules/Reporting/Infrastructure/PaymentCollectionProjectionRebuilder.cs` (`IProjectionRebuilder`, yalnız tahsilat boyutunu kaynaktan kurar; hosted initializer eski raporu ilk isteği kabul etmeden bir kez geçirir)
- Modify: `tests/Unit/PaymentRequestCacheTests.cs`, `WriteEndpointLinkAuthorizationTests.cs`, `PaymentSearchTests.cs`, `PaymentSummaryOutstandingTests.cs`, `PaymentOverdueScannerTests.cs`, `ParentPaymentDeclarationTests.cs`, `ParentPaymentSettlementIntegrityTests.cs`, `ReportingProjectionTests.cs`, `tests/Integration/RealConcurrencyTokenTests.cs`, `RealProjectionRebuildTests.cs`, `TeacherWorkflowIntegrationTests.cs` (değişen komut/kurucu ve eski tahsilat beklentileri)
- Docs: `doc/modules/m07_payments.md`, `m14_reporting.md`, `00_genel_bakis.md`, `veri_modeli.md`, `doc/roles/ogretmen.md`, `doc/roles/veli.md`, `doc/architecture/backend.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`, bu plan; ayrı ilerleme: `docs/superpowers/ILERLEME.md`
- Modify: `src/API.Host/Seeding/DemoDataSeeder.cs` (demo tam tahsilatın tarihi gelecekteydi; gerçek geçmiş tarih), `tests/Integration/RealJobLeaseRenewalTests.cs` (tek seferlik senkron başlangıç kapısını periyodik BackgroundService sınıflarından ayırır), `doc/architecture/environments.md`
- Test: `tests/Unit/PaymentTransactionTests.cs`, `tests/Integration/RealPaymentCollectionConcurrencyTests.cs`

**Interfaces:**
- `enum PaymentTransactionKind { Collection = 1, Refund = 2 }`, `enum PaymentMethod { Cash = 1, BankTransfer = 2, Card = 3, ParentDeclaration = 4, Other = 5, Migrated = 99 }`
- Uçlar: `POST /api/payments/records/{id}/collections` `{ amount, method, occurredOnUtc, note }`, `POST /api/payments/records/{id}/refunds`, `POST /api/payments/records/{id}/cancel`, `PUT /api/payments/records/{id}` (yalnız ayrıntılar).
- Türetilmiş durum: `Cancelled` (iptal) · `Paid` (tahsil ≥ beklenen) · `PartiallyPaid` (0 < tahsil < beklenen) · `Overdue` (tahsil < beklenen ve vade geçmiş, yerel gün) · `Pending`.
- Olay: `PaymentTransactionRecordedIntegrationEvent(PaymentRecordId, TransactionId, TeacherUserId, Kind, Amount, Currency, OccurredOnUtc)`.

**T2 kapsam/karar (2026-11-15):** Mevcut depo arayüzü değişmez (altı sahte tarandı); hareketler aggregate ile Include/AutoInclude yüklenir. PUT yalnız açıklama/beklenen tutar/vade/not düzenler; eski JSON'un tahsilat/durum alanları yok sayılır. Para birimi ve ilişki kimliği PUT ile değişmez. Yeni olay öğretmen kimliğini de taşır (eski outbox kayıtlarına alan eklenmez). Eski Updated olayı diğer tüketiciler için korunur; aylık tahsilat yalnız hareket olayından, TransactionId upsert'iyle beslenir. Kaynak hareketlerden `IProjectionRebuilder` göç edilmiş tahsilatları da kurar; eski delta olayları yeniden tahsilat sayılmaz. İptal tahsilat sıfırsa serbest, aksi önce iade istenir. İade net tahsilatı aşamaz, iptal kayda hareket eklenmez; pozitif/iki haneli tutar ve geçerli yöntem doğrulanır. Durum sırası Paid → PartiallyPaid → (sıfır tahsilatta yerel vade geçtiyse) Overdue → Pending; gecikme bayrağı kısmi bakiyede ayrıca görünür. Mobil geçiş T4'tedir.

- [x] **Step 0: İmza** — davranışsız hareket metotları/tipleri, komutlar ve kaynak/rebuilder imzaları; Release derle.
- [x] **Step 1: Kırmızı testler** — veli beyanı onayı (500) sonrası öğretmen not düzeltmesi → tahsilat **500 kalır**, durum `Paid`; iki paralel 250 tahsilat (Testcontainers) → toplam 500 **veya** biri 409 — sessiz kayıp **yok**; iade → tahsil düşer, durum yeniden hesaplanır; iptal edilmiş kayıt özette beklenen toplama girmez; aylık rapor tahsilatı hareket toplamına eşit.
- [x] **Step 2: Kırmızı gör.**
- [x] **Step 3: Domain + komutlar + uçlar + migration + projeksiyon.** Projeksiyonu yeniden kur (`IProjectionRebuilder`).
- [x] **Step 4: Yeşil gör** — `./scripts/test-with-docker.sh`
- [x] **Step 5: Doküman + commit** — `m07_payments.md` (uç envanteri, durum türetme tablosu), `00_genel_bakis.md`, `veri_modeli.md`; rapor N-09 backend ✅ (mobil T4 açık), N-10 ✅.
  ```bash
  git commit -m "feat(payments): tahsilat hareketleri + niyete dayali komutlar, durum turetilir (N-09, N-10)"
  ```

---

**Doğrulama (2026-11-15):** Davranışsız domain imzaları Release 0 uyarı/0 hata; 8/8 ilk unit, hareket kimliği aylık rapor ve yerel vade testleri kırmızı → yeşil. İlk PostgreSQL göçte hareket yokluğu, farklı EventId ile eski delta çift sayımı kırmızı → yeşil; yetki testindeki yanlış arşiv URL/204 beklentisi test kurulumu hatası olarak düzeltildi (özellik kırmızısı sayılmadı). Bağımsız inceleme sonrası 1 unit + 4 PG kırmızı: eski500+yeni100 raporu100, notta ikinci gecikme olayı, eski Paid+0, ilk 1.001/gelecek tarih200 → düzeltildi. Son tam **bash scripts/test-with-docker.sh: 19 mimari / 1219 birim / 164 integration**, başarısız **0**, atlanan **0**. Release `-warnaserror --no-incremental`: **0 uyarı/0 hata**, tool restore başarılı, drift **0**, **164 Down** (9 no-op). Yedi yeni gerçek PG testi: paralel tahsilat rollback; veli onayı/eski PUT/iade/yeniden oynatma/kaynak rebuild; yabancı ve arşivli öğretmen; ayrı DB’de eski veri Down/Up; startup eski rapor geçişi; iki ilk tahsilat doğrulaması. Tam paketin yakaladığı eski PUT workflow beklentisi yeni collections adımıyla, seed gelecekte tahsil tarihi ve periyodik-job envanteri kapsamı da düzeltildi; hedefli 7 workflow ve 12 seed/kira testi yeşil. Mobil değişmedi, Flutter koşulmadı. Doc health RED 0, mevcut 2 YELLOW + 1 BLUE kapsam dışında. Sonraki görev yalnız T3; merge/push yok.

### Task 3: Para birimi bazlı özetler ve veli beyanı tekliği (N-11 para birimi, N-16 beyan maddesi) ✅

**Files:**
- Modify: `src/Modules/Payments/Application/PaymentFeatures.cs` (mevcut `CurrencySummaries` korunur; `PaymentMonthlyPointResponse.Currency`, altı ay × para birimi)
- Modify: `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` (kayıt başına tek bekleyen; onayda diğer bekleyenler Rejected; repo imzaları)
- Modify: `src/Modules/Payments/Infrastructure/ParentPaymentDeclarationRepository.cs` (ödeme bazlı okuma ve yalnız ilgili unique ihlalini iş hata koduna çeviren atomik ekle/kaydet)
- Modify: `src/Modules/Payments/Domain/PaymentsDomainModel.cs` (beyan `IVersioned`, onay/ret yarışı kayıp güncelleme yapmaz)
- Modify: `src/Modules/Payments/API/PaymentsModule.cs` (`payments.declaration_pending` → 409)
- Modify: `src/Modules/Payments/Infrastructure/PaymentsDbContext.cs` (kısmi unique `(PaymentRecordId) WHERE Status = 'Declared'`)
- Migration: Payments `*SinglePendingDeclaration*.cs` + snapshot (önce fazla bekleyenleri en yenisi/Id büyük olan hariç Rejected; çözüm olaylarını aynı göçte outbox'a yaz; xmin sistem kolonuna DDL yok)
- Create: `src/Modules/Notifications/Infrastructure/PaymentDeclarationResolvedNotificationHandler.cs`; Modify: `src/Modules/Notifications/Infrastructure/DependencyInjection.cs` (mevcut resolution olayını tüketir; onay/ret alan veliye bildirim, eski olay şeması değişmez)
- Modify: `tests/Unit/ParentPaymentSettlementIntegrityTests.cs` (tek sahte depo yeni imzalar; diğer uygulayanlar rg ile tarandı)
- Test: `tests/Unit/PaymentSummaryCurrencyTests.cs`, `tests/Unit/ParentDeclarationUniquenessTests.cs`, `tests/Integration/RealPaymentDeclarationUniquenessTests.cs` (HTTP eşzamanlı beyan + onay/ret + gerçek göç/Down/Up + bildirim)
- Docs: `doc/modules/m07_payments.md`, `m11_notifications.md`, `00_genel_bakis.md`, `veri_modeli.md`, `doc/roles/veli.md`, `ogretmen.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`, bu plan; ayrı ilerleme `docs/superpowers/ILERLEME.md`

**Interfaces / karar (2026-11-15):** Genel özet zaten para birimi bazlıdır; ikinci `totals` şeması kurulmaz. Aylık nokta yeni `Currency` alanıyla altı ay/para birimi taşır (kayıt yoksa boş). Mobilin bu alanı yok sayıp para birimlerini tekrar toplaması mevcut UI borcudur; T4'te para modelleri/ekran geçişiyle kapatılır, N-11 backend tamamlanır/mobil açık kaydedilir. `IParentPaymentDeclarationRepository`: `AddAsync` yerini `Task<Result> TryAddPendingAsync(...)` alır (tek SaveChanges + outbox, unique yarışında `payments.declaration_pending`); `ListPendingForPaymentAsync(Guid, CancellationToken)` eklenir. İmza adımı boş sonuçlarla derlenir, sonra kırmızı görülür. Onay/ret xmin ile korunur; unique indeks de doğrudan yazanlara uygulanır. Migration'ın reddettiği eski tekrarlar için var olan `ParentPaymentDeclarationResolvedDomainEvent` zarfı üretilir; olay tüketicisi bildirim/inbox'ı atomik yazar. Down kısıtı kaldırır, tarihsel retleri geri açmaz (gerçek kullanıcı kararını geri almamak için).

- [x] **Step 0: İmza** — aylık Currency alanı, repo yeni imzalar/derlenen davranışsız iskelet, beyan Version alanı; Release derle.
- [x] **Step 1: Kırmızı testler** — TRY + USD kayıtlar → iki ayrı toplam; aynı kayda ikinci bekleyen beyan → 409 `payments.declaration_pending`.
- [x] **Step 2–4: Kırmızı gör → uygula (+ migration) → yeşil gör.**
- [x] **Step 5: Doküman + commit** — `m07`; rapor N-11 (para birimi), N-16 (beyan maddesi) ✅.
  ```bash
  git commit -m "fix(payments): ozetler para birimi bazinda, odeme basina tek bekleyen veli beyani (N-11, N-16)"
  ```

---

**Doğrulama (2026-11-15):** İmza Release **0 uyarı/0 hata**; 4/4 yeni unit kırmızı → yeşil, ilgili 19 unit yeşil. PG ilk denemesinde davranışsız Version alanının göç drift’i test kurulumunu kesti; bu sonuç özellik kırmızısı sayılmadı. İmza alanı geçici ignore edilerek gerçek kırmızı görüldü: iki HTTP beyanı da200, bayat ret onayı ezdi, migration yok. Kısmi unique + xmin + dedup göçüyle ilk ikisi/göç yeşil; tüketici eksikliği ayrıca kırmızı → sonuç bildirimiyle yeşil. Göç SQL zarfları gerçek Notifications tüketicisine iki kez teslim edildi, yalnız iki doğru alıcı bildirimi; Down/Up retleri açmıyor ve olayları çoğaltmıyor. Son **bash scripts/test-with-docker.sh: 19 mimari / 1223 birim / 167 integration**, başarısız **0**, atlanan **0**. Release `-warnaserror --no-incremental` **0/0**; tool restore başarılı, drift **0**, **165 Down** (9 no-op). Doc health RED0, mevcut2YELLOW+1BLUE. Mobil kodu değişmedi/Flutter koşulmadı; N-11 mobil boyutu T4 açık. P18 **3/6**, merge/push yok.

### Task 4: Mobil para ve idempotency (N-64, N-65, N-09 mobil) ✅

**Files:**
- Create: `mobile/lib/core/money/money.dart` (`Money { int minor; String currency; }`, `parseTr(String)` → `"1.500"`=150000, `"750,50"`=75050, `"1.500,5"`=150050; `formatTr`)
- Modify: `mobile/lib/core/network/api_client.dart` (`idempotent: true` → `Idempotency-Key` UUID; yeniden denemede aynı anahtar)
- Modify: `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`, `.../widgets/collect_payment_sheet.dart`, `.../cubit/payments_cubit.dart` (tahsilat → `POST .../collections`; istemcide toplam hesabı **kaldırılır**)
- Modify: `mobile/lib/features/payments/data/**` (modeller `int` kuruş; JSON `decimal` ↔ kuruş dönüşümü tek yerde)
- T3 devri: `mobile/lib/features/payments/domain/payment_contracts.dart`, `presentation/pages/payments_page.dart`, `presentation/widgets/**` ve ilgili model/özet testleri: aylık `Currency` alanını tüket; özet/donut/aylık grafiği seçilen para birimine göre göster, para birimleri arasında fold toplama yapma (N-11 mobil). Tahsilat ve iptali yeni collections/cancel uçlarına geçir.
- Modify: ödev teslimi, mesaj gönderme, ödeme oluşturma, ders oluşturma repository'leri (`idempotent: true`)
- Test: `mobile/test/core/money/money_test.dart`, `mobile/test/core/network/idempotency_key_test.dart`, `mobile/test/features/payments/payment_form_parsing_test.dart`

**T4 kapsam doğrulaması / karar (2026-11-15):** Para alanlarının adı korunur ama birimi artık açıkça **int kuruş** olur; tüm ödeme modelleri ve tüketicileri birlikte geçirilir. Gerçek `PaymentRepository` + üç test sahtesi tarandı. API tekrarında anahtar istekten önce üretilir; token yenileme aynı başlığı korur. Belirsiz ağ hatasında tek otomatik tekrar aynı anahtarı/payload'ı kullanır; iki ağ denemesi de sonuçsuzsa manuel tekrar için kullanıcı/uç/aynı içerik anahtarı ve orijinal gövdesi oturum belleğinde korunur; başarılı yanıttan sonra aynı içerikli yeni işlem yeni UUID alır. Hareket zamanı/ilk tahsil zamanı dışındaki alanlar retry kimliğini oluşturur; yeni not/tutar yeni işlemdir. Multipart dosya kimliği yol/boyut/değişiklik anını içerir. Uygulama yeniden başlatılınca devam eden işlem kurtarma bu görevde eklenmez. Çağıran ayrıca açık `idempotencyKey` kullanabilir. Multipart tekrarında FormData clone edilir. Sunucuda gövde özeti T5'tir; T4 bunu tamamlandı saymaz. Ödeme düzenlemesinde tahsilat/durum ve para birimi değiştirme kaldırılır; ilk tahsilat yeni kaydın create sözleşmesinde korunur, sonraki tahsilat/cancel niyet uçlarına gider. Grafikler seçilen para birimiyle filtrelenir.

**Files (ek / kesin yollar):**
- Modify: `mobile/lib/features/payments/domain/payment_contracts.dart`, `data/models/payment_model.dart`, `data/repositories/payment_repository_impl.dart`, `presentation/pages/payments_page.dart`, `presentation/widgets/finance_charts.dart`.
- Modify: `mobile/lib/features/students/presentation/cubit/student_detail_state.dart`, `presentation/pages/student_detail_page.dart` (ödeme int tüketicileri + K-26).
- Modify: `mobile/lib/features/assignments/data/repositories/assignment_repository_impl.dart`, `mobile/lib/features/messaging/data/repositories/messaging_repository_impl.dart`, `mobile/lib/features/scheduling/data/repositories/scheduling_repository_impl.dart` (oluşturma/gönderim/teslim anahtarları).
- Modify: `mobile/test/features/payments/data/payment_model_test.dart`, `presentation/payments_cubit_test.dart`, `presentation/widgets/payment_sheet_layout_test.dart`, `mobile/test/features/dashboard/presentation/cubit/dashboard_cubit_test.dart` (repo sahte imzaları), `mobile/test/features/students/student_subject_link_test.dart`, `mobile/test/features/lesson_sessions/data/lesson_session_repository_impl_test.dart` (ApiClient override imzaları), `mobile/test/features/teacher_profile/photo_upload_test.dart` (postFile override).
- Create: `mobile/test/core/network/write_repository_idempotency_test.dart` (gerçek repository taşıma sözleşmeleri), `mobile/test/features/payments/payment_currency_test.dart`, `payment_collection_request_test.dart`; Modify: `mobile/lib/l10n/app_tr.arb`, `app_en.arb`; Verify/Modify: ilgili mevcut ödeme repository testleri.
- Modify: `mobile/lib/features/dashboard/presentation/cubit/dashboard_cubit.dart` (ayrı eski dashboard modeline kuruş→ana birim adaptörü; dashboard modeli P21 kapsamındadır).
- Docs: `doc/modules/m07_payments.md`, `00_genel_bakis.md`, `doc/roles/ogretmen.md`, `doc/architecture/mobile_flutter.md`, `ux_rules.md`, `doc/pages/payment_form.md`, `payments_list.md`, `students_detail.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`, bu plan; ayrı ilerleme commit'i `docs/superpowers/ILERLEME.md`.

**Interfaces:** `Money(minor, currency)`, `Money.parseTr`, `Money.fromJson`, `toJsonAmount`, `formatTr`; `ApiClient.post/postFile/postMultipart(idempotent: false, idempotencyKey: null, retryIdentity: null)`; `PaymentRepository.addCollection(id, int amount, {int method=1, required DateTime occurredOnUtc, String? note, String? idempotencyKey})`, `cancelRecord(id)`; aylık nokta `currency`.

- [x] **Step 0: İmza** — Money/ApiClient/repository yeni imzaları derlenen davranışsız iskeletle ekle; gerçek + üç sahteyi güncelle; analizle imzayı doğrula.
- [x] **Step 1: Kırmızı testler** — `parseTr` tablo testi (`"1.500"`, `"1500"`, `"750,50"`, `"0,5"`, `"1.500,50"`, `"abc"`→hata, `"-5"`→hata); aynı işlem yeniden denendiğinde aynı anahtar, yeni işlemde farklı anahtar; tahsilat sheet'i yeni ucu çağırır ve toplam göndermez; ödeme formu `"1.500"` girişini 1.500,00 TL olarak gönderir.
- [x] **Step 2: Kırmızı gör** — `cd mobile && flutter test test/core/money test/core/network test/features/payments`
- [x] **Step 3: Uygula.** **Step 4: Yeşil gör** — `flutter test && flutter analyze --fatal-infos`
- [x] **Step 5: Doküman + commit** — `doc/architecture/mobile_flutter.md` (para + idempotency), `doc/pages/payment_form.md`, `payments_list.md`; rapor N-64/N-65 ✅.
  ```bash
  git commit -m "fix(mobile): para kurus olarak, TR bicimi dogru ayristirilir, yazma isteklerinde Idempotency-Key (N-64, N-65)"
  ```

---

**Doğrulama (2026-11-15):** İmza derlenebilirliği analizle kontrol edildi; önce bulunmayan postFile override'ı ve imza lint'leri düzeltildi (özellik kırmızısı sayılmadı). İlk test kurulumundaki yanlış AppConfig parametresi düzeltildikten sonra gerçek RED: **23 başarısız / 1 başarılı** (TR/JSON ayrıştırma, başlık/tekrar yokluğu ve tahsilatta eski PUT); aylık Currency/kuruş testi ayrıca RED → GREEN. Manuel tekrarın yeni UUID üretmesi ayrıca RED → aynı anahtar/orijinal gövde ile GREEN. Gerçek widget→repository→HTTP testleri form `1.500`→1500, edit kilitleri/yalnız ayrıntı PUT, sheet kuruş sonucu, TRY→USD özet+iki grafik ve İngilizce formu doğruladı. Bayat toplam testi sunucunun 300 tahsil/200 kalan yanıtını uygular; aşan tutar istemcide clamp'lenmez. Ödev dosya teslimi/mesaj/ders repository'lerinin gerçek taşıma başlıkları doğrulandı. Ek HTTP testinin eksik tarih yanıtı ve multipart yakalama hatası test kurulumu olarak düzeltildi.

Son **Flutter 331 test**, başarısız **0**; `flutter analyze --fatal-infos` **No issues found**. `flutter pub get` başarılı. Aynı koşumda **bash scripts/test-with-docker.sh: 19 mimari / 1223 birim / 167 integration**, başarısız **0**, atlanan **0**; Release `-warnaserror --no-incremental` **0 uyarı/0 hata**, tool restore başarılı. Backend/migration değişmedi; drift/Down bu mobil görevde yeniden gerekmedi. Doc-health RED **0**, mevcut **2 YELLOW + 1 BLUE**. Platform dosyaları dışarıda. P18 **4/6**, sonraki yalnız T5; merge/push yok.

### Task 5: Idempotency anahtarında gövde özeti (N-55 idempotency) ✅

**Files:**
- Modify: `src/Shared/Infrastructure/Middleware/IdempotencyMiddleware.cs` (anahtar + SHA-256 gövde özeti; farklı gövde → `422 shared.idempotency_key_reused`)
- Modify: `src/Shared/Infrastructure/Caching/IIdempotencyStore.cs`, `RedisIdempotencyStore.cs` (işleniyor işareti ve tamamlanmış yanıtta gövde özeti; aynı anahtar/farklı gövde atomik sahiplenmede de ayrılır)
- Test: `tests/Unit/IdempotencyMiddlewareTests.cs` (genişlet)
- Modify: `tests/Unit/IdempotencyIdentityIsolationTests.cs` (ikinci depo sahtesi yeni imza; izolasyon beklentileri korunur)
- Create: `tests/Integration/RealIdempotencyBodyHashTests.cs` (gerçek Redis: replay, farklı/eşzamanlı gövde, eski kayıt, hata sonrası tekrar; gerçek HTTP ödeme yazımı)
- Docs: `doc/architecture/backend.md`, `doc/modules/00_genel_bakis.md`, `doc/modules/m07_payments.md`, `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`, bu plan; ayrı ilerleme `docs/superpowers/ILERLEME.md`

**Interfaces / T5 kapsam kararı (2026-11-15):** `TryBeginAsync(string key, string requestBodyHash, CancellationToken)`; `IdempotencyOutcome.BodyMismatch`; `IdempotentResponse(..., string? RequestBodyHash = null)`. İki test sahtesi rg ile doğrulandı. SHA-256 ham gövde baytlarından akışla hesaplanır, istek başa sarılır (JSON sıralama/boşluk ve multipart boundary de içeriktir; mobil ilk gövdeyi korur). Hash yalnız yanıtla değil SET NX processing işaretiyle saklanır; farklı gövde iş sürerken de422, aynı gövde409. Redis namespace/TTL korunur:5dk processing/24saat completed. Eski hash’siz tamamlanmış kayıt422 ile reddedilir, eski processing409 kalır; eski kaydı silip yeniden mutasyon üretmek seçilmez. Hatalı istek serbest bırakma ve Redis fail-open mevcut sözleşmedir. N-55 toplu maddesinin yalnız idempotency ayağı kapanır; diğer ayaklar P19 T6’da açık kalır. Kullanıcının devrettiği karar yetkisiyle en düşük riskli geçiş seçildi.

- [x] **Step 0: İmza** — yeni enum/yanıt alanı/depo imzası ve iki sahte, davranış eklemeden Release derle.

- [x] **Step 1: Kırmızı test** — aynı anahtar + farklı gövde → 422; aynı anahtar + aynı gövde → önbellekli yanıt; mevcut `IdempotencyIdentityIsolationTests` yeşil.
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [x] **Step 5: Commit** — rapor N-55 (idempotency) ✅.
  ```bash
  git commit -m "fix(idempotency): anahtar govde ozetiyle eslesir, farkli govde 422 (N-55)"
  ```

---

**Doğrulama (2026-11-15):** İmza Release **0 uyarı/0 hata**. Hedefli ilk unit **4 kırmızı/22 yeşil**, gerçek Redis/HTTP **4 kırmızı/2 yeşil** → uygulamadan sonra **26/26 unit + 6/6 Redis/HTTP yeşil**. Büyük (128 KiB), seek edilemeyen/chunked binary yükleme ve atomik 20 ilk sahiplenme regresyonları eklendi; son tam **bash scripts/test-with-docker.sh: 19 mimari / 1228 birim / 174 integration**, başarısız **0**, atlanan **0**. Release `-warnaserror --no-incremental` **0 uyarı/0 hata**, tool restore başarılı. Aynı tahsilat key/gövde ilk yanıtı döndürür ve tek DB hareketi bırakır; farklı tutar422. Aynı gövde processing409, farklı422; reddedilen tekrar ilk işareti silmez. Eski hash’siz kayıtlar tamamlandı422/processing409; başarısız istekte düzeltilmiş gövde tekrar işlenebilir. Mevcut kullanıcı/anonim/Identity izolasyon testleri korunur. Doc health RED0/mevcut2YELLOW+1BLUE. Migration/mobil değişmedi; drift/Down ve Flutter yeniden gerekmedi. N-55 yalnız idempotency ayağı kapandı; P18 **5/6**, T6 başlamadı, merge/push yok.

### Task 6: Kapanış — finans senaryosu ✅

**T3 ön kontrol kararı (2026-11-15):** Beyan hâlen güncel kalan bakiyeyi tam kapatır (mevcut domain/HTTP sözleşmesi). Eski senaryo 500 borca önce 300 beyan istiyordu ve bu sözleşmeyle çelişiyordu. Önce 200 nakit, sonra kalan 300 beyan sırasıyla aynı 500 tahsil/100 iade sonucu korunur; kısmi beyan özelliği sessizce eklenmez.

**Files:**
- Create: `tests/Integration/RealTeacherFinanceScenarioTests.cs`
- Docs: `doc/modules/m07_payments.md`, `doc/modules/m14_reporting.md`, `doc/modules/00_genel_bakis.md`, bu plan; ayrı ilerleme commit'i `docs/superpowers/ILERLEME.md`.

**Interfaces / T6 kapsam doğrulaması (2026-11-15):** Yeni üretim arayüzü yok. Ders `from-schedule` → oturum `/complete` HTTP yolundan tamamlanır, gerçek `IOutboxProcessor` ile otomatik ücret ve aylık projeksiyon beslenir. Veli bağı davet/claim ile kurulur; beyan doğrudan DB'ye eklenmez. İkinci öğretmen cihazının ayrıntı PUT'u SaveChanges öncesi test bariyerinde beklerken beyan onaylanır; bayat PUT xmin ile409, aynı eski gövde yeniden gönderilince200 ve500 tahsil korunur. Aylık HTTP yanıtı yalnız net `CollectedAmount` içerir;100 iade mevcut `TeacherMonthlyProjection.RefundedAmount` ve hareket kimlikleri üzerinden doğrulanır, bu test görevinde yeni API alanı eklenmez. İmza adımı yalnız test senaryosu iskeletidir; kırmızı kontrol gerçek outbox teslimi bağlanmadan eksik otomatik ücrette görülür (üretim davranışı değişmez).

**T6 test izolasyonu:** Ortak container DB'sinde `RealProgressSnapshotTests` tüm öğrencilerin konu kayıtlarını sayar. Bu senaryonun tamamlamayla ürettiği `TopicMastery` kaydı kendi `finally` bloğunda, yalnız kendi `StudentId`'si için temizlenir (P17 T8'deki desen); başka testin beklentisi değiştirilmez.

- [x] **Step 0: İmza** — senaryo/test yardımcısı iskeletini derle; ardından otomatik ücret beklentisini ekleyip gerçek kırmızıyı gör, outbox teslimini bağla.

- [x] **Step 1: Senaryo** — öğretmen ders tamamlar → 500 TL ücret kaydı (P17 sonrası) → öğretmen 200 TL nakit tahsilat girer → veli kalan 300 TL için "ödedim" → öğretmen onay (paralel ikinci cihazdan eski ekranla not düzeltmesi → tahsilat korunur) → kayıt `Paid` → 100 TL iade → `PartiallyPaid` → aylık raporda tahsilat 400, iade 100.
- [x] **Step 2: Koş** — `./scripts/test-with-docker.sh`, `flutter test`.
- [x] **Step 3: Doküman + ILERLEME + commit**
  ```bash
  git commit -m "test(payments): ogretmen finans senaryosu, P18 kapanisi"
  ```

*Güncelleme: 2026-11-15 — P18 T1 kapsam doğrulaması: sekiz domain dosyası, boş migration/snapshot kapsamı, mevcut outbox retry kanıtı ve imza adımı.*

*Güncelleme: 2026-11-15 — P18 T2 tamamlandı; niyet komutları, veri/rapor geçişi ve regresyon kanıtları.*

*Güncelleme: 2026-11-15 — P18 T3 tamamlandı; aylık Currency, tek bekleyen beyan, güvenli göç ve sonuç bildirimi. T4 mobil devri ve T6 senaryo sözleşme düzeltmesi kaydedildi.*

*Güncelleme: 2026-11-15 — P18 T4 tamamlandı; mobil kuruş/niyet uçları/para birimi ve otomatik-manuel idempotency tekrarları.*

*Güncelleme: 2026-11-15 — P18 T5 tamamlandı; Redis processing/completed gövde özeti ve gerçek HTTP tekrar kanıtı.*

**P18 T6 doğrulama (2026-11-15):** İmza Release0/0; ilk liste isteğinin eksik zorunlu outstandingOnly parametresi test kurulumu hatası olarak düzeltildi. Gerçek RED: outbox iskeletiyle otomatik ücret listesi boş; gerçek IOutboxProcessor bağlanınca senaryo GREEN. Veli davet/claim ve beyan HTTP; ikinci cihazın gerçekten eşzamanlı not PUT'u409, aynı eski gövde tekrar200/Paid500, iade100 sonrası400net/100açık. Kaynakta200Cash+300ParentDeclaration+100Refund ve üç hareket olayı; aylık HTTP1ders/500beklenen/400net, projeksiyon100iade ve aynı hareket kimlikleri. İlk tam pakette yeni TopicMastery kaydı eski global snapshot sayımını2→4 yaptı; yeni test kendi kaydını finally'de temizledi. Son **bash scripts/test-with-docker.sh:19 mimari/1228 birim/175 integration**, başarısız **0**, atlanan **0**. **331 Flutter**; pub get başarılı, fatal-infos **No issues found**, analysis_options geri alındı. Release `-warnaserror --no-incremental` **0uyarı/0hata**, tool restore başarılı. Doc health RED0/mevcut2YELLOW+1BLUE. Yeni migration/mobil değişikliği yok; drift/Down bu görevde yeniden gerekmedi (önceki görevlerin gerçek göç kanıtları yukarıda). P18 **6/6**.

*Güncelleme: 2026-11-15 — P18 T6 tamamlandı; finans zinciri ve test izolasyonu kanıtlandı.*
