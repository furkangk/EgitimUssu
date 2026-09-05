---
title: Yedekleme, Geri Yükleme ve Migration Geri Alma
summary: "Veritabani yedekleme/geri yukleme prosedürü, RPO/RTO hedefleri, üretimde migration'in ayri deploy adimi olmasi ve Down() geri alma kurali"
authority: derived
updated: 2026-09-05
type: architecture
status: 🟢 uygulandı
tags: [ops, backup, restore, migration, rollback, disaster-recovery, E-05]
---

# 💾 Yedekleme, Geri Yükleme ve Migration Geri Alma (E-05)

> **Neden var:** Bir sistemin üretime hazır olup olmadığını belirleyen tek soru "çalışıyor mu"
> değil, **"bozulduğunda geri dönebiliyor muyuz"**dur. Bu doküman iki geri dönüş yolunu tanımlar:
> veriyi yedekten geri yükleme ve şema değişikliğini geri alma. İkisi de **denenmiş** olmalıdır —
> denenmemiş yedek, yedek değildir.

---

## 1. Hedefler (RPO / RTO)

| Ölçüt | Hedef | Nasıl karşılanıyor |
|-------|-------|--------------------|
| **RPO** (kabul edilen veri kaybı) | **≤ 24 saat** | Günlük tam yedek. Kayıp penceresi son yedekten bu yana geçen süredir. |
| **RTO** (geri dönüş süresi) | **≤ 1 saat** | Geri yükleme tek komuttur; süre veritabanı boyutuyla artar (bugün ~250 KB, saniyeler). |
| **Saklama** | **30 gün** | `BACKUP_RETENTION_DAYS` (varsayılan 30); daha eskisi silinir. |
| **Sıklık** | **Günlük** | `scripts/backup-db.sh` zamanlanmış bir işten çağrılır. |

> ⚠️ **Bugünkü durum:** Yedekleme **otomatik değil** — zamanlanmış iş henüz kurulmadı, çünkü
> üretim veritabanı ve nesne depolama hesabı açılmadı (Q4). Script'ler hazır ve denenmiş;
> eksik olan yalnız zamanlayıcı ile kimlik bilgileri. Bkz. §7.

> **RPO'yu düşürmek** günlük yedeğin ötesinde sürekli arşivleme (WAL) gerektirir; bu, yönetilen
> veritabanı sağlayıcısının ücretli planlarında gelir. Bu doküman **sağlayıcıdan bağımsız asgari
> prosedürü** tanımlar: sağlayıcı ne sunarsa sunsun, bu yol her zaman çalışır.

---

## 2. Yedek alma

```bash
# Bağlantı argüman olarak ya da ConnectionStrings__Postgres ortam değişkeninden gelir.
scripts/backup-db.sh "Host=...;Port=5432;Database=egitimussu;Username=...;Password=..."
scripts/backup-db.sh "postgresql://kullanici:parola@host:5432/egitimussu"
```

| Değişken | Varsayılan | Anlamı |
|----------|-----------|--------|
| `BACKUP_DIR` | `./backups` | Yedeklerin yazıldığı dizin |
| `BACKUP_RETENTION_DAYS` | `30` | Bu günden eski yerel yedekler silinir (`0` = silme) |
| `BACKUP_S3_BUCKET` | *(boş)* | Doluysa yedek buraya da yüklenir (`s3://...`) |
| `BACKUP_S3_ENDPOINT` | *(boş)* | S3-uyumlu özel uç — Cloudflare R2 için gerekli (K-03) |
| `PG_IMAGE_TAG` | `17` | `pg_dump` yerelde yoksa kullanılacak imaj sürümü |

**Çıktı:** `egitimussu-<veritabani>-<YYYYMMDD-HHMMSS>Z.dump` — `pg_dump --format=custom`.
Zaman damgası **UTC**'dir; farklı saat dilimlerindeki makinelerden alınan yedekler de doğru sıralanır.

### Script'in ısrar ettiği üç şey

1. **Yarım dosya bırakmaz.** `pg_dump` başarısız olursa dosya silinir. "Yedeğim var" yanılgısı,
   yedeksizlikten daha tehlikelidir — çünkü kimse ikinci bir çare aramaz.
2. **Yedeği okur.** Yazdıktan sonra `pg_restore --list` ile arşivi açar ve tablo sayısını yazar.
   0 baytlık ya da kesik bir dump sessizce "başarılı" sayılmaz.
