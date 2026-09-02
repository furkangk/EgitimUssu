import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';

/// Backend `UserSettingsResponse` → mobil `UserSettings`.
/// `privacyLevel`/`sessionTerminationPolicy` yanıtta **ad** (string) olarak gelir,
/// istekte ise **sayı** gönderilir (backend enum bağlaması).
class UserSettingsModel {
  const UserSettingsModel._();

  static UserSettings fromJson(Map<String, dynamic> json) {
    return UserSettings(
      userId: json['userId'] as String? ?? '',
      pushNotificationsEnabled: json['pushNotificationsEnabled'] as bool? ?? true,
      emailNotificationsEnabled:
          json['emailNotificationsEnabled'] as bool? ?? true,
      upcomingLessonReminderEnabled:
          json['upcomingLessonReminderEnabled'] as bool? ?? true,
      homeworkReminderEnabled: json['homeworkReminderEnabled'] as bool? ?? true,
      paymentReminderEnabled: json['paymentReminderEnabled'] as bool? ?? true,
      weeklySummaryEnabled: json['weeklySummaryEnabled'] as bool? ?? true,
      shareStudyDataWithTeacher:
          json['shareStudyDataWithTeacher'] as bool? ?? false,
      shareStudyDataWithParent:
          json['shareStudyDataWithParent'] as bool? ?? false,
      privacyLevel: PrivacyLevel.fromName(json['privacyLevel'] as String?),
      sessionTerminationPolicy: SessionTerminationPolicy.fromName(
        json['sessionTerminationPolicy'] as String?,
      ),
    );
  }

  static Map<String, dynamic> toRequest(UserSettings settings) {
    return <String, dynamic>{
      'pushNotificationsEnabled': settings.pushNotificationsEnabled,
      'emailNotificationsEnabled': settings.emailNotificationsEnabled,
      'upcomingLessonReminderEnabled': settings.upcomingLessonReminderEnabled,
      'homeworkReminderEnabled': settings.homeworkReminderEnabled,
      'paymentReminderEnabled': settings.paymentReminderEnabled,
      'weeklySummaryEnabled': settings.weeklySummaryEnabled,
      'privacyLevel': settings.privacyLevel.value,
      'sessionTerminationPolicy': settings.sessionTerminationPolicy.value,
    };
  }
}
