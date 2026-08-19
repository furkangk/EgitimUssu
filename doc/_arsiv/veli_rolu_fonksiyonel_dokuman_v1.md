# Veli Rolü — Fonksiyonel Yetenek ve Kullanım Akışı Dokümanı

> ⚠️ **ARŞİV (2026-08-19):** Bu doküman tarihîdir. Geçerli otorite `doc/roles/` + `doc/modules/`'tedir. Buradaki bilgi yalnızca geçmiş referans içindir; çelişkide roles/modules esastır.

**Ürün:** Özel Ders Yönetim ve Eşleştirme Platformu
**Kaynak:** `ozel_ders_platformu_PRD_v2.docx` (v2.0, Nisan 2025)
**Bu doküman:** v1.0 — Veli rolü detaylandırması
**Kardeş dokümanlar:** `ogretmen_rolu_fonksiyonel_dokuman_v1.md` · `ogrenci_rolu_fonksiyonel_dokuman_v1.md`
**Tarih:** 17 Temmuz 2026

---

## 0. Bu Doküman Nasıl Okunur

Diğer iki rol dokümanıyla aynı etiket sistemi:

| Etiket | Anlamı |
|---|---|
| **[PRD]** | PRD v2.0'da açıkça yazılmış |
| **[TÜRETİLMİŞ]** | PRD'de ima edilmiş, burada detaylandırıldı |
| **[YENİ]** | PRD'de yok — bu dokümanda önerilen ekleme. Onayınız gerekiyor. |

> ⚠️ **Bu dokümanın iki kritik bulgusu:**
> 1. **Bölüm 15.1** — PRD, veliyi *"bildirimlerle aktif kalır"* diye tanımlayıp bildirimleri Free'de kapatıyor. Öğrenci rolündeki streak çelişkisinin aynısı, ama bu kez **doğrudan büyüme stratejisinin cümlesiyle** çelişiyor.
> 2. **Bölüm 15.2** — **Veli, eşleştirme modülünde (M12) hiç yok.** Birincil kullanıcılar "Öğrenci / Öğretmen" olarak yazılmış. Oysa PRD velinin *"özellikle küçük yaş gruplarında"* dahil olduğunu söylüyor. 10 yaşındaki çocuk öğretmen seçmez; parayı ödeyen ve kararı veren velidir. Bu hâliyle en ödemeye yatkın segment eşleştirmeyi kullanamaz.

---

## 1. Rol Tanımı ve Stratejik Konum

### 1.1 Veli Kimdir

**[PRD Bölüm 4.3]** *"Özellikle küçük yaş gruplarında sürece dahil olur."*

Veli, üç rol içinde **en pasif yetkilere ama en aktif ekonomik güce** sahip olandır. Bu asimetri, rolün tüm tasarımını belirler:

| Rol | Yetki | Ekonomik güç | Günlük kullanım |
|---|---|---|---|
| Öğretmen | **Çok yüksek** (7 modülün sahibi) | Orta (abonelik öder) | **Çok yüksek** |
| Öğrenci | Orta (M08 sahibi) | **Yok** (geliri yok) | **Çok yüksek** |
| **Veli** | **Çok düşük** (salt okunur) | **En yüksek** (3 paketi de fiilen o öder) | **Düşük** (haftada 2–5) |

**Kritik çıkarım [TÜRETİLMİŞ]:** Veli, platformda neredeyse hiçbir şey **yapamaz** — ama üç Premium paketinin de (öğretmen hariç) parasını o öder. PRD Bölüm 5'teki gelir kalemlerinden **"Veli paneli premium"** ve fiilen **"Öğrenci premium paketi"** velinin cebinden çıkar. Rol, yetki açısından zayıf; gelir açısından belirleyicidir.

### 1.2 Velinin İki Veri Kaynağı **[PRD]**

Bu, veli rolünün en belirleyici yapısal özelliğidir. **[PRD M09]** aynen:

| Veri Kaynağı | İçerik | Durum |
|---|---|---|
| **Bireysel çalışma** | Haftalık çalışma süreleri, konu dağılımı, test performansı, streak | **Öğretmen gerekmez** |
| **Öğretmen bağlıysa** | Son ders özeti, verilen ödevler, öğretmen notları, ödeme özeti | Öğretmen gerekir |

**[PRD Bölüm 4.3]** *"İki farklı içerik görebilir: Çocuğunun bireysel çalışma verileri (öğretmenden bağımsız) · Öğretmenle ilgili ders, ödev ve performans verileri (öğretmen bağlıysa)"*

Yani **veli paneli iki hâlde çalışır** ve öğretmensiz hâli tek başına ayakta durmalıdır. Bu, PRD'nin büyüme tezinin veli ayağıdır:

**[PRD Bölüm 2.1]** *"Veliyi platforma dahil et — çocuğunun gelişimini şeffaf şekilde görüntülesin."*
**[PRD Bölüm 10.2]** *"Veli — çocuğun gelişimini görmek için gelir, **bildirimlerle aktif kalır**."*

> Bu son cümle Bölüm 15.1'deki çelişkinin kanıtıdır: velinin elde tutma mekanizması **bildirimler** olarak tanımlanmış, ancak Bölüm 9.3'te bildirimler **Free'de ❌**.

### 1.3 Velinin Beklentileri (PRD Bölüm 4.3 + M09)

**[PRD M09 "Temel görünümler"]**
1. Çocuğun o hafta kaç saat çalıştığı
2. Hangi derslere ne kadar zaman ayırdığı
3. Test performansı özeti
4. Yaklaşan dersler *(öğretmen bağlıysa)*
5. Öğretmen mesajları *(öğretmen bağlıysa)*

### 1.4 Velinin Çözdüğü Problem (PRD Bölüm 3)

**[PRD]** Öğretmen tarafında: *"Veli sürece şeffaf şekilde dahil edilemez."*
**[PRD]** Öğrenci tarafında: *"Velinin çocuğunun çalışmasını takip etmesi için doğrudan bir araç yoktur."*

Velinin tek problemi vardır ve tek kelimeyle ifade edilir: **görünürlük.** Veli bu ürüne bir şey *yapmak* için değil, *görmek* için gelir. Rol tasarımı bunu kabul etmeli ve veliye sahte yetkiler vermeye çalışmamalıdır.

---

## 2. Velinin Sahip Olduğu Modüller

**[PRD]** PRD Bölüm 6'daki 15 modülden veliyi ilgilendirenler:

| Modül | Ad | Velinin Rolü | Faz |
|---|---|---|---|
| M01 | Kullanıcı ve Rol Yönetimi | Kullanıcı | Faz 0 |
| M03 | Öğrenci Profili | **İzleyici** (çocuğunun profili) | Faz 2 |
| M04 | Takvim | **İzleyici** (yaklaşan dersler) | Faz 3 |
| M05 | Ders Oturumu | **İzleyici** (son ders özeti) | Faz 3 |
| M06 | Not ve Ödev | **İzleyici** (ödev görüntüleme) | Faz 3 |
| M07 | Manuel Ödeme | **İzleyici** (ödeme özeti) — *ama ödeyen taraf* | Faz 3 |
| M08 | Öğrenci Bireysel Çalışma | **İzleyici** (izin bazlı) | Faz 2 |
| **M09** | **Veli Paneli** | **BİRİNCİL SAHİP — rolün tamamı** | **Faz 2** |
| M10 | Öğrenci Gelişim Takibi | **İzleyici** — *PRD'de birincil kullanıcı olarak listeli* | Faz 3 |
| M11 | Bildirim ve Hatırlatma | **Alıcı** — velinin retention motoru | Faz 3 |
| M12 | Eşleştirme ve Keşif | **⚠️ PRD'DE YOK** — kritik boşluk | Faz 4 |
| M13 | Puanlama ve Yorum | **⚠️ Yetkisi yok** — yalnızca öğrenci yorum yapabilir | Faz 4 |
| M14 | Raporlama | **⚠️ PRD'de birincil kullanıcı: Öğretmen** | Faz 5 |
| M15 | Ayarlar ve Güvenlik | Kullanıcı | Faz 0+ |

**[PRD Bölüm 6]** M10'un birincil kullanıcısı **"Öğretmen / Veli"** olarak yazılmış — veli, M09 dışında yalnızca burada birincil kullanıcı sayılıyor.

**Gözlem:** Velinin 15 modülden **13'ünde rolü "izleyici"dir.** Sahip olduğu tek modül M09'dur ve M09'un kendisi de diğer modüllerin bir **görüntüleme katmanıdır** — kendi verisi yoktur. Veli rolü, teknik olarak **bir dashboard'dur.**

---

## 3. Veli Yaşam Döngüsü

