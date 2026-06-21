import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/models/assignment_model.dart';
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
    try {
      final response = await _apiClient.get(
        '/api/assignments/lesson-sessions/$lessonSessionId/follow-up',
      );
      return FollowUpAssignmentModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('assignments')) {
        return FollowUpAssignmentModel.demo(lessonSessionId);
      }
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
}
