import 'package:dio/dio.dart';
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
    required Dio refreshDio,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage,
       _localCache = localCache,
       _config = config,
       _refreshDio = refreshDio;

  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;
  final LocalCache _localCache;
  final AppConfig _config;

  /// Auth interceptor'ından bağımsız ham Dio — yalnızca token yenileme için.
  final Dio _refreshDio;

  static const _sessionKey = 'user_session';

  @override
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  }) async {
    // Mock önce kontrol edilir — API timeout beklenmez
    if (_config.isMockFallbackEnabled('auth')) {
      final session = _buildMockSession(email: email, roleId: roleId);
      await _persistSession(session);
      return session;
    }
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
    int roleId = 2,
  }) async {
    if (_config.isMockFallbackEnabled('auth')) {
      final session = _buildMockSession(
        email: email,
        fullName: '$firstName $lastName',
        roleId: roleId,
      );
      await _persistSession(session);
      return session;
    }
    final response = await _apiClient.post(
      '/api/identity/register',
      data: <String, dynamic>{
        'email': email,
        'password': password,
        'firstName': firstName,
        'lastName': lastName,
        'phoneNumber': phoneNumber,
        'roles': <int>[roleId],
      },
    );
    final session = UserSessionModel.fromJson(response);
    await _persistSession(session);
    return session;
  }

  @override
  Future<UserSession?> restoreSession() async {
    final cached = await _localCache.readString(_sessionKey);
    if (cached == null) return null;

    // Y7: Token'lar yalnız secure storage'da; önbellek token içermez.
    final accessToken = await _tokenStorage.readAccessToken();
    if (accessToken == null || accessToken.isEmpty) {
      await logout();
      return null;
    }
    final refreshToken = await _tokenStorage.readRefreshToken();

    final session = UserSessionModel.fromCache(
      cached,
      accessToken: accessToken,
      refreshToken: refreshToken,
    );
    if (session.isExpiringSoon) {
      // Refresh token varsa sessizce yenile; yoksa oturumu kapat
      if (session.refreshToken != null) {
        try {
          return await refreshSession();
        } catch (_) {
          await logout();
          return null;
        }
      }
      await logout();
      return null;
    }

    return session;
  }

  @override
  Future<UserSession> refreshSession() async {
    final storedRefreshToken = await _tokenStorage.readRefreshToken();
    if (storedRefreshToken == null || storedRefreshToken.isEmpty) {
      throw const ApiException(
        message: 'Oturum yenileme bilgisi bulunamadı.',
        statusCode: 401,
      );
    }

    try {
      final response = await _refreshDio.post<Map<String, dynamic>>(
        '/api/identity/refresh',
        data: <String, dynamic>{'refreshToken': storedRefreshToken},
      );
      final session = UserSessionModel.fromJson(
        response.data ?? <String, dynamic>{},
      );
      await _persistSession(session);
      return session;
    } on DioException catch (e) {
      throw ApiException.fromDioException(e);
    }
  }

  UserSessionModel _buildMockSession({
    required String email,
    String? fullName,
    int roleId = 2,
  }) {
    final roleName = _roleName(roleId);
    final isParent = roleId == 4;
    return UserSessionModel(
      userId: isParent ? 'mock-parent-user' : 'mock-teacher-user',
      email: email,
      fullName: fullName ?? (isParent ? 'Demo Veli' : 'Demo Öğretmen'),
      roles: <String>[roleName],
      accessToken: 'mock-access-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 7)),
    );
  }

  static String _roleName(int roleId) {
    switch (roleId) {
      case 3:
        return 'Student';
      case 4:
        return 'Parent';
      default:
        return 'Teacher';
    }
  }

  Future<void> _persistSession(UserSessionModel session) async {
    await Future.wait(<Future<void>>[
      _tokenStorage.writeAccessToken(session.accessToken),
      if (session.refreshToken != null)
        _tokenStorage.writeRefreshToken(session.refreshToken!),
      _localCache.writeString(_sessionKey, session.toCache()),
    ]);
  }
}
