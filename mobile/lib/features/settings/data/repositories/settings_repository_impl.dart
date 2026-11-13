import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/settings/data/models/user_settings_model.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';

/// M15-1: Ayar uçlarının istemci tarafı. **Mock fallback yok** (A-05/D-14):
/// sahte bir ayar ekranı, gerçekte kaydedilmeyen tercihi kaydedilmiş gibi gösterir.
class SettingsRepositoryImpl implements SettingsRepository {
  SettingsRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<UserSettings> load(String userId) async {
    final response = await _apiClient.get('/api/settings/users/$userId');
    return UserSettingsModel.fromJson(response);
  }

  @override
  Future<UserSettings> save(String userId, UserSettings settings) async {
    final response = await _apiClient.put(
      '/api/settings/users/$userId',
      data: UserSettingsModel.toRequest(settings),
    );
    return UserSettingsModel.fromJson(response);
  }
}
