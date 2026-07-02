# 📖 Ders Oturumları (M05) — Detaylı Tasarım Dokümanı

> **PRD: M05 Ders Oturumu** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (çekirdek), ⚠️ yaşam döngüsü genişletme bekliyor**
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
| `InProgress` geçişi (dersi başlat) | 🔴 **Yok** | Enum değeri var, davranış yok |
| `Cancel()` (oturum iptali) | 🔴 **Yok** | Enum değeri var, davranış yok |
| `MeetingUrl` / kayıt (recording) URL | 🔴 **Yok** | Önerilen — bkz. §2.2 |
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
| `CreatedOnUtc` | `DateTime` | Oluşturma |
| `CompletedOnUtc` | `DateTime?` | Tamamlanma |

**Davranışlar (kodda):**
- **Constructor** → `LessonSessionCreatedDomainEvent` yayar. `CreateLessonSessionCommandHandler` oturumu `Status = Planned`, `AttendanceStatus = Unknown`, gerçek zaman/süre `null` ile oluşturur.
- `Complete(actualStart, actualEnd, attendanceStatus, topicTitle, coveredContent?, teacherNotes?, completedOnUtc)` →
  - `DurationMinutes = (int)Math.Ceiling((actualEnd - actualStart).TotalMinutes)` (**manuel girilmez**),
  - `Status = Completed`, `CompletedOnUtc` set edilir,
  - `LessonSessionCompletedDomainEvent` yayılır.

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
| `MeetingUrl` | `string?` | Online derste oturumdan doğrudan "Derse Katıl". M04'teki link buraya kopyalanabilir/türetilebilir. |
| `RecordingUrl` | `string?` | **Opsiyonel ders kaydı** linki (sonradan paylaşılır). |
| `Start()` davranışı | `Planned → InProgress` | `promp.txt` "dersi işle" akışı için ders başlatma (canlı durum). |
| `Cancel(reason)` davranışı | `→ Cancelled` | İşlenmeyen ders için iptal (no-show vb.). `Absent` katılım ile birlikte kullanılabilir. |
| Otomatik türetme | M04 → M05 köprüsü | Planlı dersten (özellikle ders günü) oturumun otomatik veya tek tıkla oluşturulması (bkz. §5). |

> **Not:** `InProgress` ve `Cancelled` **enum değerleri zaten var** ama geçiş davranışları yoktur; bu yüzden
> "alan var, davranış yok" olarak işaretlenmiştir.

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")`; `Result<T>` döner.
> Route prefix: `/api/lesson-sessions`.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Oturum oluştur | `POST /api/lesson-sessions` | `CreateLessonSessionRequest` → `LessonSessionResponse` | `Status=Planned`, `AttendanceStatus=Unknown` |
| Oturumu tamamla | `POST /api/lesson-sessions/{lessonSessionId}/complete` | `CompleteLessonSessionRequest` → `LessonSessionResponse` | Süre otomatik; `Completed` event yayılır |
| Oturum getir | `GET /api/lesson-sessions/{lessonSessionId}` | → `LessonSessionResponse` | Yoksa `404 lesson_sessions.not_found` |
| Oturum listele | `GET /api/lesson-sessions?teacherUserId=&studentId=&dateFromUtc=&dateToUtc=` | → `LessonSessionResponse[]` | **K2 (2026-07-01):** Admin dışı çağıranlar için sahiplik filtresi **server tarafında zorlanır** (öğretmen→kendi dersleri, diğer→kendi kaydı); istemci filtresine güvenilmez. Filtresiz istek artık tüm tabloyu döndürmez (IDOR kapandı). |

**`CreateLessonSessionRequest` (koddan):** `LessonScheduleId?, TeacherUserId, StudentId, Subject, PlannedStartAtUtc, TopicTitle`

**`CompleteLessonSessionRequest` (koddan):** `ActualStartAtUtc, ActualEndAtUtc, AttendanceStatus, TopicTitle, CoveredContent?, TeacherNotes?`

