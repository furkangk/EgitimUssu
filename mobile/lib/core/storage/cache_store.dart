import 'dart:convert';

import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:flutter/foundation.dart';

import 'local_cache.dart';

class CachedValue<T> {
  const CachedValue({
    required this.value,
    required this.savedAt,
    this.isStale = false,
  });
  final T value;
  final DateTime savedAt;
  final bool isStale;
}

/// Session-scoped read cache. Domain repositories retain their value contracts;
/// the app's shared banner observes [staleSince] for every cache consumer.
class CacheStore {
  CacheStore(this._cache);
  final LocalCache _cache;
  final staleSince = ValueNotifier<DateTime?>(null);
  final Map<String, DateTime> _stale = {};
  String? _userId;
  int _generation = 0;
  Future<void> _writes = Future<void>.value();
  bool get isStale => staleSince.value != null;

  void activate(String userId) {
    if (_userId == userId) return;
    _generation++;
    _userId = userId;
    _stale.clear();
    _publish();
  }

  Future<void> clearAll() {
    _generation++;
    _userId = null;
    _stale.clear();
    _publish();
    return _enqueue(
      () => _removeWhere((key) => key.startsWith('cache.') || _legacy(key)),
    );
  }

  Future<void> clearUser(String userId) {
    if (_userId == userId) {
      _generation++;
      _stale.clear();
      _publish();
    }
    return _enqueue(
      () => _removeWhere((key) => key.startsWith('cache.${_part(userId)}.')),
    );
  }

  Future<void> clearLegacy() => _enqueue(() => _removeWhere(_legacy));
  static bool _legacy(String key) =>
      ['payments.', 'students.', 'scheduling.'].any(key.startsWith);
  static String _part(String value) =>
      Uri.encodeComponent(value).replaceAll('.', '%2E');
  String? _key(String feature, String key) => _userId == null
      ? null
      : 'cache.${_part(_userId!)}.${_part(feature)}.${_part(key)}';

  Future<void> _removeWhere(bool Function(String) matches) async {
    for (final key in await _cache.keys()) {
      if (matches(key)) await _cache.remove(key);
    }
  }

  Future<void> _enqueue(Future<void> Function() action) {
    final next = _writes.then((_) => action());
    // Keep the queue usable after an error; the caller still receives the error.
    _writes = next.then((_) {}, onError: (Object _, StackTrace __) {});
    return next;
  }

  void _check(int generation) {
    if (generation != _generation) {
      throw const ApiException(
        message: 'Oturum değişti.',
        kind: ApiErrorKind.auth,
        statusCode: 401,
      );
    }
  }

  Future<CachedValue<T>?> read<T>(
    String feature,
    String key,
    T Function(Object?) decode,
  ) async {
    final generation = _generation;
    final storageKey = _key(feature, key);
    if (storageKey == null) return null;
    final cached = await _read(storageKey, decode);
    _check(generation);
    return cached;
  }

  Future<CachedValue<T>?> _read<T>(
    String key,
    T Function(Object?) decode,
  ) async {
    try {
      final raw = await _cache.readString(key);
      if (raw == null) return null;
      final json = jsonDecode(raw) as Map<String, dynamic>;
      return CachedValue(
        value: decode(json['value']),
        savedAt: DateTime.parse(json['savedAt'] as String).toUtc(),
        isStale: true,
      );
    } catch (error) {
      debugPrint('Cache read ignored: ${error.runtimeType}');
      return null;
    }
  }

  Future<void> write(String feature, String key, Object? value) =>
      _save(_key(feature, key), value, DateTime.now().toUtc(), _generation);

  Future<void> _save(
    String? key,
    Object? value,
    DateTime savedAt,
    int generation,
  ) => _enqueue(() async {
    _check(generation);
    if (key == null) return;
    try {
      await _cache.writeString(
        key,
        jsonEncode({'value': value, 'savedAt': savedAt.toIso8601String()}),
      );
    } catch (error) {
      debugPrint('Cache write skipped: ${error.runtimeType}');
    }
  });

  Future<CachedValue<T>> load<T>({
    required String feature,
    required String key,
    required Future<Object?> Function() fetch,
    required T Function(Object?) decode,
  }) async {
    final generation = _generation;
    final storageKey = _key(feature, key);
    try {
      final raw = await fetch();
      _check(generation);
      final value = decode(raw);
      final savedAt = DateTime.now().toUtc();
      await _save(storageKey, raw, savedAt, generation);
      _check(generation);
      _stale.remove(storageKey);
      _publish();
      return CachedValue(value: value, savedAt: savedAt);
    } on ApiException catch (error) {
      _check(generation);
      if (error.kind != ApiErrorKind.network || error.statusCode != null) {
        // Existing consumers may still display the last stale value.
        rethrow;
      }
      if (storageKey != null) {
        final cached = await _read(storageKey, decode);
        _check(generation);
        if (cached != null) {
          _stale[storageKey] = cached.savedAt;
          _publish();
          return cached;
        }
      }
      rethrow;
    }
  }

  void _publish() {
    staleSince.value = _stale.isEmpty
        ? null
        : _stale.values.reduce((a, b) => a.isBefore(b) ? a : b);
  }
}
