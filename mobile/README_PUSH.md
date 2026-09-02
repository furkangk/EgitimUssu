# Push Bildirim Kurulumu (A-04 / P03)

Kod tarafı hazır; **Firebase projesi** kurulmadan push çalışmaz. Uygulama bu durumda
açılmaya devam eder ve `FirebasePushBinding` bir **uyarı** log'lar (sessiz kayıp yok).

## 1. Firebase projesi

1. [Firebase Console](https://console.firebase.google.com) → yeni proje.
2. Android uygulaması ekle → paket adı `com.example.egitim_ussu_mobile` (gerçek değeri
   `android/app/build.gradle.kts` içindeki `applicationId`'den doğrula) → `google-services.json`
   indir → `mobile/android/app/google-services.json`.
3. iOS uygulaması ekle → bundle id (`ios/Runner.xcodeproj` → `PRODUCT_BUNDLE_IDENTIFIER`) →
   `GoogleService-Info.plist` indir → `mobile/ios/Runner/GoogleService-Info.plist`.
4. Her ikisi de `.gitignore`'dadır — **repoya commit'leme**.

## 2. Android gradle eklentisi

`google-services.json` yerine konduktan **sonra** (öncesinde build kırılır):

- `android/settings.gradle.kts` → plugins bloğuna
  `id("com.google.gms.google-services") version "4.4.2" apply false`
- `android/app/build.gradle.kts` → plugins bloğuna `id("com.google.gms.google-services")`

## 3. iOS APNs

Apple Developer → APNs Auth Key (.p8) üret → Firebase Console → Project Settings →
Cloud Messaging → iOS app → APNs Authentication Key yükle. Xcode'da
**Push Notifications** ve **Background Modes → Remote notifications** yeteneklerini aç.

## 4. Backend

Firebase Console → Project Settings → Service accounts → "Generate new private key" →
inen JSON'u **repoya koymadan** ortam değişkeni olarak ver:

```bash
export Push__Provider=Fcm
export Push__ProjectId=<firebase-proje-kimligi>
export Push__ServiceAccountJson='{"type":"service_account",...}'
```

Eksik ayarla uygulama **açılmaz** (`PushOptionsGuard`, fail-fast). Varsayılan `Push:Provider=Logging`
push göndermez, yalnız log'a yazar.

## 5. Doğrulama

1. Uygulamada giriş yap → backend `notifications.device_tokens` tablosunda satır oluşmalı.
2. Bir ders planla (hatırlatma üretilir) veya bir ödev oluştur (`UserNotification` üretilir).
3. `NotificationDispatcher` turu (30 sn) bildirimi FCM'e gönderir.
4. Cihazda bildirim görünmeli; dokununca `data.route` rotası açılmalı.
