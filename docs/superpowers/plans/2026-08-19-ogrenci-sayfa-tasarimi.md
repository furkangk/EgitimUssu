# Öğrenci Rolü 4-Sekme Sayfa Tasarımı · Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğrenci alt-navigasyonunu 4 sekmeye (Çalışma · Derslerim · Performans · Profil) indirip Keşfet'i kaldırmak; Çalışma'yı dashboard'a çevirip büyük sayacı ayrı Kronometre'ye taşımak; Derslerim'e liste/takvim + kendi/öğretmen ayrımı eklemek; yeni Ders Detayı sayfası açmak; Performans ve Profil'i brief'e göre zenginleştirmek. Yalnız mobil sunum katmanı.

**Architecture:** Salt **mobil sunum** (`mobile/lib/features/study/presentation/**`, `features/assignments`, `core/routing`). `StudentNavTab` 4 sekmeye indirilir. Ortak kart dili `study_tab_widgets.dart`'ta birleştirilir (yeni: demo rozeti, ikon-chip, pressable, hızlı erişim kartı, ilerleme barı). Her sekme bu ortak set üzerine kurulur. Backend/domain/endpoint/repository **değişmez**; eksik veriler açık **"demo" rozetiyle** yerel/statik gösterilir.

**Tech Stack:** Flutter · `go_router` · `flutter_bloc`/Cubit · `get_it` (`injector`) · Syncfusion `SfCalendar` (mevcut) · mevcut `study`/`scheduling`/`assignments` feature'ları · repository'ler `StudyRepository`/`SchedulingRepository`/`TeacherRepository`/`AssignmentRepository` (`injector<...>()`), öğrenci kimliği `StudentScope.resolve(userId, fullName)`.

## Global Constraints

- **Görünen ad:** EğitimÜssü · **kod tanımlayıcı:** EgitimUssu (Türkçe karaktersiz).
- **Backend/domain/endpoint/repository değişmez.** Yalnız `mobile/lib/features/study/presentation/**`, `mobile/lib/features/assignments/presentation/**`, `mobile/lib/core/routing/app_router.dart`.
- **Eksik backend = demo.** Backend'i olmayan veri/eylemler yerel/statik gösterilir ve `StudyDemoBadge` (Task 2) ile "demo" işaretlenir. Backend'i olan akışlar (seans başlat/bitir, ders ekle, deneme gir, ödev teslim, streak, dashboard, achievements, notlar) gerçek repository'yi kullanır.
- **Ders Detayı yetkisi:** öğretmen dersinde (`teacherUserId != null`) öğretmenin ödev/konusu **salt görüntüleme**, ekle gizli; not/test/deneme öğrencinin kendi eklemesi açık. Kendi dersinde (`teacherUserId == null`) tümü açık.
- **Kart dili:** Yeni renk/spacing/radius **doğrudan yazılmaz**; `AppColors`/`AppShadows.soft` + mevcut token değerleri kullanılır (kart radius 18, `skyBorder`, `AppShadows.soft`).
- **Test gerçeği:** Öğrenci içerik sayfaları `initState`'te `StudentScope.resolve` ile ağ çağırır; mobil test paketi auth-fake nedeniyle önceden bozuktur (kapsam dışı). Bu yüzden **her görevin derleme kapısı `flutter analyze` (yeni hata yok)**; ek olarak yalnız **ağ/auth bağımsız saf birim/widget testleri** yazılır (enum sözleşmesi, saf yardımcı fonksiyonlar, stateless widget render'ı).
- **Komut dizini:** Tüm `flutter`/`git` komutları `mobile/` altından; `git add` proje kökünden göreli yollarla.
- **Doküman bakımı (CLAUDE.md):** İlgili görevin son adımı `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci.md`, `doc/roles/ogrenci_ux.md`, `doc/architecture/{mobile_flutter,widgets}.md`'yi **aynı görevde** günceller; güncellenen dokümanın alt tarihini `2026-08-19` yapar.

---

## Dosya Yapısı (kim neyden sorumlu)

- `features/study/presentation/widgets/student_bottom_nav.dart` — `StudentNavTab` (4 sekme). **Değişir** (Task 1).
- `core/routing/app_router.dart` — Keşfet builder kaldır + `/student/discover`→`/student/lessons` redirect + `/student/lessons/:id` (Ders Detayı). **Değişir** (Task 1 + Task 5).
- `features/study/presentation/widgets/study_tab_widgets.dart` — ortak kart dili; **eklenir** `StudyDemoBadge`, `StudyIconChip`, `StudyPressable`, `StudyQuickAccessCard`, `StudyProgressBar`, `StudyOwnershipBadge`. **Değişir** (Task 2).
- `features/study/presentation/pages/student_home_page.dart` — Çalışma dashboard. **Değişir** (Task 3).
- `features/study/presentation/pages/study_timer_page.dart` — Kronometre (form + Molada + manuel demo). **Değişir** (Task 4).
- `features/study/presentation/timer/manual_session_store.dart` — manuel seans demo/yerel deposu + `TimerAccumulator` değer nesnesi. **Yeni** (Task 4).
- `features/study/presentation/pages/student_lesson_detail_page.dart` — Ders Detayı. **Yeni** (Task 5).
- `features/study/presentation/lessons/lesson_detail_permissions.dart` — yetki saf fonksiyonu. **Yeni** (Task 5).
- `features/study/presentation/pages/student_calendar_page.dart` — Derslerim (liste/takvim + ayrım). **Değişir** (Task 6).
- `features/study/presentation/lessons/lesson_ownership.dart` — kendi/öğretmen ayrım + gruplama saf fonksiyonu. **Yeni** (Task 6).
- `features/study/presentation/pages/student_tests_page.dart` — Performans. **Değişir** (Task 7).
- `features/study/presentation/performance/personal_records.dart` — kişisel rekor + zayıf konu saf fonksiyonları. **Yeni** (Task 7).
- `features/study/presentation/pages/student_profile_page.dart` — Profil. **Değişir** (Task 8).
- `features/study/presentation/pages/student_discover_page.dart` — **silinir** (Task 1).

**Sekme sözleşmesi (tüm görevlerde tutarlı):**

| Enum üyesi | İkon | Etiket | Rota | Sayfa |
|---|---|---|---|---|
| `work` | `Icons.rocket_launch_rounded` | `Çalışma` | `/student-home` | `StudentHomePage` |
| `lessons` | `Icons.menu_book_rounded` | `Derslerim` | `/student/lessons` | `StudentCalendarPage` |
| `performance` | `Icons.insights_rounded` | `Performans` | `/student/performance` | `StudentTestsPage` |
| `profile` | `Icons.person_rounded` | `Profil` | `/student/profile` | `StudentProfilePage` |
| `none` | `Icons.circle` | `''` | `''` | (dormant) |

---

## Task 1: Navigasyon revizyonu — 4 sekme + Keşfet kaldır

**Files:**
- Modify: `mobile/lib/features/study/presentation/widgets/student_bottom_nav.dart`
- Modify: `mobile/lib/core/routing/app_router.dart`
- Delete: `mobile/lib/features/study/presentation/pages/student_discover_page.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart` (yalnız `current:` satırı, gerekiyorsa)
- Create: `mobile/test/features/study/presentation/widgets/student_nav_tab_test.dart`
- Docs: `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`, `doc/roles/ogrenci_ux.md`

