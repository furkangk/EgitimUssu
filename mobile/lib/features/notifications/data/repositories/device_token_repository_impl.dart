import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

/// M11-2: Cihaz push token'ını backend'e kaydeder/siler.
/// Mock fallback yok — sahte bir token kaydı hiçbir işe yaramaz, hatayı gizler (A-05 ilkesi).
class DeviceTokenRepositoryImpl implements DeviceTokenRepository {
  DeviceTokenRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<void> register({
    required String userId,
    required String token,
    required DevicePlatform platform,
    String? deviceName,
  }) async {
    await _apiClient.post(
      '/api/notifications/device-tokens',
      data: <String, dynamic>{
        'userId': userId,
        'token': token,
        'platform': platform.wireValue,
        'deviceName': deviceName,
      },
    );
  }

  @override
  Future<void> unregister(String token) async {
    await _apiClient.delete('/api/notifications/device-tokens/$token');
  }
}
