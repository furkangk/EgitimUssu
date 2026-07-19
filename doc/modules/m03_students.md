# 🎓 Öğrenci Profili (Students) Modülü (M03) — Detaylı Tasarım Dokümanı

> **PRD: M03 Öğrenci Profili** · **Faz: 1 (öğretmen tarafı 🟢) / öğrenci self-register 🟡** · **Durum: Öğretmenin manuel öğrenci eklemesi uçtan uca çalışır; çoklu öğretmen bağlantısı (`TeacherStudentLink`), free limit=5, arşivleme, öğrenci bazlı ücret ve davet/kabul akışı eklendi (Dilim C, 2026-07-18); öğrencinin kendi kaydı domain'de var, mobil akışı tamamlanmadı**
>
> **Amaç:** Bir öğrencinin platformdaki kimlik kartını (ad, sınıf, hedef, branşlar) **iki farklı yoldan** oluşturup yönetmek:
> (1) **öğretmen manuel ekler** (`TeacherManaged`), (2) **öğrenci kendi kaydolur** (`SelfRegistered`).
> Öğrenci profili; ders planlama (M04), ders oturumu (M05), ödev/not (M06), ödeme (M07) ve veli paneli (M09) için
> merkezi referanstır.
>
> **🔑 Ürün kuralı (kanonik):** Öğrenci **manuel** (kayıtlı kullanıcısı olmayan) olabilir; ancak **veli yalnızca gerçek,
> kayıtlı bir kullanıcı** olabilir. `ParentUserId`, daima Identity'deki bir `UserAccount`'a (M01) işaret eder — bkz. [`m09_parents.md`](m09_parents.md).
>
> İlgili: [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`m01_identity.md`](m01_identity.md) · [`m02_teachers.md`](m02_teachers.md) · [`m04_scheduling.md`](m04_scheduling.md) · [`m08_study.md`](m08_study.md) · [`m09_parents.md`](m09_parents.md) · [`mimari_inceleme.md`](mimari_inceleme.md) · [`veri_modeli.md`](veri_modeli.md) · [`00_genel_bakis.md`](00_genel_bakis.md) · [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

`src/Modules/Students/` katmanları incelenerek çıkarılmıştır.

| Yetenek | Durum | Kanıt (kod) |
|---------|-------|-------------|
| Öğretmenin manuel öğrenci eklemesi (`TeacherManaged`) | ✅ var | `CreateStudentProfileCommandHandler` |
| Öğrencinin kendi kaydı (`SelfRegistered`) domain + yetki | ✅ var (akış 🟡) | `StudentOrigin.SelfRegistered`, authorizer dalı |
| Profil getirme (studentId ile) | ✅ var | `GetStudentProfileByIdQueryHandler` |
| Profil getirme (userId ile) | ✅ var | `GetStudentProfileByUserIdQueryHandler` |
| Öğretmenin öğrenci listesi | ✅ var | `ListStudentsByTeacherQueryHandler` |
| Branş + hedef seviye (`StudentSubject`) | ✅ var | `StudentProfile.Subjects` |
| Origin ↔ kimlik tutarlılığı kuralı | ✅ var | `students.invalid_origin` |
| Kullanıcı başına tek profil | ✅ var | `students.user_profile_exists` (409) |
| Veli referansı (gerçek kullanıcı) | ✅ var | `ParentUserId` (Guid?) |
| Komut + sorgu yetkilendirmesi | ✅ var | `CreateStudentProfileCommandAuthorizer`, `StudentProfileQueryAuthorizer` |
| **Öğrenci güncelleme / pasifleştirme** | ✅ var | `UpdateStudentProfileCommand`, `PUT /profiles/{studentId}`, sahiplik yetkisi |
| **Çoklu öğretmen bağlantısı** (`TeacherStudentLink`) | ✅ var (Dilim C) | Bir öğrenci birden fazla öğretmene bağlanabilir; listeleme link üzerinden |
| **Free plan limiti (5 öğrenci)** | ✅ var (Dilim C) | `students.free_limit_reached` (409); premium bypass yok — `// TODO(M17)` |
| **Öğrenci arşivleme / arşivden çıkarma** | ✅ var (Dilim C) | `Archive/Unarchive`; arşivli öğrenci varsayılan listede gizli, limit sayımına dahil |
| **Öğrenci bazlı ücret** (B-07) | ✅ var (Dilim C) | `AgreedRateAmount` + `Currency` link üzerinde |
| **Manuel öğrenciyi gerçek hesaba bağlama (davet/kabul)** | ✅ var (Dilim C) | `Invite` → `Accept`; kabul eden `currentUser` profile bağlanır (`LinkUser`) |
| **Davet kodu ile claim (Ö-C)** | ✅ var (Ö-C) | `InviteStudent` 6 haneli `InviteCode` üretir; öğrenci `ClaimStudentLinkCommand` ile kodu girip profili devralır (`POST /links/claim`) |
| **Tam profil birleştirme (merge, Ö-C/B5)** | ✅ var (Ö-C) | Öğrencinin mevcut self-profil'i varsa claim'de kanonik=self, manuel profil `MarkMerged`; `StudentProfilesMergedDomainEvent` → Outbox → modüller-arası `StudentId` yeniden atama |
| ContactEmail benzersizlik kontrolü | 🟡 kısmi | `ExistsByContactEmailAsync` repo'da var ama create handler'da kullanılmıyor |
| Self-register mobil akışı | 🟢 var | `by-user` çözümü + yoksa `SelfRegistered` otomatik oluşturma (`StudentRepository.getByUser`/`createSelfProfile`), öğrenci `study` feature ilk girişinde tetiklenir |

> **Özet:** Öğretmen tarafı (manuel ekle/**güncelle**/listele/getir) **çalışır durumdadır**. Self-register backend'de hazırdır ama
> **mobil akış ve manuel→gerçek bağlama** tamamlanmamıştır.

---

## 2. Domain Modeli

Kaynak: `src/Modules/Students/Domain/StudentsDomainModel.cs`. Şema: **`students`**.
Tablolar: `student_profiles`, `student_subjects`, `teacher_student_links`.

### 2.1 🟢 Mevcut (koddan) — `StudentProfile` (AggregateRoot&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Öğrenci profil kimliği |
| `UserId` | Guid? | Öğrenci kendi hesabıyla bağlıysa (self-registered) Identity kullanıcısı |
| `CreatedByTeacherUserId` | Guid? | Öğretmen eklediyse, ekleyen öğretmenin `UserId`'si |
| `ParentUserId` | Guid? | Bağlı veli — **yalnızca gerçek kullanıcı** (M01 `UserAccount`) |
| `FullName` | string | Ad soyad |
| `GradeLevel` | string | Sınıf/seviye (örn. "8. sınıf", "Lise 3") |
| `ContactEmail` | string? | İletişim e-postası |
| `ContactPhone` | string? | İletişim telefonu |
| `GoalSummary` | string? | Hedef özeti |
| `LevelNotes` | string? | Seviye/başlangıç notları |
| `Origin` | enum `StudentOrigin` | Profilin kaynağı (kim oluşturdu) |
| `TargetExam` | enum `TargetExam` | Öğrencinin hedeflediği sınav (S-03.9); varsayılan `None`. M08 net formülü ceza bölenini bundan türetir (`SetTargetExam`) |
| `IsActive` | bool | Aktif/pasif (create'te `true`) |
| `IsMerged` | bool | Profil başka bir kanonik profile birleştirildiyse `true` (Ö-C claim/merge); birleşince `IsActive=false` |
| `MergedIntoStudentId` | Guid? | Birleştirme sonrası kanonik (hedef) `StudentProfile.Id`; birleşmediyse null |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |
| `Subjects` | List&lt;`StudentSubject`&gt; | Branş + hedef seviye |

**Davranış:** Yapıcı `StudentProfileCreatedDomainEvent` yayar. `Update(...)` metodu tüm scalar alanları (ad, sınıf, iletişim, hedef, `IsActive`) günceller; `Subjects` yeniden yazımı `ReplaceSubjectsAsync` (repository) ile yapılır. `LinkParent(parentUserId, ...)` onaylı veli bağını kurar; **`LinkUser(userId, ...)`** (Dilim C) manuel öğrenciyi davet kabulünde gerçek öğrenci kullanıcısına bağlar (`UserId` set eder). **`MarkMerged(canonicalStudentId, ...)`** (Ö-C) bu manuel profili kanonik self-profil'e birleştirir: `IsMerged=true`, `MergedIntoStudentId` set, `IsActive=false` ve `StudentProfilesMergedDomainEvent` yayar (Outbox → modüller-arası `StudentId` yeniden atama).

### 2.2 🟢 Mevcut (koddan) — `StudentSubject` (Entity&lt;Guid&gt;)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Kayıt kimliği |
| `StudentProfileId` | Guid | Bağlı profil |
| `Subject` | string | Branş (boş/whitespace olanlar create'te atlanır) |
| `TargetLevel` | string? | Hedef seviye (opsiyonel) |

### 2.3 🟢 Mevcut (koddan) — Enum & Domain Event

| Enum | Değerler |
|------|----------|
| `StudentOrigin` | `TeacherManaged = 1`, `SelfRegistered = 2` |
| `TargetExam` | `None = 0`, `LGS = 1`, `TYT = 2`, `AYT = 3`, `YDS = 4`, `School = 5`, `Other = 6` (DB'de string; varsayılan `None`). M08 net böleni: LGS→3, TYT/AYT→4, School→yanlış götürmez |

```
StudentProfileCreatedDomainEvent(Guid StudentProfileId, Guid? UserId,
                                 Guid? CreatedByTeacherUserId, StudentOrigin Origin, DateTime CreatedOnUtc)
StudentProfilesMergedDomainEvent(Guid FromStudentId, Guid ToStudentId, DateTime OnUtc)   // Ö-C claim/merge
```

> **Merge event (Ö-C):** `StudentProfilesMergedDomainEvent`, iki öğrenci profili birleştirildiğinde yayılır.
> `Shared/Contracts`'taki `StudentProfilesMergedIntegrationEvent(FromStudentId, ToStudentId)` payload'ı ile eşleşir;
> tüketen modüller (Scheduling, Assignments, Payments, LessonSessions, Study) kendi `StudentId=FromStudentId` kayıtlarını
> kanonik `ToStudentId`'ye yeniden atar (bkz. §5).

### 2.4 🟢 Mevcut (koddan) — `TeacherStudentLink` (AggregateRoot&lt;Guid&gt;, Dilim C)

Öğretmen ile öğrenci arasındaki **çok-öğretmenli** ilişkiyi taşır. Bir öğrenci birden fazla öğretmene bağlanabilir;
öğretmenin öğrenci listesi bu tablo üzerinden yürür. Tablo: `teacher_student_links`. Benzersiz index: `(TeacherUserId, StudentId)`.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Bağlantı kimliği |
| `TeacherUserId` | Guid | Öğretmenin `UserId`'si |
| `StudentId` | Guid | Bağlı `StudentProfile.Id` |
| `AgreedRateAmount` | decimal? | Öğrenci bazlı anlaşılan ders ücreti (B-07), `numeric(12,2)` |
| `Currency` | string | Para birimi (varsayılan `"TRY"`) |
| `Status` | enum `TeacherStudentLinkStatus` | Bağlantı durumu (string olarak saklanır) |
| `IsArchived` | bool | Arşiv bayrağı (listede gizle; limit sayımını etkilemez) |
| `InviteTargetUserId` | Guid? | Davet belirli bir kullanıcıya yöneldiyse hedef |
| `InviteCode` | string? | Öğrencinin claim için gireceği 6 haneli davet kodu (Ö-C); `students` şemasında indeksli, `varchar(8)` |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |

**Davranış:** `SetRate(amount, currency, ...)`, `Archive(...)` / `Unarchive(...)`, `MarkInviteSent(inviteCode, targetUserId?, ...)` (→ `TeacherStudentInviteSentDomainEvent`; kod handler'da `GenerateInviteCode()` ile üretilir — 6 haneli rakam), `Accept(...)` (→ `TeacherStudentLinkAcceptedDomainEvent`), `Reject(...)`.

| Enum | Değerler |
|------|----------|
| `TeacherStudentLinkStatus` | `Manual = 1`, `InviteSent = 2`, `Linked = 3`, `Rejected = 4`, `Disconnected = 5` |

```
TeacherStudentInviteSentDomainEvent(Guid LinkId, Guid TeacherUserId, Guid StudentId, Guid? TargetUserId, DateTime OnUtc)
TeacherStudentLinkAcceptedDomainEvent(Guid LinkId, Guid TeacherUserId, Guid StudentId, DateTime OnUtc)
```

> **Geriye-uyum:** `AddTeacherStudentLinks` migration'ı, `CreatedByTeacherUserId` dolu mevcut manuel öğrenciler için
> `Manual` durumunda link üretir (backfill SQL, `gen_random_uuid()`). `CreatedByTeacherUserId` alanı korunur.
> `Disconnected` durumu tanımlıdır ancak bu dilimde bağlantı-kesme endpoint'i yoktur (ileride).

### 2.5 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Gerekçe |
|-------|---------|
| `UnlinkParent()` | Veli bağını çözme |
| `StudentProfileUpdatedDomainEvent` | Diğer modüllerin (M04/M06/M09) değişimi yakalaması için |
| Bağlantı-kesme ucu (`Disconnected`) | Öğretmen-öğrenci bağını sonlandırma (durum tanımlı, endpoint yok) |
| Premium limit bypass (M17) | Free limit=5 herkese uygulanır; premium gelince sınır kalkacak (`// TODO(M17)`) |

---

## 3. API Sözleşmesi

Tüm uçlar `RoutePrefix = /api/students` altında ve grup **`RequireAuthorization("AuthenticatedUser")`** ile korunur.
`Result<T>` döner; hata kodları `StudentsModule.ToHttpResult` ile HTTP'ye eşlenir.

### 3.1 Mevcut Endpoint'ler ✅

| Yetenek | Method + Route | Yetki kontrolü | İstek | Yanıt |
|---------|----------------|----------------|-------|-------|
| Öğrenci ekle | `POST /profiles` | `CreateStudentProfileCommandAuthorizer` | `CreateStudentProfileRequest` | `StudentProfileResponse` |
| Öğrenci güncelle / pasifleştir | `PUT /profiles/{studentId:guid}` | `UpdateStudentProfileCommandAuthorizer` | `UpdateStudentProfileRequest` | `StudentProfileResponse` |
| Öğrenci getir (id) | `GET /profiles/{studentId:guid}` | `StudentProfileQueryAuthorizer` | — | `StudentProfileResponse` |
| Öğrenci getir (userId) | `GET /profiles/by-user/{userId:guid}` | `StudentProfileQueryAuthorizer` | — | `StudentProfileResponse` |
| Öğretmenin öğrencileri | `GET /profiles/by-teacher/{teacherUserId:guid}?includeArchived=false` | `StudentProfileQueryAuthorizer` | — | `IReadOnlyCollection<StudentProfileSummaryResponse>` (link üzerinden) |
| Öğrenciyi arşivle | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/archive` | `TeacherStudentLinkAuthorizer` | — | `204` |
| Arşivden çıkar | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/unarchive` | `TeacherStudentLinkAuthorizer` | — | `204` |
| Öğrenci ücreti belirle (B-07) | `PUT /teachers/{teacherUserId:guid}/students/{studentId:guid}/rate` | `TeacherStudentLinkAuthorizer` | `SetStudentRateRequest` | `204` |
| Öğrenci davet et (B-06) | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/invite` | `TeacherStudentLinkAuthorizer` | `InviteStudentRequest` | `204` |
| Daveti kabul et | `POST /links/{linkId:guid}/accept` | `TeacherStudentLinkResponseAuthorizer` | — (`currentUser`) | `204` |
| Daveti reddet | `POST /links/{linkId:guid}/reject` | `TeacherStudentLinkResponseAuthorizer` | — (`currentUser`) | `204` |
| **Davet kodu ile profili devral (Ö-C)** | `POST /links/claim` | `TeacherStudentLinkResponseAuthorizer` (açık claim: kimliği doğrulanmış herhangi bir öğrenci; kod bilgisi sahiplik yerine geçer) | `ClaimLinkRequest` (`currentUser` = devralan) | `204` |

**Modüller-arası sözleşme (Shared.Contracts):** M03, öğrenci↔kullanıcı bağının otoritesidir ve
`IStudentDirectory` (`GetOwnerUserIdAsync(studentId) → Guid?`) sözleşmesini `StudentDirectory` ile uygular
(2026-07-07). Diğer modüller (ör. M04 Scheduling, öğrenci-kapsamlı ders listesi yetkilendirmesi) bu sözleşmeyi
tüketerek sahiplik doğrular — M03'ün `DbContext`'ine doğrudan erişmeden, proje referansı olmadan (anti-corruption).
Aynı desenin M05'teki karşılığı `ILessonSessionAccessService`.

**İstek/yanıt sözleşmeleri (koddan):**

```
StudentSubjectItem(string Subject, string? TargetLevel)

CreateStudentProfileRequest(Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                            string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                            string? GoalSummary, string? LevelNotes, StudentOrigin Origin,
                            IReadOnlyCollection<StudentSubjectItem> Subjects,
                            TargetExam TargetExam = TargetExam.None)

UpdateStudentProfileRequest(string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                            string? GoalSummary, string? LevelNotes, bool IsActive,
                            IReadOnlyCollection<StudentSubjectItem> Subjects,
                            TargetExam TargetExam = TargetExam.None)  // branşlar tam yeniden yazar

StudentProfileResponse(Guid Id, Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                       string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                       string? GoalSummary, string? LevelNotes, string Origin, bool IsActive,
                       string TargetExam,
                       IReadOnlyCollection<StudentSubjectResponse> Subjects, DateTime CreatedOnUtc, DateTime UpdatedOnUtc)

StudentProfileSummaryResponse(Guid Id, string FullName, string GradeLevel, string Origin,
                              bool IsActive, DateTime CreatedOnUtc,
                              bool IsArchived, decimal? AgreedRateAmount, string LinkStatus)  // FullName'e göre sıralı

SetStudentRateRequest(decimal AgreedRateAmount, string Currency)   // B-07
InviteStudentRequest(Guid? TargetUserId)                            // B-06; hedef opsiyonel (açık davet)
ClaimLinkRequest(string InviteCode)                                 // Ö-C; öğretmenin verdiği 6 haneli kod
```

### 3.2 Hata Kodları → HTTP Eşleme (koddan)

| Hata kodu | HTTP | Mesaj |
|-----------|------|-------|
| `students.user_profile_exists` | **409** | Bu kullanıcı için öğrenci profili zaten var. |
| `students.free_limit_reached` | **409** | Free planda en fazla 5 ogrenci ekleyebilirsiniz. Premium'a gecin. |
| `students.profile_not_found` | **404** | Öğrenci profili bulunamadı. |
| `students.link_not_found` | **404** | Ogrenci baglantisi bulunamadi. (arşiv/ücret/davet uçları) |
| `students.invite_not_found` | **404** | Davet kodu bulunamadi. (claim ucu) |
| `students.invite_invalid` | **400** | Davet kodu artik gecerli degil. (claim ucu; link `InviteSent` değil) |
| `shared.forbidden` | **403** | Bu işlemi yapma / bu kaynağa erişim yetkiniz yok. |
| `students.invalid_origin` | 400 | Öğrenci profili kaynağı ile kimlik bilgileri uyumsuz. |
| `students.invalid_request` | 400 | (Validator) Ad soyad ve sınıf seviyesi zorunlu. |

> **Not:** `StudentProfileQueryAuthorizer`, tekil getirme uçlarında profili önce yükler; bulunamazsa `shared.forbidden` yerine
> `students.profile_not_found` (404) döner (varlık sızdırmadan, sahip değilse yetki reddi).
>
> **Not (PUT):** `UpdateStudentProfileCommandAuthorizer` sahipliği kontrol eder: admin her zaman, öğretmen yalnızca kendi eklediği öğrenciyi (`CreatedByTeacherUserId == currentUserId`) güncelleyebilir. Branşlar PUT ile **tam yeniden yazılır** (merge değil).

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [ ] **`PATCH /profiles/{studentId}/status`** — ayrı pasifleştirme ucu (şimdilik PUT ile `isActive: false` göndererek yapılıyor).
- [x] **Manuel öğrenciyi gerçek hesaba bağlama** — `.../invite` + `/links/{linkId}/accept` ile karşılandı (Dilim C).
- [x] **Davet kodu ile devralma + profil birleştirme** — `.../invite` 6 haneli kod üretir; `POST /links/claim` ile öğrenci devralır, mevcut self-profil varsa merge (Ö-C).
- [ ] **`POST /profiles/{studentId}/link-parent`** — gerçek veli kullanıcısı bağlama (kural: veli daima kayıtlı kullanıcı).
- [ ] **`POST` / `DELETE /profiles/{studentId}/subjects`** — branş ekleme/çıkarma (şu an yalnız create'te).
- [ ] **`GET /profiles/by-parent/{parentUserId}`** — velinin çocuklarını listeleme (M09 ile).

---

## 4. İş Kuralları

1. **İki giriş yolu:** Öğrenci profili ya **öğretmen tarafından** (`TeacherManaged`) ya da **öğrencinin kendisi tarafından** (`SelfRegistered`) oluşturulur.
2. **Origin ↔ kimlik tutarlılığı (`students.invalid_origin`):**
   - `TeacherManaged` ise `CreatedByTeacherUserId` **zorunlu** (null olamaz).
   - `SelfRegistered` ise `UserId` **zorunlu** (null olamaz).
3. **Kullanıcı başına tek profil:** `UserId` doluysa, o kullanıcı için zaten profil varsa `students.user_profile_exists` (409).
4. **Manuel öğrenci, gerçek olmayabilir:** `TeacherManaged` profilde `UserId` **null** olabilir — öğrencinin platform hesabı olması gerekmez. Profil tamamen öğretmenin verisidir.
5. **🔑 Veli daima gerçek kullanıcı:** `ParentUserId`, varsa **Identity'deki gerçek bir `UserAccount`'a** işaret eder. Manuel/sahte veli kaydı yoktur. Veli bağlama akışı [`m09_parents.md`](m09_parents.md)'de detaylanır.
6. **Varsayılan aktiflik:** Create'te `IsActive = true` set edilir; sonradan PUT ile `isActive: false` göndererek pasifleştirilebilir.
7. **Branş yeniden yazımı:** PUT isteğindeki `Subjects` mevcut branşları **tamamen değiştirir** (merge değil). Boş/whitespace olanlar atlanır; kalanlar `Trim()` edilir.
8. **Validator:** `FullName` ve `GradeLevel` zorunlu (hem create hem update için `students.invalid_request`).
9. **Create yetkisi (`CreateStudentProfileCommandAuthorizer`):**
   - **Admin** → her zaman.
   - `TeacherManaged` + rol `Teacher` + `currentUserId == CreatedByTeacherUserId` → izin.
   - `SelfRegistered` + rol `Student` + `currentUserId == UserId` → izin.
   - Aksi halde `shared.forbidden`.
10. **Okuma yetkisi (`StudentProfileQueryAuthorizer`):**
    - Tekil getirme: profil yüklenir; yoksa `students.profile_not_found`. **Admin** veya `currentUserId`'nin profilin `UserId` / `CreatedByTeacherUserId` / `ParentUserId` alanlarından biriyle eşleşmesi gerekir.
    - Liste (`by-teacher`): **admin** veya `Teacher` rolü + `currentUserId == teacherUserId` (kendi listesi).
11. **Güncelleme yetkisi (`UpdateStudentProfileCommandAuthorizer`):**
    - **Admin** → her zaman.
    - `Teacher` rolü + `CreatedByTeacherUserId == currentUserId` → yalnız kendi eklediği öğrenci.
    - Aksi halde `shared.forbidden` (403). Diğer öğretmen 403 alır (sahiplik testi entegrasyon testinde doğrulanmıştır).
12. **Sahiplik üçlüsü:** Bir profile erişebilen taraflar = bağlı öğrenci (UserId) + ekleyen öğretmen (CreatedByTeacherUserId) + bağlı veli (ParentUserId). Bu, öğretmen-öğrenci-veli ilişkisinin yetki temelidir.
13. **🔑 Çoklu öğretmen (Dilim C):** Bir öğrenci **birden fazla öğretmene** bağlanabilir; her bağ ayrı bir `TeacherStudentLink`'tir. Öğretmenin öğrenci listesi `CreatedByTeacherUserId` yerine link tablosu üzerinden yürür. `(TeacherUserId, StudentId)` benzersizdir (aynı öğretmen-öğrenci ikilisi tek link).
14. **Free limit=5 (`students.free_limit_reached`):** Bir öğretmen `TeacherManaged` öğrenci eklerken aktif (reddedilmemiş) link sayısı 5'e ulaştıysa yeni ekleme 409 ile reddedilir. **Arşivli linkler limite dâhildir** (arşiv, kotayı boşaltmaz). Premium bypass yoktur (`// TODO(M17)`).
15. **Arşivleme (B-04):** `Archive`/`Unarchive` link'in `IsArchived` bayrağını değiştirir. Arşivli öğrenci varsayılan listede gizlenir; `?includeArchived=true` ile görünür. Reddedilmiş (`Rejected`) linkler listede ve limitte sayılmaz.
16. **Öğrenci bazlı ücret (B-07):** `SetRate(amount, currency)` link üzerinde anlaşılan ders ücretini tutar; ücret öğrenci-öğretmen ikilisine özeldir (öğretmen bazlı sabit ücrete bağlı değil).
17. **Davet/kabul (B-06):** Öğretmen mevcut link'i `Invite` ile `InviteSent` yapar (hedef kullanıcı opsiyonel). Kabulde (`Accept`) link `Linked` olur ve **kabul eden `currentUser`** öğrenci profiline `LinkUser` ile bağlanır. Belirli hedef varsa yalnız o kullanıcı yanıtlayabilir; admin serbest. Identity'de e-posta/telefonla kullanıcı araması **yapılmaz** (kararı: 2026-07-18).
18. **🔑 Davet kodu ile claim (Ö-C):** `Invite`, link'e 6 haneli rakamsal `InviteCode` yazar. Öğrenci `POST /links/claim` ile kodu girer (`ClaimStudentLinkCommand`, `ClaimingUserId = currentUser`). Handler: kod yoksa `students.invite_not_found` (404); link `InviteSent` değilse `students.invite_invalid` (400); aksi halde `link.Accept()`. Kod bilgisi sahiplik kanıtı yerine geçer (açık claim); yalnızca kimlik doğrulaması gerekir.
19. **🔑 Tam profil birleştirme (merge, Ö-C/B5):** Claim anında öğrencinin **mevcut bir self-profil'i** (`GetByUserIdAsync(ClaimingUserId)`) varsa **kanonik = self-profil**; manuel profil `MarkMerged(self.Id)` ile pasifleşir ve `StudentProfilesMergedDomainEvent(FromStudentId=manuel, ToStudentId=self)` yayılır (Outbox). Self-profil **yoksa** manuel profil doğrudan devralınır (`LinkUser`). Merge **her zaman öğrenci onayıyla** (kod girişi); kişisel not/paylaşım kanonik profile taşınır. Modüller-arası `StudentId` yeniden atama §5'te.

---

## 5. Olay Akışı (Event-Driven)

```
Öğrenci profili oluşturuldu → StudentProfileCreatedDomainEvent
                              (StudentProfileId, UserId?, CreatedByTeacherUserId?, Origin, CreatedOnUtc)
                              → Outbox → (gelecek) Notifications (M11): öğretmene/veliye bilgilendirme
                              → (gelecek) Matching (M12): SelfRegistered öğrenci için öğretmen önerisi
                              → (öneri) Parents (M09): veli bağlandığında çocuk gelişim akışını başlatma
Öğretmen daveti gönderdi     → TeacherStudentInviteSentDomainEvent (LinkId, TeacherUserId, StudentId, TargetUserId?, OnUtc)
                              → Outbox → (gelecek) Notifications (M11): öğrenciye/veliye davet bildirimi
Davet kabul edildi           → TeacherStudentLinkAcceptedDomainEvent (LinkId, TeacherUserId, StudentId, OnUtc)
                              → kabul eden currentUser profile LinkUser ile bağlanır
Profiller birleştirildi      → StudentProfilesMergedDomainEvent (FromStudentId=manuel, ToStudentId=self, OnUtc)
                              → Outbox → StudentProfilesMergedIntegrationEvent (Shared.Contracts)
                              → Scheduling / Assignments / Payments / LessonSessions / Study handler'ları:
                                UPDATE ... SET StudentId=ToStudentId WHERE StudentId=FromStudentId
                                (Study.StudyStudent PK=StudentId olduğundan kaynak satır silinir; kanonik korunur)
                              → veli paneli tek kanonik StudentId'den beslenir (veri bölünmesi biter — B-01/AKIŞ 3)
```

> Olaylar **Outbox pattern** ile yayılır (`Shared/Infrastructure/Messaging`). Merge event'i **aktif olarak tüketilir**:
> her modül `IIntegrationEventHandler` uygular (`<Module>StudentMergedHandler`), `SourceModule=="Students" && Name=="StudentProfilesMergedDomainEvent"`
> eşleşmesinde `ExecuteUpdateAsync`/`ExecuteDeleteAsync` ile toplu yeniden atama yapar. Davet bildirimleri (M11) ve
> eşleştirme (M12) için diğer olaylar hâlâ doğal entegrasyon noktasıdır.

---

## 6. Mobil Ekranlar (mevcut + planlanan)

`mobile/lib/features/students/` (flutter_bloc/Cubit) — şu an **öğretmen perspektifli**.

| Route | Sayfa | Durum | Açıklama |
|-------|-------|-------|----------|
| `/students` | `StudentsPage` | ✅ | Öğretmenin öğrenci listesi (`by-teacher`) |
| `/students/:studentId` | `StudentDetailPage` | ✅ | Öğrenci detayı + düzenleme formu (`_EditStudentSheet`) + aktif/pasif toggle |

### Eksik / planlanan mobil ekranlar ⚠️
- [ ] **Öğrenci ekleme formu zenginleştirme** — `StudentSubject` (branş + hedef seviye) çoklu giriş + **veli bağlama** alanı.
- [ ] **Öğrenci self-register akışı** — öğrencinin kendi profilini oluşturduğu ekran (`SelfRegistered`).
- [ ] **Manuel→gerçek bağlama** — öğretmenin öğrenciyi davet etmesi / öğrencinin daveti kabul etmesi.

---

## 7. Kabul Kriterleri

- [x] Öğretmen, kendi `UserId`'siyle `TeacherManaged` öğrenci ekleyebilir ve listeleyebilir.
- [x] Öğrenci, `SelfRegistered` olarak kendi profilini (backend'de) oluşturabilir.
- [x] Origin ile kimlik alanları tutarsızsa `students.invalid_origin` döner.
- [x] Aynı kullanıcı için ikinci profil 409 ile engellenir.
- [x] Profil erişimi sahiplik üçlüsü (öğrenci/öğretmen/veli) + admin ile sınırlıdır.
- [x] Veli alanı yalnızca gerçek kullanıcı kimliği (`ParentUserId`) ile doldurulur.
- [x] **Öğrenci güncellenebilir / pasifleştirilebilir** — `PUT /profiles/{studentId}`, `IsActive`, branş yeniden yazımı, sahiplik yetki testi.
- [x] **Başka öğretmen başkasının öğrencisini güncelleyemez** (403) — entegrasyon testinde doğrulandı.
- [x] **Bir öğrenci birden fazla öğretmene bağlanabilir** — `TeacherStudentLink` (Dilim C).
- [x] **Free planda öğretmen en fazla 5 öğrenci ekleyebilir** — `students.free_limit_reached` (409), birim testinde doğrulandı.
- [x] **Öğrenci arşivlenip arşivden çıkarılabilir**; arşivli varsayılan listede gizli, `includeArchived=true` ile görünür — birim testinde doğrulandı.
- [x] **Öğretmen öğrenci bazlı ücret belirleyebilir** (B-07) — link üzerinde `AgreedRateAmount`/`Currency`.
- [x] **Manuel öğrenci, davet/kabul ile gerçek öğrenci hesabına bağlanabilir** (B-06) — kabul eden `currentUser` profile bağlanır; birim testinde doğrulandı.
- [x] **Öğrenci 6 haneli davet koduyla profili devralır** (Ö-C) — `POST /links/claim`; geçersiz/eksik kod `invite_not_found`/`invite_invalid`; birim testinde doğrulandı.
- [x] **Mevcut self-profil varsa claim'de profiller birleşir** (Ö-C/B5) — manuel profil `IsMerged`, `StudentProfilesMergedDomainEvent` yayılır; birim testinde doğrulandı. Modüller-arası `StudentId` yeniden atama Testcontainers entegrasyon testinde doğrulanır (gerçek Postgres gerekli).
- [x] **Self-register mobil akışı** uçtan uca çalışır — öğrenci ilk `study` girişinde profili yoksa `SelfRegistered` olarak oluşturulur (`getByUser` → yoksa `createSelfProfile`).

---

## 8. Eksikler ve Yapılacaklar (öncelik sırasıyla)

1. **Davet bildirimleri (M11)** — `TeacherStudentInviteSentDomainEvent`/`...AcceptedDomainEvent` için Outbox tüketicisi; öğrenciye/veliye davet bildirimi. Manuel→gerçek bağlama akışı (Dilim C) domain'de tamam, bildirim tarafı eksik.
2. **Bağlantı-kesme ucu** — `Disconnected` durumu için endpoint (öğretmen-öğrenci bağını sonlandırma).
3. **Veli bağlama ucu** — `link-parent` + gerçek kullanıcı doğrulaması (M01 ile); kuralı domain'de zorla.
4. **`StudentSubject` yönetimi** — branş ekle/sil uçları (create dışında).
5. **Self-register mobil akışı** — öğrenci kendi profil ekranı; öğrenci paneli ([`../roles/ogrenci.md`](../roles/ogrenci.md)) ve bireysel çalışma (M08) ile bağ.
6. **ContactEmail benzersizliği** — repo'daki `ExistsByContactEmailAsync` create handler'a bağlanmalı (mükerrer iletişim engeli) ya da bilinçli olarak kaldırılmalı.
7. **`by-parent` listesi** — velinin çocuklarını getirme (M09).

---

## 9. İlişkili Dokümanlar

- Öğrencinin uçtan uca yolculuğu → [`../roles/ogrenci.md`](../roles/ogrenci.md)
- Velinin yolculuğu ve "veli daima gerçek kullanıcı" kuralı → [`../roles/veli.md`](../roles/veli.md), [`m09_parents.md`](m09_parents.md)
- Öğretmenin öğrenci yönetimi perspektifi → [`../roles/ogretmen.md`](../roles/ogretmen.md), [`m02_teachers.md`](m02_teachers.md)
- Kimlik/oturum ve gerçek kullanıcı temeli → [`m01_identity.md`](m01_identity.md)
- Ders planlama → [`m04_scheduling.md`](m04_scheduling.md)
- Bireysel çalışma (öğrenci tarafı) → [`m08_study.md`](m08_study.md)
- Aggregate ER şeması ve modüller arası referanslar → [`veri_modeli.md`](veri_modeli.md)
- Mimari/güvenlik inceleme → [`mimari_inceleme.md`](mimari_inceleme.md)
- Genel durum ve endpoint envanteri → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

*Öğrenci Profili (Students) Modülü (M03) — Detaylı Tasarım | Güncelleme: 2026-07-19 (Ö-C: davet kodu `InviteCode` + kod tabanlı claim `POST /links/claim` + tam profil birleştirme merge `StudentProfilesMergedDomainEvent` → modüller-arası `StudentId` yeniden atama; Ö-B: `TargetExam` hedef sınavı S-03.9 — M08 net formülü böleni; Dilim C: `TeacherStudentLink` çoklu öğretmen bağlantısı, free limit=5, arşivleme, öğrenci bazlı ücret B-07, davet/kabul B-06)*
