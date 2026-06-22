# 🔬 Mimari İnceleme — Hatalar ve Eksikler

> **Tarih:** 2026-06-21 · **Kapsam:** Backend (.NET 9 modüler monolit) + Flutter mobil
> **Yöntem:** Çekirdek altyapı (CQRS, Outbox, DI, Auth) + tüm modüllerin domain/feature/API katmanları + mobil çekirdek incelendi.

> **Genel değerlendirme:** Mimari **sağlam temellere** oturuyor (temiz katman ayrımı, modül başına DbContext,
> Outbox envelope, CQRS, mimari testleri, doğru parola/JWT kullanımı). Ancak **birkaç kritik footgun ve güvenlik
> açığı** var; bunların bir kısmı sistemi "sessizce" çalışmaz/güvensiz hale getiriyor.

**Önem skalası:** 🔴 Kritik · 🟠 Yüksek · 🟡 Orta · ⚪ Düşük/Hijyen

---

## 🔴 KRİTİK

### K1 — Outbox varsayılan olarak KAPALI → integration event'ler hiç yayınlanmıyor
`src/API.Host/appsettings.json` **ve** `appsettings.Development.json`:
```json
"Outbox": { "DispatchEnabled": false }
```
`OutboxDispatcher.ExecuteAsync` ilk satırda bunu görüp **çıkıyor** (`return`). Sonuç:
- `LessonSessionCompletedDomainEvent` → Assignments takip akışı **tetiklenmez**.
- `LessonScheduledDomainEvent` → Notifications hatırlatma kaydı **oluşmaz**.
- Modüller arası tüm event tabanlı akış **ölü**.

Olay mesajları `outbox_messages` tablosunda birikir ama asla işlenmez. Demo/dev dahil her ortamda kapalı.

**Öneri:** En azından Development'ta `true` yap; prod için bilinçli bir karar + izleme ekle. Açık unutulmasın diye startup'ta bir uyarı log'u bas.

---

### K2 — Outbox: mesaj başına hata izolasyonu / retry / dead-letter yok
`OutboxProcessor.DispatchPendingAsync`:
```csharp
foreach (var item in batch) { ...; await eventBus.PublishAsync(integrationEvent, ...); }
await outboxStore.MarkProcessedAsync(batch, ...);   // tüm batch tek seferde işaretlenir
```
Bir handler **exception fırlatırsa**, `MarkProcessedAsync` hiç çalışmaz → **tüm batch** (zaten başarıyla yayınlanmış mesajlar dahil) bir sonraki turda yeniden işlenir → **çift teslimat (duplicate delivery)**. Ayrıca tek bir "zehirli mesaj" (poison message) sonsuza dek tüm kuyruğu bloklar.

