import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  PaymentRecord pendingRecord() => const PaymentRecordModel(
    id: '11111111-1111-1111-1111-111111111111',
    teacherUserId: 'teacher-1',
    studentId: 'student-1',
    description: 'Haziran — Matematik',
    currency: 'TRY',
    expectedAmount: 900,
    collectedAmount: 0,
    outstandingAmount: 900,
    status: 'Pending',
    itemType: 'LessonFee',
  );

  test(
    'collect (full outstanding) marks the record as Paid (enum round-trip)',
    () async {
      final cubit = PaymentsCubit(_BackendMimicRepository(pendingRecord()));

      await cubit.load('teacher-1');
      final emitted = <PaymentsState>[];
      final sub = cubit.stream.listen(emitted.add);
      // Kalanın tamamı (900) tahsil edilir → Paid.
      await cubit.collect(pendingRecord(), 900);
      await sub.cancel();

      // Tıklanan kayda özel yükleniyor göstergesi için ara durumda savingRecordId set olmalı.
      expect(
        emitted.any(
          (s) => s.isSaving && s.savingRecordId == pendingRecord().id,
        ),
        isTrue,
      );

      // Tamamı alınınca kart "Ödendi"ye geçmeli. Enum eşlemesi hatalıysa
      // backend'i taklit eden repo "PartiallyPaid" döner ve bu test kırmızıya döner.
      expect(cubit.state.records.single.status, 'Paid');
      expect(cubit.state.records.single.collectedAmount, 900);
      expect(cubit.state.isSaving, isFalse);
      expect(cubit.state.savingRecordId, isNull);
      expect(cubit.state.successMessage, isNotNull);

      await cubit.close();
    },
  );

  test('collect (partial) marks the record as PartiallyPaid', () async {
    final cubit = PaymentsCubit(_BackendMimicRepository(pendingRecord()));

    await cubit.load('teacher-1');
    // 900 beklenen, 0 tahsil → 400 alınırsa kısmi tahsilat.
    await cubit.collect(pendingRecord(), 400);

    expect(cubit.state.records.single.status, 'PartiallyPaid');
    expect(cubit.state.records.single.collectedAmount, 400);
    expect(cubit.state.records.single.outstandingAmount, 500);
    expect(cubit.state.successMessage, isNotNull);

    await cubit.close();
  });

  test('collect sends the requested delta without a client-side cap', () async {
    final repo = _BackendMimicRepository(pendingRecord());
    final cubit = PaymentsCubit(repo);
    await cubit.load('teacher-1');
    await cubit.collect(pendingRecord(), 5000);
    expect(repo.lastDelta, 5000);
    expect(cubit.state.errorMessage, isNotNull);
    expect(cubit.state.records.single.collectedAmount, 0);
    await cubit.close();
  });

  test('update replaces the edited record and sets success message', () async {
    final cubit = PaymentsCubit(_BackendMimicRepository(pendingRecord()));

    await cubit.load('teacher-1');
    await cubit.update(
      const PaymentRecordModel(
        id: '11111111-1111-1111-1111-111111111111',
        teacherUserId: 'teacher-1',
        studentId: 'student-1',
        description: 'Haziran — Matematik (güncellendi)',
        currency: 'TRY',
        expectedAmount: 1000,
        collectedAmount: 0,
        outstandingAmount: 1000,
        status: 'Pending',
        itemType: 'LessonFee',
      ),
    );

    expect(cubit.state.records.single.expectedAmount, 1000);
    expect(
      cubit.state.records.single.description,
      'Haziran — Matematik (güncellendi)',
    );
    expect(cubit.state.isSaving, isFalse);
    expect(cubit.state.successMessage, isNotNull);

    await cubit.close();
  });

  test(
    'collect resets isSaving even on unexpected (non-ApiException) error',
    () async {
      final cubit = PaymentsCubit(_ThrowingRepository());

      await cubit.collect(pendingRecord(), 900);

      // Buton yeniden etkinleşebilsin diye isSaving mutlaka false olmalı.
      expect(cubit.state.isSaving, isFalse);
      expect(cubit.state.errorMessage, isNotNull);

      await cubit.close();
    },
  );

  test(
    'cancel marks the record as Cancelled and keeps it in the list',
    () async {
      final cubit = PaymentsCubit(_BackendMimicRepository(pendingRecord()));

      await cubit.load('teacher-1');
      await cubit.cancel(pendingRecord());

      // Kayıt silinmez; "Cancelled" olarak listede kalır.
      expect(cubit.state.records.single.status, 'Cancelled');
      expect(cubit.state.isSaving, isFalse);
      expect(cubit.state.successMessage, isNotNull);

      await cubit.close();
    },
  );

  test('cancel resets isSaving on unexpected error', () async {
    final cubit = PaymentsCubit(_ThrowingRepository());

    await cubit.cancel(pendingRecord());

    expect(cubit.state.isSaving, isFalse);
    expect(cubit.state.errorMessage, isNotNull);

    await cubit.close();
  });
}

