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
