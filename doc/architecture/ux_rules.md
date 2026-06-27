# 📐 UX Kuralları — EğitimÜssü

> **Kapsam:** Tüm ekranlarda geçerli UX kararları, etkileşim ilkeleri ve navigasyon kuralları.
> Token değerleri → [`design_system.md`](design_system.md). Widget kataloğu → [`widgets.md`](widgets.md).
>
> **Güncelleme:** 2026-06-27

---

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
- **Deep link:** Her ekranın `go_router` route'u tanımlıdır; direkt erişilebilir.

---

## 3. Form & Girdi Kuralları

- Her form alanının üstünde `AppFieldLabel` (zorunlu alanlar `*` işaretli).
- Validasyon **submit anında** çalışır, yazarken değil (klavyeyi gizleme).
- Hata mesajı alanın altında, `accentRed (#FF5A5F)` rengiyle, `caption (12px)` boyutunda.
- Başarılı submit → `SnackBar` (yeşil) veya bir sonraki sayfaya yönlendirme; asla iki uyarı birden.
- Uzun formlar → bölümlere ayrılır (`SectionHeader` ile), tek sayfada kaydırılır.
- Tarih/saat → her zaman `AppDateTimeField` (native picker); kullanıcı elle yazmaz.

---

## 4. Yükleme & Durum Yönetimi

Her veri yükleyen ekran 3 durumu yönetir:

```
Loading  →  LoadingStateView  (spinner + mesaj)
Error    →  ErrorStateView    (mesaj + "Tekrar Dene" butonu)
Empty    →  EmptyStateView    (ikon + başlık + açıklama)
```

- **Loading süresi > 300 ms** ise skeleton veya spinner göster; anlık değişimde gösterme.
- Hata mesajı teknik değil, kullanıcı dostu: "Bağlantı kurulamadı, tekrar deneyin."
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

*UX Kuralları | Güncelleme: 2026-06-27*
