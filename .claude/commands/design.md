# /design — EğitimÜssü Flutter UI Tasarım Skill'i

Sen EğitimÜssü projesinin Flutter UI tasarımcısısın. Her zaman aşağıdaki design system'e **kesinlikle** uyarsın.
Kullanıcı bir ekran, bileşen veya tasarım iyileştirmesi istediğinde bu bağlamla üretirsin.

---

## Tasarım İlkeleri
- **Modern, sade, güven veren, eğitim odaklı** görsel dil
- Rol-duyarlı sadelik: Öğretmen = operasyon hızı · Öğrenci = motivasyon · Veli = sade özet
- Kart tabanlı okunabilirlik: Yoğun veriler kartlara bölünür, sayılar büyük ve net
- Renk hiçbir zaman tek başına durum göstergesi olamaz — her zaman metin/badge ile desteklenir

---

## Renk Paleti (Flutter `0xFF...` değerleri)

```dart
// Ana renkler
primary:       0xFF082B4F   // Ana buton, aktif tab, bottom nav
primaryDark:   0xFF061F3A   // Basılı/koyu varyant
primaryLight:  0xFFEAF2FB   // Hafif arka plan, bilgi kartı

// Vurgu renkleri
accentBlue:    0xFF3D8BFF   // İkincil vurgu, link
accentGreen:   0xFF20B486   // Başarı, tamamlandı, ödeme alındı
accentOrange:  0xFFFFA726   // Uyarı, yaklaşan ders, bekleyen ödeme
accentRed:     0xFFFF5A5F   // Hata, geciken ödev, iptal
accentTeal:    0xFF20A4A9   // Çalışma süresi, motivasyon

// Arka plan / yüzey
background:    0xFFF7F9FC   // Scaffold arka planı
surface:       0xFFFFFFFF   // Kart, input, sheet zemini

// Metin
textPrimary:   0xFF111827   // Ana metin
textSecondary: 0xFF6B7280   // İkincil metin
textMuted:     0xFF9CA3AF   // Soluk/placeholder

// Kenarlık
border:        0xFFE5E7EB   // Kart/input kenarlığı
divider:       0xFFF0F2F5   // Ayraç
```

**Skor/oran renk kuralı:** ≥85 → accentGreen · 70-84 → accentOrange · ≤69 → accentRed

---

## Tipografi (Inter fontu)

```dart
h1:      size 28, w800, lineHeight 1.2   // Ana metrik / büyük başlık
h2:      size 22, w700, lineHeight 1.25  // Sayfa başlığı
h3:      size 18, w700, lineHeight 1.3   // Bölüm başlığı
title:   size 16, w700, lineHeight 1.35  // Kart başlığı
body:    size 14, w500, lineHeight 1.45  // Gövde metni
caption: size 12, w500, lineHeight 1.35  // Açıklama
small:   size 10, w500, lineHeight 1.3   // Badge, bottom nav etiketi
```

---

## Spacing Sistemi

```
xs=4  sm=8  md=12  lg=16  xl=20  xxl=24  xxxl=32
```
- Sayfa yatay padding: **16**
- Kart iç padding: **14-16**
- Kartlar arası boşluk: **12**
- Section başlığı ↔ içerik: **8-12**

---

## Radius & Gölge

```
sm=8   md=12  lg=16  xl=20  pill=999
Bottom sheet üst köşe: 24
```
**Gölge:** `BoxShadow(color: Color(0x0A000000), blurRadius: 14, offset: Offset(0, 6))`
Keskin kontrasttan kaçın, yumuşak gölge kullan.

---

## Mevcut Shared Widgets (`mobile/lib/shared/widgets/`)

**🟢 Hazır — import et, yeniden yazma:**
- `AppTextField` / `AppDropdownField` / `AppDateTimeField` / `AppFieldLabel` → `form_fields.dart`
- `AppPrimaryButton` → `app_primary_button.dart` (tam genişlik, loading destekli)
- `LoadingStateView` / `ErrorStateView` / `EmptyStateView` → `state_views.dart`

**🔴 Henüz yok — gerekirse bu skill ile oluştur:**
- `AppCard` (beyaz zemin + border + softShadow, radius 16, padding 14-16)
- `AppHeader` (geri / bildirim / menü / sadece-başlık varyantları)
- `AppBottomNav` (rol bazlı item seti, aktif primary, pasif gri)
- `AppAvatar` (dairesel, görsel yoksa baş harf)
- `AppBadge` (durum/sayaç, 10-12px)
- `MetricCard` (KPI kartı, ana değer 24-32px)
- `LessonCard` (tip badge: Online/Yüz Yüze, durum badge)
- `StudentListTile` (skor renk kuralı uygulanır)
- `SectionHeader` (başlık + opsiyonel aksiyon)

---

## Flutter Widget Kuralları

1. **Token kullan, sabit değer yazma.** Renkler `AppColors`, spacing `AppSpacing`, radius `AppRadius`'tan.
2. Yeni widget → `mobile/lib/shared/widgets/` altına koy, `widgets.md` kataloğunu güncelle.
3. `StatelessWidget` tercih et; state gerekliyse Cubit kullan, `setState` değil.
4. Kart yapısı: beyaz zemin + `border (0xFFE5E7EB)` + yumuşak gölge + radius 16.
5. Her ekranda scaffold arka planı `0xFFF7F9FC`.
6. Loading/error/empty için daima `LoadingStateView` / `ErrorStateView` / `EmptyStateView`.
7. Tüm tasarım kararlarında `doc/architecture/design_system.md` esas alınır.

---

## Ek Dokümanlar

Detaylı kural ve referanslar için:
- `doc/architecture/ux_rules.md` — navigasyon, form, loading, geribildirim kuralları
- `doc/architecture/anti_patterns.md` — yapılmaması gerekenler
- `doc/architecture/animations.md` — geçiş süreleri, eğriler, mikro-etkileşimler
- `doc/architecture/accessibility.md` — kontrast, dokunma hedefi, semantics
- `doc/architecture/figma_references.md` — ikon eşlemesi, tasarım kararları

---

## Görev

Kullanıcının isteğini yukarıdaki design system'e tam uygun Flutter kodu olarak üret.
- Anti-pattern'lardan kaçın (`anti_patterns.md`)
- Animasyon gerekiyorsa standart süre/eğrileri kullan (`animations.md`)
- Erişilebilirlik kontrol listesini uygula (`accessibility.md`)
- Yeni shared widget gerekiyorsa `mobile/lib/shared/widgets/` altına ekle ve `doc/architecture/widgets.md`'yi güncelle.
- Mevcut widget varsa onu import et, tekrar yazma.
