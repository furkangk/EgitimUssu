import 'dart:typed_data';

import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/cubit/teacher_profile_cubit.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/widgets/availability_editor.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// P06 Task 8 (D-04 / M02-5): Öğretmen uygunluk düzenleyicisi.
/// Kritik sözleşme: aynı gün içinde **kesişen** aralık eklenemez; her slot kendi
/// "Online" / "Yüz yüze" bayrağını taşır (profil geneli değil).
void main() {
  group('availabilitySlotOverlaps', () {
    test('ayni gunde kesisen aralik cakisir', () {
      expect(
        availabilitySlotOverlaps(
          <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
          _slot(day: 1, start: '19:00:00', end: '21:00:00'),
        ),
        isTrue,
      );
    });

    test('bitisik aralik (20:00 baslangic) cakismaz', () {
      expect(
        availabilitySlotOverlaps(
          <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
          _slot(day: 1, start: '20:00:00', end: '21:00:00'),
        ),
        isFalse,
      );
    });

    test('farkli gun cakismaz', () {
      expect(
        availabilitySlotOverlaps(
          <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
          _slot(day: 2, start: '18:00:00', end: '20:00:00'),
        ),
        isFalse,
      );
    });

    test('duzenlenen slotun kendisi cakisma sayilmaz', () {
      final existing = _slot(day: 1, start: '18:00:00', end: '20:00:00');
      expect(
        availabilitySlotOverlaps(
          <TeacherAvailability>[existing],
          _slot(day: 1, start: '18:30:00', end: '20:30:00'),
          ignore: existing,
        ),
        isFalse,
      );
    });
  });

  group('availabilitySlotError', () {
    test('bitis saati baslangictan onceyse hata doner', () {
      expect(
        availabilitySlotError(
          const <TeacherAvailability>[],
          _slot(day: 2, start: '20:00:00', end: '08:00:00'),
        ),
        isNotNull,
      );
    });

    test('bitis saati baslangica esitse hata doner', () {
      expect(
        availabilitySlotError(
          const <TeacherAvailability>[],
          _slot(day: 2, start: '18:00:00', end: '18:00:00'),
        ),
        isNotNull,
      );
    });

    test('cakisan aralik hata doner', () {
      expect(
        availabilitySlotError(
          <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
          _slot(day: 1, start: '19:00:00', end: '21:00:00'),
        ),
        isNotNull,
      );
    });

    test('gecerli aralik null doner', () {
      expect(
        availabilitySlotError(
          <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
          _slot(day: 1, start: '20:00:00', end: '21:00:00'),
        ),
        isNull,
      );
    });
  });

  group('AvailabilityEditor', () {
    testWidgets('haftanin yedi gunu de listelenir', (tester) async {
      await _pumpEditor(tester, slots: const <TeacherAvailability>[]);

      for (final day in availabilityDayNames) {
        expect(find.text(day), findsOneWidget);
      }
    });

    testWidgets('var olan slot kendi gununde gorunur', (tester) async {
      await _pumpEditor(
        tester,
        slots: <TeacherAvailability>[_slot(day: 3, start: '19:00:00', end: '21:00:00')],
      );

      expect(find.text('19:00 - 21:00'), findsOneWidget);
    });

    testWidgets('slot eklenince listede gorunur', (tester) async {
      final captured = <List<TeacherAvailability>>[];
      await _pumpEditor(
        tester,
        slots: const <TeacherAvailability>[],
        onChanged: captured.add,
      );

      await tester.tap(find.byKey(const Key('availability-add-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('availability-confirm')));
      await tester.pumpAndSettle();

      expect(captured, hasLength(1));
      expect(captured.single, hasLength(1));
      expect(captured.single.single.dayOfWeek, 1);
      expect(captured.single.single.startTime, '18:00:00');
      expect(captured.single.single.endTime, '20:00:00');
      expect(find.text('18:00 - 20:00'), findsOneWidget);
    });

    testWidgets('cakisan slot eklenemez, hata gosterilir', (tester) async {
      final captured = <List<TeacherAvailability>>[];
      await _pumpEditor(
        tester,
        slots: <TeacherAvailability>[_slot(day: 1, start: '18:00:00', end: '20:00:00')],
        onChanged: captured.add,
      );

      await tester.tap(find.byKey(const Key('availability-add-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('availability-confirm')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('availability-error')), findsOneWidget);
      expect(captured, isEmpty);
    });

    testWidgets('slot silinince listeden cikar', (tester) async {
      final captured = <List<TeacherAvailability>>[];
      await _pumpEditor(
        tester,
        slots: <TeacherAvailability>[
          _slot(day: 1, start: '18:00:00', end: '20:00:00'),
          _slot(day: 1, start: '20:00:00', end: '21:00:00'),
        ],
        onChanged: captured.add,
      );

      await tester.tap(find.byKey(const Key('availability-delete-1-18:00:00')));
      await tester.pumpAndSettle();

      expect(captured, hasLength(1));
      expect(captured.single, hasLength(1));
      expect(captured.single.single.startTime, '20:00:00');
    });

    testWidgets('slot bazli online anahtari degistirilebilir', (tester) async {
      final captured = <List<TeacherAvailability>>[];
      await _pumpEditor(
        tester,
        slots: <TeacherAvailability>[
          _slot(day: 4, start: '10:00:00', end: '12:00:00', online: false),
        ],
        onChanged: captured.add,
      );

      await tester.tap(find.byKey(const Key('availability-online-4-10:00:00')));
      await tester.pumpAndSettle();

      expect(captured, hasLength(1));
      expect(captured.single.single.isOnlineAvailable, isTrue);
      expect(captured.single.single.isInPersonAvailable, isTrue);
    });

    testWidgets('kaydet dugmesi onSave cagirir', (tester) async {
      var saved = 0;
      await _pumpEditor(
        tester,
        slots: const <TeacherAvailability>[],
        onSave: () => saved++,
      );

      await tester.ensureVisible(find.byKey(const Key('availability-save')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('availability-save')));
      await tester.pumpAndSettle();

      expect(saved, 1);
    });

    testWidgets('kaydederken dugme pasiflesir', (tester) async {
      var saved = 0;
      await _pumpEditor(
        tester,
        slots: const <TeacherAvailability>[],
        onSave: () => saved++,
        isSaving: true,
      );

      await tester.ensureVisible(find.byKey(const Key('availability-save')));
      await tester.pump();
      await tester.tap(
        find.byKey(const Key('availability-save')),
        warnIfMissed: false,
      );
      await tester.pump();

      expect(saved, 0);
    });
  });

  group('TeacherProfileCubit.saveAvailability', () {
    test('yuklu profili koruyup yeni slotlarla PUT eder', () async {
      final repository = _FakeTeacherRepository();
      final cubit = TeacherProfileCubit(repository);
      await cubit.load('t-1');

      await cubit.saveAvailability(<TeacherAvailability>[
        _slot(day: 5, start: '09:00:00', end: '10:00:00'),
      ]);

      final sent = repository.updated!;
      expect(sent.userId, 't-1');
      expect(sent.fullName, 'Ayşe Yılmaz');
      expect(sent.availabilitySlots, hasLength(1));
      expect(sent.availabilitySlots.single.dayOfWeek, 5);
      expect(cubit.state.successMessage, 'Uygunluk saatleri kaydedildi.');
      await cubit.close();
    });

    test('profil yuklenmeden kaydetmeye calisirsa istek gitmez', () async {
      final repository = _FakeTeacherRepository();
      final cubit = TeacherProfileCubit(repository);

      await cubit.saveAvailability(const <TeacherAvailability>[]);

      expect(repository.updated, isNull);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });
  });
}

class _FakeTeacherRepository implements TeacherRepository {
  TeacherProfile? updated;

  @override
  Future<TeacherProfile> getProfile(String userId) async {
    return TeacherProfile(
      id: 'p-1',
      userId: userId,
      fullName: 'Ayşe Yılmaz',
      subject: 'Matematik',
      city: 'İstanbul',
      district: 'Kadıköy',
      lessonFormat: 'OnlineAndInPerson',
      experienceYears: 8,
      educationLevel: 'Lisans',
      hourlyRateAmount: 1250,
      currency: 'TRY',
      availabilitySlots: <TeacherAvailability>[
        _slot(day: 1, start: '18:00:00', end: '20:00:00'),
      ],
    );
  }

  @override
  Future<TeacherProfile> updateProfile(TeacherProfile profile) async {
    updated = profile;
    return profile;
  }

  @override
  Future<TeacherProfile> createProfile(TeacherProfile profile) async =>
      throw UnimplementedError();

  @override
  Future<String> uploadProfilePhoto({
    required String userId,
    required String filePath,
  }) async => throw UnimplementedError();

  @override
  Future<Uint8List?> downloadProfilePhoto(String userId) async => null;
}

TeacherAvailability _slot({
  required int day,
  required String start,
  required String end,
  bool online = true,
  bool inPerson = true,
}) {
  return TeacherAvailability(
    dayOfWeek: day,
    startTime: start,
    endTime: end,
    isOnlineAvailable: online,
    isInPersonAvailable: inPerson,
  );
}

Future<void> _pumpEditor(
  WidgetTester tester, {
  required List<TeacherAvailability> slots,
  ValueChanged<List<TeacherAvailability>>? onChanged,
  VoidCallback? onSave,
  bool isSaving = false,
}) async {
  // Editör kontrollü (controlled) bir widget: listeyi sahibi tutar. Test koşumu
  // da gerçek kullanımdaki gibi (profil sayfası) durumu dışarıda tutar.
  var current = List<TeacherAvailability>.of(slots);
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: StatefulBuilder(
          builder: (context, setState) {
            return SingleChildScrollView(
              child: AvailabilityEditor(
                slots: current,
                onChanged: (next) {
                  onChanged?.call(next);
                  setState(() => current = next);
                },
                onSave: onSave,
                isSaving: isSaving,
              ),
            );
          },
        ),
      ),
    ),
  );
  // isSaving: sonsuz donen CircularProgressIndicator pumpAndSettle'i kilitler.
  if (isSaving) {
    await tester.pump();
  } else {
    await tester.pumpAndSettle();
  }
}
