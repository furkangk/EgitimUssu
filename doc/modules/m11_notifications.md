# 🔔 Bildirim Modülü (M11) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M11 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Notifications`) + Flutter mobil
> **PRD:** M11 · **Faz:** 3 (push altyapısı Faz 0.7) · **Durum:** 🟡 Kısmen — domain + zamanlayıcı + ders hatırlatması var, **gerçek push teslimatı YOK**
> **Mimari:** CQRS + Outbox + Integration Event (modüller arası), PostgreSQL (`notifications` şeması), Redis (gelecekte fan-out/cache)
> **Marka rengi (mobil):** `0xFF082B4F`

> Bu modül kendi başına az kullanıcı akışı içerir; büyük ölçüde **diğer modüllerin integration event'lerini dinleyerek** hatırlatma/bildirim üretir. Bir tür "olay tüketici + zamanlayıcı" modülüdür.
>
> ⚠️ **Kritik bağımlılık:** [`mimari_inceleme.md`](mimari_inceleme.md) **K1** — Outbox dispatch varsayılan **kapalı**. Açılmadıkça Scheduling event'leri yayımlanmaz ve **hiçbir hatırlatma kaydı oluşmaz**.

---

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
  - **LessonScheduled** → ilgili `LessonScheduleId` için kayıt **yoksa** yeni `LessonReminder` oluşturur. Başlık `"Yaklasan ders hatirlatmasi"`, mesaj `"Ders {StartAtUtc:O} tarihinde baslayacak."`, `Channel = InApp`, `Status = Pending`, `RemindAtUtc = StartAtUtc.AddMinutes(-max(ReminderOffsetMinutes, 0))` — offset **event payload'ından** gelir (2026-07-01, Y1; önceden sabit 60 dk idi). Öğretmen offset'i artık mobil `LessonFormSheet` içinde seçer (Kapalı/15/30dk/1sa/1gün, 2026-07-08). Var olan kayıt kontrolü ile **idempotent**.
  - **LessonScheduleCancelled** → `LessonScheduleId` ile bulunan kayıtta `Cancel()` çağrılır.
