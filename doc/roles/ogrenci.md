# 🎓 Öğrenci Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 2️⃣** · **Faz 2 — Öğrenci Bireysel Çalışma** · **Durum: 🟢 Bireysel çalışma uçtan uca (M08: kronometre/test/hedef/streak/rozet + mobil `study` feature + self-register)**
>
> **Amaç:** Öğrenci, **öğretmene ihtiyaç duymadan** kendi ders programını, çalışmasını, hedeflerini ve gelişimini
> takip edebilsin; öğretmenle eşleşirse ders/ödev akışı da buraya bağlansın.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogretmen.md`](ogretmen.md) · [`veli.md`](veli.md) · [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)
> **Güncelleme:** 2026-07-19

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
| **Kendi ders programı / Takvim** (kişisel dersler + öğretmenin özel dersi salt-okunur/öncelikli, tek/tekrarlı, çakışma reddi) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (2026-07-08 — `StudentCalendarPage` + `StudyScheduleEntry`) |
| **Ders/konu kataloğu** (ders + tekrar kullanılabilir konu listesi; kronometre/deneme/takvim/gelişim temeli) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 (2026-07-09 — `StudentSubjectCatalog`/`StudentTopicCatalog` + `SubjectCatalogPage`) |
| **Çalışma kronometresi** (odak süresi) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Deneme/test** (D-Y-boş, net, artış/azalış analizi) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Hedefler** (deneme net hedefi, günlük hedef) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Seri (streak) + başarımlar** | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Konu eksikleri + konu gelişimi + konu hedefleri** | [`m10_progress_tracking`](../modules/m10_progress_tracking.md) | M10 | 🟡 (2026-07-09 — konu hâkimiyeti/eksik-güçlü + hedef + `ProgressOverviewPage`; zaman serisi grafiği ⚠️) |
| Ödev **yükleme** + tamamlama (öğretmene bağlıysa) | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (2026-07-09 — `StudentAssignmentsPage`: dosya yükleme + tamamlama) |
| Ders notu + **kaynak** görüntüleme (öğretmen notu) | [`m06_assignments`](../modules/m06_assignments.md) | M06 | 🟢 (öğrenci görünümü ⚠️) |
| **Kendi ders notunu ekleme** (öğretmen notundan ayrı) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 (2026-07-09 — `StudyNote` + `StudyNotesPage`) |
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
8. **Streak eşiği (anlamlı seri):** Bir gün seriye ancak günlük hedefin ayarlanabilir bir yüzdesi (`StreakThresholdPercent`, varsayılan **%60**; günlük hedef yoksa sabit **20 dk**) tamamlanınca sayılır — kısa bir seans seriyi ilerletmez. Streak gün sınırı **04:00**'tir; gece geç çalışan öğrenci dünkü serisini korur (M08).

---

## 6. Mobil Ekranlar

**Mevcut ✅** (`mobile/lib/features/study/`):
- **Sekmeler (StudentBottomNav, 5):** `student-home` (çalışma panosu, ux bilgi hiyerarşisine göre sıralı: motivasyon alt başlığı + yan yana bugünkü çalışma/günlük seri kartları + hızlı işlemler + yaklaşan ders + bu hafta/rekor + son çalışma/deneme), `student/studies` (Çalışmalarım: sayaç + haftalık grafik + derslere göre süre + istatistik + son çalışmalar), `student/tests` (Testler: deneme gir + net trend grafiği + ders bazlı analiz + son denemeler), `student/calendar` (**Takvim** 🟢 2026-07-08: birleşik ders programı — öğretmen dersleri salt-okunur/öncelikli + öğrencinin kendi oluşturduğu dersler; ay takvimi (`SfCalendar`) → seçili gün listesi; tek/tekrarlı kişisel ders ekle/düzenle/sil, öğretmen slotuna eklenemez), `student/more` (Diğer hub).
- **Diğer hub'ından açılan (push):** `student/profile` (Profil/İstatistik — ux §11: toplam çalışma/gün/rekor + toplam deneme/net + en çok çalışılan ders + rozet özeti), `student/teacher` (Öğretmenlerim: yalnızca bağlı öğretmen(ler)in bilgi kartı — dersler Takvim'de, ödev/not/mesaj "yakında"), `student/goals-overview` (**Hedefler** — 2026-07-08'de sekmeden Diğer'e taşındı: günlük/haftalık/net ilerleme + düzenleme), ayrıca Rozetler/Hedef&paylaşım/Hesap/Çıkış.
- **Alt (detay) ekranlar:** `study/timer` (kronometre: başlat/mola/devam/bitir/iptal + canlı sayaç), `study/test` (deneme girişi + otomatik net önizleme), `study/goals` (günlük/haftalık/net hedef + veli/öğretmen paylaşım anahtarları), `study/history` (seans/deneme/haftalık grafik + manuel seans), `study/achievements` (rozetler + ilerleme). Auth ekranları ortak.

> **Rol bazlı navigasyon ✅:** `app_router.dart` redirect'i öğrenciyi (`Student`, öğretmen değil) `/student-home`'a yönlendirir; öğretmene özel ekranlara düşerse geri alır. Profili olmayan öğrenci ilk girişte `SelfRegistered` olarak otomatik oluşturulur.

> **Alt navigasyon (StudentBottomNav) 🟢:** Öğrenci arayüzü öğretmen/veli tasarım diliyle uyumlu (AppBar yerine `AppPageHeader`, `AppShadows.soft` + `skyBorder` kartlar). Nav **5 sekme** (ux §4, max-5): **Ana Sayfa · Çalışmalarım · Testler · Takvim · Diğer** (2026-07-08: "Hedefler" sekmesi Diğer'e taşındı, yerine "Takvim" geldi). İlk üçü + Takvim gerçek API verisiyle çalışır 🟢. **Takvim** (`/student/calendar`) birleşik ders programıdır: `GET /api/scheduling/students/{id}/calendar` ile öğretmen dersleri (salt-okunur) + öğrencinin kendi `StudyScheduleEntry` girdileri, tekrarlar backend'de genişletilmiş olarak; kendi dersini ekler/düzenler/siler, öğretmen dersinin saatine çakışan ders eklenemez (`scheduling.teacher_conflict`). **Diğer** bir hub'dır (Profil/İstatistik, Rozetler, Öğretmenlerim, **Hedefler**, Hedef & paylaşım, Hesap, Çıkış). **Öğretmenlerim** (Diğer'den push) 🟡: **yalnızca bağlı öğretmen(ler)i** gösterir. **Öğretmen(ler)im 🟢** gerçek — öğrencinin bağlı olduğu öğretmen(ler) güvenli öğrenci-kapsamlı derslerin (`GET /api/scheduling/students/{id}/lessons`; sahiplik M03'ün yayınladığı `IStudentDirectory` sözleşmesiyle doğrulanır — öğrenci yalnızca kendi `studentId`'sini görebilir, IDOR koruması, modüller-arası proje referansı yok) `teacherUserId` kümesinden türetilir; her biri için `GET /api/teachers/profiles/{userId}` ile profil çekilip **açılır-kapanır gradient hero kartı** gösterilir (varsayılan yalnızca başlık: navy→mavi degrade band + avatar + ad + doğrulama rozeti + başlık + branş pill'i + sağda aç/kapat oku; oka/başlığa dokununca `AnimatedSize` ile gövde açılır: deneyim/eğitim/ücret istatistik satırı + konum/format meta + "Hakkında" biyografi; profili alınamayan öğretmen atlanır). Birden fazla öğretmen desteklenir (kart listesi + sayaç). Dersler bu ekranda listelenmez (Takvim ekranındadır). Ödevler/Ders Notları/Mesajlar backend olmadığından "yakında". Ana sayfada **Yaklaşan Ders** kartı da bu endpoint'i kullanır. Ortak sekme bileşenleri `study/presentation/widgets/study_tab_widgets.dart` (StudyCard, StudyStatTile, StudySectionHeader, StudySessionTile, StudyComingSoonCard); studentId çözümü `study/presentation/student_scope.dart`.

**Planlanan ⚠️:** `progress` (M10 gelişim analizi), `my-lessons` (eşleşmiş ders geçmişi + ödev yükleme), öğretmenle mesajlaşma (M16).

---

## 7. Bireysel vs Eşleşmiş Kullanım
- **Bireysel (Faz 2):** Kronometre, test, hedef, seri, konu gelişimi — öğretmensiz.
- **Eşleşmiş:** Özel ders programa eklenir; ödev yükleme; ders notu/kaynak görüntüleme; öğretmenle mesajlaşma; ders sonrası öğretmen puanlama (M13).

## 8. Üyelik Etkisi (Free/Premium)
Premium öğrenci: reklamsız, geçmiş çalışma kayıtları, haftalık/aylık analiz, hedef belirleme, seri/motivasyon, öğretmenle detaylı veri paylaşımı. Free: temel kronometre/test + reklam + limit (PRD §9.2, [`../modules/m17_membership.md`](../modules/m17_membership.md)).

## 9. Kabul Kriterleri (Faz 2)
- [x] Öğretmensiz kayıt (`SelfRegistered`) — mobil ilk girişte otomatik profil.
- [x] Çalışma kronometresi (konu seç, başlat/durdur/bitir, mola) + seans geçmişi + haftalık özet.
- [x] Test girişi + net + konu bazlı takip (net-trend).
- [x] Hedef + seri + başarım sistemi (seri, ayarlanabilir günlük-hedef eşiği + 04:00 gün sınırı ile — anlamlı streak).
- [ ] Konu eksik/gelişim/hedef (M10 — iskelet).
- [ ] Öğretmene bağlıysa ödev yükleme + ders notu/kaynak görüntüleme (M06 öğrenci görünümü ⚠️).
- [ ] Özel ders çakışmasında öncelik + uyarı (M04 entegrasyonu ⚠️).

## 10. İlişkili Dokümanlar
- Öğretmen tarafı → [`ogretmen.md`](ogretmen.md) · Veli (öğrenci verisini tüketir) → [`veli.md`](veli.md)
- Teknik → [`../modules/m08_study.md`](../modules/m08_study.md), [`m10_progress_tracking`](../modules/m10_progress_tracking.md), [`m03_students`](../modules/m03_students.md), [`m06_assignments`](../modules/m06_assignments.md), [`m04_scheduling`](../modules/m04_scheduling.md)

---

*Öğrenci Rolü — Detaylı Tasarım | Güncelleme: 2026-07-19 (Ö-A streak eşiği: `StreakThresholdPercent` + 04:00 gün sınırı — anlamlı seri) · 2026-07-09 (Öğretmenlerim ekranı: bağlı öğretmen(ler) bilgi kartı `GET /api/teachers/profiles/{userId}` ile eklendi; ux §4 IA: 5 sekme — Ana Sayfa/Çalışmalarım/Testler/**Takvim**/Diğer; Hedefler+Öğretmenlerim+Profil Diğer hub'ında; **Takvim** = birleşik ders programı, öğrenci kişisel `StudyScheduleEntry` CRUD + tekrar + öğretmen çakışma reddi; Dersler gerçek — güvenli öğrenci-kapsamlı scheduling endpoint'i `IStudentDirectory` ile)*
