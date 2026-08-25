---
title: "M16 — Mesajlaşma"
summary: "Planlanan mesajlaşma modülü; backend klasörü henüz yok (Faz 2-3, tüm domain önerilen)"
tags: [modul, messaging, planlanan, faz-2, faz-3]
status: "🔴"
authority: product
updated: 2026-08-19
---

# 💬 Mesajlaşma Modülü (M16) — Detaylı Tasarım Dokümanı

> **PRD: M16 (YENİ)** · **Faz 2-3** · **Durum: 🔴 YENİ — kodda HİÇ YOK (tüm domain ⚠️ Önerilen, planlanan)**
>
> **Amaç:** Öğretmen ile öğrenci/veli arasında, ders bağlamına bağlı, güvenli ve denetlenebilir
> uygulama içi mesajlaşma. Bugün bu iletişim telefon/WhatsApp üzerinden, sistem dışında yürüyor
> (bkz. [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)); amaç bu trafiği platforma taşımaktır.

> **KESİN KURAL (PRD — kapsam):** Mesajlaşma **yalnızca** iki çift arasında kurulur:
> **öğretmen ↔ öğrenci** ve **öğretmen ↔ veli**.
> **öğrenci ↔ veli**, **öğrenci ↔ öğrenci** ve **öğretmen ↔ öğretmen** mesajlaşması **YASAKTIR** ve domain seviyesinde reddedilir.
>
> İlgili: yeni mesaj bildirimi → [`m11_notifications.md`](m11_notifications.md) · mesaj şikayeti / moderasyon → [`m18_feedback.md`](m18_feedback.md) · gizlilik/engelleme tercihleri → [`m15_settings.md`](m15_settings.md).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

🔴 **Kodda hiçbir karşılığı yoktur.** Bu modül tamamen planlama aşamasındadır:

- **Backend:** `src/Modules/Messaging/` **yok** — `MessagingDbContext` yok, `ModuleDefinition` yok, DI kaydı yok, EF migration yok, hatta `/status` placeholder endpoint'i bile yok.
- **Mobil:** `mobile/lib/features/messaging/` **yok**.
- **Şema:** PostgreSQL'de `messaging` şeması **yok**.
- **Olay:** Diğer modüller henüz hiçbir mesajlaşma integration event'i yayınlamıyor/dinlemiyor.

> ⚠️ Bu dokümandaki **tüm** domain modeli, API, iş kuralı ve ekranlar **önerilen / planlanan**dır.
> Sıfırdan modül oluşturma deseni için bkz. [`00_genel_bakis.md`](00_genel_bakis.md) §"Backend modül katman yapısı".

---

## 2. Domain Modeli (⚠️ Önerilen)

