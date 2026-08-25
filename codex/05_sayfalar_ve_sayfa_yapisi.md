---
title: "Olusturulacak Sayfalar ve Sayfa Yapisi"
summary: "EgitimUssu icin rol bazli tum mobil/web sayfalarinin kapsamli tasarim, icerik, aksiyon ve navigasyon dokumani"
tags: [sayfa, mobil, navigasyon, ia, ux, tasarim]
authority: derived
updated: 2026-08-22
source: ../doc/ozel_ders_platformu_PRD.md
---

# Olusturulacak Sayfalar ve Sayfa Yapisi

Bu dokuman, EgitimUssu uygulamasinda olusturulacak sayfalari "hangi sayfa var?" seviyesinde degil, "sayfa nasil calisacak, hangi widget'lar olacak, kullanici ne yapacak, hangi veriler gorunecek?" seviyesinde tarif eder.

Ana referans: mobil oncelikli urun, uc rol, once gunluk kullanim ve bireysel calisma, sonra eslestirme.

## 1. Genel UI ve Navigasyon Kurallari

### 1.1 Ortak Sayfa Iskeleti

Tum rol sayfalarinda genel iskelet su sekilde olmalidir:

1. Ust navbar/header.
2. Sayfa basligi veya rol durumuna gore karsilama alani.
3. Kritik istatistik veya durum widget'lari.
4. Ana islem alani.
5. Liste/grafik/detay bolumleri.
6. Bos durum, hata durumu ve yukleniyor durumlari.
7. En altta rol bazli bottom navigation.

### 1.2 Login Olan Kullanici Navbar'i

Ogretmen, ogrenci ve veli ana sayfalarinin en ustunde login olan kullaniciyi taniyan sade bir navbar olmalidir.

Navbar icerigi:

- Sol: profil fotografi veya bas harf avatar.
- Orta: kisa karsilama metni.
- Alt/ikincil satir: kullanicinin rolune uygun durum metni.
- Sag: bildirim ikonu, ayarlar/profil kisa yolu.
- Premium kullanicida kucuk Premium rozeti.

Ornekler:

- Ogrenci: "Merhaba Elif" + "Bugun hedefin 90 dk".
- Ogretmen: "Merhaba Ayse Hoca" + "Bugun 4 dersin var".
- Veli: "Merhaba Mehmet Bey" + "2 cocuk takip ediliyor".

### 1.3 Bottom Navigation

Ogrenci bottom navigation:

1. Calisma
2. Derslerim
3. Performans
4. Profil

Ogretmen bottom navigation:

1. Panel
2. Takvim
3. Ogrenciler
4. Odemeler
5. Profil

Veli bottom navigation:

1. Ana Sayfa
2. Cocuklar
3. Bildirimler
4. Profil

Admin paneli mobil bottom nav yerine web sol menusuyle tasarlanabilir.

### 1.4 Ortak Durumlar

Her sayfada su durumlar tasarlanmalidir:

- Yukleniyor: skeleton veya shimmer.
- Bos durum: kullaniciyi bir sonraki dogru aksiyona goturen metin + buton.
- Hata: tekrar dene + destek/geri bildirim linki.
- Yetkisiz: rol veya sahiplik aciklamasi.
- Offline: son senkron zamani ve sinirli kullanim bilgisi.

## 2. Ortak Auth, Onboarding ve Hesap Sayfalari

### 2.1 Karsilama Sayfasi

Route: `/`

Amaç: Ilk acilista kullaniciyi uygulamanin ana degerine sokmak ve giris/kayit aksiyonunu vermek.

Sayfa yapisi:

- Ustte logo ve uygulama adi.
- Orta alanda kisa deger onerisi: ogretmen, ogrenci ve veli icin tek cumlelik aciklama.
- Ana butonlar: Giris Yap, Kayit Ol.
- Alt alanda rol bazli mini kartlar:
  - Ogretmen: ders, odev, odeme takibi.
  - Ogrenci: calisma, deneme, hedef.
  - Veli: gelisim, odev, odeme takibi.

### 2.2 Rol Secimi Sayfasi

Route: `/role-selection`

Sayfa yapisi:

- Navbar: geri butonu, baslik.
- Uc buyuk secim karti:
  - Ogretmen olarak devam et.
  - Ogrenci olarak devam et.
  - Veli olarak devam et.
- Her kartta rolun ana vaadi ve ikon.
- Secim sonrasi kayit formu role gore sekillenir.

### 2.3 Kayit Sayfasi

Route: `/register`

Ortak alanlar:

- Ad soyad.
- E-posta.
- Telefon.
- Sifre.
- Sifre tekrar.
- KVKK/onay checkbox'lari.

Role gore ek alanlar:

- Ogretmen: brans, sehir/ilce, ders sekli.
- Ogrenci: sinif seviyesi, hedef sinav, hedef ozet.
- Veli: cocuk baglama kodu opsiyonel.

Kayit sonrasi yonlendirme:

- Ogretmen -> ogretmen profil tamamlama.
- Ogrenci -> calisma sayfasi veya hedef kurulum.
- Veli -> cocuk baglama sayfasi.

### 2.4 Giris Sayfasi

Route: `/login`

Sayfa yapisi:

