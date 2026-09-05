---
title: API Sürümleme ve Sözleşme Kararlılığı
type: architecture
status: 🟢 uygulandı
tags: [api, versioning, contract, backend, C-10]
---

# API Sürümleme ve Sözleşme Kararlılığı (C-10)

> **Neden var:** Mobil istemci mağazadaki sürümüyle **aylarca** yaşar. Kullanıcı uygulamayı güncellemeden
> sunucu güncellenir; yani her an sahada birden çok istemci sürümü vardır. Sunucu sözleşmeyi habersiz
> değiştirdiğinde kırılan taraf her zaman **güncellenemeyen istemcidir**. Bu doküman, hangi değişikliğin
> serbest, hangisinin yeni sürüm gerektirdiğini ve eskisinin ne zaman kaldırılabileceğini tanımlar.

---

## 1. Bugünkü durum

| Alan | Değer |
|------|-------|
| Yayınlanan sözleşme sürümü | **`v1`** |
| Yol biçimi | `"/api/<modul>/..."` — **sürüm eki yok**, mevcut yollar `v1` kabul edilir |
| Sürüm başlığı | Her `/api/*` yanıtında `X-Api-Version: v1` |
| Belge | `GET /api/meta/version` → `apiVersion` (sözleşme) + `version` (derleme) · OpenAPI `info.version` |

> **Yollar değişmedi ve değişmeyecek.** `v1` uçlarına `"/api/v1/"` ön eki *eklenmedi*: bunu yapmak
> yayınlanmış her istemciyi anında kırardı — yani sürümlemenin önlemek için var olduğu şeyi yapardı.
> `v1`, "sürüm eki taşımayan yol" demektir. Ek yalnız **v2 ve sonrasında** görünür.

---

## 2. Sözleşme sürümü ≠ derleme sürümü

İki ayrı sayı, iki ayrı tempo — karıştırılırsa "sunucu değişti mi?" sorusu cevapsız kalır:

| | `apiVersion` (`X-Api-Version`) | `version` |
|---|---|---|
| Ne söyler | Yayınlanan **sözleşmenin** sürümü | Çalışan **derlemenin** sürümü |
| Ne zaman değişir | Yalnız kırıcı değişiklikte | Her yayında |
| Kaynak | `ApiVersions.Current` | Assembly sürümü |
| İstemci ne yapar | Uyumluluk kararı verir | Yalnız hata bildiriminde raporlar |

---

## 3. Kırıcı değişiklik nedir?

**Kırıcı (yeni sürüm gerekir):**

- Bir alanın **kaldırılması** ya da yeniden adlandırılması
- Bir alanın **tipinin** değişmesi (`string` → `object`, `int` → `string`)
- Bir alanın **anlamının** değişmesi (aynı ad, farklı içerik — en sinsi tür; ör. `startAtUtc` "seri başlangıcı"ndan "bu oluşumun saati"ne kayarsa)
- İsteğe bağlı bir alanın **zorunlu** hâle gelmesi
- Bir ucun kaldırılması ya da yolunun değişmesi
- Başarılı bir isteğin **durum kodunun** değişmesi (`200` → `202`)
- Bir hata kodunun (`Error.Code`) kaldırılması ya da yeniden adlandırılması
- Bir enum değerinin kaldırılması

**Kırıcı değil (`v1` içinde serbest):**

- **Yanıta** yeni alan eklemek (istemci bilmediği alanı yok sayar)
- **İsteğe** varsayılanı olan, isteğe bağlı bir alan eklemek
- Yeni uç, yeni modül, yeni sorgu parametresi (varsayılanlı)
- Yeni hata kodu eklemek — *ama* uç zaten belgelenmiş bir kodu döndürüyorsa onu değiştirmemek
- Yeni enum değeri eklemek ⚠️ — istemcinin bilinmeyen değeri düşürmeden karşılaması şartıyla;
  bu, mobil tarafta `switch`'lerin **daima** bir `default` dalı taşıması demektir

> **Enum uyarısı gerçek bir olaydan geliyor:** sunucu tarafında yeni bir durum değeri eklemek
> ucuz görünür, ama eski istemci onu tanımıyorsa ekran boş kalır. Yeni enum değeri eklerken
> "sahadaki en eski istemci bunu görünce ne yapar?" sorusu cevaplanmadan yayınlanmaz.

---

## 4. Kırıcı bir değişiklik nasıl yapılır?

`v1` **kırılmaz**. Kırıcı değişiklik yeni bir sürüm yolu altında açılır:

```csharp
// Modül, v1 grubunun yanına ikinci bir grup açar. v1 uçları olduğu gibi çalışmaya devam eder.
public override void MapEndpoints(IEndpointRouteBuilder endpoints)
{
    var v1 = CreateModuleGroup(endpoints);                              // "/api/students"
    // Aynı anda ApiVersions'a `V2` sabiti eklenir; bugün yalnız `V1` tanımlıdır.
    var v2 = CreateModuleGroup(endpoints, "/api/v2/students", "v2");    // yeni sözleşme

    v1.MapGet("/profiles/{id:guid}", GetProfileAsync);        // eski gövde — dokunulmaz
    v2.MapGet("/profiles/{id:guid}", GetProfileV2Async);      // yeni gövde
}
```

Sürüm etiketi **grup metadata'sına** yazılır (`ApiVersionMetadata`); `X-Api-Version` başlığı ve
OpenAPI etiketi bunu okur, yani başka hiçbir yeri değiştirmek gerekmez.

### Yaşam döngüsü

| Aşama | Ne olur | Süre |
|-------|---------|------|
| 1. Yayın | `v2` açılır, `v1` çalışmaya devam eder | — |
| 2. Kullanımdan kaldırma duyurusu | `v1` yanıtlarına `Deprecation` + `Sunset` başlığı; sürüm notlarına yazılır | — |
| 3. Geçiş | Mobil `v2`'ye taşınır ve **mağazada yayınlanır** | — |
| 4. Bekleme | `v1` yayınlanmış **en az 2 mobil sürüm** boyunca ve **en az 90 gün** ayakta kalır | ≥ 2 sürüm / ≥ 90 gün |
| 5. Kaldırma | Telemetride `v1` trafiği sıfıra yakınsa kaldırılır | — |

> **Neden hem sürüm hem gün sayısı?** Yalnız sürüm sayısı: arka arkaya iki yamayı bir haftada
> yayınlarsanız süre buharlaşır. Yalnız gün: kullanıcı uygulamayı hiç güncellemediyse takvim
> tek başına yeterli değildir. İkisinin **büyüğü** beklenir.

> **Kaldırma kararı ölçüme dayanır, tahmine değil:** `v1` trafiği gözlemlenebilirlikten okunur
> (C-04, [`backend.md` §11](backend.md)). Trafik varsa süre dolsa bile kaldırılmaz.

---

## 5. Uygulama

| Parça | Yer |
|-------|-----|
| Sürüm sabitleri | `src/Shared/Infrastructure/Modules/ApiVersioning.cs` — `ApiVersions.V1` / `.Current` |
| Grup etiketi | `ApiVersionMetadata` + `WithApiVersion()`; `ModuleDefinition.CreateModuleGroup` her modül grubuna uygular |
| Başlık | `src/Shared/Infrastructure/Middleware/ApiVersionHeaderMiddleware.cs` |
| Belge | `src/API.Host/Program.cs` — OpenAPI `AddDocumentTransformer` + `GET /api/meta/version` |
| Test | `tests/Integration/ApiVersionHeaderTests.cs` · `tests/Unit/ApiVersionHeaderMiddlewareTests.cs` |

### Başlık hangi değeri seçer?

1. Ucun `ApiVersionMetadata` etiketi (modül grubundan gelir) — **kazanır**
2. Etiket yoksa ve yol `/api` ile başlıyorsa → `ApiVersions.Current`

İkincisi bir **güvenlik ağıdır**: `CreateModuleGroup` kullanmayan bir uç (host'un kendi
`/api/meta/version`'ı, `MapTeacherDashboard`) unutulsa bile sürümsüz yanıt dönmez.

**Sürümlenmeyenler:** `/health/*` ve `/openapi/*` başlık taşımaz — bunlar altyapı uçlarıdır,
yayınlanan sözleşmenin parçası değildir.

Başlık, sonraki middleware'den **önce** yazılır: `401`/`403`/`429` ve beklenmeyen `500` dahil her
yanıt sürümü taşır — istemci hata gördüğünde de hangi sözleşmeye baktığını bilmelidir.

---

## 6. İstemci tarafı kuralları (mobil)

- Bilinmeyen JSON alanı **yok sayılır**, hata verilmez (`dio` + `fromJson` varsayılanı budur).
- Bilinmeyen enum değeri bir `default`/`unknown` dalına düşer, `throw` etmez.
- `X-Api-Version` beklenenden farklıysa uygulama **çalışmayı sürdürür**; yalnız telemetriye not düşer.
  Sürüm başlığına bakıp kendini kilitleyen istemci, sunucu bir üst sürüme geçtiğinde sahadaki
  tüm eski kurulumları çöpe çevirir.

---

> İlgili: backend mimari → [`backend.md`](backend.md) · gözlemlenebilirlik → [`backend.md` §11](backend.md) ·
> modül envanteri → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)

*API Sürümleme | Güncelleme: 2026-09-05 (P13 Task 2 / C-10: ilk sürüm — `v1` sözleşmesi, `X-Api-Version` başlığı, kırıcı değişiklik tanımı ve ≥2 mobil sürüm / ≥90 gün kaldırma prosedürü)*
