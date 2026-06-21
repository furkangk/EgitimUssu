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
    const baseUrl = String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://10.0.2.2:8080',
    );
    const appEnvironment = String.fromEnvironment(
      'APP_ENV',
      defaultValue: 'development',
    );
    const useMockFallback = bool.fromEnvironment(
      'USE_MOCK_FALLBACK',
      defaultValue: true,
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
}
