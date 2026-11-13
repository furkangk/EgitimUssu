---
title: "Rakip Paritesi ve Farklılaşma Analizi (2026-09-05)"
summary: "Rakiplerde olup bizde olmayan (R-01…R-14) ve hiçbir rakipte olmayıp bizde olabilecek (Ü-01…Ü-10) maddelerin kod gerçeğine karşı envanteri; her madde için kanıt, etki, çözüm taslağı, kabul kriteri, risk, efor ve karar sütunu"
tags: [denetim, rakip, parite, farklilasma, envanter, planlama]
authority: derived
updated: 2026-09-05
---

# ⚔️ EğitimÜssü — Rakip Paritesi ve Farklılaşma Analizi

> **Tarih:** 2026-09-05
> **Soru:** *"Onlarla aynı seviyeye gelmek ve onlardan öne çıkmak için ne yapmam gerekiyor?"*
> **Kapsam:** [Rakip analizi](../kaynaklar/2026-09-05_rakip_analizi.md)'nde incelenen 11 ürün (Doping Hafıza, Pakodemy,
> Net Up, DUS Club, VeliCep + Tilki, Özel Ders Alanı, Öğretsen, Öğretmenim.app, Özel Ders Takip Sistemi) ×
> EğitimÜssü'nün **bugünkü kod gerçeği** (`src/` 16 modül, `mobile/lib/` 52 ekran / 67 rota).
> **Yöntem:** Her rakip özelliği için kodda karşılığı arandı (endpoint envanteri, domain sınıfları, seed verisi,
> `grep`). "Bizde yok" diyen her madde koda karşı doğrulandı; "bizde var" diyen her madde `dosya:satır` ile
> kanıtlandı. Çözüm taslakları **mevcut desenlere** dayanır (projeksiyon, outbox, entitlement, depolama);
> uydurma mimari önerilmedi. Doküman iddiası ile kod çeliştiğinde **kod esas alındı** (bkz. §3 — üç çelişki çıktı).
> **İlgili:** [`../kaynaklar/2026-09-05_rakip_analizi.md`](../kaynaklar/2026-09-05_rakip_analizi.md) (pazar tarafı) ·
> [`2026-09-02_eksik_analizi.md`](2026-09-02_eksik_analizi.md) (iç eksikler) ·
> [`../ozel_ders_platformu_PRD.md`](../ozel_ders_platformu_PRD.md) · [`../../docs/superpowers/ILERLEME.md`](../../docs/superpowers/ILERLEME.md)

---

## 0. Bu dokümanı nasıl kullanacaksın

[`2026-09-02_eksik_analizi.md`](2026-09-02_eksik_analizi.md) ile **aynı şema**, üç alan eklenmiş
(*çözüm taslağı*, *kabul kriteri*, *risk*) — böylece karar verdiğin madde doğrudan plan görevine çevrilebilir.

| Alan | Anlamı |
|------|--------|
| **ID** | `R-nn` = **parite** (rakipte var, bizde yok) · `Ü-nn` = **üstünlük** (hiçbir rakipte yok, bizde olabilir) · `X-nn` = **çelişki** (doküman ≠ kod) |
| **Rakip kanıtı** | Hangi üründe, nasıl doğrulandı (mağaza açıklaması / site / API verisi) |
| **Bizdeki durum** | Kodda ne var, ne yok — `dosya:satır`, endpoint veya `grep` sonucu |
| **Etki** | Bu olmayınca ne kaybediyoruz / olunca ne kazanıyoruz |
| **Çözüm taslağı** | Hangi modül, hangi entity/olay, hangi uç, hangi mobil ekran, hangi test. **Mevcut desenlere dayanır** |
| **Kabul kriteri** | "Bitti" ne demek — doğrulanabilir maddeler |
| **Risk / tuzak** | Bu işi yaparken nerede tökezlenir |
| **Efor** | **S** ≤1 gün · **M** 2–4 gün · **L** 1–2 hafta · **XL** 2+ hafta. Backend + mobil + test dahil |
| **Karar** | **SEN DOLDURACAKSIN.** `✅ Yap` · `⏳ Sonra` · `❌ İptal` · `❓ Konuşalım` |

**Akış:** (1) Karar sütunlarını doldur → (2) "planı çıkar" de → (3) `docs/superpowers/plans/` altına
bağımlılık sıralı dilim planı üretilir (mevcut P11–P14 ile birleştirilerek).

**Lejant (etki):** 🔴 Rekabette kaybettiriyor · 🟠 Ana akışı zayıflatıyor · 🟡 İkincil · 🟢 Farklılaşma fırsatı

---

## 1. Mevcut durum fotoğrafı

### 1.1 Rakip merceğinden bizde ne var

> Bu tablo analiz sırasında **koddan** çıkarıldı. Rakip analizi turunda "bizde yok" sandığım birkaç şey
> aslında **var** çıktı; aşağısı gerçek durumdur.

