---
title: "Bildirim Ayarları Ekranı"
summary: "Kullanıcının bildirim kanalları ve türlerini gerçek Settings API'sine yazan ayar ekranı (M15-1)"
tags: [sayfa, settings, bildirim, ayarlar]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/settings/presentation/pages/notification_settings_page.dart
  - mobile/lib/features/settings/presentation/cubit/settings_cubit.dart
  - mobile/lib/features/settings/data/repositories/settings_repository_impl.dart
updated: 2026-09-03
---

# Bildirim Ayarları (`/settings/notifications`)

> **Feature:** `settings` · **Dosya:** `mobile/lib/features/settings/presentation/pages/notification_settings_page.dart`
> **State:** `SettingsCubit` · **Veri:** 🟢 `GET`/`PUT /api/settings/users/{userId}` · **Güncelleme:** 2026-09-03

## Amaç
Kullanıcının bildirim tercihlerini yönetmesi. Buradaki her anahtar backend'de **gerçekten uygulanır**:
kapatılan bir tür için `NotificationDispatchProcessor` push göndermez (bkz. [`../modules/m11_notifications.md`](../modules/m11_notifications.md) §5.1).

## İçerik
| Bölüm | Satır | Alan |
|-------|-------|------|
| Kanallar | Anlık bildirimler | `pushNotificationsEnabled` |
| Kanallar | E-posta bildirimleri | `emailNotificationsEnabled` |
| Türler | Ders hatırlatmaları | `upcomingLessonReminderEnabled` |
| Türler | Ödev hatırlatmaları | `homeworkReminderEnabled` |
| Türler | Ödeme hatırlatmaları | `paymentReminderEnabled` |
| Türler | Haftalık özet | `weeklySummaryEnabled` |

## Davranış
- Yükleme: `SettingsCubit.load(userId)` → `GET`. Kayıt yoksa backend **varsayılan** döner (404 değil).
- Değişiklik: **iyimser** uygulanır ve anında `PUT` edilir. Kaydederken satır `AbsorbPointer` ile pasifleşir (çift istek yok).
- Hata: eski değere dönülür + kırmızı SnackBar (`settingsErrorListener`).
- Oturum yoksa: "Oturum bulunamadı" boş durumu (**mock kullanıcı yok** — D-17).
- **Mock fallback yok** (A-05/D-14): kaydedilmemiş bir tercihi kaydedilmiş gibi göstermez.

## İlgili
- Modül: [`../modules/m15_settings.md`](../modules/m15_settings.md) · [`../modules/m11_notifications.md`](../modules/m11_notifications.md)
- Kardeş ekran: [`settings_privacy.md`](settings_privacy.md) · Giriş noktası: [`more.md`](more.md)

---

*Bildirim Ayarları Ekranı | Güncelleme: 2026-09-03*
