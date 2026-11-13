import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/more/domain/device_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/more/presentation/cubit/device_sessions_cubit.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

/// M01-4 / D-03: "Hesabım hangi cihazlarda açık?" ekranı.
///
/// Güvenlik ekranı olduğu için iki kural var: (1) her işlemden sonra liste **sunucudan**
/// yeniden okunur — yerelde satır silmek, başarısız bir iptali başarılı göstermeye açık kapı
/// bırakır; (2) her kapatma **onay ister** — yanlışlıkla kendi cihazını kapatan kullanıcı
/// yeniden giriş yapmak zorunda kalır.
class DeviceSessionsPage extends StatelessWidget {
  const DeviceSessionsPage({super.key, this.cubit});

  /// Testlerin kendi cubit'ini vermesi için; verilmezse DI'dan kurulur.
  final DeviceSessionsCubit? cubit;

  @override
  Widget build(BuildContext context) {
    return BlocProvider<DeviceSessionsCubit>(
      create: (_) => (cubit ?? DeviceSessionsCubit.create())..load(),
      child: const _DeviceSessionsView(),
    );
  }
}

class _DeviceSessionsView extends StatelessWidget {
  const _DeviceSessionsView();

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(l10n.sessions_title)),
      body: BlocConsumer<DeviceSessionsCubit, DeviceSessionsState>(
        listenWhen: (previous, current) =>
            previous.lastRevokedCount != current.lastRevokedCount &&
            current.lastRevokedCount != null,
        listener: (context, state) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(l10n.sessions_closedCount(state.lastRevokedCount!))),
          );
        },
        builder: (context, state) {
          if (state.isLoading && state.sessions.isEmpty) {
            return const LoadingStateView();
          }

          return RefreshIndicator(
            onRefresh: () => context.read<DeviceSessionsCubit>().load(),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              children: <Widget>[
                Text(
                  l10n.sessions_subtitle,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 16),
                if (state.errorMessage != null) ...<Widget>[
                  ErrorStateView(
                    message: state.errorMessage!,
                    onRetry: () => context.read<DeviceSessionsCubit>().load(),
                  ),
                  const SizedBox(height: 16),
                ],
                if (state.sessions.isEmpty && state.errorMessage == null)
                  EmptyStateView(
                    title: l10n.sessions_empty,
                    subtitle: l10n.sessions_emptyHint,
                  ),
                for (final session in state.sessions) ...<Widget>[
                  _SessionCard(
                    session: session,
                    busy: state.busySessionId == session.sessionId,
                  ),
                  const SizedBox(height: 10),
                ],
                if (state.hasOtherSessions) ...<Widget>[
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                      onPressed: state.revokingAll
                          ? null
                          : () => _confirmRevokeAll(context, l10n),
                      icon: const Icon(Icons.logout_rounded),
                      label: Text(l10n.sessions_closeOthers),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.accentRed,
                        minimumSize: const Size.fromHeight(48),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }

  Future<void> _confirmRevokeAll(BuildContext context, AppLocalizations l10n) async {
    final cubit = context.read<DeviceSessionsCubit>();
    final confirmed = await _confirm(context, l10n, l10n.sessions_closeOthersConfirm);
    if (confirmed) {
      await cubit.revokeAll();
    }
  }
}

class _SessionCard extends StatelessWidget {
  const _SessionCard({required this.session, required this.busy});

  final DeviceSession session;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final format = DateFormat('d MMMM y, HH:mm', 'tr');

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: session.isCurrent
              ? AppColors.primary.withValues(alpha: 0.4)
              : Colors.black.withValues(alpha: 0.06),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            session.isCurrent ? Icons.phone_iphone_rounded : Icons.devices_other_rounded,
            color: session.isCurrent ? AppColors.primary : AppColors.textSecondary,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Flexible(
                      child: Text(
                        session.deviceName ?? l10n.sessions_unknownDevice,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    if (session.isCurrent) ...<Widget>[
                      const SizedBox(width: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          l10n.sessions_thisDevice,
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  l10n.sessions_connectedOn(format.format(session.createdOnUtc)),
                  style: theme.textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
                ),
                Text(
                  l10n.sessions_lastUsed(format.format(session.lastUsedOnUtc)),
                  style: theme.textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
                ),
              ],
            ),
          ),
          // Mevcut oturum buradan kapatılmaz: kullanıcı kendini atmış olur ve bunun adı
          // "çıkış yap"tır — o eylem zaten Diğer ekranında var.
          if (!session.isCurrent)
            TextButton(
              onPressed: busy ? null : () => _confirmRevoke(context, l10n),
              style: TextButton.styleFrom(foregroundColor: AppColors.accentRed),
              child: Text(l10n.sessions_close),
            ),
        ],
      ),
    );
  }

  Future<void> _confirmRevoke(BuildContext context, AppLocalizations l10n) async {
    final cubit = context.read<DeviceSessionsCubit>();
    final confirmed = await _confirm(context, l10n, l10n.sessions_closeOneConfirm);
    if (confirmed) {
      await cubit.revoke(session.sessionId);
    }
  }
}

/// Her iptal onay ister: yanlış cihazı kapatmak geri alınamaz, yeniden giriş gerektirir.
Future<bool> _confirm(BuildContext context, AppLocalizations l10n, String message) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: Text(l10n.sessions_title),
      content: Text(message),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: Text(l10n.common_cancel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: Text(l10n.sessions_close),
        ),
      ],
    ),
  );
  return result ?? false;
}
