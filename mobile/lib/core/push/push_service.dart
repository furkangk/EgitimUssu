import 'dart:async';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:flutter/foundation.dart';

/// FCM kayıt token'ını üretir. Üretimde `FirebaseMessaging.instance.getToken`.
typedef PushTokenProvider = Future<String?> Function();

/// Bildirim iznini ister; kullanıcı reddederse `false` döner.
typedef PushPermissionRequester = Future<bool> Function();

/// Bir push bildirimine dokunulduğunda taşınan bilgi.
/// [route] backend'in `data.route` alanıdır (ör. `/scheduling`); doluysa o ekran açılır.
@immutable
class PushTap {
  const PushTap({this.route, this.data = const <String, String>{}});

  /// FCM `data` sözlüğünden bir dokunma üretir.
  factory PushTap.fromData(Map<String, dynamic> data) {
    final normalized = <String, String>{
      for (final entry in data.entries) entry.key: '${entry.value}',
    };
    final route = normalized['route'];

    return PushTap(
      route: route != null && route.isNotEmpty ? route : null,
      data: normalized,
    );
  }

  final String? route;
  final Map<String, String> data;
}

/// A-04/D-11: Push izni, cihaz token kaydı ve bildirime dokunma akışı.
///
/// Firebase'e doğrudan bağlı **değildir**: token üretimi, izin isteği ve gelen dokunmalar
/// dışarıdan verilir ([PushTokenProvider], [PushPermissionRequester], `tokenRefreshes`,
/// `incomingTaps`). Böylece servis platform kanalı olmadan test edilebilir; üretim bağlantısı
/// `firebase_push_binding.dart` içindedir.
class PushService {
  PushService({
    required DeviceTokenRepository repository,
    required PushTokenProvider tokenProvider,
    required PushPermissionRequester permissionRequester,
    required DevicePlatform platform,
    Stream<String>? tokenRefreshes,
    Stream<PushTap>? incomingTaps,
    String? deviceName,
    PushEventBus? eventBus,
    Stream<Map<String, dynamic>>? foregroundMessages,
  }) : _repository = repository,
       _tokenProvider = tokenProvider,
       _permissionRequester = permissionRequester,
       _platform = platform,
       _deviceName = deviceName,
       _eventBus = eventBus {
    _foregroundSubscription = foregroundMessages?.listen(
      (data) =>
          eventBus?.publish(data, messageId: data['_messageId'] as String?),
    );
    _tokenRefreshSubscription = tokenRefreshes?.listen(_onTokenRefreshed);
    _incomingTapSubscription = incomingTaps?.listen(_tapController.add);
  }

  final DeviceTokenRepository _repository;
  final PushTokenProvider _tokenProvider;
  final PushPermissionRequester _permissionRequester;
  final DevicePlatform _platform;
  final String? _deviceName;

  final StreamController<PushTap> _tapController =
      StreamController<PushTap>.broadcast();

  StreamSubscription<String>? _tokenRefreshSubscription;
  StreamSubscription<PushTap>? _incomingTapSubscription;

  final PushEventBus? _eventBus;
  StreamSubscription<Map<String, dynamic>>? _foregroundSubscription;
  int _generation = 0;
  bool _disposed = false;
  Future<void> _writes = Future.value();
  bool _current(int generation) => !_disposed && generation == _generation;
  Future<T> _serial<T>(Future<T> Function() action) {
    final result = _writes.then((_) => action());
    _writes = result.then<void>((_) {}, onError: (Object e, StackTrace s) {});
    return result;
  }

  String? _currentUserId;
  String? _currentToken;

  /// Bildirime dokunma olayları; `route` doluysa uygulama o rotaya gider.
  Stream<PushTap> get taps => _tapController.stream;

  /// En son kaydedilen cihaz token'ı (kayıt yoksa `null`).
  String? get currentToken => _currentToken;

  /// Girişten sonra çağrılır: izin ister, token alır ve backend'e kaydeder.
  /// İzin verilmezse veya token alınamazsa `null` döner — **oturum akışı kesilmez**.
  Future<String?> registerForUser(String userId) async {
    if (_disposed) return null;
    final generation = ++_generation;
    _currentUserId = userId;
    _eventBus?.activate(userId);
    try {
      final granted = await _permissionRequester();
      if (!_current(generation) || !granted) return null;
      final token = await _tokenProvider();
      if (!_current(generation) || token == null || token.isEmpty) return null;
      final saved = await _serial(() => _register(userId, token, generation));
      return saved ? token : null;
    } catch (error) {
      debugPrint('Push setup failed: ${error.runtimeType}');
      return null;
    }
  }

  /// Çıkışta çağrılır: kayıtlı token backend'de devre dışı bırakılır.
  Future<void> unregister() async {
    _generation++;
    _currentUserId = null;
    _eventBus?.activate(null);
    await _serial(() async {
      final token = _currentToken;
      _currentToken = null;
      if (token != null) await _remove(token);
    });
  }

  Future<void> _remove(String token) async {
    try {
      await _repository.unregister(token);
    } catch (error) {
      debugPrint('Push removal failed: ${error.runtimeType}');
    }
  }

  Future<void> dispose() async {
    _disposed = true;
    _generation++;
    await _foregroundSubscription?.cancel();
    await _tokenRefreshSubscription?.cancel();
    await _incomingTapSubscription?.cancel();
    await _tapController.close();
  }

  Future<void> _onTokenRefreshed(String token) async {
    final userId = _currentUserId;
    if (userId == null) {
      // Oturum yokken gelen yenileme yok sayılır; giriş sonrası zaten kaydedilir.
      return;
    }

    final generation = _generation;
    await _serial(() => _register(userId, token, generation));
  }

  Future<bool> _register(String userId, String token, int generation) async {
    if (!_current(generation)) return false;
    try {
      await _repository.register(
        userId: userId,
        token: token,
        platform: _platform,
        deviceName: _deviceName,
      );
      if (!_current(generation)) {
        await _remove(token);
        if (_currentToken == token) _currentToken = null;
        return false;
      }
      _currentToken = token;
      return true;
    } catch (error) {
      debugPrint('PushService: cihaz token kaydı başarısız ($error).');
      return false;
    }
  }
}
