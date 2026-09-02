# egitim_ussu_mobile

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Lab: Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Cookbook: Useful Flutter samples](https://docs.flutter.dev/cookbook)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.

## Sahte (mock) veriyle çalıştırma

Varsayılan olarak **kapalıdır** — uygulama daima gerçek API'yi kullanır (A-05). Backend olmadan denemek için:

```bash
flutter run --dart-define=USE_MOCK_FALLBACK=true --dart-define=MOCK_FALLBACK_FEATURES=payments,scheduling
```

`MOCK_FALLBACK_FEATURES=*` tüm özellikleri sahte veriye düşürür (bayrak verilmezse varsayılan budur).
Beta/production ortamında (`APP_ENV=beta|production`) bu bayrak **yok sayılır**.

## API adresi

`--dart-define=API_BASE_URL=...` ile değiştirilir. Verilmezse Android emülatöründe `http://10.0.2.2:5296`,
iOS simülatörü/masaüstünde `http://localhost:5296` kullanılır (backend varsayılan portu 5296).
