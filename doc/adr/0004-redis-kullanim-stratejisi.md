# ADR-0004: Redis kullanım stratejisi (dağıtık cache & rate limiting)

- **Durum:** Kabul edildi (Accepted) — **Seçenek B**; 2026-07-01 uygulandı (Aşama 1/Y4). Limiter fallback: **fail-open**.
- **Tarih:** 2026-06-30 (güncelleme: 2026-07-01)
- **Uygulama:** Dağıtık rate limiting (`DistributedRateLimitMiddleware`), idempotency (`IdempotencyMiddleware`), token blacklist (`RedisTokenBlacklist`), login kilidi (`RedisLoginAttemptThrottle`) — tümü `ResilientRedisExecutor` üzerinden fail-open. **Cache (öncelik 4) henüz yapılmadı.**
- **Karar vericiler:** Backend ekibi / operasyon
- **İlgili:** Denetim bulgusu Y4 (rate limiter partition'sız + in-memory). Bkz. `src/Shared/Infrastructure/ServiceCollectionExtensions.cs:36-38`, `render.yaml:27`.

## Bağlam ve Problem

- **Redis kayıtlı ama hiç kullanılmıyor.** `IRedisConnectionFactory`/`LazyRedisConnectionFactory` DI'a ekleniyor (`ServiceCollectionExtensions.cs:38`) fakat kod tabanında **hiçbir yerden enjekte/çağrılmıyor** (doğrulandı). Ölü altyapı.
- Buna rağmen `render.yaml:24-28` Upstash Redis kurulumunu ve elle endpoint girilmesini şart koşuyor → kurulum maliyeti olan, hiçbir işe yaramayan bir bağımlılık.
- **Rate limiter in-memory ve partition'sız** (`Program.cs:82-98`): `auth` limiter tüm istemciler için tek global 10/dk pencere. Bu hem **scale-out'ta bozulur** (her instance kendi sayacını tutar → toplam limit instance sayısıyla çarpılır) hem self-DoS riski taşır (Y4).
- **Hiç cache yok** (`IDistributedCache`/`IMemoryCache` yok): yoğun dashboard/özet sorguları her seferinde DB'ye gidiyor.
- **Idempotency deposu yok** (ADR-0001/ödeme uçları için gerekecek).
- **Access token erken iptal yolu yok** (logout yalnız refresh'i iptal ediyor; rol düşüşü 60 dk gecikiyor).

Yani: parası ödenen bir altyapı atıl dururken, tam da onun çözeceği üç gerçek problem (dağıtık rate limit, cache, idempotency/iptal) çözümsüz.

## Karar Etkenleri

- **Doğruluk (ölçek):** çok-instance'ta rate limit ve idempotency tek doğru sayaç gerektirir.
- **Maliyet:** atıl bağımlılık ya değer üretmeli ya kaldırılmalı.
- **Basitlik:** MVP'de gereksiz dağıtık durum eklememe.

## Değerlendirilen Seçenekler

### Seçenek A — Redis'i tamamen kaldır
Tek-instance MVP olarak kal, in-memory ile yetin.
- ➕ Daha az hareketli parça; Upstash kurulum yükü kalkar; daha ucuz.
- ➖ Yatay ölçekleme anında rate limit/cache/idempotency'yi yeniden gerektirir; dağıtık token iptali olmaz.

### Seçenek B — Redis'i fiilen kullan
- **Dağıtık rate limiting** (IP+hesap partition'lı) — Y4'ü kalıcı çözer ve scale-out'ta doğru.
- **Idempotency anahtarları** (mutasyon/ödeme uçları) — ADR-0001 inbox'ı tamamlar.
- **Dağıtık cache** (read-heavy dashboard/özet; kısa TTL).
- **Refresh-session / token blacklist** — anlık iptal (kritik yetki düşüşü).
- ➕ Dört gerçek problemi tek altyapıyla çözer; scale-out'a hazır; ödenen bedel değer üretir.
- ➖ Dağıtık durum karmaşıklığı; cache invalidation disiplini; Redis erişilemezse graceful degradation gerekir.

## Karar

**Koşullu, ama Seçenek B yönünde.** Yatay ölçekleme yol haritada ise (PRD Faz 4-5 ölçek hedefleri) → **B**, ve öncelik sırası **doğruluk** odaklı: (1) dağıtık rate limiting, (2) idempotency, (3) token blacklist, (4) cache. Tek-instance kalınacaksa Redis **kaldırılır** (A) ve ihtiyaç doğunca eklenir — atıl bağımlılık bırakılmaz.

Her hâlükârda **mevcut "kayıtlı ama kullanılmayan" durum kabul edilemez**: ya kullan ya kaldır.

## Sonuçlar

- ✅ **Olumlu (B):** Scale-out'ta doğru rate limit; idempotent ödemeler; anlık token iptali; hızlı dashboard. (A): daha yalın sistem, daha düşük maliyet.
- ⚠️ **Olumsuz / maliyet (B):** Dağıtık durum + cache invalidation karmaşıklığı; Redis erişilemezliğine karşı fallback (limiter "fail-open" mı "fail-closed" mı kararı).
- 🔭 **Riskler / izlenecekler:** Render free Redis yok (Upstash bağımlılığı); cache tutarlılığı; limiter fallback davranışı.

## Uygulama Notları

- Karar A ise: `IRedisConnectionFactory` kaydını ve `render.yaml` Redis adımını kaldır.
- Karar B ise: `RedisRateLimiterPartition` (IP+hesap) + `IDistributedCache` (StackExchange.Redis) + idempotency middleware + refresh/blacklist deposu. Redis down ise limiter davranışını bilinçli seç.
- Token blacklist, ADR-0002'deki anlık yetki-düşüşü ihtiyacını karşılar.
