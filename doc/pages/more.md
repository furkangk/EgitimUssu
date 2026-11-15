---
title: "Diğer / Ayarlar Merkezi Ekranı"
summary: "Ayar/profil merkezi; sahte abonelik/rapor/ayar sheet'leri kaldırıldı, ayar satırları gerçek ekranlara yönlendiriyor"
tags: [sayfa, more, ogretmen]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/more/presentation/pages/more_page.dart
  - mobile/lib/core/routing/app_router.dart
  - mobile/lib/features/membership/presentation/cubit/membership_cubit.dart
updated: 2026-11-15
---

# Diğer / Ayarlar Merkezi (`/more`)

> **Feature:** `more` · **Dosya:** `mobile/lib/features/more/presentation/pages/more_page.dart`
> **State:** `TeacherProfileCubit` + kökteki `MembershipCubit` · **Veri:** 🟢 (profil ve üyelik gerçek API; ayar satırları gerçek ekranlara yönlendirir)
> **Güncelleme:** 2026-11-15

## Amaç
Ayar/profil merkezi: profil özeti, ayar giriş noktaları, yardım/SSS, iletişim, hakkında, çıkış.

## Menü satırları (P09 sonrası)

| Satır | Davranış |
|-------|----------|
| Profil Bilgileri | → `/teacher-profile` |
| Hesap bilgileri | P21 gerçek hesap akışı gelene kadar gizli; statik `/account-info` yalnız debug |
| **Üyelik** | → `/membership` — **P09 Task 6'da aktifleşti** (D-10/D-15); alt metin üyelik seviyesine göre değişir |
| **Raporlar** | → `/teacher/reports` — P08 Task 5'te aktifleşti (D-16) |
| **Gizlilik ve güvenlik** | → `/settings/privacy` (M15-3/M15-4) |
| **Bildirim ayarları** | → `/settings/notifications` (M15-1/M11-5) |
| Çalışma ayarları | → `/teacher-profile` (ders biçimi + müsaitlik gerçek karşılığıdır) |
| Tatil ayarları | → `/scheduling` (M04 tatil blokları) |
| Yardım / Bize ulaşın / Hakkımızda | Bilgi bottom-sheet'leri (statik içerik) |
| Çıkış yap | Onay + `AuthCubit.logout()` |

## P05'te kaldırılanlar (sahte veri temizliği)

| Kaldırılan | Neden |
|------------|-------|
| `_showSubscriptionSheet` ("Plus", "15 Haziran 2026", "Aktif") | Karşılığı olmayan uydurma abonelik verisi (D-15) |
| `_showReportsSheet` (42 ders / %94 devam / %81 tahsilat) | Uydurma rapor sayıları (D-16) |
| `_showGeneralSettingsSheet` (sessiz saatler) | Backend'de karşılığı yok; satır gizlilik ekranına dönüştü |
| `_showWorkSettingsSheet` / `_showHolidaySettingsSheet` | Yerel `setState` toggle'ları hiçbir yere yazılmıyordu |
| `_lessonReminders`, `_paymentReminders`, … 11 yerel alan | Bildirim tercihleri artık `SettingsCubit` üzerinden gerçek API'ye yazılıyor |

> Dosya 1166 → 750 satır. Kullanılmayan `_SubscriptionSummary`, `_SwitchRow`, `_ChoiceRow`, `_InfoRow`,
> `_StatTile` widget'ları ve `_pickValue` yardımcı metodu da silindi.

## P09 Task 6'da değişenler (D-10/D-15)

| Değişiklik | Neden |
|------------|-------|
| Profil kartındaki "Plus üye" rozeti | Sabit `isPlusMember: true` sahtesi kaldırıldı; rozet artık `MembershipCubit` (→ `GET /api/membership/me`) okunarak çiziliyor |
| "Üyelik" satırı | Pasif "Yakında" durumundan çıkıp `/membership` ekranına bağlandı |
| `_MenuTile.enabled` / `.badge` | Son pasif satır da kalkınca kullanılmayan parametreler silindi (`-warnaserror` karşılığı: `flutter analyze` `unused_element_parameter`) |

> **P20 T1:** Sabit `rating: 4.8` / `reviewCount: 126` ve puan satırı kaldırıldı. Yorum sistemi M13/P11 tamamlanıp gerçek kaynak sağlanana kadar puan gösterilmez. Normal hesap bilgileri menüsü de statik prototipe üretim girişi açmamak için gizlidir.

## Ana bileşenler
Profil özet kartı, menü panelleri (`_MenuPanel` + `_MenuTile`), bilgi bottom-sheet'leri.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.20
- Ayar ekranları: [`settings_notifications.md`](settings_notifications.md) · [`settings_privacy.md`](settings_privacy.md)
- Hesap: [`account_info.md`](account_info.md) · Modül: [`../modules/m15_settings.md`](../modules/m15_settings.md)
- Üyelik: [`membership.md`](membership.md) · [`paywall.md`](paywall.md) · Modül: [`../modules/m17_membership.md`](../modules/m17_membership.md)

---

*Diğer / Ayarlar Merkezi Ekranı | Güncelleme: 2026-11-15*
