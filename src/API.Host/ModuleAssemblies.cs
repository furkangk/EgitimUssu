using System.Reflection;
        using EgitimUssu.Modules.Identity.API;
using EgitimUssu.Modules.Teachers.API;
using EgitimUssu.Modules.Students.API;
using EgitimUssu.Modules.Scheduling.API;
using EgitimUssu.Modules.LessonSessions.API;
using EgitimUssu.Modules.Assignments.API;
using EgitimUssu.Modules.Payments.API;
using EgitimUssu.Modules.Study.API;
using EgitimUssu.Modules.Parents.API;
using EgitimUssu.Modules.ProgressTracking.API;
using EgitimUssu.Modules.Notifications.API;
using EgitimUssu.Modules.Matching.API;
using EgitimUssu.Modules.Reviews.API;
using EgitimUssu.Modules.Reporting.API;
using EgitimUssu.Modules.Settings.API;

        namespace EgitimUssu.API.Host;

        public static class ModuleAssemblies
        {
            public static readonly Assembly[] All =
            [
                typeof(IdentityModule).Assembly,
        typeof(TeachersModule).Assembly,
        typeof(StudentsModule).Assembly,
        typeof(SchedulingModule).Assembly,
        typeof(LessonSessionsModule).Assembly,
        typeof(AssignmentsModule).Assembly,
        typeof(PaymentsModule).Assembly,
        typeof(StudyModule).Assembly,
        typeof(ParentsModule).Assembly,
        typeof(ProgressTrackingModule).Assembly,
        typeof(NotificationsModule).Assembly,
        typeof(MatchingModule).Assembly,
        typeof(ReviewsModule).Assembly,
        typeof(ReportingModule).Assembly,
        typeof(SettingsModule).Assembly
            ];
        }
