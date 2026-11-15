import 'package:egitim_ussu_mobile/core/money/money.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/open_conversation_action.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/lesson_form_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_state.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

class StudentDetailPage extends StatefulWidget {
  const StudentDetailPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<StudentDetailPage> createState() => _StudentDetailPageState();
}

class _StudentDetailPageState extends State<StudentDetailPage> {
  List<String> get _tabs => <String>[
    AppLocalizations.of(context).finance_genel,
    AppLocalizations.of(context).finance_dersler,
    AppLocalizations.of(context).finance_performans,
    AppLocalizations.of(context).finance_odemeler,
  ];

  int _selectedTab = 0;

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit c) => c.state.session);
    final teacherUserId = session?.userId ?? '';

    return BlocProvider(
      create: (_) =>
          StudentDetailCubit.create()
            ..load(studentId: widget.studentId, teacherUserId: teacherUserId),
      child: BlocConsumer<StudentDetailCubit, StudentDetailState>(
        listenWhen: (prev, next) =>
            prev.successMessage != next.successMessage ||
            (prev.errorMessage != next.errorMessage && !next.isLoading),
        listener: (context, state) {
          if (state.successMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.successMessage!),
                backgroundColor: AppColors.accentGreen,
              ),
            );
          } else if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.errorMessage!),
                backgroundColor: AppColors.accentRed,
              ),
            );
          }
        },
        builder: (context, state) {
          final student = state.student;

          if (state.isLoading && student == null) {
            return Scaffold(
              backgroundColor: AppColors.background,
              body: SafeArea(
                child: Column(
                  children: <Widget>[
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                      child: _TopBar(onBack: () => context.pop()),
                    ),
                    const Expanded(
                      child: Center(
                        child: CircularProgressIndicator(
                          color: AppColors.primary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            );
          }

          if (student == null) {
            return Scaffold(
              backgroundColor: AppColors.background,
              body: SafeArea(
                child: Column(
                  children: <Widget>[
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                      child: _TopBar(onBack: () => context.pop()),
                    ),
                    Expanded(
                      child: EmptyStateView(
                        title: AppLocalizations.of(
                          context,
                        ).finance_ogrenciBulunamadi,
                        subtitle:
                            state.errorMessage ??
                            AppLocalizations.of(
                              context,
                            ).finance_lutfenYenidenDeneyin,
                      ),
                    ),
                  ],
                ),
              ),
            );
          }

          return Scaffold(
            backgroundColor: AppColors.background,
            body: SafeArea(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
                children: <Widget>[
                  _TopBar(
                    onBack: () => context.pop(),
                    onMore: () => _showMoreMenu(context, student),
                  ),
                  const SizedBox(height: 12),
                  _ProfileHero(student: student),
                  const SizedBox(height: 14),
                  _StudentDetailTabs(
                    tabs: _tabs,
                    selectedIndex: _selectedTab,
                    onChanged: (index) => setState(() => _selectedTab = index),
                  ),
                  const SizedBox(height: 16),
                  AnimatedSwitcher(
                    duration: const Duration(milliseconds: 180),
                    switchInCurve: Curves.easeOut,
                    switchOutCurve: Curves.easeIn,
                    child: KeyedSubtree(
                      key: ValueKey<int>(_selectedTab),
                      child: _buildTabContent(
                        context,
                        student,
                        state.lessons,
                        state.payments,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  /// M03-2: Branş ekleme alt sayfası. Sunucu aynı branşı ikinci kez kabul etmez;
  /// hata mesajı ekranın ortak SnackBar dinleyicisinden gösterilir.
  void _showAddSubjectSheet(BuildContext context) {
    final cubit = context.read<StudentDetailCubit>();
    final subjectCtrl = TextEditingController();
    final targetCtrl = TextEditingController();

    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) => Padding(
        padding: EdgeInsets.fromLTRB(
          16,
          16,
          16,
          16 + MediaQuery.of(sheetContext).viewInsets.bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              AppLocalizations.of(context).finance_bransEkle,
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
            ),
            const SizedBox(height: 12),
            _FormField(
              controller: subjectCtrl,
              label: AppLocalizations.of(context).finance_ders,
            ),
            const SizedBox(height: 10),
            _FormField(
              controller: targetCtrl,
              label: AppLocalizations.of(context).finance_hedefSeviye,
            ),
            const SizedBox(height: 16),
            FilledButton(
              key: const Key('student-subject-save'),
              onPressed: () {
                final subject = subjectCtrl.text.trim();
                if (subject.isEmpty) return;
                Navigator.pop(sheetContext);
                cubit.addSubject(
                  subject: subject,
                  targetLevel: targetCtrl.text.trim().isEmpty
                      ? null
                      : targetCtrl.text.trim(),
                );
              },
              child: Text(AppLocalizations.of(context).finance_ekle),
            ),
          ],
        ),
      ),
    ).whenComplete(() {
      subjectCtrl.dispose();
      targetCtrl.dispose();
    });
  }

  /// M03-1: Veli bağlama alt sayfası.
  /// ⚠️ Veliyi **e-postayla arama** ucu yok (Identity'de kullanıcı arama yok);
  /// bu yüzden şimdilik veli kullanıcı kimliği girilir.
  void _showLinkParentSheet(BuildContext context) {
    final cubit = context.read<StudentDetailCubit>();
    final parentCtrl = TextEditingController();

    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) => Padding(
        padding: EdgeInsets.fromLTRB(
          16,
          16,
          16,
          16 + MediaQuery.of(sheetContext).viewInsets.bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              AppLocalizations.of(context).finance_veliBagla,
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
            ),
            const SizedBox(height: 8),
            Text(
              AppLocalizations.of(
                context,
              ).finance_veliKayitliBirKullaniciOlmalidirBaglantiOnayBekler,
              style: TextStyle(color: AppColors.textSecondary),
            ),
            const SizedBox(height: 12),
            _FormField(
              controller: parentCtrl,
              label: AppLocalizations.of(context).finance_veliKullaniciKimligi,
            ),
            const SizedBox(height: 16),
            FilledButton(
              key: const Key('student-link-parent-save'),
              onPressed: () {
                final parentUserId = parentCtrl.text.trim();
                if (parentUserId.isEmpty) return;
                Navigator.pop(sheetContext);
                cubit.linkParent(parentUserId);
              },
              child: Text(AppLocalizations.of(context).finance_link),
            ),
          ],
        ),
      ),
    ).whenComplete(parentCtrl.dispose);
  }

  void _showMoreMenu(BuildContext context, StudentProfile student) {
    final cubit = context.read<StudentDetailCubit>();
    showModalBottomSheet<void>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              ListTile(
                leading: const Icon(
                  Icons.edit_rounded,
                  color: AppColors.primary,
                ),
                title: Text(AppLocalizations.of(context).finance_duzenle),
                onTap: () {
                  Navigator.pop(context);
                  _showEditSheet(context, student, cubit);
                },
              ),
              ListTile(
                leading: Icon(
                  student.isActive
                      ? Icons.person_off_rounded
                      : Icons.person_rounded,
                  color: student.isActive
                      ? AppColors.accentRed
                      : AppColors.accentGreen,
                ),
                title: Text(
                  student.isActive
                      ? AppLocalizations.of(context).finance_pasiflestir
                      : AppLocalizations.of(context).finance_aktiflestir,
                ),
                onTap: () {
                  Navigator.pop(context);
                  _confirmToggleActive(context, student, cubit);
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showEditSheet(
    BuildContext context,
    StudentProfile student,
    StudentDetailCubit cubit,
  ) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => _EditStudentSheet(student: student, cubit: cubit),
    );
  }

  void _confirmToggleActive(
    BuildContext context,
    StudentProfile student,
    StudentDetailCubit cubit,
  ) {
    final willDeactivate = student.isActive;
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(
          willDeactivate
              ? AppLocalizations.of(context).finance_ogrenciyiPasiflestir
              : AppLocalizations.of(context).finance_ogrenciyiAktiflestir,
        ),
        content: Text(
          willDeactivate
              ? AppLocalizations.of(
                  context,
                ).finance_deactivateStudent(student.fullName)
              : AppLocalizations.of(
                  context,
                ).finance_activateStudent(student.fullName),
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: Text(AppLocalizations.of(context).finance_iptal),
          ),
          TextButton(
            onPressed: () {
              Navigator.pop(ctx);
              cubit.setIsActive(isActive: !student.isActive);
            },
            style: TextButton.styleFrom(
              foregroundColor: willDeactivate
                  ? AppColors.accentRed
                  : AppColors.accentGreen,
            ),
            child: Text(
              willDeactivate
                  ? AppLocalizations.of(context).finance_pasiflestir
                  : AppLocalizations.of(context).finance_aktiflestir,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTabContent(
    BuildContext context,
    StudentProfile student,
    List<LessonSchedule> lessons,
    List<PaymentRecord> payments,
  ) {
    return switch (_selectedTab) {
      0 => _OverviewTab(
        student: student,
        lessons: lessons,
        payments: payments,
        onPlanLesson: () => _openLessonForm(context, student, lessons),
        onAddPayment: () => context.push('/payments'),
        onAddSubject: () => _showAddSubjectSheet(context),
        onRemoveSubject: (subjectId) =>
            context.read<StudentDetailCubit>().removeSubject(subjectId),
        onLinkParent: () => _showLinkParentSheet(context),
      ),
      1 => _LessonsTab(
        lessons: lessons,
        onPlanLesson: () => _openLessonForm(context, student, lessons),
      ),
      2 => _PerformanceTab(student: student, lessons: lessons),
      3 => _PaymentsTab(
        payments: payments,
        onAddPayment: () => context.push('/payments'),
      ),
      _ => const SizedBox.shrink(),
    };
  }

  /// Ortak ders olusturma formunu (LessonFormSheet) bu ogrenci secili olarak
  /// modal acar. Tek elemanli [students] verildigi icin form ogrenciyi otomatik
  /// secer. Form kapaninca ogrenci detayi (ders listesi vb.) tazelenir.
  Future<void> _openLessonForm(
    BuildContext context,
    StudentProfile student,
    List<LessonSchedule> lessons,
  ) async {
    final teacherUserId = context.read<AuthCubit>().state.session?.userId ?? '';
    final detailCubit = context.read<StudentDetailCubit>();

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return BlocProvider<SchedulingCubit>(
          create: (_) => SchedulingCubit.create(),
          child: LessonFormSheet(
            teacherUserId: teacherUserId,
            students: <StudentProfile>[student],
            existingLessons: lessons,
          ),
        );
      },
    );

    if (context.mounted) {
      detailCubit.load(
        studentId: widget.studentId,
        teacherUserId: teacherUserId,
      );
    }
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack, this.onMore});

  final VoidCallback onBack;
  final VoidCallback? onMore;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _RoundIconButton(icon: Icons.arrow_back_rounded, onTap: onBack),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            AppLocalizations.of(context).finance_ogrenciDetayi,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        _RoundIconButton(icon: Icons.more_horiz_rounded, onTap: onMore),
      ],
    );
  }
}