- E-posta/telefon alanı.
- Sifre alani.
- Sifremi unuttum linki.
- Giris yap butonu.
- Kayit ol linki.
- Hata durumunda net mesaj: "E-posta veya sifre hatali".

### 2.5 Hesap Bilgileri Sayfasi

Route: `/account-info`

Sayfa yapisi:

- Navbar.
- Profil bilgileri formu.
- E-posta/telefon dogrulama durumu.
- Sifre degistirme bolumu.
- Cihaz/oturum bilgileri.
- Hesap kapatma ve veri silme bolumu.

## 3. Ogrenci Rolu Sayfa Tasarimi

Ogrenci deneyimi 4 ana sekme ve sekme disi detay sayfalarindan olusur. Ogrenci, ogretmensiz de tum temel calisma akisini kullanabilmelidir.

### 3.1 Calisma Sayfasi

Route: `/student-home`

Menu konumu: bottom nav 1. eleman.

Amaç: Ogrencinin gunluk calisma davranisini baslatmak, motive etmek ve hizli aksiyonlara ulastirmak.

Sayfa yapisi:

1. Login olan kullanicinin navbar'i.
2. Istatistik widget'lari.
3. Calismaya basla motivasyon widget'i.
4. Hizli erisim kartlari.
5. Yaklasan dersler.
6. Yaklasan/aktif odevler.
7. Son calisma ve son deneme ozeti.
8. Bottom navigation.

#### 3.1.1 Navbar

Icerik:

- Profil avatar/fotograf.
- "Merhaba, {ad}".
- Gunluk hedef bilgisi.
- Bildirim ikonu.
- Premium rozeti, varsa.

#### 3.1.2 Istatistik Widget'lari

Navbar altinda yatay kaydirilabilir veya 2x2 grid olabilir.

Gosterilecek veriler:

- Streak: kac gunluk seri, dun/bugun durumu.
- Bugunku calisma zamani: dakika/saat, gunluk hedefle beraber progress/loading bar.
- Haftalik odev istatistigi: verilen, tamamlanan, geciken.
- Haftalik ders istatistigi: planlanan, tamamlanan, kalan.

Ek opsiyonel veriler:

- Haftalik toplam calisma.
- Son deneme neti.
- Kisisel rekor etiketi.

Widget davranisi:

- Hedef tamamlandiysa rozet/renk degisir.
- Geciken odev varsa odev karti uyarici ama panik yaratmayan tonda olur.
- Veriler yoksa "Bugun ilk calismani baslat" bos durumu gosterilir.

#### 3.1.3 Calismaya Basla Widget'i

Icerik:

- Motive edici kisa metin.
- Alt metin: hedefe kalan sure veya son calisilan ders.
- Ana buton: "Calismaya Basla".
- Ikincil buton: "Manuel sure ekle" opsiyonel.

Buton davranisi:

- "Calismaya Basla" -> Kronometre sayfasi `/study/timer`.
- Son aktif seans varsa -> "Devam Et" olarak gorunebilir.

#### 3.1.4 Hizli Erisim Kartlari

Kartlar:

- Takvim -> `/student/lessons` takvim gorunumu.
- Derslerim -> `/student/lessons`.
- Odevlerim -> `/student/assignments`.
- Hedeflerim -> `/study/goals`.
- Performansim -> `/student/performance`.

Tasarim:

- Ikon + baslik + kisa durum.
- Ornek: Odevlerim kartinda "2 bekleyen".
- Kartlar 2 sutun grid veya yatay kaydirma olabilir.

#### 3.1.5 Yaklasan Dersler ve Odevler

Yaklasan dersler:

- Ders adi.
- Tarih/saat.
- Ogretmen adi, varsa.
- Online/yuz yuze bilgisi.
- Kalan sure.
- Kart tiklama -> Ders detayi.

Odevler:

- Odev basligi.
- Ders/konu.
- Son teslim tarihi.
- Durum: bekliyor, teslim edildi, onaylandi, geri gonderildi.
- Kart tiklama -> Odev detayi.

Bos durum:

- Ders yoksa: "Bugun planli dersin yok".
- Odev yoksa: "Bekleyen odevin yok".

### 3.2 Derslerim Sayfasi

Route: `/student/lessons`

Menu konumu: bottom nav 2. eleman.

Amaç: Ogrencinin kendi olusturdugu calisma derslerini ve ogretmenin olusturdugu ozel dersleri tek yerde gormesi.

Sayfa yapisi:

1. Navbar.
2. Gorunum secici: Takvim / Liste.
3. Filtreler.
4. Takvim veya liste icerigi.
5. Yeni ders ekle butonu.
6. Bottom navigation.

#### 3.2.1 Gorunum Secici

Default: Takvim gorunumu.

Secenekler:

- Takvim.
- Liste.

Takvim gorunumu:

- Ustte aylik/haftalik takvim.
- Secilen gun vurgulanir.
- Takvimde ders bulunan gunlerde nokta/renk gosterilir.
- Takvimin altinda secilen gune ait dersler listelenir.

Liste gorunumu:

- Ogretmen sayfasindaki dersler listesine benzer.
- Bugun, bu hafta, gelecek, gecmis gruplari olabilir.
- Arama ve filtre desteklenir.

#### 3.2.2 Ders Kaynagi Ayrimi

Ders kartlarinda ogrencinin kendi olusturdugu derslerle ogretmenin olusturdugu dersler net ayrilmalidir.

