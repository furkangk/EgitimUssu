# ADR-0003: API versiyonlama & contract-first istemci üretimi

- **Durum:** Önerildi (Proposed)
- **Tarih:** 2026-06-30
- **Karar vericiler:** Backend + Mobil ekibi
- **İlgili:** Mobil DTO drift'i (`mobile/.../auth_repository_impl.dart:88` sabit `roles:[2]`), event isim çakışması (M6). Bkz. `src/API.Host/Program.cs:23` (`AddOpenApi`).

## Bağlam ve Problem

- **API versiyonlama yok.** Yalnız bilgilendirme amaçlı `/api/meta/version` ucu var (`Program.cs:132`). Kırılgan değişiklik (breaking change) yapıldığında mobil istemcileri kontrollü taşımanın yolu yok.
- **DTO'lar elle, iki tarafta ayrı yazılmış.** Backend request/response kayıtları ile mobil model sınıfları manuel; senkronizasyon insana bağlı. Sonuç: mobil tarafta `register` her zaman `roles:[2]` gönderiyor (sözleşme drift'i, hem güvenlik hem işlevsel hata).
- **Entegrasyon event'leri `GetType().Name` ile** adlandırılıyor (`JsonDomainEventMapper.cs:18`) → versiyonsuz, namespace'siz, çakışmaya açık sözleşmeler (M6).
- OpenAPI üretiliyor (`AddOpenApi`) ama **bir doğruluk kaynağı olarak kullanılmıyor** (yayınlanmıyor, istemci üretilmiyor).

## Karar Etkenleri

- **Geriye dönük uyumluluk:** mobil istemcileri kırmadan API evrimi.
- **Drift'i bitirme:** backend ↔ mobil DTO uyumunu derleme zamanında garanti.
- **Sözleşme kararlılığı:** hem HTTP API hem modüller-arası event'ler için açık, versiyonlu sözleşmeler.

## Değerlendirilen Seçenekler

### Seçenek A — Mevcut durum (elle DTO, versiyon yok)
- ➕ Ek araç yok.
- ➖ Drift insana bağlı; breaking change yönetilemez; mobil hataları üretimde fark edilir.

### Seçenek B — Contract-first: versiyonlama + OpenAPI doğruluk kaynağı + üretilen istemci
- **Asp.Versioning** ile URL/Date/Header tabanlı API versiyonlama.
- OpenAPI şemasını **CI'da artefakt** olarak yayınla.
- Mobil Dart istemcisini OpenAPI'den **üret** (openapi-generator / swagger codegen) → DTO drift'i derleme zamanında biter.
- Modüller-arası event'ler için `Shared.Contracts`'ta **açık adlı, versiyonlu** kayıtlar (`LessonScheduledV1` gibi); mapper'da tam sözleşme adı.
- ➕ Drift kökten biter; breaking change yönetilir; mobil istemci hep güncel; event çakışması (M6) çözülür.
- ➖ Codegen pipeline kurulumu; üretilen kodu repoya/CI'a oturtma; ekip alışkanlığı.

### Seçenek C — gRPC / Protobuf sözleşmeleri
- ➕ Güçlü tipli, çok-dilli codegen, performans.
- ➖ Mobil REST/JSON yığınıyla ve mevcut minimal API'lerle uyumsuz; büyük göç.

## Karar

**Seçenek B.** Asp.Versioning + OpenAPI'yi doğruluk kaynağı yapma + mobil istemci üretimi + versiyonlu event sözleşmeleri. gRPC (C) yalnız ileride yüksek-performanslı servis-içi ihtiyaç doğarsa.

## Sonuçlar

- ✅ **Olumlu:** Mobil/backend DTO drift'i (ve `roles:[2]` tipi hatalar) biter; API kontrollü evrilir; event sözleşmeleri kararlı/versiyonlu (M6 çözülür); dokümantasyon otomatik.
- ⚠️ **Olumsuz / maliyet:** Codegen + versiyonlama altyapısı kurulumu; üretilen istemcinin mobil mimariye (repository katmanı) entegrasyonu.
- 🔭 **Riskler / izlenecekler:** Üretilen istemcinin elle yazılmış `ApiClient`/interceptor katmanıyla uyumu (mevcut dio interceptor'ları korunmalı).

## Uygulama Notları

- Backend: `Asp.Versioning.Http` + grupları `/api/v1/...` altına al; OpenAPI'yi sürümle.
- CI'da OpenAPI şemasını üret → mobil codegen adımı (üretilen modeller `mobile/lib/.../generated/`).
- `Shared.Contracts`: event sözleşmelerini açık adlandır + versiyonla; `JsonDomainEventMapper`'ı buna göre güncelle (ADR-0001 ile birlikte).
- Mobilde elle DTO'ları kademeli olarak üretilenlerle değiştir; kullanılmayan `freezed/json_serializable` ya codegen'e bağlanır ya kaldırılır.
