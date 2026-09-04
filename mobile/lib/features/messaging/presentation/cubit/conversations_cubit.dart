import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
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
  bool get isEmpty => status == ConversationsStatus.loaded && conversations.isEmpty;

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
  ConversationsCubit(this._repository) : super(const ConversationsState());

  factory ConversationsCubit.create() =>
      ConversationsCubit(injector<MessagingRepository>());

  final MessagingRepository _repository;

  /// Gelen kutusunu (yeniden) yükler.
  Future<void> load() async {
    emit(state.copyWith(status: ConversationsStatus.loading, clearError: true));
    try {
      final rows = await _repository.conversations(skip: 0, take: 50);
      emit(
        state.copyWith(status: ConversationsStatus.loaded, conversations: rows),
      );
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: ConversationsStatus.failure,
          errorMessage: error.message,
        ),
      );
    }
  }

  /// Pull-to-refresh ve sohbetten dönüşte tazeleme.
  Future<void> refresh() => load();
}
