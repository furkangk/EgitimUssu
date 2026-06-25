# Ödeme Takibi (`/payments`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payments_page.dart`
> **State:** `PaymentsCubit` (BlocProvider.value) · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-06-26

## Amaç
Öğretmenin tüm ödeme kayıtlarını görüntüleme: açıklama, vade, tutar, durum (Ödendi/Bekleyen/Kısmi/Geciken) + tek tıkla tahsil etme.

## Veri / API
- `PaymentsCubit.load(teacherUserId)` → `GET /api/payments/teachers/{id}/records` + `GET .../summary` (paralel)
- `PaymentsCubit.markPaid(record)` → `PUT /api/payments/records/{id}` (status=Paid, collectedAmount=expectedAmount)
- `PaymentsCubit.create(record)` → `POST /api/payments/records` (PaymentFormPage'den dönüşte `load` yeniden çağrılır)
- Offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

## Status string → UI mapping
| Backend string | Etiket    | Renk    |
|----------------|-----------|---------|
| `Paid`         | Ödendi    | emerald |
| `Pending`      | Bekleyen  | amber   |
| `PartiallyPaid`| Kısmi     | blue    |
| `isOverdue=true` | Geciken | red     |

## State akışı
- `isLoading=true` → shimmer özet + shimmer liste
- `errorMessage!=null && records.isEmpty` → hata kartı + "Tekrar Dene"
- `records.isEmpty` (başarılı ama boş) → `_EmptyPanel`
- Yüklü → 4-tab filtre (Tümü/Ödenen/Bekleyen/Geciken) + kayıt kartları
- `successMessage` / `errorMessage` → `SnackBar` (BlocConsumer listener)

## Ana bileşenler
- `_FinanceSummaryPanel` — tahsil edilen / bekleyen / geciken tutarları `state.records`'dan hesaplar
- `_PaymentTile` — açıklama, vade, `outstandingAmount`/`collectedAmount`, durum rozeti + "Tahsil Et" butonu (Paid olmayanlar için)
- `_PaymentTabs` — 4 tab filtre (client-side)
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`
- `_ShimmerSummary` / `_ShimmerList` — yükleme iskeletleri

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.10 · Form: [`payment_form.md`](payment_form.md) · Modül: M07
