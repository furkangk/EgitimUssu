# Öğrenci 5-Sekme IA Yeniden Yapılandırma · Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrenci alt-navigasyonunu mevcut 5 sekmeden (Ana Sayfa·Çalışmalarım·Testler·Takvim·Diğer) hedef IA'ya (⏱️ Çalış · 📊 Performans · 📚 Derslerim · 🔍 Keşfet · 👤 Profil) taşımak; "Çalışmalarım" ve "Diğer" hub'larını dağıtıp kaldırmak, eski rotaları redirect'lemek.

**Architecture:** Yalnız mobil **sunum katmanı**. `StudentNavTab` enum'u yeniden yazılır; `app_router.dart` yeni rotalar + eski→yeni redirect'ler alır; mevcut sayfalar yeniden adlandırılıp yeni sekmelere bağlanır; bir yeni yer-tutucu sayfa (`student_discover_page`) eklenir; `student_studies_page` + `student_more_page` içerikleri dağıtıldıktan sonra silinir. Backend/domain/endpoint/repository **değişmez**.

**Tech Stack:** Flutter · `go_router` (rota + redirect) · `flutter_bloc`/Cubit · `get_it` (`injector`) · mevcut `study` + `scheduling` feature'ları.

## Global Constraints

- **Görünen ad:** EğitimÜssü · **kod tanımlayıcı:** EgitimUssu (Türkçe karaktersiz).
- **Backend/domain/endpoint/repository değişmez.** Yalnız `mobile/lib/features/study/presentation/**` + `mobile/lib/core/routing/app_router.dart`.
- **Kapsam dışı:** Faz 4 öğretmen arama işlevi (yalnız yer tutucu) · Abonelik gerçek akışı (yer tutucu) · Free/Premium ⚠️ 9.2 çelişkileri (mevcut davranış korunur) · yeni tasarım dili (mevcut premium kart stili korunur).
- **Doküman bakımı (CLAUDE.md):** Her tab görevinin son adımı ilgili `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci.md`, `doc/roles/ogrenci_ux.md`'yi **aynı görevde** günceller; güncellenen her dokümanın alt tarihini `2026-07-21` yapar.
- **Test gerçeği:** Öğrenci nav'ı için mevcut widget/route testi yoktur; mobil test paketi auth-fake nedeniyle önceden bozuktur (kapsam dışı). Bu yüzden **her görevin derleme kapısı `flutter analyze` (yeni hata yok)**; ek olarak yalnız ağ/auth bağımlılığı olmayan saf birim/widget testleri yazılır (Task 1 enum testi, Task 5 discover widget testi). İçerik-zengin sayfalar (`home/tests/calendar/profile`) `StudentScope.resolve` ile `initState`'te ağ çağırır → widget testi bozuk fake'e bağımlı olur, bu yüzden onlarda doğrulama `flutter analyze` + manuel duman testidir.
- **Komut dizini:** Tüm `flutter`/`git` komutları `mobile/` altından çalıştırılır.

---

## Dosya Yapısı (kim neyden sorumlu)

- `features/study/presentation/widgets/student_bottom_nav.dart` — `StudentNavTab` enum'u (5 sekme sözleşmesi) + alt-nav görseli. **Değişir.**
- `core/routing/app_router.dart` — yeni `/student/*` rotaları + eski rota redirect'leri. **Değişir.**
- `features/study/presentation/pages/student_home_page.dart` — ⏱️ Çalış. **Değişir** (sayaç üste + kısayol düzeni).
- `features/study/presentation/pages/student_tests_page.dart` — 📊 Performans (eski Testler; genişler). **Değişir.**
- `features/study/presentation/pages/student_calendar_page.dart` — 📚 Derslerim (eski Takvim; araç girişleri eklenir). **Değişir.**
- `features/study/presentation/pages/student_profile_page.dart` — 👤 Profil (tab olur + Diğer kalanı absorbe). **Değişir.**
- `features/study/presentation/pages/student_discover_page.dart` — 🔍 Keşfet yer tutucu. **Yeni.**
- `features/study/presentation/pages/student_studies_page.dart` — içeriği Performans'a taşındıktan sonra **silinir** (Task 3).
- `features/study/presentation/pages/student_more_page.dart` — içeriği dağıtıldıktan sonra **silinir** (Task 6).
- `test/features/study/presentation/widgets/student_nav_tab_test.dart` — enum sözleşme testi. **Yeni** (Task 1).
- `test/features/study/presentation/pages/student_discover_page_test.dart` — yer tutucu render testi. **Yeni** (Task 5).
- Korunan (yalnız erişim linki bağlanır, içeriği değişmez): `achievements_page`, `study_goals_page`, `study_notes_page`, `subject_catalog_page`, `student_teacher_page`, `study_history_page`, `student_goals_overview_page`, `test_entry_page`, `student_assignments_page`, `progress_overview_page`.

**Sekme → rota → kaynak sayfa haritası (sözleşme, tüm görevlerde tutarlı):**

| Enum üyesi | İkon | Etiket | Rota | Sayfa |
|---|---|---|---|---|
| `work` | `Icons.play_circle_fill_rounded` | `Çalış` | `/student-home` | `StudentHomePage` |
| `performance` | `Icons.insights_rounded` | `Performans` | `/student/performance` | `StudentTestsPage` |
| `lessons` | `Icons.menu_book_rounded` | `Derslerim` | `/student/lessons` | `StudentCalendarPage` |
| `discover` | `Icons.travel_explore_rounded` | `Keşfet` | `/student/discover` | `StudentDiscoverPage` |
| `profile` | `Icons.person_rounded` | `Profil` | `/student/profile` | `StudentProfilePage` |
| `none` | `Icons.circle` | `''` | `''` | — (dormant sayfalar için) |

**Eski→yeni redirect'ler:** `/student/tests`→`/student/performance` (Task 1) · `/student/calendar`→`/student/lessons` (Task 1) · `/student/studies`→`/student-home` (Task 3) · `/student/more`→`/student/profile` (Task 6).

---

## Task 1: Navigasyon iskeleti (enum + router + redirect + tab bağlama)

