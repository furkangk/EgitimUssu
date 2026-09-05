---
title: Ortamlar — Development, Staging, Production
type: architecture
status: 🟢 uygulandı
tags: [ops, environment, staging, seeding, render, E-06]
---

# 🌍 Ortamlar (E-06)

> **Neden var:** Üretime giden her değişikliğin, üretimin **provasını** yapabileceği bir yer olmalı.
> Prova ortamı yoksa ilk gerçek koşum canlıda olur; bu doküman üç ortamın ne ile ayrıldığını,
> hangi sağlayıcının nerede açık olduğunu ve demo verinin nereye yazıldığını tanımlar.

---

## 1. Üç ortam bir bakışta

| | **Development** | **Staging** | **Production** |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging` | `Production` |
| Nerede | Geliştiricinin makinesi | Render (`egitimussu-api-staging`) | Render (`egitimussu-api`) |
| Veritabanı | `InMemory:development` ya da yerel Docker Postgres | `egitimussu-staging-db` (**ayrı**) | `egitimussu-db` |
| Migration | Elle (`dotnet ef database update`) | Açılışta otomatik | **Açılışta otomatik** *(E-05 ile ayrı adıma taşınacak)* |
| Demo veri (`Seeding:Enabled`) | Kapalı (istenirse açılır) | **Açık** | **Yasak** — guard bayrağı yok sayar |
| E-posta | `Logging` (göndermez) | `Logging` (göndermez) | `Smtp` *(Q7 bekliyor)* |
| Push | `Logging` (göndermez) | `Logging` (göndermez) | `Fcm` *(Q8 bekliyor)* |
| Dosya depolama | `Local` (disk) | `Local` | `S3` *(Q4 bekliyor)* |
| JWT imzalama anahtarı | Sabit dev anahtarı | Render üretir (**üretimden ayrı**) | Render üretir |
| Gözlemlenebilirlik | Kapalı | Kapalı (OTLP adresi girilince açılır) | Kapalı (aynı) |
| Kime açık | Yalnız geliştirici | Ekip + beta öğretmenler | Herkes |

> **Staging'in en önemli özelliği ayrı veritabanıdır.** Aynı veritabanını paylaşan bir "staging"
> ortam, prova değil ikinci bir üretim istemcisidir: orada yapılan silme denemesi gerçek kullanıcının
> verisini siler.

> **Staging'de e-posta ve push bilerek kapalıdır.** Demo hesapların adresleri gerçek değil
> (`@egitimussu.local`); gerçek gönderim denemesi hem bounce üretir hem gönderen itibarını yakar.
> Gönderilecek içerik log'a yazılır ve orada okunur.

---

## 2. Demo veri (staging)

Staging her deploy'da yeniden başlar ve açılışta `DemoDataSeeder` koşar
(`src/API.Host/Seeding/DemoDataSeeder.cs`).

### Ne oluşturuluyor?

| Kayıt | Adet | Not |
|-------|------|-----|
| Öğretmen | 2 | Matematik (İstanbul) ve Fizik (Ankara), uygunluk aralıklarıyla |
| Öğrenci | 5 | **2'si kendi hesabıyla** (mobil giriş + kronometre denenebilsin), 3'ü öğretmen-yönetimli |
| Veli | 2 | Her biri kendi hesabı olan bir çocuğa bağlı, bağ **onaylanmış** |
| Ders | 15 | Öğrenci başına 3 haftalık geçmiş; hepsi tamamlanmış |
| Ödev | 15 | Her dersin ardından ders notu + ödev |
| Ödeme | 5 | 4'ü ödenmiş, **1'i bilerek gecikmiş** (geciken ödeme ekranı boş listeyle test edilemez) |
| Çalışma seansı | 12 | Yalnız kendi hesabı olan öğrenciler için |

### Demo hesaplar

Parola hepsinde **`Demo1234!`** — bu bir sır değildir, yalnız staging'de var olan hesaplara aittir.

| Rol | E-posta |
|-----|---------|
| Öğretmen | `demo.ogretmen1@egitimussu.local` · `demo.ogretmen2@egitimussu.local` |
| Öğrenci | `demo.ogrenci1@egitimussu.local` · `demo.ogrenci2@egitimussu.local` |
| Veli | `demo.veli1@egitimussu.local` · `demo.veli2@egitimussu.local` |

### İki kural

**1. Üretimde asla çalışmaz.** `DemoDataSeedGuard` ortam adına bakar; `Production` ise
`Seeding:Enabled=true` olsa **bile** engeller ve `LogWarning` düşer. Bayrağın yanlışlıkla açılması
bir yapılandırma hatasıdır, veri kaybı olmamalıdır.

**2. İdempotenttir.** Her adım önce "bu kayıt zaten var mı" diye sorar; yalnız eksikleri açar ve
**var olana dokunmaz**. Staging'de elle yapılan bir düzenleme sonraki deploy'da geri alınmaz.
İkinci koşumda oluşturulan kayıt sayısı **sıfırdır** — bu, `DemoDataSeederTests` ile doğrulanır.

### Seeder neden doğrudan tabloya yazmıyor?

Her kayıt, o kaydı gerçek hayatta açacak kullanıcının kimliğiyle ilgili **modül komutundan** geçer:
öğretmen profilini öğretmen açar, veli bağını veli ister ve **öğrenci onaylar** (veli kendi bağını
onaylayamaz). Böylece iş kuralları ve yetki kuralları atlanmaz; tohumlanan veri, gerçek
kullanıcıların üretebileceği veriyle birebir aynı biçimde olur. Doğrudan tablo yazımı, yarın bir
domain kuralı değiştiğinde sessizce geçersiz veri bırakırdı.

> ⚠️ **Modüller birbirini olayla tanır.** Parents, bir öğrenciyi ancak öğrenci olayı işlendikten
> sonra bilir ve bilmeden bağ onayını fail-closed reddeder. Bu yüzden seeder, veli adımından önce
> outbox kuyruğunu boşaltır — adımların başarısı arka plan dispatcher'ının periyoduna bırakılamaz.

### Yerelde denemek

```bash
scripts/seed-demo-data.sh                          # InMemory (hızlı)
scripts/seed-demo-data.sh "Host=localhost;Port=5432;Database=egitimussu;Username=...;Password=..."
```

Script uygulamayı `Staging` ortamıyla ayağa kaldırır, `/health/ready` yeşile dönünce tohumlamanın
bittiğini doğrular ve demo hesapları yazdırır.

---

## 3. Ortam değişkenleri

Sırlar repoda tutulmaz; hepsi env'den gelir ve startup'ta guard'lardan geçer (yoksa uygulama açılmaz).

| Değişken | Development | Staging | Production |
|----------|-------------|---------|------------|
| `ConnectionStrings__Postgres` | `InMemory:development` | staging DB | üretim DB |
| `Jwt__SigningKey` | dev anahtarı | Render üretir | Render üretir |
| `Redis__Configuration` | `localhost:6379` | elle girilir (Upstash) | elle girilir (Upstash) |
| `Database__ApplyMigrationsOnStartup` | `false` | `true` | `true` |
| `Seeding__Enabled` | `false` | `true` | `false` *(açık olsa da engellenir)* |
| `Email__Provider` / `Push__Provider` | `Logging` | `Logging` | `Smtp` / `Fcm` |
| `Storage__Provider` | `Local` | `Local` | `S3` |
| `Observability__Enabled` + `__OtlpEndpoint` | `false` | toplayıcı varsa `true` | toplayıcı varsa `true` |

Blueprint: [`render.yaml`](../../render.yaml) — iki web servisi + iki veritabanı.

---

## 4. Deploy ve geri alma

| Adım | Staging | Production |
|------|---------|------------|
| Tetikleyici | Dal push'u | `main` merge'ü |
| Migration | Açılışta otomatik | Açılışta otomatik *(E-05'te ayrı adıma taşınacak)* |
| Sağlık kontrolü | `/health/live` | `/health/live` |
| Geri alma | Render'da önceki deploy | Render'da önceki deploy + gerekiyorsa yedekten geri yükleme |

> **Sürüm kontrolü:** Deploy sonrası `curl <url>/api/meta/version` → `apiVersion` (sözleşme) ve
> `version` (derleme) birlikte döner. Sözleşme kuralları: [`api_versioning.md`](api_versioning.md).

---

> İlgili: backend mimari → [`backend.md`](backend.md) · API sürümleme → [`api_versioning.md`](api_versioning.md) ·
> gözlemlenebilirlik → [`backend.md` §11](backend.md)

*Ortamlar | Güncelleme: 2026-09-05 (P13 Task 3 / E-06: ilk sürüm — staging servisi + ayrı veritabanı, idempotent demo veri tohumlaması ve üretim guard'ı)*
