# Öğrenci Listesi (`/students`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/students_page.dart`
> **State:** `StudentsCubit` (BlocProvider.value) · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-06-25

## Amaç
Öğretmenin öğrencilerini arama/filtreyle listelemesi; ad, sınıf, aktif durumu.

## Veri / API
- `StudentsCubit.load(teacherUserId)` → `GET /api/students/profiles/by-teacher/{teacherUserId}`
- `StudentsCubit.addStudent(profile)` → `POST /api/students/profiles`
- Teacher userId ve fullName: `AuthCubit.state.session` üzerinden
- Offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

## State akışı
- `isLoading=true` → shimmer iskelet (5 kart)
- `errorMessage!=null && students.isEmpty` → hata kartı + "Tekrar Dene"
- `students.isEmpty` (başarılı ama boş) → `EmptyStateView`
- Yüklü liste → öğrenci kartları; client-side metin filtreleme (`_query`)
- `successMessage` / `errorMessage` → `SnackBar` (BlocConsumer listener)

## Ana bileşenler
- `_ShimmerList` — shimmer yükleme iskelet
- `_ErrorCard` — hata görünümü + retry
- `_StudentCard` — ad / sınıf / Aktif-Pasif rozeti / chevron, `onTap → /students/{id}`
- `_SearchField` — client-side `_query` filtresi
- `_AddStudentSheet` — "Manuel Ekle" / "Davet Gönder" sekme; pops `StudentProfile` → cubit.addStudent()
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.4 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)
