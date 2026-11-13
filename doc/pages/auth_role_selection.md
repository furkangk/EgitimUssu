---
title: "Hesap Türü Seçimi Ekranı"
summary: "Öğretmen/Öğrenci/Veli rol seçim ekranı; üç rol de ilgili kayıt akışına yönlendirilir"
tags: [sayfa, auth, role-selection]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/auth/presentation/pages/role_selection_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-10-16
---

# Hesap Türü Seçimi (`/role-selection`)

> **Feature:** `auth` · **Dosya:** `mobile/lib/features/auth/presentation/pages/role_selection_page.dart`
> **State:** Yok (Stateless) · **Veri:** — · **Güncelleme:** 2026-10-16

## Amaç
Kullanıcının rolünü seçmesi: Öğretmen / Öğrenci / Veli.

## Mevcut durum
- **Öğretmen, Öğrenci ve Veli** kartları seçilen rol ile kayıt ekranını açar.
- Öğrenci/veli ekranları için bkz. [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md).

## Ana bileşenler
- İkon + açıklamalı üç rol kartı.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.2

## 2026-10-16 ekran düzeltmesi

Üç rol de `/register?role=ogretmen|ogrenci|veli` yoluyla kayıt akışına gider. Kartlar ve giriş bağlantısı kısa ekran ve büyük yazıda kaydırılabilir; alt kart kesilmez.

Güncelleme: 2026-10-16
