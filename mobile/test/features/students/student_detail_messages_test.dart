import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_scheduling_repository.dart';

void main() {
  for (final operation in <String>[
    'studentUpdated',
    'subjectAdded',
    'subjectRemoved',
    'parentLinkPending',
  ]) {
    test(
      '$operation UI tarafından çevrilecek anlam anahtarını taşır',
      () async {
        final repository = _Students();
        final cubit = StudentDetailCubit(
          studentRepository: repository,
          schedulingRepository: FakeSchedulingRepository(),
          paymentRepository: _Payments(),
        );
        await cubit.load(studentId: 'student', teacherUserId: 'teacher');
        switch (operation) {
          case 'studentUpdated':
            await cubit.updateStudent(repository.profile);
          case 'subjectAdded':
            await cubit.addSubject(subject: 'Matematik');
          case 'subjectRemoved':
            await cubit.removeSubject('subject');
          case 'parentLinkPending':
            await cubit.linkParent('parent');
        }
        expect(cubit.state.successMessage, 'studentDetail_$operation');
        await cubit.close();
      },
    );
  }
}

class _Students implements StudentRepository {
  final profile = const StudentProfile(
    id: 'student',
    fullName: 'Gerçek Öğrenci',
    gradeLevel: '10',
    origin: 'Manual',
    subjects: [],
  );
  @override
  Future<StudentProfile> getStudent(String studentId) async => profile;
  @override
  Future<StudentProfile> updateStudent(StudentProfile studentProfile) async =>
      studentProfile;
  @override
  Future<StudentSubjectTarget> addSubject({
    required String studentId,
    required String subject,
    String? targetLevel,
  }) async => StudentSubjectTarget(
    id: 'subject',
    subject: subject,
    targetLevel: targetLevel ?? '',
  );
  @override
  Future<void> removeSubject({
    required String studentId,
    required String subjectId,
  }) async {}
  @override
  Future<void> linkParent({
    required String studentId,
    required String parentUserId,
  }) async {}
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _Payments implements PaymentRepository {
  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async =>
      [];
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
