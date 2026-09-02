# P05 — Ayarlar Modülü ve Gerçek Tercih Yönetimi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** M15'i gerçek bir modül haline getirmek (oku/yaz ayar uçları), bildirim tercihlerini gönderim yoluna bağlamak, "diğer oturumları sonlandır" güvenlik ayarını çalıştırmak ve mobildeki **tamamen sahte** ayar/abonelik/rapor sayfalarını gerçek veriye bağlamak (ya da yanıltıcı olanları kaldırmak).

**Architecture:** `UserSetting` aggregate'i zaten var; eksik olan okuma/yazma uçları ve tüketiciler. `Shared/Contracts`'a `IUserNotificationPreferences` eklenir; Settings uygular, Notifications tüketir — böylece bir bildirim türü kapatıldığında **gerçekten gönderilmez**. Identity giriş akışı `SessionTerminationPolicy.TerminateOtherSessions` seçiliyse kullanıcının diğer refresh oturumlarını iptal eder. Mobilde `more` sekmesindeki bottom-sheet'ler gerçek `SettingsRepository`'ye bağlanır; karşılığı olmayan (abonelik, raporlar) sahte kartlar **kaldırılır** ve ilgili plana (P09/P08) ertelenir.

**Tech Stack:** .NET 9, EF Core, xUnit; Flutter (flutter_bloc, get_it).

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md`

## Global Constraints

- **Varsayılan güvenli:** Ayar kaydı yoksa `GET` **varsayılan** nesne döner (hepsi açık, `PrivacyLevel.Standard`, `KeepLatest`), 404 dönmez.
- **Sahiplik:** Kullanıcı yalnız kendi ayarını okur/yazar; Admin istisnadır (mevcut `SettingsAuthorizer` deseni).
- **Sahte veri yok:** Karşılığı olmayan hiçbir ekran "gerçekmiş gibi" veri göstermez; ya gerçek API'ye bağlanır ya kaldırılır.
- **Modül sınırı:** Notifications, Settings'in DbContext'ini okumaz; yalnız `IUserNotificationPreferences` sözleşmesini kullanır.
- **Zaman:** `IClock.UtcNow`. **Sonuç:** `Result`/`Result<T>`.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: Kullanıcı ayarlarını oku/yaz (M15-1)

**Files:**
- Modify: `src/Modules/Settings/Application/SettingsFeatures.cs`
- Modify: `src/Modules/Settings/Application/SettingsFeatures.cs` (authorizer'lar aynı dosyada)
- Modify: `src/Modules/Settings/API/SettingsModule.cs`
- Modify: `src/Modules/Settings/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/UserSettingTests.cs` (mevcut dosyaya ekleme)
- Test: `tests/Integration/SettingsWorkflowIntegrationTests.cs` (yeni)

**Interfaces:**
- Produces:
  - `sealed record GetUserSettingsQuery(Guid UserId) : IQuery<Result<UserSettingsResponse>>`
  - `sealed record UpdateUserSettingsCommand(Guid UserId, bool PushNotificationsEnabled, bool EmailNotificationsEnabled, bool UpcomingLessonReminderEnabled, bool HomeworkReminderEnabled, bool PaymentReminderEnabled, bool WeeklySummaryEnabled, PrivacyLevel PrivacyLevel, SessionTerminationPolicy SessionTerminationPolicy) : ICommand<Result<UserSettingsResponse>>`
  - `sealed record UserSettingsResponse(Guid UserId, bool PushNotificationsEnabled, bool EmailNotificationsEnabled, bool UpcomingLessonReminderEnabled, bool HomeworkReminderEnabled, bool PaymentReminderEnabled, bool WeeklySummaryEnabled, bool ShareStudyDataWithTeacher, bool ShareStudyDataWithParent, string PrivacyLevel, string SessionTerminationPolicy, DateTime LastUpdatedOnUtc)`
  - `GET /api/settings/users/{userId}` · `PUT /api/settings/users/{userId}`
  - `UserSetting.UpdatePreferences(...)` domain metodu (`SetStudySharing` ile aynı desende, `LastUpdatedOnUtc` günceller).

- [ ] **Step 1: Domain testini yaz (kırmızı)**

`tests/Unit/UserSettingTests.cs` içine:
```csharp
[Fact]
public void UpdatePreferences_Should_Change_Flags_And_Timestamp()
{
    var setting = new UserSetting(
        Guid.NewGuid(), Guid.NewGuid(), true, true, true, true, true, true, false, false,
        PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, new DateTime(2026, 1, 1));

    setting.UpdatePreferences(
        pushNotificationsEnabled: false,
        emailNotificationsEnabled: true,
        upcomingLessonReminderEnabled: false,
        homeworkReminderEnabled: true,
        paymentReminderEnabled: false,
        weeklySummaryEnabled: true,
        privacyLevel: PrivacyLevel.Limited,
        sessionTerminationPolicy: SessionTerminationPolicy.TerminateOtherSessions,
        updatedOnUtc: new DateTime(2026, 2, 1));

    Assert.False(setting.PushNotificationsEnabled);
    Assert.False(setting.UpcomingLessonReminderEnabled);
    Assert.Equal(PrivacyLevel.Limited, setting.PrivacyLevel);
    Assert.Equal(SessionTerminationPolicy.TerminateOtherSessions, setting.SessionTerminationPolicy);
    Assert.Equal(new DateTime(2026, 2, 1), setting.LastUpdatedOnUtc);
    // Çalışma paylaşımı bu komutla değişmez (ayrı uç):
    Assert.False(setting.ShareStudyDataWithTeacher);
}
```

- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserSettingTests"`