Ayrim onerileri:

- Kendi dersi: "Kendi Planim" rozeti.
- Ogretmen dersi: "Ogretmen Dersi" rozeti + ogretmen avatar/ad.
- Renk veya sol serit farki.
- Ogretmen dersleri salt okunur; ogrenci sadece erteleme talebi gonderebilir.

#### 3.2.3 Ders Karti Alanlari

- Ders adi.
- Konu veya konu sayisi.
- Tarih/saat.
- Sure.
- Ders kaynagi.
- Ogretmen adi, varsa.
- Online link veya lokasyon.
- Durum: planlandi, tamamlandi, iptal, ertelendi.

Kart aksiyonlari:

- Tikla -> Ders detayi.
- Kendi dersi ise duzenle/sil.
- Ogretmen dersi ise erteleme talebi.

#### 3.2.4 Yeni Ders Ekleme

Buton:

- Floating action button veya ust sag buton.

Form alanlari:

- Ders.
- Konu veya konular.
- Tarih/saat.
- Sure.
- Tekrarlama.
- Not.
- Hatirlatici.

Kurallar:

- Ogretmen dersinin saatiyle cakisirsa uyarilir.
- Ogrenci kendi dersini duzenleyebilir ve silebilir.
- Ogretmen dersini duzenleyemez.

### 3.3 Performans Sayfasi

Route: `/student/performance`

Menu konumu: bottom nav 3. eleman.

Amaç: Ogrencinin test/deneme, hedef net, konu eksigi, haftalik/aylik analiz ve kisisel rekorlarini anlamli sekilde takip etmesi.

Sayfa yapisi:

1. Navbar.
2. Performans ozet widget'lari.
3. Test/deneme girisi CTA.
4. Net gelisim grafigi.
5. Hedef net takibi.
6. Konu bazli istatistikler.
7. Konu eksigi ve guclu konular.
8. Haftalik/aylik analiz.
9. Kisisel rekorlar.
10. Son denemeler/testler listesi.
11. Bottom navigation.

#### 3.3.1 Performans Ozet Widget'lari

Gosterilecek veriler:

- Son deneme neti.
- Haftalik cozulen soru.
- Net artis/azalis.
- En cok calisilan ders.
- En zayif konu.
- Hedefe kalan net.

#### 3.3.2 Test ve Deneme Girisi

CTA butonlari:

- Deneme gir.
- Konu testi gir.
- Gecmis sonucu duzenle.

Test girisi alanlari:

- Ders.
- Konu.
- Toplam soru.
- Dogru.
- Yanlis.
- Bos.
- Sure.
- Not.

Deneme girisi alanlari:

- Deneme adi.
- Tarih.
- Sinav tipi: LGS, TYT, AYT, okul, diger.
- Ders bazli dogru/yanlis/bos.
- Otomatik toplam net.

#### 3.3.3 Net Gelisim Grafigi

Grafik:

- Cizgi grafik.
- Zaman araligi: 7 gun, 30 gun, 3 ay, tum zaman.
- Ders filtresi.
- Deneme/test ayrimi.

Grafikte gosterilecekler:

- Net.
- Hedef net cizgisi.
- Ortalama trend.
- Kisisel rekor noktasi.

#### 3.3.4 Hedef Net Takibi

Bolum icerigi:

- Hedef sinav.
- Hedef net/puan.
- Mevcut ortalama.
- Hedefe kalan fark.
- Hedef ilerleme bari.
- Hedefi duzenle butonu.

Premium ayrimi:

- Free kullanicida temel hedef gorunur.
- Detayli tahmin/grafik Premium olabilir.

#### 3.3.5 Konu Eksigi Tespiti

Konu kartlari:

- Konu adi.
- Ders.
- Basari yuzdesi.
- Son calisma tarihi.
- Cozulen soru sayisi.
- Onerilen aksiyon: "Bu konudan 20 soru coz", "15 dk tekrar yap".

Kategoriler:

- Guclu konular.
- Gelisen konular.
- Riskli konular.
- Hic calisilmayan konular.

#### 3.3.6 Haftalik ve Aylik Analiz

Analizler:

- Calisma suresi dagilimi.
- Ders/kategori dagilimi.
- Hedef vs gerceklesen.
- Deneme net ortalamasi.
- Odev tamamlama etkisi.
- En verimli gun/saat.

Gorunum:

- Sekmeli: Haftalik / Aylik.
- Bar grafik + kisa yorum kartlari.

#### 3.3.7 Kisisel Rekorlar

Gosterilecek rekorlar:

- En uzun calisma.
- En uzun streak.
- En yuksek deneme neti.
- Bir haftada en cok soru.
- Bir ayda en cok calisma saati.

### 3.4 Profil Sayfasi

Route: `/student/profile`

Menu konumu: bottom nav 4. eleman.

Amaç: Ogrencinin kimlik, abonelik, basarim, hedef, baglanti, bildirim ve gizlilik ayarlarini yonetmesi.

Sayfa yapisi:

1. Navbar veya profil hero.
2. Profil tanitim karti.
3. Abonelik/Premium durumu.
4. Mini istatistikler.
5. Kazanilan basarimlar.
6. Menu listesi.
7. Cikis yap.
8. Bottom navigation.

#### 3.4.1 Profil Hero

