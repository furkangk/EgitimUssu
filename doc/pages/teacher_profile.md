# Öğretmen Profili (`/teacher-profile`)

> **Feature:** `teacher_profile` · **Dosya:** `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart`
> **State:** `TeacherProfileCubit` / `TeacherProfileState` + Stateful · **Veri:** Gerçek API · **Güncelleme:** 2026-06-23

## Amaç
Öğretmen profili düzenleyici: kişisel bilgiler, öğretmen bilgileri (başlık, branş, eğitim, deneyim, saatlik ücret, ders şekli), biyografi, uzmanlık alanları, ders tercihleri, uygunluk slotları.

## State / API
- `TeacherProfileCubit` → `POST/PUT/GET /api/teachers/profiles[/{userId}]`.
- ⚠️ Güvenlik: `IsVerified` update yolunda client'tan yazılabiliyor (bkz. [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) Y1) — profil formu bunu göndermemeli.

## Ana bileşenler
- Profil fotoğrafı, metrik kutucukları, görünürlük toggle, modal bottom-sheet'li düzenleme alanları, uzmanlık chip'leri, uygunluk slotları (ekle/sil).

## İlgili
- Modül: [`../modules/m02_teachers.md`](../modules/m02_teachers.md) (M02)
