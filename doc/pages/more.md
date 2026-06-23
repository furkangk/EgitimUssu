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
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.20 · Hesap: [`account_info.md`](account_info.md) · Modül: M15