class _RoundIconButton extends StatelessWidget {
  const _RoundIconButton({required this.icon, this.onTap});

  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
          boxShadow: AppShadows.soft,
        ),
        child: Icon(icon, color: AppColors.textPrimary),
      ),
    );
  }
}

class _ProfileHero extends StatelessWidget {
  const _ProfileHero({required this.student});

  final StudentProfile student;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        children: <Widget>[
          _LargeAvatar(name: student.fullName),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  student.fullName,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 7),
                _Pill(
                  icon: Icons.school_rounded,
                  label: student.gradeLevel,
                  color: AppColors.primary,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LargeAvatar extends StatelessWidget {
  const _LargeAvatar({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 66,
      height: 66,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: <Color>[AppColors.primary, AppColors.accentBlue],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
        boxShadow: AppShadows.soft,
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.titleLarge?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }

  String _initials(String value) {
    final parts = value.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty || parts.first.isEmpty) {
      return '?';
    }
    if (parts.length == 1) {
      return parts.first.substring(0, 1).toUpperCase();
    }
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }
}

class _StudentDetailTabs extends StatelessWidget {
  const _StudentDetailTabs({
    required this.tabs,
    required this.selectedIndex,
    required this.onChanged,
  });

  final List<String> tabs;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 42,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.tabBackground,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: List<Widget>.generate(tabs.length, (index) {
          final selected = selectedIndex == index;
          return Expanded(
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: () => onChanged(index),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary : Colors.transparent,
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Text(
                  tabs[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
                    color: selected ? Colors.white : AppColors.textSecondary,
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

class _OverviewTab extends StatelessWidget {
  const _OverviewTab({
    required this.student,
    required this.lessons,
    required this.payments,
    required this.onPlanLesson,
    required this.onAddPayment,
    required this.onAddSubject,
    required this.onRemoveSubject,
    required this.onLinkParent,
  });

  final StudentProfile student;
  final List<LessonSchedule> lessons;
  final List<PaymentRecord> payments;
  final VoidCallback onPlanLesson;
  final VoidCallback onAddPayment;
  final VoidCallback onAddSubject;
  final ValueChanged<String> onRemoveSubject;
  final VoidCallback onLinkParent;

  @override
  Widget build(BuildContext context) {
    final outstanding = _paymentTotals(payments, collected: false);

    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_ders,
                value: lessons.length.toString(),
                caption: AppLocalizations.of(context).finance_toplamKayit,
                color: AppColors.accentBlue,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_odeme,
                value: outstanding,
                caption: AppLocalizations.of(context).finance_bekleyen,
                color: payments.any((p) => p.outstandingAmount > 0)
                    ? AppColors.amber
                    : AppColors.accentGreen,
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_hedef,
                value: student.subjects.length.toString(),
                caption: AppLocalizations.of(context).finance_dersHedefi,
                color: AppColors.accentTeal,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_durum,
                value: payments.any((p) => p.outstandingAmount > 0)
                    ? AppLocalizations.of(context).finance_takip
                    : AppLocalizations.of(context).finance_temiz,
                caption: payments.any((p) => p.outstandingAmount > 0)
                    ? AppLocalizations.of(context).finance_odemeVar
                    : AppLocalizations.of(context).finance_borcsuz,
                color: payments.any((p) => p.outstandingAmount > 0)
                    ? AppColors.accentRed
                    : AppColors.accentGreen,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _InfoCard(
          title: AppLocalizations.of(context).finance_ogrenciNotlari,
          children: <Widget>[
            _InfoLine(
              icon: Icons.track_changes_rounded,
              label: AppLocalizations.of(context).finance_hedef,
              value: _emptyFallback(context, student.goalSummary),
            ),
            _InfoLine(
              icon: Icons.timeline_rounded,
              label: AppLocalizations.of(context).finance_seviyeNotu,
              value: _emptyFallback(context, student.levelNotes),
            ),
            _InfoLine(
              icon: Icons.mail_outline_rounded,
              label: AppLocalizations.of(context).finance_email,
              value: _emptyFallback(context, student.contactEmail),
            ),
            _InfoLine(
              icon: Icons.phone_outlined,
              label: AppLocalizations.of(context).finance_phone,
              value: _emptyFallback(context, student.contactPhone),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _SubjectTargetsCard(
          student: student,
          onAdd: onAddSubject,
          onRemove: onRemoveSubject,
        ),
        const SizedBox(height: 16),
        _ParentLinkCard(onLinkParent: onLinkParent),
        const SizedBox(height: 16),
        Row(
          children: <Widget>[
            Expanded(
              child: _ActionButton(
                label: AppLocalizations.of(context).finance_yeniDers,
                icon: Icons.calendar_month_rounded,
                onTap: onPlanLesson,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _ActionButton(
                label: AppLocalizations.of(context).finance_odemeEkle,
                icon: Icons.payments_rounded,
                onTap: onAddPayment,
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        // D-08 (M16): Ogrenciyle sohbet. Ogrencinin KULLANICI kimligi bu ekranda yok
        // (profil kimligi var); karsi tarafi sunucu studentId'den cozer.
        _ActionButton(
          label: AppLocalizations.of(context).finance_mesajGonder,
          icon: Icons.chat_bubble_outline_rounded,
          onTap: () => openConversationAndGo(
            context,
            counterpartRole: kParticipantRoleStudent,
            studentId: student.id,
          ),
        ),
      ],
    );
  }

  String _emptyFallback(BuildContext context, String? value) {
    final text = value?.trim();
    if (text == null || text.isEmpty) {
      return AppLocalizations.of(context).finance_notAdded;
    }
    return text;
  }
}

class _LessonsTab extends StatelessWidget {
  const _LessonsTab({required this.lessons, required this.onPlanLesson});

  final List<LessonSchedule> lessons;
  final VoidCallback onPlanLesson;

  @override
  Widget build(BuildContext context) {
    final sortedLessons = [...lessons]
      ..sort((a, b) => b.startAtUtc.compareTo(a.startAtUtc));

    return Column(
      children: <Widget>[
        _SectionHeader(
          title: AppLocalizations.of(context).finance_dersler,
          actionText: AppLocalizations.of(context).finance_yeniDers102,
          onActionTap: onPlanLesson,
        ),
        const SizedBox(height: 12),
        if (sortedLessons.isEmpty)
          EmptyStateView(
            title: AppLocalizations.of(context).finance_dersKaydiYok,
            subtitle: AppLocalizations.of(
              context,
            ).finance_buOgrenciIcinPlanliDersBulunamadi,
          )
        else
          ...List<Widget>.generate(sortedLessons.length, (index) {
            final lesson = sortedLessons[index];
            return Padding(
              padding: EdgeInsets.only(
                bottom: index == sortedLessons.length - 1 ? 0 : 12,
              ),
              child: _LessonCard(lesson: lesson),
            );
          }),
      ],
    );
  }
}

class _PerformanceTab extends StatelessWidget {
  const _PerformanceTab({required this.student, required this.lessons});

  final StudentProfile student;
  final List<LessonSchedule> lessons;

  @override
  Widget build(BuildContext context) {
    final completed = lessons
        .where((lesson) => lesson.status.toLowerCase() == 'completed')
        .length;
    final total = lessons.isEmpty ? 1 : lessons.length;
    final completionRatio = completed / total;
    final hasRealCompletion = lessons.isNotEmpty && completed > 0;

    return Column(
      children: <Widget>[
        _InfoCard(
          title: AppLocalizations.of(context).finance_performansOzeti,
          children: <Widget>[
            _ProgressLine(
              label: AppLocalizations.of(context).finance_dersTamamlama,
              value: hasRealCompletion ? completionRatio : 0.72,
              valueText: hasRealCompletion
                  ? '%${(completionRatio * 100).round()}'
                  : AppLocalizations.of(context).finance_takipBasladi,
              color: AppColors.accentGreen,
            ),
            const SizedBox(height: 14),
            _ProgressLine(
              label: AppLocalizations.of(context).finance_hedefNetligi,
              value: student.goalSummary?.trim().isNotEmpty == true
                  ? 0.9
                  : 0.35,
              valueText: student.goalSummary?.trim().isNotEmpty == true
                  ? AppLocalizations.of(context).finance_clearGoal
                  : AppLocalizations.of(context).finance_missingGoal,
              color: AppColors.accentBlue,
            ),
            const SizedBox(height: 14),
            _ProgressLine(
              label: AppLocalizations.of(context).finance_dersCesitliligi,
              value: student.subjects.isEmpty
                  ? 0.2
                  : (student.subjects.length / 4).clamp(0.25, 1).toDouble(),
              valueText: AppLocalizations.of(
                context,
              ).finance_goalCount(student.subjects.length),
              color: AppColors.accentTeal,
            ),
          ],
        ),
        const SizedBox(height: 16),
        _InfoCard(
          title: AppLocalizations.of(context).finance_dersHedefleri,
          children: student.subjects.isEmpty
              ? <Widget>[
                  Text(
                    AppLocalizations.of(
                      context,
                    ).finance_hedefDersEklenincePerformansTakibiBuradaZenginlesir,
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                ]
              : student.subjects
                    .map(
                      (target) => _TargetPerformanceRow(
                        subject: target.subject,
                        level: target.targetLevel,
                      ),
                    )
                    .toList(),
        ),
      ],
    );
  }
}

class _PaymentsTab extends StatelessWidget {
  const _PaymentsTab({required this.payments, required this.onAddPayment});

  final List<PaymentRecord> payments;
  final VoidCallback onAddPayment;

  @override
  Widget build(BuildContext context) {
    final collectedAmount = _paymentTotals(payments, collected: true);
    final outstandingAmount = _paymentTotals(payments, collected: false);

    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_tahsil,
                value: collectedAmount,
                caption: AppLocalizations.of(context).finance_toplam,
                color: AppColors.accentGreen,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: AppLocalizations.of(context).finance_bekleyen,
                value: outstandingAmount,
                caption: AppLocalizations.of(context).finance_toplam,
                color: payments.any((p) => p.outstandingAmount > 0)
                    ? AppColors.amber
                    : AppColors.accentGreen,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _SectionHeader(
          title: AppLocalizations.of(context).finance_odemeGecmisi,
          actionText: AppLocalizations.of(context).finance_odemeEkle114,
          onActionTap: onAddPayment,
        ),
        const SizedBox(height: 12),
        if (payments.isEmpty)
          EmptyStateView(
            title: AppLocalizations.of(context).finance_odemeKaydiYok,
            subtitle: AppLocalizations.of(
              context,
            ).finance_buOgrenciIcinTahsilatKaydiBulunamadi,
          )
        else
          ...List<Widget>.generate(payments.length, (index) {
            final payment = payments[index];
            return Padding(
              padding: EdgeInsets.only(
                bottom: index == payments.length - 1 ? 0 : 12,
              ),
              child: _PaymentCard(payment: payment),
            );
          }),
      ],
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.label,
    required this.value,
    required this.caption,
    required this.color,
  });

  final String label;
  final String value;
  final String caption;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            caption,
            style: Theme.of(
              context,
            ).textTheme.labelMedium?.copyWith(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 12),
          ...children,
        ],
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  const _InfoLine({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: AppColors.primary, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  label,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  value,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// M03-2: Branş ekle/sil. Silme sunucudaki **branş kimliğiyle** yapılır; kimliği
/// olmayan (eski gövdeden gelen) satırlarda silme düğmesi gösterilmez.
class _SubjectTargetsCard extends StatelessWidget {
  const _SubjectTargetsCard({
    required this.student,
    required this.onAdd,
    required this.onRemove,
  });

  final StudentProfile student;
  final VoidCallback onAdd;
  final ValueChanged<String> onRemove;

  @override
  Widget build(BuildContext context) {
    return _InfoCard(
      title: AppLocalizations.of(context).finance_hedefDersler,
      children: <Widget>[
        if (student.subjects.isEmpty)
          Text(
            AppLocalizations.of(
              context,
            ).finance_ogrenciHedefleriEklendigindeBuradaGorunur,
            style: TextStyle(color: AppColors.textSecondary),
          )
        else
          ...student.subjects.map(
            (target) => Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: _InfoLine(
                      icon: Icons.menu_book_rounded,
                      label: target.subject,
                      value: target.targetLevel.isEmpty
                          ? AppLocalizations.of(context).finance_hedefYok
                          : target.targetLevel,
                    ),
                  ),
                  if (target.id.isNotEmpty)
                    IconButton(
                      key: Key('student-subject-remove-\${target.id}'),
                      tooltip: AppLocalizations.of(context).finance_bransiSil,
                      onPressed: () => onRemove(target.id),
                      icon: const Icon(Icons.close_rounded),
                      color: AppColors.accentRed,
                    ),
                ],
              ),
            ),
          ),
        const SizedBox(height: 6),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            key: const Key('student-subject-add'),
            onPressed: onAdd,
            icon: const Icon(Icons.add_rounded),
            label: Text(AppLocalizations.of(context).finance_bransEkle),
          ),
        ),
      ],
    );
  }
}

/// M03-1: Kayıtlı bir veli kullanıcısını öğrenciye bağlama kartı.
/// Bağ **onay bekler**; onaylandığında profilin velisi dolar.
class _ParentLinkCard extends StatelessWidget {
  const _ParentLinkCard({required this.onLinkParent});

  final VoidCallback onLinkParent;

  @override
  Widget build(BuildContext context) {
    return _InfoCard(
      title: AppLocalizations.of(context).finance_parent,
      children: <Widget>[
        Text(
          AppLocalizations.of(
                context,
              ).finance_kayitliBirVeliKullanicisiniBuOgrenciyeBaglayabilirsi +
              AppLocalizations.of(
                context,
              ).finance_baglantiOnayBeklerOnaylandiktanSonraVeliOgrencininG,
          style: TextStyle(color: AppColors.textSecondary),
        ),
        const SizedBox(height: 6),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            key: const Key('student-link-parent'),
            onPressed: onLinkParent,
            icon: const Icon(Icons.family_restroom_rounded),
            label: Text(AppLocalizations.of(context).finance_veliBagla),
          ),
        ),
      ],
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.label,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      width: double.infinity,
      child: FilledButton(
        onPressed: onTap,
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.primary,
          foregroundColor: Colors.white,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 18),
            const SizedBox(width: 6),
            Flexible(
              child: Text(label, maxLines: 1, overflow: TextOverflow.ellipsis),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.title,
    this.actionText,
    this.onActionTap,
  });

  final String title;
  final String? actionText;
  final VoidCallback? onActionTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        if (actionText != null) ...<Widget>[
          const SizedBox(width: 10),
          ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 112, maxWidth: 136),
            child: SizedBox(
              height: 38,
              child: FilledButton(
                onPressed: onActionTap,
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(horizontal: 10),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    const Icon(Icons.add_rounded, size: 18),
                    const SizedBox(width: 4),
                    Flexible(
                      child: Text(
                        actionText!,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _LessonCard extends StatelessWidget {
  const _LessonCard({required this.lesson});

  final LessonSchedule lesson;

  @override
  Widget build(BuildContext context) {
    final start = lesson.startAtUtc.toLocal();
    final end = lesson.endAtUtc.toLocal();
    final dateLabel = DateFormat('dd.MM.yyyy').format(start);
    final timeLabel =
        '${DateFormat('HH:mm').format(start)} - ${DateFormat('HH:mm').format(end)}';

    return _ListCard(
      leadingColor: _statusColor(lesson.status),
      icon: Icons.menu_book_rounded,
      title: lesson.subject,
      subtitle: '$dateLabel, $timeLabel',
      trailing: _StatusPill(
        label: lesson.status,
        color: _statusColor(lesson.status),
      ),
      footer: lesson.locationLabel ?? lesson.lessonFormat,
    );
  }

  Color _statusColor(String status) {
    final normalized = status.toLowerCase();
    if (normalized == 'completed') {
      return AppColors.accentGreen;
    }
    if (normalized == 'cancelled' || normalized == 'canceled') {
      return AppColors.accentRed;
    }
    return AppColors.accentBlue;
  }
}

class _PaymentCard extends StatelessWidget {
  const _PaymentCard({required this.payment});

  final PaymentRecord payment;

  @override
  Widget build(BuildContext context) {
    final dueDate = payment.dueDateUtc == null
        ? AppLocalizations.of(context).finance_tarihYok
        : DateFormat('dd.MM.yyyy').format(payment.dueDateUtc!.toLocal());
    final color = payment.outstandingAmount > 0
        ? AppColors.amber
        : AppColors.accentGreen;

    return _ListCard(
      leadingColor: color,
      icon: Icons.payments_rounded,
      title: payment.description,
      subtitle: AppLocalizations.of(context).finance_dueDate(dueDate),
      trailing: _StatusPill(label: payment.status, color: color),
      footer: AppLocalizations.of(context).finance_amountPending(
        Money(payment.outstandingAmount, payment.currency).formatTr(),
      ),
    );
  }
}

class _ListCard extends StatelessWidget {
  const _ListCard({
    required this.leadingColor,
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.trailing,
    required this.footer,
  });

  final Color leadingColor;
  final IconData icon;
  final String title;
  final String subtitle;
  final Widget trailing;
  final String footer;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: leadingColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Icon(icon, color: leadingColor),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  footer,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          trailing,
        ],
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: color,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _ProgressLine extends StatelessWidget {
  const _ProgressLine({
    required this.label,
    required this.value,
    required this.valueText,
    required this.color,
  });

  final String label;
  final double value;
  final String valueText;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                label,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            Text(
              valueText,
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: color,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: LinearProgressIndicator(
            minHeight: 8,
            value: value.clamp(0, 1).toDouble(),
            color: color,
            backgroundColor: AppColors.divider,
          ),
        ),
      ],
    );
  }
}

class _TargetPerformanceRow extends StatelessWidget {
  const _TargetPerformanceRow({required this.subject, required this.level});

  final String subject;
  final String level;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: AppColors.accentTeal.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.trending_up_rounded,
              color: AppColors.accentTeal,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  subject,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  level,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          _StatusPill(
            label: AppLocalizations.of(context).finance_aktif,
            color: AppColors.accentGreen,
          ),
        ],
      ),
    );
  }
}

class _EditStudentSheet extends StatefulWidget {
  const _EditStudentSheet({required this.student, required this.cubit});

  final StudentProfile student;
  final StudentDetailCubit cubit;

  @override
  State<_EditStudentSheet> createState() => _EditStudentSheetState();
}

class _EditStudentSheetState extends State<_EditStudentSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _fullNameCtrl;
  late final TextEditingController _gradeLevelCtrl;
  late final TextEditingController _contactEmailCtrl;
  late final TextEditingController _contactPhoneCtrl;
  late final TextEditingController _goalSummaryCtrl;
  late final TextEditingController _levelNotesCtrl;
  late final TextEditingController _subjectCtrl;
  late final TextEditingController _targetLevelCtrl;
  late bool _isActive;

  @override
  void initState() {
    super.initState();
    final s = widget.student;
    _fullNameCtrl = TextEditingController(text: s.fullName);
    _gradeLevelCtrl = TextEditingController(text: s.gradeLevel);
    _contactEmailCtrl = TextEditingController(text: s.contactEmail ?? '');
    _contactPhoneCtrl = TextEditingController(text: s.contactPhone ?? '');
    _goalSummaryCtrl = TextEditingController(text: s.goalSummary ?? '');
    _levelNotesCtrl = TextEditingController(text: s.levelNotes ?? '');
    _subjectCtrl = TextEditingController(
      text: s.subjects.isNotEmpty ? s.subjects.first.subject : '',
    );
    _targetLevelCtrl = TextEditingController(
      text: s.subjects.isNotEmpty ? s.subjects.first.targetLevel : '',
    );
    _isActive = s.isActive;
  }

  @override
  void dispose() {
    _fullNameCtrl.dispose();
    _gradeLevelCtrl.dispose();
    _contactEmailCtrl.dispose();
    _contactPhoneCtrl.dispose();
    _goalSummaryCtrl.dispose();
    _levelNotesCtrl.dispose();
    _subjectCtrl.dispose();
    _targetLevelCtrl.dispose();
    super.dispose();
  }

  void _save() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final subjects = <StudentSubjectTarget>[];
    final subjectText = _subjectCtrl.text.trim();
    if (subjectText.isNotEmpty) {
      subjects.add(
        StudentSubjectTarget(
          subject: subjectText,
          targetLevel: _targetLevelCtrl.text.trim(),
        ),
      );
    }
    final updated = StudentProfile(
      id: widget.student.id,
      fullName: _fullNameCtrl.text.trim(),
      gradeLevel: _gradeLevelCtrl.text.trim(),
      origin: widget.student.origin,
      teacherUserId: widget.student.teacherUserId,
      contactEmail: _emptyToNull(_contactEmailCtrl.text),
      contactPhone: _emptyToNull(_contactPhoneCtrl.text),
      goalSummary: _emptyToNull(_goalSummaryCtrl.text),
      levelNotes: _emptyToNull(_levelNotesCtrl.text),
      isActive: _isActive,
      subjects: subjects,
    );
    widget.cubit.updateStudent(updated);
    Navigator.pop(context);
  }

  String? _emptyToNull(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      AppLocalizations.of(context).finance_ogrenciGuncelle,
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(context),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              _FormField(
                controller: _fullNameCtrl,
                label: AppLocalizations.of(context).finance_adSoyad,
                validator: (v) => (v == null || v.trim().length < 2)
                    ? AppLocalizations.of(context).finance_adSoyadGiriniz
                    : null,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _gradeLevelCtrl,
                label: AppLocalizations.of(context).finance_sinifSeviye,
                validator: (v) => (v == null || v.trim().length < 2)
                    ? AppLocalizations.of(context).finance_sinifGiriniz
                    : null,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _subjectCtrl,
                label: AppLocalizations.of(context).finance_ders,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _targetLevelCtrl,
                label: AppLocalizations.of(context).finance_hedefSeviye129,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _contactEmailCtrl,
                label: AppLocalizations.of(context).finance_email,
                keyboardType: TextInputType.emailAddress,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _contactPhoneCtrl,
                label: AppLocalizations.of(context).finance_phone,
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _goalSummaryCtrl,
                label: AppLocalizations.of(context).finance_hedefAmac,
                maxLines: 2,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _levelNotesCtrl,
                label: AppLocalizations.of(context).finance_seviyeNotu131,
                maxLines: 2,
              ),
              const SizedBox(height: 16),
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      AppLocalizations.of(context).finance_aktifOgrenci,
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  Switch(
                    value: _isActive,
                    activeThumbColor: AppColors.accentGreen,
                    onChanged: (value) => setState(() => _isActive = value),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _save,
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: Text(
                    AppLocalizations.of(context).finance_kaydet,
                    style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FormField extends StatelessWidget {
  const _FormField({
    required this.controller,
    required this.label,
    this.validator,
    this.keyboardType,
    this.maxLines = 1,
  });

  final TextEditingController controller;
  final String label;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      validator: validator,
      keyboardType: keyboardType,
      maxLines: maxLines,
      decoration: InputDecoration(
        labelText: label,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 14,
          vertical: 12,
        ),
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.icon, required this.label, required this.color});

  final IconData icon;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.11),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, color: color, size: 16),
          const SizedBox(width: 6),
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: color,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

String _paymentTotals(List<PaymentRecord> payments, {required bool collected}) {
  final totals = <String, int>{};
  for (final p in payments) {
    totals.update(
      p.currency,
      (v) => v + (collected ? p.collectedAmount : p.outstandingAmount),
      ifAbsent: () => collected ? p.collectedAmount : p.outstandingAmount,
    );
  }
  return totals.entries
      .map((e) => Money(e.value, e.key).formatTr())
      .join(' · ');
}