- [ ] **Step 3: Domain metodunu yaz**

`src/Modules/Settings/Domain/SettingsDomainModel.cs`, `SetStudySharing`'in altına:
```csharp
    /// <summary>Bildirim/gizlilik/güvenlik tercihlerini günceller. Çalışma paylaşımı ayrı uçtan yönetilir.</summary>
    public void UpdatePreferences(
        bool pushNotificationsEnabled,
        bool emailNotificationsEnabled,
        bool upcomingLessonReminderEnabled,
        bool homeworkReminderEnabled,
        bool paymentReminderEnabled,
        bool weeklySummaryEnabled,
        PrivacyLevel privacyLevel,
        SessionTerminationPolicy sessionTerminationPolicy,
        DateTime updatedOnUtc)
    {
        PushNotificationsEnabled = pushNotificationsEnabled;
        EmailNotificationsEnabled = emailNotificationsEnabled;
        UpcomingLessonReminderEnabled = upcomingLessonReminderEnabled;
        HomeworkReminderEnabled = homeworkReminderEnabled;
        PaymentReminderEnabled = paymentReminderEnabled;
        WeeklySummaryEnabled = weeklySummaryEnabled;
        PrivacyLevel = privacyLevel;
        SessionTerminationPolicy = sessionTerminationPolicy;
        LastUpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Kaydı olmayan kullanıcı için varsayılan ayar (kalıcı değil, yalnız okuma yanıtı).</summary>
    public static UserSetting Default(Guid userId, DateTime nowUtc)
        => new(Guid.Empty, userId, true, true, true, true, true, true, false, false,
            PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, nowUtc);
```

- [ ] **Step 4: Query/command + handler + authorizer'ları yaz**

`SettingsFeatures.cs` — `SetStudySharingCommandHandler` desenini izle:
- `GetUserSettingsQueryHandler`: kayıt yoksa `UserSetting.Default(...)` map'ler.
- `UpdateUserSettingsCommandHandler`: kayıt yoksa oluşturur (mevcut `ShareStudyData*` değerlerini korur), varsa `UpdatePreferences` çağırır.
- `GetUserSettingsQueryAuthorizer` (`IQueryAuthorizer<GetUserSettingsQuery>`) ve `UpdateUserSettingsCommandAuthorizer` — mevcut `SettingsAuthorizer` mantığının aynısı (kendisi veya Admin).

