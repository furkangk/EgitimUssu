import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/push/firebase_push_binding.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/repositories/assignment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/dashboard/data/repositories/dashboard_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/data/repositories/lesson_session_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/data/repositories/device_token_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/notifications/data/repositories/notification_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/data/repositories/parent_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/progress/data/repositories/progress_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/data/repositories/reporting_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/reporting/data/services/device_report_file_opener.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/repositories/scheduling_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/settings/data/repositories/settings_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/data/repositories/study_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/repositories/teacher_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:get_it/get_it.dart';

final GetIt injector = GetIt.instance;

Future<void> configureDependencies({bool firebaseAvailable = false}) async {
  if (injector.isRegistered<AppConfig>()) {
    return;
  }

  final cache = await SharedPrefsLocalCache.create();

  injector
    ..registerLazySingleton<AppConfig>(AppConfig.fromEnvironment)
    ..registerLazySingleton<TokenStorage>(SecureTokenStorage.new)
    ..registerLazySingleton<LocalCache>(() => cache)
    ..registerLazySingleton<Dio>(
      () {
        final config = injector<AppConfig>();
        // Render free tier uykudan uyanmak için 60 sn gerekebilir
        final connectTimeout = config.isProductionLike
            ? const Duration(seconds: 60)
            : const Duration(seconds: 15);
        return Dio(
          BaseOptions(
            baseUrl: config.apiBaseUrl,
            connectTimeout: connectTimeout,
            receiveTimeout: const Duration(seconds: 30),
            sendTimeout: const Duration(seconds: 15),
            contentType: 'application/json',
          ),
        );
      },
    )
    ..registerLazySingleton<ApiClient>(
      () => ApiClient(
        dio: injector<Dio>(),
        tokenStorage: injector<TokenStorage>(),
        // Lazy: AuthRepository henüz çözülmemiş olabilir; callback çağrıldığında çözülür.
        onRefreshToken: () async {
          try {
            final session = await injector<AuthRepository>().refreshSession();
            return session.accessToken;
          } catch (_) {
            return null;
          }
        },
      ),
    )
    ..registerLazySingleton<AuthRepository>(
      () {
        final config = injector<AppConfig>();
        final refreshDio = Dio(
          BaseOptions(
            baseUrl: config.apiBaseUrl,
            connectTimeout: const Duration(seconds: 60),
            receiveTimeout: const Duration(seconds: 30),
            sendTimeout: const Duration(seconds: 15),
            contentType: 'application/json',
          ),
        );
        return AuthRepositoryImpl(
          apiClient: injector<ApiClient>(),
          tokenStorage: injector<TokenStorage>(),
          localCache: injector<LocalCache>(),
          config: config,
          refreshDio: refreshDio,
        );
      },
    )
    ..registerLazySingleton<TeacherRepository>(
      () => TeacherRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    ..registerLazySingleton<StudentRepository>(
      () => StudentRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
        localCache: injector<LocalCache>(),
      ),
    )
    ..registerLazySingleton<StudyRepository>(
      () => StudyRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    ..registerLazySingleton<ProgressRepository>(
      () => ProgressRepositoryImpl(apiClient: injector<ApiClient>()),
    )
    ..registerLazySingleton<ReportingRepository>(
      () => ReportingRepositoryImpl(apiClient: injector<ApiClient>()),
    )
    ..registerLazySingleton<ReportFileOpener>(
      () => const DeviceReportFileOpener(),
    )
    ..registerLazySingleton<SchedulingRepository>(
      () => SchedulingRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
        localCache: injector<LocalCache>(),
      ),
    )
    ..registerLazySingleton<LessonSessionRepository>(
      () => LessonSessionRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    ..registerLazySingleton<AssignmentRepository>(
      () => AssignmentRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    ..registerLazySingleton<PaymentRepository>(
      () => PaymentRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
        localCache: injector<LocalCache>(),
      ),
    )
    ..registerLazySingleton<DashboardRepository>(
      () => DashboardRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    ..registerLazySingleton<SettingsRepository>(
      () => SettingsRepositoryImpl(apiClient: injector<ApiClient>()),
    )
    ..registerLazySingleton<NotificationRepository>(
      () => NotificationRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    )
    // A-04/M11-2: cihaz push token kaydı + FCM'e bağlı push servisi.
    ..registerLazySingleton<DeviceTokenRepository>(
      () => DeviceTokenRepositoryImpl(apiClient: injector<ApiClient>()),
    )
    ..registerLazySingleton<PushService>(
      () => FirebasePushBinding.createService(
        repository: injector<DeviceTokenRepository>(),
        firebaseAvailable: firebaseAvailable,
      ),
    )
    ..registerLazySingleton<ParentRepository>(
      () => ParentRepositoryImpl(
        apiClient: injector<ApiClient>(),
        config: injector<AppConfig>(),
      ),
    );
}
