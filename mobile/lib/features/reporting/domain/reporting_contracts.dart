import 'dart:typed_data';

/// M14 Raporlama (Reporting) — mobil domain sözleşmeleri.
/// Backend: `/api/reporting`. Sayılar event-beslemeli projeksiyonlardan gelir.

/// M14-1: Öğretmenin bir ayının ders ve tahsilat özeti.
class TeacherMonthlyReport {
  const TeacherMonthlyReport({
    required this.teacherUserId,
    required this.year,
    required this.month,
    required this.completedLessonCount,
    required this.cancelledLessonCount,
    required this.activeStudentCount,
    required this.expectedAmount,
    required this.collectedAmount,
    this.updatedOnUtc,
  });

  final String teacherUserId;
  final int year;
  final int month;
  final int completedLessonCount;
  final int cancelledLessonCount;
  final int activeStudentCount;
  final double expectedAmount;
  final double collectedAmount;
  final DateTime? updatedOnUtc;

  /// 0–1 arası tahsilat oranı. Beklenen tutar yoksa (henüz ders yok) 0 döner —
  /// sıfıra bölmek yerine "veri yok" anlamı taşır.
  double get collectionRatio =>
      expectedAmount <= 0 ? 0 : (collectedAmount / expectedAmount).clamp(0, 1);

  double get outstandingAmount {
    final remaining = expectedAmount - collectedAmount;
    return remaining <= 0 ? 0 : remaining;
  }

  bool get isEmpty =>
      completedLessonCount == 0 &&
      cancelledLessonCount == 0 &&
      activeStudentCount == 0 &&
      expectedAmount == 0 &&
      collectedAmount == 0;
}

/// M14-5: Öğretmenin uygunluk takviminde ders planlanmamış bir aralık.
class FreeSlot {
  const FreeSlot({
    required this.startAtUtc,
    required this.endAtUtc,
    required this.durationMinutes,
  });

  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final int durationMinutes;
}

/// M14-2: Öğrencinin bir ISO haftasındaki çalışma toplamı.
class StudentStudyWeek {
  const StudentStudyWeek({
    required this.year,
    required this.weekOfYear,
    required this.weekStart,
    required this.studyMinutes,
    required this.sessionCount,
    required this.testCount,
  });

  final int year;
  final int weekOfYear;
  final DateTime weekStart;
  final int studyMinutes;
  final int sessionCount;
  final int testCount;
}

/// M14-3: Öğrencinin bir dersteki aylık performansı. [delta] bir önceki aya göre
/// skor farkıdır ve **sunucuda sorgu anında** hesaplanır (saklanmaz).
class StudentPerformanceMonth {
  const StudentPerformanceMonth({
    required this.subject,
    required this.year,
    required this.month,
    required this.masteryScore,
    required this.averageNet,
    required this.testCount,
    required this.delta,
  });

  final String subject;
  final int year;
  final int month;
  final int masteryScore; // 0-100
  final double averageNet; // 0-1 net oranı
  final int testCount;
  final int delta;
}

/// M14-4: Üretilmiş PDF raporun kaydı. Dosyanın kendisi ayrı bir yetkili uçtan indirilir.
class GeneratedStudentReport {
  const GeneratedStudentReport({
    required this.reportId,
    required this.studentId,
    required this.periodStart,
    required this.periodEnd,
    required this.fileName,
    required this.sizeBytes,
    required this.createdOnUtc,
  });

  final String reportId;
  final String studentId;
  final DateTime periodStart;
  final DateTime periodEnd;
  final String fileName;
  final int sizeBytes;
  final DateTime createdOnUtc;
}

/// PDF'i cihaza yazıp sistem görüntüleyicisiyle açan bağımlılık.
/// Cubit'in dosya sistemine doğrudan dokunmaması için soyutlandı (test edilebilirlik).
abstract interface class ReportFileOpener {
  /// Açılan dosyanın yerel yolunu döner.
  Future<String> open({required String fileName, required Uint8List bytes});
}

abstract interface class ReportingRepository {
  Future<TeacherMonthlyReport> teacherMonthly({
    required String teacherUserId,
    int? year,
    int? month,
  });

  Future<List<FreeSlot>> teacherFreeSlots({
    required String teacherUserId,
    DateTime? from,
    DateTime? to,
  });

  Future<List<StudentStudyWeek>> studentStudyAnalysis({
    required String studentId,
    DateTime? from,
    DateTime? to,
  });

  Future<List<StudentPerformanceMonth>> studentPerformance({
    required String studentId,
    String? subject,
    DateTime? from,
    DateTime? to,
  });

  /// Premium kapısı sunucudadır: yetersiz üyelikte `402` gelir.
  Future<GeneratedStudentReport> generateStudentReport({
    required String studentId,
    DateTime? from,
    DateTime? to,
    String? note,
  });

  /// Dosya yoksa `null` döner (kayıt var, içerik yok).
  Future<Uint8List?> downloadReport(String reportId);
}