`DependencyInjection.cs`'e 2 handler + 2 authorizer kaydı ekle.

- [ ] **Step 5: Endpoint'leri ekle**

`SettingsModule.cs`:
```csharp
        group.MapGet("/users/{userId:guid}", GetUserSettingsAsync)
            .WithSummary("Kullanıcının ayarlarını getirir (kayıt yoksa varsayılan)")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPut("/users/{userId:guid}", UpdateUserSettingsAsync)
            .WithSummary("Bildirim/gizlilik/güvenlik tercihlerini günceller")
            .RequireAuthorization("AuthenticatedUser");
```
İstek kaydı:
```csharp
public sealed record UpdateUserSettingsRequest(
    bool PushNotificationsEnabled,
    bool EmailNotificationsEnabled,
    bool UpcomingLessonReminderEnabled,
    bool HomeworkReminderEnabled,
    bool PaymentReminderEnabled,
    bool WeeklySummaryEnabled,
    PrivacyLevel PrivacyLevel,
    SessionTerminationPolicy SessionTerminationPolicy);
```

- [ ] **Step 5b: Sahiplik authorizer testini yaz (M15-2)**

`tests/Unit/UserSettingTests.cs` içine: başka kullanıcının ayarını okumaya/yazmaya çalışan authorizer `shared.forbidden` döndürüyor; `Admin` rolü izin alıyor; kimliksiz istek reddediliyor.
Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserSettingTests"` → PASS.

- [ ] **Step 6: Integration testi yaz ve koştur**

`tests/Integration/SettingsWorkflowIntegrationTests.cs`: kayıt → `GET` varsayılan döner (200) → `PUT` ile `homeworkReminderEnabled: false` → `GET` bunu yansıtır → **başka bir kullanıcının** ayarına `GET` 403.
Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~SettingsWorkflow"`
Expected: PASS.

- [ ] **Step 7: Doküman + commit**

`doc/modules/m15_settings.md` (durum 🟡 → 🟢 adayı, uçlar, kontrol listesi), `doc/modules/00_genel_bakis.md` (Settings bloğu).
```bash
git add src/Modules/Settings tests doc
git commit -m "feat(settings): kullanici ayarlarini oku/yaz uclari (M15-1)"
```

---

### Task 2: Bildirim tercihlerini gönderim yoluna bağla (M11-5)

**Files:**
- Create: `src/Shared/Contracts/UserNotificationPreferencesContract.cs`
- Create: `src/Modules/Settings/Infrastructure/UserNotificationPreferencesDirectory.cs`
- Modify: `src/Modules/Settings/Infrastructure/DependencyInjection.cs`
- Modify: `src/Modules/Notifications/Infrastructure/NotificationDispatching.cs`
- Modify: `src/Modules/Notifications/Infrastructure/ParentEventNotificationHandler.cs`
- Test: `tests/Unit/NotificationPreferenceGateTests.cs`

**Interfaces:**
- Produces:
  - ```csharp
    namespace EgitimUssu.Shared.Contracts;

    /// <summary>Bir kullanıcının bildirim tercihleri (Settings uygular, Notifications tüketir).</summary>
    public sealed record UserNotificationPreferences(
        bool PushEnabled,
        bool EmailEnabled,
        bool LessonReminderEnabled,
        bool HomeworkReminderEnabled,
        bool PaymentReminderEnabled,
        bool WeeklySummaryEnabled);

    public interface IUserNotificationPreferences
    {
        Task<UserNotificationPreferences> GetAsync(Guid userId, CancellationToken cancellationToken);
    }
    ```
  - `UserNotificationPreferencesDirectory : IUserNotificationPreferences` — kayıt yoksa hepsi `true` döner.

- [ ] **Step 1: Testi yaz (kırmızı)**

