# Öğrenci Rolü Çekirdeği — Boşluk Analizi & Tasarım Spec'i

**Tarih:** 2026-07-18
**Kaynak analiz:** `doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md` (S-01…S-15, 14 akış) ↔ kod gerçeği
**Kapsam:** M08 Study (rolün kalbi) ağırlıklı; M03 Students, M04 Scheduling, M06 Assignments, M09 Parents dokunuşları.
**Onaylanan kararlar:** Premium yönetimi evet · yaş/KVKK ertelendi · claim tam merge · streak eşiği ayarlanabilir %.

---

## 1. Amaç

Öğrenci rolünü fonksiyonel dokümanın Faz 2 hedefine ("öğretmensiz de tam işlevsel bireysel çalışma") taşımak. Kod ile doküman arasındaki **yanlış kurgulanmış** ve **eksik** noktalar kapatılır. İş, sıralı dilimlere (Ö-A…Ö-F) bölünür; her dilim ayrı implementasyon planıdır ve tek başına çalışır/test edilebilir.

## 2. Mevcut Durum — Doğru Kurgulanmış (korunacak)

- **Mola net süreye eklenmiyor** (S-08.4): `StudySession.EffectiveMinutes`/`BreakMinutes`.
- **Kişisel not hiçbir role sızmıyor** (AKIŞ 9 değişmez kural): `PersonalNote` yalnız Study modülünde; veli read-model'i taşımıyor; `StudyOwnershipGuard` yalnız öğrenci/admin. **Değiştirmeyin.**
- **Test D+Y+B=Toplam + net** (S-08.12/13); **manuel seans** (S-08.8); **ödev yükleme + öğretmen feedback** (S-06.6/7); **hedefler** (`StudyGoal`); **birleşik takvim** (`StudyScheduleEntry`); **IDOR koruması** (`IStudentDirectory`).

## 3. Boşluklar (özet)

| # | Boşluk | Şiddet | Dilim |
|---|--------|--------|-------|
| B3 | Streak: 1 dk seans günü sayıyor; eşik/04:00/dondurma yok | 🔴 | Ö-A |
| B7 | Tamamlanmış seans/test düzenle-sil yok | 🟠 | Ö-A |
| B4 | Net formülü sınav tipine bağlı değil; profilde hedef sınav yok | 🟠 | Ö-B |
| B6 | Çok dersli deneme sınavı (MockExam) yok | 🟠 | Ö-B |
| B5 | Claim: davet kodu yok, iki profil birleşmiyor | 🔴 | Ö-C |
| B10 | Öğrenci Free/Premium kapıları yönetilmiyor | 🟡 | Ö-D |
| B8 | Arka plan/offline sayaç + kurtarma yok | 🔴 (mobil) | Ö-E |
| B9 | Ders erteleme talebi (öğrenci→öğretmen) yok | 🟡 | Ö-F |
| B1/B2 | Yaş/KVKK + yaş bazlı gizlilik | — | **Ertelendi** |

## 4. Dilim Tasarımları

### Ö-A — Streak kuralları + seans/test düzenle-sil [B3, B7]

- **Streak eşiği:** `StudyCompletionService.RecordCompletedAsync` günü işaretlemeden önce o günün toplam efektif dakikasını hesaplar; gün ancak `günlük_toplam ≥ eşik` ise streak'e sayılır. Eşik = `dailyGoal * StreakThresholdPercent/100` (hedef yoksa 20 dk sabit). `StreakThresholdPercent` `StudyGoal`'a eklenir (varsayılan 60, kullanıcı ayarlar).
- **04:00 gün sınırı:** `StudyLocalTime` streak gün hesabı için 04:00 kaydırma (`StreakDayOffsetHours=4`).
- **Dondurma:** `StudyStreak.FreezesAvailable` + `UseFreeze` (Premium — Ö-D ile bağ). Kırılma kontrolünde donmuş gün atlanır.
- **Proaktif kırılma uyarısı** (S-11.6): Notifications tarafı zamanlanmış kontrol — dün çalıştı, bugün yok → push.
- **Seans/test düzenle-sil:** `StudySession.EditCompleted(...)` + silme; `TestResult` düzenle/sil + net yeniden hesap. Kritik: düzenle/sil sonrası **rollup + streak** o gün için yeniden türetilir (recompute).
- Saf sınıflar: `StreakRules` (eşik, sınır, dondurma), birim testli.

### Ö-B — Hedef sınav + net formülü + deneme sınavı [B4, B6]

