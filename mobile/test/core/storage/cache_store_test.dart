import 'dart:async';

import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  late SharedPreferences prefs;
  late CacheStore store;
  setUp(() async {
    SharedPreferences.setMockInitialValues({'theme': 'dark'});
    prefs = await SharedPreferences.getInstance();
    store = CacheStore(SharedPrefsLocalCache(prefs))..activate('alice');
  });
  test(
    'failed refresh preserves the warning for data already displayed',
    () async {
      await store.write('students', 'list', ['old']);
      await store.load(
        feature: 'students',
        key: 'list',
        fetch: () async => throw const ApiException(
          message: 'offline',
          kind: ApiErrorKind.network,
        ),
        decode: (v) => v,
      );
      await expectLater(
        store.load(
          feature: 'students',
          key: 'list',
          fetch: () async =>
              throw const ApiException(message: 'failed', statusCode: 500),
          decode: (v) => v,
        ),
        throwsA(isA<ApiException>()),
      );
      expect(store.isStale, isTrue);
    },
  );
  test('scopes values and timestamp to the authenticated user', () async {
    await store.write('students', 'list', ['Alice student']);
    final value = await store.read('students', 'list', (v) => v as List);
    expect(value?.value, ['Alice student']);
    expect(value?.savedAt.isUtc, isTrue);
    expect(prefs.getKeys(), contains('cache.alice.students.list'));
    store.activate('bob');
    expect(await store.read('students', 'list', (v) => v), isNull);
    await store.write('students', 'list', ['Bob student']);
    await store.clearUser('alice');
    expect(prefs.containsKey('cache.alice.students.list'), isFalse);
    expect(prefs.containsKey('cache.bob.students.list'), isTrue);
  });
  test(
    'clearAll removes caches and legacy PII but preserves preferences',
    () async {
      await store.write('payments', 'summary', {'amount': 100});
      for (final key in [
        'payments.records.a',
        'students.byTeacher.a',
        'scheduling.lessons.a',
      ]) {
        await prefs.setString(key, 'private');
      }
      await store.clearAll();
      expect(prefs.getKeys(), {'theme'});
      expect(store.isStale, isFalse);
    },
  );
  test(
    'empty cached list is valid offline data and refresh clears stale state',
    () async {
      await store.load(
        feature: 'students',
        key: 'list',
        fetch: () async => [],
        decode: (v) => v as List,
      );
      final offline = await store.load(
        feature: 'students',
        key: 'list',
        fetch: () async => throw const ApiException(
          message: 'offline',
          kind: ApiErrorKind.network,
        ),
        decode: (v) => v as List,
      );
      expect(offline.value, isEmpty);
      expect(offline.isStale, isTrue);
      expect(store.staleSince.value, offline.savedAt);
      await store.load(
        feature: 'students',
        key: 'list',
        fetch: () async => ['fresh'],
        decode: (v) => v as List,
      );
      expect(store.isStale, isFalse);
    },
  );
  test(
    'corrupt cache is ignored and original network error survives',
    () async {
      await prefs.setString('cache.alice.students.list', '{broken');
      const offline = ApiException(
        message: 'offline',
        kind: ApiErrorKind.network,
      );
      await expectLater(
        store.load(
          feature: 'students',
          key: 'list',
          fetch: () async => throw offline,
          decode: (v) => v as List,
        ),
        throwsA(same(offline)),
      );
    },
  );
  test(
    'response arriving after logout cannot return or resurrect cached PII',
    () async {
      final response = Completer<Object?>();
      final loading = store.load(
        feature: 'students',
        key: 'list',
        fetch: () => response.future,
        decode: (v) => v as List,
      );
      final check = expectLater(loading, throwsA(isA<ApiException>()));
      await store.clearAll();
      store.activate('bob');
      response.complete(['Alice student']);
      await check;
      expect(prefs.getKeys(), {'theme'});
    },
  );
}
