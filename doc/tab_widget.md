# EğitimÜssü Tab Widget Tasarım Dokümanı

> Bu doküman, ortak widget kataloğundaki ([`architecture/widgets.md`](architecture/widgets.md) → `AppSegmentedTab`) **Tab / Segment
> Control Widget** bileşeninin detaylı tasarım, kullanım ve Flutter implementasyon kurallarını açıklar.
> Renkler ana palet ([`architecture/design_system.md`](architecture/design_system.md) §3 `AppColors`) ile aynıdır; aşağıda yalnızca tab'a özel ek token'lar (ör. `tabBackground`) tanımlanır.

---

## 1. Widget Amacı

Tab widget; aynı ekran içinde ilişkili içerikleri bölümlere ayırmak için kullanılır.

EğitimÜssü tasarımında tab yapısı özellikle şu ekranlarda kullanılır:

- Öğretmen Derslerim
- Öğrenci Detay
- Ödevlerim
- Ödeme Takibi
- Gelişim Analizi
- Bildirimler
- Test Performansı
- Ders Detayı

Amaç, kullanıcıyı yeni sayfaya yönlendirmeden aynı bağlam içinde içerik değiştirmektir.

---

## 2. Genel Tasarım Dili

EğitimÜssü tab widget tasarımı:

- Minimal
- Yuvarlatılmış
- Kart yapısına uyumlu
- Lacivert aktif durum
- Açık gri pasif durum
- Yumuşak geçişli
- Mobil kullanım için büyük dokunma alanlı

olmalıdır.

---

## 3. Görsel Özellikler

### 3.1 Container

Tab widget genellikle açık gri bir kapsayıcı içinde yer alır.

```dart
height: 38
padding: EdgeInsets.all(4)
borderRadius: BorderRadius.circular(12)
backgroundColor: Color(0xFFF3F5F8)
```

### 3.2 Aktif Tab

Aktif tab, EğitimÜssü primary rengi olan koyu lacivert ile gösterilir.

```dart
backgroundColor: Color(0xFF082B4F)
textColor: Colors.white
fontWeight: FontWeight.w600
borderRadius: BorderRadius.circular(10)
```

### 3.3 Pasif Tab

Pasif tab sade kalmalıdır.

```dart
backgroundColor: Colors.transparent
textColor: Color(0xFF7A8494)
fontWeight: FontWeight.w500
```

---

## 4. Renk Paleti

> Aşağıdaki değerler ana palet ([`architecture/design_system.md`](architecture/design_system.md) §3) ile aynıdır; `tabBackground` tab'a özgü ek token'dır.

```dart
class AppColors {
  static const primary = Color(0xFF082B4F);
  static const tabBackground = Color(0xFFF3F5F8);
  static const textPrimary = Color(0xFF111827);
  static const textSecondary = Color(0xFF7A8494);
  static const border = Color(0xFFE5EAF0);
  static const white = Color(0xFFFFFFFF);
}
```

---

## 5. Kullanım Varyasyonları

### 5.1 İki Sekmeli Tab

Örnek ekranlar:

- Gelişim Analizi: Haftalık / Aylık
- Ders Ekle: Tek Ders / Tekrarlı Ders
- Ödev Durumu: Aktif Ödevler / Tamamlananlar

```text
[ Haftalık ] [ Aylık ]
```

### 5.2 Üç Sekmeli Tab

Örnek ekranlar:

- Derslerim: Yaklaşan / Geçmiş / İptal Edilen
- Ödeme Geçmişi: Tümü / Ödendi / Beklemede
- Bildirimler: Tümü / Okunmamış / Önemli

```text
[ Yaklaşan ] [ Geçmiş ] [ İptal Edilen ]
```

### 5.3 Dört Sekmeli Tab

Örnek ekranlar:

- Öğrenci Detay: Genel / Dersler / Ödevler / Ödemeler
- Ders Detayı: Ders Notu / Ödevler / Ödeme / Diğer

```text
[ Genel ] [ Dersler ] [ Ödevler ] [ Ödemeler ]
```

---

## 6. Boyut Kuralları

| Özellik                | Değer |
| ---------------------- | ----: |
| Container yüksekliği   | 38 px |
| İç padding             |  4 px |
| Tab border radius      | 10 px |
| Container radius       | 12 px |
| Font size              | 12 px |
| Font weight aktif      |   600 |
| Font weight pasif      |   500 |
| Minimum tab yüksekliği | 30 px |

---

## 7. Flutter Widget Örneği

```dart
import 'package:flutter/material.dart';

class EgitimUssuTabBar extends StatelessWidget {
  final List<String> tabs;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  const EgitimUssuTabBar({
    super.key,
    required this.tabs,
    required this.selectedIndex,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 38,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: const Color(0xFFF3F5F8),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: List.generate(tabs.length, (index) {
          final bool isSelected = selectedIndex == index;

          return Expanded(
            child: GestureDetector(
              onTap: () => onChanged(index),
              behavior: HitTestBehavior.opaque,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: isSelected
                      ? const Color(0xFF082B4F)
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  tabs[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight:
                        isSelected ? FontWeight.w600 : FontWeight.w500,
                    color: isSelected
                        ? Colors.white
                        : const Color(0xFF7A8494),
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}
```

