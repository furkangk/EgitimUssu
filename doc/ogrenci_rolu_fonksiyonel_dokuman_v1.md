# Öğrenci Rolü — Fonksiyonel Yetenek ve Kullanım Akışı Dokümanı

**Ürün:** Özel Ders Yönetim ve Eşleştirme Platformu
**Kaynak:** `ozel_ders_platformu_PRD_v2.docx` (v2.0, Nisan 2025)
**Bu doküman:** v1.0 — Öğrenci rolü detaylandırması
**Kardeş doküman:** `ogretmen_rolu_fonksiyonel_dokuman_v1.md`
**Tarih:** 17 Temmuz 2026

---

## 0. Bu Doküman Nasıl Okunur

Öğretmen dokümanıyla aynı etiket sistemi kullanılır:

| Etiket | Anlamı |
|---|---|
| **[PRD]** | PRD v2.0'da açıkça yazılmış |
| **[TÜRETİLMİŞ]** | PRD'de ima edilmiş, burada detaylandırıldı |
| **[YENİ]** | PRD'de yok — bu dokümanda önerilen ekleme. Onayınız gerekiyor. |

> ⚠️ **Bu dokümanın en önemli bulgusu Bölüm 16.1'dedir.** PRD, M08'i "platformun büyüme motorlarından biri" ilan ediyor; ancak Bölüm 9.2'deki Free/Premium tablosu o motorun **çalışan parçalarını Free'de kapatıyor.** Bu, öğrenci stratejisini doğrudan çürüten bir çelişkidir ve karar verilmesi gerekir.

---

## 1. Rol Tanımı ve Stratejik Konum

### 1.1 Öğrenci Kimdir

**[PRD]** Ders alan kişi. Ancak öğretmen rolünden **yapısal olarak farklıdır:** öğrenci, platformun tek rolüdür ki **diğer hiçbir role ihtiyaç duymadan tam işlevsel kullanabilir.**

**[PRD]** *"Öğrenci bu modülü öğretmensiz de tam işlevsel olarak kullanabilir. Bu, platforma bağımsız bir kullanıcı kitlesi oluşturur ve eşleştirme modülüne hazır bir öğrenci havuzu sağlar."*

Bu, öğrenci rolünün stratejik işlevidir:

```
Öğretmen rolü  →  Ürünün GELİR ve OPERASYON merkezi
Öğrenci rolü   →  Ürünün BÜYÜME motoru + eşleştirme havuzu
Veli rolü      →  Ürünün ELDE TUTMA (retention) ve ödeme gerekçesi
```

**[PRD]** *"Kritik fark: Öğrenci ve veli, platforma öğretmenden ÖNCE girebilir. Bireysel çalışma takibi ile sisteme girip zamanla öğretmen arayışına geçebilirler. Bu, eşleştirme modülüne iki taraftan da kullanıcı akışı sağlar."*

### 1.2 Öğrencinin İki Giriş Yolu **[PRD]**

Bu, öğrenci rolünün en belirleyici tasarım kararıdır:

| Yol | Nasıl | Öğretmen gerekir mi | Faz |
|---|---|---|---|
| **A — Öğretmen tarafından eklenerek** | Öğretmen manuel öğrenci oluşturur | Evet | Faz 1 |
| **B — Doğrudan kayıt olarak** | Öğrenci kendi indirir, kaydolur | **Hayır** | Faz 2 |

**[PRD]** *"Bu profil öğretmen tarafından oluşturulabilir VEYA öğrenci doğrudan kayıt olabilir. Her iki durum da desteklenir."*
**[PRD]** *"Öğrenci hem öğretmenden bağımsız kayıt olabilmeli hem de öğretmen tarafından sisteme eklenebilmelidir. Her iki giriş yolu da desteklenmelidir."*

**⚠️ PRD bunların nasıl BİRLEŞECEĞİNİ hiçbir yerde tanımlamıyor.** Öğretmen "Ali Yılmaz"ı manuel eklemişse ve Ali 3 ay sonra kendi kaydolursa ne olur? İki ayrı Ali mi olur? Bu boşluk Bölüm 6/AKIŞ 3'te çözülmüştür (**[YENİ]**).

### 1.3 Öğrencinin Beklentileri (PRD Bölüm 4.2)

**[PRD]**
1. Kendi çalışma sürelerini takip etmek
2. Test ve sınav performansını kayıt altına almak
3. Haftalık/aylık gelişimini görmek
4. Öğretmeni varsa ders geçmişini ve ödevleri takip etmek

**Dikkat:** 4 beklentiden **3'ü öğretmenden bağımsızdır.** Öğretmen yalnızca 4. maddede geçer ve "varsa" ile koşullanmıştır. Ürün tasarımı bunu yansıtmalıdır: öğrenci uygulamasının varsayılan hâli **öğretmensizdir.**

### 1.4 Öğrencinin Çözdüğü Problemler (PRD Bölüm 3)

**[PRD]**
| Bugünkü Durum | Platformdaki Karşılığı |
|---|---|
| Çalışma sürelerini takip için ayrı uygulamalar kullanılıyor | M08 Çalışma sayacı |
| Test performansı sistematik kayıt altına alınmıyor | M08 Test girişi + net hesabı |
| Haftalık/aylık ilerleme görünür değil | M08 Analiz + M10 Gelişim |
| Velinin takip aracı yok | M09 Veli paneli (öğrencinin verisinden beslenir) |

---

## 2. Öğrencinin Sahip Olduğu Modüller

**[PRD]** PRD Bölüm 6'daki 15 modülden öğrenciyi ilgilendirenler:

| Modül | Ad | Öğrencinin Rolü | Faz |
|---|---|---|---|
| M01 | Kullanıcı ve Rol Yönetimi | Kullanıcı | Faz 0 |
| M03 | Öğrenci Profili | **Sahip** (veya öğretmen oluşturur) | Faz 1 |
| M04 | Takvim ve Ders Planlama | **İzleyici** (salt okunur) | Faz 1 |
| M05 | Ders Oturumu Yönetimi | **İzleyici** (ders geçmişi) | Faz 1 |
| M06 | Not ve Ödev Yönetimi | **Tüketici** (ödevi yapan taraf) | Faz 1 |
| M07 | Manuel Ödeme Takibi | **Dolaylı** (veli görür) | Faz 1 |
| **M08** | **Öğrenci Bireysel Çalışma** | **BİRİNCİL SAHİP — rolün kalbi** | **Faz 2** |
| M09 | Veli Paneli | **Veri kaynağı + gizlilik kontrolü sahibi** | Faz 2 |
| M10 | Öğrenci Gelişim Takibi | **Özne** (öğretmen/veli izler) | Faz 3 |
| M11 | Bildirim ve Hatırlatma | Alıcı | Faz 3 |
| M12 | Eşleştirme ve Keşif | **Arayan taraf** | Faz 4 |
| M13 | Puanlama ve Yorum | **Puanlayan taraf** | Faz 4 |
| M14 | Raporlama ve Analiz | Kullanıcı (Premium) | Faz 5 |
| M15 | Ayarlar ve Güvenlik | Kullanıcı | Faz 0+ |

**Öğrenci rolünün ağırlık merkezi M08'dir.** Diğer tüm modüller ya M08'i besler ya M08'den beslenir. Öğretmen rolü 7 modülün sahibiyken öğrenci tek modülün sahibidir — ama o modül, PRD'nin büyüme tezinin tamamını taşır.

---

## 3. Öğrenci Yaşam Döngüsü

PRD'nin öğrenci için öngördüğü yolculuk, öğretmeninkinden tamamen farklıdır:

```
GİRİŞ (öğretmensiz)      GÜNLÜK DÖNGÜ            DÖNÜŞÜM (Faz 4)
──────────────────       ─────────────           ───────────────
Uygulamayı duyar    →    ┌─ Sayacı başlat    →   "Matematik netim
"çalışma takibi"         │  Konu seç              artmıyor"
     ↓                   │  Çalış / mola              ↓
Kayıt ol (öğrencisiz)    │  Seansı bitir         Öğretmen ara (M12)
     ↓                   │  Not al                    ↓
Sınıf/hedef gir          │  ↓                     Filtrele, profil incele
     ↓                   ├─ Test gir                  ↓
İlk seansı başlat        │  Doğru/yanlış/boş      Talep gönder
     ↓                   │  Net hesabı                ↓
Streak başlar            │  ↓                     Öğretmen kabul eder
     ↓                   ├─ Streak devam              ↓
Veliyi bağla (ops.)      │  Hedef kontrol         ╔═══════════════════╗
                         └──┘                     ║ ARTIK HEM BİREYSEL║
                              ↓                   ║ HEM DERS TAKİPÇİSİ║
                         Haftalık:                ╚═══════════════════╝
                         ├─ Analiz görüntüle            ↓
                         └─ Hedef vs gerçekleşen   Ders geçmişi + ödev
                                                   + öğretmen notu
                                                        ↓
                                                   Ders sonrası öğretmeni
                                                   değerlendir (M13)
```

### 3.1 Kullanım Sıklığı Haritası **[TÜRETİLMİŞ]**

| Sıklık | Aksiyon | Erişim Hedefi |
|---|---|---|
| **Günde 1–8 kez** | Sayaç başlat/durdur | **Uygulama açılışı = Sayaç (0 tık)** |
| Günde 1–3 kez | Streak/günlük hedef kontrolü | Ana ekranda görünür |
| Günde 0–2 kez | Test girişi | Ana ekrandan 1 tık |
| Günde 0–2 kez | Ödev kontrolü (öğretmen varsa) | Alt sekme |
| Haftada 2–5 kez | Ders programı kontrolü (öğretmen varsa) | Alt sekme |
| Haftada 1–3 kez | Haftalık analiz | Analiz sekmesi |
| Haftada 1 kez | Hedef belirleme/güncelleme | Analiz altında |
| Ayda 0–1 | Öğretmen arama (Faz 4) | Keşfet sekmesi |

**Kritik tasarım kararı [TÜRETİLMİŞ]:** Öğretmende açılış ekranı **Takvim**, öğrencide **Sayaç**tır. Öğrenci uygulamayı "bugün ne kadar çalıştım" sorusuyla açar, "dersim ne zaman" sorusuyla değil. Öğrencisi olan bir öğrenci bile günde 1 ders alır ama 5 kez çalışır.

---

## 4. Öğrenci Yetenek Matrisi (Tam Liste)

