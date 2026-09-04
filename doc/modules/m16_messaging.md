---
title: "M16 — Mesajlaşma"
summary: "Mesajlaşma modülü: Conversation/Message domain'i + messaging şeması kodda (P10 Task 1); uçlar ve mobil sırada"
tags: [modul, messaging, faz-2, faz-3]
status: "🟡"
authority: code
updated: 2026-09-04
---

# 💬 Mesajlaşma Modülü (M16) — Detaylı Tasarım Dokümanı

> **PRD: M16** · **Faz 2-3** · **Durum: 🟡 Kısmi — modül iskeleti + domain kodda (P10 Task 1), uçlar/mobil sırada**
>
> **Amaç:** Öğretmen ile öğrenci/veli arasında, ders bağlamına bağlı, güvenli ve denetlenebilir
> uygulama içi mesajlaşma. Bugün bu iletişim telefon/WhatsApp üzerinden, sistem dışında yürüyor
> (bkz. [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)); amaç bu trafiği platforma taşımaktır.

> **KESİN KURAL (PRD — kapsam):** Mesajlaşma **yalnızca** iki çift arasında kurulur:
> **öğretmen ↔ öğrenci** ve **öğretmen ↔ veli**.
> **öğrenci ↔ veli**, **öğrenci ↔ öğrenci** ve **öğretmen ↔ öğretmen** mesajlaşması **YASAKTIR** ve domain seviyesinde reddedilir
> (`Conversation.Create` → `messaging.pair_not_allowed`).
>
> İlgili: yeni mesaj bildirimi → [`m11_notifications.md`](m11_notifications.md) · mesaj şikayeti / moderasyon → [`m18_feedback.md`](m18_feedback.md) · gizlilik/engelleme tercihleri → [`m15_settings.md`](m15_settings.md).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

🟡 **Modül iskeleti ve domain kodda** (P10 Task 1, 2026-09-04):

- **Backend:** `src/Modules/Messaging/{Domain,Application,Infrastructure,API}` — 4 katman projesi, `MessagingDbContext`
  (`messaging` şeması), `MessagingModule : ModuleDefinition` (`/api/messaging`), `MessagingDesignTimeDbContextFactory`,
  `InitialCreate` migration. `EgitimUssu.slnx` + `src/API.Host/ModuleAssemblies.cs` kayıtları yapıldı.
- **Domain:** `Conversation` (AggregateRoot) + `Message` (AggregateRoot) + `ParticipantRole` + `MessagingErrors`
  + `MessageSentDomainEvent` — bkz. §2.
- **Şema:** PostgreSQL `messaging` şeması; tablolar `conversations`, `messages` (+ ortak `inbox_messages`,
  `outbox_messages`, `module_states`).
- **Testler:** `tests/Unit/ConversationTests.cs` (17) + `tests/Unit/MessageTests.cs` (9) — izinli çift, normalizasyon,
  engelleme, önizleme kırpma, okundu idempotansı, yumuşak silme.

**Henüz yok (sırada):** uygulama katmanı uçları (§3), ilişki doğrulaması, hız sınırı → P10 Task 2 ·
yeni mesaj bildirimi → P10 Task 3 · `mobile/lib/features/messaging/` → P10 Task 4.

---

## 2. Domain Modeli (🟢 Kodda)

**Şema:** `messaging` · **DbContext:** `MessagingDbContext` · **Route prefix:** `/api/messaging`
**Aggregate kökleri:** `Conversation` ve `Message` (ikisi de `AggregateRoot<Guid>`).

> **Neden `Message` ayrı bir aggregate?** Sohbet on binlerce mesaj biriktirir; mesajlar `Conversation`'ın
> çocuk koleksiyonu olsaydı her gönderim bütün geçmişi belleğe yüklerdi. Sohbet yalnız **özet** durumu
> (son mesaj önizlemesi/zamanı, engel bayrakları) taşır, mesajlar keyset sayfalamayla ayrı okunur.

> Modül sınırı kuralı: `Messaging`, `Identity`/`Students`/`Parents` tablolarına **doğrudan erişmez**.
> İlişki doğrulaması `Shared/Contracts` üzerinden yapılır: öğretmen↔öğrenci bağı `IStudentDirectory`,
> veli↔öğrenci onaylı bağı `IParentAccessDirectory` (P10 Task 2).

### 2.1 `Conversation` (AggregateRoot) — 🟢 kodda

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Birincil anahtar |
| `TeacherUserId` | Guid | Sohbetin öğretmen tarafı (Identity kullanıcısı) |
| `CounterpartUserId` | Guid | Karşı taraf — öğrenci **veya** veli kullanıcısı (Identity) |
| `CounterpartRole` | enum `ParticipantRole` | `Student` veya `Parent` (öğretmen tarafı zaten sabittir) |
| `StudentId` | Guid | Sohbetin **bağlamı** olan öğrenci profili (veli sohbetinde de dolu: hangi çocuk) |
| `CreatedOnUtc` | DateTime | |
| `LastMessageOnUtc` | DateTime | Gelen kutusu sıralaması; mesaj yokken sohbetin açılış anı |
| `LastMessagePreview` | string? | Son mesajın kısaltılmış önizlemesi (maks. `PreviewMaxLength` = 140) |
| `IsBlockedByTeacher` | bool | Öğretmen engelledi |
| `IsBlockedByCounterpart` | bool | Öğrenci/veli engelledi |
| `BlockedOnUtc` | DateTime? | Son engelleme anı; engel tamamen kalkınca `null` |

> **Normalizasyon:** `Create` sohbeti kim başlatırsa başlatsın öğretmeni her zaman `TeacherUserId`'ye yazar.
> Aksi hâlde `(TeacherUserId, CounterpartUserId, StudentId)` benzersiz indeksi aynı çift için iki satıra izin verirdi.

