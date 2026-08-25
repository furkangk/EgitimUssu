---
title: "Özel Ders Platformu — PRD v2.1"
summary: "Ürün gereksinim dokümanı: vizyon, kullanıcı tipleri, M01-M18 modül listesi, 6 fazlı yol haritası, iş modeli, free/premium"
tags: [prd, urun, vizyon]
authority: product
updated: 2026-06-24
---

# EğitimÜssü — Özel Ders Yönetim ve Eşleştirme Platformu
## Ürün Gereksinim Dokümanı (PRD) 
> Uygulama adı: **EğitimÜssü** (kod adı: `EgitimUssu`).

> **📱 Mobil Öncelikli** — Web desteği sonraki aşamada  
> **👥 3 Kullanıcı Tipi** — Öğretmen · Öğrenci · Veli  

---

## 1. Ürün Özeti

Bu ürün, özel ders veren öğretmenlerin öğrenci bulmasını kolaylaştıran, ders sürecini yönetmesini sağlayan ve uzun süreli öğretmen-öğrenci ilişkisini platform içinde tutan bir sistemdir.

Temel yaklaşım iki parçadan oluşur:

- **Eşleştirme tarafı:** Öğretmen ve öğrenci birbirini bulabilir.
- **Yönetim tarafı:** Eşleşme oluştuktan sonra ders, ödev, not, veli takibi ve ödeme takibi platform üzerinden devam eder.

> **Ürünün temel amacı**, öğretmenin yalnızca öğrenci bulduğu bir pazar yeri olmak değil; derslerini düzenli yönettiği bir günlük çalışma aracı haline gelmesidir. Buna ek olarak öğrenci ve velinin de öğretmenden bağımsız olarak platforma dahil olabilmesi hedeflenmektedir.

---

## 2. Platform Stratejisi

### 2.1 Temel Strateji

- Öğretmeni sisteme çek — ders yönetimi için günlük kullandır.
- Öğrenciyi öğretmenden bağımsız çek — bireysel çalışma takibi ile.
- Veliyi platforma dahil et — çocuğunun gelişimini şeffaf şekilde görüntülesin.
- Eşleştirme sonrası öğretmene günlük operasyon aracı ver.
- Zamanla **abonelik (üyelik) ve reklam** ile gelir üret; ücretli üyelik reklamsız + sınırsız + ekstra özellik sunar (bkz. §9, M17).
- Rolleri **önce bireysel/ortak** kullanımla doldur, **eşleştirmeyi (ilan/keşif) en son** aç — böylece "ilk gün boş pazar yeri" sorunu (ilk kaydolan kimseyi göremez/mesaj alamaz) yaşanmaz.

> **Kritik fark:** Öğrenci ve veli, platforma öğretmenden **ÖNCE** girebilir. Bireysel çalışma takibi ile sisteme girip zamanla öğretmen arayışına geçebilirler. Bu, eşleştirme modülüne iki taraftan da kullanıcı akışı sağlar.

### 2.2 Platform Mimarisi

Platform mobil öncelikli olarak geliştirilecek, web desteği sonraki fazda eklenecektir.

| Katman         | Açıklama                                    | Öncelik       |
| -------------- | ------------------------------------------- | ------------- |
| Mobil Uygulama | iOS ve Android — birincil kullanıcı arayüzü | Faz 0–5       |
| Web Uygulaması | Tarayıcı erişimi — ikincil arayüz           | Sonraki aşama |
| API Katmanı    | Mobil ve web'i besleyen ortak backend       | Faz 0         |
| Admin Paneli   | İçerik ve kullanıcı yönetimi                | Faz 1+        |

---

## 3. Problem Tanımı

Türkiye'de özel ders öğretmenleri çoğu zaman şu sorunları yaşar:

- Öğrenci bulma süreci pahalı veya verimsiz olabilir.
- Öğrenciyle anlaşıldıktan sonra iletişim ve düzen takibi dağınık kalır.
- Dersler çoğunlukla takvim, Excel, not defteri veya mesajlaşma uygulamalarıyla yönetilir.
- Ödev, ders notu ve öğrenci gelişimi sistematik takip edilmez.
- Veli sürece şeffaf şekilde dahil edilemez.
- Ödeme çoğu zaman elden yapıldığı için manuel takip gerekir.

Öğrenci tarafında ise:

- Çalışma sürelerini takip etmek için ayrı uygulamalar kullanılır.
- Test performansı sistematik kayıt altına alınmaz.
- Haftalık/aylık ilerleme görünür değildir.
- Velinin çocuğunun çalışmasını takip etmesi için doğrudan bir araç yoktur.

> **Bu ürün tüm bu dağınık yapıyı tek yerde toplar.**

