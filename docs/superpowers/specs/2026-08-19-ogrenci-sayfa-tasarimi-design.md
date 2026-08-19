# Öğrenci Rolü Sayfa Tasarımı — 4-Sekme Yeniden Tasarım · Tasarım Spec'i

**Tarih:** 2026-08-19
**Dal:** `feat/ogrenci-5-sekme-ia` (üzerine)
**Kaynak analiz:** Kullanıcı brief'i (öğrenci 5 sayfa + ders detayı) · mevcut `feat/ogrenci-5-sekme-ia` IA'sı (`docs/superpowers/specs/2026-07-21-ogrenci-5-sekme-ia-design.md`) · kod envanteri (öğrenci sayfaları + `study_tab_widgets.dart` + Syncfusion takvim deseni) · `doc/architecture/{design_system,ux_rules,widgets}.md`
**Kapsam:** Mobil (`mobile/`) öğrenci rolü **sunum katmanı** — 4 sekme + Kronometre + Ders Detayı yeniden tasarımı. Backend/domain **değişmez**.

## Onaylanan kararlar

1. **Tam revizyon → 4 sekme.** Keşfet sekmesi kaldırılır. Sıra: **Çalışma(1) · Derslerim(2) · Performans(3) · Profil(4)**.
2. **Sayaç ayrılır.** Çalışma sekmesi dashboard olur; büyük kronometre ayrı **Kronometre** sayfasına taşınır ("Çalışmaya Başla"dan açılır). (2026-07-21 Task 2'yi tersine çevirir.)
3. **Yalnız mobil UI.** Mevcut/demo veri kullanılır; eksik backend `Ö-A…Ö-F` planlarında ayrı iş.
4. **Eksik backend davranışı = karışık.** Backend'i olan akışlar gerçek çalışır; olmayanlar **demo/yerel** görünür ve **"demo" mikro-rozetiyle** işaretlenir.
5. **Tam görsel yeniden tasarım** — mevcut içerik korunur ama layout brief'e uyarlanır; **iki paralel kart dili birleştirilir** (`student_home_page` premium yerel helper'ları + `study_tab_widgets.dart`) → tek dil.
6. **Ders Detayı yetkisi:** öğretmen dersinde öğretmenin içeriği **salt görüntüleme**; öğrenci yalnız kendi notu/testi/denemesini ekler. Kendi dersinde her şey düzenlenebilir.

---

## 1. Amaç

Öğrenci alt-navigasyonunu ve sayfalarını kullanıcı brief'indeki hedef yapıya taşımak: motivasyon-odaklı **Çalışma dashboard'u**, ayrık **Kronometre**, öğretmen-benzeri **Derslerim** (liste/takvim), zengin **Performans**, tanıtıcı **Profil** ve yeni **Ders Detayı**. Yeni domain yeteneği **yok**; iskelet + görsel dil + eksiklerde demo yer tutucu.

## 2. Genel Yapı — Navigasyon + Ortak Kabuk

**Alt navigasyon (`StudentNavTab`, 4 sekme):**

| Sıra | Sekme | İkon (öneri) | Rota | Kaynak sayfa |
|---|---|---|---|---|
| 1 | 🏠 Çalışma | `Icons.rocket_launch_rounded` | `/student-home` | `student_home_page` → dashboard |
| 2 | 📚 Derslerim | `Icons.menu_book_rounded` | `/student/lessons` | `student_calendar_page` → liste/takvim |
| 3 | 📊 Performans | `Icons.insights_rounded` | `/student/performance` | `student_tests_page` |
| 4 | 👤 Profil | `Icons.person_rounded` | `/student/profile` | `student_profile_page` |

