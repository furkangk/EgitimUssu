import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/student_report_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/widgets/report_charts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// M14-2 / M14-3 / M14-4: Öğrencinin çalışma ve performans analizi + PDF rapor.
///
/// Ekran hem öğrencinin kendisi hem bağlı öğretmen/onaylı veli tarafından açılır;
/// yetki ve gizlilik **sunucuda** uygulanır. Paylaşım kapalıysa seriler boş gelir
/// (403 değil) — bu yüzden boş durum metni iki olasılığı da söyler.
class StudentAnalysisPage extends StatelessWidget {
  const StudentAnalysisPage({
    super.key,
    required this.studentId,
    this.title = 'Çalışma analizi',
  });

  final String studentId;
  final String title;

  @override
  Widget build(BuildContext context) {
    if (studentId.isEmpty) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: Text(title)),
        body: const EmptyStateView(
          title: 'Öğrenci seçilmedi',
          subtitle: 'Analiz için bir öğrenci kimliği gerekiyor.',
        ),
      );
    }

    return BlocProvider<StudentReportCubit>(
      create: (_) => StudentReportCubit.create()..load(studentId),
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: Text(title)),
        body: SafeArea(
          child: BlocConsumer<StudentReportCubit, StudentReportState>(
            listenWhen: (previous, current) =>
                previous.reportStatus != current.reportStatus,
            listener: (context, state) {
              final messenger = ScaffoldMessenger.of(context);
              if (state.reportStatus == ReportingStatus.loaded &&
                  state.openedFilePath != null) {
                messenger.showSnackBar(
                  SnackBar(content: Text('Rapor indirildi: ${state.lastReport?.fileName}')),
                );
              } else if (state.reportStatus == ReportingStatus.failure) {
                messenger.showSnackBar(
                  SnackBar(
                    content: Text(state.errorMessage ?? 'Rapor oluşturulamadı.'),
                    backgroundColor: AppColors.accentRed,
                  ),
                );
              }
            },
            builder: (context, state) {
              final cubit = context.read<StudentReportCubit>();
              if (state.isLoading) {
                return const LoadingStateView(message: 'Analiz hazırlanıyor...');
              }
              if (state.status == ReportingStatus.failure) {
                return ErrorStateView(
                  message: state.errorMessage ?? 'Analiz yüklenemedi.',
                  onRetry: () => cubit.load(studentId),
                );
              }
              return RefreshIndicator(
                onRefresh: () => cubit.load(studentId),
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
                  children: <Widget>[
                    if (state.isEmpty)
                      const EmptyStateView(
                        title: 'Analiz verisi yok',
                        subtitle:
                            'Bu dönem için çalışma kaydı bulunmuyor. Öğrenci '
                            'çalışma verisi paylaşımını kapatmış da olabilir.',
                      )
                    else ...<Widget>[
                      ReportCard(
                        title: 'Haftalık çalışma',
                        subtitle:
                            'Toplam ${_duration(state.totalStudyMinutes)} · '
                            '${state.weeks.length} hafta',
                        child: WeeklyStudyBarChart(weeks: state.weeks),
                      ),
                      if (state.performance.isNotEmpty)
                        ReportCard(
                          title: 'Ders bazlı net',
                          subtitle: 'En güncel ayın ortalama net oranı',
                          child: SubjectNetChart(performance: state.performance),
                        ),
                    ],
                    _ReportActionCard(state: state),
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

/// M14-4: PDF üretimi. Premium kilidi sunucudan gelir (`402`/`403`) ve burada
/// hata balonu değil **kilit kartı** olarak gösterilir.
class _ReportActionCard extends StatelessWidget {
  const _ReportActionCard({required this.state});

  final StudentReportState state;

  @override
  Widget build(BuildContext context) {
    final cubit = context.read<StudentReportCubit>();
    final locked = state.reportStatus == ReportingStatus.premiumRequired;

    return ReportCard(
      title: 'PDF gelişim raporu',
      subtitle: locked
          ? 'Premium üyelik gerektirir'
          : 'Son bir ayın özetini PDF olarak indir',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (locked) ...<Widget>[
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.amber.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Row(
                children: <Widget>[
                  const Icon(
                    Icons.workspace_premium_outlined,
                    color: AppColors.amber,
                    size: 20,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      state.errorMessage ??
                          'PDF rapor premium üyelik gerektirir.',
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
          ],
          FilledButton.icon(
            onPressed: state.isGenerating ? null : cubit.generateAndOpenReport,
            icon: state.isGenerating
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.picture_as_pdf_outlined),
            label: Text(
              state.isGenerating ? 'Rapor hazırlanıyor...' : 'Rapor oluştur',
            ),
          ),
          if (state.lastReport != null && !state.isGenerating) ...<Widget>[
            const SizedBox(height: 8),
            Text(
              'Son rapor: ${state.lastReport!.fileName}'
              ' (${(state.lastReport!.sizeBytes / 1024).round()} KB)',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textMuted,
                fontSize: 11,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

String _duration(int minutes) {
  final hours = minutes ~/ 60;
  final rest = minutes % 60;
  if (hours == 0) return '$rest dk';
  if (rest == 0) return '$hours sa';
  return '$hours sa $rest dk';
}
