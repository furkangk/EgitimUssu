import 'dart:convert';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/money/money.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';

class PaymentRepositoryImpl implements PaymentRepository {
  @override
  Future<PaymentRecord> addCollection(
    String id,
    int amount, {
    int method = 1,
    required DateTime occurredOnUtc,
    String? note,
    String? idempotencyKey,
  }) async => PaymentRecordModel.fromJson(
    await _apiClient.post(
      '/api/payments/records/$id/collections',
      idempotent: true,
      idempotencyKey: idempotencyKey,
      retryIdentity: jsonEncode([id, amount, method, note]),
      data: {
        'amount': Money(amount).toJsonAmount(),
        'method': method,
        'occurredOnUtc': occurredOnUtc.toUtc().toIso8601String(),
        'note': note,
      },
    ),
  );
  @override
  Future<PaymentRecord> cancelRecord(String id) async =>
      PaymentRecordModel.fromJson(
        await _apiClient.post(
          '/api/payments/records/$id/cancel',
          idempotent: true,
        ),
      );

  PaymentRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
    required LocalCache localCache,
    CacheStore? cacheStore,
  }) : _apiClient = apiClient,
       _config = config,
       _cacheStore = cacheStore ?? CacheStore(localCache);

  final ApiClient _apiClient;
  final AppConfig _config;
  final CacheStore _cacheStore;

  @override
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord) async {
    final model = _toModel(paymentRecord);
    try {
      final response = await _apiClient.post(
        '/api/payments/records',
        idempotent: true,
        retryIdentity: jsonEncode(
          model.toUpsertPayload()..remove('collectedOnUtc'),
        ),
        data: model.toUpsertPayload(),
      );
      return PaymentRecordModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('payments')) {
        return PaymentRecordModel.demo(
          id: DateTime.now().microsecondsSinceEpoch.toString(),
          teacherUserId: paymentRecord.teacherUserId,
          studentId: paymentRecord.studentId,
          description: paymentRecord.description,
          expectedAmount: paymentRecord.expectedAmount,
          collectedAmount: paymentRecord.collectedAmount,
          dueDateUtc: paymentRecord.dueDateUtc,
        );
      }
      rethrow;
    }
  }

  @override
  Future<PaymentSummary> getSummary(String teacherUserId) async {
    if (_config.isMockFallbackEnabled('payments')) {
      return PaymentSummaryModel.demo();
    }
    return (await _cacheStore.load<PaymentSummary>(
      feature: 'payments',
      key: 'summary.v2.$teacherUserId',
      fetch: () =>
          _apiClient.get('/api/payments/teachers/$teacherUserId/summary'),
      decode: (raw) =>
          PaymentSummaryModel.fromJson(raw as Map<String, dynamic>),
    )).value;
  }

  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async {
    if (_config.isMockFallbackEnabled('payments')) {
      return _mockRecords(teacherUserId);
    }
    return (await _cacheStore.load<List<PaymentRecord>>(
      feature: 'payments',
      key: 'records.$teacherUserId',
      fetch: () => _apiClient.getList(
        '/api/payments/teachers/$teacherUserId/records',
        queryParameters: {'outstandingOnly': false},
      ),
      decode: (raw) => (raw as List)
          .cast<Map<String, dynamic>>()
          .map(PaymentRecordModel.fromJson)
          .toList(),
    )).value;
  }

  @override
  Future<PaymentPage> searchRecords(
    String teacherUserId, {
    required PaymentFilters filters,
    required int skip,
    required int take,
  }) async {
    if (_config.isMockFallbackEnabled('payments')) {
      return _mockPage(teacherUserId, filters, skip, take);
    }
    final params = <String, dynamic>{'skip': skip, 'take': take};
    final query = filters.query.trim();
    if (query.isNotEmpty) params['q'] = query;
    if (filters.status != null) params['status'] = filters.status;
    if (filters.studentId != null) params['studentId'] = filters.studentId;
    if (filters.dateFromUtc != null) {
      params['dateFromUtc'] = filters.dateFromUtc!.toIso8601String();
    }
    if (filters.dateToUtc != null) {
      params['dateToUtc'] = filters.dateToUtc!.toIso8601String();
    }
    try {
      final response = await _apiClient.get(
        '/api/payments/teachers/$teacherUserId/records/search',
        queryParameters: params,
      );
      final items = (response['items'] as List<dynamic>? ?? <dynamic>[])
          .whereType<Map<String, dynamic>>()
          .map(PaymentRecordModel.fromJson)
          .toList();
      final total = response['totalCount'] as int? ?? items.length;
      return PaymentPage(items: items, totalCount: total);
    } on ApiException {
      if (_config.isMockFallbackEnabled('payments')) {
        return _mockPage(teacherUserId, filters, skip, take);
      }
      rethrow;
    }
  }

  PaymentPage _mockPage(
    String teacherUserId,
    PaymentFilters filters,
    int skip,
    int take,
  ) {
    final all = _filterMock(_mockRecords(teacherUserId), filters);
    return PaymentPage(
      items: all.skip(skip).take(take).toList(),
      totalCount: all.length,
    );
  }

  List<PaymentRecord> _filterMock(List<PaymentRecord> all, PaymentFilters f) {
    final query = f.query.trim().toLowerCase();
    return all.where((r) {
      if (query.isNotEmpty && !r.description.toLowerCase().contains(query)) {
        return false;
      }
      if (f.studentId != null && r.studentId != f.studentId) return false;
      final due = r.dueDateUtc;
      if (f.dateFromUtc != null &&
          due != null &&
          due.isBefore(f.dateFromUtc!)) {
        return false;
      }
      if (f.dateToUtc != null && due != null && due.isAfter(f.dateToUtc!)) {
        return false;
      }
      if (f.status != null && !_matchesStatus(r, f.status!)) return false;
      return true;
    }).toList();
  }

  bool _matchesStatus(PaymentRecord r, String status) {
    switch (status) {
      case 'Open':
        return r.outstandingAmount > 0 && !r.isOverdue;
      case 'Overdue':
        return r.isOverdue;
      case 'Pending':
        return r.status == 'Pending' && !r.isOverdue;
      case 'PartiallyPaid':
        return r.status == 'PartiallyPaid' && !r.isOverdue;
      case 'Paid':
        return r.status == 'Paid';
      case 'Cancelled':
        return r.status == 'Cancelled';
      default:
        return true;
    }
  }

  List<PaymentRecord> _mockRecords(String teacherUserId) {
    final now = DateTime.now().toUtc();
    return [
      PaymentRecordModel.demo(
        id: 'payment-1',
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        description: 'Haziran — Matematik (4 ders)',
        expectedAmount: 120000,
        collectedAmount: 120000,
        dueDateUtc: now.subtract(const Duration(days: 3)),
      ),
      PaymentRecordModel.demo(
        id: 'payment-2',
        teacherUserId: teacherUserId,
        studentId: 'student-2',
        description: 'Haziran — Fizik (3 ders)',
        expectedAmount: 90000,
        collectedAmount: 0,
        dueDateUtc: now.subtract(const Duration(days: 5)),
      ),
      PaymentRecordModel.demo(
        id: 'payment-3',
        teacherUserId: teacherUserId,
        studentId: 'student-3',
        description: 'Mayıs + Haziran — Biyoloji',
        expectedAmount: 70000,
        collectedAmount: 0,
        dueDateUtc: now.subtract(const Duration(days: 12)),
      ),
      PaymentRecordModel.demo(
        id: 'payment-4',
        teacherUserId: teacherUserId,
        studentId: 'student-4',
        description: 'Temmuz — Matematik (4 ders)',
        expectedAmount: 80000,
        collectedAmount: 0,
        dueDateUtc: now.add(const Duration(days: 5)),
      ),
      PaymentRecordModel.demo(
        id: 'payment-5',
        teacherUserId: teacherUserId,
        studentId: 'student-5',
        description: 'Temmuz — TYT Paketi',
        expectedAmount: 150000,
        collectedAmount: 75000,
        dueDateUtc: now.add(const Duration(days: 10)),
      ),
    ];
  }

  @override
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord) async {
    final model = _toModel(paymentRecord);
    try {
      final response = await _apiClient.put(
        '/api/payments/records/${paymentRecord.id}',
        data: model.toDetailsPayload(),
      );
      return PaymentRecordModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('payments')) {
        return model;
      }
      rethrow;
    }
  }

  PaymentRecordModel _toModel(PaymentRecord paymentRecord) {
    return PaymentRecordModel(
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
  }
}
