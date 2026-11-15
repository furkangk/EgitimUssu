// M16 Mesajlaşma — mobil domain sözleşmeleri.
// Backend: `/api/messaging`. Sohbet yalnız öğretmen↔öğrenci ve öğretmen↔veli
// çiftlerinde kurulur; kuralı sunucu domaini zorlar, istemci onu tekrar etmez.

/// Karşı tarafın rolü (`ParticipantRole`'ün metin karşılığı).
const String kParticipantRoleTeacher = 'Teacher';

/// Öğrenci tarafı.
const String kParticipantRoleStudent = 'Student';

/// Veli tarafı.
const String kParticipantRoleParent = 'Parent';

/// Gelen kutusu satırı — `GET /api/messaging/conversations`.
class ConversationSummary {
  const ConversationSummary({
    required this.id,
    required this.counterpartUserId,
    required this.counterpartRole,
    required this.studentId,
    required this.lastMessageOnUtc,
    required this.lastMessagePreview,
    required this.unreadCount,
    required this.isBlocked,
  });

  final String id;

  /// Karşı taraf **isteği yapana göredir**: öğretmene öğrenci/veli, öğrenciye öğretmen.
  final String counterpartUserId;
  final String counterpartRole;
  final String studentId;
  final DateTime lastMessageOnUtc;
  final String? lastMessagePreview;
  final int unreadCount;

  /// Taraflardan biri engellemiş — gönderim kapalı.
  final bool isBlocked;

  bool get hasUnread => unreadCount > 0;
}

/// Sohbetin başlık bilgisi — `POST /api/messaging/conversations` yanıtı.
class ConversationDetail {
  const ConversationDetail({
    required this.id,
    required this.teacherUserId,
    required this.counterpartUserId,
    required this.counterpartRole,
    required this.studentId,
    required this.lastMessageOnUtc,
    required this.lastMessagePreview,
    required this.isBlocked,
    required this.blockedByMe,
  });

  final String id;
  final String teacherUserId;
  final String counterpartUserId;
  final String counterpartRole;
  final String studentId;
  final DateTime lastMessageOnUtc;
  final String? lastMessagePreview;
  final bool isBlocked;

  /// Engeli isteği yapan taraf koydu — "engeli kaldır" düğmesi buna bakar.
  /// Karşı tarafın koyduğu engeli kaldırmak sunucuda da mümkün değildir.
  final bool blockedByMe;
}

/// Tek mesajın gönderim durumu (iyimser gönderim için).
enum ChatMessageStatus {
  /// Sunucudan gelmiş, kalıcı.
  sent,

  /// İstemcide oluşturuldu, sunucu yanıtı bekleniyor.
  sending,

  /// Gönderim başarısız — "tekrar dene" gösterilir.
  failed,
}

/// Sohbetteki tek mesaj — `GET /api/messaging/conversations/{id}/messages`.
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.conversationId,
    required this.senderUserId,
    required this.body,
    required this.sentOnUtc,
    required this.readOnUtc,
    required this.isDeleted,
    this.status = ChatMessageStatus.sent,
    this.failureMessage,
  });

  final String id;
  final String conversationId;
  final String senderUserId;

  /// Silinmiş mesajda sunucu `null` döner — içerik gizlenir, satır kalır.
  final String? body;
  final DateTime sentOnUtc;
  final DateTime? readOnUtc;
  final bool isDeleted;
  final ChatMessageStatus status;

  /// [ChatMessageStatus.failed] durumunda kullanıcıya gösterilecek neden.
  final String? failureMessage;

  bool get isRead => readOnUtc != null;

  bool get isPending => status != ChatMessageStatus.sent;

  ChatMessage copyWith({
    String? id,
    DateTime? readOnUtc,
    bool? isDeleted,
    ChatMessageStatus? status,
    String? failureMessage,
    bool clearFailure = false,
  }) {
    return ChatMessage(
      id: id ?? this.id,
      conversationId: conversationId,
      senderUserId: senderUserId,
      body: body,
      sentOnUtc: sentOnUtc,
      readOnUtc: readOnUtc ?? this.readOnUtc,
      isDeleted: isDeleted ?? this.isDeleted,
      status: status ?? this.status,
      failureMessage: clearFailure
          ? null
          : (failureMessage ?? this.failureMessage),
    );
  }
}

/// M16 uçlarının mobil sözleşmesi.
///
/// ⚠️ Şikayet (`report`) ve sohbet susturma (`mute`) metotları **bilinçli olarak yok**:
/// karşılık gelen uçlar sunucuda da yok (şikayet M18/P12'de gelecek). Var olmayan bir uca
/// istek atan metot yazmak "sessiz başarısızlık yok" ilkesini çiğnerdi.
abstract interface class MessagingRepository {
  /// Gelen kutusu (son mesaja göre azalan).
  Future<List<ConversationSummary>> conversations({int skip, int take});

  /// Sohbetin mesajları — keyset sayfalama: [beforeUtc]'den **eski** olanlar, yeniden eskiye.
  Future<List<ChatMessage>> messages({
    required String conversationId,
    DateTime? beforeUtc,
    int take,
  });

  /// Mesaj gönderir.
  Future<ChatMessage> send({
    required String conversationId,
    required String body,
  });

  /// Sohbetteki gelen mesajları okundu işaretler.
  Future<void> markRead(String conversationId, {String? upToMessageId});

  /// Sohbeti açar; aynı çift + öğrenci bağlamı için varsa mevcudu döner.
  Future<ConversationDetail> openConversation({
    required String counterpartUserId,
    required String counterpartRole,
    required String studentId,
  });

  /// Sohbeti engeller.
  Future<ConversationDetail> block(String conversationId);

  /// Kendi koyduğun engeli kaldırır.
  Future<ConversationDetail> unblock(String conversationId);
}