```
GİRİŞ                    HAFTALIK DÖNGÜ              KARAR ANLARI
─────                    ──────────────              ────────────
Çocuk davet eder         ┌─ Bildirim gelir       →   "Netler düşüyor"
   veya                  │  "Haftalık özet"              ↓
Çocuğunu davet eder      │  ↓                       Öğretmen lazım
   ↓                     ├─ Paneli açar                  ↓
Kayıt ol (Veli)          │  Bu hafta kaç saat?      ⚠️ PRD'DE VELİ
   ↓                     │  Hangi derse ne kadar?   ÖĞRETMEN ARAYAMAZ
Çocuğu bağla             │  Test performansı            (Bölüm 15.2)
   ↓                     │  Streak devam ediyor mu?      ↓
İlk veri gelir           │  ↓                       Ay sonu:
   ↓                     ├─ (öğretmen varsa)        ├─ Ödeme özeti
Bildirim tercihleri      │  Son ders özeti          ├─ "ödedim mi?"
   ↓                     │  Ödevler                 └─ Premium kararı
╔══════════════════╗     │  Öğretmen notları
║ ÖĞRETMENSİZ DE   ║     │  Yaklaşan dersler
║ DEĞER GÖRÜR      ║     └──┘
╚══════════════════╝
```

### 3.1 Kullanım Sıklığı Haritası **[TÜRETİLMİŞ]**

Veli, üç rol içinde **en düşük frekanslı** kullanıcıdır ve bu normaldir:

| Sıklık | Aksiyon | Tetikleyici |
|---|---|---|
| **Haftada 1–3** | Panel açma | **Bildirim** ← tek gerçek tetikleyici |
| Haftada 1 | Haftalık özet okuma | Push bildirimi |
| Ayda 2–4 | Ödeme durumu kontrolü | Ay sonu |
| Ayda 0–2 | Öğretmen mesajı okuma/yanıtlama | Bildirim |
| Ayda 0–1 | Ödev kontrolü | Merak / bildirim |
| 3 ayda 1 | Gelişim raporu inceleme | Karne dönemi |

> **Bu tablo, Bölüm 15.1'in neden kritik olduğunu açıklar.** Velinin uygulamayı açma sebebi neredeyse **tamamen bildirimdir.** Bildirim yoksa veli paneli, kimsenin açmadığı bir web sayfasıdır. PRD Bölüm 10.2 bunu kendisi söylüyor: *"bildirimlerle aktif kalır."* Bildirimler Free'de kapatılırsa Free veli **hiç geri gelmez** — ve Premium'a geçmesi için önce geri gelmesi gerekir.

---

## 4. Veli Yetenek Matrisi (Tam Liste)

### M01 — Hesap ve Rol

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-01.1 | Veli olarak kayıt olabilir | [PRD] | 2 |
| V-01.2 | Giriş yapabilir | [PRD] | 2 |
| V-01.3 | Şifre yenileyebilir/sıfırlayabilir | [PRD] | 2 |
| V-01.4 | Rolüne özel ekranları görür | [PRD] | 2 |
| V-01.5 | Hesabını kapatabilir / veri sildirebilir | [PRD] | 2 |
| V-01.6 | **Çocuğunun 18 yaş altı kaydını onaylayabilir (KVKK açık rıza)** | **[YENİ]** | 0 |
| V-01.7 | **Veli olduğu doğrulanmalıdır** — kimlik/ilişki doğrulaması | **[YENİ]** | 2 |
| V-01.8 | Rolünü değiştiremez | [TÜRETİLMİŞ] | 2 |

### M09 — Veli Paneli ⭐ (ROLÜN TAMAMI)

#### 4.1 Çocuk Bağlantısı

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-09.1 | **Çocuğuyla bağlantı kurabilir** | [PRD 2.8] | 2 |
| V-09.2 | Çocuğunun davetini kabul edebilir | [TÜRETİLMİŞ] | 2 |
| V-09.3 | Çocuğunu davet edebilir | [TÜRETİLMİŞ] | 2 |
| V-09.4 | **Birden fazla çocuk bağlayabilir** | **[YENİ]** | 2 |
| V-09.5 | **Çocuklar arasında geçiş yapabilir** | **[YENİ]** | 2 |
| V-09.6 | Bağlantıyı kaldırabilir | **[YENİ]** | 2 |
| V-09.7 | **Çocuğun hesabını silemez** | [TÜRETİLMİŞ] | 2 |

#### 4.2 Bireysel Çalışma Görünümü (Öğretmen Gerekmez) **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| V-09.8 | **Çocuğun o hafta kaç saat çalıştığını** görür | [PRD] | 2 | **Free ✅** |
| V-09.9 | **Hangi derslere ne kadar zaman ayırdığını** görür | [PRD] | 2 | Free ✅* |
| V-09.10 | **Test performansı özetini** görür | [PRD] | 2 | Free ✅* |
| V-09.11 | Streak (seri gün) bilgisini görür | [PRD M09] | 2 | Free ✅* |
| V-09.12 | Konu dağılımını görür | [PRD] | 2 | Free ✅* |
| V-09.13 | **Çalışma süresi geçmişini** görür | [PRD 9.3] | 2 | ⚠️ **Premium** |
| V-09.14 | **Detaylı gelişim grafiklerini** görür | [PRD 9.3] | 3 | ⚠️ **Premium** |
| V-09.15 | **Haftalık raporu** görür | [PRD 9.3] | 3 | ⚠️ **Premium** |
| V-09.16 | **Çocuğun kişisel seans notlarını GÖREMEZ** | **[YENİ]** | 2 | Hiçbiri |
| V-09.17 | **Çocuğun gizlediği veriyi göremez** | [PRD 2.10] | 2 | — |

*9.3 tablosunda açıkça listelenmemiş; M09 "temel görünümler" kapsamında Free varsayıldı — **netleştirilmeli** (Ç-04).

#### 4.3 Öğretmen Bağlıysa Görünen Veriler **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| V-09.18 | **Son ders özetini** görür | [PRD] | 3 | **Free ✅** |
| V-09.19 | **Verilen ödevleri** görür | [PRD] | 3 | **Free ✅** |
| V-09.20 | **Yaklaşan dersleri** görür | [PRD] | 3 | **Free ✅** |
| V-09.21 | **Öğretmen notlarını** görür *(paylaşıma açıksa)* | [PRD] | 3 | Free ✅ |
| V-09.22 | **Ödeme özetini** görür | [PRD] | 3 | Free ✅ |
| V-09.23 | **Öğretmen mesajlarını** görür | [PRD] | 3 | Free ✅ |
| V-09.24 | **Öğretmene yanıt verebilir mi? — PRD sessiz** | [PRD boşluk] | 3 | ? |
| V-09.25 | Ödev tamamlanma durumunu görür | [TÜRETİLMİŞ] | 3 | Free |

#### 4.4 Bildirimler ⚠️

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| V-09.26 | **Bildirim tercihlerini yönetebilir** | [PRD 2.10] | 2 | — |
| V-09.27 | **Haftalık özet bildirimi alır** | [PRD M11 "Tümü"] | 3 | ⚠️ **Premium** |
| V-09.28 | **Bildirimler — genel** | [PRD 9.3] | 3 | ⚠️ **Premium ❌ Free** |
| V-09.29 | WhatsApp/SMS bildirimi alır | [PRD M11] | 5 | Premium |
| V-09.30 | **Yaklaşan ders bildirimi — PRD'de veli hedef DEĞİL** | [PRD boşluk] | 3 | ? |
| V-09.31 | **Ödeme gecikmesi bildirimi — PRD'de hedef: Öğretmen** | [PRD boşluk] | 3 | ? |
| V-09.32 | **Çocuk hedefini tutturamıyor uyarısı** | **[YENİ]** | 3 | Premium |
| V-09.33 | **Streak kırıldı bildirimi** | **[YENİ]** | 3 | Premium |

> 🚨 **[PRD M11] bildirim tablosunda veli hiçbir satırda açıkça hedef değildir.** Yalnızca "Haftalık özet → Tümü" ve "WhatsApp/SMS → Tümü" satırlarında dolaylı olarak kapsanır. Ödeme gecikmesi bildirimi **öğretmene** gider — oysa **parayı veli öder.** Bölüm 15.3.

### M03/M10 — Çocuğun Profili ve Gelişimi

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-10.1 | Çocuğunun profilini görüntüleyebilir | [TÜRETİLMİŞ] | 2 |
| V-10.2 | Konu kazanım durumunu görür | [PRD M10] | 3 |
| V-10.3 | Test performansı zaman serisini görür | [PRD M10] | 3 |
| V-10.4 | Eksik ve güçlü konuları görür | [PRD M10] | 3 |
| V-10.5 | Hedef puan/seviyeyi görür | [PRD M10] | 3 |
| V-10.6 | **Öğretmen değerlendirme notlarını görür** | [PRD M10] | 3 |
| V-10.7 | **Çocuğun profilini düzenleyemez** — karar gerekli (küçük yaşta?) | [PRD boşluk] | 2 |
| V-10.8 | Gelişim verisini **değiştiremez** | [TÜRETİLMİŞ] | 3 |