Icerik:

- Profil fotografi.
- Ad soyad.
- Sinif seviyesi.
- Hedef sinav/hedef.
- Premium/Free tasarim ayrimi.
- Profil duzenle butonu.

Premium tasarim:

- Premium rozeti.
- Daha belirgin kenarlik veya vurgu.
- Abonelik bitis/tip bilgisi.

#### 3.4.2 Mini Istatistikler

Gosterilecekler:

- Toplam calisma suresi.
- Toplam deneme/test sayisi.
- En uzun streak.
- Tamamlanan odev sayisi.
- En cok calisilan ders.

#### 3.4.3 Basarimlar

Bolum:

- Son kazanilan 3 rozet.
- Tum basarimlari gor butonu.
- Kilitli basarimlar icin hedef bilgisi.

#### 3.4.4 Profil Menuleri

Menuler:

- Profil duzenle.
- Veli baglantisi: veli ekleme/kontrol sayfasini acar.
- Ogretmen baglantisi: bagli ogretmenleri gosterir.
- Hedeflerim: hedef ekleme/duzenleme ekranini acar.
- Bildirim ayarlari.
- Gizlilik ve guvenlik.
- Aboneligim.
- Yardim ve geri bildirim.
- Cikis yap.

### 3.5 Kronometre Sayfasi

Route: `/study/timer`

Acilis: Calisma sayfasindaki "Calismaya Basla" butonundan acilir.

Amaç: Ogrencinin ders/konu secerek veya serbest calisma olarak net calisma suresi kaydetmesi.

Sayfa iki modlu olmalidir:

1. Hazirlik/secim modu.
2. Aktif kronometre modu.

#### 3.5.1 Hazirlik/Secim Modu

Ilk acilista ders secimi formu gelir.

Alanlar:

- Ders secimi.
- Konu secimi.
- Birden fazla konu secimi.
- Konu secmeden sadece ders secimi.
- Serbest calisma secenegi.
- Calisma hedef suresi: opsiyonel.
- Not: opsiyonel.

Hizli secim:

- Son calisilan dersler.
- Son calisilan konular.
- Favori ders/konular.
- "Dunku calismayi tekrar et" kisa yolu.

Ek aksiyonlar:

- Manuel calisma suresi ekle.
- Gecmis calismalar.

#### 3.5.2 Manuel Calisma Ekleme

Neden: Ogrenci gercekten calismis ama kronometre baslatmayi unutmus olabilir.

Form alanlari:

- Tarih.
- Baslangic/baslangicsiz sadece sure.
- Sure.
- Ders.
- Konu veya konular.
- Not.

Kurallar:

- Manuel kayit ayri etiketlenir.
- Gecmise donuk kayit siniri olabilir.
- Manuel kayit duzenlenebilir/silinebilir.

#### 3.5.3 Aktif Kronometre Modu

Basla butonuna basinca buyuk ve efektif kronometre ekrani acilir.

Gosterilecekler:

- Buyuk sayaç: `00:00:01` formatinda saat:dakika:saniye.
- Secilen ders.
- Secilen konu veya konu listesi.
- Serbest calisma ise "Serbest Calisma".
- Hedef sure, secildiyse.
- Hedefe ilerleme bari.
- Net calisma suresi.
- Toplam sure: mola + calisma.
- Mola sayisi.
- Toplam mola suresi.

Butonlar:

- Mola ver.
- Devam et.
- Calismayi bitir.
- Calismayi iptal et.

Bitirme akisi:

- Ozet ekranina gider.
- Kullanici not ekleyebilir.
- "Kaydet" ve "Duzenle" aksiyonlari olur.

Iptal akisi:

- Onay dialogu.
- Iptal edilen seans istatistige girmez.

#### 3.5.4 Gecmis Calismalar

Bu sayfadan veya performans/gecmis sayfasindan acilabilir.

Liste alanlari:

- Tarih.
- Ders/konu.
- Sure.
- Manuel/kronometre etiketi.
- Not.

Aksiyonlar:

- Duzenle.
- Sil.
- Detay gor.

### 3.6 Ders Detayi Sayfasi

Route: `/student/lessons/:id`

Amaç: Ogrencinin bir dersin tum bagli icerigini gormesi ve ilgili aksiyonlara hizli erismesi.

Sayfa yapisi:

1. Navbar.
2. Ders detay hero.
3. Ogretmen bilgisi, varsa.
4. Hizli erisim kartlari.
5. Odev listesi.
6. Test ve deneme listesi.
7. Konu listesi.
8. Not/kaynak listesi.

#### 3.6.1 Ders Detay Hero

Gosterilecekler:

- Ders adi.
- Tarih/saat.
- Sure.
- Durum.
- Online/yuz yuze bilgisi.
- Lokasyon veya meeting link.
- Ders kaynagi: kendi dersi / ogretmen dersi.

Ogretmen dersi ise:

- Ogretmen avatar/ad.
- Brans.
- Iletisim/mesaj kisa yolu, fazina gore.
- Erteleme talebi butonu.

Kendi dersi ise:

- Duzenle.
- Sil.
- Calismaya basla.

#### 3.6.2 Hizli Erisim Kartlari

Kartlar:

- Not ekle/Notlar.
- Odevler.
- Test/Deneme ekle.
- Konu ekle/duzenle.
- Calisma baslat.

