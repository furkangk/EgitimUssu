import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';
import 'package:egitim_ussu_mobile/features/auth/data/models/user_session_model.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({
    required ApiClient apiClient,
    required TokenStorage tokenStorage,
    required LocalCache localCache,
    required AppConfig config,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage,
       _localCache = localCache,
       _config = config;

  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;
  final LocalCache _localCache;
  final AppConfig _config;

  static const _sessionKey = 'user_session';

  @override
  Future<UserSession> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _apiClient.post(
        '/api/identity/login',
        data: <String, dynamic>{
          'email': email,
          'password': password,
          'deviceName': 'flutter-mobile',
        },
      );
      final session = UserSessionModel.fromJson(response);
      await _persistSession(session);
      return session;
    } on ApiException {
      if (_config.isMockFallbackEnabled('auth')) {
        final session = _buildMockSession(email: email);
        await _persistSession(session);
        return session;
      }
      rethrow;
    }
  }

  @override
  Future<void> logout() async {
    await _tokenStorage.clear();
    await _localCache.remove(_sessionKey);
  }

  @override
  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
  }) async {
    try {
      final response = await _apiClient.post(
        '/api/identity/register',
        data: <String, dynamic>{
          'email': email,
          'password': password,
          'firstName': firstName,
          'lastName': lastName,
          'phoneNumber': phoneNumber,
          'roles': <int>[2],
        },
      );
      final session = UserSessionModel.fromJson(response);
      await _persistSession(session);
      return session;
    } on ApiException {
      if (_config.isMockFallbackEnabled('auth')) {
        final session = _buildMockSession(
          email: email,
          fullName: '$firstName $lastName',
        );
        await _persistSession(session);
        return session;
      }
      rethrow;
    }
  }

  @override
  Future<UserSession?> restoreSession() async {
    final cached = await _localCache.readString(_sessionKey);
    if (cached == null) {
      return null;
    }

    final session = UserSessionModel.fromCache(cached);
    if (session.isExpiringSoon) {
      await logout();
      return null;
    }

    await _tokenStorage.writeAccessToken(session.accessToken);
    return session;
  }

  UserSessionModel _buildMockSession({
    required String email,
    String? fullName,
  }) {
    return UserSessionModel(
      userId: 'mock-teacher-user',
      email: email,
      fullName: fullName ?? 'Demo Ogretmen',
      roles: const <String>['Teacher'],
      accessToken: 'mock-access-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 7)),
    );
  }

  Future<void> _persistSession(UserSessionModel session) async {
    await _tokenStorage.writeAccessToken(session.accessToken);
    await _localCache.writeString(_sessionKey, session.toCache());
  }
}
