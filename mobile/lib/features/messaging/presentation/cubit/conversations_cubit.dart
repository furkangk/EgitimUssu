import 'dart:async';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Gelen kutusunun yükleme durumu.
enum ConversationsStatus { initial, loading, loaded, failure }

/// D-08: Gelen kutusu ekranının durumu.
class ConversationsState {
  const ConversationsState({
    this.status = ConversationsStatus.initial,
    this.conversations = const <ConversationSummary>[],
    this.errorMessage,
  });

  final ConversationsStatus status;
  final List<ConversationSummary> conversations;
  final String? errorMessage;

  bool get isLoading => status == ConversationsStatus.loading;

  /// Boş liste **hata değildir**: henüz kimseyle konuşmamış kullanıcı normal bir durumdur.
  bool get isEmpty =>
      status == ConversationsStatus.loaded && conversations.isEmpty;

  /// Alt navigasyon rozeti için toplam okunmamış.
  int get totalUnread =>
      conversations.fold(0, (total, row) => total + row.unreadCount);

  ConversationsState copyWith({
    ConversationsStatus? status,
    List<ConversationSummary>? conversations,
    String? errorMessage,
    bool clearError = false,
  }) {
    return ConversationsState(
      status: status ?? this.status,
      conversations: conversations ?? this.conversations,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// M16-1 (D-08): Gelen kutusunu okur.
class ConversationsCubit extends Cubit<ConversationsState> {
  ConversationsCubit(this._repository, {PushEventBus? eventBus})
    : _eventBus = eventBus,
      super(const ConversationsState()) {
    _subscription = eventBus?.events.listen((event) {
      if (event.conversationId == null) return;
      _onPush();
    });
    eventBus?.addListener(_sessionChanged);
  }

  factory ConversationsCubit.create() => ConversationsCubit(
    injector<MessagingRepository>(),
    eventBus: injector.isRegistered<PushEventBus>()
        ? injector<PushEventBus>()
        : null,
  );

  final MessagingRepository _repository;
  final PushEventBus? _eventBus;
  StreamSubscription<PushEvent>? _subscription;
  int _refreshRun = 0;
  bool _refreshing = false;
  bool _again = false;
  void _sessionChanged() {
    _refreshRun++;
    _refreshing = false;
    _again = false;
    _generation++;
    if (!isClosed) emit(const ConversationsState());
  }

  Future<void> _onPush() async {
    final user = _eventBus?.userId;
    if (isClosed || user == null) return;
    if (_refreshing) {
      _again = true;
      return;
    }
    _refreshing = true;
    final run = ++_refreshRun;
    final session = _eventBus?.generation;
    try {
      do {
        _again = false;
        await load(silent: true);
      } while (_again && !isClosed && session == _eventBus?.generation);
    } finally {
      if (run == _refreshRun) _refreshing = false;
    }
  }

  @override
  Future<void> close() async {
    _generation++;
    _eventBus?.removeListener(_sessionChanged);
    await _subscription?.cancel();
    return super.close();
  }

  int _generation = 0;

  /// Gelen kutusunu (yeniden) yükler.
  Future<void> load({bool silent = false}) async {
    if (isClosed) return;
    final generation = ++_generation;
    emit(
      state.copyWith(
        status: silent ? state.status : ConversationsStatus.loading,
        clearError: true,
      ),
    );
    try {
      final rows = await _repository.conversations(skip: 0, take: 50);
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(status: ConversationsStatus.loaded, conversations: rows),
      );
    } catch (error) {
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          status: silent ? state.status : ConversationsStatus.failure,
          errorMessage: error is ApiException
              ? error.message
              : 'Mesajlar alınamadı.',
        ),
      );
    }
  }

  /// Pull-to-refresh ve sohbetten dönüşte tazeleme.
  Future<void> refresh() => load();
}
