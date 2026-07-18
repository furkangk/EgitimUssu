# 👨‍🏫 Öğretmen Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 1️⃣ (ilk geliştirilen rol)** · **Faz 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Büyük ölçüde yazıldı**
>
> **Amaç:** Öğretmenin uygulamayı **her gün** kullandığı bir günlük operasyon aracı olmak — öğrencilerini,
> derslerini, ödevlerini, notlarını/kaynaklarını, tatillerini ve ödemelerini **tek takvim** etrafında yönetir.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogrenci.md`](ogrenci.md) · [`veli.md`](veli.md) · modül indeksi [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) · fonksiyonel doküman [`../ogretmen_rolu_fonksiyonel_dokuman_v1.md`](../ogretmen_rolu_fonksiyonel_dokuman_v1.md)
> **Güncelleme:** 2026-07-18

---

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
| 1 | Öğretmen profili (branş, şehir, ücret, uygunluk) | [`m02_teachers`](../modules/m02_teachers.md) | M02 | 🟢 (çoklu branş + sertifika ✅ Dilim D) |
| 2 | Öğrenci ekle & listele | [`m03_students`](../modules/m03_students.md) | M03 | 🟢 (silme/arşiv/davet ⚠️ — bkz. §11) |
| 3 | Takvimde ders planla (tek/tekrarlı, online/yüz yüze + link) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (Dilim A tamam: link+tatil+erteleme+iptal-nedeni/sil+occurrence-kapsamı, 2026-07-18) |
| 4 | Dersi işle/tamamla, katılım & not | [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md) | M05 | 🟢 (gelmedi→ücretlendirme ✅ Dilim A; not görünürlüğü ⚠️ Dilim B) |
| 5 | Ders notu + **kaynak** + ödev ver/takip | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟡 (ödev onay/geri gönder + geri bildirim + not görünürlüğü ⚠️) |
| 6 | Ödeme/bakiye takibi (manuel) + veli paylaşımı | [`m07_payments`](../modules/m07_payments.md) | M07 | 🟢 (öğrenci bazlı ücret + veli paylaşımı ⚠️) |
| 7 | Yaklaşan ders + ödev/ödeme hatırlatması | [`m11_notifications`](../modules/m11_notifications.md) | M11 | 🟡 |
| 8 | Gelir istatistik & rapor | [`m14_reporting`](../modules/m14_reporting.md) | M14 | 🔴 |
| 9 | Öğrenci gelişim takibi | [`m10_progress_tracking`](../modules/m10_progress_tracking.md) | M10 | 🔴 |
| 10 | Öğrenci/veli ile mesajlaşma | [`m16_messaging`](../modules/m16_messaging.md) | M16 | 🔴 |
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
4. **Tekrarlı ders:** Ders tek seferlik veya tekrarlı planlanabilir (`RecurrenceRule`). Tekrar açılımı ⚠️ eklenecek.
5. **Çakışma kontrolü:** Aynı öğretmende zaman çakışan ders **engellenir** (`scheduling.teacher_conflict` — koddan doğrulandı 🟢).
6. **Ders tamamlama:** Süre, gerçek başlangıç/bitişten **otomatik** hesaplanır; manuel girilmez.
7. **Ödeme manuel:** Para sistem üzerinden **alınmaz**; öğretmen "X dersinin ödemesini aldım" şeklinde elle işaretler. Ödeme bilgisi veliyle **paylaşılabilir** (`IsSharedWithParent` ⚠️).
8. **Doğrulama rozeti:** Öğretmen kendini "doğrulanmış" yapamaz; yalnızca admin (güvenlik — bkz. [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) Y1).
9. **Sahiplik:** Öğretmen yalnızca **kendi** eklediği/kendisine bağlı öğrencileri görebilir.

---

## 6. Mobil Ekranlar

**Mevcut ✅** (bkz. [`../pages/00_pages_index.md`](../pages/00_pages_index.md)):
`/dashboard` (öğretmen paneli), `/teacher-profile`, `/students` + `/students/:id`, `/scheduling` (takvim), `/lesson-sessions` + detay/not, `/assignments/...`, `/payments` + `/payments/new`, `/more`, `/account-info`.

**Planlanan ⚠️:**
- Dashboard zenginleştirme (bugünkü dersler, bekleyen ödev, geciken ödeme — tek özet endpoint).
- Kaynak paylaşma + öğrenci ödev yükleme görünümü.
- Mesajlaşma ekranı (öğrenci/veli).
- Gelir/öğrenci gelişim raporları, ilan verme, üyelik/paywall.

---

## 7. Bireysel vs Eşleşmiş Kullanım
- **Bireysel (Faz 1):** Öğretmen kendi (manuel dahil) öğrencilerini ve derslerini yönetir — eşleştirme gerekmez.
- **Eşleşmiş (Faz 4):** İlan verir, öğrenciler keşfeder; eşleşince öğrenci-öğretmen ilişkisi kurulur, ders öğrencinin programına otomatik eklenir; dersi tamamlanan öğrenci öğretmeni puanlayabilir.

## 8. Üyelik Etkisi (Free/Premium)
Premium öğretmen: reklamsız, **sınırsız öğrenci**, gelir analizi, PDF öğrenci raporu, boş zaman analizi, profil öne çıkarma, WhatsApp/SMS hatırlatma. Free: öğrenci limiti + reklam (bkz. [`../modules/m17_membership.md`](../modules/m17_membership.md), PRD §9.1).

## 9. Kabul Kriterleri (Faz 1)
- [x] Teacher rolüyle kayıt/giriş; profil oluştur/düzenle.
- [x] Manuel öğrenci ekleme + listeleme.
- [x] Takvime ders ekleme (tek + tekrar alanı) + çakışma engeli.
- [x] Ders oturumu tamamlama (konu/süre/katılım/not).
- [x] Ders sonrası not + ödev; manuel ödeme + gelir özeti + geciken filtre.
- [ ] Push bildirim uçtan uca (FCM teslimatı — M11 eksik).
- [ ] Online ders linki; kaynak paylaşımı; öğrenci ödev yükleme görünümü.
- [ ] 5–10 gerçek öğretmenle beta test (PRD önerisi).

## 10. Denetim — Kod Gerçeği vs. Fonksiyonel Doküman (2026-07-18)

Kaynak: [`../ogretmen_rolu_fonksiyonel_dokuman_v1.md`](../ogretmen_rolu_fonksiyonel_dokuman_v1.md) §15 boşluk listesi, gerçek koda (domain modeli + endpoint envanteri) karşı denetlendi. Faz 1 çekirdeği (M02–M07) domain+API+mobil olarak **çalışıyor**; ancak fonksiyonel dokümanın **[YENİ]** işaretli iş kurallarının çoğu koda henüz girmemiştir.

### 10.1 Kritik/Yüksek boşluklar (Faz 1)

| Kod | Boşluk | Kod gerçeği | Etki |
|-----|--------|-------------|------|
| B-01 | **Tatil / müsait değil bloğu** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `TimeOffBlock` aggregate + `POST/GET/DELETE /teachers/{id}/time-off`; oluşturmada çakışan dersler yanıtta | — |
| B-02 | **Ders erteleme** ayrı aksiyon | ✅ **yapıldı (Dilim A, 2026-07-18)** — `Reschedule()` + `POST /lessons/{id}/reschedule`; statü Planned kalır, `OriginalStartAtUtc`/`RescheduleNote` geçmişi, Rescheduled event | — |
| B-03 | **Tekrar eden ders occurrence yönetimi** (bu/bu+sonraki/tümü) | ✅ **yapıldı (Dilim A, 2026-07-18)** — `LessonOccurrenceException` + `RecurrenceExpander` istisna overload'u; cancel/reschedule `Scope=Single/ThisAndFuture/All` | — |
| B-05 | **Not görünürlüğü** (özel/öğrenci/veli) | `LessonNote.TeacherNotes` düz string; visibility yok | Öğretmen dürüst özel not tutamaz; veli paylaşımı süzülemez (Dilim B) |
| B-07 | **Öğrenci bazlı ücret** override | Ücret yalnız profil ya da ders bazında; öğrenciye özel alan yok | Gerçek fiyatlamayı karşılamıyor (Dilim C) |
| B-08 | **Gelmedi + ücretlendirme kararı** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `LessonSession.IsChargeable` + complete akışı (otomatik ödeme yok; audit/rapor için) | — |
| B-09 | **İptal nedeni + ücretlendirme + Sil ayrımı** | ✅ **yapıldı (Dilim A, 2026-07-18)** — `Cancel(reason, isChargeable, …)` + `CancellationReason` enum + `DELETE /lessons/{id}` (24s+gelecek kuralı) | — |
| B-04 | **Öğrenci arşivleme** | `IsActive` bayrağı var; arşiv akışı/filtresi + Free-limit bağı yok | Free limit yönetimi eksik (Dilim C) |
| B-06 | **Öğrenci-öğretmen davet/bağlanma** | Öğrenci için davet/onay akışı yok (yalnız veli `LinkParent` var) | İki giriş yolu birleşmiyor (Faz 2) |
| B-10 | **Online link semantiği** | ✅ **yapıldı (Dilim A, 2026-07-18)** — ayrı `MeetingUrl` alanı; `LocationLabel` yüz yüze adresi için | — |

### 10.2 Yanlış yapılandırma (sadece eksik değil — düzeltme gerekir)

1. ✅ **M02 branş tekilliği + sertifika (Dilim D, 2026-07-18):** `TeacherSubject` çoklu branş koleksiyonu + `TeacherCertificate` (T-02.12) eklendi; birincil `TeacherProfile.Subject` korunur (domain event + eşleştirme kırılmadı), mevcut profiller migration backfill'i ile `teacher_subjects`'e taşındı. Upsert `Subjects`/`Certificates` listeleri taşır.
2. ✅ **Erteleme = düzenleme (Dilim A, 2026-07-18):** ayrı `Reschedule()` domain metodu + `POST /lessons/{id}/reschedule` + `OriginalStartAtUtc`/`RescheduleNote` erteleme geçmişi; statü Planned kalır (kayıtlı taşıma).
3. ✅ **İptal veri modeli (Dilim A, 2026-07-18):** `CancellationReason` enum + `IsChargeable` eklendi.
4. ✅ **Tekrar eden ders sanal model (Dilim A, 2026-07-18):** `LessonOccurrenceException` tablosu + `RecurrenceExpander` istisna overload'u ile B-03 çözüldü.

### 10.3 Önceliklendirilmiş düzeltme sırası

- **Öncelik 1 (Faz 1'i kullanılabilir yapan):** ✅ B-03 (occurrence yönetimi), ✅ B-01 (tatil bloğu), ✅ B-02 (erteleme) — **Dilim A tamam**; B-05 (not görünürlüğü) → Dilim B.
- **Öncelik 2 (yüksek etkili):** B-07 (öğrenci bazlı ücret, Dilim C), ✅ B-08 (gelmedi→ücretlendirme), ✅ B-09 (iptal nedeni/sil) — **Dilim A tamam**; B-04 (arşivleme, Dilim C).
- **Öncelik 3 (olgunluk):** ✅ M02 çoklu branş + sertifika (Dilim D tamam, 2026-07-18), B-06 (öğrenci davet), ✅ B-10 (online link) — **Dilim A tamam**.

### 10.4 Karar bekleyen sorular (veri modelini etkiler)
1. Bir öğrenci **birden fazla öğretmene** bağlanabilir mi? (→ `TeacherStudent` bağlantı tablosu gerekli mi?)
2. Free limit **aktif** öğrenci mi, toplam mı?
3. Öğrenci limiti kesin sayı: **5 mi 10 mu?**
4. Erken geri bildirim Faz 4'te herkese açılacak mı? (retroaktif yayın onayı)

---

## 11. İlişkili Dokümanlar
- Öğrenci tarafı → [`ogrenci.md`](ogrenci.md) · Veli → [`veli.md`](veli.md)
- Teknik modüller → [`../modules/m02_teachers.md`](../modules/m02_teachers.md), [`m04_scheduling`](../modules/m04_scheduling.md), [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md), [`m06_assignments`](../modules/m06_assignments.md), [`m07_payments`](../modules/m07_payments.md)
- Mimari açıklar → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)

---

*Öğretmen Rolü — Detaylı Tasarım | Güncelleme: 2026-07-18 (Dilim D: M02 çoklu branş + sertifika)*