#### 3.6.3 Bagli Listeler

Odev listesi:

- Baslik.
- Son teslim.
- Durum.
- Ogretmen geri bildirimi.

Test/deneme listesi:

- Tarih.
- Net.
- Ders/konu.
- Degisim.

Konu listesi:

- Konu adi.
- Calisilan sure.
- Test basari yuzdesi.
- Eksik/guclu etiketi.

Not/kaynak listesi:

- Ogrencinin kendi notlari.
- Ogretmenin paylastigi notlar.
- Dosya/link/kaynak.

### 3.7 Odevlerim Sayfasi

Route: `/student/assignments`

Sayfa yapisi:

- Navbar.
- Filtreler: Tum, Bekleyen, Teslim Edilen, Geciken, Geri Gonderilen.
- Odev istatistik kartlari.
- Odev listesi.
- Odev detay bottom-sheet veya sayfa.

Odev karti:

- Ders.
- Ogretmen.
- Baslik.
- Son teslim.
- Durum.
- Ek dosya sayisi.
- Geri bildirim rozeti.

Aksiyonlar:

- Dosya yukle.
- Teslim et.
- Geri bildirimi gor.
- Tamamlandi isaretle, eger bireysel odev ise.

### 3.8 Hedeflerim Sayfasi

Route: `/study/goals`

Sayfa yapisi:

- Navbar.
- Gunluk calisma hedefi.
- Haftalik calisma hedefi.
- Hedef sinav.
- Hedef net/puan.
- Ders bazli hedefler.
- Veli/ogretmenle paylasim anahtarlari.

Aksiyonlar:

- Hedef ekle.
- Hedef duzenle.
- Hedefi arsivle.
- Hatirlatici kur.

### 3.9 Gelişimim Sayfasi

Route: `/student/progress`

Sayfa yapisi:

- Navbar.
- Genel gelisim skoru.
- Eksik konular.
- Guclu konular.
- Konu hedefleri.
- Zaman serisi grafik.
- Onerilen calisma listesi.

## 4. Ogretmen Rolu Sayfa Tasarimi

Ogretmen deneyimi, gunluk operasyonu hizlandirmali: bugunku ders, ogrenciler, takvim, odev ve odeme tek akista gorunmelidir.

### 4.1 Ogretmen Paneli

Route: `/dashboard`

Menu konumu: bottom nav 1. eleman.

Sayfa yapisi:

1. Login olan ogretmen navbar'i.
2. Gunluk ozet widget'lari.
3. Bugunku dersler.
4. Bekleyen aksiyonlar.
5. Gelir/odeme ozeti.
6. Hizli erisim kartlari.
7. Bottom navigation.

Istatistik widget'lari:

- Bugunku ders sayisi.
- Tamamlanan ders sayisi.
- Bekleyen odev teslimleri.
- Geciken odemeler.
- Bu ay tahsil edilen.
- Aktif ogrenci sayisi.

Bugunku ders kartlari:

- Saat.
- Ogrenci.
- Ders/konu.
- Online/yuz yuze.
- Durum.
- Dersi baslat/tamamla butonu.

Bekleyen aksiyonlar:

- Not girilmemis tamamlanan ders.
- Teslim bekleyen odev.
- Teyit bekleyen "odedim" beyanlari.
- Yaklasan ders erteleme talepleri.

### 4.2 Takvim Sayfasi

Route: `/scheduling`

Menu konumu: bottom nav 2. eleman.

Sayfa yapisi:

- Navbar.
- Gorunum secici: Gun / Hafta / Ay.
- Takvim.
- Secilen gun ders listesi.
- Ders ekle butonu.
- Tatil/izin ekle butonu.

Ders karti:

- Ogrenci adi.
- Ders adi.
- Saat araligi.
- Ders sekli.
- Ucret/chargeable bilgisi.
- Durum.

Aksiyonlar:

- Ders ekle.
- Tekrarlayan ders ekle.
- Ertele.
- Iptal et.
- Sil.
- Dersi tamamla.
- Online link ekle.

Kurallar:

- Cakisan ders engellenir.
- Tatil/izin bloğu ders olusturma sirasinda uyarir.
- Tekrarlayan derste kapsam secimi olur: bu ders, bundan sonrakiler, tum seri.

### 4.3 Ogrenciler Sayfasi

Route: `/students`

Menu konumu: bottom nav 3. eleman.

Sayfa yapisi:

- Navbar.
- Arama.
- Filtreler: aktif, arsivli, davet bekleyen, odemesi geciken.
- Ogrenci limit karti.
- Ogrenci listesi.
- Ogrenci ekle butonu.

Ogrenci karti:

- Ad soyad.
- Sinif/hedef.
- Son ders.
- Bekleyen odev.
- Bakiye durumu.
- Bag durumu: manuel, davetli, gercek hesap.
- Veli bagi, varsa.

Aksiyonlar:

- Detaya git.
- Ders planla.
- Odev ver.
- Odeme ekle.
- Arsivle.
- Davet kodu gonder.

### 4.4 Ogrenci Detayi Sayfasi

Route: `/students/:studentId`

Sayfa yapisi:

- Navbar.
- Ogrenci profil hero.
- Sekmeler: Ozet, Dersler, Odevler, Notlar, Odemeler, Gelişim, Veli.
- Hizli aksiyonlar.

