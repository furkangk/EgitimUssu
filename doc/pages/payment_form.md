---
title: "Ödeme Ekle Ekranı"
summary: "Öğretmenin ödeme kaydı oluşturduğu form ekranı; gerçek /api/payments backend'ine bağlı"
tags: [sayfa, payments, form, ogretmen]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/payments/presentation/pages/payment_form_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-11-15
---

# Ödeme Ekle/Düzenle (`/payments/new`, `/payments/edit`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payment_form_page.dart`
> **State:** `PaymentsCubit` + `StudentsCubit` · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-11-15

## Amaç
Ödeme kaydı **oluşturma veya düzenleme**: gerçek öğrenci seçimi, ders/konu seçimi, tutar, vade, para birimi, ödeme yöntemi, not.

- **Yeni** (`/payments/new`, `record == null`): boş form.
- **Düzenleme** (`/payments/edit`, `state.extra: PaymentRecord`): form kaydın alanlarıyla dolu açılır (açıklama, tutar, tahsil edilen, vade, para birimi, not). **Öğrenci, ders, tahsil edilen tutar ve para birimi salt-okunur** (`_StudentReadonly`, kilit ikonu — düzenlenemez). Başlık "Ödemeyi Düzenle", buton "Değişiklikleri Kaydet". Sağ üstte **iptal ikonu → onaylı iptal** (kayıt zaten iptal değilse). Ödeme kartına dokununca (`PaymentsPage._onEdit`) buradan açılır.

## State / API
- `StudentsCubit.load(teacherUserId)` → `GET /api/students/profiles/by-teacher/{id}` — öğrenci listesi
- `PaymentsCubit.create(record)` → `POST /api/payments/records` — yeni kayıt
- `PaymentsCubit.update(record)` → `PUT /api/payments/records/{id}` — yalnız `{description, expectedAmount, dueDateUtc, notes}`; tahsilat/durum değişmez
- `PaymentsCubit.cancel(record)` → `POST /api/payments/records/{id}/cancel` — onaylı iptal (`_confirmCancel` → `AlertDialog`); kayıt silinmez
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
- Yalnız yeni kayıtta ilk tahsilat girilebilir; create yükünün status alanı tutarlardan hesaplanır: `Paid` / `PartiallyPaid` / `Pending`. Tahsil edilen > beklenen ise uyarı verilir (kayıt engellenir).
- Ödeme yöntemi alanı yalnız **yeni kayıtta** görünür (nota eklenir); düzenlemede not doğrudan düzenlenir.
- Başarılı kayıt/güncelleme: SnackBar + `context.pop()`

## Para ve tekrar güvenliği (P18 T4)

Tutarlar `Money.parseTr` ile int kuruşa ayrıştırılır: `1.500` → 150000, `750,50` → 75050, `1.500,5` → 150050. İşaretli/bozuk gruplu/ikiden çok ondalıklı giriş reddedilir. JSON sınırında `Money.toJsonAmount` kullanılır. Düzenleme yeni tahsilat oluşturmaz; bunun için listedeki “Tahsil Et” kullanılır. İptal net tahsilat varsa sunucuda 409 döner; önce iade gerekir (iade mobil formu bu görevde eklenmedi).

Yeni ödeme POST'u `Idempotency-Key` taşır. Ağ sonucu belirsizse tek otomatik tekrar ve aynı içerikle manuel tekrar ilk anahtar/gövdeyi korur; ilk tahsil zamanı değişmez. Başarıdan sonra aynı içerikli yeni kayıt yeni anahtar alır. Koruma aynı `ApiClient` ömründedir; uygulamayı kapatıp açınca sonuçsuz işlem kurtarma yoktur. Metinler `finance_*` ARB anahtarlarından gelir (tr/en).

Regresyon: `mobile/test/features/payments/payment_form_parsing_test.dart` (gerçek form → repository → HTTP gövdesi, düzenleme kilitleri ve İngilizce görünüm).

## İlgili
- Modül: [`../modules/m07_payments.md`](../modules/m07_payments.md) (M07)

*Güncelleme: 2026-11-15 — P18 T4 mobil para ve idempotency.*
