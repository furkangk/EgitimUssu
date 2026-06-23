# EğitimÜssü Flutter UI Tasarım Dokümanı

> Bu doküman, paylaşılan mobil uygulama ekran tasarımını Flutter ile geliştirmek için detaylı UI/UX, component, tema, navigation ve sayfa bazlı uygulama rehberi olarak hazırlanmıştır.

---

## 1. Genel Tasarım Yaklaşımı

EğitimÜssü; öğretmen, öğrenci ve veli rollerine hizmet eden özel ders yönetim uygulamasıdır. Tasarım dili modern, sade, güven veren ve eğitim odaklıdır. Uygulama; ders planlama, öğrenci takibi, ödev yönetimi, ödeme takibi, çalışma süresi, gelişim analizi ve veli bilgilendirme süreçlerini tek mobil deneyimde toplar.

Tasarımda temel hedefler:

- Kullanıcının rolüne göre sadeleştirilmiş deneyim sunmak
- Öğretmen için operasyonel işleri hızlandırmak
- Öğrenci için motivasyon, çalışma takibi ve hedef yönetimi sağlamak
- Veli için çocuğun gelişimini anlaşılır özetlerle göstermek
- Yoğun veri ekranlarını kart tabanlı, okunabilir ve mobil uyumlu hale getirmek
- Flutter tarafında tekrar kullanılabilir component mimarisi oluşturmak

---

## 2. Görsel Kimlik

### 2.1 Marka Hissi

Tasarım; güven, düzen, akademik başarı ve kişisel gelişim hissi vermelidir. Logo ve renk kullanımı kurumsal ama sıcak bir eğitim platformu algısı oluşturmalıdır.

Kullanılacak ana algılar:

- Profesyonel
- Temiz
- Güvenilir
- Öğrenci dostu
- Modern SaaS mobil uygulaması hissi
- Öğretmen ve veli için ciddi, öğrenci için motive edici

---

## 3. Renk Sistemi

### 3.1 Ana Renkler

```dart
class AppColors {
  static const Color primary = Color(0xFF082B4F);
  static const Color primaryDark = Color(0xFF061F3A);
  static const Color primaryLight = Color(0xFFEAF2FB);

  static const Color secondary = Color(0xFF3D8BFF);
  static const Color accentOrange = Color(0xFFFFA726);
  static const Color accentGreen = Color(0xFF20B486);
  static const Color accentRed = Color(0xFFFF5A5F);
  static const Color accentBlue = Color(0xFF3D8BFF);
  static const Color accentTeal = Color(0xFF20A4A9);

  static const Color background = Color(0xFFF7F9FC);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color card = Color(0xFFFFFFFF);

  static const Color textPrimary = Color(0xFF111827);
  static const Color textSecondary = Color(0xFF6B7280);
  static const Color textMuted = Color(0xFF9CA3AF);

  static const Color border = Color(0xFFE5E7EB);
  static const Color divider = Color(0xFFF0F2F5);
}
```

### 3.2 Kullanım Mantığı

| Renk           | Kullanım Alanı                                                      |
| -------------- | ------------------------------------------------------------------- |
| `primary`      | Ana butonlar, aktif tab, seçili bottom nav, koyu dashboard kartları |
| `primaryLight` | Hafif arka planlar, bilgi kartları                                  |
| `accentGreen`  | Başarı, tamamlandı, ödeme alındı, devam oranı                       |
| `accentOrange` | Uyarı, yaklaşan ders, bekleyen ödeme, streak                        |
| `accentRed`    | Hata, geciken ödev, iptal, durdur butonu                            |
| `accentTeal`   | Çalışma süresi grafikleri, motivasyon öğeleri                       |
| `background`   | Genel scaffold arka planı                                           |
| `surface`      | Kart, input, sheet, modal zeminleri                                 |

---

## 4. Tipografi

Flutter tarafında `Inter`, `SF Pro Display` veya `Nunito Sans` tercih edilebilir. Tasarımdaki his için Inter önerilir.

```dart
class AppTextStyles {
  static const TextStyle h1 = TextStyle(
    fontSize: 28,
    fontWeight: FontWeight.w800,
    height: 1.2,
  );

  static const TextStyle h2 = TextStyle(
    fontSize: 22,
    fontWeight: FontWeight.w700,
    height: 1.25,
  );

  static const TextStyle h3 = TextStyle(
    fontSize: 18,
    fontWeight: FontWeight.w700,
    height: 1.3,
  );

  static const TextStyle title = TextStyle(
    fontSize: 16,
    fontWeight: FontWeight.w700,
    height: 1.35,
  );

  static const TextStyle body = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w500,
    height: 1.45,
  );

  static const TextStyle caption = TextStyle(
    fontSize: 12,
    fontWeight: FontWeight.w500,
    height: 1.35,
  );

  static const TextStyle small = TextStyle(
    fontSize: 10,
    fontWeight: FontWeight.w500,
    height: 1.3,
  );
}
```

### 4.1 Metin Hiyerarşisi

- Sayfa başlığı: 18-22 px, bold
- Kart başlığı: 14-16 px, semi-bold veya bold
- Ana metrik değerleri: 24-32 px, bold
- Açıklama metni: 12-14 px, medium
- Badge yazıları: 10-12 px, semi-bold
- Bottom navigation label: 10-11 px

---

## 5. Spacing Sistemi

Tüm ekranlarda tutarlı boşluk sistemi kullanılmalıdır.

```dart
class AppSpacing {
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double xxl = 24;
  static const double xxxl = 32;
}
```

### 5.1 Sayfa İç Boşlukları

- Sayfa yatay padding: `16 px`
- Kart iç padding: `14-16 px`
- Kartlar arası boşluk: `12 px`
- Section başlığı ile içerik arası: `8-12 px`
- Bottom nav üst boşluğu: `8 px`

