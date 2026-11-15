---
title: "Öğrenci Detayı Ekranı"
summary: "Öğrenci profili + sekmeli görünüm (Genel/Dersler/Performans/Ödemeler); demo veriye düşebilen kısmi gerçek bağlantı"
tags: [sayfa, students, ogretmen]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/students/presentation/pages/student_detail_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-11-15
---

# Öğrenci Detayı (`/students/:studentId`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/student_detail_page.dart`
> **State:** Stateful (tab seçimi) + `StudentDetailCubit`/`StudentDetailState` · **Veri:** ⚠️ Demo'ya düşebilen gerçek bağlantı · **Güncelleme:** 2026-11-15

## Amaç
Bir öğrencinin profili + sekmeli görünüm: Genel / Dersler / Performans / Ödemeler.

## Veri / API
- ⚠️ Demo veri. Gerçek bağlantı: `GET /api/students/profiles/{studentId}` + ders/ödeme endpoint'leri.

## Ana bileşenler
- Avatarlı profil başlığı, tab switcher, sekme içerikleri (ders listesi, performans metrikleri, ödeme listesi).
- **Genel sekmesi (2026-09-04, P06/M03-1/M03-2):** `_SubjectTargetsCard` artık düzenlenebilir (branş ekle/sil) ve altına `_ParentLinkCard` (veli bağla) geldi.

## Branş ekle/sil (M03-2)
1. "Branş ekle" → alt sayfa (ders + hedef seviye) → `StudentDetailCubit.addSubject` → `POST /api/students/profiles/{id}/subjects`.
2. Satırdaki **×** → `removeSubject` → `DELETE .../subjects/{subjectId}`. Silme **sunucudaki branş kimliğiyle** çalışır; kimliği olmayan (eski gövdeden gelen) satırda silme düğmesi görünmez.
3. Aynı branş ikinci kez eklenirse sunucu `students.duplicate_subject` (409) döner; hata SnackBar ile gösterilir (sessiz yutma yok).

## Veli bağlama (M03-1)
1. "Veli bağla" → alt sayfa → `StudentDetailCubit.linkParent` → `POST /api/students/profiles/{id}/link-parent`.
2. Bağ **onay bekler** (`Pending`); ekran "Veli baglama talebi olusturuldu; onay bekliyor." mesajı verir ve profil hemen değişmez.
3. ⚠️ **Bilinen sınır:** Identity'de e-postayla kullanıcı arama ucu olmadığı için veli **kullanıcı kimliğiyle** giriliyor. Arama ucu gelince alan e-posta aramasına dönmeli.

## Aksiyonlar
- **Yeni Ders** (Genel sekmesi) ve **Yeni ders** (Dersler sekmesi başlığı): ortak `LessonFormSheet`'i **modal** açar — bu öğrenci tek elemanlı `students` listesi olarak verildiği için form öğrenciyi otomatik seçer (`_openLessonForm`). Sheet kendi `SchedulingCubit`'ini sağlar; başarılı kayıtta kapanır ve öğrenci detayı (`StudentDetailCubit.load`) tazelenir. (Önceden bu butonlar yalnızca `/scheduling`'e gidiyordu.)
- **Ödeme Ekle:** `/payments` ekranına gider.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.5 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)

## Para gösterimi ve yerelleştirme (P18 T4)

Genel ve Ödemeler sekmesindeki metrikler PaymentRecord int kuruşlarını para birimine göre ayrı toplar; örneğin TRY ve USD iki ayrı etiketle gösterilir. Ödeme kartı kendi ISO para birimi ve iki ondalıkla biçimlenir. Önceki tümünü TRY diye gösteren toplam kaldırıldı. Bu sayfanın bütün kullanıcı metinleri tr/en `finance_*` ARB anahtarlarından gelir; branş/veli formundaki eski ASCII yazımlar da aynı geçişte düzeltildi. Diğer öğrenci sayfaları ve StudentDetailCubit'in önceki mesajları bu kapsamda taşınmadı.

*Güncelleme: 2026-11-15 — P18 T4 mobil para ve idempotency.*
