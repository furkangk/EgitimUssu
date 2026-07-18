import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';

/// Öğrenci sekmeleri bottom nav'dan `studentId` almadan açıldığı için, oturum
/// açan kullanıcının StudentId'sini çözen ortak yardımcı. Profil yoksa
/// self-register profili oluşturur (StudyHomeCubit ile aynı davranış).
class StudentScope {
  const StudentScope._();

  static Future<String> resolve({
    required String userId,
    required String fullName,
  }) async {
    final repo = injector<StudentRepository>();
    final existing = await repo.getByUser(userId);
    if (existing != null) return existing.id;
    final created = await repo.createSelfProfile(
      userId: userId,
      fullName: fullName.trim().isEmpty ? 'Öğrenci' : fullName.trim(),
      gradeLevel: 'Belirtilmedi',
    );
    return created.id;
  }
}