---

## 6. Border Radius ve Shadow

### 6.1 Radius

```dart
class AppRadius {
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double pill = 999;
}
```

Kullanım:

- Buton: `12 px`
- Kart: `14-18 px`
- Input: `12 px`
- Avatar: dairesel
- Segment tab: `12 px`
- Bottom sheet: üst köşeler `24 px`

### 6.2 Shadow

Kartlar hafif gölge kullanmalı. Tasarımın genelinde keskin kontrasttan kaçınılmalıdır.

```dart
final List<BoxShadow> softShadow = [
  BoxShadow(
    color: Colors.black.withOpacity(0.04),
    blurRadius: 14,
    offset: const Offset(0, 6),
  ),
];
```

---

## 7. Layout Kuralları

### 7.1 Temel Ekran Ölçüsü

Referans tasarım iPhone benzeri dar mobil genişlikte tasarlanmıştır. Flutter tarafında responsive davranış için:

- `SafeArea` kullanılmalı
- Yatay padding sabit `16`
- Geniş ekranlarda içerik maksimum genişliği `430-480 px` ile sınırlandırılabilir
- Liste ekranlarında `CustomScrollView` veya `SingleChildScrollView + Column` tercih edilebilir

### 7.2 Genel Sayfa Şablonu

```dart
Scaffold(
  backgroundColor: AppColors.background,
  appBar: AppHeader(...),
  body: SafeArea(
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: ...,
    ),
  ),
  bottomNavigationBar: AppBottomNav(...),
)
```

---

## 8. Ortak Component Sistemi

Flutter projesinde tasarımın sürdürülebilir olması için component-based yapı kurulmalıdır.

Önerilen klasör yapısı:

```text
lib/
  core/
    theme/
      app_colors.dart
      app_text_styles.dart
      app_theme.dart
      app_spacing.dart
      app_radius.dart
    constants/
    utils/
  shared/
    widgets/
      app_button.dart
      app_card.dart
      app_header.dart
      app_bottom_nav.dart
      app_text_field.dart
      app_avatar.dart
      app_badge.dart
      app_segmented_tab.dart
      metric_card.dart
      progress_card.dart
      lesson_card.dart
      student_list_tile.dart
      payment_tile.dart
      assignment_tile.dart
  features/
    auth/
    role_selection/
    teacher_dashboard/
    student_dashboard/
    parent_dashboard/
    lessons/
    assignments/
    payments/
    study_room/
    analytics/
    profile/
```

---

## 9. Ortak Widget Detayları

### 9.1 AppButton

Ana butonlar koyu lacivert olmalıdır. Tasarımda butonlar geniş, yüksekliği yaklaşık `48 px`, radius `12 px` olarak görünmektedir.

Varyantlar:

- Primary button
- Secondary outline button
- Danger button
- Icon button
- Small action button

```dart
class AppButton extends StatelessWidget {
  final String text;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool isLoading;
  final AppButtonVariant variant;

  const AppButton({
    super.key,
    required this.text,
    this.onPressed,
    this.icon,
    this.isLoading = false,
    this.variant = AppButtonVariant.primary,
  });
}
```

### 9.2 AppCard

Tüm bilgi alanları kart tabanlıdır.

Özellikleri:

- Beyaz zemin
- Hafif border
- Hafif shadow
- Radius 16
- Padding 14-16

```dart
Container(
  padding: const EdgeInsets.all(16),
  decoration: BoxDecoration(
    color: AppColors.surface,
    borderRadius: BorderRadius.circular(16),
    border: Border.all(color: AppColors.border),
    boxShadow: softShadow,
  ),
  child: child,
)
```

### 9.3 AppHeader

Ekran üst başlık alanı sade olmalıdır.

Tipler:

- Geri butonlu başlık
- Bildirim ikonlu başlık
- Sağ menü ikonlu başlık
- Sadece başlık

Örnek kullanımlar:

- Öğretmen Paneli: sol geri, başlık, sağ bildirim
- Öğrenci Listesi: başlık ve sağ küçük ikon
- Profil: geri butonu ve başlık
- Bildirimler: geri butonu ve başlık

### 9.4 AppBottomNav

Bottom navigation rol bazlı değişmelidir.

Öğretmen için:

- Ana Sayfa
- Dersler
- Öğrenciler
- Takvim
- Diğer

Öğrenci için:

- Ana Sayfa
- Çalışma
- Dersler
- Gelişim
- Profil

Veli için:

- Ana Sayfa
- Raporlar
- Bildirimler
- Profil

Aktif item koyu lacivert veya primary renk ile gösterilmelidir. Pasif item gri olmalıdır.

### 9.5 MetricCard

KPI ve özet kartları için kullanılacaktır.

Alanlar:

- Başlık
- Ana değer
- Alt açıklama
- Opsiyonel ikon
- Opsiyonel trend
- Opsiyonel progress

Kullanıldığı ekranlar:

- Öğretmen paneli
- Öğrenci detay
- Öğrenci paneli
- Veli paneli
- Test performansı

### 9.6 AppSegmentedTab

Tasarımda çok sayıda segment tab bulunmaktadır.

Örnekler:

- Genel / Dersler / Ödevler / Ödemeler
- Verilenler / Teslim Edilenler
- Tümü / Tahsil Edilen / Bekleyen
- Haftalık / Aylık
- Aktif Ödevler / Tamamlananlar
- Tümü / Okunmamış / Önemli

Aktif tab:

- Koyu lacivert arka plan
- Beyaz yazı

Pasif tab:

- Açık gri arka plan
- Gri/lacivert yazı

---

# 10. Sayfa Bazlı Detaylı Tasarım

