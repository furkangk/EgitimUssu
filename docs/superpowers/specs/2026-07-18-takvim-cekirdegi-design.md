# Dilim A — Takvim Çekirdeği Düzeltmeleri · Tasarım Spec'i

**Tarih:** 2026-07-18
**Kaynak analiz:** `doc/roles/ogretmen.md` §10 (Denetim) · `doc/ogretmen_rolu_fonksiyonel_dokuman_v1.md` §15
**Kapsam:** Öğretmen takvim çekirdeği — M04 (Scheduling) ağırlıklı, M05 (LessonSessions) + M07 (Payments) dokunuşları.
**Onaylanan kararlar:** B-03 için occurrence exception yaklaşımı (a).

---

## 1. Amaç

Öğretmenin günlük kullandığı takvim akışını fonksiyonel dokümanın Faz 1 hedefine taşımak. 6 boşluk kapatılır: B-01 (tatil bloğu), B-02 (erteleme), B-03 (tekrar eden ders occurrence yönetimi), B-08 (gelmedi→ücretlendirme), B-09 (iptal nedeni/sil), B-10 (online link).

**Kapsam dışı** (sonraki dilimler): B-05 not görünürlüğü + ödev onay/geri bildirim (Dilim B) · TeacherStudent ilişki modeli + öğrenci bazlı ücret + arşiv + davet (Dilim C) · M02 çoklu branş + sertifika (Dilim D).

## 2. Mevcut Durum (kod gerçeği)

- `LessonSchedule` aggregate: statü `Draft/Planned/Cancelled/Completed`. Alanlar arasında `RecurrenceRule` (string), `LocationLabel`, `Notes`. Metotlar: `UpdateDetails` (Rescheduled event yayar), `Cancel(cancellationNote)`, `Complete`.
- Tekrar eden dersler **tek satır** olarak tutulur; okuma anında `RecurrenceExpander.Expand(...)` ile **sanal** genişletilir (materyalize edilmez).
- Çakışma kontrolü mevcut: `scheduling.teacher_conflict` (`HasTeacherConflictAsync`).
- Endpoint'ler: `POST /lessons`, `PUT /lessons/{id}`, `POST /lessons/{id}/cancel`, `POST /lessons/{id}/complete`, `GET /lessons/{id}`, `GET /teachers/{id}/lessons`, `GET /students/{id}/lessons`, `GET /students/{id}/calendar`.
- `LessonSession.Complete(...)`: gerçek başlangıç/bitişten süreyi otomatik hesaplar; `StudentAttendanceStatus = Unknown/Attended/Late/Absent`.
- Payments: ders tamamlanınca **otomatik ödeme kaydı yok**; `PaymentRecord` yalnız manuel `POST /records` ile oluşur.

## 3. Tasarım

### 3.1 B-03 — Occurrence exception modeli (yaklaşım a)

Sanal genişletme korunur; tek-oturum işlemleri için istisna tablosu eklenir.

- Yeni entity `LessonOccurrenceException`:
  - `Id`, `SeriesLessonScheduleId` (temel satıra FK), `OriginalStartAtUtc` (RECURRENCE-ID), `Action` (`Skipped | Cancelled | Rescheduled`), `OverrideStartAtUtc?`, `OverrideEndAtUtc?`, `Note?`, `CreatedOnUtc`.
- `RecurrenceExpander` genişletme sırasında istisnaları uygular:
  - `Skipped` → occurrence üretilmez.
  - `Cancelled` → occurrence "iptal" durumuyla görünür (takvimde soluk).
  - `Rescheduled` → occurrence override tarih/saatle üretilir.
- Kapsam seçimi (`scope` parametresi: `single | thisAndFuture | all`):
  - `single` → ilgili işlem için `LessonOccurrenceException` satırı eklenir.
  - `thisAndFuture` → seri bölünür: temel satırın `RecurrenceRule` bitişi seçilen tarihten önce sonlandırılır; seçilen tarihten itibaren yeni ayarlarla yeni `LessonSchedule` (yeni seri) oluşturulur.
  - `all` → temel satır `UpdateDetails`/`Cancel` ile düzenlenir.