**Files:**
- Modify: `mobile/lib/features/study/presentation/widgets/student_bottom_nav.dart`
- Modify: `mobile/lib/core/routing/app_router.dart`
- Create: `mobile/lib/features/study/presentation/pages/student_discover_page.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart` (yalnız `current:` satırı)
- Modify: `mobile/lib/features/study/presentation/pages/student_tests_page.dart` (yalnız `current:` satırı)
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart` (yalnız `current:` satırı)
- Modify: `mobile/lib/features/study/presentation/pages/student_profile_page.dart` (bottomNav ekle)
- Modify: `mobile/lib/features/study/presentation/pages/student_studies_page.dart` (yalnız `current:` → `none`)
- Modify: `mobile/lib/features/study/presentation/pages/student_more_page.dart` (yalnız `current:` → `none`)
- Create: `mobile/test/features/study/presentation/widgets/student_nav_tab_test.dart`
- Docs: `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci_ux.md`, `doc/roles/ogrenci.md`

**Interfaces:**
- Produces: `StudentNavTab { work, performance, lessons, discover, profile, none }` — her üye `(IconData icon, String label, String route)`. `StudentBottomNav({required StudentNavTab current})`. `StudentDiscoverPage()` (const, argümansız).
- Consumes: mevcut `StudentBottomNav` görsel gövdesi (değişmez), `StudentTestsPage/StudentCalendarPage/StudentProfilePage` const kurucuları.

- [ ] **Step 0: Analiz baselineı al**

Run: `cd mobile && flutter analyze`
Beklenen: mevcut durumu kaydet (0 hata olması beklenir; varsa sayıyı not al — sonraki adımlarda bu sayının artmaması gerekir).

- [ ] **Step 1: Enum sözleşme testini yaz (failing)**

Create `mobile/test/features/study/presentation/widgets/student_nav_tab_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('StudentNavTab 5 görünür sekme; sıra ve rotalar IA ile uyumlu', () {
    const expected = <(StudentNavTab, String, String)>[
      (StudentNavTab.work, 'Çalış', '/student-home'),
      (StudentNavTab.performance, 'Performans', '/student/performance'),
      (StudentNavTab.lessons, 'Derslerim', '/student/lessons'),
      (StudentNavTab.discover, 'Keşfet', '/student/discover'),
      (StudentNavTab.profile, 'Profil', '/student/profile'),
    ];
    for (final (tab, label, route) in expected) {
      expect(tab.label, label);
      expect(tab.route, route);
    }
    // Eski üyeler kaldırıldı: home/studies/tests/calendar/more artık yok.
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('studies')));
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('more')));
  });

  testWidgets('StudentBottomNav 5 sekme etiketini çizer', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        bottomNavigationBar: StudentBottomNav(current: StudentNavTab.work),
      ),
    ));
    for (final label in ['Çalış', 'Performans', 'Derslerim', 'Keşfet', 'Profil']) {
      expect(find.text(label), findsOneWidget);
    }
  });
}
```

- [ ] **Step 2: Testin derlenmeyip/başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/student_nav_tab_test.dart`
Beklenen: FAIL — `StudentNavTab.work` vb. tanımlı değil (derleme hatası).

- [ ] **Step 3: `StudentNavTab` enum'unu yeniden yaz**

`student_bottom_nav.dart` içinde doküman yorumu + enum bloğunu değiştir. `enum StudentNavTab { ... }` bloğunu (satır ~5-22) tamamen şununla değiştir:

```dart
/// Öğrenci paneline özgü alt navigasyon. Öğretmen [AppBottomNav] ve veli
/// ParentBottomNav'ından ayrıdır; öğrenci sekmeleri 5-sekme IA'ya göredir
/// (⏱️ Çalış · 📊 Performans · 📚 Derslerim · 🔍 Keşfet · 👤 Profil) —
/// bkz. `doc/roles/ogrenci_ux.md` §4 ve `doc/pages/study_student.md`.
enum StudentNavTab {
  work(Icons.play_circle_fill_rounded, 'Çalış', '/student-home'),
  performance(Icons.insights_rounded, 'Performans', '/student/performance'),
  lessons(Icons.menu_book_rounded, 'Derslerim', '/student/lessons'),
  discover(Icons.travel_explore_rounded, 'Keşfet', '/student/discover'),
  profile(Icons.person_rounded, 'Profil', '/student/profile'),
  none(Icons.circle, '', '');

  const StudentNavTab(this.icon, this.label, this.route);

  final IconData icon;
  final String label;
  final String route;
}
```

`_tabs` listesini (satır ~29-35) değiştir:

```dart
  static const List<StudentNavTab> _tabs = <StudentNavTab>[
    StudentNavTab.work,
    StudentNavTab.performance,
    StudentNavTab.lessons,
    StudentNavTab.discover,
    StudentNavTab.profile,
  ];
```

`build` gövdesinin geri kalanı (Container/Row/InkWell) **değişmez**.

- [ ] **Step 4: Keşfet yer-tutucu sayfasını (minimal stub) oluştur**

Create `mobile/lib/features/study/presentation/pages/student_discover_page.dart`:

```dart
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';

/// 🔍 Keşfet sekmesi — Faz 4 öğretmen arama/keşfi için yer tutucu.
/// İşlevsel arama yoktur (bkz. `doc/roles/ogrenci_ux.md` §4, spec 2026-07-21).
/// Task 5'te tasarımlı boş durum (arama kutusu görünümü + devre dışı filtre
/// çipleri + "yakında") ile zenginleştirilir.
class StudentDiscoverPage extends StatelessWidget {
  const StudentDiscoverPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
          children: const <Widget>[
            AppPageHeader(
              title: 'Keşfet',
              subtitle: 'Sana uygun öğretmeni bul.',
            ),
            SizedBox(height: 40),
            Center(
              child: Text(
                'Bu özellik yakında (Faz 4).',
                style: TextStyle(color: AppColors.textSecondary),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.discover),
    );
  }
}
```

- [ ] **Step 5: Router'a yeni rotalar + redirect'ler ekle**

