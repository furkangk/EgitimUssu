---
title: "Öğretmen Paneli Önizleme Ekranı"
summary: "Giriş yapmadan dashboard'ı statik demo veriyle gösteren önizleme; dashboard_page.dart içindeki iç sınıf"
tags: [sayfa, dashboard, demo]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/dashboard/presentation/pages/dashboard_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Öğretmen Paneli Önizleme (`/teacher-panel-preview`)

> **Feature:** `dashboard` · **Dosya:** `mobile/lib/features/dashboard/presentation/pages/dashboard_page.dart` (iç sınıf `TeacherPanelPreviewPage`)
> **State:** Yok (Stateless, `DashboardState.preview()`) · **Veri:** Statik demo · **Güncelleme:** 2026-06-23

## Amaç
Giriş yapmadan dashboard'ı statik demo veriyle gösteren önizleme.

## Notlar
- `DashboardPage` ile aynı UI, ama veri sabit (cubit/yükleme yok).
- ⚠️ Ayrı dosyası yok; `dashboard_page.dart` içinde iç sınıf olarak tanımlı.

## İlgili
- Asıl ekran: [`dashboard.md`](dashboard.md)
