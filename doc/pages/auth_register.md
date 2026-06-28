# Kayıt / Register (`/register`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/register_page.dart`
> **State:** `AuthCubit` / `AuthState` · **Veri:** Gerçek API · **Güncelleme:** 2026-06-28

## Amaç
Öğretmen kaydı: ad, soyad, telefon, e-posta, şifre (tümü zorunlu).

## State / API
- `context.read<AuthCubit>().register(email, password, firstName, lastName, phoneNumber)` → `POST /api/identity/register`.
- Backend: Identity modülü (`UserRole.Teacher`).

## Ana bileşenler
- Ad/soyad alanları, telefon, e-posta, şifre (görünürlük toggle), kayıt butonu.

## Layout / kaydırma
- İçerik `LayoutBuilder` + `SingleChildScrollView` + `ConstrainedBox(minHeight: viewport)` + `Center` ile sarılır; kart `Flexible` **değildir** (içeriğe göre boyutlanır). Böylece dar/uzun ekranlarda (ör. Honor 400 Pro) veya klavye açıkken form **taşmaz/üst üste binmez**, gerekince kaydırılır. Önceki `Flexible` + kaydırmasız `Center` düzeninde "Kayıt Ol" butonu alttaki "Zaten hesabın var mı?" ile çakışıyordu — düzeltildi.

## İlgili
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
