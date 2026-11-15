---
title: "UX Kuralları"
summary: "Tüm ekranlarda geçerli UX kararları, etkileşim ilkeleri ve navigasyon kuralları; rol bazlı öncelikler"
tags: [mimari, ux, navigasyon]
authority: derived
updated: 2026-11-15
---

# 📐 UX Kuralları — EğitimÜssü

> **Kapsam:** Tüm ekranlarda geçerli UX kararları, etkileşim ilkeleri ve navigasyon kuralları.
> Token değerleri → [`design_system.md`](design_system.md). Widget kataloğu → [`widgets.md`](widgets.md).
>
> **Güncelleme:** 2026-11-15

---

### Oluşum iptali (P17 T5)

Tekrarlı derste iptal önce kapsam seçimi (varsayılan yalnız bu ders), ardından neden ve ücretsiz iptal seçimiyle onaylanır. Başarılı Scheduling yanıtı yalnız kapsam içindeki satırların durumuna uygulanır; serinin çapa zamanı diğer haftalara kopyalanmaz. Hata başarıya çevrilmez. Ders detayı geri dönüşte değişiklik sonucu verir ve kaynak liste yenilenir. Yeni iptal metinleri tr/en ARB’dedir.


## 1. Rol Bazlı UX İlkeleri

Her rol için öncelik farklıdır — ekran tasarımı bunu yansıtır:

| Rol | Öncelik | Ekran tonu |
|-----|---------|-----------|
| **Öğretmen** | Operasyon hızı — bugünkü ders, ödev, ödeme | Yoğun bilgi, aksiyona odaklı |
| **Öğrenci** | Motivasyon — streak, ilerleme, görev listesi | Enerjik, teşvik edici |
| **Veli** | Sade özet — çocuğun durumu, yaklaşan dersler | Minimal, sade, güven verici |

---

## 2. Navigasyon Kuralları

- **Bottom Navigation:** Maksimum 5 sekme. Aktif sekme `primary (#082B4F)`, pasif `textSecondary (#6B7280)`.
- **Stack derinliği:** 3 seviyeyi geçme — Ana → Liste → Detay.
- **Geri butonu:** Her iç ekranda sol üstte. Başlık metni `h3 (18px, w700)`.
- **Modal / Bottom Sheet:** Yıkıcı işlemler (silme, iptal) modal'da onaylanır. Sheet üst köşe radius: 24.
- **Tab geçişi:** Sekme değişiminde scroll pozisyonu sıfırlanmaz.
- **Deep link:** Kayıtlı rota, rol izin tablosuna göre açılır. Yetkisiz alan rolün ana ekranına döner; bilinmeyen adres “Sayfa bulunamadı” ve ana sayfa düğmesi gösterir.
- **Ortak ekranlar (P20 T2):** Mesajlar, bildirim merkezi, kayıtlı ayarlar/hesap ve üyelik rotaları veli dahil tüm oturumlu rollere açıktır. Veli ana sayfasında mesaj ve zil girişleri bulunur.
- **Bildirim hedefi:** OS push ve in-app kart aynı rol eşleyicisini kullanır; bilinmeyen/yetkisiz hedef bildirim merkezine düşer. Veli ödeme/ödev/ders hedefleri mevcut onaylı çocuk seçicili özet ekranlarıdır; yeni işlem yetkisi vermez.

---

## 3. Form & Girdi Kuralları

- Her form alanının üstünde `AppFieldLabel` (zorunlu alanlar `*` işaretli).
- Validasyon **submit anında** çalışır, yazarken değil (klavyeyi gizleme).
- Hata mesajı alanın altında, `accentRed (#FF5A5F)` rengiyle, `caption (12px)` boyutunda.
- Başarılı submit → `SnackBar` (yeşil) veya bir sonraki sayfaya yönlendirme; asla iki uyarı birden.
- Başarı bildirimi yalnız gerçek kayıt tamamlandıktan sonra gösterilir. Kayıt hatasında girilen metin korunur; “Tekrar dene” formu yeniden yüklemek yerine aynı kaydı tekrar gönderir (ders notu formu, 2026-10-16).
- Uzun formlar → bölümlere ayrılır (`SectionHeader` ile), tek sayfada kaydırılır.
- Dropdown seçenekleri alan genişliğine sığmalı; uzun adlar ve büyük yazı ölçeği için sabit seçenek yüksekliği kullanılmaz (`AppDropdownField`).
- Tarih/saat → her zaman `AppDateTimeField` (native picker); kullanıcı elle yazmaz.

