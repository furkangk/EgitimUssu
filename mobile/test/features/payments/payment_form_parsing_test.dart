import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payment_form_page.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payments_page.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/collect_payment_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/date_symbol_data_local.dart';
import '../../helpers.dart';
import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_notification_repository.dart';

void main() {
  setUpAll(() => initializeDateFormatting('tr_TR'));
  tearDown(injector.reset);
  Future<_Fixture> mount(
    WidgetTester tester,
    Widget page, {
    String locale = 'tr',
  }) async {
    tester.view.physicalSize = const Size(1000, 1600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final f = _Fixture();
    addTearDown(f.dispose);
    await f.auth.restoreSession();
    final notifications = NotificationsCubit(FakeNotificationRepository());
    addTearDown(notifications.close);
    injector.registerSingleton<PaymentRepository>(f.repo);
    injector.registerSingleton<StudentRepository>(
      StudentRepositoryImpl(
        apiClient: f.api,
        config: const AppConfig(
          apiBaseUrl: 'https://test',
          appEnvironment: 'test',
          useMockFallback: false,
          mockFallbackFeatures: {},
        ),
        localCache: InMemoryLocalCache(),
      ),
    );
    final router = GoRouter(
      routes: [GoRoute(path: '/', builder: (_, _) => page)],
    );
    addTearDown(router.dispose);
    await tester.pumpWidget(
      MultiBlocProvider(
        providers: [
          BlocProvider<AuthCubit>.value(value: f.auth),
          BlocProvider<NotificationsCubit>.value(value: notifications),
        ],
        child: MaterialApp.router(
          locale: Locale(locale),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();
    return f;
  }

  testWidgets(
    'new payment form sends 1.500 as 1500 decimal with an operation key',
    (tester) async {
      final f = await mount(tester, const PaymentFormPage());
      final field = find.descendant(
        of: find.byWidgetPredicate(
          (w) => w is AppTextField && w.labelText == 'Tutar',
        ),
        matching: find.byType(TextFormField),
      );
      await tester.enterText(field, '1.500');
      await tester.tap(find.text('Ödemeyi Kaydet'));
      await tester.pumpAndSettle();
      final write = f.adapter.requests.singleWhere((o) => o.method == 'POST');
      expect(write.path, '/api/payments/records');
      expect(write.data['expectedAmount'], 1500);
      expect(write.data['collectedAmount'], 0);
      expect(write.headers['Idempotency-Key'], isNotEmpty);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'edit keeps collection and currency locked and sends only details',
    (tester) async {
      final record = PaymentRecordModel.fromJson(_record);
      final f = await mount(tester, PaymentFormPage(record: record));
      final collected = tester.widget<AppTextField>(
        find.byWidgetPredicate(
          (w) => w is AppTextField && w.labelText == 'Tahsil edildi',
        ),
      );
      expect(collected.readOnly, isTrue);
      final currency = tester.widget<DropdownButtonFormField<String>>(
        find.byType(DropdownButtonFormField<String>).last,
      );
      expect(currency.onChanged, isNull);
      await tester.enterText(
        find.descendant(
          of: find.byWidgetPredicate(
            (w) => w is AppTextField && w.labelText == 'Açıklama',
          ),
          matching: find.byType(TextFormField),
        ),
        'Yeni not',
      );
      await tester.tap(find.text('Değişiklikleri Kaydet'));
      await tester.pumpAndSettle();
      final write = f.adapter.requests.singleWhere((o) => o.method == 'PUT');
      expect(
        write.data.keys,
        unorderedEquals([
          'description',
          'expectedAmount',
          'dueDateUtc',
          'notes',
        ]),
      );
      expect(write.data['expectedAmount'], 1500.5);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'collection sheet parses grouped cents and returns only this delta',
    (tester) async {
      int? delta;
      final record = PaymentRecordModel.fromJson({
        ..._record,
        'outstandingAmount': 1500.5,
      });
      await mount(
        tester,
        Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () async {
                delta = await CollectPaymentSheet.show(context, record);
              },
              child: const Text('Open'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Open'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '1.500,50');
      await tester.ensureVisible(find.text('Tahsilatı Kaydet'));
      await tester.tap(find.text('Tahsilatı Kaydet'));
      await tester.pumpAndSettle();
      expect(delta, 150050);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'currency selector updates summary and both graphs without TRY/USD totals',
    (tester) async {
      await mount(tester, const PaymentsPage());
      expect(find.text('1.500,50 TRY'), findsWidgets);
      expect(find.text('1.520,51 TRY'), findsNothing);
      await tester.tap(find.byType(DropdownButton<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('USD').last);
      await tester.pumpAndSettle();
      expect(find.text('20,01 USD'), findsWidgets);
      expect(find.text('1.500,50 TRY'), findsNothing);
      await tester.tap(find.text('İstatistikler'));
      await tester.pumpAndSettle();
      expect(find.textContaining('toplam 20,01 USD'), findsOneWidget);
      expect(find.text('20,01 USD'), findsWidgets);
      expect(find.textContaining('1.520'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('English payment form and collection labels come from ARB', (
    tester,
  ) async {
    await mount(tester, const PaymentFormPage(), locale: 'en');
    expect(find.text('Add Payment'), findsOneWidget);
    expect(find.text('Save Payment'), findsOneWidget);
    expect(find.text('Amount'), findsOneWidget);
    expect(find.text('Ödemeyi Kaydet'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}

const _record = <String, dynamic>{
  'id': 'p',
  'teacherUserId': 't',
  'studentId': 's',
  'description': 'Ders',
  'currency': 'USD',
  'expectedAmount': 1500.5,
  'collectedAmount': 500,
  'outstandingAmount': 1000.5,
  'dueDateUtc': '2026-11-20T00:00:00Z',
  'status': 'PartiallyPaid',
};

class _Fixture {
  _Fixture() {
    dio.httpClientAdapter = adapter;
    api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    repo = PaymentRepositoryImpl(
      apiClient: api,
      config: const AppConfig(
        apiBaseUrl: 'https://test',
        appEnvironment: 'test',
        useMockFallback: false,
        mockFallbackFeatures: {},
      ),
      localCache: InMemoryLocalCache(),
    );
    auth = AuthCubit(
      FakeAuthRepository(
        session: FakeAuthRepository.defaultSession(userId: 't'),
      ),
    );
  }
  final adapter = _Adapter();
  final dio = Dio(BaseOptions(baseUrl: 'https://test'));
  late final ApiClient api;
  late final PaymentRepository repo;
  late final AuthCubit auth;
  Future<void> dispose() async {
    api.dispose();
    dio.close();
    await auth.close();
  }
}

class _Adapter implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    requests.add(o);
    final Object response = o.path.startsWith('/api/students/')
        ? [
            {
              'id': 's',
              'fullName': 'Ada',
              'gradeLevel': '8',
              'origin': 'TeacherCreated',
              'subjects': [],
            },
          ]
        : o.method != 'GET'
        ? _record
        : o.path.endsWith('/summary')
        ? {
            'totalRecords': 2,
            'currencySummaries': [
              for (final c in ['TRY', 'USD'])
                {
                  'currency': c,
                  'collectedAmountTotal': c == 'TRY' ? 1500.5 : 20.01,
                  'expectedAmountTotal': c == 'TRY' ? 1500.5 : 20.01,
                },
            ],
            'monthlyBreakdown': [
              for (final c in ['TRY', 'USD'])
                {
                  'currency': c,
                  'year': 2026,
                  'month': 11,
                  'collectedAmount': c == 'TRY' ? 1500.5 : 20.01,
                  'expectedAmount': c == 'TRY' ? 1500.5 : 20.01,
                },
            ],
          }
        : {'items': [], 'totalCount': 0};
    return ResponseBody.fromString(
      jsonEncode(response),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
