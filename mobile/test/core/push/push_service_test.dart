import 'dart:async';

import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

/// P03 Task 5 (A-04/D-11): Push izni + cihaz token kaydı.
/// Kritik davranış: izin verilmezse token **kaydedilmez**, token yenilenince kayıt tazelenir,
/// çıkışta token devre dışı bırakılır ve bildirime dokunma `taps` akışına düşer.
void main() {
  group('PushService', () {
    test('registerForUser token alinca repository.register cagirir', () async {
      final repository = _RecordingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );

      final token = await service.registerForUser('user-1');

      expect(token, 'fcm-token-1');
      expect(repository.registered.single.token, 'fcm-token-1');
      expect(repository.registered.single.userId, 'user-1');
      expect(repository.registered.single.platform, DevicePlatform.android);

      await service.dispose();
    });

    test('izin verilmezse token kaydedilmez', () async {
      final repository = _RecordingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => false,
        platform: DevicePlatform.ios,
      );

      final token = await service.registerForUser('user-1');

      expect(token, isNull);
      expect(repository.registered, isEmpty);

      await service.dispose();
    });

    test('token alinamazsa kayit yapilmaz', () async {
      final repository = _RecordingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => null,
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );

      expect(await service.registerForUser('user-1'), isNull);
      expect(repository.registered, isEmpty);

      await service.dispose();
    });

    test('token yenilendiginde kayit tazelenir', () async {
      final repository = _RecordingDeviceTokenRepository();
      final refreshes = StreamController<String>();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        tokenRefreshes: refreshes.stream,
        platform: DevicePlatform.android,
      );

      await service.registerForUser('user-1');
      refreshes.add('fcm-token-2');
      await Future<void>.delayed(Duration.zero);

      expect(repository.registered.length, 2);
      expect(repository.registered.last.token, 'fcm-token-2');

      await refreshes.close();
      await service.dispose();
    });

    test('oturum yokken token yenilenirse kayit yapilmaz', () async {
      final repository = _RecordingDeviceTokenRepository();
      final refreshes = StreamController<String>();
      PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        tokenRefreshes: refreshes.stream,
        platform: DevicePlatform.android,
      );

      refreshes.add('fcm-token-2');
      await Future<void>.delayed(Duration.zero);

      expect(repository.registered, isEmpty);
      await refreshes.close();
    });

    test('unregister kayitli tokeni devre disi birakir', () async {
      final repository = _RecordingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );

      await service.registerForUser('user-1');
      await service.unregister();

      expect(repository.unregistered, ['fcm-token-1']);

      await service.dispose();
    });

    test('kayit yokken unregister sessizce gecer', () async {
      final repository = _RecordingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );

      await service.unregister();

      expect(repository.unregistered, isEmpty);
      await service.dispose();
    });

    test('gelen bildirim dokunmasi taps akisina dusuyor', () async {
      final repository = _RecordingDeviceTokenRepository();
      final incoming = StreamController<PushTap>();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        incomingTaps: incoming.stream,
        platform: DevicePlatform.android,
      );

      final received = <PushTap>[];
      service.taps.listen(received.add);

      incoming.add(const PushTap(route: '/scheduling', data: {'lessonId': 'l-1'}));
      await Future<void>.delayed(Duration.zero);

      expect(received.single.route, '/scheduling');
      expect(received.single.data['lessonId'], 'l-1');

      await incoming.close();
      await service.dispose();
    });

    test('kayit hatasi uygulamayi durdurmaz', () async {
      final repository = _ThrowingDeviceTokenRepository();
      final service = PushService(
        repository: repository,
        tokenProvider: () async => 'fcm-token-1',
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );

      // Push kaydı başarısız olsa da oturum akışı kesilmemeli (null döner, fırlatmaz).
      expect(await service.registerForUser('user-1'), isNull);

      await service.dispose();
    });
  });

  group('PushTap.fromData', () {
    test('route ve veri alanlarini okur', () {
      final tap = PushTap.fromData({'route': '/scheduling', 'lessonId': 'l-1'});

      expect(tap.route, '/scheduling');
      expect(tap.data['lessonId'], 'l-1');
    });

    test('route yoksa null olur', () {
      expect(PushTap.fromData({'kind': 'System'}).route, isNull);
    });
  });

  group('DevicePlatform', () {
    test('backend enum degerleriyle eslesir', () {
      expect(DevicePlatform.android.wireValue, 1);
      expect(DevicePlatform.ios.wireValue, 2);
      expect(DevicePlatform.web.wireValue, 3);
    });
  });
}

class _RecordingDeviceTokenRepository implements DeviceTokenRepository {
  final List<({String userId, String token, DevicePlatform platform})> registered = [];
  final List<String> unregistered = [];

  @override
  Future<void> register({
    required String userId,
    required String token,
    required DevicePlatform platform,
    String? deviceName,
  }) async {
    registered.add((userId: userId, token: token, platform: platform));
  }

  @override
  Future<void> unregister(String token) async {
    unregistered.add(token);
  }
}

class _ThrowingDeviceTokenRepository implements DeviceTokenRepository {
  @override
  Future<void> register({
    required String userId,
    required String token,
    required DevicePlatform platform,
    String? deviceName,
  }) async {
    throw Exception('ağ hatası');
  }

  @override
  Future<void> unregister(String token) async {
    throw Exception('ağ hatası');
  }
}
