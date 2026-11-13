# EğitimÜssü Olgunlaştırma Programı — Master Tasarım (Spec)

**Tarih:** 2026-11-13
**Önceki program:** [`2026-09-02-saglamlastirma-master-design.md`](2026-09-02-saglamlastirma-master-design.md) (P01–P14). Onun §1 çıtası, §2 kararları (K-01…K-12) ve §4 ortak sözleşmeleri **aynen geçerlidir**; bu doküman yalnız ekler ve değişiklikleri içerir.
**Kaynak analiz:** [`doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`](../../../doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md) (N-01…N-72, P-01…P-14, §2 özellikler, §4 doküman çelişkileri)
**Amaç:** P01–P13 `main`'e merge edildikten sonra koddan çıkan mantık hatalarını kapatmak, üç rolün (öğrenci · öğretmen · veli) birbirine bağlı akışlarını uçtan uca çalışır hale getirmek, **okul öğretmeni** personasını eklemek ve ürünü mağaza yayınına hazırlamak.

---

## 0. Ürün vizyonu (kullanıcı, 2026-11-13)

Üç süreç gerçekte birbirine bağlıdır:

| Rol | İhtiyaç |
|-----|---------|
| **Öğrenci** | Kendi çalışmasını takip etmek, derslerini ve ders programını kontrol etmek, **planlı çalışmak** |
| **Öğretmen** | **Okul öğretmeni** ve **özel öğretmen**: birden fazla öğrenciyi kolay takip etmek ve yönetmek. **Özel öğretmen** ayrıca finansını (ücret, tahsilat) takip etmek |
| **Veli** | Çocuğunun ders durumunu takip etmek |

Bu vizyon PRD v2.1'i genişletir: PRD ürünü "Özel Ders Yönetim ve Eşleştirme Platformu" olarak tanımlıyor, okul öğretmeni personası ve sınıf kavramı yok. PRD güncellemesi **P22 Task 1**'dir.

---

## 1. Çıta eklemeleri (2026-09-02 §1'e ek)

| Ölçüt | Kural |
|-------|-------|
| **Senaryo testi** | Modüller arası bir akışa dokunan her plan, akışı **gerçek Postgres'te uçtan uca** koşan en az bir integration senaryosu ekler (Testcontainers). Gerekçe: N-01/N-02/N-12/N-40, 733 birim testi yeşilken bozuktu; birim testleri sahte depo ve tek modül gördüğü için yakalayamadı. |
| **Sahte depo yasağı (davranış testinde)** | Bir handler'ın "kaydetmeden önce okuma" gibi persistence sırasına bağlı davranışı, sahte depo ile değil EF InMemory/Testcontainers ile test edilir (N-40 dersi). |
| **Saat** | Gün/hafta/ay sınırı ve kullanıcıya gösterilen her saat `ILocalCalendar` üzerinden (K-19). `DateOnly.FromDateTime(utc)`, `utc.Date`, UTC `DayOfWeek` iş kuralında **yasak**. |
| **Para** | Backend `decimal` + ISO para birimi; mobil kuruş `int`; ayrıştırma ve biçimleme tek yardımcıdan (K-23). |
| **Mobil yazma** | Kayıt oluşturan/para hareketi yapan/mesaj gönderen her mobil çağrı `Idempotency-Key` taşır. |
| **Veri göçü** | Benzersiz indeks eklemeden **önce** mevcut tekrarlar temizlenir ve yeni kolon geriye dönük doldurulur; migration'ın `Up`'ı bunu SQL ile yapar, `Down`'ı geri alır. `scripts/check-migration-down.sh` yeşil kalır. |
| **ARB** | Bir görevin dokunduğu her ekran ARB'ye taşınır (K-26). |
| **Yetki testi** | Yeni/değişen her yazma ucu için "başka öğretmen / bağı arşivlenmiş öğretmen / bağsız kullanıcı → 403" testi yazılır. |

---

## 2. Karar kaydı (K-13 …)

> Önceki K-01…K-12 geçerli. **K-05 daraltıldı** (bkz. K-25).

