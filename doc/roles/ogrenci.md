# 🎓 Öğrenci Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 2️⃣** · **Faz 2 — Öğrenci Bireysel Çalışma** · **Durum: 🟡 Kısmen (profil var; bireysel çalışma 🔴 iskelet)**
>
> **Amaç:** Öğrenci, **öğretmene ihtiyaç duymadan** kendi ders programını, çalışmasını, hedeflerini ve gelişimini
> takip edebilsin; öğretmenle eşleşirse ders/ödev akışı da buraya bağlansın.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogretmen.md`](ogretmen.md) · [`veli.md`](veli.md) · [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)
> **Güncelleme:** 2026-06-24

---

## 1. Tasarım İlkesi
Öğrenci platforma **öğretmenden bağımsız** girer ve tek başına değer bulur — bu, platformun **büyüme motorlarından biridir** (bireysel çalışma takibi). Bağımsız öğrenci havuzu, ileride eşleştirmeye (M12) hazır kitleyi oluşturur. Öğrenci sistemi kullanmak için öğretmene ihtiyaç duymaz; öğretmenle eşleşirse ek özellikler (ödev, ders notu/kaynak) açılır.

---

## 2. Kullanıcı Tipi ve Giriş (İki Senaryo)

| Senaryo | `StudentOrigin` | `UserId` | `CreatedByTeacherUserId` |
|---------|-----------------|----------|--------------------------|
| Öğrenci kendi kaydoldu | `SelfRegistered` | set | null |
| Öğretmen ekledi (manuel) | `TeacherManaged` | null (başta) | set |

Manuel öğrenci sonradan gerçek hesabına bağlanabilir (davet/eşleşme — ⚠️ planlanan). Detay: [`../modules/m03_students.md`](../modules/m03_students.md).

---

## 3. Yetenek Haritası

| Yetenek | Modül | PRD | Durum |
|---------|-------|-----|-------|
| Kayıt/giriş (Student) | [`m01_identity`](../modules/m01_identity.md) | M01 | 🟢 |
| Öğrenci profili | [`m03_students`](../modules/m03_students.md) | M03 | 🟢/🟡 |
| **Kendi ders programı** (+ özel ders otomatik eklenir) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (öğrenci görünümü ⚠️) |
| **Çalışma kronometresi** (odak süresi) | [`m08_study`](../modules/m08_study.md) | M08 | 🔴 |
| **Deneme/test** (D-Y-boş, net, artış/azalış analizi) | [`m08_study`](../modules/m08_study.md) | M08 | 🔴 |
| **Hedefler** (deneme net hedefi, günlük hedef) | [`m08_study`](../modules/m08_study.md) | M08 | 🔴 |
| **Seri (streak) + başarımlar** | [`m08_study`](../modules/m08_study.md) | M08 | 🔴 |
| **Konu eksikleri + konu gelişimi + konu hedefleri** | [`m10_progress_tracking`](../modules/m10_progress_tracking.md) | M10 | 🔴 |
| Ödev **yükleme** + takip (öğretmene bağlıysa) | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (öğrenci yükleme ⚠️) |
| Ders notu + **kaynak** görüntüleme | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (öğrenci görünümü ⚠️) |
| Öğretmenle mesajlaşma | [`m16_messaging`](../modules/m16_messaging.md) | M16 | 🔴 |
| İlan verme (aradığı ders) + öğretmen puanlama | [`m12_matching`](../modules/m12_matching.md) / [`m13_reviews`](../modules/m13_reviews.md) | M12/M13 | 🔴 |
| Profil/bildirim/üyelik | [`m15_settings`](../modules/m15_settings.md) / [`m17_membership`](../modules/m17_membership.md) | M15/M17 | 🟡/🔴 |

---

## 4. Altın Akış (Golden Path)

```
Kayıt (Student, öğretmensiz) → kendi ders programını oluştur
  → Çalışma odası: konu seç → kronometre başlat/mola/bitir → seans özeti
    → Deneme gir (D/Y/boş → net) → konu bazlı artış/azalış analizi
      → Günlük hedef + seri (streak) + başarımlar
        → Konu eksiklerini gör → konu gelişim hedefleri
          → (öğretmenle eşleşirse) özel ders otomatik programda + ödev yükle + not/kaynak gör
