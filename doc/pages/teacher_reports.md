---
title: "Rapor Ekranları (Öğretmen aylık + Öğrenci analizi/PDF)"
summary: "M14 raporlama ekranları: öğretmen aylık ders/tahsilat özeti + boş zaman analizi; öğrenci haftalık çalışma, ders bazlı net ve PDF gelişim raporu"
tags: [sayfa, reporting, rapor, grafik, pdf, m14]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/reporting/presentation/pages/teacher_reports_page.dart
  - mobile/lib/features/reporting/presentation/pages/student_analysis_page.dart
  - mobile/lib/features/reporting/presentation/cubit/teacher_reports_cubit.dart
  - mobile/lib/features/reporting/presentation/cubit/student_report_cubit.dart
  - mobile/lib/features/reporting/presentation/widgets/report_charts.dart
  - mobile/lib/features/reporting/data/repositories/reporting_repository_impl.dart
  - mobile/lib/features/reporting/data/services/device_report_file_opener.dart
updated: 2026-09-04
---

# Raporlar

> **Feature:** `reporting` · **Veri:** 🟢 `/api/reporting` (event-beslemeli projeksiyonlar)
> **Güncelleme:** 2026-09-04

Bu doküman iki ekranı kapsar; ikisi de aynı feature'da ve aynı `ReportingRepository` üzerinde çalışır.

---

## 1. Öğretmen raporları (`/teacher/reports`)

> **Dosya:** `teacher_reports_page.dart` · **State:** `TeacherReportsCubit` · **Giriş:** `/more` → "Raporlar"

### İçerik
| Bölüm | Kaynak | Not |
|-------|--------|-----|
| Ay değiştirici | — | `TeacherReportsCubit.showPreviousMonth/showNextMonth`, yıl sınırını doğru geçer |
| Ders özeti | `GET /teachers/{id}/monthly` (M14-1) | Tamamlanan / iptal / aktif öğrenci |
| Tahsilat | aynı uç | Tahsil edilen, oran çubuğu, kalan tutar |
| Boş zamanların | `GET /teachers/{id}/free-slots` (M14-5) | **Her zaman bugünden itibaren 7 gün** — geçmiş bir ayın boş saatini göstermek planlanamaz olduğu için anlamsız |

### Davranış
- Projeksiyon henüz beslenmemişse (`isEmpty`) hata değil **"bu ay için kayıt yok"** kartı gösterilir.
- Ay değiştirme sırasında oklar pasifleşir (çift istek yok).

---

## 2. Öğrenci çalışma analizi + PDF rapor (`/student/reports?studentId=`)

> **Dosya:** `student_analysis_page.dart` · **State:** `StudentReportCubit`
> **Giriş:** Gelişim ekranındaki "Çalışma analizi ve PDF rapor" köprüsü ([`progress_overview.md`](progress_overview.md))

### İçerik
| Bölüm | Kaynak | Grafik |
|-------|--------|--------|
| Haftalık çalışma | `GET /students/{id}/study-analysis` (M14-2) | `WeeklyStudyBarChart` — son 12 hafta, dakika; dokununca oturum sayısı |
| Ders bazlı net | `GET /students/{id}/performance` (M14-3) | `SubjectNetChart` — dersin **en güncel** ayı, % net + delta oku |
| PDF gelişim raporu | `POST /students/{id}/report` → `GET /reports/{reportId}/file` (M14-4) | — |

### PDF akışı
1. "Rapor oluştur" → `POST .../report` (sunucuda QuestPDF ile üretilir, `IFileStorage`'a yazılır).
2. `GET /reports/{reportId}/file` ile **yetkili** indirme (dosya public URL ile sunulmaz).
3. `DeviceReportFileOpener` baytları geçici dizine yazar ve `open_filex` ile sistem görüntüleyicisinde açar.

Üç adım ayrı ayrı başarısız olabilir; kısmi başarı korunur (rapor üretildi ama açılamadıysa kayıt durumda kalır).

### Premium kilidi
`402` (üyelik yetersiz) **ve** `403` (paylaşım/yetki kapalı) `ReportingStatus.premiumRequired` durumuna düşer:
ekran hata balonu değil **kilit kartı** gösterir, grafikler görünmeye devam eder. Analiz serilerinin durumu
(`status`) ile rapor üretiminin durumu (`reportStatus`) bilinçli olarak ayrı taşınır.

### Gizlilik
Sunucu, öğrenci paylaşımı kapalıyken 403 değil **boş liste** döndürür (`StudentReportPrivacyGate`).
İstemci "veri yok" ile "paylaşım kapalı"yı ayırt edemediği için boş durum metni ikisini birden söyler.

---

## 3. Veli çocuk detayındaki gelişim bölümü

`parent_child_detail_page.dart` içindeki `_ChildProgressSection` aynı iki grafiği (`WeeklyStudyBarChart`,
`SubjectNetChart`) `StudentReportCubit` ile çizer. Yetki onaylı veli bağıyla, gizlilik öğrencinin ayarıyla
sunucuda uygulanır; kapalıysa "öğrenci veli paylaşımını kapatmış olabilir" metni görünür.

## İlgili
- Modül: [`../modules/m14_reporting.md`](../modules/m14_reporting.md) · [`../modules/m10_progress_tracking.md`](../modules/m10_progress_tracking.md)
- Rol: [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/veli.md`](../roles/veli.md)
- Giriş noktası: [`more.md`](more.md) · [`progress_overview.md`](progress_overview.md)

---

*Rapor Ekranları | Güncelleme: 2026-09-04*
