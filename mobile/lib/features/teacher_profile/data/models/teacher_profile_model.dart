import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';

class TeacherAvailabilityModel extends TeacherAvailability {
  const TeacherAvailabilityModel({
    required super.dayOfWeek,
    required super.startTime,
    required super.endTime,
    required super.isOnlineAvailable,
    required super.isInPersonAvailable,
  });

  factory TeacherAvailabilityModel.fromJson(Map<String, dynamic> json) {
    return TeacherAvailabilityModel(
      dayOfWeek: json['dayOfWeek'] as int? ?? 1,
      startTime: json['startTime']?.toString() ?? '18:00:00',
      endTime: json['endTime']?.toString() ?? '19:00:00',
      isOnlineAvailable: json['isOnlineAvailable'] as bool? ?? true,
      isInPersonAvailable: json['isInPersonAvailable'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'dayOfWeek': dayOfWeek,
      'startTime': startTime,
      'endTime': endTime,
      'isOnlineAvailable': isOnlineAvailable,
      'isInPersonAvailable': isInPersonAvailable,
    };
  }
}

class TeacherProfileModel extends TeacherProfile {
  const TeacherProfileModel({
    required super.id,
    required super.userId,
    required super.fullName,
    required super.subject,
    required super.city,
    required super.district,
    required super.lessonFormat,
    required super.experienceYears,
    required super.educationLevel,
    required super.hourlyRateAmount,
    required super.currency,
    required super.availabilitySlots,
    super.biography,
    super.headline,
    super.profilePhotoUrl,
    super.isVerified,
  });

  factory TeacherProfileModel.fromJson(Map<String, dynamic> json) {
    return TeacherProfileModel(
      id: json['id']?.toString() ?? '',
      userId: json['userId']?.toString() ?? '',
      fullName: json['fullName']?.toString() ?? '',
      subject: json['subject']?.toString() ?? '',
      city: json['city']?.toString() ?? '',
      district: json['district']?.toString() ?? '',
      biography: json['biography']?.toString(),
      headline: json['headline']?.toString(),
      lessonFormat: json['lessonFormat']?.toString() ?? 'OnlineAndInPerson',
      experienceYears: json['experienceYears'] as int? ?? 0,
      educationLevel: json['educationLevel']?.toString() ?? '',
      hourlyRateAmount: (json['hourlyRateAmount'] as num?)?.toDouble() ?? 0,
      currency: json['currency']?.toString() ?? 'TRY',
      isVerified: json['isVerified'] as bool? ?? false,
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      availabilitySlots:
          ((json['availabilitySlots'] as List<dynamic>? ?? <dynamic>[]))
              .whereType<Map<String, dynamic>>()
              .map(TeacherAvailabilityModel.fromJson)
              .toList(),
    );
  }

  /// Backend'den profil gelmediğinde (404 / mock fallback) gösterilen demo öğretmen.
  /// [userId]'e göre değişir: böylece öğrencinin birden fazla öğretmeni olduğunda
  /// hepsi aynı isimle değil, farklı demo öğretmenler olarak görünür.
  factory TeacherProfileModel.demo({required String userId}) {
    final int index = _demoIndex(userId);
    final _DemoTeacher t = _demoTeachers[index % _demoTeachers.length];
    return TeacherProfileModel(
      id: 'demo-teacher-$index',
      userId: userId,
      fullName: t.fullName,
      subject: t.subject,
      city: t.city,
      district: t.district,
      biography: t.biography,
      headline: t.headline,
      lessonFormat: t.lessonFormat,
      experienceYears: t.experienceYears,
      educationLevel: t.educationLevel,
      hourlyRateAmount: t.hourlyRateAmount,
      currency: 'TRY',
      isVerified: t.isVerified,
      profilePhotoUrl: null,
      availabilitySlots: const <TeacherAvailabilityModel>[
        TeacherAvailabilityModel(
          dayOfWeek: 1,
          startTime: '18:00:00',
          endTime: '20:00:00',
          isOnlineAvailable: true,
          isInPersonAvailable: true,
        ),
      ],
    );
  }