**Sekme-dışı ekranlar (nav'da yok):**
- ⏱️ Kronometre — `/study/timer` (Çalışma → "Çalışmaya Başla").
- 📖 Ders Detayı — `/student/lessons/:id` (Derslerim/Çalışma → ders kartı).

**Kaldırma / yönlendirme:**
- Keşfet sekmesi + `student_discover_page` **kaldırılır**; `/student/discover` → `/student/lessons` redirect.
- Mevcut geri-uyum redirect'leri korunur (`/student/studies`→`/student-home`, `/student/tests`→`/student/performance`, `/student/calendar`→`/student/lessons`, `/student/more`→`/student/profile`).

**Ortak kabuk (sekme kökleri):** `Scaffold(bg: AppColors.background)` + `SafeArea(bottom:false)` + `AppPageHeader` (sol başlık/selamlama, sağ bildirim zili+rozet) + `ListView` (yatay padding 16, kartlar arası 12) + `StudentBottomNav`. AppBar yok. Push sayfaları (Kronometre, Ders Detayı, Öğretmenlerim vb.) `AppBar` kullanır, BottomNav yok. Tek gölge `AppShadows.soft`, kart radius 16.

**Kart dili birleştirme:** `student_home_page` yerel helper'ları (`_softCard`, `_IconChip`, `_Pressable`, `_ProgressRing`) ile `study_tab_widgets.dart` (`StudyCard`/`StudyStatTile`/`StudySectionHeader`) tek sete indirgenir; istatistik widget'ları ve kartlar her sekmede aynı görünür.

## 3. Sekme / Sayfa Tasarımları

### 3.1 Çalışma (dashboard) — `/student-home`

`StudyHomeCubit` korunur; `_HeroSummary` (büyük halka/sayaç) çıkarılır.

Blok düzeni (yukarıdan aşağı):
1. `AppPageHeader` — kişiye özel selamlama + motivasyon alt satırı + bildirim zili.
2. **İstatistik ızgarası (2×2)** — `StudyStatTile`:
   - 🔥 Streak — `StudyRepository.getStreak` (✅).
   - ⏱️ Bugünkü çalışma — süre + **günlük hedef ilerleme barı** (`getDashboard` + `StudyGoal`) (✅).
   - 📝 Haftalık ödev (verilen/tamamlanan) — `AssignmentRepository` haftalık sayım (⚠️ yoksa demo).
   - 📚 Haftalık ders (planlı/tamamlanan) — `SchedulingRepository` haftalık occurrence sayımı (⚠️ yoksa demo).
3. **"Çalışmaya Başla" kartı** (`_PrimaryActionCard`) — büyük buton + değişen motivasyon metni → `/study/timer`.
4. **Hızlı erişim** — `SectionHeader` + **4 kart** ızgarası: 📚 Derslerim (`/student/lessons`) · 📝 Ödevlerim (`/student/assignments`) · 🎯 Hedeflerim (`/student/goals-overview`) · 📊 Performansım (`/student/performance`). *(Takvim kartı kaldırıldı — Derslerim içinde.)*
5. **Yaklaşanlar** — "Yaklaşan dersler" (`_UpcomingLessonCard`, `SchedulingRepository`, ✅) + "Yaklaşan ödevler" (`AssignmentRepository`, ⚠️ yoksa demo). Boşsa `EmptyStateView`.

Kaldırılan: `_HeroSummary`, `_TodayPlanCard` (→ Derslerim), "Son çalışmalar" (→ Performans).

### 3.2 Kronometre — `/study/timer`

`study_timer_page` + `StudyTimerCubit` genişler. AppBar'lı push. İki aşama:

**A) Hazırlık formu** (`_StartForm`):
- **Ders seçimi** — `_SubjectChoiceChip` ızgarası (kendi + öğretmen dersleri, `SchedulingRepository`) + ayrı **"Serbest çalışma"** çipi.
- **Konu seçimi** — ders seçilince açılır; **opsiyonel** (konusuz ders olur) + **çoklu konu** seçilebilir.
- **Hızlı seçim** — "Son çalıştıkların" (son N ders/konu, `StudyRepository.listSessions` türevi, ✅).
- **Hedef süre (opsiyonel)** — 25/45/60 dk + özel çipleri.
- `Başla` → aktif kronometre. İkincil: "Süre ekle / geçmiş" → (C) sheet.

