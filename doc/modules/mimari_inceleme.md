# 🔬 Mimari İnceleme — Hatalar ve Eksikler

> **İlk inceleme:** 2026-06-21 · **Güncelleme:** 2026-06-24 · **Kapsam:** Backend (.NET 9 modüler monolit) + Flutter mobil
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
`OutboxProcessor.DispatchPendingAsync`: bir handler **exception fırlatırsa**, `MarkProcessedAsync` hiç çalışmaz → **tüm batch** (zaten başarıyla yayınlanmış mesajlar dahil) yeniden işlenir → **çift teslimat**. Tek bir "zehirli mesaj" tüm kuyruğu bloklar. `OutboxMessage.Error` kolonu tanımlı ama doldurulmuyor; retry/max-deneme/dead-letter yok.

**Öneri:** Mesaj başına try/catch; başarılıyı işaretle, başarısıza `Error` + `RetryCount` yaz; eşik aşılınca dead-letter. Handler'ları **idempotent** yap (bkz. Y4).

---

### K3 — Yetkilendirme "opt-in" ve emniyet ağı yok → eksik kayıt = sessiz açık erişim
`CommandDispatcher`/`QueryDispatcher`, DI'dan **kayıtlı** authorizer'ları çalıştırır. Bir komut/sorgu için authorizer **kaydedilmezse**, istek doğrudan handler'a geçer — endpoint'teki `RequireAuthorization("AuthenticatedUser")` dışında sahiplik/rol kontrolü olmaz.

**Somut kanıt:** `GetTeacherProfileByUserIdQuery` için handler kayıtlı ama **authorizer YOK** → giriş yapmış herhangi bir kullanıcı herhangi bir öğretmenin profilini okuyabilir.

**Öneri:** Startup'ta her handler için authorizer var mı diye doğrulayan **fail-fast guard**; veya "varsayılan reddet" + açık `[AllowAnonymous...]` işareti.

---

## 🟠 YÜKSEK

### ✅ Y1 — Öğretmen kendi profilini "doğrulanmış" yapabiliyor (yetki yükseltme) — **Düzeltildi 2026-06-24 (backend) / 2026-06-26 (client)**
`IsVerified`, `UpsertTeacherProfileRequest`, `UpdateTeacherProfileCommand` ve `TeacherProfile.Update()` metodundan kaldırıldı. Client `toUpdatePayload()` artık `isVerified` göndermez; regresyon testi eklendi.
> **Kalan:** Admin-only `PUT /profiles/{userId}/verification` endpoint + `TeacherVerifiedDomainEvent` henüz eklenmedi.

### Y2 — JWT imza anahtarı ve DB parolası repoda + zayıf varsayılanlar
`appsettings.json`: `Jwt:SigningKey = "change-this-development-signing-key"`, Postgres `Password=postgres`; kod-içi varsayılan `"replace-with-a-long-development-key"`. Prod'da override edilmezse token **taklit edilebilir**.

**Öneri:** Sırları environment/secret manager'dan al; varsayılanı boş bırak + **prod'da yoksa fail-fast**.

### ✅ Y3 — Mobil: token yenileme (refresh) akışı yok → kullanıcı 60 dk'da bir atılıyor — **Düzeltildi 2026-06-25**
`TokenStorage`'a `readRefreshToken`/`writeRefreshToken` eklendi. `TokenRefreshInterceptor` (`QueuedInterceptorsWrapper`) 401'de `POST /api/identity/refresh` ile sessiz yenileme yapar; yenileme de başarısız olursa `_onUnauthorized` callback'i tetikler. `ApiClient` lazy closure ile `AuthRepository.refreshSession()`'ı çağırır (döngüsel bağımlılık önlendi).

### Y4 — Handler'lar idempotent değil (at-least-once teslimat varsayımıyla çelişir)
Mevcut integration event handler'ları (örn. `LessonSessionCompletedIntegrationEventHandler`) tekrarı engellemiyor → çift kayıt riski.

**Öneri:** İşlenen `EventId`'leri modül bazında "inbox/processed" tablosunda tut; tekrarı atla.

