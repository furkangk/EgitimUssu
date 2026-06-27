# ♿ Erişilebilirlik (Accessibility) — EğitimÜssü Flutter

> **Kapsam:** Tüm ekranlarda uygulanacak erişilebilirlik kuralları — ekran okuyucu, renk kontrastı,
> dokunma hedefleri, yazı boyutu ölçekleme.
>
> **Güncelleme:** 2026-06-27

---

## 1. Temel İlkeler

EğitimÜssü öğretmen, öğrenci ve veli kullanır. Kullanıcı kitlesi geniş yaş aralığını kapsar:
- Veliler: 35-55 yaş — yazı boyutu hassasiyeti
- Öğrenciler: 10-18 yaş — görsel netlik
- Öğretmenler: 25-55 yaş — hız + netlik

WCAG 2.1 AA seviyesi hedeflenir.

---

## 2. Renk Kontrastı

| Kombinasyon | Kontrast oranı | Durum |
|-------------|---------------|-------|
| `textPrimary (#111827)` / `background (#F7F9FC)` | ~18:1 | ✅ AAA |
| `textSecondary (#6B7280)` / `background (#F7F9FC)` | ~5:1 | ✅ AA |
| `white` / `primary (#082B4F)` | ~13:1 | ✅ AAA |
| `white` / `accentGreen (#20B486)` | ~3.2:1 | ⚠️ Büyük metin/ikon için OK |
| `white` / `accentOrange (#FFA726)` | ~2.1:1 | ❌ Tek başına kullanma |

**Kural:** `accentOrange` ve `accentRed` arka plan olarak kullanılıyorsa metin rengi her zaman `textPrimary (#111827)` olmalı, beyaz değil.

```dart
// YANLIŞ
Container(
  color: AppColors.accentOrange,
  child: Text('Uyarı', style: TextStyle(color: Colors.white)),  // kontrast yetersiz
)

// DOĞRU
Container(
  color: AppColors.accentOrange,
  child: Text('Uyarı', style: TextStyle(color: AppColors.textPrimary)),
)
```

---

## 3. Dokunma Hedefleri (Touch Targets)

Minimum dokunma alanı: **48×48 dp** (Material guideline).

```dart
// YANLIŞ — ikon çok küçük
IconButton(iconSize: 16, onPressed: ...)

// DOĞRU
IconButton(
  iconSize: 24,
  padding: EdgeInsets.all(12),  // toplam: 48x48
  onPressed: ...
)

// Liste tile için minimum yükseklik
ListTile(minVerticalPadding: 12)   // ~56dp toplam
```

---

## 4. Ekran Okuyucu (Semantics)

Flutter'da ekran okuyucu Android TalkBack / iOS VoiceOver'dır.

### Temel kullanım
```dart
Semantics(
  label: 'Ahmet Yılmaz öğrencisi, puan 87, Matematik',
  child: StudentListTile(...),
)
```

### Dekoratif öğeleri gizle
```dart
// İkon salt görsellik içinse
ExcludeSemantics(
  child: Icon(Icons.star_rounded),
)
```

### Buton açıklaması
```dart
Semantics(
  button: true,
  label: 'Ders ekle',
  child: AppPrimaryButton(label: '+', onPressed: ...),
)
```

### Görüntü açıklaması
```dart
Image.network(url, semanticLabel: 'Öğrenci profil fotoğrafı')
```

### Canlı bölge (dinamik güncelleme)
```dart
Semantics(
  liveRegion: true,    // ekran okuyucu değişimi seslendirir
  child: Text(statusMessage),
)
```

---

## 5. Yazı Boyutu Ölçekleme (Text Scale)

Kullanıcı sistem font boyutunu büyütebilir. Layout bozulmamalı.

```dart
// YANLIŞ — sabit piksel, ölçeklenmez
Text('Başlık', style: TextStyle(fontSize: 18))

// DOĞRU — AppTextStyles token'ı kullan (sp birimi)
Text('Başlık', style: AppTextStyles.h3)
```

**Büyük ölçek testleri:**
- Sistem font boyutu 1.5× yapıldığında kartlar taşmamalı.
- `overflow: TextOverflow.ellipsis` uzun metinlerde kullan.
- Sabit yükseklikli container'lardan kaçın; `minHeight` tercih et.

```dart
// Uzun metin için
Text(
  studentName,
  maxLines: 1,
  overflow: TextOverflow.ellipsis,
  style: AppTextStyles.title,
)
```

---

## 6. Odak (Focus) Yönetimi

```dart
// Form alanları arasında sıralı geçiş
TextField(
  textInputAction: TextInputAction.next,
  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
)

// Son alan
TextField(
  textInputAction: TextInputAction.done,
  onFieldSubmitted: (_) => _submit(),
)
```

- Modal/dialog açıldığında focus otomatik modal'a taşınır — Flutter varsayılan.
- Modal kapandığında focus tetikleyen öğeye döner.

---

## 7. Hareket Duyarlılığı (Reduced Motion)

```dart
// Sistem "hareketi azalt" tercihini kontrol et
final reduceMotion = MediaQuery.of(context).disableAnimations;

AnimatedContainer(
  duration: reduceMotion ? Duration.zero : kDurationNormal,
  ...
)
```

---

## 8. Erişilebilirlik Kontrol Listesi

Yeni ekran teslim edilmeden önce:

- [ ] Tüm interaktif öğeler ≥48×48 dp dokunma alanına sahip
- [ ] Metin/arka plan kontrast oranı AA standardını karşılıyor
- [ ] Renk tek başına durum göstergesi değil (metin/ikon eşliği var)
- [ ] Anlamlı `Semantics.label` eklendi (ikon butonlar, görüntüler)
- [ ] Dekoratif öğeler `ExcludeSemantics` ile gizlendi
- [ ] Font boyutu 1.5× yapıldığında layout bozulmuyor
- [ ] Form alanları `textInputAction` ile sıralı geçiş yapıyor
- [ ] Dinamik içerik değişimlerinde `liveRegion: true` var

---

*Erişilebilirlik | Güncelleme: 2026-06-27*
