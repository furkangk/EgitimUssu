# Ödeme Ekle/Düzenle (`/payments/new`, `/payments/edit`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`
> **State:** `PaymentsCubit` + `StudentsCubit` · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-07-06

## Amaç
Ödeme kaydı **oluşturma veya düzenleme**: gerçek öğrenci seçimi, ders/konu seçimi, tutar, vade, para birimi, ödeme yöntemi, not.

- **Yeni** (`/payments/new`, `record == null`): boş form.
- **Düzenleme** (`/payments/edit`, `state.extra: PaymentRecord`): form kaydın alanlarıyla dolu açılır (açıklama, tutar, tahsil edilen, vade, para birimi, not). **Öğrenci ve ders salt-okunur** (`_StudentReadonly`, kilit ikonu — düzenlenemez). Başlık "Ödemeyi Düzenle", buton "Değişiklikleri Kaydet". Sağ üstte **iptal ikonu → onaylı iptal** (kayıt zaten iptal değilse). Ödeme kartına dokununca (`PaymentsPage._onEdit`) buradan açılır.

## State / API
- `StudentsCubit.load(teacherUserId)` → `GET /api/students/profiles/by-teacher/{id}` — öğrenci listesi
- `PaymentsCubit.create(record)` → `POST /api/payments/records` — yeni kayıt
- `PaymentsCubit.update(record)` → `PUT /api/payments/records/{id}` — düzenleme (kayıt `id`/`studentId`/`relatedLessonSessionId`/`itemType` korunur; öğrenci/ders değişmez)
- `PaymentsCubit.cancel(record)` → `PUT /api/payments/records/{id}` (`Status=Cancelled`) — onaylı iptal (`_confirmCancel` → `AlertDialog`); kayıt silinmez
- Başarıda `context.pop()` ile liste sayfasına dönülür; `PaymentsPage` dönüşte reload yapar
- `studentId`: gerçek `StudentProfile.id` gönderilir (önceden ad string'i gönderiliyordu)

## Cubit yaşam döngüsü
- `initState`: `PaymentsCubit.create()` + `StudentsCubit.create()`
- `didChangeDependencies`: `_studentsCubit.load(userId)` (AuthCubit'ten alınan userId)
- `BlocListener<StudentsCubit>`: ilk öğrenci geldiğinde otomatik seçer + `_syncDescription()` çağırır
- `dispose`: her iki cubit kapatılır

## Ana bileşenler
- `_StudentSection` — öğrenci yüklenirken shimmer, yoksa uyarı, varsa gerçek dropdown; öğrencinin subjects'i varsa ders dropdown'u gösterir
- Açıklama: yeni kayıtta `"<subject> dersi — <öğrenci adı>"` otomatik doldurulur. **Düzenlemede** kaydın açıklaması korunur (öğrenci ön-seçimi açıklamayı ezmez; kullanıcı manuel öğrenci değiştirirse yeniden senkronlanır).
- Beklenen tutar / Tahsil edildi → status otomatik: `Paid` / `PartiallyPaid` / `Pending`. Tahsil edilen > beklenen ise uyarı verilir (kayıt engellenir).
- Ödeme yöntemi alanı yalnız **yeni kayıtta** görünür (nota eklenir); düzenlemede not doğrudan düzenlenir.
- Başarılı kayıt/güncelleme: SnackBar + `context.pop()`

## İlgili
- Modül: [`../modules/m07_payments.md`](../modules/m07_payments.md) (M07)
