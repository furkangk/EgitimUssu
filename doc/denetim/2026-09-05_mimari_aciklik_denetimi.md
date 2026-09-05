---
title: "Mimari Açıklık Denetimi — 2026-09-05 (A serisi)"
summary: "Backend + mobil mimarisinde koddan doğrulanmış 19 açıklık; 18'i kapatıldı, A-19 ürün kararı bekliyor"
tags: [denetim, mimari, guvenlik, dayaniklilik]
authority: source
updated: 2026-09-05
---

# 🔎 Mimari Açıklık Denetimi — 2026-09-05

> **Kapsam:** `src/` (558 C# dosyası, ~72k satır) + `mobile/lib/` (192 Dart dosyası, ~55k satır), statik okuma.
> **Yöntem:** Kapalı görünen ya da hiç kaydedilmemiş bulguları aramak. Kapatıldığı doğrulanan
> K1–K5 / Y1–Y5 / O1–O8 maddeleri tekrarlanmadı.
> **Dal:** `fix/mimari-aciklar` (taban: `6b6795a`, P13 Task 1 sonrası).
> **Özet tablo:** [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) § A serisi.

| Önem | Adet | Durum |
|------|------|-------|
| 🔴 Kritik | 2 | ✅ kapatıldı |
| 🟠 Yüksek | 5 | ✅ kapatıldı |
| 🟡 Orta | 9 | ✅ kapatıldı |
| ⚪ Hijyen | 3 | 2 ✅ · 1 açık (**A-19**) |

---

## Ortak örüntü: sessiz başarısızlık

Kritik iki bulgu da aynı sınıftan: **bir koruma çalışmıyor ama hiçbir yerde iz bırakmıyor.**

- **A-01** — Redis'e bağlı dört koruma (hız sınırı, token kara listesi, giriş kilidi, idempotency)
  bilinçli olarak fail-open'dır (ADR-0004). Sorun fail-open'ın kendisi değil, **fark edilemez**
  olmasıydı: `RedisOptions` varsayılanı `"localhost:6379"` olduğu için `Redis__Configuration`
  verilmeyen bir üretim dağıtımı sorunsuz açılıyor, `ConfigurationHealthCheck` yalnız "boş mu"
  diye baktığı için `/health/ready` `Healthy` diyordu. JWT, veritabanı, e-posta, push ve depolama
  için startup fail-fast koruması varken Redis için yoktu — tutarsızlık.
- **A-02** — Hız sınırı, istemcinin **kendi gönderdiği** `X-Forwarded-For` başlığının ilk girdisine
  güveniyordu. Her istekte farklı bir değer göndermek yeni bir kova açıyor, `/api/identity/*`
  için tanımlı 10/dk sınırı tümüyle atlanabiliyordu. A-01 ile birleştiğinde brute-force'a karşı
  geriye tek savunma olarak hesap-bazlı kilit kalıyordu — o da Redis'e bağımlı.

Bu ikisi birlikte, "korumaların var olduğu ama açık olmayabileceği" bir durum üretiyordu.
Düzeltmenin ekseni de bu oldu: **eksik yapılandırma sessizce geçilemez** (guard), **kapanan koruma
ölçülür** (`egitimussu.redis.unavailable`), **kimlik güvenilir kaynaktan okunur** (`ClientIpResolver`).

---

## Bulgular ve çözümler

### 🔴 A-01 — Redis sessizce düşerse dört koruma birden kapanıyor
`RedisOptionsGuard` (üretimde boş/loopback reddedilir; geliştirmede serbest) · `RedisConnectionHealthCheck`
(gerçek `PING`; sonuç bilinçli olarak `Degraded` — fail-open bir tasarım kararı olduğundan Redis kesintisi
konteyneri yeniden başlatmamalı) · `egitimussu.redis.unavailable` sayacı, kapanan korumanın adıyla etiketli
(`ratelimit` | `blacklist` | `login-throttle` | `idempotency`). Varsayılan boşa çekildi; geliştirme adresi
`appsettings.Development.json`'a taşındı.

### 🔴 A-02 — `X-Forwarded-For` doğrulanmıyordu
`ClientIpResolver`: IP, `RateLimiting:TrustedProxyHopCount` kadar **sondan** okunur — o girdiyi bizim
önümüzdeki proxy yazmıştır. Render tek proxy arkasındadır (`1`); yerelde başlık yok sayılır (`0`).
Hız sınırı ve giriş kilidi artık aynı kaynağı kullanır; iki yerde ayrı yazılsaydı biri düzeltilip
diğeri açık kalabilirdi.

### 🟠 A-03 — Integration event payload'ı log'a düşüyordu
`DispatchingEventBus` her olayı **payload'ıyla** Information seviyesinde logluyordu.
`UserRegisteredDomainEvent` kullanıcının e-postasını, `MessageSentDomainEvent` öğretmen–öğrenci
**özel mesaj önizlemesini** taşır. P13'ün `SensitiveDataMask`'i yalnız sorgu dizesini kapsıyor.
Seviye `Debug`'a indirildi, payload çıkarıldı.

### 🟠 A-04 — E-posta istek yolunda, senkron
`SaveChanges` sonrası SMTP'ye gidiliyor ve hata yeniden fırlatılıyordu: SMTP kapalıyken kullanıcı
**oluşmuş ve commit edilmiş** olmasına rağmen istek 500 dönüyor, tekrar deneme `duplicate_email`
veriyordu. `outgoing_emails` kuyruğu + `IEmailQueue` (iş yazımıyla **aynı transaction'da** stage —
`SaveChanges` çağırmaz) + `OutgoingEmailDispatcher` (retry/backoff/dead-letter). Gönderilmiş satırlar
retention ile budanır: gövdeleri token taşır.

### 🟠 A-05 — Modül sınırını aşan yazımda tutarlılık boşluğu
`ClaimParentInvite` önce bağı (Parents şeması), **sonra** daveti (Students şeması) yazıyordu; iki ayrı
`DbContext`, kapsayıcı transaction yok. İkinci yazım düşerse bağ kurulmuş ama davet `Pending` kalmış
oluyor, **aynı kod ikinci bir veli tarafından tekrar kullanılabiliyordu**.
`TryConsumeAsync` (atomik koşullu güncelleme — yarışı kaybeden `false` alır) + `ReleaseAsync` (telafi).
Davet **önce** tüketilir; bağ yazılamazsa geri verilir.

### 🟠 A-06 — Parola sıfırlama erişim token'ını iptal etmiyordu
Refresh oturumları kapatılıyor ama access token 60 dk daha geçerli kalıyordu; aynı boşluk rol
değişiminde de vardı (roller JWT claim'inden okunur). JWT'ye `iat` eklendi; `ITokenBlacklist`'e
kullanıcı bazlı iptal damgası (`jti` kara listesi + damga **tek Redis turunda**, istek başına ek
gecikme yok). Application portu `ITokenRevocationService` — katman kuralı gereği port Application'da,
uygulama Infrastructure'da.

### 🟠 A-07 — Arka plan işleri çoklu-instance'a hazır değildi
Beş servis düz `while + Task.Delay` idi; kilit yoktu. İkinci instance = **çift push bildirimi**,
çift gecikme olayı — her instance farklı `EventId` ürettiği için inbox dedup'ı da yakalayamıyordu.
`job_leases` + `JobLeaseStore` (`INSERT … ON CONFLICT DO UPDATE … WHERE süresi dolmuş`) +
`LeasedBackgroundService` ortak tabanı. **Neden Redis değil veritabanı:** Redis'e bağlı korumalar
fail-open'dır; Redis düştüğünde kilit "alınmış" sayılıp iş yine koşar ve çift bildirim tam da o an
üretilirdi.

> ⚠️ **Kalan operasyonel gerçek (kod dışı):** Render `plan: free` 15 dk hareketsizlikte uyur —
> trafik yokken saatlik taramalar ve outbox dispatch **hiç koşmaz**. Kira bunu çözmez; ücretli plan
> ya da ayrı bir worker gerekir.

### 🟡 A-08 — Kimlik yolunda sınırsız eager loading
Depo her çağrıda `RefreshSessions` + `SecurityTokens` koleksiyonlarının tamamını yüklüyordu
(30 gün ömürlü, hiç silinmeyen satırlar; üç `Include` → kartezyen büyüme). Filtreli `Include`
(yalnız etkin oturum / kullanılabilir token) + girişte kullanıcıya özel budama.

### 🟡 A-09 — Outbox/inbox hiç budanmıyordu
Tek `HasIndex(ProcessedOnUtc)` claim sorgusunu karşılamıyordu. Kısmi indeks
`ix_outbox_messages_pending (NextAttemptUtc, OccurredOnUtc) WHERE bekleyen` +
`Outbox:RetentionDays` (14) budaması. Dead-letter satırlarına dokunulmaz — incelenmek için oradalar.

### 🟡 A-10 — Kuyruk derinliği göstergesi modülleri ayırmıyordu
`RecordOutboxPending` etiketsiz bir gauge'dı; dispatcher 15 modülü tek turda dolaştığı için dışa
aktarılan değer **son modülünki** oluyordu. `module` etiketi eklendi (kardinalite 15).

### 🟡 A-11 — Üyelik okuması hatayı yutup Free dönüyordu
Ödeme yapmış kullanıcı geçici bir DB sorununda sessizce premium'unu kaybediyordu.
`egitimussu.entitlement.fallback` sayacı eklendi; `OperationCanceledException` artık yutulmuyor.

### 🟡 A-12 — Idempotency kilidi yavaş isteklerde bozuluyordu
30 sn'lik işaret yenilenmiyordu; 30 sn'yi aşan istek sürerken işaret düşüyor, aynı anahtarla gelen
tekrar `Proceed` alıp iş **iki kez** koşuyordu. TTL 5 dk + `ReleaseAsync`: 2xx dışı yanıt ya da
istisna işareti hemen bırakır (eskiden "geçici hatalar tekrar denenebilir" niyeti yalnız TTL dolduğu
için gerçekleşiyordu).

### 🟡 A-13 — Yetkilendirme HTTP katmanında opt-in'di
`CreateModuleGroup` korumasız grup döndürüyordu; `Matching`/`Reviews` hiç `RequireAuthorization`
çağırmamıştı. Grup **varsayılan-deny** yapıldı; anonim uçlar istisnayı `AllowAnonymous` ile açıkça
bildirir. Regresyon: `ModuleGroupDefaultDenyTests`.

### 🟡 A-14 — OpenAPI üretimde açıktı
`/openapi/v1.json` kimliksiz erişilebilir durumda tüm API yüzeyini yayınlıyordu. Yalnız `Development`.

### 🟡 A-15 — Refresh token yeniden-kullanımı tespit edilmiyordu
İptal edilmiş token tekrar sunulduğunda yalnız 401 dönülüyordu; ele geçirme normal bir oturum sonu
gibi görünüyordu. Artık tüm oturumlar kapatılır + erişim token'ları iptal edilir. **Süresi dolmuş**
(iptal edilmemiş) token ihlal sayılmaz.

### 🟡 A-16 — Giriş kilidi tek boyutluydu
Yalnız e-posta bazlıydı: (1) e-postayı bilen herkes 5 hatalı denemeyle kullanıcıyı 15 dk kilitleyebiliyordu
(**DoS**), (2) her hesaba tek deneme yapan **spraying** sayaca hiç takılmıyordu.
Artık iki boyutlu: (hesap + kaynak IP) 5/15dk, (kaynak IP) 20/15dk. Küresel hesap kilidi bilinçli
olarak kaldırıldı — hangi eşiğe konursa konsun DoS'a açıktır (OWASP da sert hesap kilidini önermez).
Başarılı giriş yalnız hesap sayacını sıfırlar; kaynak sayacı durur ki saldırgan kendi hesabına girip
sayacını temizleyemesin.

### ⚪ A-17 — HTTP sertleştirme yoktu
`SecurityHeadersMiddleware` (`nosniff`, `DENY`, `no-referrer`, dar CSP, CORP; `Server`/`X-Powered-By`
kaldırılır) + yapılandırmayla açılan adlandırılmış CORS politikası (varsayılan boş — bugünkü mobil-only
durumda hiçbir köken açılmaz; planlanan Angular istemciden önce hazır).

### ⚪ A-18 — Bağlantı dayanıklılığı yoktu
`EnableRetryOnFailure` + `CommandTimeout(60)`; yinelenen `AddScoped(typeof(TContext))` kaydı kaldırıldı.
Retry stratejisi kullanıcı-başlatımlı transaction'ı reddettiği için `IdempotentIntegrationEventHandler`'ın
iş birimi `CreateExecutionStrategy` içine alındı — yeniden denemede `ChangeTracker` temizlenir, yoksa
`ApplyAsync` ikinci kez stage ederdi. **Bu tuzağı entegrasyon testleri yakaladı**, derleme değil.

### ⚪ A-19 — E-posta doğrulaması zorlanmıyor · **AÇIK**
Kayıt anında access + refresh token verilir, `LoginUserCommandHandler` `PendingActivation` durumunu
kabul eder, `IsEmailVerified` hiçbir yetkilendirmede okunmaz. Doğrulama akışı çalışıyor ama sonucu
bir şeyi açmıyor. **Ürün kararı:** bilinçliyse dokümana yazılmalı; değilse kapı (ödeme/mesajlaşma
gibi uçlarda) authorizer katmanında kurulmalı. Mevcut doğrulanmamış hesapların akışını değiştireceği
için bu turda uygulanmadı.

---

## Doğrulama

| Paket | Öncesi | Sonrası |
|-------|--------|---------|
| Mimari | 4 | 4 |
| Birim | 683 | 711 |
| Entegrasyon (gerçek Postgres + Redis) | 44 | 52 |

`dotnet build -warnaserror` → 0 uyarı / 0 hata. Yeni entegrasyon testleri gerçek Postgres'e karşı
koşar: iş kirasının karşılıklı dışlaması ve süreli devralması, outbox/inbox retention'ın dead-letter'a
dokunmaması, e-posta kuyruğunun kayıt akışında dolması, modül gruplarının varsayılan-deny olması.

**Üretilen migration'lar (her biri 15 modül):** `AddOutboxPendingIndex`, `AddJobLeases`,
`AddOutgoingEmails`. Toplu üretim: `scripts/add-migration-all.sh`.

---

## Bekleyen doküman işi

Bu tur `doc/architecture/backend.md`'ye **dokunmadı**: paralel bir oturum aynı dosyayı düzenliyordu
(C-10 API sürümleme). Merge sonrası oraya taşınacak başlıklar:

- `LeasedBackgroundService` + `job_leases` — zamanlayıcılı modül işlerinin kanonik deseni
- `IEmailQueue` / `OutgoingEmailDispatcher` — e-postanın outbox deseni
- `ClientIpResolver` + `RateLimiting:TrustedProxyHopCount` — ters-proxy altında istemci kimliği
- `SecurityHeadersMiddleware` + `Cors:AllowedOrigins`
- `CreateModuleGroup` varsayılan-deny sözleşmesi

Ayrıca [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) O5'teki "senkron çapraz-modül
okuma hâlâ yasak" ifadesi kodla çelişiyor (21 senkron kontrat, ikisi yazma portu) — kural yeniden
yazılmalı.

---

*Mimari Açıklık Denetimi | Güncelleme: 2026-09-05*
