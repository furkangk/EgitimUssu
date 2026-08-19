import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Profil sekmesi (ogrenci_ux §11) — 5-sekme alt navigasyonun kök ekranı.
/// Premium/free ayrımlı hero (ad, sınıf, seri) + istatistik özeti (toplam
/// çalışma, toplam test/net, en çok çalışılan ders, seri gün, rozetler) +
/// Ayarlar menüsü (Velim/Öğretmenlerim/Hedef ekle/Bildirim/Gizlilik ve
/// Güvenlik/Aboneliğim) + Çıkış yap. İstatistik ve sınıf değerleri mevcut
/// Study/Student API verisinden türetilir; premium, profil düzenleme, veli
/// bağlantısı, bildirim ayarları ve abonelik backend'i henüz yok → demo.
class StudentProfilePage extends StatefulWidget {
  const StudentProfilePage({super.key});

  @override
  State<StudentProfilePage> createState() => _StudentProfilePageState();
}

class _StudentProfilePageState extends State<StudentProfilePage> {
  StudyRepository get _repo => injector<StudyRepository>();
  StudentRepository get _studentRepo => injector<StudentRepository>();

  String? _studentId;
  StudentProfile? _profile;
  StudyStreak? _streak;
  List<StudySession> _sessions = const <StudySession>[];
  List<TestResult> _tests = const <TestResult>[];
  List<StudyAchievement> _achievements = const <StudyAchievement>[];
  bool _loading = true;
  String? _error;

