import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/conversations_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/widgets/messaging_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/session_required_view.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Gelen kutusu rotası.
const String kMessagingInboxRoute = '/messages';

/// D-08 / M16-1: Gelen kutusu — sohbet listesi, son mesaj önizlemesi ve okunmamış rozeti.
class MessagingInboxPage extends StatelessWidget {
  const MessagingInboxPage({super.key, this.cubit});

  /// Testlerde hazır bir cubit verilebilir; üretimde DI'dan üretilir.
  final ConversationsCubit? cubit;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);

    // D-17: Oturum yokken sahte bir kullanıcı uydurmak yerine açıkça giriş isteriz.
    if (userId == null) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: SessionRequiredView(
            title: 'Mesajlar',
            subtitle: 'Mesajlarını görmek için giriş yapmalısın.',
          ),
        ),
      );
    }

    return BlocProvider<ConversationsCubit>(
      create: (_) => (cubit ?? ConversationsCubit.create())..load(),
      child: const _InboxView(),
    );
  }
}

class _InboxView extends StatelessWidget {
  const _InboxView();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            BlocBuilder<ConversationsCubit, ConversationsState>(
              buildWhen: (previous, current) =>
                  previous.totalUnread != current.totalUnread,
              builder: (context, state) => MessagingHeader(
                title: 'Mesajlar',
                subtitle: state.totalUnread > 0
                    ? '${state.totalUnread} okunmamış mesaj'
                    : null,
              ),
            ),
            Expanded(
              child: BlocBuilder<ConversationsCubit, ConversationsState>(
                builder: (context, state) {
                  if (state.isLoading) {
                    return const LoadingStateView(message: 'Mesajlar yükleniyor...');
                  }
                  if (state.status == ConversationsStatus.failure) {
                    return ErrorStateView(
                      message: state.errorMessage ?? 'Mesajlar alınamadı.',
                      onRetry: () => context.read<ConversationsCubit>().refresh(),
                    );
                  }
                  if (state.conversations.isEmpty) {
                    return const EmptyStateView(
                      title: 'Henüz mesajın yok',
                      subtitle:
                          'Öğrenci veya veli sayfasından "Mesaj gönder" ile sohbet başlatabilirsin.',
                    );
                  }

                  return RefreshIndicator(
                    color: AppColors.primary,
                    onRefresh: () => context.read<ConversationsCubit>().refresh(),
                    child: ListView.builder(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
                      itemCount: state.conversations.length,
                      itemBuilder: (context, index) {
                        final row = state.conversations[index];
                        return ConversationTile(
                          conversation: row,
                          title: conversationTitleFor(row.counterpartRole),
                          onTap: () async {
                            await context.push('$kMessagingInboxRoute/${row.id}');
                            if (context.mounted) {
                              // Sohbetten dönünce okunmamış rozeti gerçeği yansıtmalı.
                              await context.read<ConversationsCubit>().refresh();
                            }
                          },
                        );
                      },
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

/// Karşı tarafın görünen adı.
///
/// ⚠️ Sunucu bu uçta **ad döndürmüyor** (yalnız `counterpartUserId`). Uydurma bir ad
/// göstermek yerine rolü yazarız; ad alanı M16'nın sonraki turunda gelecek.
String conversationTitleFor(String counterpartRole) => switch (counterpartRole) {
  kParticipantRoleTeacher => 'Öğretmen',
  kParticipantRoleParent => 'Veli',
  kParticipantRoleStudent => 'Öğrenci',
  _ => 'Sohbet',
};
