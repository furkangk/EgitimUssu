---
title: "Mesajlar (Gelen Kutusu)"
summary: "M16 gelen kutusu: sohbet listesi, son mesaj önizlemesi, okunmamış rozeti ve engel işareti; üç rol de aynı ekranı kullanır"
tags: [sayfa, messaging, mesajlasma, gelen-kutusu, m16]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/messaging/presentation/pages/messaging_inbox_page.dart
  - mobile/lib/features/messaging/presentation/cubit/conversations_cubit.dart
  - mobile/lib/features/messaging/domain/messaging_contracts.dart
  - mobile/lib/features/messaging/data/repositories/messaging_repository_impl.dart
  - mobile/lib/features/messaging/presentation/widgets/messaging_widgets.dart
updated: 2026-11-15
---

# Mesajlar (Gelen Kutusu)

> **Route:** `/messages` · **Feature:** `messaging` · **Veri:** 🟢 `GET /api/messaging/conversations`
> **Giriş:** öğretmen `/more` → "Mesajlar" · öğrenci `/student/profile` → "Mesajlar" · veli `/parent/home` başlık ikonu
> **State:** `ConversationsCubit` (ekran ömrü — kökte tek örnek değildir)
> **Güncelleme:** 2026-11-15

---

## 1. İçerik

| Bölüm | Kaynak | Not |
|-------|--------|-----|
| Başlık | `ConversationsState.totalUnread` | "N okunmamış mesaj"; sıfırsa alt başlık hiç çizilmez |
| Sohbet satırı | `GET /conversations` | Avatar (baş harf) + karşı taraf + son mesaj önizlemesi + göreli zaman + okunmamış rozeti |
| Engel işareti | `isBlocked` | Satırda kırmızı 🚫; sohbete girilebilir ama yazılamaz |
| Boş durum | — | "Henüz mesajın yok" + nereden sohbet başlatılacağı |

**Göreli zaman:** bugün → saat, dün → "Dün", son 7 gün → gün kısaltması, öncesi → `gg.aa`.

## 2. Kritik davranışlar

- **Karşı taraf isteği yapana göredir.** Sunucu `counterpartUserId`/`counterpartRole` alanlarını
  çağıranın perspektifinden döner: öğretmene öğrenci/veli, öğrenciye öğretmen görünür. Ekran bu
  hesabı tekrar etmez.
- **Boş liste hata değildir.** Henüz kimseyle konuşmamış kullanıcı normal bir durumdur;
  `EmptyStateView` gösterilir, `ErrorStateView` değil.
- **Sohbetten dönünce liste tazelenir** — okunmamış rozeti, sohbette okunanları yansıtmalı.
- **Pull-to-refresh** desteklenir (ux_rules §4).
- Oturum yoksa `SessionRequiredView` (D-17): sahte kullanıcı uydurulmaz.

## 3. Henüz olmayan

| Eksik | Neden | Nerede gelecek |
|-------|-------|----------------|
| Karşı tarafın **adı** ve fotoğrafı | `GET /conversations` yalnız `counterpartUserId` döner; ad alanı sunucuda yok. Uydurma ad yazmak yerine rol etiketi ("Öğrenci"/"Veli"/"Öğretmen") gösterilir | M16 sonraki turu |
| Sayfalama (sonsuz scroll) | Gelen kutusu tek seferde 50 satır çeker; bu sayının aşılması beklenmiyor | ihtiyaç doğarsa |
| Sohbet arama / filtre | — | ihtiyaç doğarsa |
| Sohbet susturma | Uç yok; bildirim tercihi M15'te | — |

---

> İlgili: sohbet ekranı → [`messaging_chat.md`](messaging_chat.md) · modül → [`../modules/m16_messaging.md`](../modules/m16_messaging.md)

*Mesajlar (Gelen Kutusu) | Güncelleme: 2026-11-15 (P10 Task 4 — D-08)*

## P20 Task 4 — Ön plan tazeleme

Ön planda mesaj bildirimi geldiğinde açık gelen kutusu ve okunmamış toplamı yetkili API’den yeniden okunur. Tek uçuş ve bir bekleyen yenileme push yığınlarını birleştirir. Hesap değişiminde ekranın cubit’i yeniden oluşturulur; eski yanıtlar uygulanmaz. Sohbetten dönüş ve elle yenileme korunur. Ekran/ortak mesaj widget metinleri tr/en ARB’dedir.

Güncelleme: 2026-11-15