---

## 10.1 Splash / Welcome Ekranı

### Amaç

Kullanıcıyı uygulamaya karşılamak, marka algısı oluşturmak ve giriş/kayıt aksiyonlarını göstermek.

### UI Yapısı

- Üstte logo ve uygulama adı
- Altında kısa slogan
- Ortada 3D öğrenci illüstrasyonu
- Arka planda eğitim ikonları
- Altta iki buton: `Giriş Yap`, `Kayıt Ol`

### İçerik

Başlık:

```text
EğitimÜssü
```

Açıklama:

```text
Özel ders süreçlerinizi tek bir yerde yönetin.
```

Butonlar:

- Giriş Yap
- Kayıt Ol

### Flutter Notları

- Logo için SVG kullanılabilir
- İllüstrasyon asset olarak eklenmeli
- Ekran dikeyde `Column` ile ortalanmalı
- Butonlar ekranın alt kısmına yakın konumlandırılmalı
- `Spacer` ile üst/orta/alt alan dengelenmeli

---

## 10.2 Hesap Türü Seçimi

### Amaç

Kullanıcının rolünü belirlemek ve uygulama deneyimini buna göre kişiselleştirmek.

### UI Yapısı

- Üstte başlık: `Hesap türünü seçin`
- Alt başlık: `Size en uygun deneyimi sunalım.`
- 3 adet role card:
  - Öğretmen
  - Öğrenci
  - Veli
- Her kartta avatar/illustration, başlık, açıklama, sağ ok ikonu
- Altta giriş bağlantısı

### Role Card İçeriği

Öğretmen:

```text
Derslerinizi yönetin, öğrencilerinizi takip edin.
```

Öğrenci:

```text
Çalışmalarınızı takip edin, gelişiminizi görün.
```

Veli:

```text
Çocuğunuzun gelişimini takip edin.
```

### Component

`RoleSelectionCard`

Parametreler:

```dart
final String title;
final String description;
final String imagePath;
final VoidCallback onTap;
```

---

## 10.3 Öğretmen Paneli

### Amaç

Öğretmenin günlük operasyonlarını tek ekranda yönetebilmesini sağlamak.

### Ekrandaki Ana Bölümler

1. Header
2. Günlük özet kartları
3. Yaklaşan ders kartı
4. Hızlı işlem butonları
5. Son aktiviteler
6. Bottom navigation

### Header

Başlık:

```text
Öğretmen Paneli
```

Sağda bildirim ve alarm ikonu bulunur. Bildirim ikonunda küçük kırmızı badge olabilir.

### Günlük Özet Kartları

İki adet yatay kart:

1. Günlük Streak
   - Değer: `14 Gün`
   - Turuncu tonlu arka plan
2. Bugünün Dersleri
   - Değer: `2`
   - Açıklama: `Toplam 3 saat`
   - Koyu lacivert arka plan

### Yaklaşan Ders Kartı

İçerik:

```text
Yaklaşan Ders
Matematik - 9. Sınıf
Ali Yılmaz
15:30 - 16:30
Online
```

Kartta öğrenci avatarı, ders bilgisi ve sağda online badge yer alır.

### Hızlı İşlemler

Dört küçük action kart:

- Ders Planla
- Ödev Ver
- Not Ekle
- Ödeme Ekle

Her biri pastel mor ikon kutucuğu ile gösterilir.

### Son Aktiviteler

Liste kartları:

```text
Ali Yılmaz - Ödev teslim edildi
Bugün, 10:30
```

```text
Zeynep Demir - Ödeme yaptı
Bugün, 08:15
```

Sağda chevron ikonu bulunur.

### Flutter Notları

- Bu ekran `CustomScrollView` ile geliştirilebilir
- Üst KPI kartları için `Row` + `Expanded`
- Quick actions için `GridView.count` veya `Row`
- Son aktiviteler için `ListView.separated`, ancak parent scroll varsa `shrinkWrap: true`

---

## 10.4 Öğrenci Listesi

### Amaç

Öğretmenin öğrencilerini hızlıca araması, durumlarını görmesi ve detaylarına geçmesi.

### UI Yapısı

- Header: `Öğrenci Listesi`
- Search input: `Öğrenci ara...`
- Öğrenci listesi
- Altta primary buton: `+ Yeni Öğrenci Ekle`
- Bottom navigation

### Öğrenci Liste Kartı

Her satırda:

- Avatar
- Öğrenci adı
- Sınıf bilgisi
- Son ders zamanı
- Sağda başarı/progress skoru

Örnek:

```text
Ali Yılmaz
9. Sınıf
Son Ders: Bugün
92
```

Skor rengi:

- 85 ve üzeri: yeşil
- 70-84: turuncu
- 69 ve altı: kırmızı

### Component

`StudentListTile`

Parametreler:

```dart
final String name;
final String grade;
final String lastLessonText;
final int score;
final String avatarUrl;
final VoidCallback onTap;
```

---

## 10.5 Öğrenci Detay Ekranı

### Amaç

Öğretmenin belirli bir öğrencinin ders, ödev, ödeme ve genel performansını tek noktadan incelemesi.

### UI Yapısı

- Header: geri butonu + sağ menü
- Profil alanı
- Segment tab
- Metrik kartları
- Yakın zamandaki dersler
- Ders planlama butonu

### Profil Alanı

```text
Ali Yılmaz
9. Sınıf
```

Avatar büyük gösterilir.

### Segmentler

- Genel
- Dersler
- Ödevler
- Ödemeler

### Genel Metrikler

3 kolonlu kart:

1. Ders Saati
   - `36`
   - `Bu Ay`
2. Ortalama
   - `92`
   - `Başarı`
3. Devam Oranı
   - `%95`
   - `Harika`

