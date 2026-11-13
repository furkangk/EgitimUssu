---
title: Ortamlar — Development, Staging, Production
summary: "Development / Staging / Production ortamlarinin farklari: hangi saglayici nerede acik, demo veri nereye tohumlanir, migration ve yedekleme nasil isler"
authority: derived
updated: 2026-11-14
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
| Migration | Bayrağa göre (`dotnet ef database update`) | Açılışta otomatik | **Asla açılışta** — ayrı deploy adımı ([`backup_restore.md`](backup_restore.md) §4) |
| Demo veri (`Seeding:Enabled`) | Kapalı (istenirse açılır) | **Açık** | **Yasak** — guard bayrağı yok sayar |
| E-posta | `Logging` (göndermez) | `Logging` (göndermez) | `Smtp` *(Q7 bekliyor)* |
| Push | `Logging` (göndermez) | `Logging` (göndermez) | `Fcm` *(Q8 bekliyor)* |
| Dosya depolama | `Local` (disk) | `Local` | `S3` *(Q4 bekliyor)* |
| JWT imzalama anahtarı | Sabit dev anahtarı | Render üretir (**üretimden ayrı**) | Render üretir |
| Gözlemlenebilirlik | Kapalı (yerelde Jaeger ile açılır) | Kapalı — anahtarlar tanımlı, toplayıcı bekliyor (**§5**) | Kapalı — aynı (**§5**) |
| Yedekleme | Yok | Yok | **Günlük**, 30 gün saklama ([`backup_restore.md`](backup_restore.md)) |
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
| Öğrenci | 5 | **2'si kendi hesabıyla** (mobil giriş + kronometre denenebilsin), 3'ü hesapsız. **Beşi de öğretmenin açtığı profil** (`TeacherManaged`) ve öğretmenle yaşayan bağı var — P15 T2'den beri öğretmen yalnız bağı olduğu öğrenciye ders/ödeme yazabilir. Hesaplı iki öğrencinin profilini öğretmen hesapsız açar, öğrenciyi hedefli davet eder, öğrenci kodu girer (claim) — ürünün kendi yolu (P15 T3 / N-23: profil oluşturma gövdesindeki `userId` artık reddedilir) |
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
öğretmen profilini öğretmen açar, öğrenci hesabı **davet koduyla** bağlanır (öğretmen davet eder, öğrenci
kodu girer) ve veli, öğretmenin ürettiği **veli davet koduyla** bağlanır (kod = öğretmen onayı → bağ doğrudan
onaylı). Böylece iş kuralları ve yetki kuralları atlanmaz; tohumlanan veri, gerçek
kullanıcıların üretebileceği veriyle birebir aynı biçimde olur. Doğrudan tablo yazımı, yarın bir
domain kuralı değiştiğinde sessizce geçersiz veri bırakırdı.

> ⚠️ **Modüller birbirini olayla tanır.** Parents, bir öğrenciyi ancak öğrenci olayı işlendikten
> sonra bilir ve bilmeden bağ onayını fail-closed reddeder. Bu yüzden seeder, veli adımından önce
> outbox kuyruğunu boşaltır — adımların başarısı arka plan dispatcher'ının periyoduna bırakılamaz.
> P15 T3'ten beri veli bağı "veli ister, öğrenci onaylar" yoluyla **kurulmaz**: davetle bağlanan öğrencinin
> kullanıcısı Parents'a P15 T4'e kadar ulaşmıyor (`KnownStudent.UserId` boş), o yol 403 verirdi.
>
> ⚠️ **Davet kodu API'den dönmüyor** (P21 T1 açacak): seeder kodu Students deposundan **salt-okunur** alır,
> yazım yine komutla (claim) yapılır. Yarıda kalmış tur (profil açılmış, claim yapılmamış) kaldığı yerden
> devam eder — profil öğretmenin listesinde adıyla bulunur. P15 T2 ile tohumlanmış bir veritabanı (profil
> öğrencinin hesabıyla açılmış) olduğu gibi kullanılmaya devam eder.

### Yerelde denemek

```bash
scripts/seed-demo-data.sh                          # InMemory (hızlı)
scripts/seed-demo-data.sh "Host=localhost;Port=5432;Database=egitimussu;Username=...;Password=..."
```

Script uygulamayı `Staging` ortamıyla ayağa kaldırır, `/health/ready` yeşile dönünce tohumlamanın
bittiğini doğrular ve demo hesapları yazdırır.