| Rakip özelliği | Bizdeki karşılığı | Kanıt |
|----------------|-------------------|-------|
| Çalışma kronometresi | ✅ Var — başlat/duraklat/devam/tamamla/iptal/**kurtar** + manuel giriş | `StudyModule.cs` → `/sessions/start`, `/pause`, `/resume`, `/complete`, `/discard`, `/recover`, `/sessions/manual` |
| Günlük seri (streak) | ✅ Var — güncel seri, en uzun seri, toplam gün, bugün çalıştı mı, bugünkü dakika | `StudyStreak` (`StudyDomainModel.cs:592`) · `StudyProgressFeatures.cs:151-157` · `GET /students/{id}/streak` |
| Anlamlı seri kuralı | ✅ Var — gün, günlük hedefin ayarlanabilir yüzdesi (`StreakThresholdPercent`, varsayılan %60; hedef yoksa 20 dk) tamamlanınca sayılır; gün sınırı **04:00** | `roles/ogrenci.md` §8 · migration `AddStreakThresholdPercent` |
| Rozet / başarım | ✅ Var — **10 tohumlanmış rozet**, eşik tabanlı otomatik kazanım | `StudyDbContext.cs:140-150` · `AchievementEvaluator` (`StudyPolicies.cs:178`) · mobil `achievements_page.dart` |
| Deneme sınavı + net trendi | ✅ Var — çok dersli deneme, sınav tipine göre net böleni | `MockExam` (`:466`), `TestResult` (`:320`) · `/students/{id}/mock-exams`, `/test-results`, `/net-trend` |
| Konu/kazanım kataloğu | ✅ Var — öğrenci bazlı ders + konu CRUD | `StudentSubjectCatalog` / `StudentTopicCatalog` (`StudyCatalogModel.cs`) · `/students/{id}/subjects`, `/subjects/{id}/topics` |
| Hedef | ✅ Var — günlük dakika hedefi + hedef net/puan (`TargetNet`/`TargetScore`) | `StudyGoal` (`:513`) · `/students/{id}/goals` |
| Öğrenci notu | ✅ Var | `StudyNote` (`StudyCatalogModel.cs:107`) · `/students/{id}/notes` |
| Konu hâkimiyeti + gelişim eğrisi | ✅ Var (M10 🟢) — snapshot + trend | `/api/progress-tracking` |
| Veli gelişim görünümü | ✅ Var (M09 🟢) — 11 uç | `/api/parents` |
| Rapor + PDF | ✅ Var (M14 🟢) — QuestPDF, premium kapılı | `/students/{id}/report`, `/reports/{id}/file` |
| Öğretmen boş zaman analizi | ✅ Var — **rakiplerin hiçbirinde yok** | `FreeSlotCalculator.cs` · `/teachers/{id}/free-slots` |
| Ders materyali paylaşımı | ✅ Var — dosya veya bağlantı; `StudentId` null ise **tüm öğrencilere açık** | `LessonResource` (`AssignmentsDomainModel.cs:368`), `LessonResourceKind{File,Link}` |
| Ödev akışı | ✅ Var — atama, teslim, onay/geri gönderme, puanlama, son teslim uyarısı | `Assignment` + `AssignmentStatus` |
| Mesajlaşma | 🟡 Var (M16 🟡) — 1-1 sohbet, 8 uç, kullanıcı başına 30/dk kota | `Conversation`/`Message` · `ParticipantRole{Teacher,Student,Parent}` |
| Push + bildirim merkezi | ✅ Var (M11 🟢) — FCM, cihaz token, çift-push koruması | `UserNotification`, `DeviceToken`, `UserNotificationKind` (9 tip) |
| Üyelik + deneme + referans | 🟡 Var (M17 🟡) — plan/deneme/referans/entitlement; **checkout yok** | `/api/membership` 9 uç |
| Çevrimdışı dayanıklılık | 🟡 Kısmi — `LocalCache` auth/scheduling/payments/students'ta | `mobile/lib/core/storage/local_cache.dart` |

**Sonuç:** Öğrenci tarafındaki "alışkanlık motoru"nun **iskeleti zaten kurulu**. Eksik olan, o iskeletin
**sosyal ve döngüsel** kısmı (§4) ile bu verilerin **birleştirilmesinden doğan** üstünlük (§5).

### 1.2 Elimizdeki domain olayları — yeni özelliklerin bedava yakıtı

Aşağıdaki olaylar **zaten üretiliyor**. §4–§5'teki projeksiyonların çoğu yeni olay yazmadan, sadece
bu olayları dinleyerek kurulabilir:

| Olay | Modül | Ne zaman | Hangi maddede kullanılır |
|------|-------|----------|--------------------------|
| `StudySessionStartedDomainEvent` | M08 | Kronometre başlatılınca | R-01, Ü-01 |
| `StudySessionCompletedDomainEvent` | M08 | Seans tamamlanınca (efektif dakika ile) | **R-01, R-03, Ü-01, Ü-02, Ü-07** |
| `TestResultRecordedDomainEvent` | M08 | Test/deneme girilince | Ü-02, Ü-08 |
| `StudyGoalUpdatedDomainEvent` | M08 | Hedef değişince | R-03 |
| `StreakMilestoneReachedDomainEvent` | M08 | Seri eşiği aşılınca | R-10, Ü-07 |
| `StreakBrokenDomainEvent` | M08 | Seri kırılınca | **R-10** (hatırlatıcı), Ü-08 |
| `AchievementEarnedDomainEvent` | M08 | Rozet kazanılınca | R-01 (lig bildirimi) |
| `MessageSentDomainEvent` | M16 | Mesaj gönderilince | R-06 (duyuru fan-out'u aynı yolu izler) |

Ayrıca M05 (ders tamamlandı/iptal), M07 (ödeme beklendi/tahsil edildi) ve M10 (konu hâkimiyeti)
olayları M14 projeksiyonlarını zaten besliyor — Ü-01/Ü-02/Ü-04/Ü-08 aynı borulardan içer.

### 1.3 Yerleşik desenler — yeni iş bunların üstüne biner

| Desen | Nerede | Ne sağlar |
|-------|--------|-----------|
| **Projeksiyon (read-model)** | `ProjectionEntity` (Shared.Kernel) + `ProjectionHandler<TEvent>` + `ReportingProjectionHandler<TEvent>` | Olaydan okuma modeli üretme; **yeniden kurulabilirlik gerçek Postgres'te kanıtlanmış** (P08 Task 6, `RealProjectionRebuildTests`) |
| **Yeniden inşa** | `IProjectionRebuilder` (Shared.Infrastructure) | Projeksiyon bozulursa sıfırdan oynatma |
| **Outbox / inbox** | Her modülde `outbox` + `inbox_messages` tabloları | Modüller-arası güvenilir teslimat, tekrar-güvenli (idempotent) |
| **Entitlement** | `IEntitlementDirectory` + `Entitlement` + `MembershipGate` | Premium kapısının **tek kaynağı**; fail-safe (okunamazsa Free) |
| **Dosya depolama** | S3/MinIO (P04, canlı doğrulandı) + `LessonResource` | Dosya yükleme/erişim; imzalı URL deseni mevcut |
| **Bildirim** | `UserNotification` + `NotificationDispatcher` + `DeviceToken` + FCM | In-app + push, `PushedOnUtc` ile çift bildirim koruması |
| **Gizlilik kapısı** | `GET/PUT /students/{id}/sharing` + M15'in üç sözleşmesi | Öğrencinin neyi paylaşacağını seçmesi |
| **Test zinciri** | 4 mimari + 683 birim + 44 integration (gerçek Postgres+Redis) + 193 mobil | Yeni işin regresyon ağı hazır |

> **Okuma:** §4–§5'teki maddelerin çoğunun eforu düşük çünkü **yeni altyapı gerekmiyor**.
> Pahalı olanlar (Ü-03, R-09, R-13) yeni bir yetenek sınıfı açtığı için pahalı.

---

## 2. Tek bakışta parite tablosu

| Rakip özelliği | Kimde var | Bizde | Madde | Efor |
|----------------|-----------|-------|-------|------|
| Uygulama dışı paylaşım (WhatsApp/link) | Tilki, Net Up | ❌ | **R-08** | S + M |
| Bire-çok duyuru | VeliCep, Doping, Pakodemy | ❌ | **R-06** | S–M |
| Zamanlanmış yerel hatırlatıcı | Net Up (19:00 + 20:00) | ❌ (paket kurulu, kullanılmıyor) | **R-10** | S |
| Fotoğraf/video paylaşımı (galeri) | VeliCep | 🟡 dosya var, galeri yok | **R-07** | S |
| Günlük görev döngüsü | Net Up, Pakodemy | ❌ | **R-03** | M |
| Haftalık lig / sıralama | Net Up, DUS Club | ❌ | **R-01** | M |
| Ekip / kulüp (kod ile katılım) | DUS Club | ❌ | **R-02** | M |
| Flashcard + aralıklı tekrar | DUS Club, Doping | ❌ | **R-05** | M |
| Gerçek para tahsilatı | Hepsi | ❌ (Q1 bloke) | **R-09** | L |
| Öğretmen arama / filtreleme | Öğretsen, Özel Ders Alanı | 🔴 M12 iskelet | **R-11** | L |
| Puan / yorum | Öğretsen | 🔴 M13 iskelet | **R-12** | M |
| Web erişimi | Tilki, Doping, Pakodemy | ❌ (P14) | **R-13** | XL |
| XP / seviye / ödül sandığı | Net Up | ❌ | **R-04** | S *(iptal önerisi)* |
| Soru bankası / video içerik | Doping, Pakodemy, Net Up, DUS Club | ❌ | **R-14** | XL *(iptal önerisi)* |

---

## 3. 🔴 X — Doküman ile kod çelişkileri

### X-01 — PRD §9.2 streak'i Premium sayıyor, kod **Free** veriyor ✅ PRD düzeltildi 2026-09-05

- **Kanıt (kod):** `src/Shared/Contracts/MembershipGate.cs:31-36` sınıf yorumu:
  *"Free = çekirdek alışkanlık (kronometre, test, **streak tam**, son 30 gün geçmiş, temel haftalık analiz);
  Premium = derinlik (sınırsız geçmiş + tüm `PremiumFeature` özellikleri)."*
  `PremiumFeature` enum'unda streak'in kendisi değil yalnız **`StreakFreeze`** (seri dondurma hakkı) var.
  `FreeHistoryWindowDays = 30` → geçmiş kayıtlar Free'de 30 gün görünüyor. Kapılar `MembershipGate.ClampFrom`
  (`StudyTestFeatures.cs:244,270`, `StudySessionFeatures.cs:566`) ve `MembershipGate.Allows(tier,
  PremiumFeature.TargetTracking)` (`StudyProgressFeatures.cs:98`) ile uygulanıyor.
- **Kanıt (doküman):** PRD §9.2 tablosu "Streak (seri gün) | ❌ | ✅", "Motivasyon sistemi | ❌ | ✅",
  "Geçmiş çalışma kayıtları | ❌ | ✅", "Haftalık / aylık analiz | ❌ | ✅" diyordu. §9.1 ise Free öğrenci
  limitini "5–10" gösteriyordu; kodda `MembershipFeatures.FreeMaxStudents = 5`.
- **Neden önemli:** Rakip analizi turunda "streak'i paywall'un arkasına koymak dönüşümü öldürür" uyarısı
  **PRD'ye bakılarak** yazılmıştı. Kod zaten doğru olanı yapıyor — **Net Up modeliyle aynı hizada**
  (alışkanlık ücretsiz, derinlik ücretli). Yapılacak iş kod değişikliği değil, doküman düzeltmesiydi.
- **Yapılan (2026-09-05):** PRD §9.1 (limit 5) ve §9.2 (kapı sütunlu yeni tablo + tasarım kararı notu)
  yeniden yazıldı; aynı bayat liste `m17_membership.md` §1 ve §7'de, `roles/ogrenci.md` §14.3'te düzeltildi
  (orada ayrıca kapı kaynağı `IMembershipDirectory` → `IEntitlementDirectory` olarak güncellendi).
- **Efor:** S (yapıldı) · **Karar:** — *(düzeltme, karar gerektirmiyor)*

### X-02 — `Entitlement` kaydında iki alan tanımlı ama hiçbir yerde okunmuyor 🟡

- **Kanıt:** `src/Shared/Contracts/EntitlementContract.cs:19-20` → `DetailedAnalytics` ve `RichNotifications`
  alanları tanımlı ve `MembershipFeatures.cs:31,33,39,41`'de doldurulmuş. Tüketim tarafında
  `grep -rn "DetailedAnalytics\|RichNotifications" src/Modules/` yalnız **doldurma** satırlarını buluyor;
  hiçbir kapı bu iki alanı **okumuyor**. Bugün gerçekten uygulanan kapılar: `MaxStudents`
  (`StudentProfileFeatures.cs:173`), `PdfReports` (`StudentReportFeatures.cs:335`), `Tier` → `MembershipGate` (Study).
- **Etki:** "Zengin bildirim" ve "detaylı analiz" premium vaadi **sözleşmede var, kodda yok**.
  Premium alan kullanıcı Free ile aynı bildirimi alıyor. Ödeme açıldığında (R-09) bu bir iade sebebi olur.
- **Çözüm taslağı:** İki yol var, biri seçilmeli:
  (a) **Bağla** — `RichNotifications`'ı `NotificationDispatcher`'da haftalık özet/ödeme uyarısı üretimine,
  `DetailedAnalytics`'i M14'ün aylık analiz uçlarına kapı olarak koy;
  (b) **Kaldır** — alanları sözleşmeden çıkar, `MembershipGate` + `MaxStudents` + `PdfReports` yeter.
  **Öneri: (a)**, çünkü Ü-08 (erken uyarı) zaten "zengin bildirim" kategorisinde satılacak.
- **Kabul kriteri:** Free kullanıcı haftalık özet bildirimi almıyor · Premium alıyor · integration testi ikisini de gösteriyor.
- **Risk:** Kapıyı sonradan koymak, bugün bildirim alan Free kullanıcıdan özelliği geri almak demek —
  ödeme açılmadan (R-09) önce yapılırsa kimse fark etmez, sonra yapılırsa şikâyet gelir. **Sıralama önemli.**
- **Efor:** S · **Karar:** ` ` · **Not:** ` `

### X-03 — `AchievementCategory.Goal` tanımlı ama hiç rozet kullanmıyor 🟡

- **Kanıt:** `StudyDomainModel.cs:874` enum'da beş değer var (`Streak`, `StudyTime`, `TestPerformance`,
  `Goal`, `Consistency`); `StudyDbContext.cs:140-150` seed'inde **`Goal` kategorisinde tek rozet yok**
  (`grep -c "AchievementCategory.Goal" StudyDbContext.cs` → **0**). Ayrıca `StudyMetrics.ValueFor`
  (`StudyPolicies.cs:228`) `Goal` için bir metrik döndürmüyor.
- **Etki:** Ölü kod değil ama **yarım desen**: hedefini tutturan öğrenciyi ödüllendiren rozet yok.
  R-03 (günlük görev döngüsü) tam olarak bu boşluğu dolduracak yerdir.
- **Çözüm taslağı:** `StudyMetrics`'e "hedefi tutturulan gün sayısı" metriği ekle
  (`StudySession` + `StudyGoal`'dan hesaplanır, `StudyGoalUpdatedDomainEvent` gerekmez), seed'e
  `GOAL_7` / `GOAL_30` rozetlerini ekle. R-03 ile aynı işte yapılmalı.
- **Kabul kriteri:** `GET /students/{id}/achievements` `Goal` kategorisinde en az iki rozet döndürüyor ·
  eşik aşılınca `AchievementEarnedDomainEvent` üretiliyor.
- **Efor:** S *(R-03 içinde)* · **Karar:** ` ` · **Not:** ` `

---

## 4. 🟠 R — Parite: rakiplerde var, bizde yok

### R-08 — Uygulama dışı paylaşım yok (WhatsApp / link / özet) 🔴

- **Rakip kanıtı:** **Tilki**'nin bel kemiği — *"Veliler ders, ödev ve ödeme bildirimlerini **uygulama
  indirmeden** WhatsApp'tan alır."* Ürün sayfası bunu bir özellik değil, **tasarım kararı** olarak anlatıyor.
  **Net Up** haftalık veli raporunu WhatsApp / e-posta / SMS ile paylaştırıyor (App Store açıklaması).
- **Bizdeki durum:** PDF üretiliyor ve cihazda açılıyor (`open_filex`, P08 Task 5), ama **paylaşım yok**:
  `mobile/pubspec.yaml`'da `share_plus` **yok**; `grep -i whatsapp mobile/lib` → **boş**.
  Veli, veriyi görmek için **uygulamayı indirmek + hesap açmak + veli bağı kurmak** zorunda.
- **Etki:** 🔴 Ürünün en yüksek sürtünme noktası ve en büyük sessiz kayıp kaynağı. Özel derste veli
  çoğu zaman uygulama indirmez; rakip bunu kabul edip WhatsApp'a teslim olmuş, biz zorunlu tutuyoruz.
  Öğretmen de "veliye göstereceğim" diyemediği için platformu ikinci sınıf araç olarak kullanıyor.
- **Çözüm taslağı — iki katman:**
  - **(a) Cihazdan paylaşım [S]** — mobil: `share_plus` ekle; `ReportingRepository`'nin ürettiği PDF'i
    ve *hazır özet metnini* paylaş. Özet metni **istemcide** üretilir (yeni uç gerekmez), kaynağı
    mevcut `/students/{id}/report` yanıtı. Giriş noktaları: `/student/reports`, veli paneli, öğretmenin
    öğrenci detayı. Metin şablonu `mobile/lib/features/reporting/domain/` altına saf fonksiyon olarak
    yazılır → birim testlenebilir.
  - **(b) Giriş gerektirmeyen özet linki [M]** — backend: `Reporting` modülüne `ShareLink` entity'si
    (`Id`, `StudentId`, `CreatedByUserId`, `Scope`, `ExpiresAtUtc`, `RevokedOnUtc`, `TokenHash`).
    Uçlar: `POST /api/reporting/students/{id}/share-link` (imzalı token üretir),
    `GET /api/reporting/shared/{token}` (**anonim**, yalnız okunur özet döner).
    Token **hash'lenerek** saklanır (parola deseni gibi); varsayılan ömür 7 gün; veri kapsamı
    `/students/{id}/sharing` gizlilik ayarına saygı duyar.
- **Kabul kriteri:**
  - (a) Öğretmen ve veli, tek dokunuşla WhatsApp'a düşen bir özet metni + PDF paylaşabiliyor.
  - (b) Link paylaşılan kişi **giriş yapmadan** özet görebiliyor; süre dolunca 410 dönüyor; üreten kişi iptal edebiliyor.
  - Özet metninde öğrenci **tam adı** ve iletişim bilgisi geçmiyor (yalnız ad + baş harf).
  - Integration testi: link üret → anonim eriş → iptal et → 403/410.
- **Risk / tuzak:**
  - **KVKK:** Anonim erişilebilir link, çocuk verisi taşıyor. Kapsam dar tutulmalı (dakika, ödev sayısı,
    net trendi — ders içeriği ve mesaj **asla**), süre kısa olmalı, iptal edilebilmeli. P13 Task 5 (Q5) ile hizalı gitmeli.
  - **Tahmin edilebilir token:** `Guid` yeterli değil; kriptografik rastgele + hash saklama.
  - Paylaşım metni çok uzun olursa WhatsApp'ta kırpılır — 4 satırı geçmemeli.
- **Bağımlılık:** Yok. (b) için M15 gizlilik ayarı okunmalı (mevcut).
- **Efor:** S (a) + M (b) · **Karar:** ` ` · **Not:** ` `

### R-06 — Bire-çok duyuru yok 🔴

- **Rakip kanıtı:** **VeliCep** — *"Sınıf veya okul genelinde anlık duyuru yayınlayın"*, öğretmen
  tarafının üç ana özelliğinden biri. Doping ve Pakodemy'de de duyuru/bildirim akışı var.
- **Bizdeki durum:** M16 yalnız **1-1 `Conversation`** modeli: `ParticipantRole{Teacher,Student,Parent}`,
  iki katılımcı, `MessagingErrors.PairNotAllowed` çifti kısıtlıyor. Öğretmen 10 öğrencisine aynı şeyi
  söylemek için **10 sohbet açmak** zorunda (üstelik kullanıcı başına 30 mesaj/dk kotası var).
- **Etki:** 🔴 "Yarınki ders iptal", "Pazar deneme sınavı var", "kitap getirin" — özel derste en sık
  iletişim ihtiyacı bu ve karşılanmıyor. Öğretmen WhatsApp grubuna kaçıyor; iletişim platformu terk edince
  **geri kalan her şey de terk ediliyor**. Terk riski en yüksek madde.
- **Çözüm taslağı:** Duyuru **sohbet değildir** — ayrı bir kavram olarak modellenmeli, yoksa M16'nın
  çift-katılımcı değişmezleri bozulur.
  - **Domain (M16):** `Announcement` (`Id`, `TeacherUserId`, `Title`, `Body`, `Audience`, `CreatedOnUtc`,
    `ArchivedOnUtc`) + `AnnouncementRecipient` (`AnnouncementId`, `UserId`, `ReadOnUtc`).
    `Audience` enum: `AllStudents`, `AllParents`, `Both`, `SelectedStudents`.
  - **Uçlar:** `POST /api/messaging/announcements`, `GET /api/messaging/announcements` (öğretmen: gönderdikleri;
    öğrenci/veli: aldıkları), `POST /api/messaging/announcements/{id}/read`, `DELETE .../{id}` (yumuşak arşiv,
    `LessonResource`'un `Archive` desenini izler).
  - **Alıcı çözümü:** `IStudentDirectory` (öğretmenin öğrencileri) + `IParentAccessDirectory` (o öğrencilerin
    velileri) — **ikisi de mevcut**, M16 Task 2'de zaten kullanılıyor.
  - **Bildirim:** `AnnouncementPublishedDomainEvent` → outbox → M11'de handler →
    `UserNotificationKind`'a **yeni değer `AnnouncementPublished = 10`** + push. Fan-out outbox üzerinden
    yapılır ki 30 alıcılı duyuru istek zaman aşımına uğramasın.
  - **Kota:** Mesaj kotasından ayrı — öğretmen başına **günde 10 duyuru** gibi bir sınır (P10'da düzeltilen
    `UseAuthentication` sırası tuzağına dikkat: kota kullanıcı bazlı olmalı, IP'ye düşmemeli).
  - **Mobil:** Öğretmen "Duyuru gönder" (öğrenci listesi ekranından), öğrenci/veli gelen kutusunda
    duyurular ayrı sekme/başlık; okunmamış rozeti mevcut bildirim merkezine bağlanır.
- **Kabul kriteri:**
  - Öğretmen tek işlemde tüm öğrencilerine + velilerine duyuru gönderiyor; her alıcı **bir** bildirim alıyor.
  - Duyuruya **cevap verilemiyor** (sohbet değil) — cevap isteyen kullanıcı ilgili sohbete yönlendiriliyor.
  - Öğretmen kimin okuduğunu görüyor.
  - `RealMessagingChainTests` tarzı gerçek-Postgres testi: 1 duyuru → N alıcı → N bildirim, tekrar teslimde çoğalmıyor.
- **Risk / tuzak:**
  - **Fan-out N+1:** Alıcı başına ayrı komut çalıştırılırsa 30 öğrencide 30 SaveChanges olur — tek toplu
    ekleme + outbox kullanılmalı.
  - **Spam:** Kota konmazsa öğretmen günde 50 duyuru atıp bildirimleri değersizleştirir.
  - **Yanlış kapsam:** Veli, çocuğunun öğretmeninden gelen duyuruyu görmeli; ilişkisi bitmiş öğretmenden görmemeli
    (`IParentAccessDirectory` bunu zaten çözüyor, atlanmamalı).
- **Bağımlılık:** M16 (🟡, mevcut) · M11 (🟢, mevcut).
- **Efor:** S–M · **Karar:** ` ` · **Not:** ` `

### R-10 — Zamanlanmış yerel hatırlatıcı yok 🟠

- **Rakip kanıtı:** **Net Up** — *"Akşam 19:00 hatırlatıcı + 20:00 günlük seri uyarısı."* Uygulamanın
  günlük açılışını taşıyan mekanizma bu; streak tek başına hatırlatıcısız çalışmıyor.
- **Bizdeki durum:** `flutter_local_notifications: ^18.0.1` **pubspec'te kurulu ama `lib/` içinde hiç
  kullanılmıyor** (`grep -rn "FlutterLocalNotifications" mobile/lib` → **boş**). Yani hem özellik eksik,
  hem de bu, `D-18` (kullanılmayan ağır bağımlılık) maddesinin hâlâ açık olan kısmı.
- **Etki:** 🟠 Streak var ama kimse hatırlatmıyor → seri kırılıyor → alışkanlık kurulmuyor.
  Streak'e yapılan yatırımın getirisi hatırlatıcı olmadan düşük kalıyor. Ayrıca sunucu push'u
  (M11) internet ve FCM'e bağlı; yerel hatırlatıcı **çevrimdışı da** çalışır.
- **Çözüm taslağı:** Tamamen **mobil** iş, backend değişikliği yok.
  - `mobile/lib/core/notifications/local_reminder_service.dart`: zamanlama, iptal, izin akışı.
  - İki hatırlatıcı: (1) **çalışma hatırlatıcısı** — kullanıcının seçtiği saat, varsayılan 19:00;
    (2) **seri koruma uyarısı** — 20:00, *yalnız o gün eşik dolmadıysa*. Eşik durumu `GET /students/{id}/streak`
    yanıtındaki `todayMinutes` + günlük hedeften hesaplanır (uç zaten var).
  - Zamanlama uygulama her açıldığında yeniden kurulur (yerel bildirimler cihazda tutulur, sunucu bilmez).
  - Ayar: M15 bildirim tercihleri ekranına iki anahtar + saat seçici.
  - iOS/Android izin akışı: reddedilirse sessizce devre dışı, ayarlar ekranında durum gösterilir.
- **Kabul kriteri:**
  - Kullanıcı saati değiştirince eski bildirim iptal olup yenisi kuruluyor (çift bildirim yok).
  - O gün eşiği dolduran öğrenci 20:00 uyarısını **almıyor**.
  - Bildirim kapalıyken hiçbir yerel bildirim planlanmıyor.
  - `flutter test` ile servis birim testleniyor (sahte zamanlayıcı ile).
  - `pubspec.yaml`'da kullanılmayan paket kalmıyor → **D-18 kapanıyor**.
- **Risk / tuzak:**
  - **Çift bildirim:** Sunucu push'u (M11) ile yerel hatırlatıcı aynı anda düşerse kullanıcı iki bildirim görür.
    Kapsamları ayrılmalı: sunucu = olay bildirimi (ödev, mesaj, ödeme), yerel = alışkanlık hatırlatıcısı.
  - **Saat dilimi ve yaz saati:** Yerel bildirimlerde `tz` paketiyle doğru saat dilimi kurulmalı; yoksa
    hatırlatıcı yanlış saatte düşer.
  - iOS'ta uygulama uzun süre açılmazsa planlanmış bildirim sayısı sınırlıdır (64) — tekrar eden bildirim kullanılmalı.
- **Bağımlılık:** Yok. **P13 Task 6 (mobil hijyen) ile birlikte yapılmalı** — D-18 orada kapanıyor.
- **Efor:** S · **Karar:** ` ` · **Not:** ` `

### R-07 — Fotoğraf/video paylaşımı (galeri) yok 🟡

- **Rakip kanıtı:** **VeliCep** — *"Öğretmenin paylaştığı fotoğraf ve videolara **tarihe göre** göz atın,
  indirin."* Öğretmen tarafında *"fotoğraf ve video yükleyin, sınıfınızın anlarını velilerle paylaşın."*
- **Bizdeki durum:** Yapı taşları **hazır**: `LessonResource` (`File`/`Link`, `StudentId` null ise tüm
  öğrencilere açık, yumuşak arşiv), S3/MinIO depolama canlı doğrulanmış (P04), mobilde `image_picker` kurulu.
  Eksik olan **tarih eksenli galeri görünümü** ve kaynağın ders/oturuma bağlanması.
- **Etki:** 🟡 Veli için somut kanıt ("çocuğum bu hafta bunu çözdü" fotoğrafı) rakipte var, bizde yok.
  Anaokulunda beklenti hâline gelmiş bir davranış; özel derste karşılığı **çözülmüş soru fotoğrafı**
  ve tahta görüntüsü. Ü-01'in (doğrulanmış karne) görsel ayağı.
- **Çözüm taslağı:**
  - Backend: yeni entity **gerekmiyor**. `LessonResource`'a `MediaKind` (`Image`/`Video`/`Document`) türetimi
    içerik tipinden yapılır; `GET /api/assignments/students/{id}/gallery?from=&to=` — tarihe göre gruplanmış,
    keyset sayfalı liste. Veli erişimi `IParentAccessDirectory` üzerinden.
  - Mobil: `features/assignments` altına galeri ekranı — ay başlıklarıyla ızgara, tam ekran görüntüleyici,
    indirme (`open_filex` mevcut). Öğretmen tarafında çoklu yükleme (`image_picker` mevcut).
- **Kabul kriteri:** Veli, çocuğunun materyallerini tarihe göre görüyor ve indirebiliyor · başka öğrencinin
  materyali görünmüyor · arşivlenmiş kaynak listede çıkmıyor · 50 öğeli galeri tek istekte değil sayfalı geliyor.
- **Risk / tuzak:**
  - **Video boyutu:** Sınırsız video yüklemesi depolama maliyetini patlatır — boyut sınırı ve süre sınırı konmalı.
  - **İzinsiz görsel:** Başka çocuğun görüntüsü kadraja girerse KVKK sorunu; öğretmene uyarı metni gösterilmeli.
  - Küçük resim (thumbnail) üretilmezse liste ekranı ham görselleri indirir → yavaş ve pahalı.
- **Bağımlılık:** M06 + P04 depolama (ikisi de 🟢).
- **Efor:** S · **Karar:** ` ` · **Not:** ` `

### R-03 — Günlük görev döngüsü yok 🟠

- **Rakip kanıtı:** **Net Up** günlük görev + günlük seri uyarısı ("her gün küçük adımlar at").
  **Pakodemy** *"seviyene ve hedeflerine göre haftalık 10 özel görev seç, bunları tamamla ve planlı ilerle."*
- **Bizdeki durum:** `/students/{id}/goals` **günlük dakika hedefi** tutuyor ve dashboard'da
  `dailyGoalMinutes > 0 && todayMinutes >= dailyGoalMinutes` hesaplanıyor (`StudyProgressFeatures.cs:309`).
  Yani "bugünkü hedef" var, ama **çok adımlı görev listesi ve tamamlama ödülü** yok.
  Ayrıca `AchievementCategory.Goal` boş duruyor (X-03).
- **Etki:** 🟠 Uygulamayı açan öğrenciye "bugün ne yapmalıyım" cevabı verilmiyor. Kronometre bir araç,
  görev listesi bir **davet**. Rakipler bu daveti üretiyor.
- **Çözüm taslağı:** Görevleri **üretmek** yerine **türetmek** — asıl fark burada.
  - Domain (M08): `DailyQuest` (`StudentId`, `Date`, `Kind`, `TargetValue`, `CurrentValue`, `CompletedOnUtc`).
    `QuestKind`: `StudyMinutes` (hedeften), `CompleteAssignment` (M06'dan **teslim edilmemiş ödev**),
    `SolveTest` (deneme girişi), `ReviewWeakTopic` (M10'un en zayıf kazanımı).
  - Üretim: gün başında (ilk istekte, tembel üretim) o günün görevleri hesaplanır — arka plan işi gerekmez.
  - İlerleme: `StudySessionCompletedDomainEvent` ve ödev teslim olayı görev sayaçlarını günceller.
  - Uçlar: `GET /api/study/students/{id}/quests?date=`, `POST /api/study/quests/{id}/dismiss`.
  - X-03 ile birlikte: `Goal` kategorisinde `GOAL_7` / `GOAL_30` rozetleri.
  - Mobil: öğrenci ana ekranında "Bugün" kartı — 3 görev, tamamlananlar üstü çizili, hepsi bitince kutlama.
- **Kabul kriteri:** Öğrenci ana ekranda en fazla 3 görev görüyor · görevlerden en az biri **öğretmeninin
  verdiği gerçek ödevden** türüyor · kronometre bitince görev ilerlemesi otomatik güncelleniyor ·
  gün değişince görevler sıfırlanıyor (04:00 gün sınırı, streak ile aynı kural).
- **Risk / tuzak:**
  - **Gün sınırı tutarsızlığı:** Streak 04:00'ü kullanıyor; görevler gece yarısını kullanırsa gece çalışan
    öğrencide iki sistem çelişir. **Aynı kuralı paylaşmalılar.**
  - **Ödevi olmayan öğrenci:** Öğretmensiz öğrencide `CompleteAssignment` görevi üretilemez — jenerik
    görevlere düşmeli, boş liste gösterilmemeli.
  - Görev sayısı 3'ü geçerse "yapılacaklar listesi" hissi verir ve motive etmez, yorar.
- **Bağımlılık:** M08 (🟢), M06 (🟢), M10 (🟢).
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### R-01 — Haftalık lig / karşılaştırmalı sıralama yok 🟠

- **Rakip kanıtı:** **Net Up** haftalık lig tablosu ("arkadaşlarınla yarış, zirveye çık") ürünün üç ana
  mekaniğinden biri. **DUS Club** "ekip içi günlük çalışma sıralaması" sunuyor.
- **Bizdeki durum:** Rozet (10 adet) ve streak **var**, ama hepsi **tek kişilik**. Kodda hiçbir
  karşılaştırma/sıralama yok (`grep -i leaderboard src/` → boş).
- **Etki:** 🟠 Motivasyon yalnızca kendi geçmişine karşı. Rakipler sosyal baskıyı kullanırken bizde günlük
  açılış sebebi zayıf. Öğrenci tutundurmasının en ucuz kaldıracı bu — çünkü **veri zaten toplanıyor**.
- **Çözüm taslağı:** Klasik projeksiyon işi; §1.3'teki desen birebir uygulanır.
  - Domain/altyapı: `Study` (ya da yeni hafif bir modül yerine M08 içinde) `WeeklyLeaderboardProjection : ProjectionEntity`
    — `GroupId`, `WeekStartDate`, satırlar: `StudentId`, `Minutes`, `SessionCount`, `Rank`.
  - Besleme: `StudySessionCompletedDomainEvent` → `ProjectionHandler<...>`. **Yeni olay yazılmıyor.**
  - Uç: `GET /api/study/groups/{groupId}/leaderboard?week=` — kendi sıran her zaman döner (listede
    görünmesen bile), üst N gösterilir.
  - Gizlilik: katılım **opt-in**; `GET/PUT /students/{id}/sharing` sözleşmesine yeni bir alan
    (`ShareLeaderboard`) eklenir. Katılmayan öğrenci listede **hiç** yer almaz.
  - Mobil: `features/study` altında lig ekranı; ana ekranda "bu hafta sınıfında N."'sin şeridi.
- **Kabul kriteri:** Sıralama haftalık pencerede doğru · opt-out öğrenci listede yok · projeksiyon
  `IProjectionRebuilder` ile sıfırdan aynı sonucu üretiyor (P08'deki `RealProjectionRebuildTests` deseni) ·
  hafta değişince yeni tablo başlıyor, eskisi kayboluyor değil.
- **Risk / tuzak:**
  - **Utandırma:** Alt sıralar açıkça gösterilirse zayıf öğrenci uygulamayı bırakır. Öneri: yalnız ilk 5 +
    kendi sıran, alt sıralar hiç gösterilmez.
  - **Küçük grup:** 2 kişilik grupta lig anlamsız — en az 3 katılımcı yoksa şerit gizlenir.
  - **Manipülasyon:** Kronometreyi açık bırakıp dakika biriktirme. M08'in `EffectiveMinutes` ve
    `recover`/`discard` mantığı buna karşı zaten çalışıyor, ama lige girmeden önce **günlük üst sınır**
    (ör. 8 saat) konmalı.
  - Gizlilik ayarı sonradan kapatılırsa geçmiş sıralamadan da düşmeli.
- **Bağımlılık:** **R-02 (grup kavramı) olmadan `groupId` yok** — ikisi tek işte yapılmalı.
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### R-02 — Ekip / grup kavramı yok (kod ile katılım) 🟠

- **Rakip kanıtı:** **DUS Club** — *"Kendi ekibini kur ya da bir **koda dahil ol**. Ekip içi günlük çalışma
  sıralamasında yerini gör."*
- **Bizdeki durum:** `TeacherStudent` bağı var (çoklu öğretmen kararı, 2026-07-18), ama **grup/sınıf
  entity'si yok**. Referans kodu altyapısı M17'de mevcut (`GET /referral-code`, `POST /referral/redeem`) —
  kod üretme/çözme/tek-kullanım deseni hazır, kopyalanabilir.
- **Etki:** 🟠 R-01'in taşıyıcısı. Ayrıca öğretmenin "sınıfım" görünümü yok; her öğrenci ayrı ada —
  toplu işlem (duyuru, toplu ödev, toplu rapor) için ortak bir kap yok.
- **Çözüm taslağı:**
  - Domain (M03 Students ya da M02 Teachers): `StudyGroup` (`Id`, `OwnerUserId`, `Name`, `JoinCode`,
    `IsActive`, `CreatedOnUtc`) + `StudyGroupMember` (`GroupId`, `StudentId`, `JoinedOnUtc`, `LeftOnUtc`).
  - **Varsayılan grup:** Öğretmenin öğrencileri **otomatik** bir gruba düşer (kurulum gerektirmez) —
    DUS Club'da kullanıcı ekibi kendi kurmak zorunda, bizde hazır. Bkz. **Ü-07**.
  - Katılım kodu: M17'nin `ReferralCode` deseni — tekil, sahibi kendi kodunu kullanamaz, iptal edilebilir.
  - Uçlar: `POST /api/students/groups`, `POST /api/students/groups/join`, `GET /api/students/groups/{id}/members`,
    `DELETE /api/students/groups/{id}/members/{studentId}`.
  - Shared sözleşme: `IStudyGroupDirectory` — M08 (lig) ve M16 (duyuru hedefi) buradan okur, **yerel kopya yok**
    (P10'da `IStudentDirectory` için uygulanan kural).
- **Kabul kriteri:** Öğretmenin öğrencileri otomatik grupta · kodla katılan öğrenci gruba düşüyor ·
  gruptan çıkarılan öğrenci ligde görünmüyor · kod tahmin edilebilir değil · aynı kod iki kez kullanılamıyor.
- **Risk / tuzak:**
  - **Kapsam kayması:** Grup, "sınıf yönetimi" modülüne dönüşmemeli. Bu iş yalnız *lig + duyuru hedefi* için.
  - **İzinsiz katılım:** Kod sızarsa yabancı çocuk gruba girer ve diğerlerinin adını görür.
    Öğretmen onayı (bekleyen üyelik) ya da kod yenileme şart.
  - Öğrenci birden çok grupta olabilir (iki öğretmen) — tekil grup varsayımı yapılmamalı.
- **Bağımlılık:** M03 · M17 (kod deseni) · R-01 bunu bekliyor.
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### R-05 — Flashcard / aralıklı tekrar yok 🟠

- **Rakip kanıtı:** **DUS Club** — *"Flashcard'larla aktif tekrar yap. Kendi kartlarını oluştur, **topluluk
  kartlarını** kullan, mnemonics ve şifreler ile kritik bilgileri kafanda tut."* **Doping Hafıza** —
  hafıza teknikleri, akıl haritaları, "refleksler".
- **Bizdeki durum:** `StudyNote` (`StudyCatalogModel.cs:107`) serbest metin notu tutuyor; **tekrar
  algoritması ve kart yapısı yok**. `grep -i flashcard src/ mobile/` → **boş**.
- **Etki:** 🟠 "Öğrenme" tarafında elimiz boş; sadece **takip** yapıyoruz. Öğrenci öğrenmek için başka
  uygulamaya gidiyor, orada kalıyor. Bu, içerik boşluğunun (R-14) günlük hissedilen yüzü.
- **Çözüm taslağı:** Tek başına yapılırsa sıradan bir flashcard uygulaması olur; **Ü-03 ile birlikte
  yapılırsa rakiplerde olmayan şey** çıkar (öğretmenin verdiği kart).
  - Domain (M08): `Flashcard` (`Id`, `OwnerUserId`, `SubjectId`, `TopicId`, `Front`, `Back`, `Source`)
    + `FlashcardReview` (`CardId`, `StudentId`, `EaseFactor`, `IntervalDays`, `DueOnDate`, `Repetition`).
    `Source`: `Student` | `Teacher` (Ü-03 köprüsü).
  - Algoritma: SM-2'nin sadeleştirilmişi — **saf, birim testli** bir sınıf (`MembershipGate` gibi
    `Shared` değil, M08 Application içinde). Karmaşık aralıklandırma v1'de gerekmez: 1/3/7/16/35 gün.
  - Uçlar: `POST /api/study/flashcards`, `GET /api/study/students/{id}/flashcards/due`,
    `POST /api/study/flashcards/{id}/review` (kolay/zor/bilmiyorum).
  - Mobil: kart çevirme ekranı; günlük "tekrar zamanı gelen N kart" → R-03 görevine bağlanır.
- **Kabul kriteri:** Zor denen kart yakında tekrar geliyor, kolay denen uzağa gidiyor · tekrar kuyruğu
  günlük görevde görünüyor · öğretmenin atadığı kart öğrencide çıkıyor (Ü-03 yapıldıysa) ·
  aralık hesabı birim testli ve deterministik.
- **Risk / tuzak:**
  - **Boş kart havuzu:** Öğrenci kendi kartını yazmazsa özellik ölü doğar. Bu yüzden **Ü-03 önce/birlikte** gelmeli.
  - SM-2'yi tam uygulamak v1 için aşırı; basit aralık listesi yeterli ve daha kolay açıklanır.
  - Kartlar konu kataloğuna bağlanmazsa M10 kazanım verisiyle ilişkilenemez → Ü-02'nin değeri düşer.
- **Bağımlılık:** **Ü-03 ile birlikte yapılmalı.** M08 katalog (🟢).
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### R-09 — Gerçek para tahsilatı yok 🔴

- **Rakip kanıtı:** İncelenen 10 üründen **10'u** para alabiliyor. Net Up (99,99 ₺/ay · 499,99 ₺/yıl),
  DUS Club (DUSClubber) ve Özel Ders Alanı (Eko/Bronz/Silver/Gold/Platinum + ders talebi kontörü)
  **mağaza içi satın alma**; Tilki kart ile SaaS (120 ₺/aktif öğrenci/ay); Doping ve Pakodemy havale + 12 taksit.
- **Bizdeki durum:** M17 🟡 — plan kataloğu, ücretsiz deneme (kullanıcı başına bir kez), referans kampanyası,
  entitlement ve **mobil paywall ekranı** var; **checkout yok**. `ILERLEME.md` → P09 Task 4 (iyzico) **Q1'e
  bloke**, Task 5 (AdMob) **Q3'e bloke**. `RealMembershipEntitlementTests` deneme→premium→süre sonu→Free
  zincirini gerçek Postgres'te doğruluyor — yani **kilit çalışıyor, açacak anahtar yok**.
- **Etki:** 🔴 Ürün fiilen ücretsiz. Premium kapıları (öğrenci limiti 5, PDF raporu 402, geçmiş 30 gün)
  çalışıyor ama kullanıcı ödeyerek açamıyor.
- **Çözüm taslağı — iki ayrı para akışı, iki ayrı yol:**

  | Akış | Yöntem | Gerekçe |
  |------|--------|---------|
  | **Premium üyelik** (uygulama özelliği açar) | **StoreKit 2 / Google Play Billing** | App Store 3.1.1: dijital içerik/özellik IAP zorunlu. Rakiplerin hepsi böyle yapıyor |
  | **Ders ücreti** (öğrenci → öğretmen) | **iyzico** (ya da havale, bugünkü manuel akış) | App Store 3.1.3(d): *"real-time person-to-person services… for example **tutoring students**"* IAP dışı ödeme kullanabilir. **Bire-çok grup dersi bu istisnanın dışında** |

  - Backend: `Membership`'e `PurchaseReceipt` (`Provider`, `ExternalSubscriptionId`, `RawReceipt`,
    `VerifiedOnUtc`) + `POST /api/membership/purchases/verify` (mağaza fişini sunucuda doğrular) +
    mağaza sunucu bildirimi (webhook) ucu. Backend **kart verisi tutmaz** (m17 §9 kuralı korunur).
  - Yenileme/iptal/geri ödeme mağazadan gelir → `UserSubscription` durumu güncellenir → entitlement
    kendiliğinden değişir (zincir zaten çalışıyor).
  - Mobil: paywall ekranındaki pasif düğme gerçek satın almaya bağlanır; "satın alımları geri yükle" akışı.
- **Kabul kriteri:** Satın alan kullanıcı `GET /api/membership/me`'de Premium görüyor · iptal edince dönem
  sonunda Free'ye düşüyor · **fiş sunucuda doğrulanıyor** (istemciye güvenilmiyor) · aynı fiş iki hesaba
  bağlanamıyor · sandbox hesabıyla uçtan uca test edilmiş.
- **Risk / tuzak:**
  - **Mağaza reddi:** Premium'u harici ödemeyle satmak reddedilir. En büyük risk bu ve ücretsiz — sadece
    doğru yolu seçmekle çözülüyor.
  - **Fişi istemcide doğrulamak:** Klasik güvenlik açığı; doğrulama sunucuda olmalı.
  - **Çift kaynak:** Hem iyzico hem IAP aboneliği satılırsa entitlement iki yerden gelir → tek yazar kuralı bozulur.
  - **Komisyon:** Mağaza %15–30 alır; fiyatlandırma buna göre yapılmalı (Net Up 99,99 ₺'yi bu komisyonla koymuş).
- **Bağımlılık:** 🚫 **Q1 kararı.** M17 (🟡) hazır.
- **Efor:** L · **Karar:** ` ` · **Not:** ` `

### R-11 — Öğretmen arama / filtreleme yok 🟠

- **Rakip kanıtı:** **Özel Ders Alanı** — branş, konum, fiyat ve ders biçimine göre arama; öğretmen profili,
  ders talebi oluşturma. **Öğretsen** — konuma göre keşif, **harita üzerinden filtreleme**, mesafeye göre sıralama.
- **Bizdeki durum:** M12 Matching **🔴 iskelet** — 0 entity, 0 migration, yalnız `GET /status`.
- **Etki:** 🟠 Eşleştirme yok → yeni kullanıcı kendi öğretmenini getirmek zorunda; platform büyümesi
  ağızdan ağıza kalıyor.
- **Çözüm taslağı:** P11 planı mevcut; buraya eklenmesi gereken tek şey **Ü-10** (profilin operasyon
  verisiyle beslenmesi) ve **Ü-06** (boş saat → ilan).
- **Kabul kriteri:** P11 planındaki kriterler + arama sonucunda gösterilen her metrik **gerçek veriden** geliyor.
- **Risk / tuzak:**
  - **Boş pazar:** Öğretsen 4 yılda 41 değerlendirme, Öğretmenim.app 2022'den beri güncellenmemiş —
    saf eşleştirme tek başına tutmuyor. PRD §10.1'in "eşleştirmeyi en son aç" kararı **dışarıdan doğrulanıyor**.
  - Konum verisi toplamak KVKK yükü getirir; ilçe düzeyi yeterli, tam koordinat gerekmez.
- **Efor:** L *(P11)* · **Karar:** ` ` · **Not:** ` `

### R-12 — Puan / yorum sistemi yok 🟡

- **Rakip kanıtı:** **Öğretsen** — *"eğitmen profilleri ve kullanıcı yorumlarını görerek doğru kararı ver."*
- **Bizdeki durum:** M13 Reviews **🔴 iskelet**.
- **Etki:** 🟡 R-11 olmadan tek başına anlamsız; ikisi birlikte P11'de.
- **Çözüm taslağı:** P11 planı. Bizde ayrıcalıklı bir imkân var: yorum bırakabilecek kişi **gerçekten ders
  almış** olan öğrenci/veli (M05 kaydı kanıt) → **doğrulanmış yorum**. Pazar yerlerinde bu yok.
- **Risk / tuzak:** Sahte yorum ve intikam yorumu. Ders kaydı şartı ilkini, moderasyon (P12/M18) ikincisini çözer.
- **Efor:** M *(P11)* · **Karar:** ` ` · **Not:** ` `

### R-13 — Web erişimi yok 🟡

- **Rakip kanıtı:** **Tilki** iOS + Android + **web**; Doping ve Pakodemy tarayıcıdan çalışıyor
  (Pakodemy ayrıca Huawei AppGallery).
- **Bizdeki durum:** Angular planlı, kod yok (P14).
- **Etki:** 🟡 Öğretmen masaüstünde ders planlayamıyor, toplu veri giremiyor. Haftalık planlama işi
  doğal olarak bilgisayarda yapılır — mobil-only bunu zorlaştırıyor ve öğretmeni Excel'e geri itiyor.
- **Çözüm taslağı:** P14 planı. Öncelik sırası: **öğretmen takvimi + toplu ders girişi + raporlar**;
  öğrenci/veli web'i gerekmez (onlar zaten mobilde).
- **Risk / tuzak:** Mobil ve web'i aynı anda geliştirmek PRD §10.3'te "yaygın hata" olarak işaretli —
  mobil kararlı olmadan başlanmamalı.
- **Efor:** XL *(P14)* · **Karar:** ` ` · **Not:** ` `

### R-04 — XP / seviye / ödül sandığı yok 🟡 *(iptal önerisi)*

- **Rakip kanıtı:** **Net Up** — XP topla, sandık aç, rozet kazan, avatar özelleştir.
- **Bizdeki durum:** Rozet var (10 adet, 4 kategori kullanımda), XP/seviye yok.
- **Etki:** 🟡 Mikro ödül döngüsü eksik; ancak rozet aynı ihtiyacın büyük kısmını karşılıyor.
- **Görüş — neden şimdi değil:**
  1. Rozet sistemiyle **çakışır**: iki paralel ödül para birimi kafa karıştırır.
  2. Yaş grubumuz Net Up'ın 6–8. sınıfından **geniş** (lise sonuna kadar); XP/sandık dili büyük öğrencide ters teper.
  3. Asıl eksik sosyal katman (R-01/R-02), ödül enflasyonu değil.
- **Yeniden değerlendirme koşulu:** R-01 ve R-03 yayına girdikten sonra ölçüm "rozet kazanımı düşük,
  günlük dönüş zayıf" derse gelsin.
- **Efor:** S *(altyapı hazır: `AchievementEvaluator` eşik motoru)* · **Karar:** ` ` · **Not:** ` `

### R-14 — Soru bankası / video içerik yok ⚪ *(kapsam dışı önerisi)*

- **Rakip kanıtı:** Pakodemy 400–500 bin soru + 20'den fazla yayınevi (Test Okul, VİP, Açı, Delta, Karat,
  Röntgen…) + 6.000+ kazanım videosu (iddia); Doping her soruya çözüm videosu + çok formatlı anlatım;
  Net Up 2.000+ soru; DUS Club soru havuzu + vaka soruları.
- **Bizdeki durum:** Yok ve **olmamalı**. Merkezî içerik üretimi sermaye, telif ve editoryal ekip işi;
  PRD §1 ürünü "eşleştirme + yönetim" olarak tanımlıyor.
- **Alternatif:** İçeriği **öğretmen üretsin** → **Ü-03**. Bu, içerik boşluğunu içerik şirketi olmadan kapatır
  ve üstelik rakiplerin kopyalayamayacağı bir kilitlenme yaratır.
- **Efor:** XL *(girilirse)* · **Karar:** ` ` · **Öneri:** `❌ İptal` — Ü-03 ile ikame edilsin

---

## 5. 🟢 Ü — Üstünlük: hiçbir rakipte olmayan, bizde olabilecekler

> Buradaki maddelerin ortak özelliği: **hepsi hâlihazırda topladığımız verinin birleştirilmesinden** doğuyor.
> Yeni içerik, yeni pazar, yeni sermaye gerektirmiyorlar — rakiplerin **yapısal olarak yapamadığı** şeyler,
> çünkü onların elinde bu üç taraf (öğretmen + öğrenci + veli) aynı veri modelinde değil.
>
> Her maddede ayrıca **"nasıl ölçeriz"** var: farklılaşma iddiası ölçülmeden doğrulanmış sayılmaz.

### Ü-01 — Doğrulanmış çalışma karnesi (üç kaynaklı kanıt) 🟢

- **Fikir:** Rakiplerde çalışma verisi **tek kaynaklı**: öğrenci kendi kendini raporlar. Net Up bunu
  *"gerçek çalışma dakikası, ekran süresi değil"* diye pazarlıyor — yine de tek taraflı beyan.
  Bizde aynı zaman çizgisinde **üç bağımsız kanıt** var: öğretmenin işaretlediği ders (M05),
  öğrencinin kronometresi (M08), teslim edilen ödev (M06).
- **Ürün ifadesi:** *"Bu ay: 8 ders yapıldı (öğretmen onaylı), 14 saat bireysel çalışma (kronometre),
  11/12 ödev teslim edildi."* Veli için tartışmasız, öğrenci için gurur, öğretmen için kanıt.
- **Neden rakip yapamaz:** Doping/Pakodemy/Net Up'ın elinde **öğretmen yok**; Tilki'nin elinde
  **öğrenci kronometresi yok**; VeliCep'in elinde **çalışma verisi yok**.
- **Çözüm taslağı:** Yeni veri toplanmıyor — yalnız birleştiriliyor.
  - `Reporting` modülüne `StudentEvidenceProjection : ProjectionEntity` (`StudentId`, `PeriodStart`,
    `LessonsPlanned/Completed`, `StudyMinutes`, `SessionCount`, `AssignmentsAssigned/Submitted/Approved`).
  - Besleme: mevcut olaylar — ders tamamlandı (M05), `StudySessionCompletedDomainEvent` (M08),
    ödev teslim/onay (M06). Üçü de **zaten outbox'tan akıyor**.
  - Uç: `GET /api/reporting/students/{id}/evidence?from=&to=`.
  - Mobil: veli panelinde ve öğrenci profilinde tek kart; PDF raporuna bölüm olarak eklenir (M14 mevcut).
  - R-08 ile birleşince: bu kart **paylaşılabilir özetin gövdesi** olur.
- **Kabul kriteri:** Üç sayı da bağımsız kaynaktan geliyor ve tıklanınca detayına iniliyor ·
  projeksiyon yeniden kurulunca aynı sonucu veriyor · veli, öğrenci ve öğretmen aynı sayıyı görüyor.
- **Nasıl ölçeriz:** Veli panelinin haftalık açılma oranı; R-08 paylaşımlarının kaçının bu kartı içerdiği.
- **Risk / tuzak:**
  - **Çelişkili sayı:** Öğretmen dersi işaretlemeyi unutursa "8 ders yapıldı" yanlış çıkar ve **güven
    ters yönde** zedelenir. Eksik işaretlemeyi görünür kılan bir uyarı gerekir.
  - Ödev sayısı düşük öğrencide kart suçlayıcı görünebilir — dil dikkatli kurulmalı.
- **Bağımlılık:** M05 · M06 · M08 · M14 projeksiyon deseni (hepsi 🟢).
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### Ü-02 — Kapalı döngü: ödev → çalışma → deneme → gelişim nedenselliği 🟢

- **Fikir:** Rakiplerde bu zincirin halkaları farklı ürünlerde. Bizde tek veri modelinde:
  ödevin konusu (M06) → o konuya ayrılan süre (M08 `subjects`/`topics`) → denemede o konunun neti
  (M08 `TestResult` + `/net-trend`) → kazanım hâkimiyeti (M10).
- **Ürün ifadesi:** *"Paragraf konusuna 3 hafta önce 40 dk çalıştın, netin 6 → 11 çıktı.
  Çarpanlara ayırmaya hiç çalışmadın, netin 3'te sabit."* — **nedensellik**, kuru istatistik değil.
- **Neden rakip yapamaz:** Pakodemy'nin "dışarıda çalıştığını da ekle" özelliği bunun zayıf taklidi —
  onların elinde **ödevi veren öğretmen** ve **dersin konusu** yok.
- **Çözüm taslağı:**
  - `Reporting`'e `TopicCausalityProjection` — konu bazında: ayrılan dakika, çözülen soru, net değişimi,
    hâkimiyet değişimi; dönem karşılaştırmalı.
  - Besleme: `StudySessionCompletedDomainEvent` (konu kırılımlı), `TestResultRecordedDomainEvent`,
    M10 kazanım olayı — **üçü de mevcut**.
  - Sunum: "en çok ilerlediğin 3 konu" + "dokunmadığın 3 konu" + her biri için bir cümlelik açıklama.
    Cümle üretimi **kural tabanlı** (yapay zekâ gerekmez, gerekirse sonra).
  - Uç: `GET /api/reporting/students/{id}/topic-insights?period=`.
- **Kabul kriteri:** Her içgörü cümlesi **iki veri noktasına** dayanıyor ve ikisi de arayüzde görülebiliyor ·
  yetersiz veri varsa cümle üretilmiyor ("henüz yeterli veri yok") · yanlış nedensellik iddiası yok
  (metin "ilişkili" der, "sebep oldu" demez).
- **Nasıl ölçeriz:** Öğretmenin bu ekranı ders öncesi açma oranı; içgörü sonrası o konuya ayrılan sürede artış.
- **Risk / tuzak:**
  - **Sahte nedensellik:** Az veriyle "çalıştın, net arttı" demek yanıltıcı. Eşik (ör. en az 2 deneme +
    30 dk) altında cümle üretilmemeli.
  - Konu eşleşmesi zayıfsa (ödev konusu ≠ katalog konusu) zincir kopar — konu kataloğu tek kaynak olmalı.
- **Bağımlılık:** M06 · M08 · M10 · M14 (hepsi 🟢).
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### Ü-03 — Öğretmenin kendi içerik havuzu (soru + kart), öğrenciye atanabilir 🟢

- **Fikir:** R-14'ün (soru bankası) cevabı içerik satın almak değil, **öğretmeni içerik üreticisi yapmak**.
  Öğretmen zaten her derste soru seçiyor, kendi notlarını ve püf noktalarını hazırlıyor. Bunu platformda
  tutarsa: havuzu birikir, tüm öğrencilerine atar, **ertesi yıl tekrar kullanır**.
- **Ürün ifadesi:** *"Kendi soru bankan. Bir kez hazırla, her öğrencine ata, her yıl kullan."*
- **Neden güçlü:**
  1. **İçerik maliyeti sıfır** — telif, editör, video prodüksiyon yok.
  2. **Kişiselleştirme rakiplerin yapay zekâsından isabetli** — öğretmen çocuğu tanıyor.
  3. **Kilitlenme (lock-in):** Havuzunu iki yıl biriktiren öğretmen platformdan çıkamaz.
     Tilki'de ve pazar yerlerinde bunun karşılığı **yok** — orada öğretmenin biriktirdiği bir varlık yok.
- **Çözüm taslağı:** `LessonResource` zaten "öğretmenin paylaştığı materyal" kavramını taşıyor ve
  `StudentId` null ise **tüm öğrencilere açık** — yani bire-çok fikri kodda mevcut. Üstüne:
  - Domain (M06): `ContentItem` (`Id`, `TeacherUserId`, `Kind`, `SubjectId`, `TopicId`, `Title`, `Body`,
    `AnswerKey`, `Difficulty`, `IsArchived`) — `Kind`: `Question` | `Flashcard` | `Note`.
    `ContentAssignment` (`ContentItemId`, `StudentId` **veya** `GroupId`, `AssignedOnUtc`, `DueOnUtc`).
  - **R-05 köprüsü:** `Kind = Flashcard` olan öğe öğrencinin tekrar kuyruğuna düşer (`Flashcard.Source = Teacher`).
  - **R-03 köprüsü:** Atanan içerik günlük görev üretir.
  - Uçlar: `POST /api/assignments/content`, `GET /api/assignments/teachers/{id}/content?subject=&topic=`,
    `POST /api/assignments/content/{id}/assign`, `GET /api/assignments/students/{id}/content`.
  - Yeniden kullanım: içerik **yıla bağlı değil**; arşivleme var, silme yok (`LessonResource`'un yumuşak silme deseni).
  - Mobil: öğretmende "İçeriklerim" (konuya göre filtreli liste + hızlı ekle), öğrencide atanan içerik akışı.
- **Kabul kriteri:** Öğretmen bir içeriği **iki farklı öğrenciye ve ertesi dönem tekrar** atayabiliyor ·
  içerik konu kataloğuna bağlı (Ü-02 zinciri kopmuyor) · arşivlenen içerik yeni atamada çıkmıyor,
  eski atamalarda görünmeye devam ediyor · öğretmen kendi içeriğini dışa aktarabiliyor (kilitlenme ≠ rehin).
- **Nasıl ölçeriz:** Öğretmen başına biriken içerik sayısı; **yeniden kullanım oranı** (aynı içeriğin
  ikinci kez atanma yüzdesi) — asıl kilitlenme göstergesi bu.
- **Risk / tuzak:**
  - **Boş havuz sorunu:** Öğretmen ilk gün 0 içerikle karşılaşırsa özelliği kullanmaz. Ödev/materyal
    akışından **otomatik tohumlama** (mevcut `LessonResource` kayıtları içeriğe dönüştürülebilir) şart.
  - **Telif:** Öğretmen yayınevi sorusu yükleyebilir. Kullanım şartlarına madde + paylaşımın **kendi
    öğrencileriyle sınırlı** kalması (topluluk havuzu **yapılmamalı** — DUS Club yapıyor, biz yapmamalıyız).
  - **Kapsam kayması:** Bu bir "soru bankası ürünü" değil; editör basit kalmalı (metin + görsel + cevap).
- **Bağımlılık:** M06 (🟢) · P04 depolama (🟢) · R-05 ile birlikte.
- **Efor:** L · **Karar:** ` ` · **Not:** ` `

### Ü-04 — Veli için tek ekranda "para + gelişim" 🟢

- **Fikir:** Doping veliye **yalnız gelişim** gösteriyor (ayrı veli uygulaması), VeliCep **yalnız iletişim**,
  Tilki **yalnız ödeme hatırlatması**. Bizde M07 (ödeme) + M10 (gelişim) + M05 (katılım) aynı veli panelinde.
- **Ürün ifadesi:** *"Eylül: 8 ders planlandı, 7'si yapıldı, 1'i ertelendi · 2.400 ₺ ödendi, 600 ₺ kaldı ·
  matematik neti 12 → 18."* Velinin gerçekten sorduğu soru bu: **"verdiğim paranın karşılığı ne?"**
- **Neden rakip yapamaz:** Para akışı ile gelişim verisi hiçbir rakipte aynı üründe değil.
- **Çözüm taslağı:**
  - `Reporting`'e `ParentMonthlyProjection` — `TeacherMonthlyProjection`'ın veli aynası: ders sayıları,
    ödeme durumu (`PaymentRecord` + `ParentPaymentDeclaration`), net/hâkimiyet değişimi.
  - Uç: `GET /api/reporting/parents/{id}/children/{studentId}/monthly`.
  - Mobil: veli ana ekranında ay kartı + geçmiş aylar listesi.
  - **Önkoşul:** `modules/00_genel_bakis.md`'de M07 için işaretli **"veli paylaşımı ⚠️"** borcu önce kapanmalı —
    veli ödeme verisini görmüyorsa bu ekran yarım olur.
- **Kabul kriteri:** Veli ödeme ve gelişimi aynı ekranda görüyor · ödeme verisi öğretmenin girdiğiyle birebir ·
  veli yalnız kendi çocuğunu görüyor · para bilgisi öğrenciye gösterilmiyor.
- **Nasıl ölçeriz:** Veli aylık aktifliği; ödeme gecikmesinde azalma (şeffaflık tahsilatı hızlandırır).
- **Risk / tuzak:**
  - **Hassas veri:** Ücret bilgisi çocuğa görünmemeli; rol bazlı alan filtresi şart.
  - **Yanlış tahsilat görüntüsü:** Elden ödeme öğretmen tarafından işlenmezse veli "ödemedim" görür ve
    gereksiz gerilim çıkar. `ParentPaymentDeclaration` (veli beyanı) akışı bu yüzden önemli.
- **Bağımlılık:** M07 (🟢, veli paylaşımı borcu) · M09 · M10 · M14.
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### Ü-05 — Çok öğretmenli öğrenci: tek çocuk, tüm dersler 🟢

- **Fikir:** Bir öğrencinin matematik ve fizik öğretmeni ayrı kişiler. Tilki **tek öğretmenin defteri**;
  VeliCep **tek kurumun** uygulaması; pazar yerleri ders bitince ilişkiyi bırakıyor. Bizde `TeacherStudent`
  çoklu bağ modeli var → **öğrenci merkezli birleşik görünüm** mümkün.
- **Ürün ifadesi:** Veli tek uygulamada üç öğretmeni, üç dersi, tek takvimi ve tek ödeme özetini görüyor.
- **Neden güçlü:** Ağ etkisi **ters yönde** çalışıyor — öğrenci zaten platformda olduğu için **ikinci
  öğretmen de** platforma çekiliyor. Rakiplerde her öğretmen kendi adasında; müşteri edinme maliyeti bizde düşüyor.
- **Çözüm taslağı:** Veri modeli hazır; eksik olan sunum ve birkaç sorgu.
  - Öğrenci/veli için **birleşik takvim** (M04 zaten öğrenci-kapsamlı sorguyu destekliyor) ve
    öğretmen bazlı kırılımlı özet.
  - "Öğretmen davet et" akışı: öğrenci/veli kendi öğretmenini davet eder (M17 referans kodu deseni) →
    **büyüme kanalı**.
- **Kabul kriteri:** İki öğretmenli öğrencide takvim çakışmasız birleşiyor · her öğretmen yalnız kendi
  dersini/ödevini görüyor · veli hepsini görüyor · davet edilen öğretmen öğrenciye otomatik bağlanmıyor
  (onay gerekiyor).
- **Nasıl ölçeriz:** Öğrenci başına ortalama öğretmen sayısı; davetle gelen öğretmen oranı.
- **Risk / tuzak:**
  - **Veri sızıntısı:** Öğretmen A, öğretmen B'nin ders notunu görmemeli. `LessonNoteVisibility` bunu
    zaten modelliyor, atlanmamalı.
  - Birleşik takvimde çakışan dersler karışabilir — renk/etiket ayrımı gerekli.
- **Bağımlılık:** M02/M03 (çoklu bağ) · M04.
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### Ü-06 — Boş zamanı gelire çevirme (kapasite → ilan) 🟢

- **Fikir:** `FreeSlotCalculator` + `GET /teachers/{id}/free-slots` **zaten kodda ve rakiplerin hiçbirinde
  yok.** Bugün yalnızca bir rapor; M12 açılınca **gelir aracına** dönüşür: *"Salı 16:00 boş — bu saati ilana çevir."*
- **Ürün ifadesi:** Öğretmen için doluluk oranı, boş saatin parasal karşılığı ve tek dokunuşla ilan.
- **Neden rakip yapamaz:** Pazar yerlerinde **takvim yok**; Tilki'de takvim var ama **pazar yeri yok**.
  İkisi aynı üründe yalnız bizde.
- **Çözüm taslağı:** M12 ilanına `AvailabilityWindow` alanı; `free-slots` çıktısından **tek dokunuşla**
  ilan oluşturma; ilan eşleşince takvime otomatik yerleşme (M04 çakışma kontrolü mevcut).
- **Kabul kriteri:** Öğretmen boş saatini ilana çevirebiliyor · ilan dolunca takvimde blok oluşuyor ·
  çakışma reddediliyor · doluluk oranı gerçek ders verisinden hesaplanıyor.
- **Nasıl ölçeriz:** Boş saat oranındaki düşüş; ilanların kaçının `free-slots` ekranından açıldığı.
- **Risk / tuzak:** Öğretmen "boş" görünen ama kişisel olarak dolu saatlerini ilana açmak istemez —
  `free-slots` bir **öneri** olmalı, otomatik yayın olmamalı.
- **Bağımlılık:** M12 (🔴) · M14 `FreeSlotCalculator` (🟢).
- **Efor:** M *(M12 sonrası)* · **Karar:** ` ` · **Not:** ` `

### Ü-07 — Sınıf ligi: öğretmenin gerçek öğrenci grubu 🟢

- **Fikir:** R-01/R-02'nin üzerine bir kat. Net Up'ta lig **anonim/global**, DUS Club'da kullanıcı ekibini
  **kendi kurmak zorunda**. Bizde lig **hazır ve gerçek**: öğretmenin öğrencileri. Sıfır kurulum,
  gerçek tanışıklık ve — kritik fark — **sıralamayı gören, müdahale edebilen bir yetişkin**.
- **Ürün ifadesi:** *"Sınıfının bu haftaki çalışma sıralaması"* — öğrenciye rekabet, öğretmene erken uyarı.
- **Neden rakip yapamaz:** Ligi yöneten bir öğretmen yok. Bizde sosyal mekanik **pedagojik denetim** altında.
- **Çözüm taslağı:** R-02'nin varsayılan grubu + R-01'in projeksiyonu; üstüne öğretmen görünümü:
  sıralama + "bu hafta hiç çalışmayanlar" listesi (öğretmene özel, öğrenciye gösterilmez).
- **Kabul kriteri:** Öğretmen sıralamayı ve çalışmayanları görüyor · öğrenci yalnız ilk N + kendi sırasını
  görüyor · opt-out öğrenci hiçbir listede yok · öğretmen tek dokunuşla o öğrenciye mesaj/duyuru atabiliyor (R-06).
- **Nasıl ölçeriz:** Lig gören öğrencinin haftalık çalışma dakikasında artış (A/B ile ölçülebilir).
- **Risk / tuzak:**
  - **Utandırma ve baskı:** En büyük risk. Alt sıralar gizli, "çalışmayanlar" listesi **yalnız öğretmende**.
  - Velinin sıralamayı görmesi çocuk üzerinde baskı yaratır — v1'de veliye **gösterilmemeli**.
- **Bağımlılık:** R-01 + R-02.
- **Efor:** S *(ikisi yapıldıysa)* · **Karar:** ` ` · **Not:** ` `

### Ü-08 — Erken uyarı: ödeme + devamsızlık + düşüş birleşik risk sinyali 🟢

- **Fikir:** M07 (geciken ödeme) + M05 (kaçırılan ders) + M10 (düşen trend) birleşince **öğrenci kaybı
  sinyali** çıkar: *"Ali: son 2 dersi kaçırdı, ödemesi 12 gün gecikti, neti 3 hafta düşüyor."*
- **Ürün ifadesi:** Öğretmen panelinde "dikkat isteyen öğrenciler" kartı; gerekçesi tıklanınca açılıyor.
- **Neden rakip yapamaz:** Üç sinyalin üçü de aynı üründe yalnız bizde. Tilki ödeme görür ama gelişim görmez;
  Doping gelişim görür ama ödeme ve devam görmez.
- **Neden değerli:** Öğretmenin **doğrudan cebine dokunuyor** — kaybedilen öğrenci = kaybedilen gelir.
  Bu yüzden premium için en ikna edici özellik adayı: Tilki'nin 120 ₺/öğrenci/ay fiyatına karşı somut karşılık.
- **Çözüm taslağı:**
  - `Reporting`'e `StudentRiskProjection` — üç bileşenli skor (devam, ödeme, gelişim), her biri
    açıklanabilir; **kara kutu skor yok**, her bileşen ayrı gösterilir.
  - Besleme: mevcut olaylar (ders tamamlandı/iptal, ödeme beklendi/tahsil edildi, hâkimiyet değişimi).
  - Uç: `GET /api/reporting/teachers/{id}/at-risk`.
  - Bildirim: haftalık özet olarak (X-02'nin `RichNotifications` kapısı burada anlam kazanır) —
    `UserNotificationKind` mevcut, yeni tip gerekmez (`System` veya yeni `RiskAlert`).
  - **Entitlement:** Premium özelliği olarak kapılanır (`DetailedAnalytics`).
- **Kabul kriteri:** Her uyarı **üç gerekçeyi de** ayrı gösteriyor · yanlış pozitif (tatilde olan öğrenci)
  için "yok say" seçeneği var · Free kullanıcı kartı görüyor ama içeriği kilitli (paywall'a doğru yol) ·
  projeksiyon yeniden kurulabiliyor.
- **Nasıl ölçeriz:** Uyarı sonrası öğretmenin temas oranı; uyarı alan öğrencilerde ilişki devam oranı.
- **Risk / tuzak:**
  - **Yanlış pozitif güveni öldürür:** İlk sürüm dar ve muhafazakâr olmalı (üç sinyalden **en az ikisi**).
  - **Ödeme sinyali gizlilik gerektirir:** Öğrenciye asla gösterilmemeli.
  - Skoru tek sayıya indirgemek ("risk %72") güveni azaltır; gerekçeli liste daha ikna edici.
- **Bağımlılık:** M05 · M07 · M10 · M14 · X-02 (bildirim kapısı).
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

### Ü-09 — Veri sahipliği ve gizlilik duruşu 🟡

- **Fikir:** "Özel Ders Takip Sistemi" **"tüm verileriniz sadece kendi cihazınızda saklanır, internet
  gerektirmez, %100 gizlilik"** diyerek bir niş açmış — bu, bulut ürünlerine duyulan güvensizliğin kanıtı.
  Bizde `GET/PUT /students/{id}/sharing` (öğrencinin neyi paylaşacağını seçmesi) ve M15'in üç sözleşmesi
  **zaten var**; bunu **duruşa** çevirmek bedava.
- **Ürün ifadesi:** *"Çocuğunun verisini kimin göreceğine sen karar verirsin."* — ayar değil, vaat.
- **Çözüm taslağı:** Yeni özellik değil, **görünürlük**: onboarding'de bir ekran, ayarlarda toplu
  "kim neyi görüyor" özeti, gizlilik sözleşmesinin sade dille yazılmış hâli. Teknik ayağı P13 Task 5
  (KVKK hesap silme ve saklama, **Q5**'e bağlı).
- **Kabul kriteri:** Kullanıcı tek ekranda "öğretmenim şunu görüyor, velim şunu görüyor" listesini görüyor ·
  her satır kapatılabiliyor · kapatınca ilgili uç gerçekten veri döndürmüyor (integration testi).
- **Nasıl ölçeriz:** Gizlilik ekranını açan kullanıcı oranı; destek taleplerinde "verim kimde" sorusunun azalması.
- **Risk / tuzak:** Vaat edip uygulamamak en kötüsü. "Kapattım ama öğretmen hâlâ görüyor" tek bir örnekte
  bile çıkarsa duruş tersine döner — kapatma **sunucuda** zorlanmalı, arayüzde gizlemek yetmez.
- **Bağımlılık:** M08 sharing (🟢) · M15 (🟢) · P13 Task 5 (Q5).
- **Efor:** S *(+ P13 Task 5)* · **Karar:** ` ` · **Not:** ` `

### Ü-10 — Doğrulanmış öğretmen profili (operasyondan ilana) 🟢

- **Fikir:** Pazar yerlerinin çözemediği problem **güven**: Öğretsen'de ve Özel Ders Alanı'nda profil
  öğretmenin **kendi beyanı**. Bizde M12 açıldığında profil **gerçek operasyon verisiyle** dolu olur:
  tamamlanan ders sayısı, planlanan/gerçekleşen oranı, süreklilik (aynı öğrenciyle kaç ay),
  veli memnuniyeti (M13 — üstelik **yalnız gerçekten ders almış** kişiden).
- **Ürün ifadesi:** *"142 ders tamamladı · %96 planına sadık · ortalama öğrenci ilişkisi 7 ay"* —
  beyan değil, **kayıt**.
- **Neden rakip yapamaz:** Pazar yerlerinin elinde ders **sonrası** veri yok; ilişki ilk mesajdan sonra
  platform dışına çıkıyor. Bu, PRD §10.1'in "önce operasyon, sonra eşleştirme" stratejisinin **asıl ödülü**:
  bugün gecikme gibi görünen şey, M12 açıldığında kopyalanamaz bir avantaja dönüşüyor.
- **Çözüm taslağı:** `Reporting`'e `TeacherCredibilityProjection` (tamamlanan ders, plana sadakat oranı,
  ortalama ilişki süresi, doğrulanmış yorum sayısı); M12 ilan kartı bu projeksiyondan okur.
  Rozetler: "100+ ders", "3+ yıl", "yüksek süreklilik".
- **Kabul kriteri:** Profildeki her sayı **tıklanınca kaynağına** iniyor · yeni öğretmende "henüz veri yok"
  dürüstçe yazıyor (uydurma rozet yok) · metrikler manipüle edilemiyor (sahte ders girip sayı şişirme
  engelli — ör. yalnız öğrenci tarafından da onaylanan dersler sayılır).
- **Nasıl ölçeriz:** İlan → ilk mesaj dönüşüm oranı; doğrulanmış metrikli profillerin dönüşüm farkı.
- **Risk / tuzak:**
  - **Yeni öğretmen dezavantajı:** Sıfır verili öğretmen hiç eşleşemezse pazar tek yanlı olur —
    "yeni" etiketi ve ayrı bir vitrin gerekir.
  - **Metrik şişirme:** Sahte öğrenci + sahte ders. Karşı önlem: yalnız iki taraflı onaylı dersler sayılır.
- **Bağımlılık:** M12 (🔴) · M13 (🔴) · M05/M04 (🟢) — **P11'e eklenmeli**.
- **Efor:** M · **Karar:** ` ` · **Not:** ` `

---

## 6. Veri modeli ve olay etkisi (plan çıkarırken tek bakış)

| Madde | Modül | Yeni entity / alan | Yeni olay | Yeni uç (özet) | Mobil |
|-------|-------|--------------------|-----------|----------------|-------|
| R-08a | — | — | — | — | paylaşım düğmesi + metin şablonu |
| R-08b | M14 Reporting | `ShareLink` | — | `POST .../share-link`, `GET /shared/{token}` (anonim) | link üret/iptal |
| R-06 | M16 Messaging | `Announcement`, `AnnouncementRecipient`, `Audience` | `AnnouncementPublished` | 4 uç | duyuru gönder + gelen kutusu sekmesi |
| R-10 | — *(yalnız mobil)* | — | — | — | `local_reminder_service` + ayar |
| R-07 | M06 Assignments | *(yok — `LessonResource` yeter)* | — | `GET .../gallery` | galeri ekranı + çoklu yükleme |
| R-03 | M08 Study | `DailyQuest`, `QuestKind`, `Goal` rozetleri (X-03) | — | 2 uç | "Bugün" kartı |
| R-01 | M08 Study | `WeeklyLeaderboardProjection` | — | 1 uç | lig ekranı + ana ekran şeridi |
| R-02 | M03 Students | `StudyGroup`, `StudyGroupMember`, `IStudyGroupDirectory` | — | 4 uç | grup/katılım ekranı |
| R-05 | M08 Study | `Flashcard`, `FlashcardReview` | — | 3 uç | kart çevirme ekranı |
| R-09 | M17 Membership | `PurchaseReceipt` | — | 2 uç + webhook | satın alma + geri yükleme |
| Ü-01 | M14 Reporting | `StudentEvidenceProjection` | — | 1 uç | karne kartı |
| Ü-02 | M14 Reporting | `TopicCausalityProjection` | — | 1 uç | içgörü ekranı |
| Ü-03 | M06 Assignments | `ContentItem`, `ContentAssignment` | — | 4 uç | "İçeriklerim" + atanan içerik |
| Ü-04 | M14 Reporting | `ParentMonthlyProjection` | — | 1 uç | veli ay kartı |
| Ü-05 | M04/M02 | *(yok)* | — | birleşik sorgu | birleşik takvim |
| Ü-06 | M12 Matching | `AvailabilityWindow` (ilan alanı) | — | M12 içinde | boş saat → ilan |
| Ü-07 | M08 + M16 | *(yok — R-01+R-02 üstüne)* | — | 1 uç (öğretmen görünümü) | öğretmen lig ekranı |
| Ü-08 | M14 Reporting | `StudentRiskProjection` | — | 1 uç | "dikkat isteyen öğrenciler" kartı |
| Ü-09 | — | *(yok)* | — | — | gizlilik özet ekranı |
| Ü-10 | M14 Reporting | `TeacherCredibilityProjection` | — | M12 profilinde | ilan kartı |

> **Dikkat çeken sonuç:** 20 maddeden **19'u yeni domain olayı gerektirmiyor.** Mevcut olay akışı
> (§1.2) ve projeksiyon deseni (§1.3) neredeyse tüm işi taşıyor. Yeni olay yalnız R-06'da gerekiyor.

---

## 7. Dalga planı

> Sıralama ölçütü: **etki ÷ efor**, ve her dalganın kendi içinde tutarlı bir hikâye anlatması.
> Her dalga sonunda ürün **anlatılabilir** bir şey kazanmalı.

### 🌊 Dalga 1 — Sızıntıyı durdur · ≈1 hafta

**Amaç:** Ürünü terk ettiren şeyleri kapatmak. Yeni pazar açmaz, mevcut kullanıcıyı tutar.

| Madde | Efor | Neden burada |
|-------|------|--------------|
| **R-08a** Paylaşım (`share_plus` + hazır özet metni) | S | Velinin uygulama indirme zorunluluğunu kaldırır — en büyük sessiz kayıp |
| **R-06** Bire-çok duyuru | S–M | Öğretmeni WhatsApp grubuna kaçıran boşluk; terk riski en yüksek madde |
| **R-10** Zamanlı yerel hatırlatıcı | S | Streak zaten var ama hatırlatan yok; **D-18'i de kapatır** |
| **R-07** Galeri görünümü | S | Depolama hazır, yalnız ekran |

- **Çıktı:** Öğretmen tek işlemde herkese duyuru atabiliyor; veli özeti WhatsApp'tan alabiliyor;
  öğrenci akşam hatırlatıcısı görüyor; veli materyalleri tarihe göre gezebiliyor.
- **Doğrulama:** Duyuru fan-out'u gerçek Postgres testinde (P10 deseni); paylaşım metni birim testli;
  hatırlatıcı çift bildirim üretmiyor; `flutter analyze` temiz ve **kullanılmayan paket kalmadı**.
- **Bitince ne değişir:** "Veliye gösteremiyorum" ve "duyuru atamıyorum" şikâyetleri biter.

### 🌊 Dalga 2 — Alışkanlık motorunu tamamla · ≈1,5 hafta

**Amaç:** Öğrencinin günlük açılış sebebini kurmak. Rozet + streak zaten var; eksik olan sosyal ve döngüsel kısım.

| Madde | Efor | Neden burada |
|-------|------|--------------|
| **R-03** Günlük görev döngüsü (+ **X-03** `Goal` rozetleri) | M | "Bugün ne yapmalıyım" cevabı — ödevden türerse rakiplerinkinden gerçek |
| **R-02** Grup + kod ile katılım | M | R-01'in taşıyıcısı; öğretmenin sınıfı varsayılan grup |
| **R-01** Haftalık lig | M | Sosyal baskı — tutundurmanın en ucuz kaldıracı |
| **Ü-07** Sınıf ligi (öğretmen görünümü) | S | Rakiplerin ligini geçen kat: müdahale edebilen yetişkin |
| **Ü-01** Doğrulanmış çalışma karnesi | M | Üç kaynaklı kanıt — Net Up'ın tek kaynaklı iddiasını geçer |

- **Çıktı:** Öğrenci uygulamayı "bugünkü görevim var" diye açıyor; sınıf sıralaması görüyor;
  veli/öğretmen doğrulanmış karneyi görüyor.
- **Doğrulama:** Lig projeksiyonu `IProjectionRebuilder` ile aynı sonucu üretiyor; opt-out öğrenci
  hiçbir listede yok; görev üretimi 04:00 gün sınırını streak ile paylaşıyor.
- **Bitince ne değişir:** Ürün "kayıt tutma aracı"ndan "günlük kullanılan uygulama"ya geçer.

### 🌊 Dalga 3 — Parayı aç · ≈1,5 hafta · 🚫 **Q1 kararına bağlı**

**Amaç:** Ürünün para kazanması ve premium'un **satılabilir** bir gerekçe kazanması.

| Madde | Efor | Neden burada |
|-------|------|--------------|
| **R-09** IAP (premium) + iyzico (ders ücreti) ayrımı | L | Ürün ücretsiz olmaktan çıkar; P09 Task 4 |
| **X-02** `DetailedAnalytics` / `RichNotifications` kapılarını bağla | S | Ödeme açılmadan **önce** yapılmalı, sonra yapmak özellik geri almak olur |
| **Ü-08** Erken uyarı kartı | M | Premium'un en ikna edici gerekçesi — öğretmenin cebine dokunuyor |
| **Ü-04** Veli: para + gelişim tek ekranda | M | Veliyi ödeme yapan tarafa dönüştürür *(M07 veli paylaşımı borcu önce kapanmalı)* |

- **Çıktı:** Kullanıcı premium satın alabiliyor; premium gerçekten fazla şey veriyor; veli ne için ödediğini görüyor.
- **Doğrulama:** Sandbox satın alma → `GET /membership/me` Premium → iptal → dönem sonunda Free
  (mevcut `RealMembershipEntitlementTests` zinciri genişletilir).
- **Bitince ne değişir:** M17 🟡 → 🟢 olur.

### 🌊 Dalga 4 — Kopyalanamaz derinlik · ≈2–3 hafta

**Amaç:** İçerik boşluğunu içerik şirketi olmadan kapatmak ve öğretmen tarafında kilitlenme yaratmak.

| Madde | Efor | Neden burada |
|-------|------|--------------|
| **Ü-03** Öğretmen içerik havuzu | L | İçerik maliyeti sıfır; **havuzunu biriktiren öğretmen çıkamaz** |
| **R-05** Flashcard + aralıklı tekrar | M | Ü-03'ün öğrenci tarafındaki tüketim yüzeyi |
| **Ü-02** Nedensellik raporu | M | "İstatistik" ile "içgörü" arasındaki fark |
| **Ü-05** Çok öğretmenli birleşik görünüm | M | İkinci öğretmeni platforma çeker — büyüme kanalı |

- **Çıktı:** Öğretmenin kendi soru/kart havuzu birikiyor ve yeniden kullanılıyor; öğrenci tekrar kuyruğu
  görüyor; raporlar neden-sonuç anlatıyor.
- **Doğrulama:** Aynı içeriğin iki öğrenciye ve ikinci döneme atanabilmesi; içgörü cümlelerinin
  eşik altında üretilmemesi.
- **Bitince ne değişir:** Rakiplerin kopyalayamayacağı ilk gerçek hendek açılır.

### 🌊 Dalga 5 — Pazarı aç · ≈2–3 hafta *(mevcut P11)*

| Madde | Efor | Neden burada |
|-------|------|--------------|
| **R-11 + R-12** Eşleştirme + yorum | L + M | Mevcut P11 planı |
| **Ü-10** Doğrulanmış profil | M | P11'in **ayrılmaz parçası** — beyan değil kayıt |
| **Ü-06** Boş zaman → ilan | M | `FreeSlotCalculator` hazır, yalnız M12 bekliyor |

- **Çıktı:** Öğretmen bulunabiliyor, profili gerçek veriyle dolu, boş saatini ilana çevirebiliyor.
- **Ön koşul:** Dalga 1–4 bitmiş olmalı — boş pazar sorununa karşı tek savunma **dolu bir operasyon ürünü**.

### 🌊 Dalga 6 — Masaüstü *(mevcut P14, XL)*
**R-13** Web (Angular). Öncelik: öğretmen takvimi + toplu giriş + raporlar.

### Kapsam dışı
**R-14** (soru bankası/video) — Ü-03 ile ikame ediliyor. **R-04** (XP) — rozet varken çakışıyor;
R-01/R-03 ölçümü isterse yeniden değerlendirilir.

---

## 8. Bağımlılık ve program haritası

```
Dalga 1 (R-06, R-07, R-08a, R-10)   → bağımsız, hemen başlar · D-18'i kapatır
Dalga 2 (R-01, R-02, R-03 + Ü-01, Ü-07)
        ├─ R-02 → R-01 → Ü-07   (grup olmadan lig, lig olmadan sınıf ligi yok)
        ├─ R-03 → X-03          (Goal rozetleri aynı işte)
        └─ gizlilik kapısı: /students/{id}/sharing (🟢) + M15 sözleşmeleri
Dalga 3 (R-09 + X-02 + Ü-04, Ü-08)  → 🚫 Q1 · X-02 R-09'dan ÖNCE · M07 "veli paylaşımı ⚠️" Ü-04'ten önce
Dalga 4 (Ü-03 → R-05 → Ü-02, Ü-05)  → M06 + P04 depolama üzerine
Dalga 5 (R-11, R-12 + Ü-06, Ü-10)   → mevcut P11; P12 (admin/moderasyon) yorum moderasyonu için gerekli
Dalga 6 (R-13)                      → mevcut P14
```

**Mevcut programla ilişki** (`docs/superpowers/ILERLEME.md` — aktif: **P13, 1/8**):

| Bu doküman | Mevcut plan | İlişki |
|------------|-------------|--------|
| R-10 | P13 Task 6 (mobil hijyen) | `flutter_local_notifications` kullanılınca **D-18 kapanır** — birlikte yapılmalı |
| R-09, X-02 | P09 Task 4 (🚫 Q1) | Rakip kanıtı Q1'in cevabını değiştiriyor: **IAP (premium) + iyzico (ders ücreti) ayrı** |
| R-11, R-12, Ü-06, Ü-10 | P11 (0/6) | **Ü-10 P11 kapsamına eklenmeli** — profil beslemesi planda yok |
| Ü-09 | P13 Task 5 (🚫 Q5) | KVKK ayağı |
| R-13 | P14 (0/5) | Değişiklik yok |
| **Dalga 1, 2, 4** | **Plan yok** | Yeni plan gerekiyor: **P15 — Alışkanlık, paylaşım ve içerik** önerilir |
| R-06 | — | M16 🟡 → 🟢 yolundaki eksiklerden biri (karşı tarafın adı, veli sohbeti ile birlikte) |

---

## 9. Karar özeti (senin dolduracağın hızlı tablo)

| ID | Başlık | Efor | Dalga | Öneri | **Kararın** | Not |
|----|--------|------|-------|-------|-------------|-----|
| X-02 | Entitlement kapılarını bağla | S | 3 | ✅ Yap *(R-09'dan önce)* | ` ` | ` ` |
| X-03 | `Goal` kategorisinde rozet yok | S | 2 | ✅ Yap *(R-03 ile)* | ` ` | ` ` |
| R-08a | Paylaşım: `share_plus` + özet metni | S | 1 | ✅ Yap | ` ` | ` ` |
| R-08b | Giriş gerektirmeyen veli özet linki | M | 1–2 | ✅ Yap | ` ` | ` ` |
| R-06 | Bire-çok duyuru | S–M | 1 | ✅ Yap | ` ` | ` ` |
| R-10 | Zamanlı yerel hatırlatıcı (+D-18) | S | 1 | ✅ Yap | ` ` | ` ` |
| R-07 | Galeri (foto/video, tarihe göre) | S | 1 | ✅ Yap | ` ` | ` ` |
| R-03 | Günlük görev döngüsü | M | 2 | ✅ Yap | ` ` | ` ` |
| R-02 | Grup + kod ile katılım | M | 2 | ✅ Yap | ` ` | ` ` |
| R-01 | Haftalık lig / sıralama | M | 2 | ✅ Yap | ` ` | ` ` |
| Ü-07 | Sınıf ligi (öğretmen görünümü) | S | 2 | ✅ Yap | ` ` | ` ` |
| Ü-01 | Doğrulanmış çalışma karnesi | M | 2 | ✅ Yap | ` ` | ` ` |
| R-09 | IAP + iyzico ayrımı | L | 3 | ✅ Yap *(Q1)* | ` ` | ` ` |
| Ü-08 | Erken uyarı (risk altındaki öğrenci) | M | 3 | ✅ Yap | ` ` | ` ` |
| Ü-04 | Veli: para + gelişim tek ekran | M | 3 | ✅ Yap | ` ` | ` ` |
| Ü-03 | Öğretmen içerik havuzu | L | 4 | ✅ Yap | ` ` | ` ` |
| R-05 | Flashcard + aralıklı tekrar | M | 4 | ✅ Yap *(Ü-03 ile)* | ` ` | ` ` |
| Ü-02 | Nedensellik raporu | M | 4 | ✅ Yap | ` ` | ` ` |
| Ü-05 | Çok öğretmenli birleşik görünüm | M | 4 | ⏳ Sonra | ` ` | ` ` |
| Ü-10 | Doğrulanmış öğretmen profili | M | 5 | ✅ **P11'e ekle** | ` ` | ` ` |
| R-11 | Öğretmen arama / filtreleme | L | 5 | ⏳ P11 | ` ` | ` ` |
| R-12 | Puan / yorum | M | 5 | ⏳ P11 | ` ` | ` ` |
| Ü-06 | Boş zaman → ilan | M | 5 | ⏳ P11 sonrası | ` ` | ` ` |
| Ü-09 | Veri sahipliği duruşu | S | — | ✅ Yap | ` ` | ` ` |
| R-13 | Web (Angular) | XL | 6 | ⏳ P14 | ` ` | ` ` |
| R-04 | XP / seviye / sandık | S | — | ❌ İptal *(rozet var)* | ` ` | ` ` |
| R-14 | Soru bankası / video içerik | XL | — | ❌ İptal *(Ü-03 ikame)* | ` ` | ` ` |

**Toplam efor (öneriler kabul edilirse):** Dalga 1–2 ≈ 2,5 hafta · Dalga 3 ≈ 1,5 hafta (Q1 açılınca) ·
Dalga 4 ≈ 2–3 hafta · Dalga 5 = mevcut P11 · Dalga 6 = mevcut P14.

---

## 10. Üç cümlelik özet

1. **Aynı seviyeye gelmek sanıldığından yakın.** Streak, 10 rozet, deneme, net trendi, konu kataloğu,
   hedef, PDF rapor ve boş zaman analizi **zaten kodda**. Gerçek parite açığı dört küçük maddede
   (paylaşım, duyuru, hatırlatıcı, galeri) ve bir büyük maddede (para tahsilatı) toplanıyor.
2. **Öne geçmek içerik almakla değil, veriyi birleştirmekle olur.** Ü-01…Ü-10'un tamamı, elimizdeki
   üç tarafın (öğretmen + öğrenci + veli) tek veri modelinde olmasından doğuyor — rakiplerin
   yapısal olarak kopyalayamayacağı tek şey bu. **20 maddeden 19'u yeni domain olayı bile gerektirmiyor.**
3. **En büyük tek risk para değil, sürtünme.** Velinin veriyi görmek için uygulama indirmek zorunda
   olması (R-08), rakibin (Tilki) bilerek terk ettiği bir zorunluluk. En ucuz, en yüksek getirili iş bu.

---

*Güncelleme: 2026-09-05*