### M01 — Hesap ve Rol

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-01.1 | Öğrenci olarak **doğrudan kayıt olabilir** (öğretmensiz) | [PRD] | 2 |
| S-01.2 | Öğretmen tarafından eklenmiş profili **devralabilir** (claim) | **[YENİ]** | 2 |
| S-01.3 | Giriş yapabilir | [PRD] | 0 |
| S-01.4 | Şifre yenileyebilir/sıfırlayabilir | [PRD] | 0 |
| S-01.5 | Rolüne özel ekranları görür | [PRD] | 0 |
| S-01.6 | Hesabını kapatabilir ve verisini sildirebilir | [PRD] | 0 |
| S-01.7 | **18 yaş altıysa veli onayı gerekir (KVKK açık rıza)** | **[YENİ]** | 0 |
| S-01.8 | Rolünü değiştiremez | [TÜRETİLMİŞ] | 0 |

### M03 — Profil

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-03.1 | Ad soyad girebilir | [PRD] | 1 |
| S-03.2 | Sınıf seviyesi girebilir | [PRD] | 1 |
| S-03.3 | Ders aldığı/çalıştığı branşları tanımlayabilir | [PRD] | 1 |
| S-03.4 | İletişim bilgisi girebilir | [PRD] | 1 |
| S-03.5 | Veli bağlayabilir | [PRD] | 2 |
| S-03.6 | Hedef/seviye bilgisi girebilir | [PRD] | 1 |
| S-03.7 | Aktif derslerini görebilir | [PRD] | 1 |
| S-03.8 | Profil fotoğrafı ekleyebilir | [TÜRETİLMİŞ] | 2 |
| S-03.9 | **Hedef sınavını seçebilir (LGS/TYT/AYT/YDS/okul)** | **[YENİ]** | 2 |
| S-03.10 | **Öğretmenin kendisi hakkındaki özel notunu GÖREMEZ** | [TÜRETİLMİŞ] | 1 |

### M04 — Takvim (İzleyici)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-04.1 | Yaklaşan derslerini görebilir | [PRD] | 1 |
| S-04.2 | Ders programını görüntüleyebilir | [PRD 1.7] | 1 |
| S-04.3 | Ders detayını (tarih, saat, konu, yer) görebilir | [TÜRETİLMİŞ] | 1 |
| S-04.4 | **Ders ekleyemez / değiştiremez / iptal edemez** | [TÜRETİLMİŞ] | 1 |
| S-04.5 | **Ders erteleme talebi gönderebilir** | **[YENİ]** | 1 |
| S-04.6 | **Kendi çalışma programını oluşturabilir** (derslerden bağımsız) | [PRD 9.2] | 2 |
| S-04.7 | **Kendi dersini/planını ekleyebilir·düzenleyebilir·silebilir** (`teacher_id=null`; öğretmenin M04 dersine dokunamaz) | **[YENİ]** ✅ Ç-06 | 2 |

> ⚠️ **[PRD]** Bölüm 9.2'de öğrenci Free özelliği olarak **"Ders programı oluşturma ✅"** yazıyor. Ancak M04 modülü tamamen öğretmene ait ve öğrencinin ders ekleme yetkisi hiçbir yerde tanımlı değil. Bu ifadenin **"kendi çalışma programı"** (öğrenci kendine plan yapar, öğretmen dersi değil) anlamına geldiği varsayılmıştır. **✅ Çözüldü** — bkz. **§5.2 Kendi Ders/Plan Modeli**: öğrenci kendi dersini (`teacher_id=null`) ekleyip planlar, seans bu derse bağlanır; `S-04.4` gereği **öğretmenin** dersine yine dokunamaz.

### M05 — Ders Geçmişi (İzleyici)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-05.1 | Ders geçmişini görebilir | [PRD 1.7] | 1 |
| S-05.2 | İşlenen konuyu görebilir | [TÜRETİLMİŞ] | 1 |
| S-05.3 | Öğretmenin **paylaşıma açtığı** notu görebilir | [TÜRETİLMİŞ] | 1 |
| S-05.4 | Kendi katılım durumunu görebilir | [TÜRETİLMİŞ] | 1 |
| S-05.5 | Ders kaydını **değiştiremez** | [TÜRETİLMİŞ] | 1 |

### M06 — Ödev (Tüketici)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-06.1 | Kendisine verilen ödevleri görebilir | [PRD 1.7] | 1 |
| S-06.2 | Ödev son tarihini görebilir | [PRD] | 1 |
| S-06.3 | Ödeve eklenen dosya/görseli görüntüleyebilir | [PRD] | 1 |
| S-06.4 | Ödevi **"tamamladım" olarak işaretleyebilir** | [PRD] | 1 |
| S-06.5 | Ödev son tarihi yaklaşınca bildirim alır | [PRD M11] | 3 |
| S-06.6 | **Ödev teslimi olarak dosya/foto yükleyebilir** | **[YENİ]** | 1 |
| S-06.7 | **Öğretmenin ödev geri bildirimini görebilir** | **[YENİ]** | 1 |
| S-06.8 | Ödev **oluşturamaz/silemez** | [TÜRETİLMİŞ] | 1 |

### M07 — Ödeme (Dolaylı)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-07.1 | **PRD öğrenciye ödeme görünürlüğü tanımlamıyor** | [PRD boşluk] | — |
| S-07.2 | Ödeme özeti **veliye** gider, öğrenciye değil | [PRD M09] | 3 |
| S-07.3 | Platform üzerinden ödeme **yapamaz** | [PRD] | — |

> **Karar gerekli:** Reşit/yetişkin öğrenci (üniversite hazırlık, YDS, kendi parasını ödeyen) kendi bakiyesini görmeli mi? PRD sessiz — Bölüm 16.3/S-05.

### ⭐ M08 — Bireysel Çalışma (ROLÜN KALBİ)

**[PRD]** *"Bu modül platformun büyüme motorlarından biridir. Öğrenci ve veliyi öğretmenden bağımsız platforma çeker."*

#### 4.1 Çalışma Seansı ve Sayaç **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| S-08.1 | Ders/konu seçip **sayaç başlatabilir** | [PRD] | 2 | Free ✅ (basit) |
| S-08.2 | Sayacı durdurabilir/devam ettirebilir | [PRD 2.2] | 2 | Free ✅ |
| S-08.3 | Seansı bitirebilir | [PRD 2.2] | 2 | Free ✅ |
| S-08.4 | **Mola verebilir — mola süresi toplam süreye eklenmez** | [PRD] | 2 | Free ✅ |
| S-08.5 | Seans bitince özet görür: süre, konu, kişisel notlar | [PRD] | 2 | Free ✅ |
| S-08.6 | Seansa kişisel not ekleyebilir | [PRD] | 2 | Free ✅ |
| S-08.7 | **Geçmişe dönük seans listesini görebilir** | [PRD] | 2 | ⚠️ **Premium** |
| S-08.8 | **Manuel seans girebilir** (sayaçsız çalışmayı sonradan ekleme) | **[YENİ]** | 2 | Free |
| S-08.9 | **Sayaç arka planda / ekran kapalıyken çalışır** | **[YENİ]** | 2 | Free |
| S-08.10 | **Seansı silebilir/düzenleyebilir** | **[YENİ]** | 2 | Free |
| S-08.11 | **Pomodoro modu** (25/5 otomatik döngü) | **[YENİ]** | 5 | Premium |

#### 4.2 Test ve Sınav Performansı **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| S-08.12 | Test girebilir: **toplam soru, doğru, yanlış, boş** | [PRD] | 2 | Free ✅ |
| S-08.13 | **Konu bazlı net hesabı** otomatik yapılır | [PRD] | 2 | Free ✅ |
| S-08.14 | Zaman içinde aynı konudaki **gelişim grafiğini** görür | [PRD] | 2 | ⚠️ Premium* |
| S-08.15 | **Hedef net / hedef puan** tanımlayabilir ve takip edebilir | [PRD] | 2/3 | ⚠️ **Premium** |
| S-08.16 | **Net hesap formülü sınav tipine göre ayarlanır** (4 yanlış 1 doğru götürür vb.) | **[YENİ]** | 2 | Free |
| S-08.17 | **Deneme sınavı girebilir** (çok dersli, tek kayıt) | **[YENİ]** | 3 | Free |
| S-08.18 | **Test kaydını düzenleyebilir/silebilir** | **[YENİ]** | 2 | Free |
| S-08.19 | **Yanlış yaptığı soruların konusunu işaretleyebilir** | **[YENİ]** | 3 | Premium |

*S-08.14 doğrudan tabloda geçmiyor ama "Haftalık/aylık analiz ❌ Free" kapsamına giriyor.

#### 4.3 Haftalık ve Aylık Analiz **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| S-08.20 | **Günlük çalışma süresini** görür | [PRD] | 2 | Free ✅ |
| S-08.21 | Konuya göre çalışma süresi dağılımını görür | [PRD] | 2 | ⚠️ Premium |
| S-08.22 | En çok / en az çalışılan dersleri görür | [PRD] | 2 | ⚠️ Premium |
| S-08.23 | **Haftalık hedef vs. gerçekleşen** karşılaştırması | [PRD] | 2 | ⚠️ Premium |
| S-08.24 | Aylık toplam çalışma özeti | [PRD] | 2 | ⚠️ Premium |

#### 4.4 Motivasyon Sistemi **[PRD]**

| # | Yetenek | Kaynak | Faz | Free/Premium |
|---|---|---|---|---|
| S-08.25 | **Streak (seri gün) takibi** | [PRD] | 2 | ⚠️ **Premium** |
| S-08.26 | **Günlük çalışma hedefi** belirleme | [PRD] | 2 | ⚠️ **Premium** |
| S-08.27 | Tamamlanan görevleri işaretleme | [PRD] | 2 | ⚠️ Premium |
| S-08.28 | **Kişisel rekor göstergeleri** | [PRD] | 2 | ⚠️ Premium |
| S-08.29 | Günlük çalışma hedefi hatırlatması alır | [PRD M11] | 3 | — |
| S-08.30 | **Streak dondurma / telafi hakkı** | **[YENİ]** | 3 | Premium |

> 🚨 **Buradaki ⚠️ işaretlerinin tamamı Bölüm 16.1'deki stratejik çelişkinin kanıtıdır.** PRD, öğrenciyi platforma çeken motoru (streak, hedef, geçmiş, analiz) tarif edip aynı belgede Free'de kapatıyor. Free kullanıcı **sayaç ve test girişi** yapabiliyor ama **hiçbirinin geçmişini göremiyor.** Bu, ürünü "hafızasız kronometre"ye indirger.

#### 4.5 Veli ile Paylaşım ve Gizlilik **[PRD]**

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-08.31 | Çalışma verileri **otomatik olarak veli paneline yansır** | [PRD] | 2 |
| S-08.32 | **Öğrenci isterse belirli verileri gizleyebilir (gizlilik kontrolü)** | [PRD] | 2 |
| S-08.33 | Öğretmen bağlıysa veriler öğretmenle de paylaşılabilir | [PRD] | 2 |
| S-08.34 | Öğretmenle paylaşım seviyesi: Free basit / Premium detaylı | [PRD 9.2] | 5 |
| S-08.35 | **Yaşa göre gizlilik politikası** (küçük yaşta veli kontrolü baskın) | **[YENİ]** | 2 |