> ⚠️ **P15 T2 öncesi tohumlanmış veritabanı:** o sürümde kendi hesabı olan iki öğrenci `SelfRegistered` profille ve
> öğretmen bağı **olmadan** açılıyordu. Bu bağ komutlarla sonradan kurulamadığı için seeder açılışta
> "P15 T2 öncesi tohumlanmış … Demo veritabanını sıfırlayıp yeniden tohumlayın" hatasıyla durur. Çare: demo
> veritabanını boşaltıp (ya da yeni bir veritabanı adıyla) yeniden tohumla. Staging henüz deploy edilmediği için
> etkilenen yalnız yerel geliştirme veritabanlarıdır.

> ⚠️ **P15 T3 ile (T4 öncesi) tohumlanmış veritabanı — geri doldurma yok:** o sürümde davetle hesaba bağlanan
> öğrencilerin `StudentProfileLinkedToUser` olayı tüketicisiz dağıtıldı; Parents'taki `known_students.UserId` boş kaldı.
> T4'ten sonra bu öğrencilerin velisi panelde çalışma verisini **gizli** görür (fail-closed) ve öğrenci veli talebini
> onaylayamaz. Ayrıca T4 öncesi biten veli bağlarının `student_profiles.ParentUserId`'si temizlenmez (artık yetki
> vermez, yalnız gösterge). Çare yine yeniden tohumlamadır; seeder hata vermeden açılır, belirti yalnız davranıştadır.
> Canlı üretim olmadığı için geri doldurma (backfill) yazılmadı.

---

## 3. Ortam değişkenleri

Sırlar repoda tutulmaz; hepsi env'den gelir ve startup'ta guard'lardan geçer (yoksa uygulama açılmaz).

| Değişken | Development | Staging | Production |
|----------|-------------|---------|------------|
| `ConnectionStrings__Postgres` | `InMemory:development` | staging DB | üretim DB |
| `Jwt__SigningKey` | dev anahtarı | Render üretir | Render üretir |
| `Redis__Configuration` | `localhost:6379` | elle girilir (Upstash) | elle girilir (Upstash) |
| `Database__ApplyMigrationsOnStartup` | `false` | `true` | `false` *(açık olsa da `MigrationStartupGuard` engeller)* |
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
| Migration | Açılışta otomatik | **Ayrı adım**: önce yedek, sonra `dotnet ef database update`, sonra deploy ([`backup_restore.md`](backup_restore.md) §4) |
| Sağlık kontrolü | `/health/live` | `/health/live` |
| Geri alma | Render'da önceki deploy | Kod: önceki deploy · Şema: `dotnet ef database update <öncekiMigration>` · Veri: yedekten geri yükleme ([`backup_restore.md`](backup_restore.md) §4) |

> **Yedekleme:** Üretim veritabanı günlük yedeklenir (30 gün saklama), geri yükleme `--yes` onayı
> ister ve tatbikatı kayıt altındadır: [`backup_restore.md`](backup_restore.md).

> **Sürüm kontrolü:** Deploy sonrası `curl <url>/api/meta/version` → `apiVersion` (sözleşme) ve
> `version` (derleme) birlikte döner. Sözleşme kuralları: [`api_versioning.md`](api_versioning.md).

## 5. Gözlemlenebilirlik (C-04 / K-10)

`render.yaml` her iki serviste de üç anahtarı **tanımlar**, ama `Observability__Enabled` bilerek
`"false"` durur:

| Anahtar | Production | Staging |
|---------|------------|---------|
| `Observability__Enabled` | `"false"` | `"false"` |
| `Observability__OtlpEndpoint` | `sync: false` (panelden girilir) | `sync: false` |
| `Observability__ServiceName` | `egitimussu-api` | `egitimussu-api-staging` |

**Neden kapalı:** `OtlpEndpoint` boşken kod sessizce hiçbir şey yapmak yerine **konsol exporter'a
düşer** — Render'da bu, her SQL komutunun servis log'una akması demektir. Ücretsiz planda log
penceresi bununla dolar ve gerçek hata satırları görünmez olur. Bu yüzden sıra şu:

1. Bir toplayıcı seç (Grafana Cloud / Honeycomb / self-hosted `otelcol`) — **açık soru Q9**
2. `Observability__OtlpEndpoint`'i Render panelinden gir
3. `Observability__Enabled` → `"true"`

