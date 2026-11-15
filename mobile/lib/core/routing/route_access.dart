/// UI navigation permissions. Resource authorization remains on the server.
enum AppRole { teacher, student, parent, admin, unknown }

abstract final class RouteAccess {
  static const _teacher = {AppRole.teacher, AppRole.admin};
  static const _student = {AppRole.student};
  static const _parent = {AppRole.parent};
  static const _common = {
    AppRole.teacher,
    AppRole.student,
    AppRole.parent,
    AppRole.admin,
    AppRole.unknown,
  };

  /// Every registered private route must have an explicit entry.
  static const Map<String, Set<AppRole>> routes = {
    '/student-home': _student,
    '/student/studies': _student,
    '/student/performance': _student,
    '/student/lessons': _student,
    '/student/lessons/:id': _student,
    '/student/discover': _student,
    '/student/tests': _student,
    '/student/calendar': _student,
    '/student/goals-overview': _student,
    '/student/more': _student,
    '/student/profile': _student,
    '/student/teacher': _student,
    '/study/timer': _student,
    '/study/test': _student,
    '/study/catalog': _student,
    '/study/notes': _student,
    '/study/goals': _student,
    '/study/history': _student,
    '/study/achievements': _student,
    '/dashboard': _teacher,
    '/teacher-profile': _teacher,
    '/more': _teacher,
    '/membership': _common,
    '/membership/paywall': _common,
    '/messages': _common,
    '/messages/:conversationId': _common,
    '/settings/notifications': _common,
    '/settings/privacy': _common,
    '/account/sessions': _common,
    '/students': _teacher,
    '/students/:studentId': _teacher,
    '/scheduling': _teacher,
    '/lesson-sessions': _teacher,
    '/lesson-sessions/detail': _teacher,
    '/lesson-notes/new': _teacher,
    '/lesson-sessions/detail/note': _teacher,
    '/assignments': _teacher,
    '/student/assignments': _student,
    '/student/progress': _student,
    '/student/reports': _common,
    '/teacher/reports': _teacher,
    '/assignments/new': _teacher,
    '/assignments/:lessonSessionId': _teacher,
    '/payments/new': _teacher,
    '/payments/edit': _teacher,
    '/payments': _teacher,
    '/notifications': _common,
    '/parent': _parent,
    '/parent/children': _parent,
    '/parent/notifications': _parent,
    '/parent/profile': _parent,
    '/parent/child-detail': _parent,
    '/parent/assignments': _parent,
    '/parent/payments': _parent,
    '/parent/lessons': _parent,
  };

  static AppRole roleFor(List<String> roles) {
    if (roles.contains('Parent')) return AppRole.parent;
    if (roles.contains('Teacher')) return AppRole.teacher;
    if (roles.contains('Student')) return AppRole.student;
    if (roles.contains('Admin')) return AppRole.admin;
    return AppRole.unknown;
  }

  static String homeFor(AppRole role) => switch (role) {
    AppRole.parent => '/parent',
    AppRole.student => '/student-home',
    AppRole.teacher || AppRole.admin => '/dashboard',
    AppRole.unknown => '/notifications',
  };

  static bool canAccess(String location, AppRole role) {
    final pattern = _patternFor(location);
    return pattern != null && routes[pattern]!.contains(role);
  }

  /// Unknown locations must reach the router's error page, not another area.
  static String? redirectFor(String location, AppRole role) {
    final pattern = routes.containsKey(location)
        ? location
        : _patternFor(location);
    if (pattern == null || routes[pattern]!.contains(role)) return null;
    return homeFor(role);
  }

  static String? _patternFor(String location) {
    final uri = localUri(location);
    if (uri == null) return null;
    final segments = uri.pathSegments.toList();
    if (segments.isNotEmpty && segments.last.isEmpty) segments.removeLast();
    for (final pattern in routes.keys) {
      final expected = pattern.substring(1).split('/');
      if (expected.length != segments.length) continue;
      var matches = true;
      for (var i = 0; i < expected.length; i++) {
        if (expected[i].startsWith(':')) {
          if (!RegExp(r'^[a-zA-Z0-9_-]+$').hasMatch(segments[i])) {
            matches = false;
          }
        } else if (expected[i] != segments[i]) {
          matches = false;
        }
      }
      if (matches) return pattern;
    }
    return null;
  }

  /// Push payloads are data, never arbitrary external navigation.
  static Uri? localUri(String location) {
    if (!location.startsWith('/') ||
        location.startsWith('//') ||
        location.contains(r'\') ||
        RegExp(r'%(?![0-9a-fA-F]{2})').hasMatch(location)) {
      return null;
    }
    final uri = Uri.tryParse(location);
    if (uri == null || uri.hasScheme || uri.hasAuthority) return null;
    try {
      if (uri.pathSegments.any(
        (s) => s.contains('/') || s.contains(r'\') || s == '.' || s == '..',
      )) {
        return null;
      }
    } on FormatException {
      return null;
    }
    // Uri normalizes dot segments; reject them before that normalization too.
    if (location
        .split('?')
        .first
        .split('/')
        .any(
          (s) =>
              s == '.' ||
              s == '..' ||
              s.toLowerCase() == '%2e' ||
              s.toLowerCase() == '%2e%2e',
        )) {
      return null;
    }
    return uri;
  }
}