> ⚠️ **Gizlilik gerilimi [YENİ]:** PRD hem *"veriler otomatik olarak veli paneline yansır"* hem *"öğrenci isterse belirli verileri gizleyebilir"* diyor. 10 yaşındaki bir öğrenci, Premium'u ödeyen velisinden veri gizleyebilecek mi? Bu iki cümle yaş politikası tanımlanmadan uygulanamaz. Bölüm 8/AKIŞ 12'de çözüm önerisi var.

### M09 — Veli Bağlantısı

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-09.1 | Veli bağlantısı kurabilir/kabul edebilir | [PRD 2.8] | 2 |
| S-09.2 | Velinin ne göreceğini kontrol edebilir (izin bazlı görünürlük) | [PRD 2.10] | 2 |
| S-09.3 | **Veli bağlantısını kaldırabilir** | **[YENİ]** | 2 |
| S-09.4 | **Birden fazla veli bağlayabilir (anne + baba)** | **[YENİ]** | 2 |

### M10 — Gelişim Takibi (Özne)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-10.1 | Konu kazanım durumunu görebilir | [PRD] | 3 |
| S-10.2 | Test performansı zaman serisini görebilir | [PRD] | 3 |
| S-10.3 | Eksik ve güçlü konularını görebilir | [PRD] | 3 |
| S-10.4 | Hedef puan/seviyesini görebilir | [PRD] | 3 |
| S-10.5 | **Öğretmen değerlendirme notlarını görebilir** *(paylaşıma açıksa)* | [PRD] | 3 |
| S-10.6 | Bu verileri **değiştiremez** — öğretmen değerlendirmesi salt okunur | [TÜRETİLMİŞ] | 3 |

### M11 — Bildirimler

| # | Bildirim | Öncelik | Kaynak | Faz |
|---|---|---|---|---|
| S-11.1 | Yaklaşan ders hatırlatması | **Kritik** | [PRD] | 1 |
| S-11.2 | Ödev son tarihi yaklaşıyor | Yüksek | [PRD] | 3 |
| S-11.3 | Günlük çalışma hedefi hatırlatması | Orta | [PRD] | 3 |
| S-11.4 | Haftalık özet | Orta | [PRD] | 3 |
| S-11.5 | Ders sonrası değerlendirme daveti | — | [PRD M13] | 1 |
| S-11.6 | **Streak kırılma uyarısı** | Orta | **[YENİ]** | 3 |
| S-11.7 | Bildirim tercihlerini yönetebilir | — | [TÜRETİLMİŞ] | 0 |
| S-11.8 | WhatsApp/SMS hatırlatma (Premium) | — | [PRD] | 5 |

### M12 — Eşleştirme (Arayan Taraf)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-12.1 | Öğretmenleri listeleyebilir ve arayabilir | [PRD] | 4 |
| S-12.2 | **Branşa göre** filtreleyebilir | [PRD] | 4 |
| S-12.3 | **Şehir/ilçeye göre** filtreleyebilir | [PRD] | 4 |
| S-12.4 | **Ücrete göre** filtreleyebilir | [PRD] | 4 |
| S-12.5 | **Ders şekline göre** filtreleyebilir (yüz yüze/online) | [PRD] | 4 |
| S-12.6 | **Uygun saatlere göre** filtreleyebilir | [PRD] | 4 |
| S-12.7 | Öğretmen profil sayfasını görüntüleyebilir (puan, yorumlar, geçmiş) | [PRD] | 4 |
| S-12.8 | Doğrulama rozetini görebilir | [PRD] | 4 |
| S-12.9 | **Mesaj gönderebilir / talep oluşturabilir** | [PRD] | 4 |
| S-12.10 | **Öğretmeni favorilere ekleyebilir** | **[YENİ]** | 4 |
| S-12.11 | **Talebinin durumunu takip edebilir** | **[YENİ]** | 4 |

### M13 — Puanlama (Puanlayan Taraf)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-13.1 | **Yalnızca ders aldığı öğretmene** yorum yapabilir | [PRD] | 4 |
| S-13.2 | Ders tamamlandıktan sonra **otomatik yorum daveti** alır | [PRD] | 4 |
| S-13.3 | **1–5 yıldız** genel puan verebilir | [PRD] | 4 |
| S-13.4 | Yorum metni yazabilir | [PRD] | 4 |
| S-13.5 | **Anlatım netliği** puanlayabilir | [PRD] | 4 |
| S-13.6 | **Dakiklik ve güvenilirlik** puanlayabilir | [PRD] | 4 |
| S-13.7 | **Sabır ve öğrenciye yaklaşım** puanlayabilir | [PRD] | 4 |
| S-13.8 | **Ders hazırlığı** puanlayabilir | [PRD] | 4 |
| S-13.9 | **Doğrulanmış öğrenci rozeti** kazanır (kayıtlı + ders kaydı olan) | [PRD] | 4 |
| S-13.10 | Öğretmenin yanıtını görebilir | [PRD] | 4 |
| S-13.11 | **Faz 1–2'de özel geri bildirim gönderir** (yalnızca öğretmen görür) | [PRD] | 1 |
| S-13.12 | **Yorumunu düzenleyebilir/silebilir mi?** — PRD sessiz | [PRD boşluk] | 4 |
| S-13.13 | **Anonim yorum yapabilir mi?** — PRD sessiz | [PRD boşluk] | 4 |

### M14 — Raporlama (Premium)

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-14.1 | Haftalık/aylık çalışma süresi analizi | [PRD] | 5 |
| S-14.2 | Konu bazlı performans değişimi | [PRD] | 5 |
| S-14.3 | Hedef vs. gerçekleşen karşılaştırması | [PRD] | 5 |

### M15 — Ayarlar ve Güvenlik

| # | Yetenek | Kaynak | Faz |
|---|---|---|---|
| S-15.1 | Şifre değiştirme | [PRD] | 0 |
| S-15.2 | Bildirim tercihleri | [PRD] | 0 |
| S-15.3 | **Gizlilik ayarları (veli/öğretmen görünürlüğü)** | [PRD] | 2 |
| S-15.4 | Hesap kapatma / veri silme | [PRD] | 0 |
| S-15.5 | Abonelik yönetimi | [PRD] | 5 |

---

## 5. Ekran Haritası (Bilgi Mimarisi)

**[TÜRETİLMİŞ]** Öğrenci uygulamasının **iki hâli** vardır ve bu, tasarımın en kritik noktasıdır:

```
ÖĞRENCİ UYGULAMASI
│
├── [Alt Sekme 1] ⏱️ ÇALIŞ  ← AÇILIŞ EKRANI (öğretmensiz de dolu)
│   ├── Büyük sayaç
│   ├── Konu/ders seçici
│   ├── Başlat / Mola / Bitir
│   ├── Bugünkü toplam süre
│   ├── Streak göstergesi 🔥
│   ├── Günlük hedef ilerlemesi
│   └── (+) Manuel seans ekle        [YENİ]
│
├── [Alt Sekme 2] 📊 PERFORMANS
│   ├── Test girişi (+)
│   │   ├── Konu bazlı test
│   │   └── Deneme sınavı           [YENİ]
│   ├── Net gelişim grafiği
│   ├── Hedef net takibi
│   ├── Haftalık analiz
│   ├── Aylık analiz
│   └── Kişisel rekorlar
│
├── [Alt Sekme 3] 📚 DERSLERİM   ← ÖĞRETMEN YOKSA BOŞ DURUM
│   │
│   ├── ┌─ ÖĞRETMEN YOKSA ─────────────────────────┐
│   │   │ "Henüz bir öğretmenin yok"                │
│   │   │ [Öğretmen Bul]  ← Faz 4'te aktif          │
│   │   │ [Öğretmenim var, davet kodu gir]  [YENİ]  │
│   │   └───────────────────────────────────────────┘
│   │
│   └── ┌─ ÖĞRETMEN VARSA ─────────────────────────┐
│       │ Yaklaşan dersler                          │
│       │ Ders geçmişi                              │
│       │ Ödevlerim (bekleyen / tamamlanan)         │
│       │ Öğretmen notları (paylaşılanlar)          │
│       │ Gelişim değerlendirmem (Faz 3)            │
│       └───────────────────────────────────────────┘
│
├── [Alt Sekme 4] 🔍 KEŞFET  ← FAZ 4'E KADAR YOK
│   ├── Öğretmen arama
│   ├── Filtreler (branş, şehir, ücret, şekil, saat)
│   ├── Öğretmen profili
│   ├── Favorilerim              [YENİ]
│   └── Taleplerim               [YENİ]
│
└── [Alt Sekme 5] 👤 PROFİL
    ├── Profil bilgileri (sınıf, branşlar, hedef)
    ├── Velim  ← bağlantı + gizlilik kontrolü
    ├── Gizlilik ayarları ⭐
    ├── Abonelik (Faz 5)
    ├── Bildirim ayarları
    └── Ayarlar & Güvenlik
```

### 5.1 Faz Bazlı Sekme Durumu **[TÜRETİLMİŞ]**

| Sekme | Faz 1 | Faz 2 | Faz 3 | Faz 4 |
|---|---|---|---|---|
| ⏱️ Çalış | ❌ yok | ✅ tam | ✅ | ✅ |
| 📊 Performans | ❌ yok | ✅ temel | ✅ tam | ✅ |
| 📚 Derslerim | ✅ **tek sekme** | ✅ | ✅ | ✅ |
| 🔍 Keşfet | ❌ | ❌ | ❌ | ✅ |
| 👤 Profil | ✅ | ✅ | ✅ | ✅ |

> **Kritik gözlem:** **Faz 1'de öğrencinin uygulamada yapabildiği tek şey "ders geçmişi ve ödevleri görmek"tir** (PRD 1.7, öncelik: Yüksek). Bu, tek başına indirilecek bir uygulama değildir. Faz 1'de öğrenci uygulaması **öğretmenin bir uzantısıdır** ve bu bilinçli bir karardır — PRD Faz 1'in hedefi öğretmendir. Öğrenci ürünü Faz 2'de doğar.

### 5.2 Karar — Kendi Ders/Plan Modeli (Ç-06 çözümü) **[YENİ — onaylandı 2026-07-19]**

**Sorun:** §5 ekran haritasında **Derslerim yalnız öğretmen içeriklidir**; öğretmensiz öğrenci *"hangi gün hangi derse/konuya çalışacağım"* planını hiçbir ekranda yapamıyordu (Ç-06). Sayaç (seans) reaktiftir — *"şu an çalış"* — proaktif planlama katmanı yoktu.

