---
title: "Öğretmen Rolü"
summary: "Öğretmenin günlük operasyon aracı — öğrenci/takvim/ders/ödev/ödeme tek uygulamada yönetilir"
tags: [rol, ogretmen, gunluk-operasyon]
status: "🟢"
authority: derived
updated: 2026-11-15
---

# 👨‍🏫 Öğretmen Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 1️⃣ (ilk geliştirilen rol)** · **Faz 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Büyük ölçüde yazıldı**
>
> **Amaç:** Öğretmenin uygulamayı **her gün** kullandığı bir günlük operasyon aracı olmak — öğrencilerini,
> derslerini, ödevlerini, notlarını/kaynaklarını, tatillerini ve ödemelerini **tek takvim** etrafında yönetir.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogrenci.md`](ogrenci.md) · [`veli.md`](veli.md) · modül indeksi [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) · fonksiyonel doküman (arşiv) [`../_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md`](../_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md)
> **Güncelleme:** 2026-11-15


**Ders hatırlatması (P17 T6):** Gelecek 7 günün her aktif oluşumu için öğretmen, bağlı öğrenci hesabı ve ders tercihi açık onaylı velilere ayrı push planlanır. Genel push/ders tercihi gönderimde uygulanır; veli onayı yeniden denetlenir. İptal/silme eski hatırlatmayı kapatır, erteleme saati/metni yeniler. Başlayan ders veya 30 dakikadan fazla gecikmiş uyarı gönderilmez. Gerçek cihaz FCM doğrulaması Q8/P25 T5'tedir.

---

> **P17 Task 3 (2026-11-15):** Haftalık dersin her oluşumunu ayrı başlatabilir; aynı oluşuma çift dokunuş tek oturum üretir. Tamamlanmış oturumun not/konu düzeltmesi yeni tamamlama olayı yaymaz ve ilk tamamlanma damgasını korur. `Absent` gelişim katılımına eklenmez. **P17 T4 / K-21:** Tamamlama yalnız LessonSessions’tan yapılır; ders kartı ve detay ekranı orijinal oluşumun oturumunu türetip tamamlar. Takvim olayı tüketince yalnız o hafta Completed olur. Tamamlanmış oluşumu içeren iptal/erteleme ve tüm seri güncellemesi 409 verir; gelecekten başlayan kapsam geçmişi korur.

### Ders iptali (P17 T5)

Öğretmen ders detayından tekrarlı derste “Yalnız bu ders / Bu ve sonrakiler / Tüm seri” kapsamını seçer; iptal nedeni ve isteğe bağlı açıklama girer, “Ücretsiz iptal et” ile politika ücretinden vazgeçebilir. Politika profil API’sinden NoticeHours/ChargePercent olarak ayarlanır (varsayılan kapalı; mobil ayar formu bu görevde eklenmedi). Öğrencinin geç iptali anlaşılmış ders ücreti üzerinden borç üretir; erken/öğretmen kaynaklı/ücretsiz iptalde üretmez. Bekleyen oturum, normal ücret ve hatırlatma outbox ile kapanır; tahsil edilmiş para korunur. Gelecekteki ders tamamlanamaz.


## 1. Tasarım İlkesi

Türkiye'de özel ders veren öğretmenler bugün dersi **zihinde/Excel'de**, iletişimi **telefon/WhatsApp'ta**, ödevi **sözlü/PDF** ile, ödemeyi **elden** yönetir; konu/ödev/gelişim takibi sistematik değildir. Bu rol, tüm bu dağınık işi **tek uygulamada** toplar.

> Öğretmen, "öğrenci bulduğu bir pazar yeri"ne değil, "derslerini düzenli yönettiği bir günlük araca" bağlanır. **Eşleştirme (M12) en son gelir;** önce yönetim tarafı eksiksiz çalışmalıdır (PRD §10.1).

**Takvim merkezdedir:** dersler, ödevler, tatiller, ödemeler ve notlar takvimden görülebilmeli ve yönetilebilmeli. İleride (eşleştirme açılınca) öğrenciler de öğretmenin takvimini takip edebilecek şekilde tasarlanır.

---

## 2. Kullanıcı Tipi ve Giriş

- Kayıt: `Teacher` rolüyle (ad, soyad, telefon, e-posta, şifre) — [`../modules/m01_identity.md`](../modules/m01_identity.md).
- Tek profil: bir kullanıcının **yalnızca bir** öğretmen profili olabilir.

---

## 3. Yetenek Haritası (Hangi Modülleri Kullanır?)

"Öğretmen rolü" tek bir teknik modül değil; günlük iş akışını oluşturan **modüllerin birleşimidir**:

| Adım | Yetenek | Modül | PRD | Durum |
|------|---------|-------|-----|-------|
| 0 | Giriş / kayıt / rol | [`m01_identity`](../modules/m01_identity.md) | M01 | 🟢 |
| 1 | Öğretmen profili (branş, şehir, ücret, uygunluk) | [`m02_teachers`](../modules/m02_teachers.md) | M02 | 🟢 (çoklu branş + sertifika ✅ Dilim D; profil fotoğrafı ✅ P04; **aramada görünürlük açma/kapama** ✅ P06/M02-4) |
| 2 | Öğrenci ekle & listele + **branş ekle/sil** + **kayıtlı veliyi bağla** | [`m03_students`](../modules/m03_students.md) | M03 | 🟢 (branş yönetimi + veli bağlama ✅ P06/M03-1+M03-2; veli bağı **onay bekler**; silme/arşiv/davet ⚠️ — bkz. §11) |
| 3 | Takvimde ders planla (tek/tekrarlı, online/yüz yüze + link) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (Dilim A tamam: link+tatil+erteleme+iptal-nedeni/sil+occurrence-kapsamı, 2026-07-18) |
| 3.1 | Öğrenci erteleme taleplerini görüp kabul (mevcut erteleme çalışır) / red et | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (backend Ö-F, 2026-07-18 — `GET /teachers/{id}/lesson-requests` + `/accept` + `/reject`; mobil ⚠️) |
| 4 | Dersi işle/tamamla, katılım & not | [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md) | M05 | 🟢 (gelmedi→ücretlendirme ✅ Dilim A; not görünürlüğü ✅ Dilim B) |
| 5 | Ders notu + **kaynak** + ödev ver/takip/**puanla** | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (onay/geri gönder + geri bildirim + not görünürlüğü ✅ Dilim B; kaynak paylaşımı + öğrenci yükleme ✅ P04; **0–100 puanlama + son teslim uyarısı ✅ P06/M06-3+M06-4**) |
| 6 | Ödeme/bakiye takibi (manuel) + **otomatik ders ücreti + gecikme uyarısı** + veli paylaşımı | [`m07_payments`](../modules/m07_payments.md) | M07 | 🟢 (M07-1/M07-2 2026-09-04; veli paylaşımı ⚠️) |
| 7 | Yaklaşan ders + ödev/ödeme hatırlatması | [`m11_notifications`](../modules/m11_notifications.md) | M11 | 🟢 (ders hatırlatma + geciken ödeme + ödev son teslim uyarısı) |
| 8 | Gelir istatistik & rapor + **PDF öğrenci raporu** | [`m14_reporting`](../modules/m14_reporting.md) | M14 | 🟢 API + mobil (`/teacher/reports`: ay seçici, ders/tahsilat özeti, boş zamanlar · `/student/reports`: öğrenci analizi + PDF üret/indir/aç; premium kapısı 402/403 → kilit kartı) → [`../pages/teacher_reports.md`](../pages/teacher_reports.md) |
| 9 | Öğrenci gelişim takibi | [`m10_progress_tracking`](../modules/m10_progress_tracking.md) | M10 | 🟢 API + mobil (`GET /teachers/{id}/students/{id}/overview`, gizlilik filtreli; `ProgressOverviewPage` öğretmen kapsamı) → [`../pages/progress_overview.md`](../pages/progress_overview.md) · ⚠️ snapshot çizgi grafiği öğretmene **kapalı** (uç öğrenci+Admin'e sınırlı) |
| 10 | Öğrenci/veli ile mesajlaşma | [`m16_messaging`](../modules/m16_messaging.md) | M16 | 🟡 (P10: gelen kutusu + sohbet + engelleme + yeni mesaj bildirimi kodda; **şikayet** P12, gerçek zamanlı v2. **P15 T5 / K-28:** öğrenci arşivlenince ya da veli bağı bitince sohbet **salt-okunur** — gönderim `403 messaging.relationship_ended`, geçmiş okunur; arşivden çıkarınca yeniden açılır) |
| 11 | İlan verme + puanlama/yorum alma | [`m12_matching`](../modules/m12_matching.md) / [`m13_reviews`](../modules/m13_reviews.md) | M12/M13 | 🔴 |
| 12 | Profil & bildirim/üyelik ayarları | [`m15_settings`](../modules/m15_settings.md) / [`m17_membership`](../modules/m17_membership.md) | M15/M17 | 🟡/🔴 |

---

## 4. Altın Akış (Golden Path)

```
Kayıt (Teacher) → Profil doldur (branş, şehir, ücret, uygunluk saatleri)
  → Öğrenci ekle (bağlı hesap VEYA manuel)
    → Takvime ders ekle (tek/tekrarlı, online→link / yüz yüze)
      → [ders günü] Push hatırlatma
        → Dersi tamamla (süre, konu, katılım, not)
          → Ders notu + kaynak paylaş + ödev ver
            → Ödevi takip et (öğrenci yükledi mi?)
              → Ödemeyi işaretle (tahsil edildi / bekliyor) → veliyle paylaş
                → Aylık gelir özeti + öğrenci gelişimi
