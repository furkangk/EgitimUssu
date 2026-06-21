import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/repositories/assignment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/data/repositories/lesson_session_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/repositories/scheduling_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/repositories/teacher_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:get_it/get_it.dart';

final GetIt injector = GetIt.instance;

Future<void> configureDependencies() async {
  if (injector.isRegistered<AppConfig>()) {
    return;
  }

  final cache = await SharedPrefsLocalCache.create();

  injector
    ..registerLazySingleton<AppConfig>(AppConfig.fromEnvironment)
    ..registerLazySingleton<TokenStorage>(SecureTokenStorage.new)
    ..registerLazySingleton<LocalCache>(() => cache)
    ..registerLazySingleton<Dio>(
      () => Dio(
        BaseOptions(
          baseUrl: injector<AppConfig>().apiBaseUrl,
          connectTimeout: const Duration(seconds: 15),
          receiveTimeout: const Duration(seconds: 15),
          sendTimeout: const Duration(seconds: 15),
          contentType: 'application/json',
        ),
      ),
    )
    ..registerLazySingleton<ApiClient>(
      () => ApiClient(
        dio: injector<Dio>(),
        tokenStorage: injector<TokenStorage>(),
      ),
    )
    ..registerLazySingleton<AuthRepository>(
      () => AuthRepositoryImpl(
        apiClient: injector<ApiClient>(),
        tokenStorage: injector<TokenStorage>(),
        localCache: injector<LocalCache>(),
        config: injector<AppConfig>(),
      ),
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
    );
}
