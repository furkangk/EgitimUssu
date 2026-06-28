import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Tum ana ekranlarda ortak kullanilan alt navigasyon menusu (master page
/// mantigi). Sekme seti, ikonlar, renkler ve hedef rotalar tek yerde
/// tanimlidir; buradaki bir degisiklik tum sayfalara yansir.
///
/// Kullanim: ilgili sayfa yalnizca aktif sekmeyi bildirir:
/// `bottomNavigationBar: const AppBottomNav(current: AppNavTab.home)`.
/// Aktif sekmeye tekrar dokunmak bir sey yapmaz; digerleri `context.go` ile
/// ilgili rotaya gider. Hicbir ana sekmeye karsilik gelmeyen alt sayfalar
/// [AppNavTab.none] kullanir (hicbiri secili gorunmez).
enum AppNavTab {
  home(Icons.home_rounded, 'Ana sayfa', '/dashboard'),
  lessons(Icons.menu_book_rounded, 'Dersler', '/lesson-sessions'),
  students(Icons.groups_rounded, 'Öğrenciler', '/students'),
  schedule(Icons.calendar_month_rounded, 'Takvim', '/scheduling'),
  finance(Icons.account_balance_wallet_rounded, 'Finans', '/payments'),
  more(Icons.widgets_rounded, 'Diğer', '/more'),

  /// Ana sekmelerden birine karsilik gelmeyen sayfalar icin: menu gosterilir
  /// ama hicbir sekme secili degildir.
  none(Icons.circle, '', '');

  const AppNavTab(this.icon, this.label, this.route);

  final IconData icon;
  final String label;
  final String route;
}

class AppBottomNav extends StatelessWidget {
  const AppBottomNav({super.key, required this.current});

  /// Aktif (secili) sekme. Eslesme yoksa [AppNavTab.none] verilir.
  final AppNavTab current;

  static const List<AppNavTab> _tabs = <AppNavTab>[
    AppNavTab.home,
    AppNavTab.lessons,
    AppNavTab.students,
    AppNavTab.schedule,
    AppNavTab.finance,
    AppNavTab.more,
  ];

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.divider)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: _tabs.map((tab) {
          final selected = tab == current;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap: selected ? null : () => context.go(tab.route),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(
                      tab.icon,
                      color: selected
                          ? AppColors.primary
                          : AppColors.textSecondary,
                    ),
                    const SizedBox(height: 4),
                    FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Text(
                        tab.label,
                        maxLines: 1,
                        style: Theme.of(context).textTheme.labelMedium?.copyWith(
                          color: selected
                              ? AppColors.primary
                              : AppColors.textSecondary,
                          fontWeight: selected
                              ? FontWeight.w800
                              : FontWeight.w600,
                          fontSize: 11,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}
