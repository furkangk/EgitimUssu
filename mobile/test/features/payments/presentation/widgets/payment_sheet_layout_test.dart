import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/collect_payment_sheet.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/widgets/payment_filter_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

const _record = PaymentRecord(
  id: 'payment',
  teacherUserId: 'teacher',
  studentId: 'student',
  description: 'Matematik ders ücreti',
  currency: 'TRY',
  expectedAmount: 123456789,
  collectedAmount: 0,
  outstandingAmount: 123456789,
  status: 'Pending',
);

void main() {
  setUpAll(() => initializeDateFormatting('tr_TR'));

  Future<void> openSheet(
    WidgetTester tester,
    Widget sheet, {
    double textScale = 1.5,
    double keyboardInset = 0,
    ValueChanged<Object?>? onResult,
  }) async {
    tester.view.physicalSize = const Size(320, 568);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('tr'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(context).copyWith(
            textScaler: TextScaler.linear(textScale),
            viewInsets: EdgeInsets.only(bottom: keyboardInset),
          ),
          child: child!,
        ),
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () async {
                final result = await showModalBottomSheet<Object>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => sheet,
                );
                onResult?.call(result);
              },
              child: const Text('Aç'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Aç'));
    await tester.pumpAndSettle();
  }

  testWidgets(
    'collection stays usable on narrow screen with keyboard and large text',
    (tester) async {
      await openSheet(
        tester,
        const CollectPaymentSheet(record: _record),
        keyboardInset: 260,
      );
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Tahsilatı Kaydet'));
      await tester.pumpAndSettle();
      expect(find.text('Tahsilatı Kaydet').hitTestable(), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'filter handles long student names and large text without overflow',
    (tester) async {
      await openSheet(
        tester,
        const PaymentFilterSheet(
          filters: PaymentFilters(studentId: 'student'),
          students: [
            StudentProfile(
              id: 'student',
              fullName: 'Çok Uzun İsimli Öğrenci Ayşe Zeynep Demir',
              gradeLevel: '8',
              origin: 'TeacherCreated',
              subjects: [],
            ),
          ],
        ),
        textScale: 2,
      );
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Uygula'));
      await tester.pumpAndSettle();
      expect(find.text('Uygula').hitTestable(), findsOneWidget);
    },
  );

  testWidgets(
    'filter preserves a selected student missing from refreshed options',
    (tester) async {
      await openSheet(
        tester,
        const PaymentFilterSheet(
          filters: PaymentFilters(
            studentId: 'archived',
            studentLabel: 'Arşivlenen öğrenci',
          ),
          students: [],
        ),
      );
      expect(tester.takeException(), isNull);
      expect(find.text('Arşivlenen öğrenci'), findsWidgets);
    },
  );

  testWidgets('reversed date filter stays open and explains correction', (
    tester,
  ) async {
    await openSheet(
      tester,
      PaymentFilterSheet(
        filters: PaymentFilters(
          dateFromUtc: DateTime.utc(2026, 10, 20),
          dateToUtc: DateTime.utc(2026, 10, 1),
        ),
        students: const [],
      ),
    );
    await tester.ensureVisible(find.text('Uygula'));
    await tester.tap(find.text('Uygula'));
    await tester.pumpAndSettle();
    expect(
      find.text('Bitiş tarihi başlangıç tarihinden önce olamaz.'),
      findsOneWidget,
    );
    expect(find.byType(PaymentFilterSheet), findsOneWidget);
  });

  testWidgets(
    'selected local days keep their labels and submit complete UTC day boundaries',
    (tester) async {
      PaymentFilters? submitted;
      await openSheet(
        tester,
        const PaymentFilterSheet(filters: PaymentFilters(), students: []),
        textScale: 1,
        onResult: (value) => submitted = value as PaymentFilters?,
      );
      final now = DateTime.now();
      final firstDay = DateTime(now.year, now.month, 10);
      final lastDay = DateTime(now.year, now.month, 12);

      await tester.tap(find.text('Başlangıç'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('10').last);
      await tester.tap(
        find.text(
          MaterialLocalizations.of(
            tester.element(find.byType(DatePickerDialog)),
          ).okButtonLabel,
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bitiş'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('12').last);
      await tester.tap(
        find.text(
          MaterialLocalizations.of(
            tester.element(find.byType(DatePickerDialog)),
          ).okButtonLabel,
        ),
      );
      await tester.pumpAndSettle();

      expect(
        find.text(DateFormat('d MMM y', 'tr_TR').format(firstDay)),
        findsOneWidget,
      );
      expect(
        find.text(DateFormat('d MMM y', 'tr_TR').format(lastDay)),
        findsOneWidget,
      );
      await tester.tap(find.text('Uygula'));
      await tester.pumpAndSettle();
      expect(submitted!.dateFromUtc, firstDay.toUtc());
      expect(
        submitted!.dateToUtc,
        DateTime(
          lastDay.year,
          lastDay.month,
          lastDay.day + 1,
        ).subtract(const Duration(microseconds: 1)).toUtc(),
      );
      expect(submitted!.dateToUtc!.toLocal().day, lastDay.day);
      expect(tester.takeException(), isNull);
    },
  );

  for (final yearOffset in [-10, 10]) {
    testWidgets(
      'date picker accepts retained filter outside picker range ($yearOffset years)',
      (tester) async {
        final oldDay = DateTime(DateTime.now().year + yearOffset, 6, 15);
        await openSheet(
          tester,
          PaymentFilterSheet(
            filters: PaymentFilters(dateFromUtc: oldDay.toUtc()),
            students: const [],
          ),
          textScale: 1,
        );
        await tester.tap(
          find.text(DateFormat('d MMM y', 'tr_TR').format(oldDay)),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(find.byType(CalendarDatePicker), findsOneWidget);
        final picker = tester.widget<CalendarDatePicker>(
          find.byType(CalendarDatePicker),
        );
        expect(
          picker.initialDate,
          yearOffset < 0 ? picker.firstDate : picker.lastDate,
        );
      },
    );
  }
}
