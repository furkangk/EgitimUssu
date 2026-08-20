---
title: "Öğrenci Listesi Ekranı"
summary: "Öğretmenin öğrencilerini arama/filtreyle listelediği ekran; StudentsCubit üzerinden gerçek /api/students backend'ine bağlı"
tags: [sayfa, students, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/students/presentation/pages/students_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-08-20
---

# Öğrenci Listesi (`/students`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/students_page.dart`
> **State:** `StudentsCubit` (BlocProvider.value) · **Veri:** 🟡 Gerçek API + demo fallback · **Güncelleme:** 2026-08-20

## Amaç
Öğretmenin öğrencilerini arama/filtreyle listelemesi; ad, sınıf, aktif durumu.

## Veri / API
- `StudentsCubit.load(teacherUserId)` → `GET /api/students/profiles/by-teacher/{teacherUserId}`
- `StudentsCubit.addStudent(profile)` → `POST /api/students/profiles`
- Teacher userId ve fullName: `AuthCubit.state.session` üzerinden
- 🟡 Gerçek API (yukarıdaki uçlar) çağrılır; erişilemezse offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

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
- `_AddStudentSheet` — **premium tam ekran** ekleme formu (`showModalBottomSheet(isScrollControlled, backgroundColor: transparent)`; durum çubuğunun altından ekranın tamamını kaplar, üst köşeler 28 radius). İç `Scaffold` klavyeyi yönetir. Bileşenler: `_PremiumSheetHeader` (primary gradient zemin + sağ üstte **kapatma (X)** butonu + ikon rozeti/başlık), `_StudentAddTabs` ("Manuel Ekle" / "Davet Gönder"), kaydırılan form ve alta **sabit CTA** (`_SubmitBar` — `FilledButton.icon`, sekmeye göre "Öğrenciyi Kaydet" / "Davet Gönder"). Manuel form `StudentProfile` pop'lar → `cubit.addStudent()`. (Önceki sürüm 0.94 yükseklikli, tutaçlı, butonu form içinde olan sheet idi.)
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.4 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)
