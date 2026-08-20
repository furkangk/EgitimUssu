---
title: "Öğretmen Rolü — Fonksiyonel Doküman (ARŞİV)"
summary: "ARŞİV (tarihî): güncel otorite roles/+modules/ — PRD v2.0 öğretmen rolü fonksiyonel yetenek ve kullanım akışı detaylandırması v1.0"
tags: [arsiv, ogretmen, fonksiyonel-dokuman]
authority: archive
updated: 2026-07-17
---

# Öğretmen Rolü — Fonksiyonel Yetenek ve Kullanım Akışı Dokümanı

> ⚠️ **ARŞİV (2026-08-19):** Bu doküman tarihîdir. Geçerli otorite `doc/roles/` + `doc/modules/`'tedir. Buradaki bilgi yalnızca geçmiş referans içindir; çelişkide roles/modules esastır.

**Ürün:** Özel Ders Yönetim ve Eşleştirme Platformu
**Kaynak:** `ozel_ders_platformu_PRD_v2.docx` (v2.0, Nisan 2025)
**Bu doküman:** v1.0 — Öğretmen rolü detaylandırması
**Tarih:** 17 Temmuz 2026

---

## 0. Bu Doküman Nasıl Okunur

Bu doküman, PRD v2.0'daki öğretmen ile ilgili tüm dağınık maddeleri tek bir rol dokümanında toplar ve geliştirmeye hazır seviyeye çıkarır.

Her madde bir kaynak etiketi taşır:

| Etiket | Anlamı |
|---|---|
| **[PRD]** | PRD v2.0'da açıkça yazılmış |
| **[TÜRETİLMİŞ]** | PRD'de ima edilmiş, burada detaylandırıldı |
| **[YENİ]** | PRD'de yok — bu dokümanda önerilen ekleme. Onayınız gerekiyor. |

> **Önemli:** Sorunuzda örnek verdiğiniz **"tatil ekleyebilir"** özelliği PRD v2.0'da **bulunmuyor**. Bölüm 7'de tam akışıyla **[YENİ]** olarak tasarlandı. Aynı şekilde ders erteleme, öğrenci arşivleme, toplu işlemler ve ders iptal politikası da PRD'de eksik — hepsi Bölüm 15'te listelendi.

---

## 1. Rol Tanımı ve Stratejik Konum

### 1.1 Öğretmen Kimdir

**[PRD]** Özel ders veren kişi. **Sistemin ana kullanıcısıdır.**

**[PRD]** PRD'nin temel tezi şudur: ürün, öğretmenin sadece öğrenci bulduğu bir pazar yeri değil; **derslerini düzenli yönettiği bir günlük çalışma aracıdır.** Öğretmen bu ürüne öğrenci bulmak için değil, **derslerini yönetmek için** her gün girer. Öğrenci bulma (eşleştirme) Faz 4'e kadar hiç açılmaz.

Bu, öğretmen rolünün tüm tasarımını belirler:
- Öğretmenin **günlük** dokunduğu ekran = Takvim (M04). Ana ekran budur.
- Öğretmenin **ders başına** dokunduğu ekran = Ders Oturumu (M05).
- Öğretmenin **haftalık/aylık** dokunduğu ekran = Ödeme Takibi (M07) ve Raporlar (M14).

### 1.2 Öğretmenin Beklentileri (PRD Bölüm 4.1)

**[PRD]**
1. Yeni öğrenci bulmak
2. Ders saatlerini takip etmek
3. Öğrencilerle iletişimi düzenli tutmak
4. Verdiği dersleri, ödevleri ve notları kayıt altına almak
5. Ödeme durumunu görmek

### 1.3 Öğretmenin Çözdüğü Problemler (PRD Bölüm 3)

**[PRD]**
| Bugünkü Durum | Platformdaki Karşılığı |
|---|---|
| Öğrenci bulma pahalı/verimsiz | M12 Eşleştirme (Faz 4) |
| İletişim ve düzen takibi dağınık | M04 Takvim + M11 Bildirim |
| Dersler takvim/Excel/not defteri ile yönetiliyor | M04 + M05 |
| Ödev, not, gelişim sistematik takip edilmiyor | M06 + M10 |
| Veli şeffaf dahil edilemiyor | M09 Veli Paneli |
| Ödeme elden, manuel takip gerekiyor | M07 Manuel Ödeme Takibi |

---

## 2. Öğretmenin Sahip Olduğu Modüller

**[PRD]** PRD Bölüm 6'daki 15 modülden öğretmeni ilgilendirenler:

| Modül | Ad | Öğretmenin Rolü | Faz |
|---|---|---|---|
| M01 | Kullanıcı ve Rol Yönetimi | Kullanıcı | Faz 0 |
| M02 | Öğretmen Profili | **Birincil sahip** | Faz 1 |
| M03 | Öğrenci Profili | **Oluşturucu** (öğrenci de kendi oluşturabilir) | Faz 1 |
| M04 | Takvim ve Ders Planlama | **Birincil sahip** | Faz 1 |
| M05 | Ders Oturumu Yönetimi | **Birincil sahip** | Faz 1 |
| M06 | Not ve Ödev Yönetimi | **Birincil sahip** (öğrenci tüketici) | Faz 1 |
| M07 | Manuel Ödeme Takibi | **Birincil sahip** | Faz 1 |
| M08 | Öğrenci Bireysel Çalışma | **İzleyici** (öğrenci izin verirse) | Faz 2 |
| M09 | Veli Paneli | **Veri kaynağı** (öğretmen verisi buraya akar) | Faz 2–3 |
| M10 | Öğrenci Gelişim Takibi | **Birincil sahip** | Faz 3 |
| M11 | Bildirim ve Hatırlatma | Alıcı + tetikleyici | Faz 3 |
| M12 | Eşleştirme ve Keşif | **Listelenen taraf** | Faz 4 |
| M13 | Puanlama ve Yorum | **Puanlanan taraf** (yanıt verebilir) | Faz 4 |
| M14 | Raporlama ve Analiz | **Birincil sahip** | Faz 5 |
| M15 | Ayarlar ve Güvenlik | Kullanıcı | Faz 0+ |

**Öğretmenin hiç dokunmadığı modül yok** — 15/15. Rol, sistemin merkezidir.

---

## 3. Öğretmen Yaşam Döngüsü (Üst Seviye Akış)

```
KEŞİF          KURULUM              GÜNLÜK DÖNGÜ                 DÖNEMSEL DÖNGÜ
─────          ───────              ────────────                 ──────────────
Uygulamayı  →  Kayıt ol         →   ┌─ Takvimi aç            →  Ay sonu:
duyar          Rol: Öğretmen        │  Bugünün derslerini gör    ├─ Gelir özeti (P)
               Profil doldur        │  Derse gir                 ├─ Geciken ödemeler
               (M02)                │  Ders oturumunu tamamla    ├─ PDF rapor (P)
               ↓                    │  (konu, süre, katılım)     └─ Öğrenci değerlendirme
               Öğrenci ekle         │  Not gir                       ↓
               (M03)                │  Ödev ver                  Yeni dönem planlaması
               ↓                    │  Ödeme işaretle            (tatil, program değişikliği)
               Ders planla          │  ↓
               (M04)                └──┘ (ertesi gün tekrar)
               ↓                         ↓
               Fiyat tanımla        Haftalık:
               (M07)                ├─ Haftalık görünüm kontrolü
                                    ├─ Ödev durumu kontrolü
                                    └─ Ödeme durumu kontrolü
```

### 3.1 Ana Kullanım Sıklığı Haritası

**[TÜRETİLMİŞ]** Bu, hangi ekranın kaç tıkla erişilebilir olması gerektiğini belirler:

| Sıklık | Aksiyon | Erişim Hedefi |
|---|---|---|
| Günde 3–10 kez | Takvimi görme | Uygulama açılışı = Takvim (0 tık) |
| Günde 1–5 kez | Ders oturumu tamamlama | Takvimden 1 tık |
| Günde 1–5 kez | Ders notu girme | Ders tamamlamadan sonra aynı akış içinde |
| Günde 0–3 kez | Ödev verme | Ders tamamlama akışı içinde |
| Haftada 1–3 kez | Ders ekleme | Takvimden 1 tık (+ butonu) |
| Haftada 1 kez | Ödeme işaretleme | Alt menüden 1 tık |
| Ayda 1–2 kez | Öğrenci ekleme | Öğrenciler ekranından 1 tık |
| Ayda 1 kez | Gelir raporu | Raporlar ekranı |
| 3 ayda 1 | Profil güncelleme | Ayarlar altında |

---

## 4. Öğretmen Yetenek Matrisi (Tam Liste)

Aşağıda öğretmenin yapabildiği **her şey** modül modül, CRUD bazında listelenmiştir.

### M01 — Hesap ve Rol

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-01.1 | Öğretmen olarak kayıt olabilir | [PRD] | 0 |
| T-01.2 | E-posta/telefon ile giriş yapabilir | [PRD] | 0 |
| T-01.3 | Şifresini yenileyebilir / sıfırlayabilir | [PRD] | 0 |
| T-01.4 | Rolüne özel ekranları görür (rol bazlı erişim kontrolü) | [PRD] | 0 |
| T-01.5 | Profil doğrulama talebi gönderebilir (rozet için) | [PRD] | 0/4 |
| T-01.6 | Hesabını kapatabilir ve verisini sildirebilir (KVKK) | [PRD] | 0 |
| T-01.7 | Free planda öğrenci limiti vardır (maks. 5–10); Premium'da sınırsız | [PRD] | 5 |
| T-01.8 | **Rol değiştiremez** — öğretmen hesabı öğrenci hesabına dönüşemez | [TÜRETİLMİŞ] | 0 |

