---
title: "Gizlilik ve Güvenlik Ayarları Ekranı"
summary: "Çalışma verisi gizlilik seviyesi (M15-4) ve oturum sonlandırma politikası (M15-3) ayar ekranı"
tags: [sayfa, settings, gizlilik, guvenlik, ayarlar]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/settings/presentation/pages/privacy_settings_page.dart
  - mobile/lib/features/settings/presentation/cubit/settings_cubit.dart
updated: 2026-09-03
---

# Gizlilik ve Güvenlik (`/settings/privacy`)

> **Feature:** `settings` · **Dosya:** `mobile/lib/features/settings/presentation/pages/privacy_settings_page.dart`
> **State:** `SettingsCubit` · **Veri:** 🟢 `GET`/`PUT /api/settings/users/{userId}` · **Güncelleme:** 2026-09-03

## Amaç
Öğrencinin çalışma verisinin ne kadarının paylaşılacağını ve girişte diğer oturumlara ne olacağını seçmesi.

## İçerik

**Çalışma verisi gizliliği** (`privacyLevel`, M15-4) — tekli seçim:

| Seçenek | Anlamı |
|---------|--------|
| Standart | Öğretmen ve onaylı veli çalışma özetini + detayını görür |
| Sınırlı | Yalnız haftalık toplam süre görünür; seri ve ders kırılımı gizlenir |
| Gizli | Hiçbir çalışma verisi paylaşılmaz (ders/ödev/ödeme etkilenmez) |

> Veli dashboard'undaki karşılığı: [`../modules/m09_parents.md`](../modules/m09_parents.md) gizlilik matrisi.

**Oturum güvenliği** (`sessionTerminationPolicy`, M15-3) — tekli seçim:

| Seçenek | Anlamı |
|---------|--------|
| Tüm cihazlarda açık kal (`KeepLatest`) | Birden fazla cihazda aynı anda oturum |
| Yalnız son cihazda kal (`TerminateOtherSessions`) | Yeni giriş diğer refresh oturumlarını iptal eder |

## Davranış
`settings_notifications.md` ile aynı: iyimser güncelleme + anında `PUT`, hata olursa geri alma + SnackBar,
oturum yoksa boş durum, mock fallback yok.

## İlgili
- Modül: [`../modules/m15_settings.md`](../modules/m15_settings.md) §3.1 / §3.2 · [`../modules/m01_identity.md`](../modules/m01_identity.md)
- Kardeş ekran: [`settings_notifications.md`](settings_notifications.md) · Giriş noktası: [`more.md`](more.md)

---

*Gizlilik ve Güvenlik Ayarları Ekranı | Güncelleme: 2026-09-03*
