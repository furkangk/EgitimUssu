import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('session gate and bounded dedup deliver one refresh hint', () {
    final bus = PushEventBus();
    final ids = <String?>[];
    bus.events.listen((e) => ids.add(e.conversationId));
    bus.publish({'route': '/messages/c1'}, messageId: 'm');
    bus.activate('a');
    bus.publish({'route': '/messages/c1'}, messageId: 'm');
    bus.publish({'route': '/messages/c1'}, messageId: 'm');
    expect(ids, ['c1']);
    final epoch = bus.generation;
    bus.activate(null);
    expect(bus.generation, greaterThan(epoch));
    bus.publish({'conversationId': 'c2'});
    expect(ids, ['c1']);
    bus.activate('b');
    bus.publish({'conversationId': 'c2'}, messageId: 'm');
    expect(ids, ['c1', 'c2']);
    bus.dispose();
  });
}