### M02 — Profil

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-02.1 | Profil oluşturabilir ve düzenleyebilir | [PRD] | 1 |
| T-02.2 | Ad soyad girebilir | [PRD] | 1 |
| T-02.3 | Branş(lar) tanımlayabilir | [PRD] | 1 |
| T-02.4 | Şehir/ilçe belirtebilir | [PRD] | 1 |
| T-02.5 | Ders verme şeklini seçebilir (yüz yüze / online / her ikisi) | [PRD] | 1 |
| T-02.6 | Deneyim yılı girebilir | [PRD] | 1 |
| T-02.7 | Eğitim seviyesi girebilir | [PRD] | 1 |
| T-02.8 | Fiyat bilgisi tanımlayabilir | [PRD] | 1 |
| T-02.9 | Uygun saatlerini (müsaitlik) tanımlayabilir | [PRD] | 1 |
| T-02.10 | Serbest metin açıklama yazabilir | [PRD] | 1 |
| T-02.11 | Profil fotoğrafı yükleyebilir | [PRD] | 1 |
| T-02.12 | Sertifika ve deneyimlerini ekleyebilir | [PRD] | 1 |
| T-02.13 | Doğrulama durumunu görebilir | [PRD] | 1 |
| T-02.14 | Faz 4'te profili herkese açık listelenir | [PRD] | 4 |
| T-02.15 | Premium ile profilini öne çıkarabilir | [PRD] | 5 |

### M03 — Öğrenci Yönetimi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-03.1 | **Manuel olarak öğrenci ekleyebilir** | [PRD] | 1 |
| T-03.2 | Öğrenci profili oluşturabilir (öğrenci uygulamaya hiç girmese bile) | [PRD] | 1 |
| T-03.3 | Öğrencinin ad soyadını girebilir | [PRD] | 1 |
| T-03.4 | Sınıf seviyesi girebilir | [PRD] | 1 |
| T-03.5 | Ders aldığı branşları tanımlayabilir | [PRD] | 1 |
| T-03.6 | İletişim bilgisi girebilir | [PRD] | 1 |
| T-03.7 | Öğrenciye bağlı veliyi görebilir/tanımlayabilir | [PRD] | 1/2 |
| T-03.8 | Öğrenci hakkında özel not tutabilir | [PRD] | 1 |
| T-03.9 | Öğrencinin aktif derslerini görebilir | [PRD] | 1 |
| T-03.10 | Öğrencinin hedef/seviye bilgisini tanımlayabilir | [PRD] | 1 |
| T-03.11 | Öğrenci listesini görüntüleyebilir | [TÜRETİLMİŞ] | 1 |
| T-03.12 | Öğrenci bilgilerini düzenleyebilir | [TÜRETİLMİŞ] | 1 |
| T-03.13 | Öğrenciyi silebilir | [TÜRETİLMİŞ] | 1 |
| T-03.14 | **Öğrenciyi arşivleyebilir** (silmeden pasife alma) | **[YENİ]** | 1 |
| T-03.15 | Var olan (kendi kaydolmuş) öğrenciyi davet edip bağlayabilir | **[YENİ]** | 2 |
| T-03.16 | Free planda maks. 5–10 aktif öğrenci ekleyebilir | [PRD] | 5 |

### M04 — Takvim ve Ders Planlama

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-04.1 | **Ders ekleyebilir** | [PRD] | 1 |
| T-04.2 | **Ders değiştirebilir (düzenleyebilir)** | [PRD] | 1 |
| T-04.3 | **Ders iptal edebilir** | [PRD] | 1 |
| T-04.4 | **Tekrar eden (recurring) ders oluşturabilir** | [PRD] | 1 |
| T-04.5 | Haftalık görünümde takvimi görebilir | [PRD] | 1 |
| T-04.6 | Aylık görünümde takvimi görebilir | [PRD] | 1 |
| T-04.7 | Ders çakışması kontrolü ile uyarı alır | [PRD] | 1 |
| T-04.8 | Ders için hatırlatma oluşturabilir | [PRD] | 1 |
| T-04.9 | Günlük görünüm (bugünün dersleri) | [TÜRETİLMİŞ] | 1 |
| T-04.10 | **Ders erteleyebilir** (tarih/saat değiştirme, öğrenciye bildirim) | **[YENİ]** | 1 |
| T-04.11 | **Tatil / müsait değil bloğu ekleyebilir** | **[YENİ]** | 1 |
| T-04.12 | Tekrar eden dersin tek bir oturumunu iptal edebilir (seriyi bozmadan) | **[YENİ]** | 1 |
| T-04.13 | Tekrar eden dersin tüm serisini sonlandırabilir | **[YENİ]** | 1 |
| T-04.14 | Ders yerini/linkini (adres veya online link) belirtebilir | **[YENİ]** | 1 |
| T-04.15 | Boş zaman analizi görebilir (Premium) | [PRD] | 5 |

### M05 — Ders Oturumu Yönetimi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-05.1 | Her dersi bir oturum kaydı olarak tutar | [PRD] | 1 |
| T-05.2 | Oturumun tarih/saat/süre bilgisini kaydeder | [PRD] | 1 |
| T-05.3 | İşlenen konuyu girebilir | [PRD] | 1 |
| T-05.4 | İşlenen içeriği detaylandırabilir | [PRD] | 1 |
| T-05.5 | Ders durumunu belirleyebilir (planlandı/tamamlandı/iptal…) | [PRD] | 1 |
| T-05.6 | Öğretmen notu yazabilir | [PRD] | 1 |
| T-05.7 | Öğrenci katılım durumunu işaretleyebilir | [PRD] | 1 |
| T-05.8 | Ders tamamlanınca not girebilir ve ödev verebilir | [PRD] | 1 |
| T-05.9 | Geçmiş ders oturumlarını listeleyebilir | [TÜRETİLMİŞ] | 1 |
| T-05.10 | Gerçekleşen süreyi planlanandan farklı girebilir | **[YENİ]** | 1 |
| T-05.11 | Öğrenci gelmezse "gelmedi" işaretleyip ücretlendirme kararı verebilir | **[YENİ]** | 1 |

### M06 — Not ve Ödev Yönetimi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-06.1 | Ders notu ekleyebilir | [PRD] | 1 |
| T-06.2 | Ödev ekleyebilir | [PRD] | 1 |
| T-06.3 | Ödev son tarihi belirleyebilir | [PRD] | 1 |
| T-06.4 | Ödev durumunu takip edebilir (tamamlandı / bekliyor) | [PRD] | 1 |
| T-06.5 | Dosya veya görsel ekleyebilir | [PRD] | 1 |
| T-06.6 | Ödevi düzenleyebilir / silebilir | [TÜRETİLMİŞ] | 1 |
| T-06.7 | Ödevi onaylayabilir / geri gönderebilir | **[YENİ]** | 1 |
| T-06.8 | Ödeve geri bildirim yazabilir | **[YENİ]** | 1 |
| T-06.9 | Aynı ödevi birden fazla öğrenciye atayabilir | **[YENİ]** | 3 |

### M07 — Manuel Ödeme Takibi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-07.1 | Ders ücreti tanımlayabilir | [PRD] | 1 |
| T-07.2 | "Tahsil edildi" işaretleyebilir | [PRD] | 1 |
| T-07.3 | "Bekliyor" işaretleyebilir | [PRD] | 1 |
| T-07.4 | "Kısmi ödendi" işaretleyebilir | [PRD] | 1 |
| T-07.5 | Öğrenci bazlı bakiye görebilir | [PRD] | 1 |
| T-07.6 | Aylık gelir özeti görebilir (Premium) | [PRD] | 5 |
| T-07.7 | Geciken ödemeleri listeleyebilir (Premium) | [PRD] | 5 |
| T-07.8 | Otomatik ödeme hesaplama (Premium) | [PRD] | 5 |
| T-07.9 | **Platform üzerinden para tahsil EDEMEZ** — sistem sadece kayıt tutar | [PRD] | — |
| T-07.10 | Öğrenci bazlı farklı fiyat tanımlayabilir | **[YENİ]** | 1 |
| T-07.11 | Ödeme geçmişini (kim ne zaman ne kadar ödedi) görebilir | **[YENİ]** | 3 |

### M08 — Öğrencinin Bireysel Çalışması (İzleyici Rolü)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-08.1 | Öğrenci izin verirse bireysel çalışma verisini görebilir | [PRD] | 2 |
| T-08.2 | Öğrenci belirli verileri gizlerse öğretmen **göremez** | [PRD] | 2 |
| T-08.3 | Öğrencinin çalışma süresi / konu dağılımını izleyebilir | [TÜRETİLMİŞ] | 2 |
| T-08.4 | Öğrencinin test/net performansını izleyebilir | [TÜRETİLMİŞ] | 2 |
| T-08.5 | **Öğrencinin sayacını başlatamaz/durduramaz** — o veri öğrenciye aittir | [TÜRETİLMİŞ] | 2 |

### M09 — Veli Paneli (Veri Kaynağı Rolü)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-09.1 | Girdiği ders özeti veliye yansır | [PRD] | 3 |
| T-09.2 | Verdiği ödevler veliye yansır | [PRD] | 3 |
| T-09.3 | Öğretmen notları veliye yansır | [PRD] | 3 |
| T-09.4 | Ödeme özeti veliye yansır | [PRD] | 3 |
| T-09.5 | Veliye mesaj gönderebilir | [PRD] | 3 |
| T-09.6 | Free planda veli görünümü sınırlı, Premium'da detaylı | [PRD] | 5 |
| T-09.7 | Hangi notun veliyle paylaşılacağını seçebilir (özel not / paylaşılan not) | **[YENİ]** | 3 |