**Interfaces:**
- Produces: `StudentNavTab { work, lessons, performance, profile, none }`, her üye `(IconData icon, String label, String route)`; `StudentBottomNav({required StudentNavTab current})` (görsel gövde değişmez, yalnız `_tabs` listesi 4 üye).
- Consumes: mevcut `StudentBottomNav` build gövdesi; `StudentCalendarPage/StudentTestsPage/StudentProfilePage/StudentHomePage` const kurucuları.

- [ ] **Step 0: Analiz baseline**

Run: `cd mobile && flutter analyze`
Beklenen: mevcut hata sayısını not al (sonraki adımlarda artmamalı).

- [ ] **Step 1: Enum sözleşme testini yaz (failing)**

Create `mobile/test/features/study/presentation/widgets/student_nav_tab_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('StudentNavTab 4 görünür sekme; sıra ve rotalar 4-sekme IA ile uyumlu', () {
    const expected = <(StudentNavTab, String, String)>[
      (StudentNavTab.work, 'Çalışma', '/student-home'),
      (StudentNavTab.lessons, 'Derslerim', '/student/lessons'),
      (StudentNavTab.performance, 'Performans', '/student/performance'),
      (StudentNavTab.profile, 'Profil', '/student/profile'),
    ];
    for (final (tab, label, route) in expected) {
      expect(tab.label, label);
      expect(tab.route, route);
    }
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('discover')));
  });

  testWidgets('StudentBottomNav 4 sekme etiketi çizer, Keşfet yok', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        bottomNavigationBar: StudentBottomNav(current: StudentNavTab.work),
      ),
    ));
    for (final label in ['Çalışma', 'Derslerim', 'Performans', 'Profil']) {
      expect(find.text(label), findsOneWidget);
    }
    expect(find.text('Keşfet'), findsNothing);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/student_nav_tab_test.dart`
Beklenen: FAIL — `Çalış` etiketi `Çalışma` değil + `discover` üyesi hâlâ var.

- [ ] **Step 3: `StudentNavTab` enum + `_tabs` listesini güncelle**

`student_bottom_nav.dart` içinde enum bloğunu (satır 5-22) şu doküman yorumu + enum ile değiştir:

```dart
/// Öğrenci paneline özgü alt navigasyon. Öğretmen [AppBottomNav] ve veli
/// ParentBottomNav'ından ayrıdır; öğrenci sekmeleri 4-sekme IA'ya göredir
/// (🏠 Çalışma · 📚 Derslerim · 📊 Performans · 👤 Profil) —
/// bkz. `doc/roles/ogrenci_ux.md` §4 ve `doc/pages/study_student.md`.
enum StudentNavTab {
  work(Icons.rocket_launch_rounded, 'Çalışma', '/student-home'),
  lessons(Icons.menu_book_rounded, 'Derslerim', '/student/lessons'),
  performance(Icons.insights_rounded, 'Performans', '/student/performance'),
  profile(Icons.person_rounded, 'Profil', '/student/profile'),
  none(Icons.circle, '', '');

  const StudentNavTab(this.icon, this.label, this.route);

  final IconData icon;
  final String label;
  final String route;
}
```

Ardından `_tabs` listesini (satır 29-35) şununla değiştir:

```dart
  static const List<StudentNavTab> _tabs = <StudentNavTab>[
    StudentNavTab.work,
    StudentNavTab.lessons,
    StudentNavTab.performance,
    StudentNavTab.profile,
  ];
```

- [ ] **Step 4: Router — Keşfet builder'ı redirect'e çevir + import sil**

`app_router.dart`'ta `/student/discover` route bloğunu (satır ~146-149) şununla değiştir:

```dart
        GoRoute(
          path: '/student/discover',
          redirect: (context, state) => '/student/lessons',
        ),
```

`import '...student_discover_page.dart';` satırını (satır 34) sil.

- [ ] **Step 5: `student_discover_page.dart` dosyasını sil**

Run: `cd mobile && git rm lib/features/study/presentation/pages/student_discover_page.dart`
Not: Başka referans kalmadığını doğrula: `grep -rn "StudentDiscoverPage" lib` → boş dönmeli.

- [ ] **Step 6: Testi geçir + analyze**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/student_nav_tab_test.dart && flutter analyze`
Beklenen: test PASS; analyze yeni hata yok (Step 0'daki sayı ≥ değil).

- [ ] **Step 7: Dokümanları güncelle**

`doc/pages/study_student.md` navigasyon bölümünü 4 sekmeye getir (Keşfet satırını kaldır, sıra Çalışma·Derslerim·Performans·Profil). `doc/pages/00_pages_index.md`'de `student_discover_page` satırını sil. `doc/roles/ogrenci_ux.md` §4 nav açıklamasını 4 sekme yap. Her dosyanın alt tarihini `2026-08-19` yap.

- [ ] **Step 8: Commit**

```bash
git add mobile/lib/features/study/presentation/widgets/student_bottom_nav.dart mobile/lib/core/routing/app_router.dart mobile/test/features/study/presentation/widgets/student_nav_tab_test.dart doc/pages/study_student.md doc/pages/00_pages_index.md doc/roles/ogrenci_ux.md
git rm mobile/lib/features/study/presentation/pages/student_discover_page.dart
git commit -m "feat(mobile): öğrenci 4-sekme nav — Keşfet kaldırıldı (Task 1)"
```

---

## Task 2: Ortak kart dili — `study_tab_widgets.dart` genişletme

Sonraki tüm görevlerin dayandığı ortak stateless bileşenleri ekler. `student_home_page` yerel helper'ları (`_softCard`/`_IconChip`/`_Pressable`) bu ortak sürümlerle Task 3'te değiştirilir.

**Files:**
- Modify: `mobile/lib/features/study/presentation/widgets/study_tab_widgets.dart`
- Create: `mobile/test/features/study/presentation/widgets/study_tab_widgets_test.dart`
- Docs: `doc/architecture/widgets.md`

**Interfaces:**
- Produces:
  - `StudyDemoBadge()` — const, argümansız; "demo" pill'i (10px, `warning` tonlu).
  - `StudyIconChip({required IconData icon, required Color color, double size = 44})` — gradient ikon madalyonu.
  - `StudyPressable({required Widget child, required VoidCallback onTap})` — scale 0.97 dokunma efekti.
  - `StudyQuickAccessCard({required IconData icon, required Color color, required String label, required VoidCallback onTap})` — dashboard hızlı erişim kartı.
  - `StudyProgressBar({required double value, Color? color, String? trailingLabel})` — `value` 0..1; hedef ilerleme barı.
  - `StudyOwnershipBadge({required bool isOwn})` — `👤 Kendi` (teal) / `👨‍🏫 Öğretmen` (accentBlue) rozeti.

- [ ] **Step 1: Yeni widget'lar için saf render testini yaz (failing)**

Create `mobile/test/features/study/presentation/widgets/study_tab_widgets_test.dart`:

```dart
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _wrap(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  testWidgets('StudyDemoBadge "Demo" metnini çizer', (tester) async {
    await tester.pumpWidget(_wrap(const StudyDemoBadge()));
    expect(find.text('Demo'), findsOneWidget);
  });

  testWidgets('StudyOwnershipBadge kendi/öğretmen etiketini ayırır', (tester) async {
    await tester.pumpWidget(_wrap(const Column(
      children: [StudyOwnershipBadge(isOwn: true), StudyOwnershipBadge(isOwn: false)],
    )));
    expect(find.text('Kendi'), findsOneWidget);
    expect(find.text('Öğretmen'), findsOneWidget);
  });

  testWidgets('StudyQuickAccessCard etiket çizer ve tıklanır', (tester) async {
    var tapped = false;
    await tester.pumpWidget(_wrap(StudyQuickAccessCard(
      icon: Icons.menu_book_rounded,
      color: AppColors.primary,
      label: 'Derslerim',
      onTap: () => tapped = true,
    )));
    expect(find.text('Derslerim'), findsOneWidget);
    await tester.tap(find.text('Derslerim'));
    expect(tapped, isTrue);
  });

  testWidgets('StudyProgressBar value 0..1 sınırına kırpar', (tester) async {
    await tester.pumpWidget(_wrap(const StudyProgressBar(value: 1.4)));
    final bar = tester.widget<FractionallySizedBox>(find.byType(FractionallySizedBox));
    expect(bar.widthFactor, 1.0);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/study_tab_widgets_test.dart`
Beklenen: FAIL — `StudyDemoBadge` vb. tanımlı değil.

- [ ] **Step 3: Widget'ları `study_tab_widgets.dart` sonuna ekle**

Dosyanın en altına ekle:

```dart
/// Backend'i olmayan veri/eylemler için dürüst "demo" rozeti.
class StudyDemoBadge extends StatelessWidget {
  const StudyDemoBadge({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.warningSurfaceStrong,
        borderRadius: BorderRadius.circular(999),
      ),
      child: const Text('Demo',
          style: TextStyle(
              color: AppColors.warning, fontSize: 10, fontWeight: FontWeight.w700)),
    );
  }
}

/// Gradient ikon madalyonu (kart başlıkları / hızlı erişim).
class StudyIconChip extends StatelessWidget {
  const StudyIconChip(
      {super.key, required this.icon, required this.color, this.size = 44});

  final IconData icon;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [color.withValues(alpha: 0.18), color.withValues(alpha: 0.08)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: size * 0.5),
    );
  }
}