---

## 4. Hedef Kullanıcılar

### 4.1 Öğretmen
Özel ders veren kişi. Sistemin ana kullanıcısıdır.

Beklentileri:
- Yeni öğrenci bulmak
- Ders saatlerini takip etmek
- Öğrencilerle iletişimi düzenli tutmak
- Verdiği dersleri, ödevleri ve notları kayıt altına almak
- Ödeme durumunu görmek

### 4.2 Öğrenci
Ders alan kişi. Platforma iki farklı yoldan girebilir:
- Öğretmeni tarafından sisteme eklenerek
- Doğrudan kayıt olarak — bireysel çalışma takibi için

Beklentileri:
- Kendi çalışma sürelerini takip etmek
- Test ve sınav performansını kayıt altına almak
- Haftalık/aylık gelişimini görmek
- Öğretmeni varsa ders geçmişini ve ödevleri takip etmek

### 4.3 Veli
Özellikle küçük yaş gruplarında sürece dahil olur. İki farklı içerik görebilir:
- Çocuğunun bireysel çalışma verileri (öğretmenden bağımsız)
- Öğretmenle ilgili ders, ödev ve performans verileri (öğretmen bağlıysa)

---

## 5. İş Modeli

Ödeme sistemi üzerinden para tahsilatı yapılmaz. Gelir modeli şu şekilde planlanmıştır:

| Gelir Kalemi              | Hedef Kullanıcı    | Faz    |
| ------------------------- | ------------------ | ------ |
| Öğretmen aboneliği        | Öğretmen           | Faz 5  |
| Premium özellik paketi    | Öğretmen / Öğrenci | Faz 5  |
| Öğrenci bulma kredisi     | Öğretmen           | Faz 4+ |
| Gelişmiş raporlama paketi | Öğretmen           | Faz 5  |
| Veli paneli premium       | Veli               | Faz 5  |
| Öğrenci premium paketi    | Öğrenci            | Faz 5  |

> **Başlangıçta en önemli amaç**, öğretmeni ve öğrenciyi platformda uzun süre tutacak çekirdek kullanım değerini oluşturmaktır. Gelir ikincil önceliktir.

**İki gelir kaynağı (promp.txt):**
- **Reklam:** Ücretsiz kullanıcılar reklam görür; ücretli üyeler görmez.
- **Üyelik (abonelik):** Ücretli üyelik reklamsız + sınırsız + ekstra özellik sunar.

**Kullanıcı çekme kampanyaları:** İlk ay ücretsiz; **arkadaşını getir → 1 ay ücretsiz** (referans). Teknik tasarım: [`modules/m17_membership.md`](modules/m17_membership.md).

---

## 6. Modül Listesi

| #   | Modül Adı                                                                                                                                       | Birincil Kullanıcı        | Faz                     |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------- | ----------------------- |
| M01 | Kullanıcı ve Rol Yönetimi                                                                                                                       | Tümü                      | Faz 0                   |
| M02 | Öğretmen Profili                                                                                                                                | Öğretmen                  | Faz 1                   |
| M03 | Öğrenci Profili                                                                                                                                 | Öğretmen / Öğrenci        | Faz 1                   |
| M04 | Takvim ve Ders Planlama                                                                                                                         | Öğretmen                  | Faz 1                   |
| M05 | Ders Oturumu Yönetimi                                                                                                                           | Öğretmen                  | Faz 1                   |
| M06 | Not ve Ödev Yönetimi                                                                                                                            | Öğretmen / Öğrenci        | Faz 1                   |
| M07 | Manuel Ödeme Takibi                                                                                                                             | Öğretmen                  | Faz 1                   |
| M08 | Öğrenci Bireysel Çalışma                                                                                                                        | Öğrenci                   | Faz 2                   |
| M09 | Veli Paneli                                                                                                                                     | Veli                      | Faz 2                   |
| M10 | Öğrenci Gelişim Takibi                                                                                                                          | Öğretmen / Veli           | Faz 3                   |
| M11 | Bildirim ve Hatırlatma                                                                                                                          | Tümü                      | Faz 3                   |
| M12 | Eşleştirme, İlan ve Keşif (iki taraflı: öğretmen **sunduğu** ders ilanı / öğrenci **aradığı** ders ilanı; konum + yıldız + ücretli öne çıkarma) | Öğrenci / Öğretmen        | Faz 4                   |
| M13 | Puanlama ve Yorum                                                                                                                               | Öğrenci                   | Faz 4                   |
| M14 | Raporlama ve Analiz                                                                                                                             | Öğretmen                  | Faz 5                   |
| M15 | Ayarlar ve Güvenlik                                                                                                                             | Tümü                      | Faz 0+                  |
| M16 | Mesajlaşma (öğretmen↔öğrenci, öğretmen↔veli)                                                                                                    | Öğretmen / Öğrenci / Veli | Faz 2-3                 |
| M17 | Üyelik & Para Kazanma (abonelik + reklam + kampanya/referans)                                                                                   | Tümü                      | Faz 5 (temel: Faz 0+)   |
| M18 | Geri Bildirim & Şikayet (hata bildirimi + kötüye kullanım/şikayet + moderasyon)                                                                 | Tümü                      | Faz 1+ (şikayet: Faz 4) |

