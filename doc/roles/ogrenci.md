# 🎓 Öğrenci Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 2️⃣** · **Faz 2 — Öğrenci Bireysel Çalışma** · **Durum: 🟢 Bireysel çalışma uçtan uca (M08: kronometre/test/hedef/streak/rozet + mobil `study` feature + self-register)**
>
> **Amaç:** Öğrenci, **öğretmene ihtiyaç duymadan** kendi ders programını, çalışmasını, hedeflerini ve gelişimini
> takip edebilsin; öğretmenle eşleşirse ders/ödev akışı da buraya bağlansın.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogretmen.md`](ogretmen.md) · [`veli.md`](veli.md) · [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)
> **Güncelleme:** 2026-07-21

---

## 1. Tasarım İlkesi
Öğrenci platforma **öğretmenden bağımsız** girer ve tek başına değer bulur — bu, platformun **büyüme motorlarından biridir** (bireysel çalışma takibi). Bağımsız öğrenci havuzu, ileride eşleştirmeye (M12) hazır kitleyi oluşturur. Öğrenci sistemi kullanmak için öğretmene ihtiyaç duymaz; öğretmenle eşleşirse ek özellikler (ödev, ders notu/kaynak) açılır.

---

## 2. Kullanıcı Tipi ve Giriş (İki Senaryo)

| Senaryo | `StudentOrigin` | `UserId` | `CreatedByTeacherUserId` |
|---------|-----------------|----------|--------------------------|
| Öğrenci kendi kaydoldu | `SelfRegistered` | set | null |
| Öğretmen ekledi (manuel) | `TeacherManaged` | null (başta) | set |

**S-01.2 Davet kodu ile devralma (claim) + profil birleştirme (Ö-C):** Öğretmen bir manuel öğrenciyi davet ettiğinde sistem 6 haneli bir **davet kodu** üretir. Öğrenci hesabıyla giriş yapıp bu kodu girer (`POST /api/students/links/claim`), böylece öğretmenin oluşturduğu profili **kendi hesabına devralır**. Öğrencinin zaten kendi kaydettiği bir profili (`SelfRegistered`) varsa iki profil **birleşir**: kanonik profil öğrencinin self-profil'i olur; manuel profile bağlı tüm veriler (ders programı, ödev, ders notu, ödeme, ders seansı, çalışma kayıtları) kanonik profile taşınır — böylece **veri bölünmesi biter** (B-01/AKIŞ 3) ve veli paneli tek bir öğrenciden beslenir. Birleştirme **her zaman öğrencinin onayıyla** (kod girişi) gerçekleşir. Detay: [`../modules/m03_students.md`](../modules/m03_students.md) §4 (kural 18–19), §5.

---

## 3. Yetenek Haritası

| Yetenek | Modül | PRD | Durum |
|---------|-------|-----|-------|
| Kayıt/giriş (Student) | [`m01_identity`](../modules/m01_identity.md) | M01 | 🟢 |
| Öğrenci profili | [`m03_students`](../modules/m03_students.md) | M03 | 🟢/🟡 |
| **Kendi ders programı / Takvim** (kişisel dersler + öğretmenin özel dersi salt-okunur/öncelikli, tek/tekrarlı, çakışma reddi) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (2026-07-08 — `StudentCalendarPage` + `StudyScheduleEntry`) |
| **Ders erteleme talebi** (S-04.5 — öğretmen dersinin ertelenmesini neden + alternatif tarihle **talep** eder; dersi kendisi değiştirmez, öğretmen kabul/red eder) | [`m04_scheduling`](../modules/m04_scheduling.md) | M04 | 🟢 (backend Ö-F, 2026-07-18 — `LessonChangeRequest` + `POST /students/{id}/lesson-requests`; mobil ⚠️) |
| **Ders/konu kataloğu** (ders + tekrar kullanılabilir konu listesi; kronometre/deneme/takvim/gelişim temeli) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 (2026-07-09 — `StudentSubjectCatalog`/`StudentTopicCatalog` + `SubjectCatalogPage`) |
| **Çalışma kronometresi** (odak süresi) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Deneme/test** (D-Y-boş, net, artış/azalış analizi) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 |
| **Çok dersli deneme** (tam deneme oturumu; dersler toplanıp toplam net) | [`m08_study`](../modules/m08_study.md) | M08 | 🟢 (2026-07-19 — `MockExam`, `POST /students/{id}/mock-exams`) |
| **Hedef sınav** (LGS/TYT/AYT/…; net ceza bölenini belirler) | [`m03_students`](../modules/m03_students.md) | M03 | 🟢 (2026-07-19 — `StudentProfile.TargetExam`, S-03.9) |
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
4. **Net hesabı:** Test girişinde `Doğru + Yanlış + Boş = Toplam`; net = `Doğru − Yanlış/ceza`. Ceza böleni hedef sınava göre türetilir (`ExamPenalty`): **LGS → 3**, **TYT/AYT → 4**, **okul denemesi → yanlış götürmez** (M08/M03 `TargetExam`).
5. **Ödev yükleme + son tarih:** Öğretmene bağlıysa öğrenci ödevini **yükler**; son teslim tarihinden önce yüklemezse **veliye bildirim** gidebilir (M06 + M11 + M09).
6. **Gizlilik:** Öğrenci bireysel çalışma verisini veli/öğretmenle paylaşıp paylaşmayacağını seçer (M15 `ShareStudyDataWith*`).
7. **Gamification amacı:** Seri ve başarımlar, öğrenciyi çalışmaya teşvik ve sistemde tutma içindir.
8. **Streak eşiği (anlamlı seri):** Bir gün seriye ancak günlük hedefin ayarlanabilir bir yüzdesi (`StreakThresholdPercent`, varsayılan **%60**; günlük hedef yoksa sabit **20 dk**) tamamlanınca sayılır — kısa bir seans seriyi ilerletmez. Streak gün sınırı **04:00**'tir; gece geç çalışan öğrenci dünkü serisini korur (M08).

---

## 6. Mobil Ekranlar

**Mevcut ✅** (`mobile/lib/features/study/`):
- **Sekmeler (StudentBottomNav, 4 — 4-sekme IA, 2026-08-19, Task 1: Keşfet kaldırıldı):** `student-home` (**Çalış** — çalışma panosu, ux bilgi hiyerarşisine göre sıralı: motivasyon alt başlığı + yan yana bugünkü çalışma/günlük seri kartları + hızlı işlemler + yaklaşan ders + bu hafta/rekor + son çalışma/deneme), `student/performance` (**Performans** — Task 3'te genişledi: `student_tests_page.dart` — deneme gir + net trend grafiği + ders bazlı analiz + son denemeler **+ Haftalık analiz (çubuk grafik) + Ders → konu kırılımı** (eski Çalışmalarım'dan taşındı) **+ Analiz & Gelişim** girişleri (Detaylı analiz → `/study/history`, Gelişimim → `/student/progress`)), `student/lessons` (**Derslerim** — Task 1'de rota/nav taşındı, Task 4'te başlık "Derslerim" oldu + **Ders araçları** bölümü eklendi: `student_calendar_page.dart` — birleşik ders programı: öğretmen dersleri salt-okunur/öncelikli + öğrencinin kendi oluşturduğu dersler; ay takvimi (`SfCalendar`) → seçili gün listesi; tek/tekrarlı kişisel ders ekle/düzenle/sil, öğretmen slotuna eklenemez; **liste sonunda 4 giriş kartı** (`_LessonToolTile`): Dersler & Konular (→`/study/catalog`), Ödevlerim (→`/student/assignments`), Öğretmenlerim (→`/student/teacher`), Notlarım (→`/study/notes`)), `student/profile` (**Profil** — ayrı sekme, ux §11: `AppPageHeader` sayfa içi başlık (AppBar yok) + **premium hero + Aboneliğim + birleşik Gizlilik&Güvenlik — Task 8, 2026-08-19**: üstte profil hero (baş harf avatar, ad, gerçek `gradeLevel`/`goalSummary`, 🔥 seri; `bool _isPremium` demo `false` — premium'da altın kenarlık + "Premium" rozeti, free'de sade + tıklanabilir "Yükselt" ipucu; sağ üstte Düzenle → demo yer tutucu) + toplam çalışma/gün/rekor + toplam deneme/net + en çok çalışılan ders + rozet özeti **+ Ayarlar menüsü** (Velim[demo] · **Öğretmenlerim→`/student/teacher`** [Task 6'da orphan kalan rota artık bağlı] · Hedef ekle→`/study/goals` [gerçek] · Bildirim ayarları[demo] · **Gizlilik ve Güvenlik** [birleşik tek tile — eski ayrı "Gizlilik"+"Ayarlar & Güvenlik"; sheet'te demo gizlilik satırı + gerçek `/account-info`] · **Aboneliğim**[Faz 5 demo, eski pasif "Abonelik"in yerini aldı]) + Çıkış yap [bottom-sheet onayı]). Eski rotalar geri-uyum redirect'i: `student/tests`→`student/performance`, `student/calendar`→`student/lessons`, `student/studies`→`student-home` (Task 3, sayfa silindi), `student/more`→`student/profile` (Task 6, sayfa silindi), `student/discover`→`student/lessons` (Task 1, Keşfet sekmesi kaldırıldı, sayfa silindi).
- **Sekme dışı push sayfalar:** `study/timer` (**Kronometre** — Çalışma sekmesindeki "Çalışmaya Başla" girişinden açılır), `student/lessons/:id` (**Ders Detayı** — Derslerim'deki ders kartına dokununca açılır; öğretmen dersi salt görüntüle, kendi dersi düzenlenebilir — Task 5).
- **Diğer hub'ı kaldırıldı (Task 6, 2026-07-21):** eski `student/more` hub'ı (Rozetler, Öğretmenlerim, Hedefler, Hedef & paylaşım, Hesap, Çıkış) tamamen dağıtıldı ve silindi; rota `student/profile`'a redirect eder.
- **Profil'in Ayarlar menüsünden açılan (Task 8, 2026-08-19 — Derslerim'in "Ders araçları" bölümünde Task 6'dan beri yer almıyor):** `student/teacher` (Öğretmenlerim: yalnızca bağlı öğretmen(ler)in bilgi kartı — dersler Derslerim'de, ödev/not/mesaj "yakında"). **Çalış'ın kısayollarından açılan:** `student/goals-overview` (**Hedefler**: günlük/haftalık/net ilerleme + düzenleme).
- **Alt (detay) ekranlar:** `study/timer` (kronometre: başlat/mola/devam/bitir/iptal + canlı sayaç), `study/test` (deneme girişi + otomatik net önizleme), `study/goals` (günlük/haftalık/net hedef + veli/öğretmen paylaşım anahtarları), `study/history` (seans/deneme/haftalık grafik + manuel seans), `study/achievements` (rozetler + ilerleme). Auth ekranları ortak.

> **Rol bazlı navigasyon ✅:** `app_router.dart` redirect'i öğrenciyi (`Student`, öğretmen değil) `/student-home`'a yönlendirir; öğretmene özel ekranlara düşerse geri alır. Profili olmayan öğrenci ilk girişte `SelfRegistered` olarak otomatik oluşturulur.

> **Alt navigasyon (StudentBottomNav) 🟢:** Öğrenci arayüzü öğretmen/veli tasarım diliyle uyumlu (AppBar yerine `AppPageHeader`, `AppShadows.soft` + `skyBorder` kartlar). Nav **4 sekme** (ux §4, 2026-08-19 4-sekme IA — Task 1: eski 5-sekme IA'daki **Keşfet** sekmesi kaldırıldı): **🏠 Çalışma · 📚 Derslerim · 📊 Performans · 👤 Profil**. Tümü gerçek API verisiyle çalışır 🟢. Sekme dışı push sayfalar: **⏱️ Kronometre** (`/study/timer`, Çalışma sekmesindeki "Çalışmaya Başla" girişinden açılır) ve **📖 Ders Detayı** (`/student/lessons/:id`, Derslerim'deki ders kartından açılır). **Derslerim** (`/student/lessons`, eski `/student/calendar`) birleşik ders programıdır: `GET /api/scheduling/students/{id}/calendar` ile öğretmen dersleri (salt-okunur) + öğrencinin kendi `StudyScheduleEntry` girdileri, tekrarlar backend'de genişletilmiş olarak; kendi dersini ekler/düzenler/siler, öğretmen dersinin saatine çakışan ders eklenemez (`scheduling.teacher_conflict`). **Diğer** hub'ı kaldırıldı (Task 6, 2026-07-21); eski girdileri (Rozetler/Hedefler → Çalış, Dersler & Konular/Ödevlerim/Notlarım → Derslerim, Gelişimim → Performans, Velim/Gizlilik/Bildirim/Abonelik/Hesap/Çıkış → Profil'in Ayarlar menüsü) diğer sekmelere dağıtıldı; **Öğretmenlerim** Task 6'da Derslerim'in Ders araçları bölümünden kaldırılıp geçici olarak hiçbir sekmeden bağlanmıyordu, **Task 8 (2026-08-19) Profil'in Ayarlar menüsüne** eklendi (`/student/teacher`, artık orphan değil). Ayrıca Task 8'de Profil menüsüne **Hedef ekle** (→`/study/goals`) eklendi, eski ayrı "Gizlilik"+"Ayarlar & Güvenlik" **"Gizlilik ve Güvenlik"** altında birleşti, pasif "Abonelik" **"Aboneliğim"** oldu (Faz 5 demo). **Öğretmenlerim** (Profil'in Ayarlar menüsünden erişilir) 🟡: **yalnızca bağlı öğretmen(ler)i** gösterir. **Öğretmen(ler)im 🟢** gerçek — öğrencinin bağlı olduğu öğretmen(ler) güvenli öğrenci-kapsamlı derslerin (`GET /api/scheduling/students/{id}/lessons`; sahiplik M03'ün yayınladığı `IStudentDirectory` sözleşmesiyle doğrulanır — öğrenci yalnızca kendi `studentId`'sini görebilir, IDOR koruması, modüller-arası proje referansı yok) `teacherUserId` kümesinden türetilir; her biri için `GET /api/teachers/profiles/{userId}` ile profil çekilip **açılır-kapanır gradient hero kartı** gösterilir (varsayılan yalnızca başlık: navy→mavi degrade band + avatar + ad + doğrulama rozeti + başlık + branş pill'i + sağda aç/kapat oku; oka/başlığa dokununca `AnimatedSize` ile gövde açılır: deneyim/eğitim/ücret istatistik satırı + konum/format meta + "Hakkında" biyografi; profili alınamayan öğretmen atlanır). Birden fazla öğretmen desteklenir (kart listesi + sayaç). Dersler bu ekranda listelenmez (Derslerim ekranındadır). Ödevler/Ders Notları/Mesajlar backend olmadığından "yakında". Ana sayfada **Yaklaşan Ders** kartı da bu endpoint'i kullanır. Ortak sekme bileşenleri `study/presentation/widgets/study_tab_widgets.dart` (StudyCard, StudyStatTile, StudySectionHeader, StudySessionTile, StudyComingSoonCard); studentId çözümü `study/presentation/student_scope.dart`.

**Planlanan ⚠️:** `progress` (M10 gelişim analizi), `my-lessons` (eşleşmiş ders geçmişi + ödev yükleme), öğretmenle mesajlaşma (M16).

---

## 7. Bireysel vs Eşleşmiş Kullanım
- **Bireysel (Faz 2):** Kronometre, test, hedef, seri, konu gelişimi — öğretmensiz.
- **Eşleşmiş:** Özel ders programa eklenir; ödev yükleme; ders notu/kaynak görüntüleme; öğretmenle mesajlaşma; ders sonrası öğretmen puanlama (M13).

## 8. Üyelik Etkisi (Free/Premium)
Premium öğrenci: reklamsız, geçmiş çalışma kayıtları, haftalık/aylık analiz, hedef belirleme, seri/motivasyon, öğretmenle detaylı veri paylaşımı. Free: temel kronometre/test + reklam + limit (PRD §9.2, [`../modules/m17_membership.md`](../modules/m17_membership.md)).

**Kodda uygulanan kapılar (Ö-D):** Üyelik seviyesi `MembershipTier` (Free/Premium) M03 profilde tutulur; Study modülü Free/Premium kapılarını `IMembershipDirectory` sözleşmesinden okur. **Karar: Free geniş** (streak tam + son 30 gün geçmiş), **Premium yalnız derinlik**. Bugün zorlanan farklar: geçmiş/net-trend Free'de **son 30 güne** kısılı (Premium sınırsız); **hedef net/puan takibi** (`TargetNet`/`TargetScore`) Premium'a özel → Free'de `study.premium_required` (HTTP 402). Aylık analiz / konu zayıflık / streak dondurma kapı mekanizması hazır, endpoint'leri gelince bağlanacak. Ayrıntı: [`../modules/m08_study.md`](../modules/m08_study.md) §4.7.

## 9. Kabul Kriterleri (Faz 2)
- [x] Öğretmensiz kayıt (`SelfRegistered`) — mobil ilk girişte otomatik profil.
- [x] Çalışma kronometresi (konu seç, başlat/durdur/bitir, mola) + seans geçmişi + haftalık özet.
- [x] **Sayaç güvenilirliği (Ö-E, API):** offline/arka planda birikmiş net süre istemci-otoriter kabul edilir (`clientEffectiveMinutes`, şişirmeye karşı `elapsed+2` tavanı); çökme sonrası takılı seans `recover` ile kurtarılır; `active-session` sorgusu 6 saatten uzun süredir çalışan seansı `isStale` ile işaretler (B-02/AKIŞ 4). *Not: asıl arka plan/offline mantığı mobil tarafta ayrı iştir.*
- [x] Test girişi + net + konu bazlı takip (net-trend).
- [x] **Çok dersli deneme** (`MockExam`): dersler tek oturumda girilir, toplam net türetilir.
- [x] **Hedef sınav** (`TargetExam`) net ceza bölenini belirler (LGS/3, TYT-AYT/4, okul yanlış götürmez).
- [x] Tamamlanmış seans/test **düzenle-sil** (net + konu rollup tutarlı kalır; streak zinciri v1'de geri sarılmaz).
- [x] Hedef + seri + başarım sistemi (seri, ayarlanabilir günlük-hedef eşiği + 04:00 gün sınırı ile — anlamlı streak).
- [ ] Konu eksik/gelişim/hedef (M10 — iskelet).
- [ ] Öğretmene bağlıysa ödev yükleme + ders notu/kaynak görüntüleme (M06 öğrenci görünümü ⚠️).
- [ ] Özel ders çakışmasında öncelik + uyarı (M04 entegrasyonu ⚠️).

## 10. İlişkili Dokümanlar
- Öğretmen tarafı → [`ogretmen.md`](ogretmen.md) · Veli (öğrenci verisini tüketir) → [`veli.md`](veli.md)
- Teknik → [`../modules/m08_study.md`](../modules/m08_study.md), [`m10_progress_tracking`](../modules/m10_progress_tracking.md), [`m03_students`](../modules/m03_students.md), [`m06_assignments`](../modules/m06_assignments.md), [`m04_scheduling`](../modules/m04_scheduling.md)

---

*Öğrenci Rolü — Detaylı Tasarım | Güncelleme: 2026-08-19 (Kapanış: §6 Mobil Ekranlar kalan "5-sekme"/"Keşfet" referansları 4-sekme gerçeğine göre düzeltildi — Nav 4 sekme (Çalışma/Derslerim/Performans/Profil), Keşfet yok, Kronometre "Çalışmaya Başla"dan / Ders Detayı ders kartından push ile açılır · Task 8: Profil sekmesi — premium/free ayrımlı hero (altın kenarlık+"Premium" rozeti vs. sade+"Yükselt" ipucu, demo), Düzenle→demo, Ayarlar menüsüne **Öğretmenlerim** (Task 6'da orphan kalmıştı, artık `/student/teacher`'a bağlı) ve **Hedef ekle** (→`/study/goals`) eklendi, ayrı "Gizlilik"+"Ayarlar & Güvenlik" **"Gizlilik ve Güvenlik"** altında birleşti, pasif "Abonelik" **"Aboneliğim"** oldu · 5-sekme IA yeniden yapılandırması TAMAMLANDI — Task 6: Profil sekmesi AppBar → sayfa içi `AppPageHeader`; **Ayarlar menüsü** eklendi (Velim/Gizlilik/Bildirim/Abonelik/Ayarlar & Güvenlik) + Çıkış yap (bottom-sheet); eski Diğer hub'ı (`student_more_page.dart`) silindi, `/student/more`→`/student/profile` redirect · Task 4: Derslerim sekmesi başlığı "Derslerim" oldu + liste sonunda **Ders araçları** bölümü (Dersler & Konular/Ödevlerim/Öğretmenlerim/Notlarım kısayolları, `_LessonToolTile`) eklendi · Task 3: Performans sekmesi genişledi — eski Testler içeriğine Çalışmalarım'ın haftalık analiz + ders→konu kırılımı widget'ları taşındı + Analiz & Gelişim girişleri (Detaylı analiz/Gelişimim) eklendi; `student/studies` sayfası silindi, rota `/student-home`'a redirect · Task 1: `StudentBottomNav`/`StudentNavTab` → Çalış/Performans/Derslerim/Keşfet/Profil; `student/tests`→`student/performance`, `student/calendar`→`student/lessons` redirect; Keşfet Faz 4 yer tutucu; Profil artık ayrı sekme; Diğer sekme olmaktan çıktı (henüz push ile erişilebilir) · Ç-06: öğretmensiz "programla → çalış → analiz" döngüsü uçtan uca — kendi ders birleşik `LessonSchedule` (nullable teacher); kronometre/takvim katalog seçici; "Bugünün planı" kartı → sayaç `lessonId` ile başlar; takvimde ✓ çalışıldı / ○ atlandı rozeti · Ö-F: ders erteleme talebi — öğrenci `LessonChangeRequest` ile talep eder, öğretmen kabul (mevcut Reschedule)/red — backend; mobil bekliyor · Ö-C: S-01.2 davet kodu ile profili devralma (claim) + tam profil birleştirme (merge) → modüller-arası veri taşıma · Ö-B: çok dersli deneme `MockExam` + hedef sınav `TargetExam` → sınav tipine göre net böleni `ExamPenalty` · Ö-A streak eşiği: `StreakThresholdPercent` + 04:00 gün sınırı — anlamlı seri) · 2026-07-09 (Öğretmenlerim ekranı: bağlı öğretmen(ler) bilgi kartı `GET /api/teachers/profiles/{userId}` ile eklendi; ux §4 IA: 5 sekme — Ana Sayfa/Çalışmalarım/Testler/**Takvim**/Diğer; Hedefler+Öğretmenlerim+Profil Diğer hub'ında; **Takvim** = birleşik ders programı, öğrenci kişisel `StudyScheduleEntry` CRUD + tekrar + öğretmen çakışma reddi; Dersler gerçek — güvenli öğrenci-kapsamlı scheduling endpoint'i `IStudentDirectory` ile)*
