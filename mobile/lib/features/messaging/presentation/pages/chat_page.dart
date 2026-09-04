import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/pages/messaging_inbox_page.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/widgets/messaging_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/session_required_view.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// D-08 / M16-1: Tek sohbet ekranı — balonlar, eskiye kaydırınca sayfalama, iyimser gönderim.
class ChatPage extends StatelessWidget {
  const ChatPage({required this.conversationId, super.key, this.cubit, this.detail});

  final String conversationId;

  /// Testlerde hazır bir cubit verilebilir; üretimde DI'dan üretilir.
  final ChatCubit? cubit;

  /// Sohbet gelen kutusundan değil de "Mesaj gönder" ile açıldıysa başlık bilgisi hazırdır.
  final ConversationDetail? detail;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);

    if (userId == null) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: SessionRequiredView(
            title: 'Sohbet',
            subtitle: 'Mesajlaşmak için giriş yapmalısın.',
          ),
        ),
      );
    }

    return BlocProvider<ChatCubit>(
      create: (_) =>
          (cubit ?? ChatCubit.create(userId))..load(conversationId, detail: detail),
      child: const _ChatView(),
    );
  }
}

class _ChatView extends StatefulWidget {
  const _ChatView();

  @override
  State<_ChatView> createState() => _ChatViewState();
}

class _ChatViewState extends State<_ChatView> {
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController
      ..removeListener(_onScroll)
      ..dispose();
    super.dispose();
  }

  /// Liste **ters** çizilir (en yeni altta), bu yüzden "eskiye kaydırmak" maxScrollExtent'e
  /// yaklaşmaktır — sonsuz scroll; "daha fazla yükle" düğmesi yok (ux_rules §6).
  void _onScroll() {
    if (!_scrollController.hasClients) {
      return;
    }
    final position = _scrollController.position;
    if (position.pixels >= position.maxScrollExtent - 240) {
      context.read<ChatCubit>().loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            BlocBuilder<ChatCubit, ChatState>(
              buildWhen: (previous, current) =>
                  previous.conversation?.isBlocked != current.conversation?.isBlocked ||
                  previous.conversation?.counterpartRole !=
                      current.conversation?.counterpartRole,
              builder: (context, state) {
                final role = state.conversation?.counterpartRole;
                return MessagingHeader(
                  title: role == null ? 'Sohbet' : conversationTitleFor(role),
                  subtitle: state.conversation?.isBlocked ?? false
                      ? 'Engellendi'
                      : null,
                  trailing: const _BlockMenu(),
                );
              },
            ),
            Expanded(
              child: BlocConsumer<ChatCubit, ChatState>(
                listenWhen: (previous, current) =>
                    previous.errorMessage != current.errorMessage &&
                    current.errorMessage != null,
                listener: (context, state) {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(
                      SnackBar(
                        content: Text(state.errorMessage!),
                        backgroundColor: AppColors.accentRed,
                      ),
                    );
                  context.read<ChatCubit>().clearError();
                },
                builder: (context, state) {
                  if (state.isLoading) {
                    return const LoadingStateView(message: 'Mesajlar yükleniyor...');
                  }
                  if (state.status == ChatStatus.failure) {
                    return ErrorStateView(
                      message: state.errorMessage ?? 'Mesajlar alınamadı.',
                      onRetry: () =>
                          context.read<ChatCubit>().load(state.conversationId),
                    );
                  }
                  if (state.messages.isEmpty) {
                    return const EmptyStateView(
                      title: 'Sohbet boş',
                      subtitle: 'İlk mesajı sen yaz.',
                    );
                  }

                  final currentUserId = context.read<ChatCubit>().currentUserId;

                  return ListView.builder(
                    controller: _scrollController,
                    // Ters liste: en yeni mesaj altta, kaydırma yukarı doğru geçmişe gider.
                    reverse: true,
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
                    itemCount: state.messages.length + (state.isLoadingMore ? 1 : 0),
                    itemBuilder: (context, index) {
                      if (index >= state.messages.length) {
                        return const Padding(
                          padding: EdgeInsets.symmetric(vertical: 16),
                          child: Center(
                            child: SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            ),
                          ),
                        );
                      }

                      final message = state.messages[index];
                      return MessageBubble(
                        message: message,
                        isMine: message.senderUserId == currentUserId,
                        onRetry: () => context.read<ChatCubit>().retry(message.id),
                      );
                    },
                  );
                },
              ),
            ),
            BlocBuilder<ChatCubit, ChatState>(
              buildWhen: (previous, current) => previous.canSend != current.canSend,
              builder: (context, state) => MessageComposer(
                enabled: state.canSend,
                onSend: (text) => context.read<ChatCubit>().send(text),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BlockMenu extends StatelessWidget {
  const _BlockMenu();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<ChatCubit, ChatState>(
      buildWhen: (previous, current) =>
          previous.canSend != current.canSend || previous.canUnblock != current.canUnblock,
      builder: (context, state) {
        // Karşı tarafın koyduğu engeli kaldıramazsın: menüde tek seçenek de kalmazsa
        // düğmeyi hiç göstermeyiz (ölü menü açmak yanıltıcı olurdu).
        final blocked = !state.canSend;
        if (blocked && !state.canUnblock) {
          return const SizedBox.shrink();
        }

        return PopupMenuButton<String>(
          icon: const Icon(Icons.more_vert_rounded, color: AppColors.textSecondary),
          onSelected: (value) async {
            final cubit = context.read<ChatCubit>();
            if (value == 'block') {
              final confirmed = await _confirmBlock(context);
              if (confirmed) {
                await cubit.setBlocked(true);
              }
              return;
            }
            await cubit.setBlocked(false);
          },
          itemBuilder: (context) => <PopupMenuEntry<String>>[
            if (state.canUnblock)
              const PopupMenuItem<String>(
                value: 'unblock',
                child: Text('Engeli kaldır'),
              )
            else
              const PopupMenuItem<String>(
                value: 'block',
                child: Text('Engelle'),
              ),
          ],
        );
      },
    );
  }

  /// Engelleme yıkıcı bir işlemdir: onay diyalogundan geçer (ux_rules §2/§5).
  Future<bool> _confirmBlock(BuildContext context) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Sohbeti engelle'),
        content: const Text(
          'Engellersen bu sohbette iki taraf da mesaj gönderemez. '
          'Engeli daha sonra kaldırabilirsin.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Vazgeç'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColors.accentRed),
            child: const Text('Engelle'),
          ),
        ],
      ),
    );
    return result ?? false;
  }
}