/// Mobil `toUpsertPayload()` ile int gönderir; backend enum'u ada çevirip döndürür.
/// Bu repo o davranışı taklit ederek status eşlemesini uçtan uca doğrular.
class _BackendMimicRepository implements PaymentRepository {
  int? lastDelta;
  @override
  Future<PaymentRecord> addCollection(
    String id,
    int amount, {
    int method = 1,
    required DateTime occurredOnUtc,
    String? note,
    String? idempotencyKey,
  }) async {
    lastDelta = amount;
    if (amount > _current.outstandingAmount) {
      throw const ApiException(message: 'payments.amount_exceeds_outstanding');
    }
    return _current = PaymentRecordModel(
      id: _current.id,
      teacherUserId: _current.teacherUserId,
      studentId: _current.studentId,
      description: _current.description,
      currency: _current.currency,
      expectedAmount: _current.expectedAmount,
      collectedAmount: _current.collectedAmount + amount,
      outstandingAmount: _current.outstandingAmount - amount,
      status: _current.outstandingAmount == amount ? 'Paid' : 'PartiallyPaid',
    );
  }

  @override
  Future<PaymentRecord> cancelRecord(String id) async =>
      _current = PaymentRecordModel(
        id: _current.id,
        teacherUserId: _current.teacherUserId,
        studentId: _current.studentId,
        description: _current.description,
        currency: _current.currency,
        expectedAmount: _current.expectedAmount,
        collectedAmount: _current.collectedAmount,
        outstandingAmount: 0,
        status: 'Cancelled',
      );
  _BackendMimicRepository(this._current);

  PaymentRecord _current;

  static String _statusName(int value) => switch (value) {
    2 => 'PartiallyPaid',
    3 => 'Paid',
    4 => 'Overdue',
    5 => 'Cancelled',
    _ => 'Pending',
  };

  @override
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord) async {
    final model = PaymentRecordModel(
      id: paymentRecord.id,
      teacherUserId: paymentRecord.teacherUserId,
      studentId: paymentRecord.studentId,
      description: paymentRecord.description,
      currency: paymentRecord.currency,
      expectedAmount: paymentRecord.expectedAmount,
      collectedAmount: paymentRecord.collectedAmount,
      outstandingAmount: paymentRecord.outstandingAmount,
      status: paymentRecord.status,
      relatedLessonSessionId: paymentRecord.relatedLessonSessionId,
      dueDateUtc: paymentRecord.dueDateUtc,
      collectedOnUtc: paymentRecord.collectedOnUtc,
      notes: paymentRecord.notes,
      isOverdue: paymentRecord.isOverdue,
      itemType: paymentRecord.itemType,
    );
    final payload = model.toUpsertPayload();
    final serverStatus = _statusName(payload['status'] as int);
    _current = PaymentRecordModel(
      id: paymentRecord.id,
      teacherUserId: paymentRecord.teacherUserId,
      studentId: paymentRecord.studentId,
      description: paymentRecord.description,
      currency: paymentRecord.currency,
      expectedAmount: paymentRecord.expectedAmount,
      collectedAmount: paymentRecord.collectedAmount,
      outstandingAmount: paymentRecord.outstandingAmount,
      status: serverStatus,
      dueDateUtc: paymentRecord.dueDateUtc,
      collectedOnUtc: paymentRecord.collectedOnUtc,
      itemType: paymentRecord.itemType,
    );
    return _current;
  }

  @override
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord) async =>
      paymentRecord;

  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async =>
      <PaymentRecord>[_current];

  @override
  Future<PaymentPage> searchRecords(
    String teacherUserId, {
    required PaymentFilters filters,
    required int skip,
    required int take,
  }) async => PaymentPage(items: <PaymentRecord>[_current], totalCount: 1);

  @override
  Future<PaymentSummary> getSummary(String teacherUserId) async =>
      const PaymentSummary(totalRecords: 1, currencySummaries: []);
}

class _ThrowingRepository implements PaymentRepository {
  @override
  Future<PaymentRecord> addCollection(
    String id,
    int amount, {
    int method = 1,
    required DateTime occurredOnUtc,
    String? note,
    String? idempotencyKey,
  }) async => throw UnimplementedError();
  @override
  Future<PaymentRecord> cancelRecord(String id) async =>
      throw UnimplementedError();

  @override
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord) async =>
      throw StateError('unexpected');

  @override
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord) async =>
      throw const ApiException(message: 'boom');

  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async =>
      <PaymentRecord>[];

  @override
  Future<PaymentPage> searchRecords(
    String teacherUserId, {
    required PaymentFilters filters,
    required int skip,
    required int take,
  }) async => const PaymentPage(items: <PaymentRecord>[], totalCount: 0);

  @override
  Future<PaymentSummary> getSummary(String teacherUserId) async =>
      const PaymentSummary(totalRecords: 0, currencySummaries: []);
}
