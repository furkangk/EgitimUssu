---
title: "M11 — Bildirim (Notifications)"
summary: "Cihaz token + FCM push teslimatı + rolden bağımsız in-app bildirim merkezi (rozet/okundu) kodda var"
tags: [modul, notifications, bildirim, hatirlatma, faz-3]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Notifications/**
updated: 2026-11-15
---

# 🔔 Bildirim Modülü (M11) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M11 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Notifications`) + Flutter mobil
> **PRD:** M11 · **Faz:** 3 (push altyapısı Faz 0.7) · **Durum:** 🟢 Çalışır — cihaz token kaydı + FCM push teslimatı + in-app bildirim merkezi (liste/rozet/okundu). Kalan: kendi-ders alıcı çözümlemesi (P07). **Tercih kapısı P05/M11-5 ile kapandı.**
> **Mimari:** CQRS + Outbox + Integration Event (modüller arası), PostgreSQL (`notifications` şeması), Redis (gelecekte fan-out/cache)
> **Marka rengi (mobil):** `0xFF082B4F`

> Bu modül kendi başına az kullanıcı akışı içerir; büyük ölçüde **diğer modüllerin integration event'lerini dinleyerek** hatırlatma/bildirim üretir. Bir tür "olay tüketici + zamanlayıcı" modülüdür.
>
> ⚠️ **Kritik bağımlılık:** [`mimari_inceleme.md`](mimari_inceleme.md) **K1** — Outbox dispatch varsayılan **kapalı**. Açılmadıkça Scheduling event'leri yayımlanmaz ve **hiçbir hatırlatma kaydı oluşmaz**.

---

### İptal/silme temizliği (P17 T5)

`LessonScheduleNotificationIntegrationEventHandler` yeni `LessonCancellationAppliedDomainEvent` ve `LessonScheduleDeletedDomainEvent` olaylarını da tüketir. Pending hatırlatma Single’da görünen başlangıç, ThisAndFuture’da cutoff, All/silmede bütün plan üzerinden iptal edilir; eski `LessonScheduleCancelledDomainEvent` tüketimi korunur. Ücret/oturum temizliğiyle aynı outbox zincirindedir. Kalıcı orijinal oluşum anahtarı, kayan pencere planlayıcısı, erteleme ve çoklu alıcılar **P17 T6** kapsamındadır; bu görev yalnız mevcut hatırlatma modelinin temizliğini ekler.


## 1. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Var olan (kodda mevcut)

| Bileşen | Konum | Açıklama |
|---------|-------|----------|
| Domain aggregate | `src/Modules/Notifications/Domain/NotificationsDomainModel.cs` | `LessonReminder : AggregateRoot<Guid>` |
| DbContext | `src/Modules/Notifications/Infrastructure/NotificationsDbContext.cs` | `DbSet<LessonReminder>`, şema `notifications`, tablo `lesson_reminders` |
| Migration | `src/Modules/Notifications/Infrastructure/Migrations/*_InitialCreate.cs` + `NotificationsDesignTimeDbContextFactory` | **K4 (2026-07-01) eklendi.** Önceden migration yoktu → prod'da ilk sorguda `relation "notifications.lesson_reminders" does not exist` ile çöküyordu. Şimdi `lesson_reminders` + `module_states` + `outbox_messages` tablolarını üretir. |
| Repository | `src/Modules/Notifications/Infrastructure/LessonReminderRepository.cs` (arayüz: `Application/NotificationFeatures.cs`) | `ILessonReminderRepository` |
| Query (CQRS) | `src/Modules/Notifications/Application/NotificationFeatures.cs` | `ListTeacherLessonRemindersQuery` + handler + `LessonReminderResponse` |
| Validator | `src/Modules/Notifications/Application/NotificationPolicies.cs` | `ListTeacherLessonRemindersQueryValidator` (`TeacherUserId != Guid.Empty`) |
| Authorizer | `src/Modules/Notifications/Application/NotificationPolicies.cs` | `LessonReminderQueryAuthorizer` (Admin **veya** sahibi öğretmen) |
| API endpoint | `src/Modules/Notifications/API/NotificationsModule.cs` | `GET /api/notifications/teachers/{teacherUserId}/lesson-reminders?activeOnly=` |
| Zamanlayıcı | `src/Modules/Notifications/Infrastructure/NotificationDispatching.cs` | `NotificationDispatcher` (BackgroundService, **30 sn** poll) → `NotificationDispatchProcessor.DispatchDueRemindersAsync()` |
| Integration handler | `src/Modules/Notifications/Infrastructure/LessonScheduleNotificationIntegrationEventHandler.cs` | Scheduling **ders** event'lerini dinler |
| Integration handler | `src/Modules/Notifications/Infrastructure/StudyScheduleReminderIntegrationEventHandler.cs` | Scheduling **öğrenci kişisel program** event'lerini dinler (2026-07-08) |

**Çalışma şekli (doğrulanmış):**
- API grubu `RequireAuthorization("AuthenticatedUser")` ile korunur. Tek endpoint öğretmen hatırlatma listesini döner; `activeOnly=true` ise yalnızca `Pending` durumdakiler filtrelenir, `RemindAtUtc` artan + `CreatedOnUtc` sırasıyla döner.
- `LessonScheduleNotificationIntegrationEventHandler.CanHandle` yalnızca `SourceModule == "Scheduling"` ve `Name ∈ {LessonScheduledDomainEvent, LessonScheduleCancelledDomainEvent}` olduğunda çalışır.
  - **LessonScheduled** → ilgili `LessonScheduleId` için kayıt **yoksa** yeni `LessonReminder` oluşturur. Başlık `"Yaklasan ders hatirlatmasi"`, mesaj `"Ders başlangıcı: {13 Kasım Cuma 10:00}."` (P16 T5: `NotificationTimeText.LessonReminderBody` — **yerel** saat, `ILocalCalendar.FormatForUser`; eskiden ISO UTC damgası `{StartAtUtc:O}`), `Channel = InApp`, `Status = Pending`, `RemindAtUtc = StartAtUtc.AddMinutes(-max(ReminderOffsetMinutes, 0))` — offset **event payload'ından** gelir (2026-07-01, Y1; önceden sabit 60 dk idi). Öğretmen offset'i artık mobil `LessonFormSheet` içinde seçer (Kapalı/15/30dk/1sa/1gün, 2026-07-08). Var olan kayıt kontrolü ile **idempotent**.
  - **LessonScheduleCancelled** → `LessonScheduleId` ile bulunan kayıtta `Cancel()` çağrılır.
- `StudyScheduleReminderIntegrationEventHandler.CanHandle` (2026-07-08): `SourceModule == "Scheduling"` ve `Name ∈ {StudyScheduleEntryScheduledDomainEvent, StudyScheduleEntryRescheduledDomainEvent, StudyScheduleEntryCancelledDomainEvent}`. Öğrencinin kendi program girdisi için hatırlatma yönetir; kayıt aynı `LessonReminder` aggregate'ında tutulur — girdinin kimliği `LessonScheduleId` alanına (tekil), `StudentId` öğrenciye, `TeacherUserId = Guid.Empty` (öğretmen yok).
  - **Scheduled** → `ReminderOffsetMinutes > 0` ise (yoksa kayıt oluşturulmaz, `0` = kapalı) idempotent `LessonReminder` (başlık `"Calisma hatirlatmasi"`, `RemindAtUtc = StartAtUtc − offset`).
  - **Rescheduled** → mevcut kayıt yeni saate taşınır (`Reschedule`, Pending'e alınır); offset `0` olduysa iptal edilir; kayıt yoksa oluşturulur.
  - **Cancelled** → mevcut kayıtta `Cancel()`.
  - Tekrarlı girdide hatırlatma **ilk oluşuma** göre (öğretmen dersleriyle aynı MVP davranışı). Notifications, Scheduling'e proje referansı vermez — olay adı + JSON payload üzerinden çalışır.
- `NotificationDispatcher` her 30 sn'de bir kendi scope'unu açar, `ListDuePendingAsync(utcNow)` (yani `RemindAtUtc <= now && Status == Pending`) sonuçlarının her biri için `reminder.MarkSent(utcNow)` çağırır ve değişiklikleri kaydeder.

### 🔴 Eksik olan

- ✅ **Gerçek push teslimatı eklendi (2026-09-03, P03 Task 3 — Y5 kapandı).** `DispatchDueRemindersAsync` artık alıcının aktif `DeviceToken`'larına `IPushSender` ile gönderir; en az biri teslim edilirse `MarkSent`, hiçbiri edilemezse `MarkFailed` (durum `Pending` kalır → tekrar denenir, 5 denemeden sonra `Failed`). FCM `UNREGISTERED`/`INVALID_ARGUMENT` yanıtında token `Deactivate` edilir. **Kalan:** kendi-ders (Ç-06) hatırlatmalarında `TeacherUserId` boş olduğu için öğrenciye teslim yok — `StudentId → UserId` eşlemesi P07 read-model'e bağlı.
- ✅ **Cihaz token kaydı eklendi** (2026-09-03, P03/M11-2) — `DeviceToken` aggregate + `POST /device-tokens` / `DELETE /device-tokens/{token}`. Gerçek gönderim hâlâ yok (P03 Task 2-3).
- `NotificationChannel.Push` enum değeri tanımlı ama tüm hatırlatmalar `InApp` olarak üretiliyor.
- ✅ **`ReminderOffsetMinutes` artık kullanılıyor** (2026-07-01, Y1) — offset `LessonScheduledDomainEvent` payload'ında taşınıp handler'da uygulanıyor; hatırlatma ders bazında erken/geç kurulabiliyor ([`m04_scheduling.md`](m04_scheduling.md)).
- **Ders dışı bildirim türleri yok:** ödev son teslim yaklaşıyor/kaçırıldı, ödeme gecikmesi, yeni mesaj, günlük çalışma, haftalık özet (PRD M11 tablosu).
- **Öğrenci/veli için in-app bildirim listesi endpoint'i yok** — yalnızca öğretmen-merkezli ders hatırlatması listelenebiliyor. Okundu/okunmamış kavramı domain'de yok.
- ✅ **(P05/M11-5) Settings tercihleri artık gönderim turunda uygulanıyor** — `IUserNotificationPreferences` (Shared.Contracts) üzerinden okunur; ayrıntı için §5.1.
- ⚠️ **Outbox kapalı (K1):** integration event'ler yayımlanmadığından, gerçekte ders planlansa bile hatırlatma kaydı **hiç oluşmaz**. Bu, modülün uçtan uca çalışmasının ön koşuludur.

---

## 1.5 🟢 Veli Bildirim Motoru (Veli V-E, 2026-07-19 — uygulandı)

Veliye bildirim üretir; **yalnız Premium veliye** (PRD 9.3) ve velinin tercih anahtarlarına saygılı.

- **`ParentNotification : AggregateRoot<Guid>`** (`Id, ParentUserId, StudentId, Type, Title, Message, CreatedOnUtc`); tablo `parent_notifications`
  (index `{ParentUserId, CreatedOnUtc}`). `enum ParentNotificationType { WeeklySummary, NewAssignment, LessonCompleted, PaymentUpdate, LinkConnected, PaymentDeclared }`.
- **`ProcessedIntegrationEvent`** dedup tablosu (`processed_integration_events`) — Parents deseni birebir (çift-üretim engeli).
- **`ParentEventNotificationHandler : IIntegrationEventHandler`** — dinlediği olaylar ve tercih/tip eşlemesi:
  | Olay (SourceModule/Name) | Tip | Tercih kapısı |
  |---|---|---|
  | Assignments/`AssignmentCreatedDomainEvent` | `NewAssignment` | `NotifyMissedAssignment` |
  | LessonSessions/`LessonSessionCompletedDomainEvent` | `LessonCompleted` | `NotifyLessonReminders` |
  | Payments/`PaymentRecordUpdatedDomainEvent` | `PaymentUpdate` | `NotifyPayments` |
  | Parents/`ParentLinkConnectionNoticeDomainEvent` (V-C) | `LinkConnected` | **koşulsuz** (güvenlik bildirimi) |

  Her hedef için **`Tier == Premium`** kapısı + (varsa) tercih kapısı uygulanır; `IParentNotificationDirectory.GetApprovedParentsForStudentAsync`
  ile öğrencinin onaylı velileri + tier + tercih okunur. `EventId` ile idempotent. V-G'nin `ParentPaymentDeclaredDomainEvent`'i öğretmen hedefli
  olduğundan bu handler'da **veli bildirimi üretmez** (öğretmen bildirimi sonraki faz).
- **`PaymentOverdueNotificationHandler : IdempotentIntegrationEventHandler`** (M07-1, 2026-09-04) — Payments/`PaymentBecameOverdueDomainEvent`
  tüketir ve **`UserNotification`** yazar (veli-özel `ParentNotification` değil):
  | Alıcı | Kapı | Kind / Route |
  |---|---|---|
  | Ödemeyi takip eden **öğretmen** (`TeacherUserId`) | **koşulsuz** (kendi tahsilat kaydı) | `PaymentOverdue` / `/payments` |
  | Öğrencinin onaylı velileri | `Tier == Premium` **ve** `Prefs.Payments` | `PaymentOverdue` / `/parent/payments` |

  Push burada gönderilmez — `NotificationDispatchProcessor` turu `PushedOnUtc == null` kayıtları toplu gönderir
  (Öğrenilenler #40/#55). Gövde: vade tarihi + kalan tutar + para birimi (tr-TR biçimi). **P16 T5 / N-50:** vade tarihi
  **Türkiye yerel günüdür** (`NotificationTimeText.LocalDate` — 1 Eylül 00:30 TR vade "01.09.2026", eskiden "31.08.2026");
  ödev son teslimi `dd.MM.yyyy HH:mm` **yerel** (`NotificationTimeText.LocalDateTime` — 23:59 TR "23:59", eskiden "20:59").
  Üç bildirim metninin zaman biçimi tek yerde: `Infrastructure/NotificationTimeText.cs`. Hatırlatmanın **anı** ve oluşum
  bazlı hatırlatma P17 T6 (N-06).
- **`AssignmentDueSoonNotificationHandler` / `AssignmentMissedNotificationHandler : IdempotentIntegrationEventHandler`**
  (M06-3, 2026-09-04) — Assignments/`AssignmentDueSoonDomainEvent` ve `AssignmentMissedDomainEvent` tüketir, **`UserNotification`** yazar:
  | Olay | Alıcı | Kapı | Kind / Route |
  |---|---|---|---|
  | `AssignmentDueSoonDomainEvent` | öğrenci | — | `AssignmentDue` / `/student/assignments` |
  | `AssignmentMissedDomainEvent` | öğrenci | — | `AssignmentMissed` / `/student/assignments` |
  | `AssignmentMissedDomainEvent` | öğrencinin onaylı velileri | `Tier == Premium` **ve** `Prefs.MissedAssignment` | `AssignmentMissed` / `/parent/assignments` (+ `ParentNotification`) |

  Olay yalnız **öğrenci profili** kimliği (`StudentId`) taşır; alıcı kullanıcı kimliği `IStudentDirectory.GetOwnerUserIdAsync`
  ile çözülür (modül sınırı korunur). Bağlı kullanıcı yoksa öğrenci bildirimi atlanır, olay yine de işlenmiş sayılır.
  Push burada gönderilmez; tercih kapısı (`HomeworkReminderEnabled`) gönderim turunda uygulanır (Öğrenilenler #40/#55).
- **`TopicGoalAchievedNotificationHandler : IdempotentIntegrationEventHandler`** (M10-3, 2026-09-04) —
  ProgressTracking/`TopicGoalAchievedDomainEvent` tüketir, öğrenciye **koşulsuz** `UserNotification`
  (`Kind=System`, route `/student/progress`, "Hedefine ulaştın 🎯") yazar. Olay `StudentId` (öğrenci profili)
  taşır; alıcı `IStudentDirectory.GetOwnerUserIdAsync` ile çözülür — bağlı kullanıcı yoksa bildirim atlanır ama
  olay işlenmiş sayılır (aksi hâlde outbox sonsuza dek yeniden denerdi). Push gönderim turunda yapılır.
- **`ParentWeeklySummaryService : BackgroundService`** (6 saatte bir poll) → `ParentWeeklySummaryProcessor.RunAsync(now)` her Premium +
  `NotifyWeeklyProgressSummary` açık veliye **hafta başına bir** `WeeklySummary` üretir; hafta dedup anahtarı deterministik `Guid(parentUserId, isoYear, isoWeek)`
  (`ProcessedIntegrationEvent` ile). Metin MVP'de sabit; V-F study verisiyle zenginleşecek. **P16 T6 / K-19 (2026-11-15):**
  hafta **Türkiye haftasıdır** — ISO hafta `ILocalCalendar.ToLocalDate(now)`'ın gece yarısından alınır, Pazartesi 00:00 TR'de
  (Pazar 21:00Z) yeni hafta başlar. Eskiden UTC anın ISO haftasıydı: Pazar 21:00–24:00Z turu bir önceki haftanın anahtarıyla
  özeti atlıyordu.
- **Premium kaynağı (P09 Task 2'den beri):** `IEntitlementDirectory` — üyelik **Membership** modülünden gelir
  (`ParentNotificationTarget.Tier` bu kaynakla doldurulur; Notifications handler'ları değişmedi).
  Abonelik akışı (P09 Task 3-4) gelene kadar admin'in `PUT /api/parents/{id}/membership-tier` ile yazdığı
  eski alan **geçici köprü** üzerinden hâlâ Premium sayılır. "Bildirimler Free" istenirse Premium kapısı
  tek satırla kaldırılır.

- **`MessageSentNotificationHandler : IdempotentIntegrationEventHandler`** (M16-1, 2026-09-04) —
  Messaging/`MessageSentDomainEvent` tüketir, **alıcıya** `UserNotification(Kind=MessageReceived,
  Route="/messages/{conversationId}")` yazar. Olay alıcı kullanıcı kimliğini kendisi taşır, bu yüzden
  dizin sorgusu yoktur. Push burada gönderilmez: `NotificationDispatcher` turu üstlenir ve tercih
  kapısını orada uygular — `MessageReceived` tür bazında **koşulsuzdur** (§5.1), yalnız `PushEnabled`
  kapalıysa push atlanır, in-app kayıt yine durur.

## 2. Domain Modeli

### 🟢 Mevcut — `LessonReminder` (AggregateRoot)

Kaynak: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Birincil anahtar |
| `LessonScheduleId` | `Guid` | İlişkili planlı ders — DB'de **UNIQUE** index |
| `TeacherUserId` | `Guid` | Identity kullanıcısı (öğretmen) |
| `StudentId` | `Guid` | Öğrenci profili |
| `Title` | `string` | Bildirim başlığı, **maks. 200**, zorunlu |
| `Message` | `string` | Bildirim metni, **maks. 1000**, zorunlu |
| `ScheduledLessonStartAtUtc` | `DateTime` | Dersin başlangıcı |
| `RemindAtUtc` | `DateTime` | Hatırlatmanın tetikleneceği an |
| `Channel` | `enum NotificationChannel` | `InApp = 1`, `Push = 2` |
| `Status` | `enum ReminderStatus` | `Pending = 1`, `Sent = 2`, `Cancelled = 3`, `Failed = 4` (P03 Task 3) |
| `AttemptCount` | `int` | Başarısız push teslimat denemesi sayısı (P03 Task 3) |
| `LastError` | `string?` | Son teslimat hatası, **maks. 1000** (P03 Task 3) |
| `CreatedOnUtc` | `DateTime` | Oluşturulma |
| `UpdatedOnUtc` | `DateTime` | Son güncelleme (constructor'da `CreatedOnUtc` ile eşitlenir) |

**Davranışlar (idempotent guard'lı):**
- `MarkSent(utcNow)` → yalnızca `Status == Pending` ise `Sent` yapar, `LessonReminderSentDomainEvent` üretir.
- `Cancel(utcNow)` → zaten `Cancelled` değilse `Cancelled` yapar, `LessonReminderCancelledDomainEvent` üretir.
- `MarkFailed(error, utcNow)` (2026-09-03, P03 Task 3) → yalnızca `Pending` ise `AttemptCount++` + `LastError`; `MaxDeliveryAttempts` (5) sayısına ulaşınca `Failed`. Event üretmez.
- `Reschedule(scheduledStartAtUtc, remindAtUtc, utcNow)` (2026-07-08) → kayıt aynı kalırken zamanı günceller ve `Pending`'e alır (kaynak ders/girdi güncellendiğinde; tek-satır kısıtı korunur). Event üretmez.
- Constructor → `LessonReminderCreatedDomainEvent` üretir.

**Domain event'leri:** `LessonReminderCreatedDomainEvent`, `LessonReminderSentDomainEvent`, `LessonReminderCancelledDomainEvent` (her biri `LessonReminderId, LessonScheduleId, TeacherUserId, StudentId` + ilgili zaman damgasını taşır).

**Kalıcılık (DB):** şema `notifications`, tablo `lesson_reminders`.
- `Title` (200), `Message` (1000) zorunlu; `Channel` ve `Status` `string` enum dönüşümü (maks. 32).
- Index: `LessonScheduleId` **UNIQUE**; `(TeacherUserId, Status, RemindAtUtc)` bileşik (zamanlayıcı sorgusunu hızlandırır).

### 🟢 Mevcut — `DeviceToken` (AggregateRoot, 2026-09-03)

`src/Modules/Notifications/Domain/NotificationsDomainModel.cs` · tablo `notifications.device_tokens`

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | İstemcide üretilir (`IIdGenerator`); EF'te `ValueGeneratedNever()` |
| `UserId` | Guid | Token'ın sahibi |
| `Token` | string(512) | FCM/APNs kayıt token'ı · **UNIQUE indeks** |
| `Platform` | `DevicePlatform` | `Android=1` / `IOS=2` / `Web=3`, string olarak saklanır |
| `DeviceName` | string(128)? | "Pixel 8" gibi görünen ad |
| `IsActive` | bool | `(UserId, IsActive)` indeksli — gönderimde yalnız aktifler okunur |
| `CreatedOnUtc` / `LastSeenOnUtc` | DateTime | `IClock.UtcNow` |

Metotlar: `Touch(nowUtc)` — tazeler ve **yeniden aktifleştirir**; `Deactivate(nowUtc)` — FCM `UNREGISTERED`/`INVALID_ARGUMENT` yanıtında veya çıkışta.

> **Idempotens:** Aynı token yeniden kaydedilirse yeni satır açılmaz, mevcut satır `Touch` edilir (FCM token'ları rotasyona uğrar). Bilinmeyen bir token'ın silinmesi de hata değil, başarıdır.

### ⚠️ Önerilen — Genelleştirilmiş bildirim modeli

Mevcut model yalnızca **ders hatırlatması**na özgüdür ve alıcısı yalnızca öğretmendir. Ödev/ödeme/mesaj bildirimlerini ve öğrenci/veli alıcılarını desteklemek için ayrı, genel bir aggregate önerilir:

**`Notification` (önerilen AggregateRoot)**

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `RecipientUserId` | `Guid` | Bildirimi alacak Identity kullanıcısı (öğretmen/öğrenci/**veli**) |
| `RecipientRole` | `enum` | `Teacher / Student / Parent / Admin` (hedefleme + filtreleme) |
| `Type` | `enum NotificationType` | aşağıdaki tablo |
| `Title` / `Body` | `string` | metin (200 / 1000) |
| `Channel` | `NotificationChannel` | `InApp`, `Push` (ileride `Email`, `Sms`, `WhatsApp`) |
| `Status` | `enum` | `Pending / Sent / Failed / Cancelled` |
| `RelatedEntityId` | `Guid?` | İlgili kaynak (ödev, ders, mesaj, ödeme kaydı) |
| `DeepLink` | `string?` | Mobil yönlendirme (`/assignments/{id}` vb.) |
| `ReadAtUtc` | `DateTime?` | In-app okundu zamanı (null = okunmadı) |
| `RemindAtUtc` / `SentAtUtc` | `DateTime?` | Zamanlama / teslim |
| `CreatedOnUtc`, `UpdatedOnUtc` | `DateTime` | |

**`NotificationType` (önerilen enum):**

| Değer | Tetikleyen | Alıcı(lar) | Faz |
|-------|-----------|-----------|-----|
| `LessonReminder` | Scheduling (mevcut) | Öğretmen (+ öğrenci/veli) | 3 |
| `HomeworkDueSoon` | Assignments ([`m06_assignments.md`](m06_assignments.md)) son teslim yaklaşıyor | **Öğrenci + Veli** ([`m09_parents.md`](m09_parents.md)) | 3 |
| `HomeworkMissed` | Assignments son teslim kaçırıldı | **Öğrenci + Veli** | 3 |
| `PaymentOverdue` | Payments ([`m07_payments.md`](m07_payments.md)) gecikme | Öğretmen + Veli | 3 |
| `NewMessage` | Messaging ([`m16_messaging.md`](m16_messaging.md)) yeni mesaj | Karşı taraf | 3 |
| `DailyStudyReminder` | Study ([`m08_study.md`](m08_study.md)) günlük plan | Öğrenci | 4 |
| `WeeklySummary` | ProgressTracking ([`m10_progress_tracking.md`](m10_progress_tracking.md)) | Öğrenci/Veli/Öğretmen | 4 |

**`DeviceToken` (önerilen AggregateRoot)** — push için zorunlu:

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `UserId` | `Guid` | Sahibi |
| `Token` | `string` | FCM/APNs token (UNIQUE) |
| `Platform` | `enum` | `Android / iOS / Web` |
| `IsActive` | `bool` | Geçersiz token devre dışı bırakılır |
| `RegisteredOnUtc`, `LastSeenOnUtc` | `DateTime` | |

> Geçiş stratejisi: `LessonReminder` korunabilir veya yeni `Notification` modeline projekte edilebilir; ilk adımda `Notification` + `DeviceToken` eklemek mevcut davranışı bozmadan diğer türleri açar.

---

## 3. API Sözleşmesi

### Mevcut ✅

| Yetenek | Method + Route | Yetki |
|---------|----------------|-------|
| Öğretmen ders hatırlatmaları | `GET /api/notifications/teachers/{teacherUserId}/lesson-reminders?activeOnly={bool}` | `AuthenticatedUser` + Admin **veya** sahibi öğretmen (`LessonReminderQueryAuthorizer`) |
| **Veli bildirim listesi (Veli V-E)** | `GET /api/notifications/parents/{parentUserId}/notifications` | `AuthenticatedUser` + self **veya** Admin (`LessonReminderQueryAuthorizer`) — `ParentNotificationResponse[]`, en yeni önce |
| **Cihaz token kaydı (M11-2)** | `POST /api/notifications/device-tokens` | `AuthenticatedUser` + self **veya** Admin (`DeviceTokenCommandAuthorizer`) — gövde `RegisterDeviceTokenRequest(UserId, Token, Platform, DeviceName)`; idempotent |
| **Cihaz token silme (M11-2)** | `DELETE /api/notifications/device-tokens/{token}` | `AuthenticatedUser`; sahiplik gövdeden değil **oturumdan** (`ICurrentUser`) okunur — bilinmeyen token da `200` |

### Eksik / Önerilen ⚠️

```
GET  /api/notifications/users/{userId}?unreadOnly=       → kullanıcının in-app bildirim listesi (öğrenci/veli dahil)
GET  /api/notifications/users/{userId}/unread-count      → okunmamış sayısı (rozet)
PUT  /api/notifications/{id}/read                        → tek bildirimi okundu işaretle
PUT  /api/notifications/users/{userId}/read-all          → tümünü okundu işaretle
```

> Yetki kuralı: Her endpoint, kaynağın **sahibi** kullanıcı (veya Admin) için açılmalı; varsayılan reddet guard'ı şart (K3). Veli, bağlı olduğu öğrencinin bildirimlerini değil yalnızca **kendi** veli bildirimlerini görür ([`m09_parents.md`](m09_parents.md)).

---

## 4. İş Kuralları

0. **Cihaz token idempotensi (M11-2):** `Token` UNIQUE; aynı token yeniden POST edilirse `Touch` ile tazelenir. Boş/boşluk token `notifications.invalid_device_token` (**400**). Başkasının `UserId`'si ile kayıt `shared.forbidden` (**403**).
1. **Tek hatırlatma / ders:** `LessonScheduleId` UNIQUE; handler önce mevcut kaydı kontrol eder → tekrarlı event'te ikinci kayıt oluşmaz (idempotent).
2. **Durum geçişleri:** `MarkSent` yalnızca `Pending → Sent`; `Cancel` `* → Cancelled` (Cancelled hariç). Sent/Cancelled kayıt tekrar tetiklenmez.
3. **Zamanlama:** `RemindAtUtc <= UtcNow && Status == Pending` olan kayıtlar 30 sn'lik döngüde işlenir. Sunucu kapalıyken biriken kayıtlar açılınca toplu işlenir (geçmiş hatırlatma "geç teslim" sayılır).
4. ✅ **Offset (yapıldı 2026-07-01):** `ReminderOffsetMinutes` event payload'ında taşınıp handler'da `RemindAtUtc = StartAtUtc − offset` olarak uygulanıyor; kullanıcı ders bazında erken/geç hatırlatma seçebiliyor.
5. **Settings tercihleri (uygulandı — M11-5):** bir bildirim **gönderilmeden** önce alıcının `UserSetting` bayrakları `IUserNotificationPreferences` sözleşmesiyle okunur (bkz. §5.1).
   - `PushNotificationsEnabled == false` → push gönderilmez (in-app yine yazılabilir).
   - Tür bazlı: `UpcomingLessonReminderEnabled`, `HomeworkReminderEnabled`, `PaymentReminderEnabled`, `WeeklySummaryEnabled`.
   - Kayıt yoksa **varsayılan açık** kabul edilir.
6. **Alıcı hedefleme (önerilen):** Ödev son teslim bildirimi hem **öğrenci** hem **veli**ye gider; veli alıcısı yalnızca ilgili öğrenciye bağlıysa ([`m09_parents.md`](m09_parents.md)).
7. **Outbox zorunluluğu (K1):** Modül integration event'lere bağımlı; Outbox dispatch açık olmadan hiçbir bildirim doğmaz. Startup'ta uyarı loglanması önerilir.
8. **Push başarısızlığı:** geçersiz token `DeviceToken.IsActive = false` ile devre dışı; bildirim `Failed` işaretlenmeli (yalnızca `Sent` değil).
9. **Premium kanallar (Faz 5):** WhatsApp/SMS yalnızca üyelik paketine bağlı açılır ([`m17_membership.md`](m17_membership.md)).

---

## 5. Olay Akışı

### Mevcut (ders hatırlatması)

```
Scheduling: LessonScheduledDomainEvent (ReminderOffsetMinutes taşır)  ──(Outbox/IntegrationEvent)──▶
  LessonScheduleNotificationIntegrationEventHandler.HandleScheduledAsync
    → mevcut kayıt yoksa LessonReminder(Channel=InApp, Status=Pending,
                                         RemindAtUtc = StartAtUtc − ReminderOffsetMinutes)

Scheduling: LessonScheduleCancelledDomainEvent  ──▶
  HandleCancelledAsync → LessonScheduleId ile bul → reminder.Cancel()

[her 30 sn] NotificationDispatcher
  → ListDuePendingAsync(now)   (RemindAtUtc ≤ now && Status==Pending)
     → her biri: reminder.MarkSent(now)
        ↑ IPushSender ile aktif DeviceToken'lara gerçek push (P03 Task 3) ✅
```

### 🟢 Mevcut — vade geçti (M07-1, 2026-09-04)

```
PaymentOverdueScanner (Payments, saatlik)
  → PaymentRecord.MarkOverdue(now) → PaymentBecameOverdueDomainEvent (outbox)
     → PaymentOverdueNotificationHandler (inbox idempotent)
        → UserNotification(Kind=PaymentOverdue) → öğretmen (koşulsuz)
        → UserNotification(Kind=PaymentOverdue) → Premium + Prefs.Payments açık veli
           ↓ NotificationDispatchProcessor turu: PushedOnUtc boş olanları push eder
```

### 🟢 Mevcut — ödev son teslim uyarısı (M06-3, 2026-09-04)

```
AssignmentDueScanner (Assignments, saatlik)
  → Assignment.MarkDueSoon(now) → AssignmentDueSoonDomainEvent (outbox, ödev başına bir kez)
     → AssignmentDueSoonNotificationHandler (inbox idempotent)
        → UserNotification(Kind=AssignmentDue) → öğrenci
  → Assignment.MarkMissed(now)  → AssignmentMissedDomainEvent (outbox, ödev başına bir kez)
     → AssignmentMissedNotificationHandler (inbox idempotent)
        → UserNotification(Kind=AssignmentMissed) → öğrenci
        → UserNotification(Kind=AssignmentMissed) + ParentNotification → Premium + Prefs.MissedAssignment açık veli
           ↓ NotificationDispatchProcessor turu: PushedOnUtc boş olanları push eder (HomeworkReminderEnabled kapısı)
```

### Önerilen (yeni türler)

```
Assignments: AssignmentDueSoon / AssignmentMissed ──▶ Notification(Type=HomeworkDueSoon/Missed,
                                                       Recipient=Öğrenci) + Notification(Recipient=Veli)
Messaging:   MessageSent                          ──▶ Notification(Type=NewMessage, karşı taraf)
   ↓ her oluşturmada: UserSetting tercih kontrolü → kanal seçimi → DeviceToken'lara push
```

---

## 6. Mobil Ekranlar (Flutter)

- **Bildirim merkezi (Notification Center):** in-app bildirim listesi (okunmuş/okunmamış ayrımı, rozet sayacı). `GET /users/{userId}` + `unread-count`. Marka rengi `0xFF082B4F` başlık/rozet.
- **Bildirim ayarları:** [`m15_settings.md`](m15_settings.md) ekranına bağlı; push/e-posta ve tür bazlı anahtarlar.
- **Cihaz token kaydı:** login/uygulama açılışında push izni istenir, alınan FCM token `POST /devices` ile kaydedilir; logout'ta `DELETE /devices/{token}`.
- **Push tıklama → deep-link:** bildirim türüne göre ilgili ekrana yönlendirme (ders detayı, ödev takibi, mesaj, ödeme).
- **Rol farkları:** öğretmen ders/ödeme; öğrenci ders/ödev/çalışma; veli ödev/ödeme/özet bildirimleri görür ([`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md)).

---

## 7. Kabul Kriterleri

- [x] Outbox dispatch açık ([K1] — `Outbox:DispatchEnabled=true`) ve Scheduling event'leri tüketiliyor (`LessonScheduleNotificationIntegrationEventHandler`); tekrar teslimde ortak inbox (`IdempotentIntegrationEventHandler`) mükerrer `LessonReminder` üretilmesini engelliyor.
- [x] `RemindAtUtc` geldiğinde kullanıcıya **gerçek push** ulaşıyor (FCM) — `NotificationDispatchProcessor` `IPushSender` ile gönderiyor **(2026-09-03, P03 Task 3)**. Not: kendi-ders kayıtlarında alıcı çözümlemesi P07'ye bağlı.
- [x] Cihaz token'ı kaydediliyor/siliniyor (`POST`/`DELETE /device-tokens`); FCM `UNREGISTERED`/`INVALID_ARGUMENT` yanıtında token `Deactivate` ediliyor **(2026-09-03, P03 Task 1+3)**. Mobil tarafı P03 Task 5'te bağlanacak.
- [x] In-app bildirim listesi rolden bağımsız: `GET /users/{userId}/notifications` (sayfalı, `onlyUnread`), rozet `GET /users/{userId}/unread-count`, okundu `POST /{notificationId}/read` **(2026-09-03, P03 Task 4)**. Veli-özel `ParentNotification` listesi geriye dönük uyumluluk için korundu.
  - ⚠️ **Sorgu parametreleri varsayılanlı olmalıdır (P06 Task 10, 2026-09-04):** `onlyUnread`/`skip`/`take` varsayılansız zorunlu parametrelerdi; eksik gönderen istemci minimal API'nin `BadHttpRequestException`'ı yüzünden **500** alıyordu (istemci hatası sunucu hatası gibi görünüyordu). Artık `onlyUnread=false, skip=0, take=20` varsayılanları var. Regresyon: `RealNotificationCenterEndpointTests`.
- [~] Ödev bildirimi: **yeni ödev** → veliye hem `ParentNotification` hem `UserNotification` üretiliyor ve push turuna giriyor (2026-09-03, P03 Task 4); **son teslim yaklaşıyor/kaçırıldı** tetikleyicisi ve öğrenciye gönderim hâlâ yok **(P06/P07)**.
### 5.1 Tercih kapısı (M11-5) — nerede uygulanır

**Tek nokta: `NotificationDispatchProcessor` (gönderim turu).** Olay tüketicisine çapraz-modül okuma eklenmez
(senkron çağrı inbox transaction'ını uzatır); kapı, push atılmadan hemen önce çalışır.

| Yol | Okunan tercih | Kapalıysa ne olur |
|-----|---------------|-------------------|
| `DispatchDueRemindersAsync` (ders hatırlatması) | `PushEnabled` **ve** `LessonReminderEnabled` | Push gönderilmez, hatırlatma `MarkSent` edilir (kuyruk tıkanmaz, tekrar denenmez) |
| `DispatchPendingUserNotificationsAsync` (in-app kaynaklı push) | `PushEnabled` **ve** türe karşılık gelen bayrak | Push gönderilmez, `MarkPushed` edilir — **in-app kaydı durur** (bildirim merkezinde görünür) |

**Tür → tercih eşlemesi (`IsKindEnabled`):**

| `UserNotificationKind` | Tercih bayrağı |
|------------------------|----------------|
| `LessonReminder`, `LessonCompleted` | `UpcomingLessonReminderEnabled` |
| `AssignmentCreated`, `AssignmentDue`, `AssignmentMissed` | `HomeworkReminderEnabled` |
| `PaymentDue`, `PaymentOverdue` | `PaymentReminderEnabled` |
| `MessageReceived`, `System` | **koşulsuz** — kullanıcı kapatamaz (güvenlik/sistem) |

> Sözleşme: `EgitimUssu.Shared.Contracts.IUserNotificationPreferences` → Settings'teki
> `UserNotificationPreferencesDirectory` uygular. Ayar kaydı yoksa `UserNotificationPreferences.AllEnabled` döner.

- [x] Tercihe saygı: **veli** tercihleri (`ParentNotificationPreferences`) + genel `UserSetting` (M15) tercih kapısı gönderim turunda uygulanıyor (P05/M11-5).
- [x] `ReminderOffsetMinutes` ayarı hatırlatma zamanını etkiliyor (2026-07-01, Y1).
- [~] Sahiplik authorizer'ı: **yeni bildirim merkezi uçları** self/Admin karşılaştırması yapıyor (`UserNotificationAuthorizer`, okundu komutunda sahiplik bildirimin `UserId`'sinden okunuyor — 2026-09-03, P03 Task 4). Eski `parents`/`teachers` uçları için karşılaştırma zaten vardı; genel tarama **(P13)**.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **Outbox'ı aç (K1)** — yoksa hiçbir hatırlatma/bildirim oluşmaz. Startup uyarısı ekle.
2. **Gerçek push entegrasyonu (Y5) — ✅ kapandı (2026-09-03, P03 Task 1-3):** `DeviceToken` aggregate + `POST/DELETE /device-tokens` (Task 1); `IPushSender` + `FcmPushSender` (FCM HTTP v1, service-account OAuth2, `Push:Provider` ile seçilir, varsayılan `Logging`) (Task 2); `NotificationDispatchProcessor` gerçek gönderim + `MarkFailed` yeniden deneme + geçersiz token kapatma (Task 3). Kalan alt madde: kendi-ders (Ç-06) hatırlatmalarında öğrenciye teslim (`StudentId → UserId`, P07).
3. ✅ **`ReminderOffsetMinutes` kullanımı** — ders bazlı offset event payload'ıyla uygulandı (2026-07-01, Y1).
4. **Yeni bildirim türleri** — ödev son teslim yaklaşıyor/kaçırıldı (öğrenci + veli), ödeme gecikmesi, yeni mesaj; ileride günlük çalışma/haftalık özet.
5. ✅ **In-app bildirim merkezi (M11-3/M11-4)** — `UserNotification` aggregate + liste/rozet/okundu uçları + `NotificationDispatcher` push turu **eklendi (2026-09-03, P03 Task 4)**.
6. ~~**Settings (M15) entegrasyonu**~~ — ✅ P05/M11-5 ile kapandı (bkz. §5.1).
7. **Genelleştirilmiş `Notification` aggregate** — çok türlü/çok alıcılı model.
8. **WhatsApp/SMS (Faz 5, premium)** — [`m17_membership.md`](m17_membership.md) paketine bağlı.
9. **Test** — handler, authorizer, dispatcher ve idempotency birim/entegrasyon testleri (mimari incelemede test boşluğu).

---

## 9. İlişkili Dokümanlar

- Hatırlatmayı tetikleyen ders akışı → [`m04_scheduling.md`](m04_scheduling.md)
- Ödev son teslim bildirimleri → [`m06_assignments.md`](m06_assignments.md), veli kopyası → [`m09_parents.md`](m09_parents.md)
- Ödeme gecikmesi → [`m07_payments.md`](m07_payments.md)
- Yeni mesaj bildirimi → [`m16_messaging.md`](m16_messaging.md)
- Günlük çalışma / haftalık özet kaynağı → [`m08_study.md`](m08_study.md), [`m10_progress_tracking.md`](m10_progress_tracking.md)
- Bildirim tercihleri (push/e-posta/tür) → [`m15_settings.md`](m15_settings.md)
- Premium kanallar (WhatsApp/SMS) → [`m17_membership.md`](m17_membership.md)
- Mimari açıklar (K1 Outbox kapalı, Y5 push, K3 yetki) → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli / şema → [`veri_modeli.md`](veri_modeli.md)
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/admin.md`](../roles/admin.md)

---

*Bildirim Modülü (M11) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-11-15 (P16 Task 6 / K-19: haftalık veli özetinin hafta anahtarı Türkiye haftası) · 2026-11-15 (P16 Task 5 / N-50, N-06 metin: ders hatırlatması, ödev son teslimi ve ödeme vadesi metinleri Türkiye saatiyle — `NotificationTimeText`) · 2026-09-04 (P10 Task 3 / M16-1: yeni tüketici `MessageSentNotificationHandler` — yeni mesajda alıcıya `UserNotification(Kind=MessageReceived)`; rota `/messages/{conversationId}` bildirime dokunuşta derin bağlantı olur). Önceki not — P09 Task 2 / K-09: veli Premium kapısının kaynağı `ParentProfile.MembershipTier` alanından `IEntitlementDirectory`'ye taşındı — handler'lar değişmedi, `ParentNotificationTarget.Tier` artık Membership'ten doluyor). Önceki not — 2026-09-04 (P08 Task 2 / M10-3: yeni tüketici `TopicGoalAchievedNotificationHandler` — konu hedefi otomatik kapanınca öğrenciye `UserNotification(Kind=System)`) · 2026-09-04 (P06 Task 10 canlı doğrulama: bildirim listesi ucuna sorgu parametresi varsayılanları eklendi — eksik parametre 500 veriyordu; ayrıca vadesi geçen ödeme → in-app bildirim zinciri gerçek Postgres'te uçtan uca doğrulandı) · 2026-09-04 (P06 Task 7 / M06-3 + M09-3: iki yeni tüketici `AssignmentDueSoonNotificationHandler` + `AssignmentMissedNotificationHandler` — öğrenciye "teslime 24 saat kaldı"/"teslim edilmedi", kaçırılan ödevde ayrıca Premium + `Prefs.MissedAssignment` açık veliye; `StudentId → UserId` eşlemesi `IStudentDirectory` ile). Önceki not — 2026-09-04 (P06 Task 5 / M07-1: yeni tüketici `PaymentOverdueNotificationHandler` — `PaymentBecameOverdueDomainEvent` → öğretmene koşulsuz, Premium + "ödemeler" tercihi açık veliye `UserNotification(Kind=PaymentOverdue)`) · 2026-09-03 (P05/M11-5: tercih kapısı gönderim turunda — `IUserNotificationPreferences` + §5.1 tür eşlemesi; P03 Task 4: `UserNotification` aggregate + `GET /users/{userId}/notifications` + `/unread-count` + `POST /{id}/read` + dispatcher'ın push turu; modül durumu 🟡 → 🟢; P03 Task 3: gerçek push gönderimi + `LessonReminder.MarkFailed`/`ReminderStatus.Failed` + geçersiz token kapatma; P03 Task 2: `IPushSender`/`FcmPushSender` + `PushOptions`/`PushOptionsGuard`; P03/M11-2: `DeviceToken` aggregate + `POST`/`DELETE /device-tokens`). Önceki not — 2026-09-02 (F-06: kabul kriterleri kodla karşılaştırıldı — outbox/K1 ✅, push+cihaz token (P03), UserSetting (P05), sahiplik K3 (P13)) · 2026-08-19 (kod-senkron: API 2 endpoint doğrulandı — öğretmen `lesson-reminders` + veli `notifications`; `ParentNotificationType`/`NotificationChannel`/`ReminderStatus` enum değerleri koda eşit). Önceki not — 2026-07-19 (Veli V-E: veli bildirim motoru — `ParentNotification` + `ParentEventNotificationHandler` (Premium + tercih kapılı) + `ParentWeeklySummaryService` haftalık özet + `GET /parents/{id}/notifications`; `IParentNotificationDirectory`; olay kaynakları V-C bağlantı + V-G ödeme) · 2026-07-08 (öğrenci kişisel program hatırlatması: `StudyScheduleReminderIntegrationEventHandler` + `LessonReminder.Reschedule`)*
