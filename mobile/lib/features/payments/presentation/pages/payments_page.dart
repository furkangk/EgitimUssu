import 'dart:async';

import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/collect_payment_sheet.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/finance_charts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/payment_filter_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:shimmer/shimmer.dart';

/// Filtre sekmeleri → sunucu `status` değeri eşlemesi.
const List<(String, String?)> _statusTabs = <(String, String?)>[
  ('Tümü', null),
  ('Ödenen', 'Paid'),
  ('Bekleyen', 'Open'),
  ('Geciken', 'Overdue'),
];

class PaymentsPage extends StatefulWidget {
  const PaymentsPage({super.key});

  @override
  State<PaymentsPage> createState() => _PaymentsPageState();
}

class _PaymentsPageState extends State<PaymentsPage> {
  late final PaymentsCubit _cubit;
  late final StudentsCubit _studentsCubit;
  final ScrollController _scrollController = ScrollController();
  final TextEditingController _searchController = TextEditingController();
  Timer? _searchDebounce;

  @override
  void initState() {
    super.initState();
    _cubit = PaymentsCubit.create();
    _studentsCubit = StudentsCubit.create();
    _scrollController.addListener(_onScroll);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId == null) return;
    if (_cubit.state.records.isEmpty && !_cubit.state.isLoading) {
      _cubit.load(userId);
    }
    if (_studentsCubit.state.students.isEmpty && !_studentsCubit.state.isLoading) {
      _studentsCubit.load(userId);
    }
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _scrollController.dispose();
    _searchController.dispose();
    _cubit.close();
    _studentsCubit.close();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 400) {
      _cubit.loadMore();
    }
  }

  void _onSearchChanged(String text) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 350), () {
      _cubit.applyFilters(_cubit.state.filters.copyWith(query: text));
    });
  }

  Future<void> _onCollect(BuildContext context, PaymentRecord record) async {
    final amount = await CollectPaymentSheet.show(context, record);
    if (amount == null) return;
    await _cubit.collect(record, amount);
  }

  Future<void> _onEdit(BuildContext context, PaymentRecord record) async {
    await context.push('/payments/edit', extra: record);
    if (!context.mounted) return;
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId != null) _cubit.load(userId);
  }

  Future<void> _openFilterSheet(BuildContext context, PaymentFilters current) async {
    final result = await PaymentFilterSheet.show(
      context,
      filters: current,
      students: _studentsCubit.state.students,
    );
    if (result != null) _cubit.applyFilters(result);
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
          return Scaffold(
            backgroundColor: AppColors.background,
            floatingActionButton: FloatingActionButton.extended(
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              onPressed: () {
                context.push('/payments/new').then((_) {
                  if (!context.mounted) return;
                  final userId =
                      context.read<AuthCubit>().state.session?.userId;
                  if (userId != null) _cubit.load(userId);
                });
              },
              label: const Text('Ödeme Ekle'),
              icon: const Icon(Icons.add_card_rounded),
            ),
            bottomNavigationBar: const AppBottomNav(current: AppNavTab.finance),
            body: SafeArea(
              child: RefreshIndicator(
                color: AppColors.primary,
                onRefresh: () {
                  final userId =
                      context.read<AuthCubit>().state.session?.userId;
                  if (userId != null) return _cubit.load(userId);
                  return Future<void>.value();
                },
                child: CustomScrollView(
                  controller: _scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  slivers: <Widget>[
                    SliverToBoxAdapter(
                      child: Padding(
                        padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: <Widget>[
                            AppPageHeader(title: teacherName),
                            const SizedBox(height: 22),
                            if (state.summary == null && state.isLoading)
                              const _ShimmerSummary()
                            else if (state.summary != null)
                              _FinanceSummaryPanel(summary: state.summary!),
                            if (state.summary != null) ...<Widget>[
                              const SizedBox(height: 14),
                              _StatsSection(summary: state.summary!),
                            ],
                            const SizedBox(height: 16),
                            Row(
                              children: <Widget>[
                                Expanded(
                                  child: _SearchField(
                                    controller: _searchController,
                                    onChanged: _onSearchChanged,
                                  ),
                                ),
                                const SizedBox(width: 10),
                                _FilterButton(
                                  count: state.filters.advancedCount,
                                  onTap: () =>
                                      _openFilterSheet(context, state.filters),
                                ),
                              ],
                            ),
                            const SizedBox(height: 12),
                            _PaymentTabs(
                              selectedStatus: state.filters.status,
                              resultCount: state.totalCount,
                              onChanged: (status) => _cubit.applyFilters(
                                state.filters.copyWith(status: status),
                              ),
                            ),
                            if (state.filters.studentId != null ||
                                state.filters.hasDate) ...<Widget>[
                              const SizedBox(height: 10),
                              _ActiveFilterChips(
                                filters: state.filters,
                                onClearStudent: () => _cubit.applyFilters(
                                  state.filters.copyWith(
                                    studentId: null,
                                    studentLabel: null,
                                  ),
                                ),
                                onClearDate: () => _cubit.applyFilters(
                                  state.filters.copyWith(
                                    dateFromUtc: null,
                                    dateToUtc: null,
                                  ),
                                ),
                              ),
                            ],
                            const SizedBox(height: 14),
                          ],
                        ),
                      ),
                    ),
                    if (state.isLoading)
                      const SliverToBoxAdapter(
                        child: Padding(
                          padding: EdgeInsets.symmetric(horizontal: 16),
                          child: _ShimmerList(),
                        ),
                      )
                    else if (state.errorMessage != null && state.records.isEmpty)
                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 16),
                          child: _ErrorCard(
                            message: state.errorMessage!,
                            onRetry: () {
                              final userId = context
                                  .read<AuthCubit>()
                                  .state
                                  .session
                                  ?.userId;
                              if (userId != null) _cubit.load(userId);
                            },
                          ),
                        ),
                      )
                    else if (state.records.isEmpty)
                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 16),
                          child: _hasAnyFilter(state.filters)
                              ? const _EmptyPanel(
                                  title: 'Sonuç bulunamadı',
                                  subtitle:
                                      'Arama veya filtreleri değiştirmeyi deneyin.',
                                )
                              : const _EmptyPanel(
                                  title: 'Ödeme kaydı yok',
                                  subtitle:
                                      'Sağ alttaki "Ödeme Ekle" ile ilk kaydı oluşturun.',
                                ),
                        ),
                      )
                    else ...<Widget>[
                      SliverPadding(
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        sliver: SliverList(
                          delegate: SliverChildBuilderDelegate((context, index) {
                            final record = state.records[index];
                            return Padding(
                              padding: EdgeInsets.only(
                                bottom:
                                    index == state.records.length - 1 ? 0 : 12,
                              ),
                              child: _PaymentTile(
                                record: record,
                                accentColor: _accentForIndex(index),
                                isSaving: state.savingRecordId == record.id,
                                onCollect: () => _onCollect(context, record),
                                onEdit: () => _onEdit(context, record),
                              ),
                            );
                          }, childCount: state.records.length),
                        ),
                      ),
                      if (state.isLoadingMore)
                        const SliverToBoxAdapter(
                          child: Padding(
                            padding: EdgeInsets.symmetric(vertical: 16),
                            child: Center(
                              child: SizedBox(
                                width: 22,
                                height: 22,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.4,
                                  color: AppColors.primary,
                                ),
                              ),
                            ),
                          ),
                        ),
                    ],
                    const SliverToBoxAdapter(child: SizedBox(height: 120)),
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

