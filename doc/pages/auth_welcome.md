---
title: "Karşılama / Welcome Ekranı"
summary: "Uygulama giriş/karşılama ekranı; marka bloğu + Giriş Yap/Kayıt Ol yönlendirmesi, backend verisi yok"
tags: [sayfa, auth, welcome]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/welcome_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Karşılama / Welcome (`/`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/welcome_page.dart`
> **State:** Yok (Stateless) · **Veri:** — · **Güncelleme:** 2026-06-23

## Amaç
Uygulamaya giriş ekranı; marka algısı + "Giriş Yap" / "Kayıt Ol" aksiyonları.

## Ana bileşenler
- Marka/logo bloğu, gradient overlay'li karşılama görseli, açıklama metni
- İki aksiyon butonu → `/login`, `/register`

## Veri / API
Yok.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.1
- Modül: [`../modules/m01_identity.md`](../modules/m01_identity.md) (M01)
