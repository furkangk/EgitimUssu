---
title: "Cihaz Oturumları Ekranı"
summary: "Hesabın açık olduğu cihazların listesi (/account/sessions): 'bu cihaz' rozeti, tekil oturum kapatma ve 'diğer tüm oturumları kapat' — onay diyaloğuyla"
tags: [sayfa, guvenlik, oturum, identity, M01-4, D-03]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/more/presentation/pages/device_sessions_page.dart
  - mobile/lib/features/more/presentation/cubit/device_sessions_cubit.dart
  - mobile/lib/features/more/data/repositories/device_session_repository_impl.dart
  - mobile/lib/features/more/domain/device_session_contracts.dart
updated: 2026-09-05
---

# Cihaz Oturumları (`/account/sessions`)

> **Feature:** `more` · **State:** `DeviceSessionsCubit` / `DeviceSessionsState` · **Veri:** Gerçek API (mock fallback **yok**)
> **Güncelleme:** 2026-09-05

## Amaç
"Hesabım hangi cihazlarda açık?" sorusunu cevaplar ve tanınmayan bir cihazın oturumunu kapatma
imkânı verir. Parola değiştirmenin tek başına yetmediği durumun (çalınmış bir refresh token)
kullanıcı tarafındaki çaresidir.

## Erişim
Her rol. **Diğer → Cihaz oturumları**. Oturum zorunludur.

## Veri kaynağı

| İşlem | Uç |
|-------|-----|
| Liste | `GET /api/identity/sessions` |
| Tekil kapatma | `POST /api/identity/sessions/{sessionId}/revoke` |
| Diğerlerini kapatma | `POST /api/identity/sessions/revoke-all` → `{ revokedCount }` |

Yalnız **aktif** oturumlar gelir. Refresh akışı jetonu döndürdüğü için iptal edilmiş satırlar
birikir; hepsi gösterilseydi kullanıcı tek cihazını onlarca oturum olarak görürdü.

## Ekranda ne var

| Öğe | Davranış |
|-----|----------|
| Cihaz adı | İstemcinin bildirdiği ad; yoksa "Bilinmeyen cihaz" |
| **"Bu cihaz" rozeti** | `isCurrent` — erişim jetonundaki `sid` talebine göre sunucu belirler |
| Bağlandı / Son kullanım | İlk bağlanma anı **jeton yenilense de değişmez**; son kullanım her yenilemede ilerler |
| "Kapat" | **Yalnız yabancı cihazlarda.** Mevcut cihazın oturumunu buradan kapatmak "çıkış yap"tır ve o eylem Diğer ekranında zaten var |
| "Diğer tüm oturumları kapat" | Yalnız başka oturum varsa görünür; kapatılan sayı SnackBar'da bildirilir |

## Üç kural

1. **Her kapatma onay ister.** Yanlış cihazı kapatmak geri alınamaz; o cihazda yeniden giriş gerekir.
2. **İşlemden sonra liste sunucudan tazelenir.** Yerelde satır silmek, sunucuda başarısız olmuş bir
   iptali başarılı göstermeye açık kapı bırakırdı — güvenlik ekranında iyimser güncelleme yanlış
   güven verir.
3. **Mock fallback yok.** Sahte bir oturum listesi "yalnız kendi telefonum açık" yanılgısı üretir.
   Hata olduğu gibi gösterilir (A-05).

## Durumlar

| Durum | Görünen |
|-------|---------|
| Yükleniyor | `LoadingStateView` |
| Hata | `ErrorStateView` + "Tekrar dene" |
| Boş | "Açık oturum yok" |

---

> İlgili: [m01_identity](../modules/m01_identity.md) §3.3 · [more](../architecture/mobile_flutter.md) ·
> [sayfa indeksi](00_pages_index.md)

*Cihaz Oturumları | Güncelleme: 2026-09-05 (P13 Task 7 / M01-4+D-03: ilk sürüm)*
