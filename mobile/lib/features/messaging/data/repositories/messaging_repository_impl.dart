import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';

/// M16 uçlarının Dio üzerinden okunması. Taraf **oturumdan** gelir; uçlar `{userId}` almaz.
class MessagingRepositoryImpl implements MessagingRepository {
  MessagingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  static const String _base = '/api/messaging';

  static int _int(dynamic value) =>
      value is num ? value.toInt() : int.tryParse('$value') ?? 0;

  static DateTime _date(dynamic value) =>
      DateTime.tryParse('$value')?.toUtc() ??
      DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);

  static DateTime? _dateOrNull(dynamic value) =>
      value == null ? null : DateTime.tryParse('$value')?.toUtc();

  static String? _stringOrNull(dynamic value) {
    if (value == null) {
      return null;
    }
    final text = '$value';
    return text.isEmpty ? null : text;
  }

  static ConversationSummary _summary(Map<String, dynamic> json) =>
      ConversationSummary(
        id: '${json['id']}',
        counterpartUserId: '${json['counterpartUserId']}',
        counterpartRole: '${json['counterpartRole']}',
        studentId: '${json['studentId']}',
        lastMessageOnUtc: _date(json['lastMessageOnUtc']),
        lastMessagePreview: _stringOrNull(json['lastMessagePreview']),
        unreadCount: _int(json['unreadCount']),
        isBlocked: json['isBlocked'] as bool? ?? false,
      );

  static ConversationDetail _detail(Map<String, dynamic> json) =>
      ConversationDetail(
        id: '${json['id']}',
        teacherUserId: '${json['teacherUserId']}',
        counterpartUserId: '${json['counterpartUserId']}',
        counterpartRole: '${json['counterpartRole']}',
        studentId: '${json['studentId']}',
        lastMessageOnUtc: _date(json['lastMessageOnUtc']),
        lastMessagePreview: _stringOrNull(json['lastMessagePreview']),
        isBlocked: json['isBlocked'] as bool? ?? false,
        blockedByMe: json['blockedByMe'] as bool? ?? false,
      );

  static ChatMessage _message(Map<String, dynamic> json) => ChatMessage(
    id: '${json['id']}',
    conversationId: '${json['conversationId']}',
    senderUserId: '${json['senderUserId']}',
    // Silinmiş mesajda sunucu `null` döner; boş metinle değiştirmek silmeyi gizlerdi.
    body: _stringOrNull(json['body']),
    sentOnUtc: _date(json['sentOnUtc']),
    readOnUtc: _dateOrNull(json['readOnUtc']),
    isDeleted: json['isDeleted'] as bool? ?? false,
  );

  @override
  Future<List<ConversationSummary>> conversations({
    int skip = 0,
    int take = 30,
  }) async {
    final rows = await _apiClient.getList(
      '$_base/conversations',
      queryParameters: <String, dynamic>{'skip': skip, 'take': take},
    );
    return rows.whereType<Map<String, dynamic>>().map(_summary).toList();
  }

  @override
  Future<List<ChatMessage>> messages({
    required String conversationId,
    DateTime? beforeUtc,
    int take = 30,
  }) async {
    final rows = await _apiClient.getList(
      '$_base/conversations/$conversationId/messages',
      queryParameters: <String, dynamic>{
        'take': take,
        if (beforeUtc != null) 'beforeUtc': beforeUtc.toUtc().toIso8601String(),
      },
    );
    return rows.whereType<Map<String, dynamic>>().map(_message).toList();
  }

  @override
  Future<ChatMessage> send({
    required String conversationId,
    required String body,
  }) async => _message(
    await _apiClient.post(
      idempotent: true,
      '$_base/conversations/$conversationId/messages',
      data: <String, dynamic>{'body': body},
    ),
  );

  @override
  Future<void> markRead(String conversationId, {String? upToMessageId}) =>
      _apiClient.post(
        '$_base/conversations/$conversationId/read',
        data: <String, dynamic>{'upToMessageId': upToMessageId},
      );

  @override
  Future<ConversationDetail> openConversation({
    required String counterpartUserId,
    required String counterpartRole,
    required String studentId,
  }) async => _detail(
    await _apiClient.post(
      idempotent: true,
      '$_base/conversations',
      data: <String, dynamic>{
        'counterpartUserId': counterpartUserId,
        'counterpartRole': counterpartRole,
        'studentId': studentId,
      },
    ),
  );

  @override
  Future<ConversationDetail> block(String conversationId) async => _detail(
    await _apiClient.post('$_base/conversations/$conversationId/block'),
  );

  @override
  Future<ConversationDetail> unblock(String conversationId) async => _detail(
    await _apiClient.post('$_base/conversations/$conversationId/unblock'),
  );
}