| # | Karar | Gerekçe | Reddedilen alternatif | Onay |
|---|-------|---------|------------------------|------|
| **K-13** | **Öğretmen tipi hesapta değil ilişkide.** Tek `Teacher` rolü. Öğretmen–öğrenci erişimi `ITeacherStudentAccess.GetScopeAsync(...)` → `TeacherAccessScope { None, Private, Class }`. `TeachingContext { School, Private, Both }` profilde yalnız **arayüz varsayılanı** belirler, yetki vermez. | Aynı kişi hem okulda hem özel ders verebiliyor; iki rol iki hesap demek. | Ayrı `SchoolTeacher` rolü | ✅ kullanıcı 2026-11-13 |
| **K-14** | **Sınıfa katılım yalnız kodla.** Öğretmen sınıf bağlamında öğrenci kaydı açamaz, kişisel veri giremez. 18 yaş altı öğrencinin katılımı veli onayına bağlıdır (model Q10 hukuk teyidine açık). | KVKK: rıza öğrenci/veliden gelir, öğretmen veri sorumlusu durumuna düşmez. | Öğretmenin sınıf listesi yüklemesi | ✅ kullanıcı 2026-11-13 |
| **K-15** | **Sınıf bağlamında öğrencinin bireysel çalışma verisi varsayılan kapalı**; öğrenci "sınıf öğretmenlerimle paylaş" ile açar. Ödev ve deneme sonuçları sınıf öğretmenine görünür. | Öğrencinin özel alanı korunur; okul öğretmeninin gerçekten ihtiyacı olan veri ödev/sınav. | Varsayılan açık | ✅ kullanıcı 2026-11-13 |
| **K-16** | **Okul öğretmeni ücretsiz.** `MaxStudents` yalnız **özel ders bağlarını** sayar; sınıf üyeleri saymaz. Sınıf için yalnız kötüye kullanım koruması olarak adil kullanım tavanı (varsayılan 15 aktif sınıf × 60 üye; config). | Okul öğretmeni büyüme kanalı: öğrenciyi ve veliyi getirir; gelir onların premium'undan ve özel öğretmenden. | Okul öğretmenine de 5 öğrenci limiti | ✅ kullanıcı 2026-11-13 |
| **K-17** | **Sınıfta iletişim v1'de yalnız tek yönlü duyuru.** Birebir mesajlaşma özel ders bağına özgü. | Öğretmeni yormaz; reşit olmayanlarla birebir iletişim riskini azaltır. | Sınıfta serbest mesajlaşma | ✅ kullanıcı 2026-11-13 |
| **K-18** | **Finans yalnız özel ders bağında.** Sınıf bağında ücret/ödeme kavramı yoktur. Özel öğretmenin grup dersi (P29) sınıf modelini `Private` bağlamla kullanır, kişi başı ücret orada açılır. | Devlet okulu öğretmeninin ücretli özel ders vermesi mevzuatla kısıtlı (Q10 teyit); okul ilişkisine finans karışmamalı. | Sınıfta isteğe bağlı ücret | Öneri |
| **K-19** | **Zamanın tek kaynağı `ILocalCalendar`** (`Shared/Kernel` arayüz, varsayılan `Europe/Istanbul`). Ders kendi `TimeZone` alanını korur; kullanıcı saat dilimi P24'te. Mevcut `StudyLocalTime` (04:00 seri günü) bunun üzerine kurulur. | Saat dilimi hatası 6 modüle yayılmış (N-04, N-06, N-11, N-43, N-49, N-50); tek yerde çözülmeli. | Her modülde ayrı düzeltme | Öneri |
| **K-20** | **Ders oluşumu birinci sınıf kavram.** Oturum, ücret, hatırlatma ve projeksiyon satırının anahtarı `(LessonScheduleId, OccurrenceStartUtc)`. Tekrarlı derste iptal/erteleme/tamamlama **kapsam zorunlu** (`Single | ThisAndFuture | All`). | N-01, N-02, N-06, N-49 aynı kök neden: seri tek kayıt gibi işleniyor. | Seriyi her hafta ayrı ders olarak üretmek (veri patlaması, seri düzenleme kaybı) | Öneri |
| **K-21** | **Ders tamamlama yalnız LessonSessions'ta.** Scheduling'in durumu `LessonSessionCompleted` olayıyla senkronize edilir; Scheduling `/complete` ucu kaldırılır; mobilde onu çağıran iki ekran (`lesson_detail_page.dart`, `lesson_sessions_page.dart` → `SchedulingCubit.completeLesson`) aynı görevde oturum tamamlamaya taşınır (P17 T4). | N-03: iki tamamlama yolu birbirini kilitliyor. | İki yolu da koruyup senkronize etmek | Öneri |
| **K-22** | **Eşzamanlılık:** kritik aggregate'larda `uint Version` + `.IsRowVersion()` (Npgsql 9 bunu `xmin` sistem kolonuna eşler; kolon eklenmez). Çakışma `409 shared.concurrency_conflict`. Para ve sayaç değişiklikleri **niyete dayalı komutlarla** (`AddCollection`, `Refund`), tam PUT ile değil. | Projede concurrency token **0** (§1.5); ödeme, abonelik, seri sayaçlarında lost update. | Pesimist kilit her yerde | Öneri |
| **K-23** | **Para:** backend `decimal(18,2)` + `Currency`; mobil `int` kuruş; toplamlar para birimi bazında döner. | N-64 ("1.500" → 1,5 TL), N-11 (farklı para birimleri toplanıyor). | Mobilde `double` | Öneri |
| **K-24** | **Davet ve katılım kodları:** `ISecureCodeGenerator` (CSPRNG, karışmayan 31 karakterli alfabe — `0/O/1/I/L` yok —, 8 karakter: 31⁸ ≈ 8,5×10¹¹). Davet kodu **7 gün** geçerli; sınıf kodu öğretmen yenileyene kadar. Kullanıcı başına başarısız deneme limiti (10/saat → 429). Talep eden rolü doğrulanır (veli kodu → `Parent`, öğrenci kodu → `Student`). Üreten kendi kodunu kullanamaz. | N-20: 6 haneli `Random.Shared`, süresiz, rol kontrolsüz. Mobil bu kodları henüz tüketmediği için biçim değişikliği **şimdi** risksiz. | Kısa sayısal kod + yalnız hız sınırı | Öneri |
| **K-25** | **Premium mobilde mağaza içi satın almayla** (StoreKit 2 / Google Play Billing; RevenueCat önerilir — Q16). iyzico yalnız ileride **özel ders ücreti tahsilatı** için (Apple 3.1.3(d) istisnası). P09 Task 4 → P28'e devredildi. | Apple 3.1.1 / Play Billing: dijital içerik/abonelik mobilde IAP ister; aksi mağaza reddi. | iyzico ile mobil abonelik | Öneri |
| **K-26** | **ARB taşıması tur tur değil, "dokunulan ekran taşınır" kuralıyla**; kalan ekranlar P25'te tek süpürmede. P13 Task 6b bu kurala devredildi, **P13 kapandı**. | 17 ayrı tur, ürün değeri yüksek işleri bloke ediyordu. | Turlara devam | Öneri |
| **K-27** | **Yayın kapısı** (bkz. §3.2). Kapı maddeleri bitmeden ilgili yayın yapılmaz. | Mağaza reddi ve hukuki risk. | — | Öneri (Q15) |
| **K-28** | **Mesajlaşma:** ilişki (özel bağ / veli bağı) bitince sohbete **gönderim kapanır**, geçmiş **salt-okunur** kalır. | N-46; kullanıcının kendi geçmişini görmesi meşru, yeni temas değil. | Sohbeti tamamen gizlemek | Öneri |
| **K-29** | **Migration konsolidasyonu (P-13) en son**, açık özellik planı yokken (P26 son görevi). Konsolidasyonda outbox/inbox modül şemasında kalır; `job_leases`, `outgoing_emails`, `module_states` tek `infra` şemasına taşınır; eski migration'lar squash edilip yeni baseline alınır. | Concurrency token ve yeni modül migration'larıyla çakışmasın. | Hemen squash | Öneri |

