import 'dart:convert';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';

class PaymentRepositoryImpl implements PaymentRepository {
  PaymentRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
    required LocalCache localCache,
  }) : _apiClient = apiClient,
       _config = config,
       _localCache = localCache;

  final ApiClient _apiClient;
  final AppConfig _config;
  final LocalCache _localCache;

  @override
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord) async {
    final model = _toModel(paymentRecord);
    try {
      final response = await _apiClient.post(
        '/api/payments/records',
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
    final cacheKey = 'payments.summary.$teacherUserId';
    try {
      final response = await _apiClient.get(
        '/api/payments/teachers/$teacherUserId/summary',
      );
      await _localCache.writeString(cacheKey, jsonEncode(response));
      return PaymentSummaryModel.fromJson(response);
    } on ApiException {
      final cached = await _readCachedSummary(cacheKey);
      if (cached != null) {
        return cached;
      }
      if (_config.isMockFallbackEnabled('payments')) {
        return PaymentSummaryModel.demo();
      }
      rethrow;
    }
  }

  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async {
    final cacheKey = 'payments.records.$teacherUserId';
    try {
      final response = await _apiClient.getList(
        '/api/payments/teachers/$teacherUserId/records',
        queryParameters: <String, dynamic>{'outstandingOnly': false},
      );
      await _localCache.writeString(cacheKey, jsonEncode(response));
      return response
          .whereType<Map<String, dynamic>>()
          .map(PaymentRecordModel.fromJson)
          .toList();
    } on ApiException {
      final cached = await _readCachedRecords(cacheKey);
      if (cached.isNotEmpty) {
        return cached;
      }
      if (_config.isMockFallbackEnabled('payments')) {
        return <PaymentRecord>[
          PaymentRecordModel.demo(
            id: 'payment-1',
            teacherUserId: teacherUserId,
            studentId: 'student-1',
            description: 'Matematik dersi',
            expectedAmount: 750,
            collectedAmount: 0,
            dueDateUtc: DateTime.now().toUtc().add(const Duration(days: 2)),
          ),
          PaymentRecordModel.demo(
            id: 'payment-2',
            teacherUserId: teacherUserId,
            studentId: 'student-2',
            description: 'Geometri dersi',
            expectedAmount: 900,
            collectedAmount: 900,
            dueDateUtc: DateTime.now().toUtc().subtract(
              const Duration(days: 1),
            ),
          ),
        ];
      }
      rethrow;
    }
  }

  @override
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord) async {
    final model = _toModel(paymentRecord);
    try {
      final response = await _apiClient.put(
        '/api/payments/records/${paymentRecord.id}',
        data: model.toUpsertPayload(),
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

  Future<List<PaymentRecord>> _readCachedRecords(String cacheKey) async {
    final cached = await _localCache.readString(cacheKey);
    if (cached == null || cached.isEmpty) {
      return const <PaymentRecord>[];
    }
    final decoded = jsonDecode(cached);
    if (decoded is! List<dynamic>) {
      return const <PaymentRecord>[];
    }
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(PaymentRecordModel.fromJson)
        .toList();
  }

  Future<PaymentSummary?> _readCachedSummary(String cacheKey) async {
    final cached = await _localCache.readString(cacheKey);
    if (cached == null || cached.isEmpty) {
      return null;
    }
    final decoded = jsonDecode(cached);
    if (decoded is! Map<String, dynamic>) {
      return null;
    }
    return PaymentSummaryModel.fromJson(decoded);
  }
}
