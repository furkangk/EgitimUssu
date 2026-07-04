import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class ParentHomePage extends StatelessWidget {
  const ParentHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final userId = context.select(
      (AuthCubit c) => c.state.session?.userId ?? 'mock-parent-user',
    );
    final fullName = context.select(
      (AuthCubit c) => c.state.session?.fullName ?? 'Veli',
    );

    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..load(userId, fullName: fullName),
      child: Scaffold(
        backgroundColor: AppColors.background,
        bottomNavigationBar: const ParentBottomNav(current: ParentNavTab.home),
        body: SafeArea(
          child: Column(
            children: <Widget>[
              ParentHeader(
                title: 'Merhaba, ${fullName.split(' ').first}',
                subtitle: 'Çocuğunuzun bu haftaki gelişimi',
              ),
              Expanded(
                child: BlocBuilder<ParentCubit, ParentState>(
                  builder: (context, state) {
                    if (state.isLoading) {
                      return const LoadingStateView(
                        message: 'Veli paneli yükleniyor...',
                      );
                    }
                    if (state.status == ParentStatus.error) {
                      return Padding(
                        padding: const EdgeInsets.all(20),
                        child: ErrorStateView(
                          message: state.errorMessage ?? 'Bir hata oluştu.',
                          onRetry: () => context.read<ParentCubit>().load(
                            userId,
                            fullName: fullName,
                          ),
                        ),
                      );
                    }
                    if (state.approvedChildren.isEmpty) {
                      return _EmptyChildren(
                        onLink: () => context.go('/parent/children'),
                      );
                    }
                    return _HomeContent(userId: userId, fullName: fullName);
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyChildren extends StatelessWidget {
  const _EmptyChildren({required this.onLink});

  final VoidCallback onLink;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const EmptyStateView(
              title: 'Henüz onaylı çocuk yok',
              subtitle:
                  'Çocuğunuzu bağladıktan ve bağ onaylandıktan sonra gelişimi '
                  'burada görünecek.',
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onLink,
              icon: const Icon(Icons.person_add_alt_1_rounded),
              label: const Text('Çocuk bağla'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                minimumSize: const Size(220, 48),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _HomeContent extends StatelessWidget {
  const _HomeContent({required this.userId, required this.fullName});

  final String userId;
  final String fullName;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<ParentCubit>().state;
    final cubit = context.read<ParentCubit>();
    final children = state.approvedChildren;
    final dashboard = state.dashboard;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => cubit.refresh(userId, fullName: fullName),
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: <Widget>[
          if (children.length > 1) ...<Widget>[
            _ChildSelector(
              children: children,
              selectedId: state.selectedStudentId,
              onSelect: (id) => cubit.selectChild(userId, id),
            ),
            const SizedBox(height: 14),
          ],
          if (state.dashboardLoading || dashboard == null)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 40),
              child: LoadingStateView(message: 'Gelişim verisi yükleniyor...'),
            )
          else
            _DashboardBody(
              dashboard: dashboard,
              onDetail: () => context.push('/parent/child-detail', extra: dashboard.studentId),
            ),
        ],
      ),
    );
  }
}

class _ChildSelector extends StatelessWidget {
  const _ChildSelector({
    required this.children,
    required this.selectedId,
    required this.onSelect,
  });

  final List<ChildLink> children;
  final String? selectedId;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: children.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final child = children[index];
          final selected = child.studentId == selectedId;
          return ChoiceChip(
            label: Text(child.displayName),
            selected: selected,
            onSelected: (_) => onSelect(child.studentId),
            selectedColor: AppColors.primary,
            backgroundColor: AppColors.surface,
            side: const BorderSide(color: AppColors.border),
            labelStyle: TextStyle(
              color: selected ? Colors.white : AppColors.textPrimary,
              fontWeight: FontWeight.w700,
            ),
          );
        },
      ),
    );
  }
}

class _DashboardBody extends StatelessWidget {
  const _DashboardBody({required this.dashboard, required this.onDetail});

  final ChildDashboard dashboard;
  final VoidCallback onDetail;

  @override
  Widget build(BuildContext context) {
    final study = dashboard.study;
    final lessons = dashboard.lessons;
    final assignments = dashboard.assignments;
    final payments = dashboard.payments;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 12,
          crossAxisSpacing: 12,
          childAspectRatio: 1.35,
          children: <Widget>[
            ParentStatTile(
              icon: Icons.timer_rounded,
              value: formatMinutes(study.weeklyStudyMinutes),
              label: 'Bu hafta çalışma',
              accent: AppColors.primary,
            ),
            ParentStatTile(
              icon: Icons.local_fire_department_rounded,
              value: '${study.streakDays} gün',
              label: 'Çalışma serisi',
              accent: AppColors.accentOrange,
            ),
            ParentStatTile(
              icon: Icons.menu_book_rounded,
              value: '${lessons.completedLessonCount}',
              label: 'Tamamlanan ders',
              accent: AppColors.accentGreen,
            ),
            ParentStatTile(
              icon: Icons.assignment_late_rounded,
              value: '${assignments.openCount}',
              label: 'Açık ödev',
              accent: AppColors.accentRed,
            ),
          ],
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Haftalık çalışma',
          icon: Icons.bar_chart_rounded,
          child: study.hasData && study.weeklyBreakdownMinutes.isNotEmpty
              ? ParentWeeklyBars(minutesPerDay: study.weeklyBreakdownMinutes)
              : Padding(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  child: Text(
                    study.hasData
                        ? 'Bu hafta ${formatMinutes(study.weeklyStudyMinutes)} çalışıldı.'
                        : 'Bireysel çalışma verisi henüz paylaşılmadı.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Ödeme özeti',
          icon: Icons.account_balance_wallet_rounded,
          child: Column(
            children: <Widget>[
              _MoneyRow(
                label: 'Beklenen',
                amount: payments.expectedTotal,
                currency: payments.currency,
                color: AppColors.textPrimary,
              ),
              _MoneyRow(
                label: 'Tahsil edilen',
                amount: payments.collectedTotal,
                currency: payments.currency,
                color: AppColors.accentGreen,
              ),
              _MoneyRow(
                label: 'Kalan',
                amount: payments.outstandingTotal,
                currency: payments.currency,
                color: payments.outstandingTotal > 0
                    ? AppColors.accentRed
                    : AppColors.textSecondary,
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        SizedBox(
          width: double.infinity,
          child: OutlinedButton.icon(
            onPressed: onDetail,
            icon: const Icon(Icons.insights_rounded),
            label: const Text('Detaylı gelişim'),
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.primary,
              side: const BorderSide(color: AppColors.primary),
              minimumSize: const Size.fromHeight(48),
            ),
          ),
        ),
      ],
    );
  }
}

class _MoneyRow extends StatelessWidget {
  const _MoneyRow({
    required this.label,
    required this.amount,
    required this.currency,
    required this.color,
  });

  final String label;
  final double amount;
  final String currency;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          Text(
            '${amount.toStringAsFixed(0)} $currency',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}
