---
title: "Geliştirme Yol Haritası"
summary: "Faz 0-5 geliştirme yol haritası: epic→faz eşlemesi, bağımlılıklar, milestone'lar"
tags: [yol-haritasi, planlama, faz]
authority: product
updated: 2026-06-24
---

# 🗺️ EğitimÜssü — Geliştirme Yol Haritası

> Bu yol haritası; [`ozel_ders_platformu_PRD_v2.md`](ozel_ders_platformu_PRD.md) (v2.1) fazları,
> [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) modül durumları ve
> [`modules/mimari_inceleme.md`](modules/mimari_inceleme.md) öncelikleri birleştirilerek hazırlanmıştır.
> Jira karşılığı: [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv) (her görevde `faz-N` etiketi).
>
> **Temel strateji:** Önce rollerin **bireysel/ortak** kullanımını doldur, **eşleştirme/ilan**'ı en son aç (PRD §2).
>
> **Güncelleme:** 2026-06-24

---

## Faz Özeti

| Faz | Ad | Ana hedef | Durum |
|-----|-----|-----------|-------|
| **Faz 0** | Temel & Sertleştirme | Mimari/güvenlik açıklarını kapat + ortak altyapı (dosya depolama, read-model, push) | 🔴 Açık (kritik) |
| **Faz 1** | Öğretmen Çekirdeği (MVP) | Öğretmen her gün kullanır: takvim, öğrenci, ders, ödev/kaynak, ödeme | 🟢 Büyük ölçüde hazır, eksikler var |
| **Faz 2** | Öğrenci Bireysel Çalışma | Öğretmensiz öğrenci: kronometre, test/net, hedef, seri, başarım | 🔴 İskelet |
| **Faz 3** | Gelişim, Bildirim & Veli | Konu gelişimi, veli paneli (tam), mesajlaşma, zengin bildirim | 🔴 İskelet |
| **Faz 4** | Eşleştirme & Değerlendirme | İki taraflı ilan, keşif/filtre, puanlama/yorum, şikayet/moderasyon | 🔴 İskelet |
| **Faz 5** | Premium & Para Kazanma | Üyelik/abonelik, reklam, kampanya/referans, raporlama/analitik | 🔴 İskelet |

> Lejant: 🟢 büyük ölçüde hazır · 🟡 kısmen · 🔴 yapılacak.

---

## 🔴 Faz 0 — Temel & Sertleştirme (ÖNCE)

> Bu faz **diğer her şeyin önkoşuludur**. Mimari açıklar kapanmadan üst fazlar güvensiz/çalışmaz olur.

**Epicler:** `Mimari ve Güvenlik Sertleştirme`
**Kritik işler (mimari_inceleme):**
- **Y1** Öğretmen self-verify açığını kapat · **K1** Outbox'ı aç + startup uyarısı · **K3** authorizer fail-fast guard + eksik authorizer'lar
- **K2+Y4** Outbox retry/error izolasyonu + idempotent handler (inbox) · **Y2** sırları env'e taşı · **Y3** mobil refresh token
- **O1** rate limiting · **O7** güvenlik/outbox regresyon testleri

**Ortak altyapı (üst fazların önkoşulu):**
- **O8 Dosya depolama (`IFileStorage`)** → ödev yükleme, ders kaynağı, profil fotoğrafı için (Faz 1-2 bloklar).
- **O5 Modüller arası okuma (read-model/contract)** → Veli paneli (M09), Eşleştirme (M12), Raporlama (M14) önkoşulu.
- **Push altyapısı (FCM/APNs) + cihaz token kaydı** (PRD Faz 0.7) → Faz 1 bildirimleri için.

**Çıktı:** Event akışı çalışır, yetki açıkları kapalı, sırlar güvenli, dosya depolama + read-model + push hazır.

---

## 🟢 Faz 1 — Öğretmen Çekirdeği (MVP)

> Çoğu hazır (🟢). Hedef: 5–10 gerçek öğretmenle beta'ya hazır hale getirmek.

**Epic:** `Öğretmen MVP Tamamlama` · İlgili roller/modüller: [`roles/ogretmen`](roles/ogretmen.md), M02–M07, M11.
**Açık işler:**
- Dashboard özet endpoint + zengin ana ekran · Ders güncelleme (PUT) · LessonSession yaşam döngüsü
- Öğrenci düzenleme/pasifleştirme + branş & veli bağlama alanları · Push bildirim uçtan uca · Ödeme Overdue otomasyonu · Yetki testleri
- **Yeni (promp.txt):** Online ders linki (`MeetingUrl`) · Tekrarlı ders açılımı · Takvim tatilleri (`ScheduleException`) · Ders **kaynağı** (`LessonResource`)

**Çıktı:** Öğretmen öğrencilerini/derslerini/ödev-kaynak/ödemelerini tek takvimde yönetir; beta test.

---

## 🔴 Faz 2 — Öğrenci Bireysel Çalışma