---

## 7. Modül Detayları

### M01 — Kullanıcı ve Rol Yönetimi
**Amaç:** Sistemdeki tüm kullanıcıları doğru yetkilerle yönetmek.

**Roller:**
- Öğretmen — ders yönetimi ve öğrenci takibi
- Öğrenci — bireysel çalışma + ders takibi
- Veli — görüntüleme ve bildirim
- Admin — sistem yönetimi

**Temel özellikler:**
- Kayıt olma, giriş yapma, şifre yenileme
- Rol bazlı ekranlar ve erişim kontrolü
- Profil doğrulama mekanizması
- Hesap kapatma ve veri silme

> **Not:** Öğrenci hem öğretmenden bağımsız kayıt olabilmeli hem de öğretmen tarafından sisteme eklenebilmelidir. Her iki giriş yolu da desteklenmelidir.

---

### M02 — Öğretmen Profili
**Amaç:** Öğretmenin kendisini ve sunduğu hizmeti tanıtması.

**Alanlar:** ad soyad, branş, şehir/ilçe, ders verme şekli (yüz yüze / online / her ikisi), deneyim yılı, eğitim seviyesi, fiyat bilgisi, uygun saatler, açıklama, profil fotoğrafı, sertifikalar ve deneyimler, doğrulama durumu.

---

### M03 — Öğrenci Profili
**Amaç:** Öğrenciyi ders takibi ve bireysel çalışma için sisteme almak.

**Alanlar:** ad soyad, sınıf seviyesi, ders aldığı branşlar, iletişim bilgisi, bağlı veli, notlar, aktif dersler, hedefler/seviye bilgisi.

> Bu profil öğretmen tarafından oluşturulabilir **VEYA** öğrenci doğrudan kayıt olabilir. Her iki durum da desteklenir.

---

### M04 — Takvim ve Ders Planlama
**Amaç:** Öğretmenin tüm derslerini tek yerde görmesi. Uygulamanın günlük kullanımını sağlayan ana ekrandır.

**Fonksiyonlar:**
- Ders ekleme, değiştirme, iptal etme
- Tekrar eden ders oluşturma
- Haftalık / aylık görünüm
- Ders çakışması kontrolü
- Hatırlatma oluşturma

---

### M05 — Ders Oturumu Yönetimi
**Amaç:** Her dersin kayıt olarak tutulması.

Her ders oturumu içerir: tarih, saat, süre, konu, işlenen içerik, ders durumu, öğretmen notu, öğrenci katılım durumu. Ders tamamlanınca öğretmen not girebilir ve ödev verebilir.

---

### M06 — Not ve Ödev Yönetimi
**Amaç:** Ders sonrası sürecin takip edilmesi.

**Fonksiyonlar:**
- Ders notu ekleme
- Ödev ekleme, son tarihi belirleme
- Ödev durumu takibi (tamamlandı / bekliyor)
- Dosya veya görsel ekleme

---

### M07 — Manuel Ödeme Takibi
**Amaç:** Para transferi yapmadan ödeme durumunu kaydetmek.

**Fonksiyonlar:**
- Ders ücreti tanımlama, aylık paket oluşturma
- Tahsil edildi / bekliyor / kısmi ödendi işaretleme
- Öğrenci bazlı bakiye gösterme
- Aylık gelir özeti, geciken ödemeleri listeleme

---

### M08 — Öğrenci Bireysel Çalışma Takibi

> **Bu modül platformun büyüme motorlarından biridir.** Öğrenci ve veliyi öğretmenden bağımsız platforma çeker.

#### Çalışma Seansı ve Sayaç
- Ders/konu seçip sayaç başlatma
- Mola desteği (mola süresi toplam süreye eklenmez)
- Seans bitince özet: süre, konu, kişisel notlar
- Geçmişe dönük seans listesi

#### Test ve Sınav Performansı
- Test girişi: toplam soru, doğru, yanlış, boş sayısı
- Konu bazlı net hesabı
- Zaman içinde aynı konudaki gelişim grafiği
- Hedef net / hedef puan tanımlama ve takibi

