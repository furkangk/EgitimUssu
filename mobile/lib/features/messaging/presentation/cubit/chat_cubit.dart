import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Sohbet ekranının yükleme durumu.
enum ChatStatus { initial, loading, loaded, failure }

/// Varsayılan sayfa boyutu (sunucu da 30 kullanır).
const int kChatPageSize = 30;

/// D-08: Sohbet ekranının durumu. [messages] **yeniden eskiye** sıralıdır.
class ChatState {
  const ChatState({
    this.status = ChatStatus.initial,
    this.conversationId = '',
    this.conversation,
    this.messages = const <ChatMessage>[],
    this.hasMore = false,
    this.isLoadingMore = false,
    this.errorMessage,
  });

  final ChatStatus status;
  final String conversationId;

  /// Sohbet başlığı; sohbete gelen kutusundan girildiyse başta `null` olabilir.
  final ConversationDetail? conversation;
  final List<ChatMessage> messages;

  /// Son sayfa `take` kadar satır döndüyse daha eski mesaj olabilir.
  final bool hasMore;
  final bool isLoadingMore;
  final String? errorMessage;

  bool get isLoading => status == ChatStatus.loading;

  bool get isEmpty => status == ChatStatus.loaded && messages.isEmpty;

  /// Engel **çift yönlüdür**: kim koyarsa koysun gönderim kapanır.
  bool get canSend => !(conversation?.isBlocked ?? false);

  /// Engeli yalnız koyan taraf kaldırabilir; karşı tarafın engeli için düğme gösterilmez.
  bool get canUnblock =>
      (conversation?.isBlocked ?? false) &&
      (conversation?.blockedByMe ?? false);