### M10 — Öğrenci Gelişim Takibi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-10.1 | Konu kazanım durumunu izleyebilir | [PRD] | 3 |
| T-10.2 | Deneme/test performansını zaman serisi olarak görebilir | [PRD] | 3 |
| T-10.3 | Eksik ve güçlü konuları görebilir | [PRD] | 3 |
| T-10.4 | Hedef puan / seviye tanımlayabilir ve takip edebilir | [PRD] | 3 |
| T-10.5 | Öğrenci değerlendirme notu yazabilir | [PRD] | 3 |

### M11 — Bildirimler

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-11.1 | Yaklaşan ders hatırlatması alır (Kritik öncelik) | [PRD] | 1 |
| T-11.2 | Ders sonrası not girme hatırlatması alır (Yüksek) | [PRD] | 3 |
| T-11.3 | Ödeme gecikmesi bildirimi alır (Yüksek) | [PRD] | 3 |
| T-11.4 | Haftalık özet bildirimi alır (Orta) | [PRD] | 3 |
| T-11.5 | Ders & ödev hatırlatmalarını **öğrencilere gönderebilir** (Premium) | [PRD] | 5 |
| T-11.6 | WhatsApp/SMS hatırlatma gönderebilir (Premium) | [PRD] | 5 |
| T-11.7 | Bildirim tercihlerini yönetebilir | [TÜRETİLMİŞ] | 0 |

### M12 — Eşleştirme (Listelenen Taraf)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-12.1 | Profili öğrenci aramalarında listelenir | [PRD] | 4 |
| T-12.2 | Branş/şehir/ücret/ders şekli/uygun saat filtrelerinde çıkar | [PRD] | 4 |
| T-12.3 | Herkese açık profil sayfası olur (puan, yorumlar, geçmiş) | [PRD] | 4 |
| T-12.4 | Öğrencilerden talep/mesaj alabilir | [PRD] | 4 |
| T-12.5 | Talebi kabul/red edebilir | [TÜRETİLMİŞ] | 4 |
| T-12.6 | Doğrulama rozeti kazanabilir | [PRD] | 4 |
| T-12.7 | Premium ile profilini öne çıkarabilir | [PRD] | 5 |
| T-12.8 | Öğrenci bulma kredisi satın alabilir | [PRD] | 4+ |

### M13 — Puanlama (Puanlanan Taraf)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-13.1 | Yalnızca kendisinden **ders almış** öğrenciler yorum yapabilir | [PRD] | 4 |
| T-13.2 | 1–5 yıldız genel puan alır | [PRD] | 4 |
| T-13.3 | Alt kategorilerde puanlanır: anlatım netliği, dakiklik, sabır, ders hazırlığı | [PRD] | 4 |
| T-13.4 | Profilinde ortalama puan + yorum sayısı görünür | [PRD] | 4 |
| T-13.5 | **Yorumlara yanıt verebilir** | [PRD] | 4 |
| T-13.6 | **Olumsuz yorumu gizleyemez / silemez** — sadece yanıtlayabilir | [PRD] | 4 |
| T-13.7 | Şüpheli yorumu admin'e bildirebilir | [PRD] | 4 |
| T-13.8 | Faz 1–2'de öğrenciden **özel geri bildirim** alır (sadece öğretmen görür) | [PRD] | 1 |

### M14 — Raporlama (Premium)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-14.1 | Aylık ders sayısı ve gelir özeti | [PRD] | 5 |
| T-14.2 | Aktif / pasif öğrenci sayısı | [PRD] | 5 |
| T-14.3 | Boş zaman analizi ("ne zaman müsaitim") | [PRD] | 5 |
| T-14.4 | PDF öğrenci raporu oluşturma | [PRD] | 5 |
| T-14.5 | Öğrenci performans analizi | [PRD] | 5 |

### M15 — Ayarlar ve Güvenlik

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| T-15.1 | Şifre değiştirme | [PRD] | 0 |
| T-15.2 | Bildirim tercihleri | [PRD] | 0 |
| T-15.3 | Gizlilik ayarları | [PRD] | 0 |
| T-15.4 | Hesap kapatma / veri silme (KVKK) | [PRD] | 0 |
| T-15.5 | Abonelik yönetimi | [PRD] | 5 |

---

## 5. Ekran Haritası (Bilgi Mimarisi)

**[TÜRETİLMİŞ]** — PRD ekran listesi vermiyor; yetenek listesinden türetildi.

```
ÖĞRETMEN UYGULAMASI
│
├── [Alt Sekme 1] 📅 TAKVİM  ← AÇILIŞ EKRANI (günlük kullanım motoru)
│   ├── Günlük görünüm (varsayılan)
│   ├── Haftalık görünüm
│   ├── Aylık görünüm
│   ├── (+) Ders Ekle
│   │   ├── Tek seferlik ders
│   │   ├── Tekrar eden ders
│   │   └── Tatil / Müsait Değil bloğu   [YENİ]
│   └── Ders detayı (tıklayınca)
│       ├── Düzenle
│       ├── Ertele          [YENİ]
│       ├── İptal Et
│       └── Dersi Tamamla → Ders Oturumu akışı
│
├── [Alt Sekme 2] 👥 ÖĞRENCİLER
│   ├── Öğrenci listesi (aktif / arşiv)
│   ├── (+) Öğrenci Ekle
│   │   ├── Manuel oluştur
│   │   └── Kayıtlı öğrenciyi davet et   [YENİ]
│   └── Öğrenci detayı
│       ├── Profil bilgileri
│       ├── Ders geçmişi
│       ├── Ödev listesi
│       ├── Gelişim/performans (Faz 3)
│       ├── Bireysel çalışma verisi (Faz 2, izin varsa)
│       ├── Ödeme/bakiye
│       ├── Veli bilgisi
│       └── Özel notlar
│
├── [Alt Sekme 3] 📝 ÖDEVLER
│   ├── Bekleyen ödevler
│   ├── Tamamlananlar
│   ├── Gecikenler
│   └── (+) Ödev Ver
│
├── [Alt Sekme 4] 💰 ÖDEMELER
│   ├── Bu ay özeti
│   ├── Öğrenci bazlı bakiye listesi
│   ├── Geciken ödemeler (Premium)
│   └── Ödeme kaydı işaretleme
│
└── [Alt Sekme 5] 👤 PROFİL & DAHA FAZLASI
    ├── Öğretmen profili (M02)
    ├── Raporlar (M14 — Premium)
    ├── Gelen talepler (Faz 4)
    ├── Yorumlarım (Faz 4)
    ├── Abonelik (Faz 5)
    ├── Bildirim ayarları
    └── Ayarlar & Güvenlik (M15)
```

**Tasarım kuralı:** Uygulama açılışında öğretmen **Takvim/bugün** ekranını görür. PRD'nin "günlük çalışma aracı" tezinin doğrudan karşılığıdır.

---

## 6. Detaylı Kullanım Akışları — Kurulum

### AKIŞ 1: Öğretmen Kaydı ve Onboarding

**Aktör:** Yeni öğretmen | **Ön koşul:** Yok | **Faz:** 0–1

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Uygulamayı açar | Karşılama ekranı: "Öğretmen misin, Öğrenci mi, Veli mi?" |
| 2 | **"Öğretmenim"** seçer | Kayıt formu açılır |
| 3 | E-posta/telefon + şifre girer | Doğrulama kodu gönderir |
| 4 | Kodu girer | Hesap oluşur, rol = ÖĞRETMEN atanır |
| 5 | — | Onboarding başlar: "Profilini tamamlayalım" |
| 6 | Ad soyad, branş, şehir/ilçe girer | Kaydeder |
| 7 | Ders şekli seçer (yüz yüze / online / her ikisi) | Kaydeder |
| 8 | Deneyim yılı, eğitim seviyesi girer | Kaydeder |
| 9 | Saatlik ücret girer | Kaydeder — M07'de varsayılan ücret olur |
| 10 | Uygun saatlerini işaretler | Takvimde müsaitlik olarak görünür |
| 11 | Profil fotoğrafı yükler (atlanabilir) | Kaydeder |
| 12 | — | **"İlk öğrencini ekle"** yönlendirmesi |

**Alternatif akış 12a:** Öğretmen "Sonra" der → Boş takvim ekranı + "İlk öğrencini ekleyerek başla" boş durum kartı.

**Kritik tasarım kararı:** Onboarding'de **eşleştirmeden hiç bahsedilmez** (Faz 4'e kadar yok). Vaat: "derslerini yönet".

**Hata durumları:**
- E-posta zaten kayıtlı → "Bu e-posta kullanımda, giriş yap"
- Zayıf şifre → Anlık uyarı
- Doğrulama kodu 3 kez yanlış → 5 dk kilit

---

### AKIŞ 2: Öğrenci Ekleme (Manuel)

**Aktör:** Öğretmen | **Ön koşul:** Profil oluşturulmuş | **Faz:** 1 | **PRD:** 1.2

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğrenciler sekmesine gider | Liste (veya boş durum) gösterilir |
| 2 | **(+) Öğrenci Ekle** | Form açılır |
| 3 | Ad soyad girer *(zorunlu)* | — |
| 4 | Sınıf seviyesi seçer | — |
| 5 | Branş seçer (öğretmenin branşları ön dolu) | — |
| 6 | İletişim bilgisi girer *(opsiyonel)* | — |
| 7 | Veli bilgisi girer *(opsiyonel)* | — |
| 8 | Hedef/seviye notu girer *(opsiyonel)* | — |
| 9 | Ders ücreti girer (profil ücreti ön dolu) | — |
| 10 | **Kaydet** | **Free limit kontrolü** |
| 11 | — | Öğrenci oluşur → "Bu öğrenciye ders planla?" |