#### Haftalık ve Aylık Analiz
- Konuya göre çalışma süresi dağılımı
- En çok / en az çalışılan dersler
- Haftalık hedef vs. gerçekleşen karşılaştırması
- Aylık toplam çalışma özeti

#### Motivasyon Sistemi
- Streak (seri gün) takibi
- Günlük çalışma hedefi belirleme
- Tamamlanan görevleri işaretleme
- Kişisel rekor göstergeleri

#### Veli ile Paylaşım
- Çalışma verileri otomatik olarak veli paneline yansır
- Öğrenci isterse belirli verileri gizleyebilir (gizlilik kontrolü)
- Öğretmen bağlıysa veriler öğretmenle de paylaşılabilir

> Öğrenci bu modülü öğretmensiz de tam işlevsel olarak kullanabilir. Bu, platforma bağımsız bir kullanıcı kitlesi oluşturur ve eşleştirme modülüne hazır bir öğrenci havuzu sağlar.

---

### M09 — Veli Paneli
**Amaç:** Veliye şeffaf bilgi sunmak. İki farklı veri kaynağından beslenebilir:

| Veri Kaynağı | İçerik | Durum |
|-------------|--------|-------|
| Bireysel çalışma | Haftalık çalışma süreleri, konu dağılımı, test performansı, streak | Öğretmen gerekmez |
| Öğretmen bağlıysa | Son ders özeti, verilen ödevler, öğretmen notları, ödeme özeti | Öğretmen gerekir |

**Temel görünümler:**
- Çocuğun o hafta kaç saat çalıştığı
- Hangi derslere ne kadar zaman ayırdığı
- Test performansı özeti
- Yaklaşan dersler (öğretmen bağlıysa)
- Öğretmen mesajları (öğretmen bağlıysa)

---

### M10 — Öğrenci Gelişim Takibi
**Amaç:** Öğrencinin ilerlemesini sayısal ve görsel olarak göstermek.

**İzlenecek alanlar:**
- Konu kazanım durumu
- Deneme/test performansı zaman serisi
- Eksik ve güçlü konular
- Hedef puan / seviye
- Öğretmen değerlendirme notları

---

### M11 — Bildirim ve Hatırlatma Sistemi
**Amaç:** Kullanıcıyı uygulamaya geri getirmek. İlk fazda push notification ile başlanacaktır.

| Bildirim Türü | Hedef | Öncelik |
|-------------|-------|---------|
| Yaklaşan ders hatırlatması | Öğretmen / Öğrenci | Kritik |
| Ders sonrası not girme hatırlatması | Öğretmen | Yüksek |
| Ödev son tarihi yaklaşıyor | Öğrenci | Yüksek |
| Günlük çalışma hedefi hatırlatması | Öğrenci | Orta |
| Ödeme gecikmesi | Öğretmen | Yüksek |
| Haftalık özet | Tümü | Orta |
| WhatsApp/SMS (premium) | Tümü | Faz 5 |

---

### M12 — Eşleştirme ve Keşif
**Amaç:** Öğretmen ile öğrencinin birbirini bulmasını sağlamak.

**Fonksiyonlar:**
- Öğretmen listeleme ve arama
- Filtreleme: branş, şehir/ilçe, ücret, ders şekli, uygun saatler
- Öğretmen profil sayfası (puan, yorumlar, geçmiş)
- Mesaj gönderme / talep oluşturma
- Profil doğrulama rozeti
- Premium: profil öne çıkarma

> Bu modül Faz 4'te açılacaktır. O noktada hem öğretmen hem öğrenci tarafında aktif kullanıcı kitlesi oluşmuş olacak, puanlama için de veri birikmiş olacak.

---

### M13 — Öğretmen Puanlama ve Yorum Sistemi

> Bu modül eşleştirme sisteminin güven altyapısıdır. Sosyal kanıt olmadan öğretmen keşfi yeterince güven vermez.

**Temel Kurallar:**
- Yalnızca o öğretmenden ders almış öğrenciler yorum yapabilir (sahte yorum önleme)
- Ders tamamlandıktan sonra sistem otomatik yorum daveti gönderir
- Yorum metni + 1–5 yıldız genel puan

**Alt Kategori Puanlama:**
- Anlatım netliği
- Dakiklik ve güvenilirlik
- Sabır ve öğrenciye yaklaşım
- Ders hazırlığı

**Görünürlük ve Yönetim:**
- Öğretmen profil sayfasında ortalama puan + yorum sayısı
- Öğretmen yorumlara yanıt verebilir
- Olumsuz yorum gizlenemez, yalnızca yanıtlanabilir
- Doğrulanmış öğrenci rozeti (sisteme kayıtlı, ders kaydı olan)
- Şüpheli yorum bildirme ve admin moderasyon paneli

