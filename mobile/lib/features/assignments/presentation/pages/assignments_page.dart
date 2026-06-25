import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignments_list_cubit.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignments_list_state.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';

class AssignmentsPage extends StatefulWidget {
  const AssignmentsPage({super.key});

  @override
  State<AssignmentsPage> createState() => _AssignmentsPageState();
}

enum _AssignmentFilter {
  all('Tümü'),
  pending('Bekleyen'),
  inProgress('Devam'),
  completed('Tamamlanan');

  const _AssignmentFilter(this.label);

  final String label;

  bool matches(String? status) {
    return switch (this) {
      _AssignmentFilter.all => true,
      _AssignmentFilter.pending => status == 'Pending',
      _AssignmentFilter.inProgress => status == 'InProgress',
      _AssignmentFilter.completed =>
        status == 'Completed' || status == 'Cancelled',
    };
  }
}

class _AssignmentsPageState extends State<AssignmentsPage> {
  static const _navy = Color(0xFF082B4F);
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF6B7A90);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EEF7);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _red = Color(0xFFFF5A5F);
  static const _purple = Color(0xFF8B5CF6);

  _AssignmentFilter _filter = _AssignmentFilter.all;
  late AssignmentsListCubit _cubit;

  @override
  void initState() {
    super.initState();
    _cubit = AssignmentsListCubit.create();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId != null) {
      _cubit.load(userId);
    }
  }

  @override
  void dispose() {
    _cubit.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider<AssignmentsListCubit>.value(
      value: _cubit,
      child: Scaffold(
        backgroundColor: _background,
        bottomNavigationBar: _AssignmentsBottomNav(
          onHomeTap: () => context.go('/dashboard'),
          onLessonsTap: () => context.go('/lesson-sessions'),
          onStudentsTap: () => context.go('/students'),
          onCalendarTap: () => context.go('/scheduling'),
          onMoreTap: () => context.go('/more'),
        ),
        body: SafeArea(
          child: BlocBuilder<AssignmentsListCubit, AssignmentsListState>(
            builder: (context, state) {
              final filtered = state.assignments
                  .where((a) => _filter.matches(a.status))
                  .toList();

              return RefreshIndicator(
                color: _navy,
                onRefresh: () {
                  final userId =
                      context.read<AuthCubit>().state.session?.userId;
                  if (userId != null) {
                    return _cubit.refresh(userId);
                  }
                  return Future<void>.value();
                },
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(16, 10, 16, 20),
                  children: <Widget>[
                    _PageHeader(onNotificationTap: () {}),
                    const SizedBox(height: 20),
                    _SummaryRow(assignments: state.assignments),
                    const SizedBox(height: 18),
                    _FilterTabs(
                      selected: _filter,
                      onChanged: (f) => setState(() => _filter = f),
                    ),
                    const SizedBox(height: 14),
                    if (state.isLoading)
                      _ShimmerList()
                    else if (state.errorMessage != null)
                      _ErrorCard(
                        message: state.errorMessage!,
                        onRetry: () {
                          final userId =
                              context.read<AuthCubit>().state.session?.userId;
                          if (userId != null) {
                            _cubit.load(userId);
                          }
                        },
                      )
                    else if (filtered.isEmpty)
                      _EmptyCard(filter: _filter)
                    else
                      ...filtered.map(
                        (a) => Padding(
                          padding: const EdgeInsets.only(bottom: 12),
                          child: _AssignmentTile(item: a),
                        ),
                      ),
                  ],
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}

class _PageHeader extends StatelessWidget {
  const _PageHeader({required this.onNotificationTap});

  final VoidCallback onNotificationTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            'Ödevler',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: _AssignmentsPageState._text,
              fontWeight: FontWeight.w900,
            ),
          ),
        ),
        InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: onNotificationTap,
          child: Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: _AssignmentsPageState._border),
            ),
            child: const Icon(
              Icons.notifications_none_rounded,
              color: _AssignmentsPageState._text,
            ),
          ),
        ),
      ],
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.assignments});

  final List<AssignmentItem> assignments;

  @override
  Widget build(BuildContext context) {
    final pending = assignments.where((a) => a.status == 'Pending').length;
    final inProgress = assignments.where((a) => a.status == 'InProgress').length;
    final completed = assignments.where((a) => a.status == 'Completed').length;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: _AssignmentsPageState._navy,
        borderRadius: BorderRadius.circular(22),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x22082B4F),
            blurRadius: 24,
            offset: Offset(0, 12),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Ödev Özeti',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Tüm öğrencilerin ödev durumu',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.72),
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: <Widget>[
              Expanded(
                child: _SummaryMetric(
                  label: 'Bekleyen',
                  value: '$pending',
                  icon: Icons.hourglass_empty_rounded,
                  color: _AssignmentsPageState._amber,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Devam',
                  value: '$inProgress',
                  icon: Icons.autorenew_rounded,
                  color: _AssignmentsPageState._blue,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Tamamlanan',
                  value: '$completed',
                  icon: Icons.check_circle_outline_rounded,
                  color: _AssignmentsPageState._emerald,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _SummaryMetric extends StatelessWidget {
  const _SummaryMetric({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(minHeight: 96),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: color, size: 20),
          const SizedBox(height: 10),
          Text(
            label,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.78),
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w900,
            ),
          ),
        ],
      ),
    );
  }
}

class _FilterTabs extends StatelessWidget {
  const _FilterTabs({required this.selected, required this.onChanged});

