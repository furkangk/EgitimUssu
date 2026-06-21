import 'package:shared_preferences/shared_preferences.dart';

abstract interface class LocalCache {
  Future<void> writeString(String key, String value);
  Future<String?> readString(String key);
  Future<void> remove(String key);
}

class SharedPrefsLocalCache implements LocalCache {
  SharedPrefsLocalCache(this._preferences);

  final SharedPreferences _preferences;

  static Future<SharedPrefsLocalCache> create() async {
    final preferences = await SharedPreferences.getInstance();
    return SharedPrefsLocalCache(preferences);
  }

  @override
  Future<String?> readString(String key) async => _preferences.getString(key);

  @override
  Future<void> remove(String key) async {
    await _preferences.remove(key);
  }

  @override
  Future<void> writeString(String key, String value) async {
    await _preferences.setString(key, value);
  }
}
