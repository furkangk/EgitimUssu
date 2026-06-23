# 👨‍🏫 Öğretmen Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 1️⃣ (ilk geliştirilen rol)** · **Faz 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Büyük ölçüde yazıldı**
>
> **Amaç:** Öğretmenin uygulamayı **her gün** kullandığı bir günlük operasyon aracı olmak — öğrencilerini,
> derslerini, ödevlerini, notlarını/kaynaklarını, tatillerini ve ödemelerini **tek takvim** etrafında yönetir.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogrenci.md`](ogrenci.md) · [`veli.md`](veli.md) · modül indeksi [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)
> **Güncelleme:** 2026-06-24

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
| 1 | Öğretmen profili (branş, şehir, ücret, uygunluk) | [`m02_teachers`](../modules/m02_teachers.md) | M02 | 🟢 |
| 2 | Öğrenci ekle & listele | [`m03_students`](../modules/m03_students.md) | M03 | 🟢 |
| 3 | Takvimde ders planla (tek/tekrarlı, online/yüz yüze + link) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 |
| 4 | Dersi işle/tamamla, katılım & not | [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md) | M05 | 🟢 |
| 5 | Ders notu + **kaynak** + ödev ver/takip | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (kaynak/öğrenci yükleme ⚠️) |
| 6 | Ödeme/bakiye takibi (manuel) + veli paylaşımı | [`m07_payments`](../modules/m07_payments.md) | M07 | 🟢 (veli paylaşımı ⚠️) |
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
3. **Online ders linki:** Ders online ise öğretmen bir **bağlantı (MeetingUrl)** girer; öğrenciler bu linkle derse katılır (⚠️ alan eklenecek — M04/M05).
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

## 10. İlişkili Dokümanlar
- Öğrenci tarafı → [`ogrenci.md`](ogrenci.md) · Veli → [`veli.md`](veli.md)
- Teknik modüller → [`../modules/m02_teachers.md`](../modules/m02_teachers.md), [`m04_scheduling`](../modules/m04_scheduling.md), [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md), [`m06_assignments`](../modules/m06_assignments.md), [`m07_payments`](../modules/m07_payments.md)
- Mimari açıklar → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)

---

*Öğretmen Rolü — Detaylı Tasarım | Güncelleme: 2026-06-24*
