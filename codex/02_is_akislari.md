---
title: "Is Akislari"
summary: "EgitimUssu icin kritik kullanici yolculuklari ve uc uca operasyon akislari"
tags: [is-akisi, journey, mvp]
authority: derived
updated: 2026-08-22
source: ../doc/ozel_ders_platformu_PRD.md
---

# Is Akislari

## 1. Urun Buyume Akisi

```
Ogretmen gunluk operasyon -> Ogrenci bireysel calisma -> Veli seffaf takip -> Eslesme/ilan -> Uyelik ve reklam geliri
```

Bu siralama urunun temel riskini azaltir: pazaryeri acildiginda iki tarafta da hazir kullanici ve davranis verisi bulunur.

## 2. Ogretmen MVP Akisi

### Amaç

Ogretmenin derslerini her gun platform uzerinden yonetmesini saglamak.

### Akis

1. Ogretmen kaydolur ve profilini doldurur.
2. Brans, sehir/ilce, ders sekli, ucret ve uygunluk bilgilerini girer.
3. Ogrenci ekler veya davetle mevcut ogrenci hesabina baglanir.
4. Takvimde ders planlar.
5. Ders zamani hatirlatma alir.
6. Dersi tamamlar; konu, katilim, not ve kaynak ekler.
7. Odev verir ve son tarih belirler.
8. Odeme durumunu manuel isaretler.
9. Dashboard uzerinden bugunku ders, bekleyen odev, geciken odeme ve gelir ozetini gorur.

### Kabul Kriterleri

- Ogretmen manuel ogrenci ekleyebilir.
- Takvimde ders cakismasi engellenir.
- Ders tamamlanmadan odeme/ders notu uretimi zorlanmaz.
- Ders sonrasi not, odev ve odeme birbirine bagli gorunur.

## 3. Ogrenci Bireysel Calisma Akisi

### Amaç

Ogrencinin ogretmensiz de uygulamada kalici deger bulmasi.

### Akis

1. Ogrenci kaydolur.
2. Hedef sinav/sinif/ana ders bilgilerini girer.
3. Ders ve konu katalogu olusturur veya varsayilan katalogu kullanir.
4. Gunluk/haftalik hedef belirler.
5. Konu secip kronometre baslatir.
6. Mola verir; mola suresi net calismaya eklenmez.
7. Seansi bitirir ve not ekler.
8. Deneme/test sonucu girer.
9. Performans sekmesinde net, konu dagilimi, streak ve haftalik hedef durumunu gorur.
10. Ileride ogretmen aramak isterse eslestirme akisine gecis yapar.

### Kabul Kriterleri

- Ogrenci ogretmen olmadan calisma seansi kaydedebilir.
- Deneme neti otomatik hesaplanir.
- Haftalik ozet ve streak davranisi anlasilir gorunur.
- Ogrenci gizlilik ayarindan hangi veriyi paylasacagini kontrol eder.

## 4. Veli Baglanma ve Takip Akisi

### Amaç

Velinin cocuk gelisimini guvenli, onayli ve sade bicimde izlemesi.

### Akis

1. Veli kaydolur.
2. Cocuk davet kodu/e-posta akisi ile baglanir.
3. Cocuk onayli bag olarak panele eklenir.
4. Veli ana sayfada cocuk secici ile ilgili cocugu secer.
5. Haftalik calisma, ders dagilimi, deneme ozeti ve streak gorur.
6. Ogretmen bagliysa son ders, odev, ogretmen notu ve odeme ozetini gorur.
7. Bildirim tercihlerini belirler.
8. Odeme icin "odedim" beyaninda bulunabilir; ogretmen teyit eder.

### Kabul Kriterleri

- Veli yalnizca onayli cocuk verisini gorur.
- Ogrencinin gizledigi veri maskelenir.
- Private ogretmen notu veliye acilmaz.
- Odeme beyanini tahsilat olarak degil mutabakat olarak ele alir.

## 5. Eslestirme Akisi

### Amaç

Yeterli aktif ogretmen ve ogrenci olustuktan sonra ders arayan ile ders sunani bulusturmak.

### Akis

1. Ogretmen herkese acik profilini tamamlar.
2. Admin veya sistem gerekli dogrulama rozetlerini uygular.
3. Ogrenci ders/konum/butce/ders sekli filtresiyle arama yapar.
4. Ogretmen kartlarini karsilastirir.
5. Profil detayinda puan, yorum, brans, fiyat ve uygunluk gorur.
6. Talep veya mesaj gonderir.
7. Ogretmen talebi kabul ederse ogretmen-ogrenci bagi kurulur.
8. Dersler ogrenci programina yansir.
9. Tamamlanan derslerden sonra ogrenci yorum daveti alir.

### Kabul Kriterleri

- Veli eslestirme yapan rol degildir.
- Yalnizca ders almis ogrenci yorum yapabilir.
- Ogretmen olumsuz yorumu silemez.
- Premium one cikarma organik sonuclari tamamen ezmeyecek sekilde etiketlenir.

## 6. Uyelik ve Gelir Akisi

### Amaç

Free kullanicilara temel deger verip Premium'a anlamli derinlik acmak.

### Akis

1. Kullanici Free baslar.
2. Uygulama kritik isleri yapmasina izin verir.
3. Limit veya gelismis analiz noktasinda Premium degeri gosterir.
4. Ilk ay ucretsiz veya referans kampanyasi uygulanir.
5. Premium kullanici reklamsiz, limitsiz veya gelismis ozelliklerle devam eder.

### Kabul Kriterleri

- Paywall cekirdek is akisini erken bogmaz.
- Free kullanici urunu deneyimler; Premium derinlik ve verimlilik satar.
- Reklam yerlesimi calisma/ders odagini bozmayacak alanlarda olur.

