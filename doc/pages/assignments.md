---
title: "Ödev Listeleri — Öğretmen ve Öğrenci"
summary: "Öğretmen ödev listesi/durum filtresi/puanlama ve öğrenci ödev listesi/çözüm yükleme; cursor ile sonsuz kaydırma"
tags: [sayfa, assignments, odev, ogretmen, ogrenci]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/assignments/presentation/pages/assignments_page.dart
  - mobile/lib/features/assignments/presentation/pages/student_assignments_page.dart
  - mobile/lib/features/assignments/presentation/cubit/assignments_list_cubit.dart
  - mobile/lib/features/assignments/data/repositories/assignment_repository_impl.dart
updated: 2026-11-15
---

# Ödev listeleri (`/assignments`, `/student/assignments`)

Öğretmen kendi ödevlerini görür, yüklenen kayıtlar arasında Tümü/Bekleyen/Devam/Tamamlanan filtresi uygular ve onaylanmış ödeve0–100puan ile geri bildirim kaydeder. Özet sayıları yüklenen kayıtların durumudur; ekran bu kapsamı belirtir. Öğrenci kendi ödevlerinde öğretmen geri bildirimini/puanı görür, dosya yükler veya tamamlandı işaretler. Backend yetkisi zorunludur; profilId/UserId ayrımı (#267) P21'de çözülmeyi bekler.

## Veri ve devam yükleme (P19 Task 7)

`GET /api/assignments?teacherUserId=&studentId=&after=&limit=50` → `items/nextCursor`. Azalan sıra `(CreatedOnUtc,Id)`. Aşağı kaydırma devam sayfasını ekler, kısa ilk sayfa görünümü doldurur, son cursor null ise durur. Devam hatası listeyi/cursor'u korur ve tekrar denenir. İlk yükleme hatası tam hata görünümüdür. Yenileme ve başarılı yazma sonrası yeniden yükleme cursor'u sıfırlar; önceki isteğin gecikmiş yanıtı atılır. Kapanan cubit emit yapmaz. Öğretmen genel paneli tüm sayfaları okuyarak bekleyen toplamını korur.

İki ekranın sabit metinleri tr/en ARB'dedir. Öğretmen son tarihleri locale ile biçimlenir. Ortak [PagedListBoundary](../architecture/widgets.md#cursor-sayfası-sınırı-p19-task-7) istek eşiği/yükleme/tekrar düğmesini birleştirir.

## İlgili

[Modül sözleşmesi](../modules/m06_assignments.md) · [Ders sonrası takip formu](assignment_follow_up.md) · [UX kuralları](../architecture/ux_rules.md)

*Güncelleme: 2026-11-15 — P19 Task 7.*
