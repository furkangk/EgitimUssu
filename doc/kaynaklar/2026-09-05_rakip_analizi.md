---
title: "Rakip Analizi (2026-09)"
summary: "Doping Hafıza, Pakodemy, Net Up, DUS Club, VeliCep ve özel ders operasyon/pazar yeri kuşağının (Tilki, Öğretsen, Özel Ders Alanı, Öğretmenim.app, Özel Ders Takip Sistemi) mağaza verisiyle doğrulanmış karşılaştırması: ürün, fiyat, konumlandırma, mağaza kuralları ve EğitimÜssü'ye çıkarımları"
tags: [kaynak, research, rakip, pazar, konumlandirma, fiyatlandirma]
authority: reference
subtype: research
source: raw/2026-09-05_rakip_magaza_verisi.md
updated: 2026-09-05
---

# 🔭 EğitimÜssü — Rakip Analizi (2026-09-05)

> **Ne için okunur:** EğitimÜssü'nün hangi pazarda kiminle yarıştığını, rakiplerin ne sattığını, nasıl
> fiyatladığını ve hangi ürün/gelir kararlarının dışarıda nasıl çözüldüğünü gösterir.
> **Kanonik gerçeği ezmez** (bkz. [`../INDEX.md`](../INDEX.md) §0); ürün kararları için esas doküman
> [`../ozel_ders_platformu_PRD.md`](../ozel_ders_platformu_PRD.md)'dir.
>
> **Bu analizin uygulama tarafı ayrı dokümanda:**
> [`../denetim/2026-09-05_rakip_paritesi_analizi.md`](../denetim/2026-09-05_rakip_paritesi_analizi.md) —
> parite (R-01…R-14) ve üstünlük (Ü-01…Ü-10) maddeleri, her biri kod gerçeğine karşı doğrulanmış,
> çözüm taslağı + kabul kriteri + risk ile.
>
> **Veri kaynağı ve tarih:** Tüm mağaza rakamları 2026-09-05'te Apple iTunes Lookup API (TR mağazası) ve
> Google Play / APKCombo listelerinden alındı; ham döküm [`../raw/2026-09-05_rakip_magaza_verisi.md`](../raw/2026-09-05_rakip_magaza_verisi.md).
> Play indirme sayıları **kova değeridir** (`50+`, `1.000+` …), kesin sayı değildir. Şirketlerin kendi
> sitelerindeki büyüme rakamları "**iddia**" olarak işaretlendi ve bağımsız doğrulanmadı.

---

## 0. Yönetici özeti

1. **Talep edilen altı isimden beşi doğrulandı**, `northcreative` Türkiye eğitim pazarında bulunamadı (§8).
2. **Bu beş ürünün hiçbiri EğitimÜssü'nün doğrudan rakibi değil.** Liste üç ayrı pazarı karıştırıyor;
   EğitimÜssü yalnız birinde (§2). Gerçek rakip kuşağı listede yoktu — analize eklendi (§5).
3. **Doğrudan rakip: Tilki** (öğretmen SaaS, 120 ₺/aktif öğrenci/ay). Öğrenci uygulaması ve eşleştirmesi yok.
4. **Saf eşleştirme ölü bir model:** Öğretsen dört yılda 41 değerlendirme, Öğretmenim.app 2022'den beri
   güncellenmemiş. PRD §10.1'in "eşleştirmeyi en son aç" kararı dışarıdan doğrulanıyor.
5. **İncelenen 11 üründen hiçbiri reklam göstermiyor.** Gelir aboneliktir.
6. **Ödeme yolu ikiye ayrılıyor:** premium abonelik mağaza içi satın alma (Apple 3.1.1), bire-bir ders
   ücreti harici ödeme (Apple 3.1.3(d), kural metninde örnek olarak *"tutoring students"* geçiyor) — §7.
7. **Öğrenci tarafındaki mekaniklerin çoğu EğitimÜssü'de zaten var** (streak, 10 rozet, deneme, net trendi).
   Eksik olan sosyal katman ve dış kanal — ayrıntı parite dokümanında.

---

## 1. Talep edilen listede kim kimdir

| # | Talep edilen ad | Gerçek ürün | Yayıncı | Ne yapar |
|---|-----------------|-------------|---------|----------|
| 1 | `northcreative` | **❓ Doğrulanamadı** | — | Türkiye eğitim pazarında eşleşme bulunamadı (§8) |
| 2 | `netup lgs hazırlık` | **Net Up: LGS Hazırlık** (`com.netup.app`) | Net Up Eğitim Uygulamaları (iOS satıcısı: Yusuf Öksüz) | Oyunlaştırılmış LGS mikro-öğrenme (6–8. sınıf) |
| 3 | `velicepapp` | **VeliCep: Okul Bilgi Sistemi** (`com.tmgames.velicepapp`) | TMGAMES Oyun Yazılım Tekn. ve Paz. Ltd. Şti. | Anaokulu ↔ veli iletişimi (kuruma satılır) |
| 4 | `pakodemy` | **Pakodemy: KPSS YKS LGS** (`com.tosanalytics.pakodemy`) | TOS Analytics Yapay Zeka A.Ş. | Çok-yayınevili soru bankası + hazırlık platformu |
| 5 | `dopinghafıza` | **Doping Hafıza** (`com.dopinghafiza.mobile.app`) | Doping Bilişim Teknolojileri A.Ş. | Türkiye'nin en büyük dijital hazırlık platformu |
| 6 | `dusclupapp` | **DUS Club: DUS'a Hazırlık** (`com.denthall.dusclub`) | Denthall Club (iOS satıcısı: Oğuzhan Arslan) | DUS adayları için çalışma yoldaşı + kulüp |

### 1.1 Ölçek ve olgunluk (mağaza verisi, 2026-09-05)