### Yakın Zamandaki Dersler

Liste:

```text
Matematik
Bugün, 15:30
Gerçekleşti
```

```text
Fizik
Dün, 17:00
Gerçekleşti
```

```text
Kimya
2 gün önce, 16:00
Gerçekleşti
```

### Alt Aksiyon

```text
Ders Planla
```

---

## 10.6 Takvim Ekranı

### Amaç

Dersleri tarih bazlı göstermek ve yeni ders planlamayı kolaylaştırmak.

### UI Yapısı

- Geri butonu
- Aylık takvim
- Seçili gün koyu lacivert daire ile belirtilir
- Seçili güne ait ders listesi
- Alt buton: `+ Ders Planla`

### Takvim Alanı

Ay başlığı:

```text
Mayıs 2025
```

Gün başlıkları:

```text
Pzt Sal Çar Per Cum Cmt Paz
```

Seçili gün:

```text
15
```

### Ders Liste Kartları

Her satırda:

- Saat
- Ders adı + öğrenci adı
- Ders tipi
- Sağda renkli nokta

Örnek:

```text
15:30 Matematik - Ali Yılmaz Online
17:00 Fizik - Zeynep Demir Yüz Yüze
19:00 Kimya - Mehmet Kaya Online
```

### Flutter Paket Önerisi

- `table_calendar`

---

## 10.7 Ders Planla Ekranı

### Amaç

Öğretmenin yeni ders oluşturmasını sağlamak.

### Form Alanları

1. Öğrenci
2. Ders
3. Tarih
4. Saat aralığı
5. Ders şekli
6. Tekrar
7. Not

### UI Yapısı

Her alan kart/input formatında olmalıdır.

Örnek değerler:

```text
Öğrenci: Ali Yılmaz
Ders: Matematik
Tarih: 20 Mayıs 2025 Salı
Saat: 19:30 - 16:30
Ders Şekli: Online
Tekrar: Tekrar etme
Not: Bu derste polinomlar konusu işlenecek.
```

> Saat örneğinde tasarımda ters aralık görünüyor olabilir. Uygulamada başlangıç saati bitiş saatinden küçük olacak şekilde validasyon eklenmelidir.

### Validasyonlar

- Öğrenci zorunlu
- Ders zorunlu
- Tarih zorunlu
- Başlangıç saati bitiş saatinden önce olmalı
- Aynı öğretmenin aynı saatte başka dersi varsa uyarı gösterilmeli

### Alt Buton

```text
Kaydet
```

---

## 10.8 Ders Notu Ekranı

### Amaç

Öğretmenin işlenen ders ve verilen ödev hakkında not tutmasını sağlamak.

### UI Yapısı

- Header: `Ders Notu`
- Ders bilgi alanı
- Ders notu input alanı
- Ödev input alanı
- Dosya ekleme alanı
- Alt buton: `Kaydet`

### Ders Bilgi Alanı

```text
Matematik - Ali Yılmaz
20 Mayıs 2025, 15:30
```

### Input Alanları

Ders Notu:

```text
Polinomlar konusu işlendi. Çarpanlara ayırma üzerinde duruldu.
```

Ödev:

```text
Polinomlarla ilgili 20 soru çözümü.
```

Dosya:

```text
Polinomlar Notu.pdf
1.2 MB
```

### Flutter Notları

- Çok satırlı input için `TextFormField(maxLines: 5)`
- Dosya ekleme için `file_picker`
- Dosya kartında ikon, dosya adı, boyut ve sağ chevron kullanılmalı

---

## 10.9 Ödevler Ekranı

### Amaç

Öğretmenin verdiği ödevleri ve teslim durumlarını takip etmesini sağlamak.

### UI Yapısı

- Header: `Ödevlerim`
- Segment tab: Verilenler / Teslim Edilenler
- Ödev listesi
- Alt buton: `+ Yeni Ödev Ver`

### Ödev Kartı

Alanlar:

- Ödev başlığı
- Öğrenci adı
- Teslim tarihi
- Teslim sayısı / durum oranı
- Progress bar

Örnekler:

```text
Polinomlar - Soru Çözümü
Ali Yılmaz
Teslim: 22 Mayıs 2025
2 / 5 Teslim
```

```text
Fizik - Hareket Problemleri
Zeynep Demir
Teslim: 21 Mayıs 2025
4 / 5 Teslim
```

```text
Kimya - Kimyasal Tepkimeler
Mehmet Kaya
Teslim: 25 Mayıs 2025
1 / 5 Teslim
```

### Durum Renkleri

- Tamamlanmaya yakın: yeşil
- Orta seviye: turuncu
- Düşük teslim: kırmızı

---

## 10.10 Ödeme Takibi Ekranı

### Amaç

Öğretmenin öğrencilerden alacaklarını, tahsil edilen ve bekleyen ödemeleri takip etmesini sağlamak.

### UI Yapısı

- Header: `Ödeme Takibi`
- Sağda filtre ikonu
- Üstte 3 özet kart
- Segment tab
- Ödeme listesi
- Alt buton: `+ Ödeme Ekle`

### Özet Kartları

1. Toplam Alacak
   - `₺12.500`
2. Tahsil Edilen
   - `₺7.500`
3. Bekleyen
   - `₺5.000`

### Segmentler

- Tümü
- Tahsil Edilen
- Bekleyen

### Ödeme Liste Kartı

Alanlar:

- Öğrenci avatarı
- Öğrenci adı
- Ay bilgisi
- Tutar
- Durum

Örnek:

```text
Ali Yılmaz
Mayıs 2025
₺1.500
Bekliyor
```

```text
Zeynep Demir
Mayıs 2025
₺1.500
Ödendi
```

### Durum Renkleri