**B) Aktif kronometre** (`_ActiveTimer`):
- Büyük dial (`_TimerDial`/`_RingPainter`) — **`SS:DD:SN`** formatı.
- Hedef varsa dial içinde **kalan/ilerleme** halkası.
- Ders + konu(lar) `_SubjectPill`'de; serbest çalışmada "Serbest çalışma".
- `_StatsRow` — **Toplam = çalışma + mola**, **mola sayısı**, **toplam mola süresi**.
- Kontroller: **Mola ver** · **Çalışmayı bitir** · **Çalışmayı iptal et**.
  - **Molada durumu ayrı** — turuncu tema + mola süresi sayacı; "Devam et" ile döner. Mola net süreye eklenmez (mevcut doğru kurgu korunur).
  - Bitir → `_CompletionSummary` → kaydeder (✅). İptal → onay dialog'u (UX §5) → kayıt yok.

**C) Manuel çalışma & geçmiş** (bottom sheet):
- Süre ekle (ders/konu + tarih + süre) + geçmiş liste (düzenle/sil).
- ⚠️ Backend (`Ö-A2` seans düzenle-sil + istatistik geri-hesabı, `Ö-E` "unutuldu") yok → **demo/yerel + "demo" rozeti**.

### 3.3 Derslerim — `/student/lessons`

`student_calendar_page` genişler; öğretmen `scheduling_page` takvim desenini paylaşır.

1. `AppPageHeader("Derslerim")`.
2. **Liste / Takvim segmenti** (yeni üst geçiş, **Takvim default**) — `study_tab` segment dili. (Takvim-içi `Aylık/Haftalık/Günlük` seçicisinden ayrı.)
3. **Takvim modu:** `SfCalendar` + `_ViewSwitcher` + `_DateNavigator` + `_SelectedDayPanel` (seçili günün dersleri).
4. **Liste modu:** kronolojik gruplu ders listesi; ayrıca **Kendi/Öğretmen ayrı gruplama** ("Kendi derslerim" / "Öğretmen dersleri" başlıkları).
5. **Kendi/Öğretmen ayrımı** (her iki mod): rozet (`👤 Kendi` / `👨‍🏫 Öğretmen`) + aksan rengi (Kendi→teal, Öğretmen→accentBlue) + öğretmen adı; **filtre çipleri** Tümü/Kendi/Öğretmen. (Ç-06: `TeacherUserId=null` → kendi.)
6. **Etkileşim:** kart → Ders Detayı (`/student/lessons/:id`); FAB "Ders ekle" → `StudyEntryFormSheet` (kendi ders, `teacher=null`); düzenleme kart aksiyonu/detaydan.
7. **Küçük katalog girişi** — "Dersler & Konular" (`/study/catalog`) Derslerim içinde kalır.
8. "Bugünün planı" özeti buraya taşınır (seçili gün paneli karşılar).

**Ders araçları dağıtımı:** Öğretmenlerim → **Profil**; Notlarım → **Ders Detayı** (derse özel not listesi + "tüm notlarım" girişiyle `study_notes` sayfasına ulaşılır; Profil menüsünde ayrı satır yok); Katalog → **Derslerim** (kalır); Ödevlerim → **Çalışma** hızlı erişim.

### 3.4 Ders Detayı — `/student/lessons/:id`

