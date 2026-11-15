---
title: "Sohbet Ekranı"
summary: "M16 sohbet ekranı: balonlar, keyset sayfalama ile geçmiş, iyimser gönderim ve tekrar dene, okundu işareti, engelleme"
tags: [sayfa, messaging, mesajlasma, sohbet, m16]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/messaging/presentation/pages/chat_page.dart
  - mobile/lib/features/messaging/presentation/cubit/chat_cubit.dart
  - mobile/lib/features/messaging/presentation/widgets/messaging_widgets.dart
  - mobile/lib/features/messaging/presentation/open_conversation_action.dart
updated: 2026-11-15
---

# Sohbet

> **Route:** `/messages/:conversationId` · **Feature:** `messaging` · **Veri:** 🟢 `/api/messaging`
> **Giriş:** gelen kutusundan satıra dokunma · "Mesaj gönder" düğmeleri · **push bildirimine dokunma**
> **State:** `ChatCubit` (ekran ömrü)
> **Güncelleme:** 2026-11-15

---

## 1. Push'tan derin bağlantı

Sunucu yeni mesaj bildirimini `Route = "/messages/{conversationId}"` ile yazar
(`MessageSentNotificationHandler`). Uygulama kökündeki push dinleyicisi (`app/app.dart`) `data.route`
alanını rol bazlı `mapPushRoute` ile doğrular; yalnız izinli uygulama içi hedef açılır (P20 T2).

## 2. İçerik

| Bölüm | Kaynak | Not |
|-------|--------|-----|
| Başlık | `ConversationDetail` | Karşı tarafın rolü; engelliyse alt başlık "Engellendi" |
| Menü | — | "Engelle" (onay diyaloglu) / "Engeli kaldır" |
| Balonlar | `GET /conversations/{id}/messages` | Kendi mesajın sağda (primary zemin), karşı taraf solda |
| Balon altı | — | Saat + kendi mesajında okundu ✓✓; gönderilirken "Gönderiliyor…"; hatada neden + "Tekrar dene" |
| Yazma alanı | — | 1-5 satır büyüyen alan + gönder düğmesi (48px dokunma hedefi) |

## 3. Kritik davranışlar

- **Keyset sayfalama, offset değil.** Liste `reverse: true` çizilir; kullanıcı geçmişe kaydırdıkça
  imleç listedeki **en eski** mesajın `sentOnUtc`'si olarak gönderilir. Offset kullanılsaydı sayfalar
  arasında yeni mesaj geldiğinde satırlar kayar, kullanıcı bir mesajı iki kez görürdü. Sonsuz scroll;
  "daha fazla yükle" düğmesi yok (ux_rules §6).
- **İyimser gönderim.** Mesaj sunucu yanıtı beklenmeden listeye girer (`sending`). Başarısız olursa
  **kaybolmaz**, `failed` durumuna düşer ve "Tekrar dene" aynı metni yeniden gönderir — kullanıcı
  yazdığını yeniden yazmak zorunda kalmaz. Başarıda taslak satır sunucudan gelenle **değiştirilir**
  (kopya bırakmaz).
- **Bekleyen mesaj imleç olamaz.** Sayfalama, sunucunun bilmediği taslak satırları atlar.
- **Engel çift yönlüdür.** Kim koyarsa koysun yazma alanı kapanır ve yerine açıklama çıkar; boşuna
  yazdırmak yerine nedeni söylenir. Engeli **yalnız koyan taraf** kaldırabilir — karşı tarafın koyduğu
  engelde menü hiç gösterilmez (ölü menü açmak yanıltıcı olurdu).
- **Okundu işareti yan etkidir.** Ekran açılınca yüklü en yeni sunucu mesajının `upToMessageId` alanıyla `POST /read` çağrılır; boş/taslak listede çağrı yapılmaz. Daha sonra gelen mesaj unread kalır; başarısız olursa **yutulur**,
  mesajlar görünür kalır ve bir sonraki açılışta yeniden denenir.
- **Silinmiş mesaj listede kalır**, içeriği yerine "Bu mesaj silindi" yazar (sunucu `body: null` döner).
- Boş/boşluk mesaj sunucuya hiç gitmez.

## 4. Henüz olmayan

| Eksik | Neden | Nerede gelecek |
|-------|-------|----------------|
| Karşı tarafın adı/fotoğrafı | Sunucu ad döndürmüyor | M16 sonraki turu |
| Mesajı **şikayet et** | M18 uçları yok | P12 |
| Ek/dosya gönderme | Kapsam dışı (v1 yalnız metin) | — |
| Gerçek zamanlı teslimat | Karar **K-07**: v1 REST + push; SignalR v2'ye ertelendi | v2 |
| Yeni mesajın ekran açıkken düşmesi | ✅ P20 T4: görünür sohbette10sn poll + ön plan push | Uygulandı |
| Mesaj silme düğmesi | Uç var (`DELETE /messages/{id}`) ama UI'da tetikleyici yok | M16 sonraki turu |

---

> İlgili: gelen kutusu → [`messaging_inbox.md`](messaging_inbox.md) · modül → [`../modules/m16_messaging.md`](../modules/m16_messaging.md)

*Sohbet Ekranı | Güncelleme: 2026-11-15 (P10 Task 4 — D-08)*

## P20 Task 4 — Ön plan tazeleme

Görünür ve ön plandaki sohbet her 10 saniyede REST ile tazelenir; aynı sohbetin ön plan push olayı beklemeden tazeler. Başka ekran üzerine açıldığında, uygulama arka plana geçtiğinde ve çıkışta poll durur; dönüşte hemen okunur. Cubit kapanınca timer/abonelik iptal edilir. Yeni sayfalar bilinen mesaja ulaşana kadar keyset ile okunur; yüklü geçmiş ve gönderim taslakları korunur, sunucu kimliğiyle tekrarlar elenir. Geç eski oturum yanıtları uygulanmaz. Push içeriği mesaj olarak gösterilmez. Metinler tr/en ARB’dedir.

Güncelleme: 2026-11-15