### Y5 — Bildirimler gerçekten gönderilmiyor (yalnızca "gönderildi" işaretleniyor)
`NotificationDispatchProcessor.DispatchDueRemindersAsync` `reminder.MarkSent(...)` diyor ama **FCM/APNs'e hiçbir şey göndermiyor**. Push altyapısı (PRD Faz 0.7) bağlı değil (bkz. [`m11_notifications.md`](m11_notifications.md)).

**Öneri:** Gerçek push entegrasyonu (FCM) + cihaz token kaydı.

---

## 🟡 ORTA

### O1 — Rate limiting yalnızca Identity'de uygulanıyor
`"auth"` ve `"default"` limiter tanımlı ama yalnızca Identity `.RequireRateLimiting("auth")` kullanıyor; `"default"` (120/dk) hiçbir endpoint'e bağlı değil. **Öneri:** `CreateModuleGroup`'ta varsayılan limiter veya global limiter.

### O2 — Sorgu authorizer'ları varlığı iki kez yüklüyor (çift DB sorgusu)
Örn. `GetStudentProfileByIdQuery`: authorizer profili yükler, handler **aynı** profili tekrar yükler. **Öneri:** request-context cache veya erişim kontrolünü handler'a entegre et.

### O3 — `MarkProcessedAsync` `DateTime.UtcNow` kullanıyor (IClock değil)
Kod tabanı test edilebilirlik için `IClock` kullanırken `EfOutboxStore` doğrudan `DateTime.UtcNow` çağırıyor — tutarsız.

### O4 — `CommandDispatcher`/`QueryDispatcher` ağır `dynamic` + reflection
Her dispatch'te `MakeGenericType` + `dynamic` + `GetMethod(...).Invoke`. Cache/pipeline yok. **Öneri:** tip başına derlenmiş delegate cache veya MediatR benzeri pipeline.

### O5 — Modüller arası okuma (read-model) mekanizması yok
Payments özeti, veli paneli (M09), eşleştirme arama (M12), raporlama (M14) birden çok modülün verisini birleştirecek. Şu an yalnız integration event var; **senkron sorgu/kontrat** yok. **Öneri:** `Shared/Contracts` read kontratları veya ACL/projeksiyon. (Bu, M09/M12/M14 için **kritik önkoşul**.)

### O6 — `LessonSchedule` durum yaşam döngüsü kısmen
✅ _Düzeltme (2026-06-24):_ **Ders çakışması kontrolü ASLINDA mevcut** — `HasTeacherConflictAsync` → `scheduling.teacher_conflict` (409) ve `scheduling.invalid_range` (400) koddan doğrulandı; teacher-lesson liste endpoint'i `startAtUtc`/`endAtUtc` tarih filtresi alıyor.
✅ _Düzeltme (2026-06-26):_ **`Planned → Completed` geçişi eklendi** — `Complete(updatedOnUtc)` domain metodu, `CompleteLessonScheduleCommand`/handler, `POST /lessons/{id}/complete` endpoint ve mobil `completeLesson` cubit metodu + UI butonu eklendi.
**Hâlâ eksik:** `Reschedule` geçişi; online ders linki (`MeetingUrl`), tekrar açılımı, tatil/blackout ve **öğrenci tarafı çakışma önceliği** (bkz. [`m04_scheduling.md`](m04_scheduling.md)).

### O7 — Test kapsamı çok düşük
Yalnız ~5 test dosyası. Handler/authorizer/outbox/domain davranışları test edilmiyor; Y1, K3 bir test olsa yakalanırdı.

### O8 — Dosya yükleme/depolama servisi yok (YENİ — yükseltilebilir 🟠)
`Shared/`'da **`IFileStorage`/blob soyutlaması yok**; dosya URL'leri (`Assignment.AttachmentUrl`, `TeacherProfile.ProfilePhotoUrl`) düz string olarak kabul ediliyor, gerçek yükleme/saklama katmanı yok. Bu, promp.txt'teki şu özellikleri **bloklar**: öğrenci **ödev yükleme** (`AssignmentSubmission`), ders **kaynağı (kaynak)** paylaşımı (`LessonResource`), profil fotoğrafı.
**Öneri:** `Shared/Infrastructure`'da `IFileStorage` soyutlaması + sağlayıcı (yerel/S3/Azure Blob) + yükleme endpoint deseni (bkz. [`m06_assignments.md`](m06_assignments.md)).