Yeni push sayfa (AppBar'lı, BottomNav yok). Veri: `SchedulingRepository.getLesson`, `TeacherRepository.getProfile`, `AssignmentRepository`, `StudyRepository`, `SubjectCatalog`.

1. **Başlık kartı** — ders adı, tarih/saat, tür (Online/Yüz yüze + link/konum), Kendi/Öğretmen rozeti, durum.
2. **Öğretmen bilgisi kartı** — *yalnız öğretmen dersinde* (avatar+ad+branş, salt görüntüleme). Kendi dersinde gizli.
3. **Hızlı erişim kartları (ekle/düzenle)** — yetkiye göre:

   | Kart | Kendi dersi | Öğretmen dersi |
   |---|---|---|
   | 📝 Not | ekle/düzenle | ekle/düzenle (kendi notu) |
   | 📊 Test | ekle | ekle (kendi) |
   | 🎯 Deneme | ekle | ekle (kendi) |
   | 📚 Ödev | ekle/düzenle | **salt görüntüle + teslim et** (ekle gizli) |
   | 🗂️ Konu | ekle/düzenle | **salt görüntüle** (ekle gizli) |

4. **Listeler:**
   - Ödev listesi + teslim durumu (`AssignmentRepository`, ✅ + teslim).
   - **Test & Deneme — tek liste, tür rozetli** (🔹 Test / 🔸 Deneme) (`StudyRepository.listTests`, ⚠️ derse-bağ yoksa demo).
   - Konu listesi + hâkimiyet rozeti (`SubjectCatalog`; hâkimiyet ⚠️ demo).
   - Not listesi (`study_notes`, ✅).

### 3.5 Performans — `/student/performance`

`student_tests_page` yeniden düzenlenir + eksikler eklenir.

1. `AppPageHeader("Performans")`.
2. **Özet istatistik satırı** (`StudyStatTile`) — toplam deneme · ort. net · en iyi net · **hedef nete kalan**.
3. **Birincil eylem** — "Test / Deneme gir" → `/study/test`; girişte **tür** 🔹 Test (konu bazlı) / 🔸 Deneme (çok dersli).
4. **Hedef net takibi** — hedef + ortalama + ilerleme barı (skor renk kuralı). ⚠️ `Ö-B` yoksa demo.
5. **Net gelişim grafiği** (`_NetTrendChart`) — filtre Genel/ders + Haftalık/Aylık.
6. **Konu bazlı istatistikler — iki ayrı bölüm:** 🔹 Test istatistikleri (konu bazlı doğru/yanlış/net, `_LessonBreakdown`) · 🔸 Deneme istatistikleri (derslere göre net, `_SubjectAnalysis`).
7. **Konu eksiği tespiti** — zayıf konular + "çalış" kısayolu (Kronometre'ye konu seçili). ⚠️ hesaplama demo.
8. **Haftalık / Aylık analiz** — Hafta/Ay segmenti + `_WeeklyBars`.
9. **Kişisel rekorlar** — en iyi net · en uzun seri · en uzun tek seans · en çok çalışılan gün · en verimli ders. ⚠️ bazıları demo.
10. **Alt linkler** — Geçmiş (`/study/history`) · Gelişim (`/student/progress`).

### 3.6 Profil — `/student/profile`

`student_profile_page` genişler.

1. **Profil hero** — büyük avatar (foto/baş harf), ad, sınıf/hedef sınav, 🔥 seri. **Abonelik ayrımı:** premium'da altın/gradient kenarlık + "Premium" rozeti; free'de sade + "Yükselt" ipucu. Sağ üstte **Düzenle** (profil düzenleme — ⚠️ backend yoksa demo/yerel).
2. **Mini istatistikler** (`StudyStatTile`) — toplam çalışma · çalışılan gün · rekor seri · toplam deneme (✅).
3. **Başarımlar** — `StudySectionHeader("Rozetler", "Tümü")` → `/study/achievements` (✅).
4. **Menü listesi** (`ProfileMenuTile`):

   | Menü | Hedef | Durum |
   |---|---|---|
   | 👨‍👩‍👧 Velim (bağlantı kontrol + ekle) | veli bağlantı sayfası | ⚠️ yoksa demo |
   | 👨‍🏫 Öğretmenlerim | `student_teacher_page` (salt görüntüleme) | ✅ |
   | 🎯 Hedef ekle | `/study/goals` | ✅ |
   | 🔔 Bildirim ayarları | ayar ekranı | ⚠️ demo/yer tutucu |
   | 🔒 Gizlilik ve Güvenlik (birleşik) | gizlilik + `/account-info` | kısmen ✅ |
   | ⭐ Aboneliğim | plan/fatura (Faz 5) | ⚠️ yer tutucu |
   | 🚪 Çıkış yap | `AuthCubit.logout()` (onay sheet) | ✅ |

## 4. Değişecek / Yeni Dosyalar

- `features/study/presentation/widgets/student_bottom_nav.dart` — `StudentNavTab` 4 sekmeye indirilir (Keşfet çıkar), ikon/etiket/sıra güncellenir.
- `core/routing/app_router.dart` — `/student/lessons/:id` (Ders Detayı) eklenir; `/student/discover` → `/student/lessons` redirect; Keşfet builder kaldırılır.
- `student_home_page.dart` — dashboard: istatistik ızgarası + "Çalışmaya Başla" + 4 hızlı erişim + yaklaşanlar; `_HeroSummary`/`_TodayPlanCard`/"son çalışmalar" çıkar.
- `study_timer_page.dart` — hazırlık formu (çoklu konu/serbest/hızlı seçim/hedef) + **Molada** durumu + manuel süre/geçmiş sheet (demo).
- `student_calendar_page.dart` → **Derslerim** — Liste/Takvim segmenti + Kendi/Öğretmen ayrımı+gruplama+filtre + karttan detaya git.
- **Yeni** `student_lesson_detail_page.dart` — Ders Detayı (yetki tablosu + tek Test&Deneme listesi).
- `student_tests_page.dart` → **Performans** — hedef net + konu eksiği + aylık/haftalık + kişisel rekorlar; test/deneme iki ayrı bölüm.
- `student_profile_page.dart` → **Profil** — premium hero + Aboneliğim + birleşik Gizlilik&Güvenlik + Öğretmenlerim.
- **Kaldırılır:** `student_discover_page.dart`.
- Kart dili birleştirme — `study_tab_widgets.dart`'a taşınan ortak kart/stat/ikon-chip helper'ları; `student_home_page` yerel helper'ları buraya indirgenir (`shared/widgets` katalog güncellenir).
- Alt sayfalar (`achievements`, `study_goals`, `study_notes`, `subject_catalog`, `student_teacher`, `study_history`, `student_goals_overview`, `test_entry`) korunur; yalnız giriş noktaları yeni yerlere bağlanır.

## 5. Doküman Bakımı (CLAUDE.md kuralı — tamamlanınca)

- `doc/pages/study_student.md` + `doc/pages/00_pages_index.md` — 4 sekme + Ders Detayı; Keşfet satırı kaldır.
- `doc/roles/ogrenci.md` + `doc/roles/ogrenci_ux.md` — 4-sekme nav + sayfa akışları.
- `doc/architecture/mobile_flutter.md` — öğrenci nav yapısı.
- `doc/architecture/widgets.md` — birleşen kart/stat widget'ları + `StudentBottomNav` (4 sekme) durumu.

## 6. Test / Doğrulama

- Mobil widget/route testleri yeni rotalara göre güncellenir; Keşfet redirect testi + `/student/lessons/:id` testi eklenir.
- Manuel: her sekme geçişi · dashboard istatistik/hızlı erişim · Kronometre form→aktif→**Molada**→bitir · Derslerim liste/takvim + Kendi/Öğretmen filtre + detaya git · Ders Detayı yetki tablosu · Performans blokları · Profil premium/menü.
- Demo rozetli alanların açıkça "demo" işaretli göründüğü doğrulanır.
- Not: mobil test paketinde önceden bozuk 6 test var (auth fake) — bu iş kapsamı dışı.

## 7. Açık / Ertelenen

- Eksik backend (`Ö-A…Ö-F`): haftalık ödev/ders istatistiği, manuel seans ekle/sil + istatistik geri-hesabı, hedef net formülü/çok dersli deneme, konu hâkimiyeti/eksik tespiti, veli bağlantı ekleme, bildirim ayarları, abonelik — hepsi demo yer tutucu; gerçek akış ilgili dilim işlerinde.
- Öğretmen arama/keşif (eski Keşfet, Faz 4) — bu revizyonda tamamen kaldırıldı; ileride ayrı giriş noktasıyla dönebilir.

---

**Sıradaki adım:** Bu spec onaylanınca `writing-plans` ile uygulama planı çıkarılır (her sekme/ekran ayrı adım, sırayla — önceki 5-sekme gibi).