```

---

## 5. Rol-Özel İş Kuralları

1. **Öğretmensiz tam işlevsellik:** Bireysel çalışma (kronometre, test, hedef, seri, konu gelişimi) öğretmen gerektirmez.
2. **Özel ders otomatik program + çakışma önceliği** (promp): Öğretmenle eşleşilen ders **otomatik** öğrencinin programına eklenir. Öğrencinin kendi planı ile özel ders **çakışırsa öncelik özel derstedir** ve öğrenci **uyarılır** (M04/M08).
3. **Mola net süreye eklenmez:** Kronometrede mola süresi toplam **net** çalışma süresine dahil edilmez (M08).
4. **Net hesabı:** Test girişinde `Doğru + Yanlış + Boş = Toplam`; net formülü (örn. `Doğru − Yanlış/4`) konfigüre edilebilir (M08).
5. **Ödev yükleme + son tarih:** Öğretmene bağlıysa öğrenci ödevini **yükler**; son teslim tarihinden önce yüklemezse **veliye bildirim** gidebilir (M06 + M11 + M09).
6. **Gizlilik:** Öğrenci bireysel çalışma verisini veli/öğretmenle paylaşıp paylaşmayacağını seçer (M15 `ShareStudyDataWith*`).
7. **Gamification amacı:** Seri ve başarımlar, öğrenciyi çalışmaya teşvik ve sistemde tutma içindir.

---

## 6. Mobil Ekranlar

**Mevcut ✅:** Öğrenci için adanmış akış **henüz yok**; mevcut app öğretmen odaklı. Auth ekranları ortak.

**Planlanan ⚠️** (UI tasarımında öngörülmüş — bkz. [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)):
`student-onboarding`, `student-dashboard`, `study-room` (kronometre), `study-history`, `test-entry`/`test-performance`, `goals-streak`, `progress` (gelişim analizi), `my-lessons` (ders geçmişi + ödevler), mesajlaşma.

> Rol bazlı navigasyon: `app_router.dart` redirect mantığı role (`Teacher`/`Student`) göre farklı dashboard'a yönlendirecek şekilde genişletilmeli.

---

## 7. Bireysel vs Eşleşmiş Kullanım
- **Bireysel (Faz 2):** Kronometre, test, hedef, seri, konu gelişimi — öğretmensiz.
- **Eşleşmiş:** Özel ders programa eklenir; ödev yükleme; ders notu/kaynak görüntüleme; öğretmenle mesajlaşma; ders sonrası öğretmen puanlama (M13).

## 8. Üyelik Etkisi (Free/Premium)
Premium öğrenci: reklamsız, geçmiş çalışma kayıtları, haftalık/aylık analiz, hedef belirleme, seri/motivasyon, öğretmenle detaylı veri paylaşımı. Free: temel kronometre/test + reklam + limit (PRD §9.2, [`../modules/m17_membership.md`](../modules/m17_membership.md)).

## 9. Kabul Kriterleri (Faz 2)
- [ ] Öğretmensiz kayıt (`SelfRegistered`).
- [ ] Çalışma kronometresi (konu seç, başlat/durdur/bitir, mola) + seans geçmişi + haftalık özet.
- [ ] Test girişi + net + konu bazlı takip.
- [ ] Hedef + seri + başarım sistemi.
- [ ] Konu eksik/gelişim/hedef (M10).
- [ ] Öğretmene bağlıysa ödev yükleme + ders notu/kaynak görüntüleme.
- [ ] Özel ders çakışmasında öncelik + uyarı.

## 10. İlişkili Dokümanlar
- Öğretmen tarafı → [`ogretmen.md`](ogretmen.md) · Veli (öğrenci verisini tüketir) → [`veli.md`](veli.md)
- Teknik → [`../modules/m08_study.md`](../modules/m08_study.md), [`m10_progress_tracking`](../modules/m10_progress_tracking.md), [`m03_students`](../modules/m03_students.md), [`m06_assignments`](../modules/m06_assignments.md), [`m04_scheduling`](../modules/m04_scheduling.md)

---

*Öğrenci Rolü — Detaylı Tasarım | Güncelleme: 2026-06-24*
