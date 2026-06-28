import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:shimmer/shimmer.dart';

class PaymentsPage extends StatefulWidget {
  const PaymentsPage({super.key});

  @override
  State<PaymentsPage> createState() => _PaymentsPageState();
}

class _PaymentsPageState extends State<PaymentsPage> {
  late final PaymentsCubit _cubit;
  _PaymentFilter _selectedFilter = _PaymentFilter.all;

  @override
  void initState() {
    super.initState();
    _cubit = PaymentsCubit.create();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId != null &&
        _cubit.state.records.isEmpty &&
        !_cubit.state.isLoading) {
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
    final session = context.select((AuthCubit c) => c.state.session);
    final teacherName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Öğretmen';

    return BlocProvider<PaymentsCubit>.value(
      value: _cubit,
      child: BlocConsumer<PaymentsCubit, PaymentsState>(
        listener: (context, state) {
          if (state.successMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.successMessage!),
                backgroundColor: AppColors.accentGreen,
                behavior: SnackBarBehavior.floating,
              ),
            );
          }
          if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.errorMessage!),
                backgroundColor: AppColors.accentRed,
                behavior: SnackBarBehavior.floating,
              ),
            );
          }
        },
        builder: (context, state) {
          final filtered = state.records
              .where((r) => _selectedFilter.matches(r))
              .toList();

          return Scaffold(
            backgroundColor: AppColors.background,
            floatingActionButton: FloatingActionButton.extended(
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              onPressed: () {
                context.push('/payments/new').then((_) {
                  if (!context.mounted) return;
                  final userId = context
                      .read<AuthCubit>()
                      .state
                      .session
                      ?.userId;
                  if (userId != null) _cubit.load(userId);
                });
              },
              label: const Text('Ödeme Ekle'),
              icon: const Icon(Icons.add_card_rounded),
            ),
            bottomNavigationBar: _FinanceBottomNav(
              onHomeTap: () => context.go('/dashboard'),
              onLessonsTap: () => context.go('/lesson-sessions'),
              onStudentsTap: () => context.go('/students'),
              onCalendarTap: () => context.go('/scheduling'),
              onMoreTap: () => context.go('/more'),
            ),
            body: SafeArea(
              child: RefreshIndicator(
                color: AppColors.primary,
                onRefresh: () {
                  final userId = context
                      .read<AuthCubit>()
                      .state
                      .session
                      ?.userId;
                  if (userId != null) return _cubit.load(userId);
                  return Future<void>.value();
                },
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(16, 10, 16, 116),
                  children: <Widget>[
                    AppPageHeader(title: teacherName),
                    const SizedBox(height: 22),
                    if (state.isLoading)
                      const _ShimmerSummary()
                    else
                      _FinanceSummaryPanel(records: state.records),
                    const SizedBox(height: 18),
                    _PaymentTabs(
                      selectedFilter: _selectedFilter,
                      onChanged: (f) => setState(() => _selectedFilter = f),
                    ),
                    const SizedBox(height: 14),
                    if (state.isLoading)
                      const _ShimmerList()
                    else if (state.errorMessage != null &&
                        state.records.isEmpty)
                      _ErrorCard(
                        message: state.errorMessage!,
                        onRetry: () {
                          final userId = context
                              .read<AuthCubit>()
                              .state
                              .session
                              ?.userId;
                          if (userId != null) _cubit.load(userId);
                        },
                      )
                    else if (filtered.isEmpty)
                      _EmptyPanel(
                        title: '${_selectedFilter.label} ödeme yok',
                        subtitle:
                            'Bu durumda görüntülenecek ödeme kaydı bulunmuyor.',
                      )
                    else
                      _PaymentList(
                        records: filtered,
                        isSaving: state.isSaving,
                        onMarkPaid: (record) => _cubit.markPaid(record),
                      ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

// ── Shimmer ──────────────────────────────────────────────────────────────────

class _ShimmerSummary extends StatelessWidget {
  const _ShimmerSummary();

  @override
  Widget build(BuildContext context) {
    return Shimmer.fromColors(
      baseColor: const Color(0xFFD0DFF0),
      highlightColor: const Color(0xFFECF4FF),
      child: Container(
        height: 150,
        decoration: BoxDecoration(
          color: AppColors.primary,
          borderRadius: BorderRadius.circular(22),
        ),
      ),
    );
  }
}

class _ShimmerList extends StatelessWidget {
  const _ShimmerList();

  @override
  Widget build(BuildContext context) {
    return Shimmer.fromColors(
      baseColor: const Color(0xFFEEF4FB),
      highlightColor: Colors.white,
      child: Column(
        children: List<Widget>.generate(
          4,
          (_) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Container(
              height: 90,
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

// ── Hata kartı ──────────────────────────────────────────────────────────────

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
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.error_outline_rounded,
            size: 36,
            color: AppColors.accentRed,
          ),
          const SizedBox(height: 10),
          Text(
            'Ödemeler yüklenemedi',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            message,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 14),
          TextButton(onPressed: onRetry, child: const Text('Tekrar Dene')),
        ],
      ),
    );
  }
}

// ── Sayfa bileşenleri ────────────────────────────────────────────────────────

class _FinanceSummaryPanel extends StatelessWidget {
  const _FinanceSummaryPanel({required this.records});

  final List<PaymentRecord> records;

  @override
  Widget build(BuildContext context) {
    final collected = records
        .where((r) => r.status == 'Paid')
        .fold<double>(0, (t, r) => t + r.collectedAmount);
    final outstanding = records
        .where((r) => !r.isOverdue && r.status != 'Paid')
        .fold<double>(0, (t, r) => t + r.outstandingAmount);
    final overdue = records
        .where((r) => r.isOverdue)
        .fold<double>(0, (t, r) => t + r.outstandingAmount);

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: AppColors.primary,
        borderRadius: BorderRadius.circular(22),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Finans Özeti',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            '${records.length} ödeme kaydı',
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
                  label: 'Tahsil edilen',
                  value: _money(collected),
                  icon: Icons.trending_up_rounded,
                  color: AppColors.accentGreen,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Bekleyen',
                  value: _money(outstanding),
                  icon: Icons.schedule_rounded,
                  color: AppColors.amber,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Geciken',
                  value: _money(overdue),
                  icon: Icons.warning_amber_rounded,
                  color: AppColors.accentRed,
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
      constraints: const BoxConstraints(minHeight: 112),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: color, size: 21),
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
          const SizedBox(height: 10),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w900,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PaymentTabs extends StatelessWidget {
  const _PaymentTabs({required this.selectedFilter, required this.onChanged});

  final _PaymentFilter selectedFilter;
  final ValueChanged<_PaymentFilter> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: _PaymentFilter.values.map((filter) {
          final selected = filter == selectedFilter;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(14),
              onTap: () => onChanged(filter),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                padding: const EdgeInsets.symmetric(vertical: 11),
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary : Colors.white,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    filter.label,
                    maxLines: 1,
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: selected ? Colors.white : AppColors.textSecondary,
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

class _PaymentList extends StatelessWidget {
  const _PaymentList({
    required this.records,
    required this.isSaving,
    required this.onMarkPaid,
  });

  final List<PaymentRecord> records;
  final bool isSaving;
  final ValueChanged<PaymentRecord> onMarkPaid;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: List<Widget>.generate(records.length, (index) {
        return Padding(
          padding: EdgeInsets.only(
            bottom: index == records.length - 1 ? 0 : 12,
          ),
          child: _PaymentTile(
            record: records[index],
            accentColor: _accentForIndex(index),
            isSaving: isSaving,
            onMarkPaid: () => onMarkPaid(records[index]),
          ),
        );
      }),
    );
  }

  Color _accentForIndex(int index) {
    const colors = <Color>[
      AppColors.accentGreen,
      AppColors.accentBlue,
      AppColors.amber,
      AppColors.accentRed,
    ];
    return colors[index % colors.length];
  }
}

class _PaymentTile extends StatelessWidget {
  const _PaymentTile({
    required this.record,
    required this.accentColor,
    required this.isSaving,
    required this.onMarkPaid,
  });

  final PaymentRecord record;
  final Color accentColor;
  final bool isSaving;
  final VoidCallback onMarkPaid;

  @override
  Widget build(BuildContext context) {
    final isPaid = record.status == 'Paid';
    final isOverdue = record.isOverdue;
    final statusColor = isPaid
        ? AppColors.accentGreen
        : isOverdue
        ? AppColors.accentRed
        : record.status == 'PartiallyPaid'
        ? AppColors.accentBlue
        : AppColors.amber;
    final statusLabel = isPaid
        ? 'Ödendi'
        : isOverdue
        ? 'Geciken'
        : record.status == 'PartiallyPaid'
        ? 'Kısmi'
        : 'Bekleyen';
    final displayAmount = isPaid
        ? record.collectedAmount
        : record.outstandingAmount;
    final dueLabel = record.dueDateUtc != null
        ? _dateLabel(record.dueDateUtc!)
        : '';

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _InitialsAvatar(name: record.description, accent: accentColor),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  record.description,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                if (dueLabel.isNotEmpty) ...<Widget>[
                  const SizedBox(height: 4),
                  Row(
                    children: <Widget>[
                      Icon(
                        Icons.event_rounded,
                        size: 15,
                        color: AppColors.textSecondary.withValues(alpha: 0.84),
                      ),
                      const SizedBox(width: 4),
                      Text(
                        dueLabel,
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: AppColors.textSecondary,
                              fontWeight: FontWeight.w600,
                            ),
                      ),
                    ],
                  ),
                ],
                if (!isPaid) ...<Widget>[
                  const SizedBox(height: 8),
                  SizedBox(
                    height: 34,
                    child: OutlinedButton.icon(
                      onPressed: isSaving ? null : onMarkPaid,
                      icon: const Icon(
                        Icons.check_circle_outline_rounded,
                        size: 16,
                      ),
                      label: const Text('Tahsil Et'),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.primary,
                        side: const BorderSide(color: AppColors.primary),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 0,
                        ),
                        textStyle: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 12,
                        ),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 10),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                _money(displayAmount),
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 8),
              _StatusPill(label: statusLabel, color: statusColor),
            ],
          ),
        ],
      ),
    );
  }
}

class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.name, required this.accent});

  final String name;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 54,
      height: 54,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.92),
            accent.withValues(alpha: 0.56),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w900,
        ),
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
    );
  }
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: <Widget>[
          Icon(Icons.receipt_long_outlined, color: AppColors.primary, size: 32),
          const SizedBox(height: 10),
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class _FinanceBottomNav extends StatelessWidget {
  const _FinanceBottomNav({
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
    final items = <_BottomNavItem>[
      _BottomNavItem(Icons.home_rounded, 'Ana sayfa', false, onHomeTap),
      _BottomNavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      _BottomNavItem(Icons.groups_rounded, 'Öğrenciler', false, onStudentsTap),
      _BottomNavItem(
        Icons.calendar_month_rounded,
        'Takvim',
        false,
        onCalendarTap,
      ),
      const _BottomNavItem(
        Icons.account_balance_wallet_rounded,
        'Finans',
        true,
      ),
      _BottomNavItem(Icons.widgets_rounded, 'Diğer', false, onMoreTap),
    ];

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColors.border)),
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
                          ? AppColors.primary
                          : AppColors.textSecondary,
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
                                  ? AppColors.primary
                                  : AppColors.textSecondary,
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

// ── Enum + yardımcı ──────────────────────────────────────────────────────────

enum _PaymentFilter {
  all('Tümü'),
  paid('Ödenen'),
  pending('Bekleyen'),
  overdue('Geciken');

  const _PaymentFilter(this.label);

  final String label;

  bool matches(PaymentRecord record) {
    return switch (this) {
      _PaymentFilter.all => true,
      _PaymentFilter.paid => record.status == 'Paid',
      _PaymentFilter.pending => record.status != 'Paid' && !record.isOverdue,
      _PaymentFilter.overdue => record.isOverdue,
    };
  }
}

class _BottomNavItem {
  const _BottomNavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}

String _money(double amount) {
  final formatted = NumberFormat.decimalPattern('tr_TR').format(amount.round());
  return '$formatted TRY';
}

String _dateLabel(DateTime date) {
  const months = <String>[
    'Ocak',
    'Şubat',
    'Mart',
    'Nisan',
    'Mayıs',
    'Haziran',
    'Temmuz',
    'Ağustos',
    'Eylül',
    'Ekim',
    'Kasım',
    'Aralık',
  ];
  return '${date.day} ${months[date.month - 1]} ${date.year}';
}

String _initials(String value) {
  final parts = value.trim().split(RegExp(r'\s+'));
  if (parts.isEmpty || parts.first.isEmpty) return '?';
  if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
  return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
      .toUpperCase();
}
