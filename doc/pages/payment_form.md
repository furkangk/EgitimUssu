# Ödeme Ekle/Kaydet (`/payments/new`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`
> **State:** `PaymentsCubit` + `StudentsCubit` · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-06-26

## Amaç
Yeni ödeme kaydı: gerçek öğrenci seçimi, ders/konu seçimi, tutar, vade, para birimi, ödeme yöntemi, not.

## State / API
- `StudentsCubit.load(teacherUserId)` → `GET /api/students/profiles/by-teacher/{id}` — öğrenci listesi
- `PaymentsCubit.create(record)` → `POST /api/payments/records` — ödeme kaydı
- Başarıda `context.pop()` ile liste sayfasına dönülür; `PaymentsPage` dönüşte reload yapar
- `studentId`: gerçek `StudentProfile.id` gönderilir (önceden ad string'i gönderiliyordu)

## Cubit yaşam döngüsü
- `initState`: `PaymentsCubit.create()` + `StudentsCubit.create()`
- `didChangeDependencies`: `_studentsCubit.load(userId)` (AuthCubit'ten alınan userId)
- `BlocListener<StudentsCubit>`: ilk öğrenci geldiğinde otomatik seçer + `_syncDescription()` çağırır
- `dispose`: her iki cubit kapatılır

## Ana bileşenler
- `_StudentSection` — öğrenci yüklenirken shimmer, yoksa uyarı, varsa gerçek dropdown; öğrencinin subjects'i varsa ders dropdown'u gösterir
- Açıklama: `"<subject> dersi — <öğrenci adı>"` formatında otomatik doldurulur
- Beklenen tutar / Tahsil edildi → status otomatik: `Paid` / `PartiallyPaid` / `Pending`
- Başarılı kayıt: SnackBar + `context.pop()`

## İlgili
- Modül: [`../modules/m07_payments.md`](../modules/m07_payments.md) (M07)