**Karar:** Öğrenci **kendi dersini de öğretmen dersi gibi** ekleyip planlar. **Tek fark öğretmen bağının (`teacher_id`) olmamasıdır.** "Seans" kaldırılmadı; planın **gerçekleşme** katmanı oldu — bir derse bağlı (`lesson_id` dolu) ya da serbest (anlık çalışma) olabilir.

| Katman | Ne | Öğretmensiz (kendi) | Öğretmenli |
|---|---|---|---|
| **Plan = Ders** | kim/ne zaman/hangi konu | öğrenci ekler (`teacher_id=null`) | öğretmen ekler |
| **Gerçekleşme = Seans** | fiilen çalışılan süre | `StudySession` (sayaç) | `LessonSession` (öğretmen tamamlar) |

**Derslerim'in yeni içeriği:**
- 🗓️ **Program / Takvim** — kendi + öğretmen dersleri, gün gün (*"hangi gün hangi ders"*)
- 👤 **Kendi ders ekle** — ders·konu·tarih·saat·süre (öğretmen dersi gibi, `teacher_id=null`)
- 📖 **Dersler & Konular kataloğu** — çalışılan ders/konuları yönet
- Kart rozeti + filtre: `👤 Kendi` / `👨‍🏫 Öğretmen` (Tümü/Kendi/Öğretmen)
- Öğretmen bağlı derslerde ek: ödev·not·katılım·değerlendirme (kendi derste bunlar yok)

**Sınır:** `S-04.4` **geçerliliğini korur** — öğrenci **öğretmenin** M04 dersine dokunamaz; yalnızca **kendi** dersini yönetir.

**Veri modeli etkisi — fiziksel (kodda uygulanmış, Ç-06):** Kendi ders artık ayrı bir `StudyScheduleEntry`
değil; **birleşik `LessonSchedule`** entity'sinde `TeacherUserId` null olarak tutulur. Eski
`scheduling.study_schedule_entries` tablosu `lesson_schedules`'e göç edilip kaldırıldı (StudyScheduleEntry
domain/uygulama/altyapı katmanları da silindi). Takvim tek kaynaktan (`lesson_schedules`) okunur.
```
LessonSchedule.TeacherUserId  Guid? NULLABLE   ← boş = kendi dersi
LessonSchedule.Topic/ColorHex                  ← kendi derste kullanılır (öğretmen dersinde LessonFormat)
StudySession.LessonId         Guid? NULLABLE   ← derse bağlı ya da serbest (gevşek referans, FK yok)
CalendarOccurrence.completed                   ← o gün derse bağlı tamamlanmış seans (planla→çalış→✓)
```
Modüller arası okuma `IStudyPlanCompletionReader` sözleşmesiyle yapılır (Study → Scheduling; doğrudan referans yok).

**Faz:** Kendi ders/plan **Faz 2** (öğrenci ürünüyle birlikte). Faz 1'de Derslerim yalnız öğretmen dersleridir.

**Diyagramlar:** [`diagrams/rol_sayfa_mimarisi/ogrenci.md`](diagrams/rol_sayfa_mimarisi/ogrenci.md) §1.2 (kavramsal model) + §3b (öğretmensiz döngü) + SVG'ler.

---

## 6. Detaylı Kullanım Akışları — Giriş

### AKIŞ 1: Doğrudan Kayıt (Öğretmensiz) ⭐

**Aktör:** Yeni öğrenci | **Faz:** 2 | **PRD:** 2.1 (Kritik)

Bu akış, PRD'nin büyüme stratejisinin giriş kapısıdır. **Öğretmenden hiç bahsedilmemelidir.**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Uygulamayı açar | "Öğretmen misin, Öğrenci mi, Veli mi?" |
| 2 | **"Öğrenciyim"** seçer | Kayıt formu |
| 3 | E-posta/telefon + şifre | Doğrulama kodu |
| 4 | Kodu girer | Hesap oluşur, rol = ÖĞRENCİ |
| 5 | **Yaş / doğum tarihi** girer **[YENİ]** | 18 altıysa → veli onayı akışı (AKIŞ 2) |
| 6 | Sınıf seviyesi seçer | — |
| 7 | Hedef sınav seçer **[YENİ]** | LGS / TYT / AYT / Okul / Diğer |
| 8 | Çalıştığı dersleri seçer | Sınıf seviyesine göre ön dolu liste |
| 9 | Günlük çalışma hedefi belirler *(atlanabilir)* | Örn: 3 saat |
| 10 | — | **"İlk çalışma seansını başlat"** → doğrudan sayaç ekranı |

**Tasarım kuralı [TÜRETİLMİŞ]:** Onboarding **10 adımda ve 90 saniyede** bitmeli, sonunda öğrenci **sayaç ekranında** olmalı. Öğretmen sorulmaz, veli sorulmaz (zorunlu değilse), ödeme sorulmaz. Tek hedef: ilk seansı başlatmak.

**Alternatif akış 2a — "Öğretmenim beni ekledi" [YENİ]:** Kayıt ekranında "Öğretmenimden davet kodum var" seçeneği → AKIŞ 3'e gider.

---

### AKIŞ 2: 18 Yaş Altı — Veli Onayı **[YENİ]**

**Aktör:** Öğrenci (reşit değil) | **Faz:** 0/2 | **Durum: PRD'de YOK — yasal zorunluluk**

**⚠️ Bu, dokümandaki en kritik [YENİ] maddedir.** PRD, KVKK'yı yalnızca ödeme altyapısı bağlamında anıyor (*"Ödeme altyapısı (abonelik) için KVKK uyumluluğu ve uygulama mağazası kuralları gözetilmelidir"* — Bölüm 10.3). Ancak:

- Ürünün hedef kitlesi **büyük ölçüde 18 yaş altıdır** (LGS, lise, sınav hazırlık)
- PRD *"Özellikle küçük yaş gruplarında sürece dahil olur"* diyerek bunu kendisi kabul ediyor (Bölüm 4.3)
- Toplanan veri **davranışsal ve kişiseldir** (çalışma saatleri, performans, konum/şehir)
- **KVKK ve App Store/Google Play çocuk politikaları**, reşit olmayanların verisi için veli açık rızası ister
- Bu, Faz 5'te değil **Faz 0'da** çözülmesi gereken bir konudur — sonradan eklenirse tüm veri modelinin ve onboarding'in yeniden yazılması gerekir

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Doğum tarihi girer (< 18) | Veli onayı akışı tetiklenir |
| 2 | — | "Devam etmek için veli onayı gerekiyor" |
| 3 | Veli e-posta/telefonu girer | Veliye onay talebi gider |
| 4 | *(Veli onaylar)* | Hesap **tam aktif** olur |
| 5 | — | Veli otomatik bağlanır, veli paneli açılır |

**Onay beklerken [YENİ]:** Öğrenci hesabı **kısıtlı modda** çalışabilir (sayaç kullanılabilir) ama veri veliye/öğretmene paylaşılmaz, eşleştirme (Faz 4) kapalıdır. Bu, kaydı öldürmeden yasal zorunluluğu karşılar.

**Yaş bazlı politika önerisi [YENİ]:**

| Yaş | Veli onayı | Öğrencinin gizlilik kontrolü | Eşleştirme (Faz 4) |
|---|---|---|---|
| < 13 | **Zorunlu** | Yok — veli tam görür | Veli üzerinden |
| 13–17 | **Zorunlu** | **Kısmi** — kişisel notlarını gizleyebilir, süre/performans gizlenemez | Veli onayıyla |
| 18+ | Gerekmez | **Tam** — veli bağlamak isteğe bağlı | Serbest |

Bu tablo, PRD'deki *"veriler otomatik veli paneline yansır"* ile *"öğrenci isterse belirli verileri gizleyebilir"* çelişkisini de çözer.

---

### AKIŞ 3: Öğretmenin Eklediği Profili Devralma (Claim) **[YENİ]**

**Aktör:** Öğrenci | **Faz:** 2 | **Durum: PRD'de YOK — kritik boşluk**

PRD iki giriş yolunu da zorunlu kılıyor ama birleşmelerini tanımlamıyor. Bu akış olmadan **kaçınılmaz sonuç:** öğretmenin eklediği "Ali Yılmaz" ile kendi kaydolan "Ali Yılmaz" iki ayrı kayıt olur; ders geçmişi bir tarafta, çalışma verisi diğer tarafta kalır ve veli paneli **[PRD M09]** iki kaynaktan beslenemez.

**Yöntem A — Davet kodu (önerilen):**

| Adım | Aktör | Sistem |
|---|---|---|
| 1 | Öğretmen öğrenciyi manuel ekler | 6 haneli davet kodu üretilir |
| 2 | Öğretmen kodu öğrenciye verir | (WhatsApp, sözlü, QR) |
| 3 | Öğrenci kayıt olur → "Davet kodum var" | Kod girişi |
| 4 | Kodu girer | Kod doğrulanır |
| 5 | — | "Ahmet Öğretmen seni Matematik dersine eklemiş. Doğru mu?" |
| 6 | **Onayla** | **Manuel profil, gerçek hesaba birleştirilir** |
| 7 | — | Ders geçmişi + ödevler anında görünür; bağlantı durumu = BAĞLI |

**Yöntem B — Otomatik eşleşme:** Kayıt sırasında girilen telefon/e-posta, öğretmenin girdiğiyle eşleşirse: *"Ahmet Öğretmen seni eklemiş görünüyor. Bağlanmak ister misin?"* → onay → birleştirme.

**Çakışma kuralı [YENİ]:** Birleştirme **her zaman öğrencinin onayıyla** olur. Öğretmen tek taraflı bağlayamaz — aksi hâlde bir öğretmen rastgele telefon numarası girerek yabancı bir öğrencinin verisine erişebilir.

---

## 7. Detaylı Kullanım Akışları — Bireysel Çalışma (M08)

### AKIŞ 4: Çalışma Seansı ⭐ **(Ürünün Öğrenci Tarafındaki Kalbi)**

**Aktör:** Öğrenci | **Faz:** 2 | **PRD:** 2.2, 2.3 (Kritik)

Öğretmen tarafındaki "ders tamamlama" ne ise, öğrenci tarafında bu odur. **Günde 8 kez tekrarlanır — sürtünme sıfır olmalıdır.**

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Uygulamayı açar | **Sayaç ekranı** (açılış) |
| 2 | Ders/konu seçer **[PRD]** | Son çalışılan konular üstte |
| 3 | **Başlat** | Sayaç çalışır, arka planda devam eder **[YENİ]** |
| 4 | *(çalışır)* | Ekran kapalıyken de sayar |
| 5 | **Mola** **[PRD]** | Sayaç durur, **mola süresi toplama eklenmez** |
| 6 | **Devam** | Sayaç kaldığı yerden devam |
| 7 | **Bitir** **[PRD]** | Seans özeti açılır |
| 8 | Özeti görür: **süre, konu** **[PRD]** | — |
| 9 | **Kişisel not** ekler *(opsiyonel)* **[PRD]** | Örn: "türev zor geldi" |
| 10 | **Kaydet** | Seans kaydedilir → veriler dağılır ↓ |