3. **İstemci sürümünü sunucuya uydurur.** `pg_dump`, kendisinden **yeni** bir sunucuyu dökümlemeyi
   reddeder. PATH'te yoksa `postgres:${PG_IMAGE_TAG}` imajından çalıştırılır — Postgres kurulu
   olmayan bir makineden (örneğin bu repo'nun geliştirildiği MacBook) yedek almanın yolu budur.

> **Arşiv konteynere bağlanmaz, stdin/stdout ile akar.** İlk yazımda `-v` ile bind mount
> kullanılıyordu; Docker Desktop paylaşılmayan bir host dizinine **sessizce boş** bir bağlama yaptı,
> `pg_dump` konteynerin içinde başarıyla yazdı, host'ta dosya oluşmadı ve script "yedek alındı" dedi.
> Akış biçimi hem bu tuzağı hem de dosyanın `root`'a ait olması sorununu ortadan kaldırır.

> **Tek kopya yedek, yedek sayılmaz.** `BACKUP_S3_BUCKET` boşsa script bunu ekrana yazar. Yedek,
> yedeklediği makinenin diskinde durduğu sürece aynı arızayla birlikte kaybolur.

---

## 3. Geri yükleme

```bash
scripts/restore-db.sh backups/egitimussu-egitimussu-20260905-180748Z.dump \
    "Host=...;Database=egitimussu;Username=...;Password=..." --yes
```

**`--yes` olmadan çalışmaz.** Script hedefi (kullanıcı@host:port/veritabanı) ekrana yazar ve durur.
Geri yükleme yanlış veritabanına yapıldığında geri alınamaz; onay bilinçli olmalıdır.

Kullanılan bayraklar ve nedenleri:

| Bayrak | Neden |
|--------|-------|
| `--single-transaction` | **Ya hepsi ya hiçbiri.** Yarım geri yüklenmiş bir veritabanı, hiç geri yüklenmemişten tehlikelidir: kısmi veri gerçek sanılır. Hata olursa hedef **değişmez**. |
| `--clean --if-exists` | Var olan nesneler önce düşürülür; yok olanlar için hata üretmez (boş veritabanına da geri yüklenebilir). |
| `--no-owner --no-privileges` | Yedek başka bir rol adıyla alınmış olabilir (üretim ve staging kullanıcıları ayrıdır); sahiplik hedefteki role bırakılır. |

Arşivin okunabilirliği **hedefe dokunmadan önce** doğrulanır: bozuk bir dosya yüzünden önce hedefi
temizleyip sonra "arşiv okunamıyor" demek, veriyi silip yerine bir şey koyamamak demektir.

**Geri yükleme sonrası:** uygulamayı başlatın ve `/health/ready` yeşil mi bakın; ardından bir
hesapla giriş yapın. Şema var ama veri bozuksa sağlık kontrolü bunu göstermez.

---

## 4. Migration: üretimde ayrı adım

**Üretimde migration açılışta uygulanmaz.** `MigrationStartupGuard`
(`src/Shared/Infrastructure/Configuration/`) `ASPNETCORE_ENVIRONMENT=Production` ise
`Database:ApplyMigrationsOnStartup` **açık olsa bile** engeller ve `LogWarning` düşer.

| Ortam | Açılışta migration | Neden |
|-------|--------------------|-------|
| Development | Bayrağa göre | Hızlı döngü |
| Staging | **Otomatik** (`true`) | Prova ortamı; sıfırlanabilir |
| **Production** | **Asla** | Aşağıdaki iki gerekçe |

1. **Zamanlama gözlenebilir olmalı.** Şema değişikliği deploy'un açılış saniyesine gömülürse,
   sorun çıktığında "kod mu, şema mı kırdı" ayırt edilemez.
2. **Yarış.** Birden çok örnek aynı anda açıldığında migration'lar birbirine girer.

### Üretimde migration uygulama

```bash
# 1) ÖNCE YEDEK — geri dönüş yolu olmadan şema değiştirilmez.
scripts/backup-db.sh "$PROD_CONNECTION"

# 2) Migration'ları uygula (her modülün kendi DbContext'i vardır).
export ConnectionStrings__Postgres="$PROD_CONNECTION"
for proj in $(grep -rl "DesignTimeDbContextFactoryBase" src --include="*.cs" \
                | grep -v "Shared/Infrastructure/Design" | xargs -n1 dirname | sort -u); do
  dotnet ef database update \
    --project "$proj" \
    --startup-project src/API.Host/EgitimUssu.API.Host.csproj
done

# 3) Uygulamayı deploy et ve /health/ready + /api/meta/version doğrula.
```

> Sıra önemlidir: **önce şema, sonra kod.** Yeni kolonu okuyan kod, kolon açılmadan yayına
> girerse istekler hata verir. Bu yüzden migration'lar **geriye dönük uyumlu** yazılır
> (kolon ekle → kod yayınla → eski kolonu ayrı bir sürümde düşür).

### Geri alma

| Durum | Yol |
|-------|-----|
| Kod hatalı, şema sağlam | Önceki deploy'a dön (Render'da önceki sürüm). Şemaya dokunma. |
| Şema hatalı, veri sağlam | `dotnet ef database update <ÖncekiMigrationAdı>` — `Down()` çalışır. |
| Veri bozuldu | §3 geri yükleme. En son yedekten sonraki değişiklikler kaybolur (RPO). |

