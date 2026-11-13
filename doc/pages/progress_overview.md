---
title: "Gelişim Ekranı"
summary: "Öğrencinin konu bazlı hâkimiyeti + dönemsel snapshot çizgi grafiği (M10-1/M10-2); aynı gövde öğretmen ve veli kapsamında da çalışır"
tags: [sayfa, progress, gelisim, grafik, m10]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/progress/presentation/pages/progress_overview_page.dart
  - mobile/lib/features/progress/presentation/cubit/progress_cubit.dart
  - mobile/lib/features/progress/presentation/widgets/progress_snapshot_chart.dart
  - mobile/lib/features/progress/data/repositories/progress_repository_impl.dart
updated: 2026-09-04
---

# Gelişim (`/student/progress?studentId=`)

> **Feature:** `progress` · **Dosya:** `mobile/lib/features/progress/presentation/pages/progress_overview_page.dart`
> **State:** `ProgressCubit` · **Veri:** 🟢 `/api/progress-tracking` · **Güncelleme:** 2026-09-04

## Amaç
Öğrencinin konu bazlı gelişimini (hâkimiyet dağılımı, eksik/güçlü konular) ve **dönemler boyunca
skor eğrisini** göstermek.

## Kapsamlar (aynı gövde, üç uç)

`ProgressOverviewPage` `scope` parametresiyle üç kimlikte çalışır. Yetki ve gizlilik **sunucudadır**;
istemci yalnız doğru ucu seçer.

| `scope` | Uç | Not |
|---------|-----|-----|
| `self` (varsayılan) | `GET /students/{studentId}/overview` + `/mastery` + `/snapshots` | Tam görünüm |
| `teacher` | `GET /teachers/{teacherUserId}/students/{studentId}/overview` | Bağlı öğretmen; gizlilik filtreli |
| `parent` | `GET /parents/{parentUserId}/children/{studentId}/overview` | Onaylı veli; gizlilik filtreli |

> ⚠️ **Snapshot serisi yalnız `self` kapsamında gelir.** `GET /students/{id}/snapshots` ucu öğrencinin
> kendisine ve Admin'e açıktır (`ProgressOwnershipQueryAuthorizer`); öğretmen/veli kapsamında istek
> hiç atılmaz ve çizgi grafiği gizlenir. Boş grafik çizmek yalancı-yeşil olurdu.

## İçerik
1. **Konu dağılımı kartı** — Uzman / Yeterli / Gelişen / Zayıf sayaçları + aktif hedef sayısı.
2. **Dönemsel gelişim** (M10-1) — `ProgressSnapshotChart`: ders başına bir çizgi, y ekseni 0–100
   hâkimiyet skoru, x ekseni dönem başlangıcı. Renkler `design_system.md` aksan token'larından
   deterministik sırayla atanır; altta ders lejandı durur.
3. **Çalışma analizi köprüsü** — yalnız `self` kapsamında; [`teacher_reports.md`](teacher_reports.md)
   ile aynı feature'daki `StudentAnalysisPage`'e götürür (haftalık süre + ders bazlı net + PDF rapor).
4. **Eksik konular / Güçlü konular / Tüm konular** — `TopicMastery` listeleri, trend ikonu + seviye rozeti.

## Davranış
- Yükleme `LoadingStateView`, hata `ErrorStateView` + yeniden dene, boş `EmptyStateView`.
- Boş durum metni kapsama göre değişir: `self`'te "çalışıp deneme girdikçe oluşur",
  öğretmen/veli kapsamında **"öğrenci paylaşımı kapatmış olabilir"** uyarısı eklenir — sunucu
  gizlilik kapalıyken 403 değil boş özet döndürdüğü için istemci iki durumu ayırt edemez.
- Aşağı çekip yenileme (`RefreshIndicator`) son kapsamı tekrarlar.
- Grafik geçişleri 250 ms / `Curves.easeOut` (`animations.md`).

## İlgili
- Modül: [`../modules/m10_progress_tracking.md`](../modules/m10_progress_tracking.md)
- Kardeş ekran: [`teacher_reports.md`](teacher_reports.md) · Rol: [`../roles/ogrenci.md`](../roles/ogrenci.md)

---

*Gelişim Ekranı | Güncelleme: 2026-09-04*