---

## 3. Plan seti, dalgalar ve sıra

> **Görev sayıları** hafif planlarda Task 0 (ayrıntılandırma) **hariç** verilmiştir.
> **Detay seviyesi:** 🅳 = adım adım yürütülebilir (Files/Interfaces/Steps). 🅻 = hafif plan (görevler + kapsadığı maddeler + kabul kriterleri); **ilk görevin ilk adımı "planı kodu okuyarak ayrıntılandır"dır** ve `/devam` kod yazmadan önce onu yürütür.

| Dalga | Plan | Kapsam | Bağımlılık | Efor | Detay |
|-------|------|--------|------------|------|-------|
| **0 Güvenlik** | [P15 Güvenlik acil](../plans/2026-11-13-15-guvenlik-acil.md) | Davet kodları, erişim kapsamı, bağ/claim/merge kuralları, veli erişimi iptali, mesajlaşma ilişkisi, üretim sağlayıcı guard'ı, token/oturum, iş kirası | — | L (9 görev) | 🅳 |
| **1 Çekirdek doğruluk** | [P16 Zaman ve takvim çekirdeği](../plans/2026-11-13-16-zaman-takvim-cekirdegi.md) | `ILocalCalendar`, yerel tekrar genişletme, istisnalar, çakışma, gün/ay kovaları | P15 | M (6) | 🅳 |
| | [P17 Ders oluşumu zinciri](../plans/2026-11-13-17-ders-olusumu-zinciri.md) | Kapsam zorunlu, seri bölme, oluşum anahtarlı oturum, tek tamamlama, iptal politikası, oluşum hatırlatması, pano | P16 | L (8) | 🅳 |
| | [P18 Veri bütünlüğü ve ödeme](../plans/2026-11-13-18-veri-butunlugu-odeme.md) | Concurrency token, tahsilat hareketleri, para birimi, mobil para + idempotency | P15 | M (6) | 🅳 |
| | [P19 Çalışma, ödev, arka plan](../plans/2026-11-13-19-calisma-odev-arkaplan.md) | Seri, outbox izolasyonu, ödev durum makinesi, tarayıcı, düzenleme olayları, bildirim kuyruğu, sayfalama | P15 (kira), P16 T1 | L (8) | 🅳 |
| | [P20 Mobil sağlamlaştırma](../plans/2026-11-13-20-mobil-saglamlastirma.md) | Sahte veri, rol rotaları, çıkış/önbellek, ön plan push, cubit yaşam döngüsü | P15 T7 | M (6) | 🅳 |
| **2 Üç rolün akışları** | [P21 Bağ ve rol akışları (mobil)](../plans/2026-11-13-21-rol-akislari-mobil.md) | Davet, Velim, veli kodla katılım, tatil/erteleme, ödedim, hesap silme + rıza, sohbet adları | P15, P17, P18, P20 | L (9) | 🅻 |
| **3 Okul öğretmeni** | [P22 Sınıflar — backend (M19)](../plans/2026-11-13-22-siniflar-backend.md) | PRD/rol dokümanları, Classrooms modülü, kodla katılım, `Class` kapsamı, toplu ödev, duyuru, sınıf panosu, limitler | P15, P16, P19 | L (9) | 🅻 |
| | [P23 Sınıflar — mobil](../plans/2026-11-13-23-siniflar-mobil.md) | Öğretmen sınıf ekranları, toplu ödev, duyuru, öğrenci "Sınıfa katıl" + "Öğretmenlerim", veli onayı | P21, P22 | M (7) | 🅻 |
| | [P24 Ders programı ve birleşik takvim](../plans/2026-11-13-24-ders-programi-birlesik-takvim.md) | Okul ders programı, birleşik takvim + boş zaman önerisi, yerel hatırlatıcı, sessiz saatler | P16, P17 | M (5) | 🅻 |
| **4 Yayın kapısı** | [P12 Admin & moderasyon](../plans/2026-09-02-12-admin-moderasyon.md) *(revize)* | + ilk admin bootstrap, P11 bağımlılığı kaldırıldı, mobil şikayet düğmesi | P15 | M | 🅳 (mevcut) |
| | [P25 Mobil yayın hazırlığı](../plans/2026-11-13-25-mobil-yayin.md) | İmza/AAB/flavor/kimlik/iOS izinleri/App Links/Sentry/zorunlu güncelleme/mağaza formları/ARB süpürmesi/erişilebilirlik | P20 | L (8) | 🅻 |
| | [P26 Operasyon ve mühendislik hijyeni](../plans/2026-11-13-26-operasyon-hijyen-2.md) | Barındırma/yedek, Sentry+OTLP+alarm, Dockerfile, CI güvenliği, deploy hattı, PR akışı, README/sürüm, repo temizliği, analitik, migration konsolidasyonu (son) | — (son görev hariç) | L (9) | 🅻 |
| | [P27 Hukuk ve uyum](../plans/2026-11-13-27-hukuk-uyum.md) | Metinler, kullanım şartları, rıza sürümleme, yaş/veli onayı, aktarım, VERBİS, mağaza veri formları | Q10 | M (6) | 🅻 |
| **5 Gelir** | [P28 Gelir v2 — mağaza içi satın alma](../plans/2026-11-13-28-gelir-v2-iap.md) | IAP + sunucu doğrulaması, premium vaat tutarlılığı, referans/deneme suistimali, AdMob (ops.) | P12, P25, Q11/Q16 | M (6) | 🅻 |
| **6 Büyüme** | [P11 Eşleştirme & yorum](../plans/2026-09-02-11-eslestirme-yorum.md) *(mevcut)* | İlan, keşif, public profil, yorum | P21, P28 | L | 🅳 (mevcut) |
| | [P29 Etkileşim ve büyüme](../plans/2026-11-13-29-etkilesim-buyume.md) | Özet paylaşımı, iCal, oyunlaştırma, ÖSYM tahmini, paket/kredi, ekstre, 2FA, sosyal giriş, CAPTCHA, grup dersi | P23, P24 | XL | 🅻 |
| | [P14 Web (Angular)](../plans/2026-09-02-14-web-angular.md) *(mevcut)* | Önce admin paneli, sonra public keşif | P12, K-11 | L | 🅳 (mevcut) |

