import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/dashboard/data/models/teacher_dashboard_summary_model.dart';
import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';

class DashboardRepositoryImpl implements DashboardRepository {
  DashboardRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  })  : _apiClient = apiClient,
        _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  @override
  Future<TeacherDashboardSummary> getSummary(String teacherUserId) async {
    try {
      final response = await _apiClient.get(
        '/api/teachers/profiles/$teacherUserId/dashboard-summary',
      );
      return TeacherDashboardSummaryModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('dashboard')) {
        return _mockSummary();
      }
      rethrow;
    }
  }

  TeacherDashboardSummary _mockSummary() {
    final now = DateTime.now().toUtc();
    final today = DateTime.utc(now.year, now.month, now.day);
    return TeacherDashboardSummary(
      todayLessons: [
        DashboardTodayLesson(
          id: 'mock-lesson-1',
          studentId: 'student-1',
          subject: 'Matematik',
          lessonFormat: 'Online',
          startAtUtc: today.add(const Duration(hours: 15, minutes: 30)),
          endAtUtc: today.add(const Duration(hours: 16, minutes: 30)),
        ),
        DashboardTodayLesson(
          id: 'mock-lesson-2',
          studentId: 'student-2',
          subject: 'Fizik',
          lessonFormat: 'InPerson',
          startAtUtc: today.add(const Duration(hours: 18)),
          endAtUtc: today.add(const Duration(hours: 19, minutes: 30)),
          locationLabel: 'Çalışma odası',
        ),
      ],
      pendingAssignmentsCount: 3,
      pendingAssignments: const [
        DashboardPendingAssignment(
          id: 'asgn-1',
          studentId: 'student-1',
          title: 'Problem föyü 1',
        ),
        DashboardPendingAssignment(
          id: 'asgn-2',
          studentId: 'student-2',
          title: 'Fizik deney raporu',
        ),
        DashboardPendingAssignment(
          id: 'asgn-3',
          studentId: 'student-1',
          title: 'Mini tekrar testi',
        ),
      ],
      overduePaymentsCount: 1,
      overduePaymentsCurrency: 'TRY',
      overduePaymentsTotal: 850,
      overduePayments: [
        DashboardOverduePayment(
          id: 'pay-1',
          studentId: 'student-2',
          description: 'Mayıs ayı ders ücreti',
          currency: 'TRY',
          outstandingAmount: 850,
          dueDateUtc: today.subtract(const Duration(days: 5)),
        ),
      ],
    );
  }
}