**Erken Açılış Stratejisi:**
> Puanlama sistemi Faz 4'te herkese açılır. Ancak Faz 1–2'de "öğretmene özel geri bildirim" olarak erken aktive edilebilir — öğrenci değerlendirme gönderir, yalnızca öğretmen görür. Bu sayede veri birikimi Faz 4'ten önce başlar.

---

### M14 — Raporlama ve Analiz
**Amaç:** Öğretmene ve öğrenciye işlerini daha iyi yönetme gücü vermek. Premium paketin güçlü bileşeni.

**Öğretmen raporları:**
- Aylık ders sayısı ve gelir özeti
- Aktif / pasif öğrenci sayısı
- Boş zaman analizi (ne zaman müsaitim)
- PDF öğrenci raporu oluşturma

**Öğrenci raporları:**
- Haftalık / aylık çalışma süresi analizi
- Konu bazlı performans değişimi
- Hedef vs. gerçekleşen karşılaştırması

---

### M16 — Mesajlaşma
**Amaç:** Platform içi iletişim — bugün dağınık (telefon/WhatsApp) yürüyen öğretmen-öğrenci/veli iletişimini sisteme taşımak.

- Mesajlaşma **yalnızca** şu çiftler arasında: **öğretmen ↔ öğrenci** ve **öğretmen ↔ veli**. (Öğrenci↔veli, öğrenci↔öğrenci, öğretmen↔öğretmen yoktur.)
- Birebir konuşma, okundu bilgisi, yeni mesaj bildirimi (M11), engelleme ve şikayet (M18).
- Detay: [`modules/m16_messaging.md`](modules/m16_messaging.md).

---

### M17 — Üyelik ve Para Kazanma
**Amaç:** Gelir modeli — **reklam + üyelik**. Platform parayı reklam ve ücretli üyelikten kazanır.

- **Ücretsiz üyelik:** reklam görür + özellik limitleri (örn. öğretmende öğrenci limiti).
- **Ücretli üyelik:** reklamsız + sınırlama yok + ekstra özellikler (rol bazlı — bkz. §9).
- **Kampanyalar:** ilk ay ücretsiz; **arkadaşını getir → 1 ay ücretsiz** (referans).
- Reklam yerleşimi istemci tarafında; ücretli kullanıcıda gizlenir. Detay: [`modules/m17_membership.md`](modules/m17_membership.md).

---

### M18 — Geri Bildirim ve Şikayet
**Amaç:** Güven ve ürün kalitesi.

- **Hata/geri bildirim:** kullanıcılar geliştirme bug'larını/önerilerini bildirir.
- **Bildirme ve şikayet:** kötüye kullanım — bir kullanıcıyı, yorumu veya mesajı şikayet etme → admin moderasyonu.
- M13 yorum şikayeti (`ReviewFlag`) ve M16 mesaj şikayeti ile ortak moderasyon kuyruğu. Detay: [`modules/m18_feedback.md`](modules/m18_feedback.md).

---

## 7.A promp.txt ile Gelen Yeni İş Kuralları (Modüllere Dağıtılmış)

> Bu kurallar v2.1 ile eklendi; teknik tasarımları ilgili modül dokümanlarındadır.

| Kural | Açıklama | Modül(ler) |
|-------|----------|-----------|
| **Online ders linki** | Online derste öğretmen bağlantı (MeetingUrl) girer; öğrenciler linkle katılır | [M04](modules/m04_scheduling.md) / [M05](modules/m05_lesson_sessions.md) |
| **Takvim tatilleri** | Takvimde tatil/izin/blackout günleri; dersler/ödevler/ödemeler tek takvimde | [M04](modules/m04_scheduling.md) |
| **Ders kaynağı (kaynak)** | Öğretmen ders notuna ek olarak **kaynak/materyal** paylaşır; öğrenci görür | [M06](modules/m06_assignments.md) |
| **Öğrenci ödev yükleme + veli bildirimi** | Öğrenci ödevini yükler; son tarihten önce yüklemezse **veliye bildirim** | [M06](modules/m06_assignments.md) + [M11](modules/m11_notifications.md) + [M09](modules/m09_parents.md) |
| **Çakışma önceliği** | Öğrencinin kendi planı ile özel ders çakışırsa **öncelik özel derste**, öğrenci uyarılır | [M04](modules/m04_scheduling.md) / [M08](modules/m08_study.md) |
| **Veli = gerçek kişi** | Öğrenci manuel olabilir; **veli yalnızca gerçek kayıtlı kullanıcı** olabilir | [M03](modules/m03_students.md) / [M09](modules/m09_parents.md) |
| **Ödeme veliyle paylaşım** | Ödeme bilgisi veliyle paylaşılabilir (bayrak) | [M07](modules/m07_payments.md) |
| **Başarım + seri + kronometre** | Öğrenciyi teşvik/elde tutma: streak, başarımlar, odak süresi sayacı | [M08](modules/m08_study.md) |
| **Konu bazlı gelişim** | Konu eksikleri, konu gelişimi, konu gelişim hedefleri | [M10](modules/m10_progress_tracking.md) |
| **İki taraflı ilan + öne çıkarma** | Öğretmen/öğrenci ilan verir; konum + yıldız + ücretli üyelik öne çıkarma | [M12](modules/m12_matching.md) |
| **Yıldız + yorum (güven)** | Puanlama/yorum sistemi; yalnız ders almış öğrenci yorumlar | [M13](modules/m13_reviews.md) |

