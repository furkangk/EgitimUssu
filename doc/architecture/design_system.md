# 🎨 Tasarım Sistemi (Design System) — Platformlar Arası

> **Kapsam:** Mobil (Flutter) ve web (Angular) için **ortak görsel dil**: renk, tipografi, spacing, radius, gölge
> token'ları + Bileşen Tabanlı / Atomic Design ilkeleri. Bu doküman token'ların **tek doğruluk kaynağıdır**;
> platform uygulamaları (Flutter `AppColors`, Angular Tailwind config) buradaki değerleri bağlar.
>
> **Platform uygulamaları:** Flutter → [`mobile_flutter.md`](mobile_flutter.md) §11-12 (Dart `app_colors.dart` vb.) ·
> Angular → [`web_angular.md`](web_angular.md) (Tailwind `theme.extend`). Çelişki halinde **bu dosya esastır.**
>
> **Güncelleme:** 2026-06-24

---

## 1. Tasarım İlkeleri

EğitimÜssü; öğretmen, öğrenci ve veli rollerine hizmet eden özel ders platformudur. Tasarım dili **modern, sade,
güven veren ve eğitim odaklıdır**.

- **Rol-duyarlı sadelik:** Her rol yalnızca kendine gereken bilgiyi görür (öğretmen=operasyon hızı, öğrenci=motivasyon,
  veli=sade özet).
- **Kart tabanlı okunabilirlik:** Yoğun veri ekranları kartlara bölünür; sayılar büyük ve net.
- **Tutarlılık:** Renk/tipografi/spacing değerleri **iki platformda da aynıdır** (bu dosya).
- **Erişilebilirlik:** Renk tek başına durum göstergesi olamaz; metin/badge ile desteklenir.

## 2. Bileşen Tabanlı Tasarım (Atomic / CBD)

Faz 0.6 hedefi olan "UI tasarım sistemi ve bileşen kütüphanesi" doğrultusunda **her iki platform Atomic Design** uygular.

- **Atomik bileşenler:** En küçük yapı taşları (buton, input, etiket, avatar, badge) merkezi bir klasörde toplanır
  (Flutter `shared/widgets`, Angular `shared/components`).
- **Smart vs. Dumb:** Görsel sunum yapan bileşenler (Dumb) ile veri/iş mantığını yöneten bileşenler (Smart) ayrılır.
- **Bileşik bileşenler:** `MetricCard`, `LessonCard`, `StudentListTile` gibi atomlardan oluşan moleküller paylaşılır.

## 3. Renk Paleti (Kanonik)

> **Ana renk (primary): `#082B4F` / `0xFF082B4F`** — INDEX §0 kanonik değeriyle aynı.

| Token | HEX | Flutter | Kullanım |
|-------|-----|---------|----------|
| `primary` | `#082B4F` | `0xFF082B4F` | Ana buton, aktif tab, seçili bottom nav, koyu dashboard kartı |
| `primaryDark` | `#061F3A` | `0xFF061F3A` | Basılı/koyu varyant |
| `primaryLight` | `#EAF2FB` | `0xFFEAF2FB` | Hafif arka plan, bilgi kartı |
| `secondary` / `accentBlue` | `#3D8BFF` | `0xFF3D8BFF` | İkincil vurgu, link |
| `accentGreen` | `#20B486` | `0xFF20B486` | Başarı, tamamlandı, ödeme alındı, devam oranı |
| `accentOrange` | `#FFA726` | `0xFFFFA726` | Uyarı, yaklaşan ders, bekleyen ödeme, streak |
| `accentRed` | `#FF5A5F` | `0xFFFF5A5F` | Hata, geciken ödev, iptal, durdur |
| `accentTeal` | `#20A4A9` | `0xFF20A4A9` | Çalışma süresi grafikleri, motivasyon |
| `background` | `#F7F9FC` | `0xFFF7F9FC` | Genel scaffold arka planı |
| `surface` / `card` | `#FFFFFF` | `0xFFFFFFFF` | Kart, input, sheet, modal zemini |
| `textPrimary` | `#111827` | `0xFF111827` | Ana metin |
| `textSecondary` | `#6B7280` | `0xFF6B7280` | İkincil metin |
| `textMuted` | `#9CA3AF` | `0xFF9CA3AF` | Soluk/placeholder |
| `border` | `#E5E7EB` | `0xFFE5E7EB` | Kart/input kenarlığı |
| `divider` | `#F0F2F5` | `0xFFF0F2F5` | Ayraç |

**Durum renk kuralı (skor/oran):** ≥85 → yeşil · 70-84 → turuncu · ≤69 → kırmızı.

## 4. Tipografi

Önerilen font: **Inter** (alternatif: SF Pro Display, Nunito Sans).

| Stil | Boyut | Ağırlık | Satır yüks. | Kullanım |
|------|------:|---------|------------:|----------|
| `h1` | 28 | w800 | 1.2 | Ana metrik / büyük başlık |
| `h2` | 22 | w700 | 1.25 | Sayfa başlığı |
| `h3` | 18 | w700 | 1.3 | Bölüm başlığı |
| `title` | 16 | w700 | 1.35 | Kart başlığı |
| `body` | 14 | w500 | 1.45 | Gövde metni |
| `caption` | 12 | w500 | 1.35 | Açıklama |
| `small` | 10 | w500 | 1.3 | Badge, bottom nav etiketi |

Hiyerarşi özeti: sayfa başlığı 18-22 bold · kart başlığı 14-16 semi/bold · ana metrik 24-32 bold ·
açıklama 12-14 medium · badge 10-12 semi-bold · bottom nav etiketi 10-11.

## 5. Spacing

| Token | px | Token | px |
|-------|---:|-------|---:|
| `xs` | 4 | `xl` | 20 |
| `sm` | 8 | `xxl` | 24 |
| `md` | 12 | `xxxl` | 32 |
| `lg` | 16 |  |  |

Sayfa kuralları: yatay padding **16** · kart iç padding 14-16 · kartlar arası 12 · section başlığı↔içerik 8-12 ·
bottom nav üst boşluğu 8.

## 6. Radius & Gölge

| Token | px | Kullanım |
|-------|---:|----------|
| `sm` | 8 | küçük öğe |
| `md` | 12 | buton, input, segment tab |
| `lg` | 16 | kart |
| `xl` | 20 | büyük kart |
| `pill` | 999 | chip/pill, avatar (dairesel) |

Bottom sheet üst köşeler: 24. **Gölge:** yumuşak — `black @ %4 opacity, blur 14, offset (0,6)`. Keskin kontrasttan kaçınılır.

## 7. Platform Bağlama (Binding)

| Token grubu | Flutter | Angular |
|-------------|---------|---------|
| Renk | `core/theme/app_colors.dart` (`AppColors`) | Tailwind `theme.extend.colors` |
| Tipografi | `core/theme/app_text_styles.dart` | Tailwind `fontSize` + global CSS |
| Spacing | `core/theme/app_spacing.dart` | Tailwind `spacing` (4px tabanlı zaten uyumlu) |
| Radius | `core/theme/app_radius.dart` | Tailwind `borderRadius` |
| Karanlık mod | (planlanan) | Tailwind native dark mode (öğrenci çalışma seansları için) |

> **Kural:** Değerler doğrudan kodda yazılmaz; her platform yukarıdaki token sınıfları/konfigürasyonu üzerinden çağırır.
> Tab/segment widget'ına özgü ek token'lar → [`../tab_widget.md`](../tab_widget.md).

---

*Tasarım Sistemi | Güncelleme: 2026-06-24*
