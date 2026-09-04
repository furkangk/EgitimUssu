---
title: "M07 — Ödeme Takibi (Payments)"
summary: "Öğretmenin manuel ödeme takibi + veli 'ödedim' beyan akışı çalışır; vade taraması (kalıcı Overdue + bildirim) ve tamamlanan dersten otomatik ücret kaydı eklendi"
tags: [modul, payments, odeme, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Payments/**
updated: 2026-09-04
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

## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`PaymentRecord`) | ✅ Mevcut | `src/Modules/Payments/Domain/PaymentsDomainModel.cs` |
| Application (CQRS + handler) | ✅ Mevcut | `src/Modules/Payments/Application/PaymentFeatures.cs` |
| API (oluştur/güncelle/getir/listele/özet/filtre/**arama+sayfalama** + veli beyan 4 endpoint) | ✅ Mevcut (**11 endpoint**) | `src/Modules/Payments/API/PaymentsModule.cs` |
| Hesaplanan alanlar (kalan/gecikme/gösterim durumu) | ✅ Mevcut | `PaymentRecordMappings` (`GetOutstandingAmount`, `IsOverdue`, `GetDisplayStatus`) |
| Para birimi bazında özet | ✅ Mevcut | `GetTeacherPaymentSummaryQuery` |
| Veli ile paylaşım (`IsSharedWithParent`) | 🔴 **Yok** | Önerilen — bkz. §2.2 |
| `Overdue` **kalıcı** otomasyonu (M07-1) | ✅ Mevcut | `PaymentOverdueScanner` / `PaymentOverdueScanProcessor` — saatte bir tarar, `PaymentRecord.MarkOverdue` uygular |
| Vade hatırlatma bildirimi (M07-1) | ✅ Mevcut | `PaymentBecameOverdueDomainEvent` → m11 `PaymentOverdueNotificationHandler` |
| Tamamlanan dersten otomatik ücret kaydı (M07-2) | ✅ Mevcut | `LessonSessionCompletedPaymentHandler` (integration event + `IStudentDirectory` ücreti) |
| Mobil ödeme ekranları | ✅ Mevcut | `mobile/lib/features/payments` |

> **Güncelleme (2026-09-04, P06 Task 5):** Kalıcı statü değişimi artık var. Okuma anındaki
> `GetDisplayStatus(now)` hesabı **korunur** (tarama turu arasındaki kayıtlar da gecikmiş görünür);
> `PaymentOverdueScanProcessor` bunun üstüne DB'deki `Status`'ü de `Overdue`'a çeker ve bildirim
> zincirini başlatır. İkisi çelişmez: tarama yalnız zaten gecikmiş olanı kalıcı hâle getirir.

---

## 2. Domain Modeli

### 2.1 🟢 Mevcut (koddan) — `PaymentRecord` (AggregateRoot<Guid>)

`src/Modules/Payments/Domain/PaymentsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Ödeme kaydı kimliği |
| `TeacherUserId` | `Guid` | Kaydı tutan öğretmen |
| `StudentId` | `Guid` | İlgili öğrenci |
| `RelatedLessonSessionId` | `Guid?` | İlişkili ders oturumu (opsiyonel) |
| `ItemType` | enum `BillingItemType` | `LessonFee=1`, `MonthlyPackage=2`, `ManualAdjustment=3` |
| `Description` | `string` | Açıklama |
| `Currency` | `string` | Para birimi (varsayılan `"TRY"`; oluştur/güncellemede **büyük harfe** çevrilir) |
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

**Davranış:** `UpdateManualTracking(...)` → tüm izlenen alanları günceller ve
`PaymentRecordUpdatedDomainEvent` yayar; olay **eski/yeni durum** ve **eski/yeni tahsil tutarını** taşır.

**`MarkOverdue(nowUtc)` (M07-1, 2026-09-04):** Yalnız `Pending`/`PartiallyPaid` **ve** `DueDateUtc < now`
olan kaydı `Overdue` yapar, `OverdueMarkedOnUtc` yazar ve `PaymentBecameOverdueDomainEvent` yayar.
Geçiş yapılmaması hata değildir (tarama turu saatte bir koşar) — bu yüzden metot `Result` değil **`bool`**
döner: `true` = durum değişti. Zaten `Overdue`/`Paid`/`Cancelled` olan ya da vadesi gelmemiş kayıt için
`false` döner ve **ikinci bir olay üretilmez** (idempotent).

**Hesaplanan değerler (Application katmanı — `PaymentRecordMappings`):**
| Yöntem | Mantık |
|--------|--------|
| `GetOutstandingAmount()` | `Cancelled` ise **0**; değilse `max(ExpectedAmount - CollectedAmount, 0)` — kalan tutar |
| `IsOutstanding(now)` | kalan > 0 (iptal edilen kayıt zaten 0 kalan döner) |
| `IsOverdue(now)` | kalan > 0 **ve** `DueDateUtc < now` |
| `GetDisplayStatus(now)` | gecikmişse `Overdue`, değilse mevcut `Status` (**görüntüleme** statüsü) |

> **Bug düzeltmesi (2026-07-06):** `GetOutstandingAmount()` önce statüye bakmadan `Expected − Collected`
> döndürüyordu; bu yüzden **iptal edilmiş** ödemelerin tahsil edilmemiş tutarı öğretmen ödeme özetindeki
> `OutstandingAmountTotal`/`ExpectedAmountTotal` yolunda "ödenmemiş bakiye"ye sızıyordu (`IsOutstanding`
> `Status != Cancelled` derken tutarsızdı). Artık iptal edilen kayıt **0 kalan** döner; `IsOutstanding`/`IsOverdue`
> içindeki gereksiz `Status != Cancelled` şartı kaldırıldı. Regresyon testi:
> `tests/Unit/PaymentSummaryOutstandingTests.cs`.

**Enum'lar (koddan birebir):**
```
BillingItemType : LessonFee = 1, MonthlyPackage = 2, ManualAdjustment = 3
PaymentStatus   : Pending = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Cancelled = 5
```

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `PaymentRecordCreatedDomainEvent` | `PaymentRecordId, TeacherUserId, StudentId, RelatedLessonSessionId?, ExpectedAmount, Currency, Status, CreatedOnUtc` |
| `PaymentRecordUpdatedDomainEvent` | `PaymentRecordId, TeacherUserId, StudentId, PreviousStatus, CurrentStatus, PreviousCollectedAmount, CurrentCollectedAmount, UpdatedOnUtc` |
| `PaymentBecameOverdueDomainEvent` (M07-1) | `PaymentRecordId, TeacherUserId, StudentId, OutstandingAmount, Currency, DueDateUtc, MarkedOnUtc` — m11 tüketir (öğretmen + Premium veli bildirimi) |
| `ParentPaymentDeclaredDomainEvent` (Veli V-G) | `DeclarationId, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, CreatedOnUtc` — öğretmene bildirim (teslim V-E) |
| `ParentPaymentDeclarationResolvedDomainEvent` (Veli V-G) | `DeclarationId, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, Status, ResolvedOnUtc` |

### 2.1.1 🟢 `ParentPaymentDeclaration` (AggregateRoot<Guid>) — veli "ödedim" beyanı (Veli V-G, 2026-07-19)

Veli bir ödeme kaydı için **beyan** verir (para transferi değil, mutabakat). Alanlar:
`Id, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, Note?, Status (Declared→Confirmed/Rejected), CreatedOnUtc, ResolvedOnUtc?`.
`Confirm(now)`/`Reject(now)` yalnız `Declared`'dan çalışır (aksi `InvalidOperationException`). Öğretmen **teyit** edince
`PaymentRecord.MarkCollectedByParentConfirmation(now)` çağrılır → `Status=Paid`, `CollectedAmount=ExpectedAmount`,
`CollectedOnUtc=now` ve mevcut `PaymentRecordUpdatedDomainEvent` yayılır (gelir özeti/veli paneli projeksiyonu bozulmaz).
Tablo `parent_payment_declarations`. **Karar (2026-07-19):** beyan öğretmen teyidine bağlı; PRD "platform para tahsil etmez" kuralı korunur.

### 2.2 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Tip / Şekil | Gerekçe |
|-------|-------------|---------|
| `IsSharedWithParent` | `bool` | **Ödeme bilgisi veliyle paylaşılır** (`promp.txt`: "Dersin ödeme bilgileri veli ile de paylaşılacak"). `true` ise bağlı veli (m09) ödemeyi görür. |
| `SharedWithParentOnUtc` | `DateTime?` | Paylaşım zamanı (denetim/iz). |
| Tahsilat kalemi (`PaymentInstallment`) | yeni Entity (öneri) | Kısmi tahsilatların **dökümü** (tek `CollectedAmount` yerine her ödemenin tarihçesi). |

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")`; `Result<T>` döner.
> Route prefix: `/api/payments`. Oluştur ve güncelle **aynı** `UpsertPaymentRecordRequest`'i kullanır.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Ödeme kaydı oluştur | `POST /api/payments/records` | `UpsertPaymentRecordRequest` → `PaymentRecordResponse` | `Currency` büyük harfe çevrilir |
| Ödeme güncelle | `PUT /api/payments/records/{paymentRecordId}` | `UpsertPaymentRecordRequest` → `PaymentRecordResponse` | `UpdateManualTracking()` → event |
| Ödeme getir | `GET /api/payments/records/{paymentRecordId}` | → `PaymentRecordResponse` | Yoksa `404 payments.record_not_found` |
| Öğretmen ödemeleri | `GET /api/payments/teachers/{teacherUserId}/records?outstandingOnly=` | → `PaymentRecordResponse[]` | `outstandingOnly=true` → yalnızca kalanı olanlar; vade artan sıralı |
| Gelir özeti | `GET /api/payments/teachers/{teacherUserId}/summary` | → `TeacherPaymentSummaryResponse` | **Para birimi bazında** gruplu özet |
| Filtreli liste | `GET /api/payments/teachers/{teacherUserId}/records/filter?outstanding=&overdue=&paid=&dateFromUtc=&dateToUtc=` | → `PaymentRecordResponse[]` | Bayraklar VEYA mantığıyla; hiçbiri seçili değilse tümü |
| **Arama + sayfalama** | `GET /api/payments/teachers/{teacherUserId}/records/search?q=&status=&studentId=&dateFromUtc=&dateToUtc=&skip=&take=` | → `PagedPaymentRecordsResponse` `{ Items[], TotalCount }` | Metin (açıklama), durum (`Open`/`Paid`/`Pending`/`PartiallyPaid`/`Overdue`/`Cancelled`), öğrenci, tarih; `take` ≤ 100; vade artan sıralı |
| **Veli "ödedim" beyanı (Veli V-G)** | `POST /api/payments/records/{paymentRecordId}/declare-paid` | `DeclarePaidRequest(DeclaredAmount, Note?)` → `ParentPaymentDeclarationResponse` | Yetki: ödemedeki öğrencinin **onaylı velisi** (`IParentAccessDirectory`) / Admin; `currentUser` = veli; `DeclaredAmount>0`; kayıt yoksa `404` |
| **Öğretmene gelen beyanlar (Veli V-G)** | `GET /api/payments/teachers/{teacherUserId}/payment-declarations?onlyPending=` | → `ParentPaymentDeclarationResponse[]` | Öğretmen/Admin; `onlyPending=true` → yalnız `Declared` |
| **Beyanı teyit et (Veli V-G)** | `POST /api/payments/payment-declarations/{declarationId}/confirm` | → `ParentPaymentDeclarationResponse` | Öğretmen/Admin; `Declared` değilse `409 payments.declaration_not_pending`; teyit → `PaymentRecord.MarkCollectedByParentConfirmation` (Status=Paid, tam tahsil) |
| **Beyanı reddet (Veli V-G)** | `POST /api/payments/payment-declarations/{declarationId}/reject` | → `ParentPaymentDeclarationResponse` | Öğretmen/Admin; ödeme kaydına dokunmaz; yoksa `404 payments.declaration_not_found` |

**`UpsertPaymentRecordRequest` (koddan):**
`TeacherUserId, StudentId, RelatedLessonSessionId?, ItemType, Description, Currency, ExpectedAmount, CollectedAmount, DueDateUtc, CollectedOnUtc?, Status, BillingPeriodStartUtc?, BillingPeriodEndUtc?, Notes?`

**`PaymentRecordResponse` (koddan):**
`Id, TeacherUserId, StudentId, RelatedLessonSessionId?, ItemType (string), Description, Currency, ExpectedAmount, CollectedAmount, OutstandingAmount, DueDateUtc, CollectedOnUtc?, Status (string), IsOverdue, BillingPeriodStartUtc?, BillingPeriodEndUtc?, Notes?`
> `OutstandingAmount`, `IsOverdue` ve `Status` (görüntüleme statüsü) **hesaplanmış** alanlardır; `Status` yanıtta `GetDisplayStatus(now)` sonucudur (gecikmişse `Overdue`).

**`TeacherPaymentSummaryResponse` (koddan):** `TeacherUserId, TotalRecords, CurrencySummaries[], MonthlyBreakdown[]`
> `MonthlyBreakdown[]` = son 6 ayın `{ Year, Month, ExpectedAmount, CollectedAmount }` kırılımı (vade ayına göre, iptaller hariç) — mobil grafikleri besler.
**`PagedPaymentRecordsResponse` (koddan):** `Items[] (PaymentRecordResponse), TotalCount`
**`PaymentCurrencySummaryResponse` (koddan):** `Currency, PendingCount, PartialCount, PaidCount, OverdueCount, CancelledCount, ExpectedAmountTotal, CollectedAmountTotal, OutstandingAmountTotal, OverdueAmountTotal`
> `PendingCount`/`PartialCount`, **gecikmemiş** kayıtları sayar; gecikmiş olanlar `OverdueCount`'a düşer.

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP |
|-----|------|
| `payments.record_not_found` | `404` |
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

1. **Manuel takip (🟢 kodda):** Ödeme **sistemden alınmaz**; öğretmen `ExpectedAmount`/`CollectedAmount`/`Status` alanlarını elle yönetir.
2. **Para birimi normalizasyonu (🟢 kodda):** `Currency` oluştur/güncellemede `Trim().ToUpperInvariant()` ile normalize edilir (varsayılan `TRY`).
3. **Kalan tutar (🟢 kodda):** `OutstandingAmount = max(Expected - Collected, 0)` — negatif olmaz.
4. **Gecikme (görüntüleme) (🟢 kodda):** Bir kayıt; kalanı varsa, iptal değilse ve vadesi geçmişse `Overdue` **görüntülenir** (`GetDisplayStatus`). Bu, DB'deki `Status` alanını değiştirmez.
5. **Outstanding filtresi (🟢 kodda):** `outstandingOnly=true` → kalanı olan ve iptal olmayan kayıtlar.
6. **Özet sayımları (🟢 kodda):** `Pending`/`PartiallyPaid` sayıları **gecikmemiş** kayıtları kapsar; gecikmişler `OverdueCount`'a; `Paid` ve `Cancelled` ayrı sayılır. Tutarlar para birimi başına toplanır.
7. **Filtre VEYA mantığı (🟢 kodda):** `outstanding`/`overdue`/`paid` bayraklarından herhangi biri eşleşen kayıt döner; tarih aralığı `DueDateUtc`'ye göre uygulanır; hiçbiri seçili değilse tarih aralığındaki tümü.
8. **Güncellemede event (🟢 kodda):** `UpdateManualTracking()`, durum/tahsilat değişimini `PaymentRecordUpdatedDomainEvent` ile yayar (gelir özeti yeniden hesaplama tetikleyebilir).
9. **⚠️ Veli paylaşımı:** `IsSharedWithParent = true` olduğunda yalnızca o ödeme bilgisi bağlı veliye (m09) açılır; mahremiyet sınırı korunur.
10. **Kalıcı gecikme + bildirim (🟢 kodda, M07-1):** `PaymentOverdueScanner` saatte bir `PaymentOverdueScanProcessor.ScanAsync()` koşturur; vadesi geçmiş `Pending`/`PartiallyPaid` kayıtları (tur başına en fazla 200) `Overdue`'a çeker. Her geçiş bir `PaymentBecameOverdueDomainEvent` yayar → m11 öğretmene, Premium + "ödemeler" tercihi açık veliye in-app bildirim yazar. Push doğrudan gönderilmez; `NotificationDispatcher` turu gönderir.
11. **Sahiplik (yetki):** Öğretmen yalnızca kendi kayıtlarını yönetir/görür (`PaymentPolicies.cs`); veli yalnızca paylaşılmış kayıtları görür.
12. **Ders tamamlanınca otomatik ücret kaydı (🟢 kodda, M07-2):** `LessonSessions/LessonSessionCompletedDomainEvent` tüketilir. Kayıt **yalnız** oturum `IsCompleted` **ve** `IsChargeable` iken açılır; tutar `IStudentDirectory.GetAgreedLessonRateAsync(teacher, student)` (B-07, **ders başına** ücret) ile gelir. Ücret tanımlı değilse kayıt **açılmaz** (`LogInformation`) — sessizce yanlış tutar üretilmez. Vade = tamamlanma anı **+ 7 gün**. Oturum başına **tek** kayıt (`RelatedLessonSessionId` kontrolü) — ders yeniden tamamlanabildiği için ikinci olay kopya üretmez.

---

## 5. Olay Akışı (Event-Driven)

```
POST /records
   → PaymentRecordCreatedDomainEvent
       → (öneri) m14 Reporting: gelir göstergeleri güncellenir
       → (öneri) m09 Parents: IsSharedWithParent ise veliye bildirim

PUT /records/{id}
   → PaymentRecordUpdatedDomainEvent (eski/yeni durum + tahsilat)
       → (öneri) m14 Reporting: gelir özeti yeniden hesaplanır
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
       → IStudentDirectory.GetAgreedLessonRateAsync: öğrenci bazlı ücret (B-07)
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
- **Tahsilat formu** (`CollectPaymentSheet`): "Tahsil Et" tek tıkla tamamı yerine tutar girişli form açar; **tam veya kısmi** tahsilat (`newCollected = min(collected + girilen, expected)` → `Paid`/`PartiallyPaid`). Kalandan fazlaya izin vermez.
- **Kart → düzenleme**: Ödeme kartına dokununca (kartta sağda `chevron` ok) `PaymentFormPage` düzenleme modunda açılır (`PUT`). Düzenlemede **öğrenci ve ders salt-okunur** (kilitli); tutar/vade/açıklama/not değişebilir.
- **İptal (onaylı)**: Düzenleme formunda iptal ikonu → onay dialogu → ödeme **silinmez**, `Status=Cancelled` olarak işaretlenir (`PaymentsCubit.cancel` → `PUT /records/{id}`). İptal edilen kayıt listede "İptal" görünür, açık bakiye/gecikme doğurmaz (`OutstandingAmount=0`). Kalıcı silme (hard delete) **bilinçli olarak yok**.
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
- [x] "Tahsil edildi" işaretleme + **kısmi tahsilat** (mobil `CollectPaymentSheet` → `PUT /records/{id}`).
- [ ] ⚠️ M14 ile dönemsel gelir raporu/grafik.
- [x] M05 tamamlama → otomatik `LessonFee` kaydı (`LessonSessionCompletedPaymentHandler`) — **M07-2, 2026-09-04**.

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. **Veli ile paylaşım (`IsSharedWithParent`)** — alan + paylaş endpoint'i + veli görünümü (m09).
2. ~~**Kalıcı `Overdue` otomasyonu** — zamanlanmış iş + öğretmen/veli bildirimi (m11).~~ ✅ Yapıldı (2026-09-04, M07-1).
3. ~~**"Tahsil edildi" hızlı işaretleme** + kısmi tahsilat akışı~~ ✅ Yapıldı (2026-07-06, `CollectPaymentSheet`; ayrı `PaymentInstallment` dökümü hâlâ öneri — bkz. §2.2).
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

*Ödeme Takibi (M07) — Detaylı Tasarım | Güncelleme: 2026-09-04 (P06 Task 5 — **M07-1**: `PaymentRecord.MarkOverdue` + `OverdueMarkedOnUtc` alanı + `PaymentBecameOverdueDomainEvent` + saatlik `PaymentOverdueScanner`; **M07-2**: `LessonSessionCompletedPaymentHandler` — tamamlanan ücretli dersten otomatik `LessonFee` kaydı; migration `AddOverdueTracking`) · 2026-08-19 (kod-senkron: API 11 endpoint doğrulandı — 7 manuel takip + 4 veli beyan). Önceki not — Veli V-F: `IStudentPaymentDigestDirectory` — veli paneli için öğrenci ödeme kalem listesi canlı okuması; Veli V-G: `ParentPaymentDeclaration` veli "ödedim" beyanı — öğretmen teyitli → `MarkCollectedByParentConfirmation`; `declare-paid`/`confirm`/`reject`/liste endpoint'leri + `IParentAccessDirectory` + `parent_payment_declarations` migration)*