Ozet:

- Son ders.
- Yaklasan ders.
- Bekleyen odev.
- Bakiye.
- Haftalik calisma, paylasim aciksa.

Hizli aksiyonlar:

- Ders ekle.
- Odev ver.
- Not ekle.
- Odeme ekle.
- Veli davet kodu olustur.
- Mesaj gonder.

### 4.5 Dersler Sayfasi

Route: `/lesson-sessions`

Sayfa yapisi:

- Navbar.
- Filtre: bugun, bu hafta, tamamlanan, iptal, not bekleyen.
- Ders listesi.
- Toplu durum ozetleri.

Ders karti:

- Ogrenci.
- Tarih/saat.
- Konu.
- Durum.
- Not/odev var mi.
- Odeme durumu.

Aksiyonlar:

- Detay.
- Tamamla.
- Not gir.
- Odev ver.
- Odeme isaretle.

### 4.6 Ders Detayi Sayfasi

Route: `/lesson-sessions/:id`

Sayfa yapisi:

- Navbar.
- Ders bilgi hero.
- Ogrenci bilgisi.
- Ders tamamlama alani.
- Not/kaynak alani.
- Odev alani.
- Odeme alani.
- Gecmis degisiklikler.

Alanlar:

- Tarih/saat/sure.
- Konu.
- Katilim.
- IsChargeable.
- Iptal/erteleme notu.
- Online link/lokasyon.

Aksiyonlar:

- Tamamla.
- Not ekle.
- Kaynak ekle.
- Odev ver.
- Odeme kaydi olustur.
- Iptal/ertele.

### 4.7 Odev Takip Sayfasi

Route: `/assignments/:lessonSessionId` veya `/assignments`

Sayfa yapisi:

- Navbar.
- Odev durum istatistikleri.
- Filtreler.
- Odev listesi.
- Odev detay/duzenleme.

Odev alanlari:

- Baslik.
- Aciklama.
- Ders/konu.
- Son teslim.
- Dosya/kaynak.
- Hedef ogrenci.
- Durum.
- Ogretmen geri bildirimi.

Aksiyonlar:

- Odev olustur.
- Teslimi gor.
- Onayla.
- Geri gonder.
- Geri bildirim yaz.
- Veliye bildirim tetikle, kural dahilinde.

### 4.8 Odemeler Sayfasi

Route: `/payments`

Menu konumu: bottom nav 4. eleman.

Sayfa yapisi:

- Navbar.
- Gelir ozet kartlari.
- Filtreler.
- Odeme listesi.
- Odeme ekle butonu.

Ozet kartlari:

- Bu ay tahsil edilen.
- Bekleyen bakiye.
- Geciken odeme.
- Kismi odeme.

Odeme karti:

- Ogrenci.
- Ders/paket.
- Tutar.
- Para birimi.
- Tarih.
- Durum.
- Veliyle paylasiliyor mu.
- Veli "odedim" beyan durumu.

Aksiyonlar:

- Tahsil edildi isaretle.
- Kismi odeme gir.
- Bekliyor yap.
- Veliyle paylas.
- Beyani onayla/reddet.

### 4.9 Ogretmen Profili Sayfasi

Route: `/teacher-profile`

Menu konumu: bottom nav 5. eleman veya Profil menusu.

Sayfa yapisi:

- Profil hero.
- Dogrulama durumu.
- Branslar.
- Ders sekli.
- Sehir/ilce.
- Ucret.
- Uygunluk.
- Sertifikalar.
- Deneyim ve hakkinda.
- Profil onizleme.
- Uyelik/Premium bolumu.

Aksiyonlar:

- Profili duzenle.
- Sertifika ekle.
- Uygunluk saatlerini duzenle.
- Herkese acik profili onizle.
- Premium'a gec.

### 4.10 Mesajlar Sayfasi

Route: `/messages`

Faz: 2-3.

Sayfa yapisi:

- Konusma listesi.
- Filtre: ogrenci, veli, okunmamis.
- Konusma detayi.
- Dosya/link paylasimi, fazina gore.
- Sikayet/engelle aksiyonu.

### 4.11 Ilanlarim ve Yorumlar

Route: `/teacher/listings`, `/teacher/reviews`

Faz: 4.

Ilanlarim:

- Aktif/pasif ilanlar.
- Brans, ucret, konum, online/yuz yuze.
- One cikarma durumu.
- Talep sayisi.

Yorumlar:

- Ortalama puan.
- Alt kategori puanlari.
- Yorum listesi.
- Yanitla.
- Sikayet et.

### 4.12 Raporlar Sayfasi

Route: `/reports`

Faz: 5.

Sayfa yapisi:

- Aylik gelir grafigi.
- Ders sayisi.
- Aktif/pasif ogrenci.
- Bos zaman analizi.
- Ogrenci raporu olustur.
- PDF indir/paylas.

## 5. Veli Rolu Sayfa Tasarimi

Veli sayfalari okunabilir, grafik agirlikli ve guven hissi veren bir yapiya sahip olmalidir.

### 5.1 Veli Ana Sayfa

Route: `/parent`

Menu konumu: bottom nav 1. eleman.

Sayfa yapisi:

