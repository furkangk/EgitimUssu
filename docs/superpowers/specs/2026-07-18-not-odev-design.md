# Dilim B — Not & Ödev · Tasarım Spec'i

**Tarih:** 2026-07-18
**Kaynak:** `doc/roles/ogretmen.md` §10 · fonksiyonel doküman §15 (B-05) + §12.2 (ödev durum makinesi)
**Kapsam:** M06 (Assignments modülü — `Assignment` + `LessonNote`). 3 boşluk: B-05 not görünürlüğü, T-06.7 ödev onay/geri gönder, T-06.8 ödev geri bildirim.

**Kapsam dışı:** T-06.9 aynı ödevi çok öğrenciye atama (Faz 3) · öğrenci bazlı ücret/ilişki (Dilim C).

## Mevcut Durum
- `LessonNote` aggregate: `Summary/CoveredTopics/Recommendations`, görünürlük alanı **yok**. `CreateLessonSessionFollowUpCommand` ile ders oturumu tamamlandıktan sonra oluşturulur.
- `Assignment` aggregate: statü `Pending/InProgress/Completed/Cancelled`. `MarkCompleted` (öğrenci) + `SubmitWork` (öğrenci dosya). Öğretmen onay/geri bildirim **yok**.
- Endpoint'ler: `POST/GET /lesson-sessions/{id}/follow-up`, `GET /`, `POST /{id}/complete`, `POST /{id}/submission`, `GET /{id}/attachment`.

## Tasarım

### B-05 — Not görünürlüğü
- `enum LessonNoteVisibility { Private = 1, Student = 2, StudentAndParent = 3 }`; varsayılan `Private`.
- `LessonNote`'a `Visibility` alanı; ctor + `Update` imzasına eklenir.
- `CreateLessonSessionFollowUpCommand` + `CreateLessonSessionFollowUpRequest`'e `LessonNoteVisibility Visibility` (varsayılan `Private`). `LessonNoteResponse`'a `string Visibility`.
- Veli/öğrenci okuma tarafında (M09/öğrenci) `Private` notlar süzülür — bu dilimde alan + kayıt; süzme M09 tarafında ayrı iş (not olarak bırakılır).

### T-06.7 / T-06.8 — Ödev onay / geri gönder + geri bildirim
- `AssignmentStatus`'e `Approved = 5`, `ReturnedForRevision = 6` eklenir.
- `Assignment`'a `TeacherFeedback` (string?) alanı.
- Domain metotları: `Approve(string? feedback, DateTime nowUtc)` (yalnız `Completed`/`InProgress`'ten), `ReturnForRevision(string feedback, DateTime nowUtc)` (statü `ReturnedForRevision`, öğrenci yeniden yükleyebilir → `SubmitWork` `ReturnedForRevision`'ı da `InProgress`'e çeker).
- Yeni komutlar (öğretmen): `ApproveAssignmentCommand(Guid AssignmentId, string? Feedback)`, `ReturnAssignmentCommand(Guid AssignmentId, string Feedback)`; öğretmen sahiplik authorizer'ı (assignment.TeacherUserId == currentUser).
- Endpoint'ler: `POST /assignments/{id}/approve`, `POST /assignments/{id}/return`.
- `AssignmentResponse`'a `string? TeacherFeedback`.
- Domain event: `AssignmentApprovedDomainEvent`, `AssignmentReturnedDomainEvent` (öğrenci/veli bildirimi için outbox).

## Test Stratejisi (TDD)
- `LessonNote` visibility ctor/update.
- `Assignment.Approve`/`ReturnForRevision` statü geçişleri; `SubmitWork` `ReturnedForRevision`'ı `InProgress`'e çeker.
- Handler yetki: başka öğretmenin ödevini onaylayamaz.

## Doküman Bakımı
`doc/modules/m06_assignments.md`, `00_genel_bakis.md` (endpoint + durum), `veri_modeli.md`, `doc/roles/ogretmen.md` §10 (B-05 + T-06.7/8).

## Kabul Kriterleri
- [ ] Not oluştururken görünürlük seçilebiliyor; varsayılan `Private`.
- [ ] Öğretmen ödevi onaylayabiliyor / geri bildirimle geri gönderebiliyor.
- [ ] Geri gönderilen ödevi öğrenci yeniden yükleyince `InProgress`'e dönüyor.
- [ ] Başka öğretmenin ödevine onay/geri gönderme reddediliyor.
