import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';

/// M17 uçlarının Dio üzerinden okunması. Hedef kullanıcı **oturumdan** gelir;
/// uçlar `{userId}` almaz (başkasının aboneliğine dokunmanın yolu sözleşme düzeyinde kapalı).
class MembershipRepositoryImpl implements MembershipRepository {
  MembershipRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  static const String _base = '/api/membership';

  static int _int(dynamic value) =>
      value is num ? value.toInt() : int.tryParse('$value') ?? 0;

  static double _double(dynamic value) =>
      value is num ? value.toDouble() : double.tryParse('$value') ?? 0;

  static DateTime? _dateOrNull(dynamic value) =>
      value == null ? null : DateTime.tryParse('$value');

  static String? _stringOrNull(dynamic value) {
    if (value == null) {
      return null;
    }
    final text = '$value'.trim();
    return text.isEmpty ? null : text;
  }

  static MembershipEntitlement _entitlement(Map<String, dynamic>? json) {
    if (json == null) {
      return MembershipEntitlement.free;
    }
    return MembershipEntitlement(
      tier: '${json['tier']}',
      effectiveUntilUtc: _dateOrNull(json['effectiveUntilUtc']),
      maxStudents: _int(json['maxStudents']),
      canSeeAds: json['canSeeAds'] as bool? ?? true,
      detailedAnalytics: json['detailedAnalytics'] as bool? ?? false,
      pdfReports: json['pdfReports'] as bool? ?? false,
      richNotifications: json['richNotifications'] as bool? ?? false,
    );
  }

  static MembershipSnapshot _snapshot(Map<String, dynamic> json) =>
      MembershipSnapshot(
        tier: '${json['tier']}',
        effectiveUntilUtc: _dateOrNull(json['effectiveUntilUtc']),
        planCode: _stringOrNull(json['planCode']),
        subscriptionStatus: _stringOrNull(json['status']),
        entitlement: _entitlement(json['entitlement'] as Map<String, dynamic>?),
      );

  static MembershipPlan _plan(Map<String, dynamic> json) => MembershipPlan(
    id: '${json['id']}',
    code: '${json['code']}',
    role: '${json['role']}',
    tier: '${json['tier']}',
    period: '${json['period']}',
    price: _double(json['price']),
    currency: '${json['currency']}',
    trialDays: _int(json['trialDays']),
    isActive: json['isActive'] as bool? ?? false,
  );

  @override
  Future<MembershipSnapshot> me() async =>
      _snapshot(await _apiClient.get('$_base/me'));

  @override
  Future<List<MembershipPlan>> listPlans({String? role}) async {
    final rows = await _apiClient.getList(
      '$_base/plans',
      queryParameters: role == null || role.isEmpty
          ? null
          : <String, dynamic>{'role': role},
    );
    return rows.whereType<Map<String, dynamic>>().map(_plan).toList();
  }

  @override
  Future<MembershipSnapshot> startTrial(String planCode) async => _snapshot(
    await _apiClient.post(
      '$_base/subscriptions/start-trial',
      data: <String, dynamic>{'planCode': planCode},
    ),
  );

  @override
  Future<MembershipSnapshot> cancel() async =>
      _snapshot(await _apiClient.post('$_base/subscriptions/cancel'));

  @override
  Future<ReferralCode> referralCode() async {
    final json = await _apiClient.get('$_base/referral-code');
    return ReferralCode(
      code: '${json['code']}',
      redemptionCount: _int(json['redemptionCount']),
      rewardMonths: _int(json['rewardMonths']),
    );
  }

  @override
  Future<MembershipSnapshot> redeemReferral(String code) async => _snapshot(
    await _apiClient.post(
      '$_base/referral/redeem',
      data: <String, dynamic>{'code': code},
    ),
  );
}