`app_router.dart` importlarına ekle (mevcut study import bloğuna, alfabetik yakınına):

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_discover_page.dart';
```

`routes:` listesinde, mevcut `'/student/studies'`, `'/student/tests'`, `'/student/calendar'` GoRoute'larını bul. `'/student/tests'` ve `'/student/calendar'` builder'larını **redirect**'e çevir ve yeni rotaları ekle. Bu üç GoRoute'un olduğu bloğu (satır ~133-144) şununla değiştir:

```dart
        // Öğrenci alt navigasyon sekmeleri — 5-sekme IA (study_student.md §Navigasyon).
        GoRoute(
          path: '/student/studies',
          builder: (context, state) => const StudentStudiesPage(),
        ),
        GoRoute(
          path: '/student/performance',
          builder: (context, state) => const StudentTestsPage(),
        ),
        GoRoute(
          path: '/student/lessons',
          builder: (context, state) => const StudentCalendarPage(),
        ),
        GoRoute(
          path: '/student/discover',
          builder: (context, state) => const StudentDiscoverPage(),
        ),
        // Eski rota → yeni rota geri-uyum redirect'leri.
        GoRoute(
          path: '/student/tests',
          redirect: (context, state) => '/student/performance',
        ),
        GoRoute(
          path: '/student/calendar',
          redirect: (context, state) => '/student/lessons',
        ),
```

(`/student/studies` bu görevde hâlâ builder ile kalır — Task 3'te redirect'e çevrilip sayfası silinir. `/student/more` ve `/student/profile` mevcut GoRoute'larıyla değişmeden kalır.)

- [ ] **Step 6: Sayfaların `current:` bağlamalarını güncelle**

Her dosyada `StudentBottomNav(current: StudentNavTab.X)` satırını değiştir:

- `student_home_page.dart` (satır ~234): `StudentNavTab.home` → `StudentNavTab.work`
- `student_tests_page.dart` (satır ~88): `StudentNavTab.tests` → `StudentNavTab.performance`
- `student_calendar_page.dart` (satır ~253-255): `StudentNavTab.calendar` → `StudentNavTab.lessons`
- `student_studies_page.dart` (satır ~88): `StudentNavTab.studies` → `StudentNavTab.none`
- `student_more_page.dart` (satır ~134): `StudentNavTab.more` → `StudentNavTab.none`

- [ ] **Step 7: Profil sayfasını tab yap (bottomNav ekle)**

`student_profile_page.dart` importlarına ekle:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
```

`build` içindeki `Scaffold`'a `bottomNavigationBar` ekle (mevcut `appBar` bu görevde kalır; Task 6'da header'a çevrilir):

```dart
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Profil')),
      body: _loading
          ? const LoadingStateView(message: 'Profilin yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
      bottomNavigationBar:
          const StudentBottomNav(current: StudentNavTab.profile),
    );
```

- [ ] **Step 8: Enum testinin geçtiğini gör**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/student_nav_tab_test.dart`
Beklenen: PASS (2 test).

- [ ] **Step 9: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: Step 0'daki baseline'a göre **yeni hata yok**.

- [ ] **Step 10: Dokümanları güncelle (CLAUDE.md kuralı)**

1. `doc/pages/study_student.md` §Navigasyon ilk maddesini yeni 5-sekme yapısına göre yeniden yaz: "Alt navigasyon (`StudentBottomNav`, 5 sekme): ⏱️ Çalış (`/student-home`) · 📊 Performans (`/student/performance`) · 📚 Derslerim (`/student/lessons`) · 🔍 Keşfet (`/student/discover`, Faz 4 yer tutucu) · 👤 Profil (`/student/profile`). Eski rotalar redirect'lenir: `/student/tests`→`/student/performance`, `/student/calendar`→`/student/lessons` (Task 3/6'da `/student/studies`→`/student-home`, `/student/more`→`/student/profile`)." Eski "Ana Sayfa·Çalışmalarım·Testler·Takvim·Diğer" ve "Hedefler Diğer'e taşındı" ifadelerini kaldır.
2. `doc/pages/study_student.md` Ekranlar tablosuna `/student/discover` satırı ekle (Sayfa: `student_discover_page.dart`, State: yer tutucu, İçerik: "Keşfet — Faz 4 öğretmen arama yer tutucu").
3. `doc/pages/00_pages_index.md`'ye `study_student.md` altındaki listede/uygun bölümde `student_discover_page.dart` satırı ekle (mevcut satır biçimini birebir taklit et).
4. `doc/roles/ogrenci_ux.md` §4 nav açıklamasını 5-sekme IA'ya güncelle (eski sekme adlarını düzelt).
5. `doc/roles/ogrenci.md`'de öğrenci alt-nav / sayfa listesini 5-sekme IA'ya göre güncelle.
6. Güncellenen her dosyanın alt "Güncelleme: …" tarihini `2026-07-21` yap.

- [ ] **Step 11: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ docs/ && \
git commit -m "feat(mobile): öğrenci 5-sekme nav iskeleti — enum + router + redirect (Task 1)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Task 2: ⏱️ Çalış — sayaç üste + kısayol düzeni

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart`
- Docs: `doc/pages/study_student.md`, `doc/roles/ogrenci_ux.md`

**Interfaces:**
- Consumes: Task 1 `StudentNavTab.work`, mevcut `_PrimaryActionCard`, `_ActionGrid`, `StudyDashboard` (`todayEffectiveMinutes`, `weekEffectiveMinutes`).
- Produces: (davranışsal) Çalış açılışında **büyük sayaç/başlat kartı en üstte**; ikincil kısayollar `Hedefler · Rozetler · Manuel Ekle`.

**Not:** "bugün/bu hafta özeti" zaten mevcut (hero halkası = bugün; "Bu hafta" `_StatTile`). Bu görev yalnız (a) sayacı üste taşıyıp büyütür, (b) kısayolları IA'ya göre sadeleştirir. Detay analiz (haftalık çubuklar, ders→konu kırılımı) Task 3'te Performans'a taşınır.

- [ ] **Step 1: Sayaç/başlat kartını en üste al**