```

---

## 5. Rol-Özel İş Kuralları

1. **Öğrenci ekleme — iki yol** (promp): (a) öğrenci uygulamayı kullanıyorsa **gerçek hesaba bağlanır**; (b) öğrenci reddederse öğretmen **manuel öğrenci** ekler (`Origin=TeacherManaged`, `CreatedByTeacherUserId` set). Manuel öğrenci sonradan gerçek hesaba bağlanabilir (davet/eşleşme — ⚠️ planlanan).
2. **Veli gerçek kişi:** Öğrenci manuel olabilir, ama velisi yalnızca **gerçek kayıtlı kullanıcı** olabilir (M09).
3. **Online ders linki:** Ders online ise öğretmen bir **bağlantı (MeetingUrl)** girer; öğrenciler bu linkle derse katılır (✅ ayrı `MeetingUrl` alanı — Dilim A, 2026-07-18).
4. **Tekrarlı ders:** Ders tek seferlik veya tekrarlı planlanabilir (`RecurrenceRule`); takvim her haftayı ayrı oluşum olarak gösterir (M04-1). **P17 T1 (2026-11-15):** tekrarlı derste iptal/erteleme **kapsam ister** — "Yalnız bu ders / Bu ve sonrakiler / Tüm seri" (kapsamsız istek `400 scheduling.scope_required`; eskiden "bu haftanın iptali" bütün seriyi iptal ediyordu). Öğrencinin erteleme talebini kabul etmek yalnız o haftayı taşır. Mobilde seçim sayfası hazır, ekran girişi henüz yok (iptal P17 T5, erteleme P21 T4).
5. **Çakışma kontrolü:** Aynı öğretmende zaman çakışan ders **engellenir** (`scheduling.teacher_conflict` — koddan doğrulandı 🟢). **P16 T4 (2026-11-14):** kontrol tekrarlı derslerin **her oluşumuna** bakar (Eylül'de başlamış haftalık Salı dersi Kasım'ın Salı'sını da korur; tekrarlı yeni ders 12 hafta ileriye kadar denetlenir), iptal edilen oluşum saati boşaltır, ertelenen oluşum yeni saatini tutar; aynı saate aynı anda gelen iki istekten yalnız biri geçer. **Tatil günü:** ders öğretmenin tatil / müsait değil bloğuna düşerse `409 scheduling.time_off_conflict` — öğretmen bilerek isterse `allowTimeOffOverride` ile yine ekler/erteler (mobil bu seçeneği henüz sunmuyor, P21). Tatil oluştururken o aralıkta kalan dersler (tekrarlı dersin oluşumları ve tatilden önce başlayıp içine taşan dersler dahil) listelenir.
6. **Ders tamamlama:** Süre, gerçek başlangıç/bitişten **otomatik** hesaplanır; manuel girilmez.
7. **Ödeme manuel:** Para sistem üzerinden **alınmaz**; öğretmen "X dersinin ödemesini aldım" şeklinde elle işaretler. Ödeme bilgisi veliyle **paylaşılabilir** (`IsSharedWithParent` ⚠️).
   **Otomasyon (2026-09-04):** *kayıt açma* ve *gecikme uyarısı* otomatiktir — ücretli ders tamamlanınca vadesi 7 gün sonra olan bir `LessonFee` kaydı açılır (M07-2), vadesi geçen kayıt saatlik tarama ile `Overdue` olur ve öğretmene bildirim düşer (M07-1). **Tahsilat işaretlemesi hâlâ elle yapılır.**
8. **Doğrulama rozeti:** Öğretmen kendini "doğrulanmış" yapamaz; yalnızca admin (güvenlik — bkz. [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) Y1).
9. **Sahiplik:** Öğretmen yalnızca **kendi** eklediği/kendisine bağlı öğrencileri görebilir.

### 5.1 Yetki Sınırları — Öğretmen Neyi Yapamaz

Rol, sınırlarıyla birlikte anlamlıdır. Aşağıdaki kısıtlar iş kuralı olarak zorlanır (arşivlenen fonksiyonel dokümandan taşındı, kanonik gerçeklere göre düzeltildi):

| # | Öğretmen ŞUNU YAPAMAZ | Neden |
|---|---|---|
| 1 | Öğrencinin bireysel çalışma sayacını başlatamaz/durduramaz | Veri öğrenciye ait (M08) |
| 2 | Öğrencinin gizlediği/paylaşmadığı çalışma verisini göremez | Paylaşım kontrolü (M15 `ShareStudyDataWith*`) |
| 3 | Öğrencinin test/deneme kayıtlarını değiştiremez | Öğrenci verisi (M08) |
| 4 | Aldığı olumsuz yorumu silemez/gizleyemez — yalnızca yanıtlar | M13 |
| 5 | Puanını değiştiremez, yorum yapacak öğrenciyi seçemez | M13 (yalnız ders alan öğrenci yorumlar) |
| 6 | Platform üzerinden para tahsil edemez | M07 (yalnız manuel kayıt/mutabakat) |
| 7 | Başka öğretmenin öğrencisini göremez | Sahiplik/rol izolasyonu (M03) |
| 8 | Kendini "doğrulanmış" yapamaz | Yalnız admin — `PUT /profiles/{userId}/verification` (mimari_inceleme Y1) |
| 8.1 | Başka bir öğretmenin profilini aramada gizleyemez/açamaz | `TeacherActivationCommandAuthorizer` — yalnız sahibi veya Admin (M02-4) |
| 9 | Free planda 5 aktif bağ üstünde öğrenci ekleyemez | `StudentLimits.FreeStudentLimit=5` (M03/M17) |
| 10 | Rolünü değiştiremez | Identity |
| 11 | Öğrencinin hesabını silemez — yalnız bağlantıyı arşivler/keser | M03 (`TeacherStudentLink`) |
| 11.1 | **Bağı olmayan / bağını arşivlediği öğrenciye** ders, ödeme kaydı, manuel oturum, veli daveti ve **veli bağlama talebi (`link-parent`, P15 T9)** **yazamaz**; arşivli öğrencinin eski borcunu da arşivden çıkarmadan güncelleyemez; arşivli bağda **otomatik ders ücreti oluşmaz** (P15 T9). Arşivden önce açılmış kayıtları kapatabilir: eski derslerini iptal edip silebilir, planlı dersten oturum türetip tamamlayabilir, ödev takibi/puanlama yapabilir, kayıtlarını okuyabilir | `ITeacherStudentAccess` → `403` (P15 T2 / N-08, N-21; T9 — [`m03`](../modules/m03_students.md) kural 21; [`backend.md`](../architecture/backend.md) §8.1) |
| 12 | Tamamlanmış dersin tarih/saatini değiştiremez — yalnız not/konu düzeltir | M05 |
| 13 | Admin işlemleri (moderasyon, doğrulama vb.) yapamaz | M01 |

---

## 6. Mobil Ekranlar

**Mevcut ✅** (bkz. [`../pages/00_pages_index.md`](../pages/00_pages_index.md)):
`/dashboard` (öğretmen paneli), `/teacher-profile`, `/students` + `/students/:id`, `/scheduling` (takvim), `/lesson-sessions` + detay/not, `/assignments/...`, `/payments` + `/payments/new`, `/more`, `/account-info`.

**Planlanan ⚠️:**
- Dashboard zenginleştirme (bugünkü dersler, bekleyen ödev, geciken ödeme — tek özet endpoint). *Geciken ödeme artık bildirim merkezine düşüyor (M07-1); dashboard rozeti ayrı iş.*
- ~~Mesajlaşma ekranı (öğrenci/veli).~~ ✅ **P10 Task 4** — `/more` → "Mesajlar" ve öğrenci detayında "Mesaj Gonder"; sohbet `/messages/:conversationId`. Kalan: mesaj **şikayeti** (P12), karşı tarafın adı (sunucu döndürmüyor), gerçek zamanlı teslimat (K-07 ile v2).
- Gelir/öğrenci gelişim raporları, ilan verme, üyelik/paywall.

---

## 7. Bireysel vs Eşleşmiş Kullanım
- **Bireysel (Faz 1):** Öğretmen kendi (manuel dahil) öğrencilerini ve derslerini yönetir — eşleştirme gerekmez.
- **Eşleşmiş (Faz 4):** İlan verir, öğrenciler keşfeder; eşleşince öğrenci-öğretmen ilişkisi kurulur, ders öğrencinin programına otomatik eklenir; dersi tamamlanan öğrenci öğretmeni puanlayabilir.

## 8. Üyelik Etkisi (Free/Premium)
Premium öğretmen: reklamsız, **sınırsız öğrenci**, gelir analizi, PDF öğrenci raporu, boş zaman analizi, profil öne çıkarma, WhatsApp/SMS hatırlatma. Free: öğrenci limiti + reklam (bkz. [`../modules/m17_membership.md`](../modules/m17_membership.md), PRD §9.1).

> **Yeni (P09 Task 3, 2026-09-04 — backend, mobil arayüz yok):** Kullanıcı **ilk ay ücretsiz** denemesini
> `POST /api/membership/subscriptions/start-trial` ile başlatır (**kullanıcı başına bir kez** — iptal, hakkı geri vermez),
> `POST /api/membership/subscriptions/cancel` ile iptal eder (**erişim dönem sonuna kadar sürer**), referans kodunu
> `GET /api/membership/referral-code` ile alır ve `POST /api/membership/referral/redeem` ile kullanır — davet edene **ve**
> gelene **1 ay premium** yazılır (ödül mevcut dönemi ezmez, uzatır). Fiyat listesi hâlâ hipotez (**Q2**); paywall ekranı Task 6'da geldi.

> **Yeni (P09 Task 6, 2026-09-04 — mobil arayüz geldi):** `/more → "Uyelik"` → **Üyelik** ekranı (`/membership`)
> mevcut planı, dönem sonunu, iptali ve referans kodunu gösterir; **Paywall** (`/membership/paywall`)
> rol bazlı Free/Premium karşılaştırmasını, plan kartlarını ve ücretsiz denemeyi taşır. Premium'a özel
> alanlar ortak `PremiumLock` ile kilitlenir ve kilit kartı paywall'a götürür.
> ⚠️ **Satın alma düğmesi pasiftir** — ödeme entegrasyonu P09 Task 4 (Q1 bloke).

> **Kapanış (P09 Task 7, 2026-09-04 — kısmi):** Öğretmenin premium yolu **gerçek Postgres'te uçtan uca
> doğrulandı**: Free öğretmen 6. öğrenciyi ekleyemiyor (`409 students.free_limit_reached`) → paywall'dan
> deneme başlatınca sınır kalkıyor → deneme dönemi bitince limit geri geliyor.
> ⚠️ **Öğretmenin bugün premium olmasının tek yolu ücretsiz deneme, referans ödülü veya admin köprüsüdür**;
> **satın alma yok** (Task 4 / Q1) ve **reklamsızlık uygulanmıyor** çünkü reklam altyapısı hiç yok (Task 5 / Q3).
> PRD §9.1'deki gelir analizi, profil öne çıkarma ve WhatsApp/SMS hatırlatma da hâlâ uygulanmadı.


## 9. Kabul Kriterleri (Faz 1)
- [x] Teacher rolüyle kayıt/giriş; profil oluştur/düzenle.
- [x] Manuel öğrenci ekleme + listeleme.
- [x] Takvime ders ekleme (tek + tekrar alanı) + çakışma engeli.
- [x] Ders oturumu tamamlama (konu/süre/katılım/not).
- [x] Ders sonrası not + ödev; manuel ödeme + gelir özeti + geciken filtre.
- [ ] Push bildirim uçtan uca (FCM teslimatı — M11 eksik).
- [x] Kaynak paylaşımı; öğrenci ödev yükleme görünümü; ödev puanlama + son teslim uyarısı.
- [ ] Online ders linki.
- [ ] 5–10 gerçek öğretmenle beta test (PRD önerisi).

## 10. Denetim — Kod Gerçeği vs. Fonksiyonel Doküman (2026-07-18)

Kaynak: [`../_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md`](../_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md) (arşiv) §15 boşluk listesi, gerçek koda (domain modeli + endpoint envanteri) karşı denetlendi. Faz 1 çekirdeği (M02–M07) domain+API+mobil olarak **çalışıyor**; ancak fonksiyonel dokümanın **[YENİ]** işaretli iş kurallarının çoğu koda henüz girmemiştir.

### 10.1 Kritik/Yüksek boşluklar (Faz 1)

| Kod | Boşluk | Kod gerçeği | Etki |
|-----|--------|-------------|------|
| B-01 | **Tatil / müsait değil bloğu** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `TimeOffBlock` aggregate + `POST/GET/DELETE /teachers/{id}/time-off`; oluşturmada çakışan dersler yanıtta | — |
| B-02 | **Ders erteleme** ayrı aksiyon | ✅ **yapıldı (Dilim A, 2026-07-18)** — `Reschedule()` + `POST /lessons/{id}/reschedule`; statü Planned kalır, `OriginalStartAtUtc`/`RescheduleNote` geçmişi, Rescheduled event | — |
| B-03 | **Tekrar eden ders occurrence yönetimi** (bu/bu+sonraki/tümü) | ✅ **yapıldı (Dilim A, 2026-07-18)** — `LessonOccurrenceException` + `RecurrenceExpander` istisna overload'u; cancel/reschedule `Scope=Single/ThisAndFuture/All` | — |
| B-05 | **Not görünürlüğü** (özel/öğrenci/veli) | ✅ **yapıldı (Dilim B, 2026-07-18)** — `LessonNote.Visibility` enum (`Private`/`Student`/`StudentAndParent`); follow-up isteğinde seçilir, response'ta döner | — |
| T-06.9 | **Ödev puanlama (0–100)** | ✅ **yapıldı (P06/M06-4, 2026-09-04)** — `Assignment.Grade(score, feedback?, now)` yalnız `Approved` ödevde; `POST /assignments/{id}/grade`; mobilde ödev kartına dokununca değerlendirme alt sayfası (puan kaydırıcısı + geri bildirim), ödev onaylı değilse önce `approve` çağrılır | — |
| T-06.7/8 | **Ödev onay / geri gönder + geri bildirim** | ✅ **yapıldı (Dilim B, 2026-07-18)** — `Assignment.Approve()`/`ReturnForRevision()` + `Approved`/`ReturnedForRevision` statüleri + `TeacherFeedback`; `POST /assignments/{id}/approve`, `POST /assignments/{id}/return` (öğretmen sahiplik yetkisi); geri gönderilen ödev yeniden teslimde `InProgress`'e döner | — |
| B-07 | **Öğrenci bazlı ücret** override | ✅ **yapıldı (Dilim C, 2026-07-18)** — `TeacherStudentLink.AgreedRateAmount`/`Currency` + `PUT /teachers/{id}/students/{sid}/rate`; ücret öğrenci-öğretmen ikilisine özel | — |
| B-08 | **Gelmedi + ücretlendirme kararı** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `LessonSession.IsChargeable` + complete akışı. **2026-09-04 (M07-2):** artık otomatik ödeme kaydı da üretir — `IsChargeable=true` tamamlanan ders, B-07 ücretiyle `LessonFee` kaydı açar (ücret girilmemişse kayıt açılmaz) | — |
| B-09 | **İptal nedeni + ücretlendirme + Sil ayrımı** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `Cancel(reason, isChargeable, …)` + `CancellationReason` enum + `DELETE /lessons/{id}` (24s+gelecek kuralı) | — |
| B-04 | **Öğrenci arşivleme + Free limit** | ✅ **yapıldı (Dilim C, 2026-07-18)** — `TeacherStudentLink.IsArchived` + `POST .../archive\|unarchive` + liste `?includeArchived=`; Free limit=5 (`students.free_limit_reached`), arşivli link limite dâhil, premium bypass yok (`// TODO(M17)`) | — |
| B-06 | **Öğrenci-öğretmen davet/bağlanma** | ✅ **yapıldı (Dilim C, 2026-07-18)** — `Invite` → `Accept`/`Reject` (`POST .../invite`, `POST /links/{linkId}/accept\|reject`); kabul eden `currentUser` profile `LinkUser` ile bağlanır. Identity'de e-posta araması yapılmaz (StudentId deseni). **P15 T3 (N-22, N-23):** öğretmen profili açarken onu bir öğrenci hesabına **bağlayamaz** (`userId`/`parentUserId` gövdede → `400 students.field_not_allowed`) — hesap yalnız davetle bağlanır; kabul/ret davet kodunu ister; zaten bağlı (`Linked`) öğrenciye yeniden davet `409`. Öğrencinin kendi profili varsa birleşmede öğretmenin bağı ücretiyle kanonik profile taşınır, öğretmen o profili açıp ders/ödeme yazabilir | — |
| B-10 | **Online link semantiği** | ✅ **yapıldı (Dilim A, 2026-07-18)** — ayrı `MeetingUrl` alanı; `LocationLabel` yüz yüze adresi için | — |