| Ürün | Play indirme | Play puanı | App Store (TR) puanı | İlk yayın | Son sürüm | Boyut |
|------|--------------|-----------|----------------------|-----------|-----------|-------|
| **Doping Hafıza** | **1.000.000+** | 4,2 (28.386) | 4,56 (15.342) | 2021-02-26 | 2026-08-24 (v3.0.22) | 129 MB |
| **Pakodemy** | *(sayfa alınamadı)* | — | 4,62 (7.897) | 2019-12-28 | 2026-08-28 | — |
| **Net Up** | 1.000+ | — | 4,85 (13) | **2026-05-27** | 2026-08-31 (v1.4.3) | 59,6 MB |
| **VeliCep** | 100+ | — | 5,0 (11) | **2026-03-08** | 2026-08-25 (v1.3.0) | — |
| **DUS Club** | 50+ | — | 4,77 (22) | **2026-03-03** | 2026-08-21 (v2.1.5) | 65 MB |

> **Okuma:** Liste **iki ayrı ligi** karıştırıyor.
> **Kurumsal ölçek:** Doping (1M+ indirme, A.Ş., çağrı merkezi) ve Pakodemy (2019'dan beri, A.Ş.,
> 20+ yayınevi anlaşması) — bunlar pazarın gerilim çizgisini belirler: aile bütçesi ve beklenti seviyesi.
> **Yeni ve küçük:** Net Up, VeliCep ve DUS Club **2026'da doğmuş**, tek kişi/küçük ekip ürünleri —
> EğitimÜssü'nün gerçek karşılaştırma ölçütü bunlar, çünkü aynı kaynak ölçeğinde çalışıyorlar.
> Üçünün de son sürümü **Ağustos 2026** — hepsi aktif geliştiriliyor.

---

## 2. Kritik tespit: bunlar **üç ayrı pazar**, ikisi bizim pazarımız değil

| Pazar | Kim satar | Kime satar | Ürün neyi çözer | Gelir modeli | Listedeki oyuncular |
|-------|-----------|-----------|------------------|--------------|---------------------|
| **A. İçerik / sınav hazırlık** | Yayıncı-platform | Öğrenci + aile | "Çalışacak materyalim yok" | Yıllık paket / abonelik | Doping Hafıza, Pakodemy, Net Up, DUS Club |
| **B. Kurum ↔ veli iletişimi** | SaaS | **Okul yönetimi** (B2B2C) | "Veli ne olduğunu bilmiyor" | Kurum lisansı | VeliCep |
| **C. Özel ders operasyonu + eşleştirme** | SaaS / pazar yeri | Öğretmen (ve öğrenci/veli) | "Dersimi Excel + WhatsApp ile yönetiyorum" | Öğretmenden abonelik | **← EğitimÜssü burada** · Tilki, Özel Ders Alanı, Öğretsen, Öğretmenim.app, Özel Ders Takip Sistemi |

**Dolayısıyla:** Talep edilen listedeki 5 üründen **hiçbiri EğitimÜssü'nün doğrudan rakibi değil.**
Yine de üçü de bizi etkiliyor:

1. **A grubu = cüzdan rakibi.** Pakodemy YKS paketi 10.999 ₺/yıl; Doping paketleri üçüncü taraf kaynaklara
   göre 2.799–37.900 ₺. Bu para **ailenin aynı eğitim bütçesinden** çıkıyor. Aile "Doping aldım, ayrıca
   özel ders de mi?" diye soruyor — ve özel ders zaten aylık binlerce lira.
2. **A grubu = alışkanlık rakibi.** Streak, lig ve akşam bildirimleriyle öğrencinin **aynı günlük zaman
   dilimini** istiyorlar — M08 çalışma takibinin istediği dilimi. Ekran zamanı sıfır toplamlı.
3. **B grubu = beklenti çıpası.** Veli anaokulundan beri günlük bilgilendirme + fotoğraf + anlık mesaj
   görüyor. M09 veli paneli bu çıpaya göre değerlendirilecek — "az" değil, "VeliCep kadar değil" denecek.

**Gerçek rakip kuşağını (C) bu analize ekledim** (§5) — istenen listede yoktu ama karşılaştırmayı
anlamlı kılan grup bu.

---

## 3. A grubu — içerik / sınav hazırlık platformları

### 3.1 Doping Hafıza — pazar lideri, yüksek dokunuşlu satış

| | |
|---|---|
| **Şirket** | Doping Bilişim Teknolojileri A.Ş. |
| **Ölçek** | Play 1.000.000+ indirme · Play 4,2 (28.386 yorum) · App Store TR 4,56 (15.342) |
| **Yaş** | iOS uygulaması 2021-02'den beri; şirket "16 yıl" iddiasında; Webrazzi 2021: 120 bin+ öğrenci (**iddia**) |
| **Fiyat** | **Sitede yayınlanmıyor** — yalnız "Peşin fiyatına 12 taksit" + form / WhatsApp (0533 236 2005) / 444 59 63 |

**Kapsam — 13 ayrı paket ailesi:** YKS 2027 (TYT + AYT: Sayısal / Eşit Ağırlık / Sözel / Tüm Dersler),
LGS 2027, 9-11. sınıf, 5-7. sınıf, 1-4. sınıf, KPSS (Lisans / Ön Lisans / Ortaöğretim), AGS, DGS 2027,
ALES, YDT İngilizce, Almanca, Kariyer Yolum, Okul Öncesi.

**Ürün:**
- "Dopi" — yapay zekâ destekli kişiselleştirilmiş öğrenme patikası
- Günlük çalışma süresine göre **otomatik ders programı** (en çok kullanılan özellik olarak anlatılıyor)
- Her soruya **çözüm videosu**; video ardı sorularla pekiştirme; video izlerken not alma
- Çok formatlı anlatım: hafıza teknikleri, akıl haritaları, infografikler, "refleksler", "serüvenler"
- Akıllı testler (eksik kazanıma yönelik), deneme sınavları ve yönlendirme
- Birebir rehberlik/koçluk + branş uzmanıyla **ekspres birebir görüşme**
- "Her Güne 1 Doping" — günlük motivasyon

