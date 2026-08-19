import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_timer_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudyTimerCubit extends Cubit<StudyTimerState> {
  StudyTimerCubit({required StudyRepository studyRepository})
      : _studyRepository = studyRepository,
        super(const StudyTimerState());

  final StudyRepository _studyRepository;
  Timer? _ticker;

  factory StudyTimerCubit.create() =>
      StudyTimerCubit(studyRepository: injector<StudyRepository>());

  /// Sayfa açılışında devam eden (Running/Paused) seansı geri yükler.
  Future<void> restore(String studentId) async {
    try {
      final sessions = await _studyRepository.listSessions(studentId);
      final active = sessions.where((s) => s.isActive).toList();
      if (active.isEmpty || isClosed) return;
      final session = active.first;
      final elapsed = session.effectiveMinutes * 60 +
          (session.isRunning
              ? DateTime.now().toUtc().difference(session.startedAtUtc).inSeconds
              : 0);
      emit(state.copyWith(
        session: session,
        elapsedSeconds: elapsed.clamp(0, 1 << 30),
        breakSeconds: session.breakMinutes * 60,
      ));
      if (session.isActive) _startTicker();
    } on ApiException {
      // sessizce geç — kullanıcı yeni seans başlatabilir
    }
  }

  Future<void> start(
    String studentId,
    String subject,
    String? topic, {
    String? lessonId,
    String? subjectId,
    String? topicId,
    int? targetMinutes,
  }) async {
    if (state.isBusy) return;
    emit(state.copyWith(isBusy: true, clearError: true, clearSummary: true));
    try {
      final session = await _studyRepository.startSession(
        studentId,
        subject: subject,
        topic: topic,
        lessonId: lessonId,
        subjectId: subjectId,
        topicId: topicId,
      );
      emit(state.copyWith(
        session: session,
        isBusy: false,
        elapsedSeconds: 0,
        breakSeconds: 0,
        breakCount: 0,
        targetMinutes: targetMinutes,
        clearTargetMinutes: targetMinutes == null,
      ));
      _startTicker();
    } on ApiException catch (e) {
      emit(state.copyWith(isBusy: false, errorMessage: e.message));
    }
  }

  Future<void> pause() async {
    final id = state.session?.id;
    if (id == null || state.isBusy) return;
    // Molayı hemen say — ticker durum değişince mola süresini işlemeye başlar.
    emit(state.copyWith(
      isBusy: true,
      clearError: true,
      breakCount: state.breakCount + 1,
    ));
    try {
      final session = await _studyRepository.pauseSession(id);
      emit(state.copyWith(session: session, isBusy: false));
    } on ApiException catch (e) {
      // Başarısızsa mola sayısını geri al.
      emit(state.copyWith(
        isBusy: false,
        errorMessage: e.message,
        breakCount: state.breakCount - 1,
      ));
    }
  }

  Future<void> resume() async {
    final id = state.session?.id;
    if (id == null || state.isBusy) return;
    emit(state.copyWith(isBusy: true, clearError: true));
    try {
      final session = await _studyRepository.resumeSession(id);
      emit(state.copyWith(session: session, isBusy: false));
      _startTicker();
    } on ApiException catch (e) {
      emit(state.copyWith(isBusy: false, errorMessage: e.message));
    }
  }

  Future<void> complete({String? note}) async {
    final id = state.session?.id;
    if (id == null || state.isBusy) return;
    emit(state.copyWith(isBusy: true, clearError: true));
    try {
      _stopTicker();
      final summary = await _studyRepository.completeSession(id, personalNote: note);
      // Özet, kullanıcının kronometrede gördüğü canlı (saniye hassas) net/mola
      // sürelerini gösterir; elapsedSeconds/breakSeconds bilerek sıfırlanmaz,
      // "Kapat" (acknowledgeSummary) ile temizlenir.
      emit(state.copyWith(
        isBusy: false,
        clearSession: true,
        completedSummary: summary,
      ));
    } on ApiException catch (e) {
      emit(state.copyWith(isBusy: false, errorMessage: e.message));
    }
  }

  Future<void> discard() async {
    final id = state.session?.id;
    if (id == null || state.isBusy) return;
    emit(state.copyWith(isBusy: true, clearError: true));
    try {
      _stopTicker();
      await _studyRepository.discardSession(id);
      emit(state.copyWith(
        isBusy: false,
        clearSession: true,
        elapsedSeconds: 0,
        breakSeconds: 0,
        breakCount: 0,
        clearTargetMinutes: true,
      ));
    } on ApiException catch (e) {
      emit(state.copyWith(isBusy: false, errorMessage: e.message));
    }
  }

  void acknowledgeSummary() => emit(state.copyWith(
        clearSummary: true,
        elapsedSeconds: 0,
        breakSeconds: 0,
        breakCount: 0,
        clearTargetMinutes: true,
      ));

  void _startTicker() {
    _stopTicker();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (isClosed) return;
      // Çalışırken net süre, molada mola süresi artar; ikisi ayrı izlenir.
      if (state.isRunning) {
        emit(state.copyWith(elapsedSeconds: state.elapsedSeconds + 1));
      } else if (state.session?.isPaused ?? false) {
        emit(state.copyWith(breakSeconds: state.breakSeconds + 1));
      }
    });
  }

  void _stopTicker() {
    _ticker?.cancel();
    _ticker = null;
  }

  @override
  Future<void> close() {
    _stopTicker();
    return super.close();
  }
}
