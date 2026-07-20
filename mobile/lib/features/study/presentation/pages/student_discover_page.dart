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
        ),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.discover),
    );
  }
}