**Yerelde denemek** (P13 Task 8'in koştuğu düzenek):

```bash
docker run -d --name jaeger -e COLLECTOR_OTLP_ENABLED=true -p 16686:16686 -p 4317:4317 \
  jaegertracing/all-in-one:1.57
Observability__Enabled=true Observability__OtlpEndpoint=http://localhost:4317 \
  ASPNETCORE_ENVIRONMENT=Staging dotnet run --project src/API.Host/EgitimUssu.API.Host.csproj
# Jaeger arayüzü: http://localhost:16686
```

### Bugün ne görünüyor, ne görünmüyor

| Zincir parçası | Durum |
|----------------|-------|
| HTTP isteği → EF/Npgsql SQL komutları | ✅ **Tek trace içinde**, ebeveyn-çocuk ilişkisiyle |
| Her log satırında `TraceId` + `CorrelationId` | ✅ |
| Hata gövdelerinde `traceId` | ✅ |
| Metrikler (`egitimussu.outbox.pending/dispatched`, `email.sent`, `push.sent`) | ✅ Gerçek OTLP toplayıcısında okundu |
| Sorgu dizesindeki sır (`?token=…`) | ✅ `SensitiveDataMask` ile `***` |
| CQRS komut/sorgu adımı ayrı span | ✅ `command <Tip>` / `query <Tip>` |
| Outbox → handler işinin **isteğin trace'ine bağlanması** | ✅ `outbox_messages.TraceParent` ile (P13 Task 9) |
| Arka plan turlarının kendi span'ı | ✅ `outbox.poll` · `retention.sweep` · `scan.*` (7 tarayıcı) |

> ✅ **Kapandı (P13 Task 9).** Önce şöyleydi: outbox/inbox yoklayıcıları `Activity` açmadığı için
> Npgsql her SQL komutunu **ebeveynsiz kök trace** olarak yayıyordu; Task 8 ölçümünde 1500 trace'in
> **1488'i** bu gürültüydü (yalnız 12'si gerçek HTTP isteği) ve trace'in adı veritabanı adı olarak
> görünüyordu. Task 9'da (a) dispatch/handler/tur adımlarına kendi span'ları eklendi, (b)
> `outbox_messages`'a `TraceParent` sütunu geldi (**15 migration**). Doğrulama koşumunda
> **ebeveynsiz ham DB kök trace'i: 0** (29 trace'in hepsi adlandırılmış) ve ders oluşturma isteği
> **tek trace, 18 span** olarak göründü:
>
> ```
> POST /api/scheduling/lessons
> └── command CreateLessonScheduleCommand
>     ├── SELECT EXISTS (çakışma) · INSERT INTO scheduling.lesson_schedules
>     └── outbox.dispatch LessonScheduledDomainEvent
>         ├── handler LessonScheduleNotificationIntegrationEventHandler
>         └── handler LessonScheduleUpsertProjectionHandler
>             └── outbox.dispatch LessonReminderCreatedDomainEvent
> ```
>
> Ayrıntı: [`backend.md` §11.5](backend.md).

> ⚠️ **`Logging` e-posta sağlayıcısı PII yazar.** Development ve Staging'de `Email__Provider=Logging`
> olduğu için doğrulama e-postasının **alıcısı ve jetonu** servis log'una düşer. Bu bilinçli bir
> geliştirme kolaylığıdır (staging'de gerçek e-posta gönderilmiyor) ama staging log'u bu yüzden
> **kişisel veri içerir** ve paylaşılmamalıdır. Üretimde sağlayıcı `Smtp` olduğu için bu satır oluşmaz.

---

---

> İlgili: backend mimari → [`backend.md`](backend.md) · API sürümleme → [`api_versioning.md`](api_versioning.md) ·
> yedekleme/geri yükleme → [`backup_restore.md`](backup_restore.md) ·
> gözlemlenebilirlik → [`backend.md` §11](backend.md)

*Ortamlar | Güncelleme: 2026-11-14 (P15 Task 4 / N-27: T3 ile tohumlanmış veritabanında davetle bağlanan öğrencilerin Parents eşlemesi boş — yeniden tohumlama notu §2) · 2026-11-14 (P15 Task 3 / N-23: demo öğrenci hesapları davet + claim ile, veli bağları veli davet koduyla kurulur; §2) · 2026-11-13 (P15 Task 2 / N-08: demo öğrencilerinin beşi de öğretmen bağlı `TeacherManaged` profil; hesaplı ikisi öğrencinin hesabıyla açılır. Eski demo veritabanı için sıfırlama notu §2) · 2026-09-06 (P13 Task 9: §5 tablosundaki iki ❌ **kapandı** — komut/sorgu span'ları ve outbox→handler trace bağı; ölçüm ebeveynsiz kök trace'i 1488'den **0'a** indirdi) · 2026-09-05 (P13 Task 8: §5 **Gözlemlenebilirlik** eklendi — `render.yaml`'a `Observability__*` anahtarları girildi (varsayılan kapalı, gerekçesiyle), staging profilinde canlı doğrulama sonuçları ve **outbox trace zinciri açığı** tablolandı) · 2026-09-05 (P13 Task 4 / E-05: üretimde açılış migration'ı **kapatıldı** — ayrı deploy adımı; yedekleme satırları eklendi) · 2026-09-05 (P13 Task 3 / E-06: ilk sürüm — staging servisi + ayrı veritabanı, idempotent demo veri tohumlaması ve üretim guard'ı)*
