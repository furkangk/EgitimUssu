# Kayıt / Register (`/register`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/register_page.dart`
> **State:** `AuthCubit` / `AuthState` · **Veri:** Gerçek API · **Güncelleme:** 2026-06-23

## Amaç
Öğretmen kaydı: ad, soyad, telefon, e-posta, şifre (tümü zorunlu).

## State / API
- `context.read<AuthCubit>().register(email, password, firstName, lastName, phoneNumber)` → `POST /api/identity/register`.
- Backend: Identity modülü (`UserRole.Teacher`).

## Ana bileşenler
- Ad/soyad alanları, telefon, e-posta, şifre (görünürlük toggle), kayıt butonu.

## İlgili
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