**Alternatif akış 10a — Free limit dolu:**
> "Free planda maks. 10 aktif öğrenci ekleyebilirsin. Yeni öğrenci eklemek için Premium'a geç veya bir öğrenciyi arşivle."
> [Premium'a Geç] [Öğrenci Arşivle] [İptal]

**Alternatif akış 3a — Aynı isim var:** "Ali Yılmaz adında bir öğrencin zaten var. Yine de ekle?"

**Önemli:** **[PRD]** Bu akışta öğrencinin uygulamaya kayıtlı olması **gerekmez**. Öğretmen, öğrenci hiç uygulamaya girmese bile tüm ders yönetimini yapabilir. Bu, Faz 1'in tek başına değer üretebilmesi için kritiktir.

---

### AKIŞ 3: Kayıtlı Öğrenciyi Davet Etme **[YENİ]**

**Aktör:** Öğretmen | **Faz:** 2 | **Kaynak:** PRD M01/M03 "her iki giriş yolu da desteklenmelidir" notundan türetilmiştir

PRD, öğrencinin kendi başına kayıt olabileceğini söylüyor ama **iki tarafın nasıl birleşeceğini tanımlamıyor.** Bu boşluğun doldurulması gerekir.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | (+) Öğrenci Ekle → **"Kayıtlı öğrenciyi davet et"** | — |
| 2 | Öğrencinin e-posta/telefonunu girer | Kayıt var mı kontrol eder |
| 3 | — | Bulunursa: "Ayşe K. bulundu — davet gönderilsin mi?" |
| 4 | **Davet Gönder** | Öğrenciye push + uygulama içi bildirim |
| 5 | *(Öğrenci onaylar)* | Bağlantı kurulur — durum: BAĞLI |
| 6 | — | Öğretmene bildirim: "Ayşe daveti kabul etti" |

**Bağlantı sonrası:**
- Öğrencinin bireysel çalışma verisi öğretmene **öğrencinin izin verdiği ölçüde** açılır **[PRD]**
- Öğretmenin ders/ödev/not verisi öğrenciye açılır
- Veli bağlıysa veli paneli her iki kaynaktan beslenmeye başlar **[PRD M09]**

**Alternatif akış 3a — Kayıt yok:** "Bu kişi kayıtlı değil. Manuel öğrenci olarak ekleyip davet linki gönderebilirsin."
**Alternatif akış 5a — Öğrenci reddeder:** Öğretmene "Davet reddedildi" bildirimi; manuel kayıt kalır, bağlantı kurulmaz.

---

## 7. Detaylı Kullanım Akışları — Takvim (Günlük Motor)

### AKIŞ 4: Ders Ekleme (Tek Seferlik)

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M04, 1.3 (Kritik)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Takvim → **(+)** | "Ne eklemek istersin?" → Ders / Tekrar eden ders / Tatil |
| 2 | **Ders** seçer | Form açılır |
| 3 | Öğrenci seçer *(zorunlu)* | Öğrencinin branşı ön dolu gelir |
| 4 | Branş/konu seçer | — |
| 5 | Tarih seçer | — |
| 6 | Başlangıç saati seçer | — |
| 7 | Süre girer (varsayılan 60 dk) | Bitiş saati otomatik hesaplanır |
| 8 | Ders şekli seçer (yüz yüze / online) | — |
| 9 | Yer/link girer *(opsiyonel)* **[YENİ]** | — |
| 10 | Ücret (öğrenci ücreti ön dolu) | — |
| 11 | Hatırlatma ayarlar (varsayılan: 1 saat önce) **[PRD]** | — |
| 12 | **Kaydet** | **Çakışma kontrolü** çalışır **[PRD]** |
| 13 | — | Ders oluşur, durum = **PLANLANDI**, takvimde görünür |

**Alternatif akış 12a — Çakışma var [PRD]:**
> ⚠️ "Bu saatte zaten bir dersin var: Mehmet D. — Matematik, 14:00–15:00"
> [Yine de Ekle] [Saati Değiştir] [İptal]

**Alternatif akış 12b — Tatil bloğuyla çakışıyor [YENİ]:**
> ⚠️ "Bu tarih 'Yaz Tatili' olarak işaretli (1–15 Ağustos). Yine de ders eklensin mi?"
> [Yine de Ekle] [İptal]

**Alternatif akış 12c — Müsait saat dışında [YENİ]:**
> ℹ️ "Bu saat profilindeki uygun saatlerin dışında. Sorun değilse devam et."
> *(Uyarı, engel değil.)*

**Validasyon kuralları [TÜRETİLMİŞ]:**
- Geçmiş tarihe ders eklenebilir (geriye dönük kayıt için gerekli) ama uyarı verilir
- Süre: min 15 dk, max 8 saat
- Öğrenci seçimi zorunlu — öğrencisiz ders olamaz

---

### AKIŞ 5: Tekrar Eden Ders Oluşturma

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M04, 1.3 (Kritik)

Özel derste tipik düzen "her Salı 16:00" şeklindedir. Bu akış, öğretmenin haftalık tekrar eden yükünü ortadan kaldırır.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Takvim → (+) → **Tekrar Eden Ders** | — |
| 2 | Öğrenci, branş, süre, ücret girer | — |
| 3 | **Tekrar deseni** seçer | Seçenekler: Her hafta / 2 haftada bir / Her ay / Özel |
| 4 | Gün(leri) seçer | Örn: Salı + Perşembe |
| 5 | Saat girer | Örn: 16:00 |
| 6 | Başlangıç tarihi | — |
| 7 | **Bitiş koşulu** seçer | Tarihe kadar / N ders sonra / Süresiz |
| 8 | **Kaydet** | Tüm oturumlar toplu üretilir |
| 9 | — | Çakışma kontrolü **her oturum için** çalışır |

**Alternatif akış 9a — Bazı oturumlar çakışıyor:**
> ⚠️ "24 dersten 2'si çakışıyor: 12 Mart, 19 Mart"
> [Çakışanları Atla] [Hepsini Ekle] [Düzenle]

**Alternatif akış 9b — Bazı oturumlar tatile denk geliyor [YENİ]:**
> ℹ️ "3 ders tatil dönemine denk geliyor. Ne yapalım?"
> [Tatildekileri Atla] *(önerilen)* [Hepsini Ekle]

**Süresiz seri kuralı [TÜRETİLMİŞ]:** Sistem 6 ay ileriye kadar oturum üretir, sonra kayan pencereyle devam ettirir. Sınırsız kayıt üretilmez.

---

### AKIŞ 6: Ders Düzenleme

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M04 "ders değiştirme"

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Takvimde derse dokunur | Ders detayı açılır |
| 2 | **Düzenle** | Form açılır |
| 3 | Alanları değiştirir | — |
| 4 | **Kaydet** | Tekrar eden ders ise kapsam sorulur |

**Kritik kapsam sorusu (tekrar eden ders için) [YENİ]:**
> "Bu değişiklik neyi etkilesin?"
> - ○ **Sadece bu ders**
> - ○ **Bu ve sonraki tüm dersler**
> - ○ **Serideki tüm dersler**

Bu üçlü seçim standart takvim davranışıdır; PRD'de tanımlı değil ama olmadan tekrar eden ders modülü kullanılamaz.

**Bildirim kuralı [TÜRETİLMİŞ]:** Tarih/saat değişirse öğrenciye (ve bağlıysa veliye) otomatik bildirim gider. Sadece "konu" değişirse bildirim gitmez.

**Kısıt:** Durumu **TAMAMLANDI** olan ders tarih/saat açısından düzenlenemez — sadece notu/konusu düzeltilebilir.

---

### AKIŞ 7: Ders İptal Etme

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M04 "ders iptal etme"

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders detayı → **İptal Et** | Onay ekranı |
| 2 | İptal nedeni seçer **[YENİ]** | Öğretmen iptali / Öğrenci iptali / Tatil / Diğer |
| 3 | Ücretlendirme kararı **[YENİ]** | ○ Ücret alınmayacak *(varsayılan)* ○ Ücret alınacak |
| 4 | *(Tekrar eden ise)* kapsam seçer | Sadece bu / Bu ve sonrakiler / Tüm seri |
| 5 | **Onayla** | Durum = **İPTAL EDİLDİ** |
| 6 | — | Öğrenci + veliye bildirim gider |
| 7 | — | Ücret alınmayacaksa M07'de bakiyeye yansımaz |

**Önemli tasarım kararı [TÜRETİLMİŞ]:** İptal edilen ders **silinmez** — takvimde soluk/üstü çizili görünür ve geçmişte kalır. Nedeni: iptal geçmişi hem öğretmen-öğrenci arasında kanıt, hem de Faz 4 puanlamasında "dakiklik" alt kategorisi için veri kaynağıdır **[PRD M13]**.

**Silme vs. İptal ayrımı [YENİ]:**
| İşlem | Ne zaman | Sonuç |
|---|---|---|
| **İptal** | Ders planlıydı, gerçekleşmeyecek | Kayıt kalır, durum=İPTAL, taraflara bildirim |
| **Sil** | Yanlışlıkla eklendi | Kayıt tamamen kaldırılır, bildirim gitmez |

Silme yalnızca **oluşturulduktan sonra 24 saat içinde** ve ders **gelecekteyse** mümkün olmalıdır. Aksi hâlde iptal kullanılır.

---

### AKIŞ 8: Ders Erteleme **[YENİ]**

**Aktör:** Öğretmen | **Faz:** 1

PRD'de yok ama özel derste **en sık yaşanan senaryodur** ("bu hafta olmaz, cumaya alalım"). Düzenle akışıyla yapılabilir ama ayrı bir aksiyon olması gerekir: erteleme, iptal+yeni ders değil, **aynı dersin taşınmasıdır** ve bakiye/ödeme sürekliliğini bozmamalıdır.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders detayı → **Ertele** | Tarih/saat seçici açılır |
| 2 | Yeni tarih/saat seçer | Çakışma kontrolü |
| 3 | Not ekler *(opsiyonel)* | Örn: "Öğrenci hasta" |
| 4 | **Onayla** | Ders taşınır, durum = **ERTELENDİ → PLANLANDI** |
| 5 | — | Öğrenci + veliye bildirim: "Dersin 14 Mart 16:00'ya alındı" |
| 6 | — | Ders geçmişinde erteleme kaydı tutulur |

---

### AKIŞ 9: Tatil / Müsait Değil Bloğu Ekleme **[YENİ]**

**Aktör:** Öğretmen | **Faz:** 1 | **Durum: PRD'de YOK — önerilen**

Sizin sorunuzda örnek olarak verdiğiniz özellik. PRD M04'te "ders ekleme/değiştirme/iptal" var ama **müsait olmama** kavramı hiç tanımlanmamış. Öğretmen tatile çıktığında bugün yapabileceği tek şey her dersi tek tek iptal etmek — bu, ürün açığıdır.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Takvim → (+) → **Tatil / Müsait Değil** | Form açılır |
| 2 | Tür seçer | Tatil / İzin / Resmî tatil / Diğer |
| 3 | Başlık girer | Örn: "Yaz tatili" |
| 4 | Başlangıç tarihi | — |
| 5 | Bitiş tarihi | Tek gün veya aralık |
| 6 | Tüm gün mü, saat aralığı mı | Örn: "her gün 09:00–13:00 müsait değilim" |
| 7 | **Kaydet** | **Çakışan dersler taranır** |
| 8 | — | Çakışma varsa karar sorulur ↓ |

**Adım 8 — Çakışan ders yönetimi (bu akışın en kritik parçası):**
> "Bu tarihlerde **7 planlı dersin** var. Ne yapalım?"
>
> - ○ **Hepsini iptal et ve öğrencilere bildir** *(önerilen)*
> - ○ **Hepsini ertele** → sistem her ders için yeni tarih önerir
> - ○ **Dokunma, sadece takvimi işaretle**
>
> [Dersleri Gör] [Onayla]

**Tatil bloğunun etkileri:**
| Alan | Etki |
|---|---|
| Takvim | Blok, günlerin arka planında ayrı renkte görünür |
| Ders ekleme | Tatile ders eklenirken uyarı verilir (engel değil) |
| Tekrar eden ders | Tatile denk gelen oturumlar otomatik atlanabilir |
| Müsaitlik (Faz 4) | Öğrenciler eşleştirmede öğretmeni müsait görmez |
| Bildirimler | Tatil süresince ders hatırlatması gönderilmez |
| Ödeme | İptal edilen tatil dersleri bakiyeye yansımaz |
| Boş zaman analizi (M14) | Tatil, "boş zaman" olarak sayılmaz |

**Ek öneri:** Türkiye resmî tatilleri (23 Nisan, 19 Mayıs, bayramlar, ara tatiller) sistemde ön tanımlı gelmeli; öğretmen tek tıkla "resmî tatillerde ders yok" diyebilmelidir.

---

### AKIŞ 10: Takvim Görüntüleme

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M04 "haftalık/aylık görünüm"

| Görünüm | İçerik | Kullanım Anı |
|---|---|---|
| **Günlük** *(varsayılan açılış)* [TÜRETİLMİŞ] | Bugünün dersleri, saat sıralı, her biri tek dokunuşla tamamlanabilir | Sabah / gün içi |
| **Haftalık** [PRD] | 7 gün × saat ızgarası, çakışmalar ve boşluklar görünür | Hafta planlaması |
| **Aylık** [PRD] | Gün başına ders sayısı, tatil blokları | Dönem planlaması |

**Ders kartında görünen bilgi [TÜRETİLMİŞ]:** Saat · Öğrenci adı · Branş · Durum rozeti · Ödeme durumu ikonu

**Renk kodlaması [TÜRETİLMİŞ]:**
- 🔵 Planlandı · 🟢 Tamamlandı · 🔴 İptal · 🟡 Öğrenci gelmedi · ⬜ Tatil bloğu

---

## 8. Detaylı Kullanım Akışları — Ders Yürütme

### AKIŞ 11: Ders Oturumunu Tamamlama ⭐ **(Ürünün Kalbi)**

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M05, 1.4 (Kritik)

Bu, **ürünün en kritik akışıdır.** PRD'nin tüm tezi buna dayanır: öğretmen her ders sonrası buraya girmezse platform bir "not defteri" olur ve terk edilir. Akış **60 saniyeden uzun sürmemelidir.**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders saati geçer | Push: "Matematik dersini tamamla" **[PRD M11]** |
| 2 | Bildirime dokunur veya takvimden girer | Ders tamamlama ekranı |
| 3 | **Katılım durumu** işaretler **[PRD]** | Geldi / Gelmedi / Geç geldi |
| 4 | **İşlenen konu** girer **[PRD]** | Son kullanılan konular önerilir |
| 5 | **Gerçekleşen süre** onaylar **[YENİ]** | Planlanan süre ön dolu, değiştirilebilir |
| 6 | **İşlenen içerik** yazar *(opsiyonel)* **[PRD]** | Serbest metin |
| 7 | **Öğretmen notu** yazar *(opsiyonel)* **[PRD]** | Serbest metin |
| 8 | Notun görünürlüğünü seçer **[YENİ]** | ○ Özel ○ Öğrenciyle paylaş ○ Öğrenci+Veli |
| 9 | **Ödev ver** *(opsiyonel)* **[PRD]** | → AKIŞ 13'e dallanır |
| 10 | **Ödeme durumu** işaretler **[PRD M07]** | ○ Tahsil edildi ○ Bekliyor ○ Kısmi |
| 11 | **Tamamla** | Durum = **TAMAMLANDI** |
| 12 | — | Veriler dağılır ↓ |

**Adım 12 — Tamamlama sonrası veri akışı:**
```
DERS TAMAMLANDI
    │
    ├─→ Ders geçmişine eklenir (öğretmen + öğrenci görür)
    ├─→ Veli paneline "son ders özeti" düşer          [PRD M09]
    ├─→ Ödeme bakiyesi güncellenir                     [PRD M07]
    ├─→ Öğrenci gelişim verisine işlenir (Faz 3)       [PRD M10]
    ├─→ Aylık gelir özetine sayılır (Faz 5)            [PRD M14]
    ├─→ Ödev verildiyse öğrenciye bildirim gider       [PRD M06]
    └─→ Öğrenciye geri bildirim daveti tetiklenir      [PRD M13, Faz 1'de "özel geri bildirim"]
```

**Alternatif akış 3a — Öğrenci gelmedi [YENİ]:**
> "Ders ücretlendirilsin mi?"
> [Evet, ücret alınacak] [Hayır]
> *Sonuç: durum = ÖĞRENCİ GELMEDİ. Ücret alınacaksa bakiyeye yansır. Bu kayıt, aynı öğrencide tekrarlarsa öğretmene desen olarak gösterilebilir.*

**Alternatif akış 11a — Öğretmen ekranı yarıda bırakır:** Girilen veri taslak olarak saklanır; ders "tamamlanmayı bekliyor" durumunda kalır ve ertesi gün tekrar hatırlatılır **[PRD M11: "ders sonrası not girme hatırlatması"]**.

**Tasarım kuralı:** Tek zorunlu alan **katılım durumu**dur. Diğer her şey atlanabilir. Sürtünme, bu akışın en büyük düşmanıdır.

---

### AKIŞ 12: Ders Notu Girme

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M06 "ders notu ekleme"

Genellikle AKIŞ 11 içinde yapılır; ayrıca bağımsız olarak da erişilebilir olmalıdır (öğretmen sonradan not eklemek isteyebilir).

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğrenci detayı → Ders geçmişi → Derse dokunur | Oturum detayı |
| 2 | **Not Ekle / Düzenle** | Metin alanı |
| 3 | Notu yazar | — |
| 4 | Görünürlük seçer **[YENİ]** | Özel / Öğrenci / Öğrenci+Veli |
| 5 | Dosya/görsel ekler *(opsiyonel)* **[PRD]** | — |
| 6 | **Kaydet** | Not kaydedilir, seçilen taraflara yansır |

**Not görünürlüğü neden gerekli [TÜRETİLMİŞ]:** PRD M09'da "öğretmen notları" velinin gördüğü veriler arasında. Ancak öğretmenin bazı notları özeldir ("bu öğrenci derse hazırlıksız geliyor, veliyle konuşmalıyım"). Görünürlük kontrolü olmazsa öğretmen ya dürüst not tutmaz ya da modülü kullanmaz. **Varsayılan: Özel.**

---

### AKIŞ 13: Ödev Verme

**Aktör:** Öğretmen | **Faz:** 1 | **PRD:** M06, 1.5 (Kritik)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders tamamlama ekranı → **Ödev Ver** *(veya Ödevler → (+))* | Form |
| 2 | Öğrenci seçer *(ders akışından geliyorsa ön dolu)* | — |
| 3 | Ödev başlığı girer | — |
| 4 | Açıklama yazar | — |
| 5 | **Son tarih** belirler **[PRD]** | Varsayılan: sonraki ders tarihi |
| 6 | Dosya/görsel ekler *(opsiyonel)* **[PRD]** | PDF, foto, doküman |
| 7 | **Kaydet** | Durum = **BEKLİYOR** |
| 8 | — | Öğrenciye bildirim; bağlıysa veliye de yansır **[PRD M09]** |

**Ödev takibi [PRD M06]:**
- Öğretmen ödev listesinde durumu görür: Bekliyor / Tamamlandı / Gecikti
- Öğrenci "tamamladım" işaretler → öğretmene bildirim
- **[YENİ]** Öğretmen ödevi onaylayabilir veya geri bildirimle geri gönderebilir
- **[PRD M11]** "Ödev son tarihi yaklaşıyor" bildirimi öğrenciye gider (Yüksek öncelik)

---

## 9. Detaylı Kullanım Akışları — Ödeme (M07)

> **Kritik hatırlatma [PRD]:** *"Ödeme sistemi üzerinden para tahsilatı yapılmaz."* Öğretmen platformdan para almaz. Bu modül yalnızca **kayıt ve hatırlatma** aracıdır. Para elden/havale ile ders dışında hareket eder.

### AKIŞ 14: Ücret Tanımlama

| Seviye | Nerede | Öncelik |
|---|---|---|
| Profil ücreti | M02 profil | En düşük (varsayılan) |
| Öğrenci ücreti **[YENİ]** | Öğrenci detayı | Profili ezer |
| Ders ücreti | Ders ekleme | Öğrenciyi ezer |
| Aylık paket **[PRD]** | Öğrenci detayı | Ders başı ücretin yerine geçer |

**Neden öğrenci bazlı ücret gerekli:** Özel derste fiyat öğrenciye göre değişir (mesafe, seviye, yakınlık, indirim). Tek fiyat modeli gerçek kullanımı karşılamaz.

### AKIŞ 15: Ödeme İşaretleme

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ödemeler sekmesi | Öğrenci bazlı bakiye listesi **[PRD]** |
| 2 | Öğrenciye dokunur | Ders bazlı ödeme detayı |
| 3 | Durum işaretler **[PRD]** | Tahsil edildi / Bekliyor / Kısmi ödendi |
| 4 | *(Kısmi ise)* tutar girer | — |
| 5 | **Kaydet** | Bakiye güncellenir, veli paneline yansır |

### AKIŞ 16: Aylık Paket Oluşturma **[PRD M07]**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğrenci detayı → **Aylık Paket** | Form |
| 2 | Ay seçer | — |
| 3 | Ders sayısı girer (örn. 8 ders) | — |
| 4 | Paket tutarı girer | Ders başı tutar otomatik hesaplanır |
| 5 | **Kaydet** | O ay dersleri paket kapsamına girer, ders başı ücretlendirme devre dışı kalır |

### AKIŞ 17: Gelir Takibi (Premium — Faz 5)

| Yetenek | Free | Premium |
|---|---|---|
| Öğrenci bazlı bakiye | ✅ | ✅ |
| Ödeme durumu işaretleme | ✅ | ✅ |
| **Aylık kazanç toplamı** | ❌ | ✅ |
| **Geciken ödeme listesi** | ❌ | ✅ |
| **Otomatik ödeme hesaplama** | ❌ | ✅ |
| **Gelir analizi** | ❌ | ✅ |

**Not [PRD]:** "Aylık gelir özeti" M07'de temel özellik olarak yazılmış ama Free/Premium tablosunda "Aylık kazanç toplamı ❌ Free" deniyor. Bu bir **PRD çelişkisidir** — Bölüm 15'te listelendi.

---

## 10. Detaylı Kullanım Akışları — Faz 3+

### AKIŞ 18: Öğrenci Gelişimini İzleme (M10, Faz 3)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğrenci detayı → **Gelişim** | Dashboard |
| 2 | Konu kazanım durumunu görür **[PRD]** | Konu bazlı ilerleme |
| 3 | Test performansı zaman serisini görür **[PRD]** | Grafik |
| 4 | Eksik/güçlü konuları görür **[PRD]** | Otomatik çıkarım |
| 5 | Hedef puan/seviye tanımlar **[PRD]** | — |
| 6 | Değerlendirme notu yazar **[PRD]** | Veliye yansıyabilir |

**Veri kaynakları:** Öğretmenin ders oturumu kayıtları (M05) + öğrencinin bireysel test girişleri (M08, izin varsa) **[PRD]**

### AKIŞ 19: Bireysel Çalışma Verisini Görme (M08, Faz 2)

**[PRD]** *"Öğrenci isterse belirli verileri gizleyebilir (gizlilik kontrolü)"* ve *"Öğretmen bağlıysa veriler öğretmenle de paylaşılabilir."*

Bu, öğretmen için **salt okunur** bir alandır ve **öğrencinin iznine tabidir.**

| Öğrenci izni | Öğretmenin gördüğü |
|---|---|
| Tam paylaşım | Çalışma süreleri, konu dağılımı, test netleri, streak |
| Kısmi paylaşım | Yalnızca öğrencinin açtığı alanlar |
| Paylaşım kapalı | "Öğrenci çalışma verisini paylaşmıyor" |

**Öğretmen bu veriyi değiştiremez, silemez, sayacı kontrol edemez.**

### AKIŞ 20: Veliyle İletişim (M09, Faz 3)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğrenci detayı → Veli | Veli profili |
| 2 | **Mesaj Gönder** **[PRD]** | Metin alanı |
| 3 | Gönderir | Veliye push + panelde görünür |

**Veliye otomatik yansıyan öğretmen verileri [PRD M09]:** Son ders özeti · Verilen ödevler · Öğretmen notları *(paylaşıma açıksa)* · Ödeme özeti · Yaklaşan dersler

---

## 11. Detaylı Kullanım Akışları — Faz 4 (Eşleştirme ve Puanlama)

### AKIŞ 21: Eşleştirmede Listelenme (M12)

**[PRD]** Öğretmen Faz 4'te herkese açık listelenmeye başlar.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Profil → **Eşleştirmede Görün** *(açık/kapalı)* [YENİ] | Onay |
| 2 | — | Profil aramalarda listelenir |
| 3 | — | Filtrelerde çıkar: branş, şehir, ücret, ders şekli, uygun saatler **[PRD]** |
| 4 | Öğrenciden talep gelir **[PRD]** | Push bildirim |
| 5 | Talebi inceler | Öğrenci bilgisi, mesajı |
| 6 | **Kabul / Red** [TÜRETİLMİŞ] | Kabul → öğrenci listesine eklenir |

**Öne çıkan profil (Premium, Faz 5) [PRD]:** Arama sonuçlarında üst sıralarda gösterilir.

**[YENİ] Öneri:** "Eşleştirmede görün" açık/kapalı anahtarı olmalı. Kontenjanı dolu öğretmen listelenmek istemeyebilir; bu olmadan gereksiz talep akışı oluşur ve deneyim bozulur.

### AKIŞ 22: Yorum ve Puan Alma (M13)

**[PRD] Kurallar:**
1. Yalnızca o öğretmenden **ders almış** öğrenciler yorum yapabilir → sahte yorum önleme
2. Ders tamamlandıktan sonra sistem **otomatik yorum daveti** gönderir
3. Yorum = metin + 1–5 yıldız
4. Alt kategoriler: **anlatım netliği · dakiklik ve güvenilirlik · sabır ve öğrenciye yaklaşım · ders hazırlığı**
5. Profilde ortalama puan + yorum sayısı görünür
6. **Öğretmen yoruma yanıt verebilir**
7. **Olumsuz yorum gizlenemez — yalnızca yanıtlanabilir**
8. "Doğrulanmış öğrenci" rozeti: sisteme kayıtlı + ders kaydı olan
9. Öğretmen şüpheli yorumu bildirebilir → admin moderasyonu

**Öğretmenin yorum üzerindeki gücü net olarak:**
| Yapabilir | Yapamaz |
|---|---|
| Yorumu görüntüleme | Yorumu silme |
| Yoruma yanıt verme | Yorumu gizleme |
| Şüpheli yorumu bildirme | Puanı değiştirme |
| — | Yorum yapacak öğrenciyi seçme |

### AKIŞ 23: Erken Geri Bildirim (Faz 1) **[PRD]**

**[PRD]** *"Puanlama sistemi Faz 4'te herkese açılır. Ancak Faz 1–2'de 'öğretmene özel geri bildirim' olarak erken aktive edilebilir — öğrenci değerlendirme gönderir, yalnızca öğretmen görür. Bu sayede veri birikimi Faz 4'ten önce başlar."*

| Adım | Aktör | Sistem |
|---|---|---|
| 1 | Ders tamamlanır | Öğrenciye değerlendirme daveti |
| 2 | Öğrenci puan + yorum gönderir | Kaydedilir, **gizli** |
| 3 | Öğretmen görür | Yalnızca öğretmen — kimseye açık değil |
| 4 | — | Faz 4'te bu veri geçmiş puan havuzu olur |

**Öğretmene not gösterilmeli [YENİ]:** "Bu geri bildirimler şu an sadece sana özel. İleride profilinde puan gösterilmeye başlandığında hangi verilerin görünür olacağı sana önceden bildirilecek ve onayın alınacaktır." — Aksi hâlde Faz 4'te retroaktif yayın güven krizi yaratır.

---

## 12. Durum Makineleri

### 12.1 Ders Durumu

```
                    ┌─────────────┐
        (oluştur)──→│ PLANLANDI   │
                    └──────┬──────┘
                           │
        ┌──────────────────┼──────────────────┬─────────────────┐
        ↓                  ↓                  ↓                 ↓
  ┌───────────┐    ┌──────────────┐   ┌────────────┐   ┌──────────────┐
  │ ERTELENDİ │    │ TAMAMLANDI   │   │   İPTAL    │   │   ÖĞRENCİ    │
  │  [YENİ]   │    │              │   │  EDİLDİ    │   │   GELMEDİ    │
  └─────┬─────┘    └──────┬───────┘   └────────────┘   └──────┬───────┘
        │                 │            (son durum)            │
        └→ PLANLANDI      │                                   │
           (yeni tarih)   ↓                                   ↓
                    Not/ödev/ödeme                     Ücretlendirme
                    akışları tetiklenir                kararı → M07
```

| Durum | Öğretmen ne yapabilir | Kaynak |
|---|---|---|
| PLANLANDI | Düzenle, Ertele, İptal Et, Tamamla, Sil* | [PRD] |
| TAMAMLANDI | Not/konu düzelt, ödev ekle, ödeme işaretle. **Tarih değiştiremez.** | [PRD] |
| İPTAL EDİLDİ | Sadece görüntüleme | [PRD] |
| ERTELENDİ | Otomatik olarak PLANLANDI'ya döner | [YENİ] |
| ÖĞRENCİ GELMEDİ | Ücretlendirme kararı verebilir | [YENİ] |

*Sil: yalnızca oluşturmadan sonraki 24 saat içinde ve ders gelecekteyse.

### 12.2 Ödev Durumu **[PRD M06 + türetilmiş]**

```
VERİLDİ ──→ BEKLİYOR ──┬──→ TAMAMLANDI (öğrenci işaretler) ──→ ONAYLANDI (öğretmen) [YENİ]
                       │                                    └──→ GERİ GÖNDERİLDİ [YENİ]
                       └──→ GECİKTİ (son tarih geçti)
```

### 12.3 Ödeme Durumu **[PRD M07]**

```
BEKLİYOR ──┬──→ TAHSİL EDİLDİ
           ├──→ KISMİ ÖDENDİ ──→ TAHSİL EDİLDİ
           └──→ GECİKTİ (Premium: listede görünür + bildirim)
```

### 12.4 Öğrenci-Öğretmen Bağlantı Durumu **[YENİ]**

```
MANUEL KAYIT (öğrenci uygulamada yok)
     │
     ├──→ DAVET GÖNDERİLDİ ──┬──→ BAĞLI (öğrenci onayladı)
     │                       └──→ REDDEDİLDİ
     │
BAĞLI ──┬──→ ARŞİVLENDİ (ders bitti, veri korunur)
        └──→ BAĞLANTI KESİLDİ (iki taraftan biri sonlandırır)
```

---

## 13. Yetki Matrisi — Öğretmen Neyi Yapamaz

**[TÜRETİLMİŞ]** Rol tanımı, sınırlarıyla birlikte anlamlıdır.

| # | Öğretmen ŞUNU YAPAMAZ | Neden |
|---|---|---|
| 1 | Öğrencinin bireysel çalışma sayacını başlatamaz/durduramaz | Veri öğrenciye aittir [PRD M08] |
| 2 | Öğrencinin gizlediği çalışma verisini göremez | [PRD] "öğrenci isterse belirli verileri gizleyebilir" |
| 3 | Öğrencinin test kayıtlarını değiştiremez | Öğrenci verisi |
| 4 | Aldığı olumsuz yorumu silemez/gizleyemez | [PRD M13] "olumsuz yorum gizlenemez" |
| 5 | Puanını değiştiremez | [PRD M13] |
| 6 | Kendine yorum yazamaz / yorum yapacakları seçemez | [PRD M13] "yalnızca ders almış öğrenciler" |
| 7 | Platform üzerinden para tahsil edemez | [PRD Bölüm 5] |
| 8 | Başka öğretmenin öğrencisini göremez | Rol bazlı erişim [PRD M01] |
| 9 | Velinin diğer çocuklarının verisini göremez | [TÜRETİLMİŞ] |
| 10 | Free planda 5–10 üstü aktif öğrenci ekleyemez | [PRD] |
| 11 | Rolünü değiştiremez | [TÜRETİLMİŞ] |
| 12 | Öğrencinin hesabını silemez (sadece bağlantıyı keser) | [TÜRETİLMİŞ] |
| 13 | Tamamlanmış dersin tarihini değiştiremez | [TÜRETİLMİŞ] |
| 14 | Admin işlemleri yapamaz (moderasyon vb.) | [PRD M01] |

---

## 14. Öğretmen Verisi — Kavramsal Model

**[TÜRETİLMİŞ]** — PRD Faz 0.2'de "veritabanı şeması tasarımı" iş kalemi var, şema yok. Aşağıdaki, öğretmen rolü açısından gereken minimum modeldir.

```
User (id, rol, email, telefon, şifre, doğrulama, oluşturma)
  │
  └─1:1─ TeacherProfile
           ├─ ad_soyad, branşlar[], şehir, ilçe
           ├─ ders_şekli (yüzyüze|online|her_ikisi)
           ├─ deneyim_yılı, eğitim_seviyesi
           ├─ varsayılan_ücret, para_birimi
           ├─ açıklama, fotoğraf_url
           ├─ doğrulama_durumu, doğrulama_rozeti
           ├─ ortalama_puan, yorum_sayısı        (Faz 4)
           ├─ abonelik_tipi (free|premium)        (Faz 5)
           └─ eşleştirmede_görün (bool)           [YENİ]
                │
                ├─1:N─ Availability (gün, başlangıç_saat, bitiş_saat)
                ├─1:N─ Certificate (başlık, kurum, yıl, dosya)
                │
                ├─1:N─ TeacherStudent  ← bağlantı tablosu
                │        ├─ öğrenci_id (nullable — manuel öğrenci)
                │        ├─ durum (manuel|davet|bağlı|arşiv|kesildi)
                │        ├─ öğrenci_ücreti (override)     [YENİ]
                │        └─ öğretmen_özel_notu
                │
                ├─1:N─ Lesson
                │        ├─ öğrenci_id, branş, konu
                │        ├─ tarih, başlangıç, süre_planlanan
                │        ├─ süre_gerçekleşen                [YENİ]
                │        ├─ durum (planlandı|tamamlandı|iptal|ertelendi|gelmedi)
                │        ├─ ders_şekli, yer_veya_link       [YENİ]
                │        ├─ ücret, ücretlendirilecek_mi     [YENİ]
                │        ├─ recurrence_id (nullable)
                │        ├─ iptal_nedeni, ertelendiği_ders_id [YENİ]
                │        └─1:1─ LessonSession
                │                 ├─ katılım (geldi|gelmedi|geç)
                │                 ├─ işlenen_içerik
                │                 ├─ öğretmen_notu
                │                 └─ not_görünürlüğü (özel|öğrenci|öğrenci_veli) [YENİ]
                │
                ├─1:N─ RecurrenceRule
                │        ├─ desen, günler[], saat
                │        ├─ başlangıç, bitiş_koşulu
                │        └─ tatil_atlansın_mı (bool)        [YENİ]
                │
                ├─1:N─ TimeOff  ← TATİL BLOĞU              [YENİ]
                │        ├─ tür (tatil|izin|resmi|diğer)
                │        ├─ başlık, başlangıç, bitiş
                │        ├─ tüm_gün (bool), saat_aralığı
                │        └─ çakışan_ders_politikası (iptal|ertele|dokunma)
                │
                ├─1:N─ Homework
                │        ├─ öğrenci_id, ders_id, başlık, açıklama
                │        ├─ son_tarih, durum
                │        ├─ dosyalar[]
                │        └─ öğretmen_geri_bildirimi          [YENİ]
                │
                ├─1:N─ Payment
                │        ├─ öğrenci_id, ders_id (nullable — paket ise)
                │        ├─ tutar, ödenen_tutar
                │        ├─ durum (bekliyor|tahsil|kısmi|gecikti)
                │        └─ işaretlenme_tarihi
                │
                ├─1:N─ MonthlyPackage
                │        ├─ öğrenci_id, ay, ders_sayısı, tutar
                │        └─ kullanılan_ders_sayısı
                │
                ├─1:N─ Review  (Faz 4)
                │        ├─ öğrenci_id, yıldız, metin
                │        ├─ alt_puanlar {anlatım, dakiklik, sabır, hazırlık}
                │        ├─ doğrulanmış_öğrenci (bool)
                │        ├─ öğretmen_yanıtı
                │        ├─ görünürlük (özel_geribildirim|herkese_açık)  ← Faz 1 vs Faz 4
                │        └─ şüpheli_bildirimi
                │
                └─1:N─ MatchRequest  (Faz 4)
                         ├─ öğrenci_id, mesaj
                         └─ durum (bekliyor|kabul|red)
```

---

## 15. PRD Boşlukları ve Çelişkiler ⚠️

Öğretmen rolünü A'dan Z'ye çıkarırken tespit edilen, **karar verilmesi gereken** noktalar:

### 15.1 Eksik Özellikler (PRD'de yok, gerekli)

| # | Boşluk | Etki | Önerilen Faz |
|---|---|---|---|
| B-01 | **Tatil / müsait değil bloğu** | Yüksek — öğretmen tatile çıkınca dersleri tek tek iptal etmek zorunda | Faz 1 |
| B-02 | **Ders erteleme** ayrı aksiyon olarak | Yüksek — en sık senaryo | Faz 1 |
| B-03 | **Tekrar eden ders düzenleme kapsamı** (bu / bu ve sonraki / tümü) | Kritik — olmadan M04 kullanılamaz | Faz 1 |
| B-04 | **Öğrenci arşivleme** | Orta — Free limitiyle doğrudan bağlantılı | Faz 1 |
| B-05 | **Not görünürlük kontrolü** (özel/öğrenci/veli) | Yüksek — olmadan öğretmen dürüst not tutmaz | Faz 1 |
| B-06 | **Öğrenci-öğretmen bağlanma akışı** | Kritik — PRD iki giriş yolunu söylüyor, birleşmeyi tanımlamıyor | Faz 2 |
| B-07 | **Öğrenci bazlı ücret** | Yüksek — tek fiyat gerçek kullanımı karşılamıyor | Faz 1 |
| B-08 | **Öğrenci gelmedi + ücretlendirme kararı** | Orta — gerçek hayatta çok yaşanır | Faz 1 |
| B-09 | **İptal vs. silme ayrımı** | Orta — veri bütünlüğü | Faz 1 |
| B-10 | **Ders yeri / online link alanı** | Orta | Faz 1 |
| B-11 | **Eşleştirmede görünürlük anahtarı** | Orta | Faz 4 |
| B-12 | **Gerçekleşen süre ≠ planlanan süre** | Düşük | Faz 1 |

### 15.2 Çelişkiler

| # | Çelişki | Detay |
|---|---|---|
| Ç-01 | **Aylık gelir özeti** | M07'de temel özellik olarak listelenmiş, Bölüm 9.1'de "Free ❌ / Premium ✅". Hangisi? |
| Ç-02 | **Ders & ödev hatırlatmaları** | M11'de "yaklaşan ders hatırlatması = Kritik" ve Faz 1.8'de var; ama 9.1'de "Free ❌". Öğretmene giden hatırlatma Free'de var, öğrencilere **gönderilen** hatırlatma mı Premium? Netleşmeli. |
| Ç-03 | **Geciken ödeme** | M07'de temel, 9.1'de Premium |
| Ç-04 | **Öğrenci limiti** | "5–10" aralık olarak yazılmış — kesin sayı gerekli |
| Ç-05 | **Erken geri bildirim → Faz 4** | Faz 1'de "sadece öğretmen görür" toplanan puanlar Faz 4'te herkese mi açılacak? Açılırsa öğretmenlerin önceden bilgilendirilmesi ve onayı şart. |

### 15.3 Öğretmen Rolü İçin Karar Bekleyen Sorular

1. Bir öğrenci **birden fazla öğretmene** bağlanabilir mi? (PRD sessiz — veri modelini doğrudan etkiler)
2. Öğretmen **grup dersi** verebilir mi? *(PRD: "Gelecek aşamalar" — yol haritası dışı, ama veri modeli buna hazır tasarlanmalı)*
3. Öğretmen-öğrenci **mesajlaşması** var mı? PRD'de öğretmen↔veli mesajı ve Faz 4 eşleştirme mesajı var; öğretmen↔öğrenci doğrudan mesaj tanımlı değil.
4. Öğretmen kendi verisini **dışa aktarabilir mi**? (KVKK açısından gerekebilir)
5. Free plandaki öğrenci limiti **aktif** öğrenci sayısı mı, toplam mı? (Arşivleme özelliği buna bağlı)

---

## 16. Faz Bazlı Öğretmen Yol Haritası

### FAZ 0 — Altyapı (2–3 hafta) **[PRD]**
- Kayıt / giriş / şifre sıfırlama
- Rol bazlı yetkilendirme (öğretmen rolü tanımı)
- Push notification altyapısı
- Ayarlar ve güvenlik ekranları

### FAZ 1 — Öğretmen Çekirdeği / MVP (4–6 hafta) **[PRD]**
> **[PRD] Hedef:** *"Öğretmenin uygulamayı her gün kullanmasını sağlayan minimum ürün."*
> **[PRD] Çıktı:** *"Öğretmen kendi öğrencilerini ekleyip derslerini yönetebilir. 5–10 gerçek öğretmenle beta test yapılmalıdır."*

| PRD # | İş | Öncelik |
|---|---|---|
| 1.1 | Öğretmen profili oluşturma/düzenleme | Kritik |
| 1.2 | Öğrenci ekleme — manuel | Kritik |
| 1.3 | Takvim — ders ekleme, tekrar eden ders, haftalık görünüm | Kritik |
| 1.4 | Ders oturumu kaydı (konu, süre, notlar, katılım) | Kritik |
| 1.5 | Not ve ödev ekleme, ödev durumu takibi | Kritik |
| 1.6 | Manuel ödeme takibi — basit | Yüksek |
| 1.8 | Yaklaşan ders push bildirimleri | Yüksek |
| 1.9 | Öğretmene özel geri bildirim | Orta |
| **+B-01** | **Tatil bloğu** | **Yüksek [YENİ]** |
| **+B-02** | **Ders erteleme** | **Yüksek [YENİ]** |
| **+B-03** | **Tekrar eden ders düzenleme kapsamı** | **Kritik [YENİ]** |
| **+B-05** | **Not görünürlük kontrolü** | **Yüksek [YENİ]** |
| **+B-07** | **Öğrenci bazlı ücret** | **Yüksek [YENİ]** |

### FAZ 2 — Öğrenci/Veli (4–5 hafta) — Öğretmen tarafı sınırlı **[PRD]**
- Öğrencinin bireysel çalışma verisini izleme (izin bazlı)
- **+B-06 Öğrenci-öğretmen bağlanma akışı [YENİ — kritik]**

### FAZ 3 — Gelişim & Bildirimler (3–4 hafta) **[PRD]**
- 3.1 Veli paneli — öğretmen verilerini kapsayan görünüm
- 3.2 Öğrenci gelişim takibi
- 3.3 Performans grafikleri
- 3.4 Hedef puan/net takibi
- 3.5 Bildirim genişletme (ödev, ödeme, çalışma)
- 3.6 Haftalık özet bildirimi
- 3.7 Gelişmiş ödeme takibi — aylık paket, geciken ödemeler

### FAZ 4 — Eşleştirme & Puanlama (4–5 hafta) **[PRD]**
- 4.1–4.5 Listelenme, filtreleme, açık profil, talep alma, doğrulama rozeti
- 4.6–4.9 Puanlanma, yorum alma, yorumlara yanıt, şüpheli yorum bildirme
- **+B-11 Eşleştirmede görünürlük anahtarı [YENİ]**

### FAZ 5 — Premium & Analitik (3–4 hafta) **[PRD]**
- 5.1–5.2 Abonelik altyapısı, Free/Premium kısıtları
- 5.3 Profil öne çıkarma
- 5.4 Öğrenci limiti kaldırma
- 5.5 Aylık gelir özeti ve kazanç analizi
- 5.6 PDF öğrenci raporu
- 5.7 Boş zaman analizi
- 5.9 WhatsApp/SMS hatırlatma

---

## 17. Faz 1 Kabul Kriterleri (Öğretmen Rolü)

**[TÜRETİLMİŞ]** — PRD "5–10 gerçek öğretmenle beta test" diyor; test edilebilir kriterler gerekli.

Faz 1 aşağıdakilerin **hepsi** doğrulanmadan tamamlanmış sayılmaz:

- [ ] Öğretmen kayıt olup profilini **5 dakikadan kısa sürede** tamamlayabiliyor
- [ ] Öğretmen öğrenci ekleyip aynı akış içinde ders planlayabiliyor
- [ ] Öğretmen tekrar eden ders oluşturabiliyor ve tek oturumu seriden bağımsız iptal edebiliyor
- [ ] Çakışan ders eklenirken uyarı görüyor
- [ ] Öğretmen ders tamamlama akışını **60 saniyeden kısa sürede** bitirebiliyor
- [ ] Ders tamamlanınca not + ödev + ödeme aynı akışta girilebiliyor
- [ ] Öğretmen tatil bloğu ekleyip çakışan dersleri toplu yönetebiliyor **[YENİ]**
- [ ] Öğretmen ders erteleyebiliyor ve öğrenciye bildirim gidiyor **[YENİ]**
- [ ] Öğretmen öğrenci bazlı bakiyeyi görebiliyor
- [ ] Yaklaşan ders push bildirimi doğru saatte geliyor
- [ ] Free limit dolduğunda anlaşılır bir yükseltme yönlendirmesi çıkıyor
- [ ] **Öğrenci uygulamaya hiç girmeden** öğretmen tüm akışları tamamlayabiliyor
- [ ] Öğretmen 7 gün boyunca uygulamayı **başka araç kullanmadan** (Excel/not defteri/takvim) kullanabiliyor ← *asıl test budur*

---

## 18. Özet — Öğretmen Rolü Tek Sayfada

| Boyut | Özet |
|---|---|
| **Kim** | Özel ders veren kişi. Sistemin ana kullanıcısı. |
| **Neden gelir** | Derslerini yönetmek için (öğrenci bulmak için değil — o Faz 4) |
| **Neden kalır** | Takvim + ders kaydı + ödeme takibi günlük alışkanlık olur |
| **Ana ekran** | Takvim (günlük görünüm) |
| **Kritik akış** | Ders oturumunu tamamlama (< 60 sn) |
| **Toplam yetenek** | **~95 yetenek** (T-01.1 … T-15.5) |
| **Sahip olduğu modül** | M02, M04, M05, M06, M07, M10, M14 (7 modülün birincil sahibi) |
| **İzleyici olduğu** | M08 (izin bazlı), M09 (veri kaynağı) |
| **Pasif olduğu** | M12 (listelenen), M13 (puanlanan) |
| **Faz 1'de hazır olması gereken** | Profil · Öğrenci ekleme · Takvim · Ders oturumu · Not/ödev · Basit ödeme |
| **En büyük risk** | Ders tamamlama akışında sürtünme → öğretmen Excel'e geri döner |
| **PRD'de en büyük boşluk** | Tatil/müsaitlik yönetimi, ders erteleme, tekrar eden ders düzenleme kapsamı |

---

*Bu doküman PRD v2.0'a dayanır. **[YENİ]** etiketli maddeler öneridir ve onayınızı bekler.*
