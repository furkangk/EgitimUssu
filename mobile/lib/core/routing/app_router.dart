import 'dart:async';

import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/assignment_follow_up_page.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/assignments_page.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/student_assignments_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/login_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/register_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/role_selection_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/welcome_page.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/pages/dashboard_page.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/pages/notifications_page.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/pages/parent_child_detail_page.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/pages/parent_children_page.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/pages/parent_home_page.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/pages/parent_notifications_page.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/pages/parent_profile_page.dart';
import 'package:egitim_ussu_mobile/features/progress/presentation/pages/progress_overview_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/pages/account_info_page.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/pages/more_page.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payment_form_page.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/pages/payments_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/pages/scheduling_page.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/pages/student_detail_page.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/pages/students_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/achievements_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_calendar_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_goals_overview_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_home_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_more_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_profile_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_studies_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_teacher_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_tests_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/study_goals_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/study_history_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/study_notes_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/study_timer_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/subject_catalog_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/test_entry_page.dart';
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

        if (status == AuthStatus.loading) {
          return null;
        }

        if (status == AuthStatus.initial) {
          return state.matchedLocation == '/' || onPreviewScreen ? null : '/';
        }

        if (status == AuthStatus.unauthenticated) {
          if (onPreviewScreen) {
            return null;
          }
          return onAuthScreen ? null : '/';
        }

        // Rol bazlı yönlendirme: veli kendi paneline, öğrenci çalışma paneline,
        // diğerleri (öğretmen/admin) öğretmen paneline.
        final roles = authCubit.state.session?.roles ?? const <String>[];
        final isParent = roles.contains('Parent');
        final isStudent =
            !isParent && roles.contains('Student') && !roles.contains('Teacher');
        final home = isParent
            ? '/parent'
            : isStudent
            ? '/student-home'
            : '/dashboard';

        if (onAuthScreen || state.matchedLocation == '/') {
          return home;
        }

        // Veli, öğretmen-özel ekranlara düşerse kendi paneline geri al (ve tersi).
        final onParentArea = state.matchedLocation.startsWith('/parent');
        if (isParent && !onParentArea) {
          return '/parent';
        }
        if (!isParent && onParentArea) {
          return '/dashboard';
        }

        // Öğrenci, öğretmene özel ekranlara düşerse kendi paneline geri al.
        const teacherOnly = <String>[
          '/dashboard',
          '/students',
          '/scheduling',
          '/lesson-sessions',
          '/lesson-notes',
          '/assignments',
          '/payments',
          '/teacher-profile',
        ];
        if (isStudent &&
            teacherOnly.any((path) => state.matchedLocation.startsWith(path))) {
          return '/student-home';
        }

        return null;
      },
      routes: <RouteBase>[
        GoRoute(path: '/', builder: (context, state) => const WelcomePage()),
        GoRoute(
          path: '/student-home',
          builder: (context, state) => const StudentHomePage(),
        ),
        // Öğrenci alt navigasyon sekmeleri (ogrenci_ux §4).
        GoRoute(
          path: '/student/studies',
          builder: (context, state) => const StudentStudiesPage(),
        ),
        GoRoute(
          path: '/student/tests',
          builder: (context, state) => const StudentTestsPage(),
        ),
        GoRoute(
          path: '/student/calendar',
          builder: (context, state) => const StudentCalendarPage(),
        ),
        GoRoute(
          path: '/student/goals-overview',
          builder: (context, state) => const StudentGoalsOverviewPage(),
        ),
        GoRoute(
          path: '/student/more',
          builder: (context, state) => const StudentMorePage(),
        ),
        GoRoute(
          path: '/student/profile',
          builder: (context, state) => const StudentProfilePage(),
        ),
        GoRoute(
          path: '/student/teacher',
          builder: (context, state) => const StudentTeacherPage(),
        ),
        GoRoute(
          path: '/study/timer',
          builder: (context, state) => StudyTimerPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
            initialSubject: state.uri.queryParameters['subject'],
            initialTopic: state.uri.queryParameters['topic'],
          ),
        ),
        GoRoute(
          path: '/study/test',
          builder: (context, state) => TestEntryPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/study/catalog',
          builder: (context, state) => SubjectCatalogPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/study/notes',
          builder: (context, state) => StudyNotesPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/study/goals',
          builder: (context, state) => StudyGoalsPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/study/history',
          builder: (context, state) => StudyHistoryPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
        GoRoute(
          path: '/study/achievements',
          builder: (context, state) => AchievementsPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
          ),
        ),
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
          builder: (context, state) => RegisterPage(
            role: state.uri.queryParameters['role'] ?? 'ogretmen',
          ),
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
          path: '/assignments',
          builder: (context, state) => const AssignmentsPage(),
        ),
        GoRoute(
          path: '/student/assignments',
          builder: (context, state) => const StudentAssignmentsPage(),
        ),
        GoRoute(
          path: '/student/progress',
          builder: (context, state) => ProgressOverviewPage(
            studentId: state.uri.queryParameters['studentId'] ?? '',
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
          path: '/payments/edit',
          builder: (context, state) => PaymentFormPage(
            record: state.extra is PaymentRecord
                ? state.extra as PaymentRecord
                : null,
          ),
        ),
        GoRoute(
          path: '/payments',
          builder: (context, state) => const PaymentsPage(),
        ),
        GoRoute(
          path: '/notifications',
          builder: (context, state) => const NotificationsPage(),
        ),
        // Veli (Parent) paneli
        GoRoute(
          path: '/parent',
          builder: (context, state) => const ParentHomePage(),
        ),
        GoRoute(
          path: '/parent/children',
          builder: (context, state) => const ParentChildrenPage(),
        ),
        GoRoute(
          path: '/parent/notifications',
          builder: (context, state) => const ParentNotificationsPage(),
        ),
        GoRoute(
          path: '/parent/profile',
          builder: (context, state) => const ParentProfilePage(),
        ),
        GoRoute(
          path: '/parent/child-detail',
          builder: (context, state) => ParentChildDetailPage(
            studentId: state.extra is String ? state.extra as String : '',
          ),
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