bool _hasAnyFilter(PaymentFilters f) =>
    f.query.trim().isNotEmpty ||
    f.status != null ||
    f.studentId != null ||
    f.hasDate;

// ── Arama + filtre çubuğu ─────────────────────────────────────────────────────

class _SearchField extends StatelessWidget {
  const _SearchField({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      style: const TextStyle(fontWeight: FontWeight.w600),
      decoration: InputDecoration(
        isDense: true,
        hintText: 'Ödeme ara…',
        prefixIcon: const Icon(Icons.search_rounded, size: 20),
        suffixIcon: controller.text.isEmpty
            ? null
            : IconButton(
                icon: const Icon(Icons.close_rounded, size: 18),
                onPressed: () {
                  controller.clear();
                  onChanged('');
                },
              ),
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AppColors.primary, width: 1.4),
        ),
      ),
    );
  }
}

class _FilterButton extends StatelessWidget {
  const _FilterButton({required this.count, required this.onTap});

  final int count;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = count > 0;
    return Material(
      color: active ? AppColors.primary : Colors.white,
      borderRadius: BorderRadius.circular(16),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Container(
          height: 48,
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: active ? AppColors.primary : AppColors.border,
            ),
          ),
          child: Row(
            children: <Widget>[
              Icon(
                Icons.tune_rounded,
                size: 20,
                color: active ? Colors.white : AppColors.textSecondary,
              ),
              if (active) ...<Widget>[
                const SizedBox(width: 6),
                Text(
                  '$count',
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ActiveFilterChips extends StatelessWidget {
  const _ActiveFilterChips({
    required this.filters,
    required this.onClearStudent,
    required this.onClearDate,
  });

  final PaymentFilters filters;
  final VoidCallback onClearStudent;
  final VoidCallback onClearDate;

  @override
  Widget build(BuildContext context) {
    final chips = <Widget>[];
    if (filters.studentId != null) {
      chips.add(
        _Chip(
          label: filters.studentLabel ?? 'Öğrenci',
          icon: Icons.person_rounded,
          onClear: onClearStudent,
        ),
      );
    }
    if (filters.hasDate) {
      chips.add(
        _Chip(
          label: _dateRangeLabel(filters),
          icon: Icons.event_rounded,
          onClear: onClearDate,
        ),
      );
    }
    return Wrap(spacing: 8, runSpacing: 8, children: chips);
  }

  static String _dateRangeLabel(PaymentFilters f) {
    final fmt = DateFormat('d MMM', 'tr_TR');
    final from = f.dateFromUtc != null
        ? fmt.format(f.dateFromUtc!.toLocal())
        : '…';
    final to = f.dateToUtc != null ? fmt.format(f.dateToUtc!.toLocal()) : '…';
    return '$from – $to';
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.icon,
    required this.onClear,
  });

  final String label;
  final IconData icon;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.only(left: 10, right: 6, top: 6, bottom: 6),
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: AppColors.skyBorder),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 14, color: AppColors.primary),
          const SizedBox(width: 6),
          Text(
            label,
            style: const TextStyle(
              color: AppColors.primary,
              fontWeight: FontWeight.w700,
              fontSize: 12,
            ),
          ),
          const SizedBox(width: 4),
          GestureDetector(
            onTap: onClear,
            child: const Icon(
              Icons.close_rounded,
              size: 15,
              color: AppColors.primary,
            ),
          ),
        ],
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
              height: 120,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(20),
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
          Icon(Icons.error_outline_rounded, size: 36, color: AppColors.accentRed),
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

// ── Finans özeti (sunucu aggregate'inden) ────────────────────────────────────

class _FinanceSummaryPanel extends StatelessWidget {
  const _FinanceSummaryPanel({required this.summary});

  final PaymentSummary summary;

  @override
  Widget build(BuildContext context) {
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
            '${summary.totalRecords} ödeme kaydı',
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
                  value: _money(summary.collectedTotal),
                  icon: Icons.trending_up_rounded,
                  color: AppColors.accentGreen,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Bekleyen',
                  value: _money(summary.pendingTotal),
                  icon: Icons.schedule_rounded,
                  color: AppColors.amber,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Geciken',
                  value: _money(summary.overdueTotal),
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

// ── İstatistikler (katlanabilir, varsayılan kapalı) ──────────────────────────

class _StatsSection extends StatefulWidget {
  const _StatsSection({required this.summary});

  final PaymentSummary summary;

  @override
  State<_StatsSection> createState() => _StatsSectionState();
}

class _StatsSectionState extends State<_StatsSection> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Material(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          clipBehavior: Clip.antiAlias,
          child: InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                children: <Widget>[
                  const Icon(
                    Icons.insights_rounded,
                    size: 20,
                    color: AppColors.primary,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'İstatistikler',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  Text(
                    _expanded ? 'Gizle' : 'Göster',
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: AppColors.textSecondary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(width: 2),
                  Icon(
                    _expanded
                        ? Icons.expand_less_rounded
                        : Icons.expand_more_rounded,
                    color: AppColors.textSecondary,
                  ),
                ],
              ),
            ),
          ),
        ),
        AnimatedSize(
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeInOut,
          alignment: Alignment.topCenter,
          child: _expanded
              ? Column(
                  children: <Widget>[
                    const SizedBox(height: 12),
                    MonthlyCollectionCard(
                      points: widget.summary.monthlyBreakdown,
                    ),
                    const SizedBox(height: 12),
                    PaymentDistributionCard(summary: widget.summary),
                  ],
                )
              : const SizedBox(width: double.infinity),
        ),
      ],
    );
  }
}

