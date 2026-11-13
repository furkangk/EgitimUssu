import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:flutter/material.dart';

/// D-08: Mesajlaşma ekranlarının ortak geri-başlıklı üst barı.
class MessagingHeader extends StatelessWidget {
  const MessagingHeader({
    required this.title,
    super.key,
    this.subtitle,
    this.trailing,
  });

  final String title;
  final String? subtitle;
  final Widget? trailing;

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
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                if (subtitle != null)
                  Text(
                    subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
          if (trailing != null) trailing!,
        ],
      ),
    );
  }
}

/// Gelen kutusundaki tek sohbet satırı: avatar + ad + önizleme + göreli zaman + rozet.
class ConversationTile extends StatelessWidget {
  const ConversationTile({
    required this.conversation,
    required this.title,
    required this.onTap,
    super.key,
  });

  final ConversationSummary conversation;

  /// Karşı tarafın görünen adı; henüz çözülemediyse rol etiketi gösterilir.
  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final preview = conversation.lastMessagePreview;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Material(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          splashColor: AppColors.primaryLight,
          onTap: onTap,
          child: Container(
            // Liste öğesi dokunma hedefi en az 56px (ux_rules §6).
            constraints: const BoxConstraints(minHeight: 56),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: AppColors.border),
              boxShadow: AppShadows.soft,
            ),
            child: Row(
              children: [
                _Avatar(label: title, role: conversation.counterpartRole),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              title,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.titleSmall
                                  ?.copyWith(
                                    color: AppColors.textPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            relativeTime(conversation.lastMessageOnUtc),
                            style: Theme.of(context).textTheme.bodySmall
                                ?.copyWith(color: AppColors.textMuted),
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              // Önizleme yoksa "henüz mesaj yok" demek, boş satır göstermekten
                              // dürüsttür: sohbet açılmış ama kimse yazmamış olabilir.
                              preview ?? 'Henüz mesaj yok',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.bodyMedium
                                  ?.copyWith(
                                    color: preview == null
                                        ? AppColors.textMuted
                                        : AppColors.textSecondary,
                                    fontWeight: conversation.hasUnread
                                        ? FontWeight.w700
                                        : FontWeight.w500,
                                  ),
                            ),
                          ),
                          if (conversation.isBlocked) ...[
                            const SizedBox(width: 8),
                            const Icon(
                              Icons.block_rounded,
                              size: 16,
                              color: AppColors.error,
                            ),
                          ],
                          if (conversation.hasUnread) ...[
                            const SizedBox(width: 8),
                            _UnreadBadge(count: conversation.unreadCount),
                          ],
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.label, required this.role});

  final String label;
  final String role;

  @override
  Widget build(BuildContext context) {
    final color = switch (role) {
      kParticipantRoleParent => AppColors.accentTeal,
      kParticipantRoleTeacher => AppColors.primary,
      _ => AppColors.accentBlue,
    };

    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        shape: BoxShape.circle,
      ),
      child: Text(
        initialsOf(label),
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          color: color,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _UnreadBadge extends StatelessWidget {
  const _UnreadBadge({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.accentRed,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        count > 99 ? '99+' : '$count',
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
          color: AppColors.surface,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

/// Sohbetteki tek mesaj balonu.
class MessageBubble extends StatelessWidget {
  const MessageBubble({
    required this.message,
    required this.isMine,
    required this.onRetry,
    super.key,
  });

  final ChatMessage message;
  final bool isMine;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final body = message.body;
    final failed = message.status == ChatMessageStatus.failed;

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.sizeOf(context).width * 0.78,
        ),
        child: Column(
          crossAxisAlignment: isMine
              ? CrossAxisAlignment.end
              : CrossAxisAlignment.start,
          children: [
            Container(
              margin: const EdgeInsets.only(bottom: 4),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: message.isDeleted
                    ? AppColors.tabBackground
                    : (isMine ? AppColors.primary : AppColors.surface),
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(16),
                  topRight: const Radius.circular(16),
                  bottomLeft: Radius.circular(isMine ? 16 : 4),
                  bottomRight: Radius.circular(isMine ? 4 : 16),
                ),
                border: Border.all(
                  color: isMine ? AppColors.primary : AppColors.border,
                ),
              ),
              child: Text(
                // Silinmiş mesajda gövde `null` gelir; uydurma metin yazmak yerine
                // ne olduğunu açıkça söyleriz.
                body ?? 'Bu mesaj silindi',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: message.isDeleted
                      ? AppColors.textMuted
                      : (isMine ? AppColors.surface : AppColors.textPrimary),
                  fontStyle: message.isDeleted
                      ? FontStyle.italic
                      : FontStyle.normal,
                ),
              ),
            ),
            _MessageFooter(message: message, isMine: isMine, onRetry: onRetry),
            if (failed) const SizedBox(height: 4),
          ],
        ),
      ),
    );
  }
}

