/// M15-4: Çalışma verisi gizlilik seviyesi. Değerler backend'in `PrivacyLevel`
/// enum'uyla birebir eşleşir (System.Text.Json enum'ları **sayı** serileştirir).
enum PrivacyLevel {
  standard(1, 'Standart', 'Öğretmen ve onaylı veli çalışma özetini ve detayını görür.'),
  limited(2, 'Sınırlı', 'Yalnız haftalık toplam süre görünür; seans ve test detayı gizlenir.'),
  hidden(3, 'Gizli', 'Hiçbir çalışma verisi paylaşılmaz (ders, ödev ve ödeme etkilenmez).');

  const PrivacyLevel(this.value, this.label, this.description);

  final int value;
  final String label;
  final String description;

  static PrivacyLevel fromName(String? name) => switch (name) {
    'Limited' => PrivacyLevel.limited,
    'Hidden' => PrivacyLevel.hidden,
    _ => PrivacyLevel.standard,
  };
}

/// M15-3: Girişte diğer oturumlara ne olacağı.
enum SessionTerminationPolicy {
  keepLatest(1, 'Tüm cihazlarda açık kal', 'Birden fazla cihazda aynı anda oturum açabilirsin.'),
  terminateOtherSessions(2, 'Yalnız son cihazda kal', 'Yeni giriş yaptığında diğer oturumlar kapanır.');

  const SessionTerminationPolicy(this.value, this.label, this.description);

  final int value;
  final String label;
  final String description;

  static SessionTerminationPolicy fromName(String? name) =>
      name == 'TerminateOtherSessions'
      ? SessionTerminationPolicy.terminateOtherSessions
      : SessionTerminationPolicy.keepLatest;
}

/// Kullanıcının bildirim/gizlilik/güvenlik ayarları (M15-1).
/// `shareStudyDataWith*` alanları burada **salt okunur**; ayrı uçtan yönetilir.
class UserSettings {
  const UserSettings({
    required this.userId,
    required this.pushNotificationsEnabled,
    required this.emailNotificationsEnabled,
    required this.upcomingLessonReminderEnabled,
    required this.homeworkReminderEnabled,
    required this.paymentReminderEnabled,
    required this.weeklySummaryEnabled,
    required this.shareStudyDataWithTeacher,
    required this.shareStudyDataWithParent,
    required this.privacyLevel,
    required this.sessionTerminationPolicy,
  });

  final String userId;
  final bool pushNotificationsEnabled;
  final bool emailNotificationsEnabled;
  final bool upcomingLessonReminderEnabled;
  final bool homeworkReminderEnabled;
  final bool paymentReminderEnabled;
  final bool weeklySummaryEnabled;
  final bool shareStudyDataWithTeacher;
  final bool shareStudyDataWithParent;
  final PrivacyLevel privacyLevel;
  final SessionTerminationPolicy sessionTerminationPolicy;

  UserSettings copyWith({
    bool? pushNotificationsEnabled,
    bool? emailNotificationsEnabled,
    bool? upcomingLessonReminderEnabled,
    bool? homeworkReminderEnabled,
    bool? paymentReminderEnabled,
    bool? weeklySummaryEnabled,
    PrivacyLevel? privacyLevel,
    SessionTerminationPolicy? sessionTerminationPolicy,
  }) {
    return UserSettings(
      userId: userId,
      pushNotificationsEnabled:
          pushNotificationsEnabled ?? this.pushNotificationsEnabled,
      emailNotificationsEnabled:
          emailNotificationsEnabled ?? this.emailNotificationsEnabled,
      upcomingLessonReminderEnabled:
          upcomingLessonReminderEnabled ?? this.upcomingLessonReminderEnabled,
      homeworkReminderEnabled:
          homeworkReminderEnabled ?? this.homeworkReminderEnabled,
      paymentReminderEnabled:
          paymentReminderEnabled ?? this.paymentReminderEnabled,
      weeklySummaryEnabled: weeklySummaryEnabled ?? this.weeklySummaryEnabled,
      shareStudyDataWithTeacher: shareStudyDataWithTeacher,
      shareStudyDataWithParent: shareStudyDataWithParent,
      privacyLevel: privacyLevel ?? this.privacyLevel,
      sessionTerminationPolicy:
          sessionTerminationPolicy ?? this.sessionTerminationPolicy,
    );
  }
}

abstract interface class SettingsRepository {
  /// Kullanıcının ayarlarını getirir. Kayıt yoksa backend **varsayılan** döner (404 değil).
  Future<UserSettings> load(String userId);

  /// Bildirim/gizlilik/güvenlik tercihlerini kaydeder (upsert).
  Future<UserSettings> save(String userId, UserSettings settings);
}
