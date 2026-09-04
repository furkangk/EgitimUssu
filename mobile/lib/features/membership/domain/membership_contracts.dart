// M17 Üyelik (Membership) — mobil domain sözleşmeleri.
// Backend: `/api/membership`. K-09'un mobil karşılığı: premium kararının **tek**
// kaynağı `GET /api/membership/me` yanıtıdır; hiçbir ekran kendi tier alanını taşımaz.

/// Sunucunun döndüğü seviye adları (`MembershipTier` enum'unun metin karşılığı).
const String kMembershipTierFree = 'Free';

/// Premium seviye adı.
const String kMembershipTierPremium = 'Premium';

/// Premium'da `MaxStudents` sunucuda `int.MaxValue` gelir; "sınırsız" eşiği bu değerdir.
const int kUnlimitedStudentsThreshold = 1000000;

/// K-09: Kullanıcının hak ettiği özellikler (`Entitlement` sözleşmesinin mobil karşılığı).
class MembershipEntitlement {
  const MembershipEntitlement({
    required this.tier,
    required this.effectiveUntilUtc,
    required this.maxStudents,
    required this.canSeeAds,
    required this.detailedAnalytics,
    required this.pdfReports,
    required this.richNotifications,
  });

  final String tier;
  final DateTime? effectiveUntilUtc;

  /// Öğretmenin bağlayabileceği en fazla öğrenci (Free 5, Premium sınırsız).
  final int maxStudents;
  final bool canSeeAds;
  final bool detailedAnalytics;
  final bool pdfReports;
  final bool richNotifications;

  bool get isPremium => tier == kMembershipTierPremium;

  bool get hasUnlimitedStudents => maxStudents >= kUnlimitedStudentsThreshold;

  /// Fail-safe varsayılan: kaynak okunamazsa Free hakları geçerlidir (premium açılmaz).
  static const MembershipEntitlement free = MembershipEntitlement(
    tier: kMembershipTierFree,
    effectiveUntilUtc: null,
    maxStudents: 5,
    canSeeAds: true,
    detailedAnalytics: false,
    pdfReports: false,
    richNotifications: false,
  );
}

/// `GET /api/membership/me` yanıtı: etkin seviye + dönem + plan + haklar.
///
/// ⚠️ [subscriptionStatus] ile [tier] **aynı şey değildir**: `Cancelled` ve `PastDue`
/// durumlarında da kullanıcı [effectiveUntilUtc]'a kadar Premium'dur (ödediği süreyi
/// kaybetmez). Ekran kilidi [tier]'a bakar, bilgilendirme metni [subscriptionStatus]'e.
class MembershipSnapshot {
  const MembershipSnapshot({
    required this.tier,
    required this.effectiveUntilUtc,
    required this.planCode,
    required this.subscriptionStatus,
    required this.entitlement,
  });

  final String tier;
  final DateTime? effectiveUntilUtc;
  final String? planCode;

  /// `Trialing` | `Active` | `PastDue` | `Cancelled` | `Expired`; abonelik yoksa `null`.
  final String? subscriptionStatus;
  final MembershipEntitlement entitlement;

  bool get isPremium => tier == kMembershipTierPremium;

  bool get isTrialing => subscriptionStatus == 'Trialing';

  bool get isCancelled => subscriptionStatus == 'Cancelled';

  bool get hasSubscription => subscriptionStatus != null;

  /// Hiç abonelik açılmamış Free kullanıcı — fail-safe varsayılan.
  static const MembershipSnapshot free = MembershipSnapshot(
    tier: kMembershipTierFree,
    effectiveUntilUtc: null,
    planCode: null,
    subscriptionStatus: null,
    entitlement: MembershipEntitlement.free,
  );
}

/// `GET /api/membership/plans` satırı.
class MembershipPlan {
  const MembershipPlan({
    required this.id,
    required this.code,
    required this.role,
    required this.tier,
    required this.period,
    required this.price,
    required this.currency,
    required this.trialDays,
    required this.isActive,
  });

  final String id;
  final String code;
  final String role;
  final String tier;

  /// `Monthly` | `Yearly`.
  final String period;
  final double price;
  final String currency;

  /// Ücretsiz deneme günü (0 = yok). Aylık planlarda 30 = "ilk ay ücretsiz".
  final int trialDays;
  final bool isActive;

  bool get isYearly => period == 'Yearly';

  bool get hasTrial => trialDays > 0;
}

/// `GET /api/membership/referral-code` yanıtı.
class ReferralCode {
  const ReferralCode({
    required this.code,
    required this.redemptionCount,
    required this.rewardMonths,
  });

  final String code;

  /// Kaç kişi bu kodu kullandı.
  final int redemptionCount;

  /// Kullanan ve kullandıran için ödül ayı.
  final int rewardMonths;
}

/// M17 uçlarının mobil sözleşmesi.
///
/// ⚠️ Plandaki `checkout(planCode)` **bilinçli olarak yok**: `POST /api/membership/checkout`
/// ucu P09 Task 4'te (iyzico, Q1'e bloke) gelecek. Var olmayan bir uca istek atan metot
/// yazmak "sessiz başarısızlık yok" ilkesini çiğnerdi; satın alma düğmesi o güne kadar
/// paywall'da pasif ("Yakında") durur.
abstract interface class MembershipRepository {
  /// Oturumdaki kullanıcının üyelik durumu ve hakları.
  Future<MembershipSnapshot> me();

  /// Satıştaki planlar. Uç **anonim** erişilebilir (paywall giriş yapmadan da fiyat gösterir).
  Future<List<MembershipPlan>> listPlans({String? role});

  /// Seçilen planın ücretsiz denemesini başlatır (kullanıcı başına bir kez).
  Future<MembershipSnapshot> startTrial(String planCode);

  /// Aboneliği iptal eder; erişim dönem sonuna kadar sürer.
  Future<MembershipSnapshot> cancel();

  /// Kullanıcının referans kodunu getirir (yoksa sunucu üretir).
  Future<ReferralCode> referralCode();

  /// Referans kodunu kullanır; her iki tarafa bir ay premium yazar.
  Future<MembershipSnapshot> redeemReferral(String code);
}

/// Oturum rollerinden plan kataloğunun rol filtresini üretir.
///
/// Öncelik `AppRouter`'ın rol yönlendirmesiyle **birebir aynıdır** (veli > yalnız-öğrenci >
/// öğretmen): kullanıcı hangi paneli görüyorsa o rolün planlarını görmelidir. Admin ve
/// bilinmeyen roller öğretmen kataloğuna düşer — satılabilir Admin planı yok.
String membershipRoleFor(List<String> roles) {
  if (roles.contains('Parent')) {
    return 'Parent';
  }
  if (roles.contains('Student') && !roles.contains('Teacher')) {
    return 'Student';
  }
  return 'Teacher';
}
