---
title: "Paket ve Fiyatlandirma Dokumani"
summary: "Free/Premium paket mantigi, rol bazli limitler, kampanyalar ve gelir kalemleri"
tags: [fiyatlandirma, uyelik, premium, reklam]
authority: derived
updated: 2026-08-22
source: ../doc/ozel_ders_platformu_PRD.md
---

# Paket ve Fiyatlandirma Dokumani

## 1. Temel Gelir Modeli

EgitimUssu ders ucretini platform icinden tahsil etmez. Ozel ders odemeleri manuel takip edilir. Platform geliri su kaynaklardan gelir:

- Reklam: Free kullanicilar reklam gorur.
- Uyelik: Premium kullanicilar reklamsiz, daha az limitli ve gelismis ozellikli deneyim alir.
- One cikarma: Ogretmen profili veya ilan one cikarilabilir.
- Gelismis raporlama: PDF rapor, analiz, gelir ve gelisim ozetleri.
- Kampanyalar: ilk ay ucretsiz, arkadasini getir -> 1 ay ucretsiz.

## 2. Fiyatlandirma Ilkeleri

1. Free paket gercek deger vermelidir; aksi halde buyume motoru calismaz.
2. Premium, temel is akisini kilitlemek yerine zaman kazandiran, derin analiz sunan ve limit kaldiran deger satmalidir.
3. Ogretmen paketi operasyon verimliligine; ogrenci paketi motivasyon/analize; veli paketi rapor/bildirime odaklanmalidir.
4. Eslestirme one cikarma seffaf etiketlenmelidir.
5. Ilk fiyatlar lansman donemi icin dusuk tutulup dogrulama sonrasi revize edilmelidir.

## 3. Paketler

### 3.1 Ogretmen Free

- Reklamli kullanim.
- 5 aktif/arsivli ogrenci bagi limiti.
- Temel profil.
- Takvim, ders oturumu, not/odev ve manuel odeme takibi.
- Temel dashboard.

### 3.2 Ogretmen Premium

- Reklamsiz kullanim.
- Sinirsiz ogrenci bagi.
- Gelir analizi ve geciken odeme raporlari.
- PDF ogrenci raporu.
- Bos zaman/musaitlik analizi.
- Profil one cikarma kredisi veya indirimli one cikarma.
- WhatsApp/SMS hatirlatma entegrasyonu, entegrasyon maliyetine gore kota ile.

### 3.3 Ogrenci Free

- Reklamli kullanim.
- Temel kronometre.
- Temel deneme/test girisi.
- Son 30 gun gecmis.
- Temel streak ve basarimlar.
- Sinirli analiz.

### 3.4 Ogrenci Premium

- Reklamsiz kullanim.
- Sinirsiz gecmis.
- Haftalik/aylik analiz.
- Hedef net/puan takibi.
- Konu zayiflik analizi ve gelisim onerileri.
- Gelismis rozet/motivasyon ozellikleri.
- Ogretmen/veli ile detayli veri paylasim kontrolleri.

### 3.5 Veli Free

- Reklamli kullanim.
- Cocuk icin temel haftalik ozet.
- Sinirli bildirim.
- Ogretmen bagliysa temel ders/odev/odeme gorunumu.

### 3.6 Veli Premium

- Reklamsiz kullanim.
- Detayli gelisim grafikleri.
- Haftalik rapor.
- Gelismis bildirimler.
- Birden cok cocuk icin karsilastirmali ozet.
- Odeme/odev/ders kacirma bildirimleri.

## 4. Onerilen Lansman Fiyat Hipotezi

Bu fiyatlar kesin fiyat degil, test edilmesi gereken hipotezdir. Turkiye pazari, sehir ve hedef kitleye gore fiyat hassasiyeti yuksektir.

| Paket | Aylik Hipotez | Yillik Hipotez | Not |
|---|---:|---:|---|
| Ogretmen Premium | 249-399 TL | 2.490-3.990 TL | En yuksek odeme istegi ogretmende beklenir |
| Ogrenci Premium | 99-199 TL | 990-1.990 TL | Analiz ve motivasyon degeriyle satilmali |
| Veli Premium | 99-199 TL | 990-1.990 TL | Rapor ve bildirim odakli |
| Aile Paketi | 199-299 TL | 1.990-2.990 TL | 1 veli + 1-2 ogrenci |
| One Cikarma | 99-299 TL/kredi | Paketli | Faz 4 sonrasi |

## 5. Kampanyalar

| Kampanya | Hedef | Kural |
|---|---|---|
| Ilk ay ucretsiz | Yeni kullanici | Premium deneme; iptal kolay olmali |
| Arkadasini getir | Tum roller | Davet eden ve gelen kullaniciya 1 ay ucretsiz |
| Beta ogretmen | Ilk ogretmen havuzu | 3-6 ay indirimli veya lifetime avantaj |
| Okul/dershane toplu paket | B2B opsiyon | Sonraki faz; MVP kapsami disi |

## 6. Paywall Tetikleyicileri

| Rol | Paywall Noktasi | Uygun Mesaj |
|---|---|---|
| Ogretmen | 5 ogrenci limitine ulasma | "Daha fazla ogrenci yonetmek icin Premium'a gec." |
| Ogretmen | PDF rapor/g gelir analizi | "Aylik raporlar ve gelir ozeti Premium'da." |
| Ogrenci | 30 gun ustu gecmis | "Tum calisma gecmisini ac." |
| Ogrenci | hedef net/puan | "Hedefe gore takip Premium'da." |
| Veli | detayli rapor | "Haftalik gelisim raporu Premium'da." |
| Veli | gelismis bildirim | "Odev, ders ve odeme bildirimlerini ac." |

## 7. Riskler

- Cekirdek kullanim cok erken paywall'a takilirsa elde tutma duser.
- Reklam yogunlugu ogrenci calisma deneyimini bozarsa urun guveni azalir.
- Ogretmen one cikarma adaletsiz algilanirsa organik kalite zarar gorur.
- Veli Premium degeri yeterince somut olmazsa donusum dusuk kalir.

## 8. Olculmesi Gereken Metrikler

- Free -> Premium donusum orani.
- Paywall goruntuleme -> satin alma orani.
- Paket iptal nedeni.
- Ogretmen basina ogrenci limitine ulasma orani.
- Premium kullanicida 30 gun elde tutma.
- Reklam gelirinin aktif kullanici basina katkisi.

