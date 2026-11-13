# P22 — Sınıflar (M19 Classrooms) — Backend — Hafif Plan 🅻

> **Hafif plan:** `/devam` önce **Task 0**'ı yürütür (kodu okuyarak ayrıntılandırma), sonra Task 1'e geçer.

**Goal:** Okul öğretmeninin bir sınıfı (ör. "9-A Matematik") tek yerden yönetebilmesi: öğrenciler sınıf koduyla kendileri katılır, öğretmen sınıfa toplu ödev verir ve duyuru gönderir, sınıf panosunda kimin geride kaldığını görür. Finans yoktur; öğrencinin bireysel çalışması varsayılan olarak görünmez.

**Architecture:** Yeni **Classrooms** modülü (`classrooms` şeması; 2026-09-02 §4 "yeni modül" adımları). `Classroom` ve `ClassroomMembership` aggregate'ları; katılım kodu `ISecureCodeGenerator` (P15 T1). `ITeacherStudentAccess` `Class` kapsamını döndürmeye başlar (P15 T2'de sözleşme hazır). Toplu ödev Assignments'ta `GroupAssignment` → üye başına `Assignment` (fan-out, olayla). Duyuru Notifications/Messaging üzerinden tek yönlü. Sınıf panosu Reporting projeksiyonu.

**Spec:** master K-13…K-18, §4, §7 (kapsam dışı: yoklama, not defteri, e-Okul). **Kaynak:** vizyon §0, rapor §2 okul satırları.
**Dal:** `feat/p22-siniflar` · **Ön koşul:** P15, P16, P19.

## Global Constraints

- Yeni modülde value object'ler **baştan** (C-08 kararı): `ClassroomName`, `JoinCode`, `AcademicYear`.
- Görünürlük matrisi (K-15) her okuma ucunda **testle** kilitlenir.
- Okul bağlamında öğretmen öğrenci kaydı açamaz / kişisel veri giremez (K-14).

---

### Task 0: Planı ayrıntılandır
- [ ] Students/Assignments/Reporting/Notifications/Membership kodunu oku; aşağıdaki görevleri `Files/Interfaces/Steps` ile yaz; commit.

### Task 1: Ürün dokümanları (vizyon → PRD)
- PRD §1–§2: üç süreç vizyonu, **okul öğretmeni** personası, "tek hesap, ilişki türü" kararı (K-13); §9 paket tablosu: okul öğretmeni ücretsiz (K-16); §10.4 "grup dersleri / kurumsal panel" güncellemesi.
- `doc/roles/ogretmen.md` (okul + özel bağlam), `ogrenci.md` (Sınıfa katıl, Öğretmenlerim), `veli.md` (çocuğun tüm öğretmenleri).
- `doc/modules/m19_classrooms.md` (şablon), `00_genel_bakis.md`, `doc/INDEX.md`, PRD §6 modül tablosu.
- **Kabul:** `kb_healthcheck` RED 0; PRD'de "okul öğretmeni" personası ve görünürlük matrisi var.

### Task 2: Modül iskeleti + domain
- `Classroom { Id, OwnerTeacherUserId, Name, Subject, GradeLevel?, SchoolName?, AcademicYear, JoinCode, JoinCodeRotatedOnUtc, Status (Active|Archived), CreatedOnUtc }` — `RotateJoinCode()`, `Archive()`.
- `ClassroomMembership { ClassroomId, StudentId, Status (PendingParentConsent|Active|Left|Removed), JoinedOnUtc, ShareStudyWithClassTeachers=false }`.
- Adil kullanım tavanı (K-16): `Classrooms:MaxActivePerTeacher=15`, `MaxMembers=60`.
- **Kabul:** domain birim testleri; migration `InitialCreate`; mimari testler yeşil.

### Task 3: Kodla katılım + 18 yaş altı veli onayı (K-14)
- `POST /api/classrooms/join { code }` (yalnız `Student`); deneme limiti (P15 T1 limiter).
- Öğrencinin `DateOfBirth`'ü yoksa katılımda istenir; 18 yaş altı → `PendingParentConsent`; onaylı veli varsa veliye onay bildirimi, yoksa öğrenciye "velini davet et" yönlendirmesi; veli onayı → `Active`. (Model Q10 hukuk teyidine açık; config `Classrooms:RequireParentConsentUnderAge=18`.)
- Öğretmen: üyeleri listele, çıkar; bekleyenleri **yalnız ad** ile görür.
- **Kabul:** yanlış kod 404 + limit; 17 yaşındaki öğrenci veli onayı olmadan `Active` olamaz; öğretmen bekleyen üyenin hiçbir verisini göremez.

### Task 4: `Class` erişim kapsamı + görünürlük matrisi (K-15)
- `ITeacherStudentAccess.GetScopeAsync` → aktif üyelik varsa `Class` (özel bağ varsa `Private` öncelikli).
- Matris (her biri testli): ödev (sınıf ödevleri) ✔ · deneme sonuçları ✔ · bireysel çalışma ✖ (öğrenci açarsa ✔) · ders planı/ücret ✖ · birebir mesaj ✖ · profil iletişim bilgileri ✖ (yalnız ad + sınıf düzeyi).
- **Kabul:** her modülün authorizer'ı `Class` kapsamında matrise uyar; çapraz-kiracı paketi (P15 T9) sınıf senaryolarıyla genişletilir.

### Task 5: Toplu ödev (fan-out)
- Assignments: `GroupAssignment { ClassroomId, Title, Description, DueAtUtc, Attachments }`; oluşturulunca aktif üyeler için `Assignment` (kaynağı `GroupAssignmentId`); güncelleme/iptal yayılır; sonradan katılan üyeye otomatik verilmez (ayar: `includeLateJoiners=false`).
- Sınıf ödev özeti: teslim/geç/eksik sayıları.
- **Kabul:** 30 üyeli sınıfa tek ödev → 30 kayıt, tek tek teslim/notlama mevcut akışla.

### Task 6: Duyuru (K-17)
- `ClassAnnouncement { ClassroomId, Title, Body, CreatedOnUtc }` → üyelere `UserNotification` + push; velilere tercih açıksa. Yanıt yok.
- **Kabul:** duyuru öğrencinin bildirim merkezinde ve sınıf akışında; öğrenci yanıt veremez.

### Task 7: Sınıf panosu projeksiyonu
- Reporting: üye başına ödev tamamlama oranı, son 4 hafta deneme neti trendi, son etkinlik (yalnız paylaşım açıksa), risk bayrakları (2+ eksik ödev, net düşüşü).
- **Kabul:** pano tek sorguda; projeksiyon yeniden kurulabilir.

### Task 8: Limit ayrımı ve öğretmen bağlamı (K-16, K-13)
- `MaxStudents` yalnız `Private` bağları sayar; `TeachingContext` profil alanı + kayıt akışında sorulur (yalnız arayüz varsayılanı).
- **Kabul:** 5 özel öğrencisi olan ücretsiz öğretmen 3 sınıfta 90 öğrenciye sahip olabilir.

### Task 9: Kapanış — sınıf senaryosu
- Gerçek Postgres: öğretmen sınıf açar → 3 öğrenci katılır (biri 16 yaş, veli onaylar) → toplu ödev → 2 teslim → pano; sınıf öğretmeni öğrencinin çalışma geçmişini **göremez**, öğrenci paylaşımı açınca görür.
