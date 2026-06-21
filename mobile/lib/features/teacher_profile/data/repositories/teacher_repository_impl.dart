import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/models/teacher_profile_model.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';

class TeacherRepositoryImpl implements TeacherRepository {
  TeacherRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  }) : _apiClient = apiClient,
       _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  @override
  Future<TeacherProfile> createProfile(TeacherProfile profile) async {
    final model = TeacherProfileModel(
      id: profile.id,
      userId: profile.userId,
      fullName: profile.fullName,
      subject: profile.subject,
      city: profile.city,
      district: profile.district,
      biography: profile.biography,
      headline: profile.headline,
      lessonFormat: profile.lessonFormat,
      experienceYears: profile.experienceYears,
      educationLevel: profile.educationLevel,
      hourlyRateAmount: profile.hourlyRateAmount,
      currency: profile.currency,
      isVerified: profile.isVerified,
      profilePhotoUrl: profile.profilePhotoUrl,
      availabilitySlots: profile.availabilitySlots,
    );

    try {
      final response = await _apiClient.post(
        '/api/teachers/profiles',
        data: model.toCreatePayload(),
      );
      return TeacherProfileModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('teacher_profile')) {
        return model;
      }
      rethrow;
    }
  }

  @override
  Future<TeacherProfile> getProfile(String userId) async {
    try {
      final response = await _apiClient.get('/api/teachers/profiles/$userId');
      return TeacherProfileModel.fromJson(response);
    } on ApiException catch (error) {
      if (_config.isMockFallbackEnabled('teacher_profile') ||
          error.statusCode == 404) {
        return TeacherProfileModel.demo(userId: userId);
      }
      rethrow;
    }
  }

  @override
  Future<TeacherProfile> updateProfile(TeacherProfile profile) async {
    final model = TeacherProfileModel(
      id: profile.id,
      userId: profile.userId,
      fullName: profile.fullName,
      subject: profile.subject,
      city: profile.city,
      district: profile.district,
      biography: profile.biography,
      headline: profile.headline,
      lessonFormat: profile.lessonFormat,
      experienceYears: profile.experienceYears,
      educationLevel: profile.educationLevel,
      hourlyRateAmount: profile.hourlyRateAmount,
      currency: profile.currency,
      isVerified: profile.isVerified,
      profilePhotoUrl: profile.profilePhotoUrl,
      availabilitySlots: profile.availabilitySlots,
    );

    try {
      final response = await _apiClient.put(
        '/api/teachers/profiles/${profile.userId}',
        data: model.toUpdatePayload(),
      );
      return TeacherProfileModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('teacher_profile')) {
        return model;
      }
      rethrow;
    }
  }
}
