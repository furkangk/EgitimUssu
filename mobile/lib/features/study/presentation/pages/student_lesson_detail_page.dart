import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/scheduling_format.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/lessons/lesson_detail_permissions.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Ders Detayı — push sayfa (`/student/lessons/:id`, spec §3.4, plan Task 5).
///
/// Ç-06 ile birleşen [LessonSchedule] modelinde öğretmen dersi ile öğrencinin kendi
/// (öğretmensiz) dersi aynı kayıttır; ayrım backend'de `TeacherUserId is null` iledir.
/// Dart tarafında bu alan (`LessonScheduleModel.fromJson`) `null` yerine `''` boş
/// dize kullanır (bkz. `lesson_schedule_model.dart`, `student_teacher_page.dart`
/// `.isNotEmpty` filtresiyle aynı sözleşme) — bu yüzden sahiplik burada
/// `teacherUserId.isEmpty` ile belirlenir; brief'teki `== null` bunun mobil karşılığıdır.
///
/// Öğretmen dersinde (`!isOwn`) öğretmen bilgisi kartı gösterilir, ödev/konu ekleme
/// gizlenir (bkz. [LessonDetailPermissions]); kendi derste tüm hızlı erişimler açıktır.
class StudentLessonDetailPage extends StatefulWidget {
  const StudentLessonDetailPage({super.key, required this.lessonId});

  final String lessonId;

  @override
  State<StudentLessonDetailPage> createState() =>
      _StudentLessonDetailPageState();
}

class _StudentLessonDetailPageState extends State<StudentLessonDetailPage> {
  String? _studentId;
  LessonSchedule? _lesson;
  TeacherProfile? _teacher;
  List<AssignmentItem> _assignments = const <AssignmentItem>[];
  List<TestResult> _tests = const <TestResult>[];
  List<SubjectCatalog> _subjects = const <SubjectCatalog>[];
  List<StudyNote> _notes = const <StudyNote>[];
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
      final studentId =
          _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final lesson = await injector<SchedulingRepository>().getLesson(
        widget.lessonId,
      );
      final bool isOwn = lesson.teacherUserId.trim().isEmpty;

      TeacherProfile? teacher;
      if (!isOwn) {
        try {
          teacher = await injector<TeacherRepository>().getProfile(
            lesson.teacherUserId,
          );
        } on ApiException {
          teacher = null; // Profil alınamazsa kart sessizce gizlenir.
        }
      }

      final assignments = await injector<AssignmentRepository>()
          .listByStudent(studentId);
      final tests = await injector<StudyRepository>().listTests(studentId);
      final subjects = await injector<StudyRepository>().listSubjects(
        studentId,
      );
      final notes = await injector<StudyRepository>().listNotes(studentId);

      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _lesson = lesson;
        _teacher = teacher;
        _assignments = assignments;
        _tests = tests;
        _subjects = subjects;
        _notes = notes;
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
      appBar: AppBar(title: const Text('Ders Detayı')),
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Ders yükleniyor...')
            : _error != null
            ? ErrorStateView(message: _error!, onRetry: _load)
            : _content(),
      ),
    );
  }

  Widget _content() {
    final lesson = _lesson;
    if (lesson == null) {
      return const ErrorStateView(message: 'Ders bulunamadı.');
    }
    final bool isOwn = lesson.teacherUserId.trim().isEmpty;
    final perms = LessonDetailPermissions.forOwnership(isOwn);
    final studentId = _studentId ?? '';

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 32),
        children: <Widget>[
          _HeaderCard(lesson: lesson, isOwn: isOwn),
          if (!isOwn) ...<Widget>[
            const SizedBox(height: 14),
            _TeacherCard(teacher: _teacher),
          ],
          const SizedBox(height: 20),
          const StudySectionHeader(title: 'Hızlı erişim'),
          const SizedBox(height: 12),
          _QuickAccessGrid(lesson: lesson, perms: perms, studentId: studentId),
          const SizedBox(height: 24),
          _AssignmentsSection(items: _assignments),
          const SizedBox(height: 24),
          _TestsSection(tests: _tests, lesson: lesson),
          const SizedBox(height: 24),
          _TopicsSection(
            subjects: _subjects,
            lesson: lesson,
            perms: perms,
            studentId: studentId,
          ),
          const SizedBox(height: 24),
          _NotesSection(notes: _notes, lesson: lesson, studentId: studentId),
        ],
      ),
    );
  }
}

String _lessonFormatLabel(String format) => switch (format) {
  'InPerson' => 'Yüz yüze',
  'Online' => 'Online',
  _ => 'Online + Yüz yüze',
};

