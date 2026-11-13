import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/cubit/settings_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

/// P05 Task 5 (D-14): Ayar ekranları gerçek backend'e bağlı. Kritik davranış —
/// anahtar **iyimser** güncellenir; sunucu reddederse eski değere dönülür.
void main() {
  group('SettingsCubit', () {
    test('load ayarlari doldurur', () async {
      final repository = _FakeSettingsRepository(_settings());
      final cubit = SettingsCubit(repository);

      await cubit.load('user-1');

      expect(cubit.state.status, SettingsStatus.loaded);
      expect(cubit.state.settings?.homeworkReminderEnabled, isTrue);
      expect(repository.loadedUserIds, ['user-1']);
      await cubit.close();
    });

    test('load hata verirse failure olur', () async {
      final repository = _FakeSettingsRepository(_settings(), loadError: true);
      final cubit = SettingsCubit(repository);

      await cubit.load('user-1');

      expect(cubit.state.status, SettingsStatus.failure);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });

    test('toggle iyimser gunceller ve save cagirir', () async {
      final repository = _FakeSettingsRepository(_settings());
      final cubit = SettingsCubit(repository);
      await cubit.load('user-1');

      await cubit.setHomeworkReminderEnabled(false);

      expect(cubit.state.settings?.homeworkReminderEnabled, isFalse);
      expect(repository.savedSettings.single.homeworkReminderEnabled, isFalse);
      expect(cubit.state.status, SettingsStatus.loaded);
      await cubit.close();
    });

    test('save hata verirse eski degere donulur ve hata mesaji gosterilir', () async {
      final repository = _FakeSettingsRepository(_settings(), saveError: true);
      final cubit = SettingsCubit(repository);
      await cubit.load('user-1');

      await cubit.setHomeworkReminderEnabled(false);

      expect(cubit.state.settings?.homeworkReminderEnabled, isTrue);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });

    test('gizlilik seviyesi degisimi de kaydedilir', () async {
      final repository = _FakeSettingsRepository(_settings());
      final cubit = SettingsCubit(repository);
      await cubit.load('user-1');

      await cubit.setPrivacyLevel(PrivacyLevel.limited);

      expect(cubit.state.settings?.privacyLevel, PrivacyLevel.limited);
      expect(repository.savedSettings.single.privacyLevel, PrivacyLevel.limited);
      await cubit.close();
    });

    test('oturum politikasi degisimi de kaydedilir', () async {
      final repository = _FakeSettingsRepository(_settings());
      final cubit = SettingsCubit(repository);
      await cubit.load('user-1');

      await cubit.setSessionTerminationPolicy(
        SessionTerminationPolicy.terminateOtherSessions,
      );

      expect(
        repository.savedSettings.single.sessionTerminationPolicy,
        SessionTerminationPolicy.terminateOtherSessions,
      );
      await cubit.close();
    });

    test('yukleme yapilmadan toggle cagrilirsa istek gitmez', () async {
      final repository = _FakeSettingsRepository(_settings());
      final cubit = SettingsCubit(repository);

      await cubit.setHomeworkReminderEnabled(false);

      expect(repository.savedSettings, isEmpty);
      await cubit.close();
    });
  });
}

UserSettings _settings() => const UserSettings(
  userId: 'user-1',
  pushNotificationsEnabled: true,
  emailNotificationsEnabled: true,
  upcomingLessonReminderEnabled: true,
  homeworkReminderEnabled: true,
  paymentReminderEnabled: true,
  weeklySummaryEnabled: true,
  shareStudyDataWithTeacher: false,
  shareStudyDataWithParent: false,
  privacyLevel: PrivacyLevel.standard,
  sessionTerminationPolicy: SessionTerminationPolicy.keepLatest,
);

class _FakeSettingsRepository implements SettingsRepository {
  _FakeSettingsRepository(
    this._settings, {
    this.loadError = false,
    this.saveError = false,
  });

  final UserSettings _settings;
  final bool loadError;
  final bool saveError;

  final List<String> loadedUserIds = <String>[];
  final List<UserSettings> savedSettings = <UserSettings>[];

  @override
  Future<UserSettings> load(String userId) async {
    loadedUserIds.add(userId);
    if (loadError) {
      throw const ApiException(message: 'Ayarlar yuklenemedi.');
    }
    return _settings;
  }

  @override
  Future<UserSettings> save(String userId, UserSettings settings) async {
    if (saveError) {
      throw const ApiException(message: 'Ayar kaydedilemedi.');
    }
    savedSettings.add(settings);
    return settings;
  }
}
