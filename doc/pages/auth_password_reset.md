---
title: "Şifremi Unuttum (Şifre Sıfırlama) Ekranları"
summary: "İki adımlı şifre sıfırlama: kod isteme (/password-reset) ve kod + yeni şifre (/password-reset/confirm); PasswordResetCubit üzerinden gerçek Identity uçlarına bağlı"
tags: [sayfa, auth, sifre-sifirlama]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/password_reset_request_page.dart
  - mobile/lib/features/auth/presentation/pages/password_reset_confirm_page.dart
  - mobile/lib/features/auth/presentation/cubit/password_reset_cubit.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-09-02
---

# Şifremi Unuttum (`/password-reset`, `/password-reset/confirm`)

> **Feature:** `auth` · **State:** `PasswordResetCubit` / `PasswordResetState` · **Veri:** Gerçek API
> **Güncelleme:** 2026-09-02

## Amaç
Şifresini unutan kullanıcının e-posta ile aldığı kodu kullanarak yeni şifre belirlemesi (D-01).
Akış iki ekrandır: kod isteme → kod + yeni şifre.

## Route
| Ekran | Path | Parametre |
|-------|------|-----------|
| Kod isteme | `/password-reset` | opsiyonel `?email=` (giriş ekranından `extra` ile de gelir) |
| Kod + yeni şifre | `/password-reset/confirm` | `state.extra` = e-posta · opsiyonel `?token=` (derin bağlantı) |

Her iki rota da router'ın `onAuthScreen` beyaz listesindedir; oturum açmamış kullanıcı erişebilir.

## State / API
- `PasswordResetCubit.requestReset(email)` → `POST /api/identity/password-reset/request`
- `PasswordResetCubit.confirmReset(email, token, newPassword)` → `POST /api/identity/password-reset/confirm`
- Durumlar: `idle → sending → sent` ve `idle → confirming → done`; hata halinde `failure` + `message`.

> **Enumeration engeli:** İstek ucu kullanıcı kayıtlı olmasa da `200` döner. Bu yüzden `sent`
> durumundaki metin "kod gönderildi" değil, "kayıtlıysa gönderdik" anlamı taşır.

## Ana bileşenler
- **Kod isteme:** e-posta alanı (validasyon: `@` içermeli), "Sıfırlama kodu gönder" butonu, "Kodum zaten var" bağlantısı.
- **Kod + yeni şifre:** e-posta, sıfırlama kodu, yeni şifre (min. 8 karakter, görünürlük toggle), yeni şifre tekrar (eşleşme kontrolü).
- Alan görünümü ortak: `features/auth/presentation/widgets/auth_field_decoration.dart`.

## Boş / hata durumları
- API hatası → `ErrorStateView` ile kart içinde mesaj (`identity.invalid_password_reset_token` → "Şifre sıfırlama tokeni geçersiz.").
- Başarılı onay → `SnackBar('Şifren güncellendi, giriş yapabilirsin.')` + `/login`'e yönlendirme.

## İlgili
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
- Giriş ekranı: [`auth_login.md`](auth_login.md) ("Şifremi unuttum" bağlantısı buradan açılır)
