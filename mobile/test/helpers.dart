import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';

class InMemoryTokenStorage implements TokenStorage {
  String? _token;

  @override
  Future<void> clear() async {
    _token = null;
  }

  @override
  Future<String?> readAccessToken() async => _token;

  @override
  Future<void> writeAccessToken(String token) async {
    _token = token;
  }
}

class InMemoryLocalCache implements LocalCache {
  final Map<String, String> _values = <String, String>{};

  @override
  Future<String?> readString(String key) async => _values[key];

  @override
  Future<void> remove(String key) async {
    _values.remove(key);
  }

  @override
  Future<void> writeString(String key, String value) async {
    _values[key] = value;
  }
}
