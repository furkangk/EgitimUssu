# Hesap Bilgileri (`/account-info`, `/account-info-preview`)

> **Feature:** `more` · **Dosya:** `mobile/lib/features/more/presentation/pages/account_info_page.dart`
> **State:** Stateful + `AuthCubit` oturumu · **Veri:** ⚠️ Demo (`AccountData`) · **Güncelleme:** 2026-06-23

## Amaç
Hesap güvenliği/yönetimi: hesap durumu (tip, üyelik tarihi), güvenlik (şifre, 2FA, aktif oturumlar), hesap kapatma.

## State / API
- Hesap verisi `AuthCubit` oturumundan; çoğu işlem (2FA, oturum detayı, kapatma) ⚠️ demo.
- İlgili backend: Identity (oturumlar/`RefreshTokenSession`), Settings (`SessionTerminationPolicy`).

## Ana bileşenler
- Avatar + rol pill'li başlık, durum paneli, güvenlik paneli (şifre/2FA/oturumlar), tehlikeli bölge (hesap kapatma) + onay modali.

## İlgili
- Modül: [`../modules/m15_settings.md`](../modules/m15_settings.md) (M15), [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
