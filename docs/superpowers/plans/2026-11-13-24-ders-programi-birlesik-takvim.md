# P24 — Öğrenci Ders Programı ve Birleşik Takvim — Hafif Plan 🅻

> **Hafif plan:** `/devam` önce **Task 0**'ı yürütür.

**Goal:** Öğrenci "planlı çalışma"yı gerçekten yapabilsin: okul ders programı, özel dersleri ve kendi çalışma planı tek takvimde görünsün, boş saatleri ve çalışma önerisi görünsün, cihazında zamanında hatırlatma alsın; kimse gece bildirim almasın.

**Spec:** master K-19, K-20, vizyon §0. **Kaynak:** rapor 2.2-4, 2.5 (sessiz saatler, saat dilimi).
**Dal:** `feat/p24-ders-programi` · **Ön koşul:** P16, P17.

### Task 0: Planı ayrıntılandır (Scheduling'deki öğrenci "kendi dersleri" (`TeacherUserId == null`) ile ilişki kararı dahil)
- [ ] **Step 1:** Kodu oku, görevleri 2026-09-02 plan biçiminde (`Files/Interfaces/Steps`) yaz, `docs(plan): P24 ayrintilandirildi` commit'i at, ILERLEME'yi güncelle. **Kod yazma.**

### Task 1: Okul ders programı (backend)
- `SchoolTimetable { StudentId, TermStart, TermEnd, Periods[ { DayOfWeek, StartLocal, EndLocal, SubjectName, TeacherName? } ] }` — yerel saat, dönem sınırlı; öğrencinin kendisi düzenler (veli görüntüler).
- **Kabul:** program dönem dışında üretilmez; P16 kuralına uygun (yerel saat).

### Task 2: Birleşik takvim sorgusu + boş zaman önerisi
- Okul programı + özel ders oluşumları + kendi çalışma planı + ödev son tarihleri; `FreeSlotCalculator` ile boş aralıklar; "bu akşam 19:00–20:30 boş — Matematik ödevine ayır" önerisi.
- **Kabul:** tek uç, hafta penceresi; çakışmalar işaretli (M04-2/M08-1 uyarısıyla tutarlı).

### Task 3: Mobil — ders programı girişi + birleşik haftalık görünüm
- Hızlı şablon (ör. 8 ders × 40 dk, 08:30 başlangıç); kopyala-yapıştır gün.

### Task 4: Yerel hatırlatıcılar + sessiz saatler
- `flutter_local_notifications`: çalışma planı ve ödev son tarih hatırlatmaları cihazda.
- Kullanıcı ayarı: sessiz saatler (varsayılan 22:00–08:00) + saat dilimi (varsayılan `Europe/Istanbul`); sunucu push'ları sessiz saatte **ertelenir** (ders başlangıç hatırlatması hariç, ayar ile).
- **Kabul:** gece 02:00'de tarayıcı kaynaklı push gitmez.

### Task 5: Kapanış — öğrenci haftalık planlama senaryosu