- `StudentProfile`'a `enum TargetExam { None, LGS, TYT, AYT, YDS, School, Other }` + alan.
- `ExamPenalty.DivisorFor(examType)` saf fonksiyonu (LGS=3, TYT/AYT=4, School=yanlış götürmez→Net=Doğru). İstemci override edebilir; varsayılan profilden.
- `MockExam` aggregate (`Id, StudentId, ExamType, TakenOnUtc, EstimatedRank?`) + `TestResult.MockExamId?`; `POST /mock-exams` tek işlemde deneme + N test satırı, `TotalNet`.

### Ö-C — Claim: davet kodu + tam profil merge [B5]

- `TeacherStudentLink.InviteCode` (6 hane, tekil, süreli); `MarkInviteSent` üretir. `POST /api/students/links/claim { code }`.
- Claim'de öğrencinin `UserId`'sine bağlı mevcut profil varsa **merge**: kanonik = self-register; manuel profilin modüller-arası referansları (`StudentId`) kanonike taşınır → `StudentProfilesMergedDomainEvent(FromStudentId, ToStudentId)` (Outbox) → Scheduling/Assignments/Payments/LessonSessions kendi kayıtlarını günceller. Manuel profil `Merged` işaretlenir.
- Her zaman öğrenci onayıyla; kod tek kullanımlık.

### Ö-D — Öğrenci Free/Premium yönetimi [B10]

- M17 çekirdeği: `enum MembershipTier { Free, Premium }` + öğrenci abonelik durumu (M17 iskeletten çıkar veya Study içi hafif kapı).
- Kapılar (doküman §14.3): Free = temel kronometre/test/streak-tam/son-30-gün geçmiş/temel haftalık analiz; Premium = sınırsız geçmiş, aylık analiz, hedef net/puan takibi, konu zayıflık, streak dondurma, PDF.
- Enforcement: ilgili query/command'larda tier kontrolü.

### Ö-E — Sayaç güvenilirliği [B8]

- `Complete`/`Pause`'a opsiyonel `clientEffectiveMinutes` (istemci-otoriter, ≤ elapsed doğrulaması).
- `POST /sessions/{id}/recover { effectiveMinutes }` + `GET .../active-session` (mevcut) ile takılı seans kurtarma.
- `Running` + `>6 saat` → `staleWarning`.
- Ağırlıkla mobil.

### Ö-F — Ders erteleme talebi [B9]

- `LessonChangeRequest(Id, LessonScheduleId, StudentId, Reason, ProposedStartAtUtc?, Status)` (Scheduling) + `POST /students/{id}/lesson-requests`; öğretmene bildirim; kabul → mevcut `Reschedule` (Dilim A takvim) çağrısı. Öğrenci yalnız talep eder.

## 5. Test Stratejisi (TDD)

- **Saf fonksiyon birim testleri:** `StreakRules` (eşik, 04:00 sınır, dondurma), `ExamPenalty.DivisorFor` (LGS≠TYT), rollup recompute.
- **Domain:** `StudyStreak` eşik-tabanlı gün; `StudySession.EditCompleted` → recompute; `TeacherStudentLink` kod-claim + merge event; `MockExam` toplam net.
- **Entegrasyon:** claim sonrası veli paneli tek kaynaktan; kişisel notun hiçbir read-model'de görünmemesi (regresyon).

## 6. Doküman Bakımı (her dilim tamamlanınca — KALICI KURAL)

- `doc/modules/m08_study.md`, `m03_students.md`, `m04_scheduling.md`: yeni alan/endpoint/kural.
- `doc/modules/00_genel_bakis.md` endpoint envanteri + durum; `veri_modeli.md` ER (`MockExam`, `LessonChangeRequest`, yeni alanlar).
- `doc/roles/ogrenci.md` §3/§9: ilgili yetenekleri güncelle.

## 7. Kabul Kriterleri (doküman §17 uyarlanmış)

- [ ] Streak kuralları yazılı ve uygulanıyor (eşik = ayarlanabilir %, 04:00 sınır, dondurma).
- [ ] Öğrenci tamamlanmış seans/testi düzenleyip silebiliyor; istatistik tutarlı kalıyor.
- [ ] Net doğru formülle (LGS ≠ TYT); çok dersli deneme girilebiliyor.
- [ ] Öğretmenin eklediği öğrenci claim akışıyla kendi hesabına, veri kaybı olmadan geçebiliyor.
- [ ] Free/Premium kapıları çalışıyor.
- [ ] Kişisel seans notları hiçbir role sızmıyor (regresyon).

---

*Öğrenci Rolü Çekirdeği — Tasarım Spec'i | Güncelleme: 2026-07-18*