  /// Faz 5'e kadar demo/sabit: premium abonelik backend'i henüz yok.
  static const bool _isPremium = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = context.read<AuthCubit>().state.session;
      final userId = session?.userId ?? '';
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: userId,
            fullName: session?.fullName ?? '',
          );
      final streak = await _repo.getStreak(studentId);
      final sessions = await _repo.listSessions(studentId);
      final tests = await _repo.listTests(studentId);
      final achievements = await _repo.getAchievements(studentId);
      final profile = await _studentRepo.getByUser(userId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _profile = profile;
        _streak = streak;
        _sessions =
            sessions.where((StudySession s) => s.status == 'Completed').toList();
        _tests = tests;
        _achievements = achievements;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          _loading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
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
  }

  Widget _content() {
    final StudyStreak streak = _streak!;
    final String studentId = _studentId!;
    final session = context.read<AuthCubit>().state.session;
    final String name = (session?.fullName.trim().isNotEmpty ?? false)
        ? session!.fullName.trim()
        : 'Öğrenci';
    final String gradeLevel = _profile?.gradeLevel.trim().isNotEmpty ?? false
        ? _profile!.gradeLevel.trim()
        : 'Sınıf bilgisi girilmedi';
    final String? goalSummary = _profile?.goalSummary;

    final int totalMinutes = _sessions
        .fold<int>(0, (int s, StudySession x) => s + x.effectiveMinutes);
    final double totalNet =
        _tests.fold<double>(0, (double s, TestResult t) => s + t.net);
    final String mostSubject = _mostStudiedSubject();
    final int earnedBadges =
        _achievements.where((StudyAchievement a) => a.earned).length;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: <Widget>[
          const AppPageHeader(
            title: 'Profil',
            subtitle: 'Bilgilerin, ayarların ve istatistiklerin.',
          ),
          const SizedBox(height: 16),
          _ProfileHero(
            name: name,
            gradeLevel: gradeLevel,
            goalSummary: goalSummary,
            streakDays: streak.currentStreakDays,
            isPremium: _isPremium,
            onEdit: () => _showPlaceholderSheet(
              context,
              icon: Icons.edit_rounded,
              color: AppColors.primary,
              title: 'Profili düzenle',
              message:
                  'Ad, fotoğraf, sınıf ve hedef sınav bilgilerini düzenleme '
                  'özelliği yakında burada olacak.',
            ),
            onUpgrade: () => _showPlaceholderSheet(
              context,
              icon: Icons.workspace_premium_rounded,
              color: AppColors.primary,
              title: "Premium'e yükselt",
              message: 'Sınırsız deneme takibi, gelişmiş analiz ve öncelikli '
                  'destek Faz 5 abonelik planlarıyla birlikte gelecek.',
            ),
          ),
          const SizedBox(height: 20),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.timelapse_rounded,
                  color: AppColors.accentTeal,
                  value: StudyFormat.minutes(totalMinutes),
                  label: 'Toplam çalışma',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.event_available_rounded,
                  color: AppColors.accentGreen,
                  value: '${streak.totalStudyDays}',
                  label: 'Çalışılan gün',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.emoji_events_rounded,
                  color: AppColors.accentOrange,
                  value: '${streak.longestStreakDays}',
                  label: 'Rekor seri',
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.assignment_turned_in_rounded,
                  color: AppColors.accentBlue,
                  value: '${_tests.length}',
                  label: 'Toplam deneme',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.calculate_rounded,
                  color: AppColors.primary,
                  value: StudyFormat.net(totalNet),
                  label: 'Toplam net',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.menu_book_rounded,
                  color: AppColors.accentTeal,
                  value: mostSubject,
                  label: 'En çok çalışılan',
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
          StudySectionHeader(
            title: 'Rozetler',
            action: StudySectionAction(
              label: 'Tümü',
              onTap: () =>
                  context.push('/study/achievements?studentId=$studentId'),
            ),
          ),
          const SizedBox(height: 12),
          _BadgeSummary(
            earned: earnedBadges,
            total: _achievements.length,
            onTap: () =>
                context.push('/study/achievements?studentId=$studentId'),
          ),
          const SizedBox(height: 24),
          const StudySectionHeader(title: 'Ayarlar'),
          const SizedBox(height: 12),
          _ProfileMenuTile(
            icon: Icons.family_restroom_rounded,
            color: AppColors.accentBlue,
            title: 'Velim',
            subtitle: 'Veli bağlantısı ve paylaşım',
            trailingBadge: const StudyDemoBadge(),
            onTap: () => _showPlaceholderSheet(
              context,
              icon: Icons.family_restroom_rounded,
              color: AppColors.accentBlue,
              title: 'Velim',
              message: 'Veli hesabı bağlama ve ilerleme paylaşımı özelliği '
                  'yakında burada olacak.',
            ),
          ),
          _ProfileMenuTile(
            icon: Icons.school_rounded,
            color: AppColors.accentTeal,
            title: 'Öğretmenlerim',
            subtitle: 'Bağlı öğretmenlerin ve iletişim',
            onTap: () => context.push('/student/teacher'),
          ),
          _ProfileMenuTile(
            icon: Icons.flag_rounded,
            color: AppColors.accentGreen,
            title: 'Hedef ekle',
            subtitle: 'Yeni bir çalışma hedefi oluştur',
            onTap: () => context.push('/study/goals?studentId=$studentId'),
          ),
          _ProfileMenuTile(
            icon: Icons.notifications_rounded,
            color: AppColors.accentOrange,
            title: 'Bildirim ayarları',
            subtitle: 'Hatırlatma ve uyarı tercihleri',
            trailingBadge: const StudyDemoBadge(),
            onTap: () => _showPlaceholderSheet(
              context,
              icon: Icons.notifications_rounded,
              color: AppColors.accentOrange,
              title: 'Bildirim ayarları',
              message: 'Çalışma hatırlatmaları ve uyarı tercihleri yakında '
                  'burada olacak.',
            ),
          ),
          _ProfileMenuTile(
            icon: Icons.security_rounded,
            color: AppColors.accentGreen,
            title: 'Gizlilik ve Güvenlik',
            subtitle: 'Paylaşım izinleri, hesap ve oturum',
            onTap: () => _showPrivacySecuritySheet(context),
          ),
          _ProfileMenuTile(
            icon: Icons.workspace_premium_rounded,
            color: AppColors.primary,
            title: 'Aboneliğim',
            subtitle: 'Faz 5 — plan ve avantajlar',
            trailingBadge: const StudyDemoBadge(),
            onTap: () => _showPlaceholderSheet(
              context,
              icon: Icons.workspace_premium_rounded,
              color: AppColors.primary,
              title: 'Aboneliğim',
              message: 'Premium abonelik planları ve avantajları Faz 5\'te '
                  'burada sunulacak.',
            ),
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
        ],
      ),
    );
  }

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

  /// Backend'i henüz olmayan menü/hero eylemleri için ortak "yer tutucu"
  /// bilgi sayfası (Task 8 kapsam kararı — bkz. task-8-brief.md).
  void _showPlaceholderSheet(
    BuildContext context, {
    required IconData icon,
    required Color color,
    required String title,
    required String message,
  }) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (BuildContext sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
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
                    child: Text(title,
                        style: Theme.of(sheetContext)
                            .textTheme
                            .titleLarge
                            ?.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w800)),
                  ),
                  const SizedBox(width: 8),
                  const StudyDemoBadge(),
                ],
              ),
              const SizedBox(height: 14),
              Text(message,
                  style: Theme.of(sheetContext).textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary, height: 1.4)),
              const SizedBox(height: 18),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton(
                  onPressed: () => Navigator.of(sheetContext).pop(),
                  child: const Text('Kapat'),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  /// Task 8: eski ayrı "Gizlilik" ve "Ayarlar & Güvenlik" satırlarının
  /// birleştiği tek menü — gizlilik izinleri demo, hesap bilgileri gerçek
  /// `/account-info` sayfasına gider.
  void _showPrivacySecuritySheet(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (BuildContext sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text('Gizlilik ve Güvenlik',
                  style: Theme.of(sheetContext).textTheme.titleLarge?.copyWith(
                      color: AppColors.primary, fontWeight: FontWeight.w800)),
              const SizedBox(height: 14),
              _SheetOptionTile(
                icon: Icons.shield_rounded,
                color: AppColors.accentGreen,
                title: 'Gizlilik ayarları',
                subtitle: 'Veli/öğretmen paylaşım izinleri',
                trailingBadge: const StudyDemoBadge(),
                onTap: () {
                  ScaffoldMessenger.of(sheetContext).showSnackBar(
                    const SnackBar(
                        content:
                            Text('Gizlilik ayarları özelliği yakında.')),
                  );
                },
              ),
              const SizedBox(height: 10),
              _SheetOptionTile(
                icon: Icons.manage_accounts_outlined,
                color: AppColors.accentTeal,
                title: 'Hesap bilgileri',
                subtitle: 'E-posta, rol ve oturum',
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  context.push('/account-info');
                },
              ),
            ],
          ),
        );
      },
    );
  }

  String _mostStudiedSubject() {
    if (_sessions.isEmpty) return '—';
    final Map<String, int> minutes = <String, int>{};
    for (final StudySession s in _sessions) {
      minutes[s.subject] = (minutes[s.subject] ?? 0) + s.effectiveMinutes;
    }
    String best = '—';
    int bestMinutes = -1;
    minutes.forEach((String subject, int total) {
      if (total > bestMinutes) {
        bestMinutes = total;
        best = subject;
      }
    });
    return best;
  }
}