**Önceki programın kapanışı:** P01–P10 ✅, P13 ✅ (6b → K-26), P09 5/7 (Task 4 → P28, Task 5 → P28 opsiyonel), P11/P12/P14 bu programın dalgalarına yerleştirildi.

**Yürütme sırası (tek geliştirici, `/devam`):** P15 → P16 → P17 → P18 → P19 → P20 → P21 → P22 → P23 → P24 → P12 → P25 → P27 (kod kısmı) → P26 → P28 → P11 → P29 → P14.
P26'nın barındırma/Sentry/CI görevleri (T1–T6) ve P27'nin kod-dışı işleri **istenen an paralel** yürüyebilir; P26 son görevi (konsolidasyon, K-29) **en sona** kalır.

### 3.1 Sıralama tehlikeleri (planlara gömüldü)

| # | Tehlike | Çözüm (nerede) |
|---|---------|----------------|
| S-1 | Sunucu eşzamanlı refresh'te yalnız birini kazandırınca (N-30), mobilin 5 paralel refresh'inin kaybedenleri 401 alıp çıkışa düşer (N-67 + N-63). | Mobil tek-uçuş refresh ve "yalnız 400/401'de çıkış" **aynı görevde**: P15 T7. |
| S-2 | Mobil `scope` göndermeye başlayınca (N-01) aynı oluşuma ikinci istisna mobilden de üretilir → kalıcı 500 (N-05). | Benzersiz indeks + upsert **önce**: P16 T3 → P17 T1. |
| S-3 | Oturum anahtarı değişince (N-02) mevcut oturumların `OccurrenceStartUtc`'si boş, olası tekrarlar benzersiz indeksi kırar. | P17 T3 migration'ı: `PlannedStartAtUtc`'den geri doldurma + tekrar temizliği + sonra indeks. N-13 indeksi aynı migration. |
| S-4 | Concurrency token yaygınlaşırken migration squash yapılırsa çakışır. | Squash en son (K-29, P26 son görevi). Npgsql 9 `IsRowVersion()` → `xmin`, kolon migration'ı üretmez; üretirse görev durur ve incelenir. |
| S-5 | Sınıf kodu ve N-08 aynı paylaşılan parçaya ihtiyaç duyar. | `ISecureCodeGenerator` ve `ITeacherStudentAccess` **P15'te** kurulur (T1, T2); P22 yalnız `Class` kapsamını ekler. |
| S-6 | Kira düzeltilmeden yeni işler kiraya alınırsa onlar da 5 dk'da bir koşar. | N-41 düzeltmesi P15 T8'de, kirasız servislerin kiraya alınmasından **önce**. |
| S-7 | P12 P11'e bağlıydı (Reviews/Matching tüketicileri iskelet modüllere yazıyor); ama mağaza için şikayet şart. | P12 revize: Reviews/Matching tüketicileri **opsiyonel**, Messaging + Identity zorunlu; P12 yayın kapısından önce. |
| S-8 | P21'deki ekranlar P15 sonrası yeni kod biçimi ve P17 kapsam API'si olmadan yazılırsa iki kez yazılır. | P21, P15+P17+P18'den sonra. |