**Adım 10 — Seans sonrası veri akışı:**
```
SEANS KAYDEDİLDİ
    │
    ├─→ Günlük toplam süreye eklenir                    [PRD]
    ├─→ Streak kontrolü yapılır (bugün ilk seans mı?)   [PRD]
    ├─→ Günlük hedefe ilerleme işlenir                  [PRD]
    ├─→ Haftalık/aylık analize girer                    [PRD]
    ├─→ Konu bazlı süre dağılımına eklenir              [PRD]
    ├─→ Kişisel rekor kontrolü                          [PRD]
    ├─→ VELİ PANELİNE yansır (gizlilik ayarına göre)    [PRD M09]
    └─→ ÖĞRETMENE yansır (öğrenci izin verdiyse)        [PRD M08]
```

**Kritik teknik gereksinimler [YENİ] — PRD'de yok ama olmadan modül çalışmaz:**

| # | Gereksinim | Neden |
|---|---|---|
| 1 | Sayaç **arka planda** çalışmalı | Öğrenci telefonu cebine koyar; iOS/Android arka plan kısıtları özel çözüm ister |
| 2 | Sayaç **offline** çalışmalı, sonra senkronize olmalı | Kütüphanede/serviste internet olmayabilir |
| 3 | Uygulama kapanırsa/çökerse seans **kurtarılmalı** | 2 saatlik seansın kaybı = uygulama silinir |
| 4 | Telefon kapanırsa **son durum korunmalı** | Aynı |
| 5 | "Sayaç açık unutuldu" **tespiti** | 6 saatlik seans → "Hâlâ çalışıyor musun?" sorusu |

Bu 5 madde, PRD Faz 2'nin **en büyük teknik riskidir** ve Bölüm 10.3'teki risk listesinde **yer almıyor.**

**Alternatif akış 3a — Manuel seans girişi [YENİ]:** Öğrenci sayacı unutmuş olabilir. "Dün 2 saat matematik çalıştım" diyebilmeli, yoksa veri güvenilirliğini kaybeder ve öğrenci uygulamayı bırakır.

---

### AKIŞ 5: Test Girişi ve Net Hesabı

**Aktör:** Öğrenci | **Faz:** 2 | **PRD:** 2.5, 2.6 (Yüksek)

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Performans → **(+) Test Ekle** | Form |
| 2 | Ders/konu seçer **[PRD]** | — |
| 3 | **Toplam soru** girer **[PRD]** | — |
| 4 | **Doğru** girer **[PRD]** | — |
| 5 | **Yanlış** girer **[PRD]** | — |
| 6 | **Boş** girer **[PRD]** | Doğru+Yanlış+Boş = Toplam kontrolü |
| 7 | Tarih *(varsayılan bugün)* | — |
| 8 | **Kaydet** | **Net otomatik hesaplanır** **[PRD]** |
| 9 | — | Konu bazlı gelişim grafiğine işlenir **[PRD]** |
| 10 | — | Hedef net ile karşılaştırılır **[PRD]** |

**⚠️ Net formülü boşluğu [YENİ]:** PRD *"konu bazlı net hesabı"* diyor ama **formülü tanımlamıyor.** Türkiye'de tek bir formül yoktur:

| Sınav | Formül |
|---|---|
| TYT / AYT | `Net = Doğru − (Yanlış ÷ 4)` |
| LGS | `Net = Doğru − (Yanlış ÷ 3)` |
| Bazı okul/kurum denemeleri | `Net = Doğru` (yanlış götürmez) |
| KPSS/YDS vb. | Değişken |

**Öneri:** Net formülü **sınav tipine bağlı** olmalı ve öğrencinin profilindeki "hedef sınav" (S-03.9) alanından türetilmelidir. Sabit `/4` yazılırsa LGS öğrencilerinin tüm verisi yanlış olur — ve LGS, hedef kitlenin büyük bölümüdür.

**Alternatif akış — Deneme sınavı [YENİ]:** Öğrenciler konu testi kadar sık **deneme** çözer. Deneme = çok dersli tek oturum (TYT: Türkçe 40, Matematik 40, Fen 20, Sosyal 20). PRD yalnızca tek konulu test girişini tanımlıyor. Deneme desteği olmadan sınav hazırlığı öğrencisinin verisinin yarısı sisteme girmez.

---

### AKIŞ 6: Streak ve Günlük Hedef **[PRD]**

**Aktör:** Öğrenci | **Faz:** 2 | **PRD:** 2.7 (Yüksek)

**[PRD]** Motivasyon sistemi: *"Streak (seri gün) takibi · Günlük çalışma hedefi belirleme · Tamamlanan görevleri işaretleme · Kişisel rekor göstergeleri"*

**⚠️ PRD streak'i tanımlıyor ama kurallarını vermiyor. [YENİ] Karar gereken sorular:**