### M07 — Ödeme (İzleyici — ama ödeyen taraf)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-07.1 | Ödeme özetini görür | [PRD M09] | 3 |
| V-07.2 | Bakiyeyi/geciken tutarı görür | [TÜRETİLMİŞ] | 3 |
| V-07.3 | **Platform üzerinden ödeme YAPAMAZ** | [PRD Bölüm 5] | — |
| V-07.4 | **"Ödedim" bildirimi gönderemez** — PRD'de yok | **[YENİ]** | 3 |
| V-07.5 | Ödemeyi "tahsil edildi" işaretleyemez — bu öğretmenin yetkisi | [PRD M07] | — |

### M12 — Eşleştirme ⚠️ **(PRD'DE VELİ YOK)**

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-12.1 | **Öğretmen arayabilir mi?** — PRD'de birincil kullanıcı: "Öğrenci / Öğretmen" | **[YENİ]** | 4 |
| V-12.2 | **Öğretmen profili inceleyebilir mi?** | **[YENİ]** | 4 |
| V-12.3 | **Talep gönderebilir mi?** | **[YENİ]** | 4 |
| V-12.4 | **Çocuğunun öğretmen talebini onaylayabilir mi?** | **[YENİ]** | 4 |

> 🚨 Bölüm 15.2 — bu dokümanın en önemli bulgusu.

### M13 — Puanlama ⚠️ **(Velinin yetkisi yok)**

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-13.1 | **Yorum yapamaz** — *"yalnızca o öğretmenden ders almış öğrenciler yorum yapabilir"* | [PRD] | 4 |
| V-13.2 | Yorumları okuyabilir | [TÜRETİLMİŞ] | 4 |
| V-13.3 | **Küçük yaşta çocuk adına yorum yapabilir mi?** — karar gerekli | **[YENİ]** | 4 |

### M15 — Ayarlar

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| V-15.1 | Şifre değiştirme | [PRD] | 2 |
| V-15.2 | Bildirim tercihleri | [PRD 2.10] | 2 |
| V-15.3 | Hesap kapatma / veri silme | [PRD] | 2 |
| V-15.4 | Abonelik yönetimi (Veli premium) | [PRD 5.10] | 5 |
| V-15.5 | **Çocuğun Premium'unu satın alabilir** | **[YENİ]** | 5 |

---

## 5. Ekran Haritası

**[TÜRETİLMİŞ]** Veli uygulaması **bilinçli olarak sığ** olmalıdır. Veli haftada 2 kez, 90 saniye kullanır. Derin navigasyon burada düşmandır.

```
VELİ UYGULAMASI
│
├── [Alt Sekme 1] 🏠 ÖZET  ← AÇILIŞ (tek ekranda her şey)
│   │
│   ├── ┌─ ÇOCUK SEÇİCİ (birden fazla çocuk varsa) [YENİ] ─┐
│   │   │  [ Ayşe ]  [ Mehmet ]                             │
│   │   └────────────────────────────────────────────────────┘
│   │
│   ├── ⏱️ BU HAFTA
│   │   ├── Toplam çalışma süresi        ← [PRD] temel görünüm 1
│   │   ├── Geçen haftaya göre değişim
│   │   └── Streak göstergesi 🔥
│   │
│   ├── 📊 DERS DAĞILIMI                 ← [PRD] temel görünüm 2
│   │   └── Hangi derse ne kadar (grafik)
│   │
│   ├── 📈 TEST PERFORMANSI              ← [PRD] temel görünüm 3
│   │   └── Özet + trend
│   │
│   ├── ┌─ ÖĞRETMEN YOKSA ────────────────────────────┐
│   │   │ "Ayşe henüz bir öğretmenle çalışmıyor"      │
│   │   │ [Öğretmen Bul]  ← ⚠️ PRD'DE YOK (15.2)      │
│   │   └──────────────────────────────────────────────┘
│   │
│   └── ┌─ ÖĞRETMEN VARSA ────────────────────────────┐
│       │ 📅 Yaklaşan ders    ← [PRD] temel görünüm 4 │
│       │ 📝 Son ders özeti                            │
│       │ 📚 Bekleyen ödevler                          │
│       │ 💬 Öğretmen mesajı  ← [PRD] temel görünüm 5 │
│       │ 💰 Ödeme durumu                              │
│       └──────────────────────────────────────────────┘
│
├── [Alt Sekme 2] 📈 GELİŞİM  (Faz 3)
│   ├── Detaylı grafikler        ⚠️ Premium
│   ├── Konu kazanımları
│   ├── Eksik / güçlü konular
│   ├── Hedef vs. gerçekleşen
│   ├── Çalışma geçmişi          ⚠️ Premium
│   └── Öğretmen değerlendirmeleri
│
├── [Alt Sekme 3] 💬 ÖĞRETMEN  (öğretmen varsa)
│   ├── Mesajlar
│   ├── Öğretmen profili
│   └── Ödeme özeti
│
└── [Alt Sekme 4] 👤 PROFİL
    ├── Çocuklarım (+ ekle)      [YENİ]
    ├── Bildirim ayarları ⭐
    ├── Abonelik (Faz 5)
    └── Ayarlar & Güvenlik
```

### 5.1 Faz Bazlı Panel Durumu

| Bölüm | Faz 2 | Faz 3 | Faz 4 | Faz 5 |
|---|---|---|---|---|
| Bireysel çalışma özeti | ✅ **[PRD 2.9]** | ✅ | ✅ | ✅ |
| Öğretmen verileri | ❌ | ✅ **[PRD 3.1]** | ✅ | ✅ |
| Bildirimler | Tercihler **[PRD 2.10]** | ✅ **[PRD 3.5/3.6]** | ✅ | ✅ WhatsApp |
| Gelişim grafikleri | ❌ | ✅ **[PRD 3.2/3.3]** | ✅ | ✅ |
| Öğretmen bulma | ❌ | ❌ | **⚠️ PRD'de yok** | — |
| Premium | ❌ | ❌ | ❌ | ✅ **[PRD 5.10]** |

**[PRD 2.9]** *"Veli paneli — bireysel çalışma verileri (öğretmensiz)"* — **Kritik**
**[PRD 3.1]** *"Veli paneli — öğretmen verilerini de kapsayan entegre görünüm"* — **Kritik**

Bu iki iş kalemi, velinin iki veri kaynağının **iki ayrı fazda** açıldığını gösterir. Faz 2'de veli paneli tek kaynaklıdır (yalnızca çocuğun bireysel çalışması), Faz 3'te ikinci kaynak eklenir.

---

## 6. Detaylı Kullanım Akışları — Giriş ve Bağlantı

### AKIŞ 1: Veli Kaydı

**Aktör:** Yeni veli | **Faz:** 2 | **PRD:** 2.8 (Kritik)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Uygulamayı açar | "Öğretmen misin, Öğrenci mi, Veli mi?" |
| 2 | **"Veliyim"** seçer | Kayıt formu |
| 3 | E-posta/telefon + şifre | Doğrulama kodu |
| 4 | Kodu girer | Hesap oluşur, rol = VELİ |
| 5 | Ad soyad girer | — |
| 6 | — | **"Çocuğunu bağlayalım"** → AKIŞ 2 |
| 7 | *(bağlantı kurulunca)* | **Bildirim tercihleri sorulur** ⭐ **[PRD 2.10]** |
| 8 | — | Panel açılır |

**Tasarım kuralı [TÜRETİLMİŞ]:** Veli hesabı **çocuk bağlanmadan anlamsızdır** — velinin kendi verisi yoktur. Bağlantı kurulmadan panel boş bir kabuktur. Bu yüzden onboarding, bağlantı akışına doğrudan bağlanmalıdır.

**Adım 7 kritik [PRD 2.10]:** Bildirim tercihleri **onboarding'in parçası olmalı**, ayarlar menüsüne gömülmemelidir. Velinin tek geri dönüş mekanizması bildirimdir (Bölüm 3.1); izin ilk anda alınmazsa veli bir daha gelmez.

---

### AKIŞ 2: Çocuk Bağlama

**Aktör:** Veli veya Öğrenci | **Faz:** 2 | **PRD:** 2.8 (Kritik)

**[PRD]** *"Veli profili ve öğrenciyle bağlantı kurma"* — ama **kimin başlattığı tanımlı değil.** İki yön de desteklenmelidir (öğrenci dokümanı AKIŞ 8 ile aynı akış, veli tarafından):

**Yön A — Veli çocuğunu davet eder:**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | **Çocuk Ekle** | Form |
| 2 | Çocuğun telefonu/e-postası **veya** davet kodu | Kayıt aranır |
| 3 | — | **Kayıtlıysa:** öğrenciye onay talebi |
| 4 | *(18+ öğrenci onaylar)* | Bağlantı kurulur |
| 4' | *(18 altı öğrenci)* | **Onay gerekmez** — otomatik bağlanır **[YENİ]** |
| 5 | — | Panel dolmaya başlar |

**Yön B — Çocuk veliyi davet eder:** *(öğrenci dokümanı AKIŞ 8/Yön A)*

**Alternatif akış 3a — Çocuk kayıtlı değil [YENİ]:**
> "Ayşe henüz kayıtlı değil. Ona davet linki gönderelim mi?"
> → SMS/WhatsApp ile davet → çocuk kaydolunca otomatik bağlanır