**Epic:** `Öğrenci Bireysel Çalışma` · İlgili: [`roles/ogrenci`](roles/ogrenci.md), [`modules/m08_study`](modules/m08_study.md).
**İşler:**
- Study domain (StudySession/TestResult/Goal/Streak) · Kronometre API · Geçmiş + haftalık özet · Test/net API · Streak + günlük hedef
- Self-registration onboarding · Rol bazlı mobil navigasyon · Mobil kronometre/test ekranları
- Öğrencinin kendi ders geçmişi/ödev görünümü · Öğretmen-öğrenci davet/bağ kurma
- **Yeni:** **Başarım (achievement)** sistemi · Öğrenci **ödev yükleme** (`AssignmentSubmission`) + son tarih kaçırma → **veli bildirimi** · **Çakışma önceliği** (özel ders > bireysel plan, öğrenci uyarısı)

**Önkoşul:** Faz 0 dosya depolama (yükleme) + push (bildirim).
**Çıktı:** Öğrenci öğretmensiz tam değer bulur; eşleştirmeye hazır öğrenci havuzu oluşur.

---

## 🔴 Faz 3 — Gelişim, Bildirim & Veli

**Epicler:** `Veli Paneli`, `Mesajlaşma` (kısmı), ProgressTracking/Notifications genişletme.
**İşler:**
- **M10 ProgressTracking:** konu ustalığı/eksik/hedef + zaman serisi (öğretmen & veli gelişim görünümü)
- **M11 Notifications genişletme:** ödev son tarih/kaçırma → veli, ödeme, yeni mesaj; Settings tercihlerine saygı
- **Veli Paneli (M09):** Parents domain, profil, veli-çocuk onaylı bağ, görünürlük/izin matrisi, birleşik dashboard (read-model), mobil ekranlar
- **Mesajlaşma (M16):** öğretmen↔öğrenci ve öğretmen↔veli sohbet (Faz 2'de başlayıp burada olgunlaşır)

**Önkoşul:** Faz 0 read-model (veli birleşik panel), Faz 2 Study (veli için veri).
**Çıktı:** Veli çocuğunu grafik/raporla izler; mesajlaşma + zengin bildirim aktif.

---

## 🔴 Faz 4 — Eşleştirme & Değerlendirme

**Epicler:** `Eşleştirme ve Değerlendirme`, `Geri Bildirim ve Şikayet` (şikayet/moderasyon kısmı).
**İşler:**
- **M12 Matching:** arama read-model, arama/filtre API, public öğretmen profili, eşleştirme talep akışı; **iki taraflı ilan** (öğretmen sunar / öğrenci arar); **konum + yıldız + ücretli öne çıkarma** sıralaması
- **M13 Reviews:** domain, doğrulanmış öğrenci yorumu, ortalama puan, öğretmen yanıtı, flag/moderasyon (erken "öğretmene özel geri bildirim" Faz 1'de başlar)
- **M18 Feedback/Şikayet:** kötüye kullanım şikayeti + ortak admin moderasyon kuyruğu
- Web (Angular) keşif/değerlendirme için temel hazırlık

**Önkoşul:** Faz 1-2-3 gerçek kullanıcılarda doğrulanmış olmalı (PRD §10.1); read-model (Faz 0).
**Çıktı:** Platform dışarıdan öğretmen/öğrenci çeker; güven altyapısı (yıldız/yorum/moderasyon) çalışır.

---

## 🔴 Faz 5 — Premium & Para Kazanma

**Epicler:** `Üyelik ve Para Kazanma`, `Raporlama ve Analiz`.
**İşler:**
- **M17 Membership:** SubscriptionPlan + UserSubscription + entitlement yayılımı; **reklam** (free görür, premium görmez); **kampanya** (ilk ay ücretsiz) + **referans** (arkadaşını getir); ödeme sağlayıcı entegrasyonu; rol bazlı paywall
- **M14 Reporting:** öğretmen gelir/öğrenci analizi, boş zaman analizi, PDF rapor (read-model)
- WhatsApp/SMS hatırlatma (premium)

**Önkoşul:** Faz 0 read-model (raporlama); aktif kullanıcı kitlesi.
**Çıktı:** Gelir modeli (üyelik + reklam) ve gelişmiş analitik devrede.

---

## Süregelen — Hijyen & Dokümantasyon
`Raporlama ve Dokümantasyon Hijyeni` epic: build artefakt/`.gitignore` hijyeni, placeholder dosya temizliği, doküman güncelliği (CLAUDE.md kuralı). Her faz boyunca işletilir.

---

## Bağımlılık Haritası (özet)

```
Faz 0 (sertleştirme + dosya depolama + read-model + push)
   ├─► Faz 1 (öğretmen: kaynak/yükleme→dosya depolama; bildirim→push)
   ├─► Faz 2 (öğrenci: ödev yükleme→dosya depolama; bildirim→push)
   ├─► Faz 3 (veli paneli, raporlama→read-model; Study verisi→Faz 2)
   ├─► Faz 4 (eşleştirme arama→read-model; yorum→tamamlanmış ders=Faz 1)
   └─► Faz 5 (raporlama→read-model; premium→tüm modüllerde entitlement)
```

## İlişkili Dokümanlar
- Ürün: [`ozel_ders_platformu_PRD_v2.md`](ozel_ders_platformu_PRD.md) · Roller: [`roles/`](roles/00_roller_genel_bakis.md) · Modüller: [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md)
- Mimari öncelikler: [`modules/mimari_inceleme.md`](modules/mimari_inceleme.md) · Backlog: [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv)

---

*Geliştirme Yol Haritası | Güncelleme: 2026-06-24*
