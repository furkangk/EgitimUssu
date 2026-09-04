---
title: "M05 — Ders Oturumları (LessonSessions)"
summary: "Planlanan dersin fiilen işlendiği kaydı; başlat/iptal geçişleri + zaman doğrulaması + MeetingUrl çalışır, otomatik oturum türetme (M05-2) eksik"
tags: [modul, lesson-sessions, ders-oturumu, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/LessonSessions/**
updated: 2026-09-04
---

# 📖 Ders Oturumları (M05) — Detaylı Tasarım Dokümanı

> **PRD: M05 Ders Oturumu** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (çekirdek + yaşam döngüsü); ⚠️ plandan oturum türetme (M05-2) bekliyor**
>
> **Amaç:** Planlanan dersin **fiilen işlendiği** kaydı. Öğretmen dersi tamamlar; gerçek süre otomatik
> hesaplanır; öğrencinin katılım durumu, işlenen konu ve öğretmen notu kayda geçer. Tamamlama, ders
> sonrası **not + ödev** akışını (m06) tetikleyen merkezi olaydır.
>
> İlgili: [`m04_scheduling.md`](m04_scheduling.md) (planlı dersten oturum) ·
> [`m06_assignments.md`](m06_assignments.md) (tamamlama → not/ödev) ·
> [`m10_progress_tracking.md`](m10_progress_tracking.md) (gelişim verisi) ·
> [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`00_genel_bakis.md`](00_genel_bakis.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`LessonSession`) | ✅ Mevcut | `src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs` |
| Application (CQRS + handler) | ✅ Mevcut | `src/Modules/LessonSessions/Application/LessonSessionFeatures.cs` |
| API (oluştur / tamamla / getir / listele) | ✅ Mevcut (4 endpoint) | `src/Modules/LessonSessions/API/LessonSessionsModule.cs` |
| Çapraz-modül erişim servisi | ✅ Mevcut | `src/Modules/LessonSessions/Application/LessonSessionAccess.cs` (Assignments tüketir) |
| Tamamlamada süre otomatik hesabı | ✅ Mevcut | `Complete()` → `Math.Ceiling((end-start).TotalMinutes)` |
| `Completed` event yayını | ✅ Mevcut | `LessonSessionCompletedDomainEvent` → m06 dinler |
| `InProgress` geçişi (dersi başlat) | ✅ var (2026-09-04, P06/M05-1) | `LessonSession.Start()` + `POST /{id}/start`; yalnız `Planned` → `InProgress` |
| `Cancel(reason)` (oturum iptali) | ✅ var (2026-09-04, P06/M05-1) | `LessonSession.Cancel()` + `POST /{id}/cancel`; `Planned`/`InProgress` → `Cancelled` |
| `ActualEnd > ActualStart` doğrulaması | ✅ var (2026-09-04, P06/M05-3) | `Complete()` içinde invariant → `lesson_sessions.invalid_time_range` |
| `MeetingUrl` / kayıt (recording) URL | ✅ alan var (2026-09-04, P06/M05-4) | `MeetingUrl`, `RecordingUrl` + `SetMeetingUrl()`/`SetRecordingUrl()`; **yazma ucu henüz yok** — `MeetingUrl` M05-2 (plandan türetme) ile plandan kopyalanacak |
| Planlı dersten **otomatik** oturum türetme | 🔴 **Yok** | Manuel `POST` ile oluşturuluyor; `LessonScheduleId` opsiyonel bağ |

> **Düzeltme:** Önceki dokümanda "yalnızca complete + get var" deniyordu; **kodda ayrıca `POST` (oluştur)
> ve `GET` (listele, filtreli) endpoint'leri de mevcuttur.**

---

## 2. Domain Modeli

### 2.1 🟢 Mevcut (koddan) — `LessonSession` (AggregateRoot<Guid>)

`src/Modules/LessonSessions/Domain/LessonSessionsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Oturum kimliği |
| `LessonScheduleId` | `Guid?` | Hangi planlı dersten (M04) doğduğu — opsiyonel |
| `TeacherUserId` | `Guid` | Öğretmen |
| `StudentId` | `Guid` | Öğrenci |
| `Subject` | `string` | Konu / branş |
| `PlannedStartAtUtc` | `DateTime` | Planlanan başlangıç |
| `ActualStartAtUtc` | `DateTime?` | Gerçekleşen başlangıç (tamamlamada dolar) |
| `ActualEndAtUtc` | `DateTime?` | Gerçekleşen bitiş |
| `DurationMinutes` | `int?` | **Otomatik** hesaplanır (tamamlamada) |
| `AttendanceStatus` | enum `StudentAttendanceStatus` | `Unknown=1`, `Attended=2`, `Late=3`, `Absent=4` |
| `Status` | enum `LessonSessionStatus` | `Planned=1`, `InProgress=2`, `Completed=3`, `Cancelled=4` |
| `TopicTitle` | `string` | İşlenen konu başlığı |
| `CoveredContent` | `string?` | İşlenen içerik detayı |
| `TeacherNotes` | `string?` | Öğretmen notu |
| `IsChargeable` | `bool` | **Ücretlendirme kararı (B-08, 2026-07-18).** Özellikle `Absent`'ta anlamlı: gelmedi→ücretlendirilecek mi. Alan audit + ileride rapor/desen içindir (bu dilimde otomatik ödeme oluşturmaz) |
| `MeetingUrl` | `string?` | P06/M05-4: online dersin toplantı bağlantısı (maks. 512). Plandan türetmede (M05-2) `LessonSchedule.MeetingUrl`'den kopyalanacak |
| `RecordingUrl` | `string?` | P06/M05-4: ders kaydı bağlantısı (maks. 512) |
| `CancellationReason` | `string?` | P06/M05-1: iptal nedeni (maks. 500). Yalnız `Cancelled` durumunda anlamlı; boş/boşluk gönderilirse `null` yazılır |
| `CreatedOnUtc` | `DateTime` | Oluşturma |
| `CompletedOnUtc` | `DateTime?` | Tamamlanma |

**Davranışlar (kodda):**
- **Constructor** → `LessonSessionCreatedDomainEvent` yayar. `CreateLessonSessionCommandHandler` oturumu `Status = Planned`, `AttendanceStatus = Unknown`, gerçek zaman/süre `null` ile oluşturur.
- `Complete(actualStart, actualEnd, attendanceStatus, topicTitle, coveredContent?, teacherNotes?, isChargeable, completedOnUtc)` (**B-08 ile `isChargeable` eklendi, 2026-07-18**; **P06/M05-3 ile `Result` döner**) →
  - iptal edilmiş oturumda `lesson_sessions.invalid_transition`, `actualEnd <= actualStart` ise `lesson_sessions.invalid_time_range` → **kayıt hiç değişmez**,
  - `DurationMinutes = (int)Math.Ceiling((actualEnd - actualStart).TotalMinutes)` (**manuel girilmez**),
  - `Status = Completed`, `IsChargeable` + `CompletedOnUtc` set edilir,
  - `LessonSessionCompletedDomainEvent` yayılır.
  - **Tamamlanmış oturum yeniden tamamlanabilir** (öğretmenin konu/not düzeltmesi — `roles/ogretmen.md` §5.1 #12); bu durumda olay yeniden yayılır, tüketiciler `inbox_messages` ile idempotenttir.
- `Start(actualStartAtUtc)` → **`Result`** (P06/M05-1). Yalnız `Planned` → `InProgress`; aksi halde `lesson_sessions.invalid_transition` ve ilk başlangıç saati korunur.
- `Cancel(reason?, nowUtc)` → **`Result`** (P06/M05-1). Yalnız `Planned`/`InProgress` → `Cancelled`; tamamlanmış oturum iptal edilemez. `reason` trim'lenir, boşsa `null`.
- `SetMeetingUrl(url?)` / `SetRecordingUrl(url?)` → trim eder; boş/boşluk değer alanı `null`'lar.

> **Desen notu:** Bu modülde domain metotları `Shared.Kernel.Result` döner (repodaki ilk örnek). Geçiş/invariant
> kuralı **domain'de** durur; handler yalnız hatayı yukarı taşır ve `SaveChanges` **çağırmaz** — böylece geçersiz
> istek veritabanına hiç dokunmaz.

**Enum'lar (koddan birebir):**
```
StudentAttendanceStatus : Unknown = 1, Attended = 2, Late = 3, Absent = 4
LessonSessionStatus     : Planned = 1, InProgress = 2, Completed = 3, Cancelled = 4
```

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `LessonSessionCreatedDomainEvent` | `LessonSessionId, LessonScheduleId?, TeacherUserId, StudentId, PlannedStartAtUtc, CreatedOnUtc` |
| `LessonSessionCompletedDomainEvent` | `LessonSessionId, LessonScheduleId?, TeacherUserId, StudentId, CompletedOnUtc` |

### 2.2 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Tip / Şekil | Gerekçe |
|-------|-------------|---------|
| Otomatik türetme | M04 → M05 köprüsü | Planlı dersten (özellikle ders günü) oturumun otomatik veya tek tıkla oluşturulması (bkz. §5) — **M05-2, P06 Task 4**. |
| `RecordingUrl` yazma ucu | `PUT /{id}/recording` | Alan eklendi ama henüz yazan uç yok. |

**Durum geçiş diyagramı (P06/M05-1 sonrası, koddan):**

```
                 Start()                Complete()
   ┌─────────┐ ─────────────► ┌────────────┐ ──────────► ┌───────────┐
   │ Planned │                │ InProgress │             │ Completed │◄─┐
   └─────────┘ ──────────────►└────────────┘             └───────────┘  │ Complete()
        │          Complete()        │                         │        │ (konu/not düzeltme)
        │ Cancel()                   │ Cancel()                └────────┘
        ▼                            ▼
   ┌───────────┐ ◄──────────────────┘
   │ Cancelled │   (buradan çıkış yok: Start/Complete → invalid_transition)
   └───────────┘
```

> Geçersiz her geçiş `lesson_sessions.invalid_transition` (**409**) döner ve aggregate'i değiştirmez.

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")`; `Result<T>` döner.
> Route prefix: `/api/lesson-sessions`.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Oturum oluştur | `POST /api/lesson-sessions` | `CreateLessonSessionRequest` → `LessonSessionResponse` | `Status=Planned`, `AttendanceStatus=Unknown` |
| Oturumu tamamla | `POST /api/lesson-sessions/{lessonSessionId}/complete` | `CompleteLessonSessionRequest` → `LessonSessionResponse` | Süre otomatik; `Completed` event yayılır |
| **Dersi başlat** (M05-1) | `POST /api/lesson-sessions/{lessonSessionId}/start` | → `LessonSessionResponse` | Gövdesiz. Yalnız sahibi öğretmen/Admin. `Planned` değilse **409** |
| **Dersi iptal et** (M05-1) | `POST /api/lesson-sessions/{lessonSessionId}/cancel` | `CancelLessonSessionRequest?` → `LessonSessionResponse` | Gövde opsiyonel (`{ "reason": "..." }`). `Completed`/`Cancelled` ise **409** |
| Oturum getir | `GET /api/lesson-sessions/{lessonSessionId}` | → `LessonSessionResponse` | Yoksa `404 lesson_sessions.not_found` |
| Oturum listele | `GET /api/lesson-sessions?teacherUserId=&studentId=&dateFromUtc=&dateToUtc=` | → `LessonSessionResponse[]` | **K2 (2026-07-01):** Admin dışı çağıranlar için sahiplik filtresi **server tarafında zorlanır** (öğretmen→kendi dersleri, diğer→kendi kaydı); istemci filtresine güvenilmez. Filtresiz istek artık tüm tabloyu döndürmez (IDOR kapandı). |

**`CreateLessonSessionRequest` (koddan):** `LessonScheduleId?, TeacherUserId, StudentId, Subject, PlannedStartAtUtc, TopicTitle`

**`CompleteLessonSessionRequest` (koddan):** `ActualStartAtUtc, ActualEndAtUtc, AttendanceStatus, TopicTitle, CoveredContent?, TeacherNotes?, IsChargeable` (B-08, 2026-07-18)

**`CancelLessonSessionRequest` (koddan):** `Reason?` — gövde tümüyle opsiyoneldir.

**`LessonSessionResponse` (koddan):** `Id, LessonScheduleId?, TeacherUserId, StudentId, Subject, PlannedStartAtUtc, ActualStartAtUtc?, ActualEndAtUtc?, DurationMinutes?, AttendanceStatus (string), Status (string), TopicTitle, CoveredContent?, TeacherNotes?, IsChargeable, MeetingUrl?, RecordingUrl?, CancellationReason?, CreatedOnUtc, CompletedOnUtc?`
> Enum'lar yanıtta **string** olarak döner (`AttendanceStatus.ToString()`, `Status.ToString()`).

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP |
|-----|------|
| `lesson_sessions.not_found` | `404` |
| `shared.forbidden` | `403` |
| `lesson_sessions.invalid_transition` | **`409`** (P06/M05-1 — istek doğru, kaynağın durumu uygun değil) |
| `lesson_sessions.invalid_time_range` | `400` (varsayılan eşleme) |
| (varsayılan) | `400` |

### 3.2 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Plandan türet | `POST /api/lesson-sessions/from-schedule/{lessonScheduleId}` | M04 → M05 köprüsü, tek tık |
| Kayıt linki ekle | `PUT /api/lesson-sessions/{id}/recording` | `RecordingUrl` |
| Öğrenci görünümü | `GET /api/lesson-sessions/students/{studentId}` (rol kısıtı) | Öğrenci kendi geçmiş/yaklaşan oturumlarını görsün |

---

## 4. İş Kuralları

1. **Süre otomatik (🟢 kodda):** `DurationMinutes`, `ActualEnd - ActualStart` farkından `Math.Ceiling` ile hesaplanır; istemci gönderse bile dikkate alınmaz.
2. **Tamamlama tek yön (🟢 kodda):** `Complete()` durumu `Completed` yapar ve `LessonSessionCompletedDomainEvent` yayar; bu olay m06 not/ödev akışının ön koşuludur.
3. **Oturum oluşturma (🟢 kodda):** Yeni oturum `Planned` + `Unknown` katılım ile başlar; `LessonScheduleId` opsiyoneldir (plana bağlı veya serbest oturum).
4. **Katılım (🟢 kodda):** `AttendanceStatus` tamamlamada girilir (`Attended/Late/Absent`); `Unknown` varsayılan.
5. **Geçerli zaman (🟢 kodda, P06/M05-3):** `ActualEndAtUtc > ActualStartAtUtc` domain invariant'ıdır; eşit veya ters aralık `lesson_sessions.invalid_time_range` ile reddedilir ve kayıt değişmez. `CompleteLessonSessionCommandValidator` aynı kuralı istek katmanında da uygular (çift savunma).
6. **Yaşam döngüsü (🟢 kodda, P06/M05-1):** `Planned → InProgress → Completed`, `Planned/InProgress → Cancelled`. `Cancelled` **son durumdur**; oradan `Start`/`Complete` yapılamaz. Geçersiz geçiş `lesson_sessions.invalid_transition` (409).
7. **Sahiplik (yetki):** Öğretmen yalnızca kendi oturumlarını yönetebilir (`LessonSessionPolicies.cs`); öğrenci yalnızca kendi oturumlarını görüntüleyebilir (önerilen rol kısıtı).

---

## 5. Olay Akışı (Event-Driven)

```
POST /lesson-sessions (Planned)
   → LessonSessionCreatedDomainEvent
       → (öneri) m11 Notifications: öğrenci/veliye "ders oturumu oluşturuldu"

POST /lesson-sessions/{id}/complete (Completed)
   → LessonSessionCompletedDomainEvent
       → m06 Assignments: ders sonrası NOT + ÖDEV akışını mümkün kılar
         (Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs)
       → (öneri) m10 ProgressTracking: gelişim verisi güncellenir
       → (öneri) m07 Payments: LessonFee türü ödeme kaydı tetiklenir/işaretlenir
       → (öneri) m04 Scheduling: ilgili LessonSchedule.Status = Completed

(öneri) M04 planlı ders → ders günü
   → from-schedule ile LessonSession türetilir (LessonScheduleId taşınır)
```

> **Çapraz-modül erişim:** Assignments modülü, oturum bilgisine `ILessonSessionAccessService`
> (`LessonSessionAccess.cs`) üzerinden `LessonSessionDetails { IsCompleted, StudentId, TeacherUserId,
> TopicTitle, CoveredContent, TeacherNotes }` projeksiyonuyla erişir — modül sınırı korunur.
> Olaylar **Outbox** ile yayılır.

---

## 6. Mobil Ekranlar

### ✅ Mevcut
| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/lesson-sessions` | `LessonSessionsPage` | Oturum listesi (`?create=1` ile oluşturma) |
| `/lesson-sessions/detail` | `LessonDetailPage` | Oturum detayı |
| `/lesson-notes/new` | `LessonNoteFormPage` | Ders notu formu (m06) |
| `/lesson-sessions/detail/note` | `LessonNoteViewPage` | Not görüntüleme (m06) |

> `mobile/lib/features/lesson_sessions`, `flutter_bloc` (Cubit).

> **Veri katmanı (2026-09-04, P06/M05-1):** `LessonSessionRepository.startSession(id)` ve `cancelSession(id, reason:)`
> eklendi; `LessonSession` modeli `meetingUrl`/`recordingUrl`/`cancellationReason` alanlarını okuyor.
> **Ekrandaki düğmeler henüz bağlanmadı:** ders detay ekranı bir `LessonSchedule` taşıyor, oturum kimliği taşımıyor;
> köprü `POST /from-schedule/{id}` (M05-2, P06 Task 4) ile kurulacak ve düğmeler o görevde bağlanacak.

### ⚠️ Planlanan
- **"Dersi Başlat / İptal Et" düğmeleri** — backend + mobil veri katmanı hazır; ekran bağlantısı M05-2 ile.
- **Kayıt (recording) linki** görüntüleme/paylaşma.
- **Plandan tek-tık oturum oluşturma** (M04 ders kartından).
- **Öğrenci tarafı oturum görünümü** (geçmiş + yaklaşan).

---

## 7. Kabul Kriterleri

- [x] Öğretmen oturum oluşturabilir (plana bağlı veya serbest).
- [x] Öğretmen oturumu tamamlayabilir; süre otomatik hesaplanır.
- [x] Katılım, konu, içerik ve öğretmen notu kaydedilebilir.
- [x] Tamamlama, m06 not/ödev akışını tetikler (entegrasyon handler mevcut).
- [x] Oturum getir + filtreli listeleme.
- [x] `InProgress` (başlat) ve `Cancelled` (iptal) geçişleri (2026-09-04, P06/M05-1); geçersiz geçiş 409.
- [x] `ActualEnd > ActualStart` doğrulaması (2026-09-04, P06/M05-3); ihlalde kayıt değişmez.
- [x] `MeetingUrl` / `RecordingUrl` alanları + `CancellationReason` (2026-09-04, P06/M05-4). Yazma yolu: `MeetingUrl` M05-2 ile plandan kopyalanır, `RecordingUrl` için uç henüz yok.
- [ ] ⚠️ M04 planlı dersten oturum türetme (otomatik/tek tık) — **P06 Task 4**.
- [ ] ⚠️ Öğrenci tarafı görünüm + yetki kısıtı.

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. ✅ ~~**Yaşam döngüsü netleştirme**~~ — **yapıldı (2026-09-04, P06/M05-1)**: `Start()`/`Cancel()` + geçiş doğrulamaları.
2. **Plandan oturum türetme (M04↔M05 köprüsü)** — `from-schedule` endpoint'i, ders günü otomasyonu (**M05-2, P06 Task 4**).
3. ✅ ~~**`ActualEnd > ActualStart` doğrulaması**~~ — **yapıldı (2026-09-04, P06/M05-3)**.
4. ✅ ~~**`MeetingUrl` + `RecordingUrl` alanları**~~ — **yapıldı (2026-09-04, P06/M05-4)**. Kalan: `RecordingUrl` yazma ucu + mobil ekran bağlantısı.
5. **Tamamlamada otomasyon** — M07 ders ücreti kaydı + M10 gelişim verisi tetikleme.
6. **Öğrenci tarafı görünüm + yetkilendirme testleri** (`LessonSessionPolicies.cs` kapsamı).

---

## 9. İlişkili Dokümanlar

- Planlı ders → oturum → [`m04_scheduling.md`](m04_scheduling.md)
- Tamamlama → not/ödev → [`m06_assignments.md`](m06_assignments.md)
- Gelişim takibi → [`m10_progress_tracking.md`](m10_progress_tracking.md) · Ödeme → [`m07_payments.md`](m07_payments.md)
- Bildirim → [`m11_notifications.md`](m11_notifications.md) · Raporlama → [`m14_reporting.md`](m14_reporting.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md)
- Veri modeli → [`veri_modeli.md`](veri_modeli.md) · Mimari → [`mimari_inceleme.md`](mimari_inceleme.md) · Genel → [`00_genel_bakis.md`](00_genel_bakis.md)
- PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) · UI → [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)

---

*Ders Oturumları (M05) — Detaylı Tasarım | Güncelleme: 2026-09-04 (P06/M05-1 + M05-3 + M05-4: `Start()`/`Cancel()` geçişleri + `invalid_transition` 409, `ActualEnd > ActualStart` invariant'ı, `MeetingUrl`/`RecordingUrl`/`CancellationReason` alanları; domain metotları artık `Result` döner) · 2026-07-19 (Veli V-F: `IStudentLastLessonDirectory` — veli paneli için son tamamlanan ders özeti; TeacherNotes veli-görünürlük garantisi olmadan doldurulmaz; Shared.Contracts) · 2026-07-18*