- `OutboxMessage.Error` kolonu tanımlı ama **hiçbir yerde doldurulmuyor** (sadece `null`'a set ediliyor).
- Retry sayacı / max deneme / dead-letter yok.

**Öneri:** Mesaj başına try/catch; başarılı olanı işaretle, başarısıza `Error` + `RetryCount` yaz; eşik aşılınca dead-letter'a taşı. Handler'ları **idempotent** yap (bkz. Y4).

---

### K3 — Yetkilendirme "opt-in" ve emniyet ağı yok → eksik kayıt = sessiz açık erişim
`CommandDispatcher`/`QueryDispatcher`, DI'dan `ICommandAuthorizer<T>` **kayıtlı olanları** çalıştırır.
Bir komut/sorgu için authorizer **kaydedilmezse**, döngü boş döner ve istek **doğrudan handler'a geçer** —
endpoint seviyesindeki `RequireAuthorization("AuthenticatedUser")` dışında hiçbir sahiplik/rol kontrolü olmaz.

**Somut kanıt:** `GetTeacherProfileByUserIdQuery` için **handler kayıtlı ama authorizer YOK**
(`grep`: `IQueryAuthorizer<GetTeacherProfileByUserIdQuery>` hiçbir DI dosyasında yok). Yani **giriş yapmış herhangi bir
kullanıcı herhangi bir öğretmenin profilini** okuyabilir. (Öğretmen profili eşleştirmede zaten herkese açık olacak
olsa bile, bu **bilinçli** değil — desenden kaynaklı bir boşluk.)

**Öneri:** Startup'ta her kayıtlı `ICommandHandler<T>`/`IQueryHandler<T>` için bir authorizer var mı diye doğrulayan bir guard ekle
(yoksa fail-fast). Veya "varsayılan reddet" davranışı: authorizer yoksa açıkça `[AllowAnonymousCommand]` benzeri bir işaret zorunlu olsun.

---

## 🟠 YÜKSEK

### Y1 — Öğretmen kendi profilini "doğrulanmış" yapabiliyor (yetki yükseltme)
`UpsertTeacherProfileRequest` → `ToUpdateCommand` → `UpdateTeacherProfileCommand.IsVerified` **client'tan alınıyor**
ve `UpdateTeacherProfileCommandHandler` bunu doğrudan `profile.Update(..., command.IsVerified, ...)` ile yazıyor.
Authorizer yalnızca **sahiplik** kontrol ediyor (öğretmen kendi profili mi), rol/yetki değil.

Sonuç: Bir öğretmen `PUT /api/teachers/profiles/{userId}` ile `isVerified: true` göndererek **kendini doğrulanmış öğretmen
olarak işaretleyebilir** → eşleştirmede sahte güven rozeti.

> Not: **Create** yolu güvenli — handler `false` set ediyor. Açık yalnızca **Update** yolunda.

**Öneri:** `IsVerified`'ı update DTO/command'inden çıkar; doğrulama yalnızca admin/doğrulama akışıyla değişsin (ayrı endpoint + `Admin` rolü).

### Y2 — JWT imza anahtarı ve DB parolası repoda commit'li + zayıf varsayılanlar
- `appsettings.json`: `"Jwt:SigningKey": "change-this-development-signing-key"`, `Postgres ... Password=postgres`.
- `JwtOptions.SigningKey` kod-içi varsayılanı: `"replace-with-a-long-development-key"`.

Prod'da override edilmezse token'lar **taklit edilebilir** (forge). Anahtar repoda olduğu için sızıntı riski.

**Öneri:** Sırları environment / secret manager'dan al; varsayılanı boş bırakıp **prod'da yoksa fail-fast** yap. `appsettings.json`'dan gerçek değerleri çıkar.

### Y3 — Mobil: token yenileme (refresh) akışı yok → kullanıcı 60 dk'da bir atılıyor
- `TokenStorage` arayüzü yalnızca **access token** saklıyor (refresh token saklanmıyor).
- `ApiClient` interceptor'ı 401'de sadece "unauthorized" event yayınlıyor (logout), **silent refresh denemiyor**.
- Backend'de `POST /api/identity/refresh` **var ama mobil kullanmıyor**.

Access token 60 dk geçerli → her saat başı kullanıcı login ekranına düşer.

**Öneri:** Refresh token'ı da güvenli sakla; 401'de refresh endpoint'iyle sessiz yenileme yapan bir interceptor ekle.

### Y4 — Handler'lar idempotent değil (at-least-once teslimat varsayımıyla çelişir)
Outbox at-least-once teslimat yapar (K2 düzeltilse bile en az bir kez + olası tekrar). Mevcut integration event handler'ları
(örn. `LessonSessionCompletedIntegrationEventHandler` → ödev/takip oluşturma) **tekrarı engellemiyor** → çift kayıt riski.

**Öneri:** İşlenen `EventId`'leri modül bazında "inbox/processed" tablosunda tut; tekrar gelen event'i atla.

### Y5 — Bildirimler gerçekten gönderilmiyor (yalnızca "gönderildi" işaretleniyor)
`NotificationDispatchProcessor.DispatchDueRemindersAsync`, vadesi gelen hatırlatmaları bulup `reminder.MarkSent(...)`
diyor ama **FCM/APNs'e hiçbir şey göndermiyor**. Push altyapısı (PRD Faz 0.7) henüz bağlı değil.

**Öneri:** Faz 1 kabul kriteri için gerçek push entegrasyonu (FCM) + cihaz token kaydı eklenmeli.

---

## 🟡 ORTA

### O1 — Rate limiting yalnızca Identity'de uygulanıyor
`Program.cs`'te `"auth"` ve `"default"` limiter tanımlı ama yalnızca `IdentityModule` `.RequireRateLimiting("auth")`
kullanıyor. `"default"` (120/dk) **hiçbir endpoint'e bağlı değil** → diğer tüm iş endpoint'leri sınırsız.
**Öneri:** `CreateModuleGroup` içinde varsayılan limiter'ı uygula veya global limiter tanımla.

### O2 — Sorgu authorizer'ları varlığı iki kez yüklüyor (çift DB sorgusu)
Örn. `GetStudentProfileByIdQuery`: authorizer profili yükler (erişim kontrolü), ardından handler **aynı** profili tekrar yükler.
**Öneri:** Authorizer'da yüklenen entity'yi handler'a taşı (request context cache) veya erişim kontrolünü handler'a entegre et.

### O3 — `MarkProcessedAsync` `DateTime.UtcNow` kullanıyor (IClock değil)
Kod tabanının geri kalanı test edilebilirlik için `IClock` kullanırken `EfOutboxStore` doğrudan `DateTime.UtcNow` çağırıyor — tutarsız.

### O4 — `CommandDispatcher`/`QueryDispatcher` ağır `dynamic` + reflection
Her dispatch'te `MakeGenericType` + `dynamic` cast + `GetMethod(...).Invoke`. Cache yok, pipeline behavior soyutlaması yok.
Çalışıyor ama kırılgan ve her çağrıda reflection maliyeti var.
**Öneri:** Tip başına derlenmiş delegate cache; veya MediatR benzeri pipeline.

### O5 — Modüller arası okuma (read-model) mekanizması yok
Payments özeti, (gelecek) veli paneli, eşleştirme arama gibi senaryolar birden çok modülün verisini birleştirecek.
Şu an modüller arası iletişim yalnızca integration event ile; **senkron sorgu/kontrat** mekanizması yok.
**Öneri:** Modüller arası public read kontratları (`Shared/Contracts`) veya ACL/projeksiyon deseni tanımla.

### O6 — `LessonSchedule` durum yaşam döngüsü korunmuyor
`Status` (`Draft/Planned/Cancelled/Completed`) var ama yalnızca `Cancel()` davranışı mevcut.
`Complete`/`Reschedule` yok; geçersiz durum geçişleri (örn. iptal edilmiş dersi tekrar planlama) engellenmiyor.
Ayrıca **ders çakışması kontrolü** yok (PRD M04 gereği).

### O7 — Test kapsamı çok düşük
Yalnızca **5 test dosyası**: mimari testleri, health, bir öğretmen workflow integration testi, `Result`/`PagedResult` unit testleri.
Handler'lar, authorizer'lar, outbox, domain davranışları **test edilmiyor**. Yukarıdaki güvenlik açıkları (Y1, K3) bir test olsa yakalanırdı.

---

## ⚪ DÜŞÜK / HİJYEN

- **D1** — Stale build artefaktları: `src/**/bin/Debug/net10.0/` klasörleri duruyor (hedef framework aslında `net9.0`). `bin/obj` `.gitignore`'da olmalı.
- **D2** — Kök dizinde `tmp_api_stdout.log`, `tmp_api_stderr.log` commit'li — temizlenmeli.
- **D3** — Placeholder dosyalar: `Shared/*/Class1.cs` ve boş `AssemblyReference.cs` dosyaları kaldırılmalı.
- **D4** — Doküman tutarsızlığı: `ai_ready_architecture.md` ".NET 8", `00_genel_bakis.md` ".NET 10" diyor; gerçek hedef **.NET 9** (`net9.0`, `global.json` → SDK 9.0.311). Dokümanlar düzeltilmeli.
- **D5** — `StudentProfileQueryAuthorizer` 3 ayrı arayüzle 3 kez `AddScoped` ediliyor → 3 ayrı instance (zararsız ama gereksiz).
- **D6** — İskelet modüller (`Study`, `Parents`, `Matching`, `Reviews`, `ProgressTracking`, `Reporting`) yalnızca `/status` döndürüyor — beklenen (yol haritası), ama API yüzeyinde "var gibi" görünüyor.

---

## ✅ İyi Yapılmış Olanlar (Korunmalı)

- Temiz katman ayrımı + **mimari testleriyle** zorlanıyor (Application → Infrastructure referansı yasak vb.).
- Modül başına ayrı `DbContext` + ayrı schema + ayrı migration → gerçek veri izolasyonu.
- Parola **PBKDF2** (`PasswordHasher<>`), JWT **HMAC-SHA256**, refresh token **hash'lenerek** saklanıyor, kriptografik rastgele token üretimi.
- Outbox **envelope** deseni (EventId/Name/SourceModule/Payload) round-trip tutarlı; `DispatchingEventBus` doğru şekilde kendi scope'unu açıyor (captive dependency yok).
- `Result<T>` + `Error` deseni ve merkezi HTTP hata eşleme (`ApiErrorHttpResults`, ProblemDetails middleware).
- Mobil: token `FlutterSecureStorage`'da; feature bazlı temiz katman.

---

## 📋 Öncelikli Düzeltme Sırası (Önerilen)

1. **Y1** — Öğretmen self-verify açığını kapat (hızlı + güvenlik). 
2. **K1** — Outbox'ı aç + startup uyarısı.
3. **K3 / GetTeacherProfile authorizer** — eksik authorizer'ları ekle + startup guard.
4. **K2 + Y4** — Outbox hata izolasyonu/retry + idempotent handler (inbox tablosu).
5. **Y2** — Sırları config'ten çıkar, prod'da fail-fast.
6. **Y3** — Mobil refresh token akışı.
7. **O7** — Yukarıdakiler için regresyon testleri.

---

*Mimari İnceleme | Güncelleme: 2026-06-21 — Düzeltmeler yapıldıkça bu doküman güncellenmeli.*
