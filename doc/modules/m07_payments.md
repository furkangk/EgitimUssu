---
title: "M07 — Ödeme Takibi (Payments)"
summary: "Öğretmenin manuel ödeme takibi + veli 'ödedim' beyan akışı çalışır; vade taraması (kalıcı Overdue + bildirim) ve tamamlanan dersten otomatik ücret kaydı eklendi"
tags: [modul, payments, odeme, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Payments/**
updated: 2026-11-15
---

# 💰 Ödeme Takibi (M07) — Detaylı Tasarım Dokümanı

> **PRD: M07 Ödeme Takibi** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (manuel takip + otomasyon), ⚠️ veli paylaşımı bekliyor**
>
> **Amaç:** Öğretmenin ders/paket ücretlerini **elle** takip etmesi. Ödeme **sistem üzerinden alınmaz**;
> öğretmen "şu dersin ödemesini aldım" şeklinde işaretler (`promp.txt`: "ödemeyi sistem üzerinden
> almayacağız. Öğretmen kendi eliyle girerek takip edecek"). Vade, kısmi tahsilat, gecikme ve para birimi
> bazında özet desteklenir. Ödeme bilgisi **veliyle paylaşılabilir**.
>
> İlgili: [`m09_parents.md`](m09_parents.md) (veli ile paylaşım) · [`m14_reporting.md`](m14_reporting.md) (gelir raporu) ·
> [`m11_notifications.md`](m11_notifications.md) (vade hatırlatma) · [`../roles/ogretmen.md`](../roles/ogretmen.md) ·
> [`../roles/veli.md`](../roles/veli.md) · [`00_genel_bakis.md`](00_genel_bakis.md)

---

### Geç iptal ücreti ve bekleyen ücret temizliği (P17 T5 / N-15)

`LessonCancelledPaymentHandler` yeni Scheduling oluşum olayından pozitif `ChargeAmount` ile `LateCancellation=4` kaydı üretir; tutar/para birimi iptal anında dondurulmuştur, vade tüketim anından +7 gün. `PaymentRecord.LessonScheduleId uuid?` + `OccurrenceStartUtc timestamptz?` kalıcı anahtardır; `CancellationOccurrenceCharges` migration’ı LateCancellation türü için filtreli benzersiz indeks kurar. Eski kayıtlar NULL kalır; yeni tip olduğundan geriye doldurulacak eski geç iptal borcu yoktur. Inbox ve oluşum benzersizliği tekrar borcu engeller.

Kapsam/silme olayında normal LessonFee kayıtlarının oturumu `ILessonSessionAccessService` üzerinden okunur; yalnız hedef oluşumun tahsilatsız Pending/Overdue kaydı `CancelOutstanding` ile Cancelled olur ve güncelleme olayı üretir. Paid/PartiallyPaid veya tahsil tutarı pozitif kayıt otomatik değiştirilmez; tahsilat/iade kararı öğretmendedir. Geç iptal borcu normal ücret temizliğinden etkilenmez. Mobil tahsilat yükü LateCancellation=4 türünü korur.


## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`PaymentRecord`) | ✅ Mevcut | `src/Modules/Payments/Domain/PaymentsDomainModel.cs` |
| Application (CQRS + handler) | ✅ Mevcut | `src/Modules/Payments/Application/PaymentFeatures.cs` |
| API (oluştur/güncelle/getir/listele/özet/filtre/**arama+sayfalama** + veli beyan 4 endpoint) | ✅ Mevcut (**14 endpoint**) | `src/Modules/Payments/API/PaymentsModule.cs` |
| Hesaplanan alanlar (kalan/gecikme/gösterim durumu) | ✅ Mevcut | `PaymentRecordMappings` (`GetOutstandingAmount`, `IsOverdue`, `GetDisplayStatus`) |
| Para birimi bazında özet | ✅ Mevcut | `GetTeacherPaymentSummaryQuery` |
| Veli ile paylaşım (`IsSharedWithParent`) | 🔴 **Yok** | Önerilen — bkz. §2.2 |
| `Overdue` **kalıcı** otomasyonu (M07-1) | ✅ Mevcut | `PaymentOverdueScanner` / `PaymentOverdueScanProcessor` — saatte bir tarar, `PaymentRecord.MarkOverdue` uygular |
| Vade hatırlatma bildirimi (M07-1) | ✅ Mevcut | `PaymentBecameOverdueDomainEvent` → m11 `PaymentOverdueNotificationHandler` |
| Tamamlanan dersten otomatik ücret kaydı (M07-2) | ✅ Mevcut | `LessonSessionCompletedPaymentHandler` (integration event + `IStudentDirectory` ücreti) |
| Mobil ödeme ekranları | ✅ Mevcut | `mobile/lib/features/payments` |

**P18 T6 finans senaryosu (2026-11-15):** `RealTeacherFinanceScenarioTests` gerçek HTTP/PostgreSQL/outbox yolunda ücretli oturumdan tek 500 TRY borç üretir. 200 nakit → kalan 300 için davetle bağlanan velinin beyanı → onay ile 500 tahsil/`Paid` → 100 iade ile 400 net/100 açık/`PartiallyPaid` doğrulanır. Onay sırasında ikinci cihazın kaydetmeyi bekleyen not PUT'u `409 shared.concurrency_conflict` alır; aynı eski 200 tahsil/PartiallyPaid/USD gövdesi tekrar gönderildiğinde yalnız not değişir, 500 tahsil ve TRY korunur. Kalıcı üç hareket 200 Cash + 300 ParentDeclaration + 100 Refund'dır; not yazımı yeni para hareketi üretmez. M14 HTTP aylık raporu 400 net, projeksiyon 100 iade ve kaynakla aynı üç hareket kimliğini taşır. Test ödeme/beyan kayıtlarını elle eklemez.

> **Güncelleme (2026-09-04, P06 Task 5):** Kalıcı statü değişimi artık var. Okuma anındaki
> `GetDisplayStatus(calendar)` hesabı **korunur** (tarama turu arasındaki kayıtlar da gecikmiş görünür);
> `PaymentOverdueScanProcessor` bunun üstüne DB'deki `Status`'ü de `Overdue`'a çeker ve bildirim
> zincirini başlatır. İkisi çelişmez: tarama yalnız zaten gecikmiş olanı kalıcı hâle getirir.

---

### Mobil ödeme önbelleği (P20 T3 / N-68, N-70)

Özet ve öğretmen kayıt listesi ortak `CacheStore` ile oturum kullanıcı kimliğinde saklanır. 403/500 eski tutarları başarı gibi döndürmez; yalnız bağlantı/timeout durumunda son kayıt ve ARB eski veri uyarısı kullanılır. Önbellek boş listeyi de korur. Çıkışta tüm tutarlar ile `ApiClient`'ın bekleyen idempotent gövdeleri/anahtarları temizlenir; eski istek yeni hesapla otomatik tekrar gönderilmez. Yeni oturumdaki manuel işlem yeni anahtar kullanır. Sayfalı finans araması (`searchRecords`) cache kullanmaz.

## 2. Domain Modeli

**P18 T1 — eşzamanlılık (K-22):** `PaymentRecord` `IVersioned` uygular; `uint Version { get; private set; }` PostgreSQL’in ürettiği `xmin` (`xid`) sistem kolonuna eşlenir. `ModuleDbContext` tip modele eklenirken concurrency + OnAddOrUpdate konvansiyonunu uygular. Aynı sürümü okuyan iki yazmadan kaybeden `409 shared.concurrency_conflict` alır: “Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.” `AddVersionToken` yalnız model metadatasını kaydeder; yeni kolon/veri göçü yoktur. Outbox hatayı normal backoff ile yeniden teslim eder; yeni scope güncel veriyi yükler, başarısız iş ve inbox aynı transaction’da geri alınır.

**P18 T2 (2026-11-15):** Tahsilat/iade `PaymentTransaction` çocuk satırlarıdır; net toplam bu hareketlerden hesaplanıp kayıtta saklanır. PUT yalnız `Description`, `ExpectedAmount`, `DueDateUtc`, `Notes` alır. Eski ekranın `collectedAmount`, `status`, `currency` alanları yok sayılır; tahsilat ancak ayrı komutla değişir. P18 T4 ile mobil tahsilat `/collections`, iptal `/cancel` POST uçlarına taşındı; PUT yalnız ayrıntıları gönderir.


### 2.1 🟢 Mevcut (koddan) — `PaymentRecord` (AggregateRoot<Guid>)

`src/Modules/Payments/Domain/PaymentsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Ödeme kaydı kimliği |
| `TeacherUserId` | `Guid` | Kaydı tutan öğretmen |
| `StudentId` | `Guid` | İlgili öğrenci |
| `RelatedLessonSessionId` | `Guid?` | İlişkili ders oturumu (opsiyonel) |
| `ItemType` | enum `BillingItemType` | `LessonFee=1`, `MonthlyPackage=2`, `ManualAdjustment=3`, `LateCancellation=4` |
| `Description` | `string` | Açıklama |
| `Currency` | `string` | Para birimi (varsayılan `"TRY"`; oluşturmada **büyük harfe** çevrilir; sonradan değişmez) |
| `ExpectedAmount` | `decimal` | Beklenen tutar |
| `CollectedAmount` | `decimal` | Tahsil edilen tutar |
| `DueDateUtc` | `DateTime` | Vade |
| `CollectedOnUtc` | `DateTime?` | Tahsil tarihi |
| `Status` | enum `PaymentStatus` | `Pending=1`, `PartiallyPaid=2`, `Paid=3`, `Overdue=4`, `Cancelled=5` |
| `BillingPeriodStartUtc` / `EndUtc` | `DateTime?` | Aylık paket dönemi |
| `Notes` | `string?` | Not |
| `OverdueMarkedOnUtc` | `DateTime?` | **M07-1** — tarama turunun kaydı `Overdue` işaretlediği an; hiç işaretlenmediyse `null` |

> **Not (kodda):** `CreatedOnUtc` constructor'a parametre olarak alınır ancak ayrı bir property olarak
> set edilmez; oluşturma zamanı `PaymentRecordCreatedDomainEvent`'e taşınır.

**Davranış:** `AddCollection` / `Refund` pozitif iki ondalıklı hareket ekler, net toplamı ve durumu yeniden hesaplar. `UpdateDetails` tahsilata dokunmaz. `Cancel`, net tahsilat sıfırken iptal eder; aksi `409 payments.collection_requires_refund` (önce iade). İade net tahsilatı aşamaz (`409 payments.refund_exceeds_collection`); iptal kayda hareket eklenemez (`409 payments.record_cancelled`). Eski `PaymentRecordUpdatedDomainEvent` diğer tüketiciler için korunur; aylık tahsilat yalnız yeni hareket olayından beslenir.

**`PaymentTransaction`:** `Id`, `PaymentRecordId` (FK, cascade), `Kind` (`Collection=1`, `Refund=2`), `Amount decimal(18,2)`, `Method` (`Cash=1`, `BankTransfer=2`, `Card=3`, `ParentDeclaration=4`, `Other=5`, `Migrated=99`), `OccurredOnUtc`, `Note?`. Hareket tutarı/yöntemi/tarihi değişmez; düzeltme iade + yeni tahsilattır. Koleksiyon AutoInclude yüklenir, çocuk kimliği `ValueGeneratedNever`; yeni çocuk ve kökün xmin güncellemesi/outbox aynı transaction'dadır. KVKK anonimleştirmesi hareket notlarını da temizler.

| Durum türetme sırası | Sonuç |
|---|---|
| İptal edilmiş | `Cancelled` |
| Net tahsilat ≥ beklenen | `Paid` |
| 0 < net tahsilat < beklenen | `PartiallyPaid` |
| Sıfır tahsilat, yerel vade günü geçmiş | `Overdue` |
| Diğer | `Pending` |

`IsOverdue`, kısmi bakiyede de yerel vade günü geçmişse true olur. Vade günü boyunca açık kalır (`ILocalCalendar`); saatlik tarayıcı bildirim için kalıcı `Overdue` işaretleyebilir, yanıt durumu yine tutarlardan türetilir.

**Göç:** `PaymentTransactions`, mevcut `CollectedAmount > 0` için kayıt kimliğiyle tek `Collection/Migrated` hareketi açar. Kaynakta `UpdatedOnUtc` yoktur; `CollectedOnUtc`, yoksa `DueDateUtc` kullanılır. Göç iptal durumunu korur, diğer eski durumları net/beklenen tutara göre normalize eder (Paid + 0 gibi tutarsızlıklar giderilir). Down hareket tablosunu kaldırır; kökteki net tutar korunur. Down/Up ayrıntılı tarihçeyi tek sentetik net harekete indirger; geri alma ayrıntı kaybı taşır.

**`MarkOverdue(nowUtc, calendar)` (M07-1, 2026-09-04):** Yalnız `Pending`/`PartiallyPaid` **ve** `calendar.ToLocalDate(DueDateUtc) < calendar.Today()`
olan kaydı `Overdue` yapar, `OverdueMarkedOnUtc` yazar ve `PaymentBecameOverdueDomainEvent` yayar.
Geçiş yapılmaması hata değildir (tarama turu saatte bir koşar) — bu yüzden metot `Result` değil **`bool`**
döner: `true` = durum değişti. Zaten `Overdue`/`Paid`/`Cancelled` olan ya da vadesi gelmemiş kayıt için
`false` döner ve **ikinci bir olay üretilmez** (idempotent).

**Hesaplanan değerler (Application katmanı — `PaymentRecordMappings`):**
| Yöntem | Mantık |
|--------|--------|
| `GetOutstandingAmount()` | `Cancelled` ise **0**; değilse `max(ExpectedAmount - CollectedAmount, 0)` — kalan tutar |
| `IsOutstanding(now)` | kalan > 0 (iptal edilen kayıt zaten 0 kalan döner) |
| `IsOverdue(calendar)` | kalan > 0 **ve** `calendar.ToLocalDate(DueDateUtc) < calendar.Today()` |
| `GetDisplayStatus(calendar)` | yukarıdaki tutar/iptal/yerel vade sırasıyla türetilir |

> **Bug düzeltmesi (2026-07-06):** `GetOutstandingAmount()` önce statüye bakmadan `Expected − Collected`
> döndürüyordu; bu yüzden **iptal edilmiş** ödemelerin tahsil edilmemiş tutarı öğretmen ödeme özetindeki
> `OutstandingAmountTotal`/`ExpectedAmountTotal` yolunda "ödenmemiş bakiye"ye sızıyordu (`IsOutstanding`
> `Status != Cancelled` derken tutarsızdı). Artık iptal edilen kayıt **0 kalan** döner; `IsOutstanding`/`IsOverdue`
> içindeki gereksiz `Status != Cancelled` şartı kaldırıldı. Regresyon testi:
> `tests/Unit/PaymentSummaryOutstandingTests.cs`.

**Enum'lar (koddan birebir):**
```
BillingItemType : LessonFee = 1, MonthlyPackage = 2, ManualAdjustment = 3, LateCancellation = 4
PaymentStatus   : Pending = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Cancelled = 5
```

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `PaymentTransactionRecordedIntegrationEvent` | `PaymentRecordId, TransactionId, TeacherUserId, Kind, Amount, Currency, OccurredOnUtc` — m14 hareket kimliğiyle upsert |
| `PaymentRecordCreatedDomainEvent` | `PaymentRecordId, TeacherUserId, StudentId, RelatedLessonSessionId?, ExpectedAmount, Currency, Status, CreatedOnUtc` |
| `PaymentRecordUpdatedDomainEvent` | `PaymentRecordId, TeacherUserId, StudentId, PreviousStatus, CurrentStatus, PreviousCollectedAmount, CurrentCollectedAmount, UpdatedOnUtc` |
| `PaymentBecameOverdueDomainEvent` (M07-1) | `PaymentRecordId, TeacherUserId, StudentId, OutstandingAmount, Currency, DueDateUtc, MarkedOnUtc` — m11 tüketir (öğretmen + Premium veli bildirimi) |
| `ParentPaymentDeclaredDomainEvent` (Veli V-G) | `DeclarationId, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, CreatedOnUtc` — olay üretilir; öğretmen yeni beyan bildirimi tüketicisi henüz yok (P21 T5), bekleyenler API listesinden okunur |
| `ParentPaymentDeclarationResolvedDomainEvent` (Veli V-G) | `DeclarationId, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, Status, ResolvedOnUtc` |

### 2.1.1 🟢 `ParentPaymentDeclaration` (AggregateRoot<Guid>) — veli "ödedim" beyanı (Veli V-G, 2026-07-19)

Veli bir ödeme kaydı için **beyan** verir (para transferi değil, mutabakat). Alanlar:
`Id, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, Note?, Status (Declared→Confirmed/Rejected), CreatedOnUtc, ResolvedOnUtc?, Version (xmin)`.
`Confirm(now)`/`Reject(now)` yalnız `Declared`'dan çalışır (aksi `InvalidOperationException`). Öğretmen **teyit** edince
`PaymentRecord.MarkCollectedByParentConfirmation(declaredAmount, now, declarationId)` çağrılır → `AddCollection(method=ParentDeclaration)` hareketi eklenir; durum `Paid`, net tahsilat beklenen tutara eşit olur,
`CollectedOnUtc=now` ve mevcut `PaymentRecordUpdatedDomainEvent` yayılır (gelir özeti/veli paneli projeksiyonu bozulmaz).
**2026-10-16 doğruluk kontrolü:** Beyan yalnız `Pending`, `PartiallyPaid` veya `Overdue` durumundaki açık bakiye için verilir. `DeclaredAmount`, `ExpectedAmount - CollectedAmount` değerine tam eşit olmalıdır; akış tam ödeme teyididir. Aynı kurallar teyit anında yeniden kontrol edilir. Ödeme silinmişse `404 payments.record_not_found`; kapanmış/iptal edilmişse `409 payments.record_not_outstanding`; bakiye değişmiş veya beyan tutarı uyuşmuyorsa `409 payments.declaration_amount_mismatch` döner. Başarısız teyitte beyan `Declared` kalır, ödeme ve olaylar değişmez.

**P18 T3 (2026-11-15):** Bir ödeme başına, veli kimliğinden bağımsız olarak **en fazla bir bekleyen beyan** vardır. Ön kontrol ve `ux_payment_declaration_pending` kısmi benzersiz indeksi (`PaymentRecordId WHERE Status = 'Declared'`) birlikte çalışır; ikinci eşzamanlı istek `409 payments.declaration_pending` döner, kaybeden beyan/outbox kalıcılaşmaz. Ret sonrası yeni beyan verilebilir. Onay, aynı ödemedeki varsa diğer eski bekleyenleri aynı `SaveChanges` içinde `Rejected` yapar. Beyanın `xmin` sürümü, bayat bir ret yazımının onayı ezmesini önler (`409 shared.concurrency_conflict`).

`SinglePendingDeclaration` göçü önce en yeni `CreatedOnUtc` / eşitlikte büyük `Id` olan bekleyeni korur, diğerlerini silmeden reddeder; bu retler için mevcut resolution olayını outbox'a aynı transaction'da yazar. Down indeksi geri alır; tarihsel retleri ve bildirim geçmişini geri açmaz. Notifications, onay/ret sonucunu beyanı veren veliye `UserNotification(System)` olarak iletir; inbox aynı olayı tekrar bildirime dönüştürmez. Üyelik kapısı yoktur; bu velinin kendi işlem sonucudur. Rota `/parent`.

Tablo `parent_payment_declarations`. **Karar (2026-07-19):** beyan öğretmen teyidine bağlı; PRD "platform para tahsil etmez" kuralı korunur.

### 2.2 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Tip / Şekil | Gerekçe |
|-------|-------------|---------|
| `IsSharedWithParent` | `bool` | **Ödeme bilgisi veliyle paylaşılır** (`promp.txt`: "Dersin ödeme bilgileri veli ile de paylaşılacak"). `true` ise bağlı veli (m09) ödemeyi görür. |
| `SharedWithParentOnUtc` | `DateTime?` | Paylaşım zamanı (denetim/iz). |


---

## 3. API Sözleşmesi

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")`; `Result<T>` döner.
> Route prefix: `/api/payments`. Oluşturma `UpsertPaymentRecordRequest`, güncelleme ayrı `UpdatePaymentDetailsRequest` kullanır.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Ödeme kaydı oluştur | `POST /api/payments/records` | `UpsertPaymentRecordRequest` → `PaymentRecordResponse` | `Currency` büyük harfe çevrilir |
| Ödeme ayrıntıları | `PUT /api/payments/records/{paymentRecordId}` | `{ description, expectedAmount, dueDateUtc, notes }` → `PaymentRecordResponse` | Tahsilat/durum/para birimi değişmez |
| Tahsilat ekle | `POST /api/payments/records/{id}/collections` | `{ amount, method, occurredOnUtc, note }` → `PaymentRecordResponse` | Pozitif hareket |
| İade ekle | `POST /api/payments/records/{id}/refunds` | Aynı hareket gövdesi → `PaymentRecordResponse` | Net tahsilatı aşamaz |
| Ödemeyi iptal et | `POST /api/payments/records/{id}/cancel` | Gövdesiz → `PaymentRecordResponse` | Önce net tahsilat sıfırlanmalı |
| Ödeme getir | `GET /api/payments/records/{paymentRecordId}` | → `PaymentRecordResponse` | Yoksa `404 payments.record_not_found` |
| Öğretmen ödemeleri | `GET /api/payments/teachers/{teacherUserId}/records?outstandingOnly=` | → `PaymentRecordResponse[]` | `outstandingOnly=true` → yalnızca kalanı olanlar; vade artan sıralı |
| Gelir özeti | `GET /api/payments/teachers/{teacherUserId}/summary` | → `TeacherPaymentSummaryResponse` | **Para birimi bazında** gruplu özet |
| Filtreli liste | `GET /api/payments/teachers/{teacherUserId}/records/filter?outstanding=&overdue=&paid=&dateFromUtc=&dateToUtc=` | → `PaymentRecordResponse[]` | Bayraklar VEYA mantığıyla; hiçbiri seçili değilse tümü |
| **Arama + sayfalama** | `GET /api/payments/teachers/{teacherUserId}/records/search?q=&status=&studentId=&dateFromUtc=&dateToUtc=&skip=&take=` | → `PagedPaymentRecordsResponse` `{ Items[], TotalCount }` | Metin (açıklama), durum (`Open`/`Paid`/`Pending`/`PartiallyPaid`/`Overdue`/`Cancelled`), öğrenci, tarih; `take` ≤ 100; vade artan sıralı |
| **Veli "ödedim" beyanı (Veli V-G)** | `POST /api/payments/records/{paymentRecordId}/declare-paid` | `DeclarePaidRequest(DeclaredAmount, Note?)` → `ParentPaymentDeclarationResponse` | Yetki: ödemedeki öğrencinin **onaylı velisi** (`IParentAccessDirectory`) / Admin; `currentUser` = veli; `DeclaredAmount` güncel açık bakiyeye eşit; tek bekleyen beyan; ikinci istek `409 payments.declaration_pending`; kapanmış kayıt/tutar uyuşmazlığı `409`, kayıt yoksa `404` |
| **Öğretmene gelen beyanlar (Veli V-G)** | `GET /api/payments/teachers/{teacherUserId}/payment-declarations?onlyPending=` | → `ParentPaymentDeclarationResponse[]` | Öğretmen/Admin; `onlyPending=true` → yalnız `Declared` |
| **Beyanı teyit et (Veli V-G)** | `POST /api/payments/payment-declarations/{declarationId}/confirm` | → `ParentPaymentDeclarationResponse` | Öğretmen/Admin; `Declared` değilse `409 payments.declaration_not_pending`; teyit → `PaymentRecord.MarkCollectedByParentConfirmation` (Status=Paid, tam tahsil) |
| **Beyanı reddet (Veli V-G)** | `POST /api/payments/payment-declarations/{declarationId}/reject` | → `ParentPaymentDeclarationResponse` | Öğretmen/Admin; ödeme kaydına dokunmaz; yoksa `404 payments.declaration_not_found` |

**`UpsertPaymentRecordRequest` (koddan):**
`TeacherUserId, StudentId, RelatedLessonSessionId?, ItemType, Description, Currency, ExpectedAmount, CollectedAmount, DueDateUtc, CollectedOnUtc?, Status, BillingPeriodStartUtc?, BillingPeriodEndUtc?, Notes?`

**`PaymentRecordResponse` (koddan):**
`Id, TeacherUserId, StudentId, RelatedLessonSessionId?, ItemType (string), Description, Currency, ExpectedAmount, CollectedAmount, OutstandingAmount, DueDateUtc, CollectedOnUtc?, Status (string), IsOverdue, BillingPeriodStartUtc?, BillingPeriodEndUtc?, Notes?, Transactions[]`
> `Transactions[]`: `{ id, kind, amount, method, occurredOnUtc, note }`, tarih/kimlik sıralı.
> `OutstandingAmount`, `IsOverdue` ve `Status` (görüntüleme statüsü) **hesaplanmış** alanlardır; `Status` yanıtta `GetDisplayStatus(calendar)` sonucudur (tutar ve yerel vade tablosu).

**`TeacherPaymentSummaryResponse` (koddan):** `TeacherUserId, TotalRecords, CurrencySummaries[], MonthlyBreakdown[]`
> `MonthlyBreakdown[]` = son 6 ayın `{ Year, Month, Currency, ExpectedAmount, CollectedAmount }` kırılımı (vade ayına göre, iptaller hariç) — mobil grafikleri besler. **P16 T5 / N-11 (2026-11-15):** ay **Türkiye yerel ayıdır** — "bu ay" ve kaydın vade ayı `ILocalCalendar`'dan (1 Aralık 01:00 TR vadeli kayıt Aralık'ta; eskiden UTC'de Kasım). **P18 T3:** Her mevcut para birimi için altı ayrı ay noktası üretilir; tutarlar asla para birimleri arasında toplanmaz. Boş ay o para biriminde sıfırdır; kayıt yoksa noktalar boştur. Tarih, ardından para birimi sırası kullanılır. Genel `CurrencySummaries` sözleşmesi korunmuştur. **P18 T4 mobil:** Aylık model Currency alanını tüketir; özet/donut/aylık grafik seçilen para biriminden gösterilir. Tüm ödeme modelleri int kuruştur; JSON decimal dönüşümü yalnız Money yardımcısındadır. Öğretmen panelinin (`dashboard-summary`, m14) gecikmiş toplamı artık para birimi bazında (`overduePaymentTotals`).
**`PagedPaymentRecordsResponse` (koddan):** `Items[] (PaymentRecordResponse), TotalCount`
**`PaymentCurrencySummaryResponse` (koddan):** `Currency, PendingCount, PartialCount, PaidCount, OverdueCount, CancelledCount, ExpectedAmountTotal, CollectedAmountTotal, OutstandingAmountTotal, OverdueAmountTotal`
> `PendingCount`/`PartialCount`, **gecikmemiş** kayıtları sayar; gecikmiş olanlar `OverdueCount`'a düşer.

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP |
|-----|------|
| `payments.record_not_found` / `payments.declaration_not_found` | `404` |
| `payments.declaration_pending` / `payments.declaration_not_pending` / `payments.record_not_outstanding` / `payments.declaration_amount_mismatch` | `409` |
| `shared.forbidden` | `403` |
| (varsayılan) | `400` |

### 3.2 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Veli ile paylaş | `POST /api/payments/records/{id}/share-with-parent` | `IsSharedWithParent = true` + event |
| Veli görünümü | `GET /api/payments/parents/{parentUserId}/records` (rol kısıtı) | Veli, paylaşılmış ödemeleri görür (m09) |
| Hızlı "tahsil edildi" | `POST /api/payments/records/{id}/mark-paid` | Tek tıkla `CollectedAmount=ExpectedAmount`, `Status=Paid`, `CollectedOnUtc=now` |
| Öğrenci bazlı liste | `GET /api/payments/students/{studentId}/records` | Belirli öğrencinin ödeme geçmişi |
| Dönemsel gelir raporu | `GET /api/payments/teachers/{id}/summary?from=&to=` | m14 ile gelir raporu (aylık trend) |

---

## 4. İş Kuralları

1. **Manuel takip (🟢 kodda):** Ödeme **sistemden alınmaz**; öğretmen beklenen tutarı ve tahsilat/iade hareketlerini girer; net toplam/durum türetilir.
2. **Para birimi normalizasyonu (🟢 kodda):** `Currency` oluşturmada `Trim().ToUpperInvariant()` ile normalize edilir (varsayılan `TRY`).
3. **Kalan tutar (🟢 kodda):** `OutstandingAmount = max(Expected - Collected, 0)` — negatif olmaz.
4. **Gecikme (görüntüleme) (🟢 kodda):** Bir kayıt; kalanı varsa, iptal değilse ve yerel vade günü geçmişse `IsOverdue=true` olur; sıfır tahsilatta `Overdue`, kısmi tahsilatta `PartiallyPaid` görüntülenir. Bu, DB'deki `Status` alanını değiştirmez.
5. **Outstanding filtresi (🟢 kodda):** `outstandingOnly=true` → kalanı olan ve iptal olmayan kayıtlar.
6. **Özet sayımları (🟢 kodda):** `Pending`/`PartiallyPaid` sayıları **gecikmemiş** kayıtları kapsar; gecikmişler `OverdueCount`'a; `Paid` ve `Cancelled` ayrı sayılır. Tutarlar para birimi başına toplanır.
7. **Filtre VEYA mantığı (🟢 kodda):** `outstanding`/`overdue`/`paid` bayraklarından herhangi biri eşleşen kayıt döner; tarih aralığı `DueDateUtc`'ye göre uygulanır; hiçbiri seçili değilse tarih aralığındaki tümü.
8. **Güncellemede event (🟢 kodda):** `UpdateDetails` / hareket komutları, durum/tahsilat değişimini `PaymentRecordUpdatedDomainEvent` ile yayar (gelir özeti yeniden hesaplama tetikleyebilir).
9. **⚠️ Veli paylaşımı:** `IsSharedWithParent = true` olduğunda yalnızca o ödeme bilgisi bağlı veliye (m09) açılır; mahremiyet sınırı korunur.
10. **Kalıcı gecikme + bildirim (🟢 kodda, M07-1):** `PaymentOverdueScanner` saatte bir `PaymentOverdueScanProcessor.ScanAsync()` koşturur; vadesi geçmiş `Pending`/`PartiallyPaid` kayıtları (tur başına en fazla 200) `Overdue`'a çeker. Her geçiş bir `PaymentBecameOverdueDomainEvent` yayar → m11 öğretmene, Premium + "ödemeler" tercihi açık veliye in-app bildirim yazar. Push doğrudan gönderilmez; `NotificationDispatcher` turu gönderir.
11. **Sahiplik (yetki):** Öğretmen yalnızca kendi kayıtlarını yönetir/görür (`PaymentPolicies.cs`); veli yalnızca paylaşılmış kayıtları görür. **P15 T2 / N-08:** ödeme kaydı **oluşturma, ayrıntı güncelleme, tahsilat, iade ve iptal** ayrıca öğretmenin öğrenciyle yaşayan özel ders bağını ister (`ITeacherStudentAccess.GetScopeAsync == Private`; güncellemede öğrenci kimliği kayıttan alınır) → yoksa `403 shared.forbidden` (Admin muaf). Bağı arşivlenmiş eski öğretmen velinin paneline borç kaydı yazamaz. **P18 T2 kararı:** tahsilat/iade/iptal komutları da yaşayan özel ders bağı ister; arşivli öğrencinin kaydını değiştirmek için bağ önce arşivden çıkarılır. Okumalar yalnız sahipliğe bakar. ✅ **N-08 artığı kapandı (P15 T9, #185 a):** otomatik ders ücreti (`LessonSessionCompletedPaymentHandler`) outbox'tan yazdığı için authorizer'a tabi değil; kural artık ücret kaynağında: `IStudentDirectory.GetAgreedLessonRateAsync` ücreti yalnız **yaşayan özel ders bağında** (`ITeacherStudentAccess` = `Private`) döner, aksi halde `null` → kayıt açılmaz. Bağı arşivlenmiş öğretmen eski plandan `from-schedule` → ücretli tamamlama yapabilir (eski kaydı kapatma) ama velinin paneline borç **düşmez**. Arşiv-yalnız filtre bilinçli seçilmedi: reddedilmiş bağın ve P22 `Class` kapsamının girilmiş ücretini de faturalardı (K-18). ⚠️ **Bedeli:** kontrol olayın işlendiği andadır — dersi tamamlayıp olay işlenmeden (bir outbox turu) bağı arşivleyen öğretmenin o dersi için de ücret açılmaz; ücreti arşivden çıkarıp elle girer. Kanıt: `RealCrossTenantAccessTests.Archived_Teacher_Completing_From_An_Old_Schedule_Creates_No_Lesson_Fee` (pozitif kontrol: aynı öğretmen bağlıyken ücret oluşur).
12. **Ders tamamlanınca otomatik ücret kaydı (🟢 kodda, M07-2):** `LessonSessions/LessonSessionCompletedDomainEvent` tüketilir. Kayıt **yalnız** oturum `IsCompleted` **ve** `IsChargeable` iken açılır; tutar `IStudentDirectory.GetAgreedLessonRateAsync(teacher, student)` (B-07, **ders başına** ücret) ile gelir. Ücret tanımlı değilse **ya da bağ yaşayan özel ders bağı değilse** (arşivli/bitmiş — P15 T9, kural 11) kayıt **açılmaz** (`LogInformation`) — sessizce yanlış tutar üretilmez. Vade = tamamlanma anı **+ 7 gün**. Oturum başına **tek** kayıt (`RelatedLessonSessionId` kontrolü) — ders yeniden tamamlanabildiği için ikinci olay kopya üretmez.

---

## 5. Olay Akışı (Event-Driven)

```
POST /records
   → PaymentRecordCreatedDomainEvent
       → (öneri) m14 Reporting: gelir göstergeleri güncellenir
       → (öneri) m09 Parents: IsSharedWithParent ise veliye bildirim

PUT /records/{id}
   → PaymentRecordUpdatedDomainEvent (tahsilat değişmez)
       → diğer mevcut durum tüketicileri; aylık tahsilata yazılmaz
       → (öneri) m09 Parents: tahsilat durumu paylaşılıyorsa veli güncellenir

Vade geçti (PaymentOverdueScanner, saatte bir) — M07-1
   → PaymentRecord.MarkOverdue(now) → Status = Overdue (kalıcı) + OverdueMarkedOnUtc
   → PaymentBecameOverdueDomainEvent
       → m11 PaymentOverdueNotificationHandler: öğretmene UserNotification (Kind=PaymentOverdue, route=/payments)
                                                 + Premium & "ödemeler" tercihi açık veliye (route=/parent/payments)
       → push: NotificationDispatcher turu (PushedOnUtc boş olanlar)

M05: LessonSessionCompletedDomainEvent — M07-2
   → Payments/LessonSessionCompletedPaymentHandler (inbox idempotent)
       → ILessonSessionAccessService: oturum tamamlandı mı + IsChargeable mi
       → IStudentDirectory.GetAgreedLessonRateAsync: öğrenci bazlı ücret (B-07) — yalnız yaşayan özel ders bağında (P15 T9)
       → LessonFee türünde PaymentRecord (Pending, vade = tamamlanma + 7 gün)
```

> Olaylar **Outbox** ile yayılır (`Shared/Infrastructure/Messaging`).

---

## 6. Mobil Ekranlar

### ✅ Mevcut
| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/payments` | `PaymentsPage` | Ödeme listesi (kalan/geciken/ödenen filtreleri) |
| `/payments/new` | `PaymentFormPage` | Ödeme kaydı oluştur/düzenle |

> `mobile/lib/features/payments`, `flutter_bloc` (Cubit).

### ✅ Yeni Eklenen (2026-07-06)
- **Tahsilat formu — P18 T4:** `CollectPaymentSheet` TR tutarı kuruşa ayrıştırır ve yalnız yeni tahsilatın tutarını döndürür. `PaymentsCubit.collect` → `POST /records/{id}/collections`; toplam/durum istemcide hesaplanmaz. Yöntem varsayılan Cash=1'dir. Sunucunun güncel yanıtı kayıt satırını değiştirir.
- **Kart → düzenleme:** `PaymentFormPage` öğrenci/ders/tahsilat/para birimini kilitler; PUT yalnız açıklama/beklenen tutar/vade/not gönderir. İlk tahsilat yalnız yeni kayıt formunda girilebilir.
- **İptal — P18 T4:** Onay dialogu → `POST /records/{id}/cancel`; kayıt silinmez, iptalde açık bakiye sıfırdır. Net tahsilat pozitifse sunucu önce iade ister (mobil iade formu eklenmedi).
- **Sunucu tarafı sayfalama + filtre**: Ödemeler sayfası artık listeyi `records/search` ile **sayfa sayfa** çeker (sonsuz kaydırma, `take=20`) + metin arama, durum sekmesi, öğrenci ve tarih-aralığı filtresi. Aggregate'ler (özet paneli + grafikler) `getSummary`'den gelir (sayfalıyken tüm kayıtlar client'ta olmaz).
- **Finans grafikleri**: **katlanabilir "İstatistikler"** bölümünde (varsayılan kapalı) `MonthlyCollectionCard` (özetteki `MonthlyBreakdown`'dan) + `PaymentDistributionCard` (özet toplamlarından donut + tahsilat oranı %). Not: m14 dönemsel rapor hâlâ ayrı bir gelişim.

### ⚠️ Planlanan
- **Gelir özeti kartı** (para birimi bazında beklenen/tahsil/kalan/geciken) — dashboard'da (`getSummary` mevcut).
- **Veli ile paylaş** anahtarı (`IsSharedWithParent`) + veli ödeme görünümü.
- **Geciken ödeme uyarı rozeti** (takvim + dashboard).
- ~~**Aylık gelir grafiği**~~ ✅ İstemci tarafı yapıldı (`MonthlyCollectionCard` + `PaymentDistributionCard`); m14 ile **sunucu tabanlı dönemsel** rapor/grafik hâlâ öneri.

---

## 7. Kabul Kriterleri

- [x] Öğretmen manuel ödeme kaydı oluşturabilir (ders ücreti / aylık paket / düzeltme).
- [x] Kayıt güncellenebilir; kısmi tahsilat ve kalan tutar izlenir.
- [x] Geciken kayıtlar görüntülemede `Overdue` olarak işaretlenir.
- [x] Para birimi bazında gelir özeti alınabilir.
- [x] Kalan / geciken / ödenen filtreleri + tarih aralığı.
- [ ] ⚠️ Veli ile paylaşım (`IsSharedWithParent`) + veli görünümü.
- [x] Kalıcı `Overdue` otomasyonu (`PaymentOverdueScanner`, saatlik) + vade bildirimi (m11) — **M07-1, 2026-09-04**.
- [x] Backend tahsilat/iade hareketleri + ayrıntı PUT koruması (P18 T2).
- [x] Mobil `CollectPaymentSheet` `POST .../collections` ucunu kullanır; yalnız bu tahsilat tutarı gönderilir, iptal ayrı `/cancel` komutudur (**P18 T4**).
- [ ] ⚠️ M14 ile dönemsel gelir raporu/grafik.
- [x] M05 tamamlama → otomatik `LessonFee` kaydı (`LessonSessionCompletedPaymentHandler`) — **M07-2, 2026-09-04**.

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. **Veli ile paylaşım (`IsSharedWithParent`)** — alan + paylaş endpoint'i + veli görünümü (m09).
2. ~~**Kalıcı `Overdue` otomasyonu** — zamanlanmış iş + öğretmen/veli bildirimi (m11).~~ ✅ Yapıldı (2026-09-04, M07-1).
3. ~~**"Tahsil edildi" hızlı işaretleme** + kısmi tahsilat akışı~~ ✅ Yapıldı (2026-07-06, `CollectPaymentSheet`; `PaymentTransaction` dökümü backend’de P18 T2, mobil geçiş P18 T4).
4. **M14 gelir raporu** — dönemsel özet + aylık trend grafiği.
5. ~~**M05 köprüsü** — ders tamamlanınca otomatik `LessonFee` ödeme kaydı.~~ ✅ Yapıldı (2026-09-04, M07-2). Kalan: ücret tanımlı olmayan öğrenci için öğretmene "ücret gir" uyarısı (şu an yalnız log).
6. **(Öneri) Tahsilat dökümü (`PaymentInstallment`)** — kısmi ödemelerin tarihçesi.
7. **Yetkilendirme testleri** — öğretmen yalnızca kendi, veli yalnızca paylaşılan kayıtlar (`PaymentPolicies.cs`).

---

## 9. İlişkili Dokümanlar

- Veli ile paylaşım → [`m09_parents.md`](m09_parents.md)
- Gelir raporu → [`m14_reporting.md`](m14_reporting.md) · Vade bildirimi → [`m11_notifications.md`](m11_notifications.md)
- İlişkili ders/oturum → [`m05_lesson_sessions.md`](m05_lesson_sessions.md) · Takvimde ödeme → [`m04_scheduling.md`](m04_scheduling.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md)
- Veri modeli → [`veri_modeli.md`](veri_modeli.md) · Mimari → [`mimari_inceleme.md`](mimari_inceleme.md) · Genel → [`00_genel_bakis.md`](00_genel_bakis.md)
- PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) · UI → [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)

---

*Ödeme Takibi (M07) — Detaylı Tasarım | Güncelleme: 2026-11-15 (P16 Task 5 / N-11: özetin aylık kırılımı Türkiye yerel ayı; para birimi boyutu P18 T3) · 2026-11-14 (P15 Task 9 / #185 a: §4 kural 11 — otomatik ders ücreti yalnız yaşayan özel ders bağında; `GetAgreedLessonRateAsync` kapsam `Private` değilse `null`) · 2026-11-13 (P15 Task 2 / N-08: §4 kural 11 — ödeme oluştur/güncelle `ITeacherStudentAccess` ile yaşayan özel ders bağı ister; arşivli öğrencinin borcu arşivden çıkarmadan güncellenemez) · 2026-10-16 (veli beyanında güncel kalan tutar/kayıt durumu doğrulaması; geçersiz teyit durum veya olay değiştirmez). Önceki: 2026-09-04 (P06 Task 5 — **M07-1**: `PaymentRecord.MarkOverdue` + `OverdueMarkedOnUtc` alanı + `PaymentBecameOverdueDomainEvent` + saatlik `PaymentOverdueScanner`; **M07-2**: `LessonSessionCompletedPaymentHandler` — tamamlanan ücretli dersten otomatik `LessonFee` kaydı; migration `AddOverdueTracking`) · 2026-08-19 (kod-senkron: API 11 endpoint doğrulandı — 7 manuel takip + 4 veli beyan). Önceki not — Veli V-F: `IStudentPaymentDigestDirectory` — veli paneli için öğrenci ödeme kalem listesi canlı okuması; Veli V-G: `ParentPaymentDeclaration` veli "ödedim" beyanı — öğretmen teyitli → `MarkCollectedByParentConfirmation`; `declare-paid`/`confirm`/`reject`/liste endpoint'leri + `IParentAccessDirectory` + `parent_payment_declarations` migration)*

*Güncelleme: 2026-11-15 — P18 Task 1: xmin iyimser eşzamanlılık koruması ve 409 sözleşmesi.*

*Güncelleme: 2026-11-15 — P18 T2: tahsilat hareketleri, güvenli ayrıntı komutları ve yerel vade günü.*

İlk tahsilat girişi de iki ondalık/range ve gelecek tahsilat tarihi doğrulamasından geçer. Değişmeyen vadede yalnız not düzenlemek yeni gecikme bildirimi üretmez; vade değişirse yeni gecikme dönemi başlayabilir.

*Güncelleme: 2026-11-15 — P18 T3: para birimi bazlı aylık özet, tek bekleyen beyan, xmin ve veli sonuç bildirimi; mobil para birimi gösterimi T4 açık.*

### P18 T4 mobil para ve yazma güvenliği

`Money` kuruş ayrıştırma/gösterim/decimal JSON sınırının tek yeridir. `1.500`=150000 kuruş, `750,50`=75050. Üç ödeme modelinin Amount alanları int kuruştur. Mobil JSON number sınırında kuruş hassasiyetini korumak için en büyük desteklenen tutar 999.999.999.999,99'dur; taşma sessiz yuvarlanmaz, reddedilir. Öğrenci detayının ödeme metrikleri para birimi başına ayrı gösterilir. Dashboard'un ayrı eski ana-birim modeli yalnız Cubit adaptörüyle beslenir; kendi model/UI dönüşümü P21'de kalır.

Ödeme oluşturma/tahsilat/iptal POST'ları UUID v4 `Idempotency-Key` taşır. Başarısız ağ denemesinde aynı anahtarla tek tekrar, iki sonuçsuz denemeden sonra aynı kullanıcı/uç/içerikle manuel tekrar ilk anahtar ve gövdeyi korur; başarılı işlemden sonraki aynı içerik yeni işlemdir. Koruma ApiClient belleğindedir, uygulama yeniden açılışı kurtarılmaz. Sunucu P18 T5 ile ham gövdenin SHA-256 özetini aynı anahtarla saklar: aynı gövde tekrarında tek tahsilat hareketi ve ilk yanıt; farklı tutar/not/tarih/gövde gönderilirse `422 shared.idempotency_key_reused`. İlk iş sürerken aynı gövde409, farklı gövde422 alır. İstemci aynı mantıksal işlemde ilk anahtarı **ve ilk gövde baytlarını** korur. Yeni işlem yeni anahtar gerektirir. Koruma Redis erişilebildiğinde geçerlidir; mevcut fail-open sözleşmesi sürer.

*Güncelleme: 2026-11-15 — P18 T4: mobil kuruş, niyet uçları, para birimi seçimi ve tekrar anahtarı.*

*Güncelleme: 2026-11-15 — P18 T4 mobil para ve idempotency.*

*Güncelleme: 2026-11-15 — P18 T5: aynı tahsilat anahtarında gövde eşlemesi ve 422 sözleşmesi.*

*Güncelleme: 2026-11-15 — P18 T6: gerçek ders→ücret→nakit/beyan→eşzamanlı not→iade finans senaryosu.*

**Outbox teslimi (P19 T2 / N-42):** Otomatik ücrette zaten faturalanmış/ücretsiz oturum veya anlaşılmış ücret yokluğu başarılı etkisiz sonuçtur ve inbox'a yazılır; okunamayan oturum açık hata ile yeniden denenir. İptalde değişecek kayıt yokluğu başarıdır; bozuk yük poison olur. Ortak teslim, retry ve hata yalıtımı sözleşmesi: [Backend §7](../architecture/backend.md#7-outbox--event-akışı).

*Güncelleme: 2026-11-15 — P19 Task 2: açık olay tüketim sonuçları ve inbox güvenliği.*

*Güncelleme: 2026-11-15 — P20 Task 3.*