**⚠️ Yaş kuralı [YENİ]** *(öğrenci dokümanıyla tutarlı)*:

| Çocuğun yaşı | Bağlantı | Gerekçe |
|---|---|---|
| **< 18** | Veli hakkıdır, çocuk **reddedemez** | Yasal velayet |
| **18+** | **Çocuğun onayı zorunlu** | Yetişkin mahremiyeti |

---

### AKIŞ 3: Veli Kimlik/İlişki Doğrulaması **[YENİ]**

**Aktör:** Sistem | **Faz:** 2 | **Durum: PRD'de YOK — güvenlik açığı**

**⚠️ PRD'de hiç kimse velinin gerçekten veli olduğunu doğrulamıyor.** Bugünkü tasarımla:

> Herhangi biri "veliyim" diyerek kaydolur, bir çocuğun telefon numarasını girer ve — çocuk 18 yaşından küçükse **otomatik bağlanma kuralıyla** — o çocuğun çalışma saatlerini, konumunu (şehir/ilçe), okulunu, öğretmenini ve günlük rutinini görmeye başlar.

Bu, bir çocuk uygulamasında kabul edilebilir bir açık değildir. Öğretmen için PRD "profil doğrulama mekanizması" ve "doğrulama rozeti" tanımlıyor **[PRD M01, M12]** — ama **veli için hiçbir doğrulama yok.** Oysa risk profili velide daha yüksektir: öğretmen bir yetişkine ders verir, veli ise bir çocuğun tüm rutinini görür.

**Önerilen katmanlı doğrulama:**

| Katman | Yöntem | Ne zaman |
|---|---|---|
| 1 | **Çocuk onayı** (18+ ise zorunlu) | Her bağlantıda |
| 2 | **Öğrenci tarafından başlatılan davet tercih edilir** | Varsayılan yön |
| 3 | **Öğretmen teyidi** — öğretmen bağlıysa: *"Bu kişi Ayşe'nin velisi mi?"* | Öğretmen varsa |
| 4 | **Tek veli kısıtı** — birincil veli, ikinci veliyi onaylar | 2. veli eklenirken |
| 5 | **Bildirim şeffaflığı** — çocuğa ve mevcut veliye *"X hesabın veli olarak bağlandı"* | Her bağlantıda |

**Minimum kural [YENİ]:** Bir bağlantı **hiçbir zaman sessizce** kurulmamalıdır. 18 altı çocuk reddedemese bile **haberdar edilmelidir**; mevcut veli varsa **o da bilgilendirilmelidir.**

---

### AKIŞ 4: 18 Yaş Altı Kayıt Onayı **[YENİ]**

**Aktör:** Veli | **Faz:** 0 | **Kaynak:** Öğrenci dokümanı AKIŞ 2'nin veli tarafı

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | *(Çocuk kaydolur, yaş < 18)* | Veliye onay talebi (SMS/e-posta) |
| 2 | Veli linke tıklar | Onay ekranı |
| 3 | **KVKK aydınlatma metnini** okur **[YENİ]** | Hangi veri toplanıyor, kim görüyor, ne kadar saklanıyor |
| 4 | **Açık rıza verir** | Çocuğun hesabı **tam aktif** olur |
| 5 | — | Veli otomatik bağlanır, panel açılır |

**[PRD Bölüm 10.3]** KVKK'yı yalnızca ödeme altyapısı için anıyor. Ancak açık rıza, **veri toplanmaya başladığı anda** gerekir — Faz 5'te değil **Faz 0'da.**

---

## 7. Detaylı Kullanım Akışları — Panel (M09)

### AKIŞ 5: Haftalık Panel Kontrolü ⭐ **(Velinin Ana Akışı)**

**Aktör:** Veli | **Faz:** 2 | **PRD:** 2.9 (Kritik)

Bu, velinin **tek gerçek akışıdır.** Öğretmende "ders tamamlama", öğrencide "çalışma seansı" ne ise, velide budur — ama farkla: **veli bu akışa bildirim olmadan girmez.**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | **Push bildirimi alır** ⭐ | *"Ayşe bu hafta 12 saat çalıştı"* |
| 2 | Bildirime dokunur | **Özet ekranı** (açılış) |
| 3 | Bu haftaki toplam süreyi görür **[PRD]** | Geçen haftayla karşılaştırma |
| 4 | Ders dağılımını görür **[PRD]** | Grafik |
| 5 | Test performansını görür **[PRD]** | Özet + trend |
| 6 | Streak'i görür | 🔥 14 gün |
| 7 | *(öğretmen varsa)* son ders özetini görür **[PRD]** | Konu + öğretmen notu |
| 8 | *(öğretmen varsa)* ödevleri görür **[PRD]** | Bekleyen/tamamlanan |
| 9 | *(öğretmen varsa)* yaklaşan dersi görür **[PRD]** | Tarih/saat |
| 10 | Uygulamayı kapatır | **Toplam süre: ~90 saniye** |

**Tasarım kuralı [TÜRETİLMİŞ]:** Adım 3–9'un **tamamı tek ekranda, kaydırmadan veya tek kaydırmayla** görünmelidir. Veli sekme gezmez, filtre açmaz, tarih aralığı seçmez. **[PRD M09]** "temel görünümler" listesi tam olarak bu ekranın içeriğidir — PRD'nin 5 maddesi, tek ekranın 5 kartıdır.

**Boş durum — çocuk hiç çalışmadıysa [YENİ]:**
> "Ayşe bu hafta henüz çalışma kaydetmemiş."
> *(Suçlayıcı değil, nötr dil. "Ayşe çalışmıyor!" değil.)*

Bu önemli: veli paneli bir **gözetim aracı** gibi hissettirirse çocuk gizlilik ayarlarını kapatır **[PRD 2.10 buna izin veriyor]** ve veri akışı durur. Panelin dili, çocuğu değil gelişimi merkeze almalıdır.

---

### AKIŞ 6: Bildirim Tercihleri **[PRD 2.10]**

**Aktör:** Veli | **Faz:** 2 | **PRD:** *"Veli bildirim tercihleri ve izin bazlı görünürlük"* (Yüksek)

| Bildirim | Varsayılan | Sıklık | Kaynak |
|---|---|---|---|
| **Haftalık özet** | ✅ Açık | Pazar akşamı | [PRD M11] |
| Çocuk bugün çalıştı | ❌ Kapalı | Günlük | **[YENİ]** — çok sık, gözetim hissi |
| Streak kırıldı | ✅ Açık | Olayda | **[YENİ]** |
| Hedef tutturulamadı | ❌ Kapalı | Haftalık | **[YENİ]** |
| Yeni ödev verildi | ✅ Açık | Olayda | **[YENİ]** |
| Ödev gecikti | ✅ Açık | Olayda | **[YENİ]** |
| Yaklaşan ders | ✅ Açık | 1 saat önce | **[YENİ]** — PRD'de veli hedef değil |
| Ders tamamlandı + özet | ✅ Açık | Olayda | **[YENİ]** |
| Öğretmen mesajı | ✅ Açık | Olayda | [TÜRETİLMİŞ] |
| **Ödeme gecikti** | ✅ Açık | Olayda | **[YENİ]** — PRD'de öğretmene gidiyor |
| WhatsApp/SMS | Premium | — | [PRD] |

> **Kritik denge [YENİ]:** Veli bildirimleri **haftalık ritimde** olmalıdır, günlük değil. Günlük bildirim iki şeyi birden öldürür: veli bildirimleri kapatır (retention gider) ve çocuk gözetlendiğini hisseder (veri akışı gider). PRD'nin haftalık özeti doğru seçimdir.

---

### AKIŞ 7: Gelişim İnceleme (Faz 3)

**Aktör:** Veli | **Faz:** 3 | **PRD:** 3.1, 3.2, 3.3 (Kritik/Yüksek)

**[PRD 3.1]** *"Veli paneli — öğretmen verilerini de kapsayan entegre görünüm"* — **Kritik**

Faz 3'te velinin iki veri kaynağı **birleşir.** Bu birleşme, velinin Premium'a geçme anıdır:

| Görünüm | Veri kaynağı | Free/Premium |
|---|---|---|
| Haftalık çalışma süresi | Bireysel (M08) | Free **[PRD 9.3]** |
| Son ders özeti | Öğretmen (M05) | Free **[PRD 9.3]** |
| Ödev görüntüleme | Öğretmen (M06) | Free **[PRD 9.3]** |
| Yaklaşan dersler | Öğretmen (M04) | Free **[PRD 9.3]** |
| **Detaylı gelişim grafikleri** | Her ikisi (M10) | **Premium [PRD 9.3]** |
| **Haftalık rapor** | Her ikisi | **Premium [PRD 9.3]** |
| **Çalışma süresi geçmişi** | Bireysel (M08) | **Premium [PRD 9.3]** |
| **Bildirimler** | — | **Premium [PRD 9.3]** ⚠️ |

