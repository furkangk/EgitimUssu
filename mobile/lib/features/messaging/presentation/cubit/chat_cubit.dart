import 'dart:async';

import 'package:egitim_ussu_mobile/core/bloc/safe_emit.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:flutter/foundation.dart';
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
class ChatCubit extends Cubit<ChatState> with SafeEmit<ChatState> {
  ChatCubit({
    required MessagingRepository repository,
    required String currentUserId,
    PushEventBus? eventBus,
  }) : _repository = repository,
       _currentUserId = currentUserId,
       _eventBus = eventBus,
       _session = eventBus?.generation,
       super(const ChatState()) {
    _subscription = eventBus?.events.listen((event) {
      if (event.conversationId == state.conversationId) refreshLatest();
    });
    eventBus?.addListener(_sessionChanged);
  }

  factory ChatCubit.create(String currentUserId) => ChatCubit(
    repository: injector<MessagingRepository>(),
    currentUserId: currentUserId,
    eventBus: injector.isRegistered<PushEventBus>()
        ? injector<PushEventBus>()
        : null,
  );

  final MessagingRepository _repository;
  final String _currentUserId;

  final PushEventBus? _eventBus;
  final int? _session;
  StreamSubscription<PushEvent>? _subscription;
  Timer? _timer;
  bool _active = false;
  bool _refreshing = false;
  bool _refreshAgain = false;
  int _generation = 0;
  bool get _currentSession =>
      !isClosed &&
      (_eventBus == null ||
          (_eventBus.generation == _session &&
              _eventBus.userId == _currentUserId));
  bool _current(int generation) => _currentSession && generation == _generation;
  void _sessionChanged() {
    if (!_currentSession) {
      setActive(false);
      _generation++;
    }
  }

  void setActive(bool active) {
    active = active && _currentSession;
    if (_active == active) return;
    _active = active;
    _timer?.cancel();
    if (active) {
      _timer = Timer.periodic(
        const Duration(seconds: 10),
        (_) => refreshLatest(),
      );
    }
  }

  /// Merge newest pages through the first known message, retaining loaded history.
  Future<void> refreshLatest() async {
    if (!_active || !_currentSession || state.conversationId.isEmpty) return;
    if (_refreshing || state.isLoading) {
      _refreshAgain = true;
      return;
    }
    _refreshing = true;
    final generation = _generation;
    final id = state.conversationId;
    try {
      final known = state.messages
          .where((m) => !m.isPending)
          .map((m) => m.id)
          .toSet();
      final fresh = <ChatMessage>[];
      DateTime? cursor;
      while (true) {
        final rows = await _repository.messages(
          conversationId: id,
          beforeUtc: cursor,
          take: _pageSize,
        );
        if (!_current(generation) || !_active) return;
        fresh.addAll(rows);
        if (rows.length < _pageSize || rows.any((m) => known.contains(m.id))) {
          break;
        }
        final next = rows.last.sentOnUtc;
        if (cursor != null && !next.isBefore(cursor)) break;
        cursor = next;
      }
      final merged = {for (final m in state.messages) m.id: m};
      for (final m in fresh) {
        merged[m.id] = m;
      }
      final rows = merged.values.toList()
        ..sort((a, b) => b.sentOnUtc.compareTo(a.sentOnUtc));
      safeEmit(state.copyWith(status: ChatStatus.loaded, messages: rows));
      await markRead();
    } catch (error) {
      // A transient background refresh must not discard the visible conversation.
      debugPrint('Chat refresh failed: ${error.runtimeType}');
    } finally {
      _refreshing = false;
      if (_refreshAgain) {
        _refreshAgain = false;
        if (_current(generation) && _active) unawaited(refreshLatest());
      }
    }
  }

  @override
  Future<void> close() async {
    _generation++;
    _timer?.cancel();
    _eventBus?.removeListener(_sessionChanged);
    await _subscription?.cancel();
    return super.close();
  }

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
    if (!_currentSession) return;
    final generation = ++_generation;
    _pageSize = take;
    safeEmit(
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
      if (!_current(generation)) return;
      safeEmit(
        state.copyWith(
          status: ChatStatus.loaded,
          messages: rows,
          hasMore: rows.length >= take,
        ),
      );
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(
        state.copyWith(status: ChatStatus.failure, errorMessage: error.message),
      );
      return;
    }

    await markRead();
    if (_refreshAgain) {
      _refreshAgain = false;
      await refreshLatest();
    }
  }

  /// Eskiye kaydırınca bir sayfa daha yükler. İmleç listedeki **en eski** mesajın zamanıdır.
  Future<void> loadMore() async {
    final generation = _generation;
    if (!_currentSession ||
        !state.hasMore ||
        state.isLoadingMore ||
        state.messages.isEmpty) {
      return;
    }

    safeEmit(state.copyWith(isLoadingMore: true, clearError: true));

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

      if (!_current(generation)) return;
      final known = state.messages.map((row) => row.id).toSet();
      final fresh = rows.where((row) => !known.contains(row.id)).toList();

      safeEmit(
        state.copyWith(
          messages: <ChatMessage>[...state.messages, ...fresh],
          hasMore: rows.length >= _pageSize,
          isLoadingMore: false,
        ),
      );
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(state.copyWith(isLoadingMore: false, errorMessage: error.message));
    }
  }

  /// İyimser gönderim: mesaj sunucu yanıtı beklenmeden listeye girer.
  ///
  /// Hata olursa satır **kaybolmaz**, [ChatMessageStatus.failed] olur — kullanıcı yazdığı
  /// metni yeniden yazmak zorunda kalmasın diye.
  Future<void> send(String body) async {
    final trimmed = body.trim();
    if (!_currentSession || trimmed.isEmpty || !state.canSend) {
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

    safeEmit(
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
    if (!_currentSession || body == null) {
      return;
    }

    safeEmit(
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
    if (!_currentSession || state.conversationId.isEmpty) {
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
      debugPrint('Chat read marker failed; retry on next refresh.');
    }
  }

  /// Sohbeti engeller / engeli kaldırır.
  Future<void> setBlocked(bool blocked) async {
    final generation = _generation;
    if (!_currentSession) return;
    try {
      final detail = blocked
          ? await _repository.block(state.conversationId)
          : await _repository.unblock(state.conversationId);
      if (!_current(generation)) return;
      safeEmit(state.copyWith(conversation: detail, clearError: true));
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(state.copyWith(errorMessage: error.message));
    }
  }

  /// Snackbar gösterildikten sonra hatayı temizler.
  void clearError() => safeEmit(state.copyWith(clearError: true));

  Future<void> _deliver(String draftId, String body) async {
    final generation = _generation;
    if (!_currentSession) return;
    try {
      final sent = await _repository.send(
        conversationId: state.conversationId,
        body: body,
      );
      if (!_current(generation)) return;
      _pendingBodies.remove(draftId);
      safeEmit(
        state.copyWith(
          messages: _replace(draftId, (_) => sent).fold<List<ChatMessage>>([], (
            rows,
            m,
          ) {
            if (!rows.any((r) => r.id == m.id)) rows.add(m);
            return rows;
          }),
        ),
      );
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(
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
