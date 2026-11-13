import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_membership_repository.dart';

/// P09 Task 6 (D-10/M17-5): Üyelik ekranını ve paywall'ı besleyen durum makinesi.
///
/// Kritik ayrımlar:
/// - **Fail-safe**: `/me` okunamazsa kullanıcı Free kabul edilir (premium açılmaz),
///   ama ekran hata da gösterir — sessizce yutmaz (master tasarım §1).
/// - **İptal ≠ anında kapanma**: sunucu `Cancelled` döner ama `Premium` seviyesi
///   dönem sonuna kadar sürer; ekran "kilit" göstermez.
void main() {
  group('MembershipCubit', () {
    test('yukleme durumu ve rol bazli plan listesini doldurur', () async {
      final repository = FakeMembershipRepository(
        snapshot: _premium(),
        plans: <MembershipPlan>[_monthly(), _yearly()],
      );
      final cubit = MembershipCubit(repository);

      await cubit.load(role: 'Teacher');

      expect(cubit.state.status, MembershipStatus.loaded);
      expect(cubit.state.isPremium, isTrue);
      expect(cubit.state.membership.planCode, 'teacher-premium-monthly');
      expect(cubit.state.plans, hasLength(2));
      expect(repository.planCalls.single, 'Teacher');
      await cubit.close();
    });

    test('free kullanicida premium kapali ve haklar Free degerlerinde', () async {
      final repository = FakeMembershipRepository(plans: <MembershipPlan>[_monthly()]);
      final cubit = MembershipCubit(repository);

      await cubit.load(role: 'Teacher');

      expect(cubit.state.isPremium, isFalse);
      expect(cubit.state.entitlement.maxStudents, 5);
      expect(cubit.state.entitlement.canSeeAds, isTrue);
      expect(cubit.state.entitlement.pdfReports, isFalse);
      await cubit.close();
    });

    test('me okunamazsa fail-safe Free kalir ama hata bildirilir', () async {
      final repository = FakeMembershipRepository(
        meError: const ApiException(message: 'Uyelik bilgisi alinamadi.'),
      );
      final cubit = MembershipCubit(repository);

      await cubit.load(role: 'Teacher');

      expect(cubit.state.status, MembershipStatus.failure);
      expect(cubit.state.isPremium, isFalse);
      expect(cubit.state.errorMessage, 'Uyelik bilgisi alinamadi.');
      await cubit.close();
    });

    test('deneme baslatinca durum premium olur', () async {
      final repository = FakeMembershipRepository(
        plans: <MembershipPlan>[_monthly()],
        afterTrial: _premium(status: 'Trialing'),
      );
      final cubit = MembershipCubit(repository);
      await cubit.load(role: 'Teacher');

      final ok = await cubit.startTrial('teacher-premium-monthly');

      expect(ok, isTrue);
      expect(cubit.state.isPremium, isTrue);
      expect(cubit.state.membership.subscriptionStatus, 'Trialing');
      expect(repository.trialCalls.single, 'teacher-premium-monthly');
      expect(cubit.state.isBusy, isFalse);
      await cubit.close();
    });

    test('deneme ikinci kez reddedilirse hata mesaji doner, durum korunur', () async {
      final repository = FakeMembershipRepository(
        trialError: const ApiException(
          message: 'Ucretsiz deneme hakkiniz kullanilmis.',
          statusCode: 409,
          code: 'membership.trial_already_used',
        ),
      );
      final cubit = MembershipCubit(repository);
      await cubit.load(role: 'Teacher');

      final ok = await cubit.startTrial('teacher-premium-monthly');

      expect(ok, isFalse);
      expect(cubit.state.errorMessage, 'Ucretsiz deneme hakkiniz kullanilmis.');
      expect(cubit.state.isPremium, isFalse);
      expect(cubit.state.status, MembershipStatus.loaded);
      await cubit.close();
    });

    test('iptal sonrasi erisim donem sonuna kadar premium kalir', () async {
      final repository = FakeMembershipRepository(
        snapshot: _premium(),
        afterCancel: _premium(status: 'Cancelled'),
      );
      final cubit = MembershipCubit(repository);
      await cubit.load(role: 'Teacher');

      final ok = await cubit.cancel();

      expect(ok, isTrue);
      expect(repository.cancelCalls, 1);
      expect(cubit.state.membership.subscriptionStatus, 'Cancelled');
      expect(cubit.state.isPremium, isTrue, reason: 'odenen sure kaybolmaz');
      await cubit.close();
    });

    test('referans kodu getirilir ve odul ayi tasinir', () async {
      final repository = FakeMembershipRepository(
        referral: const ReferralCode(
          code: 'K7MNPQ',
          redemptionCount: 2,
          rewardMonths: 1,
        ),
      );
      final cubit = MembershipCubit(repository);

      await cubit.loadReferralCode();

      expect(repository.referralCalls, 1);
      expect(cubit.state.referral?.code, 'K7MNPQ');
      expect(cubit.state.referral?.redemptionCount, 2);
      expect(cubit.state.referral?.rewardMonths, 1);
      await cubit.close();
    });

    test('referans kodu kullaninca premium acilir ve basari mesaji verilir', () async {
      final repository = FakeMembershipRepository(afterRedeem: _premium(status: 'Active'));
      final cubit = MembershipCubit(repository);
      await cubit.load(role: 'Student');

      final ok = await cubit.redeemReferral('k7mnpq');

      expect(ok, isTrue);
      expect(repository.redeemCalls.single, 'K7MNPQ', reason: 'kod buyuk harfe cevrilir');
      expect(cubit.state.isPremium, isTrue);
      expect(cubit.state.successMessage, isNotNull);
      await cubit.close();
    });

    test('kendi kodunu kullanma reddi hata olarak gosterilir', () async {
      final repository = FakeMembershipRepository(
        redeemError: const ApiException(
          message: 'Kendi referans kodunuzu kullanamazsiniz.',
          statusCode: 400,
          code: 'membership.self_referral',
        ),
      );
      final cubit = MembershipCubit(repository);
      await cubit.load(role: 'Student');

      final ok = await cubit.redeemReferral('ABC234');

      expect(ok, isFalse);
      expect(cubit.state.errorMessage, 'Kendi referans kodunuzu kullanamazsiniz.');
      expect(cubit.state.isPremium, isFalse);
      await cubit.close();
    });

    test('bos kod sunucuya gonderilmez', () async {
      final repository = FakeMembershipRepository();
      final cubit = MembershipCubit(repository);

      final ok = await cubit.redeemReferral('   ');

      expect(ok, isFalse);
      expect(repository.redeemCalls, isEmpty);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });

    test('satista olmayan plan paywall listesine girmez', () async {
      final repository = FakeMembershipRepository(
        plans: <MembershipPlan>[_monthly(), _yearly(isActive: false)],
      );
      final cubit = MembershipCubit(repository);

      await cubit.load(role: 'Teacher');

      expect(cubit.state.sellablePlans, hasLength(1));
      expect(cubit.state.sellablePlans.single.code, 'teacher-premium-monthly');
      await cubit.close();
    });

    test('rol cozumleme veli > ogrenci > ogretmen onceligini korur', () {
      expect(membershipRoleFor(const <String>['Parent', 'Student']), 'Parent');
      expect(membershipRoleFor(const <String>['Student']), 'Student');
      expect(membershipRoleFor(const <String>['Student', 'Teacher']), 'Teacher');
      expect(membershipRoleFor(const <String>['Admin']), 'Teacher');
      expect(membershipRoleFor(const <String>[]), 'Teacher');
    });
  });
}

MembershipSnapshot _premium({String status = 'Active'}) => MembershipSnapshot(
  tier: 'Premium',
  effectiveUntilUtc: DateTime.utc(2026, 10, 4),
  planCode: 'teacher-premium-monthly',
  subscriptionStatus: status,
  entitlement: const MembershipEntitlement(
    tier: 'Premium',
    effectiveUntilUtc: null,
    maxStudents: 2147483647,
    canSeeAds: false,
    detailedAnalytics: true,
    pdfReports: true,
    richNotifications: true,
  ),
);

MembershipPlan _monthly() => const MembershipPlan(
  id: 'plan-1',
  code: 'teacher-premium-monthly',
  role: 'Teacher',
  tier: 'Premium',
  period: 'Monthly',
  price: 249,
  currency: 'TRY',
  trialDays: 30,
  isActive: true,
);

MembershipPlan _yearly({bool isActive = true}) => MembershipPlan(
  id: 'plan-2',
  code: 'teacher-premium-yearly',
  role: 'Teacher',
  tier: 'Premium',
  period: 'Yearly',
  price: 2490,
  currency: 'TRY',
  trialDays: 0,
  isActive: isActive,
);
