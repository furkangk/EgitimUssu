import 'dart:async';
import 'package:flutter/foundation.dart';

class PushEvent {
  const PushEvent(this.data);
  final Map<String, dynamic> data;
  String? get conversationId {
    final explicit = data['conversationId'];
    if (explicit is String && explicit.isNotEmpty) return explicit;
    final route = data['route'];
    if (route is! String) return null;
    final uri = Uri.tryParse(route);
    if (uri == null || uri.hasScheme || uri.hasAuthority) return null;
    final parts = uri.pathSegments;
    return parts.length == 2 && parts.first == 'messages' ? parts.last : null;
  }
}

/// Foreground hints trigger authenticated reads, never manufacture UI data.
class PushEventBus extends ChangeNotifier {
  String? _userId;
  int _generation = 0;
  bool _disposed = false;
  final _seen = <String>{};
  final _events = StreamController<PushEvent>.broadcast(sync: true);
  String? get userId => _userId;
  int get generation => _generation;
  Stream<PushEvent> get events => _events.stream;
  void activate(String? userId) {
    if (_disposed || userId == _userId) return;
    _userId = userId;
    _generation++;
    _seen.clear();
    notifyListeners();
  }

  void publish(Map<String, dynamic> data, {String? messageId}) {
    if (_disposed || _userId == null) return;
    if (messageId != null && messageId.isNotEmpty) {
      if (!_seen.add(messageId)) return;
      if (_seen.length > 128) _seen.remove(_seen.first);
    }
    _events.add(PushEvent(Map.unmodifiable(data)));
  }

  @override
  void dispose() {
    _disposed = true;
    _events.close();
    super.dispose();
  }
}
