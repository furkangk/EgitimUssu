import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';

/// Ortak `MembershipRepository` sahtesi (mobile_flutter.md §17.1 — sahteler tek noktada).
///
/// Her çağrı için ayrı bir hata alanı vardır: "deneme başlatma 409 döner ama
/// `GET /me` çalışır" gibi gerçek senaryolar tek bir `error` alanıyla kurulamazdı.
class FakeMembershipRepository implements MembershipRepository {
  FakeMembershipRepository({
    MembershipSnapshot? snapshot,
    this.plans = const <MembershipPlan>[],
    this.referral,
    this.meError,
    this.plansError,
    this.trialError,
    this.cancelError,
    this.referralError,
    this.redeemError,
    this.afterTrial,
    this.afterCancel,
    this.afterRedeem,
  }) : snapshot = snapshot ?? MembershipSnapshot.free;

  MembershipSnapshot snapshot;
  final List<MembershipPlan> plans;
  final ReferralCode? referral;

  final ApiException? meError;
  final ApiException? plansError;
  final ApiException? trialError;
  final ApiException? cancelError;
  final ApiException? referralError;
  final ApiException? redeemError;

  /// Komut başarılı olduğunda sunucunun döndüğü yeni durum.
  final MembershipSnapshot? afterTrial;
  final MembershipSnapshot? afterCancel;
  final MembershipSnapshot? afterRedeem;

  final List<String> meCalls = <String>[];
  final List<String?> planCalls = <String?>[];
  final List<String> trialCalls = <String>[];
  final List<String> redeemCalls = <String>[];
  int cancelCalls = 0;
  int referralCalls = 0;

  @override
  Future<MembershipSnapshot> me() async {
    meCalls.add('me');
    if (meError != null) throw meError!;
    return snapshot;
  }

  @override
  Future<List<MembershipPlan>> listPlans({String? role}) async {
    planCalls.add(role);
    if (plansError != null) throw plansError!;
    return plans;
  }

  @override
  Future<MembershipSnapshot> startTrial(String planCode) async {
    trialCalls.add(planCode);
    if (trialError != null) throw trialError!;
    snapshot = afterTrial ?? snapshot;
    return snapshot;
  }

  @override
  Future<MembershipSnapshot> cancel() async {
    cancelCalls++;
    if (cancelError != null) throw cancelError!;
    snapshot = afterCancel ?? snapshot;
    return snapshot;
  }

  @override
  Future<ReferralCode> referralCode() async {
    referralCalls++;
    if (referralError != null) throw referralError!;
    return referral ?? const ReferralCode(code: 'ABC234', redemptionCount: 0, rewardMonths: 1);
  }

  @override
  Future<MembershipSnapshot> redeemReferral(String code) async {
    redeemCalls.add(code);
    if (redeemError != null) throw redeemError!;
    snapshot = afterRedeem ?? snapshot;
    return snapshot;
  }
}
