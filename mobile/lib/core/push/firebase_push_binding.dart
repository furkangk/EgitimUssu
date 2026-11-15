import 'dart:async';
import 'dart:io';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

/// Arka planda gelen bildirimler için üst düzey giriş noktası (Flutter zorunlu kılar).
/// Şimdilik iş yapmaz: bildirimi sistem tepsisine FCM koyar, dokunma `onMessageOpenedApp`'ten gelir.
@pragma('vm:entry-point')
Future<void> firebaseBackgroundMessageHandler(RemoteMessage message) async {
  debugPrint('Arka planda push alındı: ${message.messageId}');
}

/// A-04: Firebase'i başlatır ve [PushService]'i gerçek FCM kaynaklarına bağlar.
///
/// **Bilinçli fail-open:** `google-services.json` / `GoogleService-Info.plist` yoksa
/// (Firebase projesi henüz kurulmadıysa) uygulama açılmaya devam eder ve **uyarı log'lanır** —
/// sessizce yutulmaz. Push o oturumda çalışmaz, uygulamanın geri kalanı etkilenmez.
/// Bkz. `PushOptionsGuard` (backend tarafı fail-fast; istemci tarafı fail-open).
class FirebasePushBinding {
  const FirebasePushBinding._();

  /// Firebase'i başlatır. Başarısızsa `false` döner (uygulama yine açılır).
  static Future<bool> initializeFirebase() async {
    try {
      await Firebase.initializeApp();
      FirebaseMessaging.onBackgroundMessage(firebaseBackgroundMessageHandler);
      return true;
    } catch (error) {
      debugPrint(
        'UYARI: Firebase başlatılamadı ($error). Push bildirimleri BU OTURUMDA ÇALIŞMAYACAK. '
        'google-services.json / GoogleService-Info.plist dosyalarını ekleyin.',
      );
      return false;
    }
  }

  /// Gerçek FCM kaynaklarına bağlı bir [PushService] üretir.
  static PushService createService({
    required DeviceTokenRepository repository,
    required bool firebaseAvailable,
    required PushEventBus eventBus,
  }) {
    if (!firebaseAvailable) {
      // Firebase yok: izin hiç istenmez, token üretilmez. Kayıt çağrıları sessizce null döner
      // (PushService zaten log basar) — böylece giriş akışı bozulmaz.
      return PushService(
        repository: repository,
        eventBus: eventBus,
        tokenProvider: () async => null,
        permissionRequester: () async => false,
        platform: _currentPlatform,
      );
    }

    final messaging = FirebaseMessaging.instance;

    return PushService(
      repository: repository,
      eventBus: eventBus,
      tokenProvider: messaging.getToken,
      permissionRequester: () async {
        final settings = await messaging.requestPermission();
        return settings.authorizationStatus == AuthorizationStatus.authorized ||
            settings.authorizationStatus == AuthorizationStatus.provisional;
      },
      tokenRefreshes: messaging.onTokenRefresh,
      incomingTaps: _taps(messaging),
      foregroundMessages: FirebaseMessaging.onMessage.map(
        (message) => {...message.data, '_messageId': message.messageId},
      ),
      platform: _currentPlatform,
    );
  }

  /// Soğuk açılış (`getInitialMessage`) ve arka plandan dönüş (`onMessageOpenedApp`) tek akışta.
  static Stream<PushTap> _taps(FirebaseMessaging messaging) {
    late StreamController<PushTap> controller;
    StreamSubscription<RemoteMessage>? subscription;
    controller = StreamController<PushTap>(
      onListen: () {
        subscription = FirebaseMessaging.onMessageOpenedApp.listen((message) {
          if (!controller.isClosed) {
            controller.add(PushTap.fromData(message.data));
          }
        });
      },
      onCancel: () async {
        await subscription?.cancel();
      },
    );

    unawaited(
      messaging.getInitialMessage().then((message) {
        if (message != null && !controller.isClosed) {
          controller.add(PushTap.fromData(message.data));
        }
      }),
    );

    return controller.stream;
  }

  static DevicePlatform get _currentPlatform {
    if (kIsWeb) {
      return DevicePlatform.web;
    }

    return Platform.isIOS ? DevicePlatform.ios : DevicePlatform.android;
  }
}