  final _AssignmentFilter selected;
  final ValueChanged<_AssignmentFilter> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: _AssignmentsPageState._border),
      ),
      child: Row(
        children: _AssignmentFilter.values.map((f) {
          final isSelected = f == selected;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(14),
              onTap: () => onChanged(f),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 160),
                padding: const EdgeInsets.symmetric(vertical: 10),
                decoration: BoxDecoration(
                  color: isSelected
                      ? _AssignmentsPageState._navy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    f.label,
                    maxLines: 1,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: isSelected
                          ? Colors.white
                          : _AssignmentsPageState._slate,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class _AssignmentTile extends StatelessWidget {
  const _AssignmentTile({required this.item});

  final AssignmentItem item;

  @override
  Widget build(BuildContext context) {
    final (color, icon, label) = _statusMeta(item.status);
    final dueText = _dueLabel(item.dueDateUtc);

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _AssignmentsPageState._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x0F082B4F),
            blurRadius: 16,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(icon, color: color, size: 22),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  item.title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: _AssignmentsPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                if (item.description.isNotEmpty) ...<Widget>[
                  const SizedBox(height: 3),
                  Text(
                    item.description,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _AssignmentsPageState._slate,
                    ),
                  ),
                ],
                if (dueText != null) ...<Widget>[
                  const SizedBox(height: 6),
                  Row(
                    children: <Widget>[
                      Icon(
                        Icons.event_rounded,
                        size: 13,
                        color: _AssignmentsPageState._slate,
                      ),
                      const SizedBox(width: 4),
                      Text(
                        dueText,
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                          color: _AssignmentsPageState._slate,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 10),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              label,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: color,
                fontWeight: FontWeight.w900,
              ),
            ),
          ),
        ],
      ),
    );
  }

  static (Color, IconData, String) _statusMeta(String? status) {
    return switch (status) {
      'Pending' => (
        _AssignmentsPageState._amber,
        Icons.hourglass_empty_rounded,
        'Bekleyen',
      ),
      'InProgress' => (
        _AssignmentsPageState._blue,
        Icons.autorenew_rounded,
        'Devam',
      ),
      'Completed' => (
        _AssignmentsPageState._emerald,
        Icons.check_circle_outline_rounded,
        'Tamamlandı',
      ),
      'Cancelled' => (
        _AssignmentsPageState._red,
        Icons.cancel_outlined,
        'İptal',
      ),
      _ => (
        _AssignmentsPageState._purple,
        Icons.assignment_outlined,
        status ?? '-',
      ),
    };
  }

  static String? _dueLabel(DateTime? date) {
    if (date == null) return null;
    const months = <String>[
      'Oca',
      'Şub',
      'Mar',
      'Nis',
      'May',
      'Haz',
      'Tem',
      'Ağu',
      'Eyl',
      'Eki',
      'Kas',
      'Ara',
    ];
    final local = date.toLocal();
    return '${local.day} ${months[local.month - 1]} ${local.year}';
  }
}

class _ShimmerList extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Shimmer.fromColors(
      baseColor: const Color(0xFFEEF4FB),
      highlightColor: Colors.white,
      child: Column(
        children: List<Widget>.generate(
          5,
          (_) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Container(
              height: 80,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(22),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard({required this.filter});

  final _AssignmentFilter filter;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 32),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _AssignmentsPageState._border),
      ),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.assignment_outlined,
            size: 40,
            color: _AssignmentsPageState._slate.withValues(alpha: 0.5),
          ),
          const SizedBox(height: 12),
          Text(
            filter == _AssignmentFilter.all
                ? 'Henüz ödev yok'
                : '${filter.label} ödev yok',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _AssignmentsPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            'Ders sonrası ödev vermek için ders detayından takip ekleyebilirsiniz.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: _AssignmentsPageState._slate,
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _AssignmentsPageState._border),
      ),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.error_outline_rounded,
            size: 36,
            color: _AssignmentsPageState._red,
          ),
          const SizedBox(height: 10),
          Text(
            'Ödevler yüklenemedi',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _AssignmentsPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            message,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: _AssignmentsPageState._slate,
            ),
          ),
          const SizedBox(height: 14),
          TextButton(
            onPressed: onRetry,
            child: const Text('Tekrar Dene'),
          ),
        ],
      ),
    );
  }
}

class _AssignmentsBottomNav extends StatelessWidget {
  const _AssignmentsBottomNav({
    required this.onHomeTap,
    required this.onLessonsTap,
    required this.onStudentsTap,
    required this.onCalendarTap,
    required this.onMoreTap,
  });

  final VoidCallback onHomeTap;
  final VoidCallback onLessonsTap;
  final VoidCallback onStudentsTap;
  final VoidCallback onCalendarTap;
  final VoidCallback onMoreTap;

  @override
  Widget build(BuildContext context) {
    final items = <_NavItem>[
      _NavItem(Icons.home_rounded, 'Ana sayfa', false, onHomeTap),
      _NavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      _NavItem(Icons.groups_rounded, 'Öğrenciler', false, onStudentsTap),
      _NavItem(Icons.calendar_month_rounded, 'Takvim', false, onCalendarTap),
      const _NavItem(Icons.assignment_rounded, 'Ödevler', true),
      _NavItem(Icons.widgets_rounded, 'Diğer', false, onMoreTap),
    ];

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: _AssignmentsPageState._border)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: items.map((item) {
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap: item.onTap,
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(
                      item.icon,
                      color: item.selected
                          ? _AssignmentsPageState._navy
                          : _AssignmentsPageState._slate,
                    ),
                    const SizedBox(height: 4),
                    FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Text(
                        item.label,
                        maxLines: 1,
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: item.selected
                                  ? _AssignmentsPageState._navy
                                  : _AssignmentsPageState._slate,
                              fontWeight: item.selected
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

class _NavItem {
  const _NavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}
