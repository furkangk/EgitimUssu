---
title: "M04 — Takvim & Planlama (Scheduling)"
summary: "Öğretmenin merkezi takvim aracı: planlama/erteleme/iptal/tatil/occurrence istisnaları + birleşik öğrenci takvimi kodda çalışır durumda"
tags: [modul, scheduling, takvim, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Scheduling/**
updated: 2026-09-02
---

# 📅 Takvim & Planlama (M04) — Detaylı Tasarım Dokümanı

> **PRD: M04 Takvim & Planlama** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (çekirdek), ⚠️ genişletme bekliyor**
>
> **Amaç:** Öğretmenin **her gün** açtığı **merkezi ekranı**: takvim. Öğretmen derslerini tek seferlik
> veya tekrarlı olarak planlar; ders türünü (online / yüz yüze / hibrit) seçer; çakışmalar engellenir;
> yaklaşan ders için hatırlatma planlanır. Takvim ileride **dersler + ödevler + tatiller + ödemeler**i
> tek bir görünümde toplayacak şekilde tasarlanmıştır.
>
> İlgili: [`../roles/ogretmen.md`](../roles/ogretmen.md) (takvim öğretmenin merkezi aracı) ·
> [`../roles/ogrenci.md`](../roles/ogrenci.md) (öğrenci kendi planı + öğretmen takvimi) ·
> [`m05_lesson_sessions.md`](m05_lesson_sessions.md) (planlı dersten oturum türetme) ·
> [`m11_notifications.md`](m11_notifications.md) (hatırlatma) · [`00_genel_bakis.md`](00_genel_bakis.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`LessonSchedule`) | ✅ Mevcut | `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` |
| Application (CQRS + handler) | ✅ Mevcut | `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` |
| API endpoint'leri | ✅ Mevcut (planla/güncelle/iptal/ertele/tamamla/sil/getir/öğretmen-takvimi/öğrenci-dersleri + öğrenci **birleşik takvim** + kişisel ders **ekle/güncelle/sil** + tatil blokları + **erteleme talebi** aç/listele/kabul/red) | `src/Modules/Scheduling/API/SchedulingModule.cs` |
| Infrastructure (DbContext + repo + migration) | ✅ Mevcut | `src/Modules/Scheduling/Infrastructure/*` |
| Çakışma kontrolü (aynı öğretmen) | ✅ **Mevcut** | `HasTeacherConflictAsync` → `scheduling.teacher_conflict` (409) |
| Hatırlatma planlama | ✅ Mevcut | **Olay tabanlı (2026-07-01, Y1):** `LessonScheduledDomainEvent` → outbox → Notifications handler. Senkron `ILessonScheduleNotificationService` **kaldırıldı**; Scheduling artık Notifications'a doğrudan yazmaz |
| Mobil takvim ekranı | ✅ Mevcut | `mobile/lib/features/scheduling` (`syncfusion_flutter_calendar`) |
| Online ders linki (`MeetingUrl`) | 🟢 **Mevcut (2026-07-18, B-10)** | Ayrı `MeetingUrl` alanı; create/update request + response taşır. Migration `AddLessonMeetingUrl` |
| Ders erteleme (`Reschedule`) | 🟢 **Mevcut (2026-07-18, B-02)** | `Reschedule()` domain metodu + `POST /lessons/{id}/reschedule`; statü `Planned` kalır, `OriginalStartAtUtc`/`RescheduleNote` erteleme geçmişi tutar, Rescheduled event yayılır |
| **Öğrenci ders erteleme talebi** (`LessonChangeRequest`) | 🟢 **Mevcut (2026-07-18, Ö-F)** | Yeni hafif aggregate + `POST /students/{id}/lesson-requests` (öğrenci talep) · `GET /teachers/{id}/lesson-requests` · `POST /lesson-requests/{id}/accept` (kabulde **mevcut** Reschedule çalışır) · `/reject`. Öğrenci **dersi kendisi değiştirmez**, yalnızca talep eder. Migration `AddLessonChangeRequests` |
| İptal nedeni + ücretlendirme (`CancellationReason`, `IsChargeable`) | 🟢 **Mevcut (2026-07-18, B-09)** | `Cancel()` genişletildi; iptal nedeni (enum) + ücretlendirme kararı saklanır |
| Ders silme (24s + gelecek) | 🟢 **Mevcut (2026-07-18, B-09)** | `CanBeDeletedAt()` + `DELETE /lessons/{id}`; yalnız oluşturmadan ≤24 saat **ve** ders gelecekteyse, aksi halde `scheduling.delete_not_allowed` (409) |
| Tatil / müsait değil bloğu (`TimeOffBlock`) | 🟢 **Mevcut (2026-07-18, B-01)** | Yeni aggregate + `POST/GET/DELETE /teachers/{id}/time-off`; oluşturmada çakışan planlı dersler yanıtta döner. Migration `AddTimeOffBlocks` |
| Tekrar kuralı (`RecurrenceRule`) **açılımı** | 🟢 **Mevcut (öğrenci takviminde + occurrence istisnaları)** | `RecurrenceExpander` (DAILY/WEEKLY+BYDAY/MONTHLY + UNTIL) → `GET /students/{id}/calendar` somut oluşumları üretir; **occurrence istisnaları** (`LessonOccurrenceException`: skip/cancel/reschedule) uygulanır (2026-07-18, B-03). Öğretmen `LessonSchedule` listesi hâlâ açılımsız (tek örnek) |
| Öğrenci kişisel programı (**birleşik `LessonSchedule`**) | 🟢 **Mevcut (2026-07-19, Ç-06)** | Ayrı `StudyScheduleEntry` **kaldırıldı**; öğrencinin kendi dersi birleşik `LessonSchedule`'da `TeacherUserId=null` (self) olarak tutulur. `CreateSelfLesson` fabrikası + `CreateSelfLesson/UpdateSelfLesson/DeleteSelfLesson` komut yolu. `/students/{id}/study-entries` (POST) · `PUT/DELETE /study-entries/{id}` rotaları korunur (mobil geriye-uyum), self-lesson komutlarına köprülenir. Öğretmen dersiyle saat çakışması reddedilir. Migration `UnifyLessonSchedule` (veri göçü + tablo drop) |
| Birleşik takvim `completed` (planla→çalış→✓) | 🟢 **Mevcut (2026-07-19, Ç-06)** | `GET /students/{id}/calendar` occurrence'ına `completed` alanı eklendi; `IStudyPlanCompletionReader` (Shared/Contracts) ile Study'den (LessonId'ye bağlı tamamlanmış seanslar) doldurulur. `source`/`isEditable`/`topic`/`colorHex` tek kaynaktan (`lesson_schedules`) türetilir |
| Öğrenci programı **hatırlatması** | 🟢 **Mevcut (2026-07-19, Ç-06 revizyon)** | Kendi ders hatırlatması artık `LessonScheduledDomainEvent` (teacher null) → Notifications `LessonScheduleNotificationIntegrationEventHandler` üzerinden üretilir; ayrı `StudyScheduleReminderIntegrationEventHandler` **kaldırıldı**. Bkz. §5 |
| Ders güncelleme (`PUT /lessons/{id}`) | ✅ **Mevcut** | `UpdateDetails()` domain metodu + `PUT /lessons/{id}` + `UpdateLessonScheduleCommand`; kendini hariç tutan çakışma kontrolü + hatırlatma yeniden planlanır; yalnızca Planli/Taslak düzenlenebilir (2026-06-28) |
| `Planned → Completed` geçişi | ✅ **Mevcut** | `Complete()` domain metodu + `POST /lessons/{id}/complete` + `CompleteLessonScheduleCommand` (2026-06-26) |

> **Önemli düzeltme:** Önceki dokümanda "çakışma kontrolü eksik" yazıyordu; **kodda `HasTeacherConflictAsync`
> ile uygulanmıştır** ve `POST /lessons` sırasında devrededir. Eksik olan, öğrenci tarafındaki **öncelik
> kuralı** (özel ders > bireysel plan) ve görsel uyarıdır.

---

## 2. Domain Modeli

### 2.1 🟢 Mevcut (koddan) — `LessonSchedule` (AggregateRoot<Guid>)

`src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Ders planı kimliği |
| `TeacherUserId` | `Guid?` | Dersi planlayan öğretmenin kullanıcı kimliği. **Ç-06:** öğrencinin kendi (self) dersinde `null` |
| `StudentId` | `Guid` | Hedef öğrenci profil kimliği |
| `Subject` | `string` | Konu / branş |
| `LessonFormat` | enum `ScheduledLessonFormat?` | `InPerson=1`, `Online=2`, `Hybrid=3` (nullable — self derste boş olabilir) |
| `Topic` | `string?` | **Ç-06.** Konu (self ders birleştirmesiyle geldi; mobil formda toplanmaz — rezerve) |
| `ColorHex` | `string?` | **Ç-06.** Takvim renk kodu (self ders için) |
| `StartAtUtc` | `DateTime` | Başlangıç (UTC) |
| `EndAtUtc` | `DateTime` | Bitiş (UTC) |
| `TimeZone` | `string` | IANA zaman dilimi (gösterim için) |
| `RecurrenceRule` | `string?` | Tekrar kuralı (RRULE benzeri metin) — **yalnızca saklanır** |
| `Status` | enum `LessonScheduleStatus` | `Draft=1`, `Planned=2`, `Cancelled=3`, `Completed=4` |
| `ReminderOffsetMinutes` | `int` | Hatırlatmanın dersten kaç dk önce gönderileceği |
| `LocationLabel` | `string?` | Konum etiketi (yüz yüze için adres/etiket) |
| `MeetingUrl` | `string?` | **Online ders linki (B-10, 2026-07-18).** `LessonFormat = Online/Hybrid` için; `LocationLabel` yüz yüze adresi için kalır |
| `Notes` | `string?` | Serbest not (iptal notu da buraya eklenir) |
| `OriginalStartAtUtc` | `DateTime?` | **Erteleme geçmişi (B-02, 2026-07-18).** İlk ertelemede özgün başlangıç saklanır |
| `RescheduleNote` | `string?` | **Erteleme notu (B-02, 2026-07-18).** Son erteleme açıklaması |
| `CancellationReason` | enum `CancellationReason?` | **İptal nedeni (B-09, 2026-07-18).** `TeacherCancelled/StudentCancelled/Holiday/Other` |
| `IsChargeable` | `bool` | **Ücretlendirme kararı (B-09, 2026-07-18).** İptal edilen ders ücretlendirilecek mi |
| `CreatedOnUtc` | `DateTime` | Oluşturma |
| `UpdatedOnUtc` | `DateTime` | Son güncelleme (iptalde güncellenir) |

**Davranışlar (kodda):**
- **Constructor** → durum dışarıdan verilir; `CreateLessonScheduleCommandHandler` her zaman `LessonScheduleStatus.Planned` ile çağırır. Oluşturmada `LessonScheduledDomainEvent` yayılır.
- `Reschedule(newStartAtUtc, newEndAtUtc, note, updatedOnUtc)` (**B-02, 2026-07-18**) → dersi yeni tarih/saate taşır, statü `Planned` kalır, ilk ertelemede `OriginalStartAtUtc` saklanır, `RescheduleNote` güncellenir, `LessonScheduleRescheduledDomainEvent` yayılır.
- `Cancel(reason, isChargeable, cancellationNote, updatedOnUtc)` (**B-09 ile genişletildi, 2026-07-18**) → `Status = Cancelled`, iptal nedeni + ücretlendirme kararı saklanır, not eklenir (varsa mevcut nota satır eklenir), `LessonScheduleCancelledDomainEvent` yayılır.
- `CanBeDeletedAt(nowUtc)` (**B-09, 2026-07-18**) → silme yalnız oluşturmadan ≤24 saat **ve** ders gelecekteyse `true`.
- `EndSeriesBefore(cutoffUtc, updatedOnUtc)` (**B-03, 2026-07-18**) → tekrar serisini verilen tarihten önce sonlandırır (`RecurrenceRule`'a `UNTIL` ekler/günceller). "Bu ve sonrakiler" iptali için.
- `Complete(updatedOnUtc)` → `Status = Completed`, `LessonSessionCompletedDomainEvent` yayılır. Zaten `Completed` ise `scheduling.already_completed (409)` döner.

**Enum'lar (koddan birebir):**
```
ScheduledLessonFormat : InPerson = 1, Online = 2, Hybrid = 3
LessonScheduleStatus  : Draft = 1, Planned = 2, Cancelled = 3, Completed = 4
CancellationReason    : TeacherCancelled = 1, StudentCancelled = 2, Holiday = 3, Other = 4   (B-09)
OccurrenceScope       : Single = 1, ThisAndFuture = 2, All = 3   (B-03, cancel/reschedule kapsamı)
```

### 2.1.1 🟢 Mevcut (koddan) — `TimeOffBlock` (AggregateRoot<Guid>) — B-01, 2026-07-18

`src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`. Öğretmenin müsait olmadığı gün/aralık.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `TeacherUserId` | `Guid` | Sahibi |
| `Type` | enum `TimeOffType` | `Holiday=1, Leave=2, Official=3, Other=4` |
| `Title` | `string` | "Yaz tatili", "İzin" vb. |
| `StartAtUtc`, `EndAtUtc` | `DateTime` | Blok aralığı (UTC) |
| `IsAllDay` | `bool` | Tüm gün mü |
| `CreatedOnUtc` | `DateTime` | |

> YAGNI: günlük saat aralığı modu (`DailyStartTime/EndTime`) bu sürümde dışta; blok tam-gün/aralık penceresiyle modellenir. Çakışma taraması `LessonSchedule` zaman kesişimidir.

### 2.1.2 🟢 Mevcut (koddan) — `LessonOccurrenceException` (Entity<Guid>) — B-03, 2026-07-18

Tekrar serisinde tek bir oluşuma uygulanan istisna (iCal `EXDATE`/`RECURRENCE-ID` deseni). Sanal genişletme korunur; tek-oturum işlemleri bu tabloyla çözülür.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `SeriesLessonScheduleId` | `Guid` | Temel `LessonSchedule` satırına (seri) FK |
| `OriginalStartAtUtc` | `DateTime` | Hedef oluşum (RECURRENCE-ID) |
| `Action` | enum `OccurrenceExceptionAction` | `Skipped=1, Cancelled=2, Rescheduled=3` |
| `OverrideStartAtUtc`, `OverrideEndAtUtc` | `DateTime?` | Rescheduled için yeni tarih/saat |
| `Note` | `string?` | |
| `CreatedOnUtc` | `DateTime` | |

`RecurrenceExpander.Expand(...)` 6-argümanlı overload'u istisnaları uygular: `Skipped` → oluşum atlanır; `Cancelled` → `IsCancelled=true` ile döner (takvimde gizlenir/soluk); `Rescheduled` → override tarih/saatle döner.

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `LessonScheduledDomainEvent` | `LessonScheduleId, TeacherUserId (Guid? — Ç-06: self derste null), StudentId, StartAtUtc, EndAtUtc, ReminderOffsetMinutes, CreatedOnUtc` (2026-07-01: `ReminderOffsetMinutes` eklendi — Notifications handler'ı offset'i buradan alır, Y1) |
| `LessonScheduleCancelledDomainEvent` | `LessonScheduleId, TeacherUserId (Guid? — Ç-06), StudentId, CancelledOnUtc` |
| `LessonSessionCompletedDomainEvent` | `LessonScheduleId, TeacherUserId, StudentId, CompletedOnUtc` |
| `LessonChangeRequestedDomainEvent` | `RequestId, LessonScheduleId, StudentId, TeacherUserId, CreatedOnUtc` (Ö-F: öğretmene "yeni erteleme talebi" bildirimi için) |
| `LessonChangeRequestResolvedDomainEvent` | `RequestId, LessonScheduleId, StudentId, TeacherUserId, Status, ResolvedOnUtc` (Ö-F: öğrenciye "talebiniz kabul/red edildi" bildirimi için) |

### 2.1.3 🟢 Mevcut (koddan) — `LessonChangeRequest` (AggregateRoot<Guid>) — Ö-F, 2026-07-18

`src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`. Öğrencinin bir dersin ertelenmesi için açtığı **hafif talep**.
Öğrenci **dersi kendisi değiştirmez** (yetki matrisi #1 korunur); yalnızca neden + isteğe bağlı alternatif tarih önerir.
Öğretmen kabul ederse **mevcut** `RescheduleLessonScheduleCommand` çalışır; redde talep kapanır.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `LessonScheduleId` | `Guid` | Ertelenmesi istenen ders (`LessonSchedule`) |
| `StudentId` | `Guid` | Talebi açan öğrenci (dersin öğrencisi olmalı) |
| `TeacherUserId` | `Guid` | Dersin öğretmeni (dersten okunur, öğrenci veremez) |
| `Reason` | `string` | Erteleme nedeni (maxlen 500, zorunlu) |
| `ProposedStartAtUtc`, `ProposedEndAtUtc` | `DateTime?` | Önerilen alternatif tarih; ya ikisi birlikte ya da ikisi de boş |
| `Status` | enum `LessonChangeRequestStatus` | `Pending=1, Accepted=2, Rejected=3` |
| `CreatedOnUtc` | `DateTime` | |
| `ResolvedOnUtc` | `DateTime?` | Kabul/redde işaretlenir |

**Davranışlar:** `Accept(nowUtc)` / `Reject(nowUtc)` — **yalnızca `Pending`'den** geçiş yapar; aksi halde `InvalidOperationException` (handler'da `scheduling.request_not_pending` 409'a çevrilir). İkisi de `LessonChangeRequestResolvedDomainEvent` yayar; ctor `Pending` + `LessonChangeRequestedDomainEvent`.

### 2.2 ⚠️ Önerilen (henüz kodda yok)

Aşağıdakiler `promp.txt` ve [`../roles/ogretmen.md`](../roles/ogretmen.md) hedeflerinden türetilmiştir; **kodda yoktur**.

#### A) `LessonSchedule`'a eklenecek alanlar
> Not: `MeetingUrl` **artık kodda** (B-10, §2.1) — bu tablodan çıkarıldı.

| Alan (öneri) | Tip | Gerekçe |
|--------------|-----|---------|
| `MeetingProvider` | `string?` | Zoom/Meet/Teams/diğer (görüntüleme/ikon için). |
| `SeriesId` | `Guid?` | Tekrarlı seriye ait örnek (instance) ise serinin kimliği — açılım için. |
| `CancelledOnUtc` | `DateTime?` | İptal zamanını ayrı tutmak (şu an yalnızca `UpdatedOnUtc`). |

#### B) `ScheduleException` / `Holiday` (yeni Aggregate — tatil / blackout)
Öğretmenin **müsait olmadığı** gün/aralıkları işaretler. Takvimde "tatil" bloğu olarak görünür; bu aralıkta ders planlama uyarı verir.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `TeacherUserId` | `Guid` | Sahibi |
| `Title` | `string` | "Resmî tatil", "İzin", "Yurt dışı" vb. |
| `StartAtUtc`, `EndAtUtc` | `DateTime` | Blackout aralığı |
| `IsAllDay` | `bool` | Tüm gün mü |
| `Kind` | enum (öneri) | `Holiday`, `PersonalBlock`, `Break` |
| `Notes` | `string?` | |

#### C) `LessonScheduleSeries` (tekrarlı ders açılımı)
`RecurrenceRule` şu an yalnızca metin olarak saklanıyor; **somut örnekler üretilmiyor**. Öneri: bir seri oluşturulduğunda kural çözümlenip ufuk (örn. 8–12 hafta) kadar `LessonSchedule` örneği materyalize edilir. Her örnek `SeriesId` taşır; "tek örneği iptal et" / "tüm seriyi iptal et" ayrımı yapılabilir.

> **Tasarım kararı:** Materyalize yaklaşım (örnekleri önceden üret) çakışma kontrolü, hatırlatma ve takvim
> sorgularını basitleştirir. Alternatif (uçuş anında genişletme) daha az satır ama daha karmaşık sorgu demektir.

### 2.3 ⛔ KALDIRILDI (Ç-06, 2026-07-19) — eski `StudyScheduleEntry` aggregate → birleşik `LessonSchedule`

> **Kod gerçeği (2026-08-19 doğrulandı):** `StudyScheduleEntry` aggregate ve `StudyScheduleModel.cs` dosyası **artık yoktur**.
> `enum StudyScheduleEntryStatus` de kodda kalmadı. Öğrencinin kendi dersi Ç-06 ile birleşik `LessonSchedule`'a taşındı
> (`TeacherUserId = null` = self; `Topic`/`ColorHex` alanları §2.1'e eklendi). `UnifyLessonSchedule` migration'ı veriyi göç ettirip tabloyu düşürdü.
> API tarafında yalnızca `/study-entries` **rotaları** (POST/PUT/DELETE) geriye-uyum köprüsü olarak korunur; bunlar self-lesson komutlarına yönlenir (bkz. §3.1).
> Aşağıdaki tablo **tarihsel** referans içindir; artık koda karşılık gelmez.

Eski (kaldırılmış) `StudyScheduleEntry`: öğrencinin **kendi oluşturduğu** kişisel ders/çalışma programı girdisi.
Sahiplik `StudentId` üzerindendi; yalnızca sahibi öğrenci (veya admin) yönetirdi.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `StudentId` | `Guid` | Sahibi (öğrenci profil kimliği) |
| `Subject` | `string` | Ders adı |
| `Topic` | `string?` | Konu (opsiyonel; backend'de mevcut ama mobil formda artık toplanmıyor — rezerve) |
| `StartAtUtc`, `EndAtUtc` | `DateTime` | İlk oluşum (UTC) |
| `TimeZone` | `string` | IANA zaman dilimi |
| `RecurrenceRule` | `string?` | iCal benzeri kural (`FREQ=DAILY/WEEKLY;BYDAY=…;UNTIL=…`). Boşsa tek seferlik. **Açılım yapılır** (bkz. `RecurrenceExpander`) |
| `ReminderOffsetMinutes` | `int` | Hatırlatmanın dersten kaç dk önce planlanacağı; `0` = kapalı. **Bağlı** → outbox → m11 Notifications (bkz. §5) |
| `ColorHex` | `string?` | Takvim renk kodu (ör. `#20A4A9`); backend'de mevcut ama mobil formda artık toplanmıyor — kendi dersleri sabit renkle (teal) gösterilir |
| `Notes` | `string?` | Serbest not |
| `Status` | enum `StudyScheduleEntryStatus` | `Active=1`, `Cancelled=2` (silme = soft-cancel) |
| `CreatedOnUtc`, `UpdatedOnUtc` | `DateTime` | |

**Davranışlar:** `UpdateDetails(...)`, `Cancel(updatedOnUtc)`. **Domain event'ler (2026-07-08):**
`StudyScheduleEntryScheduledDomainEvent` (oluşturmada), `StudyScheduleEntryRescheduledDomainEvent` (güncellemede),
`StudyScheduleEntryCancelledDomainEvent` (silmede/soft-cancel) — hepsi outbox ile m11 Notifications'a taşınır (hatırlatma, §5).

**Tekrar açılımı — `RecurrenceExpander`** (`src/Modules/Scheduling/Application/RecurrenceExpander.cs`): kural + ilk
oluşumu alıp `[rangeStart, rangeEnd]` penceresine düşen somut oluşumları üretir (`FREQ=DAILY/WEEKLY/MONTHLY`,
`BYDAY`, `UNTIL`). Aritmetik UTC instant üzerinden yapılır (Türkiye DST uygulamadığı için yerel duvar-saati korunur).
Hem öğrenci girdileri hem öğretmen dersleri **birleşik takvim** sorgusunda bu genişleticiyle işlenir.

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `group.RequireAuthorization("AuthenticatedUser")` ile korunur ve `Result<T>` döner.
> Hata kodu → HTTP eşlemesi `SchedulingModule.ToHttpResult` içinde yapılır.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Ders planla | `POST /api/scheduling/lessons` | `CreateLessonScheduleRequest` → `LessonScheduleResponse` | Çakışma/aralık kontrolü uygulanır; durum `Planned` set edilir; hatırlatma planlanır |
| Ders güncelle | `PUT /api/scheduling/lessons/{lessonId}` | `UpdateLessonScheduleRequest` → `LessonScheduleResponse` | Konu/zaman/format/tekrar/hatırlatma/konum/not değişimi. Kendini hariç tutan çakışma kontrolü (`409 teacher_conflict`); aralık `400 invalid_range`; yalnızca Planlı/Taslak (`409 scheduling.not_editable`); hatırlatma yeniden planlanır |
| Ders ertele | `POST /api/scheduling/lessons/{lessonId}/reschedule` | `RescheduleLessonScheduleRequest { NewStartAtUtc, NewEndAtUtc, Note?, Scope?, OccurrenceStartAtUtc? }` → `LessonScheduleResponse` | **B-02, 2026-07-18.** Statü `Planned` kalır; erteleme geçmişi + Rescheduled event; çakışma kontrolü. Tekrarlı derste `Scope=Single` → tek oturum istisnası yazılır (temel satır bozulmaz) |
| Ders iptal | `POST /api/scheduling/lessons/{lessonId}/cancel` | `CancelLessonScheduleRequest { Reason, IsChargeable, CancellationNote?, Scope?, OccurrenceStartAtUtc? }` → `LessonScheduleResponse` | **B-09/B-03 ile genişletildi, 2026-07-18.** İptal nedeni + ücretlendirme saklanır. Tekrarlı derste `Scope=Single` → occurrence iptali istisna satırı; `Scope=ThisAndFuture` → seri `UNTIL` ile kısaltılır |
| Ders sil | `DELETE /api/scheduling/lessons/{lessonId}` | (gövde yok) → 204 | **B-09, 2026-07-18.** Yalnız oluşturmadan ≤24 saat **ve** ders gelecekteyse; aksi halde `409 scheduling.delete_not_allowed`. Silmede bildirim gitmez, kayıt kaldırılır |
| Ders tamamla | `POST /api/scheduling/lessons/{lessonId}/complete` | (gövde yok) → `LessonScheduleResponse` | `Complete()` → `LessonSessionCompletedDomainEvent`; zaten tamamsa `409 scheduling.already_completed` |
| **Tatil bloğu ekle** | `POST /api/scheduling/teachers/{teacherUserId}/time-off` | `CreateTimeOffBlockRequest { Type, Title, StartAtUtc, EndAtUtc, IsAllDay }` → `CreateTimeOffResponse { Block, ConflictingLessons[] }` | **B-01, 2026-07-18.** Blok kaydedilir; çakışan planlı dersler yanıtta listelenir (istemci ayrı çağrıyla iptal/ertele uygular) |
| **Tatil bloklarını listele** | `GET /api/scheduling/teachers/{teacherUserId}/time-off?startAtUtc=&endAtUtc=` | → `TimeOffBlockResponse[]` | **B-01, 2026-07-18** |
| **Tatil bloğu sil** | `DELETE /api/scheduling/teachers/{teacherUserId}/time-off/{timeOffId}` | → 204 | **B-01, 2026-07-18.** Yoksa `404 scheduling.timeoff_not_found` |
| Ders getir | `GET /api/scheduling/lessons/{lessonId}` | → `LessonScheduleResponse` | Yoksa `404 scheduling.lesson_not_found` |
| Takvim (aralık) | `GET /api/scheduling/teachers/{teacherUserId}/lessons?startAtUtc=&endAtUtc=` | → `LessonScheduleResponse[]` | **Tarih aralığı filtresi MEVCUT**; sonuç `StartAtUtc` artan sıralı |
| Öğrenci takvimi | `GET /api/scheduling/students/{studentId}/lessons?startAtUtc=&endAtUtc=` | → `LessonScheduleResponse[]` | **Öğrenci kendi dersleri** (2026-07-07). Sahiplik `IStudentDirectory` (Students'ın yayınladığı Shared.Contracts sözleşmesi) ile doğrulanır: admin her zaman, aksi halde `Student.UserId == currentUser`; başkasının `studentId`'si `403 shared.forbidden` (IDOR koruması). Scheduling, Students'a proje referansı vermez |
| **Birleşik takvim** | `GET /api/scheduling/students/{studentId}/calendar?startAtUtc=&endAtUtc=` | → `StudentCalendarOccurrenceResponse[]` | **2026-07-08.** Öğretmen dersleri + öğrencinin kendi programı, tekrarlar `RecurrenceExpander` ile aralığa **genişletilmiş** olarak birlikte döner. Her occurrence `Source` (`Teacher`/`Self`) + `IsEditable` taşır. Sahiplik `IStudentDirectory` ile (aynı IDOR koruması) |
| **Kişisel ders ekle** | `POST /api/scheduling/students/{studentId}/study-entries` | `CreateStudyScheduleEntryRequest` → `StudyScheduleEntryResponse` | **2026-07-08.** Öğrenci kendi programına ders ekler. Öğretmen dersiyle **saat çakışması** `409 scheduling.teacher_conflict`; aralık `400 invalid_range`. Sahip-yetki (owner-only) |
| **Kişisel ders güncelle** | `PUT /api/scheduling/study-entries/{entryId}` | `UpdateStudyScheduleEntryRequest` → `StudyScheduleEntryResponse` | **2026-07-08.** Yoksa `404 scheduling.entry_not_found`; çakışma yeniden kontrol edilir |
| **Kişisel ders sil** | `DELETE /api/scheduling/study-entries/{entryId}` | → `StudyScheduleEntryResponse` | **2026-07-08.** Soft-cancel (`Status=Cancelled`). Yoksa `404 scheduling.entry_not_found` |
| **Erteleme talebi aç** (öğrenci) | `POST /api/scheduling/students/{studentId}/lesson-requests` | `CreateLessonChangeRequestRequest { LessonScheduleId, Reason, ProposedStartAtUtc?, ProposedEndAtUtc? }` → `LessonChangeRequestResponse` | **Ö-F, 2026-07-18.** Öğrenci talep açar; ders öğrenciye ait değilse `404 scheduling.lesson_not_found` (IDOR koruması). Alternatif tarih tek taraflıysa/tutarsızsa `400 scheduling.invalid_range`. Sahiplik `IStudentDirectory` (owner-only) |
| **Erteleme taleplerini listele** (öğretmen) | `GET /api/scheduling/teachers/{teacherUserId}/lesson-requests?onlyPending=` | → `LessonChangeRequestResponse[]` | **Ö-F, 2026-07-18.** `onlyPending=true` yalnız bekleyenler; `CreatedOnUtc` azalan sıralı. Öğretmen yalnız kendi taleplerini görür |
| **Talebi kabul et** (öğretmen) | `POST /api/scheduling/lesson-requests/{requestId}/accept` | → `LessonChangeRequestResponse` | **Ö-F, 2026-07-18.** Alternatif tarih doluysa **mevcut** `RescheduleLessonScheduleCommand` çalışır (çakışma/edit kontrolü dahil); erteleme başarısızsa talep kabul edilmez, hata olduğu gibi döner. Yoksa `404 scheduling.request_not_found`; sonuçlanmışsa `409 scheduling.request_not_pending` |
| **Talebi reddet** (öğretmen) | `POST /api/scheduling/lesson-requests/{requestId}/reject` | → `LessonChangeRequestResponse` | **Ö-F, 2026-07-18.** Talep kapanır, ders değişmez. Yoksa `404`; sonuçlanmışsa `409 scheduling.request_not_pending` |

**`CreateLessonScheduleRequest` (koddan):** `TeacherUserId, StudentId, Subject, LessonFormat, StartAtUtc, EndAtUtc, TimeZone, RecurrenceRule?, ReminderOffsetMinutes, LocationLabel?, MeetingUrl?, Notes?`

**`LessonScheduleResponse` (koddan):** `Id, TeacherUserId, StudentId, Subject, LessonFormat (string), StartAtUtc, EndAtUtc, TimeZone, RecurrenceRule?, Status (string), ReminderOffsetMinutes, LocationLabel?, MeetingUrl?, Notes?, CreatedOnUtc, UpdatedOnUtc, OriginalStartAtUtc?, CancellationReason? (string), IsChargeable`
> Not: Enum'lar yanıtta **string** olarak döner (`LessonFormat.ToString()`, `Status.ToString()`).

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP | Anlam |
|-----|------|-------|
| `scheduling.teacher_conflict` | `409` | Öğretmenin bu aralıkta başka dersi var |
| `scheduling.invalid_range` | `400` | `EndAtUtc <= StartAtUtc` |
| `scheduling.lesson_not_found` | `404` | Ders planı yok |
| `scheduling.entry_not_found` | `404` | Öğrenci program girdisi yok (2026-07-08) |
| `scheduling.timeoff_not_found` | `404` | Tatil bloğu yok (B-01, 2026-07-18) |
| `scheduling.request_not_found` | `404` | Erteleme talebi yok (Ö-F, 2026-07-18) |
| `scheduling.request_not_pending` | `409` | Talep zaten kabul/red edilmiş (Ö-F, 2026-07-18) |
| `scheduling.delete_not_allowed` | `409` | Silme 24s/gelecek kuralı dışında (B-09, 2026-07-18) |
| `scheduling.not_editable` | `409` | Yalnız planlı ders düzenlenir/ertelenir |
| `scheduling.already_completed` | `409` | Ders zaten tamamlanmış |
| `shared.forbidden` | `403` | Yetki yok |
| (varsayılan) | `400` | Diğer doğrulama hataları |

### 3.2 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Tatil ekle/listele | `POST /api/scheduling/holidays`, `GET /api/scheduling/teachers/{id}/holidays` | `ScheduleException` için |
| Seri oluştur | `POST /api/scheduling/lessons/series` | `RecurrenceRule` açılımı + `SeriesId` üretimi |
| Seri iptal | `POST /api/scheduling/lessons/series/{seriesId}/cancel?scope=instance\|all` | Tek örnek / tüm seri |

---

## 4. İş Kuralları

1. **Geçerli aralık (🟢 kodda):** `EndAtUtc > StartAtUtc` zorunlu; aksi halde `scheduling.invalid_range`.
2. **Çakışma engeli (🟢 kodda):** Aynı `TeacherUserId` için zaman aralığı çakışan yeni ders oluşturulamaz → `scheduling.teacher_conflict` (409). Çakışma `HasTeacherConflictAsync` ile sorgulanır.
3. **Oluşturmada durum (🟢 kodda):** Yeni ders her zaman `Planned` ile başlar (handler `Draft` kabul etmez; `Draft` ileride taslak akışı için ayrılmıştır).
4. **İptal davranışı (🟢 kodda):** `Cancel()` durumu `Cancelled` yapar, iptal notunu mevcut nota satır olarak ekler, event yayar, hatırlatmayı iptal eder.
5. **Hatırlatma (🟢 kodda):** Oluşturmada `ReminderOffsetMinutes` ile hatırlatma planlanır (m11); iptalde geri alınır.
6. **Öğrenci tarafı öncelik kuralı (🟢 kodda, 2026-07-08):** Öğrencinin **kendi program girdisi** (`StudyScheduleEntry`), öğretmenle yapılan **özel ders** (`LessonSchedule`) ile saat çakışırsa **oluşturma/güncelleme reddedilir** → `scheduling.teacher_conflict` (409). Yani özel ders önceliklidir; öğrenci o slota kendi dersini tanımlayamaz. Çakışma, her iki tarafın tekrarları `RecurrenceExpander` ile genişletilerek `StudyScheduleConflict.OverlapsTeacherLesson` içinde hesaplanır (180 günlük ufuk). Mobilde takvim öğretmen derslerini salt-okunur + rozetli gösterir.
7. **⚠️ Online ders linki:** `LessonFormat = Online/Hybrid` ise `MeetingUrl` istenmeli; öğrenci linkle katılır.
8. **⚠️ Tatil çakışması:** Bir `ScheduleException` aralığına ders planlanırken uyarı verilmeli (sert engel değil, esnek uyarı önerilir).
9. **⚠️ Güncellemede yeniden kontrol:** `PUT` ile saat değişirse çakışma yeniden değerlendirilmeli ve hatırlatma yeniden planlanmalı.
10. **Sahiplik (yetki):** Öğretmen yalnızca kendi derslerini görüp düzenleyebilir (`LessonSchedulePolicies.cs`); ihlalde `shared.forbidden`.
11. **Öğrenci erteleme talebi (🟢 kodda, Ö-F, 2026-07-18):** Öğrenci dersi **kendisi değiştiremez**; yalnızca `POST /students/{id}/lesson-requests` ile **talep** açar (neden + isteğe bağlı alternatif tarih). Talep yalnız öğrencinin **kendi dersine** açılabilir (`lesson.StudentId == studentId`, aksi halde `lesson_not_found`). Öğretmen `accept` → kabulde **mevcut** `Reschedule` akışı (çakışma/edit kontrolü korunur) çalışır; `reject` → talep kapanır. Durum makinesi `Pending → Accepted|Rejected`; sonuçlanmış talep tekrar sonuçlandırılamaz (`request_not_pending`). Böylece yetki matrisi #1 (öğrenci dersi değiştiremez) korunur ve `Reschedule` DRY biçimde yeniden kullanılır.

---

## 5. Olay Akışı (Event-Driven)

```
POST /lessons (Planned)
   → LessonScheduledDomainEvent (ReminderOffsetMinutes taşır)
       → outbox → m11 Notifications handler: ReminderOffsetMinutes ile hatırlatma planlanır (senkron servis kaldırıldı, Y1)
       → (öneri) m08 Study: öğrencinin birleşik takvimine "özel ders" olarak yansıtılır
       → (öneri) m09 Parents: veliye "yeni ders planlandı" bildirimi

POST /lessons/{id}/cancel
   → LessonScheduleCancelledDomainEvent
       → m11 Notifications: hatırlatma iptal
       → (öneri) m08/m09: öğrenci/veli bilgilendirme

(öneri) Planlı ders → ders günü
   → M05 LessonSession türetilir (LessonScheduleId ile bağ) — bkz. m05_lesson_sessions.md §5
   → Oturum tamamlanınca (öneri) LessonSchedule.Status = Completed güncellenir

POST /students/{id}/study-entries  (öğrenci kendi dersi)
   → StudyScheduleEntryScheduledDomainEvent (Subject + StartAtUtc + ReminderOffsetMinutes taşır)
       → outbox → m11 Notifications StudyScheduleReminderIntegrationEventHandler:
           ReminderOffsetMinutes > 0 ise ilk oluşuma göre LessonReminder (StudentId'ye, TeacherUserId boş) planlar
PUT /study-entries/{id}
   → StudyScheduleEntryRescheduledDomainEvent → mevcut hatırlatma yeni saate taşınır (offset 0 ise iptal)
DELETE /study-entries/{id}
   → StudyScheduleEntryCancelledDomainEvent → hatırlatma iptal edilir

POST /students/{id}/lesson-requests  (öğrenci erteleme talebi — Ö-F)
   → LessonChangeRequestedDomainEvent
       → outbox → m11 Notifications: öğretmene "yeni erteleme talebi" bildirimi
POST /lesson-requests/{id}/accept
   → (alternatif tarih varsa) RescheduleLessonScheduleCommand → LessonScheduleRescheduledDomainEvent (mevcut akış)
   → LessonChangeRequestResolvedDomainEvent (Status=Accepted) → öğrenciye "talebiniz kabul edildi"
POST /lesson-requests/{id}/reject
   → LessonChangeRequestResolvedDomainEvent (Status=Rejected) → öğrenciye "talebiniz reddedildi"
```

> **Not:** Öğrenci kişisel programı hatırlatması, öğretmen dersleriyle **aynı** `LessonReminder` aggregate'ında
> tutulur (girdinin kimliği `LessonScheduleId` alanına yazılır, tekildir). Tekrarlı girdilerde hatırlatma **ilk
> oluşuma** göre planlanır — öğretmen dersleriyle aynı MVP davranışı. Notifications, Scheduling'e referans vermez;
> handler olay adı + JSON payload üzerinden çalışır.

> Olaylar **Outbox** ile güvenilir yayılır (`Shared/Infrastructure/Messaging`).

---

## 6. Mobil Ekranlar

### ✅ Mevcut
| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/scheduling` | `SchedulingPage` | **Öğretmen** — `syncfusion_flutter_calendar` ile gün/hafta/ay görünümü; ders ekle/iptal. **Hatırlatma süresi artık formda seçilir** (`LessonFormSheet`: Kapalı/15/30dk/1sa/1gün — önceden sabit 60 dk idi), 2026-07-08. |
| `/student/calendar` | `StudentCalendarPage` | **Öğrenci** (2026-07-08) — alt menü "Takvim" sekmesi. **Öğretmen takvim ekranıyla birebir aynı takvim:** `SfCalendar` Aylık/Haftalık/Günlük geçişi + `‹ tarih Bugün ›` gezinme çubuğu → altında **öğretmen paneli stili** seçili gün listesi (renk çubuğu + "Öğretmen/Kendi" pill + saat + tekrar/konum). Öğretmen dersleri salt-okunur/öncelikli (kilit); kendi dersleri düzenle/sil. "Ders ekle" FAB → `StudyEntryFormSheet` (**tam ekran**, öğretmen `LessonFormSheet` düzeniyle uyumlu: tek/tekrarlı günlük-haftalık-aylık, ders adı, saat, **hatırlatma**, not). Kaynak: `GET /students/{id}/calendar` |

> Durum yönetimi `flutter_bloc` (Cubit) / `StatefulWidget`. `mobile/lib/features/scheduling`.
> Kronometre (`/study/timer`) ders seçicisi de birleşik takvimden beslenir; ders seçilmeden başlatılırsa "Serbest çalışma" kaydedilir.

### ⚠️ Planlanan
- **Birleşik takvim katmanları:** dersler + ödevler (m06 son teslim) + tatiller (`ScheduleException`) + ödeme vadeleri (m07) aynı takvimde renk kodlu.
- **Çakışma görsel uyarısı:** kayıt öncesi/eş zamanlı 409 dönüşünde kullanıcıya net uyarı.
- **Online ders kartı:** `MeetingUrl` "Derse Katıl" butonu (öğretmen + öğrenci tarafı).
- **Tekrarlı ders formu:** "her hafta Salı 18:00" gibi kural seçici + seri önizleme.
- **Tatil/izin ekleme** ekranı.
- **Öğrenci takvimi görünümü** (m08 ile birleşik).

---

## 7. Kabul Kriterleri

- [x] Öğretmen tek seferlik ders planlayabilir (konu, taraflar, format, saat, zaman dilimi).
- [x] Geçersiz aralık reddedilir (`scheduling.invalid_range`).
- [x] Aynı öğretmende çakışan ders engellenir (`scheduling.teacher_conflict`).
- [x] Ders iptal edilebilir ve hatırlatma geri alınır.
- [x] Tarih aralığıyla takvim listesi alınabilir.
- [x] Ders güncelleme (`PUT /lessons/{id}`) + güncellemede kendini hariç tutan çakışma + hatırlatma yeniden planlama (2026-06-28).
- [x] Online ders linki (`MeetingUrl`) **domain alanı** uçtan uca (B-10, 2026-07-18).
- [x] **Ders erteleme** (`POST /reschedule`) — statü Planned kalır, erteleme geçmişi + bildirim (B-02, 2026-07-18).
- [x] **İptal nedeni + ücretlendirme** kaydediliyor; 24s/gelecek kuralı dışında silme reddediliyor (B-09, 2026-07-18).
- [x] **Tatil bloğu** eklenebiliyor; çakışan planlı dersler yanıtta listeleniyor (B-01, 2026-07-18).
- [x] **Tekrar eden dersin tek oturumu** seriden bağımsız iptal/ertele edilebiliyor (`Scope=Single` → occurrence istisnası); "bu ve sonrakiler" seriyi kısaltıyor (B-03, 2026-07-18).
- [x] **Öğrenci kişisel programı** (`StudyScheduleEntry`) CRUD + tekrar (günlük/haftalık/aylık) + birleşik takvim (2026-07-08).
- [x] **Tekrar açılımı** öğrenci takviminde (`RecurrenceExpander`) — DAILY/WEEKLY+BYDAY/MONTHLY + UNTIL + occurrence istisnaları (2026-07-08 / B-03 2026-07-18).
- [x] **Öğrenci tarafı öncelik kuralı** — kendi girdisi öğretmen dersiyle çakışamaz (`scheduling.teacher_conflict`), 2026-07-08.
- [x] **Öğrenci ders erteleme talebi** (`LessonChangeRequest`) — öğrenci talep açar, öğretmen kabul (mevcut Reschedule)/red eder; öğrenci dersi kendisi değiştirmez (Ö-F, 2026-07-18).
- [ ] ⚠️ Öğretmen `LessonSchedule` listesinde tekrar açılımı (şu an tek örnek; öğrenci birleşik takviminde açılıyor).
- [x] **Öğrenci kişisel programına hatırlatma** — oluştur/güncelle/sil → outbox → Notifications; `0 dk` kapalı, tekrarlıda ilk oluşum (2026-07-08).
- [x] **`Planned → Completed` geçişi** — `POST /lessons/{lessonId}/complete` (`CompleteLessonScheduleAsync`); tekrar tamamlamada `scheduling.already_completed` (409).

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. ✅ **Ders güncelleme + yeniden çakışma kontrolü** (`PUT /lessons/{id}`) — tamamlandı (2026-06-28).
2. **`MeetingUrl` / online ders linki** — backend **domain alanı** + DTO (mobil tarafı ve "Toplantıya Katıl" hazır).
3. **Tekrarlı ders açılımı** — `RecurrenceRule` çözümleyici + `SeriesId` + seri iptali.
4. **`ScheduleException` (tatil/blackout)** — aggregate + endpoint + takvim katmanı + planlama uyarısı.
5. **Öğrenci öncelik kuralı (m08 entegrasyonu)** — çakışmada özel ders önceliği + öğrenci uyarısı.
6. **`Planned → Completed` geçişi** — M05 oturum tamamlanınca planı kapatma köprüsü.
7. **Birleşik takvim** (dersler+ödev+tatil+ödeme) — backend toplayıcı veya mobil çoklu kaynak.

---

## 9. İlişkili Dokümanlar

- Rol bağlamı → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md)
- Planlı dersten oturum → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Hatırlatma → [`m11_notifications.md`](m11_notifications.md)
- Öğrenci bireysel plan + öncelik → [`m08_study.md`](m08_study.md)
- Veli takip → [`m09_parents.md`](m09_parents.md)
- Veri modeli (ER) → [`veri_modeli.md`](veri_modeli.md) · Mimari → [`mimari_inceleme.md`](mimari_inceleme.md)
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) · UI → [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)

---

*Takvim & Planlama (M04) — Detaylı Tasarım | Güncelleme: 2026-09-02 (F-04: `POST /lessons/{id}/complete` kodda — Planned→Completed işaretlendi) · 2026-08-19 (kod-senkron: `LessonSchedule.TeacherUserId`/`LessonFormat` nullable + `Topic`/`ColorHex` alanları eklendi; §2.3 `StudyScheduleEntry` aggregate + `StudyScheduleModel.cs` kodda YOK — kaldırıldı olarak işaretlendi; `MeetingUrl` "önerilen" iddiaları temizlendi) · 2026-07-19 (Ç-06: `StudyScheduleEntry` → birleşik `LessonSchedule` (nullable `TeacherUserId` + `Topic`/`ColorHex`); self-lesson komut yolu + `/study-entries` köprüleme; takvim tek kaynak + occurrence `completed` (`IStudyPlanCompletionReader`); `LessonScheduled/Cancelled` event'leri nullable teacher; `UnifyLessonSchedule` migration; `StudyScheduleReminderIntegrationEventHandler` kaldırıldı · Veli V-F: `IStudentUpcomingLessonsDirectory` — veli paneli için öğrencinin yaklaşan Planned dersleri; Shared.Contracts) · 2026-07-18*