**Veli tarafı:** Ayrı bir **veli başarı takip uygulaması**, düzenli gelişim raporu, veli bilgilendirme
sistemi, rehberlik uzmanıyla birebir görüşme, sınav sürecinde profesyonel destek.

**Gelir modeli — telesatış:** Fiyat gizli, form doldurtup arıyorlar. Bu, dershane satışının dijitali:
yüksek bilet, insan kapanışı, 12 taksit. Üçüncü taraf kaynaklar 2.799 ₺ (TYT+AYT Eşit Ağırlık) ile
37.900 ₺ (aylık 3.158 ₺ × 12) arasında rakamlar veriyor — **üreticinin resmî beyanı değil**.

**EğitimÜssü için ne öğretiyor:**
1. Türkiye'de eğitim ürününde **yüksek fiyat + insan satış** hâlâ çalışıyor; her şey self-servis olmak zorunda değil.
2. Pazar lideri veliye **ayrı bir uygulama yazacak kadar** veli ilgisine yatırım yapıyor — M09'un stratejik
   değerinin dış kanıtı.
3. Play puanı (4,2) iOS puanından (4,56) belirgin düşük ve yorum sayısı iki katı — büyük ölçekte
   memnuniyetsizlik görünür hâle geliyor. Yüksek bilet + telesatış modelinin bilinen bedeli.

### 3.2 Pakodemy — agregatör: içerik üretmiyor, içerik *toplayıp* kişiselleştiriyor

| | |
|---|---|
| **Şirket** | TOS Analytics Yapay Zeka A.Ş. |
| **Ölçek** | App Store TR 4,62 (7.897) · Site iddiası: 2M+ aktif öğrenci, 4,7/5 (23.148 yorum), YKS ilk 100'de 42 öğrenci |
| **Yaş** | iOS 2019-12'den beri — listedeki en eski ürün |
| **Platform** | iOS, Android, **Huawei AppGallery**, tarayıcı |

**İçerik — asıl ayırt edici:** 20'den fazla **yayınevinden** derlenmiş 400–500 bin soru. Uygulama
açıklamasında yayınevleri tek tek sayılıyor: Test Okul, VİP, M+, Anka, Delta Kültür, Köşebilgi, Yanıt,
Nego, Strateji, Cevap, 4K, Yeniyorum, Teknotest, Şah-Mat, Açı, Polimat, Pano, Aday, Paylaşım, Eğitim Vadisi,
Taktik Bende, Karat, Röntgen, Step Set, Adrenalin, En, Spoiler, Buga… + ÖSYM ve ÖSS çıkmış soruları
(son 20 yıl). Ayrıca 6.000+ kazanım videosu (YKS) / 1.000+ (LGS) — **iddia**.

**Öğrenci seçiyor:** "Sana göre bir yayınevinin Türkçe soruları, diğerinin Matematik soruları mı daha iyi?
İstediğin yayınevini seç, derslerin soru sıklığını belirle." Yani kişiselleştirme yalnız algoritmada değil,
**kaynak seçiminde**.

**Diğer ürün özellikleri:** Seviyeye göre zorluk kalibrasyonu; haftalık **10 özel görev** seçme;
canlı dersler; PakoLAB (deneysel öğrenme); **düello** (öğrenciler arası soru yarışı); Türkiye geneli
denemeler + detaylı analiz; yanlışlara video çözüm; tercih simülatörü; psikolog ve dereceli öğrencilerden
koçluk; **öğretmenin ödev atayabilmesi**.

**En akıllı kancası:** *"Uygulama dışında çalıştıklarını programa ekle"* — evde, okulda, dershanede,
kütüphanede geçen süreyi, kullanılan kaynağı ve girilen denemeyi içeri alıyor. Ürün, dışındaki hayatı
da kendi verisine dönüştürüyor.

**Fiyat:**

| Paket | Liste | İndirimli | Taksit |
|-------|-------|-----------|--------|
| YKS 2027 Premium | 20.999 ₺ | **10.999 ₺** | 916 ₺ × 12 |
| LGS 2027 Premium | 20.999 ₺ | **10.999 ₺** | 916 ₺ × 12 |
| KPSS 2026 Premium | 14.999 ₺ | **7.999 ₺** | 666 ₺/ay |

%47 indirim kampanyası, **7 gün iade garantisi**, 7/24 WhatsApp destek. Doping'in aksine fiyat **açık**.

**EğitimÜssü için ne öğretiyor:**
1. "Dışarıda çalıştığını da kaydet" deseni M08'de zaten var (manuel süre girişi) — **doğru yoldayız**.
2. Öğretmen → ödev akışının bir **içerik** platformuna bile eklendiğini gösteriyor: bizde çekirdek olan
   şey onlarda eklenti. Bu, ödev akışının değerini doğruluyor.
3. İçerik **satın alınabilir** (yayınevi anlaşması), üretilmesi şart değil — ama yine de 20+ anlaşma
   demek, kurumsal satın alma ekibi demek. Bizim ölçeğimizde yol bu değil.

### 3.3 Net Up — listedeki en öğretici ürün

| | |
|---|---|
| **Yayıncı** | Net Up Eğitim Uygulamaları (Play) / Yusuf Öksüz (App Store) — **tek kişi/küçük ekip** |
| **Ölçek** | Play 1.000+ · App Store TR 4,85 (13 değerlendirme) |
| **Yaş** | **2026-05-27** — dört aylık ürün |
| **Hedef** | 6, 7 ve 8. sınıf (11–14 yaş) |

**Tez:** *"Günde 10 dakika yeter."* Uzun çalışma yerine **alışkanlık**. Açıklamada gerekçesi de var:
"Bilim, alışkanlıkla, küçük ve düzenli tekrarla öğrenildiğini söylüyor."

