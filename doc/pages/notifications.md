---
title: "Bildirim Merkezi Ekranı"
summary: "Rolden bağımsız in-app bildirim listesi (/notifications): Tümü/Okunmamış filtresi, okundu işareti, route ile derin bağlantı ve zil rozeti"
tags: [sayfa, bildirim, notifications]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/notifications/presentation/pages/notifications_page.dart
  - mobile/lib/features/notifications/presentation/cubit/notification_center_cubit.dart
  - mobile/lib/features/notifications/presentation/cubit/notification_center_state.dart
  - mobile/lib/features/notifications/data/repositories/notification_repository_impl.dart
  - mobile/lib/shared/widgets/app_page_header.dart
updated: 2026-11-15
---

# Bildirim Merkezi (`/notifications`)

> **Feature:** `notifications` · **State:** `NotificationCenterCubit` / `NotificationCenterState` · **Veri:** Gerçek API (mock fallback **yok**)
> **Güncelleme:** 2026-11-15

## Rota davranışı (P20 T2)

Veli dahil bütün oturumlu roller bu merkeze erişebilir. Kart önce okundu işaretlenir; hedef varsa OS push ile aynı `mapPushRoute` kullanılır. `/scheduling` rolün ders görünümünü, veli ödev/ödeme hedefleri onaylı çocuk özetini açar. Bilinmeyen veya yetkisiz hedefte kullanıcı bildirim merkezinde kalır. Başlık ve göreli zamanlar tr/en ARB'dedir. Veli ana sayfasındaki zil bu ekranı açar; `/parent/notifications` ayrı tercih ekranıdır.

## Amaç
Öğrenci, öğretmen ve velinin **tek** bildirim merkezi. Ders hatırlatması, ödev, ödeme ve sistem
bildirimleri aynı listede toplanır; okunmamışlar rozetle sayılır.

## Erişim
Her rol. Zil ikonundan (`AppPageHeader`) veya bir push bildirimine dokunarak açılır.
Oturum yoksa "Bildirimleri görmek için giriş yapmalısın." boş durumu gösterilir.

## Veri kaynağı

| İşlem | Uç |
|-------|-----|
| Liste | `GET /api/notifications/users/{userId}/notifications?onlyUnread=&after=&limit=` |
| Rozet | `GET /api/notifications/users/{userId}/unread-count` |
| Okundu | `POST /api/notifications/{notificationId}/read` |

Yetki: kullanıcı yalnız **kendi** bildirimlerini görür (Admin istisna) — `UserNotificationAuthorizer`.

## Yapı
- **Başlık:** geri butonu, "Bildirimler", altında `{n} okunmamış`, sağda zil ikonu.
- **Filtre:** "Tümü" / "Okunmamış" çipleri → `NotificationCenterCubit.setOnlyUnread(bool)` listeyi yeniden yükler.
- **Kart:** türe göre ikon (`kind`), başlık, gövde, göreli zaman ("3 sa önce"), okunmamışsa mavi nokta + kalın başlık + vurgulu kenarlık.
- **Boş durum:** "Henüz bildirimin yok" (filtreye göre alt metin değişir).

### Tür → ikon eşlemesi
| `kind` | İkon |
|--------|------|
| `LessonReminder`, `LessonCompleted` | `event_available_rounded` |
| `AssignmentCreated`, `AssignmentDue`, `AssignmentMissed` | `assignment_rounded` |
| `PaymentDue`, `PaymentOverdue` | `payments_rounded` |
| `MessageReceived` | `chat_bubble_rounded` |
| diğer / `System` | `notifications_rounded` |

## Davranış
- **Dokunma:** önce `markRead(id)` (**iyimser** — sunucu reddederse geri alınır), sonra `route` doluysa `context.push(route)`.
- **Zaten okunmuş öğe:** istek gönderilmez, rozet bozulmaz.
- **Rozet senkronu:** `BlocListener` okunmamış sayısı değişince uygulama genelindeki
  `NotificationsCubit.setUnreadCount(n)`'i çağırır → `AppPageHeader`'daki zil rozeti anında düşer.
- **Yenileme:** aşağı çekme (`RefreshIndicator`).

> **Not (2026-09-03):** Zil rozeti önceden *bekleyen ders hatırlatması* sayısından türetiliyordu
> (yalnız öğretmende anlamlıydı). Artık gerçek `unread-count` ucundan gelir ve üç rolde de doğrudur.

## İlgili
- Modül: [`../modules/m11_notifications.md`](../modules/m11_notifications.md) (M11)
- Push zinciri: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §7.1
- Veli bildirim tercihleri: [`../modules/m15_settings.md`](../modules/m15_settings.md) (P05)

## P19 Task 7 — Sonsuz kaydırma

Liste `onlyUnread/after/limit=50` ile cursor sayfası ister; sunucudan `items/nextCursor` okur. Kaydırmada tek devam isteği çalışır, son sayfada durur. Kısa sayfa görünümü otomatik doldurur. Devam hatası mevcut kartları ve cursor'u korur, alttaki yeniden deneme düğmesiyle sürer; ilk yükleme hatası tam hata görünümüdür. Tümü/Okunmamış değişimi ve yenileme cursor'u sıfırlar; önceki isteğin gecikmiş yanıtı yeni filtreyi ezmez. Kapanan cubit emit yapmaz. Ekranın tüm sabit metinleri ve göreli zamanları tr/en ARB'dedir. Eski skip/take API tüketicileri çalışmayı sürdürür.

*Güncelleme: 2026-11-15 — P19 Task 7.*