`_StudentHomeView.build` içindeki `ListView` `children`'ında, `AppPageHeader` (karşılama) **öncesine** birincil sayaç CTA'sını taşı. `_ActionGrid` içindeki mevcut `_PrimaryActionCard` (Kronometre) tek birincil CTA olduğundan, onu grid'den çıkarıp ListView'in ilk elemanı yap. `children` başını şöyle düzenle:

```dart
                children: <Widget>[
                  // 0) Birincil eylem: büyük sayaç/başlat kartı en üstte (0 tık ilkesi).
                  _PrimaryActionCard(
                    icon: Icons.timer_rounded,
                    label: 'Çalışmaya Başla',
                    subtitle: 'Kronometreyi başlat, serini büyüt',
                    onTap: () =>
                        context.push('/study/timer?studentId=$studentId'),
                  ),
                  const SizedBox(height: 18),
                  // 1) Karşılama — pozitif, güne özel motivasyon (ux §5)
                  AppPageHeader(
                    title: greeting,
                    subtitle: _motivationSubtitle(d),
                  ),
                  const SizedBox(height: 18),
                  _HeroSummary(
```

(Geri kalan hero/plan/İlerlemen/geçmiş blokları değişmez.)

- [ ] **Step 2: `_ActionGrid` kısayollarını IA'ya göre sadeleştir**

`_ActionGrid.build` içinde birincil CTA artık üstte olduğundan onu grid'den kaldır ve `secondary` listesini Çalış IA'sına göre değiştir (`Deneme Gir`/`Geçmiş` → Performans'a ait; burada `Hedefler · Rozetler · Manuel Ekle`). `_ActionGrid.build` gövdesini şununla değiştir:

```dart
  @override
  Widget build(BuildContext context) {
    // Çalış sekmesi kısayolları (IA: Hedefler · Rozetler · Manuel seans ekle).
    // Birincil sayaç CTA'sı sayfanın en üstündedir.
    final secondary = <_ActionItem>[
      _ActionItem('Hedefler', Icons.flag_rounded, AppColors.accentGreen,
          '/study/goals'),
      _ActionItem('Rozetler', Icons.emoji_events_rounded,
          AppColors.accentOrange, '/study/achievements'),
      _ActionItem('Manuel Ekle', Icons.add_circle_rounded,
          AppColors.accentBlue, '/study/history'),
    ];
    final halfWidth = (MediaQuery.of(context).size.width - 44) / 2;
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: <Widget>[
        for (final a in secondary)
          SizedBox(
            width: halfWidth,
            child: _ActionTile(
              action: a,
              onTap: () => _go(context, a.route),
            ),
          ),
      ],
    );
  }
```

(`_PrimaryActionCard` sınıfı Step 1'de kullanıldığı için dosyada **kalır**; silme.)

- [ ] **Step 3: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata/uyarı yok (kullanılmayan import/sınıf uyarısı çıkarsa temizle).

- [ ] **Step 4: Manuel duman testi (opsiyonel, ortam varsa)**

Uygulamayı çalıştırıp öğrenci ile Çalış sekmesini aç: en üstte büyük "Çalışmaya Başla" kartı görünür; altında karşılama + hero + plan + kısayollar (Hedefler/Rozetler/Manuel Ekle). Ortam yoksa bu adımı atla, `flutter analyze` yeterli kapıdır.

- [ ] **Step 5: Dokümanları güncelle**

`doc/pages/study_student.md` `/student-home` satırını güncelle: "**büyük sayaç/başlat kartı en üstte** (0 tık); ikincil kısayollar Hedefler·Rozetler·Manuel Ekle". Alt tarih → `2026-07-21`. Gerekiyorsa `doc/roles/ogrenci_ux.md` Çalış açıklamasını hizala.

- [ ] **Step 6: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ && \
git commit -m "feat(mobile): Çalış sekmesi — sayaç üste + kısayol sadeleştirme (Task 2)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Task 3: 📊 Performans — Testler'i genişlet + detay analizi absorbe + Çalışmalarım'ı emekliye ayır

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_tests_page.dart`
- Delete: `mobile/lib/features/study/presentation/pages/student_studies_page.dart`
- Modify: `mobile/lib/core/routing/app_router.dart` (`/student/studies` → redirect, import kaldır)
- Docs: `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci_ux.md`, `doc/roles/ogrenci.md`

**Interfaces:**
- Consumes: `StudyRepository.listTests` (mevcut) + yeni `getWeeklySummary`, `listSessions`; `StudentNavTab.performance`.
- Produces: Performans sayfası — başlık "Performans"; net trendi/ders bazlı net (mevcut) + **Haftalık analiz** (çubuk grafik) + **Ders → konu kırılımı** + **Analiz & Gelişim** girişleri (`/study/history`, `/student/progress`).

**Not:** Haftalık çubuklar + ders→konu kırılımı widget'ları `student_studies_page.dart`'tan Performans sayfasına taşınır (studies silineceği için kopya kalmaz). `/study/history` ayrıca detaylı haftalık/manuel içerir → oraya link verilir; `/student/progress` = Gelişimim (konu hâkimiyeti).

- [ ] **Step 1: Studies'ten taşınacak widget'ları Performans'a ekle**

`student_tests_page.dart` sonuna, `student_studies_page.dart` içindeki şu tanımları **birebir kopyala**: `_aggregateLessons` fonksiyonu + `_LessonStat` + `_TopicStat` + `_LessonBreakdown` + `_LessonRow` + `_LessonRowState` + `_TopicList` sınıfları, ve `_WeeklyBars` sınıfı. (Bu widget'lar `StudyCard`, `AppColors`, `StudyFormat`, `WeeklySummary`, `DayMinutes`, `StudySession` kullanır — hepsi `student_tests_page.dart`'ta zaten import'lu; eksikse `study_tab_widgets.dart`/`study_contracts.dart` import'ları zaten var.)

- [ ] **Step 2: Fetch'e haftalık özet + seanslar ekle**

`_StudentTestsPageState` alanlarına ekle:

```dart
  WeeklySummary? _weekly;
  List<StudySession> _sessions = const <StudySession>[];
```

`_load` içinde `final tests = await _repo.listTests(studentId);` satırından sonra ekle:

```dart
      final weekly = await _repo.getWeeklySummary(studentId);
      final sessions = await _repo.listSessions(studentId);
```

ve `setState` bloğuna ekle:

```dart
        _weekly = weekly;
        _sessions =
            sessions.where((StudySession s) => s.status == 'Completed').toList();
```

- [ ] **Step 3: Başlığı "Performans" yap + analiz bloklarını yerleştir**

`_content` içindeki `AppPageHeader` başlığını değiştir:

```dart
          const AppPageHeader(
            title: 'Performans',
            subtitle: 'Net gelişimini, çalışma analizini ve eksik konuları gör.',
          ),
```

`else ...<Widget>[` dalının sonuna (mevcut "Son denemeler" listesinden sonra) ekle:

```dart
            if (_weekly != null) ...<Widget>[
              const SizedBox(height: 24),
              const StudySectionHeader(title: 'Haftalık analiz'),
              const SizedBox(height: 12),
              _WeeklyBars(weekly: _weekly!),
            ],
            if (_sessions.isNotEmpty) ...<Widget>[
              const SizedBox(height: 24),
              const StudySectionHeader(title: 'Ders → konu kırılımı'),
              const SizedBox(height: 4),
              const Text('Tüm zamanlar · derse ve konuya göre süre',
                  style: TextStyle(color: AppColors.textSecondary, fontSize: 12)),
              const SizedBox(height: 12),
              _LessonBreakdown(lessons: _aggregateLessons(_sessions)),
            ],
```

- [ ] **Step 4: "Analiz & Gelişim" girişlerini ekle (Detaylı analiz + Gelişimim)**

`_content` `ListView` `children`'ının sonuna (hem boş hem dolu durumda görünür şekilde, `if/else` bloğunun **dışına**, en sona) ekle:

```dart
          const SizedBox(height: 24),
          const StudySectionHeader(title: 'Analiz & Gelişim'),
          const SizedBox(height: 12),
          _PerfLink(
            icon: Icons.query_stats_rounded,
            color: AppColors.accentTeal,
            title: 'Detaylı analiz',
            subtitle: 'Haftalık geçmiş, manuel seanslar',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) context.push('/study/history?studentId=$id');
            },
          ),
          const SizedBox(height: 12),
          _PerfLink(
            icon: Icons.insights_rounded,
            color: AppColors.accentBlue,
            title: 'Gelişimim',
            subtitle: 'Konu bazlı hâkimiyet, eksik/güçlü konular',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) context.push('/student/progress?studentId=$id');
            },
          ),
