# ADR-0002: Kaynak-tabanlı yetkilendirme & izin modeli

- **Durum:** Önerildi (Proposed)
- **Tarih:** 2026-06-30
- **Karar vericiler:** Backend ekibi / güvenlik
- **İlgili:** Denetim bulguları K1 (anonim register'da Admin), K2 (liste IDOR). Bkz. `src/Shared/Infrastructure/Application/AuthorizationCoverageValidator.cs`.

## Bağlam ve Problem

Yetkilendirme iki katmanlı: HTTP `RequireAuthorization` + dispatcher-seviyesi `ICommandAuthorizer/IQueryAuthorizer`. `AuthorizationCoverageValidator` her handler için bir authorizer'ın **var olduğunu** startup'ta zorluyor (iyi). Ancak:

- **Authorizer'ın varlığı doğruluğunu garanti etmiyor.** Sahiplik ("bu kullanıcı bu kaynağın sahibi mi?") kontrolü her authorizer'da **elle ve farklı** yazılmış. Liste uçlarında filtre verilmezse `Success` dönüyor → **IDOR (K2)**: sıradan kullanıcı tüm tabloyu çeker (`LessonSessionPolicies.cs:84-114`).
- **Rol ataması istemciye bırakılmış (K1):** Register komutu rolleri istemciden alıp filtresiz işliyor → anonim Admin.
- Rol kontrolü `Roles.Contains("Admin")` gibi **string** karşılaştırmalarla koda dağılmış; ince-taneli izin (permission) kavramı yok.

Sorun yapısal: ortak bir "sahiplik/izin" primitifi olmadığı için her geliştirici kontrolü yeniden yazıyor ve biri unutulduğunda sessizce açık oluşuyor.

## Karar Etkenleri

- **Güvenlik doğruluğu:** IDOR sınıfını yapısal olarak kapatmak (tek tek yama değil).
- **DRY & tutarlılık:** "sahibi-veya-admin" mantığı tek yerde.
- **Denetlenebilirlik:** kim neye neden erişebiliyor sorusunun tek kaynağı.
- **Genişleyebilirlik:** veli→öğrenci, öğretmen→ders gibi dolaylı sahiplik ilişkileri.

## Değerlendirilen Seçenekler

### Seçenek A — Mevcut + disiplin
Her authorizer'da elle sahiplik kontrolü yazmaya devam, kod inceleme ile yakala.
- ➕ Değişiklik yok.
- ➖ İnsan hatasına açık; IDOR sınıfı tekrar tekrar açılır; denetlenemez.

### Seçenek B — Kaynak-tabanlı yetkilendirme + izin kataloğu
- Tekrar kullanılabilir bir primitif: `EnsureOwnerOrAdmin(resourceOwnerId)` / `IResourceAuthorizer<TResource>` (veya ASP.NET `IAuthorizationHandler<TRequirement, TResource>`).
- Liste uçlarında **server-side zorunlu filtre**: çağıranın kimliği server'dan enjekte edilir, istemci filtresine asla güvenilmez (varsayılan-deny).
- Roller → **izin (permission) kataloğu** (örn. `payments:read:own`, `lesson:write:own`); claim/policy üzerinden.
- ➕ IDOR/privilege-escalation sınıfını kökten kapatır; tek kaynak; test edilebilir; denetlenebilir.
- ➖ Orta ölçekli refactor; mevcut authorizer'ların gözden geçirilmesi.

### Seçenek C — Harici policy engine (OPA / Casbin)
- ➕ Çok ince-taneli, merkezî politika; politika kod-dışı.
- ➖ Bu ölçek için aşırı; operasyon + gecikme yükü; ekip için yeni teknoloji.

## Karar

**Seçenek B.** Kaynak-tabanlı yetkilendirme + izin kataloğu + server-side zorunlu liste filtresi. OPA/Casbin (C) ileride çok-kiracılı/çok ince-taneli ihtiyaç doğarsa yeniden değerlendirilir.

İlk adım acil: **K1** (rolü istemciden alma, allow-list) ve **K2** (server-side filtre) — zaten görevleştirildi (#8, #9). ADR bunları tekil yama olmaktan çıkarıp kalıcı bir desen hâline getirir.

## Sonuçlar

- ✅ **Olumlu:** IDOR/yetki-yükseltme sınıfı kapanır; sahiplik mantığı tek yerde; izin modeli ürünle (veli/öğretmen ilişkileri) birlikte büyüyebilir; denetim kolaylaşır.
- ⚠️ **Olumsuz / maliyet:** Mevcut tüm authorizer'ların gözden geçirilmesi; izin kataloğunun tasarımı; JWT claim yapısının güncellenmesi.
- 🔭 **Riskler / izlenecekler:** Token içindeki rol/izin değişiminin gecikmesi (access token TTL) — kritik yetki düşüşlerinde anlık iptal stratejisi (ADR-0004 Redis blacklist ile bağlantılı).

## Uygulama Notları

- `Shared.Kernel`/`Shared.Application`'a ortak `IResourceOwnership` + `EnsureOwnerOrAdmin` primitifi.
- Liste sorgularında repository imzalarına **zorunlu** `ownerId` parametresi (nullable değil) veya query'de server-enjekte filtre.
- Register/rol atama akışını ayır (ADR-0002 + K1 görevi).
- Mimari teste "her liste authorizer'ı varsayılan-deny" kuralı ekle (Y2 görevi ile).
