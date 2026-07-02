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

  /// Y7: Önbellek yalnız gizli-olmayan profil bilgisini taşır; token'lar secure storage'dan gelir.
  factory UserSessionModel.fromCache(
    String source, {
    required String accessToken,
    String? refreshToken,
  }) {
    final json = jsonDecode(source) as Map<String, dynamic>;
    return UserSessionModel(
      userId: json['userId'].toString(),
      email: json['email'].toString(),
      fullName: json['fullName'].toString(),
      roles: ((json['roles'] as List<dynamic>? ?? <dynamic>[]))
          .map((dynamic item) => item.toString())
          .toList(),
      accessToken: accessToken,
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'].toString()).toUtc(),
      refreshToken: refreshToken,
    );
  }

  /// Y7: access/refresh token'lar burada **saklanmaz** (düz-metin SharedPreferences); yalnız secure storage'da tutulur.
  String toCache() {
    return jsonEncode(<String, dynamic>{
      'userId': userId,
      'email': email,
      'fullName': fullName,
      'roles': roles,
      'expiresAtUtc': expiresAtUtc.toIso8601String(),
    });
  }
}