  /// [userId] sondaki rakamdan (ör. `mock-teacher-2`) veya hashCode'dan kararlı bir
  /// indeks üretir; aynı id her zaman aynı demo öğretmene eşlenir.
  static int _demoIndex(String userId) {
    final RegExpMatch? m = RegExp(r'(\d+)$').firstMatch(userId);
    if (m != null) {
      return (int.tryParse(m.group(1)!) ?? 0).abs();
    }
    return userId.hashCode.abs();
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'userId': userId,
      'fullName': fullName,
      'subject': subject,
      'city': city,
      'district': district,
      'biography': biography,
      'headline': headline,
      'lessonFormat': switch (lessonFormat) {
        'InPerson' => 1,
        'Online' => 2,
        _ => 3, // OnlineAndInPerson / Hybrid
      },
      'experienceYears': experienceYears,
      'educationLevel': educationLevel,
      'hourlyRateAmount': hourlyRateAmount,
      'currency': currency,
      'profilePhotoUrl': profilePhotoUrl,
      'availabilitySlots': availabilitySlots
          .map(
            (slot) => TeacherAvailabilityModel(
              dayOfWeek: slot.dayOfWeek,
              startTime: slot.startTime,
              endTime: slot.endTime,
              isOnlineAvailable: slot.isOnlineAvailable,
              isInPersonAvailable: slot.isInPersonAvailable,
            ).toJson(),
          )
          .toList(),
    };
  }

  Map<String, dynamic> toUpdatePayload() {
    return toCreatePayload();
  }
}

/// Demo öğretmen havuzu için taşıyıcı (yalnızca mock fallback görünümünde kullanılır).
class _DemoTeacher {
  const _DemoTeacher({
    required this.fullName,
    required this.subject,
    required this.city,
    required this.district,
    required this.headline,
    required this.biography,
    required this.lessonFormat,
    required this.experienceYears,
    required this.educationLevel,
    required this.hourlyRateAmount,
    required this.isVerified,
  });

  final String fullName;
  final String subject;
  final String city;
  final String district;
  final String headline;
  final String biography;
  final String lessonFormat;
  final int experienceYears;
  final String educationLevel;
  final double hourlyRateAmount;
  final bool isVerified;
}

/// [TeacherProfileModel.demo] havuzu. İndeks eşlemesi (`_demoIndex`) mock ders
/// atamalarıyla uyumludur: `mock-teacher-1`→Matematik, `mock-teacher-2`→Fen,
/// `mock-teacher-3`→Türkçe. İndeks 0 gerçek id'ler için genel yedek.
const List<_DemoTeacher> _demoTeachers = <_DemoTeacher>[
  _DemoTeacher(
    fullName: 'Ayşe Yılmaz',
    subject: 'Matematik',
    city: 'İstanbul',
    district: 'Kadıköy',
    headline: 'LGS ve TYT Matematik',
    biography: 'Deneyimli matematik öğretmeni. Kavram temelli, sınav odaklı çalışır.',
    lessonFormat: 'OnlineAndInPerson',
    experienceYears: 8,
    educationLevel: 'Lisans',
    hourlyRateAmount: 1250,
    isVerified: true,
  ),
  _DemoTeacher(
    fullName: 'Ahmet Kaya',
    subject: 'Matematik',
    city: 'İstanbul',
    district: 'Kadıköy',
    headline: 'LGS & TYT Matematik',
    biography: 'Soru çözüm tekniklerine ağırlık veren, eksik odaklı çalışan öğretmen.',
    lessonFormat: 'OnlineAndInPerson',
    experienceYears: 9,
    educationLevel: 'Yüksek Lisans',
    hourlyRateAmount: 1300,
    isVerified: true,
  ),
  _DemoTeacher(
    fullName: 'Mehmet Demir',
    subject: 'Fizik & Kimya',
    city: 'Ankara',
    district: 'Çankaya',
    headline: 'TYT-AYT Fen Bilimleri',
    biography: 'Fen bilimlerinde deney ve grafik yorumuna dayalı anlatım yapar.',
    lessonFormat: 'Online',
    experienceYears: 12,
    educationLevel: 'Doktora',
    hourlyRateAmount: 1500,
    isVerified: true,
  ),
  _DemoTeacher(
    fullName: 'Zeynep Şahin',
    subject: 'Türkçe',
    city: 'İzmir',
    district: 'Karşıyaka',
    headline: 'Paragraf & Dil Bilgisi',
    biography: 'Paragraf ve dil bilgisinde hızlı çözüm stratejileri üzerine çalışır.',
    lessonFormat: 'InPerson',
    experienceYears: 6,
    educationLevel: 'Lisans',
    hourlyRateAmount: 1000,
    isVerified: false,
  ),
];