/// Basılınca hafifçe küçülen dokunma sarmalayıcısı.
class StudyPressable extends StatefulWidget {
  const StudyPressable({super.key, required this.child, required this.onTap});

  final Widget child;
  final VoidCallback onTap;

  @override
  State<StudyPressable> createState() => _StudyPressableState();
}

class _StudyPressableState extends State<StudyPressable> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTapDown: (_) => setState(() => _down = true),
      onTapCancel: () => setState(() => _down = false),
      onTapUp: (_) => setState(() => _down = false),
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _down ? 0.97 : 1.0,
        duration: const Duration(milliseconds: 90),
        child: widget.child,
      ),
    );
  }
}

/// Dashboard hızlı erişim kartı (ikon + etiket).
class StudyQuickAccessCard extends StatelessWidget {
  const StudyQuickAccessCard(
      {super.key,
      required this.icon,
      required this.color,
      required this.label,
      required this.onTap});

  final IconData icon;
  final Color color;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return StudyPressable(
      onTap: onTap,
      child: StudyCard(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            StudyIconChip(icon: icon, color: color),
            const SizedBox(height: 10),
            Text(label,
                style: const TextStyle(
                    fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
          ],
        ),
      ),
    );
  }
}

/// Hedef ilerleme barı (value 0..1, 1 üzerini kırpar).
class StudyProgressBar extends StatelessWidget {
  const StudyProgressBar(
      {super.key, required this.value, this.color, this.trailingLabel});

  final double value;
  final Color? color;
  final String? trailingLabel;

  @override
  Widget build(BuildContext context) {
    final clamped = value.clamp(0.0, 1.0);
    final barColor = color ?? AppColors.accentTeal;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: Container(
            height: 8,
            color: AppColors.tabBackground,
            child: Align(
              alignment: Alignment.centerLeft,
              child: FractionallySizedBox(
                widthFactor: clamped,
                child: Container(color: barColor),
              ),
            ),
          ),
        ),
        if (trailingLabel != null) ...<Widget>[
          const SizedBox(height: 4),
          Text(trailingLabel!,
              style:
                  const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
        ],
      ],
    );
  }
}

/// Kendi (öğrenci) / öğretmen dersi ayrım rozeti.
class StudyOwnershipBadge extends StatelessWidget {
  const StudyOwnershipBadge({super.key, required this.isOwn});

  final bool isOwn;

