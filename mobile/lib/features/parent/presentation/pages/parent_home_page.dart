import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/session_required_view.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

enum ParentHomeSection { overview, assignments, payments, lessons }

class ParentHomePage extends StatelessWidget {
  const ParentHomePage({super.key, this.section = ParentHomeSection.overview});

  final ParentHomeSection section;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);
    if (userId == null) {
      // D-17: Sahte kullanici sentinel'i yok — oturum yoksa veri de yok.
      return const SessionRequiredView();
    }

    final fullName = context.select(
      (AuthCubit c) =>
          c.state.session?.fullName ??
          AppLocalizations.of(context).p20parent_role,
    );

    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..load(userId, fullName: fullName),
      child: Scaffold(
        backgroundColor: AppColors.background,
        bottomNavigationBar: ParentBottomNav(
          current: section == ParentHomeSection.overview
              ? ParentNavTab.home
              : ParentNavTab.none,
        ),
        body: SafeArea(
          child: Column(
            children: <Widget>[
              ParentHeader(
                title: AppLocalizations.of(
                  context,
                ).p20parent_greeting(fullName.split(' ').first),
                subtitle: AppLocalizations.of(context).p20parent_weeklyHint,
                // D-08 (M16): Öğretmenle sohbet. Veli sohbeti **başlatamaz** —
                // veli panelinde öğretmenin kullanıcı kimliği yok; öğretmen
                // yazınca sohbet burada görünür ve veli yanıtlar.
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    IconButton(
                      tooltip: AppLocalizations.of(context).p20parent_messages,
                      icon: const Icon(
                        Icons.chat_bubble_outline_rounded,
                        color: AppColors.primary,
                      ),
                      onPressed: () => context.push('/messages'),
                    ),
                    IconButton(
                      tooltip: AppLocalizations.of(
                        context,
                      ).p19list_notifications,
                      icon: const Icon(
                        Icons.notifications_outlined,
                        color: AppColors.primary,
                      ),
                      onPressed: () => context.push('/notifications'),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: BlocBuilder<ParentCubit, ParentState>(
                  builder: (context, state) {
                    if (state.isLoading) {
                      return LoadingStateView(
                        message: AppLocalizations.of(context).p20parent_loading,
                      );
                    }
                    if (state.status == ParentStatus.error) {
                      return Padding(
                        padding: const EdgeInsets.all(20),
                        child: ErrorStateView(
                          message:
                              state.errorMessage ??
                              AppLocalizations.of(context).p20parent_error,
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
                    return _HomeContent(
                      userId: userId,
                      fullName: fullName,
                      section: section,
                    );
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
            EmptyStateView(
              title: AppLocalizations.of(context).p20parent_empty,
              subtitle: AppLocalizations.of(context).p20parent_linkHint,
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onLink,
              icon: const Icon(Icons.person_add_alt_1_rounded),
              label: Text(AppLocalizations.of(context).p20parent_linkChild),
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
  const _HomeContent({
    required this.userId,
    required this.fullName,
    required this.section,
  });

  final ParentHomeSection section;

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
          if (children.isNotEmpty) ...<Widget>[
            _ChildSelector(
              children: children,
              selectedId: state.selectedStudentId,
              onSelect: (id) => cubit.selectChild(userId, id),
            ),
            const SizedBox(height: 14),
          ],
          if (state.dashboardLoading || dashboard == null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 40),
              child: LoadingStateView(
                message: AppLocalizations.of(context).p20parent_loadingProgress,
              ),
            )
          else if (section != ParentHomeSection.overview)
            _SectionSummary(dashboard: dashboard, section: section)
          else
            _DashboardBody(
              dashboard: dashboard,
              onDetail: () => context.push(
                '/parent/child-detail',
                extra: dashboard.studentId,
              ),
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
              value: AppLocalizations.of(
                context,
              ).p20parent_minutes(study.weeklyStudyMinutes),
              label: AppLocalizations.of(context).p20parent_weeklyStudy,
              accent: AppColors.primary,
            ),
            ParentStatTile(
              icon: Icons.local_fire_department_rounded,
              value: AppLocalizations.of(
                context,
              ).p20parent_days(study.streakDays),
              label: AppLocalizations.of(context).p20parent_streak,
              accent: AppColors.accentOrange,
            ),
            ParentStatTile(
              icon: Icons.menu_book_rounded,
              value: '${lessons.completedLessonCount}',
              label: AppLocalizations.of(context).p20parent_completedLessons,
              accent: AppColors.accentGreen,
            ),
            ParentStatTile(
              icon: Icons.assignment_late_rounded,
              value: '${assignments.openCount}',
              label: AppLocalizations.of(context).p20parent_openAssignments,
              accent: AppColors.accentRed,
            ),
          ],
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: AppLocalizations.of(context).p20parent_weekly,
          icon: Icons.bar_chart_rounded,
          child: study.hasData && study.weeklyBreakdownMinutes.isNotEmpty
              ? ParentWeeklyBars(minutesPerDay: study.weeklyBreakdownMinutes)
              : Padding(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  child: Text(
                    study.hasData
                        ? AppLocalizations.of(
                            context,
                          ).p20parent_studied(study.weeklyStudyMinutes)
                        : AppLocalizations.of(context).p20parent_notShared,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: AppLocalizations.of(context).p20parent_payments,
          icon: Icons.account_balance_wallet_rounded,
          child: Column(
            children: <Widget>[
              _MoneyRow(
                label: AppLocalizations.of(context).p20parent_expected,
                amount: payments.expectedTotal,
                currency: payments.currency,
                color: AppColors.textPrimary,
              ),
              _MoneyRow(
                label: AppLocalizations.of(context).p20parent_collected,
                amount: payments.collectedTotal,
                currency: payments.currency,
                color: AppColors.accentGreen,
              ),
              _MoneyRow(
                label: AppLocalizations.of(context).p20parent_outstanding,
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
            label: Text(AppLocalizations.of(context).p20parent_detail),
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
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
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

class _SectionSummary extends StatelessWidget {
  const _SectionSummary({required this.dashboard, required this.section});
  final ChildDashboard dashboard;
  final ParentHomeSection section;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final payments = dashboard.payments;
    final (title, icon, rows) = switch (section) {
      ParentHomeSection.payments => (
        l10n.p20parent_payments,
        Icons.account_balance_wallet_rounded,
        <Widget>[
          _MoneyRow(
            label: l10n.p20parent_expected,
            amount: payments.expectedTotal,
            currency: payments.currency,
            color: AppColors.textPrimary,
          ),
          _MoneyRow(
            label: l10n.p20parent_collected,
            amount: payments.collectedTotal,
            currency: payments.currency,
            color: AppColors.accentGreen,
          ),
          _MoneyRow(
            label: l10n.p20parent_outstanding,
            amount: payments.outstandingTotal,
            currency: payments.currency,
            color: AppColors.accentRed,
          ),
        ],
      ),
      ParentHomeSection.assignments => (
        l10n.p20parent_assignments,
        Icons.assignment_rounded,
        <Widget>[
          _CountRow(
            label: l10n.p20parent_totalAssignments,
            count: dashboard.assignments.totalCount,
          ),
          _CountRow(
            label: l10n.p20parent_completed,
            count: dashboard.assignments.completedCount,
          ),
          _CountRow(
            label: l10n.p20parent_openAssignments,
            count: dashboard.assignments.openCount,
          ),
        ],
      ),
      ParentHomeSection.lessons || ParentHomeSection.overview => (
        l10n.p20parent_lessons,
        Icons.menu_book_rounded,
        <Widget>[
          _CountRow(
            label: l10n.p20parent_completedLessons,
            count: dashboard.lessons.completedLessonCount,
          ),
          _CountRow(
            label: l10n.p20parent_plannedLessons,
            count: dashboard.lessons.plannedLessonCount,
          ),
        ],
      ),
    };
    return ParentCard(
      title: title,
      icon: icon,
      child: Column(children: rows),
    );
  }
}

class _CountRow extends StatelessWidget {
  const _CountRow({required this.label, required this.count});
  final String label;
  final int count;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Expanded(child: Text(label)),
        Text('$count'),
      ],
    ),
  );
}
