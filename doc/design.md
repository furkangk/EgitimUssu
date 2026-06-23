# 📱 🌐 Uygulama Tasarım Dokümanı (design.md)

## 1. Genel Mimari Yaklaşım

Sistem, backend katmanındaki **Modüler Monolit** yapısını takip ederek, her iki platformda da **Feature-based (Özellik tabanlı) Clean Architecture** prensiplerini benimser.

- **Mobil (Flutter):** Birincil platform; öğretmen, öğrenci ve velinin günlük operasyonel kullanımı için (Faz 1-3) optimize edilecektir.
- **Web (Angular):** İkincil platform; özellikle admin yönetimi, öğretmenler için gelişmiş raporlama ve büyük ekran deneyimi gerektiren analizler için (Faz 4-5) kullanılacaktır.

---

## 2. 🧩 Component-Based Design (Bileşen Tabanlı Tasarım)

Projenin Faz 0.6 aşamasında belirtilen **"UI tasarım sistemi ve bileşen kütüphanesi kurulumu"** hedefi doğrultusunda her iki platformda da **Atomic Design** yaklaşımı uygulanacaktır.

### Ortak CBD Prensipleri

#### Atomic Widgets / Components
En küçük yapı taşları (butonlar, inputlar, etiketler) merkezi bir klasörde (`shared/widgets` veya `shared/components`) toplanır.

#### Smart vs. Dumb Components
Görsel sunum yapan bileşenler (**Dumb Components**) ile veri ve iş mantığını yöneten bileşenler (**Smart Components**) birbirinden ayrılır.

#### Design System
Renk paleti, tipografi ve spacing değerleri platformlar arasında tutarlı tutulur.

---

## 3. 📱 Mobil Uygulama Tasarımı (Flutter)

Mobil uygulama, Clean Architecture katmanlarını özellik bazlı bir yapıda sunar.

## 📁 Klasör Yapısı

```txt
lib/
├── core/               # Ağ yönetimi (Dio), depolama, router, tema
├── shared/             # Uygulama genelinde kullanılan widget'lar (Atomic Design)
└── features/           # Backend modülleri ile 1:1 eşleşen özellikler
    ├── auth/           # Kayıt, Giriş, Rol Yönetimi (M01)
    ├── lesson/         # Takvim, Ders Planlama (M04, M05)
    ├── study/          # Çalışma Sayacı, Test Takibi (M08)
    ├── matching/       # Öğretmen Keşfi (M12)
    └── notification/   # Push Bildirimler (FCM)
```

---

## 4. 🌐 Web Uygulama Tasarımı (Angular + Tailwind CSS)

Web tarafı, Angular'ın modüler yapısını kullanarak Tailwind CSS ile modern ve esnek bir arayüz sunar.

## 📁 Klasör Yapısı

```txt
src/app/
├── core/               # Guards, Interceptors, Singleton Servisler
├── shared/             # Reusable bileşenler (Tailwind tabanlı), Pipes
└── features/           # Modül bazlı özellik setleri
    ├── admin/          # Sistem ve İçerik Yönetimi
    ├── teacher-dash/   # M14: Gelişmiş Raporlama ve Analiz
    └── matching/       # M12: Öğretmen Listeleme ve Detaylı Filtreleme
```

## 🎨 UI/UX Stratejisi (Tailwind CSS)

### Utility-First
Tailwind'in `sm`, `md`, `lg` breakpoint'leri ile öğretmenlerin hem tablet hem masaüstü tarayıcılarda rahat raporlama yapması sağlanır.

### Consistency
Ortak bileşen stilleri Tailwind `@layer components` altında tanımlanarak Angular bileşenlerinde tekrar kullanımı sağlanır.

### Dark Mode
Öğrencilerin bireysel çalışma seansları (M08) için Tailwind'in native dark mode desteği entegre edilir.

---

## 5. 🚀 Faz Bazlı Uygulama Geliştirme Planı

Tasarım, projenin 6 fazlı yol haritası ile uyumlu ilerleyecektir.

### Faz 0-1 (Altyapı & MVP)
Flutter uygulamasında Auth, Takvim ve Ders Oturumu modüllerinin çekirdek yapısı ve CBD temelleri kurulur.

### Faz 2-3 (Genişleme)
Öğrenci çalışma sayacı (M08) ve Veli paneli (M09) Flutter platformuna eklenir.

### Faz 4-5 (Web & Premium)
Angular tabanlı admin ve raporlama ekranları (M14) devreye alınır; eşleştirme sistemi (M12) her iki platformda aktif edilir.

---

## 5.5 Rol Bazlı Navigasyon ve Yeni Özellik Alanları (v2.1)

> `promp.txt` vizyonuyla gelen genişleme. Detaylar: [`roles/`](roles/00_roller_genel_bakis.md) ve ilgili modül dokümanları.

### Rol bazlı navigasyon
Mevcut app **öğretmen odaklıdır**. `app_router.dart` redirect mantığı kullanıcı rolüne göre farklı dashboard'a yönlendirecek şekilde genişletilmeli:
- **Öğretmen:** Ana Sayfa · Dersler · Öğrenciler · Takvim · Finans · Diğer
- **Öğrenci:** Ana Sayfa · Çalışma (kronometre) · Dersler · Gelişim · Profil
- **Veli:** Ana Sayfa · Raporlar · Bildirimler · Profil

### Yeni özellik alanları (feature klasörleri — planlanan)
| Alan | Açıklama | Modül |
|------|----------|-------|
| `study` | Öğrenci kronometre/test/seri/başarım | [M08](modules/m08_study.md) |
| `progress` | Konu bazlı gelişim/grafik | [M10](modules/m10_progress_tracking.md) |
| `parent` | Veli paneli (read-model) | [M09](modules/m09_parents.md) |
| `messaging` | Öğretmen↔öğrenci/veli sohbet | [M16](modules/m16_messaging.md) |
| `listings` | İlan ver/keşfet (iki taraflı) + filtre | [M12](modules/m12_matching.md) |
| `reviews` | Yıldız + yorum | [M13](modules/m13_reviews.md) |
| `membership` | Üyelik/paywall + reklam yerleşimi | [M17](modules/m17_membership.md) |
| `feedback` | Hata bildirimi + şikayet | [M18](modules/m18_feedback.md) |

> **Reklam:** Ücretsiz kullanıcılarda reklam yerleşimi (banner/araya giren); ücretli üyede gizlenir (M17).
> **Paywall:** Premium özellikler kilit/CTA ile gösterilir.

---

## 6. 🧾 Sonuç

Bu tasarım dokümanı; öğretmen, öğrenci ve veli akışlarını tek platformda birleştiren, mobil öncelikli, Bileşen Tabanlı ve ileride mikroservislere ayrılmaya hazır bir yapı sunar.