  ChatState copyWith({
    ChatStatus? status,
    String? conversationId,
    ConversationDetail? conversation,
    List<ChatMessage>? messages,
    bool? hasMore,
    bool? isLoadingMore,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ChatState(
      status: status ?? this.status,
      conversationId: conversationId ?? this.conversationId,
      conversation: conversation ?? this.conversation,
      messages: messages ?? this.messages,
      hasMore: hasMore ?? this.hasMore,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// M16-1 (D-08): Tek sohbetin mesajlarını yönetir — keyset sayfalama + iyimser gönderim.
class ChatCubit extends Cubit<ChatState> {
  ChatCubit({
    required MessagingRepository repository,
    required String currentUserId,
  }) : _repository = repository,
       _currentUserId = currentUserId,
       super(const ChatState());

  factory ChatCubit.create(String currentUserId) => ChatCubit(
    repository: injector<MessagingRepository>(),
    currentUserId: currentUserId,
  );

  final MessagingRepository _repository;
  final String _currentUserId;

  int _pageSize = kChatPageSize;
  int _draftCounter = 0;

  /// Gönderilemeyen mesajın metni id'siyle saklanır: "tekrar dene" aynı metni gönderir.
  final Map<String, String> _pendingBodies = <String, String>{};

  /// Oturumdaki kullanıcının kimliği — balonun sağda mı solda mı çizileceğini belirler.
  String get currentUserId => _currentUserId;

  /// Sohbetin ilk sayfasını yükler ve gelen mesajları okundu işaretler.
  Future<void> load(
    String conversationId, {
    ConversationDetail? detail,
    int take = kChatPageSize,
  }) async {
    _pageSize = take;
    emit(
      state.copyWith(
        status: ChatStatus.loading,
        conversationId: conversationId,
        conversation: detail,
        messages: const <ChatMessage>[],
        clearError: true,
      ),
    );

    try {
      final rows = await _repository.messages(
        conversationId: conversationId,
        take: take,
      );
      emit(
        state.copyWith(
          status: ChatStatus.loaded,
          messages: rows,
          hasMore: rows.length >= take,
        ),
      );
    } on ApiException catch (error) {
      emit(
        state.copyWith(status: ChatStatus.failure, errorMessage: error.message),
      );
      return;
    }

    await markRead();
  }

  /// Eskiye kaydırınca bir sayfa daha yükler. İmleç listedeki **en eski** mesajın zamanıdır.
  Future<void> loadMore() async {
    if (!state.hasMore || state.isLoadingMore || state.messages.isEmpty) {
      return;
    }

    emit(state.copyWith(isLoadingMore: true, clearError: true));

    // Bekleyen (henüz sunucuda olmayan) mesajlar imleç olamaz: sunucu onları bilmiyor.
    final oldestSent = state.messages.lastWhere(
      (row) => !row.isPending,
      orElse: () => state.messages.last,
    );

    try {
      final rows = await _repository.messages(
        conversationId: state.conversationId,
        beforeUtc: oldestSent.sentOnUtc,
        take: _pageSize,
      );

      final known = state.messages.map((row) => row.id).toSet();
      final fresh = rows.where((row) => !known.contains(row.id)).toList();

      emit(
        state.copyWith(
          messages: <ChatMessage>[...state.messages, ...fresh],
          hasMore: rows.length >= _pageSize,
          isLoadingMore: false,
        ),
      );
    } on ApiException catch (error) {
      emit(state.copyWith(isLoadingMore: false, errorMessage: error.message));
    }
  }

  /// İyimser gönderim: mesaj sunucu yanıtı beklenmeden listeye girer.
  ///
  /// Hata olursa satır **kaybolmaz**, [ChatMessageStatus.failed] olur — kullanıcı yazdığı
  /// metni yeniden yazmak zorunda kalmasın diye.
  Future<void> send(String body) async {
    final trimmed = body.trim();
    if (trimmed.isEmpty || !state.canSend) {
      return;
    }

    final draftId = 'draft-${++_draftCounter}';
    _pendingBodies[draftId] = trimmed;

    final draft = ChatMessage(
      id: draftId,
      conversationId: state.conversationId,
      senderUserId: _currentUserId,
      body: trimmed,
      sentOnUtc: DateTime.now().toUtc(),
      readOnUtc: null,
      isDeleted: false,
      status: ChatMessageStatus.sending,
    );

    emit(
      state.copyWith(
        messages: <ChatMessage>[draft, ...state.messages],
        clearError: true,
      ),
    );

    await _deliver(draftId, trimmed);
  }

  /// Başarısız mesajı aynı metinle yeniden gönderir; listede kopya bırakmaz.
  Future<void> retry(String messageId) async {
    final body = _pendingBodies[messageId];
    if (body == null) {
      return;
    }

    emit(
      state.copyWith(
        messages: _replace(
          messageId,
          (row) => row.copyWith(
            status: ChatMessageStatus.sending,
            clearFailure: true,
          ),
        ),
        clearError: true,
      ),
    );

    await _deliver(messageId, body);
  }

  /// Sohbeti açınca ve yeni mesaj gelince okundu işaretler.
  ///
  /// Başarısızlığı **yutulur**: okundu damgası bir yan etkidir, ekranı bozmamalı.
  Future<void> markRead() async {
    if (state.conversationId.isEmpty) {
      return;
    }
    final rows =
        state.messages
            .where((row) => row.status == ChatMessageStatus.sent)
            .toList()
          ..sort((a, b) => b.sentOnUtc.compareTo(a.sentOnUtc));
    if (rows.isEmpty) return;
    final marker = rows.first.id;
    try {
      await _repository.markRead(state.conversationId, upToMessageId: marker);
    } on ApiException {
      // Sessiz: mesajlar görünür kalır, bir sonraki açılışta yeniden denenir.
    }
  }

  /// Sohbeti engeller / engeli kaldırır.
  Future<void> setBlocked(bool blocked) async {
    try {
      final detail = blocked
          ? await _repository.block(state.conversationId)
          : await _repository.unblock(state.conversationId);
      emit(state.copyWith(conversation: detail, clearError: true));
    } on ApiException catch (error) {
      emit(state.copyWith(errorMessage: error.message));
    }
  }

  /// Snackbar gösterildikten sonra hatayı temizler.
  void clearError() => emit(state.copyWith(clearError: true));

  Future<void> _deliver(String draftId, String body) async {
    try {
      final sent = await _repository.send(
        conversationId: state.conversationId,
        body: body,
      );
      _pendingBodies.remove(draftId);
      emit(state.copyWith(messages: _replace(draftId, (_) => sent)));
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          messages: _replace(
            draftId,
            (row) => row.copyWith(
              status: ChatMessageStatus.failed,
              failureMessage: error.message,
            ),
          ),
        ),
      );
    }
  }

  List<ChatMessage> _replace(
    String messageId,
    ChatMessage Function(ChatMessage current) update,
  ) {
    return <ChatMessage>[
      for (final row in state.messages)
        if (row.id == messageId) update(row) else row,
    ];
  }
}