**Entegre görünümün değeri [TÜRETİLMİŞ]:** Veli için asıl içgörü, iki kaynağın **kesişimindedir**:
> *"Ayşe matematiğe haftada 6 saat ayırıyor (bireysel veri) ve matematik neti 12'den 18'e çıktı (bireysel veri) — öğretmeni de son 3 derste 'konu kavrandı' notu girmiş (öğretmen verisi)."*

Bu cümle, ne tek başına M08'den ne tek başına öğretmen verisinden çıkar. **Veli Premium'un asıl vaadi budur** ve PRD'nin 3.1'e "Kritik" demesi doğrudur.

---

### AKIŞ 8: Öğretmenle İletişim (Faz 3)

**Aktör:** Veli | **Faz:** 3 | **PRD:** M09 *"Öğretmen mesajları (öğretmen bağlıysa)"*

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Öğretmen sekmesi | Mesajlar |
| 2 | Öğretmenin mesajını okur **[PRD]** | — |
| 3 | **Yanıt verir — ⚠️ PRD'de tanımsız** | ? |

> **⚠️ Boşluk:** PRD **[M09]** öğretmen fonksiyonu olarak *"Veliye mesaj gönderebilir"* diyor ve veli görünümünde *"Öğretmen mesajları"* var. Ancak **velinin yanıt verebildiği hiçbir yerde yazmıyor.** Tek yönlü mesajlaşma, velinin en doğal ihtiyacını ("hocam Ayşe bu hafta gelemeyecek", "netleri neden düştü?") karşılamaz ve iletişim WhatsApp'a geri kaçar — yani PRD Bölüm 3'teki *"iletişim ve düzen takibi dağınık kalır"* problemi çözülmemiş olur. **Karar gerekli.**

---

### AKIŞ 9: Ödeme Görüntüleme (Faz 3)

**Aktör:** Veli | **Faz:** 3 | **PRD:** M09 *"ödeme özeti"*

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ödeme özeti | Bu ay: 8 ders · 4.000 TL · **Bekliyor** |
| 2 | Detayı görür | Ders bazlı liste |
| 3 | **"Ödedim" bildirimi gönderir [YENİ]** | Öğretmene bildirim |
| 4 | *(Öğretmen teyit eder)* | Durum = TAHSİL EDİLDİ |

> **[YENİ] Öneri — "Ödedim" bildirimi:** PRD'de veli ödemeyi **yalnızca görür**, hiçbir aksiyon alamaz. Ama parayı **veli öder.** Bugünkü tasarımla akış şöyle: veli havale yapar → WhatsApp'tan "hocam gönderdim" yazar → öğretmen uygulamaya girip işaretler. Yani platform, ödeme akışının ortasından atlanır.
>
> **"Ödedim" bildirimi** (para transferi değil, sadece beyan) bu boşluğu kapatır: veli işaretler → öğretmene bildirim → öğretmen teyit eder → kayıt kapanır. **[PRD Bölüm 5]** "para tahsilatı yapılmaz" kuralı **ihlal edilmez** — para yine elden/havale gider, platform yalnızca mutabakatı kaydeder. Bu, M07'nin var olma amacıyla ("manuel ödeme takibi") tam uyumludur.

---

## 8. Detaylı Kullanım Akışları — Faz 4/5

### AKIŞ 10: Öğretmen Arama ⚠️ **[YENİ — PRD'de yok]**

**Aktör:** Veli | **Faz:** 4 | **Durum: PRD M12'de veli birincil kullanıcı DEĞİL**

Bölüm 15.2'deki bulgunun akış karşılığı. PRD'de bu akış **mevcut değildir** — önerilmektedir:

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Panel → *"Ayşe'nin öğretmeni yok"* → **Öğretmen Bul** | Liste |
| 2 | Filtreler | Branş · Şehir · Ücret · Ders şekli · Uygun saatler **[PRD M12]** |
| 3 | Profilleri inceler | Puan, yorumlar, doğrulama rozeti **[PRD M12]** |
| 4 | Talep gönderir | Öğretmene bildirim |
| 5 | *(Öğretmen kabul eder)* | Bağlantı: **öğretmen ↔ çocuk** |
| 6 | — | Çocuğun paneli dolar, velinin paneli 2. kaynağı açar |

**Alternatif — Onay modeli [YENİ]:** Çocuk (13–17) öğretmen bulur, **veli onaylar:**
| Adım | Aktör | Sistem |
|---|---|---|
| 1 | Çocuk talep gönderir | **Veliye onay bildirimi** |
| 2 | Veli öğretmen profilini inceler | — |
| 3 | Veli **onaylar** | Talep öğretmene iletilir |

**Yaş bazlı politika önerisi [YENİ]:**

| Çocuğun yaşı | Öğretmen arama | Talep gönderme |
|---|---|---|
| **< 13** | **Yalnızca veli** | Yalnızca veli |
| **13–17** | Çocuk + veli | Çocuk gönderir, **veli onaylar** |
| **18+** | Çocuk | Çocuk (veli onayı gerekmez) |

### AKIŞ 11: Premium Satın Alma (Faz 5)

**Aktör:** Veli | **Faz:** 5 | **PRD:** 5.10 *"Veli premium paketi"*

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Kilitli özelliğe dokunur | Premium tanıtımı |
| 2 | Paketi inceler | Fiyat, özellikler |
| 3 | Satın alır | Uygulama içi ödeme (App Store/Play) |
| 4 | — | Premium aktif |

**⚠️ Aile paketi boşluğu [YENİ]:** PRD Bölüm 5'te **"Veli paneli premium"** ve **"Öğrenci premium paketi"** ayrı gelir kalemleri. Ama:
- İkisini de **aynı veli öder**
- İki çocuğu varsa **üç abonelik** mi olacak? (Veli + Ayşe + Mehmet)
- Çocuk Premium'unu veli mi satın alır, çocuk mu? **PRD sessiz**

**Öneri:** **Aile paketi** — tek abonelik, veli + bağlı tüm çocuklar. Hem ödeme dostudur hem de "3 ayrı abonelik" algısının yaratacağı direnci ortadan kaldırır. Bu karar Faz 5'te değil, **abonelik altyapısı tasarlanırken (5.1)** verilmelidir.

---

## 9. Durum Makineleri

### 9.1 Veli-Çocuk Bağlantısı **[YENİ]**

```
VELİ KAYITLI (çocuk yok — panel boş kabuk)
     │
     ├─(veli davet eder)──→ ONAY BEKLİYOR ──┬──→ BAĞLI
     │                       (18+ çocuk)     └──→ REDDEDİLDİ
     │
     ├─(18 altı çocuk)────→ OTOMATİK BAĞLI  ← çocuğa bildirim şart [YENİ]
     │
     └─(çocuk davet eder)─→ BAĞLI
                                │
                                ├──→ KISITLI (çocuk gizlilik ayarlarını kıstı)
                                └──→ KALDIRILDI
```

### 9.2 Veli Paneli Veri Durumu **[PRD M09'dan türetilmiş]**

```
┌─────────────────────────────────────────────────────┐
│ DURUM 1: Çocuk bağlı, öğretmen YOK        (Faz 2)   │
│ → Yalnızca bireysel çalışma verisi                  │
│ → PRD: "Öğretmen gerekmez"                          │
├─────────────────────────────────────────────────────┤
│ DURUM 2: Çocuk bağlı, öğretmen VAR        (Faz 3)   │
│ → Bireysel çalışma + ders/ödev/not/ödeme            │
│ → PRD 3.1: "entegre görünüm" (Kritik)               │
├─────────────────────────────────────────────────────┤
│ DURUM 3: Çocuk bağlı, veri gizlenmiş                │
│ → PRD 2.10: "izin bazlı görünürlük"                 │
│ → Panel kısmen boş → "Ayşe bu veriyi paylaşmıyor"   │
├─────────────────────────────────────────────────────┤
│ DURUM 4: Çocuk hiç çalışmıyor                       │
│ → Boş durum — nötr dil                     [YENİ]   │
└─────────────────────────────────────────────────────┘
```

**Tasarım gereği [TÜRETİLMİŞ]:** Panel bu **4 durumun hepsinde** anlamlı olmalıdır. Özellikle Durum 1, PRD'nin Faz 2 hedefidir (*"Veli paneli — bireysel çalışma verileri (öğretmensiz)"* — **Kritik**) ve tek başına veliyi tatmin etmelidir.

---

## 10. Yetki Matrisi — Veli Neyi Yapamaz

**[TÜRETİLMİŞ]** Veli rolü, **yapamadıklarıyla** tanımlanır:

| # | Veli ŞUNU YAPAMAZ | Neden |
|---|---|---|
| 1 | Ders ekleyemez / değiştiremez / iptal edemez | M04 öğretmene ait |
| 2 | Ödev veremez / ödevi kapatamaz | M06 öğretmene ait |
| 3 | Çocuğunun sayacını başlatamaz/duramaz | M08 öğrenciye ait |
| 4 | Çocuğunun test kaydını değiştiremez | M08 öğrenciye ait |
| 5 | **Çocuğunun kişisel seans notlarını göremez** | **[YENİ]** — mahremiyet çekirdeği |
| 6 | **Çocuğunun gizlediği veriyi göremez** | **[PRD 2.10]** |
| 7 | Ödemeyi "tahsil edildi" işaretleyemez | M07 öğretmene ait |
| 8 | Platform üzerinden ödeme yapamaz | [PRD Bölüm 5] |
| 9 | **Öğretmene yorum yapamaz** | **[PRD M13]** — yalnızca ders alan öğrenci |
| 10 | Öğretmenin özel notunu göremez | [YENİ] not görünürlüğü |
| 11 | Öğretmenin diğer öğrencilerini göremez | Rol izolasyonu |
| 12 | Başka velinin çocuğunu göremez | Rol izolasyonu |
| 13 | Çocuğunun hesabını silemez | [TÜRETİLMİŞ] |
| 14 | 18+ çocuğa onayı olmadan bağlanamaz | [YENİ] |
| 15 | **Öğretmen arayamaz** — ⚠️ **PRD'de böyle** | **[PRD M12]** — Bölüm 15.2 |

**14 maddeden 13'ü doğru ve gereklidir. 15. madde bir tasarım hatasıdır.**

---

## 11. Veli Verisi — Kavramsal Model

**[TÜRETİLMİŞ]**

```
User (id, rol=VELİ, email, telefon, şifre)
  │
  └─1:1─ ParentProfile
           ├─ ad_soyad
           ├─ abonelik_tipi (free|premium)          [PRD 5.10]
           ├─ doğrulama_durumu                      [YENİ]
           └─ bildirim_tercihleri {}                [PRD 2.10]
                │
                └─1:N─ StudentParent  ← BAĞLANTI TABLOSU
                         ├─ öğrenci_id
                         ├─ durum (bekliyor|bağlı|reddedildi|kaldırıldı)
                         ├─ birincil_veli_mi (bool)          [YENİ]
                         ├─ bağlantı_yönü (veli_davet|öğrenci_davet)  [YENİ]
                         ├─ onay_tarihi
                         ├─ kvkk_açık_rıza (bool, tarih)     [YENİ]
                         └─ ilişki (anne|baba|vasi|diğer)    [YENİ]
```

**Kritik nokta [TÜRETİLMİŞ]:** **Velinin kendi verisi yoktur.** `ParentProfile` yalnızca kimlik + tercih taşır. Velinin gördüğü her şey `StudentParent` bağlantısı üzerinden **öğrencinin verisidir** ve `PrivacySetting` filtresinden geçer.

**Bu, veli panelinin teknik olarak bir sorgu katmanı olduğu anlamına gelir:**

```
VELİ PANELİ SORGUSU
    │
    ├─→ StudentParent (bağlı çocuklar)
    │        │
    │        ├─→ PrivacySetting FİLTRESİ ⭐  ← her sorguda
    │        │        └─ yaş politikası ezebilir [YENİ]
    │        │
    │        ├─→ StudySession (M08)      → haftalık süre, konu dağılımı
    │        ├─→ TestResult (M08)        → test performansı
    │        ├─→ StreakRecord (M08)      → streak
    │        │
    │        └─(öğretmen bağlıysa)
    │             ├─→ Lesson (M04)       → yaklaşan dersler
    │             ├─→ LessonSession (M05)→ son ders özeti (görünürlük filtresi)
    │             ├─→ Homework (M06)     → ödevler
    │             ├─→ Payment (M07)      → ödeme özeti
    │             └─→ Message            → öğretmen mesajları
    │
    └─→ SONUÇ: tek ekran, ~90 saniyelik okuma
```

**Performans notu [YENİ]:** Veli paneli **hiçbir zaman ham `StudySession` kayıtlarını toplamamalıdır** (öğrenci dokümanı: 1,2M kayıt/ay). Haftalık özet, **önceden hesaplanmış özet tablosundan** okunmalıdır. Aksi hâlde haftalık özet bildirimi gönderildiği anda binlerce veli aynı anda paneli açar ve her biri milyonlarca satır tarar.

**Gizlilik notu [YENİ]:** `PrivacySetting` filtresi **veri katmanında** uygulanmalıdır, arayüzde değil. Bir API endpoint'i gizli veriyi döndürüp arayüzde saklarsa, veri fiilen sızmış olur.

---

## 12. Free vs. Premium — Veli Paketi (PRD Bölüm 9.3)

**[PRD]** aynen:

| Özellik | Free | Premium |
|---|---|---|
| Çocuğun haftalık çalışma süresi | ✅ | ✅ |
| Son ders özeti | ✅ | ✅ |
| Ödev görüntüleme | ✅ | ✅ |
| Yaklaşan dersler | ✅ | ✅ |
| **Detaylı gelişim grafikleri** | ❌ | ✅ |
| **Haftalık rapor** | ❌ | ✅ |
| **Çalışma süresi geçmişi** | ❌ | ✅ |
| **Bildirimler** | ❌ | ✅ |

### 12.1 Bu Tablonun Değerlendirmesi

**İyi haber:** Veli tablosu, öğrenci tablosundan (Bölüm 9.2) **çok daha sağlıklıdır.** Free veli, PRD M09'daki 5 "temel görünüm"den **4'ünü** görebiliyor. Çekirdek değer Free'de duruyor.

**Kötü haber:** Son satır — **Bildirimler ❌ Free** — tek başına diğer 4 satırı işlevsiz bırakıyor.

### 12.2 🚨 "Bildirimler ❌ Free" Neden Kritik

PRD'nin kendi cümleleriyle:

| PRD ne diyor | Tablo ne yapıyor |
|---|---|
| **Bölüm 10.2:** *"Veli — çocuğun gelişimini görmek için gelir, **bildirimlerle aktif kalır**"* | Aktif kalma mekanizmasını Free'de kapatıyor |
| **M11:** *"Amaç: **Kullanıcıyı uygulamaya geri getirmek**"* | Veliyi geri getirecek şeyi kapatıyor |
| **M11:** *"Haftalık özet → Hedef: **Tümü**"* (veli dahil) | 9.3'te veliye kapalı |
| **Faz 3.6:** *"Haftalık özet bildirimi"* — planlı iş kalemi | Free veli hiç göremeyecek |
| **Bölüm 2.1:** *"Veliyi platforma dahil et"* | Dahil etme kanalını kapatıyor |

**Mantık zinciri:**
```
Veli bildirimi kapalı
    → Veli paneli açmayı hatırlamaz        (Bölüm 3.1: tek tetikleyici bildirim)
    → Panel açılmaz
    → Değer görülmez
    → Premium'a geçmez  ← ödeme yapacak tek kişi kaybedildi
    → Öğrenci Premium'u da satın alınmaz   (parayı veli öder)
    → Veli paneli premium geliri = 0       (Bölüm 5 gelir kalemi)
```

**Bu, öğrenci rolündeki streak çelişkisiyle aynı sınıfta bir hatadır — ama daha kritiktir**, çünkü:
- Öğrencide çelişki **ima edilmişti** (M08 "büyüme motoru" denip kapatılıyordu)
- Velide çelişki **doğrudan yazılmış**: Bölüm 10.2 velinin bildirimlerle aktif kaldığını **açıkça söylüyor**, 9.3 bildirimi **açıkça kapatıyor**. İki cümle aynı belgede, birbirini doğrudan iptal ediyor.
- Ve veli, **ödeme yapan tek roldür.** Onu kaybetmek, üç paketin de gelirini kaybetmektir.

### 12.3 Önerilen Free/Premium Sınırı **[YENİ]**

Prensip: **Free = veli geri gelir ve temel görünürlük alır. Premium = derinlik, geçmiş, çok kanallı.**

| Özellik | Free (önerilen) | Premium (önerilen) |
|---|---|---|
| Haftalık çalışma süresi | ✅ | ✅ |
| Son ders özeti | ✅ | ✅ |
| Ödev görüntüleme | ✅ | ✅ |
| Yaklaşan dersler | ✅ | ✅ |
| **Haftalık özet bildirimi** | ✅ **(haftada 1 push)** | ✅ |
| **Kritik bildirimler** (ödev gecikti, ders iptal, öğretmen mesajı) | ✅ | ✅ |
| Günlük/anlık bildirimler | ❌ | ✅ |
| **Çalışma geçmişi** | ✅ **Son 30 gün** | ✅ Sınırsız |
| Detaylı gelişim grafikleri | ❌ | ✅ |
| Haftalık **rapor** (PDF/detaylı analiz) | ❌ | ✅ |
| Konu bazlı zayıflık analizi | ❌ | ✅ |
| Öğretmen değerlendirme geçmişi | ❌ | ✅ |
| WhatsApp/SMS bildirimi | ❌ | ✅ |
| Çoklu çocuk karşılaştırma | ❌ | ✅ |

