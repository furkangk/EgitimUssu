import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/more/domain/device_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/cubit/device_sessions_cubit.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/pages/device_sessions_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// P13 Task 7 / M01-4 + D-03: Cihaz oturumları ekranı.
///
/// Ekranın güvenlik iddiası şu: kullanıcı tanımadığı bir cihazı görüp kapatabilmeli.
/// Bu yüzden testler üç şeyi kilitler: (1) "bu cihaz" ayırt edilir ve **kapatılamaz**;
/// (2) her kapatma **onay ister**; (3) işlemden sonra liste **sunucudan** tazelenir.
void main() {
  final now = DateTime(2026, 9, 5, 12);

  group('DeviceSessionsCubit', () {
    test('yükleme oturumları sunucudan alır', () async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone', isCurrent: true, now: now),
        _session('b', name: 'Pixel', now: now),
      ]);
      final cubit = DeviceSessionsCubit(repository);

      await cubit.load();

      expect(cubit.state.status, DeviceSessionsStatus.loaded);
      expect(cubit.state.sessions, hasLength(2));
      expect(cubit.state.hasOtherSessions, isTrue);
      await cubit.close();
    });

    test('yalnız mevcut oturum varsa "diğerlerini kapat" gösterilmez', () async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone', isCurrent: true, now: now),
      ]);
      final cubit = DeviceSessionsCubit(repository);

      await cubit.load();

      expect(cubit.state.hasOtherSessions, isFalse);
      await cubit.close();
    });

    test('iptalden sonra liste sunucudan tazelenir', () async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone', isCurrent: true, now: now),
        _session('b', name: 'Pixel', now: now),
      ]);
      final cubit = DeviceSessionsCubit(repository);
      await cubit.load();

      await cubit.revoke('b');

      expect(repository.revoked, <String>['b']);
      // İki liste çağrısı: ilk yükleme + iptal sonrası tazeleme.
      expect(repository.listCalls, 2);
      expect(cubit.state.busySessionId, isNull);
      await cubit.close();
    });

    test('toplu iptal kapatılan sayıyı bildirir', () async {
      final repository = _FakeRepository(
        sessions: <DeviceSession>[_session('a', name: 'iPhone', isCurrent: true, now: now)],
        revokeAllCount: 3,
      );
      final cubit = DeviceSessionsCubit(repository);
      await cubit.load();

      await cubit.revokeAll();

      expect(cubit.state.lastRevokedCount, 3);
      expect(cubit.state.revokingAll, isFalse);
      await cubit.close();
    });

    test('API hatası kullanıcıya gösterilir', () async {
      final repository = _FakeRepository(
        sessions: <DeviceSession>[],
        failure: const ApiException(message: 'Oturumlar okunamadı.'),
      );
      final cubit = DeviceSessionsCubit(repository);

      await cubit.load();

      expect(cubit.state.status, DeviceSessionsStatus.error);
      expect(cubit.state.errorMessage, 'Oturumlar okunamadı.');
      await cubit.close();
    });
  });

  group('DeviceSessionsPage', () {
    testWidgets('mevcut cihaz rozetlenir ve kapatma düğmesi verilmez', (tester) async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone 15', isCurrent: true, now: now),
        _session('b', name: 'Pixel 8', now: now),
      ]);

      await _pump(tester, repository);

      expect(find.text('Bu cihaz'), findsOneWidget);
      expect(find.text('iPhone 15'), findsOneWidget);
      expect(find.text('Pixel 8'), findsOneWidget);
      // Yalnız yabancı cihaz için "Kapat" düğmesi var; ayrıca "Diğer tüm oturumları kapat".
      expect(find.widgetWithText(TextButton, 'Kapat'), findsOneWidget);
      expect(find.text('Diğer tüm oturumları kapat'), findsOneWidget);
    });

    testWidgets('kapatma onay ister; vazgeçilirse istek gitmez', (tester) async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone 15', isCurrent: true, now: now),
        _session('b', name: 'Pixel 8', now: now),
      ]);
      await _pump(tester, repository);

      await tester.tap(find.widgetWithText(TextButton, 'Kapat'));
      await tester.pumpAndSettle();
      expect(find.text('Vazgeç'), findsOneWidget);

      await tester.tap(find.text('Vazgeç'));
      await tester.pumpAndSettle();

      expect(repository.revoked, isEmpty);
    });

    testWidgets('onaylanırsa oturum kapatılır', (tester) async {
      final repository = _FakeRepository(sessions: <DeviceSession>[
        _session('a', name: 'iPhone 15', isCurrent: true, now: now),
        _session('b', name: 'Pixel 8', now: now),
      ]);
      await _pump(tester, repository);

      await tester.tap(find.widgetWithText(TextButton, 'Kapat'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Kapat'));
      await tester.pumpAndSettle();

      expect(repository.revoked, <String>['b']);
    });

    testWidgets('oturum yoksa boş durum gösterilir', (tester) async {
      await _pump(tester, _FakeRepository(sessions: <DeviceSession>[]));

      expect(find.text('Açık oturum yok'), findsOneWidget);
      expect(find.text('Diğer tüm oturumları kapat'), findsNothing);
    });
  });
}

Future<void> _pump(WidgetTester tester, _FakeRepository repository) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('tr', 'TR'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: DeviceSessionsPage(cubit: DeviceSessionsCubit(repository)),
    ),
  );
  await tester.pumpAndSettle();
}

DeviceSession _session(
  String id, {
  required DateTime now,
  String? name,
  bool isCurrent = false,
}) => DeviceSession(
  sessionId: id,
  deviceName: name,
  createdOnUtc: now.subtract(const Duration(days: 3)),
  lastUsedOnUtc: now.subtract(const Duration(hours: 2)),
  isCurrent: isCurrent,
);

class _FakeRepository implements DeviceSessionRepository {
  _FakeRepository({
    required this.sessions,
    this.revokeAllCount = 0,
    this.failure,
  });

  final List<DeviceSession> sessions;
  final int revokeAllCount;
  final Object? failure;
  final List<String> revoked = <String>[];
  int listCalls = 0;

  @override
  Future<List<DeviceSession>> list() async {
    listCalls++;
    if (failure != null) throw failure!;
    return sessions;
  }

  @override
  Future<void> revoke(String sessionId) async => revoked.add(sessionId);

  @override
  Future<int> revokeAll() async => revokeAllCount;
}