/// Profil hero kartı — premium'da altın gradyan kenarlık + "Premium" rozeti,
/// free'de sade kart + "Yükselt" ipucu. Fotoğraf alanı henüz backend'de yok
/// → avatar her zaman baş harflerle gösterilir.
class _ProfileHero extends StatelessWidget {
  const _ProfileHero({
    required this.name,
    required this.gradeLevel,
    required this.goalSummary,
    required this.streakDays,
    required this.isPremium,
    required this.onEdit,
    required this.onUpgrade,
  });

  final String name;
  final String gradeLevel;
  final String? goalSummary;
  final int streakDays;
  final bool isPremium;
  final VoidCallback onEdit;
  final VoidCallback onUpgrade;

  @override
  Widget build(BuildContext context) {
    final String initials = name.trim().isEmpty
        ? '?'
        : name
            .trim()
            .split(RegExp(r'\s+'))
            .take(2)
            .map((String p) => p[0].toUpperCase())
            .join();
    final bool hasGoal = goalSummary != null && goalSummary!.trim().isNotEmpty;
    final String subtitle =
        hasGoal ? '$gradeLevel · ${goalSummary!.trim()}' : gradeLevel;

    return Container(
      padding: const EdgeInsets.all(2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(24),
        gradient: isPremium
            ? const LinearGradient(
                colors: <Color>[
                  Color(0xFFFFE29A),
                  AppColors.amber,
                  Color(0xFFB8860B),
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              )
            : null,
        color: isPremium ? null : AppColors.skyBorder,
      ),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(22),
          boxShadow: AppShadows.soft,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _Avatar(initials: initials, isPremium: isPremium),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Row(
                        children: <Widget>[
                          Flexible(
                            child: Text(name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                    color: AppColors.textPrimary,
                                    fontSize: 19)),
                          ),
                          if (isPremium) ...<Widget>[
                            const SizedBox(width: 6),
                            const _PremiumBadge(),
                          ],
                        ],
                      ),
                      const SizedBox(height: 3),
                      Text(subtitle,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                              color: AppColors.textSecondary, fontSize: 13)),
                      const SizedBox(height: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: AppColors.warningSurfaceStrong,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            const Text('🔥', style: TextStyle(fontSize: 12)),
                            const SizedBox(width: 4),
                            Text(
                                streakDays > 0
                                    ? '$streakDays gün seri'
                                    : 'Bugün çalışmaya başla',
                                style: const TextStyle(
                                    color: AppColors.warning,
                                    fontSize: 11,
                                    fontWeight: FontWeight.w700)),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  onPressed: onEdit,
                  tooltip: 'Profili düzenle',
                  icon: const Icon(Icons.edit_rounded,
                      color: AppColors.textSecondary, size: 20),
                ),
              ],
            ),
            if (!isPremium) ...<Widget>[
              const SizedBox(height: 12),
              InkWell(
                borderRadius: BorderRadius.circular(14),
                onTap: onUpgrade,
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  decoration: BoxDecoration(
                    color: AppColors.primaryLight,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Row(
                    children: <Widget>[
                      const Icon(Icons.workspace_premium_rounded,
                          color: AppColors.primary, size: 18),
                      const SizedBox(width: 8),
                      const Expanded(
                        child: Text(
                            "Premium'e yükselt: sınırsız deneme takibi ve "
                            'gelişmiş analiz',
                            style: TextStyle(
                                color: AppColors.primary,
                                fontWeight: FontWeight.w700,
                                fontSize: 12)),
                      ),
                      const SizedBox(width: 6),
                      const StudyDemoBadge(),
                      const SizedBox(width: 4),
                      const Icon(Icons.chevron_right_rounded,
                          color: AppColors.primary, size: 18),
                    ],
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.initials, required this.isPremium});

  final String initials;
  final bool isPremium;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 60,
      height: 60,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        shape: BoxShape.circle,
        border: isPremium
            ? Border.all(color: AppColors.amber, width: 2.5)
            : null,
      ),
      child: Text(initials,
          style: const TextStyle(
              color: AppColors.primary,
              fontWeight: FontWeight.w800,
              fontSize: 20)),
    );
  }
}