### 3.2 Yayın kapısı (K-27)

| Aşama | Ön koşul | Hedef kitle |
|-------|----------|-------------|
| **Kapalı beta** (Play internal testing / TestFlight) | Dalga 0 + Dalga 1 + P21 + P25 T1–T3 (imza, flavor, Sentry) + P26 T1–T2 (ücretli barındırma + yedek, Sentry) + Q6 beta listesi | 5–10 özel öğretmen + öğrencileri + velileri |
| **Mağaza yayını (public v1)** | Kapalı beta + **P12** (şikayet + admin) + **P25 tamamı** + P26 T3–T6 + **P27** (hukuk onayı) + **Dalga 3** (okul öğretmeni) | Herkes |

> **Q15 (öneri = tablo):** Public v1 okul özellikleriyle mi açılsın? Öneri **evet**: okul öğretmeni büyüme kanalı ve vizyonun parçası; kapalı beta beklemeden özel öğretmenle başlar.

---

## 4. Ortak teknik sözleşmeler (2026-09-02 §4'e ek)

- **Erişim kapsamı** — `Shared/Contracts/TeacherStudentAccessContract.cs`:
  ```csharp
  public enum TeacherAccessScope { None = 0, Private = 1, Class = 2 }
  public interface ITeacherStudentAccess
  {
      Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken ct);
  }
  ```
  `IStudentDirectory.IsLinkedTeacherOfStudentAsync` geriye uyumluluk için kalır ve `scope == Private` döner; yeni kod `GetScopeAsync` kullanır. `Class` değeri P22'de dolar; o zamana kadar asla dönmez.
