---
title: "M15 — Ayarlar & Güvenlik"
summary: "Kullanıcı bildirim/gizlilik/güvenlik ayarları; oku/yaz uçları + study-sharing + sahiplik authorizer kodda mevcut"
tags: [modul, settings, gizlilik, bildirim, faz-0]
status: "🟡"
authority: code
code_refs:
  - src/Modules/Settings/**
updated: 2026-09-03
---

# ⚙️ Ayarlar ve Güvenlik Modülü (M15) — Detaylı Tasarım Dokümanı

> **Modül kodu:** M15 · **Proje:** EğitimÜssü (EgitimUssu) · **Platform:** .NET 9 modüler monolit (`src/Modules/Settings`) + Flutter mobil
> **PRD:** M15 · **Faz:** 0+ · **Durum:** 🟡 Domain var; **ayar oku/yaz** uçları (`GET`/`PUT /api/settings/users/{userId}`) + **çalışma-verisi paylaşımı** ucu + sahiplik authorizer'ları **kodda mevcut** (P05 Task 1); rol bazlı profil düzenleme hâlâ eksik
> **Mimari:** CQRS + Outbox; PostgreSQL (`settings` şeması), `UserId` UNIQUE (kullanıcı başına tek ayar kaydı)
> **Marka rengi (mobil):** `0xFF082B4F`

> Kullanıcının **bildirim tercihleri**, **gizlilik/veri paylaşımı** ve **oturum/güvenlik** ayarlarını yönettiği modül. Tercihleri özellikle Bildirim (M11) tüketir; gizlilik bayraklarını Study/Veli/Raporlama okur.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Var olan

| Bileşen | Konum | Açıklama |
|---------|-------|----------|
| Domain aggregate | `src/Modules/Settings/Domain/SettingsDomainModel.cs` | `UserSetting : AggregateRoot<Guid>` + `SetStudySharing(...)` + `UpdatePreferences(...)` + `UserSetting.Default(userId, nowUtc)` fabrikası |
| DbContext | `src/Modules/Settings/Infrastructure/SettingsDbContext.cs` | `DbSet<UserSetting>`, şema `settings`, tablo `user_settings` |
| Migration | `.../Migrations/20260504210120_InitialCreate.cs`, `.../20260701181041_AddOutboxRetryFields.cs` | `user_settings` tablosu + Outbox retry alanları |
| CQRS feature | `src/Modules/Settings/Application/SettingsFeatures.cs` | `SetStudySharingCommand` (+validator/handler) · **`GetUserSettingsQuery`** + `GetUserSettingsQueryHandler` · **`UpdateUserSettingsCommand`** + validator + handler · `UserSettingsResponse` |
| Sahiplik authorizer | `SettingsFeatures.cs` → `SettingsAuthorizer`, **`GetUserSettingsQueryAuthorizer`**, **`UpdateUserSettingsCommandAuthorizer`** | Üçü de ortak `SettingsOwnership.Check` mantığını kullanır: yalnızca kendi kullanıcısı (veya `Admin`) → aksi halde `shared.forbidden` (K3 uygulanmış) |
| Repository | `src/Modules/Settings/Infrastructure/UserSettingRepository.cs` | `IUserSettingRepository` (GetByUserId / Add / SaveChanges) |
| Read-contract | `src/Modules/Settings/Infrastructure/StudentPrivacyDirectory.cs` | `IStudentPrivacyDirectory` (`Shared/Contracts`) — diğer modüllere `ShareStudyDataWith*` bayraklarını okutur (kayıt yoksa ikisi de açık varsayılır) |
| Read-contract (M11-5) | `src/Modules/Settings/Infrastructure/UserNotificationPreferencesDirectory.cs` | **`IUserNotificationPreferences`** (`Shared/Contracts`) — Notifications gönderim turu bildirim tercihlerini buradan okur; kayıt yoksa `AllEnabled` |
| Read-contract (M15-3) | `src/Modules/Settings/Infrastructure/SessionPolicyDirectory.cs` | **`ISessionPolicyDirectory`** (`Shared/Contracts`) — Identity giriş akışı "diğer oturumları sonlandır" politikasını buradan okur; kayıt yoksa `false` |
| Module tanımı | `src/Modules/Settings/API/SettingsModule.cs` | `GET /api/settings/status` (placeholder) + `GET`/`PUT /api/settings/users/{userId:guid}` + `PUT /api/settings/users/{userId:guid}/study-sharing` (hepsi `RequireAuthorization("AuthenticatedUser")`) |

### 🔴 Eksik olan

- ✅ **(P05 Task 1) Oku/yaz uçları eklendi:** `GET /api/settings/users/{userId}` (kayıt yoksa varsayılan, 404 değil) ve `PUT /api/settings/users/{userId}` (bildirim + gizlilik seviyesi + oturum politikası tek gövdede). Rol-bazlı profil `PUT` ucu hâlâ **yok**.
- **Domain event yok** — `UpdatePreferences()` eklendi ama `UserSettingsUpdatedDomainEvent` **yok**; tercih değişikliği olay yayınlamıyor (tüketiciler tercihi okuma anında sorguluyor).
- ✅ **(P05/M11-5) Notifications entegrasyonu yapıldı** — `IUserNotificationPreferences` sözleşmesi Settings tarafından uygulanır, `NotificationDispatchProcessor` gönderim turunda tüketir ([`m11_notifications.md`](m11_notifications.md) §5.1).
- **Rol bazlı profil düzenleme yok** — öğretmen/öğrenci/veli profilleri ve bildirim izinlerinin tek yerden yönetimi henüz yok.
- **Mobil bağlama kısmî** — mobil `more` feature'ı (Ayarlar/hesap) var; yalnızca çalışma-verisi paylaşımı backend'e bağlanabilir, bildirim/güvenlik ekranlarının backend'i yok.

> Not: `study-sharing` ucu sahiplik authorizer'ı + `AuthenticatedUser` politikasıyla korunur; `SetStudySharingCommandHandler` kayıt yoksa **varsayılanlarla upsert** eder (tüm bildirimler açık, `Standard`, `KeepLatest`).

---

## 2. Domain Modeli

### 🟢 Mevcut — `UserSetting` (AggregateRoot)

Kaynak: `src/Modules/Settings/Domain/SettingsDomainModel.cs` (alanlar/enum'lar birebir kodla doğrulanmıştır)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Birincil anahtar |
| `UserId` | `Guid` | Identity kullanıcısı — DB'de **UNIQUE** |
| `PushNotificationsEnabled` | `bool` | Push bildirim ana anahtarı |
| `EmailNotificationsEnabled` | `bool` | E-posta bildirim ana anahtarı |
| `UpcomingLessonReminderEnabled` | `bool` | Yaklaşan ders hatırlatması |
| `HomeworkReminderEnabled` | `bool` | Ödev hatırlatması |
| `PaymentReminderEnabled` | `bool` | Ödeme hatırlatması |
| `WeeklySummaryEnabled` | `bool` | Haftalık özet |
| `ShareStudyDataWithTeacher` | `bool` | Çalışma verisini öğretmenle paylaş |
| `ShareStudyDataWithParent` | `bool` | Çalışma verisini veliyle paylaş (gizlilik) |
| `PrivacyLevel` | `enum PrivacyLevel` | `Standard = 1`, `Limited = 2`, `Hidden = 3` |
| `SessionTerminationPolicy` | `enum SessionTerminationPolicy` | `KeepLatest = 1`, `TerminateOtherSessions = 2` |
| `LastUpdatedOnUtc` | `DateTime` | Son güncelleme |

**Enum'lar (kodla birebir):**
- `PrivacyLevel { Standard = 1, Limited = 2, Hidden = 3 }`
- `SessionTerminationPolicy { KeepLatest = 1, TerminateOtherSessions = 2 }`

**Davranış:** Constructor + `SetStudySharing(shareWithTeacher, shareWithParent, updatedOnUtc)` metodu (`ShareStudyDataWith*` + `LastUpdatedOnUtc` günceller). Diğer kategoriler için güncelleme metodu **ve domain event henüz yok** (tüm setter'lar `private`).

**Kalıcılık (DB):** şema `settings`, tablo `user_settings`.
- `PrivacyLevel` ve `SessionTerminationPolicy` `string` enum dönüşümü (maks. 32), zorunlu; `LastUpdatedOnUtc` zorunlu.
- Index: `UserId` **UNIQUE** → kullanıcı başına tek ayar kaydı.

> Bu alanlar iki PRD ihtiyacını karşılar: **bildirim tercihleri** (M09 / M11) ve **gizlilik kontrolü** (Study "Veli ile Paylaşım" — öğrencinin verisini kimle paylaşacağını seçmesi).

### ⚠️ Önerilen — Domain genişletmeleri

- **Güncelleme davranışları + event'ler:** `UpdateNotificationPreferences(...)`, `UpdatePrivacy(...)`, `UpdateSecurity(...)` metotları; her biri `LastUpdatedOnUtc` günceller ve `UserSettingsUpdatedDomainEvent` (kategori bilgisiyle) üretir → M11 tetiklenebilir.
- **Varsayılan kayıt fabrikası:** `UserSetting.CreateDefault(userId)` — kayıt yoksa "tüm bildirimler açık, `Standard` gizlilik, `KeepLatest`" ile döner.
- **Rol bazlı profil ayarları (PRD genişletme):** öğretmen/öğrenci/veli profil alanları (görünen ad, avatar, iletişim tercihleri) ve **bildirim izinleri** tek ekrandan yönetilebilmeli. Bu, ilgili rol modüllerinin profiliyle (Teachers/Students/Parents) hizalanarak Settings üzerinden düzenleme akışı sunar.
- **Üyelik (M17) ilişkili tercihler:** reklam gösterimi (free'de açık), bildirim/limit tercihleri üyelik paketine bağlı olarak kısıtlanır/genişler ([`m17_membership.md`](m17_membership.md)).

---

## 3. API Sözleşmesi

### Mevcut ✅

| Yetenek | Method + Route | Yetki |
|---------|----------------|-------|
| Sağlık/placeholder | `GET /api/settings/status` | — (placeholder) |
| Ayarları oku | `GET /api/settings/users/{userId:guid}` | `AuthenticatedUser` + sahiplik authorizer (self/Admin) |
| Tercihleri güncelle (upsert) | `PUT /api/settings/users/{userId:guid}` | `AuthenticatedUser` + sahiplik authorizer (self/Admin) |
| Çalışma-verisi paylaşımı (upsert) | `PUT /api/settings/users/{userId:guid}/study-sharing` | `AuthenticatedUser` + sahiplik authorizer (self/Admin) |

> **Gövde:** `SetStudySharingRequest(bool ShareWithTeacher, bool ShareWithParent)` → `SetStudySharingCommand`. Kayıt yoksa varsayılanlarla oluşturulur, varsa `SetStudySharing` ile güncellenir. Yetkisizde `shared.forbidden` (403), doğrulama hatası `settings.invalid_request` (400). Bu uç, Veli **V-B gizlilik filtresi** akışını besler ([`m09_parents.md`](m09_parents.md)).

### Eksik / Önerilen ⚠️

```
PUT  /api/settings/users/{userId}/profile           → rol bazlı profil alanları (öğretmen/öğrenci/veli)
```

> `GET /users/{userId}` ile bildirim/gizlilik/güvenlik `PUT`'u **tek uçta birleştirildi** (P05 Task 1): ayrı `notifications`/`privacy`/`security` uçları yerine tek `UpdateUserSettingsRequest` gövdesi kullanılır — mobil ekran tek çağrıyla kaydeder.

**`GET`/`PUT /api/settings/users/{userId}` yanıtı — `UserSettingsResponse`:**

| Alan | Tip | Not |
|------|-----|-----|
| `userId` | `Guid` | |
| `pushNotificationsEnabled` … `weeklySummaryEnabled` | `bool` × 6 | Bildirim bayrakları |
| `shareStudyDataWithTeacher` / `shareStudyDataWithParent` | `bool` | Yalnız okunur — `study-sharing` ucundan değişir |
| `privacyLevel` | `string` | `Standard` / `Limited` / `Hidden` |
| `sessionTerminationPolicy` | `string` | `KeepLatest` / `TerminateOtherSessions` |
| `lastUpdatedOnUtc` | `DateTime` | |

> **Kayıt yoksa:** `GET` **200** + varsayılan nesne döner (`UserSetting.Default`) — kalıcı kayıt oluşturulmaz. `PUT` kayıt yoksa oluşturur ve `ShareStudyDataWith*` alanlarını **değiştirmez**.

> **Yetki:** Kullanıcı yalnızca **kendi** ayarını okuyup yazabilmeli. Sahiplik authorizer'ı (oturum `UserId == route userId` ya da Admin) + varsayılan reddet guard'ı zorunlu (K3). Yetki kaydı eksik kalırsa **sessiz açık erişim** riski vardır.

---

### 3.1 "Diğer oturumları sonlandır" (M15-3)

`SessionTerminationPolicy.TerminateOtherSessions` seçiliyken **her başarılı giriş**, o kullanıcının
diğer refresh oturumlarını iptal eder — kullanıcı yalnız son giriş yaptığı cihazda açık kalır.

| Adım | Nerede |
|------|--------|
| Politikayı oku | `ISessionPolicyDirectory.ShouldTerminateOtherSessionsAsync(userId)` → Settings'teki `SessionPolicyDirectory` |
| Uygula | `LoginUserCommandHandler` — yeni oturum eklendikten **sonra** `UserAccount.RevokeOtherSessions(session.Id, now)` |
| Kapsam | Yalnız `login`. `refresh` akışı kendi oturumunu döndürdüğü için etkilenmez |

> Domain kuralı: `RevokeOtherSessions` korunacak oturuma dokunmaz ve **zaten iptal edilmiş**
> kayıtların zaman damgasını değiştirmez (`RefreshTokenSession.Revoke` idempotenttir).
> Ayar kaydı yoksa politika `KeepLatest` sayılır → mevcut çok-cihaz davranışı korunur.

## 4. İş Kuralları

1. **Kullanıcı başına tek kayıt:** `UserId` UNIQUE; ilk `GET`'te kayıt yoksa **varsayılan** (tümü açık, `Standard`, `KeepLatest`) döner veya lazımken oluşturulur.
2. **Sahiplik:** Bir kullanıcı yalnızca kendi `UserSetting` kaydını görüntüleyip değiştirebilir (Admin istisna). Veli, çocuğun ayarını **değiştiremez** (yalnızca kendi tercihlerini yönetir).
3. **Bildirim ana anahtarı önceliklidir:** `PushNotificationsEnabled == false` ise tür bazlı bayraklar açık olsa bile push gönderilmez (M11 buna saygı gösterir).
4. **Gizlilik bayrakları görünürlüğü belirler:** `ShareStudyDataWithParent/Teacher == false` ise çalışma/performans verisi ilgili panele/rapora **yansımaz** (M08/M09/M14).
5. **`PrivacyLevel`:** `Standard` tam görünürlük, `Limited` kısıtlı, `Hidden` minimum paylaşım. Reşit olmayan öğrenci/veli senaryolarında varsayılanlar KVKK'ya göre belirlenir.
6. **Oturum politikası:** `TerminateOtherSessions` seçiliyse, login sırasında kullanıcının diğer refresh oturumları (Identity `RefreshTokenSession`) sonlandırılmalı.
7. **Güncelleme zaman damgası:** her değişiklikte `LastUpdatedOnUtc` güncellenir; (önerilen) `UserSettingsUpdatedDomainEvent` yayınlanır.

---

## 5. Olay Akışı

```
Kullanıcı ayar günceller (PUT /settings/users/{id}/notifications|privacy|security)
   → sahiplik authorizer (K3) → UserSetting güncelle + LastUpdatedOnUtc
      → (önerilen) UserSettingsUpdatedDomainEvent  ──(Outbox)──▶ Notifications (M11) tercihleri yeniden okur

Bildirim üretimi (M11):
   Notification oluştur/gönder ÖNCESİ → ilgili UserSetting bayraklarını kontrol et
      → kapalıysa kanal/tür atlanır

Veri paylaşımı (M08/M09/M14):
   Study/Rapor okuması → ShareStudyDataWith* + PrivacyLevel kontrolü → görünürlük

Güvenlik (Identity):
   Login → SessionTerminationPolicy == TerminateOtherSessions ? diğer oturumları sonlandır
```

> ⚠️ Outbox kapalıyken (K1) ayar değişiklik event'leri yayılmaz; M11 entegrasyonu senkron okuma ile de kurulabilir (her bildirimde `UserSetting`'i sorgulamak).

---

## 6. Mobil Ekranlar (Flutter)

- **Ayarlar ana ekranı (`more`/Settings):** bölümler — Bildirimler, Gizlilik, Güvenlik, Profil. Mevcut `more` feature'ı gerçek `/api/settings` backend'ine bağlanmalı. Marka rengi `0xFF082B4F`.
- **Bildirim tercihleri:** push/e-posta ana anahtarları + tür bazlı (yaklaşan ders, ödev, ödeme, haftalık özet) switch'ler → `PUT /notifications`.
- **Gizlilik ve veri paylaşımı:** "Çalışma verimi öğretmenle/veliyle paylaş" switch'leri + `PrivacyLevel` seçici → `PUT /privacy`.
- **Güvenlik:** "Yeni girişte diğer oturumları kapat" (`SessionTerminationPolicy`) → `PUT /security`.
- **Profil düzenleme (rol bazlı):** öğretmen/öğrenci/veli profil alanları ve bildirim izinleri ([`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md)) → `PUT /profile`.

---

## 7. Kabul Kriterleri

- [ ] `GET /settings/users/{userId}` kullanıcının ayarını döner; kayıt yoksa varsayılan döner.
- [ ] Bildirim/gizlilik/güvenlik tercihleri `PUT` ile güncellenebiliyor; `LastUpdatedOnUtc` değişiyor.
- [ ] Sahiplik authorizer'ı başka kullanıcının ayarına erişimi reddediyor (K3); Admin istisna doğru.
- [ ] M11, bildirim göndermeden önce ilgili `UserSetting` bayraklarına saygı gösteriyor (kapalı tür gönderilmiyor).
- [ ] `ShareStudyDataWith*` bayrakları Study/Veli/Rapor görünürlüğünü gerçekten etkiliyor.
- [ ] `TerminateOtherSessions` seçiliyken login diğer oturumları sonlandırıyor (Identity).
- [ ] Rol bazlı profil + bildirim izinleri mobil ekrandan düzenlenebiliyor.
- [ ] Mobil `more`/Settings ekranı gerçek backend'e bağlı.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **Kalan CRUD endpoint'leri + CQRS feature** (get + bildirim/güvenlik/profil update) — `study-sharing` ucu ve sahiplik authorizer'ı ✅ mevcut; diğer kategoriler eksik.
2. **Domain güncelleme metotları + event'ler** — `SetStudySharing` ✅ var; `UpdateNotifications/Privacy/Security` + `UserSettingsUpdatedDomainEvent` eksik.
3. **Notifications (M11) entegrasyonu** — tercihlere göre filtreleme/kanal seçimi.
4. **Rol bazlı profil düzenleme** — öğretmen/öğrenci/veli profilleri + bildirim izinleri tek ekrandan.
5. **Mobil ayarlar ekranı** — `more` feature'ını gerçek backend'e bağla (şu an yalnızca `study-sharing` bağlanabilir).
6. **`PrivacyLevel`'in Study/Rapor akışına bağlanması** — `ShareStudyDataWith*` ✅ `IStudentPrivacyDirectory` read-contract'ı ile diğer modüllere açık (M08/M09/M14); `PrivacyLevel` kademesi henüz uygulanmıyor.
7. **Oturum sonlandırma politikasının Identity login akışına bağlanması.**
8. **Üyelik (M17) ile reklam/limit tercihleri ilişkisi.**

---

## 9. İlişkili Dokümanlar

- Tercihleri tüketen modül → [`m11_notifications.md`](m11_notifications.md)
- Gizlilik/veri paylaşımı bağlamı (çalışma verisi) → [`m08_study.md`](m08_study.md)
- Veli görünürlüğü → [`m09_parents.md`](m09_parents.md)
- Rapor görünürlüğü (gizlilik bayrakları) → [`m14_reporting.md`](m14_reporting.md)
- Üyelik / reklam-limit tercihleri → [`m17_membership.md`](m17_membership.md)
- Yetki guard'ı (K3), Outbox (K1) → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli / şema → [`veri_modeli.md`](veri_modeli.md)
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Roller (profil düzenleme) → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/admin.md`](../roles/admin.md)

---

*Ayarlar ve Güvenlik Modülü (M15) — EğitimÜssü Detaylı Tasarım | Güncelleme: 2026-09-03*
