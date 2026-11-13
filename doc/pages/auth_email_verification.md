---
title: "E-posta Doğrulama Ekranı"
summary: "Kayıt sonrası e-posta doğrulama; egitimussu://verify-email?token=... derin bağlantısıyla kod otomatik dolar, EmailVerificationCubit gerçek Identity uçlarına bağlıdır"
tags: [sayfa, auth, e-posta-dogrulama, derin-baglanti]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/email_verification_page.dart
  - mobile/lib/features/auth/presentation/cubit/email_verification_cubit.dart
  - mobile/lib/core/routing/deep_links.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-09-02
---

# E-posta Doğrulama (`/verify-email`)

> **Feature:** `auth` · **State:** `EmailVerificationCubit` / `EmailVerificationState` · **Veri:** Gerçek API
> **Güncelleme:** 2026-09-02

## Amaç
Kayıt sırasında gönderilen doğrulama kodu ile hesabın e-postasını doğrulamak (D-02).

## Route
- Path: `/verify-email`, query: `token` (derin bağlantıdan), opsiyonel `email`.
- Derin bağlantı: `egitimussu://verify-email?token=…` (e-postadaki bağlantı).

### Derin bağlantı normalizasyonu ⚠️
Şema-bazlı bir URI'de **yol boştur**, hedef `host` alanında taşınır
(`Uri.parse('egitimussu://verify-email').path == ''`). go_router yalnız `uri.path`'e baktığı için
bağlantı olduğu gibi verilirse hiçbir rota tutmaz. `core/routing/deep_links.dart` içindeki
`normalizeDeepLink(Uri)` host'u yola taşır ve router'ın `redirect`'i bunu ilk adımda uygular:

| Gelen bağlantı | Yönlendirilen rota |
|----------------|--------------------|
| `egitimussu://verify-email?token=X` | `/verify-email?token=X` |
| `egitimussu://password-reset?token=X` | `/password-reset/confirm?token=X` |
| `egitimussu://password-reset` | `/password-reset` |

Platform tanımları: Android `AndroidManifest.xml` (`<data android:scheme="egitimussu"/>`),
iOS `Info.plist` (`CFBundleURLTypes`).

## State / API
- `EmailVerificationCubit.verify(email, token)` → `POST /api/identity/email-verification/confirm`
- `EmailVerificationCubit.resendCode(email)` → `POST /api/identity/email-verification/request`
- Durumlar: `idle → verifying → verified` ve `idle → resending → resent`; hata halinde `failure` + `message`.

## Ana bileşenler
- E-posta alanı, doğrulama kodu alanı (derin bağlantıdan gelmişse dolu), "Doğrula" butonu, "Kodu tekrar gönder".

## Boş / hata durumları
- Geçersiz kod → `ErrorStateView` (`identity.invalid_email_verification_token`).
- Başarı → `SnackBar('E-posta adresin doğrulandı.')` + `/login`'e yönlendirme.

## İlgili
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
- Şifre sıfırlama: [`auth_password_reset.md`](auth_password_reset.md)
