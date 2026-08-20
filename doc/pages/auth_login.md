---
title: "Giriş / Login Ekranı"
summary: "E-posta/şifre ile giriş ekranı; AuthCubit üzerinden gerçek /api/identity/login backend'ine bağlı"
tags: [sayfa, auth, login]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/login_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Giriş / Login (`/login?role={role}`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/login_page.dart`
> **State:** `AuthCubit` / `AuthState` · **Veri:** Gerçek API · **Güncelleme:** 2026-06-23

## Amaç
E-posta/şifre ile giriş. Beni hatırla, şifre sıfırlama bağlantısı, opsiyonel Google girişi. Demo kimlik bilgileri ön-dolu.

## Route
- Path: `/login`, opsiyonel query `role`.

## State / API
- `context.read<AuthCubit>().login(email, password)` → `POST /api/identity/login`.
- Backend: Identity modülü. ⚠️ Refresh token akışı mobilde eksik (bkz. [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) Y3).

## Ana bileşenler
- E-posta input, şifre input (görünürlük toggle), beni hatırla, giriş butonu, Google butonu.

## İlgili
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