**Kritik ayrım [YENİ]:** **"Bildirim"** ile **"Rapor"** aynı şey değildir ve PRD bunları ayırmıyor.
- **Bildirim** = *"Ayşe bu hafta 12 saat çalıştı"* → **Free olmalı.** Bu, veliyi geri getiren tetikleyicidir; ürünün pazarlama kanalıdır, özelliği değil.
- **Rapor** = detaylı analiz, grafikler, trendler, karşılaştırmalar → **Premium olabilir.** Bu, gerçek bir derinlik katmanıdır.

Free veli haftada bir *"Ayşe 12 saat çalıştı, geçen haftaya göre +3 saat"* push'u almalı; dokununca temel özeti görmeli; **detaylı analiz için Premium duvarına çarpmalı.** Böylece veli hem geri gelir hem yükseltme sebebini **kendi gözüyle** görür.

Mevcut tabloda ise veli push almaz, geri gelmez, duvarı **hiç görmez.**

---

## 13. Faz Bazlı Veli Yol Haritası

### FAZ 0 — Altyapı **[PRD]**
- Rol bazlı yetkilendirme (veli rolü) **[PRD 0.4]**
- Push altyapısı **[PRD 0.7]**
- **+ 18 yaş altı veli onayı + KVKK açık rıza [YENİ — kritik, yasal]**

### FAZ 1 — Veli yok **[PRD]**
Veli Faz 1'de **mevcut değildir.** Ancak **[PRD M03]** öğrenci profilinde *"bağlı veli"* alanı var — öğretmen Faz 1'de veli bilgisi girebiliyor.

**[YENİ] Boşluk:** Faz 1'de öğretmenin girdiği veli bilgisine ne olur? Faz 2'de veli kaydolduğunda **otomatik eşleşmeli** — yoksa öğretmenin girdiği veri ölü veridir ve veli sıfırdan bağlanmak zorunda kalır.

**Öneri:** Faz 1'de öğretmen veli telefonu girerse, Faz 2'de o numarayla kaydolan veli için *"Ahmet Öğretmen sizi Ayşe'nin velisi olarak eklemiş. Bağlanmak ister misiniz?"* akışı çalışmalıdır. *(Öğrenci dokümanındaki claim akışının veli karşılığı.)*

### FAZ 2 — Veli Doğar ⭐ **[PRD]**
> **[PRD] Hedef:** *"Öğrenci ve veli, platforma öğretmenden BAĞIMSIZ girebilmeli ve değer bulabilmelidir."*
> **[PRD] Çıktı:** *"Öğrenci kendi çalışmalarını takip eder, **veli çocuğunun gelişimini görür.** Öğretmen gerekmez."*

| PRD # | İş | Öncelik |
|---|---|---|
| 2.8 | **Veli profili ve öğrenciyle bağlantı kurma** | **Kritik** |
| 2.9 | **Veli paneli — bireysel çalışma verileri (öğretmensiz)** | **Kritik** |
| 2.10 | **Veli bildirim tercihleri ve izin bazlı görünürlük** | Yüksek |
| **+B-01** | **Veli kimlik/ilişki doğrulaması** | **Kritik [YENİ]** |
| **+B-02** | **Çoklu çocuk desteği** | **Yüksek [YENİ]** |
| **+B-03** | **Faz 1 veli bilgisi → claim eşleşmesi** | **Orta [YENİ]** |
| **+B-04** | **Yaş bazlı gizlilik matrisi** *(öğrenci dokümanıyla ortak)* | **Kritik [YENİ]** |

### FAZ 3 — Entegre Görünüm **[PRD]**
| PRD # | İş | Öncelik |
|---|---|---|
| 3.1 | **Veli paneli — öğretmen verilerini de kapsayan entegre görünüm** | **Kritik** |
| 3.2 | Gelişim takibi (veli birincil kullanıcı — M10) | Yüksek |
| 3.3 | Performans grafikleri | Yüksek |
| 3.5 | Bildirim sistemi genişletme | Yüksek |
| 3.6 | **Haftalık özet bildirimi** | Orta |
| **+B-05** | **Veli → öğretmen mesaj yanıtı** | **Yüksek [YENİ]** |
| **+B-06** | **"Ödedim" bildirimi** | **Orta [YENİ]** |
| **+B-07** | **Veli bildirim hedeflerinin M11'e eklenmesi** | **Yüksek [YENİ]** |

### FAZ 4 — Eşleştirme ⚠️ **[PRD'de veli yok]**
| # | İş | Öncelik |
|---|---|---|
| **+B-08** | **Veli öğretmen arama/talep akışı** | **Kritik [YENİ]** — Bölüm 15.2 |
| **+B-09** | **Çocuğun öğretmen talebine veli onayı** | **Kritik [YENİ]** |

### FAZ 5 — Premium **[PRD]**
| PRD # | İş | Öncelik |
|---|---|---|
| 5.10 | **Veli premium paketi** | Orta |
| 5.9 | WhatsApp/SMS hatırlatma | Orta |
| **+B-10** | **Aile paketi kararı** *(5.1 abonelik altyapısıyla birlikte)* | **Yüksek [YENİ]** |
| **+B-11** | **Bölüm 12.3 Free/Premium sınırının uygulanması** | **Kritik [YENİ]** |

---

## 14. Faz 2 Kabul Kriterleri (Veli Rolü)

**[TÜRETİLMİŞ]** — PRD Faz 2 çıktısı: *"veli çocuğunun gelişimini görür. Öğretmen gerekmez."*