String _statusLabel(String status) => switch (status) {
  'Completed' => 'Tamamlandı',
  'Cancelled' => 'İptal edildi',
  'Planned' => 'Planlandı',
  _ => status,
};

Color _statusColor(String status) => switch (status) {
  'Completed' => AppColors.accentGreen,
  'Cancelled' => AppColors.accentRed,
  _ => AppColors.accentBlue,
};

/// Başlık kartı — ders adı, tarih/saat, tür (yalnız öğretmen dersinde; kendi derste
/// [LessonSchedule.lessonFormat] backend'de yoktur, bkz. sınıf dokümantasyonu), sahiplik
/// rozeti, durum.
class _HeaderCard extends StatelessWidget {
  const _HeaderCard({required this.lesson, required this.isOwn});

  final LessonSchedule lesson;
  final bool isOwn;

  @override
  Widget build(BuildContext context) {
    final DateTime startLocal = lesson.startAtUtc.toLocal();
    final bool hasLocation = (lesson.locationLabel ?? '').trim().isNotEmpty;
    final bool hasMeetingUrl = (lesson.meetingUrl ?? '').trim().isNotEmpty;

    return StudyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  lesson.subject,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              StudyOwnershipBadge(isOwn: isOwn),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: <Widget>[
              const Icon(
                Icons.event_rounded,
                size: 15,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  '${SchedulingFormat.dayHeader(startLocal)} · '
                  '${SchedulingFormat.timeRange(lesson.startAtUtc, lesson.endAtUtc)}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: <Widget>[
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 8,
                  vertical: 3,
                ),
                decoration: BoxDecoration(
                  color: _statusColor(
                    lesson.status,
                  ).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  _statusLabel(lesson.status),
                  style: TextStyle(
                    color: _statusColor(lesson.status),
                    fontSize: 11,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
          // Ders tür/konum yalnızca öğretmen dersinde anlamlıdır — kendi (self) derste
          // backend LessonFormat taşımaz (bkz. sınıf dokümantasyonu üstte).
          if (!isOwn) ...<Widget>[
            const SizedBox(height: 12),
            const Divider(height: 1, color: AppColors.divider),
            const SizedBox(height: 12),
            Row(
              children: <Widget>[
                Icon(
                  lesson.lessonFormat == 'InPerson'
                      ? Icons.location_on_rounded
                      : Icons.videocam_rounded,
                  size: 15,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 6),
                Text(
                  _lessonFormatLabel(lesson.lessonFormat),
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
            if (hasLocation) ...<Widget>[
              const SizedBox(height: 6),
              Text(
                lesson.locationLabel!,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12.5,
                ),
              ),
            ],
            if (hasMeetingUrl) ...<Widget>[
              const SizedBox(height: 6),
              Text(
                lesson.meetingUrl!,
                style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 12.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }
}

/// Öğretmen bilgisi kartı — yalnız öğretmen dersinde gösterilir; profil alınamazsa
/// (ör. ağ hatası) kart tamamen gizlenir (bkz. `_load`).
class _TeacherCard extends StatelessWidget {
  const _TeacherCard({required this.teacher});

  final TeacherProfile? teacher;

  @override
  Widget build(BuildContext context) {
    final t = teacher;
    if (t == null) return const SizedBox.shrink();
    final List<String> parts = t.fullName
        .trim()
        .split(RegExp(r'\s+'))
        .where((String p) => p.isNotEmpty)
        .toList();
    final String initials = parts.isEmpty
        ? '?'
        : parts.length == 1
        ? parts.first.substring(0, 1).toUpperCase()
        : (parts.first.substring(0, 1) + parts.last.substring(0, 1))
              .toUpperCase();

    return StudyCard(
      child: Row(
        children: <Widget>[
          Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              shape: BoxShape.circle,
            ),
            child: Text(
              initials,
              style: const TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w800,
                fontSize: 16,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Flexible(
                      child: Text(
                        t.fullName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ),
                    if (t.isVerified) ...<Widget>[
                      const SizedBox(width: 4),
                      const Icon(
                        Icons.verified_rounded,
                        color: AppColors.accentBlue,
                        size: 15,
                      ),
                    ],
                  ],
                ),
                if (t.subject.trim().isNotEmpty) ...<Widget>[
                  const SizedBox(height: 2),
                  Text(
                    t.subject,
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12.5,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Hızlı erişim kartları — Not/Test/Deneme her zaman açık; Ödev/Konu etiketi ve hedefi
/// yetkiye göre değişir (brief Step 5, madde 3). Öğrenci ödev **oluşturamaz** (yalnız
/// öğretmen atar, bkz. `AssignmentRepository` — create metodu yok); bu yüzden "ekle" ve
/// "teslim et" aynı `/student/assignments` sayfasına gider (gerçek yetenek budur).
class _QuickAccessGrid extends StatelessWidget {
  const _QuickAccessGrid({
    required this.lesson,
    required this.perms,
    required this.studentId,
  });

  final LessonSchedule lesson;
  final LessonDetailPermissions perms;
  final String studentId;

  @override
  Widget build(BuildContext context) {
    if (studentId.isEmpty) return const SizedBox.shrink();
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.6,
      children: <Widget>[
        StudyQuickAccessCard(
          icon: Icons.sticky_note_2_rounded,
          color: AppColors.accentOrange,
          label: 'Not',
          onTap: () => context.push('/study/notes?studentId=$studentId'),
        ),
        StudyQuickAccessCard(
          icon: Icons.fact_check_rounded,
          color: AppColors.accentBlue,
          label: 'Test',
          onTap: () => context.push('/study/test?studentId=$studentId'),
        ),
        StudyQuickAccessCard(
          icon: Icons.emoji_events_rounded,
          color: AppColors.accentTeal,
          label: 'Deneme',
          onTap: () => context.push('/study/test?studentId=$studentId'),
        ),
        StudyQuickAccessCard(
          icon: Icons.assignment_turned_in_rounded,
          color: AppColors.primary,
          label: perms.canAddHomework ? 'Ödev ekle' : 'Ödev teslim et',
          onTap: () => context.push('/student/assignments'),
        ),
        StudyQuickAccessCard(
          icon: Icons.menu_book_rounded,
          color: AppColors.accentGreen,
          label: perms.canAddTopic ? 'Konu ekle' : 'Konu',
          onTap: () => context.push('/study/catalog?studentId=$studentId'),
        ),
      ],
    );
  }
}

/// Ödev listesi — `AssignmentItem`'da `lessonId` alanı yok (yalnız öğretmen atar, ders
/// bazlı bağ backend'de tutulmaz); bu yüzden derse özgü filtre uygulanamaz, öğrencinin
/// tüm ödevleri kısa önizleme olarak gösterilir.
class _AssignmentsSection extends StatelessWidget {
  const _AssignmentsSection({required this.items});

  final List<AssignmentItem> items;

  @override
  Widget build(BuildContext context) {
    final List<AssignmentItem> pending = items
        .where(
          (AssignmentItem a) =>
              a.status != 'Completed' && a.status != 'Cancelled',
        )
        .toList();
    final List<AssignmentItem> preview = pending.take(3).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        StudySectionHeader(
          title: 'Ödevler',
          action: StudySectionAction(
            label: 'Tümü',
            onTap: () => context.push('/student/assignments'),
          ),
        ),
        const SizedBox(height: 12),
        if (preview.isEmpty)
          StudyCard(
            child: Text(
              'Bekleyen ödevin yok.',
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ...preview.map(
            (AssignmentItem a) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _AssignmentTile(item: a),
            ),
          ),
      ],
    );
  }
}

class _AssignmentTile extends StatelessWidget {
  const _AssignmentTile({required this.item});

  final AssignmentItem item;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: () => context.push('/student/assignments'),
      child: StudyCard(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: <Widget>[
            const Icon(
              Icons.assignment_rounded,
              color: AppColors.primary,
              size: 20,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                item.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary,
            ),
          ],
        ),
      ),
    );
  }
}

/// Test & Deneme — TEK liste, tür rozetli (🔹 Test / 🔸 Deneme). `TestResult`'ta
/// `lessonId` yok; ders bağı `subject` metin eşleşmesiyle (best-effort) kurulur —
/// gerçek bir yabancı anahtar olmadığı için her satır [StudyDemoBadge] taşır.
class _TestsSection extends StatelessWidget {
  const _TestsSection({required this.tests, required this.lesson});

  final List<TestResult> tests;
  final LessonSchedule lesson;

  @override
  Widget build(BuildContext context) {
    final String subject = lesson.subject.trim().toLowerCase();
    final List<TestResult> matched =
        tests
            .where((TestResult t) => t.subject.trim().toLowerCase() == subject)
            .toList()
          ..sort(
            (TestResult a, TestResult b) =>
                b.takenOnUtc.compareTo(a.takenOnUtc),
          );
    final List<TestResult> preview = matched.take(5).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        StudySectionHeader(
          title: 'Test & Deneme',
          action: StudySectionAction(
            label: 'Tümü',
            onTap: () => context.push('/student/performance'),
          ),
        ),
        const SizedBox(height: 12),
        if (preview.isEmpty)
          StudyCard(
            child: Text(
              'Bu derse ait test/deneme kaydı yok.',
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ...preview.map(
            (TestResult t) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _TestTile(test: t),
            ),
          ),
      ],
    );
  }
}

class _TestTile extends StatelessWidget {
  const _TestTile({required this.test});

  final TestResult test;

  bool get _isDeneme => test.testType == 'Deneme';

  @override
  Widget build(BuildContext context) {
    return StudyCard(
      padding: const EdgeInsets.all(12),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 7,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color:
                            (_isDeneme
                                    ? AppColors.accentOrange
                                    : AppColors.accentBlue)
                                .withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        _isDeneme ? '🔸 Deneme' : '🔹 Test',
                        style: TextStyle(
                          color: _isDeneme
                              ? AppColors.accentOrange
                              : AppColors.accentBlue,
                          fontSize: 10,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    const SizedBox(width: 6),
                    const StudyDemoBadge(),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  test.testName ?? test.subject,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  StudyFormat.date(test.takenOnUtc),
                  style: const TextStyle(
                    color: AppColors.textMuted,
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(
            StudyFormat.net(test.net),
            style: const TextStyle(
              fontWeight: FontWeight.w800,
              color: AppColors.primary,
              fontSize: 16,
            ),
          ),
        ],
      ),
    );
  }
}

/// Konu listesi — dersle eşleşen `SubjectCatalog` (best-effort, isim eşleşmesiyle).
/// "Hâkimiyet" (mastery) backend'de hesaplanmıyor (`SubjectCatalog`/`TopicCatalog`'da
/// ilerleme alanı yok); bu yüzden [StudyDemoBadge] ile işaretlenir.
class _TopicsSection extends StatelessWidget {
  const _TopicsSection({
    required this.subjects,
    required this.lesson,
    required this.perms,
    required this.studentId,
  });

  final List<SubjectCatalog> subjects;
  final LessonSchedule lesson;
  final LessonDetailPermissions perms;
  final String studentId;

  @override
  Widget build(BuildContext context) {
    final String subjectName = lesson.subject.trim().toLowerCase();
    final SubjectCatalog? matched = subjects
        .cast<SubjectCatalog?>()
        .firstWhere(
          (SubjectCatalog? s) => s!.name.trim().toLowerCase() == subjectName,
          orElse: () => null,
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: StudySectionHeader(
                title: 'Konular',
                action: perms.canAddTopic
                    ? StudySectionAction(
                        label: 'Ekle',
                        onTap: () =>
                            context.push('/study/catalog?studentId=$studentId'),
                      )
                    : null,
              ),
            ),
            const SizedBox(width: 6),
            const StudyDemoBadge(),
          ],
        ),
        const SizedBox(height: 12),
        if (matched == null || matched.topics.isEmpty)
          StudyCard(
            child: Text(
              perms.canAddTopic
                  ? 'Bu ders için henüz konu eklemedin.'
                  : 'Bu derse ait konu kaydı yok.',
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          StudyCard(
            child: Wrap(
              spacing: 8,
              runSpacing: 8,
              children: matched.topics
                  .map(
                    (TopicCatalog t) => Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 6,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.primaryLight,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        t.name,
                        style: const TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w700,
                          fontSize: 12.5,
                        ),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ),
      ],
    );
  }
}

/// Not listesi — dersle eşleşen `StudyNote` (best-effort, `subject` metin eşleşmesiyle).
class _NotesSection extends StatelessWidget {
  const _NotesSection({
    required this.notes,
    required this.lesson,
    required this.studentId,
  });

  final List<StudyNote> notes;
  final LessonSchedule lesson;
  final String studentId;

  @override
  Widget build(BuildContext context) {
    final String subject = lesson.subject.trim().toLowerCase();
    final List<StudyNote> matched =
        notes
            .where(
              (StudyNote n) =>
                  (n.subject ?? '').trim().toLowerCase() == subject,
            )
            .toList()
          ..sort(
            (StudyNote a, StudyNote b) =>
                b.updatedOnUtc.compareTo(a.updatedOnUtc),
          );
    final List<StudyNote> preview = matched.take(3).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        StudySectionHeader(
          title: 'Notlarım',
          action: StudySectionAction(
            label: 'Tüm notlarım',
            onTap: () => context.push('/study/notes?studentId=$studentId'),
          ),
        ),
        const SizedBox(height: 12),
        if (preview.isEmpty)
          StudyCard(
            child: Text(
              'Bu derse ait not eklemedin.',
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ...preview.map(
            (StudyNote n) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: StudyCard(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      n.title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      n.body,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
