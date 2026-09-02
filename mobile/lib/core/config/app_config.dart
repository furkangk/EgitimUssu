import 'package:flutter/foundation.dart';

class AppConfig {
  const AppConfig({
    required this.apiBaseUrl,
    required this.appEnvironment,
    required this.useMockFallback,
    required this.mockFallbackFeatures,
  });

  final String apiBaseUrl;
  final String appEnvironment;
  final bool useMockFallback;
  final Set<String> mockFallbackFeatures;

  bool get isProductionLike {
    return appEnvironment == 'beta' || appEnvironment == 'production';
  }

  bool isMockFallbackEnabled(String feature) {
    if (!useMockFallback || isProductionLike) {
      return false;
    }
    return mockFallbackFeatures.contains('*') ||
        mockFallbackFeatures.contains(feature);
  }

  factory AppConfig.fromEnvironment() {
    const baseUrlOverride = String.fromEnvironment('API_BASE_URL');
    final baseUrl =
        baseUrlOverride.isEmpty ? _defaultBaseUrl() : baseUrlOverride;
    const appEnvironment = String.fromEnvironment(
      'APP_ENV',
      defaultValue: 'development',
    );
    // A-05: varsayılan **kapalı**. Sahte veri yalnız açık niyetle devreye girer
    // (`--dart-define=USE_MOCK_FALLBACK=true`); aksi halde API hatası ekranda görünür.
    const useMockFallback = bool.fromEnvironment(
      'USE_MOCK_FALLBACK',
      defaultValue: false,
    );
    const mockFallbackFeatures = String.fromEnvironment(
      'MOCK_FALLBACK_FEATURES',
      defaultValue: '*',
    );

    return AppConfig(
      apiBaseUrl: baseUrl,
      appEnvironment: appEnvironment,
      useMockFallback: useMockFallback,
      mockFallbackFeatures: mockFallbackFeatures
          .split(',')
          .map((feature) => feature.trim())
          .where((feature) => feature.isNotEmpty)
          .toSet(),
    );
  }

  /// `API_BASE_URL` verilmediğinde platforma göre varsayılan backend adresi.
  /// Backend varsayılan portu 5296'dır (`src/API.Host` launchSettings).
  /// Android emülatörü host makineye `10.0.2.2` ile ulaşır; iOS simülatörü ve
  /// masaüstü `localhost` kullanır.
  static String _defaultBaseUrl() {
    if (defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:5296';
    }
    return 'http://localhost:5296';
  }
}
