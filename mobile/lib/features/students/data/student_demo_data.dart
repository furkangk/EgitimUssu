import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';

class StudentDemoData {
  static final students = <StudentProfile>[
    StudentProfile(
      id: 'student-ali-yilmaz',
      fullName: 'Ali Yilmaz',
      gradeLevel: '9. Sinif',
      origin: 'Manuel kayit',
      contactEmail: 'ali.yilmaz@example.com',
      contactPhone: '0555 123 45 67',
      goalSummary:
          'Lise matematik temelini guclendirip yazili ortalamasini 85+ yapmak.',
      levelNotes: 'Denklemler iyi, problem kurma tarafinda tekrar gerekiyor.',
      subjects: <StudentSubjectTarget>[
        StudentSubjectTarget(subject: 'Matematik', targetLevel: 'Faz 2'),
        StudentSubjectTarget(subject: 'Geometri', targetLevel: 'Temel tekrar'),
      ],
    ),
    StudentProfile(
      id: 'student-zeynep-demir',
      fullName: 'Zeynep Demir',
      gradeLevel: '12. Sinif',
      origin: 'Davet ile',
      contactEmail: 'zeynep.demir@example.com',
      contactPhone: '0555 234 56 78',
      goalSummary: 'TYT-AYT matematik netlerini istikrarli sekilde artirmak.',
      levelNotes: 'Analitik dusunme guclu, sure yonetimi calisilacak.',
      subjects: <StudentSubjectTarget>[
        StudentSubjectTarget(subject: 'Matematik', targetLevel: 'AYT hedef'),
        StudentSubjectTarget(subject: 'Fizik', targetLevel: 'Orta seviye'),
      ],
    ),
    StudentProfile(
      id: 'student-mehmet-kaya',
      fullName: 'Mehmet Kaya',
      gradeLevel: '10. Sinif',
      origin: 'Manuel kayit',
      contactEmail: 'mehmet.kaya@example.com',
      contactPhone: '0555 345 67 89',
      goalSummary:
          'Fen derslerinde odev takibi ve duzenli tekrar rutini kurmak.',
      levelNotes: 'Konu anlatimi sonrasi kisa quizler faydali oluyor.',
      subjects: <StudentSubjectTarget>[
        StudentSubjectTarget(subject: 'Kimya', targetLevel: 'Faz 1'),
        StudentSubjectTarget(subject: 'Biyoloji', targetLevel: 'Okul destek'),
      ],
    ),
    StudentProfile(
      id: 'student-derin-koc',
      fullName: 'Derin Koc',
      gradeLevel: '8. Sinif',
      origin: 'Davet ile',
      contactEmail: 'derin.koc@example.com',
      contactPhone: '0555 456 78 90',
      goalSummary:
          'LGS matematikte problem ve yeni nesil soru pratiğini artirmak.',
      levelNotes: 'Motivasyonu yuksek, haftalik mini hedeflerle iyi ilerliyor.',
      subjects: <StudentSubjectTarget>[
        StudentSubjectTarget(subject: 'Matematik', targetLevel: 'LGS hazirlik'),
        StudentSubjectTarget(subject: 'Turkce', targetLevel: 'Paragraf rutini'),
      ],
    ),
  ];

  static StudentProfile studentById(String id) {
    return students.firstWhere(
      (student) => student.id == id,
      orElse: () => students.first,
    );
  }

  static List<LessonSchedule> lessonsFor(String studentId) {
    final now = DateTime.now();
    return <LessonSchedule>[
      LessonSchedule(
        id: 'lesson-1',
        teacherUserId: 'demo-teacher',
        studentId: studentId,
        subject: 'Matematik',
        lessonFormat: 'Online',
        startAtUtc: DateTime(now.year, now.month, now.day, 15, 30).toUtc(),
        endAtUtc: DateTime(now.year, now.month, now.day, 16, 30).toUtc(),
        timeZone: 'Europe/Istanbul',
        status: 'Planned',
        locationLabel: 'Online',
        notes: 'Denklemler ve problem cozumu.',
      ),
      LessonSchedule(
        id: 'lesson-2',
        teacherUserId: 'demo-teacher',
        studentId: studentId,
        subject: 'Geometri',
        lessonFormat: 'FaceToFace',
        startAtUtc: DateTime(now.year, now.month, now.day - 2, 18, 0).toUtc(),
        endAtUtc: DateTime(now.year, now.month, now.day - 2, 19, 0).toUtc(),
        timeZone: 'Europe/Istanbul',
        status: 'Completed',
        locationLabel: 'Yuz yuze',
        notes: 'Ucgenler tekrar edildi.',
      ),
      LessonSchedule(
        id: 'lesson-3',
        teacherUserId: 'demo-teacher',
        studentId: studentId,
        subject: 'Matematik',
        lessonFormat: 'Online',
        startAtUtc: DateTime(now.year, now.month, now.day - 7, 17, 0).toUtc(),
        endAtUtc: DateTime(now.year, now.month, now.day - 7, 18, 0).toUtc(),
        timeZone: 'Europe/Istanbul',
        status: 'Completed',
        locationLabel: 'Online',
        notes: 'Konu tarama testi yapildi.',
      ),
    ];
  }

  static List<PaymentRecord> paymentsFor(String studentId) {
    final now = DateTime.now();
    return <PaymentRecord>[
      PaymentRecord(
        id: 'payment-1',
        teacherUserId: 'demo-teacher',
        studentId: studentId,
        description: 'Mayis ders paketi',
        currency: 'TRY',
        expectedAmount: 3600,
        collectedAmount: 2400,
        outstandingAmount: 1200,
        status: 'Partial',
        dueDateUtc: DateTime(now.year, now.month, 20).toUtc(),
      ),
      PaymentRecord(
        id: 'payment-2',
        teacherUserId: 'demo-teacher',
        studentId: studentId,
        description: 'Nisan ders paketi',
        currency: 'TRY',
        expectedAmount: 3200,
        collectedAmount: 3200,
        outstandingAmount: 0,
        status: 'Paid',
        dueDateUtc: DateTime(now.year, now.month - 1, 20).toUtc(),
        collectedOnUtc: DateTime(now.year, now.month - 1, 18).toUtc(),
      ),
    ];
  }
}
