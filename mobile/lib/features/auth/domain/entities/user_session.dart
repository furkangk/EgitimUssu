import 'package:equatable/equatable.dart';

class UserSession extends Equatable {
  const UserSession({
    required this.userId,
    required this.email,
    required this.fullName,
    required this.roles,
    required this.accessToken,
    required this.expiresAtUtc,
  });

  final String userId;
  final String email;
  final String fullName;
  final List<String> roles;
  final String accessToken;
  final DateTime expiresAtUtc;

  bool get isExpired => expiresAtUtc.isBefore(DateTime.now().toUtc());
  bool get isExpiringSoon => expiresAtUtc.isBefore(
    DateTime.now().toUtc().add(const Duration(minutes: 1)),
  );

  @override
  List<Object?> get props => <Object?>[
    userId,
    email,
    fullName,
    roles,
    accessToken,
    expiresAtUtc,
  ];
}
