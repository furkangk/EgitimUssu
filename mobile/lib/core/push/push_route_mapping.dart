import '../routing/route_access.dart';

/// Shared by OS push taps and the in-app notification center.
String mapPushRoute(String? route, AppRole role) {
  const fallback = '/notifications';
  if (route == null) return fallback;
  final uri = RouteAccess.localUri(route);
  if (uri == null) return fallback;
  final path = switch ((role, uri.path)) {
    (AppRole.parent, '/scheduling') => '/parent/lessons',
    (AppRole.student, '/scheduling') => '/student/lessons',
    (AppRole.parent, '/payments') => '/parent/payments',
    (AppRole.parent, '/assignments' || '/student/assignments') =>
      '/parent/assignments',
    (AppRole.student, '/assignments') => '/student/assignments',
    _ => uri.path,
  };
  final target = uri.replace(path: path).toString();
  return RouteAccess.canAccess(target, role) ? target : fallback;
}