---

### Finans girdisi (P18 T4)

Türkçe para alanlarında nokta binlik, virgül ondalık ayracıdır. `1.500` her zaman 1.500,00'dır; belirsiz/bozuk gruplama ve ikiden çok ondalık reddedilir. Düzenleme formu tahsil edilen tutarı ve para birimini kilitler; yeni tahsilat listedeki ayrı eylemden girilir. Sonuç sunucu yanıtıyla gösterilir. Finans özeti ve grafiklerde tek para birimi seçilir, farklı para birimleri toplanmaz. Aynı mutasyon sürerken ikinci dokunma engellenir; belirsiz ağ sonucunun tekrarında aynı işlem anahtarı ve gövdesi korunur (uygulama belleği süresince).

## 4. Yükleme & Durum Yönetimi

Her veri yükleyen ekran 3 durumu yönetir:

```
Loading  →  LoadingStateView  (spinner + mesaj)
Error    →  ErrorStateView    (mesaj + "Tekrar Dene" butonu)
Empty    →  EmptyStateView    (ikon + başlık + açıklama)
```

- **Loading süresi > 300 ms** ise skeleton veya spinner göster; anlık değişimde gösterme.
- Hata mesajı teknik değil, kullanıcı dostu: "Bağlantı kurulamadı, tekrar deneyin."
- Ortak hata kartında "Tekrar dene" mesajın altında gösterilir; dar ekranlarda mesaj ile düğme birbirini sıkıştırmaz. Boş durum, kısa görünüm alanında içeriğini kaydırarak erişilebilir tutar.
- **Optimistik güncelleme:** Liste silme/ekleme işlemlerinde önce UI güncellenir, API başarısız olursa geri alınır.
- Pull-to-refresh: Tüm liste ekranlarında desteklenir.

---

## 5. Geribildirim & Bildirim Kuralları

| Durum | Bileşen | Renk |
|-------|---------|------|
| Başarı | `SnackBar` (alt) | `accentGreen` |
| Uyarı | `SnackBar` (alt) | `accentOrange` |
| Hata | `SnackBar` (alt) veya `ErrorStateView` | `accentRed` |
| Yıkıcı işlem onayı | `AlertDialog` | Onay butonu `accentRed` |
| Bilgi | `SnackBar` (alt) | `primary` |

- `SnackBar` süresi: 3 saniye. Aksiyonlu SnackBar: 5 saniye.
- Aynı anda birden fazla SnackBar gösterilmez.
- `AlertDialog` başlığı kısa (≤6 kelime), içerik açıklayıcı, iptal butonu daima solda.

---

## 6. Liste & Kart Kuralları

- Liste öğesi yüksekliği: minimum **56px** (dokunma hedefi).
- Swipe-to-delete: Yalnızca geri alınabilir işlemlerde. Kırmızı arka plan + çöp kutusu ikonu.
- Sayfalama: Sonsuz scroll (infinite scroll); "Daha fazla yükle" butonu kullanılmaz.
- Boş liste → `EmptyStateView` (rol ve bağlama özel başlık/açıklama).
- Kart tıklanabiliyorsa `InkWell` veya `GestureDetector` ile `splashColor: primaryLight`.

---

## 7. Tipik Ekran Yapısı

```
Scaffold(
  backgroundColor: 0xFFF7F9FC,      // background
  body: SafeArea(
    child: Column(
      children: [
        AppHeader(title: '...'),    // Üst bar
        Expanded(
          child: RefreshIndicator(
            child: ListView(         // veya CustomScrollView
              padding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              children: [
                SectionHeader(...),
                ...kartlar (gap: 12)
              ],
            ),
          ),
        ),
      ],
    ),
  ),
  bottomNavigationBar: AppBottomNav(...),
)
```

---

## 8. Metin Dili ve Yazım (D-19 / D-20)

Kullanıcıya görünen her metin **eksiksiz Türkçe** yazılır. Bu bir tercih değil, marka kuralıdır:
Türkçe karaktersiz yazım ("Cikis yap", "Odeme hatirlatmalari") ürünün özensiz görünmesine yol açar.

