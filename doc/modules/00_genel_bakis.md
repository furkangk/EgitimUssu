# 🗂️ Modül Dokümanları — Genel Bakış ve Durum

> Bu klasör (`doc/modules/`), PRD'deki ürün vizyonu ile **gerçek kod tabanı** arasında köprü kuran
> detaylı modül tasarım dokümanlarını içerir. Her doküman; domain modeli, API sözleşmesi,
> mobil ekranlar, iş kuralları, kabul kriterleri ve **mevcut implementasyon durumunu** içerir.
>
> İlgili üst dokümanlar:
> - [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) — Ürün gereksinimleri
> - [`../ai_ready_architecture.md`](../ai_ready_architecture.md) — Sistem mimarisi
> - [`../design.md`](../design.md) — UI/Frontend tasarım yaklaşımı
> - [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) — Flutter UI tasarımı

---

## 1. Geliştirme Öncelik Sırası (Senin Hedefin)

Bu doküman seti, aşağıdaki **iş öncelik sırasına** göre düzenlenmiştir:

| Sıra | Modül Dokümanı | Hedef | Durum |
|------|----------------|-------|-------|
| 1️⃣ | [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md) | Öğretmen derslerini ve öğrencilerini takip etsin | 🟢 Büyük ölçüde yazıldı |
| 2️⃣ | [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md) | Öğrenci kendi derslerini/çalışmasını yönetsin, öğretmen-öğrenci ilişkisi | 🟡 Kısmen (profil var, bireysel çalışma yok) |
| 3️⃣ | [`03_veli_modulu.md`](03_veli_modulu.md) | Veli çocuğunun gelişimini görsün | 🔴 İskelet |
| 4️⃣ | [`04_eslestirme_ve_degerlendirme.md`](04_eslestirme_ve_degerlendirme.md) | Öğrenci-öğretmen özel ders eşleştirme + puanlama | 🔴 İskelet |

---

## 2. Teknoloji Yığını (Kodda Doğrulanmış)

| Katman | Teknoloji | Not |
|--------|-----------|-----|
| Backend | **.NET 10** (Modüler Monolit) | `Directory.Build.props`, `global.json` |
| Mimari | Clean Architecture + DDD + CQRS + Outbox | Her modülde `API / Application / Domain / Infrastructure` |
| Veritabanı | **PostgreSQL** (modül başına ayrı `DbContext` + ayrı migration) | Veri izolasyonu modül sınırında |
| Cache | **Redis** (lazy bağlantı) | `Shared/Infrastructure/Caching` |
| Mesajlaşma | Domain Events → Integration Events (Outbox pattern) | `Shared/Infrastructure/Messaging` |
| Mobil | **Flutter** (`flutter_bloc` / Cubit, `go_router`, `dio`, `get_it`) | Birincil platform |
| Web | Angular (planlandı, henüz yok) | İkincil — Faz 4-5 |

### Backend modül katman yapısı (her modül için aynı)
```
src/Modules/<ModulAdi>/
 ├── API/            → ModuleDefinition, endpoint mapping, request/response DTO
 ├── Application/    → Command/Query + Handler + Policy (CQRS), Repository interface
 ├── Domain/         → AggregateRoot, Entity, Enum, DomainEvent
 └── Infrastructure/ → DbContext, Repository impl, Migrations, DI, Integration event handler
```

### Mobil feature yapısı (her özellik için aynı)
```
mobile/lib/features/<ozellik>/
 ├── data/           → model (DTO), repository_impl
 ├── domain/         → contracts (entity + repository interface)
 └── presentation/   → cubit (state mgmt), pages (ekranlar), widgets
```

---

## 3. Modül ↔ Backend ↔ Mobil Eşleme Tablosu

> PRD'deki "modül" (M01-M15) ile koddaki teknik modüller ve mobil feature'lar her zaman 1:1 değildir.
> Bir kullanıcı rolü (örn. öğretmen), birden çok backend modülünü kullanır.

