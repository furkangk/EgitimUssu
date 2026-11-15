import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Üyelik verisinin yükleme durumu. (Aboneliğin **kendi** durumu — `Trialing`/`Active`/
/// `Cancelled` — bundan ayrıdır ve `MembershipSnapshot.subscriptionStatus`'te durur.)
enum MembershipStatus { initial, loading, loaded, failure }

/// D-10: Üyelik ekranı, paywall ve `PremiumLock`'un ortak durumu.
class MembershipState {
  const MembershipState({
    this.status = MembershipStatus.initial,
    this.membership = MembershipSnapshot.free,
    this.plans = const <MembershipPlan>[],
    this.referral,
    this.isBusy = false,
    this.errorMessage,
    this.successMessage,
  });

  final MembershipStatus status;

  /// Fail-safe: hiç okunamamışsa Free kalır — premium özellik **açılmaz**.
  final MembershipSnapshot membership;
  final List<MembershipPlan> plans;
  final ReferralCode? referral;

  /// Deneme/iptal/referans komutu uçarken true (düğmeler kilitlenir).
  final bool isBusy;
  final String? errorMessage;
  final String? successMessage;

  bool get isLoading => status == MembershipStatus.loading;

  bool get isPremium => membership.isPremium;

  MembershipEntitlement get entitlement => membership.entitlement;

  /// Paywall'da gösterilecek planlar: yalnız satışta olanlar, aylık önce.
  List<MembershipPlan> get sellablePlans {
    final rows = plans.where((plan) => plan.isActive).toList()
      ..sort((a, b) => a.isYearly == b.isYearly ? 0 : (a.isYearly ? 1 : -1));
    return rows;
  }

  MembershipState copyWith({
    MembershipStatus? status,
    MembershipSnapshot? membership,
    List<MembershipPlan>? plans,
    ReferralCode? referral,
    bool? isBusy,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return MembershipState(
      status: status ?? this.status,
      membership: membership ?? this.membership,
      plans: plans ?? this.plans,
      referral: referral ?? this.referral,
      isBusy: isBusy ?? this.isBusy,
      errorMessage: clearMessages ? null : (errorMessage ?? this.errorMessage),
      successMessage: clearMessages
          ? null
          : (successMessage ?? this.successMessage),
    );
  }
}

/// M17-1/M17-3 (D-10): Üyelik durumunu okur, deneme/iptal/referans komutlarını çalıştırır.
///
/// Uygulama kökünde tek örnek olarak sağlanır; `PremiumLock` premium kararını buradan okur.
class MembershipCubit extends Cubit<MembershipState> {
  MembershipCubit(this._repository) : super(const MembershipState());

  int _generation = 0;
  void reset() {
    _generation++;
    _role = null;
    if (!isClosed) emit(const MembershipState());
  }

  bool _current(int generation) => !isClosed && generation == _generation;

  factory MembershipCubit.create() =>
      MembershipCubit(injector<MembershipRepository>());

  final MembershipRepository _repository;

  String? _role;

  /// Üyelik durumunu ve rolün plan kataloğunu yükler.
  ///
  /// Plan listesi başarısız olsa bile üyelik durumu okunmuşsa ekran çalışır: katalog
  /// anonim bir uçtur, kilit kararını etkilemez.
  Future<void> load({String? role}) async {
    final generation = _generation;
    _role = role ?? _role;
    emit(state.copyWith(status: MembershipStatus.loading, clearMessages: true));

    MembershipSnapshot? snapshot;
    List<MembershipPlan>? plans;
    String? failure;

    try {
      snapshot = await _repository.me();
    } on ApiException catch (error) {
      failure = error.message;
    }

    if (!_current(generation)) return;
    try {
      plans = await _repository.listPlans(role: _role);
    } on ApiException catch (error) {
      failure ??= error.message;
    }

    if (!_current(generation)) return;
    emit(
      state.copyWith(
        // Fail-safe: `/me` okunamadıysa Free kalır ama durum failure olur —
        // sessizce yutmak "sessiz başarısızlık yok" ilkesini çiğnerdi.
        status: failure == null
            ? MembershipStatus.loaded
            : MembershipStatus.failure,
        membership: snapshot ?? MembershipSnapshot.free,
        plans: plans ?? const <MembershipPlan>[],
        errorMessage: failure,
      ),
    );
  }

  /// Son kullanılan rolle yeniden yükler.
  Future<void> refresh() => load();

  /// Ücretsiz denemeyi başlatır. Başarılıysa `true`.
  Future<bool> startTrial(String planCode) =>
      _run(() => _repository.startTrial(planCode), 'Ücretsiz denemen başladı.');

  /// Aboneliği iptal eder. Erişim dönem sonuna kadar sürer.
  Future<bool> cancel() => _run(
    _repository.cancel,
    'Aboneliğin iptal edildi; erişimin dönem sonuna kadar sürüyor.',
  );

  /// Referans kodunu getirir (yoksa sunucu üretir).
  Future<void> loadReferralCode() async {
    final generation = _generation;
    try {
      final referral = await _repository.referralCode();
      if (!_current(generation)) return;
      emit(state.copyWith(referral: referral, clearMessages: true));
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      emit(state.copyWith(errorMessage: error.message));
    }
  }

  /// Referans kodunu kullanır; her iki tarafa bir ay premium yazar.
  Future<bool> redeemReferral(String code) {
    final normalized = code.trim().toUpperCase();
    if (normalized.isEmpty) {
      emit(state.copyWith(errorMessage: 'Referans kodu boş olamaz.'));
      return Future<bool>.value(false);
    }
    return _run(
      () => _repository.redeemReferral(normalized),
      'Referans ödülün hesabına eklendi.',
    );
  }

  /// Snackbar gösterildikten sonra mesajları temizler.
  void clearMessages() => emit(state.copyWith(clearMessages: true));

  Future<bool> _run(
    Future<MembershipSnapshot> Function() action,
    String successMessage,
  ) async {
    final generation = _generation;
    emit(state.copyWith(isBusy: true, clearMessages: true));
    try {
      final snapshot = await action();
      if (!_current(generation)) return false;
      emit(
        state.copyWith(
          status: MembershipStatus.loaded,
          membership: snapshot,
          isBusy: false,
          successMessage: successMessage,
        ),
      );
      return true;
    } on ApiException catch (error) {
      if (!_current(generation)) return false;
      // Komut reddedildi (409 deneme kullanılmış, 400 kendi kodu vb.): mevcut
      // üyelik durumu **korunur**, yalnız hata mesajı eklenir.
      emit(state.copyWith(isBusy: false, errorMessage: error.message));
      return false;
    }
  }
}
