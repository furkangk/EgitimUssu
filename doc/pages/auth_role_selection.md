---
title: "Hesap Türü Seçimi Ekranı"
summary: "Öğretmen/Öğrenci/Veli rol seçim ekranı; yalnızca Öğretmen akışı uygulanmış, diğerleri snackbar ile engelli"
tags: [sayfa, auth, role-selection]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/role_selection_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Hesap Türü Seçimi (`/role-selection`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/role_selection_page.dart`
> **State:** Yok (Stateless) · **Veri:** — · **Güncelleme:** 2026-06-23

## Amaç
Kullanıcının rolünü seçmesi: Öğretmen / Öğrenci / Veli.

## Mevcut durum
- ⚠️ Yalnızca **Öğretmen** rolü uygulanmış; Öğrenci ve Veli seçilince snackbar gösteriliyor (akış yok).
- Öğrenci/veli ekranları için bkz. [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md).

## Ana bileşenler
- İkon + açıklamalı üç rol kartı.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.2