  @override
  Widget build(BuildContext context) {
    final color = isOwn ? AppColors.accentTeal : AppColors.accentBlue;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(isOwn ? Icons.person_rounded : Icons.school_rounded,
              size: 12, color: color),
          const SizedBox(width: 4),
          Text(isOwn ? 'Kendi' : 'Öğretmen',
              style: TextStyle(
                  color: color, fontSize: 10, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }
}
```

- [ ] **Step 4: Testi geçir + analyze**

Run: `cd mobile && flutter test test/features/study/presentation/widgets/study_tab_widgets_test.dart && flutter analyze`
Beklenen: PASS; analyze yeni hata yok.

- [ ] **Step 5: Widget kataloğunu güncelle**

`doc/architecture/widgets.md`'ye 6 yeni bileşeni (StudyDemoBadge/StudyIconChip/StudyPressable/StudyQuickAccessCard/StudyProgressBar/StudyOwnershipBadge) 🟢 durumuyla ekle; alt tarih `2026-08-19`.

- [ ] **Step 6: Commit**

```bash
git add mobile/lib/features/study/presentation/widgets/study_tab_widgets.dart mobile/test/features/study/presentation/widgets/study_tab_widgets_test.dart doc/architecture/widgets.md
git commit -m "feat(mobile): ortak kart dili — demo rozeti + hızlı erişim/ilerleme/ayrım widget'ları (Task 2)"
```

---

## Task 3: Çalışma sekmesi — dashboard

`student_home_page` yeniden düzenlenir: büyük sayaç/`_HeroSummary` çıkar; istatistik ızgarası + "Çalışmaya Başla" + 4 hızlı erişim + yaklaşanlar gelir. `StudyHomeCubit` korunur.

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart`
- Create: `mobile/lib/features/study/presentation/home/dashboard_stats.dart`
- Create: `mobile/test/features/study/presentation/home/dashboard_stats_test.dart`
- Docs: `doc/pages/study_student.md`

**Interfaces:**
- Produces: `WeeklyStat({required int given, required int done})` + `double get ratio` (done/given, given=0→0). `DashboardStats.demoWeeklyHomework()` ve `DashboardStats.demoWeeklyLessons()` → demo `WeeklyStat`.
- Consumes: Task 2 widget'ları (`StudyQuickAccessCard`, `StudyProgressBar`, `StudyDemoBadge`, `StudyStatTile`, `StudySectionHeader`); `StudyHomeCubit`/`StudyHomeState` (mevcut); `SchedulingRepository.listStudentLessons` (yaklaşan dersler); `AssignmentRepository` (yaklaşan ödev — demo fallback).

- [ ] **Step 1: `WeeklyStat.ratio` saf testini yaz (failing)**

Create `mobile/test/features/study/presentation/home/dashboard_stats_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/home/dashboard_stats.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('ratio tamamlanan/verilen; verilen 0 ise 0', () {
    expect(const WeeklyStat(given: 4, done: 3).ratio, closeTo(0.75, 1e-9));
    expect(const WeeklyStat(given: 0, done: 0).ratio, 0.0);
  });

  test('demo istatistikleri makul aralıkta', () {
    expect(DashboardStats.demoWeeklyHomework().given, greaterThanOrEqualTo(DashboardStats.demoWeeklyHomework().done));
    expect(DashboardStats.demoWeeklyLessons().given, greaterThanOrEqualTo(DashboardStats.demoWeeklyLessons().done));
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/home/dashboard_stats_test.dart`
Beklenen: FAIL — `dashboard_stats.dart` yok.

- [ ] **Step 3: `dashboard_stats.dart`'ı yaz**

Create `mobile/lib/features/study/presentation/home/dashboard_stats.dart`:

```dart
/// Dashboard haftalık istatistik değer nesnesi (verilen/tamamlanan).
class WeeklyStat {
  const WeeklyStat({required this.given, required this.done});

  final int given;
  final int done;

  double get ratio => given == 0 ? 0.0 : (done / given).clamp(0.0, 1.0);
}

/// Backend'i henüz olmayan haftalık istatistikler için demo değerleri.
/// Gerçek veri gelince bu fabrikalar repository'ye taşınır (Ö-A/Ö-B işleri).
class DashboardStats {
  const DashboardStats._();

  static WeeklyStat demoWeeklyHomework() => const WeeklyStat(given: 5, done: 3);
  static WeeklyStat demoWeeklyLessons() => const WeeklyStat(given: 4, done: 2);
}
```

- [ ] **Step 4: Testi geçir**

Run: `cd mobile && flutter test test/features/study/presentation/home/dashboard_stats_test.dart`
Beklenen: PASS.

- [ ] **Step 5: `student_home_page` gövdesini dashboard düzenine getir**

`student_home_page.dart` `build`/`ListView` içeriğini şu blok sırasına göre yeniden düzenle (mevcut `StudyHomeCubit` verisi + `StudentScope` korunur):

1. `AppPageHeader` — selamlama + motivasyon alt satırı (mevcut).
2. **İstatistik ızgarası** — 2×2 `GridView.count(shrinkWrap: true, crossAxisCount: 2, childAspectRatio: 1.35, mainAxisSpacing: 12, crossAxisSpacing: 12)` içinde 4 `StudyStatTile`:
   - Streak: `icon: Icons.local_fire_department_rounded, color: AppColors.accentOrange, value: '${state.streakDays}', label: 'Gün seri'`.
   - Bugünkü çalışma: `value: StudyFormat.minutes(state.todayMinutes), label: 'Bugün'` + altına `StudyProgressBar(value: state.dailyGoalMinutes == 0 ? 0 : state.todayMinutes / state.dailyGoalMinutes, trailingLabel: 'Hedef ${state.dailyGoalMinutes} dk')` — mevcut state alan adları koddan doğrulanır; farklıysa eşleştir.
   - Haftalık ödev: demo → `StudyStatTile(value: '${hw.done}/${hw.given}', label: 'Ödev (hafta)')` + üstünde `StudyDemoBadge`. `final hw = DashboardStats.demoWeeklyHomework();`
   - Haftalık ders: demo → `final ls = DashboardStats.demoWeeklyLessons(); value: '${ls.done}/${ls.given}', label: 'Ders (hafta)'` + `StudyDemoBadge`.
3. **"Çalışmaya Başla" kartı** — mevcut `_PrimaryActionCard` korunur/uyarlanır; onTap `context.push('/study/timer?studentId=$studentId')`.
4. **Hızlı erişim** — `StudySectionHeader(title: 'Hızlı erişim')` + `GridView.count(crossAxisCount: 2, childAspectRatio: 1.6, shrinkWrap: true, ...)` içinde 4 `StudyQuickAccessCard`:
   - Derslerim → `context.go('/student/lessons')`.
   - Ödevlerim → `context.push('/student/assignments?studentId=$studentId')` (rota koddan doğrulanır).
   - Hedeflerim → `context.push('/student/goals-overview?studentId=$studentId')`.
   - Performansım → `context.go('/student/performance')`.
5. **Yaklaşanlar** — mevcut `_UpcomingLessonCard` (SchedulingRepository) "Yaklaşan dersler" başlığıyla; ardından "Yaklaşan ödevler" bölümü (`AssignmentRepository`'den teslim tarihi yaklaşan; veri yoksa `StudyComingSoonCard` + `StudyDemoBadge`).

`_HeroSummary`, `_TodayPlanCard` ve "Son çalışmalar" (`_SessionTile` listesi) bloklarını kaldır. Kullanılmayan yerel helper'ları (`_RingPainter`, `_ProgressRing`, `_StreakPill`) sil. `_softCard`/`_IconChip`/`_Pressable` yerine Task 2 ortak sürümlerini kullan.

- [ ] **Step 6: Analyze + manuel duman testi**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok. Manuel: `flutter run` ile Çalışma sekmesi — 4 istatistik, "Çalışmaya Başla" → Kronometre, 4 hızlı erişim doğru rotalara, yaklaşanlar görünür; demo kartlarında "Demo" rozeti.

- [ ] **Step 7: Doküman + commit**

`doc/pages/study_student.md` Çalışma sekmesi bölümünü dashboard düzenine güncelle (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/pages/student_home_page.dart mobile/lib/features/study/presentation/home/dashboard_stats.dart mobile/test/features/study/presentation/home/dashboard_stats_test.dart doc/pages/study_student.md
git commit -m "feat(mobile): Çalışma sekmesi — dashboard (istatistik + hızlı erişim + yaklaşanlar) (Task 3)"
```

---

## Task 4: Kronometre — hazırlık formu + "Molada" durumu + manuel demo

`study_timer_page` genişler. Manuel seans + `TimerAccumulator` saf mantığı ayrı dosyada test edilir.

**Files:**
- Create: `mobile/lib/features/study/presentation/timer/manual_session_store.dart`
- Modify: `mobile/lib/features/study/presentation/pages/study_timer_page.dart`
- Create: `mobile/test/features/study/presentation/timer/manual_session_store_test.dart`
- Docs: `doc/pages/study_student.md`

**Interfaces:**
- Produces:
  - `TimerAccumulator({int studySeconds = 0, int breakSeconds = 0, int breakCount = 0})` — immutable; `int get totalSeconds => studySeconds + breakSeconds;` `TimerAccumulator startBreak()` (breakCount+1) ve `copyWith`.
  - `ManualSession({required String id, required String subject, String? topic, required DateTime dayUtc, required int minutes})`.
  - `ManualSessionStore` (singleton, yerel/bellek-içi demo): `List<ManualSession> get sessions`, `void add(ManualSession)`, `void remove(String id)`, `Listenable get listenable`.
- Consumes: Task 2 (`StudyDemoBadge`); mevcut `StudyTimerCubit`/`StudyTimerState`; `SchedulingRepository` (ders önerileri).

- [ ] **Step 1: `TimerAccumulator` + store saf testini yaz (failing)**

Create `mobile/test/features/study/presentation/timer/manual_session_store_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/timer/manual_session_store.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('totalSeconds = çalışma + mola; mola net süreye eklenmez ayrı tutulur', () {
    const acc = TimerAccumulator(studySeconds: 600, breakSeconds: 120, breakCount: 1);
    expect(acc.totalSeconds, 720);
    expect(acc.studySeconds, 600);
  });

  test('startBreak mola sayısını artırır', () {
    const acc = TimerAccumulator();
    final next = acc.startBreak();
    expect(next.breakCount, 1);
  });

  test('ManualSessionStore ekle/sil çalışır (yerel demo)', () {
    final store = ManualSessionStore();
    store.add(ManualSession(
        id: 'a', subject: 'Matematik', dayUtc: DateTime.utc(2026, 8, 19), minutes: 45));
    expect(store.sessions.length, 1);
    store.remove('a');
    expect(store.sessions, isEmpty);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/timer/manual_session_store_test.dart`
Beklenen: FAIL — dosya yok.

- [ ] **Step 3: `manual_session_store.dart`'ı yaz**

Create `mobile/lib/features/study/presentation/timer/manual_session_store.dart`:

```dart
import 'package:flutter/foundation.dart';

/// Kronometre çalışma + mola birikimini tutan değişmez değer nesnesi.
/// Mola süresi net (çalışma) süreye EKLENMEZ; ayrı tutulur, toplam ayrı hesaplanır.
@immutable
class TimerAccumulator {
  const TimerAccumulator(
      {this.studySeconds = 0, this.breakSeconds = 0, this.breakCount = 0});

  final int studySeconds;
  final int breakSeconds;
  final int breakCount;

  int get totalSeconds => studySeconds + breakSeconds;

  TimerAccumulator startBreak() => copyWith(breakCount: breakCount + 1);

  TimerAccumulator copyWith({int? studySeconds, int? breakSeconds, int? breakCount}) =>
      TimerAccumulator(
        studySeconds: studySeconds ?? this.studySeconds,
        breakSeconds: breakSeconds ?? this.breakSeconds,
        breakCount: breakCount ?? this.breakCount,
      );
}

/// Sonradan elle eklenen ("unutulan") çalışma kaydı. Demo/yerel — backend yok.
@immutable
class ManualSession {
  const ManualSession(
      {required this.id,
      required this.subject,
      this.topic,
      required this.dayUtc,
      required this.minutes});

  final String id;
  final String subject;
  final String? topic;
  final DateTime dayUtc;
  final int minutes;
}

/// Manuel seansların yerel/bellek-içi demo deposu (Ö-A2/Ö-E backend'i gelene dek).
/// UI tam çalışır ama kayıtlar oturum içinde kalır, backend'e gitmez.
class ManualSessionStore extends ChangeNotifier {
  final List<ManualSession> _sessions = <ManualSession>[];

  List<ManualSession> get sessions => List.unmodifiable(_sessions);
  Listenable get listenable => this;

  void add(ManualSession session) {
    _sessions.add(session);
    notifyListeners();
  }

  void remove(String id) {
    _sessions.removeWhere((s) => s.id == id);
    notifyListeners();
  }
}
```

- [ ] **Step 4: Testi geçir**

Run: `cd mobile && flutter test test/features/study/presentation/timer/manual_session_store_test.dart`
Beklenen: PASS.

- [ ] **Step 5: Hazırlık formunu genişlet (`_StartForm`)**

`study_timer_page.dart` `_StartForm` içinde:
- **Serbest çalışma** çipini ders çiplerinden ayrı, belirgin bir `_SubjectChoiceChip` olarak ekle (seçilince ders/konu seçimi pasifleşir).
- Konu seçimini **çoklu** yap: seçili konular `Set<String>` tutulur; her `_SubjectChoiceChip` toggle. Ders seçili + konu boş geçerli.
- **Hızlı seçim** satırı: son N ders/konu (`StudyRepository.listSessions` sonucundan türet) tek tıkla ders+konu doldurur.
- **Hedef süre** çipleri: 25/45/60 dk + "Özel" (opsiyonel, seçilmeyebilir).
- Formun altına ikincil buton: **"Süre ekle / geçmiş"** → Step 7'deki manuel sheet'i açar.

- [ ] **Step 6: "Molada" durumunu ekle (`_ActiveTimer`)**

`_ActiveTimer`/`StudyTimerCubit` akışına ayrı **Molada** görsel durumu ekle:
- "Mola ver" → cubit mola moduna geçer (ana sayaç durur, mola sayacı başlar, `breakCount++`).
- Molada tema turuncu (`AppColors.accentOrange`): dial turuncu, `_StatusBadge` "Molada", tek büyük buton **"Devam et"**.
- `_StatsRow` her durumda: **Toplam** (`TimerAccumulator.totalSeconds` → `SS:DD:SN`), **Mola sayısı** (`breakCount`), **Mola süresi** (`breakSeconds`).
- Not: mevcut `StudyTimerCubit`/`StudyTimerState`'e `isOnBreak`, `breakSeconds`, `breakCount` alanları eklenir; süre birikimi `TimerAccumulator` mantığıyla hizalanır. Mevcut "bitir" net süreyi (yalnız `studySeconds`) kaydeder — mola eklenmez (mevcut doğru kurgu korunur).

- [ ] **Step 7: Manuel çalışma & geçmiş sheet'i ekle (demo)**

`showModalBottomSheet` ile açılan sheet:
- **Süre ekle** formu: ders (`AppDropdownField`/çip) + konu (opsiyonel) + tarih (`AppDateTimeField`) + süre (dk) → `ManualSessionStore().add(...)`.
- **Geçmiş liste**: `ManualSessionStore().sessions` → her satır `StudySessionTile(isManual: true)` + düzenle/sil (`remove`). Sheet başlığında `StudyDemoBadge`.
- `AnimatedBuilder(animation: store.listenable, ...)` ile canlı güncelle.

- [ ] **Step 8: Analyze + manuel duman testi**

Run: `cd mobile && flutter test test/features/study/presentation/timer/manual_session_store_test.dart && flutter analyze`
Beklenen: PASS + yeni analyze hatası yok. Manuel: form (serbest/çoklu konu/hızlı seçim/hedef) → Başla → Mola ver (turuncu Molada) → Devam et → Bitir; "Süre ekle / geçmiş" sheet ekle/sil (Demo rozeti).

- [ ] **Step 9: Doküman + commit**

`doc/pages/study_student.md` Kronometre bölümünü güncelle (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/timer/manual_session_store.dart mobile/lib/features/study/presentation/pages/study_timer_page.dart mobile/test/features/study/presentation/timer/manual_session_store_test.dart doc/pages/study_student.md
git commit -m "feat(mobile): Kronometre — form + Molada durumu + manuel süre demo (Task 4)"
```

---

## Task 5: Ders Detayı sayfası + rota + yetki

Yeni push sayfa `/student/lessons/:id`. Yetki saf fonksiyonu ayrı test edilir.

**Files:**
- Create: `mobile/lib/features/study/presentation/lessons/lesson_detail_permissions.dart`
- Create: `mobile/lib/features/study/presentation/pages/student_lesson_detail_page.dart`
- Modify: `mobile/lib/core/routing/app_router.dart`
- Create: `mobile/test/features/study/presentation/lessons/lesson_detail_permissions_test.dart`
- Docs: `doc/pages/study_student.md`, `doc/pages/00_pages_index.md`

**Interfaces:**
- Produces:
  - `LessonDetailPermissions({required bool canAddHomework, required bool canAddTopic, required bool canAddNote, required bool canAddTest})` + `factory LessonDetailPermissions.forOwnership(bool isOwn)`.
  - `StudentLessonDetailPage({required String lessonId})` (const kurucu).
- Consumes: `SchedulingRepository.getLesson`/eşdeğeri (koddan doğrula), `TeacherRepository.getProfile`, `AssignmentRepository`, `StudyRepository.listTests`/`study_notes`, Task 2 widget'ları (`StudyOwnershipBadge`, `StudyDemoBadge`, `StudyQuickAccessCard`, `StudyCard`, `StudySectionHeader`).

- [ ] **Step 1: Yetki saf testini yaz (failing)**

Create `mobile/test/features/study/presentation/lessons/lesson_detail_permissions_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/lessons/lesson_detail_permissions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('kendi dersinde tüm ekleme açık', () {
    final p = LessonDetailPermissions.forOwnership(true);
    expect(p.canAddHomework, isTrue);
    expect(p.canAddTopic, isTrue);
    expect(p.canAddNote, isTrue);
    expect(p.canAddTest, isTrue);
  });

  test('öğretmen dersinde ödev/konu ekle kapalı; not/test açık', () {
    final p = LessonDetailPermissions.forOwnership(false);
    expect(p.canAddHomework, isFalse);
    expect(p.canAddTopic, isFalse);
    expect(p.canAddNote, isTrue);
    expect(p.canAddTest, isTrue);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_detail_permissions_test.dart`
Beklenen: FAIL — dosya yok.

- [ ] **Step 3: `lesson_detail_permissions.dart`'ı yaz**

Create `mobile/lib/features/study/presentation/lessons/lesson_detail_permissions.dart`:

```dart
/// Ders Detayı ekleme/düzenleme yetkileri. Öğretmen dersinde (isOwn=false)
/// öğretmenin ödev/konusu salt görüntüleme; öğrenci yalnız kendi not/testini ekler.
class LessonDetailPermissions {
  const LessonDetailPermissions({
    required this.canAddHomework,
    required this.canAddTopic,
    required this.canAddNote,
    required this.canAddTest,
  });

  final bool canAddHomework;
  final bool canAddTopic;
  final bool canAddNote;
  final bool canAddTest;

  factory LessonDetailPermissions.forOwnership(bool isOwn) =>
      LessonDetailPermissions(
        canAddHomework: isOwn,
        canAddTopic: isOwn,
        canAddNote: true,
        canAddTest: true,
      );
}
```

- [ ] **Step 4: Testi geçir**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_detail_permissions_test.dart`
Beklenen: PASS.

- [ ] **Step 5: `student_lesson_detail_page.dart`'ı yaz**

Create `StudentLessonDetailPage({required String lessonId})` — `StatefulWidget`, `AppBar(title: 'Ders Detayı')`, BottomNav yok. `initState`'te `StudentScope.resolve` + dersi yükle (`SchedulingRepository`); `state_views.dart` üç durum. Blok sırası:
1. **Başlık kartı** (`StudyCard`) — ders adı, tarih/saat (`StudyFormat`), tür (Online/Yüz yüze + link/konum), `StudyOwnershipBadge(isOwn: lesson.teacherUserId == null)`, durum.
2. **Öğretmen bilgisi** — `isOwn` false ise `TeacherRepository.getProfile` ile avatar+ad+branş kartı; true ise gizli.
3. **Hızlı erişim kartları** — `final perms = LessonDetailPermissions.forOwnership(isOwn);` ile koşullu: Not (perms.canAddNote), Test (perms.canAddTest), Deneme (perms.canAddTest), Ödev (perms.canAddHomework ? ekle : "teslim et"), Konu (perms.canAddTopic ? ekle : salt görüntüle). Gizli olanlar hiç çizilmez.
4. **Listeler** — Ödev (`AssignmentRepository`, teslim); **Test & Deneme tek liste** (`StudyRepository.listTests` derse filtreli; tür rozeti 🔹 Test / 🔸 Deneme; derse-bağ yoksa `StudyDemoBadge`); Konu listesi (`SubjectCatalog`; hâkimiyet rozeti `StudyDemoBadge`); Not listesi (`study_notes`) + "tüm notlarım" → `context.push('/study/notes?studentId=$studentId')`.

Not: `SchedulingRepository`'de tekil ders getirme metodu yoksa, `listStudentLessons`/`getStudentCalendar` sonucundan `lessonId` ile filtrele (koddan doğrula, mevcut imzayı kullan).

- [ ] **Step 6: Rotayı ekle**

`app_router.dart`'ta `/student/lessons` bloğundan sonra ekle:

```dart
        GoRoute(
          path: '/student/lessons/:id',
          builder: (context, state) =>
              StudentLessonDetailPage(lessonId: state.pathParameters['id']!),
        ),
```

Üstte `import '...student_lesson_detail_page.dart';` ekle.

- [ ] **Step 7: Analyze + manuel duman testi**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_detail_permissions_test.dart && flutter analyze`
Beklenen: PASS + yeni analyze hatası yok. Manuel: `/student/lessons/<id>` — kendi dersi (tüm kartlar) vs öğretmen dersi (ödev/konu ekle gizli, öğretmen kartı görünür); tek Test&Deneme listesi tür rozetli.

- [ ] **Step 8: Doküman + commit**

`doc/pages/00_pages_index.md`'ye Ders Detayı satırı ekle; `doc/pages/study_student.md`'ye Ders Detayı bölümü (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/lessons/lesson_detail_permissions.dart mobile/lib/features/study/presentation/pages/student_lesson_detail_page.dart mobile/lib/core/routing/app_router.dart mobile/test/features/study/presentation/lessons/lesson_detail_permissions_test.dart doc/pages/study_student.md doc/pages/00_pages_index.md
git commit -m "feat(mobile): Ders Detayı sayfası + yetki (öğretmen dersi salt görüntüle) (Task 5)"
```

---

## Task 6: Derslerim — liste/takvim + kendi/öğretmen ayrımı

`student_calendar_page` genişler. Ayrım/gruplama saf fonksiyonu ayrı test edilir.

**Files:**
- Create: `mobile/lib/features/study/presentation/lessons/lesson_ownership.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_calendar_page.dart`
- Create: `mobile/test/features/study/presentation/lessons/lesson_ownership_test.dart`
- Docs: `doc/pages/study_student.md`

**Interfaces:**
- Produces:
  - `enum LessonFilter { all, own, teacher }`.
  - `bool isOwnLesson(String? teacherUserId)` → `teacherUserId == null`.
  - `List<T> filterLessons<T>(List<T> lessons, LessonFilter f, String? Function(T) teacherOf)`.
  - `({List<T> own, List<T> teacher}) partitionLessons<T>(List<T> lessons, String? Function(T) teacherOf)`.
- Consumes: mevcut `SchedulingRepository`; Task 2 (`StudyOwnershipBadge`); Ders Detayı rotası (`/student/lessons/:id`, Task 5).

- [ ] **Step 1: Ayrım/gruplama saf testini yaz (failing)**

Create `mobile/test/features/study/presentation/lessons/lesson_ownership_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/lessons/lesson_ownership.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final lessons = <(String, String?)>[
    ('a', null),       // kendi
    ('b', 't1'),       // öğretmen
    ('c', null),       // kendi
  ];
  String? teacherOf((String, String?) l) => l.$2;

  test('isOwnLesson teacherUserId null ise kendi', () {
    expect(isOwnLesson(null), isTrue);
    expect(isOwnLesson('t1'), isFalse);
  });

  test('filterLessons her filtreyi uygular', () {
    expect(filterLessons(lessons, LessonFilter.all, teacherOf).length, 3);
    expect(filterLessons(lessons, LessonFilter.own, teacherOf).length, 2);
    expect(filterLessons(lessons, LessonFilter.teacher, teacherOf).length, 1);
  });

  test('partitionLessons kendi/öğretmen ayırır', () {
    final p = partitionLessons(lessons, teacherOf);
    expect(p.own.length, 2);
    expect(p.teacher.length, 1);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_ownership_test.dart`
Beklenen: FAIL — dosya yok.

- [ ] **Step 3: `lesson_ownership.dart`'ı yaz**

Create `mobile/lib/features/study/presentation/lessons/lesson_ownership.dart`:

```dart
/// Ders sahipliği (kendi/öğretmen) filtre + gruplama saf yardımcıları.
/// Ç-06: teacherUserId == null → öğrencinin kendi dersi.
enum LessonFilter { all, own, teacher }

bool isOwnLesson(String? teacherUserId) => teacherUserId == null;

List<T> filterLessons<T>(
    List<T> lessons, LessonFilter filter, String? Function(T) teacherOf) {
  switch (filter) {
    case LessonFilter.all:
      return List<T>.from(lessons);
    case LessonFilter.own:
      return lessons.where((l) => isOwnLesson(teacherOf(l))).toList();
    case LessonFilter.teacher:
      return lessons.where((l) => !isOwnLesson(teacherOf(l))).toList();
  }
}

({List<T> own, List<T> teacher}) partitionLessons<T>(
    List<T> lessons, String? Function(T) teacherOf) {
  final own = <T>[];
  final teacher = <T>[];
  for (final l in lessons) {
    (isOwnLesson(teacherOf(l)) ? own : teacher).add(l);
  }
  return (own: own, teacher: teacher);
}
```

- [ ] **Step 4: Testi geçir**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_ownership_test.dart`
Beklenen: PASS.

- [ ] **Step 5: Derslerim'e Liste/Takvim segmenti + ayrım ekle**

`student_calendar_page.dart`:
- Üstte **Liste/Takvim segmenti** (`enum _ViewMode { calendar, list }`, default `calendar`) — mevcut `study_tab` segment görseli veya basit iki `ChoiceChip`. Takvim modu = mevcut `SfCalendar`+`_ViewSwitcher`+`_SelectedDayPanel` (değişmez).
- **Liste modu**: `SchedulingRepository` derslerini `partitionLessons(..., (l) => l.teacherUserId)` ile ayır (gerçek alan adı koddan); "Kendi derslerim" ve "Öğretmen dersleri" başlıklı iki grup; boş grup gizli.
- **Filtre çipleri** (her iki mod üstünde): `LessonFilter.values` → Tümü/Kendi/Öğretmen; seçili filtre listeyi/paneli süzer.
- Ders kartında `StudyOwnershipBadge(isOwn: isOwnLesson(l.teacherUserId))` + öğretmen adı satırı + aksan rengi.
- Karta tıkla → `context.push('/student/lessons/${l.id}')` (Task 5).
- FAB "Ders ekle" (`StudyEntryFormSheet`) korunur; "Dersler & Konular" küçük girişi (`/study/catalog`) korunur. Öğretmenlerim/Notlarım araç tile'ları **kaldırılır** (Profil/Ders Detayı'na taşındı).

- [ ] **Step 6: Analyze + manuel duman testi**

Run: `cd mobile && flutter test test/features/study/presentation/lessons/lesson_ownership_test.dart && flutter analyze`
Beklenen: PASS + yeni analyze hatası yok. Manuel: Liste/Takvim geçişi; liste modunda iki grup + filtre; kart → Ders Detayı; Kendi/Öğretmen rozet+renk.

- [ ] **Step 7: Doküman + commit**

`doc/pages/study_student.md` Derslerim bölümünü güncelle (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/lessons/lesson_ownership.dart mobile/lib/features/study/presentation/pages/student_calendar_page.dart mobile/test/features/study/presentation/lessons/lesson_ownership_test.dart doc/pages/study_student.md
git commit -m "feat(mobile): Derslerim — liste/takvim + kendi/öğretmen ayrımı + detaya git (Task 6)"
```

---

## Task 7: Performans — hedef net + konu eksiği + rekorlar

`student_tests_page` yeniden düzenlenir. Kişisel rekor + zayıf konu saf fonksiyonları ayrı test edilir.

**Files:**
- Create: `mobile/lib/features/study/presentation/performance/personal_records.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_tests_page.dart`
- Create: `mobile/test/features/study/presentation/performance/personal_records_test.dart`
- Docs: `doc/pages/study_student.md`

**Interfaces:**
- Produces:
  - `double bestNet(List<double> nets)` (boşsa 0), `double averageNet(List<double> nets)` (boşsa 0).
  - `List<String> weakTopics(Map<String, double> topicScores, {double threshold = 60})` — eşiğin altındaki konu adları, artan skor sırası.
- Consumes: mevcut `StudyRepository.listTests`/`getWeeklySummary`/`listSessions`; mevcut grafik/analiz yerel widget'ları (`_NetTrendChart`, `_SubjectAnalysis`, `_WeeklyBars`, `_LessonBreakdown`); Task 2 (`StudyDemoBadge`, `StudyStatTile`, `StudyProgressBar`, `StudySectionHeader`).

- [ ] **Step 1: Saf istatistik testini yaz (failing)**

Create `mobile/test/features/study/presentation/performance/personal_records_test.dart`:

```dart
import 'package:egitim_ussu_mobile/features/study/presentation/performance/personal_records.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('bestNet / averageNet boş listede 0', () {
    expect(bestNet(const []), 0);
    expect(averageNet(const []), 0);
  });

  test('bestNet en yüksek; averageNet ortalama', () {
    expect(bestNet(const [12.5, 30.0, 22.0]), 30.0);
    expect(averageNet(const [10, 20, 30]), closeTo(20.0, 1e-9));
  });

  test('weakTopics eşik altını artan skorla döndürür', () {
    final weak = weakTopics({'Türev': 40, 'Limit': 80, 'İntegral': 55});
    expect(weak, ['Türev', 'İntegral']);
  });
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd mobile && flutter test test/features/study/presentation/performance/personal_records_test.dart`
Beklenen: FAIL — dosya yok.

- [ ] **Step 3: `personal_records.dart`'ı yaz**

Create `mobile/lib/features/study/presentation/performance/personal_records.dart`:

```dart
/// Performans sayfası saf istatistik yardımcıları.
double bestNet(List<double> nets) =>
    nets.isEmpty ? 0 : nets.reduce((a, b) => a > b ? a : b);

double averageNet(List<double> nets) =>
    nets.isEmpty ? 0 : nets.reduce((a, b) => a + b) / nets.length;

/// Eşiğin (varsayılan 60) altındaki konu adları, en zayıftan güçlüye sıralı.
List<String> weakTopics(Map<String, double> topicScores, {double threshold = 60}) {
  final entries = topicScores.entries.where((e) => e.value < threshold).toList()
    ..sort((a, b) => a.value.compareTo(b.value));
  return entries.map((e) => e.key).toList();
}
```

- [ ] **Step 4: Testi geçir**

Run: `cd mobile && flutter test test/features/study/presentation/performance/personal_records_test.dart`
Beklenen: PASS.

- [ ] **Step 5: Performans gövdesini brief'e göre düzenle**

`student_tests_page.dart` blok sırası:
1. `AppPageHeader('Performans')`.
2. **Özet istatistik satırı** (`StudyStatTile` ×4) — toplam deneme, `averageNet`, `bestNet`, hedef nete kalan (hedef demo → `StudyDemoBadge`).
3. **"Test / Deneme gir"** → `/study/test` (tür seçimi TestEntryPage'e param olarak; mevcut giriş korunur).
4. **Hedef net takibi** kartı — hedef + ortalama + `StudyProgressBar` (skor renk kuralı) + `StudyDemoBadge`.
5. **Net gelişim grafiği** (`_NetTrendChart`) — Genel/ders + Hafta/Ay filtre.
6. **Konu bazlı — iki ayrı bölüm:** 🔹 "Test istatistikleri" (`_LessonBreakdown`) · 🔸 "Deneme istatistikleri" (`_SubjectAnalysis`), ayrı `StudySectionHeader`'larla.
7. **Konu eksiği** — `weakTopics(...)` (skorlar yoksa demo map + `StudyDemoBadge`); her satırda "çalış" → `context.push('/study/timer?studentId=$studentId')`.
8. **Haftalık/Aylık analiz** — Hafta/Ay segmenti + `_WeeklyBars`.
9. **Kişisel rekorlar** — kart ızgarası (en iyi net = `bestNet`, en uzun seri, en uzun tek seans, en çok çalışılan gün, en verimli ders; hesaplanamayan alanlar `StudyDemoBadge`).
10. **Alt linkler** — Geçmiş (`/study/history`), Gelişim (`/student/progress`).

- [ ] **Step 6: Analyze + manuel duman testi**

Run: `cd mobile && flutter test test/features/study/presentation/performance/personal_records_test.dart && flutter analyze`
Beklenen: PASS + yeni analyze hatası yok. Manuel: özet, hedef net barı, grafik, iki ayrı bölüm, konu eksiği "çalış", hafta/ay, rekorlar; demo alanlarda rozet.

- [ ] **Step 7: Doküman + commit**

`doc/pages/study_student.md` Performans bölümünü güncelle (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/performance/personal_records.dart mobile/lib/features/study/presentation/pages/student_tests_page.dart mobile/test/features/study/presentation/performance/personal_records_test.dart doc/pages/study_student.md
git commit -m "feat(mobile): Performans — hedef net + konu eksiği + kişisel rekorlar (Task 7)"
```

---

## Task 8: Profil — premium hero + menüler

`student_profile_page` genişler.

**Files:**
- Modify: `mobile/lib/features/study/presentation/pages/student_profile_page.dart`
- Docs: `doc/pages/study_student.md`, `doc/roles/ogrenci.md`

**Interfaces:**
- Consumes: mevcut `StudyRepository.getStreak`/`listSessions`/`listTests`/`getAchievements`; `AuthCubit`; `student_teacher_page` (`/student/teacher`); `study_goals_page` (`/study/goals`); `/account-info`; Task 2 (`StudyIconChip`, `StudyStatTile`, `StudyDemoBadge`).

- [ ] **Step 1: Profil hero + menü listesini yeniden yaz**

`student_profile_page.dart`:
1. **Profil hero** (`StudyCard` + gradient) — büyük avatar (foto yoksa baş harf), ad, sınıf/hedef sınav, 🔥 seri. **Abonelik ayrımı:** `bool isPremium` (şimdilik demo/false) — premium'da altın kenarlık + "Premium" rozeti; free'de sade + "Yükselt" ipucu (+ `StudyDemoBadge`). Sağ üstte **Düzenle** (profil düzenleme — sheet; backend yoksa demo).
2. **Mini istatistikler** — mevcut `StudyStatTile` satırları korunur (toplam çalışma/gün/rekor seri/deneme).
3. **Başarımlar** — mevcut `StudySectionHeader('Rozetler', action:'Tümü')` → `/study/achievements`.
4. **Menü listesi** (`_ProfileMenuTile`) tam sıra:
   - Velim → veli bağlantı sayfası (backend yoksa `StudyDemoBadge` + yer tutucu sheet).
   - Öğretmenlerim → `context.push('/student/teacher')`.
   - Hedef ekle → `context.push('/study/goals?studentId=$studentId')`.
   - Bildirim ayarları → yer tutucu (`StudyDemoBadge`).
   - Gizlilik ve Güvenlik (birleşik) → tek tile; gizlilik + `/account-info` alt seçenekleri (sheet veya `/account-info`'ya git).
   - Aboneliğim → Faz 5 yer tutucu (`StudyComingSoonCard` veya `StudyDemoBadge`).
   - Çıkış yap → mevcut `_confirmLogout` → `AuthCubit.logout()`.

Mevcut ayrı "Gizlilik" ve "Ayarlar & Güvenlik" tile'ları **tek "Gizlilik ve Güvenlik"** altında birleştirilir; "Abonelik" pasif satırı **"Aboneliğim"** olur.

- [ ] **Step 2: Analyze + manuel duman testi**

Run: `cd mobile && flutter analyze`
Beklenen: yeni hata yok. Manuel: premium/free hero ayrımı; 7 menü satırı doğru hedeflere; Çıkış onayı çalışır.

- [ ] **Step 3: Doküman + commit**

`doc/pages/study_student.md` Profil bölümü + `doc/roles/ogrenci.md` profil akışı güncelle (alt tarih `2026-08-19`).

```bash
git add mobile/lib/features/study/presentation/pages/student_profile_page.dart doc/pages/study_student.md doc/roles/ogrenci.md
git commit -m "feat(mobile): Profil — premium hero + Aboneliğim + birleşik Gizlilik&Güvenlik (Task 8)"
```

---

## Kapanış: Doküman + son analiz

- [ ] **Step 1: mobile_flutter.md nav güncellemesi**

`doc/architecture/mobile_flutter.md`'de öğrenci nav yapısını 4 sekme + Kronometre/Ders Detayı push sayfalarıyla güncelle (alt tarih `2026-08-19`).

- [ ] **Step 2: Tüm testler + analyze**

Run: `cd mobile && flutter analyze && flutter test test/features/study`
Beklenen: analyze yeni hata yok; yeni saf testler (Task 1/2/3/4/5/6/7) yeşil. (Önceden bozuk 6 auth-fake testi kapsam dışı.)

- [ ] **Step 3: Kapanış commit**

```bash
git add doc/architecture/mobile_flutter.md
git commit -m "docs(mobile): öğrenci 4-sekme + Kronometre/Ders Detayı nav dokümanı"
```

---

## Self-Review notları (yazım sonrası)

- **Spec kapsamı:** §2 nav→Task 1 · §kart dili→Task 2 · §3.1 Çalışma→Task 3 · §3.2 Kronometre→Task 4 · §3.4 Ders Detayı→Task 5 · §3.3 Derslerim→Task 6 · §3.5 Performans→Task 7 · §3.6 Profil→Task 8. Tümü karşılanıyor.
- **Bağımlılık sırası:** Task 2 (widget'lar) 3-8'den önce; Task 5 (Ders Detayı rota+sayfa) Task 6 (Derslerim linki) öncesinde → link derlenir.
- **Tip tutarlılığı:** `StudentNavTab` üye adları (`work/lessons/performance/profile/none`) tüm görevlerde sabit; `LessonFilter`/`LessonDetailPermissions`/`TimerAccumulator`/`WeeklyStat` imzaları tanımlandığı görevle tüketildiği görev arasında aynı.
- **Demo işaretleme:** Backend'i olmayan her alan `StudyDemoBadge` ile işaretli (constraint gereği).
- **Kod gerçeği kapıları:** Mevcut state/repo alan adları (ör. `StudyHomeState.todayMinutes`, `SchedulingRepository` tekil ders getirme) her görevin ilk adımında koddan doğrulanır; imza farklıysa plan koda uyarlanır (kod doğruluk kaynağı).
