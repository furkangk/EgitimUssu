---
title: "Öğretmen Profili Ekranı"
summary: "Öğretmen profil düzenleyici (kişisel/mesleki bilgiler, uzmanlık, uygunluk slotları); gerçek /api/teachers/profiles backend'ine bağlı"
tags: [sayfa, teacher-profile, ogretmen]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-09-03
---

# Öğretmen Profili (`/teacher-profile`)

> **Feature:** `teacher_profile` · **Dosya:** `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart`
> **State:** `TeacherProfileCubit` / `TeacherProfileState` + Stateful · **Veri:** Gerçek API · **Güncelleme:** 2026-09-03

## Amaç
Öğretmen profili düzenleyici: kişisel bilgiler, öğretmen bilgileri (başlık, branş, eğitim, deneyim, saatlik ücret, ders şekli), biyografi, uzmanlık alanları, ders tercihleri, uygunluk slotları.

## State / API
- `TeacherProfileCubit` → `POST/PUT/GET /api/teachers/profiles[/{userId}]`.
- `toUpdatePayload()` artık `isVerified` göndermez (Y1 kapatıldı); backend de bu alanı update yolunda kabul etmez.
- **Profil fotoğrafı (2026-09-03, P04/M02-2/D-05):** `POST /api/teachers/profiles/{userId}/photo` (multipart, `image_picker` ile kamera/galeri) → `GET .../photo` baytları indirilip `Image.memory` ile gösterilir. Fotoğraf **profil gövdesinde gönderilmez**; `toCreatePayload()`/`toUpdatePayload()` artık `profilePhotoUrl` taşımaz.

## Ana bileşenler
- Profil fotoğrafı (yükleme sırasında `CircularProgressIndicator`; fotoğraf yoksa baş harfler), metrik kutucukları, görünürlük toggle, modal bottom-sheet'li düzenleme alanları, uzmanlık chip'leri, uygunluk slotları (ekle/sil).

## Fotoğraf akışı
1. Avatar üzerindeki kamera düğmesi → alt sayfa: "Kamera ile çek" / "Galeriden seç".
2. `ImagePicker` (maks. genişlik 1024, kalite %85) → seçilen dosya `TeacherRepository.uploadProfilePhoto` ile yüklenir.
3. Sunucu doğrular (10 MB + uzantı beyaz listesi) ve `ProfilePhotoKey`'i **kendisi** set eder.
4. Yükleme başarılıysa `downloadProfilePhoto` ile baytlar çekilip ekrana basılır; hata **SnackBar** ile bildirilir (sessiz yutma yok).

## İlgili
- Modül: [`../modules/m02_teachers.md`](../modules/m02_teachers.md) (M02)