// ── Filtre sekmeleri ─────────────────────────────────────────────────────────

class _PaymentTabs extends StatelessWidget {
  const _PaymentTabs({
    required this.selectedStatus,
    required this.resultCount,
    required this.onChanged,
  });

  final String? selectedStatus;
  final int resultCount;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            children: _statusTabs.map((tab) {
              final selected = tab.$2 == selectedStatus;
              return Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: () => onChanged(tab.$2),
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
                        tab.$1,
                        maxLines: 1,
                        style: Theme.of(context).textTheme.labelLarge?.copyWith(
                          color: selected
                              ? Colors.white
                              : AppColors.textSecondary,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                  ),
                ),
              );
            }).toList(),
          ),
        ),
        Padding(
          padding: const EdgeInsets.only(left: 6, top: 10),
          child: Text(
            '$resultCount kayıt',
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }
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

// ── Ödeme kartı ───────────────────────────────────────────────────────────────

class _PaymentTile extends StatelessWidget {
  const _PaymentTile({
    required this.record,
    required this.accentColor,
    required this.isSaving,
    required this.onCollect,
    required this.onEdit,
  });

  final PaymentRecord record;
  final Color accentColor;
  final bool isSaving;
  final VoidCallback onCollect;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    final isPaid = record.status == 'Paid';
    final isCancelled = record.status == 'Cancelled';
    final isOverdue = record.isOverdue;
    final statusColor = isPaid
        ? AppColors.accentGreen
        : isCancelled
        ? AppColors.textMuted
        : isOverdue
        ? AppColors.accentRed
        : record.status == 'PartiallyPaid'
        ? AppColors.accentBlue
        : AppColors.amber;
    final statusLabel = isPaid
        ? 'Ödendi'
        : isCancelled
        ? 'İptal'
        : isOverdue
        ? 'Geciken'
        : record.status == 'PartiallyPaid'
        ? 'Kısmi'
        : 'Bekleyen';
    final isUnpaid = !isPaid && !isCancelled;
    final amountValue = isPaid
        ? record.collectedAmount
        : isCancelled
        ? record.expectedAmount
        : record.outstandingAmount;
    final amountLabel = isPaid
        ? 'Tahsil edilen'
        : isCancelled
        ? 'Tutar'
        : 'Kalan';
    final dueLabel = record.dueDateUtc != null
        ? _dateLabel(record.dueDateUtc!)
        : '';

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Material(
        color: Colors.transparent,
        borderRadius: BorderRadius.circular(20),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onEdit,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    _InitialsAvatar(
                      name: record.description,
                      accent: accentColor,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            record.description,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context).textTheme.titleMedium
                                ?.copyWith(
                                  color: AppColors.textPrimary,
                                  fontWeight: FontWeight.w800,
                                ),
                          ),
                          if (dueLabel.isNotEmpty) ...<Widget>[
                            const SizedBox(height: 4),
                            Row(
                              children: <Widget>[
                                Icon(
                                  Icons.event_rounded,
                                  size: 14,
                                  color: AppColors.textSecondary.withValues(
                                    alpha: 0.84,
                                  ),
                                ),
                                const SizedBox(width: 4),
                                Flexible(
                                  child: Text(
                                    dueLabel,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: Theme.of(context).textTheme
                                        .labelMedium
                                        ?.copyWith(
                                          color: AppColors.textSecondary,
                                          fontWeight: FontWeight.w600,
                                        ),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ],
                      ),
                    ),
                    const SizedBox(width: 10),
                    _StatusPill(label: statusLabel, color: statusColor),
                  ],
                ),
                const SizedBox(height: 14),
                const Divider(height: 1, thickness: 1, color: AppColors.divider),
                const SizedBox(height: 12),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            amountLabel,
                            style: Theme.of(context).textTheme.labelSmall
                                ?.copyWith(
                                  color: AppColors.textMuted,
                                  fontWeight: FontWeight.w600,
                                ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            _money(amountValue),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context).textTheme.titleLarge
                                ?.copyWith(
                                  color: AppColors.textPrimary,
                                  fontWeight: FontWeight.w900,
                                ),
                          ),
                        ],
                      ),
                    ),
                    if (isUnpaid) ...<Widget>[
                      const SizedBox(width: 10),
                      _CollectAction(isSaving: isSaving, onTap: onCollect),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Kart alt bölümündeki etiketli, yumuşak (tonal) "Tahsil Et" aksiyonu.
class _CollectAction extends StatelessWidget {
  const _CollectAction({required this.isSaving, required this.onTap});

  final bool isSaving;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.primaryLight,
      borderRadius: BorderRadius.circular(12),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: isSaving ? null : onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              if (isSaving)
                const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: AppColors.primary,
                  ),
                )
              else
                const Icon(
                  Icons.check_circle_outline_rounded,
                  size: 18,
                  color: AppColors.primary,
                ),
              const SizedBox(width: 8),
              Text(
                isSaving ? 'İşleniyor…' : 'Tahsil Et',
                style: const TextStyle(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w800,
                  fontSize: 13,
                ),
              ),
            ],
          ),
        ),
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

// ── Yardımcılar ───────────────────────────────────────────────────────────────

String _money(double amount) {
  final formatted = NumberFormat.decimalPattern('tr_TR').format(amount.round());
  return '$formatted TRY';
}

String _dateLabel(DateTime date) {
  const months = <String>[
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
  ];
  final local = date.toLocal();
  return '${local.day} ${months[local.month - 1]} ${local.year}';
}

String _initials(String value) {
  final parts = value.trim().split(RegExp(r'\s+'));
  if (parts.isEmpty || parts.first.isEmpty) return '?';
  if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
  return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
      .toUpperCase();
}