1. Login olan veli navbar'i.
2. Cocuk secici.
3. Haftalik KPI kartlari.
4. Calisma grafigi.
5. Yaklasan dersler.
6. Odev durumu.
7. Odeme ozeti.
8. Ogretmen notlari, gorunurse.
9. Bottom navigation.

KPI kartlari:

- Haftalik calisma suresi.
- Streak.
- Tamamlanan odev.
- Son deneme neti.
- Yaklasan ders sayisi.
- Bekleyen odeme.

Cocuk secici:

- Birden cok cocuk varsa yatay chip/kart.
- Secilen cocuga gore tum veriler yenilenir.

Gizlilik:

- Ogrenci veri paylasimini kapattiysa ilgili kartlarda "paylasilmiyor" durumu gosterilir.
- Deger 0 gibi yaniltici verilmemeli.

### 5.2 Cocuklar Sayfasi

Route: `/parent/children`

Menu konumu: bottom nav 2. eleman.

Sayfa yapisi:

- Navbar.
- Bagli cocuk listesi.
- Bag durumu.
- Cocuk ekle/bagla butonu.
- Bekleyen davetler.

Cocuk karti:

- Ad.
- Sinif/hedef.
- Bag durumu: onayli, bekliyor.
- Birincil veli bilgisi.
- Son aktivite.

Aksiyonlar:

- Detaya git.
- Davet kodu gir.
- Bag talebi gonder.
- Bildirim ayarlarini ac.

### 5.3 Cocuk Detayi Sayfasi

Route: `/parent/child-detail`

Sayfa yapisi:

- Cocuk profil hero.
- Sekmeler: Calisma, Dersler, Odevler, Odemeler, Notlar, Rapor.

Calisma sekmesi:

- Haftalik calisma.
- Ders dagilimi.
- Streak.
- Son deneme.
- Gizlilik maskesi.

Dersler sekmesi:

- Yaklasan dersler.
- Son dersler.
- Ogretmen bilgisi.
- Online/yuz yuze.

Odevler sekmesi:

- Bekleyen.
- Geciken.
- Teslim edilen.
- Geri bildirim.

Odemeler sekmesi:

- Bekleyen odemeler.
- Gecmis odemeler.
- Odemeyi veliye acma bayragina gore gorunum.
- "Odedim" beyan butonu.

Notlar sekmesi:

- Yalniz veliye gorunur ogretmen notlari.
- Private notlar asla gorunmez.

Rapor sekmesi:

- Haftalik/aylik rapor.
- Premium kilidi olabilir.

### 5.4 Bildirimler Sayfasi

Route: `/parent/notifications`

Menu konumu: bottom nav 3. eleman.

Sayfa yapisi:

- Navbar.
- Bildirim tercihleri.
- Kanal secimi.
- Son bildirimler.

Tercihler:

- Yaklasan ders.
- Ders tamamlandi.
- Yeni odev.
- Odev gecikti.
- Odeme guncellendi.
- Haftalik ozet.
- Baglanti/davet bildirimi.

Kanallar:

- Push.
- E-posta.
- SMS/WhatsApp, Premium veya sonraki faz.

### 5.5 Veli Profil Sayfasi

Route: `/parent/profile`

Menu konumu: bottom nav 4. eleman.

Sayfa yapisi:

- Profil hero.
- Abonelik durumu.
- Cocuk sayisi.
- Bildirim ozetleri.
- Menu listesi.

Menuler:

- Profil duzenle.
- Cocuklarim.
- Bildirim ayarlari.
- Aboneligim.
- Gizlilik ve guvenlik.
- Yardim ve destek.
- Cikis yap.

### 5.6 Ogretmen Mesajlari

Route: `/parent/messages`

Faz: 3.

Sayfa yapisi:

- Konusma listesi.
- Cocuk/ogretmen filtresi.
- Konusma detayi.
- Okundu bilgisi.
- Sikayet/engelle.

### 5.7 Haftalik Rapor

Route: `/parent/reports/weekly`

Faz: 5.

Sayfa yapisi:

- Cocuk secici.
- Haftalik ozet.
- Calisma grafigi.
- Odev tamamlama.
- Ders katilimi.
- Odeme ozeti.
- Ogretmen notlari.
- PDF/paylasim.

## 6. Eslestirme ve Kesif Sayfalari

Bu sayfalar Faz 4'te acilmalidir. O zamana kadar UI prototiplenebilir ama ana navigasyonda baskin olmamalidir.

### 6.1 Ogretmen Kesfi

Route: `/discover/teachers`

Kullanan rol: Ogrenci.

Sayfa yapisi:

- Navbar.
- Arama kutusu.
- Filtreler: brans, sehir/ilce, online/yuz yuze, ucret, puan, uygun saat.
- Ogretmen kartlari.
- Harita/listeli gorunum opsiyonu.

Ogretmen karti:

- Foto/ad.
- Brans.
- Puan/yorum sayisi.
- Ucret.
- Konum.
- Ders sekli.
- Dogrulama rozeti.
- Premium one cikarma etiketi.

### 6.2 Ogretmen Herkese Acik Profil

Route: `/teachers/:teacherId`

Sayfa yapisi:

- Profil hero.
- Dogrulama.
- Brans/ucret/lokasyon.
- Hakkinda.
- Deneyim/sertifika.
- Uygun saatler.
- Yorumlar.
- Talep gonder butonu.
- Mesaj gonder, izinli fazda.