- Ödendi: yeşil
- Bekliyor: turuncu
- Gecikti: kırmızı

---

## 10.11 Öğrenci Paneli

### Amaç

Öğrencinin günlük çalışma, ders, ödev ve gelişim süreçlerini görmesini sağlamak.

### UI Yapısı

- Header: `Öğrenci Paneli`
- Karşılama metni
- Günlük streak kartı
- Bugünkü çalışma süresi kartı
- Hızlı işlemler
- Yaklaşan ders
- Bottom navigation

### Karşılama

```text
Merhaba, Ali 👋
```

### Streak Kartı

```text
Günlük Streak
12 Gün
```

### Çalışma Süresi Kartı

Koyu lacivert zeminli büyük kart:

```text
Bugünkü Çalışma Süren
02:15:30
Hedef: 02:00:00
```

Kartta progress bar ve circular progress bulunur.

### Hızlı İşlemler

- Çalışma Odası
- Test Çöz
- Ödevlerim
- Derslerim

### Yaklaşan Ders

```text
Matematik - Polinomlar
Bugün, 15:30
Online
```

---

## 10.12 Çalışma Odası

### Amaç

Öğrencinin odaklanarak çalışma süresini takip etmesini sağlamak.

### UI Yapısı

- Header: `Çalışma Odası`
- Ders/konu kartı
- Büyük sayaç kartı
- Durdur butonu
- Günlük hedef kartı
- Bottom navigation

### Ders/Konu Kartı

```text
Matematik
Polinomlar
```

### Sayaç

```text
01:15:24
Odaklanma Zamanı
```

### Aksiyon

Kırmızı buton:

```text
Durdur
```

### Günlük Hedef

```text
Günlük Hedef
03:00:00
%75
```

Progress bar ve circular percent indicator bulunur.

### Flutter Notları

- Sayaç için `Timer.periodic`
- Arka plana atıldığında süre takibi için lifecycle yönetimi gerekir
- Veriler local cache + backend sync ile korunmalıdır

---

## 10.13 Gelişim Analizi

### Amaç

Öğrencinin çalışma süreleri, test başarıları ve ders bazlı performansını görmesini sağlamak.

### UI Yapısı

- Header: `Gelişim Analizi`
- Segment tab: Haftalık / Aylık
- Derslere göre çalışma süresi donut chart
- Test başarı ortalaması kartı
- Line chart
- Bottom navigation

### Donut Chart

Ders dağılımı:

```text
Matematik %40
Fizik %25
Kimya %20
Türkçe %10
Diğer %5
```

### Test Başarı Kartı

```text
Test Başarı Ortalaması
%82
```

Altında haftalara göre line chart:

```text
1.Hafta - 2.Hafta - 3.Hafta - 4.Hafta
```

### Flutter Paket Önerisi

- `fl_chart`
- Donut chart için `PieChart`
- Trend için `LineChart`

---

## 10.14 Veli Paneli

### Amaç

Velinin çocuğunun çalışma, ders ve başarı durumunu sade biçimde takip etmesini sağlamak.

### UI Yapısı

- Header: `Veli Paneli`
- Karşılama metni
- Çocuk kartı
- Üç metrik kartı
- Haftalık özet bar chart
- Bottom navigation

### Karşılama

```text
Merhaba, Ayşe Hanım 👋
```

### Çocuk Kartı

```text
Çocuğunuz
Ali Yılmaz
```

### Metrikler

1. Bu Hafta Çalışma
   - `08:45`
2. Ders Saati
   - `03`
3. Ortalama Başarı
   - `%88`

### Haftalık Özet

Bar chart günlere göre çalışma süresini gösterir:

```text
Pzt, Sal, Çar, Per, Cum, Cmt, Paz
```

---

## 10.15 Veli Öğrenci Detay / Genel Bakış

### Amaç

Velinin çocuğunun performansını daha detaylı incelemesi.

### UI Yapısı

- Profil alanı
- Segment tab: Genel Bakış / Dersler / Ödevler / Ödemeler
- Çalışma süresi kartı
- Ders dağılımı chart

### Çalışma Süresi Kartı

```text
Çalışma Süresi (Bu Hafta)
08:45
Geçen Hafta: 06:30
%35 ↑
```

### Ders Dağılımı

Donut chart:

```text
Matematik 4s 30dk
Fizik 2s 30dk
Kimya 1s 45dk
```

---

## 10.16 Dersler Ekranı

### Amaç

Öğrenci veya veli tarafında yaklaşan/geçmiş dersleri görüntülemek.

### UI Yapısı

- Header: `Dersler`
- Segment tab: Yaklaşan / Geçmiş
- Ders listesi

### Ders Kartı

Alanlar:

- Ders adı
- Tarih ve saat
- Ders tipi badge: Online / Yüz Yüze
- Durum badge: Tamamlandı / Planlandı

Örnek:

```text
Matematik
15 Mayıs, 15:30
Online
Tamamlandı
```

```text
Fizik
14 Mayıs, 17:00
Yüz Yüze
Tamamlandı
```

---

## 10.17 Ödev Durumu Ekranı

### Amaç

Öğrencinin aktif ve tamamlanan ödevlerini takip etmesi.

### UI Yapısı

- Header: `Ödev Durumu`
- Segment tab: Aktif Ödevler / Tamamlananlar
- Ödev listesi

### Ödev Kartı

Alanlar:

- Ödev başlığı
- Veriliş tarihi
- Teslim tarihi
- Durum

Örnek:

```text
Polinomlar - Soru Çözümü
Veriliş: 15 Mayıs 2025
Teslim Tarihi: 22 Mayıs 2025
Devam Ediyor
```

```text
Fizik - Hareket Problemleri
Veriliş: 14 Mayıs 2025
Teslim Tarihi: 21 Mayıs 2025
Teslim Edildi
```

