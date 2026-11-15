---
title: "M16 — Mesajlaşma"
summary: "Mesajlaşma modülü uçtan uca çalışıyor (P10 Task 1-5, gerçek Postgres+Redis'te doğrulandı); açık: karşı tarafın adı, veli sohbet başlatma, şikayet (P12), cihaza push (Q8), gerçek zamanlı (v2)"
tags: [modul, messaging, faz-2, faz-3]
status: "🟡"
authority: code
code_refs:
  - src/Modules/Messaging/Domain/MessagingDomainModel.cs
  - src/Modules/Messaging/Application/MessagingFeatures.cs
  - src/Modules/Messaging/Application/MessagingPolicies.cs
  - src/Modules/Messaging/Infrastructure/MessagingStudentMergedHandler.cs
  - src/Modules/Messaging/API/MessagingModule.cs
updated: 2026-11-15
---

# 💬 Mesajlaşma Modülü (M16) — Detaylı Tasarım Dokümanı

> **PRD: M16** · **Faz 2-3** · **Durum: 🟡 Kısmi (bilerek)** — zincir uçtan uca çalışıyor ve gerçek
> Postgres + Redis'te doğrulandı (P10 Task 1-5). **🟢 değil** çünkü: karşı tarafın **adı** sunucudan gelmiyor,
> **veli sohbet başlatamıyor**, mesaj **şikayeti** (M18) P12'de ve cihaza **push** Q8'e bloke — bkz. §7-§8.
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

🟡 **Zincir uçtan uca çalışıyor** (P10 Task 1-5, 2026-09-04):

- **Backend:** `src/Modules/Messaging/{Domain,Application,Infrastructure,API}` — 4 katman projesi, `MessagingDbContext`
  (`messaging` şeması), `MessagingModule : ModuleDefinition` (`/api/messaging`), `MessagingDesignTimeDbContextFactory`,
  `InitialCreate` migration. `EgitimUssu.slnx` + `src/API.Host/ModuleAssemblies.cs` kayıtları yapıldı.
- **Domain:** `Conversation` (AggregateRoot) + `Message` (AggregateRoot) + `ParticipantRole` + `MessagingErrors`
  + `MessageSentDomainEvent` — bkz. §2.
- **Şema:** PostgreSQL `messaging` şeması; tablolar `conversations`, `messages` (+ ortak `inbox_messages`,
  `outbox_messages`, `module_states`).
