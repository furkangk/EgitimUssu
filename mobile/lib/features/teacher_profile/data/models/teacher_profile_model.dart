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

  factory TeacherProfileModel.demo({required String userId}) {
    return TeacherProfileModel(
      id: 'demo-teacher-profile',
      userId: userId,
      fullName: 'Ayse Yilmaz',
      subject: 'Matematik',
      city: 'Istanbul',
      district: 'Kadikoy',
      biography: 'Deneyimli matematik ogretmeni',
      headline: 'LGS ve TYT',
      lessonFormat: 'OnlineAndInPerson',
      experienceYears: 8,
      educationLevel: 'Lisans',
      hourlyRateAmount: 1250,
      currency: 'TRY',
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

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'userId': userId,
      'fullName': fullName,
      'subject': subject,
      'city': city,
      'district': district,
      'biography': biography,
      'headline': headline,
      'lessonFormat': lessonFormat == 'OnlineAndInPerson' ? 3 : 2,
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
    return <String, dynamic>{...toCreatePayload(), 'isVerified': isVerified};
  }
}