### 6.3 Ders Talebi

Route: `/lesson-requests/new`

Sayfa yapisi:

- Secilen ogretmen ozeti.
- Ders/brans secimi.
- Hedef/aciklama.
- Tercih edilen zamanlar.
- Online/yuz yuze secimi.
- Mesaj.
- Talep gonder.

### 6.4 Ogrenci Ilani

Route: `/student/listings`

Faz: 4+.

Sayfa yapisi:

- Aradigi ders.
- Seviye/hedef.
- Konum/online.
- Butce araligi.
- Uygun saatler.
- Ilan durumu.

## 7. Uyelik, Paywall ve Reklam Sayfalari

### 7.1 Aboneligim

Route: `/membership`

Kullanan roller: Ogretmen, ogrenci, veli.

Sayfa yapisi:

- Mevcut paket.
- Paket karsilastirma.
- Free limit durumu.
- Premium avantajlari.
- Kampanya/referral.
- Odeme/satin alma aksiyonu, altyapi geldiginde.

Rol bazli avantajlar:

- Ogretmen: sinirsiz ogrenci, rapor, reklamsiz, one cikarma.
- Ogrenci: sinirsiz gecmis, hedef net, gelismis analiz.
- Veli: haftalik rapor, gelismis bildirim, reklamsiz.

### 7.2 Paywall Bottom-Sheet

Tetikleyiciler:

- Ogretmen ogrenci limitine ulasir.
- Ogrenci 30 gun ustu gecmis ister.
- Hedef net/puan takibi acilir.
- Veli detayli rapor veya gelismis bildirim ister.

Icerik:

- Kullanici neden burada durduruldu?
- Premium ile ne acilir?
- Fiyat/kampanya.
- Devam et/sonra.

### 7.3 Reklam Alanlari

Kurallar:

- Calisma kronometresi icinde reklam olmaz.
- Ders tamamlama gibi kritik is akisini bolmez.
- Liste aralarinda, rapor altinda veya sayfa sonlarinda olabilir.
- Premium kullanicida gizlenir.

## 8. Admin Sayfa Yapisi

Admin paneli web odakli tasarlanmalidir.

### 8.1 Admin Dashboard

Route: `/admin`

Icerik:

- Aktif kullanici.
- Yeni ogretmen.
- Bekleyen dogrulama.
- Sikayet sayisi.
- Premium kullanici.
- Sistem sagligi.

### 8.2 Ogretmen Dogrulama

Route: `/admin/teacher-verification`

Icerik:

- Bekleyen basvurular.
- Profil bilgisi.
- Sertifika/belge.
- Onayla/reddet.
- Not ekle.

### 8.3 Moderasyon

Route: `/admin/moderation`

Icerik:

- Yorum sikayetleri.
- Mesaj sikayetleri.
- Kullanici sikayetleri.
- Inceleme durumu.
- Aksiyon gecmisi.

### 8.4 Uyelik ve Kampanya Yonetimi

Route: `/admin/memberships`

Icerik:

- Paket listesi.
- Kullanici tier degistirme.
- Kampanya kodlari.
- Referral kayitlari.
- Premium sureleri.

### 8.5 Reklam Yonetimi

Route: `/admin/ads`

Icerik:

- Reklam yerlesimleri.
- Rol bazli gosterim.
- Frekans siniri.
- Kampanya durumu.

## 9. Sayfa Onceliklendirme

### Faz 0

- Karsilama.
- Rol secimi.
- Kayit.
- Giris.
- Hesap bilgileri.

### Faz 1 - Ogretmen Cekirdegi

- Ogretmen paneli.
- Ogretmen profili.
- Ogrenciler.
- Ogrenci detayi.
- Takvim.
- Ders detayi.
- Odev takip.
- Odemeler.

### Faz 2 - Ogrenci ve Veli

- Ogrenci calisma.
- Derslerim.
- Kronometre.
- Performans.
- Profil.
- Odevlerim.
- Hedeflerim.
- Veli ana sayfa.
- Cocuklar.
- Cocuk detayi.
- Veli profil.

### Faz 3 - Bagli Deneyim

- Mesajlar.
- Gelişimim.
- Veli gelismis bildirimler.
- Ogretmen-veli/ogrenci konusmalari.

### Faz 4 - Eslestirme

- Ogretmen kesfi.
- Herkese acik ogretmen profili.
- Ders talebi.
- Ilanlar.
- Yorumlar.
- Admin moderasyon.

### Faz 5 - Premium ve Analitik

- Aboneligim.
- Paywall.
- Raporlar.
- PDF rapor.
- Reklam yonetimi.
- One cikarma.

## 10. Tasarim Kontrol Listesi

Her sayfa icin kontrol:

- Navbar var mi?
- Login kullanici bilgisi dogru mu?
- Rol bazli bottom nav dogru aktif elemani gosteriyor mu?
- Ana CTA net mi?
- Bos durum tasarlandi mi?
- Hata/yukleniyor durumu var mi?
- Premium/Free farki gorunuyor mu?
- Gizlilik kurallari uygulanmis mi?
- Ogretmen dersi ile ogrencinin kendi dersi ayriliyor mu?
- Veliye private veri sizmiyor mu?
- Sayfa sonraki dogru aksiyona yonlendiriyor mu?