---

## 5. Down() kuralı ve CI kontrolü

**Kural:** `Up()` bir şey yapıyorsa `Down()` de yapmalı.

Boş — ya da yalnız `throw` içeren — bir `Down()`, o migration'ın **geri alınamaz** olduğu anlamına
gelir; hatalı bir şema değişikliğinin tek çıkış yolu yedekten geri yüklemeye iner ve RPO kadar veri
kaybedilir. Bu, tek satırlık bir ihmalin veri kaybına dönüştüğü yerdir.

```bash
scripts/check-migration-down.sh            # tüm repo
scripts/check-migration-down.sh <dosya>…   # yalnız verilen dosyalar
```

CI'da **Backend CI → "Migration geri alma kontrolü"** adımı olarak koşar; ihlal PR'ı kırar.

**Muafiyet:** `Up()` boş olan migration'lar. EF, yalnız model metadata'sını değiştiren (örneğin
`ValueGeneratedNever`) bir değişiklik için şema üretmeden migration oluşturur; böyle bir
migration'ın geri alacağı da yoktur. Repoda bugün bir tane var
(`Students/…/StudentSubjectValueGeneratedNever`).

---

## 6. Kim yetkili

| İş | Yetki |
|----|-------|
| Yedek alma | Üretim veritabanı bağlantı dizesine erişimi olan herkes (bugün: tek bakımcı) |
| **Geri yükleme** | Yalnız bakımcı. `--yes` bilinçli bir karardır; script onaysız çalışmaz. |
| Üretimde migration | Yalnız bakımcı, **yedek aldıktan sonra** |

Üretim bağlantı dizesi repoda **yoktur** ve olmayacaktır; sağlayıcı panelinden alınır
(bkz. [`environments.md`](environments.md) §3).

---

## 7. Tatbikat kaydı

> Yedekleme prosedürü, **koşulduğu gün** doğrulanmış sayılır. Her tatbikat buraya bir satır ekler.

| Tarih | Kapsam | Sonuç |
|-------|--------|-------|
| 2026-09-05 | Yerel Docker Postgres 17: `seed-demo-data.sh` ile 15 modülün şeması + demo veri → `backup-db.sh` → **tüm şemalar düşürüldü** (0 tablo) → `restore-db.sh --yes` → doğrulama | ✅ **122 tablonun satır sayısı birebir aynı** (66'sı dolu; `diff` farksız). Geri yüklenen veritabanına karşı uygulama açıldı: `/health/ready` = `Healthy`, demo öğretmen girişi **HTTP 200**. `--yes` olmadan çalıştırma **reddedildi** (çıkış 1). |
| 2026-09-05 | `MigrationStartupGuard` canlı doğrulama | ✅ `ASPNETCORE_ENVIRONMENT=Production` + `ApplyMigrationsOnStartup=true` → uyarı düştü, **0 migration** koştu, boş veritabanında **0 tablo**. Aynı veritabanı `Staging` ile açılınca **15 modül** migrate oldu, **122 tablo** oluştu. |

### Kalan iş (üretim hesabı açılınca)

- [ ] Günlük zamanlanmış yedek (cron / zamanlanmış iş) — üretim bağlantı dizesi gerekir
- [ ] `BACKUP_S3_BUCKET` + `BACKUP_S3_ENDPOINT` (R2, Q4) — makinede duran tek kopya yedek yeterli değil
- [ ] Gerçek üretim verisiyle geri yükleme tatbikatı (staging'e geri yükleyerek)

---

> İlgili: ortamlar → [`environments.md`](environments.md) · backend mimari → [`backend.md`](backend.md) ·
> API sürümleme → [`api_versioning.md`](api_versioning.md)

*Yedekleme & Geri Yükleme | Güncelleme: 2026-09-05 (P13 Task 4 / E-05: ilk sürüm — yedek/geri yükleme script'leri, üretimde açılış migration'ının kapatılması ve CI'da `Down()` kontrolü)*
