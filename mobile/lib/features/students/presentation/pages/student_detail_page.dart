import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_state.dart';
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
  static const _navy = Color(0xFF082B4F);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _red = Color(0xFFFF6B6B);
  static const _teal = Color(0xFF20A4A9);
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF6B7A90);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EAF0);
  static const _divider = Color(0xFFE5EEF7);
  static const _tabBackground = Color(0xFFF3F5F8);

  static const _tabs = <String>['Genel', 'Dersler', 'Performans', 'Odemeler'];

  int _selectedTab = 0;

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit c) => c.state.session);
    final teacherUserId = session?.userId ?? '';

    return BlocProvider(
      create: (_) => StudentDetailCubit.create()
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
                backgroundColor: _emerald,
              ),
            );
          } else if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.errorMessage!),
                backgroundColor: _red,
              ),
            );
          }
        },
        builder: (context, state) {
          final student = state.student;

          if (state.isLoading && student == null) {
            return Scaffold(
              backgroundColor: _background,
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
                          color: _StudentDetailPageState._navy,
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
              backgroundColor: _background,
              body: SafeArea(
                child: Column(
                  children: <Widget>[
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                      child: _TopBar(onBack: () => context.pop()),
                    ),
                    Expanded(
                      child: EmptyStateView(
                        title: 'Ogrenci bulunamadi',
                        subtitle:
                            state.errorMessage ?? 'Lutfen yeniden deneyin.',
                      ),
                    ),
                  ],
                ),
              ),
            );
          }

          return Scaffold(
            backgroundColor: _background,
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
                    onChanged: (index) =>
                        setState(() => _selectedTab = index),
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
                        state.outstandingAmount,
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
                leading: const Icon(Icons.edit_rounded, color: _navy),
                title: const Text('Duzenle'),
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
                  color: student.isActive ? _red : _emerald,
                ),
                title: Text(student.isActive ? 'Pasifles' : 'Aktifles'),
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
          willDeactivate ? 'Ogrenciyi Pasifles' : 'Ogrenciyi Aktifles',
        ),
        content: Text(
          willDeactivate
              ? '${student.fullName} pasif olarak isaretlenecek.'
              : '${student.fullName} tekrar aktif hale getirilecek.',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Iptal'),
          ),
          TextButton(
            onPressed: () {
              Navigator.pop(ctx);
              cubit.setIsActive(isActive: !student.isActive);
            },
            style: TextButton.styleFrom(
              foregroundColor: willDeactivate ? _red : _emerald,
            ),
            child: Text(willDeactivate ? 'Pasifles' : 'Aktifles'),
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
    double outstandingAmount,
  ) {
    return switch (_selectedTab) {
      0 => _OverviewTab(
        student: student,
        lessons: lessons,
        payments: payments,
        onPlanLesson: () => context.push('/scheduling'),
        onAddPayment: () => context.push('/payments'),
      ),
      1 => _LessonsTab(
        lessons: lessons,
        onPlanLesson: () => context.push('/scheduling'),
      ),
      2 => _PerformanceTab(student: student, lessons: lessons),
      3 => _PaymentsTab(
        payments: payments,
        outstandingAmount: outstandingAmount,
        onAddPayment: () => context.push('/payments'),
      ),
      _ => const SizedBox.shrink(),
    };
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
            'Ogrenci Detayi',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: _StudentDetailPageState._text,
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
          border: Border.all(color: _StudentDetailPageState._border),
          boxShadow: const <BoxShadow>[
            BoxShadow(
              color: Color(0x0D082B4F),
              blurRadius: 18,
              offset: Offset(0, 10),
            ),
          ],
        ),
        child: Icon(icon, color: _StudentDetailPageState._text),
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
        border: Border.all(color: _StudentDetailPageState._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x12082B4F),
            blurRadius: 24,
            offset: Offset(0, 12),
          ),
        ],
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
                    color: _StudentDetailPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 7),
                _Pill(
                  icon: Icons.school_rounded,
                  label: student.gradeLevel,
                  color: _StudentDetailPageState._navy,
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
          colors: <Color>[
            _StudentDetailPageState._navy,
            _StudentDetailPageState._blue,
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x24082B4F),
            blurRadius: 18,
            offset: Offset(0, 10),
          ),
        ],
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
        color: _StudentDetailPageState._tabBackground,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: _StudentDetailPageState._border),
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
                  color: selected
                      ? _StudentDetailPageState._navy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Text(
                  tabs[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
                    color: selected
                        ? Colors.white
                        : _StudentDetailPageState._slate,
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
  });

  final StudentProfile student;
  final List<LessonSchedule> lessons;
  final List<PaymentRecord> payments;
  final VoidCallback onPlanLesson;
  final VoidCallback onAddPayment;

  @override
  Widget build(BuildContext context) {
    final outstanding = payments.fold<double>(
      0,
      (total, payment) => total + payment.outstandingAmount,
    );

    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: 'Ders',
                value: lessons.length.toString(),
                caption: 'Toplam kayit',
                color: _StudentDetailPageState._blue,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: 'Odeme',
                value: outstanding.toStringAsFixed(0),
                caption: 'Bekleyen TRY',
                color: outstanding > 0
                    ? _StudentDetailPageState._amber
                    : _StudentDetailPageState._emerald,
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: 'Hedef',
                value: student.subjects.length.toString(),
                caption: 'Ders hedefi',
                color: _StudentDetailPageState._teal,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: 'Durum',
                value: outstanding > 0 ? 'Takip' : 'Temiz',
                caption: outstanding > 0 ? 'Odeme var' : 'Borcsuz',
                color: outstanding > 0
                    ? _StudentDetailPageState._red
                    : _StudentDetailPageState._emerald,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _InfoCard(
          title: 'Ogrenci notlari',
          children: <Widget>[
            _InfoLine(
              icon: Icons.track_changes_rounded,
              label: 'Hedef',
              value: _emptyFallback(student.goalSummary),
            ),
            _InfoLine(
              icon: Icons.timeline_rounded,
              label: 'Seviye notu',
              value: _emptyFallback(student.levelNotes),
            ),
            _InfoLine(
              icon: Icons.mail_outline_rounded,
              label: 'E-posta',
              value: _emptyFallback(student.contactEmail),
            ),
            _InfoLine(
              icon: Icons.phone_outlined,
              label: 'Telefon',
              value: _emptyFallback(student.contactPhone),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _SubjectTargetsCard(student: student),
        const SizedBox(height: 16),
        Row(
          children: <Widget>[
            Expanded(
              child: _ActionButton(
                label: 'Yeni Ders',
                icon: Icons.calendar_month_rounded,
                onTap: onPlanLesson,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _ActionButton(
                label: 'Odeme Ekle',
                icon: Icons.payments_rounded,
                onTap: onAddPayment,
              ),
            ),
          ],
        ),
      ],
    );
  }

  static String _emptyFallback(String? value) {
    final text = value?.trim();
    if (text == null || text.isEmpty) {
      return 'Eklenmemis';
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
          title: 'Dersler',
          actionText: 'Yeni ders',
          onActionTap: onPlanLesson,
        ),
        const SizedBox(height: 12),
        if (sortedLessons.isEmpty)
          const EmptyStateView(
            title: 'Ders kaydi yok',
            subtitle: 'Bu ogrenci icin planli ders bulunamadi.',
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
          title: 'Performans ozeti',
          children: <Widget>[
            _ProgressLine(
              label: 'Ders tamamlama',
              value: hasRealCompletion ? completionRatio : 0.72,
              valueText: hasRealCompletion
                  ? '%${(completionRatio * 100).round()}'
                  : 'Takip basladi',
              color: _StudentDetailPageState._emerald,
            ),
            const SizedBox(height: 14),
            _ProgressLine(
              label: 'Hedef netligi',
              value: student.goalSummary?.trim().isNotEmpty == true
                  ? 0.9
                  : 0.35,
              valueText: student.goalSummary?.trim().isNotEmpty == true
                  ? 'Net'
                  : 'Eksik',
              color: _StudentDetailPageState._blue,
            ),
            const SizedBox(height: 14),
            _ProgressLine(
              label: 'Ders cesitliligi',
              value: student.subjects.isEmpty
                  ? 0.2
                  : (student.subjects.length / 4).clamp(0.25, 1).toDouble(),
              valueText: '${student.subjects.length} hedef',
              color: _StudentDetailPageState._teal,
            ),
          ],
        ),
        const SizedBox(height: 16),
        _InfoCard(
          title: 'Ders hedefleri',
          children: student.subjects.isEmpty
              ? <Widget>[
                  const Text(
                    'Hedef ders eklenince performans takibi burada zenginlesir.',
                    style: TextStyle(color: _StudentDetailPageState._slate),
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
  const _PaymentsTab({
    required this.payments,
    required this.outstandingAmount,
    required this.onAddPayment,
  });

  final List<PaymentRecord> payments;
  final double outstandingAmount;
  final VoidCallback onAddPayment;

  @override
  Widget build(BuildContext context) {
    final collectedAmount = payments.fold<double>(
      0,
      (total, payment) => total + payment.collectedAmount,
    );

    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: _MetricCard(
                label: 'Tahsil',
                value: collectedAmount.toStringAsFixed(0),
                caption: 'Toplam TRY',
                color: _StudentDetailPageState._emerald,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _MetricCard(
                label: 'Bekleyen',
                value: outstandingAmount.toStringAsFixed(0),
                caption: 'Toplam TRY',
                color: outstandingAmount > 0
                    ? _StudentDetailPageState._amber
                    : _StudentDetailPageState._emerald,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _SectionHeader(
          title: 'Odeme gecmisi',
          actionText: 'Odeme ekle',
          onActionTap: onAddPayment,
        ),
        const SizedBox(height: 12),
        if (payments.isEmpty)
          const EmptyStateView(
            title: 'Odeme kaydi yok',
            subtitle: 'Bu ogrenci icin tahsilat kaydi bulunamadi.',
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
        border: Border.all(color: _StudentDetailPageState._border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: _StudentDetailPageState._slate,
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
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: _StudentDetailPageState._slate,
            ),
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
        border: Border.all(color: _StudentDetailPageState._border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _StudentDetailPageState._text,
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
          Icon(icon, color: _StudentDetailPageState._navy, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  label,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: _StudentDetailPageState._slate,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  value,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: _StudentDetailPageState._text,
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

class _SubjectTargetsCard extends StatelessWidget {
  const _SubjectTargetsCard({required this.student});

  final StudentProfile student;

  @override
  Widget build(BuildContext context) {
    return _InfoCard(
      title: 'Hedef dersler',
      children: student.subjects.isEmpty
          ? <Widget>[
              const Text(
                'Ogrenci hedefleri eklendiginde burada gorunur.',
                style: TextStyle(color: _StudentDetailPageState._slate),
              ),
            ]
          : student.subjects
                .map(
                  (target) => _InfoLine(
                    icon: Icons.menu_book_rounded,
                    label: target.subject,
                    value: target.targetLevel,
                  ),
                )
                .toList(),
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
          backgroundColor: _StudentDetailPageState._navy,
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
              color: _StudentDetailPageState._text,
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
                  backgroundColor: _StudentDetailPageState._navy,
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
      return _StudentDetailPageState._emerald;
    }
    if (normalized == 'cancelled' || normalized == 'canceled') {
      return _StudentDetailPageState._red;
    }
    return _StudentDetailPageState._blue;
  }
}

class _PaymentCard extends StatelessWidget {
  const _PaymentCard({required this.payment});

  final PaymentRecord payment;

  @override
  Widget build(BuildContext context) {
    final dueDate = payment.dueDateUtc == null
        ? 'Tarih yok'
        : DateFormat('dd.MM.yyyy').format(payment.dueDateUtc!.toLocal());
    final color = payment.outstandingAmount > 0
        ? _StudentDetailPageState._amber
        : _StudentDetailPageState._emerald;

    return _ListCard(
      leadingColor: color,
      icon: Icons.payments_rounded,
      title: payment.description,
      subtitle: 'Vade: $dueDate',
      trailing: _StatusPill(label: payment.status, color: color),
      footer:
          '${payment.outstandingAmount.toStringAsFixed(0)} ${payment.currency} bekleyen',
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
        border: Border.all(color: _StudentDetailPageState._border),
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
                    color: _StudentDetailPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _StudentDetailPageState._slate,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  footer,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: _StudentDetailPageState._navy,
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
                  color: _StudentDetailPageState._text,
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
            backgroundColor: _StudentDetailPageState._divider,
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
              color: _StudentDetailPageState._teal.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.trending_up_rounded,
              color: _StudentDetailPageState._teal,
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
                    color: _StudentDetailPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  level,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _StudentDetailPageState._slate,
                  ),
                ),
              ],
            ),
          ),
          const _StatusPill(
            label: 'Aktif',
            color: _StudentDetailPageState._emerald,
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
                      'Ogrenci Guncelle',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: _StudentDetailPageState._text,
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
                label: 'Ad Soyad',
                validator: (v) =>
                    (v == null || v.trim().length < 2) ? 'Ad soyad giriniz' : null,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _gradeLevelCtrl,
                label: 'Sinif / Seviye',
                validator: (v) =>
                    (v == null || v.trim().length < 2) ? 'Sinif giriniz' : null,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _subjectCtrl,
                label: 'Ders',
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _targetLevelCtrl,
                label: 'Hedef Seviye',
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _contactEmailCtrl,
                label: 'E-posta',
                keyboardType: TextInputType.emailAddress,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _contactPhoneCtrl,
                label: 'Telefon',
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _goalSummaryCtrl,
                label: 'Hedef / Amaç',
                maxLines: 2,
              ),
              const SizedBox(height: 12),
              _FormField(
                controller: _levelNotesCtrl,
                label: 'Seviye Notu',
                maxLines: 2,
              ),
              const SizedBox(height: 16),
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'Aktif Ogrenci',
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        fontWeight: FontWeight.w700,
                        color: _StudentDetailPageState._text,
                      ),
                    ),
                  ),
                  Switch(
                    value: _isActive,
                    activeThumbColor: _StudentDetailPageState._emerald,
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
                    backgroundColor: _StudentDetailPageState._navy,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: const Text(
                    'Kaydet',
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
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
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