```

Dosya sonuna `_PerfLink` widget'ını ekle:

```dart
/// Performans sekmesi giriş satırı — tonlu ikon + başlık/alt + ok.
class _PerfLink extends StatelessWidget {
  const _PerfLink({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: StudyCard(
        child: Row(
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Icon(icon, color: color, size: 22),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(title,
                      style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary)),
                  const SizedBox(height: 2),
                  Text(subtitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          color: AppColors.textSecondary, fontSize: 12)),
                ],
              ),
            ),
            const Icon(Icons.chevron_right_rounded,
                color: AppColors.textSecondary),
          ],
        ),
      ),
    );
  }
}
```

- [ ] **Step 5: Çalışmalarım sayfasını sil + rotayı redirect yap**

Sil: `mobile/lib/features/study/presentation/pages/student_studies_page.dart`.

`app_router.dart`'ta `student_studies_page.dart` import satırını kaldır. `/student/studies` GoRoute'unu redirect'e çevir:

```dart
        GoRoute(
          path: '/student/studies',
          redirect: (context, state) => '/student-home',
        ),
```

- [ ] **Step 6: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok. `StudentStudiesPage` referansı kalmadığından derleme temiz olmalı. (Kullanılmayan import kalırsa temizle.)

Run: `cd mobile && grep -rn "StudentStudiesPage\|student_studies_page" lib/ test/`
Beklenen: çıktı boş.

- [ ] **Step 7: Dokümanları güncelle**

- `doc/pages/study_student.md`: `/student/tests` satırını `/student/performance` (Testler → **Performans**) olarak güncelle; içeriğe "haftalık analiz + ders→konu kırılımı + Detaylı analiz/Gelişimim girişleri" ekle. `/student/studies` satırını **kaldır**.
- `doc/pages/00_pages_index.md`: `student_studies_page.dart` satırını kaldır.
- `doc/roles/ogrenci_ux.md` §7/§8: Çalışmalarım'ın Performans'a taşındığını yansıt.
- `doc/roles/ogrenci.md`: sekme adı/işlev güncelle.
- Alt tarihler → `2026-07-21`.

- [ ] **Step 8: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ && \
git commit -m "feat(mobile): Performans sekmesi — Testler genişler, Çalışmalarım kaldırıldı (Task 3)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Task 4: 📚 Derslerim — Takvim'e ders araçları girişleri ekle

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart`
- Docs: `doc/pages/study_student.md`, `doc/roles/ogrenci_ux.md`, `doc/roles/ogrenci.md`

**Interfaces:**
- Consumes: `StudentNavTab.lessons`, mevcut `_studentId`, korunan rotalar `/study/catalog`, `/student/assignments`, `/student/teacher`, `/study/notes`.
- Produces: Derslerim sayfası — takvim + seçili gün listesi (mevcut) altında **"Ders araçları"** bölümü (Dersler & Konular · Ödevlerim · Öğretmenlerim · Notlarım).

- [ ] **Step 1: Başlığı "Derslerim" yap**

`_content` içindeki `AppPageHeader`:

```dart
          const AppPageHeader(
            title: 'Derslerim',
            subtitle: 'Program, kendi derslerin ve ders araçların.',
          ),
```

- [ ] **Step 2: "Ders araçları" bölümünü ekle**

`_content` `ListView` `children`'ının sonuna (`_SelectedDayPanel`'den sonra) ekle:

```dart
          const SizedBox(height: 20),
          const Text(
            'Ders araçları',
            style: TextStyle(
                fontWeight: FontWeight.w800,
                color: AppColors.textPrimary,
                fontSize: 16),
          ),
          const SizedBox(height: 12),
          _LessonToolTile(
            icon: Icons.menu_book_rounded,
            color: AppColors.accentTeal,
            title: 'Dersler & Konular',
            subtitle: 'Çalıştığın ders ve konuları yönet',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) context.push('/study/catalog?studentId=$id');
            },
          ),
          _LessonToolTile(
            icon: Icons.assignment_turned_in_rounded,
            color: AppColors.accentBlue,
            title: 'Ödevlerim',
            subtitle: 'Öğretmen ödevlerini yükle ve tamamla',
            onTap: () => context.push('/student/assignments'),
          ),
          _LessonToolTile(
            icon: Icons.school_rounded,
            color: AppColors.primary,
            title: 'Öğretmenlerim',
            subtitle: 'Bağlı öğretmenlerin ve bilgileri',
            onTap: () => context.push('/student/teacher'),
          ),
          _LessonToolTile(
            icon: Icons.sticky_note_2_rounded,
            color: AppColors.accentOrange,
            title: 'Notlarım',
            subtitle: 'Kendi ders notlarını ekle ve düzenle',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) context.push('/study/notes?studentId=$id');
            },
          ),
```

`context.push` için import ekle (dosyada `go_router` yoksa): `student_calendar_page.dart` şu an `go_router` import etmiyor olabilir — kontrol et; yoksa ekle:

```dart
import 'package:go_router/go_router.dart';
```

- [ ] **Step 3: `_LessonToolTile` widget'ını ekle**

Dosya sonuna ekle:

```dart
/// Derslerim sekmesi ders-araçları giriş kartı (tonlu ikon + başlık/alt + ok).
class _LessonToolTile extends StatelessWidget {
  const _LessonToolTile({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            children: <Widget>[
              Container(
                width: 44,
                height: 44,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(icon, color: color, size: 22),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(title,
                        style: const TextStyle(
                            fontWeight: FontWeight.w800,
                            color: AppColors.textPrimary)),
                    const SizedBox(height: 2),
                    Text(subtitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                            color: AppColors.textSecondary, fontSize: 12)),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right_rounded,
                  color: AppColors.textSecondary),
            ],
          ),
        ),
      ),
    );
  }
}
```

- [ ] **Step 4: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok.

- [ ] **Step 5: Dokümanları güncelle**

`doc/pages/study_student.md` `/student/calendar` satırını `/student/lessons` (Takvim → **Derslerim**) olarak güncelle; içeriğe "Ders araçları: Dersler&Konular · Ödevlerim · Öğretmenlerim · Notlarım" ekle. `doc/roles/ogrenci_ux.md` §9 + `doc/roles/ogrenci.md` hizala. Alt tarihler → `2026-07-21`.

- [ ] **Step 6: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ && \
git commit -m "feat(mobile): Derslerim sekmesi — takvim + ders araçları girişleri (Task 4)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Task 5: 🔍 Keşfet — yer-tutucuyu tasarımlı Faz-4 boş durumuna yükselt

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_discover_page.dart`
- Create: `mobile/test/features/study/presentation/pages/student_discover_page_test.dart`
- Docs: `doc/pages/study_student.md`

**Interfaces:**
- Consumes: `StudentNavTab.discover`, `AppPageHeader`, `AppColors`.
- Produces: Keşfet — devre dışı arama kutusu görünümü + devre dışı filtre çipleri + belirgin "Bu özellik yakında (Faz 4)" boş durumu. İşlevsel arama **yok**.

- [ ] **Step 1: Render testini yaz (failing)**

Create `mobile/test/features/study/presentation/pages/student_discover_page_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_discover_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('Keşfet yer tutucu: başlık + Faz 4 boş durumu + devre dışı çipler',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(home: StudentDiscoverPage()));
    await tester.pump();

    expect(find.text('Keşfet'), findsOneWidget);
    expect(find.textContaining('Faz 4'), findsWidgets);
    // Devre dışı filtre çiplerinden en az biri.
    expect(find.text('Branş'), findsOneWidget);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/pages/student_discover_page_test.dart`
Beklenen: FAIL — 'Branş' / 'Faz 4' metinleri henüz yok.

- [ ] **Step 3: Yer-tutucuyu tasarımlı hâle getir**

`student_discover_page.dart` `build` içindeki `ListView` `children`'ını değiştir (import bloğu + sınıf iskeleti Task 1'den aynı kalır):

```dart
          children: <Widget>[
            const AppPageHeader(
              title: 'Keşfet',
              subtitle: 'Sana uygun öğretmeni bul.',
            ),
            const SizedBox(height: 16),
            // Devre dışı arama kutusu görünümü (işlevsel değil).
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                children: const <Widget>[
                  Icon(Icons.search_rounded, color: AppColors.textMuted),
                  SizedBox(width: 10),
                  Text('Öğretmen ara…',
                      style: TextStyle(color: AppColors.textMuted)),
                ],
              ),
            ),
            const SizedBox(height: 12),
            // Devre dışı filtre çipleri.
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: <Widget>[
                for (final label in const <String>[
                  'Branş',
                  'Şehir',
                  'Ücret',
                  'Şekil',
                  'Saat',
                ])
                  Chip(
                    label: Text(label),
                    backgroundColor: AppColors.background,
                    side: const BorderSide(color: AppColors.border),
                    labelStyle: const TextStyle(color: AppColors.textMuted),
                  ),
              ],
            ),
            const SizedBox(height: 40),
            // Belirgin "yakında" boş durumu.
            Column(
              children: const <Widget>[
                Icon(Icons.travel_explore_rounded,
                    size: 56, color: AppColors.primary),
                SizedBox(height: 14),
                Text('Bu özellik yakında (Faz 4)',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                        fontSize: 16)),
                SizedBox(height: 6),
                Text(
                  'Öğretmen arama ve keşfi yakında burada olacak. '
                  'Şimdilik davet koduyla öğretmenine bağlanabilirsin.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: AppColors.textSecondary),
                ),
              ],
            ),
          ],
