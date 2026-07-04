import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudyHomeCubit extends Cubit<StudyHomeState> {
  StudyHomeCubit({
    required StudyRepository studyRepository,
    required StudentRepository studentRepository,
  })  : _studyRepository = studyRepository,
        _studentRepository = studentRepository,
        super(const StudyHomeState());

  final StudyRepository _studyRepository;
  final StudentRepository _studentRepository;

  factory StudyHomeCubit.create() => StudyHomeCubit(
        studyRepository: injector<StudyRepository>(),
        studentRepository: injector<StudentRepository>(),
      );

  /// Oturum açan öğrencinin StudentId'sini çözer (yoksa self-register profili
  /// oluşturur) ve çalışma panosunu yükler.
  Future<void> load({required String userId, required String fullName}) async {
    if (isClosed) return;
    emit(state.copyWith(status: StudyHomeStatus.loading, clearError: true));
    try {
      var studentId = state.studentId;
      studentId ??= await _resolveStudentId(userId, fullName);
      final dashboard = await _studyRepository.getDashboard(studentId);
      if (isClosed) return;
      emit(state.copyWith(
        status: StudyHomeStatus.ready,
        studentId: studentId,
        dashboard: dashboard,
        clearError: true,
      ));
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(
        status: StudyHomeStatus.error,
        errorMessage: error.message,
      ));
    }
  }

  Future<void> refresh({required String userId, required String fullName}) =>
      load(userId: userId, fullName: fullName);

  Future<String> _resolveStudentId(String userId, String fullName) async {
    final existing = await _studentRepository.getByUser(userId);
    if (existing != null) {
      return existing.id;
    }
    final created = await _studentRepository.createSelfProfile(
      userId: userId,
      fullName: fullName.isEmpty ? 'Öğrenci' : fullName,
      gradeLevel: 'Belirtilmedi',
    );
    return created.id;
  }
}
