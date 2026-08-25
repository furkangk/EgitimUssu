---
title: "Rol Dokumani"
summary: "EgitimUssu rollerinin yetenekleri, sinirlari ve rol bazli urun degeri"
tags: [rol, ogretmen, ogrenci, veli, admin]
authority: derived
updated: 2026-08-22
source: ../doc/ozel_ders_platformu_PRD.md
---

# Rol Dokumani

## 1. Rol Stratejisi

Platform uc ana kullanici rolune dayanir: ogretmen, ogrenci ve veli. Admin rolu operasyon, dogrulama ve moderasyon icindir. Urun degeri, rollerin tek tek kullanim degeri bulmasi ve sonra birbirine baglanmasi uzerine kurulur.

Temel kural: eslestirme en basta degil, yonetim ve bireysel calisma degeri oturduktan sonra acilir. Boylece ilk gun bos pazaryeri problemi azalir.

## 2. Ogretmen

Ogretmen, platformun ilk cekirdek kullanicisidir. Amaci kendi ozel ders operasyonunu tek yerden yurutmektir.

### Temel Deger

- Ogrencilerini kaydeder ve takip eder.
- Takvimde tekil veya tekrarlayan ders planlar.
- Ders oturumunu tamamlar, konu/not/kaynak ekler.
- Odev verir, teslim durumunu takip eder.
- Manuel odeme ve bakiye takibi yapar.
- Ileride ilan verir, yorum alir ve profilini one cikarir.

### Sinirlar

- Platform uzerinden para tahsil etmez; yalnizca odeme durumunu kaydeder.
- Baska ogretmenin ogrencisini goremez.
- Ogrencinin gizledigi bireysel calisma verisini goremez.
- Aldigi yorumu silemez; yalnizca yanitlayabilir.
- Kendini dogrulanmis yapamaz; dogrulama admin tarafindadir.

### Basari Olcutleri

- Haftalik aktif ogretmen orani.
- Ogretmen basina aktif ogrenci bag sayisi.
- Planlanan/tamamlanan ders sayisi.
- Ders sonrasi not/odev giris orani.
- Manuel odeme takibi kullanim orani.

## 3. Ogrenci

Ogrenci, platforma ogretmensiz de girebilen buyume motorudur. Deger onerisi bireysel calisma, hedef ve gelisim takibidir.

### Temel Deger

- Kendi ders programini ve calisma planini olusturur.
- Kronometre ile konu bazli calisma seansi kaydeder.
- Deneme/test sonucu girer; net ve konu performansi gorur.
- Gunluk hedef, streak ve basarimlarla motive olur.
- Ogretmenle baglanirsa dersleri, odevleri ve notlari takip eder.
- Ileride aradigi ders icin ilan verebilir ve ders aldigi ogretmeni yorumlayabilir.

### Sinirlar

- Ogretmenin dersini degistiremez; yalnizca erteleme talebi gonderebilir.
- Kendine ogretmen odevini veremez veya silemez.
- Ders almadigi ogretmene yorum yapamaz.
- Baska ogrencinin verisini goremez.
- Ogretmenin private notunu goremez.

### Basari Olcutleri

- Haftalik calisma seansi sayisi.
- Gunluk hedef tamamlama orani.
- Deneme/test giris sikligi.
- 7 gun/30 gun elde tutma.
- Ogretmensiz ogrenciden eslestirme talebine donusum.

## 4. Veli

Veli, esas olarak okuyan ve takip eden roldur. Kendi basina cok veri uretmez; cocuk ve ogretmen verisini guvenli sekilde tuketir.

### Temel Deger

- Cocuklarini secer ve her biri icin haftalik ozet gorur.
- Calisma suresi, ders dagilimi, deneme performansi ve streak izler.
- Ogretmen bagliysa son ders, odev, ogretmen notu ve odeme ozetini gorur.
- Bildirim tercihlerini yonetir.
- Odeme icin "odedim" beyaninda bulunabilir; teyit ogretmendedir.

### Sinirlar

- Ders ekleyemez, degistiremez veya iptal edemez.
- Cocuk adina sayac baslatamaz veya test sonucu degistiremez.
- Cocuk tarafindan gizlenmis veriyi goremez.
- Platform uzerinden odeme yapmaz.
- Ogretmene yorum/puan vermez.
- Esleştirme tarafinda ogretmen arama/ilan yetkisi yoktur.

### Basari Olcutleri

- Bagli cocuk sayisi.
- Haftalik panel goruntuleme.
- Bildirim tercihlerini aktif etme orani.
- Odeme beyan/teyit akisi kullanimi.
- Premium veli donusumu.

## 5. Admin

Admin, kullanici deneyimini dogrudan yasayan rol degil; platform guveni ve operasyonundan sorumludur.

### Temel Deger

- Ogretmen profil dogrulama.
- Sikayet, yorum ve mesaj moderasyonu.
- Kullanici/icerik denetimi.
- Uyelik, kampanya ve reklam kurallarini takip.
- Destek taleplerini yonetme.

### Sinirlar

- Kullanici verisine gereksiz erisim yapmaz; KVKK ve rol izolasyonu korunur.
- Odeme tahsilati yapmaz; platform modeli manuel odeme kaydi ve uyelik geliridir.

## 6. Rol-Yetenek Matrisi

| Yetenek | Ogretmen | Ogrenci | Veli | Admin |
|---|:---:|:---:|:---:|:---:|
| Profil ve hesap | Evet | Evet | Evet | Evet |
| Takvim yonetimi | Yonetir | Kendi programini yonetir | Gorur | Denetler |
| Ders oturumu | Isler | Gorur | Gorur | Denetler |
| Odev | Verir/takip eder | Yukler/tamamlar | Gorur | Denetler |
| Bireysel calisma | Hayir | Evet | Gorur | Denetler |
| Odeme takibi | Kaydeder | Gorur, varsa | Gorur/beyan eder | Denetler |
| Mesajlasma | Ogrenci/veli ile | Ogretmen ile | Ogretmen ile | Moderasyon |
| Ilan/eslestirme | Ders sunar | Ders arar | Hayir | Moderasyon |
| Yorum/puan | Alir/yanitlar | Verir | Hayir | Moderasyon |
| Uyelik | Evet | Evet | Evet | Yonetir |