```

- [ ] **Step 4: Testin geçtiğini gör**

Run: `cd mobile && flutter test test/features/study/presentation/pages/student_discover_page_test.dart`
Beklenen: PASS.

- [ ] **Step 5: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok.

- [ ] **Step 6: Dokümanları güncelle**

`doc/pages/study_student.md` `/student/discover` satırını tasarımlı yer tutucu (arama kutusu görünümü + devre dışı çipler + Faz 4 boş durumu) olarak güncelle. Alt tarih → `2026-07-21`.

- [ ] **Step 7: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ && \
git commit -m "feat(mobile): Keşfet sekmesi — tasarımlı Faz-4 yer tutucu (Task 5)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Task 6: 👤 Profil — Diğer kalanını absorbe et + "Diğer"i emekliye ayır

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_profile_page.dart`
- Delete: `mobile/lib/features/study/presentation/pages/student_more_page.dart`
- Modify: `mobile/lib/core/routing/app_router.dart` (`/student/more` → redirect, import kaldır)
- Docs: `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci_ux.md`, `doc/roles/ogrenci.md`

**Interfaces:**
- Consumes: `StudentNavTab.profile` (Task 1), `AuthCubit.logout()`, korunan rotalar `/study/goals`, `/account-info`.
- Produces: Profil — istatistik özeti (mevcut) + **Ayarlar menüsü** (Velim · ⭐ Gizlilik ayarları · Bildirim ayarları [yakında] · Abonelik [Faz 5, yakında] · Ayarlar & Güvenlik) + **Çıkış yap** (bottom sheet). AppBar yerine sayfa içi başlık.

**Not:** Diğer'in kalan girdileri (Rozetler/Hedefler→Çalış, Öğretmenlerim/Katalog/Ödevlerim/Notlarım→Derslerim, Gelişimim→Performans) Task 2-4'te yerleştirildi. Bu görev yalnız Profil'e ait olanları (Gizlilik/Hesap/Velim/Bildirim/Abonelik/Çıkış) ekler, sonra `student_more_page`'i siler.

- [ ] **Step 1: AppBar'ı sayfa içi başlığa çevir**

`student_profile_page.dart` importlarına ekle:

```dart
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
```

`build`'te `appBar`'ı kaldır ve `body`'i `SafeArea` ile sar (bottomNav Task 1'den zaten var):

```dart
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Profilin yükleniyor...')
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : _content(),
      ),
      bottomNavigationBar:
          const StudentBottomNav(current: StudentNavTab.profile),
    );
```

`_content` `ListView`'inin başına başlık ekle (mevcut `_ProfileHeader`'dan önce):

```dart
          const AppPageHeader(
            title: 'Profil',
            subtitle: 'Bilgilerin, ayarların ve istatistiklerin.',
          ),
          const SizedBox(height: 16),
```

- [ ] **Step 2: Ayarlar menüsü + çıkışı ekle**

`_content` `ListView` `children`'ının sonuna (rozet özetinden sonra) ekle:

```dart
          const SizedBox(height: 24),
          const StudySectionHeader(title: 'Ayarlar'),
          const SizedBox(height: 12),
          _ProfileMenuTile(
            icon: Icons.family_restroom_rounded,
            color: AppColors.accentBlue,
            title: 'Velim',
            subtitle: 'Veli bağlantısı ve paylaşım',
            onTap: () => context.push('/study/goals?studentId=$studentId'),
          ),
          _ProfileMenuTile(
            icon: Icons.shield_rounded,
            color: AppColors.accentGreen,
            title: 'Gizlilik ayarları',
            subtitle: 'Veli/öğretmen paylaşım izinleri',
            onTap: () => context.push('/study/goals?studentId=$studentId'),
          ),
          _ProfileMenuTile(
            icon: Icons.notifications_rounded,
            color: AppColors.accentOrange,
            title: 'Bildirim ayarları',
            subtitle: 'Yakında',
            onTap: null,
          ),
          _ProfileMenuTile(
            icon: Icons.workspace_premium_rounded,
            color: AppColors.primary,
            title: 'Abonelik',
            subtitle: 'Faz 5 — yakında',
            onTap: null,
          ),
          _ProfileMenuTile(
            icon: Icons.manage_accounts_outlined,
            color: AppColors.accentTeal,
            title: 'Ayarlar & Güvenlik',
            subtitle: 'E-posta, rol ve oturum',
            onTap: () => context.push('/account-info'),
          ),
          const SizedBox(height: 12),
          _ProfileMenuTile(
            icon: Icons.logout_rounded,
            color: AppColors.accentRed,
            title: 'Çıkış yap',
            subtitle: 'Oturumu kapat ve giriş ekranına dön',
            titleColor: AppColors.accentRed,
            onTap: () => _confirmLogout(context),
          ),
```

- [ ] **Step 3: Çıkış onay sheet'ini + menü tile'ını ekle**

