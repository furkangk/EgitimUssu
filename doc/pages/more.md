---
title: "Diğer / Ayarlar Merkezi Ekranı"
summary: "Ayar/profil merkezi; profil özeti TeacherProfileCubit ile gerçek API'ye bağlı, ayar toggle'ları yerel/demo"
tags: [sayfa, more, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/more/presentation/pages/more_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Diğer / Ayarlar Merkezi (`/more`)

> **Feature:** `more` · **Dosya:** `mobile/lib/features/more/presentation/pages/more_page.dart`
> **State:** `TeacherProfileCubit` + Stateful (yerel toggle'lar) · **Veri:** Profil gerçek API, ayarlar ⚠️ yerel · **Güncelleme:** 2026-06-23

## Amaç
Ayar/profil merkezi: profil özeti, hesap bilgisi, abonelik, raporlar, genel ayarlar, bildirim ayarları, çalışma/tatil ayarları, yardım/SSS, iletişim, hakkında, çıkış.

## State / API
- `TeacherProfileCubit.create()..load(userId)` ile profil özeti.
- ⚠️ Ayar toggle'ları **yerel/demo**; backend `Settings` modülünde domain var ama endpoint yok (bkz. [`../modules/m15_settings.md`](../modules/m15_settings.md)). Bağlanması gereken: `GET/PUT /api/settings/users/{userId}` (henüz yok).

## Ana bileşenler
- Profil özet kartı, ayar bölümü panelleri, detaylar için modal bottom-sheet'ler. Bottom nav'da "Diğer" aktif.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.20 · Hesap: [`account_info.md`](account_info.md) · Modül: M15