`tests/Unit/NotificationPreferenceGateTests.cs`:
```csharp
[Fact]
public async Task Lesson_Reminder_Should_Not_Be_Sent_When_Preference_Disabled()
{
    // sahte IUserNotificationPreferences: LessonReminderEnabled = false
    // Assert: IPushSender hiç çağrılmadı, reminder Sent olarak işaretlendi (kuyruk tıkanmasın)
}

[Fact]
public async Task Push_Disabled_Should_Skip_Push_But_Keep_InApp_Notification()
{
    // PushEnabled = false → push yok ama UserNotification yazılmış olmalı
}
```

- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~NotificationPreferenceGateTests"`

- [ ] **Step 3: Sözleşmeyi ve implementasyonu yaz** — `Shared/Contracts` + Settings tarafında `IUserSettingRepository` üzerinden okuyan directory; DI kaydı.

- [ ] **Step 4: Notifications'ta kapıyı uygula**

`NotificationDispatchProcessor.DispatchDueRemindersAsync` içinde, token döngüsünden **önce**:
```csharp
            var preferences = await _preferences.GetAsync(reminder.UserId, cancellationToken);
            if (!preferences.LessonReminderEnabled)
            {
                // Kullanıcı bu türü kapatmış: gönderme, kuyruğu da tıkama.
                reminder.MarkSent(now);
                continue;
            }

            if (!preferences.PushEnabled)
            {
                reminder.MarkSent(now);
                continue;
            }
```
`ParentEventNotificationHandler` ve `UserNotification` üreten diğer handler'larda da ilgili bayrak kontrol edilir (`HomeworkReminderEnabled`, `PaymentReminderEnabled`, `WeeklySummaryEnabled`).

- [ ] **Step 5: Testleri yeşile al** — Run: `dotnet test EgitimUssu.slnx`

- [ ] **Step 6: Doküman + commit**

`doc/modules/m11_notifications.md` ("tercih kapısı" davranışı), `doc/modules/m15_settings.md` (expose edilen sözleşme).
```bash
git add src/Shared/Contracts src/Modules/Settings src/Modules/Notifications tests doc
git commit -m "feat(settings): bildirim tercihleri gonderim yolunda uygulaniyor (M11-5)"
```

---

### Task 3: "Diğer oturumları sonlandır" (M15-3)

**Files:**
- Create: `src/Shared/Contracts/SessionPolicyContract.cs`
- Create: `src/Modules/Settings/Infrastructure/SessionPolicyDirectory.cs`
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs` (`LoginCommandHandler`)
- Modify: `src/Modules/Identity/Infrastructure/DependencyInjection.cs`
- Test: `tests/Unit/TerminateOtherSessionsTests.cs`

**Interfaces:**
- Produces:
  - `interface ISessionPolicyDirectory { Task<bool> ShouldTerminateOtherSessionsAsync(Guid userId, CancellationToken ct); }`
  - `IUserAccountRepository.RevokeOtherSessionsAsync(Guid userId, Guid keepSessionId, DateTime nowUtc, CancellationToken ct)`

- [ ] **Step 1: Testi yaz (kırmızı)** — Politika `true` iken login sonrası kullanıcının önceki refresh oturumları `Revoked`; `false` iken korunuyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~TerminateOtherSessionsTests"`
- [ ] **Step 3: Sözleşme + directory + repository metodunu yaz.**
- [ ] **Step 4: `LoginCommandHandler`'a bağla** — yeni oturum oluşturulduktan sonra:
```csharp
        if (await _sessionPolicy.ShouldTerminateOtherSessionsAsync(user.Id, cancellationToken))
        {
            await _repository.RevokeOtherSessionsAsync(user.Id, session.Id, now, cancellationToken);
        }
```
- [ ] **Step 5: Testler** — Run: `dotnet test EgitimUssu.slnx` → yeşil.
- [ ] **Step 6: Doküman + commit**