`_StudentProfilePageState` içine `_confirmLogout` metodunu ekle (kaynağı `student_more_page.dart`'taki `_confirmLogout`; `context` parametreli sürüm):

```dart
  void _confirmLogout(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (BuildContext sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text('Çıkış yap',
                  style: Theme.of(sheetContext).textTheme.titleLarge?.copyWith(
                      color: AppColors.primary, fontWeight: FontWeight.w800)),
              const SizedBox(height: 10),
              Text('Oturumunu kapatmak istediğine emin misin?',
                  style: Theme.of(sheetContext).textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary)),
              const SizedBox(height: 18),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(sheetContext).pop(),
                      child: const Text('Vazgeç'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                          backgroundColor: AppColors.accentRed),
                      onPressed: () {
                        Navigator.of(sheetContext).pop();
                        context.read<AuthCubit>().logout();
                      },
                      child: const Text('Çıkış yap'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }
```

Dosya sonuna `_ProfileMenuTile` ekle (`onTap` null olduğunda pasif görünür):

```dart
/// Profil ayarlar menüsü satırı; onTap null ise pasif (yakında) görünür.
class _ProfileMenuTile extends StatelessWidget {
  const _ProfileMenuTile({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.titleColor,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;
  final Color? titleColor;

  @override
  Widget build(BuildContext context) {
    final bool enabled = onTap != null;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Opacity(
          opacity: enabled ? 1 : 0.55,
          child: Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: AppColors.border),
            ),
            child: Row(
              children: <Widget>[
                Container(
                  width: 44,
                  height: 44,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Icon(icon, color: color, size: 22),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(title,
                          style: TextStyle(
                              fontWeight: FontWeight.w800,
                              color: titleColor ?? AppColors.textPrimary)),
                      const SizedBox(height: 2),
                      Text(subtitle,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                              color: AppColors.textSecondary, fontSize: 12)),
                    ],
                  ),
                ),
                if (enabled)
                  const Icon(Icons.chevron_right_rounded,
                      color: AppColors.textSecondary),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
```

`flutter_bloc` (`context.read<AuthCubit>()`) importunun dosyada olduğunu doğrula; yoksa ekle:

```dart
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
```

- [ ] **Step 4: "Diğer" sayfasını sil + rotayı redirect yap**

Sil: `mobile/lib/features/study/presentation/pages/student_more_page.dart`.

`app_router.dart`'ta `student_more_page.dart` import satırını kaldır. `/student/more` GoRoute'unu redirect'e çevir:

```dart
        GoRoute(
          path: '/student/more',
          redirect: (context, state) => '/student/profile',
        ),
```

- [ ] **Step 5: Analiz temiz mi doğrula**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok.

Run: `cd mobile && grep -rn "StudentMorePage\|student_more_page" lib/ test/`
Beklenen: çıktı boş.

- [ ] **Step 6: Enum testinin hâlâ geçtiğini doğrula**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/student_nav_tab_test.dart test/features/study/presentation/pages/student_discover_page_test.dart`
Beklenen: PASS.

- [ ] **Step 7: Dokümanları güncelle**

- `doc/pages/study_student.md`: `/student/profile` satırını "tab + istatistik + Ayarlar menüsü (Velim/Gizlilik/Bildirim/Abonelik/Ayarlar&Güvenlik) + Çıkış" olarak güncelle; `/student/more` satırını **kaldır**.
- `doc/pages/00_pages_index.md`: `student_more_page.dart` satırını kaldır.
- `doc/roles/ogrenci_ux.md` §4/§11 + `doc/roles/ogrenci.md`: "Diğer" hub'ının kaldırıldığını, dağıtımın son hâlini yansıt.
- Alt tarihler → `2026-07-21`.

- [ ] **Step 8: Commit**

```bash
cd mobile && git add -A && cd .. && git add doc/ && \
git commit -m "feat(mobile): Profil sekmesi — Diğer dağıtıldı ve kaldırıldı (Task 6)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Xiv3mjMTmm2Gh9i6WSozbf"
```

---

## Kapanış Doğrulaması (tüm görevlerden sonra)

- [ ] **Analiz:** `cd mobile && flutter analyze` → yeni hata yok.
- [ ] **Saf testler:** `cd mobile && flutter test test/features/study/presentation/` → yeni yazılan enum + discover testleri geçer. (Not: paketin geri kalanında önceden bozuk 6 test auth-fake nedeniyle başarısız olabilir — kapsam dışı; yeni testler bunlardan bağımsızdır.)
- [ ] **Orphan referans yok:** `cd mobile && grep -rn "StudentStudiesPage\|StudentMorePage\|StudentNavTab.home\|StudentNavTab.studies\|StudentNavTab.tests\|StudentNavTab.calendar\|StudentNavTab.more" lib/ test/` → boş.
- [ ] **Redirect duman testi (ortam varsa):** öğrenci oturumuyla `/student/tests`, `/student/calendar`, `/student/studies`, `/student/more` derin linkleri sırasıyla `/student/performance`, `/student/lessons`, `/student-home`, `/student/profile`'a yönlenir.
- [ ] **Manuel gezinme (ortam varsa):** 5 sekme geçişi sorunsuz; eski "Diğer" girdileri yeni yerlerinden erişilebilir; Keşfet "yakında" görünür.
- [ ] **finishing-a-development-branch** skill'i ile dalın entegrasyonu (merge/PR) kullanıcıya sunulur.

---

## Öz-İnceleme (spec kapsamı)

- Spec §3.1 yeni alt-nav (5 sekme + ikon + rota) → Task 1. ✓
- Spec §3.1 eski rota redirect'leri → Task 1 (tests/calendar), Task 3 (studies), Task 6 (more). ✓
- Spec §3.2/1 Çalış (sayaç üste + büyüt + kısayollar) → Task 2. ✓
- Spec §3.2/2 Performans (Testler genişler + haftalık/aylık analiz + rekorlar + Gelişimim) → Task 3 (haftalık çubuklar + ders→konu kırılımı taşındı; Detaylı analiz `/study/history` + Gelişimim `/student/progress` linkleri; rekorlar mevcut "en iyi net" korunur). ✓
- Spec §3.2/3 Derslerim (takvim + katalog/ödev/öğretmen/notlar + Öğretmen Bul→Keşfet) → Task 4 (ders araçları girişleri). *Öğretmen-yok→Keşfet ikincil eylemi Derslerim'e opsiyonel; ders araçlarında Öğretmenlerim mevcut — Keşfet CTA'sı Keşfet sekmesi zaten nav'da olduğundan tekrar edilmedi; gerekirse Task 4'te eklenebilir.* ✓
- Spec §3.2/4 Keşfet (arama kutusu + devre dışı çipler + Faz-4 boş durum) → Task 1 (stub) + Task 5 (tasarımlı). ✓
- Spec §3.2/5 Profil (istatistik + Velim + Gizlilik + Bildirim + Abonelik + Ayarlar&Güvenlik + Çıkış) → Task 1 (tab) + Task 6 (menü). ✓
- Spec §3.3 "Diğer" dağıtımı: Rozetler/Hedefler→Çalış (Task 2), Öğretmenlerim/Katalog/Ödevlerim/Notlarım→Derslerim (Task 4), Gelişimim→Performans (Task 3), Gizlilik/Hesap/Çıkış→Profil (Task 6). ✓
- Spec §5 doküman bakımı → her görevin son adımları. ✓
- Spec §6 test → enum + discover saf testleri; içerik sayfaları `flutter analyze` + manuel (bozuk auth-fake kapsam dışı). ✓
- Backend/domain değişmez kısıtı → yalnız `presentation/` + `app_router.dart` dosyaları. ✓
