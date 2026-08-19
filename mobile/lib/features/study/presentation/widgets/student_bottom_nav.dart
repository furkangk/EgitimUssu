import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

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

class StudentBottomNav extends StatelessWidget {
  const StudentBottomNav({super.key, required this.current});

  final StudentNavTab current;

  static const List<StudentNavTab> _tabs = <StudentNavTab>[
    StudentNavTab.work,
    StudentNavTab.lessons,
    StudentNavTab.performance,
    StudentNavTab.profile,
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
