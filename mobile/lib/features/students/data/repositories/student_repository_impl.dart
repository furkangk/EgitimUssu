import 'dart:convert';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/students/data/models/student_model.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';

class StudentRepositoryImpl implements StudentRepository {
  StudentRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
    required LocalCache localCache,
  }) : _apiClient = apiClient,
       _config = config,
       _localCache = localCache;

  final ApiClient _apiClient;
  final AppConfig _config;
  final LocalCache _localCache;

  @override
  Future<StudentProfile> createStudent(StudentProfile studentProfile) async {
    final model = StudentProfileModel(
      id: studentProfile.id,
      fullName: studentProfile.fullName,
      gradeLevel: studentProfile.gradeLevel,
      origin: studentProfile.origin,
      teacherUserId: studentProfile.teacherUserId,
      contactEmail: studentProfile.contactEmail,
      contactPhone: studentProfile.contactPhone,
      goalSummary: studentProfile.goalSummary,
      levelNotes: studentProfile.levelNotes,
      subjects: studentProfile.subjects,
    );
    try {
      final response = await _apiClient.post(
        '/api/students/profiles',
        data: model.toCreatePayload(),
      );
      return StudentProfileModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('students')) {
        return StudentProfileModel.demo(
          teacherUserId: studentProfile.teacherUserId ?? 'mock-teacher-user',
          id: DateTime.now().microsecondsSinceEpoch.toString(),
          fullName: studentProfile.fullName,
          gradeLevel: studentProfile.gradeLevel,
        );
      }
      rethrow;
    }
  }

  @override
  Future<StudentProfile> updateStudent(StudentProfile studentProfile) async {
    final model = StudentProfileModel(
      id: studentProfile.id,
      fullName: studentProfile.fullName,
      gradeLevel: studentProfile.gradeLevel,
      origin: studentProfile.origin,
      teacherUserId: studentProfile.teacherUserId,
      contactEmail: studentProfile.contactEmail,
      contactPhone: studentProfile.contactPhone,
      goalSummary: studentProfile.goalSummary,
      levelNotes: studentProfile.levelNotes,
      isActive: studentProfile.isActive,
      subjects: studentProfile.subjects,
    );
    try {
      final response = await _apiClient.put(
        '/api/students/profiles/${studentProfile.id}',
        data: model.toUpdatePayload(),
      );
      return StudentProfileModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('students')) {
        return studentProfile;
      }
      rethrow;
    }
  }

  @override
  Future<StudentProfile> getStudent(String studentId) async {
    if (_config.isMockFallbackEnabled('students')) {
      return _mockStudents('mock-teacher-user').firstWhere(
        (s) => s.id == studentId,
        orElse: () => StudentProfileModel.demo(
          teacherUserId: 'mock-teacher-user',
          id: studentId,
          fullName: 'Mehmet Demir',
          gradeLevel: '8. Sinif',
        ),
      );
    }
    try {
      final response = await _apiClient.get(
        '/api/students/profiles/$studentId',
      );
      return StudentProfileModel.fromJson(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<StudentProfile?> getByUser(String userId) async {
    try {
      final response = await _apiClient.get(
        '/api/students/profiles/by-user/$userId',
      );
      return StudentProfileModel.fromJson(response);
    } on ApiException catch (error) {
      if (error.statusCode == 404) {
        return null;
      }
      rethrow;
    }
  }

  @override
  Future<StudentProfile> createSelfProfile({
    required String userId,
    required String fullName,
    required String gradeLevel,
  }) async {
    final response = await _apiClient.post(
      '/api/students/profiles',
      data: <String, dynamic>{
        'userId': userId,
        'createdByTeacherUserId': null,
        'parentUserId': null,
        'fullName': fullName,
        'gradeLevel': gradeLevel,
        'contactEmail': null,
        'contactPhone': null,
        'goalSummary': null,
        'levelNotes': null,
        'origin': 2, // SelfRegistered
        'subjects': <dynamic>[],
      },
    );
    return StudentProfileModel.fromJson(response);
  }

  @override
  Future<List<StudentProfile>> listByTeacher(String teacherUserId) async {
    if (_config.isMockFallbackEnabled('students')) {
      return _mockStudents(teacherUserId);
    }
    final cacheKey = 'students.byTeacher.$teacherUserId';
    try {
      final response = await _apiClient.getList(
        '/api/students/profiles/by-teacher/$teacherUserId',
      );
      await _localCache.writeString(cacheKey, jsonEncode(response));
      return response
          .whereType<Map<String, dynamic>>()
          .map(StudentProfileModel.fromSummaryJson)
          .toList();
    } on ApiException {
      final cached = await _readCachedStudents(cacheKey);
      if (cached.isNotEmpty) return cached;
      rethrow;
    }
  }

  List<StudentProfile> _mockStudents(String teacherUserId) => [
    StudentProfileModel.demo(
      teacherUserId: teacherUserId,
      id: 'student-1',
      fullName: 'Mehmet Demir',
      gradeLevel: '8. Sınıf',
    ),
    StudentProfileModel.demo(
      teacherUserId: teacherUserId,
      id: 'student-2',
      fullName: 'Ece Ak',
      gradeLevel: '11. Sınıf',
    ),
    StudentProfileModel.demo(
      teacherUserId: teacherUserId,
      id: 'student-3',
      fullName: 'Ali Kaya',
      gradeLevel: '10. Sınıf',
    ),
    StudentProfileModel.demo(
      teacherUserId: teacherUserId,
      id: 'student-4',
      fullName: 'Zeynep Yılmaz',
      gradeLevel: '9. Sınıf',
    ),
    StudentProfileModel.demo(
      teacherUserId: teacherUserId,
      id: 'student-5',
      fullName: 'Berk Çelik',
      gradeLevel: '12. Sınıf',
    ),
  ];

  Future<List<StudentProfile>> _readCachedStudents(String cacheKey) async {
    final cached = await _localCache.readString(cacheKey);
    if (cached == null || cached.isEmpty) {
      return const <StudentProfile>[];
    }
    final decoded = jsonDecode(cached);
    if (decoded is! List<dynamic>) {
      return const <StudentProfile>[];
    }
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(StudentProfileModel.fromSummaryJson)
        .toList();
  }
}