> **Enum yerine iki bool:** `ConversationStatus` yerine `IsBlockedByTeacher`/`IsBlockedByCounterpart` tutulur.
> Tek bir "Blocked" durumu, engeli **kimin** koyduğunu kaybederdi; o bilgi olmadan `Unblock` doğru tarafa
> bağlanamaz ve öğretmen, öğrencinin koyduğu engeli kaldırabilirdi.

**Davranışlar (metotlar):**

| Metot | Döner | Kural |
|-------|-------|-------|
| `static Create(id, initiatorRole, initiatorUserId, counterpartRole, counterpartUserId, studentId, nowUtc)` | `Result<Conversation>` | Tam olarak **bir** taraf `Teacher` olmalı → değilse `messaging.pair_not_allowed`; aynı kullanıcı iki tarafta olamaz → `messaging.self_conversation`; taraflar normalize edilir |
| `SendMessage(messageId, senderUserId, body, nowUtc)` | `Result<Message>` | Katılımcı değilse `messaging.not_participant`; engelliyse `messaging.blocked`; boş gövde `messaging.empty_body`; 4000 karakteri aşan `messaging.body_too_long`. Gövde `Trim`'lenir, önizleme tazelenir, `MessageSentDomainEvent` yayılır |
| `Block(byUserId, nowUtc)` / `Unblock(byUserId, nowUtc)` | `Result` | Katılımcı değilse `messaging.not_participant`. Engeli **yalnız koyan taraf** kaldırabilir |
| `CanSend(senderUserId)` | `bool` | Engel **çift yönlüdür**: taraflardan biri engellediyse hiçbir taraf gönderemez |
| `HasParticipant(userId)` / `RecipientOf(senderUserId)` | `bool` / `Guid?` | Yetki kontrolleri ve bildirim alıcısı için |

**Domain event'i:** `MessageSentDomainEvent(ConversationId, MessageId, SenderUserId, RecipientUserId, Preview, SentOnUtc)`
— alıcıyı yalnız sohbet bilir, bu yüzden olay `Conversation`'dan yayılır (Notifications bunu tüketir, P10 Task 3).

**DB:** tablo `conversations`; index'ler:
- `(TeacherUserId, CounterpartUserId, StudentId)` → **UNIQUE** (aynı çift + öğrenci bağlamı için tek sohbet; get-or-create yarışını veritabanı da keser).
- `(TeacherUserId, LastMessageOnUtc)`, `(CounterpartUserId, LastMessageOnUtc)` → gelen kutusu listeleme.

### 2.2 `Message` (AggregateRoot) — 🟢 kodda

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `ConversationId` | Guid | Üst sohbet |
| `SenderUserId` | Guid | Gönderen (katılımcı olmalı) |
| `Body` | string | Ham metin (maks. `MaxBodyLength` = 4000; boş olamaz, `Trim`'lenir) |
| `SentOnUtc` | DateTime | |
| `ReadOnUtc` | DateTime? | İlk okumada sabitlenir (idempotent) |
| `IsDeleted` | bool | Yumuşak silme bayrağı |
| `DeletedOnUtc` | DateTime? | |
| `VisibleBody` | string? (türetilmiş) | API'ye giden içerik — silinmişse `null` |

**Davranışlar:** `MarkRead(nowUtc)` (idempotent; silinmiş mesajda etkisiz), `SoftDelete(nowUtc)` (idempotent).

> **Yumuşak silme içeriği silmez:** `Body` satırda kalır, yalnız `VisibleBody` `null` olur. Şikayet/moderasyon
> (M18) silinmiş bir mesajı incelemek zorunda kalabilir; içerik gerçekten silinseydi inceleme imkânsız olurdu.

**DB:** tablo `messages`; index'ler `(ConversationId, SentOnUtc)` → keyset sayfalama, `(ConversationId, ReadOnUtc)` → okunmamış sayacı.

### 2.3 Enum ve hata kodları — 🟢 kodda

| Enum | Değerler |
|------|----------|
| `ParticipantRole` | `Teacher=1`, `Student=2`, `Parent=3` |

| Hata kodu | Anlam |
|-----------|-------|
| `messaging.pair_not_allowed` | Rol çifti öğretmen↔öğrenci/veli değil |
| `messaging.self_conversation` | Kullanıcı kendisiyle sohbet başlatamaz |
| `messaging.not_participant` | İstek sahibi sohbetin tarafı değil |
| `messaging.blocked` | Sohbet engellenmiş, gönderim kapalı |
| `messaging.empty_body` | Mesaj boş |
| `messaging.body_too_long` | Mesaj 4000 karakteri aşıyor |
| `messaging.relationship_required` | Taraflar arasında aktif bağ yok (P10 Task 2'de kullanılır) |
| `messaging.conversation_not_found` / `messaging.message_not_found` | Kayıt yok (P10 Task 2'de kullanılır) |

> ⚠️ Önceki tasarımdaki `ConversationParticipant` entity'si, `ConversationStatus`/`MessageStatus` enum'ları ve
> `AttachmentUrl` alanı **uygulanmadı**: v1 iki katılımcılı sabit bir çifttir (ayrı katılımcı tablosu kazanç
> getirmezdi), durum bilgisi engel bayraklarıyla taşınır ve ek/dosya paylaşımı kapsam dışıdır.

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

*Mesajlaşma Modülü (M16) — Detaylı Tasarım | Faz 2-3 | Güncelleme: 2026-09-04 (P10 Task 1: modül iskeleti + `Conversation`/`Message` domain'i + `messaging` şeması kodda; §1-§2 koda göre yeniden yazıldı, durum 🔴 → 🟡)*