### 10.2 Yanlış yapılandırma (sadece eksik değil — düzeltme gerekir)

1. ✅ **M02 branş tekilliği + sertifika (Dilim D, 2026-07-18):** `TeacherSubject` çoklu branş koleksiyonu + `TeacherCertificate` (T-02.12) eklendi; birincil `TeacherProfile.Subject` korunur (domain event + eşleştirme kırılmadı), mevcut profiller migration backfill'i ile `teacher_subjects`'e taşındı. Upsert `Subjects`/`Certificates` listeleri taşır.
2. ✅ **Erteleme = düzenleme (Dilim A, 2026-07-18):** ayrı `Reschedule()` domain metodu + `POST /lessons/{id}/reschedule` + `OriginalStartAtUtc`/`RescheduleNote` erteleme geçmişi; statü Planned kalır (kayıtlı taşıma).
3. ✅ **İptal veri modeli (Dilim A, 2026-07-18):** `CancellationReason` enum + `IsChargeable` eklendi.
4. ✅ **Tekrar eden ders sanal model (Dilim A, 2026-07-18):** `LessonOccurrenceException` tablosu + `RecurrenceExpander` istisna overload'u ile B-03 çözüldü.

### 10.3 Önceliklendirilmiş düzeltme sırası

- **Öncelik 1 (Faz 1'i kullanılabilir yapan):** ✅ B-03 (occurrence yönetimi), ✅ B-01 (tatil bloğu), ✅ B-02 (erteleme) — **Dilim A tamam**; ✅ B-05 (not görünürlüğü) + ✅ T-06.7/8 (ödev onay/geri gönder) — **Dilim B tamam (2026-07-18)**.
- **Öncelik 2 (yüksek etkili):** ✅ B-07 (öğrenci bazlı ücret) — **Dilim C tamam (2026-07-18)**, ✅ B-08 (gelmedi→ücretlendirme), ✅ B-09 (iptal nedeni/sil) — **Dilim A tamam**; ✅ B-04 (arşivleme + free limit) — **Dilim C tamam**.
- **Öncelik 3 (olgunluk):** ✅ M02 çoklu branş + sertifika (Dilim D tamam, 2026-07-18), ✅ B-06 (öğrenci davet) — **Dilim C tamam (2026-07-18)**, ✅ B-10 (online link) — **Dilim A tamam**.

### 10.4 Karar bekleyen sorular (veri modelini etkiler) — Dilim C kararları
1. Bir öğrenci **birden fazla öğretmene** bağlanabilir mi? → ✅ **Evet** (`TeacherStudentLink` bağlantı tablosu, `(TeacherUserId,StudentId)` benzersiz).
2. Free limit **aktif** öğrenci mi, toplam mı? → ✅ **Toplam** (reddedilmiş hariç); arşivli linkler limite dâhil (arşiv kotayı boşaltmaz).
3. Öğrenci limiti kesin sayı: **5 mi 10 mu?** → ✅ **5** (`StudentLimits.FreeStudentLimit`); premium sınırsız M17'de (`// TODO(M17)`).
4. Erken geri bildirim Faz 4'te herkese açılacak mı? (retroaktif yayın onayı) — açık.

---

## 11. İlişkili Dokümanlar
- Öğrenci tarafı → [`ogrenci.md`](ogrenci.md) · Veli → [`veli.md`](veli.md)
- Teknik modüller → [`../modules/m02_teachers.md`](../modules/m02_teachers.md), [`m04_scheduling`](../modules/m04_scheduling.md), [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md), [`m06_assignments`](../modules/m06_assignments.md), [`m07_payments`](../modules/m07_payments.md)
- Mimari açıklar → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)

