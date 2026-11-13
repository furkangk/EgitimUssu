import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/reporting_status.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

export 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/reporting_status.dart';

/// M14-1 + M14-5: Öğretmen raporları ekranının durumu.
class TeacherReportsState {
  const TeacherReportsState({
    this.status = ReportingStatus.initial,
    this.monthly,
    this.freeSlots = const <FreeSlot>[],
    this.year = 0,
    this.month = 0,
    this.errorMessage,
  });

  final ReportingStatus status;
  final TeacherMonthlyReport? monthly;
  final List<FreeSlot> freeSlots;
  final int year;
  final int month;
  final String? errorMessage;

  bool get isLoading => status == ReportingStatus.loading;

  TeacherReportsState copyWith({
    ReportingStatus? status,
    TeacherMonthlyReport? monthly,
    List<FreeSlot>? freeSlots,
    int? year,
    int? month,
    String? errorMessage,
    bool clearError = false,
  }) {
    return TeacherReportsState(
      status: status ?? this.status,
      monthly: monthly ?? this.monthly,
      freeSlots: freeSlots ?? this.freeSlots,
      year: year ?? this.year,
      month: month ?? this.month,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class TeacherReportsCubit extends Cubit<TeacherReportsState> {
  TeacherReportsCubit(this._repository) : super(const TeacherReportsState());

  factory TeacherReportsCubit.create() =>
      TeacherReportsCubit(injector<ReportingRepository>());

  final ReportingRepository _repository;

  String? _teacherUserId;

  Future<void> load(String teacherUserId, {int? year, int? month}) {
    _teacherUserId = teacherUserId;
    final now = DateTime.now();
    return _fetch(year ?? now.year, month ?? now.month);
  }

  Future<void> showPreviousMonth() => _fetch(
        state.month == 1 ? state.year - 1 : state.year,
        state.month == 1 ? 12 : state.month - 1,
      );

  Future<void> showNextMonth() => _fetch(
        state.month == 12 ? state.year + 1 : state.year,
        state.month == 12 ? 1 : state.month + 1,
      );

  Future<void> refresh() => _fetch(state.year, state.month);

  Future<void> _fetch(int year, int month) async {
    final teacherUserId = _teacherUserId;
    if (teacherUserId == null) {
      return;
    }
    emit(state.copyWith(
      status: ReportingStatus.loading,
      year: year,
      month: month,
      clearError: true,
    ));

    try {
      final monthly = await _repository.teacherMonthly(
        teacherUserId: teacherUserId,
        year: year,
        month: month,
      );
      // Boş zaman analizi hep "bugünden itibaren bir hafta"dır: geçmiş bir ayın
      // boş saatlerini göstermek anlamsız olurdu (planlanamaz).
      final from = DateTime.now();
      final slots = await _repository.teacherFreeSlots(
        teacherUserId: teacherUserId,
        from: from,
        to: from.add(const Duration(days: 7)),
      );

      emit(state.copyWith(
        status: ReportingStatus.loaded,
        monthly: monthly,
        freeSlots: slots,
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
        errorMessage: 'Rapor yüklenemedi.',
      ));
    }
  }
}