Etkilenen komut/endpoint'ler `scope` alır: `PUT /lessons/{id}`, `POST /lessons/{id}/cancel`, `POST /lessons/{id}/reschedule`. `scope` yalnız `RecurrenceRule` dolu satırlarda anlamlıdır; tek seferlik derste yok sayılır (default `single`/`all` eşdeğer).

### 3.2 B-01 — Tatil / müsait değil bloğu

- Yeni aggregate `TimeOffBlock`:
  - `Id`, `TeacherUserId`, `Type` (`Holiday | Leave | Official | Other`), `Title`, `StartAtUtc`, `EndAtUtc`, `IsAllDay`, `DailyStartTime?`, `DailyEndTime?` (saat aralığı modu), `CreatedOnUtc`.
- Endpoint'ler:
  - `POST /teachers/{teacherUserId}/time-off` → oluşturur; yanıtta **çakışan planlı dersler** listesi döner (`conflictingLessons[]`).
  - `GET /teachers/{teacherUserId}/time-off?startAtUtc=&endAtUtc=`
  - `DELETE /teachers/{teacherUserId}/time-off/{id}`
- Çakışan ders kararı istemci tarafından ayrı çağrılarla uygulanır (mevcut `cancel`/yeni `reschedule`). Backend tatil oluşturmayı çakışmadan bağımsız tamamlar (blok her hâlde kaydedilir).
- Ders eklerken tatil çakışması → **uyarı** döner (engel değil): `CreateLessonScheduleCommand` sonucunda `timeOffWarning` bilgisi.
- Takvim genişletmesi (`/calendar`, `/teachers/.../lessons`) tatil bloklarını ayrı bir alanda döndürür (dersle karışmaz).

### 3.3 B-02 — Ders erteleme

- Domain: `LessonSchedule.Reschedule(newStartAtUtc, newEndAtUtc, note, updatedOnUtc)`:
  - Statü `Planned` kalır (doküman §12.1). `OriginalStartAtUtc` ilk kez ertelemede saklanır; `RescheduleNote` güncellenir.
  - Mevcut `LessonScheduleRescheduledDomainEvent` yayılır (Notifications hattı öğrenci/veliye bildirir).
- Endpoint: `POST /lessons/{id}/reschedule` (body: `newStartAtUtc`, `newEndAtUtc`, `note?`, `scope?`). Çakışma kontrolü çalışır.
- Düzenlemeden ayrı tutulur: erteleme bildirim + geçmiş üretir; salt konu düzenlemesi üretmez.

### 3.4 B-09 — İptal nedeni + Sil ayrımı

- `LessonSchedule.Cancel` genişletilir: `CancellationReason` (`TeacherCancelled | StudentCancelled | Holiday | Other`) + `IsChargeable` (bool) alanları eklenir.
- `CancelLessonScheduleCommand`'a `reason`, `isChargeable`, `scope` eklenir.
- Yeni `DELETE /lessons/{id}`: yalnız (oluşturmadan sonra ≤24 saat) **ve** (ders gelecekte) ise izinli; aksi halde `scheduling.delete_not_allowed` hatası → iptal önerilir. Silmede bildirim gitmez, kayıt tamamen kaldırılır.
- İptal edilen ders silinmez; statü `Cancelled`, takvimde soluk/geçmiş.

### 3.5 B-08 — Gelmedi → ücretlendirme kararı

- `LessonSession`'a `IsChargeable` (bool) alanı; `Complete(...)` imzasına `bool isChargeable` eklenir (özellikle `Absent`'ta anlamlı).
- Ödeme kaydı **mevcut manuel akışta** kalır (otomatik oluşturma bu dilimde yok). Alan audit + ileride rapor/desen içindir.
- İlgili complete endpoint'i (`POST /sessions/{id}/complete` / `/{lessonSessionId}/complete`) yeni alanı kabul eder.

