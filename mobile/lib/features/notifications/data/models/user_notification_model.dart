import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

/// `UserNotificationResponse` (M11-3) → domain nesnesi.
class UserNotificationModel extends UserNotification {
  const UserNotificationModel({
    required super.id,
    required super.userId,
    required super.kind,
    required super.title,
    required super.body,
    required super.route,
    required super.isRead,
    required super.createdOnUtc,
    super.readOnUtc,
  });

  factory UserNotificationModel.fromJson(Map<String, dynamic> json) {
    final readOn = json['readOnUtc'] as String?;

    return UserNotificationModel(
      id: json['id'] as String,
      userId: json['userId'] as String,
      kind: json['kind'] as String? ?? 'System',
      title: json['title'] as String? ?? '',
      body: json['body'] as String? ?? '',
      route: json['route'] as String?,
      isRead: json['isRead'] as bool? ?? readOn != null,
      createdOnUtc: DateTime.parse(json['createdOnUtc'] as String),
      readOnUtc: readOn == null ? null : DateTime.parse(readOn),
    );
  }
}
