import 'dart:convert';

import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';

class UserSessionModel extends UserSession {
  const UserSessionModel({
    required super.userId,
    required super.email,
    required super.fullName,
    required super.roles,
    required super.accessToken,
    required super.expiresAtUtc,
    super.refreshToken,
  });

  factory UserSessionModel.fromJson(Map<String, dynamic> json) {
    return UserSessionModel(
      userId: json['userId'].toString(),
      email: json['email'].toString(),
      fullName: json['fullName'].toString(),
      roles: ((json['roles'] as List<dynamic>? ?? <dynamic>[]))
          .map((dynamic item) => item.toString())
          .toList(),
      accessToken: json['accessToken'].toString(),
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'].toString()).toUtc(),
      refreshToken: json['refreshToken']?.toString(),
    );
  }

  factory UserSessionModel.fromCache(String source) {
    return UserSessionModel.fromJson(
      jsonDecode(source) as Map<String, dynamic>,
    );
  }

  String toCache() {
    return jsonEncode(<String, dynamic>{
      'userId': userId,
      'email': email,
      'fullName': fullName,
      'roles': roles,
      'accessToken': accessToken,
      'expiresAtUtc': expiresAtUtc.toIso8601String(),
      'refreshToken': refreshToken,
    });
  }
}