---

## 8. Platform Fazları ve Yol Haritası

> **Teknoloji kararı verildi (v2.1):** Backend **.NET 9** modüler monolit; mobil **Flutter**; web **Angular** (sonraki aşama). Mobil öncelikli geliştirme, web sonraki aşamada.
>
> **Yeni modüllerin faz yerleşimi (v2.1):** **M16 Mesajlaşma** → Faz 2-3 (öğrenci/veli akışlarıyla birlikte); **M18 Geri Bildirim/hata** temel → Faz 1+, **şikayet/moderasyon** → Faz 4 (eşleştirme ile); **M17 Üyelik & Para Kazanma** → Faz 5 (temel altyapı Faz 0+'tan itibaren hazırlanır, çünkü "sistem baştan buna uygun olmalı").

### FAZ 0 — Temel & Altyapı *(Tahmini: 2–3 hafta)*

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 0.1 | Proje ve repo kurulumu (monorepo, CI/CD pipeline) | Kritik |
| 0.2 | Veritabanı şeması tasarımı (kullanıcılar, roller, ilişkiler) | Kritik |
| 0.3 | Kimlik doğrulama sistemi (kayıt, giriş, şifre sıfırlama) | Kritik |
| 0.4 | Rol bazlı yetkilendirme (öğretmen / öğrenci / veli / admin) | Kritik |
| 0.5 | Temel API mimarisi ve endpoint yapısı | Kritik |
| 0.6 | UI tasarım sistemi / component library kurulumu | Yüksek |
| 0.7 | Push notification altyapısı (FCM/APNs) | Yüksek |
| 0.8 | Ayarlar ve güvenlik ekranları (M15) | Orta |

### FAZ 1 — Öğretmen Çekirdeği (MVP) *(Tahmini: 4–6 hafta)*

Hedef: Öğretmenin uygulamayı her gün kullanmasını sağlayan minimum ürün. Gerçek kullanıcılarla test edilebilir seviye.

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 1.1 | Öğretmen profil oluşturma ve düzenleme (M02) | Kritik |
| 1.2 | Öğrenci ekleme — manuel (öğretmen tarafından) (M03) | Kritik |
| 1.3 | Takvim — ders ekleme, tekrar eden ders, haftalık görünüm (M04) | Kritik |
| 1.4 | Ders oturumu kaydı (konu, süre, notlar, katılım) (M05) | Kritik |
| 1.5 | Not ve ödev ekleme, ödev durumu takibi (M06) | Kritik |
| 1.6 | Manuel ödeme takibi — basit versiyon (M07) | Yüksek |
| 1.7 | Öğrenci giriş ekranı — ders geçmişi ve ödevleri görme | Yüksek |
| 1.8 | Yaklaşan ders push bildirimleri | Yüksek |
| 1.9 | Öğretmene özel geri bildirim (puanlama ön versiyonu) | Orta |

> **Faz 1 çıktısı:** Öğretmen kendi öğrencilerini ekleyip derslerini yönetebilir. 5–10 gerçek öğretmenle beta test yapılmalıdır.

### FAZ 2 — Öğrenci Bireysel Çalışma + Veli *(Tahmini: 4–5 hafta)*

Hedef: Öğrenci ve veli, platforma öğretmenden **BAĞIMSIZ** girebilmeli ve değer bulabilmelidir.

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 2.1 | Öğrenci doğrudan kayıt akışı (öğretmensiz) | Kritik |
| 2.2 | Çalışma sayacı — konu seçimi, başlat/durdur/bitir, mola desteği (M08) | Kritik |
| 2.3 | Çalışma seansı kaydı ve geçmiş listesi (M08) | Kritik |
| 2.4 | Haftalık çalışma süresi özeti (M08) | Kritik |
| 2.5 | Test/sınav girişi — doğru, yanlış, boş, net hesabı (M08) | Yüksek |
| 2.6 | Konu bazlı test performansı takibi (M08) | Yüksek |
| 2.7 | Streak (seri gün) ve günlük hedef sistemi (M08) | Yüksek |
| 2.8 | Veli profili ve öğrenciyle bağlantı kurma (M09) | Kritik |
| 2.9 | Veli paneli — bireysel çalışma verileri (öğretmensiz) (M09) | Kritik |
| 2.10 | Veli bildirim tercihleri ve izin bazlı görünürlük (M09) | Yüksek |

> **Faz 2 çıktısı:** Öğrenci kendi çalışmalarını takip eder, veli çocuğunun gelişimini görür. Öğretmen gerekmez.

### FAZ 3 — Gelişim Takibi & Bildirimler *(Tahmini: 3–4 hafta)*

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 3.1 | Veli paneli — öğretmen verilerini de kapsayan entegre görünüm (M09) | Kritik |
| 3.2 | Öğrenci gelişim takibi — konu kazanımı, eksikler, güçlü alanlar (M10) | Yüksek |
| 3.3 | Öğrenci performans grafikleri (zaman serisi) (M10) | Yüksek |
| 3.4 | Hedef puan / hedef net belirleme ve takibi (M08 + M10) | Yüksek |
| 3.5 | Bildirim sistemi genişletme — ödev, ödeme, günlük çalışma (M11) | Yüksek |
| 3.6 | Haftalık özet bildirimi (M11) | Orta |
| 3.7 | Gelişmiş ödeme takibi — aylık paket, geciken ödemeler (M07) | Orta |

### FAZ 4 — Eşleştirme & Puanlama *(Tahmini: 4–5 hafta)*

Hedef: Platforma dışarıdan öğretmen ve öğrenci çeken büyüme motoru.

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 4.1 | Öğretmen listeleme ve arama sayfası (M12) | Kritik |
| 4.2 | Filtreleme — branş, şehir, ücret, ders şekli, uygun saatler (M12) | Kritik |
| 4.3 | Öğretmen profil sayfası — herkese açık görünüm (M12) | Kritik |
| 4.4 | Talep / mesaj gönderme akışı (M12) | Yüksek |
| 4.5 | Profil doğrulama rozeti (M12) | Yüksek |
| 4.6 | Öğretmen puanlama — yıldız + alt kategoriler (M13) | Kritik |
| 4.7 | Yorum yazma ve görüntüleme (M13) | Kritik |
| 4.8 | Öğretmenin yorumlara yanıt verebilmesi (M13) | Yüksek |
| 4.9 | Doğrulanmış öğrenci rozeti ve şüpheli yorum bildirme (M13) | Yüksek |
| 4.10 | Admin moderasyon paneli — yorum yönetimi (M13) | Orta |

### FAZ 5 — Premium & Analitik *(Tahmini: 3–4 hafta)*

| # | İş Kalemi | Öncelik |
|---|-----------|---------|
| 5.1 | Abonelik altyapısı ve ödeme entegrasyonu | Kritik |
| 5.2 | Free/Premium kısıtlamalarının uygulanması | Kritik |
| 5.3 | Profil öne çıkarma (premium) (M12) | Yüksek |
| 5.4 | Öğrenci limiti kaldırma (premium) (M01) | Yüksek |
| 5.5 | Aylık gelir özeti ve kazanç analizi (M14) | Yüksek |
| 5.6 | PDF öğrenci raporu oluşturma (M14) | Yüksek |
| 5.7 | Boş zaman analizi (M14) | Orta |
| 5.8 | Öğrenci haftalık / aylık analiz premium (M08 + M14) | Yüksek |
| 5.9 | WhatsApp/SMS hatırlatma entegrasyonu (M11) | Orta |
| 5.10 | Veli premium paketi (M09) | Orta |

---

## 9. Free vs. Premium Özellik Karşılaştırması

### 9.1 Öğretmen Paketi

| Özellik | Free | Premium |
|---------|------|---------|
| Öğrenci ekleme | ✅ (maks. 5–10) | ✅ Sınırsız |
| Ders planlama (takvim) | ✅ | ✅ |
| Ders geçmişi | ✅ | ✅ |
| Not & ödev girme | ✅ | ✅ |
| Temel veli görünümü | ✅ Sınırlı | ✅ Detaylı |
| Manuel ödeme takibi | ✅ Basit | ✅ Gelişmiş |
| Aylık kazanç toplamı | ❌ | ✅ |
| Geciken ödeme listesi | ❌ | ✅ |
| Otomatik ödeme hesaplama | ❌ | ✅ |
| Ders & ödev hatırlatmaları | ❌ | ✅ |
| WhatsApp/SMS hatırlatma | ❌ | ✅ |
| PDF öğrenci raporu | ❌ | ✅ |
| Öğrenci performans analizi | ❌ | ✅ |
| Gelir analizi | ❌ | ✅ |
| Boş zaman analizi | ❌ | ✅ |
| Profil öne çıkarma | ❌ | ✅ |

### 9.2 Öğrenci Paketi

| Özellik | Free | Premium |
|---------|------|---------|
| Çalışma sayacı | ✅ Basit | ✅ Gelişmiş |
| Ders programı oluşturma | ✅ | ✅ |
| Günlük çalışma süresi | ✅ | ✅ |
| Test / sınav girişi | ✅ | ✅ |
| Geçmiş çalışma kayıtları | ❌ | ✅ |
| Haftalık / aylık analiz | ❌ | ✅ |
| Hedef belirleme | ❌ | ✅ |
| Streak (seri gün) | ❌ | ✅ |
| Motivasyon sistemi | ❌ | ✅ |
| Öğretmenle detaylı veri paylaşımı | ✅ Basit | ✅ Detaylı |

### 9.3 Veli Paketi

| Özellik | Free | Premium |
|---------|------|---------|
| Çocuğun haftalık çalışma süresi | ✅ | ✅ |
| Son ders özeti | ✅ | ✅ |
| Ödev görüntüleme | ✅ | ✅ |
| Yaklaşan dersler | ✅ | ✅ |
| Detaylı gelişim grafikleri | ❌ | ✅ |
| Haftalık rapor | ❌ | ✅ |
| Çalışma süresi geçmişi | ❌ | ✅ |
| Bildirimler | ❌ | ✅ |

---

### 9.4 Reklam ve Kampanyalar (Tüm Roller)

| Özellik | Free | Premium |
|---------|------|---------|
| Reklam gösterimi | ✅ Görür | ❌ Görmez |
| Özellik limitleri (öğrenci sayısı, geçmiş, analiz vb.) | ✅ Sınırlı | ❌ Sınırsız |
| İlk ay ücretsiz (yeni kullanıcı) | ✅ | ✅ |
| Arkadaşını getir → 1 ay ücretsiz (referans) | ✅ | ✅ |
| İlanda öne çıkarma (öğretmen) | ❌ | ✅ |

> Detaylı entitlement/kampanya/reklam tasarımı: [`modules/m17_membership.md`](modules/m17_membership.md).

---

## 10. Stratejik Notlar ve Riskler

### 10.1 Öncelik Sırası

> Faz 1'i bitirip 5–10 gerçek öğretmenle beta test yapın. Eşleştirme modülüne (Faz 4) yalnızca Faz 1–2'nin gerçek kullanıcılarda çalıştığı doğrulandıktan sonra geçin. **Sık yapılan hata:** pazar yeri fonksiyonunu erken açmak, yönetim tarafını yarım bırakmaktır.

### 10.2 Büyüme Stratejisi

- **Öğretmen** — ders yönetimiyle çekilir, günlük kullanımla kalır.
- **Öğrenci** — bireysel çalışma takibiyle çekilir, öğretmen eşleştirmesiyle kalır.
- **Veli** — çocuğun gelişimini görmek için gelir, bildirimlerle aktif kalır.
- **Eşleştirme** — her üç taraf da hazır olduğunda açılır; iki taraflı pazar sorununu hafifletir.

### 10.3 Teknik Riskler

- WhatsApp/SMS entegrasyonu (Faz 5) karmaşık — push notification ile başlanmalı.
- Mobil + web aynı anda geliştirme yaygın bir hata — mobil önce, web sonra.
- Teknoloji kararı (Flutter vs. React Native vs. native) mimariyi etkiler, erken verilmesi önerilir.
- Ödeme altyapısı (abonelik) için KVKK uyumluluğu ve uygulama mağazası kuralları gözetilmelidir.

### 10.4 Gelecek Aşamalar (Yol Haritası Dışı)

- **Web uygulaması** — mobil hazır olunca paralel geliştirme.
- **AI özellikler** — çalışma planı önerisi, konu tespiti, öğrenci profil analizi.
- **Grup dersleri** — birden fazla öğrenciye aynı anda ders verme desteği.
- **Kurumsal panel** — dershane ve kurs merkezleri için çok öğretmen yönetimi.
- **Ödeme altyapısı** — ilerleyen fazda gerçek tahsilat entegrasyonu.

---

*EğitimÜssü — Özel Ders Yönetim ve Eşleştirme Platformu — Ürün Gereksinim Dokümanı | v2.1 (2026-06-24)*
