import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';

/// Ortak `MessagingRepository` sahtesi (mobile_flutter.md §17.1 — sahteler tek noktada).
///
/// Sayfalama **gerçek keyset semantiğini** taşır: `beforeUtc`'den eski mesajlar, yeniden
/// eskiye, `take` kadar. Sahte bu kuralı uygulamasaydı sayfalama testi yalnız cubit'in
/// çağrı yaptığını doğrular, sayfalamanın doğruluğunu değil.
class FakeMessagingRepository implements MessagingRepository {
  FakeMessagingRepository({
    List<ConversationSummary>? inbox,
    List<ChatMessage>? messages,
    this.detail,
    this.inboxError,
    this.messagesError,
    this.sendError,
    this.markReadError,
    this.openError,
    this.blockError,
  }) : inbox = inbox ?? <ConversationSummary>[],
       store = messages ?? <ChatMessage>[];

  List<ConversationSummary> inbox;

  /// Sunucudaki mesajlar (sıra önemsiz; sahte kendisi sıralar).
  List<ChatMessage> store;
  ConversationDetail? detail;

  final ApiException? inboxError;
  final ApiException? messagesError;
  /// Değiştirilebilir: "önce hata, sonra başarı" (tekrar dene) senaryosu tek sahteyle kurulur.
  ApiException? sendError;
  final ApiException? markReadError;
  final ApiException? openError;
  final ApiException? blockError;

  final List<DateTime?> messageCursors = <DateTime?>[];
  final List<String> sentBodies = <String>[];
  final List<String> markReadCalls = <String>[];
  final List<String> blockCalls = <String>[];
  final List<String> unblockCalls = <String>[];

  /// Sunucunun bir sonraki `send` çağrısında döneceği kimlik.
  String nextMessageId = 'server-1';

  /// Sunucunun bir sonraki `send` çağrısında damgalayacağı an.
  DateTime nextSentOnUtc = DateTime.utc(2026, 9, 4, 12, 0);

  @override
  Future<List<ConversationSummary>> conversations({int skip = 0, int take = 30}) async {
    if (inboxError != null) throw inboxError!;
    return inbox.skip(skip).take(take).toList();
  }

  @override
  Future<List<ChatMessage>> messages({
    required String conversationId,
    DateTime? beforeUtc,
    int take = 30,
  }) async {
    messageCursors.add(beforeUtc);
    if (messagesError != null) throw messagesError!;

    final rows = store
        .where((message) => message.conversationId == conversationId)
        .where((message) => beforeUtc == null || message.sentOnUtc.isBefore(beforeUtc))
        .toList()
      ..sort((a, b) => b.sentOnUtc.compareTo(a.sentOnUtc));

    return rows.take(take).toList();
  }

  @override
  Future<ChatMessage> send({required String conversationId, required String body}) async {
    sentBodies.add(body);
    if (sendError != null) throw sendError!;

    final message = ChatMessage(
      id: nextMessageId,
      conversationId: conversationId,
      senderUserId: 'me',
      body: body,
      sentOnUtc: nextSentOnUtc,
      readOnUtc: null,
      isDeleted: false,
    );
    store = <ChatMessage>[...store, message];
    return message;
  }

  @override
  Future<void> markRead(String conversationId) async {
    markReadCalls.add(conversationId);
    if (markReadError != null) throw markReadError!;
  }

  @override
  Future<ConversationDetail> openConversation({
    required String counterpartUserId,
    required String counterpartRole,
    required String studentId,
  }) async {
    if (openError != null) throw openError!;
    return detail ??= ConversationDetail(
      id: 'c-1',
      teacherUserId: 'teacher-1',
      counterpartUserId: counterpartUserId,
      counterpartRole: counterpartRole,
      studentId: studentId,
      lastMessageOnUtc: DateTime.utc(2026, 9, 4, 12),
      lastMessagePreview: null,
      isBlocked: false,
      blockedByMe: false,
    );
  }

  @override
  Future<ConversationDetail> block(String conversationId) async {
    blockCalls.add(conversationId);
    if (blockError != null) throw blockError!;
    return detail = _withBlock(isBlocked: true, blockedByMe: true);
  }

  @override
  Future<ConversationDetail> unblock(String conversationId) async {
    unblockCalls.add(conversationId);
    if (blockError != null) throw blockError!;
    return detail = _withBlock(isBlocked: false, blockedByMe: false);
  }

  ConversationDetail _withBlock({required bool isBlocked, required bool blockedByMe}) {
    final current = detail;
    return ConversationDetail(
      id: current?.id ?? 'c-1',
      teacherUserId: current?.teacherUserId ?? 'teacher-1',
      counterpartUserId: current?.counterpartUserId ?? 'student-1',
      counterpartRole: current?.counterpartRole ?? kParticipantRoleStudent,
      studentId: current?.studentId ?? 's-1',
      lastMessageOnUtc: current?.lastMessageOnUtc ?? DateTime.utc(2026, 9, 4, 12),
      lastMessagePreview: current?.lastMessagePreview,
      isBlocked: isBlocked,
      blockedByMe: blockedByMe,
    );
  }
}