```bash
git add src/Shared/Contracts src/Modules/Settings src/Modules/Identity tests doc
git commit -m "feat(settings): diger oturumlari sonlandir politikasi (M15-3)"
```

---

### Task 4: Rol bazlı gizlilik/izin matrisi (M15-4)

**Files:**
- Modify: `src/Modules/Settings/Domain/SettingsDomainModel.cs` (`PrivacyLevel` etkisi)
- Modify: `src/Modules/Settings/Infrastructure/StudentPrivacyDirectory.cs`
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs` (dashboard gizlilik filtresi)
- Test: `tests/Unit/StudentPrivacyFilterTests.cs` (mevcut dosyaya ekleme)

**Interfaces:**
- `PrivacyLevel` semantiği netleştirilir ve **davranışa bağlanır**:
  - `Standard` — öğretmen ve onaylı veli çalışma özetini + detayını görür.
  - `Limited` — yalnız haftalık toplam süre ve ders katılımı görünür; seans/test detayı gizlenir.
  - `Hidden` — hiçbir çalışma verisi paylaşılmaz (yalnız ders/ödev/ödeme).

- [ ] **Step 1: Testleri yaz (kırmızı)** — `StudentPrivacyFilterTests` içine üç seviye için birer test: veli dashboard yanıtında hangi alanların `null`/boş geldiği.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentPrivacyFilterTests"`
- [ ] **Step 3: `StudentPrivacyDirectory`'yi seviyeye duyarlı hale getir** — bugün yalnız `ShareStudyDataWith*` bayraklarına bakıyor; `PrivacyLevel` de dikkate alınsın (en kısıtlayıcı kazanır).
- [ ] **Step 4: Veli dashboard handler'ında filtreyi uygula.**
- [ ] **Step 5: Testler** — Run: `dotnet test EgitimUssu.slnx` → yeşil.
- [ ] **Step 6: Doküman + commit**

`doc/modules/m15_settings.md` + `doc/modules/m09_parents.md` (gizlilik matrisi tablosu).
```bash
git add src/Modules mobile doc tests
git commit -m "feat(settings): gizlilik seviyesi veli gorunumune baglandi (M15-4)"
```

---

### Task 5: Mobil — ayarlar ekranlarını gerçeğe bağla, sahteleri kaldır

