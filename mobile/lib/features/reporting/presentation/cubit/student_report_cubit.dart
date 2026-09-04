import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/reporting_status.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

export 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/reporting_status.dart';

/// M14-2/M14-3/M14-4: Öğrenci analiz + PDF rapor ekranının durumu.
///
/// Analiz serilerinin durumu ([status]) ile PDF üretiminin durumu ([reportStatus])
/// **ayrı** taşınır: rapor premium yüzünden kilitliyken grafikler görünmeye devam eder.
class StudentReportState {
  const StudentReportState({
    this.status = ReportingStatus.initial,
    this.reportStatus = ReportingStatus.initial,
    this.weeks = const <StudentStudyWeek>[],
    this.performance = const <StudentPerformanceMonth>[],
    this.isGenerating = false,
    this.lastReport,
    this.openedFilePath,
    this.errorMessage,
  });

  final ReportingStatus status;
  final ReportingStatus reportStatus;
  final List<StudentStudyWeek> weeks;
  final List<StudentPerformanceMonth> performance;
  final bool isGenerating;
  final GeneratedStudentReport? lastReport;
  final String? openedFilePath;
  final String? errorMessage;

  bool get isLoading => status == ReportingStatus.loading;

  /// Sunucu, öğrenci paylaşımı kapalıyken 403 değil **boş liste** döndürür;
  /// bu yüzden "veri yok" ile "paylaşım kapalı" istemcide ayırt edilemez.
  bool get isEmpty => weeks.isEmpty && performance.isEmpty;

  int get totalStudyMinutes =>
      weeks.fold(0, (sum, week) => sum + week.studyMinutes);

  StudentReportState copyWith({
    ReportingStatus? status,
    ReportingStatus? reportStatus,
    List<StudentStudyWeek>? weeks,
    List<StudentPerformanceMonth>? performance,
    bool? isGenerating,
    GeneratedStudentReport? lastReport,
    String? openedFilePath,
    String? errorMessage,
    bool clearError = false,
    bool clearOpenedFile = false,
  }) {
    return StudentReportState(
      status: status ?? this.status,
      reportStatus: reportStatus ?? this.reportStatus,
      weeks: weeks ?? this.weeks,
      performance: performance ?? this.performance,
      isGenerating: isGenerating ?? this.isGenerating,
      lastReport: lastReport ?? this.lastReport,
      openedFilePath:
          clearOpenedFile ? null : (openedFilePath ?? this.openedFilePath),
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class StudentReportCubit extends Cubit<StudentReportState> {
  StudentReportCubit(this._repository, this._fileOpener)
      : super(const StudentReportState());

  factory StudentReportCubit.create() => StudentReportCubit(
        injector<ReportingRepository>(),
        injector<ReportFileOpener>(),
      );

  final ReportingRepository _repository;
  final ReportFileOpener _fileOpener;

  String _studentId = '';

  Future<void> load(String studentId, {DateTime? from, DateTime? to}) async {
    _studentId = studentId;
    if (studentId.isEmpty) {
      return;
    }
    emit(state.copyWith(status: ReportingStatus.loading, clearError: true));

    try {
      final weeks = await _repository.studentStudyAnalysis(
        studentId: studentId,
        from: from,
        to: to,
      );
      final performance = await _repository.studentPerformance(
        studentId: studentId,
        from: from,
        to: to,
      );
      emit(state.copyWith(
        status: ReportingStatus.loaded,
        weeks: weeks,
        performance: performance,
        clearError: true,
      ));
    } on ApiException catch (error) {
      emit(state.copyWith(
        status: ReportingStatus.failure,
        errorMessage: error.message,
      ));
    } catch (_) {
      emit(state.copyWith(
        status: ReportingStatus.failure,
        errorMessage: 'Çalışma analizi yüklenemedi.',
      ));
    }
  }

  /// M14-4: Raporu üretir, dosyayı indirir ve cihazda açar.
  /// Üç adımın her biri ayrı ayrı başarısız olabilir; kısmi başarı korunur
  /// (ör. rapor üretildi ama açılamadı → kayıt durumda kalır, yol boş kalır).
  Future<void> generateAndOpenReport({String? note}) async {
    if (_studentId.isEmpty) {
      return;
    }
    emit(state.copyWith(
      isGenerating: true,
      reportStatus: ReportingStatus.loading,
      clearError: true,
      clearOpenedFile: true,
    ));

    GeneratedStudentReport report;
    try {
      report = await _repository.generateStudentReport(
        studentId: _studentId,
        note: note,
      );
    } on ApiException catch (error) {
      // 402 = üyelik yetersiz, 403 = paylaşım/yetki kapalı. İkisi de "kilitli"dir:
      // kullanıcıya hata değil, neden erişemediği gösterilir.
      final locked = error.statusCode == 402 || error.statusCode == 403;
      emit(state.copyWith(
        isGenerating: false,
        reportStatus:
            locked ? ReportingStatus.premiumRequired : ReportingStatus.failure,
        errorMessage: error.message,
      ));
      return;
    } catch (_) {
      emit(state.copyWith(
        isGenerating: false,
        reportStatus: ReportingStatus.failure,
        errorMessage: 'Rapor oluşturulamadı.',
      ));
      return;
    }

    emit(state.copyWith(lastReport: report));

    try {
      final bytes = await _repository.downloadReport(report.reportId);
      if (bytes == null) {
        emit(state.copyWith(
          isGenerating: false,
          reportStatus: ReportingStatus.failure,
          errorMessage: 'Rapor dosyası bulunamadı.',
        ));
        return;
      }

      final path = await _fileOpener.open(
        fileName: report.fileName,
        bytes: bytes,
      );
      emit(state.copyWith(
        isGenerating: false,
        reportStatus: ReportingStatus.loaded,
        openedFilePath: path,
        clearError: true,
      ));
    } on ApiException catch (error) {
      emit(state.copyWith(
        isGenerating: false,
        reportStatus: ReportingStatus.failure,
        errorMessage: error.message,
      ));
    } catch (error) {
      emit(state.copyWith(
        isGenerating: false,
        reportStatus: ReportingStatus.failure,
        errorMessage: 'Rapor açılamadı: $error',
      ));
    }
  }
}
