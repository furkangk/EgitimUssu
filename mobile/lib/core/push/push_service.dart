import 'dart:async';

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
  }) : _repository = repository,
       _tokenProvider = tokenProvider,
       _permissionRequester = permissionRequester,
       _platform = platform,
       _deviceName = deviceName {
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

  String? _currentUserId;
  String? _currentToken;

  /// Bildirime dokunma olayları; `route` doluysa uygulama o rotaya gider.
  Stream<PushTap> get taps => _tapController.stream;

  /// En son kaydedilen cihaz token'ı (kayıt yoksa `null`).
  String? get currentToken => _currentToken;

  /// Girişten sonra çağrılır: izin ister, token alır ve backend'e kaydeder.
  /// İzin verilmezse veya token alınamazsa `null` döner — **oturum akışı kesilmez**.
  Future<String?> registerForUser(String userId) async {
    final granted = await _permissionRequester();
    if (!granted) {
      debugPrint('PushService: bildirim izni verilmedi, token kaydedilmedi.');
      return null;
    }

    final token = await _tokenProvider();
    if (token == null || token.isEmpty) {
      debugPrint('PushService: FCM token alınamadı, kayıt atlandı.');
      return null;
    }

    _currentUserId = userId;
    final saved = await _register(userId, token);
    return saved ? token : null;
  }

  /// Çıkışta çağrılır: kayıtlı token backend'de devre dışı bırakılır.
  Future<void> unregister() async {
    final token = _currentToken;
    _currentUserId = null;
    _currentToken = null;

    if (token == null) {
      return;
    }

    try {
      await _repository.unregister(token);
    } catch (error) {
      // Çıkış akışı bir ağ hatası yüzünden bloke olmamalı; token sunucuda
      // ilk geçersiz gönderimde zaten kapatılır (FCM UNREGISTERED).
      debugPrint('PushService: token silinemedi ($error).');
    }
  }

  Future<void> dispose() async {
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

    await _register(userId, token);
  }

  Future<bool> _register(String userId, String token) async {
    try {
      await _repository.register(
        userId: userId,
        token: token,
        platform: _platform,
        deviceName: _deviceName,
      );
      _currentToken = token;
      return true;
    } catch (error) {
      debugPrint('PushService: cihaz token kaydı başarısız ($error).');
      return false;
    }
  }
}
