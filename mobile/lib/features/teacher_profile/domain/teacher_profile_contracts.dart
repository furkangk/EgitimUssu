import 'dart:typed_data';

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

  /// Tek alani degistirip geri kalanini koruyan kopya. PUT govdesi profilin
  /// **tamamini** tasidigi icin (sunucu slot listesini bastan yazar) kismi
  /// kaydetmelerde kullanilir.
  TeacherProfile copyWith({List<TeacherAvailability>? availabilitySlots}) {
    return TeacherProfile(
      id: id,
      userId: userId,
      fullName: fullName,
      subject: subject,
      city: city,
      district: district,
      biography: biography,
      headline: headline,
      lessonFormat: lessonFormat,
      experienceYears: experienceYears,
      educationLevel: educationLevel,
      hourlyRateAmount: hourlyRateAmount,
      currency: currency,
      isVerified: isVerified,
      profilePhotoUrl: profilePhotoUrl,
      availabilitySlots: availabilitySlots ?? this.availabilitySlots,
    );
  }
}

abstract interface class TeacherRepository {
  Future<TeacherProfile> createProfile(TeacherProfile profile);
  Future<TeacherProfile> updateProfile(TeacherProfile profile);
  Future<TeacherProfile> getProfile(String userId);

  /// M02-2/D-05: Profil fotografini yukler; sunucunun dondugu indirme rotasini verir.
  /// Fotograf `profilePhotoUrl` alanina **istemciden yazilmaz** — yalniz bu uc set eder.
  Future<String> uploadProfilePhoto({
    required String userId,
    required String filePath,
  });

  /// Profil fotografinin baytlarini yetkili ucdan indirir; fotograf yoksa `null`.
  Future<Uint8List?> downloadProfilePhoto(String userId);
}
