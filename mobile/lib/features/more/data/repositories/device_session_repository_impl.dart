import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/more/domain/device_session_contracts.dart';

/// M01-4: Cihaz oturumları `/api/identity/sessions` üzerinden okunur ve kapatılır.
///
/// **Mock fallback yok:** sahte bir oturum listesi güvenlik ekranında yanlış bilgi verir —
/// kullanıcı "yalnız kendi telefonum açık" sanır. Hata olduğu gibi yukarı çıkar (A-05).
class DeviceSessionRepositoryImpl implements DeviceSessionRepository {
  DeviceSessionRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<DeviceSession>> list() async {
    final rows = await _apiClient.getList('/api/identity/sessions');
    return rows
        .cast<Map<String, dynamic>>()
        .map(_fromJson)
        .toList(growable: false);
  }

  @override
  Future<void> revoke(String sessionId) async {
    await _apiClient.post('/api/identity/sessions/$sessionId/revoke');
  }

  @override
  Future<int> revokeAll() async {
    final response = await _apiClient.post('/api/identity/sessions/revoke-all');
    // Gövde beklenmedik biçimdeyse 0 sayılır — kullanıcıya uydurma bir sayı gösterilmez.
    final value = response['revokedCount'];
    return value is int ? value : 0;
  }

  DeviceSession _fromJson(Map<String, dynamic> json) => DeviceSession(
    sessionId: json['sessionId'] as String,
    deviceName: json['deviceName'] as String?,
    createdOnUtc: DateTime.parse(json['createdOnUtc'] as String).toLocal(),
    lastUsedOnUtc: DateTime.parse(json['lastUsedOnUtc'] as String).toLocal(),
    isCurrent: json['isCurrent'] as bool? ?? false,
  );
}