---

*Öğretmen Rolü — Detaylı Tasarım | Güncelleme: 2026-11-15 (P17 T3: oluşum anahtarlı ders oturumu, öğrenci/veli okuması ve çift katılım koruması) · 2026-11-15 (P17 Task 1 / N-01: kural 4 — tekrarlı derste iptal/erteleme kapsamı zorunlu, talep kabulü yalnız o haftayı taşır) · 2026-11-14 (P16 Task 4 / N-07: kural 5 — çakışma tekrarlı dersin her oluşumuna bakar, eşzamanlı istekte tek kazanan; tatil bloğuna ders `time_off_conflict`, bilerek `allowTimeOffOverride`; tatil oluştururken oluşumlar listelenir) · 2026-11-14 (P15 Task 9 / #185: kısıt 11.1 — arşivli bağda `link-parent` 403 ve otomatik ders ücreti yok; eski kaydı kapatmak serbest) · 2026-11-14 (P15 Task 5 / N-46, K-28: yetenek 10 — bağ arşivlenince/veli bağı bitince sohbet salt-okunur, gönderim 403; birleşen öğrencinin sohbeti kanonik profile taşınır) · 2026-11-14 (P15 Task 3 / N-22, N-23, N-28: B-06 — profil açarken öğrenci hesabına bağlanamaz, hesap yalnız davetle; `Linked` öğrenciye yeniden davet 409; birleşmede bağ ücretiyle kanonik profile taşınır) · 2026-11-13 (P15 Task 2 / N-08, N-21: rol kısıtı 11.1 — bağı olmayan/arşivlenmiş öğrenciye ders, ödeme, manuel oturum, veli daveti yazılamaz; iptal/silme/okuma serbest) · 2026-09-04 (**P10 Task 4 / M16-1 + D-08**: öğretmen artık öğrenci ve velileriyle uygulama içinde mesajlaşıyor — `/more` → "Mesajlar" gelen kutusu, öğrenci detayında "Mesaj Gonder", sohbette engelleme ve yumuşak silme; yeni mesajda karşı tarafa bildirim. Şikayet/moderasyon (M18) P12'ye kaldı. Önceki not — **P09 Task 7 — kısmi kapanış**: Free limit → deneme → sınırsız öğrenci → dönem sonu → yeniden limit zinciri gerçek Postgres'te uçtan uca doğrulandı; satın alma (Q1) ve reklam (Q3) hâlâ yok. Önceki not — P09 Task 6 / D-10+D-15: üyelik ekranı ve paywall **mobilde**; `/more` "Uyelik" satırı aktif, "Plus üye" rozeti gerçek üyelikten geliyor, PDF rapor kilidi paywall'a bağlandı. Satın alma hâlâ pasif (Task 4/Q1). Önceki not — P09 Task 3 / M17-3: ücretsiz deneme (kullanıcı başına bir kez), abonelik iptali (erişim dönem sonuna kadar) ve referans kampanyası (iki tarafa 1 ay) **backend'de** — mobil arayüz Task 6'da. Önceki not — P08 kapanış / Task 5+6: rapor ve gelişim yetenekleri **mobilde de** kullanılabilir — `/more` → "Raporlar" satırı aktif, `/teacher/reports` aylık özet + boş zaman, öğrenci analizinden PDF üretilip cihazda açılıyor; M10 ve M14 🟢. Önceki not — P08 Task 4 / M14-4: bağlı öğrencisi için PDF gelişim raporu üretip indirebilir (`POST /api/reporting/students/{id}/report` → `GET /api/reporting/reports/{id}/file`); premium kapısı geçici olarak "taraflardan biri Premium" kuralıyla çalışır, öğrenci gizlilik seviyesi raporu kısar veya reddeder) · 2026-09-04 (P08 Task 3 / M14-1 + M14-5: öğretmen aylık ders/tahsilat özetini (`GET /api/reporting/teachers/{id}/monthly`) ve boş zaman analizini (`.../free-slots`) okuyabilir; ayrıca bağlı öğrencisinin çalışma/performans analizine gizlilik kapısıyla erişir. Mobil ekranlar P08 Task 5) · 2026-09-04 (P08 Task 2 / M10-2 + M10-4: öğretmen **bağlı** olduğu öğrencinin gelişim özetini okuyabilir (`GET /api/progress-tracking/teachers/{teacherUserId}/students/{studentId}/overview`); bağ yoksa 403, öğrenci gizliliği kapalıysa/`Hidden` ise boş özet, `Limited` ise yalnız sayaçlar. Tamamlanan ders artık ilgili konunun hâkimiyetini besler) · 2026-09-04 (**P06 Task 10 — öğretmen MVP kapanışı**: günlük akış gerçek Postgres + çalışan API üzerinde uçtan uca doğrulandı (41/41 adım): kayıt → profil+uygunluk → öğrenci ekle → davet/claim → haftalık tekrarlı ders → oturum başlat/tamamla → ders notu+ödev → 0–100 puanlama → otomatik ders ücreti → vade taraması → in-app bildirim. Doğrulama 4 gerçek hata ortaya çıkardı, dördü de düzeltildi) · 2026-09-04 (P06 Task 9 / M03-1 + M03-2: öğretmen öğrenci detayından branş ekleyip silebilir ve **kayıtlı** bir veli kullanıcısını öğrenciye bağlayabilir — bağ onay bekler, onaylanınca veli çocuğun gelişimini görür) · 2026-09-04 (P06 Task 7 / M06-3 + M06-4: ödevi 0–100 puanlama (`POST /assignments/{id}/grade`, yalnız onaylı ödev) + saatlik son teslim taraması — teslime 24 saat kala ve vade geçince öğrenciye/veliye uyarı; mobilde ödev kartından değerlendirme alt sayfası) · 2026-09-04 (P06 Task 5 / M07-1 + M07-2: ücretli ders tamamlanınca otomatik `LessonFee` kaydı (vade +7 gün, ücret B-07'den) + vadesi geçen ödemede saatlik tarama → `Overdue` + öğretmene/Premium veliye bildirim; tahsilat işaretlemesi elle kalır) · 2026-09-04 (P06/M02-3 + M02-4: öğretmen filtreli aramada listelenir; öğretmen kendi profilini `POST /profiles/{userId}/deactivate` ile aramadan çekebilir — veri silinmez) · 2026-08-19 (doküman temizliği: §5.1 "Neyi Yapamaz" yetki sınırları matrisi arşivlenen fonksiyonel dokümandan taşındı; fonksiyonel doküman `_arsiv/`'e taşındı, linkler güncellendi) · 2026-07-18 (Dilim B: not görünürlüğü B-05 + ödev onay/geri gönder T-06.7/8; Dilim C: çoklu öğretmen bağı + free limit=5 + arşiv B-04, öğrenci bazlı ücret B-07, davet/kabul B-06; Dilim D: M02 çoklu branş + sertifika)*

*Güncelleme: 2026-11-15 — P17 Task 4 / N-03, K-21: tek oturum tamamlama yolu ve oluşum durum senkronizasyonu.*

*Güncelleme: 2026-11-15 — P17 T6: oluşum/alıcı anahtarlı hatırlatma, kayan 7 gün, kaynak uzlaştırması ve süre/tercih kapıları.*
