# Öğrenci Sayfa Yapısı — 5-Sekme IA'ya Yeniden Yapılandırma · Tasarım Spec'i

**Tarih:** 2026-07-21
**Kaynak analiz:** `doc/diagrams/rol_sayfa_mimarisi/ogrenci.md` §1 (IA ağacı) + `svg/ogrenci/01_sayfa_yapisi_ia.svg` · `doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md` (v1.0) · Ç-06 kararı
**Kapsam:** Mobil (`mobile/`) öğrenci rolü sunum katmanı — alt-navigasyon + sayfa yeniden gruplama. Backend/domain **değişmez**.
**Onaylanan kararlar:** (1) Tam 5-sekme yeniden yapılandırma · (2) Açılış: dashboard korunur, sayaç belirginleşir, sekme adı "Çalış" · (3) Keşfet: 5. sekme, "Faz 4 — yakında" yer tutucu · (4) "Çalışmalarım" bölünür (özet→Çalış, detay analiz→Performans) · (5) Ara sayfalar: Notlarım→Derslerim, Rozetler+Hedefler→Çalış, Gelişimim→Performans.

---

## 1. Amaç

Öğrenci alt-navigasyonunu, fonksiyonel dokümandan türetilmiş **hedef bilgi mimarisine** (IA) taşımak. Hedef IA 5 sekme:

| Sıra | Hedef sekme | IA rolü |
|---|---|---|
| 1 | ⏱️ **Çalış** *(açılış)* | Büyük sayaç, 0 tık ile başla |
| 2 | 📊 **Performans** | Test/net + haftalık/aylık analiz + rekorlar |
| 3 | 📚 **Derslerim** | Program/takvim + kendi ders + katalog + öğretmen dersleri |
| 4 | 🔍 **Keşfet** *(Faz 4)* | Öğretmen arama/keşif |
| 5 | 👤 **Profil** | Profil + veli + gizlilik + abonelik + ayarlar |

Bu spec yalnız **navigasyon iskeleti + sayfa yeniden gruplama** kapsar. Yeni domain yeteneği (öğretmen arama, abonelik akışı vb.) kapsam dışıdır — yalnız iskelet/yer tutucu.

**Kapsam dışı:** Backend/domain değişikliği · yeni endpoint · Faz 4 öğretmen arama işlevi (sadece yer tutucu) · Free/Premium ⚠️ 9.2 çelişkilerinin çözümü (mevcut davranış korunur) · yeni tasarım dili (mevcut premium kart stili korunur).

## 2. Mevcut Durum (kod gerçeği)

**Alt-nav** (`student_bottom_nav.dart` → `StudentNavTab` enum), 5 sekme:
`home` (Ana Sayfa · `/student-home`) · `studies` (Çalışmalarım · `/student/studies`) · `tests` (Testler · `/student/tests`) · `calendar` (Takvim · `/student/calendar`) · `more` (Diğer · `/student/more`).

**Mevcut sayfalar** (`features/study/presentation/pages/`):
- `student_home_page` — zengin premium dashboard (streak, günlük hedef, motivasyon, kısayollar, sayaç linki).
- `study_timer_page` — tam sayaç ekranı ("Çalışma Kronometresi").
- `student_studies_page` — "Çalışmalarım": bu hafta · derslerim · son çalışmalar · kronometre başlat.
- `student_tests_page` — "Testler": net trendi · derslere göre net · son denemeler · deneme gir.
- `student_calendar_page` — "Takvim": ders programı + ders ekle.
- `student_more_page` — hub: Profil/İstatistik · Rozetler · Öğretmenlerim · Dersler & Konular · Ödevlerim · Notlarım · Hedefler · Gelişimim · Hedef & paylaşım ayarları · Hesap bilgileri · Çıkış.
- `student_profile_page` · `subject_catalog_page` · `achievements_page` · `study_history_page` · `study_goals_page` · `student_goals_overview_page` · `study_notes_page` · `test_entry_page` · `student_teacher_page`.

