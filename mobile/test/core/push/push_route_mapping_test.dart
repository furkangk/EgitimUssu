import 'package:egitim_ussu_mobile/core/push/push_route_mapping.dart';
import 'package:egitim_ussu_mobile/core/routing/route_access.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('backend reminder routes map to the receiving role', () {
    expect(mapPushRoute('/scheduling', AppRole.teacher), '/scheduling');
    expect(mapPushRoute('/scheduling', AppRole.student), '/student/lessons');
    expect(mapPushRoute('/scheduling', AppRole.parent), '/parent/lessons');
    expect(mapPushRoute('/payments', AppRole.parent), '/parent/payments');
    expect(
      mapPushRoute('/assignments', AppRole.student),
      '/student/assignments',
    );
    expect(
      mapPushRoute('/student/assignments', AppRole.parent),
      '/parent/assignments',
    );
    expect(
      mapPushRoute('/parent/payments', AppRole.parent),
      '/parent/payments',
    );
    expect(
      mapPushRoute('/parent/assignments', AppRole.parent),
      '/parent/assignments',
    );
    expect(mapPushRoute('/parent/lessons', AppRole.parent), '/parent/lessons');
    expect(
      mapPushRoute('/messages/chat-1?from=push', AppRole.parent),
      '/messages/chat-1?from=push',
    );
  });
  for (final role in AppRole.values) {
    test('invalid or unsupported push targets fall back safely for $role', () {
      for (final target in [
        null,
        '',
        'https://evil.test/messages',
        '//evil.test/messages',
        'javascript:alert(1)',
        'messages',
        '/unknown',
        '/messages/one/extra',
        '/settings/unknown',
        '/teacher-panel-preview',
        '/login',
        '/messages/%ZZ',
        '/messages/%2f',
        '/messages/..',
        '/messages/\\evil',
      ]) {
        expect(mapPushRoute(target, role), '/notifications', reason: '$target');
      }
    });
  }
  test('cross-role routes without an equivalent do not navigate', () {
    expect(mapPushRoute('/payments/new', AppRole.student), '/notifications');
    expect(mapPushRoute('/students/other', AppRole.parent), '/notifications');
    expect(mapPushRoute('/parent/payments', AppRole.teacher), '/notifications');
  });
}
