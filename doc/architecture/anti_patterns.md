---
title: "Anti-Pattern'lar — Flutter"
summary: "Projede yapılmaması gereken UI/UX ve kod kalıpları; her madde neden yanlış olduğu ve doğrusu açıklamasıyla gelir"
tags: [mimari, anti-pattern, flutter]
authority: derived
updated: 2026-06-27
---

# 🚫 Anti-Pattern'lar — EğitimÜssü Flutter

> **Kapsam:** Bu projede **yapılmaması gereken** UI/UX ve kod kalıpları. Her madde "neden yanlış" ve "doğrusu nedir" açıklamasıyla gelir.
>
> **Güncelleme:** 2026-06-27

---

## 1. Renk & Stil Anti-Pattern'ları

### ❌ Sabit renk değeri yazmak
```dart
// YANLIŞ
Container(color: Color(0xFF082B4F))
Text('başlık', style: TextStyle(color: Color(0xFF111827)))
```
```dart
// DOĞRU
Container(color: AppColors.primary)
Text('başlık', style: AppTextStyles.body)
```
**Neden:** Token değiştiğinde tüm sabit yazılan yerler bozulur; `AppColors` tek noktadan güncellenir.

---

### ❌ Rengi tek başına durum göstergesi olarak kullanmak
```dart
// YANLIŞ — renk körü kullanıcılar ayırt edemez
Container(color: isActive ? Colors.green : Colors.red)
```
```dart
// DOĞRU
Row(children: [
  Container(color: isActive ? AppColors.accentGreen : AppColors.accentRed),
  Text(isActive ? 'Aktif' : 'Pasif'),
])
```

---

### ❌ Her yerde farklı spacing değeri kullanmak
```dart
// YANLIŞ
Padding(padding: EdgeInsets.all(13))
SizedBox(height: 11)
```
```dart
// DOĞRU — AppSpacing token'larını kullan
Padding(padding: EdgeInsets.all(AppSpacing.md))   // 12
SizedBox(height: AppSpacing.sm)                    // 8
```

---

## 2. Widget Anti-Pattern'ları

### ❌ Shared widget'ı yeniden yazmak
```dart
// YANLIŞ — zaten AppPrimaryButton var
ElevatedButton(
  style: ElevatedButton.styleFrom(backgroundColor: Color(0xFF082B4F)),
  child: Text('Kaydet'),
  onPressed: () {},
)
```
```dart
// DOĞRU
AppPrimaryButton(label: 'Kaydet', onPressed: _save)
```

---

### ❌ setState ile global/sayfa state'i yönetmek
```dart
// YANLIŞ
class _MyPageState extends State<MyPage> {
  List<Student> students = [];
  bool isLoading = false;
  void loadStudents() { setState(() { isLoading = true; ... }); }
}
```
```dart
// DOĞRU — Cubit kullan
class StudentListCubit extends Cubit<StudentListState> { ... }
// Widget'ta BlocBuilder ile dinle
```

---

### ❌ Scaffold arka planını beyaz bırakmak
```dart
// YANLIŞ
Scaffold(body: ...)   // varsayılan beyaz
```
```dart
// DOĞRU
Scaffold(
  backgroundColor: AppColors.background,  // #F7F9FC
  body: ...
)
```

---

### ❌ Loading/Error/Empty için özel widget yazmak
```dart
// YANLIŞ
if (isLoading) Center(child: CircularProgressIndicator())
if (hasError) Text('Hata oluştu')
if (isEmpty) Text('Veri yok')
```
```dart
// DOĞRU
if (isLoading) const LoadingStateView()
if (hasError) ErrorStateView(message: '...', onRetry: _load)
if (isEmpty) const EmptyStateView(title: '...', subtitle: '...')
```

---

### ❌ Dokunma hedefini küçük bırakmak
```dart
// YANLIŞ — 24px ikon tek başına tıklanamaz
IconButton(iconSize: 16, onPressed: ...)
```
```dart
// DOĞRU — minimum 48x48 dokunma alanı
IconButton(
  iconSize: 24,
  padding: EdgeInsets.all(12),
  onPressed: ...
)
```

---

## 3. Navigasyon Anti-Pattern'ları

### ❌ Navigator.push ile doğrudan sayfaya gitmek
```dart
// YANLIŞ
Navigator.push(context, MaterialPageRoute(builder: (_) => DetailPage()))
```
```dart
// DOĞRU — go_router kullan
context.push('/students/detail/$id')
```

---

### ❌ 3 seviyeyi aşan stack derinliği
```
Ana → Liste → Detay → Alt Detay → Düzenleme  ← YANLIŞ
Ana → Liste → Detay (düzenleme modal/sheet'te)  ← DOĞRU
```

---

## 4. Form Anti-Pattern'ları

### ❌ Yazarken validasyon tetiklemek
```dart
// YANLIŞ — her tuş vuruşunda hata göstermek sinir bozucudur
onChanged: (v) => setState(() => _error = validate(v))
```
```dart
// DOĞRU — sadece submit'te veya focus kaybında
onFieldSubmitted: (_) => _formKey.currentState?.validate()
```

---

### ❌ Başarı ve hata'yı aynı anda göstermek
```dart
// YANLIŞ
ScaffoldMessenger.of(context).showSnackBar(successSnack);
showDialog(context, errorDialog);
```
— Her işlem için tek, net bir geribildirim yeterlidir.

---

## 5. Performans Anti-Pattern'ları

### ❌ ListView içinde Column/ListView
```dart
// YANLIŞ — unbounded height hatası ve performans sorunu
Column(children: [
  ListView(children: [...])  // hata verir
])
```
```dart
// DOĞRU
CustomScrollView(slivers: [
  SliverList(delegate: SliverChildBuilderDelegate(...))
])
// veya
Expanded(child: ListView.builder(...))
```

---

### ❌ build() içinde ağır işlem yapmak
```dart
// YANLIŞ — her build'de hesaplanır
Widget build(BuildContext context) {
  final sorted = students.sortedBy((s) => s.name);  // her frame'de
  ...
}
```
```dart
// DOĞRU — Cubit state'inde veya initState'te hesapla
```

---

### ❌ const kullanmayı ihmal etmek
```dart
// YANLIŞ
Text('Başlık', style: TextStyle(fontSize: 18))
```
```dart
// DOĞRU
const Text('Başlık')
AppTextStyles.h3  // const token
```

---

*Anti-Pattern'lar | Güncelleme: 2026-06-27*
