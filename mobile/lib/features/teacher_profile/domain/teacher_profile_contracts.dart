class TeacherAvailability {
  const TeacherAvailability({
    required this.dayOfWeek,
    required this.startTime,
    required this.endTime,
    required this.isOnlineAvailable,
    required this.isInPersonAvailable,
  });

  final int dayOfWeek;
  final String startTime;
  final String endTime;
  final bool isOnlineAvailable;
  final bool isInPersonAvailable;
}

class TeacherProfile {
  const TeacherProfile({
    required this.id,
    required this.userId,
    required this.fullName,
    required this.subject,
    required this.city,
    required this.district,
    required this.lessonFormat,
    required this.experienceYears,
    required this.educationLevel,
    required this.hourlyRateAmount,
    required this.currency,
    required this.availabilitySlots,
    this.biography,
    this.headline,
    this.profilePhotoUrl,
    this.isVerified = false,
  });

  final String id;
  final String userId;
  final String fullName;
  final String subject;
  final String city;
  final String district;
  final String? biography;
  final String? headline;
  final String lessonFormat;
  final int experienceYears;
  final String educationLevel;
  final double hourlyRateAmount;
  final String currency;
  final bool isVerified;
  final String? profilePhotoUrl;
  final List<TeacherAvailability> availabilitySlots;
}

abstract interface class TeacherRepository {
  Future<TeacherProfile> createProfile(TeacherProfile profile);
  Future<TeacherProfile> updateProfile(TeacherProfile profile);
  Future<TeacherProfile> getProfile(String userId);
}