**Files:**
- Create: `mobile/lib/features/settings/domain/settings_contracts.dart`
- Create: `mobile/lib/features/settings/data/models/user_settings_model.dart`
- Create: `mobile/lib/features/settings/data/repositories/settings_repository_impl.dart`
- Create: `mobile/lib/features/settings/presentation/cubit/settings_cubit.dart`
- Create: `mobile/lib/features/settings/presentation/pages/notification_settings_page.dart`
- Create: `mobile/lib/features/settings/presentation/pages/privacy_settings_page.dart`
- Modify: `mobile/lib/features/more/presentation/pages/more_page.dart` (sahte sheet'lerin kaldırılması)
- Modify: `mobile/lib/core/di/injector.dart`, `mobile/lib/core/routing/app_router.dart`
- Modify: `mobile/lib/features/notifications/presentation/pages/notifications_page.dart:18`, `mobile/lib/features/parent/presentation/pages/parent_home_page.dart:18` (D-17)
- Test: `mobile/test/features/settings/settings_cubit_test.dart`
- Create: `doc/pages/settings_notifications.md`, `doc/pages/settings_privacy.md`

**Interfaces:**
- `SettingsRepository`: `Future<UserSettings> load(String userId)`, `Future<UserSettings> save(String userId, UserSettings settings)`.
- `SettingsCubit`: `loading | loaded(settings) | saving | failure(message)`; `toggle(...)` iyimser günceller, hata olursa geri alır ve SnackBar gösterir.
- Rotalar: `/settings/notifications`, `/settings/privacy`.

- [ ] **Step 1: Cubit testini yaz (kırmızı)** — yükleme → `loaded`; `toggle` sonrası `save` çağrılıyor; `save` hata verirse eski değere dönülüyor.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/settings/settings_cubit_test.dart`
- [ ] **Step 3: Model + repository + cubit'i yaz** (mock fallback **yok**).
- [ ] **Step 4: İki ayar ekranını yaz** — `SwitchListTile`'lar gerçek değerlerle dolar, değişiklik anında kaydedilir, kaydedilirken satır `AbsorbPointer` + küçük ilerleme göstergesi.
- [ ] **Step 5: `more_page` temizliği**
  - `_showNotificationSettingsSheet` → `context.push('/settings/notifications')`.
  - `_showGeneralSettingsSheet` (sessiz saatler) → gizlilik ekranına taşınır veya karşılığı yoksa **kaldırılır**.
  - `_showWorkSettingsSheet`, `_showHolidaySettingsSheet` → gerçek karşılıkları M04 tatil uçları; "Tatil ayarları" satırı `/scheduling` tatil yönetimine yönlendirilir, sahte sheet silinir.
  - `_showSubscriptionSheet` (sahte "Plus") → **kaldırılır**; yerine "Üyelik" satırı, P09 gelene kadar `Yakında` etiketiyle pasif gösterilir (yanıltıcı veri yok). (D-15)
  - `_showReportsSheet` (sahte 42/%94/%81) → **kaldırılır**; "Raporlar" satırı P08'e kadar pasif. (D-16)
- [ ] **Step 6: D-17 — sahte kullanıcı sentinel'lerini kaldır**
  `notifications_page.dart:18` ve `parent_home_page.dart:18`'deki `?? 'mock-teacher-user'` / `?? 'mock-parent-user'` ifadelerini kaldır; oturum yoksa ekran "Oturum bulunamadı" boş durumu gösterir ve `/login`'e yönlendirir.
- [ ] **Step 7: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 8: Doküman + commit**

`doc/pages/00_pages_index.md` (2 yeni sayfa), `doc/pages/more.md` (kaldırılan sahte bölümler), `doc/architecture/widgets.md` (yeni ortak widget varsa).
```bash
git add mobile doc
git commit -m "feat(mobile): ayarlar ekranlari gercek backend'e baglandi, sahte kartlar kaldirildi (D-14/D-15/D-16/D-17)"
```

---

### Task 6: Kapanış

- [ ] **Step 1: Uçtan uca** — Mobilde "Ödev hatırlatmaları"nı kapat → backend'de ödev oluştur → **bildirim gelmediğini** doğrula → tekrar aç → bildirim geliyor.
- [ ] **Step 2: Tam testler** — Run: `dotnet test EgitimUssu.slnx && cd mobile && flutter test` → yeşil.
- [ ] **Step 3: Dokümanlar** — `doc/modules/m15_settings.md` durumu 🟢; `doc/INDEX.md` §3 durum sütunu; `doc/modules/00_genel_bakis.md` modül tablosu; `doc/denetim/2026-09-02_eksik_analizi.md` M15-1..4, M11-5, D-14..D-17 → `✅ (P05)`.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P05 ayarlar kapanisi (M15-*/M11-5/D-14..17)"
```

---

## Kabul Kriterleri

- [ ] `GET /api/settings/users/{id}` kayıt yokken varsayılan ayarı 200 ile döndürüyor
- [ ] Başka kullanıcının ayarına erişim 403
- [ ] Kapatılan bildirim türü **gerçekten gönderilmiyor** (test + elle doğrulama)
- [ ] `TerminateOtherSessions` seçiliyken yeni giriş diğer oturumları iptal ediyor
- [ ] `PrivacyLevel` üç seviyesi veli dashboard'unda farklı sonuç veriyor
- [ ] Mobilde hiçbir ayar/abonelik/rapor ekranı sahte veri göstermiyor
- [ ] `dotnet test EgitimUssu.slnx` ve `flutter test` yeşil