| PRD Modülü | Backend Modülü (`src/Modules`) | Mobil Feature (`mobile/lib/features`) | Route Prefix | Durum |
|------------|-------------------------------|----------------------------------------|--------------|-------|
| M01 Kullanıcı & Rol | `Identity` | `auth` | `/api/identity` | 🟢 Yazıldı |
| M02 Öğretmen Profili | `Teachers` | `teacher_profile` | `/api/teachers` | 🟢 Yazıldı |
| M03 Öğrenci Profili | `Students` | `students` | `/api/students` | 🟢 Yazıldı (öğretmen tarafı) |
| M04 Takvim & Planlama | `Scheduling` | `scheduling` | `/api/scheduling` | 🟢 Yazıldı |
| M05 Ders Oturumu | `LessonSessions` | `lesson_sessions` | `/api/lesson-sessions` | 🟢 Yazıldı |
| M06 Not & Ödev | `Assignments` | `assignments` | `/api/assignments` | 🟢 Yazıldı |
| M07 Ödeme Takibi | `Payments` | `payments` | `/api/payments` | 🟢 Yazıldı |
| M08 Bireysel Çalışma | `Study` | _(yok)_ | `/api/study` | 🔴 İskelet |
| M09 Veli Paneli | `Parents` | _(yok)_ | `/api/parents` | 🔴 İskelet |
| M10 Gelişim Takibi | `ProgressTracking` | _(yok)_ | `/api/progress-tracking` | 🔴 İskelet |
| M11 Bildirim | `Notifications` | _(yok, server-side)_ | `/api/notifications` | 🟡 Kısmen |
| M12 Eşleştirme | `Matching` | _(yok)_ | `/api/matching` | 🔴 İskelet |
| M13 Puanlama & Yorum | `Reviews` | _(yok)_ | `/api/reviews` | 🔴 İskelet |
| M14 Raporlama | `Reporting` | _(yok)_ | `/api/reporting` | 🔴 İskelet |
| M15 Ayarlar & Güvenlik | `Settings` | `more` | `/api/settings` | 🟡 Domain var, endpoint yok |

**Durum açıklaması:**
- 🟢 **Yazıldı** — Domain + Application (CQRS) + API endpoint + EF migration + mobil ekran(lar) mevcut.
- 🟡 **Kısmen** — Bir kısmı (örn. sadece domain modeli ya da sadece bir endpoint) mevcut.
- 🔴 **İskelet** — Sadece `DbContext` + DI + `/status` endpoint var; domain/feature yok.

---

## 4. Mevcut API Endpoint Envanteri (Koddan Çıkarıldı)

### Identity — `/api/identity`
```
POST /register                       POST /login            POST /refresh
POST /password-reset/request         POST /password-reset/confirm
POST /email-verification/request     POST /email-verification/confirm
POST /logout            (auth)       GET  /users/{userId}   (auth)
```

### Teachers — `/api/teachers`
```
POST /profiles                       PUT  /profiles/{userId}    GET /profiles/{userId}
```

### Students — `/api/students`
```
POST /profiles                       GET  /profiles/{studentId}
GET  /profiles/by-user/{userId}      GET  /profiles/by-teacher/{teacherUserId}
```

### Scheduling — `/api/scheduling`
```
POST /lessons                        POST /lessons/{lessonId}/cancel
GET  /lessons/{lessonId}             GET  /teachers/{teacherUserId}/lessons
```

### LessonSessions — `/api/lesson-sessions`
```
POST /{lessonSessionId}/complete     GET  /{lessonSessionId}
```

### Assignments — `/api/assignments`
```
POST /lesson-sessions/{lessonSessionId}/follow-up
GET  /lesson-sessions/{lessonSessionId}/follow-up
```

### Payments — `/api/payments`
```
POST /records                        PUT  /records/{paymentRecordId}    GET /records/{paymentRecordId}
GET  /teachers/{teacherUserId}/records
GET  /teachers/{teacherUserId}/summary
GET  /teachers/{teacherUserId}/records/filter
```

### Notifications — `/api/notifications`
```
GET  /teachers/{teacherUserId}/lesson-reminders
```

### İskelet modüller (sadece durum endpoint'i)
```
GET /api/study/status          GET /api/parents/status      GET /api/matching/status
GET /api/reviews/status        GET /api/reporting/status    GET /api/progress-tracking/status
GET /api/settings/status
```

---

## 5. Roller (Identity Domain'den)

`UserRole` enum'u: `Admin = 1`, `Teacher = 2`, `Student = 3`, `Parent = 4`

`UserAccountStatus`: `PendingActivation`, `Active`, `Suspended`, `Closed`

> **Önemli ürün kararı (PRD §M01):** Öğrenci hem öğretmenden bağımsız kayıt olabilmeli (`StudentOrigin.SelfRegistered`)
> hem de öğretmen tarafından eklenebilmeli (`StudentOrigin.TeacherManaged`). Her iki yol da domain'de modellenmiştir.

---

## 6. Bu Doküman Setinin Kullanımı

- **Yeni özellik yazarken:** İlgili modül dokümanını aç → "Eksikler / Yapılacaklar" bölümüne bak → domain + API + mobil katmanları sırayla tamamla.
- **AI ile kod üretirken:** Her doküman, [`ai_ready_architecture.md`](../ai_ready_architecture.md) kurallarına uyacak şekilde (CQRS, Result pattern, modül sınırı) yazılmıştır.
- **Durum güncelleme:** Bir özellik tamamlandığında ilgili dokümanın "Durum" tablosunu ve buradaki eşleme tablosunu güncelle.

---

*Genel Bakış — Modül Dokümanları | Güncelleme: 2026-06-21*
