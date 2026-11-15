import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/messaging/data/repositories/messaging_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';
import '../../helpers/fake_messaging_repository.dart';

class MarkerRepository extends FakeMessagingRepository {
  MarkerRepository({super.messages});
  final List<String?> markers = <String?>[];
  @override
  Future<void> markRead(String conversationId, {String? upToMessageId}) async {
    markers.add(upToMessageId);
  }
}

void main() {
  ChatMessage row(
    String id,
    int second, {
    ChatMessageStatus status = ChatMessageStatus.sent,
  }) => ChatMessage(
    id: id,
    conversationId: 'c',
    senderUserId: 'other',
    body: 'metin',
    sentOnUtc: DateTime.utc(2026, 11, 15, 12, 0, second),
    readOnUtc: null,
    isDeleted: false,
    status: status,
  );
  test('gercek repository okundu sinirini JSON govdesinde gonderir', () async {
    final adapter = MarkerAdapter();
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))
      ..httpClientAdapter = adapter;
    final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    addTearDown(() {
      api.dispose();
      dio.close();
    });
    await MessagingRepositoryImpl(
      apiClient: api,
    ).markRead('c', upToMessageId: 'seen');
    expect(adapter.request?.path, '/api/messaging/conversations/c/read');
    expect(adapter.request?.data, <String, dynamic>{'upToMessageId': 'seen'});
  });
  test('okundu siniri yalniz yuklenen son sunucu mesajidir', () async {
    final repository = MarkerRepository(
      messages: <ChatMessage>[row('older', 0), row('seen', 1)],
    );
    final cubit = ChatCubit(repository: repository, currentUserId: 'me');
    await cubit.load('c');
    repository.store.add(row('unseen', 2));
    await cubit.markRead();
    expect(repository.markers, <String?>['seen', 'seen']);
    await cubit.close();
  });
  test('bos veya yalniz taslak liste okundu istegi uretmez', () async {
    final repository = MarkerRepository();
    final cubit = ChatCubit(repository: repository, currentUserId: 'me');
    await cubit.load('c');
    expect(repository.markers, isEmpty);
    cubit.emit(
      cubit.state.copyWith(
        messages: <ChatMessage>[
          row('draft', 2, status: ChatMessageStatus.failed),
        ],
      ),
    );
    await cubit.markRead();
    expect(repository.markers, isEmpty);
    await cubit.close();
  });
}

class MarkerAdapter implements HttpClientAdapter {
  RequestOptions? request;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    request = options;
    return ResponseBody.fromString(
      '{}',
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