Durum renkleri:

- Devam Ediyor: mor/lacivert
- Teslim Edildi: yeşil
- Gecikti: kırmızı

---

## 10.18 Test Performansı Ekranı

### Amaç

Öğrencinin test sonuçlarını ve başarı trendini takip etmesi.

### UI Yapısı

- Header: `Test Performansı`
- Filtre dropdown: Aylık
- Ortalama başarı kartı
- Son testler listesi

### Ortalama Başarı Kartı

```text
Ortalama Başarı
%82
Geçen Ay: %76
%6 ↑
```

### Son Testler

```text
Matematik Deneme 3
12 Mayıs 2025
%85
```

```text
Fizik Deneme 2
10 Mayıs 2025
%80
```

```text
Kimya Deneme 1
8 Mayıs 2025
%78
```

---

## 10.19 Bildirimler Ekranı

### Amaç

Kullanıcıya ders, ödev, not ve ödeme hatırlatmalarını göstermek.

### UI Yapısı

- Header: `Bildirimler`
- Segment tab: Tümü / Okunmamış / Önemli
- Bildirim listesi

### Bildirim Kartı

Alanlar:

- Sol ikon
- Bildirim başlığı
- Açıklama
- Tarih/saat

Örnekler:

```text
Ders Hatırlatması
Matematik dersi 30 dakika sonra
15 Mayıs 2025, 15:00
```

```text
Ödev Hatırlatması
Polinomlar ödevi için son teslim tarihi yaklaşıyor.
15 Mayıs 2025, 10:30
```

```text
Yeni Not
Matematik ders notunuz girildi.
14 Mayıs 2025, 18:20
```

```text
Ödeme Hatırlatması
Mayıs ayı ödeme bilgilerinizi kontrol edin.
14 Mayıs 2025, 09:00
```

### Bildirim Tipleri

- Ders: mor/pembe ikon
- Ödev: yeşil ikon
- Not: mavi ikon
- Ödeme: kırmızı ikon

---

## 10.20 Profil Ekranı

### Amaç

Kullanıcının hesap ayarları, güvenlik, bildirim, yardım ve çıkış işlemlerine erişmesini sağlamak.

### UI Yapısı

- Header: `Profil`
- Avatar + kullanıcı adı + sınıf/rol
- Menü listesi
- Çıkış butonu
- Bottom navigation

### Profil Bilgisi

```text
Ali Yılmaz
9. Sınıf
```

### Menüler

- Kişisel Bilgiler
- Şifre Değiştir
- Bildirim Ayarları
- Gizlilik
- Yardım & Destek
- Çıkış Yap

### Çıkış Butonu

- Kırmızı ikon ve kırmızı metin
- Beyaz kart içinde veya ayrı danger area olarak gösterilebilir

---

# 11. Navigation Mimarisi

Flutter tarafında `go_router` önerilir.

```dart
final GoRouter router = GoRouter(
  initialLocation: '/welcome',
  routes: [
    GoRoute(path: '/welcome', builder: (_, __) => const WelcomePage()),
    GoRoute(path: '/role-selection', builder: (_, __) => const RoleSelectionPage()),

    GoRoute(path: '/teacher/home', builder: (_, __) => const TeacherDashboardPage()),
    GoRoute(path: '/teacher/students', builder: (_, __) => const StudentListPage()),
    GoRoute(path: '/teacher/student/:id', builder: (_, state) => StudentDetailPage(id: state.pathParameters['id']!)),
    GoRoute(path: '/teacher/calendar', builder: (_, __) => const CalendarPage()),
    GoRoute(path: '/teacher/lesson/create', builder: (_, __) => const LessonPlanPage()),
    GoRoute(path: '/teacher/lesson-note', builder: (_, __) => const LessonNotePage()),
    GoRoute(path: '/teacher/assignments', builder: (_, __) => const AssignmentsPage()),
    GoRoute(path: '/teacher/payments', builder: (_, __) => const PaymentsPage()),

    GoRoute(path: '/student/home', builder: (_, __) => const StudentDashboardPage()),
    GoRoute(path: '/student/study-room', builder: (_, __) => const StudyRoomPage()),
    GoRoute(path: '/student/analytics', builder: (_, __) => const AnalyticsPage()),
    GoRoute(path: '/student/lessons', builder: (_, __) => const LessonsPage()),
    GoRoute(path: '/student/assignments', builder: (_, __) => const AssignmentStatusPage()),
    GoRoute(path: '/student/test-performance', builder: (_, __) => const TestPerformancePage()),

    GoRoute(path: '/parent/home', builder: (_, __) => const ParentDashboardPage()),
    GoRoute(path: '/notifications', builder: (_, __) => const NotificationsPage()),
    GoRoute(path: '/profile', builder: (_, __) => const ProfilePage()),
  ],
);
```

---

# 12. State Management Önerisi

Bu uygulama orta-büyük ölçekli olacağı için `Bloc` önerilir.

Alternatifler:

- Riverpod
- Bloc
- Provider

Önerilen yapı:

```text
feature/
  presentation/
    pages/
    widgets/
    controllers/
  application/
    services/
  domain/
    entities/
    repositories/
  data/
    models/
    datasources/
    repositories/
```

Örnek:

```text
features/students/
  presentation/
    pages/student_list_page.dart
    pages/student_detail_page.dart
    widgets/student_list_tile.dart
  application/
    student_controller.dart
  domain/
    student.dart
    student_repository.dart
  data/
    student_model.dart
    student_remote_datasource.dart
    student_repository_impl.dart
```

---

# 13. Veri Modelleri