**Mekanikler:** Maskot **Neto** (yol arkadaşı olarak konumlanıyor — "soyut bir uygulama değil, sevdiğin
bir karakter"), günlük seri sayacı, **haftalık lig tablosu**, ödül sandığı, rozet, XP, avatar özelleştirme,
arkadaşla düello.

**Pedagoji:** Yanlış yapılan soru akıllıca tekrar sorulur; D2/D3 zorluk seviyeleri; geometri sorularında
(üçgen, dikdörtgen, çember, prizma, koni, küre) **otomatik görsel üretimi**; akşam 19:00 hatırlatıcı +
20:00 günlük seri uyarısı.

**Veli:** Haftalık özet rapor — kaç soru çözüldü, hangi konuda zorlanıldı, günlük seri durumu —
**WhatsApp / e-posta / SMS ile paylaşılabilir**. Vurgu: "gerçek çalışma dakikası, ekran süresi değil."

**Fiyat ve paketleme:**
- **Ücretsiz katman gerçekten kullanılabilir:** günlük görev, lig, binlerce soru, seri ve rozet.
- **Pro: 99,99 ₺/ay veya 499,99 ₺/yıl** — yıllık, aylığın **5 katı** (yani 7 ay bedava; agresif yıllık indirim).
- Pro'nun sattığı: **sınırsız can** (yanlış cevap stresi yok), D2/D3 zorluk, detaylı konu analizi,
  sınırsız alıştırma, "Veli raporu Pro".
- Lansman: **ilk hafta tüm Pro özellikleri ücretsiz**.

**Konumlandırma cümlesi:** *"Aylık 99,99 TL — yıllık toplam, ortalama bir dershane ücretinin yaklaşık
100'de biri. Sonuçlar dershane gibi."* Sitede dershane maliyeti 70–80 bin ₺/yıl olarak veriliyor.

**Hedef kitle tanımı (açıklamadan, aynen):** 6/7/8. sınıf öğrencileri · LGS'ye 1 yıl ve daha kısa süre
kalanlar · **dershane masrafından kaçınmak isteyen aileler** · çocuğunun her gün ne kadar çalıştığını
takip etmek isteyen veliler · eğlenerek öğrenmeyi sevenler.

**⚠️ Tutarsızlık:** Site "8.500+ / ~10.000 soru" derken App Store açıklaması "1500+/2000+ Türkçe sorusu,
38 matematik konusu" diyor. Ürün henüz içerik derinliğini kurmuş değil — **büyümesi mekanikten geliyor,
içerikten değil.** Dört ayda 4,85 puan alması da bunu destekliyor: az içerikle iyi his.

**EğitimÜssü için ne öğretiyor — üç ders:**
1. **Alışkanlık kancası ücretsiz olmalı.** Net Up streak'i, ligi ve rozeti Free verip parayı *sınırsız can,
   analiz, veli raporu* arkasına koyuyor. ✅ **EğitimÜssü kodu zaten aynı modelde** (`MembershipGate`:
   Free = kronometre + streak tam + rozet + son 30 gün; Premium = derinlik). PRD §9.2 bunun tersini
   yazıyordu, **2026-09-05'te koda göre düzeltildi** —
   bkz. [parite analizi](../denetim/2026-09-05_rakip_paritesi_analizi.md) §3 (X-01).
2. **Asıl açık streak değil, lig ve günlük görev.** Karşılaştırmalı hiçbir mekaniğimiz yok (R-01, R-03).
3. **Az içerikle de iyi ürün olunur** — mekanik ve his, içerik hacminden önce geliyor. Bu, R-14'ü
   (soru bankası) iptal önerisini destekleyen en somut kanıt.

### 3.4 DUS Club — niş, topluluk odaklı; EğitimÜssü'nün öğrenci moduna en benzer ürün

| | |
|---|---|
| **Yayıncı** | Denthall Club (Play) / Oğuzhan Arslan (App Store) |
| **Ölçek** | Play 50+ · App Store TR 4,77 (22) |
| **Yaş** | **2026-03-03** — altı aylık ürün |
| **Hedef** | DUS (Diş Hekimliğinde Uzmanlık Sınavı) adayları — dar ve profesyonel bir niş |

**Ürün — kendi başlıklarıyla:**
- **Odak modu:** Kronometre ve **Pomodoro**; hangi derse ne kadar süre ayırdığını otomatik kaydeder
- **Soru havuzu:** Soru bankası, çıkmış benzeri sorular, **vaka soruları**, **yanlış defteri**
- **Hafıza merkezi:** Flashcard ile aktif tekrar; kendi kartın + **topluluk kartları** + mnemonics/şifreler
- **Çalışma ekibim:** Kendi ekibini kur **ya da bir koda dahil ol**; ekip içi günlük çalışma sıralaması
- **Çalışma takvimim:** Haftalık / iki haftalık / aylık görünüm, günlük görev ekleme
- **İstatistik merkezi:** Derse göre saat, haftalık grafik

**Gelir:** Ücretsiz + **DUSClubber** aboneliği (mağaza içi satın alma, fiyat uygulama içinde).

**Konumlandırma:** *"DUS bir maraton, DUS Club ise bir kulüp. Bu yolda yalnız değilsin."* Ürünün sattığı
şey içerik değil, **yalnız olmama hissi**.

**EğitimÜssü için ne öğretiyor:**
1. Kronometre + istatistik + takvim + not — hepsi bizde **zaten var ve daha derin** (M08 38 uç, M04 20 uç).
2. Eksik olan iki şey: **flashcard/aralıklı tekrar** (R-05) ve **kod ile katılınan ekip + sıralama** (R-02).
3. **En önemli içgörü:** DUS Club'da kullanıcı ekibini **kendi kurmak zorunda**; bizde ekip **doğal olarak
   var** — öğretmenin öğrenci listesi. Sıfır kurulumla aynı mekanik, üstelik ligi gören bir öğretmenle.
   Bu, taklit değil **üstünlük** olur (Ü-07).
4. Niş bir dikeyde 22 değerlendirmeyle 4,77 tutmak, dar hedefin işe yaradığını gösteriyor.

---

## 4. B grubu — kurum ↔ veli iletişimi

### 4.1 VeliCep — küçük ama bizim veli modülümüzün aynası

| | |
|---|---|
| **Şirket** | TMGAMES Oyun Yazılım Teknoloji ve Pazarlama Ltd. Şti. |
| **Ölçek** | Play 100+ · App Store TR 5,0 (11) |
| **Yaş** | **2026-03-08** |
| **Kapsam** | Yalnız **anaokulu ve oyun evi**. Kurum satın alır, veli ücretsiz indirir |

**Veli tarafı:** Günlük değerlendirme (devamsızlık, **yemek**, not) · **galeri** (öğretmenin paylaştığı
fotoğraf/video, **tarihe göre**, indirilebilir) · okul ve sınıf duyuruları + okunmamış bildirim ·
öğretmenle doğrudan mesajlaşma · yeni değerlendirme/duyuru/mesajda anlık bildirim.

**Öğretmen tarafı:** Tüm sınıfın devamsızlık/yemek/not bilgisini toplu girme · fotoğraf-video yükleme ·
**sınıf veya okul genelinde anlık duyuru** · velilerle bireysel organize mesajlaşma · **çoklu sınıfı
tek hesapla** yönetme.

**Güvenlik anlatısı:** Şifreli iletişim, oturum bilgisi cihazda güvenli saklama, yalnız okulun yetkili
hesaplarıyla erişim. (Küçük bir üründe bile güvenliğin **pazarlama malzemesi** olması dikkat çekici —
veli tarafında güven, özellikten önce geliyor.)

**EğitimÜssü için ne öğretiyor:**
1. M16 (mesajlaşma) + M09 (veli) + M11 (bildirim) üçlüsü zaten paritede.
2. **Sizde olmayan iki şey:** **bire-çok duyuru** (R-06) ve **tarih eksenli medya galerisi** (R-07).
   Duyuru, VeliCep'in öğretmen tarafındaki üç ana özelliğinden biri — bizde M16 yalnız 1-1 sohbet
   modellediği için öğretmen 10 öğrenciye tek mesaj atamıyor.
3. Veli beklentisi anaokulundan başlıyor: günlük bilgilendirme + fotoğraf + anında bildirim. Özel derse
   geldiğinde bu alışkanlıkla geliyor.

---

## 5. C grubu — asıl rakip kuşağı *(istenen listede yoktu, eklendi)*

### 5.1 Karşılaştırma

| Ürün | Model | Kime satar | Fiyat | Kapsam | Durum |
|------|-------|-----------|-------|--------|-------|
| **Tilki** (`tilki.app`) | Öğretmen SaaS | Öğretmen | **120 ₺/aktif öğrenci/ay** (150 ₺'den %20 tanıtım indirimi) · 30 gün ücretsiz, **kart istemiyor** · aylık, taahhütsüz | Öğrenci profili · tekrarlayan ders planı + otomatik takvim + **görüntülü ders linki** · ödev **WhatsApp ile dağıtım** + tamamlanma takibi + gecikme hatırlatması · bakiye/gecikme/aylık kazanç raporu · veli **WhatsApp bildirimi (uygulama indirmeden)** · iOS + Android + **web** | **En yakın rakip** · aktif |
| **Özel Ders Alanı** (`com.ozeldersalani.mobileapp`) | Pazar yeri | Öğretmen (görünürlük) | **Eko 1 ay · Bronz 3 ay · Silver 6 ay · Gold ve Platinum 1 yıl** otomatik yenilenen abonelik + **ders talebi kontörü**; bazı paketlerde ek branş / ek şehir avantajı · **ödeme Apple ID üzerinden** | Branş/konum/fiyat/ders biçimi filtresi · öğretmen profili · ders talebi · mesajlaşma · ders planlama · satın alınan paket ve tamamlanan ders takibi | 2026-07 çıkışlı · iOS 4,57 (14) · aktif |
| **Öğretsen** (`com.ogretsen.app`) | Pazar yeri | Öğretmen | Öğretmene **ücretsiz** profil | Konuma göre öğretmen bulma · **harita üzerinden filtreleme** · profil + kullanıcı yorumları · doğrudan mesajlaşma · fiyat/branş/mesafe filtresi · akademik, spor, sanat, hobi kategorileri | 2022-03'ten beri · iOS 4,32 (**41**) · son güncelleme 2026-08 |
| **Öğretmenim.app** (`com.ogretmenim.ios`) | Pazar yeri | — | Öğrenciye **ücretsiz** ("ücretsiz kalacaktır") | Onaylı öğretmen profilleri (diploması doğrulanmış) · konuma göre listeleme · takvim · **soru havuzu** (öğrenci soru yükler, öğretmenler çözümüyle yanıtlar) | iOS sürümü **2022-10'dan beri güncellenmemiş** → fiilen terk edilmiş |
| **Özel Ders Takip Sistemi** (`com.karolbilisim.ozeldersapp`) | Cihaz-içi defter | Öğretmen | **Ücretsiz (2 öğrenci) · Başlangıç (5) · Standart (10) · Pro (sınırsız)** — tüm özellikler her pakette | Öğrenci + veli kaydı, aktif/arşiv · ders kaydı (tarih, konu, süre) · saatlik ücret + tahsilat + **otomatik bakiye** · **profesyonel PDF döküm** · Google Drive / e-posta yedekleme · **internet gerektirmez, veri yalnız cihazda** | 2026-04 · iOS 5,0 (4) · niş |

### 5.2 Bu kuşaktan çıkan dört ders

1. **Öğrenci başına fiyatlama tavan yapıyor.** Tilki 120 ₺/aktif öğrenci/ay → 10 öğrencili öğretmen
   **1.200 ₺/ay** öder. EğitimÜssü'nün **sabit aylık premium**'u burada net bir fiyat silahı:
   *"Tilki'de 10 öğrenci 1.200 ₺, bizde tek fiyat."* PRD §9.1 zaten sabit paket öngörüyor — eksik olan
   bunu **pazarlama mesajına çevirmek**.
2. **Saf eşleştirme ölmüş bir model.** Öğretsen 2022'den beri yayında ve dört yılda **41 değerlendirme**;
   Öğretmenim.app 2022'den beri hiç güncellenmemiş. İkisi de "öğretmen bul" vaadiyle başlamış, ders
   sonrası hiçbir şey sunmamış. PRD §10.1'in *"eşleştirmeyi en son aç"* kararı **dışarıdan doğrulanıyor**.
3. **Veliyi uygulamaya sokmak zorunda değilsiniz.** Tilki velinin uygulama indirmesini **hiç istemiyor**,
   her şeyi WhatsApp'tan yolluyor; Net Up raporu WhatsApp/SMS/e-posta ile paylaştırıyor. EğitimÜssü'nün
   M09'u indirme + hesap + veli bağı gerektiriyor → **sürtünme**. En ucuz çözüm: paylaşılabilir özet
   metni/link (R-08).
4. **Bulut karşıtı bir niş var.** "Özel Ders Takip Sistemi" *"veriniz yalnız cihazınızda, %100 gizlilik"*
   diyerek pazar açmış. Bu, bulut ürünlerine duyulan güvensizliğin kanıtı — bizde `sharing` ayarı ve
   M15 sözleşmeleri zaten var, bunu **duruşa** çevirmek bedava (Ü-09).

---

## 6. Özellik karşılaştırma matrisi

Ölçüt: EğitimÜssü'nün **bugünkü kod gerçeği**
([`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) + endpoint envanteri).

| Yetenek | EğitimÜssü | Tilki | Doping | Pakodemy | Net Up | DUS Club | VeliCep |
|---------|-----------|-------|--------|----------|--------|----------|---------|
| Öğretmen ders takvimi / planlama | 🟢 M04 (20 uç) | 🟢 | ❌ | ❌ | ❌ | 🟡 kişisel | 🟡 sınıf |
| Ders oturumu / yoklama kaydı | 🟢 M05 | 🟢 | ❌ | ❌ | ❌ | ❌ | 🟢 devamsızlık |
| Ödev atama + teslim + puanlama | 🟢 M06 | 🟢 (WhatsApp) | 🟡 | 🟡 öğretmen ödevi | ❌ | ❌ | ❌ |
| Ödeme/tahsilat takibi (öğretmen) | 🟢 M07 | 🟢 | ❌ | ❌ | ❌ | ❌ | ❌ |
| Bireysel çalışma kronometresi | 🟢 M08 (38 uç) | ❌ | 🟢 | 🟢 | 🟡 XP | 🟢 + Pomodoro | ❌ |
| Deneme + net trendi | 🟢 M08 | ❌ | 🟢 | 🟢 | 🟡 | 🟢 | ❌ |
| Konu/kazanım hâkimiyeti + hedef | 🟢 M10 | 🟡 not | 🟢 | 🟢 | 🟢 | 🟡 | ❌ |
| Veli görünümü | 🟢 M09 | 🟢 WhatsApp | 🟢 ayrı uygulama | 🟢 | 🟢 haftalık | ❌ | 🟢 |
| Rapor / analiz + PDF | 🟢 M14 | 🟢 | 🟢 | 🟢 | 🟡 | 🟡 grafik | ❌ |
| Bildirim / push | 🟢 M11 | 🟢 WhatsApp | 🟢 | 🟢 | 🟢 zamanlı | 🟢 | 🟢 |
| Mesajlaşma | 🟡 M16 | 🟡 WhatsApp | 🟡 koçluk | 🟡 koçluk | ❌ | ❌ | 🟢 |
| Üyelik / paywall | 🟡 M17 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | — |
| **Gerçek para tahsilatı** | ❌ *(Q1 bloke)* | 🟢 kart | 🟢 havale | 🟢 havale | 🟢 IAP | 🟢 IAP | — |
| Öğretmen–öğrenci eşleştirme | 🔴 M12 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Puanlama & yorum | 🔴 M13 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Öğretmen boş zaman analizi | 🟢 **tek bizde** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Soru bankası / içerik** | ❌ *(kapsam dışı)* | ❌ | 🟢 dev | 🟢 dev | 🟢 | 🟢 | ❌ |
| **Video ders / çözüm** | ❌ | ❌ | 🟢 | 🟢 | ❌ | ❌ | ❌ |
| **Oyunlaştırma (seri + rozet)** | 🟡 seri + **10 rozet** var, lig/XP yok | ❌ | 🟡 | 🟡 düello | 🟢 **çekirdek** | 🟡 ekip sıralaması | ❌ |
| **Flashcard / aralıklı tekrar** | ❌ | ❌ | 🟢 hafıza teknikleri | 🟡 | 🟡 yanlış tekrarı | 🟢 | ❌ |
| **Sosyal / kulüp / ekip** | ❌ | ❌ | ❌ | 🟡 düello | 🟡 lig | 🟢 ekip + kod | ❌ |
| **Bire-çok duyuru** | ❌ | 🟡 | 🟢 | 🟢 | ❌ | ❌ | 🟢 |
| **Medya galerisi (tarihe göre)** | 🟡 dosya var | ❌ | ❌ | ❌ | ❌ | ❌ | 🟢 |
| **WhatsApp / dış kanal** | ❌ | 🟢 **çekirdek** | 🟢 satış+destek | 🟢 destek | 🟢 rapor paylaşımı | ❌ | ❌ |
| Web erişimi | ❌ *(P14)* | 🟢 | 🟢 | 🟢 | ❌ | ❌ | ❌ |

**Sonuç:** EğitimÜssü **operasyon sütununda (ilk yedi satır) listedeki hiçbir rakibin yaklaşamadığı bir
derinlikte** — ve tek başına Tilki bu sütunda yarışıyor, o da öğrenci tarafı olmadan. Boşluklar üç kümede:
1. **İçerik** (soru bankası, video) — bilinçli kapsam dışı, öyle kalmalı.
2. **Oyunlaştırmanın sosyal kısmı** (lig, ekip, günlük görev) — seri ve rozet zaten var.
3. **Para akışı ve dış kanal** (tahsilat, WhatsApp/paylaşım, duyuru, galeri) — en yüksek getirili küme.

---

## 7. İş modeli, fiyat ve mağaza kuralları

### 7.1 Gelir modeli haritası

| Model | Kim uyguluyor | Rakam | Kime uygun |
|-------|---------------|-------|-----------|
| Yıllık yüksek paket + **telesatış** | Doping Hafıza | Fiyat gizli; 3. taraf: 2.799–37.900 ₺, 12 taksit | Yüksek bilet, satış ekibi olan |
| Yıllık paket, **açık fiyat** + kampanya | Pakodemy | 10.999 ₺ (liste 20.999 ₺), %47 indirim, 7 gün iade | İçeriği hazır olan |
| **Aylık/yıllık freemium abonelik** | Net Up, DUS Club | 99,99 ₺/ay · 499,99 ₺/yıl (yıllık = 5 aylık) | ← **EğitimÜssü'nün modeli** |
| **Kullanım başına** SaaS | Tilki | 120 ₺/aktif öğrenci/ay | Değeri öğrenci başına ölçülebilen |
| **Görünürlük aboneliği + kontör** | Özel Ders Alanı | Eko/Bronz/Silver/Gold/Platinum + ders talebi kontörü | Pazar yeri (M12 açılınca bizde de) |
| Öğrenci sayısı **kademeli lisans** | Özel Ders Takip Sistemi | Ücretsiz 2 / 5 / 10 / sınırsız | Basit araç |
| **Reklam** | **Hiçbiri** | — | — |

### 7.2 Üç somut çıkarım

**(a) Reklam bu pazarda yok.** İncelenen **11 üründen hiçbiri** reklam göstermiyor. Türkiye eğitim
pazarında — özellikle çocuk ve veli tarafında — reklam marka güvenini düşüren bir sinyal olarak görülüyor.
PRD §9.4'ün reklam kararı ve P09 Task 5 (AdMob / **Q3**) bu yüzden **düşük öncelikli** değerlendirilmeli;
gelir abonelikten gelmeli.

**(b) Ücretsiz katman gerçekten kullanılabilir olmalı.** Net Up ücretsizde lig, günlük görev, seri, rozet
ve binlerce soru veriyor; parayı *sınırsız can + analiz + veli raporu*na koyuyor. Doping ve Pakodemy'nin
ücretsiz katmanı yok denecek kadar dar, ama onların satış ekibi var. Bizim kanalımız ürün — o yüzden
ücretsiz katman geniş olmalı. **Kodumuz zaten böyle** (`MembershipGate`).

**(c) Yıllık indirim agresif olabilir.** Net Up yıllığı aylığın 5 katına satıyor (7 ay bedava).
Bu, nakit akışını öne çeken ve terk oranını düşüren bilinen bir taktik; M17 plan kataloğunda değerlendirilmeli.

### 7.3 Mağaza kuralları — Q1 (ödeme) kararına doğrudan girdi

Apple iki farklı para akışını **farklı** ele alıyor:

| Para akışı | Apple kuralı | Sonuç |
|------------|--------------|-------|
| **Uygulama özelliklerini açan premium abonelik** (M17) | **3.1.1** — dijital içerik/özellik → **IAP zorunlu** | Net Up, DUS Club ve **Özel Ders Alanı** premium paketlerini *Apple ID üzerinden* satıyor; Özel Ders Alanı'nın App Store açıklamasında bu **açıkça yazıyor** |
| **Öğrencinin öğretmene ödediği ders ücreti** | **3.1.3(d) Person-to-Person Services** — *"real-time person-to-person services between two individuals (for example **tutoring students**, medical consultations…) you may use purchase methods other than in-app purchase"* | Bire-bir özel ders tahsilatı iyzico/havale ile yapılabilir. ⚠️ *"One-to-few and one-to-many real-time services **must** use in-app purchase"* → **grup dersi bu istisnanın dışında** |

**Pratik yol:** *premium üyelik = StoreKit / Play Billing · ders ücreti = iyzico.*
Mevcut plan (her ikisi de iyzico) mobilde ret riski taşıyor. Ayrıca mağaza komisyonu (%15–30)
fiyatlandırmaya yansıtılmalı — Net Up'ın 99,99 ₺'si bu komisyonla belirlenmiş bir fiyat.

---

## 8. ❓ Çözülemeyen: `northcreative`

Taranan kaynaklar: Apple iTunes Search API (`attribute=softwareDeveloper` ile yayıncı araması),
Google Play arama ve geliştirici sayfaları, APKCombo paket dizini, genel web araması ve **sekiz alan adı
denemesi**. **Türkiye eğitim pazarında eşleşme bulunamadı.**

Bulunan üç ayrı "North Creative" kaydının **hiçbiri eğitim ürünü değil**:

| Kayıt | Ne | Not |
|-------|-----|-----|
| **North Creative** (App Store yayıncısı) | Tek uygulaması *That Run Club* (`com.vs.thatrunclub`, ABD, Sağlık & Fitness) — koşu kulübü kur/katıl, grup koşusu planla, uygulama içi sohbet, rota keşfi | *Kulüp mekaniği* açısından DUS Club'a benziyor; kastedilen buysa ilgi noktası **topluluk/kulüp deseni** olabilir |
| **northcreative.io** | ABD merkezli video prodüksiyon / pazarlama ajansı | Eğitimle ilgisi yok |
| **northcreative.net** | Birleşik Krallık (Oldham) merkezli fotoğrafçılık şirketi | Eğitimle ilgisi yok |

Alan adı denemeleri: `northcreative.com` (Cloudflare 403), `.com.tr` / `.co` / `.dev` / `.app` (çözülmedi),
`.net` (UK fotoğrafçılık), `.io` (ABD video ajansı), `north-creative.com` (North Creative, LLC).

**Gerekli:** Mağaza linki, alan adı veya ekran görüntüsü. Verilirse bu doküman ve parite analizi güncellenecek.

---

## 9. EğitimÜssü için ne yapmalı

Bu bölümün ayrıntısı ayrı dokümanda — orada her madde kod gerçeğine karşı doğrulanmış, çözüm taslağı,
kabul kriteri ve riskiyle birlikte:
**[`../denetim/2026-09-05_rakip_paritesi_analizi.md`](../denetim/2026-09-05_rakip_paritesi_analizi.md)**

Özet yönlendirme:

| Bulgu | Madde | Dalga |
|-------|-------|-------|
| Veli veriyi görmek için uygulama indirmek zorunda (Tilki bunu bilerek terk etmiş) | **R-08** paylaşım | 1 |
| Öğretmen 10 öğrenciye tek mesaj atamıyor (VeliCep'in ana özelliği) | **R-06** duyuru | 1 |
| Streak var ama hatırlatan yok (Net Up 19:00/20:00) | **R-10** yerel hatırlatıcı | 1 |
| Medya tarihe göre gezilemiyor (VeliCep galerisi) | **R-07** galeri | 1 |
| Karşılaştırmalı hiçbir mekanik yok (Net Up ligi, DUS Club ekibi) | **R-01 + R-02 + Ü-07** | 2 |
| "Bugün ne yapmalıyım" cevabı yok (Net Up günlük görev) | **R-03** | 2 |
| Ürün para alamıyor (11 rakipten 10'u alıyor) | **R-09** IAP + iyzico | 3 |
| İçerik boşluğu (Doping/Pakodemy'nin sermaye oyunu) | **Ü-03** öğretmen içerik havuzu — içeriğe girmeden | 4 |
| Rakiplerin yapısal olarak kopyalayamayacağı fırsatlar | **Ü-01…Ü-10** | 2–5 |

---

## 10. Kaynaklar ve veri kalitesi

**Mağaza verisi (2026-09-05):**
- Apple iTunes Lookup / Search API (TR mağazası) — `com.netup.app`, `com.tmgames.velicepapp`,
  `com.denthall.dusclub`, `com.tosanalytics.pakodemy`, `com.dopinghafiza.mobile.app`,
  `com.ozeldersalani.mobileapp`, `com.ogretsen.app`, `com.karolbilisim.ozeldersapp`, `com.ogretmenim.ios`
- Google Play / APKCombo listeleri — indirme kovası, geliştirici adı, sürüm ve güncelleme tarihi
- Ham döküm: [`../raw/2026-09-05_rakip_magaza_verisi.md`](../raw/2026-09-05_rakip_magaza_verisi.md)

**Ürün siteleri:**
- Net Up — https://netup.website/
- Pakodemy — https://pakodemy.com/
- Doping Hafıza — https://www.dopinghafiza.com/ · https://www.dopinghafiza.com/shop
- Tilki — https://www.tilki.app/
- Öğretmenim.app — https://ogretmenim.app/
- Özel Ders Alanı — https://www.ozeldersalani.com/

**Kural referansı:**
- Apple App Store Review Guidelines **3.1.1** (dijital içerik → IAP) ve **3.1.3(d)** *Person-to-Person
  Services* — https://developer.apple.com/app-store/review/guidelines/

**Veri kalitesi notları:**
- **Play indirme sayıları kova değeridir** (`50+`, `100+`, `1.000+`, `1.000.000+`) — kesin sayı değil,
  büyüklük mertebesi olarak okunmalı.
- **Şirketlerin kendi büyüme iddiaları doğrulanmadı** (Pakodemy "2M+ aktif öğrenci", Doping "16 yıl",
  Net Up "8.500 soru"). Bağımsız kaynak yok; mağaza değerlendirme sayıları tek bağımsız gösterge.
- **Doping Hafıza fiyatları sitede yayınlanmıyor**; 2.799–37.900 ₺ aralığı **üçüncü taraf haber
  kaynaklarından** alınmıştır, üreticinin resmî beyanı değildir.
- **Pakodemy'nin Play sayfası alınamadı** (404) — Play tarafı rakamları eksik.
- Net Up'ın site ve mağaza açıklaması **soru sayısında çelişiyor** (8.500+ vs 2.000+); ikisi de rapor edildi.

---

## İlgili dokümanlar

- [`../denetim/2026-09-05_rakip_paritesi_analizi.md`](../denetim/2026-09-05_rakip_paritesi_analizi.md) — **bu analizin devamı**: parite (R-01…R-14) + üstünlük (Ü-01…Ü-10) maddeleri, kod gerçeğine karşı
- [`../ozel_ders_platformu_PRD.md`](../ozel_ders_platformu_PRD.md) — §9 free/premium (2026-09-05'te koda göre düzeltildi) ve §10 stratejik notlar
- [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) — modül durum tablosu (karşılaştırmanın kod tarafı)
- [`../modules/m17_membership.md`](../modules/m17_membership.md) — üyelik/entitlement/kampanya tasarımı
- [`../modules/m12_matching.md`](../modules/m12_matching.md) — eşleştirme (🔴), §5.2'deki uyarı buraya bakar
- [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md) — rol×yetenek matrisi

*Güncelleme: 2026-09-05*
