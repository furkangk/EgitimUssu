import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// "Öğretmenim" ekranı (ogrenci_ux §10) — "Diğer" hub'ından açılır (push).
/// Yalnızca öğrencinin bağlı olduğu öğretmen(ler)i gösterir (dersler artık burada
/// listelenmez; onlar Takvim ekranındadır). Bağlı öğretmenler güvenli öğrenci-kapsamlı
/// endpoint'ten (`/scheduling/students/{id}/lessons`, IStudentDirectory ile IDOR korumalı)
/// gelen derslerin [LessonSchedule.teacherUserId] kümesinden türetilir; her öğretmenin
/// profili `/api/teachers/profiles/{userId}` ile getirilip kart olarak gösterilir.
class StudentTeacherPage extends StatefulWidget {
  const StudentTeacherPage({super.key});

  @override
  State<StudentTeacherPage> createState() => _StudentTeacherPageState();
}

class _StudentTeacherPageState extends State<StudentTeacherPage> {
  SchedulingRepository get _repo => injector<SchedulingRepository>();
  TeacherRepository get _teacherRepo => injector<TeacherRepository>();

  String? _studentId;
  List<TeacherProfile> _teachers = const <TeacherProfile>[];
  bool _loading = true;
  String? _error;

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
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final lessons = await _repo.listStudentLessons(studentId: studentId);
      final teachers = await _loadTeachers(lessons);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _teachers = teachers;
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

  /// Derslerdeki benzersiz [LessonSchedule.teacherUserId] kümesinden öğretmen
  /// profillerini paralel çeker. Profili alınamayan öğretmen atlanır (dersler yine
  /// gösterilir), sonuç ada göre sıralanır.
  Future<List<TeacherProfile>> _loadTeachers(List<LessonSchedule> lessons) async {
    final List<String> userIds = lessons
        .map((LessonSchedule l) => l.teacherUserId)
        .where((String id) => id.isNotEmpty)
        .toSet()
        .toList();
    final List<TeacherProfile> result = <TeacherProfile>[];
    await Future.wait(userIds.map((String id) async {
      try {
        result.add(await _teacherRepo.getProfile(id));
      } on ApiException {
        // Profili alınamayan öğretmeni sessizce atla.
      }
    }));
    result.sort((TeacherProfile a, TeacherProfile b) =>
        a.fullName.toLowerCase().compareTo(b.fullName.toLowerCase()));
    return result;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Öğretmenlerim')),
      body: _loading
          ? const LoadingStateView(message: 'Öğretmenlerin yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
    );
  }

  Widget _content() {
    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: <Widget>[
          if (_teachers.isNotEmpty) ...<Widget>[
            Text('${_teachers.length} öğretmenle çalışıyorsun',
                style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 14,
                    fontWeight: FontWeight.w600)),
            const SizedBox(height: 14),
          ],
          if (_teachers.isEmpty)
            const EmptyStateView(
              title: 'Henüz öğretmenin yok',
              subtitle:
                  'Bir öğretmen seninle ders planladığında bilgileri burada görünecek.',
            )
          else
            ..._teachers.map((TeacherProfile t) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _TeacherCard(teacher: t),
                )),
        ],
      ),
    );
  }
}

/// Öğrencinin bağlı olduğu bir öğretmenin bilgi kartı. Uygulamanın gradient hero
/// dilini (ana sayfa çalışma kartı) izler: navy→mavi degrade başlık bandında avatar +
/// ad + doğrulama + branş rozeti; altında beyaz gövdede deneyim/branş/ücret istatistik
/// satırı, konum/format meta bilgisi ve "Hakkında" bölümü.
class _TeacherCard extends StatefulWidget {
  const _TeacherCard({required this.teacher});

  final TeacherProfile teacher;

  @override
  State<_TeacherCard> createState() => _TeacherCardState();
}

class _TeacherCardState extends State<_TeacherCard> {
  bool _expanded = false;

  TeacherProfile get teacher => widget.teacher;

  String get _initials {
    final List<String> parts = teacher.fullName
        .trim()
        .split(RegExp(r'\s+'))
        .where((String p) => p.isNotEmpty)
        .toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }

  String _formatLabel(String format) => switch (format) {
        'InPerson' => 'Yüz yüze',
        'Online' => 'Online',
        _ => 'Online + Yüz yüze',
      };