> ⚠️ **Not:** Bu modeller UI çizimine yönelik **basitleştirilmiş/idealize** örneklerdir; gerçek backend domain
> modelleriyle (alan adları, tipler) birebir aynı değildir (ör. burada `Student.averageScore` / `attendanceRate`
> var; gerçek `StudentProfile`'da yoktur). **Koddan doğrulanmış domain modelleri için** → ilgili modül
> dokümanları ([`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) vb.) ve
> [`modules/veri_modeli.md`](modules/veri_modeli.md) (ER şeması).

## 13.1 UserRole

```dart
enum UserRole {
  teacher,
  student,
  parent,
}
```

## 13.2 Student

```dart
class Student {
  final String id;
  final String fullName;
  final String grade;
  final String? avatarUrl;
  final int averageScore;
  final DateTime? lastLessonDate;
  final int attendanceRate;

  Student({
    required this.id,
    required this.fullName,
    required this.grade,
    this.avatarUrl,
    required this.averageScore,
    this.lastLessonDate,
    required this.attendanceRate,
  });
}
```

## 13.3 Lesson

```dart
class Lesson {
  final String id;
  final String studentId;
  final String studentName;
  final String subject;
  final String topic;
  final DateTime startTime;
  final DateTime endTime;
  final LessonType type;
  final LessonStatus status;
  final String? note;

  Lesson({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.subject,
    required this.topic,
    required this.startTime,
    required this.endTime,
    required this.type,
    required this.status,
    this.note,
  });
}

enum LessonType { online, faceToFace }
enum LessonStatus { planned, completed, cancelled }
```

## 13.4 Assignment

```dart
class Assignment {
  final String id;
  final String title;
  final String studentId;
  final String studentName;
  final DateTime assignedAt;
  final DateTime dueDate;
  final AssignmentStatus status;
  final int submittedCount;
  final int totalCount;

  Assignment({
    required this.id,
    required this.title,
    required this.studentId,
    required this.studentName,
    required this.assignedAt,
    required this.dueDate,
    required this.status,
    required this.submittedCount,
    required this.totalCount,
  });
}

enum AssignmentStatus { active, submitted, completed, late }
```

## 13.5 Payment

```dart
class Payment {
  final String id;
  final String studentId;
  final String studentName;
  final double amount;
  final String period;
  final PaymentStatus status;
  final DateTime? paidAt;

  Payment({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.amount,
    required this.period,
    required this.status,
    this.paidAt,
  });
}

enum PaymentStatus { paid, pending, overdue }
```

## 13.6 StudySession

```dart
class StudySession {
  final String id;
  final String studentId;
  final String subject;
  final String topic;
  final DateTime startedAt;
  final DateTime? endedAt;
  final Duration duration;

  StudySession({
    required this.id,
    required this.studentId,
    required this.subject,
    required this.topic,
    required this.startedAt,
    this.endedAt,
    required this.duration,
  });
}
```

---

# 14. Component Geliştirme Sırası

Flutter geliştirmesine başlamadan önce aşağıdaki ortak componentler hazırlanmalıdır:

1. `AppTheme`
2. `AppColors`
3. `AppTextStyles`
4. `AppSpacing`
5. `AppButton`
6. `AppCard`
7. `AppHeader`
8. `AppBottomNav`
9. `AppTextField`
10. `AppAvatar`
11. `AppBadge`
12. `AppSegmentedTab`
13. `MetricCard`
14. `LessonCard`
15. `StudentListTile`
16. `AssignmentTile`
17. `PaymentTile`
18. `NotificationTile`
19. `ProfileMenuTile`
20. `EmptyState`
21. `LoadingState`
22. `ErrorState`

---

# 15. Responsive ve Erişilebilirlik Kuralları

## 15.1 Responsive

- Küçük ekranlarda metin taşmaları `ellipsis` ile engellenmeli
- KPI kartlarında `Expanded` kullanılmalı
- Grafik alanları sabit yükseklik yerine oranlı tasarlanmalı
- Scroll edilebilir ekranlarda bottom button için `SafeArea` kullanılmalı

## 15.2 Erişilebilirlik

- Buton yüksekliği minimum `44 px`
- Tıklanabilir alan minimum `44x44 px`
- Renk tek başına durum göstergesi olmamalı; metin/badge de kullanılmalı
- Font scaling desteklenmeli
- Görseller için semantic label eklenmeli

---

# 16. Animasyon ve Mikro Etkileşimler

Tasarımı daha premium göstermek için hafif animasyonlar eklenebilir.

Öneriler:

- Sayfa geçişlerinde fade + slide
- Kart tıklamalarında hafif scale
- Progress bar dolumunda animasyon
- Circular progress animasyonu
- Liste itemları için staggered fade-in
- Form validasyonlarında shake yerine sade error text

Flutter paketleri:

- `flutter_animate`
- `animations`

---

# 17. Paket Önerileri

```yaml
dependencies:
  flutter:
    sdk: flutter
  go_router: ^14.0.0
  flutter_riverpod: ^2.5.0
  google_fonts: ^6.2.0
  flutter_svg: ^2.0.0
  table_calendar: ^3.1.0
  fl_chart: ^0.68.0
  percent_indicator: ^4.2.3
  intl: ^0.19.0
  file_picker: ^8.0.0
  cached_network_image: ^3.3.0
  flutter_animate: ^4.5.0
```

---

# 18. Uygulama Akışı

## 18.1 İlk Açılış

```text
Splash / Welcome
  -> Giriş Yap
  -> Kayıt Ol
  -> Hesap Türü Seçimi
```

## 18.2 Öğretmen Akışı

```text
Öğretmen Paneli
  -> Öğrenci Listesi
    -> Öğrenci Detay
      -> Ders Planla
      -> Ödevler
      -> Ödemeler
  -> Takvim
  -> Ders Planla
  -> Ders Notu
  -> Ödeme Takibi
```

## 18.3 Öğrenci Akışı

```text
Öğrenci Paneli
  -> Çalışma Odası
  -> Dersler
  -> Ödev Durumu
  -> Gelişim Analizi
  -> Test Performansı
  -> Profil
```

## 18.4 Veli Akışı

```text
Veli Paneli
  -> Öğrenci Detay
  -> Dersler
  -> Ödevler
  -> Ödemeler
  -> Bildirimler
  -> Profil
```

---

# 19. Backend API İhtiyaçları

> ⚠️ **Not:** Aşağıdaki rotalar, bu UI tasarımı çiziminde **idealize edilmiş / erken dönem** önerilerdir
> ve gerçek backend ile **birebir uyuşmaz** (ör. burada `/students`, gerçekte `/api/students/profiles`).
> **Gerçek, koddan doğrulanmış endpoint envanteri için** → [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) §4
> ve ilgili modül dokümanları. Yeni özellik geliştirirken o listeyi esas alın.

Bu UI tasarımını beslemek için önerilen endpointler:

```text
POST   /auth/login
POST   /auth/register
GET    /me
GET    /dashboard/teacher
GET    /dashboard/student
GET    /dashboard/parent

GET    /students
POST   /students
GET    /students/{id}
GET    /students/{id}/summary
GET    /students/{id}/lessons
GET    /students/{id}/assignments
GET    /students/{id}/payments

GET    /lessons
POST   /lessons
GET    /lessons/{id}
PUT    /lessons/{id}
DELETE /lessons/{id}
POST   /lessons/{id}/note

GET    /assignments
POST   /assignments
PUT    /assignments/{id}
POST   /assignments/{id}/submit

GET    /payments
POST   /payments
PUT    /payments/{id}/mark-paid

GET    /study-sessions
POST   /study-sessions/start
POST   /study-sessions/{id}/stop
GET    /analytics/study
GET    /analytics/tests

GET    /notifications
PUT    /notifications/{id}/read
GET    /profile
PUT    /profile
```

---

# 20. Önerilen Geliştirme Fazları

## Faz 1 - Temel UI ve Rol Akışı

- Welcome ekranı
- Hesap türü seçimi
- Auth layout
- App theme
- Ortak componentler
- Bottom navigation

## Faz 2 - Öğretmen MVP

- Öğretmen paneli
- Öğrenci listesi
- Öğrenci detay
- Ders planlama
- Ders notu
- Ödevler
- Ödeme takibi

## Faz 3 - Öğrenci MVP

- Öğrenci paneli
- Çalışma odası
- Dersler
- Ödev durumu
- Gelişim analizi
- Test performansı

## Faz 4 - Veli MVP

- Veli paneli
- Çocuk detay ekranı
- Bildirimler
- Profil

## Faz 5 - İyileştirme

- Animasyonlar
- Offline cache
- Push notification
- Grafik detayları
- Gelişmiş filtreleme
- Çoklu çocuk/öğrenci desteği

---

# 21. Önemli UI Kuralları

- Her ekran tek ana amaca hizmet etmeli
- Koyu lacivert sadece ana vurgu ve aksiyonlarda kullanılmalı
- Kart içlerinde çok fazla metin kullanılmamalı
- Sayılar büyük ve kolay okunur olmalı
- Öğretmen ekranlarında operasyon hızı öncelikli olmalı
- Öğrenci ekranlarında motivasyon ve ilerleme hissi öncelikli olmalı
- Veli ekranlarında sade özet ve anlaşılır metrikler öncelikli olmalı
- Aynı data farklı rollerde farklı detay seviyesinde gösterilmeli

---

# 22. Flutter Kodlama Standartları

## 22.1 Widget Kuralları

- Büyük ekranlar küçük widgetlara bölünmeli
- Sayfa dosyasında business logic olmamalı
- Her card ayrı widget olmalı
- Sabit stringler localization için ayrılmalı
- Renkler doğrudan kullanılmamalı, `AppColors` üzerinden çağrılmalı
- TextStyle doğrudan yazılmamalı, `AppTextStyles` kullanılmalı

## 22.2 Naming

```text
teacher_dashboard_page.dart
student_list_page.dart
student_detail_page.dart
lesson_plan_page.dart
assignment_tile.dart
payment_summary_card.dart
study_timer_card.dart
```

## 22.3 Reusable Widget Örneği

```dart
class SectionHeader extends StatelessWidget {
  final String title;
  final String? actionText;
  final VoidCallback? onActionTap;

  const SectionHeader({
    super.key,
    required this.title,
    this.actionText,
    this.onActionTap,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: AppTextStyles.title),
        if (actionText != null)
          GestureDetector(
            onTap: onActionTap,
            child: Text(
              actionText!,
              style: AppTextStyles.caption.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
      ],
    );
  }
}
```

---

# 23. Sonuç

Bu tasarım, EğitimÜssü uygulamasının Flutter ile modüler, sürdürülebilir ve rol bazlı bir mobil uygulama olarak geliştirilmesi için güçlü bir temel sunar. Tasarımın ana gücü; sade kart yapısı, net metrik gösterimi, rol bazlı navigation ve tekrar kullanılabilir component sistemidir.

Flutter geliştirmesinde öncelik, önce tasarım sistemi ve ortak widgetların hazırlanması; ardından öğretmen, öğrenci ve veli akışlarının feature bazlı geliştirilmesi olmalıdır.

---

> **Not:** Tab / Segment Control Widget'ın (bkz. §9.6 `AppSegmentedTab`) detaylı tasarım, renk ve Flutter implementasyon dokümanı ayrı dosyaya taşındı → [`tab_widget.md`](tab_widget.md).