**Router:** `core/routing/app_router.dart`.

## 3. Tasarım

### 3.1 Yeni alt-nav (`StudentNavTab`)

| Yeni sekme | İkon (öneri) | Rota | Kaynak sayfa |
|---|---|---|---|
| ⏱️ Çalış | `Icons.play_circle_fill_rounded` | `/student-home` *(korunur)* | `student_home_page` (uyarlanır) |
| 📊 Performans | `Icons.insights_rounded` | `/student/performance` | `student_tests_page` (yeniden adlandırılır + genişler) |
| 📚 Derslerim | `Icons.menu_book_rounded` | `/student/lessons` | `student_calendar_page` (genişler) |
| 🔍 Keşfet | `Icons.travel_explore_rounded` | `/student/discover` | **yeni** `student_discover_page` (yer tutucu) |
| 👤 Profil | `Icons.person_rounded` | `/student/profile` | `student_profile_page` (genişler) |

- Eski rotalar (`/student/studies`, `/student/tests`, `/student/calendar`, `/student/more`) **redirect** ile yenilere yönlendirilir (derin link/geri uyum kırılmasın).
- `StudentBottomNav` görsel/etkileşim davranışı aynı kalır; yalnız sekme listesi değişir.

### 3.2 Sekme içerikleri

**1 · ⏱️ Çalış** — *minimal değişiklik*
`student_home_page` dashboard'u korunur. Değişiklik: **sayaç kartı en üste taşınır + büyütülür**, Başlat/Mola/Bitir eylemi belirginleşir (0 tık ilkesine yaklaşır). Alt bloklar korunur: bugünkü toplam süre · 🔥 streak · günlük hedef ilerlemesi · motivasyon metni · **bugün/bu hafta çalışma özeti** (Çalışmalarım'dan taşınan özet) · kısayollar: **Rozetler** (`achievements_page`) + **Hedefler** (`study_goals_page`/`student_goals_overview_page`) + manuel seans ekle.