  String _currencySymbol(String currency) => switch (currency.toUpperCase()) {
        'TRY' => '₺',
        'USD' => '\$',
        'EUR' => '€',
        _ => currency,
      };

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // Yalnızca başlık her zaman görünür; ok'a (ya da başlığa) dokununca gövde açılır.
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            child: _header(),
          ),
          AnimatedSize(
            duration: const Duration(milliseconds: 220),
            curve: Curves.easeInOut,
            alignment: Alignment.topCenter,
            child: _expanded
                ? _body()
                : const SizedBox(width: double.infinity),
          ),
        ],
      ),
    );
  }

  /// Açıldığında görünen gövde: deneyim/eğitim/ücret + konum/format + "Hakkında".
  Widget _body() {
    final bool hasBio =
        teacher.biography != null && teacher.biography!.trim().isNotEmpty;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _statRow(),
          const SizedBox(height: 14),
          _metaRow(),
          if (hasBio) ...<Widget>[
            const SizedBox(height: 14),
            const Divider(height: 1, color: AppColors.divider),
            const SizedBox(height: 12),
            const Text('Hakkında',
                style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 13,
                    color: AppColors.textPrimary)),
            const SizedBox(height: 5),
            Text(teacher.biography!.trim(),
                style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    height: 1.5)),
          ],
        ],
      ),
    );
  }

  /// Navy→mavi degrade başlık bandı: avatar + ad + doğrulama + branş rozeti.
  Widget _header() {
    final String headline =
        (teacher.headline != null && teacher.headline!.trim().isNotEmpty)
            ? teacher.headline!.trim()
            : teacher.subject;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(16, 18, 16, 18),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[AppColors.primary, AppColors.secondary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          _avatar(),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Flexible(
                      child: Text(teacher.fullName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 18,
                              color: Colors.white)),
                    ),
                    if (teacher.isVerified) ...<Widget>[
                      const SizedBox(width: 6),
                      const Icon(Icons.verified_rounded,
                          color: Colors.white, size: 18),
                    ],
                  ],
                ),
                if (headline.trim().isNotEmpty) ...<Widget>[
                  const SizedBox(height: 3),
                  Text(headline,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          color: Colors.white70,
                          fontSize: 12.5,
                          height: 1.3)),
                ],
                if (teacher.subject.trim().isNotEmpty) ...<Widget>[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        const Icon(Icons.menu_book_rounded,
                            color: Colors.white, size: 13),
                        const SizedBox(width: 5),
                        Text(teacher.subject,
                            style: const TextStyle(
                                color: Colors.white,
                                fontSize: 11.5,
                                fontWeight: FontWeight.w700)),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 8),
          // Detayları aç/kapat oku — açıkken 180° döner.
          Container(
            width: 32,
            height: 32,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.16),
              shape: BoxShape.circle,
            ),
            child: AnimatedRotation(
              turns: _expanded ? 0.5 : 0.0,
              duration: const Duration(milliseconds: 220),
              curve: Curves.easeInOut,
              child: const Icon(Icons.keyboard_arrow_down_rounded,
                  color: Colors.white, size: 22),
            ),
          ),
        ],
      ),
    );
  }

  /// Deneyim / Branş / Ücret — beyaz gövdede 3'lü istatistik satırı.
  Widget _statRow() {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Expanded(
          child: _TeacherStat(
            icon: Icons.workspace_premium_rounded,
            color: AppColors.accentOrange,
            value: teacher.experienceYears > 0
                ? '${teacher.experienceYears} yıl'
                : '—',
            label: 'Deneyim',
          ),
        ),
        const _StatDivider(),
        Expanded(
          child: _TeacherStat(
            icon: Icons.school_rounded,
            color: AppColors.accentBlue,
            value: teacher.educationLevel.trim().isNotEmpty
                ? teacher.educationLevel.trim()
                : '—',
            label: 'Eğitim',
          ),
        ),
        const _StatDivider(),
        Expanded(
          child: _TeacherStat(
            icon: Icons.payments_rounded,
            color: AppColors.accentGreen,
            value: teacher.hourlyRateAmount > 0
                ? '${StudyFormat.net(teacher.hourlyRateAmount)} ${_currencySymbol(teacher.currency)}'
                : '—',
            label: 'Saatlik',
          ),
        ),
      ],
    );
  }

  /// Konum + ders formatı satır-içi meta bilgisi.
  Widget _metaRow() {
    final String location = teacher.district.trim().isNotEmpty
        ? '${teacher.city} · ${teacher.district}'
        : teacher.city;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: AppColors.tabBackground,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: <Widget>[
          if (location.trim().isNotEmpty)
            Expanded(
              child: _TeacherMeta(
                  icon: Icons.location_on_rounded, label: location),
            ),
          _TeacherMeta(
              icon: Icons.cast_for_education_rounded,
              label: _formatLabel(teacher.lessonFormat)),
        ],
      ),
    );
  }

  Widget _avatar() {
    final String? photo = teacher.profilePhotoUrl;
    final Widget inner = (photo != null && photo.isNotEmpty)
        ? ClipOval(
            child: Image.network(photo,
                width: 56, height: 56, fit: BoxFit.cover))
        : Text(_initials,
            style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w800,
                fontSize: 20));
    return Container(
      width: 56,
      height: 56,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.16),
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white.withValues(alpha: 0.55), width: 2),
      ),
      child: inner,
    );
  }
}

/// Öğretmen kartındaki istatistik satırının tek bir sütunu (ikon + değer + etiket).
class _TeacherStat extends StatelessWidget {
  const _TeacherStat({
    required this.icon,
    required this.color,
    required this.value,
    required this.label,
  });

  final IconData icon;
  final Color color;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.14),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Icon(icon, color: color, size: 19),
        ),
        const SizedBox(height: 8),
        Text(value,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
                fontWeight: FontWeight.w800,
                fontSize: 14,
                color: AppColors.textPrimary)),
        const SizedBox(height: 2),
        Text(label,
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
      ],
    );
  }
}

/// İstatistik sütunları arasındaki ince dikey ayraç.
class _StatDivider extends StatelessWidget {
  const _StatDivider();

  @override
  Widget build(BuildContext context) {
    return Container(width: 1, height: 38, color: AppColors.divider);
  }
}

/// Satır-içi meta öğesi (ikon + kısa etiket).
class _TeacherMeta extends StatelessWidget {
  const _TeacherMeta({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 15, color: AppColors.textSecondary),
        const SizedBox(width: 6),
        Flexible(
          child: Text(label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                  fontSize: 12.5,
                  color: AppColors.textSecondary,
                  fontWeight: FontWeight.w600)),
        ),
      ],
    );
  }
}
