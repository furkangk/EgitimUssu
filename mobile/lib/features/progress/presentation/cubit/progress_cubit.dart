import 'package:egitim_ussu_mobile/core/bloc/safe_emit.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum ProgressStatus { initial, loading, loaded, failure }

/// Aynı ekran gövdesini üç farklı kimlikle besleyen kapsam.
/// Sunucudaki üç ayrı uca (öğrenci / öğretmen / veli) karşılık gelir.
enum ProgressScope { self, teacher, parent }

/// M10: Gelişim ekranının durumu.
///
/// `snapshots` **yalnız** [ProgressScope.self] kapsamında dolar: snapshot ucu
/// öğrencinin kendisine ve Admin'e açıktır. Öğretmen/veli kapsamında seri boş
/// kalır ve ekran çizgi grafiğini gizler — boş grafik çizmek yalancı-yeşil olurdu.
class ProgressState {
  const ProgressState({
    this.status = ProgressStatus.initial,
    this.scope = ProgressScope.self,
    this.overview,
    this.mastery = const <TopicMastery>[],
    this.snapshots = const <ProgressSnapshot>[],
    this.errorMessage,
  });

  final ProgressStatus status;
  final ProgressScope scope;
  final ProgressOverview? overview;
  final List<TopicMastery> mastery;
  final List<ProgressSnapshot> snapshots;
  final String? errorMessage;

  bool get isLoading => status == ProgressStatus.loading;

  /// Hiç izlenen konu ve hiç seri yoksa "veri yok". Veli/öğretmen kapsamında bu
  /// aynı zamanda "öğrenci paylaşımı kapatmış" anlamına gelebilir — sunucu iki
  /// durumu ayırt edilebilir biçimde döndürmüyor (boş özet).
  bool get isEmpty {
    final o = overview;
    if (o == null) return true;
    return o.trackedTopics == 0 &&
        o.notStartedCount == 0 &&
        mastery.isEmpty &&
        snapshots.isEmpty;
  }

  /// Çizgi grafiği için ders bazında, döneme göre artan sıralı seriler.
  Map<String, List<ProgressSnapshot>> get snapshotsBySubject {
    final grouped = <String, List<ProgressSnapshot>>{};
    for (final snapshot in snapshots) {
      grouped.putIfAbsent(snapshot.subject, () => <ProgressSnapshot>[]).add(snapshot);
    }
    for (final series in grouped.values) {
      series.sort((a, b) => a.periodStart.compareTo(b.periodStart));
    }
    return grouped;
  }

  ProgressState copyWith({
    ProgressStatus? status,
    ProgressScope? scope,
    ProgressOverview? overview,
    List<TopicMastery>? mastery,
    List<ProgressSnapshot>? snapshots,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ProgressState(
      status: status ?? this.status,
      scope: scope ?? this.scope,
      overview: overview ?? this.overview,
      mastery: mastery ?? this.mastery,
      snapshots: snapshots ?? this.snapshots,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// M10-1/M10-2: Gelişim ekranını besleyen cubit. Kapsam yükleme anında seçilir;
/// `refresh()` son kapsamı tekrarlar.
class ProgressCubit extends Cubit<ProgressState> with SafeEmit<ProgressState> {
  ProgressCubit(this._repository) : super(const ProgressState());

  factory ProgressCubit.create() => ProgressCubit(injector<ProgressRepository>());

  final ProgressRepository _repository;

  ProgressScope _scope = ProgressScope.self;
  String? _studentId;
  String? _viewerUserId;

  Future<void> loadSelf(String studentId) {
    _scope = ProgressScope.self;
    _studentId = studentId;
    _viewerUserId = null;
    return _load();
  }

  Future<void> loadForTeacher({
    required String teacherUserId,
    required String studentId,
  }) {
    _scope = ProgressScope.teacher;
    _studentId = studentId;
    _viewerUserId = teacherUserId;
    return _load();
  }

  Future<void> loadForParent({
    required String parentUserId,
    required String studentId,
  }) {
    _scope = ProgressScope.parent;
    _studentId = studentId;
    _viewerUserId = parentUserId;
    return _load();
  }

  Future<void> refresh() => _studentId == null ? Future<void>.value() : _load();

  Future<void> _load() async {
    final studentId = _studentId;
    if (studentId == null || studentId.isEmpty) {
      return;
    }
    safeEmit(state.copyWith(
      status: ProgressStatus.loading,
      scope: _scope,
      clearError: true,
    ));

    try {
      final overview = switch (_scope) {
        ProgressScope.self => await _repository.getOverview(studentId),
        ProgressScope.teacher => await _repository.getTeacherStudentOverview(
            teacherUserId: _viewerUserId!,
            studentId: studentId,
          ),
        ProgressScope.parent => await _repository.getParentChildOverview(
            parentUserId: _viewerUserId!,
            studentId: studentId,
          ),
      };

      if (isClosed) return;
      // Konu listesi ve snapshot serisi öğrenciye özel uçlardır; öğretmen/veli
      // kapsamında çağrılırsa 403 döner — özet zaten gizlilik filtresinden geçmiş.
      final mastery = _scope == ProgressScope.self
          ? await _repository.listMastery(studentId)
          : const <TopicMastery>[];
      if (isClosed) return;
      final snapshots = _scope == ProgressScope.self
          ? await _repository.listSnapshots(studentId)
          : const <ProgressSnapshot>[];

      safeEmit(state.copyWith(
        status: ProgressStatus.loaded,
        overview: overview,
        mastery: mastery,
        snapshots: snapshots,
        clearError: true,
      ));
    } on ApiException catch (error) {
      safeEmit(state.copyWith(
        status: ProgressStatus.failure,
        errorMessage: error.message,
      ));
    } catch (_) {
      safeEmit(state.copyWith(
        status: ProgressStatus.failure,
        errorMessage: 'Gelişim verisi yüklenemedi.',
      ));
    }
  }
}
