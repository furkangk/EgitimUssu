import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/repositories/assignment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignments_list_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/data/repositories/study_repository_impl.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/paged_list_boundary.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';
import '../../helpers/fake_notification_repository.dart';

const config = AppConfig(
  apiBaseUrl: 'https://test',
  appEnvironment: 'development',
  useMockFallback: false,
  mockFallbackFeatures: {},
);

class Pages implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    requests.add(options);
    final next = options.queryParameters['after'] == 'cursor';
    return ResponseBody.fromString(
      jsonEncode({
        'items': [
          {'id': next ? '2' : '1', 'title': 'Ödev'},
        ],
        'nextCursor': next ? null : 'cursor',
      }),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class NotificationPages extends FakeNotificationRepository {
  final calls = <String?>[];
  Completer<UserNotificationPage>? pending;
  bool failNext = false;
  @override
  Future<UserNotificationPage> listUserNotifications({
    required String userId,
    bool onlyUnread = false,
    int skip = 0,
    int take = 20,
    String? after,
    int? limit,
  }) async {
    calls.add(after);
    if (after != null && pending != null) return pending!.future;
    if (after != null && failNext) {
      failNext = false;
      throw Exception('network');
    }
    UserNotification row(String id) => UserNotification(
      id: id,
      userId: userId,
      kind: 'System',
      title: '',
      body: '',
      route: null,
      isRead: false,
      createdOnUtc: DateTime.utc(2026),
    );
    return UserNotificationPage(
      items: [row(after == null ? '1' : '2')],
      totalCount: null,
      nextCursor: after == null ? 'cursor' : null,
    );
  }
}

void main() {
  test('analiz adaptoru ilk sayfada kesilmeden tum sayfalari okur', () async {
    final adapter = Pages();
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))..httpClientAdapter = adapter;
    final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    addTearDown(() { api.dispose(); dio.close(); });
    final repository = StudyRepositoryImpl(apiClient: api, config: config);
    expect((await repository.listSessions('s')).map((x) => x.id), ['1', '2']);
    expect((await repository.listTests('s')).map((x) => x.id), ['1', '2']);
    expect((await repository.listNotes('s')).map((x) => x.id), ['1', '2']);
    expect(adapter.requests.length, 6);
    expect(adapter.requests.last.queryParameters, {'after': 'cursor', 'limit': 100});
  });
  test('kapanan cubit geciken sayfadan emit uretmez', () async {
    final repository = NotificationPages(); final cubit = NotificationCenterCubit(repository);
    await cubit.load('u'); repository.pending = Completer<UserNotificationPage>();
    final next = cubit.loadMore(); await cubit.close();
    repository.pending!.complete(const UserNotificationPage(items: [], totalCount: null));
    await next; expect(cubit.isClosed, isTrue);
  });
  testWidgets('kisa ilk sayfa gorunumu doldurur ve hata otomatik dongu baslatmaz', (tester) async {
    var calls = 0; var failed = false; var count = 1;
    await tester.pumpWidget(MaterialApp(localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales, locale: const Locale('tr'),
      home: StatefulBuilder(builder: (context, setState) => Scaffold(body: PagedListBoundary(
        hasMore: true, isLoading: false, errorMessage: failed ? 'network' : null,
        onLoadMore: () { setState(() { calls++; failed = true; count = 2; }); },
        builder: (controller) => ListView.builder(controller: controller, itemExtent: 40,
          itemCount: count, itemBuilder: (_, index) => Text('item $index')),
      )))));
    await tester.pumpAndSettle(); expect(calls, 1); expect(find.text('item 1'), findsOneWidget);
    await tester.pump(); expect(calls, 1);
    await tester.tap(find.byType(TextButton)); await tester.pumpAndSettle(); expect(calls, 2);
    await tester.pumpWidget(const SizedBox()); expect(tester.takeException(), isNull);
  });
  test('repository cursor sorgusunu ve son sayfa bilgisini tasir', () async {
    final adapter = Pages();
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))
      ..httpClientAdapter = adapter;
    final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    addTearDown(() {
      api.dispose();
      dio.close();
    });
    final repository = AssignmentRepositoryImpl(apiClient: api, config: config);
    final first = await repository.listPage(teacherUserId: 't');
    final second = await repository.listPage(
      teacherUserId: 't',
      after: first.nextCursor,
    );
    expect(first.items.single.id, '1');
    expect(second.items.single.id, '2');
    expect(second.nextCursor, isNull);
    expect(adapter.requests.last.queryParameters, {
      'teacherUserId': 't',
      'after': 'cursor',
      'limit': 50,
    });
  });
  test('odev cubit ilk sayfayi yukler ve devamina ekler', () async {
    final adapter = Pages();
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))
      ..httpClientAdapter = adapter;
    final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    addTearDown(() {
      api.dispose();
      dio.close();
    });
    final cubit = AssignmentsListCubit(
      AssignmentRepositoryImpl(apiClient: api, config: config),
    );
    await cubit.load('t');
    await cubit.loadMore();
    await cubit.loadMore();
    expect(cubit.state.assignments.map((x) => x.id), ['1', '2']);
    expect(adapter.requests.length, 2);
    await cubit.close();
  });
  test('bildirim devami tek ucus, son sayfada tekrar istek yok', () async {
    final repository = NotificationPages();
    final cubit = NotificationCenterCubit(repository);
    await cubit.load('u');
    repository.pending = Completer<UserNotificationPage>();
    final next = cubit.loadMore();
    await cubit.loadMore();
    expect(repository.calls, [null, 'cursor']);
    repository.pending!.complete(
      const UserNotificationPage(items: [], totalCount: null),
    );
    await next;
    await cubit.loadMore();
    expect(repository.calls.length, 2);
    await cubit.close();
  });
  test('basarisiz devam imleci korur ve tekrar denenir', () async {
    final repository = NotificationPages();
    final cubit = NotificationCenterCubit(repository);
    await cubit.load('u');
    repository.failNext = true;
    await cubit.loadMore();
    expect(cubit.state.items.length, 1);
    await cubit.loadMore();
    expect(cubit.state.items.map((x) => x.id), ['1', '2']);
    await cubit.close();
  });
  test('filtre yenilenince geciken eski sayfa yok sayilir', () async {
    final repository = NotificationPages();
    final cubit = NotificationCenterCubit(repository);
    await cubit.load('u');
    repository.pending = Completer<UserNotificationPage>();
    final next = cubit.loadMore();
    await cubit.setOnlyUnread(true);
    repository.pending!.complete(
      const UserNotificationPage(items: [], totalCount: null),
    );
    await next;
    expect(cubit.state.items.map((x) => x.id), ['1']);
    expect(cubit.state.onlyUnread, isTrue);
    await cubit.close();
  });
}