- `StudyScheduleReminderIntegrationEventHandler.CanHandle` (2026-07-08): `SourceModule == "Scheduling"` ve `Name ∈ {StudyScheduleEntryScheduledDomainEvent, StudyScheduleEntryRescheduledDomainEvent, StudyScheduleEntryCancelledDomainEvent}`. Öğrencinin kendi program girdisi için hatırlatma yönetir; kayıt aynı `LessonReminder` aggregate'ında tutulur — girdinin kimliği `LessonScheduleId` alanına (tekil), `StudentId` öğrenciye, `TeacherUserId = Guid.Empty` (öğretmen yok).
  - **Scheduled** → `ReminderOffsetMinutes > 0` ise (yoksa kayıt oluşturulmaz, `0` = kapalı) idempotent `LessonReminder` (başlık `"Calisma hatirlatmasi"`, `RemindAtUtc = StartAtUtc − offset`).
  - **Rescheduled** → mevcut kayıt yeni saate taşınır (`Reschedule`, Pending'e alınır); offset `0` olduysa iptal edilir; kayıt yoksa oluşturulur.
  - **Cancelled** → mevcut kayıtta `Cancel()`.
  - Tekrarlı girdide hatırlatma **ilk oluşuma** göre (öğretmen dersleriyle aynı MVP davranışı). Notifications, Scheduling'e proje referansı vermez — olay adı + JSON payload üzerinden çalışır.
- `NotificationDispatcher` her 30 sn'de bir kendi scope'unu açar, `ListDuePendingAsync(utcNow)` (yani `RemindAtUtc <= now && Status == Pending`) sonuçlarının her biri için `reminder.MarkSent(utcNow)` çağırır ve değişiklikleri kaydeder.

### 🔴 Eksik olan

- **Gerçek push teslimatı YOK** (bkz. [`mimari_inceleme.md`](mimari_inceleme.md) **Y5**). `DispatchDueRemindersAsync` yalnızca `reminder.MarkSent(...)` yapar; **FCM/APNs'e hiçbir şey gönderilmez**. Bildirim "gönderildi" olarak işaretlenir ama kullanıcıya hiçbir şey ulaşmaz.
- **Cihaz token kaydı yok** — mobil FCM/APNs token'larını alıp saklayan bir aggregate/endpoint bulunmuyor.
- `NotificationChannel.Push` enum değeri tanımlı ama tüm hatırlatmalar `InApp` olarak üretiliyor.
- ✅ **`ReminderOffsetMinutes` artık kullanılıyor** (2026-07-01, Y1) — offset `LessonScheduledDomainEvent` payload'ında taşınıp handler'da uygulanıyor; hatırlatma ders bazında erken/geç kurulabiliyor ([`m04_scheduling.md`](m04_scheduling.md)).
- **Ders dışı bildirim türleri yok:** ödev son teslim yaklaşıyor/kaçırıldı, ödeme gecikmesi, yeni mesaj, günlük çalışma, haftalık özet (PRD M11 tablosu).
- **Öğrenci/veli için in-app bildirim listesi endpoint'i yok** — yalnızca öğretmen-merkezli ders hatırlatması listelenebiliyor. Okundu/okunmamış kavramı domain'de yok.
- **Settings (M15) tercihleri kontrol edilmiyor** — `UserSetting`'teki `PushNotificationsEnabled`, `HomeworkReminderEnabled` vb. bayraklara saygı gösterilmiyor ([`m15_settings.md`](m15_settings.md)).
- ⚠️ **Outbox kapalı (K1):** integration event'ler yayımlanmadığından, gerçekte ders planlansa bile hatırlatma kaydı **hiç oluşmaz**. Bu, modülün uçtan uca çalışmasının ön koşuludur.

---

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
| `Status` | `enum ReminderStatus` | `Pending = 1`, `Sent = 2`, `Cancelled = 3` |
| `CreatedOnUtc` | `DateTime` | Oluşturulma |
| `UpdatedOnUtc` | `DateTime` | Son güncelleme (constructor'da `CreatedOnUtc` ile eşitlenir) |

**Davranışlar (idempotent guard'lı):**
- `MarkSent(utcNow)` → yalnızca `Status == Pending` ise `Sent` yapar, `LessonReminderSentDomainEvent` üretir.
- `Cancel(utcNow)` → zaten `Cancelled` değilse `Cancelled` yapar, `LessonReminderCancelledDomainEvent` üretir.
- `Reschedule(scheduledStartAtUtc, remindAtUtc, utcNow)` (2026-07-08) → kayıt aynı kalırken zamanı günceller ve `Pending`'e alır (kaynak ders/girdi güncellendiğinde; tek-satır kısıtı korunur). Event üretmez.
- Constructor → `LessonReminderCreatedDomainEvent` üretir.

**Domain event'leri:** `LessonReminderCreatedDomainEvent`, `LessonReminderSentDomainEvent`, `LessonReminderCancelledDomainEvent` (her biri `LessonReminderId, LessonScheduleId, TeacherUserId, StudentId` + ilgili zaman damgasını taşır).

**Kalıcılık (DB):** şema `notifications`, tablo `lesson_reminders`.
- `Title` (200), `Message` (1000) zorunlu; `Channel` ve `Status` `string` enum dönüşümü (maks. 32).
- Index: `LessonScheduleId` **UNIQUE**; `(TeacherUserId, Status, RemindAtUtc)` bileşik (zamanlayıcı sorgusunu hızlandırır).

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

### Eksik / Önerilen ⚠️

```
POST /api/notifications/devices                          → mobil FCM/APNs token kaydı (UserId = oturum sahibi)
DELETE /api/notifications/devices/{token}                → cihaz token sil (logout/yenileme)
GET  /api/notifications/users/{userId}?unreadOnly=       → kullanıcının in-app bildirim listesi (öğrenci/veli dahil)
GET  /api/notifications/users/{userId}/unread-count      → okunmamış sayısı (rozet)
PUT  /api/notifications/{id}/read                        → tek bildirimi okundu işaretle
PUT  /api/notifications/users/{userId}/read-all          → tümünü okundu işaretle
```

> Yetki kuralı: Her endpoint, kaynağın **sahibi** kullanıcı (veya Admin) için açılmalı; varsayılan reddet guard'ı şart (K3). Veli, bağlı olduğu öğrencinin bildirimlerini değil yalnızca **kendi** veli bildirimlerini görür ([`m09_parents.md`](m09_parents.md)).

---

## 4. İş Kuralları

1. **Tek hatırlatma / ders:** `LessonScheduleId` UNIQUE; handler önce mevcut kaydı kontrol eder → tekrarlı event'te ikinci kayıt oluşmaz (idempotent).
2. **Durum geçişleri:** `MarkSent` yalnızca `Pending → Sent`; `Cancel` `* → Cancelled` (Cancelled hariç). Sent/Cancelled kayıt tekrar tetiklenmez.
3. **Zamanlama:** `RemindAtUtc <= UtcNow && Status == Pending` olan kayıtlar 30 sn'lik döngüde işlenir. Sunucu kapalıyken biriken kayıtlar açılınca toplu işlenir (geçmiş hatırlatma "geç teslim" sayılır).
4. ✅ **Offset (yapıldı 2026-07-01):** `ReminderOffsetMinutes` event payload'ında taşınıp handler'da `RemindAtUtc = StartAtUtc − offset` olarak uygulanıyor; kullanıcı ders bazında erken/geç hatırlatma seçebiliyor.
5. **Settings tercihleri (önerilen, M15):** bir bildirim **oluşturulmadan/gönderilmeden** önce alıcının `UserSetting` bayrakları kontrol edilmeli:
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
        ↑ BURADA gerçek FCM/APNs push gönderilmeli (EKSİK — Y5)
```

### Önerilen (yeni türler)

```
Assignments: AssignmentDueSoon / AssignmentMissed ──▶ Notification(Type=HomeworkDueSoon/Missed,
                                                       Recipient=Öğrenci) + Notification(Recipient=Veli)
Payments:    PaymentOverdue                       ──▶ Notification(Type=PaymentOverdue, Öğretmen+Veli)
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

- [ ] Outbox dispatch açık ([K1]) ve Scheduling event'leri tüketiliyor; planlanan her ders için tam **bir** `LessonReminder` oluşuyor.
- [ ] `RemindAtUtc` geldiğinde kullanıcıya **gerçek push** ulaşıyor (FCM/APNs), yalnızca `MarkSent` değil (Y5 kapandı).
- [ ] Mobil cihaz token'ı kaydediliyor/siliniyor; geçersiz token devre dışı bırakılıyor.
- [ ] Öğrenci ve veli in-app bildirim listesini görebiliyor; okundu/okunmamış ve rozet sayısı doğru.
- [ ] Ödev son teslim yaklaşıyor/kaçırıldı bildirimi hem öğrenciye hem veliye gidiyor.
- [ ] Kullanıcı `UserSetting`'te bir türü kapattığında o bildirim **gönderilmiyor** (M15 entegrasyonu).
- [x] `ReminderOffsetMinutes` ayarı hatırlatma zamanını etkiliyor (2026-07-01, Y1).
- [ ] Sahiplik authorizer'ı, başka kullanıcının bildirimlerine erişimi reddediyor (K3).

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **Outbox'ı aç (K1)** — yoksa hiçbir hatırlatma/bildirim oluşmaz. Startup uyarısı ekle.
2. **Gerçek push entegrasyonu (Y5)** — FCM/APNs gönderimi + `DeviceToken` aggregate + `POST/DELETE /devices`.
3. ✅ **`ReminderOffsetMinutes` kullanımı** — ders bazlı offset event payload'ıyla uygulandı (2026-07-01, Y1).
4. **Yeni bildirim türleri** — ödev son teslim yaklaşıyor/kaçırıldı (öğrenci + veli), ödeme gecikmesi, yeni mesaj; ileride günlük çalışma/haftalık özet.
5. **Öğrenci/veli in-app bildirim listesi + okundu** endpoint'leri.
6. **Settings (M15) entegrasyonu** — tercih bayraklarına göre filtreleme/kanal seçimi.
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
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/admin.md`](../roles/admin.md)

---

*Bildirim Modülü (M11) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-07-08 (öğrenci kişisel program hatırlatması: `StudyScheduleReminderIntegrationEventHandler` + `LessonReminder.Reschedule`)*