**2 · 📊 Performans** — *eski "Testler" genişler*
`student_tests_page` çekirdek (net trendi · derslere göre net · son denemeler · deneme/test gir). Eklenen bloklar:
- Haftalık/aylık analiz + kişisel rekorlar (Çalışmalarım'ın *detay analiz* kısmından + `study_history_page` linki).
- **Gelişimim** — konu bazlı hâkimiyet / eksik-güçlü konular (eski "Diğer > Gelişimim").

**3 · 📚 Derslerim** — *eski "Takvim" + "Diğer"den derlenenler*
`student_calendar_page` (program/takvim + kendi ders ekle · `teacher_id=null`) çekirdek. Eklenen bloklar/girişler:
- 📖 **Dersler & Konular kataloğu** (`subject_catalog_page`).
- 👨‍🏫 **Ödevlerim** + teslim (öğretmenli derslerde).
- 👨‍🏫 **Öğretmenlerim** (`student_teacher_page` — bağlı öğretmenler, salt görüntüleme).
- 📝 **Notlarım** (`study_notes_page`).
- Ders kartı rozeti `👤 Kendi / 👨‍🏫 Öğretmen` + filtre (Tümü/Kendi/Öğretmen) — mevcut Ç-06 modeli.
- Öğretmen yoksa: "Öğretmen Bul" ikincil eylemi → Keşfet sekmesine yönlendirir.

**4 · 🔍 Keşfet** — *yeni, Faz 4 yer tutucu*
Yeni `student_discover_page`: arama kutusu + filtre çipleri (branş·şehir·ücret·şekil·saat) **devre dışı görünümde** + belirgin "Bu özellik yakında (Faz 4)" boş durumu (`state_views` deseni). İşlevsel arama yok.

**5 · 👤 Profil** — *eski "Diğer" kalanı + `student_profile_page`*
`student_profile_page` çekirdek. Bloklar: Profil bilgileri · Velim (bağlantı) · ⭐ Gizlilik ayarları (eski "Hedef & paylaşım ayarları") · Bildirim ayarları · Abonelik (Faz 5 — yer tutucu) · Ayarlar & Güvenlik (eski "Hesap bilgileri") · **Çıkış yap**.

### 3.3 "Diğer" hub'ının (`student_more_page`) dağılımı

| Eski "Diğer" girdisi | Yeni yer |
|---|---|
| Profil / İstatistik | Çalış (dashboard) + Profil |
| Rozetler | Çalış (kısayol) |
| Hedefler | Çalış (kısayol) |
| Öğretmenlerim | Derslerim |
| Dersler & Konular | Derslerim |
| Ödevlerim | Derslerim |
| Notlarım | Derslerim |
| Gelişimim | Performans |
| Hedef & paylaşım ayarları | Profil (⭐ Gizlilik) |
| Hesap bilgileri | Profil (Ayarlar & Güvenlik) |
| Çıkış yap | Profil |

`student_more_page` kaldırılır; `/student/more` → `/student/profile` redirect.

## 4. Değişecek Dosyalar

- `features/study/presentation/widgets/student_bottom_nav.dart` — enum + sekme listesi + ikonlar + doc yorumu.
- `core/routing/app_router.dart` — yeni rotalar + eski rota redirect'leri.
- `features/study/presentation/pages/student_home_page.dart` — sayaç kartını üste taşı/büyüt + çalışma özeti + Rozetler/Hedefler kısayolları.
- `student_tests_page.dart` → **Performans** (başlık/rota/nav sekmesi + analiz + Gelişimim blokları).
- `student_calendar_page.dart` → **Derslerim** (başlık/rota/nav sekmesi + katalog/ödev/öğretmen/notlar girişleri).
- `student_profile_page.dart` → **Profil** (nav sekmesi + Diğer'den gelen girdiler + çıkış).
- **Yeni** `student_discover_page.dart` — Keşfet yer tutucu.
- `student_studies_page.dart` · `student_more_page.dart` — kaldırılır (içerikleri dağıtıldıktan sonra).
- Alt sayfalar (`achievements_page`, `study_goals_page`, `study_notes_page`, `subject_catalog_page`, `student_teacher_page`, `study_history_page`, `student_goals_overview_page`, `test_entry_page`) — **korunur**, yalnız erişim noktaları (giriş linkleri) yeni sekmelere bağlanır.

## 5. Doküman Bakımı (CLAUDE.md kuralı)

Kod değişince aynı turda güncellenecek:
- `doc/pages/study_student.md` — yeni sekme yapısı.
- `doc/pages/00_pages_index.md` — kaldırılan/eklenen sayfa satırları.
- `doc/roles/ogrenci.md` + `doc/roles/ogrenci_ux.md` §4 — 5-sekme nav açıklaması (eski "Ana Sayfa·Çalışmalarım·Testler·Takvim·Diğer" düzeltilir).
- `doc/architecture/mobile_flutter.md` — öğrenci nav yapısı değiştiyse.

## 6. Test / Doğrulama

- Mevcut mobil widget/route testleri yeni rota adlarına göre güncellenir; eski rota redirect testi eklenir.
- Manuel: her sekmeye geçiş + eski "Diğer" girdilerinin yeni yerlerinden erişilebilirliği + Keşfet yer tutucu görünümü.
- Not: mobil test paketinde önceden bozuk 6 test var (auth fake) — bu iş kapsamına dahil değil.

## 7. Açık/Ertelenen

- Faz 4 öğretmen arama işlevi (Keşfet içi) — sonraki iş.
- Abonelik (Faz 5) gerçek akışı — yer tutucu.
- Free/Premium ⚠️ 9.2 çelişkileri — bu iş kapsamı dışı, mevcut davranış korunur.

---

**Sıradaki adım:** Bu spec onaylanınca `writing-plans` ile uygulama planı çıkarılır (her sekme ayrı adım, sırayla).
