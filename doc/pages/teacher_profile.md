---
title: "Öğretmen Profili Ekranı"
summary: "Öğretmen profil düzenleyici (kişisel/mesleki bilgiler, uzmanlık, uygunluk slotları); gerçek /api/teachers/profiles backend'ine bağlı"
tags: [sayfa, teacher-profile, ogretmen]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart
  - mobile/lib/features/teacher_profile/data/repositories/teacher_repository_impl.dart
  - mobile/lib/features/teacher_profile/presentation/widgets/availability_editor.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-11-15
---

# Öğretmen Profili (`/teacher-profile`)

> **Feature:** `teacher_profile` · **Dosya:** `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart`
> **State:** `TeacherProfileCubit` / `TeacherProfileState` + Stateful · **Veri:** Gerçek API · **Güncelleme:** 2026-11-15

## Amaç
Öğretmen profili düzenleyici: kişisel bilgiler, öğretmen bilgileri (başlık, branş, eğitim, deneyim, saatlik ücret, ders şekli), biyografi, uzmanlık alanları, ders tercihleri, uygunluk slotları.

## State / API
- `TeacherProfileCubit` → `POST/PUT/GET /api/teachers/profiles[/{userId}]`.
- `toUpdatePayload()` artık `isVerified` göndermez (Y1 kapatıldı); backend de bu alanı update yolunda kabul etmez.
- **Uygunluk kaydetme (2026-09-04, P06/D-04/M02-5):** `TeacherProfileCubit.saveAvailability(slots)` → yüklü profili `copyWith(availabilitySlots:)` ile kopyalayıp `PUT /api/teachers/profiles/{userId}` gönderir; başarı mesajı "Uygunluk saatleri kaydedildi." Profil henüz yüklenmediyse istek **hiç gitmez**, hata mesajı basılır. Sunucu slot listesini baştan yazdığı için gövde profilin tamamını taşır.
- **Profil fotoğrafı (2026-09-03, P04/M02-2/D-05):** `POST /api/teachers/profiles/{userId}/photo` (multipart, `image_picker` ile kamera/galeri) → `GET .../photo` baytları indirilip `Image.memory` ile gösterilir. Fotoğraf **profil gövdesinde gönderilmez**; `toCreatePayload()`/`toUpdatePayload()` artık `profilePhotoUrl` taşımaz.

## Ana bileşenler
- Profil fotoğrafı (yükleme sırasında `CircularProgressIndicator`; fotoğraf yoksa baş harfler), görünürlük toggle, modal bottom-sheet'li düzenleme alanları, uzmanlık chip'leri, **uygunluk düzenleyici** (`AvailabilityEditor`).

## Uygunluk düzenleyici (D-04/M02-5, 2026-09-04)
`presentation/widgets/availability_editor.dart` — kontrollü (controlled) widget: listeyi sayfa tutar, her değişiklik `onChanged` ile yukarı verilir.

1. Haftanın **7 günü** ayrı kartlarda listelenir; slot yoksa "Uygun saat yok" yazar.
2. Gün başlığındaki **+** düğmesi alt sayfayı açar: başlangıç/bitiş saati (06:00–23:30, 30 dk adım) + Online / Yüz yüze anahtarları.
3. Doğrulama `availabilitySlotError` ile **alt sayfada** yapılır: `bitiş ≤ başlangıç` veya **aynı gün içinde kesişme** varsa sayfa kapanmaz, kırmızı hata kutusu gösterilir. Bitişik aralık (`18:00-20:00` + `20:00-21:00`) çakışma değildir.
4. Her slot **kendi** Online / Yüz yüze bayrağını taşır; eskiden profil geneli iki anahtardan türetiliyordu. "Ders tercihleri" bölümündeki anahtarlar artık profil geneli `lessonFormat` alanını sürer (en az biri açık kalmak zorundadır).
5. "Uygunluğu kaydet" → `saveAvailability`; kaydederken düğme pasifleşir.

⚠️ **Bilinen sınır:** PUT gövdesi `subjects`/`certificates` taşımadığı için sunucu bu koleksiyonları temizler (profilin tamamını gönderen her kaydetmede geçerli, uygunluk kaydetmeye özel değil). Mobil sözleşmenin bu alanları taşıması gerekiyor.

## Fotoğraf akışı
1. Avatar üzerindeki kamera düğmesi → alt sayfa: "Kamera ile çek" / "Galeriden seç".
2. `ImagePicker` (maks. genişlik 1024, kalite %85) → seçilen dosya `TeacherRepository.uploadProfilePhoto` ile yüklenir.
3. Sunucu doğrular (10 MB + uzantı beyaz listesi) ve `ProfilePhotoKey`'i **kendisi** set eder.
4. Yükleme başarılıysa `downloadProfilePhoto` ile baytlar çekilip ekrana basılır; hata **SnackBar** ile bildirilir (sessiz yutma yok).

## İlgili
- Modül: [`../modules/m02_teachers.md`](../modules/m02_teachers.md) (M02)
- Widget kataloğu: [`../architecture/widgets.md`](../architecture/widgets.md) §3.2

## P20 T1 — sahte profil temizliği

Başlangıçta ad, uzmanlık ve uygunluk örneği üretilmez. İlk gerçek profil yüklenene kadar yükleme görünümü, başarısızlıkta hata ve tekrar deneme gösterilir; düzenleyici yalnız gerçek profil geldikten sonra açılır. Sabit 4.8 puanı, 126 yorum ve 18 aktif öğrenci kutuları kaldırılmıştır; gerçek yorum/sayaç kaynağı gelene kadar gösterilmez. Ekranın sabit metinleri tr/en ARB’dedir; `lessonFormat` içeride API kodu olarak korunur, gösterim etiketi UI’da çevrilir.

P20 T1 inceleme düzeltmesi: `TeacherRepositoryImpl.getProfile` 404 yanıtını normal modda aynen hata olarak iletir; profil yokluğu demo öğretmen üretmez. Demo yalnız `AppConfig.isMockFallbackEnabled("teacher_profile")` açıkken mümkündür; production/beta bu bayrağı engeller. Başarılı gerçek profil yanıtı korunur, yeni profil oluşturma akışı bu göreve eklenmez.
