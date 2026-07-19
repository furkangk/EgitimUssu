class LessonSchedule {
  const LessonSchedule({
    required this.id,
    required this.teacherUserId,
    required this.studentId,
    required this.subject,
    required this.lessonFormat,
    required this.startAtUtc,
    required this.endAtUtc,
    required this.timeZone,
    this.status = 'Planned',
    this.recurrenceRule,
    this.reminderOffsetMinutes,
    this.locationLabel,
    this.meetingUrl,
    this.notes,
  });

  final String id;
  final String teacherUserId;
  final String studentId;
  final String subject;
  final String lessonFormat;
  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final String timeZone;
  final String status;
  final String? recurrenceRule;
  final int? reminderOffsetMinutes;
  final String? locationLabel;

  /// Online dersler icin toplanti baglantisi (Zoom/Meet vb.).
  final String? meetingUrl;
  final String? notes;
}

/// Öğrencinin kendi oluşturduğu kişisel program girdisi (öğretmenden bağımsız).
/// Backend: `scheduling.study_schedule_entries`. Tekrar kuralı [LessonSchedule] ile aynı
/// iCal formatını (FREQ/BYDAY/UNTIL) kullanır.
class StudyScheduleEntry {
  const StudyScheduleEntry({
    required this.id,
    required this.studentId,
    required this.subject,
    required this.startAtUtc,
    required this.endAtUtc,
    required this.timeZone,
    this.topic,
    this.recurrenceRule,
    this.reminderOffsetMinutes,
    this.colorHex,
    this.notes,
    this.status = 'Active',
  });

  final String id;
  final String studentId;
  final String subject;
  final String? topic;
  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final String timeZone;
  final String? recurrenceRule;
  final int? reminderOffsetMinutes;
  final String? colorHex;
  final String? notes;
  final String status;
}

/// Öğrenci takviminde gösterilen tek bir somut oluşum. Hem öğretmenin planladığı dersleri
/// hem öğrencinin kendi programını temsil eder ([source] ile ayrışır). Tekrarlar backend'de
/// genişletildiği için her occurrence somut başlangıç/bitiş taşır.
class CalendarOccurrence {
  const CalendarOccurrence({
    required this.entryId,
    required this.source,
    required this.subject,
    required this.startAtUtc,
    required this.endAtUtc,
    required this.isEditable,
    this.topic,
    this.lessonFormat,
    this.locationLabel,
    this.colorHex,
    this.recurrenceRule,
    this.notes,
    this.completed = false,
  });

  /// Kaynak girdinin (ders planı ya da program girdisi) kimliği.
  final String entryId;

  /// 'Teacher' → öğretmenin planladığı ders (salt-okunur); 'Self' → öğrencinin kendi girdisi.
  final String source;
  final String subject;
  final String? topic;
  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final String? lessonFormat;
  final String? locationLabel;
  final String? colorHex;
  final String? recurrenceRule;
  final String? notes;

  /// Yalnızca öğrencinin kendi girdileri düzenlenebilir; öğretmen dersleri salt-okunurdur.
  final bool isEditable;

  /// Ç-06: bu occurrence'ın gününde derse bağlı tamamlanmış bir çalışma seansı var mı (çalışıldı rozeti).
  final bool completed;

  bool get isTeacher => source == 'Teacher';
  bool get isSelf => source == 'Self';
  bool get isRecurring => (recurrenceRule ?? '').isNotEmpty;
}

abstract interface class SchedulingRepository {
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule);
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule);
  Future<LessonSchedule> getLesson(String lessonId);
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  });
  Future<LessonSchedule> completeLesson({required String lessonId});
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  });

  /// Öğrencinin kendi ders planları. Backend sahipliği `IStudentDirectory` ile
  /// doğrular (öğrenci yalnızca kendi StudentId'sini görebilir).
  Future<List<LessonSchedule>> listStudentLessons({
    required String studentId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  });

  /// Öğrencinin birleşik takvimi: öğretmen dersleri + kendi programı, tekrarlar backend'de
  /// [startAtUtc, endAtUtc] aralığına genişletilmiş olarak döner. Sahiplik `IStudentDirectory`
  /// ile doğrulanır (IDOR koruması).
  Future<List<CalendarOccurrence>> getStudentCalendar({
    required String studentId,
    required DateTime startAtUtc,
    required DateTime endAtUtc,
  });

  /// Öğrencinin kendi program girdisini oluşturur. Öğretmen dersiyle saat çakışması backend'de
  /// reddedilir (409, `scheduling.teacher_conflict`).
  Future<StudyScheduleEntry> createStudyEntry({
    required String studentId,
    required StudyScheduleEntry entry,
  });

  /// Öğrencinin kendi program girdisini günceller.
  Future<StudyScheduleEntry> updateStudyEntry(StudyScheduleEntry entry);

  /// Öğrencinin kendi program girdisini siler (soft-cancel).
  Future<void> deleteStudyEntry(String entryId);
}