- **Güvenli kod** — `Shared/Kernel/ISecureCodeGenerator.cs`: `string NewCode(int length = 8)`; impl `Shared/Infrastructure/SecureCodeGenerator.cs` (`RandomNumberGenerator.GetInt32`); alfabe ve normalize kuralı `Shared/Kernel` `SecureCode` sınıfında (`23456789ABCDEFGHJKMNPQRSTUVWXYZ`, 31 karakter). Kodlar büyük harfe normalize edilerek karşılaştırılır. Deneme kilidi arayüzü `Shared/Application/IInviteClaimAttemptLimiter.cs`.
- **Yerel takvim** — `Shared/Kernel/ILocalCalendar.cs`:
  ```csharp
  public interface ILocalCalendar
  {
      TimeZoneInfo Zone { get; }                       // Europe/Istanbul
      DateOnly Today();                                // IClock.UtcNow'un yerel tarihi
      DateOnly ToLocalDate(DateTime utc);
      DateTime ToLocal(DateTime utc);
      DateTime ToUtc(DateOnly date, TimeOnly time);
      (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly date);
      (DateTime StartUtc, DateTime EndUtc) WeekRangeUtc(DateOnly anyDayInWeek);   // Pazartesi başlar
      (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month);
      string FormatForUser(DateTime utc);              // "14 Kasım Cuma 10:00"
  }
  ```
  Zaman dilimi kimliği `"Europe/Istanbul"` (IANA); .NET 9 Windows'ta ICU ile çözer, Linux imajında `tzdata` gerekir — Dockerfile'da doğrulanır (P16 T1 adımı).
- **Sürüm (concurrency)** — `Shared/Kernel/IVersioned.cs`: `uint Version { get; }`. `ModuleDbContext` `IVersioned` uygulayan her entity için `Property(nameof(Version)).IsRowVersion()` uygular. `DbUpdateConcurrencyException` → `ExceptionHandlingMiddleware`'de `409 shared.concurrency_conflict`; outbox handler'larında yeniden deneme.
- **Idempotency** — mobil `ApiClient` `idempotent: true` işaretli isteklere `Idempotency-Key: <uuid v4>` ekler, aynı mantıksal işlemin yeniden denemesinde aynı anahtarı kullanır; sunucu anahtar + **gövde özeti** saklar (P18 T5).
- **Oluşum kimliği** — API'de `occurrenceStartAtUtc` (ISO 8601 UTC) daima serinin **orijinal** oluşum zamanıdır, ertelenmiş zaman değil.
- **Yeni modül M19 Classrooms** — şema `classrooms`, 2026-09-02 §4 "Yeni modül eklerken" adımları.
- **Branch:** plan başına `feat/pNN-<kisa-ad>`; `main`'e PR ile (P26 T5'ten itibaren zorunlu, öncesinde yerel merge kabul).
- **Commit kapsamı:** `git add` yalnız görevin `Files:` listesi + o görevde değişen dokümanlar; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar ve `pubspec.lock` sızmasın).

---

## 5. İzlenebilirlik — her bulgu nereye düştü

### 5.1 Mantık hataları (rapor §1)

| ID | Plan/Görev | | ID | Plan/Görev | | ID | Plan/Görev |
|----|-----------|-|----|-----------|-|----|-----------|
| N-01 | P17 T1 | | N-20 | P15 T1 | | N-44 | P19 T3 |
| N-02 | P17 T3 | | N-21 | P15 T2 | | N-45 | P19 T4 |
| N-03 | P17 T4 | | N-22 | P15 T3 | | N-46 | P15 T5 |
| N-04 | P16 T2 | | N-23 | P15 T3 | | N-47 | P19 T5 |
| N-05 | P16 T3 | | N-24 | P15 T6 | | N-48 | P17 T7 (iptal) + P19 T5 (trend) |
| N-06 | P17 T6 (+ metin P16 T5) | | N-25 | P28 T4 | | N-49 | P17 T7 |
| N-07 | P16 T4 | | N-26 | P15 T4 | | N-50 | P16 T5 |
| N-08 | P15 T2 | | N-27 | P15 T4 | | N-51 | P19 T6 |
| N-09 | P18 T1 + T2 (+ mobil T4) | | N-28 | P15 T3 | | N-52 | P19 T6 |
| N-10 | P18 T2 | | N-29 | P15 T7 | | N-53 | P19 T6 |
| N-11 | P16 T5 (gün/ay) + P18 T3 (para birimi) | | N-30 | P15 T7 | | N-54 | P19 T7 (+ indeks T6) |
| N-12 | P17 T3 | | N-31 | P15 T7 | | N-55 | P19 T6 + P18 T5 (idempotency) |
| N-13 | P17 T3 | | N-32 | P15 T7 (+ birincil veli indeksi T4, parola politikası P29) | | N-60 | P20 T1 |
| N-14 | P16 T3 | | N-40 | P19 T1 | | N-61 | P21 T1 |
| N-15 | P17 T5 | | N-41 | P15 T8 | | N-62 | P20 T2 |
| N-16 | P17 T5 (+ veli beyanı tekliği P18 T3) | | N-42 | P19 T2 | | N-63 | P15 T7 |
| | | | N-43 | P16 T5 | | N-64 | P18 T4 |
| N-65 | P18 T4 | | N-66 | P21 T7 | | N-67 | P15 T7 |
| N-68 | P20 T3 | | N-69 | P20 T3 | | N-70 | P20 T3 |
| N-71 | P20 T2 (rotalar) + T4 (`onMessage`) | | N-72 | P20 T1 + T5 (+ bildirim sayfalama P19 T7) | | §1.5 | P18 T1 |

