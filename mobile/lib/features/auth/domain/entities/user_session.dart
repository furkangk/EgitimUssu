import 'package:equatable/equatable.dart';

class UserSession extends Equatable {
  const UserSession({
    required this.userId,
    required this.email,
    required this.fullName,
    required this.roles,
    required this.accessToken,
    required this.expiresAtUtc,
    this.refreshToken,
  });

  final String userId;
  final String email;
  final String fullName;
  final List<String> roles;
  final String accessToken;
  final DateTime expiresAtUtc;
  final String? refreshToken;

  bool get isExpired => expiresAtUtc.isBefore(DateTime.now().toUtc());
  bool get isExpiringSoon => expiresAtUtc.isBefore(
    DateTime.now().toUtc().add(const Duration(minutes: 1)),
  );

  UserSession copyWith({
    String? accessToken,
    DateTime? expiresAtUtc,
    String? refreshToken,
  }) {
    return UserSession(
      userId: userId,
      email: email,
      fullName: fullName,
      roles: roles,
      accessToken: accessToken ?? this.accessToken,
      expiresAtUtc: expiresAtUtc ?? this.expiresAtUtc,
      refreshToken: refreshToken ?? this.refreshToken,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    userId,
    email,
    fullName,
    roles,
    accessToken,
    expiresAtUtc,
    refreshToken,
  ];
}