class _MessageFooter extends StatelessWidget {
  const _MessageFooter({
    required this.message,
    required this.isMine,
    required this.onRetry,
  });

  final ChatMessage message;
  final bool isMine;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final captionStyle = Theme.of(
      context,
    ).textTheme.labelSmall?.copyWith(color: AppColors.textMuted);

    return switch (message.status) {
      ChatMessageStatus.sending => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Text('Gönderiliyor…', style: captionStyle),
      ),
      ChatMessageStatus.failed => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              message.failureMessage ?? 'Gönderilemedi',
              style: captionStyle?.copyWith(color: AppColors.error),
            ),
            const SizedBox(width: 8),
            InkWell(
              onTap: onRetry,
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                child: Text(
                  'Tekrar dene',
                  style: captionStyle?.copyWith(
                    color: AppColors.accentBlue,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
      ChatMessageStatus.sent => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(clockTime(message.sentOnUtc), style: captionStyle),
            // "Okundu" yalnız KENDİ mesajında anlamlıdır: karşı tarafın mesajını
            // zaten sen okuyorsun.
            if (isMine && message.isRead) ...[
              const SizedBox(width: 6),
              const Icon(
                Icons.done_all_rounded,
                size: 14,
                color: AppColors.accentBlue,
              ),
            ],
          ],
        ),
      ),
    };
  }
}

/// Sohbetin altındaki mesaj yazma alanı.
class MessageComposer extends StatefulWidget {
  const MessageComposer({
    required this.enabled,
    required this.onSend,
    super.key,
    this.disabledHint,
  });

  final bool enabled;
  final ValueChanged<String> onSend;

  /// Engelli sohbette gösterilecek açıklama.
  final String? disabledHint;

  @override
  State<MessageComposer> createState() => _MessageComposerState();
}

class _MessageComposerState extends State<MessageComposer> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    final text = _controller.text.trim();
    if (text.isEmpty) {
      return;
    }
    _controller.clear();
    widget.onSend(text);
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.enabled) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
        decoration: const BoxDecoration(
          color: AppColors.errorSurface,
          border: Border(top: BorderSide(color: AppColors.errorBorder)),
        ),
        child: Text(
          widget.disabledHint ?? 'Bu sohbet engellendiği için mesaj gönderemezsin.',
          textAlign: TextAlign.center,
          style: Theme.of(
            context,
          ).textTheme.bodySmall?.copyWith(color: AppColors.error),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      child: SafeArea(
        top: false,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: TextField(
                controller: _controller,
                minLines: 1,
                maxLines: 5,
                textCapitalization: TextCapitalization.sentences,
                textInputAction: TextInputAction.newline,
                decoration: InputDecoration(
                  hintText: 'Mesaj yaz…',
                  filled: true,
                  fillColor: AppColors.background,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 12,
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(18),
                    borderSide: const BorderSide(color: AppColors.border),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(18),
                    borderSide: const BorderSide(color: AppColors.border),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(18),
                    borderSide: const BorderSide(color: AppColors.primary),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 8),
            Semantics(
              button: true,
              label: 'Mesajı gönder',
              child: InkWell(
                borderRadius: BorderRadius.circular(999),
                onTap: _submit,
                child: Container(
                  width: 48,
                  height: 48,
                  decoration: const BoxDecoration(
                    color: AppColors.primary,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.send_rounded,
                    color: AppColors.surface,
                    size: 20,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Karşı taraf adının baş harfleri; ad yoksa "?".
String initialsOf(String name) {
  final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.characters.first.toUpperCase();
  }
  return (parts.first.characters.first + parts.last.characters.first).toUpperCase();
}

/// Gelen kutusundaki göreli zaman: bugün saat, dün "Dün", öncesi tarih.
String relativeTime(DateTime utc, {DateTime? now}) {
  final local = utc.toLocal();
  final reference = (now ?? DateTime.now()).toLocal();
  final today = DateTime(reference.year, reference.month, reference.day);
  final day = DateTime(local.year, local.month, local.day);
  final difference = today.difference(day).inDays;

  if (difference <= 0) {
    return clockTime(utc);
  }
  if (difference == 1) {
    return 'Dün';
  }
  if (difference < 7) {
    const names = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
    return names[local.weekday - 1];
  }
  return '${local.day.toString().padLeft(2, '0')}.${local.month.toString().padLeft(2, '0')}';
}

/// Saat:dakika (yerel saat dilimi).
String clockTime(DateTime utc) {
  final local = utc.toLocal();
  return '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}
