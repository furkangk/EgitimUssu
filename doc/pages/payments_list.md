# Ödeme Takibi (`/payments`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payments_page.dart`
> **State:** `PaymentsCubit` (BlocProvider.value) · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-07-06

## Amaç
Öğretmenin tüm ödeme kayıtlarını görüntüleme: açıklama, vade, tutar, durum (Ödendi/Bekleyen/Kısmi/Geciken/İptal) + **tam veya kısmi tahsilat** girişi (tahsilat formu).

## Veri / API
- `PaymentsCubit.load(teacherUserId)` → `GET /api/payments/teachers/{id}/records`
  - ⚠️ **Düzeltme (2026-07-06):** Önceden `load` ayrıca `GET .../summary` çağırıyordu ama sonuç (`state.summary`) UI'da **hiç kullanılmıyordu** — `_FinanceSummaryPanel` özeti `state.records`'dan yeniden hesaplıyor. Bu sürüklenmeye açık ölü çağrı kaldırıldı; ekran artık yalnızca kayıt listesini çeker. Para-birimi bazlı gelir özeti kartı (m07 §6, planlanan) yapıldığında `PaymentRepository.getSummary` yeniden bağlanacak. `load` beklenmeyen (ApiException dışı) hatada da `isLoading`'i sıfırlar (kalıcı shimmer düzeltmesi).
- `PaymentsCubit.collect(record, amountNow)` → `PUT /api/payments/records/{id}`
  - **Tahsilat formu (2026-07-06):** "Tahsil Et" artık tek tıkla tamamını almaz; `CollectPaymentSheet` (bottom sheet) açar. Öğretmen bu işlemde alınan tutarı girer (varsayılan = **kalan**; "Tamamını al" ile doldurulabilir). Cubit yeni tahsilatı hesaplar: `newCollected = min(collectedAmount + amountNow, expectedAmount)`; tamamı alınırsa `status=Paid`, kısmiyse `status=PartiallyPaid`. `collectedOnUtc=now`. Kalandan fazla girişe form izin vermez; cubit de beklenen tutarda **clamp**'ler.
  - ⚠️ **Bug düzeltmesi (2026-07-06, `collect`'in öncülü `markPaid`):** "Tahsil Et" 3 ayrı sorundan çalışmıyordu:
    1. **Enum eşleme:** `PaymentRecordModel._statusToApi`/`_itemTypeToApi` backend enum'larıyla uyuşmuyordu (Paid↔PartiallyPaid takas; Cancelled→Overdue; `MonthlyPackage`/`ManualAdjustment` isim uyuşmazlığı → LessonFee'ye düşüyordu). Sonuç: gerçek API'de "Tahsil Et" ödemeyi **PartiallyPaid** kaydedip kart "Ödendi"ye geçmiyordu. Eşleme backend değerlerine göre düzeltildi.
    2. **Görsel geri bildirim yok:** Tıklamada buton sessizce devre dışı kalıyor, spinner yoktu; backend erişilemezse Dio ~15 sn asılı kaldığından kullanıcıya "hiçbir şey olmuyor" gibi görünüyordu. Artık tıklanan kayda özel yükleniyor göstergesi (`PaymentsState.savingRecordId` → butonda `CircularProgressIndicator` + "İşleniyor…") var.
    3. **Sağlamlık:** `collect`/`load`/`create` yalnız `ApiException` yakalıyordu; başka bir hata `isSaving`/`isLoading`'i kalıcı bırakıp butonu/ekranı kilitliyordu. Genel `catch` eklendi (durum sıfırlanır + hata mesajı).
    - Testler: `test/features/payments/data/payment_model_test.dart`, `test/features/payments/data/payment_repository_mock_fallback_test.dart`, `test/features/payments/presentation/payments_cubit_test.dart`.
- `PaymentsCubit.create(record)` → `POST /api/payments/records` (PaymentFormPage'den dönüşte `load` yeniden çağrılır)
- **Kart → düzenleme (2026-07-06):** Ödeme kartına dokununca (`_PaymentTile` `InkWell` ile tıklanabilir + sağda `chevron_right` ok → `_onEdit`) `context.push('/payments/edit', extra: record)` ile `PaymentFormPage` **düzenleme modunda** açılır; dönüşte liste yeniden yüklenir. Kaydet `PaymentsCubit.update(record)` → `PUT /api/payments/records/{id}`.
- **İptal (2026-07-06):** Düzenleme formundaki iptal ikonu → **onay dialogu** → `PaymentsCubit.cancel(record)` → `PUT /api/payments/records/{id}` (`Status=Cancelled`). Kayıt **silinmez**; listede "İptal" olarak kalır. Başarıda listeye döner. (Hard delete yok.)
- Offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

## Status string → UI mapping
| Backend string | Etiket    | Renk         |
|----------------|-----------|--------------|
| `Paid`         | Ödendi    | emerald      |
| `Cancelled`    | İptal     | gri (muted)  |
| `Pending`      | Bekleyen  | amber        |
| `PartiallyPaid`| Kısmi     | blue         |
| `isOverdue=true` | Geciken | red          |

> **Düzeltme (2026-07-06):** `Cancelled` kayıtlar önceden hiçbir yerde ele alınmadığından UI'da yanlışlıkla **"Bekleyen"** (amber) görünüyor, **"Bekleyen" sekmesinde** listeleniyor ve iptal edilmiş olmasına rağmen **"Tahsil Et"** butonu gösteriyordu. Artık ayrı **"İptal"** rozeti (gri) alır, "Bekleyen" filtresinin dışında tutulur ve "Tahsil Et" butonu gösterilmez.
> **`_FinanceSummaryPanel` "Tahsil edilen"** metriği artık **tüm** kayıtların `collectedAmount` toplamıdır (kısmi tahsilatlar dahil; önceden yalnızca `Paid` kayıtlar sayılıyordu). Backend `CollectedAmountTotal` semantiğiyle örtüşür.

## State akışı
- `isLoading=true` → shimmer özet + shimmer liste
- `errorMessage!=null && records.isEmpty` → hata kartı + "Tekrar Dene"
- `records.isEmpty` (başarılı ama boş) → `_EmptyPanel`
- Yüklü → 4-tab filtre (Tümü/Ödenen/Bekleyen/Geciken) + kayıt kartları
- `successMessage` / `errorMessage` → `SnackBar` (BlocConsumer listener)

## Ana bileşenler
- `_FinanceSummaryPanel` — tahsil edilen / bekleyen / geciken tutarları `state.records`'dan hesaplar
- `_PaymentTile` — açıklama, vade, `outstandingAmount`/`collectedAmount`, durum rozeti + "Tahsil Et" butonu (Paid/İptal dışındakiler için; tıklayınca `CollectPaymentSheet` açar) + **sağda `chevron_right` ok**. **Kartın kendisi tıklanabilir** (ripple) → düzenleme formu.
- `CollectPaymentSheet` (`presentation/widgets/collect_payment_sheet.dart`) — tahsilat formu: beklenen/tahsil edilen/kalan özeti + **belirgin tam-genişlik "Tamamını al" butonu** (`_FullAmountButton`; kalanı tek dokunuşta doldurur, tam tahsilat seçiliyken primary dolgu ile geri bildirim) + "veya kısmi tutar gir" ayıracı + tutar girişi; alınan tutarı döndürür
- `_PaymentTabs` — 4 tab filtre (client-side)
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`
- `_ShimmerSummary` / `_ShimmerList` — yükleme iskeletleri

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.10 · Form: [`payment_form.md`](payment_form.md) · Modül: M07
