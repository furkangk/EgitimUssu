# Ödeme Ekle/Kaydet (`/payments/new`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`
> **State:** `PaymentsCubit` / `PaymentsState` · **Veri:** Gerçek API + demo seçenek listeleri · **Güncelleme:** 2026-06-23

## Amaç
Yeni ödeme kaydı: öğrenci, ders, tutar, vade, para birimi, ödeme yöntemi, opsiyonel not.

## State / API
- `PaymentsCubit.create()` ile form gönderimi → `POST /api/payments/records`.
- ⚠️ Öğrenci/ders açılır listeleri statik (`lessonsByStudent`); gerçek öğrenci verisine bağlanmalı.

## Ana bileşenler
- Öğrenci dropdown, ders dropdown (kademeli), açıklama, beklenen/tahsil tutar, vade tarihi, para birimi, ödeme yöntemi, not, kaydet.

## İlgili
- Modül: [`../modules/m07_payments.md`](../modules/m07_payments.md) (M07)