---

## ⚪ DÜŞÜK / HİJYEN
- **D1** — Stale build artefaktları: `src/**/bin/Debug/net10.0/` klasörleri (hedef `net9.0` olmalı). `bin/obj` `.gitignore`'da olmalı; csproj hedef framework tutarlılığı kontrol edilmeli.
- **D2** — Kök dizinde `tmp_api_stdout.log`, `tmp_api_stderr.log` — temizlenmeli.
- **D3** — Placeholder dosyalar: `Shared/*/Class1.cs` ve boş `AssemblyReference.cs` kaldırılmalı.
- **D4** — ✅ _Düzeltildi._ Doküman tutarsızlığı (.NET 8/10) gerçek hedef **.NET 9** ile hizalandı.
- **D5** — `StudentProfileQueryAuthorizer` 3 arayüzle 3 kez `AddScoped` → 3 instance (gereksiz).
- **D6** — İskelet modüller (`Study`, `Parents`, `Matching`, `Reviews`, `ProgressTracking`, `Reporting`) yalnız `/status` döndürüyor — beklenen (yol haritası), tasarımları [`m08`](m08_study.md)/[`m09`](m09_parents.md)/[`m10`](m10_progress_tracking.md)/[`m12`](m12_matching.md)/[`m13`](m13_reviews.md)/[`m14`](m14_reporting.md)'te.

---

## ➕ Yeni Modüller İçin Mimari Notlar (promp.txt genişlemesi)
- **Mesajlaşma (M16):** Kendi şeması + DbContext; gerçek-zamanlı için (ileride) SignalR/WebSocket düşünülebilir, ama ilk sürüm poll/REST + bildirim yeterli. Modül sınırı: katılımcı doğrulama (öğretmen↔öğrenci/veli) Identity/ilişki verisiyle.
- **Üyelik (M17):** Entitlement (premium/limit) bilgisi integration event ile diğer modüllere yayılmalı (modül sınırı — doğrudan DB okuma yok). Reklam istemci tarafı + config.
- **Geri Bildirim/Şikayet (M18):** M13 `ReviewFlag` + M16 mesaj şikayeti ile **ortak moderasyon kuyruğu**; karar `ModerationAction` event'iyle yayılır.
- **O5 önkoşulu:** M09/M14/M12 read-model gerektirir → modüller arası okuma mekanizması bu modüllerden önce çözülmeli.

---

## ✅ İyi Yapılmış Olanlar (Korunmalı)
- Temiz katman ayrımı + **mimari testleriyle** zorlanıyor (Application → Infrastructure yasak vb.).
- Modül başına ayrı `DbContext` + şema + migration → gerçek veri izolasyonu.
- Parola **PBKDF2**, JWT **HMAC-SHA256**, refresh token **hash'li**, kriptografik rastgele token.
- Outbox **envelope** deseni round-trip tutarlı; `DispatchingEventBus` kendi scope'unu açıyor.
- `Result<T>` + `Error` + merkezi HTTP hata eşleme (ProblemDetails).
- Mobil: token `FlutterSecureStorage`'da; feature bazlı temiz katman.

---

## 📋 Öncelikli Düzeltme Sırası (Önerilen)
1. **Y1** — Öğretmen self-verify açığını kapat (hızlı + güvenlik).
2. **K1** — Outbox'ı aç + startup uyarısı.
3. **K3** — Eksik authorizer'lar + startup guard.
4. **K2 + Y4** — Outbox hata izolasyonu/retry + idempotent handler.
5. **Y2** — Sırları config'ten çıkar, prod fail-fast.
6. **Y3** — Mobil refresh token akışı.
7. **O5 + O8** — Modüller arası okuma + dosya depolama (yeni modüllerin önkoşulu).
8. **O7** — Regresyon testleri.

---

*Mimari İnceleme | Güncelleme: 2026-06-26 — Düzeltmeler yapıldıkça güncellenmeli.*
