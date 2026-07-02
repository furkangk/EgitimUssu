# ADR-0001: Asenkron mesajlaşma & transactional outbox stratejisi

- **Durum:** Önerildi (Proposed)
- **Tarih:** 2026-06-30
- **Karar vericiler:** Backend ekibi / mimar
- **İlgili:** Denetim bulguları K3 (outbox serileştirme), K5 (zehirli mesaj + çoklu-instance), Y1 (cross-module senkron yazım), M6 (event isim çakışması). Bkz. [`architecture/backend.md`](../architecture/backend.md).

## Bağlam ve Problem

Modüller arası iletişim, elle yazılmış bir **transactional outbox** + in-process event bus ile kurulmuş.

- **Yazma tarafı doğru ve atomik:** `ModuleDbContext.SaveChangesAsync` domain event'leri aynı transaction içinde `outbox_messages`'a yazıyor (`src/Shared/Infrastructure/Persistence/ModuleDbContext.cs:51-91`). Bu kısım korunmalı.
- **Dağıtım tarafı production'a hazır değil:**
  - **Serileştirme uyumsuzluğu (K3):** Yazım `Web/camelCase`, okuma varsayılan/PascalCase (`OutboxProcessor.cs:20`) → event alanları `null` deserialize oluyor, hiçbir handler eşleşmiyor, mesaj yine "processed" işaretleniyor. Ampirik kanıtlandı.
  - **Çoklu-instance yarışı (K5):** `FOR UPDATE SKIP LOCKED`/claim yok → yatay ölçeklemede mükerrer publish.
  - **Zehirli mesaj kilitlenmesi (K5):** Batch ortasındaki exception `MarkProcessed`'i atlıyor → en eski hatalı mesaj sırayı kalıcı bloklar; `Error` kolonu var ama retry/dead-letter yok.
  - **Tüketici idempotency'si yok** (inbox/dedup tablosu yok).
  - **Event kimliği `GetType().Name`** ile (`JsonDomainEventMapper.cs:18`) → namespace yok, iki modülde aynı isim çakışıyor (M6).
  - **Y1:** Scheduling, outbox'ı baypas ederek doğrudan Notifications DbContext'ine senkron yazıyor → atomiklik + modül izolasyonu bozuluyor.

Kısaca: doğru deseni elle yazmanın bütün klasik tuzaklarına düşülmüş. Bunlar tek tek yamanabilir ama bir bütün olarak "tekrar açılmayacak" bir çözüm gerekiyor.

## Karar Etkenleri

- **Doğruluk:** at-least-once teslimat + idempotent tüketim + dayanıklı retry garantisi.
- **Ölçek:** birden fazla `API.Host` instance'ı güvenli (satır sahiplenme/leasing).
- **Bakım maliyeti & TTM:** kritik altyapıyı kendimiz yazıp test etme yükü vs. olgun kütüphane.
- **Geçiş yolu:** bugün in-process, yarın gerçek broker'a (RabbitMQ/Azure SB) düşük sürtünmeyle taşınabilirlik.
- **Modüler monolit uyumu:** tek süreç içi haberleşmeyi basit tutarken sınırları koruma.

## Değerlendirilen Seçenekler

### Seçenek A — Mevcut elle yazımı sağlamlaştır
Okuma serileştirmesini düzelt, `SKIP LOCKED`, per-mesaj retry/dead-letter, inbox/dedup tablosu, versiyonlu event adı ekle.
- ➕ Yeni bağımlılık yok; ekip mevcut kodu biliyor; en hızlı "prod'u açma" yolu.
- ➖ Saga/scheduling/timeout/transport gibi ihtiyaçlar geldiğinde tekrar elle yazılır; idempotency/inbox/observability'yi de kendimiz bakarız; "yeniden icat" maliyeti sürer.

### Seçenek B — Wolverine (mesajlaşma + mediator)
Wolverine hem CQRS dispatcher'ı (mevcut `dynamic` dispatcher'ın yerini alır) hem de transactional outbox + inbox + retry + dead-letter + scheduled message'ı yerleşik sunar; EF Core + PostgreSQL entegrasyonu hazır; in-memory transport'tan broker'a tek satırla geçiş.
- ➕ Hem K3/K5 hem `dynamic` dispatch (M13) hata sınıfını **yapısal olarak** kapatır; outbox/inbox/idempotency hazır; modüler monolit için tasarlanmış.
- ➖ Öğrenme eğrisi; mevcut `ICommand/IQuery` soyutlamalarından göç; framework'e bağlanma.

### Seçenek C — MassTransit
Transport-agnostik; outbox, saga, retry, scheduling olgun.
- ➕ Endüstri standardı; broker'a geçiş çok olgun; saga desteği güçlü.
- ➖ In-process senaryoda görece ağır; konfigürasyon yüzeyi geniş; lisans/sürüm politikası takip gerektirir.

### Seçenek D — Şimdi gerçek broker'a geç (RabbitMQ/Azure SB)
- ➕ "Gerçek" asenkron mimari.
- ➖ MVP/tek-instance için aşırı mühendislik; operasyon yükü; Render free plan'la uyumsuz.

## Karar

**İki aşamalı öneri:**

1. **Kısa vade (prod blocker'larını aç):** Seçenek A'nın yalnız *kritik* kısımları — K3 serileştirme düzeltmesi + K5 `SKIP LOCKED`/retry/dead-letter + Y1 senkron yazımın kaldırılması. Bunlar zaten görevleştirildi (#10, #12, #13) ve acil.
2. **Orta vade (yapısal):** **Seçenek B — Wolverine**'e geçiş. Modüler monolit için en düşük sürtünmeli olgun çözüm; hem outbox/inbox hem de `dynamic` CQRS dispatch'i tek hamlede emekliye ayırır. Broker ihtiyacı doğduğunda (Seçenek D) transport değişimi konfigürasyon düzeyinde kalır.

Saga/iş akışı ihtiyacı belirginse (örn. ödeme + üyelik orkestrasyonu) Seçenek C yeniden değerlendirilir.

## Sonuçlar

- ✅ **Olumlu:** K3/K5/M13 hata sınıfı kapanır; idempotency/inbox/retry/dead-letter/observability hazır gelir; çoklu-instance güvenli; broker'a geçiş ucuzlar.
- ⚠️ **Olumsuz / maliyet:** Wolverine öğrenme eğrisi + mevcut dispatcher/event soyutlamalarından göç; framework bağımlılığı; testlerin uyarlanması.
- 🔭 **Riskler / izlenecekler:** Göç sırasında outbox YAZIM atomikliğini bozmamak (mevcut doğru davranış korunmalı); event sözleşmelerini versiyonlamak (ADR-0003 ile birlikte).

## Uygulama Notları

- Önce kısa-vade yamaları ile prod'u stabilize et (event YAZIM tarafına dokunma).
- Wolverine PoC'unu **tek modülde** (örn. Scheduling→Notifications) yap: domain event → Wolverine outbox → Notifications handler; senkron `LessonScheduleNotificationService`'i kaldır.
- Tüketicilerde idempotency'yi inbox tablosu / mesaj-id dedup ile garanti et.
- Event adlarını `GetType().Name` yerine açık, versiyonlu sözleşme adlarına taşı (ADR-0003).
