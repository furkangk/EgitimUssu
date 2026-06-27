# 🎬 Animasyon Kuralları — EğitimÜssü Flutter

> **Kapsam:** Geçiş animasyonları, mikro-etkileşimler ve animasyon performans kuralları.
> Amaç: Hızlı hissettiren, dikkat dağıtmayan, amaca yönelik animasyonlar.
>
> **Güncelleme:** 2026-06-27

---

## 1. Temel İlkeler

- **Amaçlı ol:** Animasyon kullanıcıya bağlamı anlatmalı (nereye gidiyor, ne değişiyor).
- **Hızlı tut:** Kullanıcı beklemek istemez. Süre genellikle 150-300ms.
- **Tutarlı ol:** Aynı bağlamdaki animasyonlar aynı süre ve eğriyi kullanır.
- **İptal edilebilir:** Kullanıcı animasyon bitmeden aksiyona devam edebilmeli.
- **Azalt:** Gereksiz animasyon UX'i kötüleştirir — şüphede animasyon ekleme.

---

## 2. Süre & Eğri Standartları

```dart
// Süreler
const Duration kDurationFast   = Duration(milliseconds: 150);  // hover, focus, buton
const Duration kDurationNormal = Duration(milliseconds: 250);  // kart, chip, sheet
const Duration kDurationSlow   = Duration(milliseconds: 350);  // sayfa geçişi, modal

// Eğriler
const Curve kCurveDefault  = Curves.easeInOut;      // genel amaçlı
const Curve kCurveEnter    = Curves.easeOut;         // içeri giren öğeler
const Curve kCurveExit     = Curves.easeIn;          // çıkan öğeler
const Curve kCurveSpring   = Curves.elasticOut;      // başarı/ödül animasyonları (dikkatli kullan)
```

---

## 3. Sayfa Geçiş Animasyonları (go_router)

```dart
// go_router'da özel geçiş — tüm route'larda tutarlı kullan
CustomTransitionPage(
  child: page,
  transitionsBuilder: (context, animation, secondaryAnimation, child) {
    return FadeTransition(
      opacity: CurvedAnimation(parent: animation, curve: kCurveEnter),
      child: child,
    );
  },
  transitionDuration: kDurationSlow,
)
```

| Geçiş türü | Animasyon | Kullanım |
|-----------|-----------|---------|
| İleri navigasyon | Fade + SlideX (+16px → 0) | Detay sayfaları |
| Geri navigasyon | Fade (otomatik) | go_router varsayılan |
| Modal / Bottom Sheet | SlideY (alt → yukarı) | `showModalBottomSheet` |
| Dialog | Fade + Scale (0.9 → 1.0) | `showDialog` |

---

## 4. Mikro-Etkileşimler

### Buton basma
```dart
// AppPrimaryButton içinde zaten var — ayrıca ekleme
// Basılıyken: scale 0.97, opacity 0.85 — AnimatedScale veya InkWell splash
```

### Kart tıklama
```dart
InkWell(
  borderRadius: BorderRadius.circular(AppRadius.lg),  // 16
  splashColor: AppColors.primaryLight,                // #EAF2FB
  highlightColor: AppColors.primaryLight.withOpacity(0.5),
  onTap: onTap,
  child: card,
)
```

### Yükleme → İçerik geçişi
```dart
AnimatedSwitcher(
  duration: kDurationNormal,
  child: isLoading
    ? const LoadingStateView(key: ValueKey('loading'))
    : ContentWidget(key: ValueKey('content')),
)
```

### Liste öğesi ekleme/silme
```dart
AnimatedList(...)  // veya
// SliverAnimatedList ile CustomScrollView içinde
```

---

## 5. Skeleton / Shimmer Loading

Uzun süren yüklemelerde (>500ms beklenen) spinner yerine skeleton tercih et:

```dart
// Shimmer efekti için shimmer paketi veya manuel:
AnimatedBuilder(
  animation: _shimmerController,
  builder: (context, child) {
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [Color(0xFFE5E7EB), Color(0xFFF0F2F5), Color(0xFFE5E7EB)],
          stops: [0.0, _shimmerController.value, 1.0],
          begin: Alignment.centerLeft,
          end: Alignment.centerRight,
        ),
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
    );
  },
)
```

Skeleton boyutları gerçek içerikle aynı olmalı — layout kayması (layout shift) önlenir.

---

## 6. Başarı / Ödül Animasyonları

Öğrenci motivasyonu için kullanılır (streak tamamlama, ödev bitirme):

```dart
// Basit confetti / scale animasyonu
ScaleTransition(
  scale: Tween(begin: 0.0, end: 1.0).animate(
    CurvedAnimation(parent: _controller, curve: kCurveSpring),
  ),
  child: SuccessIcon(),
)
```

- **Öğretmen ekranlarında kullanma** — profesyonel ton.
- **Öğrenci ekranlarında:** Streak, görev tamamlama, puan kazanma.
- **Veli ekranlarında:** Yok — sade özet tonu bozulur.

---

## 7. Performans Kuralları

- `AnimatedBuilder` veya `AnimatedWidget` kullan — `setState` ile animasyon döngüsü oluşturma.
- `RepaintBoundary` ile animasyonlu widget'ı izole et (özellikle liste içinde).
- Animasyon controller'ı `dispose()` et — memory leak önlenir.
- `Curves.linear` kullanma — her zaman bir ease eğrisi seç.
- `opacity: 0` yerine `Offstage` veya `Visibility` — render tree'den çıkar.
- Karmaşık animasyon gerekiyorsa `Lottie` paketi (`.json` dosyası) tercih et; GIF/WebP kullanma.

```dart
// Her AnimationController için
@override
void dispose() {
  _controller.dispose();
  super.dispose();
}
```

---

## 8. `AnimatedContainer` Hızlı Referans

```dart
AnimatedContainer(
  duration: kDurationNormal,
  curve: kCurveDefault,
  width: isExpanded ? double.infinity : 120,
  height: isExpanded ? 200 : 56,
  decoration: BoxDecoration(
    color: isSelected ? AppColors.primary : AppColors.surface,
    borderRadius: BorderRadius.circular(AppRadius.lg),
  ),
)
```

---

*Animasyon Kuralları | Güncelleme: 2026-06-27*