**Rapor §1.6 (✅ ama bozuk):** A-15/M15-3 → N-29 · A-06 → N-31 · A-07 → N-41 + P15 T8 · M03-1 → N-27 + P21 T2 · D-08 → N-62 · D-14/E-07 → N-66 · D-20 → N-72 · B-06 → N-61 · K2 → N-12 · `AuthorizationCoverageTests` → **P15 T9** (çapraz-kiracı integration paketi).

### 5.2 Profesyonellik açıkları (rapor §3)

| ID | Plan | ID | Plan |
|----|------|----|------|
| P-01 | P15 T6 (+ blueprint P26 T1) | P-08 | §1 senaryo kuralı + P26 T7 (kapsam ölçümü/eşik) |
| P-02 | P26 T1 | P-09 | P26 T4 |
| P-03 | P26 T2 (backend) + P25 T5 (mobil) | P-10 | P20 T1 |
| P-04 | P25 T1–T4 | P-11 | P25 T7 (ARB süpürme) + T8 (erişilebilirlik) |
| P-05 | P27 (+ rıza ekranı P21 T7) | P-12 | P25 T2 |
| P-06 | P28 (K-25) | P-13 | P26 T9 (en son, K-29) |
| P-07 | P26 T3, T5 | P-14 | P26 T6 |

### 5.3 Özellikler (rapor §2)

| Rapor | Plan | Rapor | Plan |
|-------|------|-------|------|
| 2.1-1 Davet | P21 T1 | 2.3-1 Kodla katılım | P21 T3 |
| 2.1-2 Oluşum + iptal politikası | P17 | 2.3-2 Veli rotaları/ayarlar | P20 T2 + P21 T3 |
| 2.1-3 Tatil/erteleme | P21 T4 | 2.3-3 Ödedim | P21 T5 |
| 2.1-4 Ödedim onayı, ücret, arşiv | P21 T5 | 2.3-4 Sohbet başlatma + ad | P21 T8 |
| 2.1-5 Tahsilat hareketleri / paket | P18 T2 / P29 | 2.3-5 Girişsiz özet linki | P29 |
| 2.1-6 Duyuru + özet paylaşımı | P22 T6 (sınıf) / P29 (özel) | 2.3-6 Ödeme hatırlatma + ekstre | P29 |
| 2.1-7 iCal | P29 | 2.4-1 İlk admin | P12 Task 0 |
| 2.2-1 Velim | P21 T2 | 2.4-2 Admin arayüzü | P12 + P14 |
| 2.2-2 "Yakında" kutucukları | P21 T2 | 2.4-3 Askıya alma | P12 Task 1 |
| 2.2-3 Erteleme talebi | P21 T6 | 2.4-4 Şikayet kuyruğu | P12 Task 2–3 |
| 2.2-4 Yerel hatırlatıcı | P24 T4 | 2.4-5 Huni metrikleri | P26 T8 |
| 2.2-5 Görev/rozet/lig | P29 | 2.5 Sessiz saatler / saat dilimi | P24 T4 |
| 2.2-6 Seri dondurma | P28 T3 | 2.5 Öğrenci+veli ders hatırlatması | P17 T6 |
| 2.2-7 ÖSYM tahmini | P29 | 2.5 2FA / sosyal giriş / güvenlik e-postası / CAPTCHA | P29 |
| 2.2-8 Geç teslim / sürüm / mesaj eki | P19 T3 / P29 | 2.5 App Links / Universal Links | P25 T4 |

**Okul öğretmeni (vizyon §0):** P22 + P23 + P24.

### 5.4 Doküman çelişkileri (rapor §4)

| # | Nerede kapanır |
|---|----------------|
| 1 ILERLEME bayat | Bu program açılırken (2026-11-13) düzeltildi |
| 2–3 PRD §9.1–9.2 kapıları | P28 T3 |
| 4 Üyelik girişi (öğrenci/veli) | P21 T2/T3 |
| 5 `veli.md` ödedim / çalışma durumu | P21 T9 |
| 6 M11 🟢 ama FCM doğrulanmadı | P25 T5 (Q8) — o zamana kadar `m11` 🟡 notu |
| 7 `ogretmen.md` online link | P21 T9 |
| 8 Parite R-10/R-11 | P21 T9 |
| 9 Aydınlatma metni mobilde yok | P21 T7 + P27 |
| 10 P09 iyzico ↔ IAP | K-25 + P09 revizyon notu (bu program açılırken) |

