import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_state.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// D-11 / M11-3 / M11-4: Rolden bağımsız bildirim merkezi.
/// "Tümü / Okunmamış" filtresi, okundu işareti ve `route` ile derin bağlantı.
class NotificationsPage extends StatelessWidget {
  const NotificationsPage({super.key, this.cubit});

  /// Testlerde hazır bir cubit verilebilir; üretimde DI'dan üretilir.
  final NotificationCenterCubit? cubit;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);

    if (userId == null) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: EmptyStateView(
            title: 'Bildirim yok',
            subtitle: 'Bildirimleri görmek için giriş yapmalısın.',
          ),
        ),
      );
    }

    return BlocProvider<NotificationCenterCubit>(
      create: (_) =>
          (cubit ?? NotificationCenterCubit.create())..load(userId),
      // Okundu işaretlendiğinde uygulama genelindeki zil rozeti de anında düşsün.
      child: BlocListener<NotificationCenterCubit, NotificationCenterState>(
        listenWhen: (previous, current) =>
            previous.unreadCount != current.unreadCount,
        listener: (context, state) {
          final badge = context.read<NotificationsCubit?>();
          badge?.setUnreadCount(state.unreadCount);
        },
        child: const _NotificationsView(),
      ),
    );
  }
}

class _NotificationsView extends StatelessWidget {
  const _NotificationsView();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            const _Header(),
            const _FilterTabs(),
            Expanded(
              child:
                  BlocBuilder<NotificationCenterCubit, NotificationCenterState>(
                    builder: (context, state) {
                      if (state.isLoading) {
                        return const LoadingStateView();
                      }
                      if (state.errorMessage != null) {
                        return ErrorStateView(
                          message: state.errorMessage!,
                          onRetry: () =>
                              context.read<NotificationCenterCubit>().refresh(),
                        );
                      }
                      if (state.items.isEmpty) {
                        return EmptyStateView(
                          title: 'Henüz bildirimin yok',
                          subtitle: state.onlyUnread
                              ? 'Okunmamış bildirimin yok.'
                              : 'Ders, ödev ve ödeme bildirimleri burada görünecek.',
                        );
                      }

                      return RefreshIndicator(
                        color: AppColors.primary,
                        onRefresh: () =>
                            context.read<NotificationCenterCubit>().refresh(),
                        child: ListView.builder(
                          padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
                          itemCount: state.items.length,
                          itemBuilder: (context, index) =>
                              _NotificationCard(item: state.items[index]),
                        ),
                      );
                    },
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header();

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
                BlocBuilder<
                  NotificationCenterCubit,
                  NotificationCenterState
                >(
                  buildWhen: (previous, current) =>
                      previous.unreadCount != current.unreadCount,
                  builder: (context, state) {
                    if (state.unreadCount == 0) {
                      return const SizedBox.shrink();
                    }
                    return Text(
                      '${state.unreadCount} okunmamış',
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

class _FilterTabs extends StatelessWidget {
  const _FilterTabs();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<NotificationCenterCubit, NotificationCenterState>(
      buildWhen: (previous, current) =>
          previous.onlyUnread != current.onlyUnread,
      builder: (context, state) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: Row(
            children: [
              _FilterChip(
                label: 'Tümü',
                selected: !state.onlyUnread,
                onTap: () =>
                    context.read<NotificationCenterCubit>().setOnlyUnread(false),
              ),
              const SizedBox(width: 8),
              _FilterChip(
                label: 'Okunmamış',
                selected: state.onlyUnread,
                onTap: () =>
                    context.read<NotificationCenterCubit>().setOnlyUnread(true),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _FilterChip extends StatelessWidget {
  const _FilterChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(20),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : AppColors.surface,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(
            color: selected ? AppColors.primary : AppColors.border,
          ),
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
            color: selected ? Colors.white : AppColors.textSecondary,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({required this.item});

  final UserNotification item;

  @override
  Widget build(BuildContext context) {
    final accentColor = item.isRead
        ? AppColors.textSecondary
        : AppColors.primary;

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: item.isRead ? AppColors.border : AppColors.primary,
        ),
        boxShadow: AppShadows.soft,
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => _onTap(context),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: item.isRead
                      ? AppColors.background
                      : AppColors.primaryLight,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(_iconFor(item.kind), color: accentColor, size: 20),
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
                            item.title,
                            style: Theme.of(context).textTheme.titleSmall
                                ?.copyWith(
                                  color: AppColors.textPrimary,
                                  fontWeight: item.isRead
                                      ? FontWeight.w600
                                      : FontWeight.w800,
                                ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          _relativeTime(item.createdOnUtc),
                          style: Theme.of(context).textTheme.bodySmall
                              ?.copyWith(
                                color: AppColors.textSecondary,
                                fontSize: 11,
                              ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      item.body,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                        height: 1.45,
                      ),
                    ),
                  ],
                ),
              ),
              if (!item.isRead) ...[
                const SizedBox(width: 8),
                Container(
                  width: 8,
                  height: 8,
                  margin: const EdgeInsets.only(top: 6),
                  decoration: const BoxDecoration(
                    color: AppColors.primary,
                    shape: BoxShape.circle,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  void _onTap(BuildContext context) {
    context.read<NotificationCenterCubit>().markRead(item.id);

    final route = item.route;
    if (route != null && route.isNotEmpty) {
      context.push(route);
    }
  }

  static IconData _iconFor(String kind) {
    switch (kind) {
      case 'LessonReminder':
      case 'LessonCompleted':
        return Icons.event_available_rounded;
      case 'AssignmentCreated':
      case 'AssignmentDue':
      case 'AssignmentMissed':
        return Icons.assignment_rounded;
      case 'PaymentDue':
      case 'PaymentOverdue':
        return Icons.payments_rounded;
      case 'MessageReceived':
        return Icons.chat_bubble_rounded;
      default:
        return Icons.notifications_rounded;
    }
  }

  static String _relativeTime(DateTime createdOnUtc) {
    final diff = DateTime.now().difference(createdOnUtc.toLocal());

    if (diff.inMinutes < 1) return 'az önce';
    if (diff.inMinutes < 60) return '${diff.inMinutes} dk önce';
    if (diff.inHours < 24) return '${diff.inHours} sa önce';
    if (diff.inDays < 7) return '${diff.inDays} gün önce';
    return '${(diff.inDays / 7).floor()} hf önce';
  }
}