class _PremiumBadge extends StatelessWidget {
  const _PremiumBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.amber.withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: AppColors.amber),
      ),
      child: const Text('Premium',
          style: TextStyle(
              color: Color(0xFFB8860B),
              fontSize: 10,
              fontWeight: FontWeight.w800)),
    );
  }
}

class _BadgeSummary extends StatelessWidget {
  const _BadgeSummary({
    required this.earned,
    required this.total,
    required this.onTap,
  });

  final int earned;
  final int total;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final double ratio = total <= 0 ? 0 : (earned / total).clamp(0.0, 1.0);
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: StudyCard(
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: AppColors.accentOrange.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Icon(Icons.workspace_premium_rounded,
                  color: AppColors.accentOrange),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text('$earned / $total rozet kazanıldı',
                      style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary)),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(
                      value: ratio,
                      minHeight: 8,
                      backgroundColor: AppColors.divider,
                      valueColor: const AlwaysStoppedAnimation<Color>(
                          AppColors.accentOrange),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            const Icon(Icons.chevron_right_rounded,
                color: AppColors.textSecondary),
          ],
        ),
      ),
    );
  }
}

/// Profil ayarlar menüsü satırı; onTap null ise pasif (yakında) görünür.
/// [trailingBadge] varsa (ör. [StudyDemoBadge]) chevron'dan önce gösterilir.
class _ProfileMenuTile extends StatelessWidget {
  const _ProfileMenuTile({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.titleColor,
    this.trailingBadge,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;
  final Color? titleColor;
  final Widget? trailingBadge;

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
                if (trailingBadge != null) ...<Widget>[
                  trailingBadge!,
                  const SizedBox(width: 6),
                ],
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

/// Alt sayfa (bottom sheet) içinde kullanılan basit seçenek satırı —
/// [_ProfileMenuTile]'ın daha sade sürümü (kart çerçevesiz).
class _SheetOptionTile extends StatelessWidget {
  const _SheetOptionTile({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.trailingBadge,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final Widget? trailingBadge;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: AppColors.tabBackground,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 40,
              height: 40,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(icon, color: color, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(title,
                      style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary)),
                  const SizedBox(height: 2),
                  Text(subtitle,
                      style: const TextStyle(
                          color: AppColors.textSecondary, fontSize: 12)),
                ],
              ),
            ),
            if (trailingBadge != null) ...<Widget>[
              trailingBadge!,
              const SizedBox(width: 6),
            ],
            const Icon(Icons.chevron_right_rounded,
                color: AppColors.textSecondary),
          ],
        ),
      ),
    );
  }
}
