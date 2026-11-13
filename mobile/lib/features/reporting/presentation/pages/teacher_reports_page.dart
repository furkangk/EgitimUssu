import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/teacher_reports_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/widgets/report_charts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/session_required_view.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

/// M14-1 + M14-5: Öğretmen raporları — aylık ders/tahsilat özeti + boş zaman analizi.
///
/// Sayılar `/api/reporting` projeksiyonlarından gelir; hiçbir değer istemcide
/// uydurulmaz. Projeksiyon henüz beslenmemişse "veri yok" gösterilir.
class TeacherReportsPage extends StatelessWidget {
  const TeacherReportsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);
    if (userId == null) {
      return const SessionRequiredView();
    }

    return BlocProvider<TeacherReportsCubit>(
      create: (_) => TeacherReportsCubit.create()..load(userId),
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: const Text('Raporlar')),
        body: SafeArea(
          child: BlocBuilder<TeacherReportsCubit, TeacherReportsState>(
            builder: (context, state) {
              final cubit = context.read<TeacherReportsCubit>();
              if (state.isLoading && state.monthly == null) {
                return const LoadingStateView(message: 'Rapor hazırlanıyor...');
              }
              if (state.status == ReportingStatus.failure) {
                return ErrorStateView(
                  message: state.errorMessage ?? 'Rapor yüklenemedi.',
                  onRetry: cubit.refresh,
                );
              }
              return RefreshIndicator(
                onRefresh: cubit.refresh,
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
                  children: <Widget>[
                    _MonthSwitcher(state: state),
                    const SizedBox(height: 14),
                    _MonthlySummary(monthly: state.monthly),
                    _FreeSlots(slots: state.freeSlots),
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

class _MonthSwitcher extends StatelessWidget {
  const _MonthSwitcher({required this.state});

  final TeacherReportsState state;

  @override
  Widget build(BuildContext context) {
    final cubit = context.read<TeacherReportsCubit>();
    final label = state.year == 0
        ? '—'
        : DateFormat('MMMM yyyy', 'tr_TR')
            .format(DateTime(state.year, state.month));
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        IconButton(
          onPressed: state.isLoading ? null : cubit.showPreviousMonth,
          icon: const Icon(Icons.chevron_left_rounded),
          color: AppColors.primary,
          tooltip: 'Önceki ay',
        ),
        Text(
          label,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
        ),
        IconButton(
          onPressed: state.isLoading ? null : cubit.showNextMonth,
          icon: const Icon(Icons.chevron_right_rounded),
          color: AppColors.primary,
          tooltip: 'Sonraki ay',
        ),
      ],
    );
  }
}

class _MonthlySummary extends StatelessWidget {
  const _MonthlySummary({required this.monthly});

  final TeacherMonthlyReport? monthly;

  @override
  Widget build(BuildContext context) {
    final report = monthly;
    if (report == null || report.isEmpty) {
      return const ReportCard(
        title: 'Aylık özet',
        subtitle: 'Bu ay için henüz kayıt yok',
        child: Text(
          'Ders tamamlandıkça ve ödeme alındıkça bu ayın özeti burada oluşur.',
          style: TextStyle(color: AppColors.textSecondary, fontSize: 13),
        ),
      );
    }

    return Column(
      children: <Widget>[
        ReportCard(
          title: 'Ders özeti',
          subtitle: '${report.activeStudentCount} aktif öğrenci',
          child: Row(
            children: <Widget>[
              _Metric(
                label: 'Tamamlanan',
                value: '${report.completedLessonCount}',
                color: AppColors.accentGreen,
                icon: Icons.check_circle_outline_rounded,
              ),
              _Metric(
                label: 'İptal',
                value: '${report.cancelledLessonCount}',
                color: AppColors.accentRed,
                icon: Icons.cancel_outlined,
              ),
              _Metric(
                label: 'Öğrenci',
                value: '${report.activeStudentCount}',
                color: AppColors.accentBlue,
                icon: Icons.people_alt_outlined,
              ),
            ],
          ),
        ),
        ReportCard(
          title: 'Tahsilat',
          subtitle: 'Beklenen ${_money(report.expectedAmount)} ₺',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      '${_money(report.collectedAmount)} ₺ tahsil edildi',
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  Text(
                    '%${(report.collectionRatio * 100).round()}',
                    style: const TextStyle(
                      color: AppColors.accentGreen,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              TweenAnimationBuilder<double>(
                tween: Tween<double>(begin: 0, end: report.collectionRatio),
                duration: const Duration(milliseconds: 250),
                curve: Curves.easeOut,
                builder: (context, value, _) => ClipRRect(
                  borderRadius: BorderRadius.circular(999),
                  child: LinearProgressIndicator(
                    value: value,
                    minHeight: 8,
                    backgroundColor: AppColors.divider,
                    valueColor: const AlwaysStoppedAnimation<Color>(
                      AppColors.accentGreen,
                    ),
                  ),
                ),
              ),
              if (report.outstandingAmount > 0) ...<Widget>[
                const SizedBox(height: 8),
                Text(
                  'Kalan: ${_money(report.outstandingAmount)} ₺',
                  style: const TextStyle(
                    color: AppColors.accentOrange,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _FreeSlots extends StatelessWidget {
  const _FreeSlots({required this.slots});

  final List<FreeSlot> slots;

  @override
  Widget build(BuildContext context) {
    if (slots.isEmpty) {
      return const ReportCard(
        title: 'Boş zamanların',
        subtitle: 'Önümüzdeki 7 gün',
        child: Text(
          'Uygunluk takviminde ders planlanmamış aralık kalmadı. '
          'Yeni saat açmak için müsaitlik ayarlarını güncelleyebilirsin.',
          style: TextStyle(color: AppColors.textSecondary, fontSize: 13),
        ),
      );
    }

    final dayFormat = DateFormat('d MMM EEEE', 'tr_TR');
    final timeFormat = DateFormat('HH:mm');
    final totalMinutes =
        slots.fold<int>(0, (sum, slot) => sum + slot.durationMinutes);

    return ReportCard(
      title: 'Boş zamanların',
      subtitle: 'Önümüzdeki 7 gün · toplam ${_duration(totalMinutes)}',
      child: Column(
        children: <Widget>[
          for (final slot in slots.take(20))
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: <Widget>[
                  Container(
                    width: 4,
                    height: 34,
                    decoration: BoxDecoration(
                      color: AppColors.accentTeal,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          dayFormat.format(slot.startAtUtc.toLocal()),
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontWeight: FontWeight.w600,
                            fontSize: 13,
                          ),
                        ),
                        Text(
                          '${timeFormat.format(slot.startAtUtc.toLocal())}'
                          ' – ${timeFormat.format(slot.endAtUtc.toLocal())}',
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Text(
                    _duration(slot.durationMinutes),
                    style: const TextStyle(
                      color: AppColors.accentTeal,
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
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

class _Metric extends StatelessWidget {
  const _Metric({
    required this.label,
    required this.value,
    required this.color,
    required this.icon,
  });

  final String label;
  final String value;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: <Widget>[
          Icon(icon, color: color, size: 20),
          const SizedBox(height: 6),
          Text(
            value,
            style: TextStyle(
              color: color,
              fontWeight: FontWeight.w800,
              fontSize: 20,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 11,
            ),
          ),
        ],
      ),
    );
  }
}

String _money(double amount) =>
    NumberFormat.decimalPattern('tr_TR').format(amount.round());

String _duration(int minutes) {
  final hours = minutes ~/ 60;
  final rest = minutes % 60;
  if (hours == 0) return '$rest dk';
  if (rest == 0) return '$hours sa';
  return '$hours sa $rest dk';
}
