# Ödeme Takibi (`/payments`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payments_page.dart`
> **State:** Stateful (yerel) · **Veri:** ⚠️ Demo (`_StudentPayment` listesi) · **Güncelleme:** 2026-06-23

## Amaç
Öğrenci ödemelerini listeleme: öğrenci, ders, vade, tutar, durum (Ödendi/Bekliyor/Gecikti — renk kodlu).

## Veri / API
- ⚠️ Demo veri. Backend hazır: `GET /api/payments/teachers/{teacherUserId}/records`, `.../summary`, `.../records/filter`.

## Ana bileşenler
- Avatarlı ödeme kartları (öğrenci, ders, tutar, vade, durum rozeti) → detaya dokunma.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.10 · Form: [`payment_form.md`](payment_form.md) · Modül: M07