| Kural | Nasıl |
|-------|-------|
| **Türkçe karakterler eksiksiz** | `ç ğ ı İ ö ş ü` — kısaltma yok. `Çıkış`, `Ödeme`, `Öğrenci`, `Ayarları` |
| **Uygulama adı** | Görünen metin **EğitimÜssü**. `EgitimUssu` yalnız dosya/klasör/kod tanımlayıcısıdır (kök `CLAUDE.md`) |
| **Metinler ARB'de** | `AppLocalizations.of(context).<ekran>_<eleman>` — bkz. [`mobile_flutter.md` §10.1](mobile_flutter.md) |
| **Hizmet ≠ pazarlama dili** | Ders hatırlatması bilgilendirir, kampanya duyurusu izin ister; ikisi aynı tonu paylaşmaz |

> **Bu kural otomatik denetlenir.** `mobile/test/l10n/localization_test.dart` hem ARB değerlerini hem
> **tüm `lib/**` kaynak dosyalarını** tarar; bozuk yazım bulursa test kırılır. Yani düzeltilen bir metin
> geri gelemez. Kontrol listesi kesin hataları içerir ("Devam", "Kaydet" gibi zaten doğru yazımlar hariç)
> ve kelime **başı** sınırıyla eşleşir — "rozet" içindeki "ozet" yanlış alarm üretmez.

---

*UX Kuralları | Güncelleme: 2026-11-15 (hata sonrası kayıt tekrarında form verisini koruma) · 2026-09-05 (P13 Task 6 / D-19+D-20: §8 metin dili ve yazım kuralı — eksiksiz Türkçe, ARB'den metin, otomatik denetim) · 2026-06-27*

*Güncelleme: 2026-11-15 — P18 T4 mobil para ve idempotency.*

### Cursor ile liste devamı (P19 Task 7)

Ödev, çalışma geçmişi/notlar ve bildirim merkezinde ilk sayfa50kayıttır; aşağı kaydırınca devam eder. Kısa ilk sayfa görünümü doldurur. Devam hatası yüklenmiş satırları silmez, yeniden deneme aynı cursor'u kullanır; hata varken otomatik tekrar döngüsü kurulmaz. Yenileme/filtre değişimi cursor'u ve istek neslini sıfırlar; gecikmiş eski sonuç atılır. Öğretmen ödev özet/filtre sayıları yalnız yüklenen kayıtlar içindir ve bu kapsam etiketle görünürdür. Genel öğretmen panelinin toplamı ise bütün sayfalardan gelir.

*Güncelleme: 2026-11-15 — P19 Task 7.*

## P20 T1 — gerçek veri ve kullanılabilir eylem

Gerçek kaynağı olmayan kullanıcı sayısı, puan, hedef ve konu hâkimiyeti gösterilmez. Başlangıç/yükleme hatası örnek profil üretmez. Öğrenci ana sayfasında ödev okuma hatası gerçek hata ve tekrar deneme sunar; başarılı boş sonuç ayrı boş durumdur. Çalışmayan profil/veli/gizlilik/abonelik yer tutucuları ve satın alma/gelecek Premium vaatleri gizlidir. Test/katalog kayıtlarına ders adı eşleşmesiyle oluşum ilişkisi atfedilmez; genel gerçek erişimler korunur. Statik geliştirme önizlemelerinin rotaları yalnız debug derlemesinde bulunur.

## Önbellekteki veri ve çıkış (P20 T3)

Bağlantı kurulamadığında son kaydedilen veri kullanılıyorsa uygulama kökünde `StaleDataBanner` görünür; kayıt tarihi/saati yerel biçimde, metin tr/en ARB'den gelir. Uyarı liste/detay/form tüketicilerinin tamamını kapsar; en eski kullanılan kaydın zamanı gösterilir. İlgili sorgular başarıyla tazelenince veya çıkışta kapanır; yalnız ekran değiştirmek eski veri bilgisini gizlemez. HTTP 403/500, iptal veya sertifika hatası önbellek başarısı gibi gösterilmez. Çıkış/hesap değişimi bildirim rozetini, üyelik/premium kararını ve kişisel önbelleği temizler.

*Güncelleme: 2026-11-15 — P20 Task 3.*