**Şema:** `messaging` · **DbContext:** `MessagingDbContext` · **Route prefix:** `/api/messaging`
**Aggregate kökü:** `Conversation` (içinde `ConversationParticipant` ve `Message` entity'leri).

> Modül sınırı kuralı: `Messaging`, `Identity`/`Students`/`Parents` tablolarına **doğrudan erişmez**.
> Katılımcı doğrulaması (öğretmen-öğrenci/veli ilişkisi) integration event'lerle beslenen yerel projeksiyon
> üzerinden yapılır (bkz. §4 ve §5).

### 2.1 `Conversation` (AggregateRoot)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Birincil anahtar |
| `ConversationType` | enum `ConversationType` | `TeacherStudent=1`, `TeacherParent=2` (başka tür yasak) |
| `TeacherUserId` | Guid | Sohbetin öğretmen tarafı (Identity kullanıcısı) |
| `CounterpartUserId` | Guid | Karşı taraf — öğrenci **veya** veli kullanıcısı (Identity) |
| `StudentId` | Guid | Sohbetin **bağlamı** olan öğrenci profili (hangi öğrenci hakkında) |
| `Status` | enum `ConversationStatus` | `Active=1`, `Archived=2`, `Blocked=3`, `Closed=4` |
| `LastMessageAtUtc` | DateTime? | Son mesaj zamanı (liste sıralaması için) |
| `LastMessagePreview` | string? | Son mesajın kısa önizlemesi (maks. 140) |
| `BlockedByUserId` | Guid? | Sohbeti engelleyen taraf (varsa) |
| `CreatedOnUtc` | DateTime | |
| `UpdatedOnUtc` | DateTime | |

**Davranışlar (metotlar):**
- `Start(...)` → katılımcıları oluşturur, çifti doğrular → `ConversationStartedDomainEvent`.
- `AppendMessage(senderUserId, body, attachmentUrl, utcNow)` → gönderenin katılımcı olduğunu ve sohbetin `Active` olduğunu doğrular; `LastMessageAtUtc`/`LastMessagePreview` günceller; karşı tarafın `UnreadCount`'unu artırır → `MessageSentDomainEvent`.
- `MarkRead(readerUserId, utcNow)` → okuyanın `LastReadAtUtc`/`UnreadCount` değerlerini günceller → `ConversationReadDomainEvent`.
- `Block(byUserId, utcNow)` / `Unblock(byUserId)` → `Status = Blocked/Active` → `ConversationBlockedDomainEvent`.
- `Archive(byUserId)` / `Close(utcNow)`.

**Domain event'leri:** `ConversationStartedDomainEvent`, `MessageSentDomainEvent`, `ConversationReadDomainEvent`, `ConversationBlockedDomainEvent`.

**DB:** tablo `conversations`; index'ler:
- `(TeacherUserId, CounterpartUserId, StudentId, ConversationType)` → **UNIQUE** (aynı çift + öğrenci bağlamı için tek sohbet).
- `(TeacherUserId, LastMessageAtUtc DESC)`, `(CounterpartUserId, LastMessageAtUtc DESC)` → gelen kutusu listeleme.

### 2.2 `ConversationParticipant` (Entity — `Conversation` çocuğu)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `ConversationId` | Guid | Üst sohbet (FK) |
| `UserId` | Guid | Identity kullanıcısı |
| `Role` | enum `ParticipantRole` | `Teacher=2`, `Student=3`, `Parent=4` (Identity `UserRole` ile hizalı) |
| `LastReadAtUtc` | DateTime? | **Okundu bilgisi** — bu ana kadar olan mesajları okudu |
| `UnreadCount` | int | Okunmamış mesaj sayısı (rozet için) |
| `IsMuted` | bool | Bu sohbet için bildirim sustur |
| `JoinedOnUtc` | DateTime | |

**DB:** tablo `conversation_participants`; index `(ConversationId, UserId)` UNIQUE, `(UserId)`.

### 2.3 `Message` (Entity — `Conversation` çocuğu)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `ConversationId` | Guid | Üst sohbet (FK) |
| `SenderUserId` | Guid | Gönderen (katılımcı olmalı) |
| `Body` | string | Mesaj metni (maks. 4000; boş olamaz) |
| `AttachmentUrl` | string? | Ek/dosya bağlantısı (premium — bkz. [`m17_membership.md`](m17_membership.md)) |
| `Status` | enum `MessageStatus` | `Sent=1`, `Delivered=2`, `Read=3`, `Deleted=4` |
| `SentAtUtc` | DateTime | |
| `EditedAtUtc` | DateTime? | Düzenlendi (kısıtlı süre içinde) |
| `DeletedAtUtc` | DateTime? | Yumuşak silme (içerik gizlenir, denetim için kayıt kalır) |

**DB:** tablo `messages`; index `(ConversationId, SentAtUtc DESC)` → sayfalama (keyset/`before` imleci).

### 2.4 Enum'lar (⚠️ Önerilen)

| Enum | Değerler |
|------|----------|
| `ConversationType` | `TeacherStudent=1`, `TeacherParent=2` |
| `ConversationStatus` | `Active=1`, `Archived=2`, `Blocked=3`, `Closed=4` |
| `ParticipantRole` | `Teacher=2`, `Student=3`, `Parent=4` |
| `MessageStatus` | `Sent=1`, `Delivered=2`, `Read=3`, `Deleted=4` |

---

## 3. API Sözleşmesi (⚠️ Önerilen — `/api/messaging`)

```
GET  /api/messaging/conversations?userId={id}&status=          → kullanıcının gelen kutusu (rol fark etmez)
POST /api/messaging/conversations                              → sohbet başlat/bul (çift kuralını doğrular)
GET  /api/messaging/conversations/{conversationId}             → sohbet başlığı + katılımcılar
GET  /api/messaging/conversations/{conversationId}/messages?before={msgId}&take=30
                                                               → mesaj geçmişi (keyset sayfalama)
POST /api/messaging/conversations/{conversationId}/messages    → mesaj gönder
PUT  /api/messaging/conversations/{conversationId}/read        → okundu işaretle (LastReadAtUtc)
POST /api/messaging/conversations/{conversationId}/mute        → bu sohbeti sustur/aç
POST /api/messaging/conversations/{conversationId}/block       → karşı tarafı engelle / engeli kaldır
POST /api/messaging/conversations/{conversationId}/messages/{messageId}/report
                                                               → mesajı şikayet et → m18 AbuseReport
DELETE /api/messaging/conversations/{conversationId}/messages/{messageId}
                                                               → kendi mesajını sil (yumuşak silme)
```

> **Yetki:** Her endpoint, çağıran kullanıcının ilgili sohbetin **katılımcısı** olmasını zorunlu kılar
> ("varsayılan reddet" guard'ı — bkz. [`mimari_inceleme.md`](mimari_inceleme.md) K3). Admin yalnızca
> moderasyon/şikayet bağlamında erişir (bkz. [`../roles/admin.md`](../roles/admin.md)).
> Gerçek zamanlı teslimat ileride SignalR/WebSocket ile; ilk sürümde polling + push bildirimi yeterlidir.

---

## 4. İş Kuralları

1. **Çift kuralı (en kritik):** Sohbet yalnızca `TeacherStudent` veya `TeacherParent` türünde kurulur.
   `Conversation.Start` çağrısında bir taraf **öğretmen** değilse veya tür bu ikisinden biri değilse domain `Result.Failure` döner.
   Öğrenci↔veli, öğrenci↔öğrenci, öğretmen↔öğretmen sohbeti **hiçbir koşulda** açılamaz.
2. **İlişki önkoşulu:**
   - `TeacherStudent`: öğretmen ile öğrenci arasında kurulu bir ilişki olmalı (öğretmen öğrenciyi eklemiş/eşleşmiş — bkz. [`m03_students.md`](m03_students.md)).
   - `TeacherParent`: veli, o öğretmenin bir öğrencisine bağlı bir veli olmalı (bkz. [`m09_parents.md`](m09_parents.md)).
   - Eşleştirme (M12) ile kurulan yeni ilişkiler de geçerli sayılır (bkz. [`m12_matching.md`](m12_matching.md)).
3. **Tekillik:** Aynı `(TeacherUserId, CounterpartUserId, StudentId, ConversationType)` için en fazla bir aktif sohbet bulunur; "başlat" çağrısı varsa mevcut sohbeti döndürür (get-or-create).
4. **Gönderim:** Yalnızca sohbetin katılımcısı, sohbet `Active` iken mesaj gönderebilir. `Blocked`/`Closed` sohbete mesaj gönderilemez.
5. **Engelleme (KVKK/kötüye kullanım):** Taraflardan biri diğerini engelleyebilir → sohbet `Blocked`, yeni mesaj reddedilir. Engel kaldırılınca tekrar `Active` olur. Engel durumu karşı tarafa nötr gösterilir.
6. **Okundu bilgisi:** Okundu durumu `ConversationParticipant.LastReadAtUtc` üzerinden hesaplanır; bir mesaj, karşı tarafın `LastReadAtUtc`'sinden eski/eşitse `Read` kabul edilir.
7. **Silme/düzenleme:** Kullanıcı yalnızca **kendi** mesajını silebilir/düzenleyebilir. Silme **yumuşak silmedir** (içerik gizlenir; moderasyon ve KVKK denetimi için kayıt saklanır).
8. **Hız sınırı / spam:** Kullanıcı başına dakikalık mesaj sınırı (free için daha düşük — bkz. [`m17_membership.md`](m17_membership.md)). Aşımda `429`.
9. **Moderasyon:** Bir mesaj şikayet edildiğinde M18'de `AbuseReport(TargetType=Message)` oluşur; admin sohbeti/mesajı inceleyip kaldırabilir (bkz. [`m18_feedback.md`](m18_feedback.md)).
10. **Bildirim:** Yeni mesajta, alıcı sohbeti susturmadıysa ve ayarları izin veriyorsa push/in-app bildirim üretilir (bkz. [`m11_notifications.md`](m11_notifications.md), [`m15_settings.md`](m15_settings.md)).
11. **Premium farkı:** Free kullanıcıda ek/dosya gönderimi ve geçmiş derinliği sınırlı; premium sınırsız (bkz. [`m17_membership.md`](m17_membership.md) §rol bazlı özellikler).

---

## 5. Olay Akışı (⚠️ Önerilen)

```
[Sohbet başlatma]
İstemci → POST /conversations (teacherUserId, counterpartUserId, studentId, type)
   → Conversation.Start() çift + ilişki kuralını doğrular
      → UNIQUE çakışırsa mevcut sohbet döner (get-or-create)
      → yeni ise ConversationStartedDomainEvent

[Mesaj gönderme]
İstemci → POST /conversations/{id}/messages (body)
   → Conversation.AppendMessage()  → MessageSentDomainEvent
      → (Outbox) MessageSentIntegrationEvent
         → Notifications (M11): alıcıya "Yeni mesaj" bildirimi
            (alıcı IsMuted=false ve UserSetting izin veriyorsa)

[Okundu]
Alıcı sohbeti açar → PUT /conversations/{id}/read
   → katılımcının LastReadAtUtc/UnreadCount güncellenir → ConversationReadDomainEvent

[Şikayet]
Kullanıcı → POST /conversations/{id}/messages/{msgId}/report (reason)
   → (Outbox) MessageReportedIntegrationEvent
      → Feedback (M18): AbuseReport(TargetType=Message, TargetId=msgId) oluştur
         → Admin moderasyonu (M18) → gerekirse mesaj/sohbet kaldırma

[İlişki beslemesi — read-model]
Students/Parents/Matching modüllerinden:
   StudentLinkedToTeacher / ParentLinkedToStudent / MatchRequestAccepted (integration event)
      → Messaging: "izinli çift" projeksiyonunu günceller (çift doğrulaması için)
```

---

## 6. Mobil Ekranlar (Planlanan)

`mobile/lib/features/messaging/` (Flutter, `flutter_bloc`/Cubit, `go_router`, `dio`):

- **conversation-list** — gelen kutusu: karşı taraf adı, son mesaj önizlemesi, okunmamış rozeti, sohbet türü etiketi (öğrenci/veli).
- **conversation-thread** — mesaj balonları, gönder kutusu, okundu işareti, "engelle/sustur/şikayet et" menüsü; keyset sayfalama ile geçmiş yükleme.
- **new-conversation** — öğretmen için: öğrenci/veli seçerek sohbet başlatma (yalnızca izinli çiftler listelenir).
- **conversation-info** — katılımcılar, ilgili öğrenci bağlamı, engelleme/sustur ayarları.

> Renk/temaya `0xFF082B4F` kurumsal rengi uygulanır. Premium olmayan kullanıcıya gelen kutusunda reklam yerleşimi
> (bkz. [`m17_membership.md`](m17_membership.md) `AdPlacement`).

---

## 7. Kabul Kriterleri (⚠️ Önerilen)

- [ ] Yalnızca öğretmen↔öğrenci ve öğretmen↔veli sohbeti açılabiliyor; diğer çiftler domain tarafından reddediliyor.
- [ ] İlişkisi olmayan taraflar sohbet başlatamıyor (öğretmen-öğrenci/veli bağı doğrulanıyor).
- [ ] Aynı çift + öğrenci bağlamı için tek sohbet (get-or-create) çalışıyor.
- [ ] Mesaj gönderme, listeleme (keyset sayfalama) ve okundu işareti çalışıyor.
- [ ] Okunmamış sayacı ve son mesaj önizlemesi gelen kutusunda doğru.
- [ ] Engelleme ile mesaj gönderimi durduruluyor; engel kaldırma geri açıyor.
- [ ] Yeni mesajta alıcıya bildirim gidiyor (susturma/ayar tercihlerine saygı ile).
- [ ] Mesaj şikayeti M18'de `AbuseReport` oluşturuyor; admin kaldırabiliyor.
- [ ] Yumuşak silme: içerik gizleniyor, denetim kaydı kalıyor.
- [ ] Hız sınırı aşımında `429` dönüyor.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **Modül iskeleti** — `Messaging` modülü, `MessagingDbContext`, `messaging` şeması, DI + `ModuleDefinition` + ilk migration.
2. **Domain** — `Conversation` + `ConversationParticipant` + `Message` + enum'lar + domain event'ler.
3. **İzinli çift projeksiyonu** — Students/Parents/Matching event'lerini dinleyen read-model (çift doğrulaması).
4. **CQRS feature'ları** — start/list/get/send/read/block/report (Command+Query+Handler+Policy).
5. **API endpoint'leri** + katılımcı sahiplik authorizer'ı (varsayılan reddet).
6. **Bildirim entegrasyonu (M11)** — `MessageSentIntegrationEvent` → push/in-app.
7. **Şikayet entegrasyonu (M18)** — `MessageReportedIntegrationEvent` → `AbuseReport`.
8. **Hız sınırı + spam koruması.**
9. **Mobil feature** — gelen kutusu + sohbet ekranı + başlatma.
10. **Gerçek zamanlı teslimat (sonraki sürüm)** — SignalR/WebSocket; ilk sürümde polling + push.
11. **Premium farkları (M17)** — ek gönderimi, geçmiş derinliği, hız sınırı seviyeleri.

---

## 9. İlişkili Dokümanlar

- Yeni mesaj bildirimi → [`m11_notifications.md`](m11_notifications.md)
- Mesaj şikayeti / moderasyon → [`m18_feedback.md`](m18_feedback.md)
- Sohbet açılan öğrenci ilişkisi → [`m03_students.md`](m03_students.md)
- Veli-öğrenci-öğretmen bağı → [`m09_parents.md`](m09_parents.md)
- Eşleştirmeyle kurulan yeni ilişkiler → [`m12_matching.md`](m12_matching.md)
- Engelleme/gizlilik/bildirim tercihleri → [`m15_settings.md`](m15_settings.md)
- Premium mesajlaşma farkları + reklam → [`m17_membership.md`](m17_membership.md)
- Kullanıcı kimliği/rol → [`m01_identity.md`](m01_identity.md)
- Yetki guard'ı → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli bağlamı → [`veri_modeli.md`](veri_modeli.md)
- Rol perspektifleri → [`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../roles/admin.md`](../roles/admin.md), [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Genel durum & strateji → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Mesajlaşma Modülü (M16) — Detaylı Tasarım | Faz 2-3 | Güncelleme: 2026-08-19*