### 3.6 B-10 — Online link semantiği

- `LessonSchedule`'a `MeetingUrl` (string?) alanı; `LocationLabel` yüz yüze adres için kalır.
- `Create`/`UpdateDetails`/`Reschedule` `MeetingUrl` taşır. Format `Online|Hybrid`'de link, `InPerson`'da adres öne çıkar (istemci gösterimi).
- Oturuma taşınması opsiyonel (bu dilimde şart değil).

## 4. Kesişen Konular

- **Migration:** Her yeni alan/enum/entity için Scheduling + LessonSessions şemalarına ayrı migration.
- **Event/Outbox:** Mevcut domain event → Integration event (Outbox) → Notifications hattı korunur; reschedule/cancel bildirimleri buradan.
- **Yetki:** Tüm yeni endpoint'ler mevcut sahiplik kuralına tabi (öğretmen yalnız kendi derslerine/bloklarına). IDOR koruması korunur.

## 5. Test Stratejisi (TDD)

Her davranış için önce test:
- `RecurrenceExpander` + occurrence exception: skip/cancel/reschedule uygulanışı; `thisAndFuture` seri bölme; `all` düzenleme.
- `Reschedule` domain metodu: statü korunur, OriginalStart ilk ertelemede sabitlenir, event yayılır.
- `Cancel`: reason/isChargeable set; scope davranışı.
- `DELETE` guard: 24 saat + gelecek kuralı sınır durumları.
- `TimeOffBlock`: çakışan ders taraması; all-day vs saat aralığı; ders eklerken uyarı.
- `LessonSession.Complete`: `Absent + isChargeable` kombinasyonları.
- Endpoint entegrasyon testleri (Testcontainers Postgres + Redis — mevcut altyapı) kritik akışlar için.

## 6. Doküman Bakımı (tamamlanınca — KALICI KURAL)

- `doc/modules/m04_scheduling.md`, `m05_lesson_sessions.md`: yeni domain alanları + endpoint'ler + iş kuralları.
- `doc/modules/00_genel_bakis.md`: endpoint envanteri + M04 durumu (🟡→🟢).
- `doc/modules/veri_modeli.md`: `LessonOccurrenceException`, `TimeOffBlock`, yeni alanlar ER.
- `doc/modules/mimari_inceleme.md`: ilgili açık maddeleri "✅ Düzeltildi".
- `doc/roles/ogretmen.md` §10: B-01/B-02/B-03/B-08/B-09/B-10 durumları güncelle; Kabul Kriterleri işaretle.
- `doc/pages/...`: ilgili takvim ekranı md'leri (mobil eklenirse).

## 7. Kabul Kriterleri (Dilim A)

- [ ] Tekrar eden dersin **tek oturumu** seriden bağımsız iptal/ertele edilebiliyor (scope: single).
- [ ] "Bu ve sonrakiler" seçimi seriyi doğru bölüyor; "tümü" tabanı düzenliyor.
- [ ] Tatil bloğu eklenebiliyor; çakışan planlı dersler yanıtta listeleniyor; derse tatil çakışması uyarı veriyor (engel değil).
- [ ] Ders `POST /reschedule` ile taşınıyor, statü Planned kalıyor, öğrenciye bildirim gidiyor, erteleme geçmişi tutuluyor.
- [ ] İptal nedeni + ücretlendirme kaydediliyor; 24 saat/gelecek kuralı dışında silme reddediliyor.
- [ ] Oturum tamamlamada `Absent + isChargeable` kaydediliyor.
- [ ] `MeetingUrl` ayrı alan olarak set/edit edilebiliyor.
- [ ] İlgili birim + entegrasyon testleri yeşil.
