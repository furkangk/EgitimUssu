---
title: "M06 — Ödev, Not & Kaynak (Assignments)"
summary: "Ders sonrası not/ödev akışı, öğrenci teslim/tamamlama/onay-geri gönder, 0–100 puanlama, son teslim uyarısı taraması, ders kaynağı (LessonResource) paylaşımı ve ortak IFileStorage depolaması çalışır"
tags: [modul, assignments, odev, not, faz-1]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Assignments/**
updated: 2026-11-15
---

# 📝 Ödev, Not & Kaynak (M06) — Detaylı Tasarım Dokümanı

> **PRD: M06 Not & Ödev** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (öğretmen tarafı), ⚠️ öğrenci yükleme + kaynak genişletme bekliyor**
>
> **Amaç:** Ders sonrası **takip** (follow-up): öğretmen tamamlanan derse **ders notu** yazar ve **ödev**
> verir. Hedef, `promp.txt`'teki "Öğretmen derslere not, kaynak ve ödev verebilecek; ödev takibi
> yapabilecek; öğrenci ödevlerini yükleyebilecek; öğrenci öğretmenin paylaştığı not ve kaynakları
> görebilecek" akışını uçtan uca kurmaktır.
>
> İlgili: [`m05_lesson_sessions.md`](m05_lesson_sessions.md) (tamamlama → follow-up) ·
> [`m09_parents.md`](m09_parents.md) (son teslim uyarısı) · [`m11_notifications.md`](m11_notifications.md) ·
> [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`00_genel_bakis.md`](00_genel_bakis.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`Assignment`, `LessonNote`) | ✅ Mevcut | `src/Modules/Assignments/Domain/AssignmentsDomainModel.cs` |
| Application (follow-up + listeleme) | ✅ Mevcut | `src/Modules/Assignments/Application/AssignmentFeatures.cs` |
| API (follow-up POST/GET + liste + tamamla/teslim/indir/onayla/geri gönder/**puanla**) | ✅ Mevcut (9 endpoint + 4 kaynak ucu) | `src/Modules/Assignments/API/AssignmentsModule.cs` |
| M05 tamamlama tüketimi | ✅ Mevcut | `Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs` |
| Öğretmen eki (`AttachmentUrl`) | ✅ Mevcut | `Assignment.AttachmentUrl` (öğretmenin paylaştığı dosya linki) |
| Ödev **tamamlandı** işaretleme | ✅ Endpoint | `Assignment.MarkCompleted()` + `POST /{id}/complete` (öğrenci) |
| Öğrenci **dosya yükleme** (teslim) | ✅ Endpoint | `Assignment.SubmitWork()` + `POST /{id}/submission` (multipart) + yerel depolama |
| Ödev **onay / geri gönder** + geri bildirim | ✅ Endpoint | `Assignment.Approve()`/`ReturnForRevision()` + `POST /{id}/approve`, `POST /{id}/return` (öğretmen) |
| Not **görünürlük** kontrolü | ✅ Mevcut | `LessonNote.Visibility` (`Private`/`Student`/`StudentAndParent`) — B-05 |
| Ders **kaynağı** (`LessonResource`) | 🟢 **Var** (2026-09-03, P04/M06-1) | Dosya veya bağlantı; 4 uç + mobil bölüm |
| Öğrenci ödev **yükleme** (`AssignmentSubmission`) | 🔴 **Yok** | Önerilen — öğrenci dosya yükler |
| Ödev **puanlama** (0–100) | 🟢 **Var** (2026-09-04, P06/M06-4) | `Assignment.Grade()` + `POST /{id}/grade` (öğretmen) |
| Son teslim uyarısı (öğrenci + veli bildirimi) | 🟢 **Var** (2026-09-04, P06/M06-3) | `AssignmentDueScanner` (saatlik) → `AssignmentDueSoon`/`AssignmentMissed` → M11 tüketicileri |
| Öğrenci görünümü endpoint'leri | 🔴 **Kısmi** | `GET /assignments?studentId=` var; not/kaynak öğrenci görünümü eksik |
| Dosya depolama altyapısı | 🟢 **Var** (P04) | Ortak `IFileStorage` (yerel + S3-uyumlu) |

---

## 2. Domain Modeli

**P18 T1 — eşzamanlılık (K-22):** `Assignment` `IVersioned` uygular; `uint Version { get; private set; }` PostgreSQL’in ürettiği `xmin` (`xid`) sistem kolonuna eşlenir. `ModuleDbContext` tip modele eklenirken concurrency + OnAddOrUpdate konvansiyonunu uygular. Aynı sürümü okuyan iki yazmadan kaybeden `409 shared.concurrency_conflict` alır: “Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.” `AddVersionToken` yalnız model metadatasını kaydeder; yeni kolon/veri göçü yoktur. Outbox hatayı normal backoff ile yeniden teslim eder; yeni scope güncel veriyi yükler, başarısız iş ve inbox aynı transaction’da geri alınır.


### 2.1 🟢 Mevcut (koddan) — `Assignment` (AggregateRoot<Guid>)

`src/Modules/Assignments/Domain/AssignmentsDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Ödev kimliği |
| `StudentId` | `Guid` | Ödevin verildiği öğrenci |
| `TeacherUserId` | `Guid` | Ödevi veren öğretmen |
| `LessonSessionId` | `Guid?` | İlişkili ders oturumu (opsiyonel) |
| `Title` | `string` | Ödev başlığı |
| `Description` | `string?` | Açıklama |
| `DueDateUtc` | `DateTime?` | Son teslim tarihi |
| `Status` | enum `AssignmentStatus` | `Pending=1`, `InProgress=2`, `Completed=3`, `Cancelled=4`, `Approved=5`, `ReturnedForRevision=6` |
| `AttachmentUrl` | `string?` | Ek bağlantısı; öğrenci teslimi yapıldığında `/api/assignments/{id}/attachment` ile doldurulur |
| `AttachmentStorageKey` | `string?` | P04: teslim dosyasının `IFileStorage` anahtarı, **maks. 512** (public URL değil) |
| `AttachmentFileName` | `string?` | Özgün dosya adı, **maks. 256** |
| `AttachmentContentType` | `string?` | MIME türü, **maks. 128** |
| `AttachmentSizeBytes` | `long?` | Dosya boyutu |
| `CreatedOnUtc` | `DateTime` | Oluşturma |
| `CompletedOnUtc` | `DateTime?` | Tamamlanma |
| `TeacherFeedback` | `string?` | Öğretmenin onay/geri gönder/puanlama geri bildirimi (T-06.7/8, M06-4) |
| `Score` | `int?` | M06-4: öğretmen puanı **0–100**; puanlanmadıysa `null` |
| `GradedOnUtc` | `DateTime?` | M06-4: puanın verildiği an |
| `DueSoonNotifiedOnUtc` | `DateTime?` | M06-3: "teslime 24 saat kaldı" uyarısının üretildiği an (ödev başına bir kez) |
| `MissedNotifiedOnUtc` | `DateTime?` | M06-3: "teslim edilmedi" uyarısının üretildiği an (ödev başına bir kez) |

**Davranış:**
- `MarkCompleted(completedOnUtc)` → `Status = Completed`, `AssignmentCompletedDomainEvent` yayılır.
- `SubmitWork(attachmentUrl, nowUtc)` → teslim; `Pending` **veya** `ReturnedForRevision` durumundaki ödevi `InProgress` yapar, `AssignmentSubmittedDomainEvent` yayılır.
- `AttachSubmission(storageKey, fileName, contentType, sizeBytes, nowUtc)` (2026-09-03, P04) → depolanmış teslim dosyasını bağlar (`AttachmentStorageKey`/`AttachmentFileName`/`AttachmentContentType`/`AttachmentSizeBytes`), `AttachmentUrl`'i indirme ucuna ayarlar ve `SubmitWork`'ü uygular. **Önceki anahtarı döndürür** — çağıran onu depodan siler (yetim dosya kalmaz).
- `Approve(feedback?, nowUtc)` → `Status = Approved`, `TeacherFeedback` set edilir, `AssignmentApprovedDomainEvent` yayılır (T-06.7).
- `ReturnForRevision(feedback, nowUtc)` → `Status = ReturnedForRevision`, `TeacherFeedback` set edilir, `AssignmentReturnedDomainEvent` yayılır (T-06.8).
- `Grade(score, feedback?, nowUtc) : Result` (2026-09-04, P06/M06-4) → **yalnız `Approved`** durumundaki ödeve **0–100** puan verir; geri bildirim doluysa günceller, `AssignmentGradedDomainEvent` yayılır. Aralık dışı → `assignments.invalid_score`; durum uygun değilse → `assignments.grade_requires_approval`. Kural domain'de olduğu için geçersiz istek `SaveChanges`'e hiç ulaşmaz.
- `MarkDueSoon(nowUtc) : bool` / `MarkMissed(nowUtc) : bool` (2026-09-04, P06/M06-3) → yalnız **teslim bekleyen** (`Pending`/`ReturnedForRevision`) ve tarihi olan ödevde çalışır; her biri ödev başına **bir kez** olay yayar (`DueSoonNotifiedOnUtc`/`MissedNotifiedOnUtc` ile idempotent). `MarkDueSoon` vadesi geçmiş ödevde, `MarkMissed` vadesi gelmemiş ödevde `false` döner.
> Yeni ödev her zaman `Pending` ile oluşturulur (`CreateLessonSessionFollowUpCommandHandler`).

**Ödev durum makinesi:** `Pending → InProgress` (teslim) `→ Completed` (öğrenci tamamlar) `→ Approved` (öğretmen onaylar) **veya** `→ ReturnedForRevision` (öğretmen geri gönderir) `→ InProgress` (öğrenci yeniden teslim eder).

### 2.2 🟢 Mevcut (koddan) — `LessonNote` (AggregateRoot<Guid>)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Not kimliği |
| `LessonSessionId` | `Guid` | İlgili oturum (zorunlu — 1 oturum : 1 not) |
| `TeacherUserId`, `StudentId` | `Guid` | Taraflar |
| `Summary` | `string` | Ders özeti |
| `CoveredTopics` | `string?` | İşlenen konular |
| `Recommendations` | `string?` | Öneriler / sonraki adımlar |
| `Visibility` | enum `LessonNoteVisibility` | Not görünürlüğü: `Private=1`, `Student=2`, `StudentAndParent=3` (B-05) |
| `CreatedOnUtc` | `DateTime` | Oluşturma |

**Davranış:** `Update(summary, coveredTopics?, recommendations?, visibility)` → not içeriğini ve görünürlüğü günceller (event yok).

**Enum'lar (koddan birebir):**
```
AssignmentStatus     : Pending = 1, InProgress = 2, Completed = 3, Cancelled = 4, Approved = 5, ReturnedForRevision = 6
LessonNoteVisibility : Private = 1, Student = 2, StudentAndParent = 3
```

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `AssignmentCreatedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, LessonSessionId?, CreatedOnUtc` |
| `AssignmentCompletedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, LessonSessionId?, CompletedOnUtc` |
| `AssignmentSubmittedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, LessonSessionId?, SubmittedOnUtc` |
| `AssignmentApprovedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, OnUtc` |
| `AssignmentReturnedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, OnUtc` |
| `AssignmentGradedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, Score, GradedOnUtc` |
| `AssignmentDueSoonDomainEvent` | `AssignmentId, StudentId, TeacherUserId, Title, DueDateUtc, NotifiedOnUtc` |
| `AssignmentMissedDomainEvent` | `AssignmentId, StudentId, TeacherUserId, Title, DueDateUtc, NotifiedOnUtc` |
| `LessonNoteCreatedDomainEvent` | `LessonNoteId, LessonSessionId, TeacherUserId, StudentId, CreatedOnUtc` |

### 2.3 ⚠️ Önerilen (henüz kodda yok)

#### A) `LessonResource` (ders KAYNAĞI / materyali) — ✅ **kodda (2026-09-03, P04/M06-1)**
Ders notundan **ayrı**; öğretmenin paylaştığı **kalıcı kaynak/materyal** (dosya ya da bağlantı). Öğrenci görür ve takip eder.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `TeacherUserId` | `Guid` | Paylaşan öğretmen |
| `StudentId` | `Guid?` | Belirli öğrenciye özel mi yoksa genel mi |
| `LessonSessionId` | `Guid?` | İsteğe bağlı oturum bağı |
| `Title` | `string` | Kaynak adı, **maks. 200**, zorunlu |
| `Description` | `string?` | **maks. 1000** |
| `Kind` | `enum LessonResourceKind` | `File = 1`, `Link = 2` |
| `StorageKey` | `string?` | Dosya kaynağının `IFileStorage` anahtarı, **maks. 512** |
| `FileName` / `ContentType` / `SizeBytes` | `string?` / `string?` / `long?` | Dosya meta verisi (256 / 128) |
| `Url` | `string?` | Bağlantı kaynağının dış adresi, **maks. 1024** |
| `IsActive` | `bool` | Arşivlenince `false` (yumuşak silme) |
| `CreatedOnUtc` / `ArchivedOnUtc` | `DateTime` / `DateTime?` | |

**Fabrikalar / davranış:** `CreateFile(...)` anahtarsız, `CreateLink(...)` URL'siz oluşturulamaz (`ArgumentException`); başlık zorunludur. `Archive(nowUtc)` yumuşak silme yapar — geçmiş derse ait kaynak kaybolmaz.

**Yetki:** Oluşturma/arşivleme yalnız kaynağın öğretmeni (veya Admin). Listeleme/indirme: öğretmen kendi kaynaklarını (`teacherUserId` = kendisi), öğrenci `studentId` = kendisi olan **veya** `StudentId = null` (tüm öğrencilere açık) kaynakları görür. `LessonResourceAuthorizer` — varsayılan-deny.

#### B) `AssignmentSubmission` (öğrenci ödev YÜKLEME — yeni Entity/Aggregate)
Öğrenci ödevini **yükler** (`promp.txt`: "Öğrenci ödevlerini yüklemesi gerekir"). Ödevin teslim tarafı.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `AssignmentId` | `Guid` | Hangi ödev |
| `StudentId` | `Guid` | Yükleyen öğrenci |
| `FileUrl` | `string` | Yüklenen dosya |
| `StudentNote` | `string?` | Öğrenci açıklaması |
| `SubmissionStatus` | enum (öneri) | `Submitted`, `Late`, `Resubmitted`, `Graded` |
| `SubmittedOnUtc` | `DateTime` | Teslim zamanı |
| `Grade` / `Feedback` | `string?` | (Opsiyonel) öğretmen değerlendirmesi |

> Teslim sonrası `Assignment.Status` `InProgress`/`Completed`'a güncellenebilir; öğretmen değerlendirir.

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `RequireAuthorization("AuthenticatedUser")`; `Result<T>` döner.
> Route prefix: `/api/assignments`. Mobilde not + ödev **tek "follow-up" ekranında** birleşir.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Ders sonrası takip oluştur | `POST /api/assignments/lesson-sessions/{lessonSessionId}/follow-up` | `CreateLessonSessionFollowUpRequest` → `LessonSessionFollowUpResponse` | Oturum **tamamlanmış** olmalı |
| Takip getir | `GET /api/assignments/lesson-sessions/{lessonSessionId}/follow-up` | → `LessonSessionFollowUpResponse` | Not yoksa otomatik **özet** üretir (aşağıda) |
| Ödev listele | `GET /api/assignments?teacherUserId=&studentId=&lessonSessionId=` | → `AssignmentResponse[]` | **K2 (2026-07-01):** Admin dışı çağıranlar için sahiplik filtresi **server tarafında zorlanır**; istemci filtresine güvenilmez (IDOR kapandı). |

**`CreateLessonSessionFollowUpRequest` (koddan):**
`Summary, CoveredTopics?, Recommendations?, Assignments?, Visibility` (varsayılan `Private`) — burada `Assignments` öğesi:
`CreateLessonSessionFollowUpAssignmentRequest { Title, Description?, DueDateUtc?, AttachmentUrl? }`

**`LessonSessionFollowUpResponse` (koddan):** `LessonSessionId, Note: LessonNoteResponse, Assignments: AssignmentResponse[]`
- `LessonNoteResponse`: `Id, LessonSessionId, TeacherUserId, StudentId, Summary, CoveredTopics?, Recommendations?, Visibility (string), CreatedOnUtc`
- `AssignmentResponse`: `Id, StudentId, TeacherUserId, LessonSessionId?, Title, Description?, DueDateUtc?, Status (string), AttachmentUrl?, CreatedOnUtc, CompletedOnUtc?, TeacherFeedback?, Score?, GradedOnUtc?`

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP | Anlam |
|-----|------|-------|
| `assignments.follow_up_exists` | `409` | Not + ödev zaten oluşturulmuş |
| `assignments.lesson_session_not_found` | `404` | Oturum yok |
| `assignments.follow_up_not_found` | `404` | Bu oturum için not/ödev yok |
| `assignments.lesson_session_not_completed` | `400` (varsayılan) | Oturum tamamlanmadan not/ödev oluşturulamaz |
| `assignments.assignment_not_found` | `404` | Ödev yok (onay/geri gönder) |
| `assignments.feedback_required` | `400` (varsayılan) | Geri gönderme için geri bildirim zorunlu |
| `assignments.invalid_score` | `400` (varsayılan) | Puan 0–100 aralığında değil |
| `assignments.grade_requires_approval` | `400` (varsayılan) | Yalnız onaylanmış ödev puanlanabilir |
| `shared.forbidden` | `403` | Yetki yok |

### 3.2 ✅ Öğrenci ödev aksiyonları (2026-07-09 eklendi)

| Yetenek | Endpoint | Not |
|---------|----------|-----|
| Ödev tamamla | `POST /api/assignments/{assignmentId}/complete` | `MarkCompleted()`; yalnızca ödevin öğrencisi/admin |
| Ödev çözümü yükle | `POST /api/assignments/{assignmentId}/submission` (multipart `file`) | **P04 (2026-09-03):** ortak `IFileStorage` (`assignments/{assignmentId}/` öneki); doğrulama `FileValidation` (10 MB + uzantı beyaz listesi → `storage.file_too_large` / `storage.unsupported_type` / `storage.empty_file`); yetki+doğrulama **yazmadan önce**; yeni dosya bağlanınca eskisi silinir. Teslim ödevi `InProgress` yapar; `AttachmentUrl` = indirme endpoint'i |
| Teslim dosyasını indir | `GET /api/assignments/{assignmentId}/attachment` | `GetAssignmentAttachmentQuery` (authorizer: ödevin öğrencisi/öğretmeni/Admin) → anahtar çözülür → `IFileStorage.OpenReadAsync` ile akıtılır. **Public URL yok.** Ek yoksa/dosya kayıpsa `assignments.attachment_not_found` (404) |

> Sahiplik `AssignmentStudentActionAuthorizer` ile: tamamlama/teslim yalnızca öğrenci; indirme öğrenci+öğretmen.
> Dosya önce belleğe alınır, komut/yetki başarılıysa diske yazılır (başka öğrencinin dosyasının üzerine
> yazılması engellenir). Maks. 20 MB. **Depolama** yerel disk ile başlar; üretimde nesne depolamaya (S3/Blob) geçilebilir.

### 3.3 ✅ Öğretmen ödev onay / geri gönder (2026-07-18 eklendi — T-06.7/8)

| Yetenek | Endpoint | İstek | Not |
|---------|----------|-------|-----|
| Ödevi onayla | `POST /api/assignments/{assignmentId}/approve` | `ApproveAssignmentRequest { Feedback? }` | `Approve()`; `Status = Approved`, `TeacherFeedback` set edilir |
| Ödevi geri gönder | `POST /api/assignments/{assignmentId}/return` | `ReturnAssignmentRequest { Feedback }` | `ReturnForRevision()`; `Status = ReturnedForRevision`; **geri bildirim zorunlu** (`assignments.feedback_required` → 400) |
| Ödevi puanla (M06-4) | `POST /api/assignments/{assignmentId}/grade` | `GradeAssignmentRequest { Score, Feedback? }` | `Grade()`; **0–100** ve yalnız `Approved` durumunda; `Score`/`GradedOnUtc` set edilir, `AssignmentGradedDomainEvent` yayılır |

> Sahiplik `AssignmentTeacherAuthorizer` ile: yalnızca ödevin öğretmeni (veya admin) onay/geri gönder yapabilir
> (`assignment.TeacherUserId == currentUser`); aksi halde `shared.forbidden` (403). Her ikisi de `AssignmentResponse` döner.
> Geri gönderilen ödev, öğrenci yeniden teslim ettiğinde (`SubmitWork`) `InProgress`'e döner.

### 3.4 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Ödev güncelle/iptal | `PUT /api/assignments/{assignmentId}` | Başlık/açıklama/son teslim/iptal |
| ~~Kaynak ekle/listele~~ ✅ **eklendi (2026-09-03, P04/M06-1)** | `POST /api/assignments/lesson-resources` (multipart: `title`, `description`, `url` **veya** `file`, `studentId?`, `lessonSessionId?`) · `GET /api/assignments/lesson-resources?teacherUserId=&studentId=&lessonSessionId=` · `GET /api/assignments/lesson-resources/{resourceId}/file` (yetkili indirme) · `DELETE /api/assignments/lesson-resources/{resourceId}` (arşivle) | — |
| ~~Yükleme değerlendir~~ ✅ **eklendi (2026-09-04, P06/M06-4)** | `POST /api/assignments/{assignmentId}/grade` | — |
| Öğrenci not/kaynak görünümü | `GET /api/assignments/students/{studentId}/notes`, `.../resources` | Öğrenci kendi not/kaynaklarını görür |

---

## 4. İş Kuralları

1. **Önkoşul: tamamlanmış oturum (🟢 kodda):** Follow-up yalnızca `LessonSession.IsCompleted` ise oluşturulur; aksi halde `assignments.lesson_session_not_completed`.
2. **1 oturum : 1 not (🟢 kodda):** Bir oturumun tek `LessonNote`'u olur. Not + ödev birlikte oluşturulduktan sonra tekrar follow-up → `assignments.follow_up_exists` (409).
3. **Idempotent tamamlama davranışı (🟢 kodda):** Not zaten var ama **henüz ödev yoksa**, ikinci çağrı notu günceller ve gönderilen ödevleri **ekler** (append). Not + ödev birlikte varsa 409 döner.
4. **Otomatik özet üretimi (🟢 kodda):** `GET follow-up` çağrıldığında not yoksa ama oturum tamamlanmışsa, sistem otomatik bir `LessonNote` üretir (`BuildAutoSummary`: önce `TeacherNotes`, yoksa `CoveredContent`, yoksa `"{TopicTitle} konusu tamamlandı."`).
5. **Ödev başlangıç durumu (🟢 kodda):** Yeni ödev `Pending` ile başlar.
6. **`AttachmentUrl` sahipliği (🟢 kodda):** Bu alan **öğretmen** tarafının ekidir (öğrencinin teslimi değil). Öğrenci teslimi 2026-09-03'te (P04) ayrı `Attachment*` alanlarına taşındı; tam ayrım için hâlâ ayrı bir `AssignmentSubmission` aggregate'i düşünülebilir (⚠️).
7. **Son teslim uyarısı (🟢 kodda, 2026-09-04 / M06-3):** `AssignmentDueScanner` **saatte bir** koşar. Teslime **24 saat** kalan ve hâlâ teslim edilmemiş ödev için `AssignmentDueSoonDomainEvent`, vadesi geçip teslim edilmemiş ödev için `AssignmentMissedDomainEvent` üretir — her biri **ödev başına bir kez** (`DueSoonNotifiedOnUtc`/`MissedNotifiedOnUtc`). Teslim edilmiş (`InProgress`/`Completed`/`Approved`) ve iptal edilmiş ödevler taranmaz. Bildirim taramada gönderilmez; olaylar outbox → M11 tüketicilerine düşer.
7a. **Puanlama (🟢 kodda, 2026-09-04 / M06-4):** Puan **0–100** ve yalnız `Approved` durumundaki ödeve verilir; tekrar puanlama serbesttir (son puan geçerlidir).
8. **⚠️ Öğrenci yükleme zorunluluğu:** Öğrenci ödevini `AssignmentSubmission` ile yükler; geç teslim `Late` işaretlenir.
9. **Sahiplik (yetki):** Öğretmen yalnızca kendi öğrencisine ödev/not verir; öğrenci yalnızca kendi ödev/not/kaynaklarını görür (`AssignmentPolicies.cs`).

---

## 5. Olay Akışı (Event-Driven)

```
M05: LessonSessionCompletedDomainEvent
   → Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler
       → ders sonrası NOT + ÖDEV akışını mümkün kılar (follow-up oluşturulabilir hale gelir)

POST follow-up → not + ödev(ler)
   → LessonNoteCreatedDomainEvent
   → AssignmentCreatedDomainEvent (her ödev için)
       → (öneri) m11 Notifications: öğrenciye "yeni ödev" bildirimi
       → (öneri) m09 Parents: veliye "çocuğunuza ödev verildi"

AssignmentDueScanner (saatlik, M06-3)
   → AssignmentDueSoonDomainEvent  → m11 AssignmentDueSoonNotificationHandler → öğrenciye "teslime 24 saat kaldı"
   → AssignmentMissedDomainEvent   → m11 AssignmentMissedNotificationHandler  → öğrenciye + onaylı Premium veliye
                                      ("kaçırılan ödev" tercihi açıksa; M09-3)

POST /assignments/{id}/grade (M06-4)
   → AssignmentGradedDomainEvent

(öneri) Öğrenci ödev yükledi (AssignmentSubmission)
   → AssignmentSubmittedDomainEvent → öğretmene bildirim
POST /assignments/{id}/complete
   → AssignmentCompletedDomainEvent
       → (öneri) m10 ProgressTracking: tamamlanan ödev gelişime işlenir
```

> Çapraz-modül oturum erişimi `ILessonSessionAccessService` (`LessonSessionAccess.cs`) ile yapılır.
> Olaylar **Outbox** ile yayılır.

---

## 6. Mobil Ekranlar

### ✅ Mevcut
| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/assignments` | `AssignmentsPage` | Öğretmenin tüm ödevleri; filtre (Tümü/Bekleyen/Devam/Tamamlanan), özet kartı, shimmer yükleme. **Karta dokununca değerlendirme alt sayfası** (puan kaydırıcısı 0–100 + geri bildirim); ödev onaylı değilse önce `approve`, sonra `grade` çağrılır (M06-4). Puanlanmış ödevde puan rozeti görünür |
| `/student/assignments` | `StudentAssignmentsPage` | Öğrencinin kendi ödevleri; çözüm yükleme + tamamlama, **puan rozeti + öğretmen geri bildirimi** (M06-4) |
| `/assignments/new`, `/assignments/:lessonSessionId` | `AssignmentFollowUpPage` | Ders takibi: ders notu (özet + işlenen konular + öneriler) + çoklu ödev formu |
| `/lesson-notes/new` | `LessonNoteFormPage` | Gerçek tamamlanmış oturum/öğrenci seçimi; not API’ye kaydedilir. Mevcut konu/öneri/görünürlük korunur; ödev içeren takipte kayıt engellenir. Hata sonrası tekrar deneme girilen metni korur. Detay: [`lesson_note_form`](../pages/lesson_note_form.md) |
| `/lesson-sessions/detail/note` | `LessonNoteViewPage` | Not görüntüleme |

**Cubit'ler:**
- `AssignmentsListCubit` / `AssignmentsListState` — `GET /api/assignments?teacherUserId=` listeler.
- `AssignmentFollowUpCubit` / `AssignmentFollowUpState` — mevcut (follow-up kaydetme/yükleme).

**Domain:**
- `AssignmentItem`: `id, title, description, studentId, dueDateUtc, attachmentUrl, status, createdOnUtc, teacherFeedback, score, gradedOnUtc` (`isGraded` yardımcısı).
- `LessonNote`: yeni domain sınıfı (`id, lessonSessionId, summary, coveredTopics, recommendations, createdOnUtc`).
- `AssignmentRepository`: `listByTeacher(teacherUserId)`, `approveAssignment(id, feedback?)`, `gradeAssignment(id, score, feedback?)` (M06-4).

> `mobile/lib/features/assignments`, `flutter_bloc` (Cubit).

### ⚠️ Planlanan
- ✅ **Kaynak paylaşımı** (`LessonResource`, 2026-09-03): `LessonResourcesSection` — dosya (`file_picker`) veya bağlantı ekleme, uzun listede silme; bağlantılar `url_launcher` ile açılır, dosyalar yetkili uçtan indirilir.
- **Öğrenci tarafı:** "Ödevlerim", "Notlarım", "Kaynaklarım" listeleri + ödev **yükleme** (dosya seç).
- **Ödev durum yönetimi:** tamamla / geç teslim rozeti / son teslim geri sayımı.
- **Ödev detay sayfası** (assignment detail, status update).
- **Veli görünümü:** çocuğun ödev durumu + geç teslim uyarısı.

---

## 7. Kabul Kriterleri

- [x] Tamamlanmış derse not + ödev (follow-up) eklenebilir.
- [x] Aynı oturuma çift follow-up engellenir (409).
- [x] Not yoksa GET'te otomatik özet üretilir.
- [x] Öğretmen ödev/not listeleyebilir (teacherUserId/studentId/lessonSessionId).
- [x] Öğretmen eki (`AttachmentUrl`) paylaşabilir.
- [x] Öğretmen not görünürlüğünü (`Private`/`Student`/`StudentAndParent`) seçebilir (B-05).
- [x] Öğretmen ödevi onaylayabilir / geri bildirimle geri gönderebilir (T-06.7/8); geri gönderme için geri bildirim zorunlu.
- [x] Ödev "tamamla" endpoint'i (`POST /{id}/complete` → `MarkCompleted`) — 2026-07-09 eklendi.
- [x] `LessonResource` (ders kaynağı) — paylaşım + öğrenci görünümü **(2026-09-03, P04/M06-1)**; mobilde `LessonResourcesSection` öğretmen ders detayında "Kaynaklar" sekmesi, öğrenci ders detayında liste olarak.
- [x] **Öğrenci teslimi + değerlendirme** — `POST /{assignmentId}/submission` (dosya) → `Assignment.SubmitWork` + `AssignmentSubmittedDomainEvent`; öğretmen `approve`/`return` ile değerlendirir. (Ayrı `AssignmentSubmission` varlığı yok; teslim alanları `Assignment` üzerinde tutuluyor.)
- [x] **Son teslim uyarısı** (öğrenci + veli bildirimi) — `AssignmentDueScanner` + iki M11 tüketicisi **(2026-09-04, P06/M06-3)**.
- [x] **Ödev puanlama** (0–100, onaylı ödeve) + mobil değerlendirme akışı **(2026-09-04, P06/M06-4)**.
- [x] **Öğrenci görünümü uçları** — `GET /` (`studentId` filtresi ile `ListAssignmentsQuery`) + `GET /{assignmentId}/attachment` + `GET /lesson-resources?studentId=` (2026-09-03, P04).
- [x] Dosya depolama altyapısı **(P04)** — ortak `IFileStorage`: `LocalFileStorage` (dev) + `S3FileStorage` (S3/R2/MinIO).

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. ✅ **Dosya depolama altyapısı** — ortak `IFileStorage` (P04); bkz. [`mimari_inceleme.md`](mimari_inceleme.md).
2. **`AssignmentSubmission` (öğrenci yükleme)** + öğretmen değerlendirme + durum güncelleme.
3. ✅ **`LessonResource` (ders kaynağı)** — aggregate + 4 endpoint + öğrenci görünümü **eklendi (2026-09-03, P04/M06-1)**.
4. **Ödev güncelle/iptal endpoint'leri** (`PUT/DELETE /{id}`). Ödev "tamamla" (`POST /{id}/complete`) zaten mevcut (2026-07-09).
5. ✅ **Son teslim uyarı otomasyonu** — `AssignmentDueScanner` + m11 tüketicileri (öğrenci + veli) **eklendi (2026-09-04, P06/M06-3)**.
6. **Öğrenci görünümü endpoint'leri** (kendi ödev/not/kaynak) + yetkilendirme testleri.

---

## 9. İlişkili Dokümanlar

- Tamamlama → follow-up tetikleyicisi → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Bildirim (son teslim) → [`m11_notifications.md`](m11_notifications.md) · Veli uyarısı → [`m09_parents.md`](m09_parents.md)
- Gelişim takibi → [`m10_progress_tracking.md`](m10_progress_tracking.md) · Öğrenci çalışma → [`m08_study.md`](m08_study.md)
- Roller → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md)
- Veri modeli → [`veri_modeli.md`](veri_modeli.md) · Mimari (dosya depolama) → [`mimari_inceleme.md`](mimari_inceleme.md) · Genel → [`00_genel_bakis.md`](00_genel_bakis.md)
- PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) · UI → [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md)

---

*Ödev, Not & Kaynak (M06) — Detaylı Tasarım | Güncelleme: 2026-11-15 (mobil ders notu formu mevcut API’ye bağlandı; gerçek oturum seçimi, kayıt hataları, metadata/ödev koruması) · 2026-09-04 (P08 Task 4 / M14-4: `IStudentAssignmentDigestDirectory` sözleşmesi eklendi — bir dönemdeki toplam/tamamlanan ödev sayısı Reporting'in PDF raporuna açıldı; dönem ölçütü teslim tarihi, yoksa oluşturulma tarihi, tamamlanmış = `Completed`+`Approved`) · 2026-09-04 (P06/M06-3: `AssignmentDueScanner` — 24 saat kala "teslime az kaldı", vade geçince "teslim edilmedi", ödev başına bir kez; iki M11 tüketicisi (öğrenci + Premium veli). P06/M06-4: `Assignment.Grade` 0–100 + `POST /{id}/grade` + `Score`/`GradedOnUtc`; mobilde öğretmen değerlendirme alt sayfası ve öğrenci puan rozeti) · 2026-09-03 (P04/M06-1: `LessonResource` aggregate + 4 uç + mobil `LessonResourcesSection`; P04/C-02: ödev teslimi ortak `IFileStorage`'a taşındı — `AttachSubmission` + `Attachment*` alanları + `GetAssignmentAttachmentQuery`; `IAssignmentFileStorage`/`LocalAssignmentFileStorage` silindi; göç: `scripts/migrate-assignment-files.sh`) · 2026-09-02 (F-03: öğrenci teslimi + öğrenci görünümü uçları kodda; dosya depolama P04'e etiketlendi) · 2026-08-19 (kod-senkron: API endpoint sayısı 3→8 düzeltildi — complete/submission/attachment/approve/return kodda; "tamamla endpoint'i eksik" iddiaları kapatıldı) · 2026-07-19 (Veli V-F: `IStudentNotesDirectory` — veliye görünür öğretmen notları yalnız `LessonNoteVisibility ∈ {Student, StudentAndParent}`; `Private` asla; Shared.Contracts) · 2026-07-18 (Dilim B: not görünürlüğü B-05 + ödev onay/geri gönder + geri bildirim T-06.7/8)*

*Güncelleme: 2026-11-15 — P18 Task 1: xmin iyimser eşzamanlılık koruması ve 409 sözleşmesi.*
