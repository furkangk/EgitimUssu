import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/models/assignment_model.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/models/lesson_resource_model.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';

class AssignmentRepositoryImpl implements AssignmentRepository {
  AssignmentRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  }) : _apiClient = apiClient,
       _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  @override
  Future<FollowUpAssignment> getFollowUp(String lessonSessionId) async {
    if (_config.isMockFallbackEnabled('assignments')) {
      return FollowUpAssignmentModel.demo(lessonSessionId);
    }
    try {
      final response = await _apiClient.get(
        '/api/assignments/lesson-sessions/$lessonSessionId/follow-up',
      );
      return FollowUpAssignmentModel.fromJson(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<FollowUpAssignment> saveFollowUp(
    FollowUpAssignment followUpAssignment,
  ) async {
    final model = FollowUpAssignmentModel(
      lessonSessionId: followUpAssignment.lessonSessionId,
      summary: followUpAssignment.summary,
      coveredTopics: followUpAssignment.coveredTopics,
      recommendations: followUpAssignment.recommendations,
      assignments: followUpAssignment.assignments,
    );
    try {
      final response = await _apiClient.post(
        '/api/assignments/lesson-sessions/${followUpAssignment.lessonSessionId}/follow-up',
        data: model.toCreatePayload(),
      );
      return FollowUpAssignmentModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('assignments')) {
        return followUpAssignment;
      }
      rethrow;
    }
  }

  @override
  Future<List<AssignmentItem>> listByStudent(String studentId) async {
    if (_config.isMockFallbackEnabled('assignments')) {
      return AssignmentItemModel.demoList();
    }
    final response = await _apiClient.getList(
      '/api/assignments',
      queryParameters: <String, dynamic>{'studentId': studentId},
    );
    return response
        .whereType<Map<String, dynamic>>()
        .map(AssignmentItemModel.fromJson)
        .toList();
  }

  @override
  Future<AssignmentItem> completeAssignment(String assignmentId) async {
    final response =
        await _apiClient.post('/api/assignments/$assignmentId/complete');
    return AssignmentItemModel.fromJson(response);
  }

  @override
  Future<AssignmentItem> submitWork(String assignmentId, String filePath) async {
    final response = await _apiClient.postFile(
      '/api/assignments/$assignmentId/submission',
      filePath: filePath,
    );
    return AssignmentItemModel.fromJson(response);
  }

  @override
  Future<List<AssignmentItem>> listByTeacher(String teacherUserId) async {
    if (_config.isMockFallbackEnabled('assignments')) {
      return AssignmentItemModel.demoList();
    }
    try {
      final response = await _apiClient.getList(
        '/api/assignments',
        queryParameters: <String, dynamic>{'teacherUserId': teacherUserId},
      );
      return response
          .whereType<Map<String, dynamic>>()
          .map(AssignmentItemModel.fromJson)
          .toList();
    } on ApiException {
      rethrow;
    }
  }

  // ---- M06-1 (P04): ders kaynagi paylasimi. Mock fallback YOK — sahte kaynak,
  // gercek paylasim hatasini gizler (A-05 ilkesi).

  @override
  Future<LessonResource> createLessonResource({
    required String title,
    String? description,
    String? url,
    String? filePath,
    String? studentId,
    String? lessonSessionId,
  }) async {
    final response = await _apiClient.postMultipart(
      '/api/assignments/lesson-resources',
      fields: <String, String>{
        'title': title,
        if (description != null && description.isNotEmpty) 'description': description,
        if (url != null && url.isNotEmpty) 'url': url,
        if (studentId != null) 'studentId': studentId,
        if (lessonSessionId != null) 'lessonSessionId': lessonSessionId,
      },
      filePath: filePath,
    );
    return LessonResourceModel.fromJson(response);
  }

  @override
  Future<List<LessonResource>> listLessonResources({
    String? teacherUserId,
    String? studentId,
    String? lessonSessionId,
  }) async {
    final response = await _apiClient.getList(
      '/api/assignments/lesson-resources',
      queryParameters: <String, dynamic>{
        if (teacherUserId != null) 'teacherUserId': teacherUserId,
        if (studentId != null) 'studentId': studentId,
        if (lessonSessionId != null) 'lessonSessionId': lessonSessionId,
      },
    );
    return response
        .whereType<Map<String, dynamic>>()
        .map(LessonResourceModel.fromJson)
        .toList();
  }

  @override
  Future<void> archiveLessonResource(String resourceId) =>
      _apiClient.delete('/api/assignments/lesson-resources/$resourceId');
}
