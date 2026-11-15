import 'dart:async';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';
import 'package:egitim_ussu_mobile/features/auth/data/models/user_session_model.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:flutter/foundation.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({
    required ApiClient apiClient,
    required TokenStorage tokenStorage,
    required LocalCache localCache,
    CacheStore? cacheStore,
    required AppConfig config,
    required Dio refreshDio,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage,
       _localCache = localCache,
       _cacheStore = cacheStore ?? CacheStore(localCache),
       _config = config,
       _refreshDio = refreshDio;

  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;
  final LocalCache _localCache;
  final CacheStore _cacheStore;
  int _sessionGeneration = 0;
  Future<void> _sessionWrites = Future<void>.value();
  Future<void>? _logoutInFlight;

  Future<void> _serialize(Future<void> Function() action) {
    final next = _sessionWrites.then((_) => action());
    _sessionWrites = next.then((_) {}, onError: (Object _, StackTrace __) {});
    return next;
  }

  void _checkSession(int generation) {
    if (generation != _sessionGeneration) {
      throw const ApiException(
        message: 'Oturum değişti.',
        kind: ApiErrorKind.auth,
        statusCode: 401,
      );
    }
  }

  final AppConfig _config;

  /// Auth interceptor'ından bağımsız ham Dio — yenileme ve sunucu logout için.
  final Dio _refreshDio;

  static const _sessionKey = 'user_session';

  /// P15 T7 / N-67: Sürmekte olan yenileme. Açılıştaki [restoreSession] ile
  /// interceptor'ın 401 yenilemesi aynı anda gelirse ikisi aynı HTTP çağrısını
  /// bekler — sunucu aynı refresh token'la yalnız bir isteği kazandırır (N-30).
  Completer<UserSession>? _refreshInFlight;

  @override
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  }) async {
    await _logoutInFlight;
    final generation = _sessionGeneration;
    // Mock önce kontrol edilir — API timeout beklenmez
    if (_config.isMockFallbackEnabled('auth')) {
      final session = _buildMockSession(email: email, roleId: roleId);
      await _persistSession(session, generation);
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
    await _persistSession(session, generation);
    return session;
  }

  @override
  Future<void> logout() {
    final inFlight = _logoutInFlight;
    if (inFlight != null) return inFlight;
    _sessionGeneration++;
    _refreshInFlight = null;
    _apiClient.clearSession();
    final clearing = _cacheStore.clearAll();
    clearing.ignore();
    final future = _logout(clearing);
    _logoutInFlight = future;
    return future.whenComplete(() => _logoutInFlight = null);
  }

  Future<void> _logout(Future<void> clearing) async {
    try {
      await _sessionWrites;
      final refresh = await _tokenStorage.readRefreshToken();
      final access = await _tokenStorage.readAccessToken();
      if (refresh != null &&
          refresh.isNotEmpty &&
          !_config.isMockFallbackEnabled('auth')) {
        // No auth interceptor: a logout 401 must never start another refresh.
        await _refreshDio.post<Object?>(
          '/api/identity/logout',
          data: {'refreshToken': refresh},
          options: Options(
            headers: {if (access != null) 'Authorization': 'Bearer $access'},
          ),
        );
      }
    } catch (error) {
      // Never print Dio error/request data, bearer or refresh token.
      debugPrint('Server logout failed: ${error.runtimeType}');
    } finally {
      await _serialize(() async {
        try {
          await _tokenStorage.clear();
        } finally {
          try {
            await _localCache.remove(_sessionKey);
          } finally {
            await clearing;
          }
        }
      });
    }
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
    await _logoutInFlight;
    final generation = _sessionGeneration;
    if (_config.isMockFallbackEnabled('auth')) {
      final session = _buildMockSession(
        email: email,
        fullName: '$firstName $lastName',
        roleId: roleId,
      );
      await _persistSession(session, generation);
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
    await _persistSession(session, generation);
    return session;
  }

  @override
  Future<void> requestPasswordReset({required String email}) async {
    await _apiClient.post(
      '/api/identity/password-reset/request',
      data: <String, dynamic>{'email': email},
    );
  }

  @override
  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    await _apiClient.post(
      '/api/identity/password-reset/confirm',
      data: <String, dynamic>{
        'email': email,
        'token': token,
        'newPassword': newPassword,
      },
    );
  }

  @override
  Future<void> requestEmailVerification({required String email}) async {
    await _apiClient.post(
      '/api/identity/email-verification/request',
      data: <String, dynamic>{'email': email},
    );
  }

  @override
  Future<void> confirmEmailVerification({
    required String email,
    required String token,
  }) async {
    await _apiClient.post(
      '/api/identity/email-verification/confirm',
      data: <String, dynamic>{'email': email, 'token': token},
    );
  }

  @override
  Future<UserSession?> restoreSession() async {
    await _logoutInFlight;
    final generation = _sessionGeneration;
    final cached = await _localCache.readString(_sessionKey);
    if (cached == null) {
      await _cacheStore.clearAll();
      return null;
    }

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
    _checkSession(generation);
    _cacheStore.activate(session.userId);
    if (session.isExpiringSoon) {
      // Refresh token varsa sessizce yenile; yoksa oturumu kapat
      if (session.refreshToken != null) {
        try {
          return await refreshSession();
        } on ApiException catch (error) {
          if (generation != _sessionGeneration) return null;
          // P15 T7 / N-63: Yalnız sunucu refresh token'ı reddederse (400/401) çıkış.
          // Ağ hatası, zaman aşımı ya da 5xx'te (metro, uçak modu, uyanan sunucu)
          // oturum korunur; ilk istek 401 alırsa interceptor yeniden dener.
          if (error.statusCode == 400 || error.statusCode == 401) {
            await logout();
            return null;
          }
          return session;
        } catch (_) {
          if (generation != _sessionGeneration) return null;
          // Beklenmeyen hata (ör. okunamayan yanıt) açılışı kilitlememeli: AuthCubit
          // bu çağrıyı yakalamıyor. Oturum korunur; ilk istek gerekirse yeniden yeniler.
          return session;
        }
      }
      await logout();
      return null;
    }

    return session;
  }

  @override
  Future<UserSession> refreshSession() {
    if (_logoutInFlight != null) {
      return Future.error(
        const ApiException(
          message: 'Oturum kapanıyor.',
          kind: ApiErrorKind.auth,
          statusCode: 401,
        ),
      );
    }
    final inFlight = _refreshInFlight;
    if (inFlight != null) {
      return inFlight.future;
    }
    final completer = Completer<UserSession>();
    _refreshInFlight = completer;
    // Bekleyen başka çağıran yoksa hata "işlenmemiş" sayılmasın; çağıranlar
    // hatayı kendi `await`'lerinde yine alır.
    completer.future.ignore();
    _refreshOnce()
        .then(completer.complete, onError: completer.completeError)
        .whenComplete(() {
          if (identical(_refreshInFlight, completer)) _refreshInFlight = null;
        });
    return completer.future;
  }

  Future<UserSession> _refreshOnce() async {
    final generation = _sessionGeneration;
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
      try {
        await _persistSession(session, generation);
      } catch (_) {
        if (generation != _sessionGeneration) {
          // Rotation may commit before logout uses the old token. This also
          // covers logout racing with the queued local persistence operation.
          try {
            if (session.refreshToken != null) {
              await _refreshDio.post<Object?>(
                '/api/identity/logout',
                data: {'refreshToken': session.refreshToken},
                options: Options(
                  headers: {'Authorization': 'Bearer ${session.accessToken}'},
                ),
              );
            }
          } catch (error) {
            debugPrint('Late refresh revocation failed: ${error.runtimeType}');
          }
        }
        rethrow;
      }
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

  Future<void> _persistSession(UserSessionModel session, int generation) =>
      _serialize(() async {
        _checkSession(generation);
        await Future.wait(<Future<void>>[
          _tokenStorage.writeAccessToken(session.accessToken),
          if (session.refreshToken != null)
            _tokenStorage.writeRefreshToken(session.refreshToken!),
          _localCache.writeString(_sessionKey, session.toCache()),
        ]);
        _checkSession(generation);
        _cacheStore.activate(session.userId);
      });
}
