---
title: "M01 — Kimlik ve Erişim (Identity)"
summary: "Kullanıcı kimlik/oturum çekirdeği (kayıt/giriş/JWT/refresh/parola) uçtan uca çalışır; mobil sessiz refresh ve anahtar yönetimi açık"
tags: [modul, identity, kimlik, jwt, faz-0]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Identity/**
updated: 2026-09-02
---

# 🔐 Kimlik ve Erişim (Identity) Modülü (M01) — Detaylı Tasarım Dokümanı

> **PRD: M01 Kullanıcı & Rol** · **Faz: 0 — Temel Altyapı** · **Durum: 🟢 Yazıldı (kimlik çekirdeği uçtan uca çalışıyor; mobil refresh akışı ve anahtar yönetimi açık)**
>
> **Amaç:** EğitimÜssü platformundaki **tüm rollerin** (Admin, Öğretmen, Öğrenci, Veli) tek bir hesapla
> güvenli biçimde kayıt olması, giriş yapması ve oturumlarını yönetmesidir. Identity, diğer tüm modüllerin
> (Teachers, Students, Scheduling, Payments…) üzerine inşa edildiği **güven temelidir**: her korunan endpoint,
> bu modülün ürettiği JWT erişim token'ıyla `RequireAuthorization("AuthenticatedUser")` üzerinden doğrulanır.
>
> İlgili: [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`m02_teachers.md`](m02_teachers.md) · [`m03_students.md`](m03_students.md) · [`m09_parents.md`](m09_parents.md) · [`m15_settings.md`](m15_settings.md) · [`mimari_inceleme.md`](mimari_inceleme.md) · [`veri_modeli.md`](veri_modeli.md) · [`00_genel_bakis.md`](00_genel_bakis.md) · [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

`src/Modules/Identity/` altındaki **API / Application / Domain / Infrastructure** katmanları incelenerek aşağıdaki tablo çıkarılmıştır.

| Yetenek | Durum | Kanıt (kod) |
|---------|-------|-------------|
| Kullanıcı kaydı (çok rollü) | ✅ var | `RegisterUserCommandHandler` |
| E-posta + şifre ile giriş | ✅ var | `LoginUserCommandHandler` |
| JWT erişim token'ı üretimi (HMAC-SHA256) | ✅ var | `ITokenIssuer.Issue` |
| Refresh token (rotation + hash'li saklama) | ✅ var | `RefreshTokenCommandHandler`, `RefreshTokenSession` |
| Oturum kapatma (refresh iptal + token blacklist) | ✅ var | `LogoutCommandHandler` (refresh iptal) + **2026-07-01 (Y4):** logout, mevcut erişim token'ını `jti` ile `RedisTokenBlacklist`'e ekler → JWT `OnTokenValidated`'da reddedilir (**anlık iptal**, erken erişim token'ı iptali) |
| Şifre sıfırlama isteği + onayı | ✅ var | `RequestPasswordResetCommandHandler`, `ResetPasswordCommandHandler` |
| E-posta doğrulama isteği + onayı | ✅ var | `RequestEmailVerificationCommandHandler`, `ConfirmEmailVerificationCommandHandler` |
| Kullanıcı detayını getirme (self/admin) | ✅ var | `GetUserByIdQueryHandler` + `GetUserByIdQueryAuthorizer` |
| PBKDF2 parola hash'leme | ✅ var | `IPasswordHasher` (Infrastructure) |
| Rol üyelikleri (UserRoleMembership) | ✅ var | `UserAccount.RoleMemberships` |
| Rate limiting ("auth" politikası) | ✅ var | **Yol tabanlı, dağıtık (2026-07-01, Y4):** `DistributedRateLimitMiddleware` — `/api/identity/*` → `auth` (10/dk, IP-partition, Redis, fail-open). Eski `RequireRateLimiting` kaldırıldı |
| Kaba kuvvet (brute-force) hesap kilidi | ✅ var | **2026-07-01 (Y4):** `RedisLoginAttemptThrottle` — 5 başarısız girişte 15 dk hesap kilidi; `identity.too_many_attempts` → **429**. Redis yoksa fail-open |
| Outbox üzerinden domain event yayını | ✅ var | `UserRegisteredDomainEvent` + `ModuleDbContext` |
| **Mobil refresh token akışı (otomatik yenileme)** | 🔴 eksik | mimari_inceleme **Y3** — mobil `dio` istemcisinde 401 → `/refresh` interceptor'ı yok |
| **JWT imza anahtarının güvenli yönetimi** | 🔴 risk | mimari_inceleme **Y2** — imza anahtarı repoda/`appsettings`'te düz metin |
| Admin tarafı kullanıcı yönetimi (askıya alma/kapatma uçları) | 🔴 eksik | `Suspended`/`Closed` durumlarına geçiş için endpoint yok |
| Rol atama/kaldırma uçları | 🔴 eksik | Roller yalnızca kayıt anında belirleniyor |
| Telefon (SMS/OTP) doğrulama | 🔴 eksik | Yalnızca e-posta doğrulama var |

> **Özet:** Kimlik çekirdeği (kayıt → giriş → token → sıfırlama/doğrulama) **sunucu tarafında tamamdır**.
> Açık noktalar: (1) mobilde sessiz token yenileme, (2) imza anahtarının gizli yönetimi, (3) admin/operasyon uçları.

---

## 2. Domain Modeli

Tüm kaynak: `src/Modules/Identity/Domain/IdentityDomainModel.cs`. Şema: **`identity`** (`IdentityDbContext.SchemaName`).
Tablolar: `user_accounts`, `user_role_memberships`, `refresh_token_sessions`, `user_security_tokens`.

### 2.1 🟢 Mevcut (koddan) — `UserAccount` (AggregateRoot&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Kullanıcı kimliği (tüm modüllerde `UserId` referansı) |
| `Email` | string | Orijinal e-posta (trim'lenmiş) |
| `NormalizedEmail` | string | `Email.Trim().ToUpperInvariant()` — benzersizlik/arama anahtarı |
| `PasswordHash` | string | PBKDF2 hash (düz parola asla saklanmaz) |
| `FirstName`, `LastName` | string | Ad / soyad |
| `PhoneNumber` | string? | Telefon (opsiyonel) |
| `Status` | enum `UserAccountStatus` | Hesap durumu |
| `IsEmailConfirmed` | bool | E-posta doğrulandı mı |
| `IsProfileVerified` | bool | Profil/kimlik doğrulandı mı (rozet — admin akışı için ayrılmış) |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme zamanı (UTC) |
| `RoleMemberships` | List&lt;`UserRoleMembership`&gt; | Kullanıcının rolleri |
| `RefreshSessions` | List&lt;`RefreshTokenSession`&gt; | Aktif/iptal refresh oturumları |
| `SecurityTokens` | List&lt;`UserSecurityToken`&gt; | E-posta doğrulama / şifre sıfırlama token'ları |

**Davranışlar:** `ConfirmEmail(now)` → `IsEmailConfirmed=true`; `UpdatePassword(passwordHash, now)` → parola hash'ini günceller. Yapıcıda `UserRegisteredDomainEvent` yayılır.

### 2.2 🟢 Mevcut (koddan) — `UserRoleMembership` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Üyelik kimliği |
| `UserAccountId` | Guid | Sahip kullanıcı |
| `Role` | enum `UserRole` | Atanan rol |
| `AssignedOnUtc` | DateTime | Atama zamanı |

> Bir kullanıcı **birden fazla role** sahip olabilir (örn. hem `Teacher` hem `Parent`). Kayıtta `Roles` koleksiyonu `Distinct()` ile tekilleştirilir.

### 2.3 🟢 Mevcut (koddan) — `RefreshTokenSession` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Oturum kimliği |
| `UserAccountId` | Guid | Sahip kullanıcı |
| `RefreshTokenHash` | string | Refresh token'ın **hash'i** (düz token saklanmaz) |
| `DeviceName` | string? | Cihaz adı (çoklu cihaz oturumu) |
| `CreatedOnUtc` | DateTime | Oluşturma |
| `ExpiresOnUtc` | DateTime | Son geçerlilik (kayıt/giriş anında **+30 gün**) |
| `RevokedOnUtc` | DateTime? | İptal zamanı (logout / rotation / şifre sıfırlama) |

**Davranışlar:** `IsActive(now)` → `RevokedOnUtc is null && ExpiresOnUtc > now`; `Revoke(revokedOnUtc)` → ilk iptalde damgalanır (idempotent).

### 2.4 🟢 Mevcut (koddan) — `UserSecurityToken` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Token kimliği |
| `UserAccountId` | Guid | Sahip kullanıcı |
| `Purpose` | enum `SecurityTokenPurpose` | Token amacı |
| `TokenHash` | string | Token'ın **hash'i** |
| `CreatedOnUtc` | DateTime | Oluşturma |
| `ExpiresOnUtc` | DateTime | Son geçerlilik (e-posta doğrulama **+24 saat**, şifre sıfırlama **+1 saat**) |
| `UsedOnUtc` | DateTime? | Kullanıldığı an |

**Davranışlar:** `IsUsable(now)` → `UsedOnUtc is null && ExpiresOnUtc > now`; `MarkUsed(usedOnUtc)` → tek kullanımlık tüketim.

### 2.5 🟢 Mevcut (koddan) — Enum'lar (BİREBİR koddan)

| Enum | Değerler |
|------|----------|
| `UserRole` | `Admin = 1`, `Teacher = 2`, `Student = 3`, `Parent = 4` |
| `UserAccountStatus` | `PendingActivation = 1`, `Active = 2`, `Suspended = 3`, `Closed = 4` |
| `SecurityTokenPurpose` | `EmailVerification = 1`, `PasswordReset = 2` |

### 2.6 🟢 Mevcut (koddan) — Domain Event

```
UserRegisteredDomainEvent(Guid UserId, string Email, DateTime RegisteredOnUtc)
```
`UserAccount` yapıcısında yayılır; Outbox üzerinden integration event'e dönüşerek (gelecekte) Notifications gibi modüllerce dinlenebilir.

### 2.7 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Gerekçe |
|-------|---------|
| `UserStatusChangedDomainEvent` | Admin askıya alma/kapatma akışında diğer modüllere (örn. Scheduling iptal) sinyal vermek için |
| `LastLoginOnUtc` alanı (UserAccount) | Güvenlik/aktivite izleme |
| ~~`FailedLoginCount` + kilitlenme~~ | ✅ **Yapıldı (2026-07-01, Y4):** Redis tabanlı `RedisLoginAttemptThrottle` (hesap-bazlı, DB alanı gerektirmez). Rate limiting'i tamamlar |
| `PhoneNumberConfirmed` + `SecurityTokenPurpose.PhoneVerification` | SMS/OTP doğrulama için |

---

## 3. API Sözleşmesi

Tüm uçlar `RoutePrefix = /api/identity` altında ve `DistributedRateLimitMiddleware` tarafından **yol tabanlı `auth` politikasıyla** (IP-partition, Redis, fail-open) sınırlandırılmıştır (2026-07-01, Y4).
Yanıtlar `Result<T>` döner; hata kodları HTTP statüsüne `IdentityModule.ToHttpResult` ile eşlenir.

### 3.1 Mevcut Endpoint'ler ✅

| Yetenek | Method + Route | Auth | İstek DTO | Yanıt |
|---------|----------------|------|-----------|-------|
| Kayıt | `POST /register` | herkese açık | `RegisterUserRequest` | `AuthResponse` |
| Giriş | `POST /login` | herkese açık | `LoginUserRequest` | `AuthResponse` |
| Token yenile | `POST /refresh` | herkese açık | `RefreshTokenRequest` | `AuthResponse` |
| Şifre sıfırlama isteği | `POST /password-reset/request` | herkese açık | `PasswordResetRequest` | `200 OK` |
| Şifre sıfırlama onayı | `POST /password-reset/confirm` | herkese açık | `PasswordResetConfirmRequest` | `200 OK` |
| E-posta doğrulama isteği | `POST /email-verification/request` | herkese açık | `EmailVerificationRequest` | `200 OK` |
| E-posta doğrulama onayı | `POST /email-verification/confirm` | herkese açık | `EmailVerificationConfirmRequest` | `200 OK` |
| Oturum kapat | `POST /logout` | **AuthenticatedUser** | `LogoutRequest` | `200 OK` |
| Kullanıcı getir | `GET /users/{userId:guid}` | **AuthenticatedUser** | — | `UserAccountResponse` |
| Rol ata (yalnız Admin) | `POST /users/{userId:guid}/roles` | **AuthenticatedUser** + `AssignRolesCommandAuthorizer` (Admin) | `AssignRolesRequest` | `UserAccountResponse` |

> **K1 (2026-07-01):** `POST /register` yalnızca **self-servis roller** (`Teacher`, `Student`, `Parent`) kabul eder; istemci `Admin` gönderirse `identity.role_not_self_assignable` (400) döner. Yükseltilmiş rol ataması yalnızca yukarıdaki Admin-korumalı `POST /users/{id}/roles` ucuyla yapılır (varsayılan-deny). Bu, "anonim kayıtla anında Admin" açığını (denetim K1) kapatır.

**İstek/yanıt sözleşmeleri (koddan):**

```
RegisterUserRequest(string Email, string Password, string FirstName, string LastName,
                    string? PhoneNumber, IReadOnlyCollection<UserRole> Roles)
LoginUserRequest(string Email, string Password, string? DeviceName)
RefreshTokenRequest(string RefreshToken, string? DeviceName)
LogoutRequest(string RefreshToken)
PasswordResetRequest(string Email)
PasswordResetConfirmRequest(string Email, string Token, string NewPassword)
EmailVerificationRequest(string Email)
EmailVerificationConfirmRequest(string Email, string Token)

AuthResponse(Guid UserId, string Email, string FullName, IReadOnlyCollection<string> Roles,
             string AccessToken, DateTime ExpiresAtUtc, string RefreshToken)
UserAccountResponse(Guid UserId, string Email, string FirstName, string LastName, string? PhoneNumber,
                    string Status, bool IsEmailConfirmed, bool IsProfileVerified,
                    IReadOnlyCollection<string> Roles, DateTime CreatedOnUtc, DateTime UpdatedOnUtc)
```

> **Not:** `Roles`, istek/yanıtta string olarak taşınır (`UserRole.ToString()`), ham sayısal enum değil.

### 3.2 Hata Kodları → HTTP Eşleme (koddan)

| Hata kodu | HTTP | Mesaj |
|-----------|------|-------|
| `identity.duplicate_email` | **409** | Bu e-posta ile kayıtlı bir kullanıcı zaten var. |
| `identity.user_not_found` | **404** | Kullanıcı bulunamadı. |
| `identity.invalid_refresh_token` | **401** | Refresh token geçersiz veya süresi dolmuş. |
| `shared.forbidden` | **403** | Bu kaynağa erişim yetkiniz yok. |
| `identity.invalid_credentials` | 400 | E-posta veya şifre hatalı. |
| `identity.invalid_password` | 400 | Şifre en az 8 karakter olmalıdır. |
| `identity.missing_role` | 400 | En az bir kullanıcı rolü seçilmelidir. |
| `identity.user_inactive` | 400 | Kullanıcı hesabı aktif değil. |
| `identity.invalid_password_reset_token` | 400 | Şifre sıfırlama tokeni geçersiz. |
| `identity.invalid_email_verification_token` | 400 | E-posta doğrulama tokeni geçersiz. |
| `identity.invalid_request` | 400 | (Validator) Alanlar eksik veya hatalı. |

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [ ] `POST /refresh` için mobilde **otomatik yenileme** (dio interceptor) — sunucu ucu var, istemci akışı yok (**Y3**).
- [ ] `GET /me` — token'daki `sub`'a göre o anki kullanıcı (her seferinde `userId` taşımadan).
- [ ] `PUT /users/{userId}/status` (admin) — `Active`/`Suspended`/`Closed` geçişi.
- [x] `POST /users/{userId}/roles` (admin) — rol atama **eklendi (2026-07-01)**. `DELETE /users/{userId}/roles/{role}` (rol kaldırma) hâlâ önerilen.
- [ ] `POST /sessions/revoke-all` — kullanıcının tüm cihaz oturumlarını kapatma.
- [ ] `GET /sessions` — aktif cihaz oturumlarını listeleme (DeviceName ile).
- [ ] Telefon doğrulama uçları (SMS/OTP).

---

## 4. İş Kuralları

1. **Parola politikası:** En az **8 karakter** (`identity.invalid_password`). Kayıtta ve şifre sıfırlamada uygulanır.
2. **En az bir rol:** Kayıtta `Roles` boş olamaz (`identity.missing_role`); roller `Distinct()` ile tekilleştirilir.
3. **E-posta normalizasyonu:** Tüm aramalar `Email.Trim().ToUpperInvariant()` ile yapılır; benzersizlik `NormalizedEmail` üzerinden.
4. **Mükerrer e-posta:** Aynı normalize e-posta varsa kayıt `identity.duplicate_email` (409) ile reddedilir.
5. **Kayıt sonrası durum:** Yeni kullanıcı `Status = Active`, `IsEmailConfirmed = false`, `IsProfileVerified = false` ile oluşur; **24 saatlik** e-posta doğrulama token'ı üretilir ve doğrulama e-postası gönderilir.
6. **Giriş koşulu:** Parola PBKDF2 ile doğrulanır; hesap yalnızca `Active` **veya** `PendingActivation` ise giriş yapılabilir (`Suspended`/`Closed` → `identity.user_inactive`). Hatalı e-posta/şifre tek tip `identity.invalid_credentials` döner (kullanıcı sayımı/enumeration engeli).
7. **Refresh rotation:** `/refresh` çağrısında eski oturum `Revoke` edilir ve **yeni** bir refresh token üretilir (token rotation). Süre dolmuş/iptal edilmiş token `identity.invalid_refresh_token` (401) verir.
8. **Refresh ömrü:** Tüm refresh oturumları **30 gün** geçerlidir.
9. **Şifre sıfırlama gizliliği:** `password-reset/request`, kullanıcı **bulunsa da bulunmasa da** `200 OK` döner (e-posta enumeration engeli). Token **1 saat** geçerlidir.
10. **Şifre sıfırlama yan etkisi:** Başarılı sıfırlamada token "kullanıldı" işaretlenir, parola güncellenir ve **tüm aktif refresh oturumları iptal edilir** (tüm cihazlardan çıkış).
11. **E-posta doğrulama gizliliği:** `email-verification/request`, kullanıcı yoksa veya zaten doğrulanmışsa sessizce `200 OK` döner; aksi halde **24 saatlik** yeni token üretir.
12. **Token tek kullanımlık:** `UserSecurityToken` `MarkUsed` ile bir kez tüketilir; tekrar kullanım `IsUsable` kontrolünde başarısız olur.
13. **GetUserById yetkisi:** Yalnızca **admin** veya **kendi kaydı** (`isSelf`) erişebilir (`GetUserByIdQueryAuthorizer`); aksi halde `shared.forbidden` (403).
14. **Rate limiting + kilit (2026-07-01, Y4):** Tüm identity uçları yol tabanlı `"auth"` politikasıyla (IP başına 10/dk, Redis-dağıtık, fail-open) sınırlandırılır. Ek olarak `LoginUserCommandHandler` hesap-bazlı kilit uygular: 5 ardışık başarısız girişte hesap 15 dk kilitlenir → `identity.too_many_attempts` (**429**); başarılı girişte sayaç sıfırlanır.
15. **Token blacklist / anlık iptal (2026-07-01, Y4):** Erişim token'ları artık benzersiz `jti` claim'i taşır. Logout, `jti`'yi kalan ömrü boyunca `RedisTokenBlacklist`'e ekler; JWT `OnTokenValidated` her istekte blacklist'i kontrol eder ve iptal edilmiş token'ı `401` ile reddeder. Redis erişilemezse fail-open (token geçerli sayılır).

### 🔐 Güvenlik Notları (iyi yönler / riskler)

- ✅ **PBKDF2** ile parola hash'leme (`IPasswordHasher`) — düz parola hiçbir yerde saklanmaz.
- ✅ **HMAC-SHA256** imzalı JWT erişim token'ı (`ITokenIssuer`).
- ✅ Refresh ve güvenlik token'ları **hash'lenerek** saklanır (`ITokenProtector.Hash`); DB sızıntısında ham token ele geçmez.
- ✅ Token rotation + şifre sıfırlamada toplu oturum iptali.
- 🔴 **Y2 (mimari_inceleme):** JWT imza anahtarı repoda/`appsettings`'te düz metin — gizli yönetimi (env/secret store) gerekli, anahtar rotasyonu yok.
- 🔴 **Y3 (mimari_inceleme):** Mobilde refresh akışı yok; access token süresi dolunca kullanıcı yeniden giriş yapmak zorunda kalıyor.

---

## 5. Olay Akışı (Event-Driven)

```
Kayıt başarılı            → UserRegisteredDomainEvent (UserId, Email, RegisteredOnUtc)
                            → Outbox → (gelecek) Notifications: hoş geldin / doğrulama e-postası takibi
                            → (öneri) Teachers/Students: profil oluşturma için davet/yönlendirme
E-posta doğrulama token'ı  → IIdentityNotificationService.SendEmailVerificationAsync (kayıt + istek anında)
Şifre sıfırlama token'ı    → IIdentityNotificationService.SendPasswordResetAsync
Şifre sıfırlandı           → tüm aktif RefreshTokenSession.Revoke (toplu çıkış)
```

> Olaylar **Outbox pattern** ile güvenilir biçimde yayılır (`Shared/Infrastructure/Messaging`).
> Bildirim gönderimi `IIdentityNotificationService` arayüzü üzerinden soyutlanmıştır (gerçek e-posta/SMS sağlayıcısı Infrastructure'da takılır).

---

## 6. Mobil Ekranlar (mevcut + planlanan)

`mobile/lib/features/auth/` (flutter_bloc/Cubit + go_router + dio + get_it).

| Route | Sayfa | Durum | Açıklama |
|-------|-------|-------|----------|
| `/` | `WelcomePage` | ✅ | Karşılama; birincil renk `0xFF082B4F` |
| `/role-selection` | `RoleSelectionPage` | ✅ | Kayıt öncesi rol seçimi (`UserRole`) |
| `/login` | `LoginPage` | ✅ | E-posta + şifre girişi → `AuthResponse` saklanır |
| `/register` | `RegisterPage` | ✅ | Kayıt; seçilen rol(ler) `Roles` olarak gönderilir |

### Eksik / planlanan mobil ekranlar ⚠️
- [ ] **Şifremi unuttum** akışı (request + confirm ekranları) — sunucu ucu hazır.
- [ ] **E-posta doğrulama** ekranı / derin bağlantı (deep link) ile token onayı.
- [x] **Token yenileme interceptor'ı** (dio): 401 → `/refresh` → tekrar dene; başarısızsa `/login`'e yönlendir — ✅ 2026-07 (Y3), `mobile/lib/core/network/token_refresh_interceptor.dart`.
- [x] **Güvenli depolama:** access + refresh token `flutter_secure_storage` ile saklanıyor — ✅ 2026-07 (Y7), `mobile/lib/core/storage/token_storage.dart`.
- [ ] **Cihaz oturumları** ekranı (aktif `DeviceName` listesi + uzaktan çıkış).

---

## 7. Kabul Kriterleri

- [x] Kullanıcı bir veya birden çok rolle kayıt olabilir; mükerrer e-posta 409 ile engellenir.
- [x] Geçerli kimlikle giriş JWT erişim token'ı + refresh token döndürür; pasif hesap girişi engellenir.
- [x] Refresh token rotation ile yenilenir; iptal/süre dolumu 401 verir.
- [x] Şifre sıfırlama uçtan uca çalışır ve tüm oturumları iptal eder; e-posta enumeration sızdırılmaz.
- [x] E-posta doğrulama token'ı tek kullanımlık ve 24 saat geçerlidir.
- [x] `GET /users/{id}` yalnızca admin veya kendi kaydı için erişilebilir.
- [x] **Self-register yalnız `Teacher`/`Student`/`Parent` kabul eder; `Admin` reddedilir** (denetim K1 kapandı).
- [x] **Rol ataması yalnızca Admin'e açık `POST /users/{id}/roles` ucundadır** (varsayılan-deny).
- [ ] **Mobilde sessiz token yenileme uçtan uca** çalışır (Y3 kapanır).
- [ ] **JWT imza anahtarı gizli yönetimine taşınır** (Y2 kapanır).
- [ ] Admin, kullanıcıyı `Suspended`/`Closed` durumuna geçirebilir.

---

## 8. Eksikler ve Yapılacaklar (öncelik sırasıyla)

1. **Mobil refresh akışı (Y3)** — dio interceptor + güvenli token depolama; en yüksek kullanıcı etkisi.
2. **JWT imza anahtarı gizli yönetimi (Y2)** — secret store/env, anahtar rotasyonu, üretim öncesi zorunlu.
3. **`GET /me` ucu** — token'a dayalı geçerli kullanıcı; mobil/web için pratik.
4. **Admin kullanıcı yönetimi** — durum geçişi (`PUT /users/{id}/status`) + `UserStatusChangedDomainEvent`.
5. **Rol yönetimi uçları** — kayıt sonrası rol ekle/çıkar (örn. öğretmen aynı zamanda veli olduğunda).
6. **Oturum yönetimi** — `GET /sessions`, `POST /sessions/revoke-all`.
7. ✅ **Kaba kuvvet sertleştirme (2026-07-01, Y4)** — Redis tabanlı hesap kilidi (`RedisLoginAttemptThrottle`) + dağıtık rate limiting; DB alanı gerektirmedi.
8. **Telefon (SMS/OTP) doğrulama** — `PhoneNumberConfirmed` + yeni `SecurityTokenPurpose`.

---

## 9. İlişkili Dokümanlar

- Rollerin uçtan uca yolculuğu → [`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md)
- Profil katmanları (kayıt sonrası) → [`m02_teachers.md`](m02_teachers.md), [`m03_students.md`](m03_students.md), [`m09_parents.md`](m09_parents.md)
- Kullanıcı ayarları & güvenlik → [`m15_settings.md`](m15_settings.md)
- Bildirim teslimatı (e-posta/SMS token) → [`m11_notifications.md`](m11_notifications.md)
- Güvenlik açıkları ve öncelikli düzeltmeler (Y2, Y3) → [`mimari_inceleme.md`](mimari_inceleme.md)
- Aggregate ER şeması ve modüller arası referanslar → [`veri_modeli.md`](veri_modeli.md)
- Genel durum ve endpoint envanteri → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*Kimlik ve Erişim (Identity) Modülü (M01) — Detaylı Tasarım | Güncelleme: 2026-09-02 (F-01: mobil refresh interceptor + secure storage kodda — işaretlendi) · 2026-07-01*
