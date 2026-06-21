import 'dart:async';

import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/assignment_follow_up_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/login_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/register_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/role_selection_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/welcome_page.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/pages/dashboard_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/pages/account_info_page.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/pages/more_page.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payment_form_page.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payments_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/pages/scheduling_page.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/pages/student_detail_page.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/pages/students_page.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/pages/teacher_profile_page.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class AppRouter {
  AppRouter({required AuthCubit authCubit, String initialLocation = '/'})
    : _refreshListenable = GoRouterRefreshStream(authCubit.stream) {
    router = GoRouter(
      initialLocation: initialLocation,
      refreshListenable: _refreshListenable,
      redirect: (context, state) {
        final status = authCubit.state.status;
        final onAuthScreen =
            state.matchedLocation == '/' ||
            state.matchedLocation == '/role-selection' ||
            state.matchedLocation == '/login' ||
            state.matchedLocation == '/register';
        final onPreviewScreen =
            state.matchedLocation == '/teacher-panel-preview' ||
            state.matchedLocation == '/account-info-preview';

        if (status == AuthStatus.initial || status == AuthStatus.loading) {
          return state.matchedLocation == '/' || onPreviewScreen ? null : '/';
        }

        if (status == AuthStatus.unauthenticated) {
          if (onPreviewScreen) {
            return null;
          }
          return onAuthScreen ? null : '/';
        }

        if (onAuthScreen || state.matchedLocation == '/') {
          return '/dashboard';
        }

        return null;
      },
      routes: <RouteBase>[
        GoRoute(path: '/', builder: (context, state) => const WelcomePage()),
        GoRoute(
          path: '/role-selection',
          builder: (context, state) => const RoleSelectionPage(),
        ),
        GoRoute(
          path: '/login',
          builder: (context, state) => LoginPage(
            selectedRole: state.uri.queryParameters['role'] ?? 'ogretmen',
          ),
        ),
        GoRoute(
          path: '/register',
          builder: (context, state) => const RegisterPage(),
        ),
        GoRoute(
          path: '/dashboard',
          builder: (context, state) => const DashboardPage(),
        ),
        GoRoute(
          path: '/teacher-panel-preview',
          builder: (context, state) => const TeacherPanelPreviewPage(),
        ),
        GoRoute(
          path: '/teacher-profile',
          builder: (context, state) => const TeacherProfilePage(),
        ),
        GoRoute(path: '/more', builder: (context, state) => const MorePage()),
        GoRoute(
          path: '/account-info',
          builder: (context, state) => const AccountInfoPage(),
        ),
        GoRoute(
          path: '/account-info-preview',
          builder: (context, state) => const AccountInfoPage(),
        ),
        GoRoute(
          path: '/students',
          builder: (context, state) => const StudentsPage(),
        ),
        GoRoute(
          path: '/students/:studentId',
          builder: (context, state) => StudentDetailPage(
            studentId: state.pathParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/scheduling',
          builder: (context, state) => const SchedulingPage(),
        ),
        GoRoute(
          path: '/lesson-sessions',
          builder: (context, state) => LessonSessionsPage(
            openCreateOnStart: state.uri.queryParameters['create'] == '1',
          ),
        ),
        GoRoute(
          path: '/lesson-sessions/detail',
          builder: (context, state) => LessonDetailPage(
            payload: state.extra is LessonDetailPayload
                ? state.extra as LessonDetailPayload
                : null,
          ),
        ),
        GoRoute(
          path: '/lesson-notes/new',
          builder: (context, state) => LessonNoteFormPage(
            initialContext: state.extra is LessonNoteFormContext
                ? state.extra as LessonNoteFormContext
                : null,
          ),
        ),
        GoRoute(
          path: '/lesson-sessions/detail/note',
          builder: (context, state) => LessonNoteViewPage(
            payload: state.extra is LessonNoteViewPayload
                ? state.extra as LessonNoteViewPayload
                : null,
          ),
        ),
        GoRoute(
          path: '/assignments/new',
          builder: (context, state) =>
              const AssignmentFollowUpPage(lessonSessionId: ''),
        ),
        GoRoute(
          path: '/assignments/:lessonSessionId',
          builder: (context, state) => AssignmentFollowUpPage(
            lessonSessionId: state.pathParameters['lessonSessionId'] ?? '',
            initialContext: state.extra is AssignmentFormContext
                ? state.extra as AssignmentFormContext
                : null,
          ),
        ),
        GoRoute(
          path: '/payments/new',
          builder: (context, state) => const PaymentFormPage(),
        ),
        GoRoute(
          path: '/payments',
          builder: (context, state) => const PaymentsPage(),
        ),
      ],
    );
  }

  final GoRouterRefreshStream _refreshListenable;
  late final GoRouter router;

  void dispose() {
    _refreshListenable.dispose();
  }
}

class GoRouterRefreshStream extends ChangeNotifier {
  GoRouterRefreshStream(Stream<dynamic> stream) {
    _subscription = stream.asBroadcastStream().listen((_) => notifyListeners());
  }

  late final StreamSubscription<dynamic> _subscription;

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
