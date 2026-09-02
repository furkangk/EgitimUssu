import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('enables mock fallback per feature in development only', () {
    final config = AppConfig(
      apiBaseUrl: 'http://localhost',
      appEnvironment: 'development',
      useMockFallback: true,
      mockFallbackFeatures: <String>{'auth', 'scheduling'},
    );

    expect(config.isMockFallbackEnabled('scheduling'), isTrue);
    expect(config.isMockFallbackEnabled('payments'), isFalse);
  });

  test('disables mock fallback in beta even when feature is listed', () {
    final config = AppConfig(
      apiBaseUrl: 'http://localhost',
      appEnvironment: 'beta',
      useMockFallback: true,
      mockFallbackFeatures: <String>{'*'},
    );

    expect(config.isMockFallbackEnabled('scheduling'), isFalse);
  });

  test('mock fallback varsayilan olarak kapalidir', () {
    final config = AppConfig.fromEnvironment();

    expect(config.useMockFallback, isFalse);
    expect(config.isMockFallbackEnabled('payments'), isFalse);
  });
}