**`LessonSessionResponse` (koddan):** `Id, LessonScheduleId?, TeacherUserId, StudentId, Subject, PlannedStartAtUtc, ActualStartAtUtc?, ActualEndAtUtc?, DurationMinutes?, AttendanceStatus (string), Status (string), TopicTitle, CoveredContent?, TeacherNotes?, CreatedOnUtc, CompletedOnUtc?`
> Enum'lar yanıtta **string** olarak döner (`AttendanceStatus.ToString()`, `Status.ToString()`).

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP |
|-----|------|
| `lesson_sessions.not_found` | `404` |
| `shared.forbidden` | `403` |
| (varsayılan) | `400` |

### 3.2 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Dersi başlat | `POST /api/lesson-sessions/{id}/start` | `Planned → InProgress` (canlı ders) |
| Oturumu iptal et | `POST /api/lesson-sessions/{id}/cancel` | `→ Cancelled` (no-show / iptal) |
| Plandan türet | `POST /api/lesson-sessions/from-schedule/{lessonScheduleId}` | M04 → M05 köprüsü, tek tık |
| Kayıt linki ekle | `PUT /api/lesson-sessions/{id}/recording` | `RecordingUrl` |
| Öğrenci görünümü | `GET /api/lesson-sessions/students/{studentId}` (rol kısıtı) | Öğrenci kendi geçmiş/yaklaşan oturumlarını görsün |

---

## 4. İş Kuralları

1. **Süre otomatik (🟢 kodda):** `DurationMinutes`, `ActualEnd - ActualStart` farkından `Math.Ceiling` ile hesaplanır; istemci gönderse bile dikkate alınmaz.
2. **Tamamlama tek yön (🟢 kodda):** `Complete()` durumu `Completed` yapar ve `LessonSessionCompletedDomainEvent` yayar; bu olay m06 not/ödev akışının ön koşuludur.
3. **Oturum oluşturma (🟢 kodda):** Yeni oturum `Planned` + `Unknown` katılım ile başlar; `LessonScheduleId` opsiyoneldir (plana bağlı veya serbest oturum).
4. **Katılım (🟢 kodda):** `AttendanceStatus` tamamlamada girilir (`Attended/Late/Absent`); `Unknown` varsayılan.
5. **⚠️ Geçerli zaman:** `ActualEndAtUtc > ActualStartAtUtc` doğrulaması eklenmeli (şu an domainde açık kontrol yok; negatif süre olasılığı).
6. **⚠️ Yaşam döngüsü:** `Planned → InProgress → Completed` ve `Planned → Cancelled` geçişleri davranış olarak eklenmeli; geçersiz geçişler engellenmeli.
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

### ⚠️ Planlanan
- **"Dersi Başlat / Tamamla" akışı:** canlı durum (`InProgress`) + tamamlamada süre/katılım/konu formu.
- **Online derse katılım butonu** (`MeetingUrl`).
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
- [ ] ⚠️ `InProgress` (başlat) ve `Cancelled` (iptal) geçişleri.
- [ ] ⚠️ `ActualEnd > ActualStart` doğrulaması.
- [ ] ⚠️ M04 planlı dersten oturum türetme (otomatik/tek tık).
- [ ] ⚠️ `MeetingUrl` / `RecordingUrl`.
- [ ] ⚠️ Öğrenci tarafı görünüm + yetki kısıtı.

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. **Yaşam döngüsü netleştirme** — `Start()` / `Cancel()` davranışları + geçiş doğrulamaları.
2. **Plandan oturum türetme (M04↔M05 köprüsü)** — `from-schedule` endpoint'i, ders günü otomasyonu.
3. **`ActualEnd > ActualStart` doğrulaması** (negatif süre engeli).
4. **`MeetingUrl` + `RecordingUrl`** — online katılım ve kayıt linki.
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
- PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) · UI → [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)

---

*Ders Oturumları (M05) — Detaylı Tasarım | Güncelleme: 2026-07-01*