| Soru | Öneri |
|---|---|
| Streak neyle korunur? | Günlük hedefin **%50'sine ulaşmak** (yalnızca "1 dk açtım" streak sayılmamalı) |
| Minimum süre? | En az **15 dakika** kayıtlı seans |
| Gün sınırı? | **04:00–04:00** (gece 01:00'de çalışan öğrenci dünü korumalı) |
| Hafta sonu? | Sayılır — ama "hafta sonu muafiyeti" ayarı sunulabilir |
| Kırılırsa? | Sıfırlanır, ama **en uzun streak rekoru kalıcı saklanır** |
| Telafi hakkı? | **[YENİ]** Ayda 1 "streak dondurma" — Premium özelliği olarak güçlü |
| Manuel seans streak sayar mı? | **Evet** — yoksa manuel girişin anlamı kalmaz |
| Tatil/hastalık? | **[YENİ]** "Mola modu" — streak dondurulur |

**Bu kurallar yazılmadan streak geliştirilemez.** Streak, öğrenci retention'ının tek en güçlü mekanizmasıdır; belirsiz bırakılamaz.

---

### AKIŞ 7: Haftalık Analiz Görüntüleme

**Aktör:** Öğrenci | **Faz:** 2 | **PRD:** 2.4 (Kritik)

| Görünüm | İçerik | Kaynak |
|---|---|---|
| Haftalık toplam | Bu hafta kaç saat çalıştım | [PRD] |
| Konu dağılımı | Hangi derse ne kadar | [PRD] |
| En çok/az çalışılan | Sıralı liste | [PRD] |
| Hedef vs. gerçekleşen | Karşılaştırma | [PRD] |
| Aylık özet | Toplam çalışma | [PRD] |
| Test performansı | Konu bazlı net değişimi | [PRD] |
| Kişisel rekorlar | En uzun seans, en uzun streak | [PRD] |

> 🚨 PRD 9.2: **"Haftalık / aylık analiz ❌ Free"**. Ancak PRD Faz 2.4 aynı özelliği **"Kritik"** öncelikli olarak listeliyor. Bir özellik hem Faz 2'nin kritik çıktısı hem de Free'de kapalı olamaz — Bölüm 16.1.

---

## 8. Detaylı Kullanım Akışları — Gizlilik ve Veli

### AKIŞ 8: Veli Bağlama

**Aktör:** Öğrenci veya Veli | **Faz:** 2 | **PRD:** 2.8 (Kritik)

**⚠️ PRD "veli profili ve öğrenciyle bağlantı kurma" diyor ama kimin başlattığını söylemiyor.** İki yön de mümkün olmalıdır:

**Yön A — Öğrenci veliyi davet eder:**
| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Profil → **Velim** → Veli Ekle | — |
| 2 | Veli telefonu/e-postası girer | Davet gönderilir |
| 3 | *(Veli kaydolur/onaylar)* | Bağlantı kurulur |
| 4 | **Gizlilik ayarları sorulur** ⭐ | AKIŞ 9 |

**Yön B — Veli öğrenciyi davet eder:**
| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Veli, çocuğunun telefonunu/kodunu girer | Öğrenciye onay talebi |
| 2 | **Öğrenci onaylar** ← 18+ ise zorunlu | Bağlantı kurulur |
| 3 | *(18 altı ise)* onay gerekmez | Otomatik bağlanır **[YENİ]** |

**Kritik kural [YENİ]:** 18 yaş **üstü** öğrenci için veli bağlantısı **öğrencinin onayına tabidir.** 18 **altı** için veli hakkıdır ve öğrenci reddedemez. Bu ayrım yapılmazsa ya yasal risk doğar ya da yetişkin öğrencinin mahremiyeti ihlal edilir.

---

### AKIŞ 9: Gizlilik Kontrolü ⭐ **(En Hassas Akış)**

**Aktör:** Öğrenci | **Faz:** 2 | **PRD:** 2.10, M08

**[PRD]** *"Öğrenci isterse belirli verileri gizleyebilir (gizlilik kontrolü)"*
**[PRD]** *"Çalışma verileri otomatik olarak veli paneline yansır"*

Bu iki cümle çelişir. Çözüm önerisi — **yaş bazlı, alan bazlı matris [YENİ]:**

| Veri | < 13 | 13–17 | 18+ |
|---|---|---|---|
| Toplam çalışma süresi | Veli görür (gizlenemez) | Veli görür (gizlenemez) | Öğrenci seçer |
| Konu dağılımı | Veli görür | Veli görür | Öğrenci seçer |
| Test netleri | Veli görür | **Öğrenci gizleyebilir** | Öğrenci seçer |
| Streak | Veli görür | Veli görür | Öğrenci seçer |
| **Kişisel seans notları** | **Gizli** | **Gizli** | **Gizli** |
| Hedefler | Veli görür | Öğrenci seçer | Öğrenci seçer |

**Değişmez kural [YENİ]:** **Kişisel seans notları hiçbir yaşta kimseye açılmaz.** PRD M08'de *"seans bitince özet: süre, konu, kişisel notlar"* diyor — "kişisel" kelimesi burada bilinçli seçilmiş olmalıdır. Öğrenci "bugün hiç odaklanamadım, kafam çok dağınık" yazabilmeli ve bunun veliye gitmeyeceğini bilmelidir. Aksi hâlde not alanı ölü özelliğe döner.

**Şeffaflık kuralı [YENİ]:** Öğrenci, velisinin **tam olarak neyi gördüğünü** kendi ekranından görebilmelidir: *"Velin şunları görüyor: ✓ çalışma süren ✓ streak'in ✗ kişisel notların"*. Gizli gözetim, öğrenciyi uygulamadan kaçırır ve ürünün büyüme motorunu durdurur.

---

## 9. Detaylı Kullanım Akışları — Öğretmenli Öğrenci (Faz 1)

### AKIŞ 10: Ders Geçmişi ve Ödev Görüntüleme

**Aktör:** Öğrenci | **Faz:** 1 | **PRD:** 1.7 (Yüksek)

**[PRD 1.7]** *"Öğrenci giriş ekranı — ders geçmişi ve ödevleri görme"*

Faz 1'de öğrenci uygulamasının **tamamı** budur. Salt okunur bir penceredir.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Derslerim sekmesi | Yaklaşan dersler + geçmiş |
| 2 | Derse dokunur | Detay: tarih, konu, süre, öğretmen notu *(paylaşılansa)* |
| 3 | Ödevler sekmesi | Bekleyen / tamamlanan / geciken |
| 4 | Ödeve dokunur | Başlık, açıklama, son tarih, ekli dosyalar |

### AKIŞ 11: Ödev Tamamlama

**Aktör:** Öğrenci | **Faz:** 1 | **PRD:** M06

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ödev detayı → **Tamamladım** | Onay |
| 2 | **Dosya/foto yükler** *(opsiyonel)* **[YENİ]** | — |
| 3 | Not yazar *(opsiyonel)* **[YENİ]** | — |
| 4 | **Gönder** | Durum = TAMAMLANDI |
| 5 | — | Öğretmene bildirim; veli paneline yansır |
| 6 | — | Öğretmen geri bildirim yazarsa öğrenciye bildirim **[YENİ]** |

> **[YENİ] Boşluk:** PRD'de dosya ekleme yalnızca **öğretmen** fonksiyonu olarak listelenmiş (*"Dosya veya görsel ekleme"* — M06 öğretmen fonksiyonları altında). Öğrencinin ödev fotoğrafı yükleyememesi, ödev modülünü tek yönlü bırakır: öğretmen ödev verir, öğrenci "yaptım" der, öğretmen göremez. Çözdüğü problem yarım kalır.

### AKIŞ 12: Ders Erteleme Talebi **[YENİ]**

**Aktör:** Öğrenci | **Faz:** 1 | **Durum: PRD'de YOK**

PRD'de öğrencinin ders üzerinde **hiçbir** yetkisi yok. Gerçekte "hocam bu hafta gelemeyeceğim" en sık mesajdır ve bugün WhatsApp'ta yaşanır — yani ürünün çözmeye çalıştığı "dağınık iletişim" problemi (PRD Bölüm 3) platform dışında devam eder.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders detayı → **Erteleme Talebi** | Form |
| 2 | Neden yazar | — |
| 3 | Alternatif tarih önerir *(opsiyonel)* | — |
| 4 | **Gönder** | Öğretmene bildirim |
| 5 | *(Öğretmen kabul/red)* | Öğrenciye bildirim |
| 6 | Kabul → ders taşınır | Öğretmen AKIŞ 8'i (erteleme) çalıştırır |

---

## 10. Detaylı Kullanım Akışları — Faz 4

### AKIŞ 13: Öğretmen Arama ve Talep Gönderme (M12)

**Aktör:** Öğrenci | **Faz:** 4 | **PRD:** 4.1–4.4

Bu, PRD'nin büyüme tezinin kapanış hamlesidir: Faz 2'de öğretmensiz gelen öğrenci, Faz 4'te öğretmen arar.

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Keşfet sekmesi | Öğretmen listesi |
| 2 | Filtreler **[PRD]** | Branş · Şehir/ilçe · Ücret · Ders şekli · Uygun saatler |
| 3 | Profile dokunur | Puan, yorumlar, geçmiş, doğrulama rozeti **[PRD]** |
| 4 | Yorumları okur | Doğrulanmış öğrenci rozetli yorumlar **[PRD]** |
| 5 | **Favorilere ekler** *(opsiyonel)* **[YENİ]** | — |
| 6 | **Talep/mesaj gönderir** **[PRD]** | Öğretmene bildirim |
| 7 | *(Öğretmen kabul eder)* | Bağlantı kurulur → Derslerim sekmesi dolar |

**[YENİ] Akıllı öneri:** Öğrencinin M08 verisi zaten sistemde: *"Matematik netin 3 aydır artmıyor — matematik öğretmenlerine bak"*. PRD bunu yazmıyor ama Faz 2'de biriken verinin Faz 4'ü beslemesi, tüm stratejinin mantıksal sonucudur. (PRD Bölüm 10.4 "AI özellikler" bunu yol haritası dışına koyuyor — basit kural tabanlı öneri AI gerektirmez.)

**[YENİ] 18 yaş altı kuralı:** Öğretmen talebi **veli onayından** geçmelidir. Reşit olmayan bir çocuğun, velisinin haberi olmadan yabancı bir yetişkinle ders ayarlaması ürünün kabul edebileceği bir şey değildir.

### AKIŞ 14: Öğretmen Değerlendirme (M13)

**Aktör:** Öğrenci | **Faz:** 4 (Faz 1'de gizli versiyon) | **PRD:** 4.6, 4.7

| Adım | Kullanıcı | Sistem |
|---|---|---|
| 1 | Ders tamamlanır | **Otomatik yorum daveti** **[PRD]** |
| 2 | **1–5 yıldız** verir **[PRD]** | — |
| 3 | Alt kategorileri puanlar **[PRD]** | Anlatım netliği · Dakiklik · Sabır · Ders hazırlığı |
| 4 | Yorum metni yazar **[PRD]** | — |
| 5 | **Gönder** | Doğrulanmış öğrenci rozeti eklenir **[PRD]** |
| 6 | — | **Faz 1–2:** yalnızca öğretmen görür **[PRD]** |
| 7 | — | **Faz 4:** profilde herkese açık **[PRD]** |

**⚠️ Faz 1 → Faz 4 geçiş sorunu [YENİ]:** PRD, Faz 1–2'de toplanan gizli geri bildirimlerin *"veri birikimi Faz 4'ten önce başlar"* amacıyla toplandığını söylüyor. Ama:
- Öğrenci o yorumu **"sadece öğretmenim görecek"** diye yazdı — samimi ve sert olabilir
- Faz 4'te herkese açılırsa **öğrenci rızası ihlal edilir**
- Öğretmen de **"gizli"** sanarak düşük puanı kabullendi

**Öneri:** Faz 1–2 geri bildirimleri **ortalama puana sayılabilir**, ancak **yorum metinleri asla retroaktif yayınlanmamalıdır.** Yayınlanacaksa hem öğrenciden hem öğretmenden **ayrıca onay** alınmalıdır.

**[PRD boşlukları — karar gerekli]:**
- Öğrenci yorumunu **düzenleyebilir/silebilir mi?**
- Yorum **anonim** olabilir mi? (Tek öğretmen-tek öğrenci ilişkisinde "Ayşe K." zaten kimliği ifşa eder; küçük yaş grubunda bu risktir)
- Öğrenci **kaç ders sonra** yorum yapabilir? (İlk dersten sonra yorum, sistemin manipülasyona en açık noktasıdır)

---

## 11. Durum Makineleri

### 11.1 Çalışma Seansı **[PRD + türetilmiş]**

```
                 ┌──────────────┐
    (başlat)────→│   ÇALIŞIYOR  │←──────┐
                 └──────┬───────┘       │
                        │               │ (devam)
              ┌─────────┼─────────┐     │
              ↓         ↓         ↓     │
        ┌──────────┐ ┌─────┐ ┌─────────┴──┐
        │  BİTTİ   │ │İPTAL│ │   MOLADA   │
        └────┬─────┘ └─────┘ └────────────┘
             │      (kaydedilmez)  ↑
             ↓                     │
        KAYDEDİLDİ            mola süresi
        (süre + konu + not)   TOPLAMA EKLENMEZ [PRD]
             │
             ├─→ Streak / hedef / analiz güncellenir
             ├─→ Veli paneline yansır (gizliliğe göre)
             └─→ Öğretmene yansır (izne göre)
```

### 11.2 Öğrenci Hesap Durumu **[YENİ]**

```
MANUEL PROFİL (öğretmen oluşturdu, öğrenci uygulamada yok)
     │
     │ (davet kodu / claim)
     ↓
KAYITLI ──┬──→ VELİ ONAYI BEKLİYOR (18 altı) ──→ AKTİF
          │         │
          │         └─(kısıtlı mod: sayaç çalışır, paylaşım yok)
          └──→ AKTİF (18+)
                  │
                  ├──→ ÖĞRETMENSİZ AKTİF  ← Faz 2'nin hedef durumu
                  └──→ ÖĞRETMENLİ AKTİF   ← Faz 1 / Faz 4 sonrası
```

### 11.3 Ödev (Öğrenci Görünümü) **[PRD + türetilmiş]**

```
YENİ ──→ BEKLİYOR ──┬──→ TAMAMLANDI ──┬──→ ONAYLANDI      [YENİ]
                    │                 └──→ GERİ GÖNDERİLDİ [YENİ] ──→ BEKLİYOR
                    └──→ GECİKTİ (son tarih geçti)
```

---

## 12. Yetki Matrisi — Öğrenci Neyi Yapamaz

**[TÜRETİLMİŞ]**

| # | Öğrenci ŞUNU YAPAMAZ | Neden |
|---|---|---|
| 1 | Ders ekleyemez / değiştiremez / iptal edemez | M04 öğretmene ait |
| 2 | Ders oturumu kaydını değiştiremez | M05 öğretmene ait |
| 3 | Kendine ödev veremez / ödevi silemez | M06 öğretmene ait |
| 4 | Öğretmenin **özel** notunu göremez | Not görünürlük kontrolü [YENİ] |
| 5 | Öğretmenin gelirini/diğer öğrencilerini göremez | Rol izolasyonu |
| 6 | Ders almadığı öğretmene yorum yapamaz | [PRD M13] |
| 7 | Yorumunu öğretmenin profilinden **gizleyemez/sildiremez** — karar gerekli | [PRD boşluk] |
| 8 | Öğretmenin değerlendirme notunu değiştiremez | [PRD M10] |
| 9 | Platform üzerinden ödeme yapamaz | [PRD Bölüm 5] |
| 10 | **18 altıysa** veli onayı olmadan hesabı tam aktifleştiremez | **[YENİ]** |
| 11 | **18 altıysa** velisinden çalışma süresini gizleyemez | **[YENİ]** |
| 12 | Başka öğrencinin verisini göremez | Rol izolasyonu |
| 13 | Rolünü değiştiremez | [TÜRETİLMİŞ] |

---

## 13. Öğrenci Verisi — Kavramsal Model

**[TÜRETİLMİŞ]** — Öğretmen dokümanındaki modelin öğrenci tarafı:

```
User (id, rol=ÖĞRENCİ, email, telefon, şifre, doğum_tarihi [YENİ])
  │
  └─1:1─ StudentProfile
           ├─ ad_soyad, sınıf_seviyesi
           ├─ branşlar[], iletişim
           ├─ hedef_sınav (LGS|TYT|AYT|okul|diğer)     [YENİ]
           ├─ hedef_puan, hedef_seviye
           ├─ günlük_hedef_dakika
           ├─ abonelik_tipi (free|premium)
           ├─ veli_onay_durumu (gerekmiyor|bekliyor|onaylı)  [YENİ]
           ├─ kaynak (manuel|doğrudan_kayıt|claim_edildi)     [YENİ]
           └─ streak_güncel, streak_rekor
                │
                ├─1:N─ StudySession  ⭐ EN YOĞUN TABLO
                │        ├─ konu_id, ders
                │        ├─ başlangıç, bitiş
                │        ├─ net_süre_sn      (mola hariç) [PRD]
                │        ├─ mola_süre_sn                  [PRD]
                │        ├─ kişisel_not      ← ASLA PAYLAŞILMAZ [YENİ]
                │        ├─ giriş_tipi (sayaç|manuel)     [YENİ]
                │        └─ senkronize_mi (offline destek) [YENİ]
                │
                ├─1:N─ TestResult
                │        ├─ ders, konu, tarih
                │        ├─ toplam, doğru, yanlış, boş    [PRD]
                │        ├─ net (hesaplanan)              [PRD]
                │        ├─ net_formülü (4'lü|3'lü|yok)   [YENİ]
                │        └─ deneme_id (nullable)          [YENİ]
                │
                ├─1:N─ MockExam  ← DENEME SINAVI          [YENİ]
                │        ├─ sınav_tipi, tarih
                │        ├─ toplam_net, sıralama_tahmini
                │        └─1:N─ TestResult (dersler)
                │
                ├─1:N─ Goal
                │        ├─ tip (günlük_süre|haftalık_süre|hedef_net|hedef_puan)
                │        ├─ hedef_değer, dönem
                │        └─ gerçekleşen_değer
                │
                ├─1:N─ StreakRecord                        [YENİ]
                │        ├─ tarih, hedef_tutuldu_mu
                │        └─ dondurma_kullanıldı_mı
                │
                ├─1:N─ StudentParent
                │        ├─ veli_id, durum (bekliyor|bağlı|kaldırıldı)
                │        ├─ birincil_veli_mi               [YENİ]
                │        └─1:N─ PrivacySetting  ⭐         [PRD 2.10]
                │                 ├─ veri_alanı (süre|konu|test|streak|hedef|not)
                │                 ├─ hedef_rol (veli|öğretmen)
                │                 └─ görünür_mü (yaş politikası ezebilir) [YENİ]
                │
                ├─1:N─ TeacherStudent  (öğretmen tarafından yönetilir)
                │
                └─1:N─ Review  (Faz 4)
                         ├─ öğretmen_id, yıldız, metin
                         ├─ alt_puanlar {anlatım, dakiklik, sabır, hazırlık}
                         ├─ görünürlük (özel_geribildirim|herkese_açık)
                         └─ yayın_onayı  ← Faz 1→4 geçişi için [YENİ]
```

**Ölçek notu [YENİ]:** `StudySession` sistemdeki **en yoğun yazılan tablodur** — 10.000 aktif öğrenci × günde 4 seans = **1,2 milyon kayıt/ay**. Bu tablo Faz 0'daki şema tasarımında (PRD 0.2) buna göre indekslenmeli ve haftalık/aylık analizler için **önceden hesaplanmış özet tabloları** düşünülmelidir. Her analiz açılışında milyonlarca satır toplanamaz.

---

## 14. Free vs. Premium — Öğrenci Paketi (PRD Bölüm 9.2)

**[PRD]** aynen:

| Özellik | Free | Premium |
|---|---|---|
| Çalışma sayacı | ✅ Basit | ✅ Gelişmiş |
| Ders programı oluşturma | ✅ | ✅ |
| Günlük çalışma süresi | ✅ | ✅ |
| Test / sınav girişi | ✅ | ✅ |
| **Geçmiş çalışma kayıtları** | ❌ | ✅ |
| **Haftalık / aylık analiz** | ❌ | ✅ |
| **Hedef belirleme** | ❌ | ✅ |
| **Streak (seri gün)** | ❌ | ✅ |
| **Motivasyon sistemi** | ❌ | ✅ |
| Öğretmenle detaylı veri paylaşımı | ✅ Basit | ✅ Detaylı |

### 14.1 Bu Tablonun Anlamı

Free kullanıcının elinde kalan ürün:
> **Sayaç çalıştırabilir, test girebilir, bugünkü süresini görebilir. Dün ne yaptığını göremez. Hedef koyamaz. Streak'i yoktur. Hiçbir analiz göremez.**

Bu, **hafızasız bir kronometredir.** Telefonun yerleşik saat uygulamasından farkı yoktur — ve ondan farklı olarak kayıt tutmayı vaat edip tutmaz.

### 14.2 Neden Bu, Stratejiyi Çürütür

PRD'nin kendi cümleleriyle:

| PRD ne diyor | Tablo ne yapıyor |
|---|---|
| *"Bu modül platformun büyüme motorlarından biridir"* (M08) | Motorun parçalarını Free'de söküyor |
| *"Öğrenci ve veliyi öğretmenden bağımsız platforma çeker"* (M08) | Çeken şeyi (streak, analiz, hedef) kapatıyor |
| *"Öğrenci bu modülü öğretmensiz de **tam işlevsel** kullanabilir"* (M08) | Free'de tam işlevsel değil |
| *"Eşleştirme modülüne hazır bir öğrenci havuzu sağlar"* (M08) | Havuz oluşmadan öğrenci gider |
| *"Haftalık çalışma süresi özeti — **Kritik**"* (Faz 2.4) | Free'de ❌ |
| *"Streak ve günlük hedef sistemi — **Yüksek**"* (Faz 2.7) | Free'de ❌ |
| *"Başlangıçta en önemli amaç... **Gelir ikincil önceliktir**"* (Bölüm 5) | Gelir için çekirdek değeri kapatıyor |

**Son satır belirleyicidir.** PRD Bölüm 5, gelirin ikincil olduğunu açıkça yazıyor. 9.2 tablosu ise Faz 2'nin tüm kritik çıktısını gelir duvarının arkasına koyuyor. Bu iki bölüm aynı üründe uygulanamaz.

### 14.3 Önerilen Free/Premium Sınırı **[YENİ]**

Prensip: **Free = çekirdek alışkanlık kurulur. Premium = derinlik, geçmiş, tahmin.**

| Özellik | Free (önerilen) | Premium (önerilen) |
|---|---|---|
| Çalışma sayacı | ✅ Tam | ✅ + Pomodoro |
| **Streak** | ✅ **Tam** | ✅ + dondurma/telafi |
| **Günlük hedef** | ✅ **1 hedef** | ✅ Çoklu + branş bazlı |
| **Geçmiş kayıtlar** | ✅ **Son 30 gün** | ✅ Sınırsız |
| **Haftalık analiz** | ✅ **Temel** (toplam + konu dağılımı) | ✅ Detaylı + trend + karşılaştırma |
| Aylık analiz | ❌ | ✅ |
| Test girişi | ✅ Sınırsız | ✅ |
| Net gelişim grafiği | ✅ **Son 30 gün** | ✅ Sınırsız + tahmin |
| Deneme sınavı | ✅ Ayda 5 | ✅ Sınırsız |
| Kişisel rekorlar | ✅ | ✅ |
| Hedef puan/net takibi | ❌ | ✅ |
| Konu bazlı zayıflık analizi | ❌ | ✅ |
| PDF rapor | ❌ | ✅ |
| Reklamsız | ❌ | ✅ |

**Neden bu işler:** Streak ve 30 günlük geçmiş **alışkanlık kurar**; alışkanlık kurulunca öğrenci "geçen dönemle karşılaştır", "hedefe kalan", "zayıf konularım" ister ve **kendi isteğiyle** öder. Bugünkü tabloda öğrenci alışkanlık kurmadan ödeme duvarına çarpar ve uygulamayı siler — ne gelir olur ne eşleştirme havuzu.

**Ek not:** Hedef kitlenin büyük bölümü **öğrencidir ve kendi geliri yoktur.** Öğrenci Premium'unu fiilen veli öder. Veli, çocuğunun 3 gündür kullandığı uygulamaya para vermez; **çocuğun 3 haftadır her gün kullandığı** uygulamaya verir. Bu da Free'de alışkanlık kurulmasını zorunlu kılar.

---

## 15. Faz Bazlı Öğrenci Yol Haritası

### FAZ 0 — Altyapı **[PRD]**
- Kayıt/giriş/şifre sıfırlama
- Rol bazlı yetkilendirme (öğrenci rolü)
- Push altyapısı
- **+ 18 yaş altı veli onayı ve KVKK açık rıza akışı [YENİ — kritik]**
- **+ StudySession tablosu ölçek tasarımı [YENİ]**

### FAZ 1 — Öğretmen Çekirdeği (öğrenci tarafı minimal) **[PRD]**
| PRD # | İş | Öncelik |
|---|---|---|
| 1.2 | Öğretmen tarafından manuel eklenme (pasif) | Kritik |
| 1.7 | **Öğrenci giriş ekranı — ders geçmişi ve ödevleri görme** | Yüksek |
| 1.8 | Yaklaşan ders push bildirimi | Yüksek |
| 1.9 | Öğretmene özel geri bildirim gönderme | Orta |
| **+B-06** | **Ödev teslimi — dosya/foto yükleme** | **Yüksek [YENİ]** |
| **+B-12** | **Ders erteleme talebi** | **Orta [YENİ]** |

### FAZ 2 — Öğrenci Ürünü Doğar ⭐ **[PRD]**
> **[PRD] Hedef:** *"Öğrenci ve veli, platforma öğretmenden BAĞIMSIZ girebilmeli ve değer bulabilmelidir."*
> **[PRD] Çıktı:** *"Öğrenci kendi çalışmalarını takip eder... Öğretmen gerekmez. Bu faz, platforma bağımsız bir kullanıcı kitlesi oluşturur."*

| PRD # | İş | Öncelik |
|---|---|---|
| 2.1 | Öğrenci doğrudan kayıt akışı (öğretmensiz) | Kritik |
| 2.2 | Çalışma sayacı — konu, başlat/durdur/bitir, mola | Kritik |
| 2.3 | Çalışma seansı kaydı ve geçmiş listesi | Kritik |
| 2.4 | Haftalık çalışma süresi özeti | Kritik |
| 2.5 | Test girişi — doğru/yanlış/boş, net hesabı | Yüksek |
| 2.6 | Konu bazlı test performansı takibi | Yüksek |
| 2.7 | Streak ve günlük hedef sistemi | Yüksek |
| 2.8 | Veli bağlantısı | Kritik |
| 2.10 | İzin bazlı görünürlük (gizlilik kontrolü) | Yüksek |
| **+B-01** | **Claim akışı — manuel profili devralma** | **Kritik [YENİ]** |
| **+B-02** | **Arka plan + offline sayaç** | **Kritik [YENİ]** |
| **+B-03** | **Manuel seans girişi** | **Yüksek [YENİ]** |
| **+B-04** | **Net formülü sınav tipine göre** | **Yüksek [YENİ]** |
| **+B-05** | **Streak kuralları tanımı** | **Kritik [YENİ]** |
| **+B-07** | **Yaş bazlı gizlilik matrisi** | **Kritik [YENİ]** |

### FAZ 3 — Gelişim & Bildirimler **[PRD]**
- 3.2 Gelişim takibi (konu kazanımı, eksik/güçlü)
- 3.3 Performans grafikleri
- 3.4 Hedef puan/net takibi
- 3.5 Bildirim genişletme (ödev, günlük çalışma)
- 3.6 Haftalık özet
- **+ Deneme sınavı desteği [YENİ]**

### FAZ 4 — Eşleştirme & Puanlama **[PRD]**
- 4.1–4.4 Öğretmen arama, filtreleme, profil, talep
- 4.6–4.7 Puanlama + yorum
- 4.9 Doğrulanmış öğrenci rozeti
- **+ Faz 1–2 gizli geri bildirimlerin yayın politikası [YENİ — kritik]**

### FAZ 5 — Premium **[PRD]**
- 5.2 Free/Premium kısıtları ← **Bölüm 14.3 önerisi burada uygulanır**
- 5.8 Öğrenci haftalık/aylık analiz premium

---

## 16. PRD Boşlukları ve Çelişkiler ⚠️

### 16.1 🚨 STRATEJİK ÇELİŞKİ (En Öncelikli Karar)

**M08 hem "büyüme motoru" hem de Free'de kapalı olamaz.**

Detaylı analiz Bölüm 14'te. Karar seçenekleri:

| Seçenek | Sonuç |
|---|---|
| **A — Free'yi aç** (Bölüm 14.3 önerisi) | Büyüme motoru çalışır, gelir gecikir. **PRD Bölüm 5 ile uyumlu** ("gelir ikincil önceliktir") — **önerilen** |
| **B — 9.2 tablosunu koru** | Faz 2'nin tüm kritik çıktısı ödeme duvarında; öğrenci havuzu oluşmaz; Faz 4 eşleştirme tek taraflı kalır |
| **C — Süre bazlı deneme** | İlk 30 gün her şey açık, sonra kısıtla. Alışkanlık kurulur ama kesinti travmatiktir |

### 16.2 Çelişkiler

| # | Çelişki | Detay |
|---|---|---|
| Ç-01 | **Haftalık analiz** | Faz 2.4'te "Kritik", 9.2'de "Free ❌" |
| Ç-02 | **Streak** | Faz 2.7'de "Yüksek", 9.2'de "Free ❌" |
| Ç-03 | **Hedef belirleme** | M08'de temel, Faz 3.4'te iş kalemi, 9.2'de "Free ❌" |
| Ç-04 | **Geçmiş seans listesi** | M08'de temel ("geçmişe dönük seans listesi"), 9.2'de "Free ❌" |
| Ç-05 | **"Tam işlevsel"** | M08: *"öğretmensiz de tam işlevsel"*; 9.2 Free'de değil |
| Ç-06 | **"Ders programı oluşturma ✅ Free"** | ✅ **Çözüldü (2026-07-19)** — §5.2 Kendi Ders/Plan Modeli: öğrenci kendi dersini (`teacher_id=null`) ekler/planlar, seans gerçekleşme katmanıdır. Öğretmenin M04 dersine dokunamaz. |
| Ç-07 | **Gizlilik** | *"otomatik veli paneline yansır"* vs *"öğrenci isterse gizleyebilir"* — yaş politikası olmadan çözülemez |
| Ç-08 | **Gizli geri bildirim** | Faz 1'de "sadece öğretmen görür" → Faz 4'te açılırsa rıza ihlali |

### 16.3 Eksik Özellikler

| # | Boşluk | Etki | Faz |
|---|---|---|---|
| B-01 | **Claim akışı** (manuel profili devralma) | **Kritik** — iki giriş yolu birleşemez, veri ikiye bölünür | 2 |
| B-02 | **Arka plan/offline sayaç** | **Kritik** — sayaç güvenilmezse M08 ölür | 2 |
| B-03 | **Manuel seans girişi** | Yüksek — sayaç unutulunca veri kaybı | 2 |
| B-04 | **Net formülü sınav tipine göre** | **Yüksek** — LGS'de `/4` kullanılırsa tüm veri yanlış | 2 |
| B-05 | **Streak kuralları** (ne sayar, ne kırar) | **Kritik** — tanımsız, geliştirilemez | 2 |
| B-06 | **Ödev teslimi — dosya yükleme** | Yüksek — ödev modülü tek yönlü kalıyor | 1 |
| B-07 | **Yaş bazlı gizlilik matrisi** | **Kritik** — yasal + ürün gerekliliği | 2 |
| B-08 | **18 yaş altı veli onayı (KVKK)** | **Kritik — yasal** | **0** |
| B-09 | **Deneme sınavı** | Yüksek — sınav öğrencisinin verisinin yarısı | 3 |
| B-10 | **Hedef sınav seçimi** | Orta — net formülü ve hedefler buna bağlı | 2 |
| B-11 | **Çoklu veli** (anne + baba) | Orta | 2 |
| B-12 | **Ders erteleme talebi** | Orta — "dağınık iletişim" problemi çözülmüyor | 1 |
| B-13 | **Yorum düzenleme/silme/anonimlik** | Orta | 4 |

### 16.4 Karar Bekleyen Sorular

1. **Öğrenci birden fazla öğretmene bağlanabilir mi?** (Gerçekte matematik + fizik ayrı öğretmenlerdir — model buna göre kurulmalı) *(öğretmen dokümanında da açık)*
2. **Reşit öğrenci kendi ödeme bakiyesini görebilmeli mi?**
3. **Öğrenci Premium'unu kim öder** — öğrenci mi veli mi? Veli ödüyorsa satın alma akışı veli panelinde de olmalı.
4. **18 yaş altı öğrenci öğretmen talebi gönderebilir mi**, yoksa veli onayı şart mı?
5. **Öğrenci verisi dışa aktarılabilir mi?** (KVKK veri taşınabilirliği)
6. **Öğrenci-öğretmen doğrudan mesajlaşması var mı?** (PRD'de öğretmen↔veli mesajı var; öğretmen↔öğrenci tanımsız)
7. **Free'de streak kapalıysa** öğrenci onu hiç görmeyecek mi, yoksa "Premium'da streak'in olurdu" şeklinde mi gösterilecek?

---

## 17. Faz 2 Kabul Kriterleri (Öğrenci Rolü)

**[TÜRETİLMİŞ]** — PRD Faz 2 çıktısı: *"Öğrenci kendi çalışmalarını takip eder... Öğretmen gerekmez."* Test edilebilir hâli:

- [ ] Öğrenci **90 saniyede** kayıt olup ilk seansını başlatabiliyor
- [ ] Onboarding'de **öğretmen hiç sorulmuyor**
- [ ] Sayaç **ekran kapalıyken** doğru sayıyor
- [ ] Sayaç **uçak modunda** çalışıyor, internet gelince senkronize oluyor
- [ ] Uygulama **çökse bile** seans kurtarılıyor
- [ ] Mola süresi toplam süreye **eklenmiyor** (PRD şartı)
- [ ] Öğrenci **manuel seans** ekleyebiliyor
- [ ] Test girişinde net **doğru formülle** hesaplanıyor (LGS ≠ TYT)
- [ ] Streak kuralları **yazılı** ve uygulama bunlara uyuyor
- [ ] Öğrenci velisinin **tam olarak neyi gördüğünü** kendi ekranından görebiliyor
- [ ] **Kişisel seans notları hiçbir role sızmıyor**
- [ ] 18 yaş altı kayıt **veli onayı olmadan tam aktifleşmiyor**
- [ ] Öğretmenin eklediği öğrenci, **claim akışıyla** kendi hesabına geçebiliyor — veri kaybı olmadan
- [ ] **Öğrenci 14 gün boyunca öğretmensiz uygulamayı her gün açıyor** ← *asıl test budur*

---

## 18. Özet — Öğrenci Rolü Tek Sayfada

| Boyut | Özet |
|---|---|
| **Kim** | Ders alan **veya sadece kendi çalışmasını takip eden** kişi |
| **Neden gelir** | Çalışma takibi + test performansı (öğretmen için **değil**) |
| **Neden kalır** | Streak + günlük hedef + gelişim görme |
| **Ne zaman dönüşür** | Faz 4 — "netim artmıyor" → öğretmen arar |
| **Ana ekran** | **Sayaç** (öğretmende Takvim'di) |
| **Kritik akış** | Çalışma seansı (günde 8 kez — sürtünme sıfır) |
| **Toplam yetenek** | **~90 yetenek** (S-01.1 … S-15.5) |
| **Sahip olduğu modül** | **M08** (tek — ama stratejinin tamamını taşır) |
| **İzleyici olduğu** | M04, M05, M10 (salt okunur) |
| **Tüketici olduğu** | M06 (ödev) |
| **Kontrol sahibi olduğu** | **M09 gizlilik ayarları** ⭐ |
| **Aktif olduğu (Faz 4)** | M12 (arayan), M13 (puanlayan) |
| **Faz 1'de** | Neredeyse pasif — sadece ders geçmişi + ödev görüntüleme |
| **Faz 2'de** | **Ürün doğar** — bağımsız kullanıcı olur |
| **En büyük risk** | **Free/Premium sınırı büyüme motorunu boğuyor** (Bölüm 14) |
| **En büyük yasal risk** | **18 yaş altı veli onayı Faz 0'da yok** |
| **En büyük teknik risk** | **Arka plan/offline sayaç güvenilirliği** |
| **PRD'deki en kritik boşluk** | **Claim akışı** — iki giriş yolu birleşemiyor |

---

*Bu doküman PRD v2.0'a dayanır. **[YENİ]** etiketli maddeler öneridir ve onayınızı bekler.*
*Kardeş doküman: `ogretmen_rolu_fonksiyonel_dokuman_v1.md`*
