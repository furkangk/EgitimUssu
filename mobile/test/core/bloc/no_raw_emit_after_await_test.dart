import 'dart:io';
import 'package:flutter_test/flutter_test.dart';

void main() {
  // Stronger than line-based await scanning: every emission in these async
  // Cubits goes through the shared lifecycle guard, including catch/finally.
  const paths = <String>[
    'lib/features/messaging/presentation/cubit/chat_cubit.dart',
    'lib/features/messaging/presentation/cubit/conversations_cubit.dart',
    'lib/features/reporting/presentation/cubit/student_report_cubit.dart',
    'lib/features/assignments/presentation/cubit/lesson_resources_cubit.dart',
    'lib/features/progress/presentation/cubit/progress_cubit.dart',
    'lib/features/reporting/presentation/cubit/teacher_reports_cubit.dart',
    'lib/features/settings/presentation/cubit/settings_cubit.dart',
    'lib/features/notifications/presentation/cubit/notification_center_cubit.dart',
    'lib/features/auth/presentation/cubit/password_reset_cubit.dart',
    'lib/features/auth/presentation/cubit/email_verification_cubit.dart',
    'lib/features/auth/presentation/cubit/auth_cubit.dart',
  ];
  for (final path in paths) {
    test('$path uses lifecycle-safe emission', () {
      final source = File(path).readAsStringSync();
      expect(source, contains('with SafeEmit<'));
      expect(RegExp(r'\bemit\s*\(').hasMatch(source), isFalse);
    });
  }
}