---

## 6. Riskler

| Risk | Etki | Azaltma |
|------|------|---------|
| Dalga 1 geniş: 5 plan, ~34 görev | Ürün değeri geç görünür | Her plan kendi içinde yayınlanabilir; P17 ve P18 bitince öğretmen akışı kullanılabilir hale gelir |
| `Europe/Istanbul` Linux imajında çözülemez | Açılışta istisna | P16 T1'de Docker imajında doğrulama adımı + açılış guard'ı |
| Oluşum modeline geçiş mevcut veriyi bozar | Ücret/oturum kaybı | S-3 göç kuralı; staging'de demo veriyle göç tatbikatı (P17 T3) |
| Okul kapsamı büyür (yoklama, not defteri, e-Okul) | Program şişer | §7 kapsam dışı tablosu; P22 MVP sınırı |
| Hukuk teyidi (Q10) gecikir | Public v1 gecikir | Kapalı beta hukuk onayı beklemeden açık rıza taslağıyla; public v1 kapıda bekler |
| Hesaplar (Q7, Q8, Q11–Q14) açılmaz | Yayın kapısı kilitlenir | Kod "Logging/Disabled" sağlayıcıyla ilerler; guard üretimde engeller (P15 T6) |

---

## 7. Kapsam dışı (bilinçli)

| Madde | Neden | Ne zaman |
|-------|-------|----------|
| Yoklama, not defteri, karne, e-Okul/MEBBİS entegrasyonu | Okulun resmi sistemleriyle çakışır; veri sorumluluğu okula/MEB'e aittir | Talep gelirse ayrı ürün kararı |
| Sınıfta öğretmen–öğrenci birebir mesajlaşma | K-17 | v2 kararı |
| Kurum (dershane) paneli | PRD §10.4; tek öğretmen akışı oturmadan erken | P29 sonrası |
| Gerçek zamanlı sohbet (SignalR) | K-07 geçerli; poll + push yeterli | v2 |
| Telefon/SMS doğrulama (M01-5) | 2026-09-02 §5.1 geçerli | Karar gelince |
| Value object refactor (C-08) | 2026-09-02 §5.1 geçerli; yeni modüllerde (M19) baştan VO kullanılır | Ayrı teknik borç planı |

---

## 8. Açık sorular

| # | Soru | Bloklayan | Öneri / varsayılan |
|---|------|-----------|--------------------|
| Q1 | iyzico ticari hesabı | Artık yalnız özel ders tahsilatı (P29+) | Acil değil (K-25) |
| Q2 | Nihai fiyat listesi | P28 | Seed değerleri korunur |
| Q3 | AdMob + yerleşim politikası | P28 T5 (opsiyonel) | Lansmanda reklamsız |
| Q4 | S3 mi R2 mi | P26 T1 | R2 |
| Q6 | Beta öğretmen listesi | Kapalı beta | — |
| Q7 | SMTP sağlayıcı hesabı | Kapalı beta (P15 T6 guard'ı üretimi engeller) | — |
| Q8 | Firebase projesi | P25 T5 | — |
| Q9 | OTLP toplayıcı | P26 T2 | Grafana Cloud ücretsiz katman |
| K-11 | Angular 20 + signals | P14 | Onay |
| **Q10** | **Hukuk danışmanı**: KVKK metinleri, 18 yaş altı rıza modeli, okul öğretmeni kullanımı, devlet okulu öğretmeninin ücretli özel ders mevzuatı | P27, public v1 | — |
| **Q11** | **Apple Developer + Google Play Console** hesapları (bireysel mi şirket mi) | P25, P28 | Şirket hesabı (veri sorumlusu ile tutarlı) |
| **Q12** | **Barındırma**: Render ücretli + Frankfurt mı, başka sağlayıcı mı | P26 T1, kapalı beta | Render Starter + Frankfurt + managed Postgres |
| **Q13** | **Sentry** hesabı (veya alternatif) | P25 T5, P26 T2 | Sentry ücretsiz/takım planı, AB veri bölgesi |
| **Q14** | **Alan adı** (App Links, Universal Links, e-posta gönderen adres) | P25 T4, Q7 | — |
| **Q15** | **v1 kapsamı**: public v1 okul özellikleriyle mi? | Yayın kapısı | Evet (§3.2) |
| **Q16** | **RevenueCat** mı doğrudan StoreKit/Billing mi | P28 | RevenueCat (ücretsiz katman, sunucu webhook'u hazır) |

---

*Master Tasarım | Güncelleme: 2026-11-13*
