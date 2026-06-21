import 'package:flutter_secure_storage/flutter_secure_storage.dart';

abstract interface class TokenStorage {
  Future<String?> readAccessToken();
  Future<void> writeAccessToken(String token);
  Future<void> clear();
}

class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage() : _storage = const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const _tokenKey = 'access_token';

  @override
  Future<void> clear() async {
    await _storage.delete(key: _tokenKey);
  }

  @override
  Future<String?> readAccessToken() => _storage.read(key: _tokenKey);

  @override
  Future<void> writeAccessToken(String token) async {
    await _storage.write(key: _tokenKey, value: token);
  }
}
