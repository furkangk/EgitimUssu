import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/student_report_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/widgets/report_charts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/session_required_view.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

class ParentChildDetailPage extends StatelessWidget {
  const ParentChildDetailPage({super.key, required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);
    if (userId == null) {
      // D-17: Sahte kullanici sentinel'i yok — oturum yoksa veri de yok.
      return const SessionRequiredView();
    }


    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..focusChild(userId, studentId),
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          backgroundColor: AppColors.surface,
          foregroundColor: AppColors.textPrimary,
          elevation: 0,
          title: const Text('Gelişim detayı'),
        ),
        body: SafeArea(
          child: BlocBuilder<ParentCubit, ParentState>(
            builder: (context, state) {
              if (state.isLoading || state.dashboardLoading) {
                return const LoadingStateView();
              }
              final dashboard = state.dashboard;
              if (dashboard == null) {
                return const EmptyStateView(
                  title: 'Veri yok',
                  subtitle:
                      'Bu çocuğun verilerine erişmek için bağın onaylı olması '
                      'gerekir.',
                );
              }
              return _DetailBody(
                dashboard: dashboard,
                studentId: studentId,
              );
            },
          ),
        ),
      ),
    );
  }
}

class _DetailBody extends StatelessWidget {
  const _DetailBody({required this.dashboard, required this.studentId});

  final ChildDashboard dashboard;
  final String studentId;

  @override
  Widget build(BuildContext context) {
    final study = dashboard.study;
    final lessons = dashboard.lessons;
    final assignments = dashboard.assignments;
    final payments = dashboard.payments;
    final dateFormat = DateFormat('d MMM yyyy', 'tr_TR');

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: <Widget>[
        Text(
          dashboard.childDisplayName ?? 'Öğrenci',
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.w800,
            color: AppColors.textPrimary,
          ),
        ),
        const SizedBox(height: 16),
        ParentCard(
          title: 'Bireysel çalışma',
          icon: Icons.timer_rounded,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: _InlineMetric(
                      value: formatMinutes(study.weeklyStudyMinutes),
                      label: 'Bu hafta',
                    ),
                  ),
                  Expanded(
                    child: _InlineMetric(
                      value: '${study.streakDays} gün',
                      label: 'Seri',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (study.hasData && study.weeklyBreakdownMinutes.isNotEmpty)
                ParentWeeklyBars(minutesPerDay: study.weeklyBreakdownMinutes)
              else
                Text(
                  'Bireysel çalışma verisi henüz paylaşılmadı.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _ChildProgressSection(studentId: studentId),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Dersler',
          icon: Icons.menu_book_rounded,
          iconColor: AppColors.accentGreen,
          child: Column(
            children: <Widget>[
              _DetailRow(
                label: 'Tamamlanan ders',
                value: '${lessons.completedLessonCount}',
              ),
              _DetailRow(
                label: 'Planlanan ders',
                value: '${lessons.plannedLessonCount}',
              ),
              _DetailRow(
                label: 'Son ders',
                value: lessons.lastLessonCompletedAtUtc != null
                    ? dateFormat.format(
                        lessons.lastLessonCompletedAtUtc!.toLocal(),
                      )
                    : '—',
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Ödevler',
          icon: Icons.assignment_rounded,
          iconColor: AppColors.accentOrange,
          child: Column(
            children: <Widget>[
              _DetailRow(
                label: 'Toplam ödev',
                value: '${assignments.totalCount}',
              ),
              _DetailRow(
                label: 'Tamamlanan',
                value: '${assignments.completedCount}',
              ),
              _DetailRow(
                label: 'Açık ödev',
                value: '${assignments.openCount}',
                valueColor: assignments.openCount > 0
                    ? AppColors.accentRed
                    : AppColors.accentGreen,
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Ödemeler',
          icon: Icons.account_balance_wallet_rounded,
          child: Column(
            children: <Widget>[
              _DetailRow(
                label: 'Beklenen',
                value: '${payments.expectedTotal.toStringAsFixed(0)} ${payments.currency}',
              ),
              _DetailRow(
                label: 'Tahsil edilen',
                value: '${payments.collectedTotal.toStringAsFixed(0)} ${payments.currency}',
                valueColor: AppColors.accentGreen,
              ),
              _DetailRow(
                label: 'Kalan',
                value: '${payments.outstandingTotal.toStringAsFixed(0)} ${payments.currency}',
                valueColor: payments.outstandingTotal > 0
                    ? AppColors.accentRed
                    : AppColors.textSecondary,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _InlineMetric extends StatelessWidget {
  const _InlineMetric({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          value,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.w800,
            color: AppColors.primary,
          ),
        ),
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({
    required this.label,
    required this.value,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
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
            value,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w800,
              color: valueColor ?? AppColors.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}

/// M10/M14 (P08): Velinin çocuğuna ait gelişim grafiği.
///
/// Veriler `/api/reporting` uçlarından gelir; yetki **onaylı veli bağı**yla,
/// gizlilik ise öğrencinin paylaşım ayarıyla sunucuda uygulanır. Paylaşım
/// kapalıysa sunucu 403 değil **boş seri** döner — bu yüzden boş durum metni
/// iki olasılığı birden söyler (istemci ikisini ayırt edemez).
class _ChildProgressSection extends StatelessWidget {
  const _ChildProgressSection({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return BlocProvider<StudentReportCubit>(
      create: (_) => StudentReportCubit.create()..load(studentId),
      child: BlocBuilder<StudentReportCubit, StudentReportState>(
        builder: (context, state) {
          if (state.isLoading || state.status == ReportingStatus.initial) {
            return const ParentCard(
              title: 'Gelişim',
              icon: Icons.insights_rounded,
              iconColor: AppColors.accentTeal,
              child: Center(
                child: Padding(
                  padding: EdgeInsets.symmetric(vertical: 18),
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            );
          }

          if (state.status == ReportingStatus.failure || state.isEmpty) {
            return ParentCard(
              title: 'Gelişim',
              icon: Icons.insights_rounded,
              iconColor: AppColors.accentTeal,
              child: Text(
                state.status == ReportingStatus.failure
                    ? (state.errorMessage ?? 'Gelişim verisi alınamadı.')
                    : 'Gelişim verisi henüz yok. Öğrenci veli paylaşımını '
                        'kapatmış da olabilir.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
            );
          }

          return Column(
            children: <Widget>[
              if (state.weeks.isNotEmpty)
                ParentCard(
                  title: 'Haftalık çalışma',
                  icon: Icons.timeline_rounded,
                  iconColor: AppColors.accentTeal,
                  child: WeeklyStudyBarChart(weeks: state.weeks),
                ),
              if (state.performance.isNotEmpty) ...<Widget>[
                const SizedBox(height: 14),
                ParentCard(
                  title: 'Ders bazlı net',
                  icon: Icons.school_rounded,
                  iconColor: AppColors.accentBlue,
                  child: SubjectNetChart(performance: state.performance),
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}
