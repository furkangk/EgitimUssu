import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

class NotificationsPage extends StatelessWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final teacherUserId = context.select(
      (AuthCubit c) => c.state.session?.userId ?? 'mock-teacher-user',
    );

    return BlocProvider(
      create: (_) => NotificationsCubit.create()..load(teacherUserId),
      child: Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: Column(
            children: [
              _Header(),
              Expanded(
                child: BlocBuilder<NotificationsCubit, NotificationsState>(
                  builder: (context, state) {
                    if (state.isLoading) {
                      return const LoadingStateView();
                    }
                    if (state.errorMessage != null) {
                      return ErrorStateView(
                        message: state.errorMessage!,
                        onRetry: () => context.read<NotificationsCubit>().load(
                          teacherUserId,
                        ),
                      );
                    }
                    if (state.reminders.isEmpty) {
                      return const EmptyStateView(
                        title: 'Bildirim yok',
                        subtitle: 'Ders hatırlatmaları burada görünecek.',
                      );
                    }
                    return RefreshIndicator(
                      color: AppColors.primary,
                      onRefresh: () => context
                          .read<NotificationsCubit>()
                          .refresh(teacherUserId),
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
                        children: [
                          if (state.pending.isNotEmpty) ...[
                            _SectionLabel(
                              label: 'Yaklaşan (${state.pending.length})',
                              color: AppColors.accentOrange,
                            ),
                            const SizedBox(height: 8),
                            ...state.pending.map(
                              (r) =>
                                  _ReminderCard(reminder: r, isPending: true),
                            ),
                          ],
                          if (state.past.isNotEmpty) ...[
                            if (state.pending.isNotEmpty)
                              const SizedBox(height: 20),
                            _SectionLabel(
                              label: 'Geçmiş',
                              color: AppColors.textSecondary,
                            ),
                            const SizedBox(height: 8),
                            ...state.past.map(
                              (r) =>
                                  _ReminderCard(reminder: r, isPending: false),
                            ),
                          ],
                        ],
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        children: [
          InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: () => Navigator.of(context).pop(),
            child: Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.border),
              ),
              child: const Icon(
                Icons.arrow_back_rounded,
                color: AppColors.primary,
                size: 20,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Bildirimler',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                BlocBuilder<NotificationsCubit, NotificationsState>(
                  builder: (context, state) {
                    if (state.unreadCount == 0) return const SizedBox.shrink();
                    return Text(
                      '${state.unreadCount} bekleyen',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    );
                  },
                ),
              ],
            ),
          ),
          const Icon(
            Icons.notifications_rounded,
            color: AppColors.primary,
            size: 24,
          ),
        ],
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 3,
          height: 14,
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const SizedBox(width: 8),
        Text(
          label,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
            color: AppColors.textSecondary,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.3,
          ),
        ),
      ],
    );
  }
}

class _ReminderCard extends StatelessWidget {
  const _ReminderCard({required this.reminder, required this.isPending});

  final LessonReminder reminder;
  final bool isPending;

  @override
  Widget build(BuildContext context) {
    final accentColor = isPending ? AppColors.primary : AppColors.accentGreen;
    final bgColor = isPending
        ? AppColors.primaryLight
        : const Color(0xFFEAF7F2);
    final iconData = isPending
        ? Icons.notifications_active_rounded
        : Icons.check_circle_rounded;

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: bgColor,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(iconData, color: accentColor, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Text(
                          reminder.title,
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        _formatTime(reminder.remindAtUtc),
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          fontSize: 11,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    reminder.message,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                      height: 1.45,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Icon(
                        Icons.calendar_today_rounded,
                        size: 12,
                        color: AppColors.textSecondary,
                      ),
                      const SizedBox(width: 4),
                      Text(
                        'Ders: ${_formatLesson(reminder.scheduledLessonStartAtUtc)}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          fontSize: 11,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _formatTime(DateTime dt) {
    final now = DateTime.now();
    final local = dt.toLocal();
    final diff = now.difference(local);

    if (diff.inSeconds < 0) {
      final future = local.difference(now);
      if (future.inMinutes < 60) return '${future.inMinutes} dk sonra';
      if (future.inHours < 24) return '${future.inHours} sa sonra';
      return '${future.inDays} gün sonra';
    }
    if (diff.inMinutes < 60) return '${diff.inMinutes} dk önce';
    if (diff.inHours < 24) return '${diff.inHours} sa önce';
    return '${diff.inDays} gün önce';
  }

  String _formatLesson(DateTime dt) {
    final local = dt.toLocal();
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final tomorrow = today.add(const Duration(days: 1));
    final day = DateTime(local.year, local.month, local.day);

    final timeStr = DateFormat('HH:mm').format(local);
    if (day == today) return 'Bugün $timeStr';
    if (day == tomorrow) return 'Yarın $timeStr';
    return DateFormat('d MMM HH:mm', 'tr_TR').format(local);
  }
}