- **Uygulama katmanı (P10 Task 2):** `MessagingFeatures.cs` (5 komut + 2 sorgu + handler'lar + `IMessagingRepository`),
  `MessagingPolicies.cs` (`ConversationRelationshipPolicy`, `MessagingRoles`, `AuthenticatedMessagingAuthorizer`),
  `MessagingRepository.cs`, 8 uç (§3) ve kullanıcı başına 30/dk hız sınırı.
- **Bildirim (P10 Task 3):** `Notifications/Infrastructure/MessageSentNotificationHandler.cs` —
  `Messaging/MessageSentDomainEvent` tüketip alıcıya `UserNotification(Kind=MessageReceived,
  Route="/messages/{conversationId}")` yazar; push turunu `NotificationDispatcher` üstlenir.
- **Mobil (P10 Task 4):** `mobile/lib/features/messaging/` — `ConversationsCubit` + `ChatCubit`,
  `MessagingRepositoryImpl`, gelen kutusu ve sohbet ekranları, üç rolde giriş noktaları (§6).
- **Testler:** `ConversationTests` (17) + `MessageTests` (9) + `MessagingAccessTests` (17) + `MessagePagingTests` (10)
  + `MessageSentNotificationHandlerTests` (6) + `DistributedRateLimitMiddlewareTests`'e 5 mesajlaşma testi;
  mobilde `messaging_cubit_test` (15) + `messaging_pages_test` (8).
- **Doğrulama (P10 Task 5):** `tests/Integration/RealMessagingChainTests.cs` — **gerçek Postgres + Redis**,
  3 test / 12 adım; kabul kriterleri ve doğrulamada çıkan hata için bkz. §7.
- **Süregelen ilişki (P15 Task 5 / N-46, K-28):** ilişki artık yalnız açılışta değil **her gönderimde** ve her
  gelen kutusu/açma yanıtında kaynaktan sorulur — bağ biterse gönderim `403 messaging.relationship_ended`, geçmiş
  okunur, yanıtlar `isReadOnly: true`. Messaging öğrenci profili birleşmesini tüketir
  (`MessagingStudentMergedHandler`, #189). Testler `MessagingRelationshipTests` (13) +
  `RealMessagingRelationshipTests` (gerçek Postgres, 4).

---

## 2. Domain Modeli (🟢 Kodda)

**Şema:** `messaging` · **DbContext:** `MessagingDbContext` · **Route prefix:** `/api/messaging`
**Aggregate kökleri:** `Conversation` ve `Message` (ikisi de `AggregateRoot<Guid>`).

> **Neden `Message` ayrı bir aggregate?** Sohbet on binlerce mesaj biriktirir; mesajlar `Conversation`'ın
> çocuk koleksiyonu olsaydı her gönderim bütün geçmişi belleğe yüklerdi. Sohbet yalnız **özet** durumu
> (son mesaj önizlemesi/zamanı, engel bayrakları) taşır, mesajlar keyset sayfalamayla ayrı okunur.

> Modül sınırı kuralı: `Messaging`, `Identity`/`Students`/`Parents` tablolarına **doğrudan erişmez**.
> İlişki doğrulaması `Shared/Contracts` üzerinden yapılır: öğretmen↔öğrenci erişim kapsamı `ITeacherStudentAccess`
> (`== Private` — P15 T5; K-17 gereği sınıf kapsamı mesaj yetkisi vermez), öğrenci profilinin sahibi
> `IStudentDirectory`, veli↔öğrenci onaylı bağı `IParentAccessDirectory` (P10 Task 2).

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
| `HideDeletedMessagePreview(message)` | `void` | Silinen mesajın zaman ve içerikle eşleşen önizlemesini temizler; farklı son mesajın önizlemesi ve sohbet sıralama zamanı korunur |
| `CanSend(senderUserId)` | `bool` | Engel **çift yönlüdür**: taraflardan biri engellediyse hiçbir taraf gönderemez |
| `HasParticipant(userId)` / `RecipientOf(senderUserId)` | `bool` / `Guid?` | Yetki kontrolleri ve bildirim alıcısı için |
| `MoveToStudent(canonicalStudentId)` | `void` | P15 T5 / #189: profil birleşince bağlamı kanonik profile taşır; sohbet kimliği değişmez (bildirim rotaları geçerli kalır) |
| `Absorb(merged, copyHasMessages)` | `void` | P15 T5 / #189: kanonikte aynı üçlü varsa birleşen kopyanın özetini emer — kopyadan mesaj taşındıysa ve daha yeniyse son mesaj/önizleme alınır (mesajsız kopya kanoniğin önizlemesini ezmez, sırasını öne çekmez), engel bayrakları her durumda VEYA'lanır, açılış anı en eskisi; taraflar farklıysa `InvalidOperationException` |

> **İlişki domainde değil:** `SendMessage` ilişkinin yaşadığını bilmez — bu dış durumdur (Students/Parents) ve
> açılıştaki gibi uygulama katmanında, `IConversationRelationshipPolicy` ile denetlenir (§4 kural 12).

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
| `messaging.relationship_required` | Sohbet **açılırken** taraflar arasında aktif bağ yok (P10 Task 2) |
| `messaging.relationship_ended` | Sohbet açıldıktan sonra ilişki bitti — gönderim kapalı, geçmiş okunur (P15 T5 / K-28). Engelden farkı: katılımcılar kaldıramaz; ilişki yeniden kurulursa kendiliğinden açılır |
| `messaging.conversation_not_found` / `messaging.message_not_found` | Kayıt yok (P10 Task 2'de kullanılır) |

> ⚠️ Önceki tasarımdaki `ConversationParticipant` entity'si, `ConversationStatus`/`MessageStatus` enum'ları ve
> `AttachmentUrl` alanı **uygulanmadı**: v1 iki katılımcılı sabit bir çifttir (ayrı katılımcı tablosu kazanç
> getirmezdi), durum bilgisi engel bayraklarıyla taşınır ve ek/dosya paylaşımı kapsam dışıdır.

---

## 3. API Sözleşmesi (🟢 Kodda — `/api/messaging`)

```
GET  /api/messaging/status                                              → modül durumu (anonim)

POST   /api/messaging/conversations                        (auth) → { counterpartUserId, counterpartRole, studentId }
                                                                    → sohbeti aç; varsa mevcudu dön (get-or-create)
GET    /api/messaging/conversations?skip=&take=             (auth) → gelen kutusu: karşı taraf + son önizleme
                                                                    + okunmamış sayısı + engel bayrağı + isReadOnly
GET    /api/messaging/conversations/{id}/messages?beforeUtc=&take=
                                                            (auth) → mesaj geçmişi (keyset, yeniden eskiye)
POST   /api/messaging/conversations/{id}/messages           (auth) → { body } · dakikada 30 mesaj sınırı
POST   /api/messaging/conversations/{id}/read               (auth) → body: { upToMessageId }; sınıra kadar gelen mesajlar (204)
POST   /api/messaging/conversations/{id}/block              (auth) → sohbeti engelle
POST   /api/messaging/conversations/{id}/unblock            (auth) → kendi engelini kaldır
DELETE /api/messaging/messages/{messageId}                  (auth) → yumuşak sil (gönderen veya Admin, 204)
```

**HTTP eşlemesi** (`MessagingModule.MapError`):

| Hata kodu | Durum | Neden |
|-----------|-------|-------|
| `shared.forbidden`, `messaging.not_participant` | 403 | Oturum yok / sohbetin tarafı değilsin |
| `messaging.relationship_ended` | 403 | İlişki bitti (K-28). Kod korunur — `FromError(ctx, 403, …)`; `.Forbidden(...)` kodu `shared.forbidden`'a çevirirdi |
| `messaging.conversation_not_found`, `messaging.message_not_found` | 404 | Kayıt yok |
| `messaging.pair_not_allowed`, `messaging.self_conversation`, `messaging.relationship_required`, `messaging.blocked` | 409 | Kural çakışması |
| `messaging.empty_body`, `messaging.body_too_long`, `messaging.invalid_role` | 400 | Geçersiz istek |

> **`isReadOnly` (P15 T5, yalnız ekleme):** `POST /conversations` (yeni ya da var olan sohbet), `block`/`unblock`
> yanıtı (`ConversationResponse`) ve gelen kutusu satırı (`ConversationSummaryResponse`) taşır. Saklanmaz; istek
> anında ilişki kaynağından hesaplanır. `GET …/messages` dizi biçimi **değişmedi**. Var olan bir sohbeti açmak, ilişki
> bitmiş olsa da `409` değil `200 + isReadOnly: true` döner (kullanıcı kendi geçmişini görebilmeli).

> **Yetki:** Bütün uçlar `RequireAuthorization("AuthenticatedUser")`. Authorizer yalnız "oturum açık mı"
> sorusunu sorar; **kayıt bazlı** yetki (katılımcı mısın, gönderen misin) handler'ın içinde, veriyi
> görerek yapılır — authorizer o veriye sahip değildir.
>
> **Keyset sayfalama:** İmleç son görülen mesajın `sentOnUtc` değeridir; sunucu ondan **eski** mesajları
> yeniden eskiye döner. Offset kullanılsaydı, sayfalar arasında yeni mesaj geldiğinde satırlar kayar ve
> kullanıcı bir mesajı iki kez görürdü. `take` sunucuda kırpılır (varsayılan 30, en fazla 100).

> ⚠️ **Uygulanmayanlar:** `mute` (sohbet susturma) ve mesaj `report` (şikayet) uçları **yok** —
> şikayet M18 (P12) ile gelecek, susturma ise bildirim tercihlerine (M15) devredilmiştir.
> Mesaj **düzenleme** de yoktur: v1'de mesaj gönderildikten sonra değişmez.

---

## 4. İş Kuralları

| # | Kural | Nerede zorlanıyor | Durum |
|---|-------|-------------------|-------|
| 1 | **Çift kuralı (en kritik):** yalnız öğretmen↔öğrenci ve öğretmen↔veli. Tam olarak bir taraf öğretmen olmalı | `Conversation.Create` → `messaging.pair_not_allowed` | 🟢 |
| 2 | **İlişki önkoşulu:** öğretmenin o öğrenciyle yaşayan **özel ders** bağı olmalı (`GetScopeAsync == Private`; sınıf kapsamı yetmez — K-17); karşı taraf öğrenciyse o profilin **sahibi**, veliyse **onaylı** veli olmalı | `ConversationRelationshipPolicy` (`ITeacherStudentAccess` + `IStudentDirectory` + `IParentAccessDirectory`) → `messaging.relationship_required` | 🟢 |
| 3 | **Tekillik:** aynı `(TeacherUserId, CounterpartUserId, StudentId)` için tek sohbet; "aç" çağrısı varsa mevcudu döner | `OpenConversationCommandHandler` (get-or-create) + UNIQUE indeks | 🟢 |
| 4 | **Gönderim:** yalnız katılımcı gönderebilir | `Conversation.SendMessage` → `messaging.not_participant` | 🟢 |
| 5 | **Engelleme:** taraflardan biri engellerse gönderim **iki taraf için de** kapanır; engeli yalnız koyan taraf kaldırır | `Conversation.CanSend` / `Block` / `Unblock` → `messaging.blocked` | 🟢 |
| 6 | **Okundu bilgisi:** mesaj başına `ReadOnUtc`; ilk okumada sabitlenir, sonraki çağrılar değiştirmez | `Message.MarkRead` + `MarkConversationReadCommandHandler` | 🟢 |
| 7 | **Silme:** yalnız **gönderen** ya da Admin siler; silme **yumuşaktır** (satır ve `Body` kalır, API `null` döner); silinen mesajın gelen kutusu önizlemesi de temizlenir | `DeleteMessageCommandHandler` + `Message.SoftDelete` | 🟢 |
| 8 | **Hız sınırı:** kullanıcı başına dakikada **30** mesaj; aşımda `429` | `DistributedRateLimitMiddleware` `messaging` politikası (`RateLimiting:Messaging`) | 🟢 |
| 9 | **Moderasyon:** mesaj şikayeti → M18 `AbuseReport` | — | 🔴 P12 |
| 10 | **Bildirim:** yeni mesajda alıcıya in-app + push (tercihe saygıyla) | `MessageSentNotificationHandler` + `NotificationDispatcher` tercih kapısı | 🟢 |
| 11 | **Premium farkı:** ek/dosya gönderimi, geçmiş derinliği | — | 🔴 kapsam dışı (v1'de ek yok) |
| 12 | **Süregelen ilişki (K-28, N-46):** kural 2'nin yüklemi **her gönderimde** yeniden sorulur. İlişki biterse (bağ arşivlendi/koptu, veli bağı iptal/ret, öğrencinin hesabı profilden ayrıldı) gönderim **iki taraf için de** kapanır; öğretmenin bağı biterse veli sohbeti de kapanır. Yalnız gönderim kapanır: geçmiş listeleme, okundu, kendi mesajını silme, engelle/kaldır ve var olan sohbeti açma açık kalır. İlişki yeniden kurulursa gönderim kendiliğinden açılır (bilgi saklanmaz, #191). Sıra: katılımcı mı → ilişki → domain (engel, gövde) — yabancı ilişkinin durumunu öğrenmez | `SendMessageCommandHandler` + `IConversationRelationshipPolicy.IsActiveAsync` → `messaging.relationship_ended` (403); yanıtlarda `isReadOnly` | 🟢 |
| 13 | **Profil birleşmesi (#189):** öğrencinin manuel profili kanonik profile birleşince sohbetler de taşınır; kanonikte aynı üçlü varsa mesajlar kanonik sohbete katılır, kopya silinir (kanonik kazanır, engel kaybolmaz) | `MessagingStudentMergedHandler` (`StudentProfilesMerged` tüketicisi, idempotent) | 🟢 |

> **Gelen kutusu maliyeti:** her satırın ilişkisi canlı sorulur; aynı (öğretmen, öğrenci), öğrenci sahipliği ve
> (veli, öğrenci) sorusu sayfa içinde bir kez sorulur (`GetActiveStatesAsync`), sorgular **sırayla** gider (scoped
> DbContext). En kötü durumda sayfa başına (en fazla 100 satır) satır sayısı kadar kapsam sorgusu — toplu bir
> sözleşme metodu (`GetScopesAsync(teacher, studentIds)`) gerekirse ölçümle eklenir.

> **Hız sınırı neden kullanıcı başına?** Diğer politikalar istemci IP'sine göre sayar; mesaj gönderimi
> kullanıcı kimliğine göre sayılır. IP'ye göre sayılsaydı aynı ev veya okul ağındaki iki öğrenci
> birbirinin mesaj kotasını tüketirdi. Oturum kimliği yoksa (olmaması gerekir) kota IP'ye düşer.

---

## 5. Olay Akışı

```
[Sohbet açma] — 🟢 kodda
İstemci → POST /conversations { counterpartUserId, counterpartRole, studentId }
   → Conversation.Create() izinli çift kuralını doğrular + tarafları normalize eder
      → varsa mevcut satır döner (get-or-create; UNIQUE indeks yarışı da keser)
      → yoksa ConversationRelationshipPolicy ilişkiyi sorar (IStudentDirectory / IParentAccessDirectory)
         → izin varsa yeni Conversation

[Mesaj gönderme] — 🟢 kodda
İstemci → POST /conversations/{id}/messages { body }        (kullanıcı başına 30/dk)
   → katılımcı değilse 403 messaging.not_participant
   → ConversationRelationshipPolicy ilişkiyi kaynaktan yeniden sorar (P15 T5 / K-28)
      → bitmişse 403 messaging.relationship_ended (geçmiş okunur, isReadOnly: true)
   → Conversation.SendMessage() → MessageSentDomainEvent(ConversationId, MessageId,
                                     SenderUserId, RecipientUserId, Preview, SentOnUtc)
      → (ortak Outbox; JsonDomainEventMapper) SourceModule="Messaging", Name="MessageSentDomainEvent"
         → Notifications: MessageSentNotificationHandler (IdempotentIntegrationEventHandler)
            → UserNotification(Kind=MessageReceived, Route="/messages/{conversationId}")
               → NotificationDispatcher turu push'u gönderir; tercih kapısı ORADA uygulanır
                  (push kapalıysa in-app kayıt yine de durur)

[Okundu] — 🟢 kodda
Alıcı sohbeti açar → POST /conversations/{id}/read
   → gelen mesajların Message.ReadOnUtc'si ilk okumada damgalanır (idempotent) → 204

[Profil birleşmesi] — 🟢 kodda (P15 T5 / #189)
Students: StudentProfilesMergedDomainEvent(From, To)   (Ö-C claim/merge; öğretmen bağı aynı transaction'da kanoniğe)
   → (Outbox) Messaging: MessagingStudentMergedHandler (IdempotentIntegrationEventHandler)
      → StudentId = From olan her sohbet için kanonikte aynı (öğretmen, karşı taraf) sohbeti var mı?
         → yoksa Conversation.MoveToStudent(To) — kimlik aynı, bildirim rotaları geçerli
         → varsa mesajlar kanonik sohbete taşınır, Conversation.Absorb(kopya), kopya silinir
   ⚠️ Olay işlenene kadar (outbox turu) birleşen profildeki sohbet salt-okunur görünür; öğretmen bağı zaten
      kanonikte, veli bağı Parents tüketicisiyle taşınır.

[Şikayet] — 🔴 P12 (M18)
Uç yok; mesaj şikayeti M18 AbuseReport ile gelecek.
```

> **Neden ayrı bir `MessageSentIntegrationEvent` sınıfı yok?** Ortak `JsonDomainEventMapper` her domain
> olayını olduğu gibi seri hâle getirip `(SourceModule, Name)` çiftiyle outbox'a yazar; diğer modüllerin
> tüketicileri de (ör. `TopicGoalAchievedNotificationHandler`) doğrudan domain olayının adını dinler.
> Ayrı bir sözleşme sınıfı, aynı alanların ikinci bir kopyası olurdu.

> **İlişki için projeksiyon beslemesi de yok.** Önceki tasarım Students/Parents olaylarından beslenen
> yerel bir "izinli çift" projeksiyonu öngörüyordu; bunun yerine ilişki her sohbet açılışında, **her gönderimde
> ve her gelen kutusu/açma yanıtında** (P15 T5) `Shared/Contracts` dizinlerine **canlı** sorulur. Yerel kopya, bağ koptuktan sonra mesajlaşmayı
> açık bırakma riskini taşırdı — ilişki bilgisinin sahibi Students ve Parents'tır.

---

## 6. Mobil Ekranlar (🟢 Kodda — P10 Task 4)

`mobile/lib/features/messaging/` (`flutter_bloc`/Cubit, `go_router`, `dio`):

| Ekran | Rota | State | md |
|-------|------|-------|-----|
| Gelen kutusu | `/messages` | `ConversationsCubit` | [`../pages/messaging_inbox.md`](../pages/messaging_inbox.md) |
| Sohbet | `/messages/:conversationId` | `ChatCubit` | [`../pages/messaging_chat.md`](../pages/messaging_chat.md) |

**Giriş noktaları:** öğretmen `/more` → "Mesajlar" ve öğrenci detayında "Mesaj Gonder" ·
öğrenci `/student/profile` → "Mesajlar" ve `/student/teacher` kartında "Mesaj gönder" ·
veli ana sayfa başlığındaki mesaj ikonu.

**Push'tan derin bağlantı ek kod istemez:** bildirim `Route = "/messages/{conversationId}"` taşır,
kökteki push dinleyicisi `data.route`'u olduğu gibi `push` eder (P03 Task 5).

> **Sohbet balonları paket değil, token.** Planın öngördüğü `flutter_chat_ui` **kullanılmadı ve
> pubspec'ten kaldırıldı** (D-18): paket kendi teması, kendi girdi alanı ve kendi model tipleriyle
> geliyor, `AppColors`/`AppShadows` token'larına oturmuyordu. Balonlar
> `presentation/widgets/messaging_widgets.dart` içinde yazıldı — bkz.
> [`../architecture/widgets.md`](../architecture/widgets.md) §2c.

**Uygulanmayan ekranlar:**

| Planlanmıştı | Neden yok |
|--------------|-----------|
| `new-conversation` (öğrenci/veli seçme ekranı) | Sohbet, zaten bağlam taşıyan ekranlardan ("Mesaj gönder" düğmeleri) açılır; ayrı bir seçici, aynı listeyi ikinci kez kurmak olurdu |
| `conversation-info` | v1 sohbeti sabit iki taraflıdır; gösterilecek katılımcı listesi yok. Engelleme sohbetin kendi menüsünde |
| "sustur" ve "şikayet et" | Uçları yok — susturma bildirim tercihlerine (M15) devredildi, şikayet M18 (P12) |
| Gelen kutusunda reklam yerleşimi | `AdPlacement` hâlâ yok (M17-2 / P09 Task 5, Q3'e bloke) |

> ⚠️ **Karşı tarafın adı gösterilemiyor:** `GET /conversations` yalnız `counterpartUserId` döner.
> Uydurma ad yazmak yerine rol etiketi ("Öğrenci"/"Veli"/"Öğretmen") gösterilir; ad alanı sunucuya
> eklenene kadar bu böyle kalır.

---

## 7. Kabul Kriterleri (P10 Task 5'te doğrulandı)

**Doğrulama:** `tests/Integration/RealMessagingChainTests.cs` — **gerçek Postgres + Redis** (Testcontainers),
3 test / 12 adım. Doğrulanan zincir: öğretmen sohbeti açar → yazar → outbox → Notifications tüketicisi →
öğrencinin bildirimi → öğrenci gelen kutusunda okunmamış görür → okur → **öğretmen tarafında "okundu" görünür**.

| # | Kriter | Durum | Kanıt |
|---|--------|-------|-------|
| 1 | Yalnız öğretmen↔öğrenci ve öğretmen↔veli sohbeti açılabiliyor | ✅ | `ConversationTests` (yasak çiftlerin dördü de reddediliyor) |
| 2 | İlişkisi olmayan taraflar sohbet başlatamıyor | ✅ | `Unrelated_Teacher_Cannot_Open_Conversation` (gerçek Postgres, 409) |
| 3 | Aynı çift + öğrenci bağlamı için tek sohbet (get-or-create) | ✅ | Zincir testi adım 2 + UNIQUE indeks |
| 4 | Keyset sayfalama tekrar/atlama üretmiyor | ✅ | `MessagePagingTests` + mobil `messaging_cubit_test` |
| 5 | Okunmamış sayacı ve son mesaj önizlemesi gelen kutusunda doğru | ✅ | Zincir testi adım 6 ve 9 |
| 6 | Engelleme gönderimi durduruyor, kaldırma geri açıyor | ✅ | Zincir testi adım 10-11 (karşı tarafın engelini öğretmen **kaldıramıyor**) |
| 7 | Yeni mesajda alıcıya bildirim gidiyor (tercihe saygıyla) | 🟡 **kısmi** | In-app `UserNotification` gerçek Postgres'te doğrulandı (adım 4-5, idempotency dâhil). **Cihaza push doğrulanamadı** — Q8/Firebase hesabı yok; bu M11'in genel borcu, M16'ya özgü değil |
| 8 | Yumuşak silmede içerik gizleniyor, satır kalıyor | ✅ | Zincir testi adım 12 (`isDeleted: true`, `body: null`) |
| 9 | Dakikada 30 mesaj sınırı aşılınca 429 | ✅ | `Thirty_First_Message_In_A_Minute_Should_Be_Rate_Limited` — gerçek Redis; aynı IP'deki ikinci kullanıcı **etkilenmiyor** (kota kullanıcı başına) |
| 10 | Tam test paketi (Docker'lı) yeşil | ✅ | 4 mimari / 665 birim / 44 integration (atlanan 0) / 193 mobil |
| 11 | İlişki bitince gönderim kapanıyor, geçmiş okunuyor, ilişki dönünce açılıyor (P15 T5 / K-28) | ✅ | `RealMessagingRelationshipTests` (gerçek Postgres): arşiv → iki taraf 403 `relationship_ended` → geçmiş 200 + `isReadOnly: true` → arşivden çıkar → 200; veli iptali outbox boşaltılmadan 403 |
| 12 | Birleşen profildeki sohbet kanonik profilde yazılabilir kalıyor (#189) | ✅ | aynı dosya: taşıma + çakışma yolu (mesajlar kanoniğe, engel korunur); tüketici kaydı kapatılınca iki test kırmızı (mutasyon) |

> **Doğrulama bir hata ortaya çıkardı.** Kullanıcı başına kota, `UseAuthentication()` rate-limit
> middleware'inden **sonra** çalıştığı için `HttpContext.User` boştu ve partition sessizce IP'ye
> düşüyordu — yani "aynı ev/okul ağındaki kullanıcılar birbirinin kotasını tüketmesin" kuralı
> fiilen çalışmıyordu. Sıra düzeltildi (`Program.cs`) ve kural gerçek Redis'e karşı regresyona bağlandı.
> Bu tam olarak P06 Task 10'un dersidir: birim testi bu hatayı göremezdi.

---

## 8. Eksikler ve Yapılacaklar

**P10'da kapananlar:** modül iskeleti ✅ · domain ✅ · CQRS feature'ları ✅ · uçlar + katılımcı yetkisi ✅ ·
bildirim entegrasyonu (in-app) ✅ · hız sınırı ✅ · mobil gelen kutusu + sohbet ✅.

**Açık kalanlar (öncelik sırasıyla):**

| # | Eksik | Neden açık | Nerede |
|---|-------|-----------|--------|
| 1 | **Karşı tarafın adı** (`GET /conversations` yalnız `counterpartUserId` döner) | Ad, Identity/Teachers/Students'ın gerçeğidir; sözleşme genişletilmeli. Şimdilik rol etiketi gösteriliyor — **uydurma ad yazılmıyor** | M16 sonraki tur |
| 2 | **Veli sohbeti başlatamıyor** | Veli paneli sözleşmesi (`ParentChildDashboard`) öğretmenin kullanıcı kimliğini taşımıyor; veli okur ve yanıtlar, öğretmen başlatır | M09 sözleşmesine `teacherUserId` |
| 3 | **Şikayet entegrasyonu (M18)** — mesaj → `AbuseReport` | M18 uçları yok | **P12** |
| 4 | **Cihaza push doğrulaması** | Q8 (Firebase projesi) cevaplanmadı — M11'in genel borcu | Q8 |
| 5 | **Mesaj silme düğmesi (mobil)** | Uç var (`DELETE /messages/{id}`), UI tetikleyicisi yok | M16 sonraki tur |
| 6 | **Gerçek zamanlı teslimat** | Karar **K-07**: v1 REST + push; ölçek yokken WebSocket operasyon maliyeti taşınmıyor | v2 |
| 7 | **Premium farkları (M17)** — ek gönderimi, geçmiş derinliği, kota seviyeleri | Ek/dosya v1 kapsamında değil; kota şu an herkes için 30/dk | M17-2 sonrası |
| 8 | **İzinli çift projeksiyonu** | **Bilerek yapılmadı**: ilişki `Shared/Contracts` üzerinden canlı sorulur. Yerel kopya, bağ koptuktan sonra mesajlaşmayı açık bırakırdı | — (karar) |
| 9 | **Sohbet susturma** | Bildirim tercihlerine (M15) devredildi | — (karar) |
| 10 | **Mobilde salt-okunur sohbet** | Sunucu `isReadOnly` ve `403 messaging.relationship_ended` dönüyor; mobil henüz okumuyor — `ChatCubit.canSend` yalnız `isBlocked`'a bakar, gönderilen mesaj "gönderilemedi" olur (403 oturumu kapatmaz) | **P21** |

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

*Mesajlaşma Modülü (M16) — Detaylı Tasarım | Faz 2-3 | Güncelleme: 2026-11-15 (P19 T2: açık outbox tüketim sonuçları) · 2026-11-14 (**P15 Task 5 / N-46, K-28**: ilişki her gönderimde kaynaktan yeniden sorulur — bitince `403 messaging.relationship_ended`, geçmiş okunur, yanıtlarda `isReadOnly`; kapı `ITeacherStudentAccess == Private`'a geçti; Messaging `StudentProfilesMerged`'i tüketir (#189 — sohbet kanoniğe taşınır, çakışmada mesajlar kanoniğe katılır); §1–§5, §7 #11–12, §8 #10) · 2026-10-16 (denetim: silinen mesaj metninin gelen kutusu önizlemesinde kalması düzeltildi; gönderen/Admin silme, eski mesaj silme ve yetkisiz erişim `MessageDeletionPreviewTests` ile kapsandı) · 2026-09-04 (P10 Task 5 — **KAPANIŞ**: zincir gerçek Postgres + Redis'te uçtan uca doğrulandı (`RealMessagingChainTests`, 3 test / 12 adım); doğrulama **bir hata çıkardı** — kullanıcı başına mesaj kotası `UseAuthentication` sırası yüzünden sessizce IP'ye düşüyordu, düzeltildi. §7 kabul kriterleri ve §8 eksikler gerçeğe göre yeniden yazıldı. **M16 bilerek 🟡** — karşı tarafın adı sunucudan gelmiyor, veli sohbet başlatamıyor, şikayet (M18) P12'de, cihaza push Q8'e bloke. Önceki not — P10 Task 4: mobil gelen kutusu + sohbet ekranı, üç rolde giriş noktaları, keyset sayfalama + iyimser gönderim; §6 koda göre yeniden yazıldı, `flutter_chat_ui` kullanılmadı ve pubspec'ten kaldırıldı (D-18). Önceki not — P10 Task 3: yeni mesajda alıcıya in-app bildirim — `MessageSentNotificationHandler`; §5 olay akışı koda göre yeniden yazıldı, ayrı integration-event sınıfı ve ilişki projeksiyonu **bilerek yazılmadı**. Önceki not — P10 Task 2: 8 uç + ilişki kapısı + katılımcı yetkisi + keyset sayfalama + kullanıcı başına 30/dk hız sınırı; §3-§4 koda göre yeniden yazıldı. Önceki not — P10 Task 1: modül iskeleti + `Conversation`/`Message` domain'i + `messaging` şeması kodda; §1-§2 koda göre yeniden yazıldı, durum 🔴 → 🟡)*

**Outbox teslimi (P19 T2 / N-42):** Profil birleşme tüketicisinde null yük açık poison hatasıdır. MessageSent bildirim tüketicisinde bozuk yük artık sessiz başarılı sayılmaz; dead-letter kaydı Notifications handler adını taşır. Ortak teslim, retry ve hata yalıtımı sözleşmesi: [Backend §7](../architecture/backend.md#7-outbox--event-akışı).

*Güncelleme: 2026-11-15 — P19 Task 2: açık olay tüketim sonuçları ve inbox güvenliği.*


### P19 Task 6 — Görülen mesaj sınırı ve bildirim gizliliği (N-52, N-55)

`MarkConversationReadCommand(ConversationId, UpToMessageId?)`, katılımcı kontrolünden sonra sınır mesajının aynı sohbette olduğunu doğrular. Yok/başka sohbet sınırı 404; yabancı katılımcı 403. Sınır verilmemiş eski gövdesiz çağrı 204 ve güvenli etkisiz sonuçtur; yeni mesajları topluca okumaz. Repository SQL yalnız okuyucunun gelen/silinmemiş/okunmamış mesajlarından sınırın kendisini ve zaman bakımından kesin önce olanları alır. Aynı zaman damgalı başka mesaj muhafazakâr biçimde okunmamış kalır. İlk okuma tarihi tekrar istekte korunur. İlişkisi bitmiş katılımcının geçmiş okuma hakkı değişmez.

ChatCubit yüklediği en yeni sunucu mesajının kimliğini gönderir; boş veya yalnız taslak/başarısız mesajlı listede istek üretmez. İstek ile görünüm arasındaki yeni mesaj okunmamış kalır. Mesaj bildiriminde artık önizleme yoktur: yeni ve migration ile temizlenmiş eski bildirimler “Bir kullanıcı size mesaj gönderdi.” taşır. Gerçek HTTP senaryosu öğretmen/öğrenci/veli yabancıları 403 ve görülmeyen yeni mesajın unread kalmasını doğrular.

## P20 Task 4 — Ön plan tazeleme

K-07 REST+poll uygulandı: görünür, resumed sohbet10sn aralıkla; ilgili ön plan push’ında anında tazelenir. Yeni aralık bilinen mesaja kadar keyset sayfalarıyla kapanır; yüklü geçmiş, taslaklar ve benzersiz sunucu kimlikleri korunur. Gelen kutusu push ile yenilenir. Okundu makbuzu görüntülenen en yeni kalıcı mesajla sınırlıdır. Rota kapanışı/arka plan/oturum değişimi poll ve geç yanıtları sınırlar. SignalR, mesaj silme UI’sı, ad çözümleme ve ilişki durumu UI borçları bu görev kapsamında değildir.

Güncelleme: 2026-11-15


### P20 Task 5 — Yaşam döngüsü ve gönderim süresi

ChatCubit ve ConversationsCubit tüm state yayınlarında SafeEmit kullanır. T4 oturum kuşakları, görünürlük kapısı, 10 saniyelik poll iptali ve push abonelik temizliği korunur; SafeEmit ağ isteğini iptal etmez.

Güncelleme: 2026-11-15