- [ ] Veli **60 saniyede** kayıt olup çocuğunu bağlayabiliyor
- [ ] Bildirim izni **onboarding'de** isteniyor (ayarlara gömülü değil)
- [ ] Veli, çocuğu bağlandıktan sonra **öğretmen olmadan** dolu bir panel görüyor
- [ ] Panelin tamamı **tek ekranda, ~90 saniyede** okunabiliyor
- [ ] **[PRD M09] 5 temel görünümün** hepsi mevcut *(öğretmensiz hâlde 3'ü)*
- [ ] Çocuk veri gizlerse veli **göremiyor** ve bunu **anlıyor** ("Ayşe bu veriyi paylaşmıyor")
- [ ] **Kişisel seans notları veliye hiçbir koşulda sızmıyor**
- [ ] Veli **birden fazla çocuk** bağlayıp aralarında geçiş yapabiliyor
- [ ] 18 yaş üstü çocuk, **onayı olmadan** bağlanamıyor
- [ ] 18 yaş altı çocuk, bağlantı kurulduğunda **haberdar ediliyor**
- [ ] Doğrulanmamış biri, rastgele telefon numarasıyla bir çocuğa **bağlanamıyor**
- [ ] Haftalık özet bildirimi doğru gün/saatte gidiyor ve **Free veliye de ulaşıyor** *(Bölüm 12.3 kabul edilirse)*
- [ ] Panel, ham `StudySession` taramadan **özet tablosundan** okuyor
- [ ] **Veli 4 hafta boyunca haftalık bildirimle geri dönüyor** ← *asıl test budur*

---

## 15. PRD Boşlukları ve Çelişkiler ⚠️

### 15.1 🚨 ÇELİŞKİ: "Bildirimlerle aktif kalır" ama bildirimler Premium

Detaylı analiz Bölüm 12.2'de. **Bu dokümanın 1 numaralı bulgusudur.**

| Karar seçeneği | Sonuç |
|---|---|
| **A — Haftalık özet + kritik bildirimleri Free yap** (Bölüm 12.3) | Veli geri gelir, değeri görür, Premium'a geçer. **Bölüm 10.2 ile uyumlu** — **önerilen** |
| **B — 9.3 tablosunu koru** | Free veli hiç geri gelmez; veli paneli ölü modül olur; Premium geliri de oluşmaz |
| **C — Bildirim adedi kısıtla** (Free: haftada 1) | Orta yol; teknik olarak A'nın alt kümesi |

### 15.2 🚨 BOŞLUK: Veli, eşleştirme modülünde yok

**[PRD Bölüm 6]** M12 Eşleştirme ve Keşif → Birincil kullanıcı: **"Öğrenci / Öğretmen"**
**[PRD Bölüm 4.3]** Veli: *"**Özellikle küçük yaş gruplarında** sürece dahil olur."*

Bu iki cümle bir arada duramaz:

| Gerçek | Sonuç |
|---|---|
| 10 yaşındaki çocuk öğretmen seçmez | Veli seçer — ama **yetkisi yok** |
| Parayı veli öder | Ödeyen taraf **karar veremiyor** |
| Veli, en ödemeye yatkın segment | **Eşleştirmeyi kullanamıyor** |
| PRD *"küçük yaş grupları"* diyor | O grupta **eşleştirme çalışmaz** |

**[PRD Bölüm 10.2]** *"Eşleştirme — **her üç taraf da hazır olduğunda** açılır; iki taraflı pazar sorununu hafifletir."*

PRD'nin kendisi **üç taraftan** bahsediyor. Ama M12 iki tarafa tasarlanmış. Faz 2–3'te platforma çekilen veli, Faz 4'te eşleştirmede **hiçbir şey yapamaz** — oysa PRD'nin büyüme stratejisi tam olarak bunu vaat ediyor.

**Öneri:** M12'nin birincil kullanıcısı **"Öğrenci / Öğretmen / Veli"** olarak güncellenmeli; Bölüm 8/AKIŞ 10'daki yaş bazlı arama ve onay modeli eklenmelidir.

### 15.3 Çelişkiler

| # | Çelişki | Detay |
|---|---|---|
| Ç-01 | **Bildirimler** | Bölüm 10.2 *"bildirimlerle aktif kalır"* vs 9.3 *"Bildirimler ❌ Free"* |
| Ç-02 | **Haftalık özet** | M11 *"Hedef: Tümü"* (veli dahil) vs 9.3 Free'de kapalı |
| Ç-03 | **Veli + M12** | Bölüm 4.3 *"küçük yaş gruplarında dahil olur"* vs M12'de veli yok |
| Ç-04 | **Test performansı / streak / konu dağılımı** | M09 *"temel görünümler"* içinde ama 9.3 tablosunda **hiç geçmiyor** — Free mi Premium mi? |
| Ç-05 | **Ödeme gecikmesi bildirimi** | M11'de hedef: **Öğretmen**. Ama parayı **veli** öder — veli neden haberdar olmasın? |
| Ç-06 | **Gizlilik** | *"otomatik veli paneline yansır"* vs *"öğrenci isterse gizleyebilir"* *(3 dokümanda ortak)* |
| Ç-07 | **M10 birincil kullanıcı** | Bölüm 6'da *"Öğretmen / Veli"*; ama M10 içeriğinde velinin ne göreceği tanımlı değil |
| Ç-08 | **Veli premium vs öğrenci premium** | İkisi ayrı gelir kalemi ama **aynı kişi öder** — aile paketi kararı yok |

### 15.4 Eksik Özellikler

| # | Boşluk | Etki | Faz |
|---|---|---|---|
| B-01 | **Veli kimlik/ilişki doğrulaması** | **Kritik — güvenlik.** Herhangi biri "veliyim" deyip bir çocuğun rutinine erişebilir | 2 |
| B-02 | **Çoklu çocuk desteği** | **Yüksek** — 2 çocuklu veli çok yaygın; PRD hiç bahsetmiyor | 2 |
| B-03 | **18 yaş altı veli onayı (KVKK)** | **Kritik — yasal** *(3 dokümanda ortak)* | **0** |
| B-04 | **Veli → öğretmen mesaj yanıtı** | **Yüksek** — tek yönlü mesaj iletişimi WhatsApp'a kaçırır | 3 |
| B-05 | **Veli bildirim hedefleri M11'de yok** | **Yüksek** — velinin tek retention kanalı tanımsız | 3 |
| B-06 | **"Ödedim" bildirimi** | Orta — ödeme mutabakatı platform dışında kalıyor | 3 |
| B-07 | **Veli öğretmen arama (M12)** | **Kritik** — Bölüm 15.2 | 4 |
| B-08 | **Çocuğun öğretmen talebine veli onayı** | **Kritik** — küçük yaşta yabancı yetişkinle eşleşme | 4 |
| B-09 | **Faz 1 veli bilgisi → Faz 2 claim eşleşmesi** | Orta — öğretmenin girdiği veli verisi ölü kalıyor | 2 |
| B-10 | **Aile paketi** | Yüksek — 3 ayrı abonelik satın alma direnci yaratır | 5 |
| B-11 | **Yaş bazlı gizlilik matrisi** | **Kritik** *(3 dokümanda ortak)* | 2 |
| B-12 | **Boş durum tasarımı** (çocuk çalışmıyor) | Orta — yanlış dil çocuğu veri paylaşımından kaçırır | 2 |

### 15.5 Karar Bekleyen Sorular

1. **Veli çocuğunun profilini düzenleyebilir mi?** (Küçük yaşta veli girer mi, çocuk mu?)
2. **Veli öğretmene yanıt verebilir mi?** — PRD'de tek yönlü görünüyor
3. **Boşanmış aile / iki ayrı veli** — anne ve baba ayrı ayrı bağlanabilir mi? Biri diğerini görür mü? Biri diğerinin erişimini kaldırabilir mi? *(Hassas ve gerçek bir senaryo)*
4. **Veli, çocuğun Premium'unu satın alabilir mi?** Yoksa çocuk mu satın alır?
5. **Küçük yaşta veli, çocuk adına öğretmene yorum yapabilir mi?** *(PRD M13 sahte yorum önleme kuralıyla çelişir — dikkat)*
6. **Velinin görebileceği veri, çocuk 18 yaşına basınca ne olur?** Otomatik kısıtlanmalı mı?
7. **Veli, çocuğun gizlediği veriyi gizlediğini görebilir mi?** (*"Ayşe bu veriyi paylaşmıyor"* mu, yoksa alan hiç görünmesin mi? — Birincisi çatışma yaratabilir, ikincisi veliyi yanıltır)
8. **Öğretmen, veliyi doğrudan davet edebilir mi?** (M03'te *"bağlı veli"* alanı var ama davet akışı yok)

---

## 16. Üç Rolün Karşılaştırması

**[TÜRETİLMİŞ]** — Üç dokümanın sentezi:

| Boyut | Öğretmen | Öğrenci | **Veli** |
|---|---|---|---|
| **Ana ekran** | Takvim | Sayaç | **Özet paneli** |
| **Kritik akış** | Ders tamamlama (<60 sn) | Çalışma seansı (günde 8×) | **Haftalık kontrol (~90 sn)** |
| **Kullanım sıklığı** | Günde 3–10 | Günde 1–8 | **Haftada 1–3** |
| **Tetikleyici** | İş rutini | Alışkanlık + streak | **Yalnızca bildirim** ⚠️ |
| **Sahip olduğu modül** | 7 (M02,04,05,06,07,10,14) | 1 (M08) | **1 (M09)** |
| **Kendi verisi** | Çok | Çok | **YOK** — tümü türetilmiş |
| **Yetki seviyesi** | Çok yüksek | Orta | **Çok düşük (salt okunur)** |
| **Ekonomik güç** | Orta | Yok | **En yüksek — 2 paketi de o öder** |
| **Doğduğu faz** | Faz 1 | Faz 2 | **Faz 2 (tam: Faz 3)** |
| **Stratejik işlevi** | Gelir + operasyon | Büyüme motoru | **Retention + ödeme gerekçesi** |
| **En büyük risk** | Ders tamamlamada sürtünme | Free/Premium motoru boğuyor | **Bildirim Premium → geri gelmez** |
| **PRD'deki en kritik boşluk** | Tatil/müsaitlik | Claim akışı | **M12'de yok + doğrulama yok** |

---

## 17. Özet — Veli Rolü Tek Sayfada

| Boyut | Özet |
|---|---|
| **Kim** | Çocuğunun gelişimini görmek isteyen ebeveyn — *"özellikle küçük yaş gruplarında"* |
| **Neden gelir** | **Görünürlük** — çocuğum çalışıyor mu, gelişiyor mu? |
| **Neden kalır** | **Bildirim** — tek tetikleyici (PRD Bölüm 10.2 bunu kendisi söylüyor) |
| **Ana ekran** | Özet paneli — tek ekran, ~90 saniye |
| **Kritik akış** | Haftalık bildirim → panel → oku → kapat |
| **Toplam yetenek** | **~55 yetenek** (V-01.1 … V-15.5) — üç rolün en azı |
| **Sahip olduğu modül** | **M09** (tek — ve kendi verisi yok, bir görüntüleme katmanı) |
| **İzleyici olduğu** | M03, M04, M05, M06, M07, M08, M10 (**7 modül, hepsi salt okunur**) |
| **Yetkisi olmayan** | M12 (⚠️ olmalı), M13 (yorum yapamaz), M14 |
| **İki veri kaynağı** | Bireysel çalışma (Faz 2, öğretmensiz) + Öğretmen verisi (Faz 3) |
| **Faz 2'de** | Panel doğar — öğretmensiz de dolu olmalı |
| **Faz 3'te** | İki kaynak birleşir — **Premium'un asıl vaadi burada** |
| **En büyük çelişki** | **"Bildirimlerle aktif kalır" (10.2) ↔ "Bildirimler ❌ Free" (9.3)** |
| **En büyük boşluk** | **Eşleştirmede (M12) veli yok** — ödeyen taraf öğretmen seçemiyor |
| **En büyük güvenlik açığı** | **Veli doğrulaması yok** — herkes "veliyim" diyebilir |
| **En büyük yasal risk** | **18 yaş altı açık rıza Faz 0'da yok** *(3 dokümanda ortak)* |
| **Değişmez kural** | **Çocuğun kişisel seans notları veliye asla açılmaz** |

---

*Bu doküman PRD v2.0'a dayanır. **[YENİ]** etiketli maddeler öneridir ve onayınızı bekler.*
*Kardeş dokümanlar: `ogretmen_rolu_fonksiyonel_dokuman_v1.md` · `ogrenci_rolu_fonksiyonel_dokuman_v1.md`*