---

## 8. Kullanım Örneği

```dart
class TeacherLessonsPage extends StatefulWidget {
  const TeacherLessonsPage({super.key});

  @override
  State<TeacherLessonsPage> createState() => _TeacherLessonsPageState();
}

class _TeacherLessonsPageState extends State<TeacherLessonsPage> {
  int selectedTab = 0;

  final tabs = [
    'Yaklaşan',
    'Geçmiş',
    'İptal Edilen',
  ];

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        EgitimUssuTabBar(
          tabs: tabs,
          selectedIndex: selectedTab,
          onChanged: (index) {
            setState(() {
              selectedTab = index;
            });
          },
        ),
        const SizedBox(height: 16),
        Expanded(
          child: _buildContent(),
        ),
      ],
    );
  }

  Widget _buildContent() {
    switch (selectedTab) {
      case 0:
        return const Center(child: Text('Yaklaşan dersler'));
      case 1:
        return const Center(child: Text('Geçmiş dersler'));
      case 2:
        return const Center(child: Text('İptal edilen dersler'));
      default:
        return const SizedBox.shrink();
    }
  }
}
```

---

## 9. Öğretmenin Dersler Sayfasında Kullanım

Öğretmenin **Derslerim** ekranında tab widget şu şekilde kullanılmalıdır:

```text
Yaklaşan | Geçmiş | İptal Edilen
```

### Yaklaşan

Henüz gerçekleşmemiş dersleri listeler.

Gösterilecek bilgiler:

- Ders adı
- Öğrenci adı
- Tarih
- Saat aralığı
- Ders şekli
- Online / Yüz yüze etiketi

### Geçmiş

Tamamlanmış dersleri listeler.

Gösterilecek bilgiler:

- Ders adı
- Öğrenci
- Tarih
- Süre
- Ders notu durumu
- Ödev verildi mi?

### İptal Edilen

İptal edilen dersleri listeler.

Gösterilecek bilgiler:

- Ders adı
- Öğrenci
- Planlanan tarih
- İptal nedeni
- Yeniden planla aksiyonu

---

## 10. UX Kuralları

- Tab sayısı 4'ü geçmemelidir.
- Uzun tab isimleri mümkünse kısaltılmalıdır.
- Aktif tab her zaman net kontrast vermelidir.
- Tab değişiminde sayfa yenileniyormuş hissi verilmemelidir.
- İçerik aynı ekranda yumuşak şekilde değişmelidir.
- Tab widget ekranın üst kısmında, arama veya filtre varsa onların hemen altında kullanılmalıdır.
- Tab içinde ikon kullanılacaksa yalnızca çok gerekli durumlarda kullanılmalıdır.

---

## 11. Yanlış Kullanımlar

Aşağıdaki kullanımlardan kaçınılmalıdır:

```text
[ Yaklaşan Ders Programlarım ] [ Daha Önce Yapılmış Dersler ] [ İptal Edilmiş Ders Kayıtları ]
```

Bunun yerine:

```text
[ Yaklaşan ] [ Geçmiş ] [ İptal Edilen ]
```

kullanılmalıdır.

---

## 12. İleri Seviye: Badge Destekli Tab

Bazı ekranlarda tab içinde sayaç gösterilebilir.

Örnek:

```text
Aktif Ödevler 3
Tamamlananlar 12
```

Flutter tarafında tab label yanında küçük badge kullanılabilir.

```dart
class EgitimUssuTabItem {
  final String label;
  final int? badgeCount;

  const EgitimUssuTabItem({
    required this.label,
    this.badgeCount,
  });
}
```

Badge rengi:

```dart
backgroundColor: Color(0xFFEAF1FF)
textColor: Color(0xFF082B4F)
```

---

## 13. Component API Önerisi

```dart
EgitimUssuTabBar(
  tabs: const ['Yaklaşan', 'Geçmiş', 'İptal Edilen'],
  selectedIndex: selectedIndex,
  onChanged: (index) {},
)
```

Daha gelişmiş kullanım:

```dart
EgitimUssuSegmentedTabs(
  items: const [
    EgitimUssuTabItem(label: 'Aktif', badgeCount: 4),
    EgitimUssuTabItem(label: 'Tamamlanan', badgeCount: 12),
  ],
  selectedIndex: selectedIndex,
  onChanged: (index) {},
)
```

---

## 14. Tasarım Özeti

EğitimÜssü tab widget, uygulamanın genel sade ve profesyonel dilini destekleyen küçük ama kritik bir navigasyon bileşenidir.

Temel karakteri:

- Lacivert aktif alan
- Açık gri container
- Yuvarlak köşeler
- Kompakt yükseklik
- Yumuşak geçiş
- Mobil öncelikli kullanım

Bu widget tüm öğretmen, öğrenci ve veli ekranlarında ortak component olarak kullanılmalıdır.
