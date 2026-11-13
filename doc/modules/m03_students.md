---
title: "M03 — Öğrenci Profili (Students)"
summary: "Öğretmenin manuel öğrenci eklemesi uçtan uca çalışır; çoklu öğretmen bağı + free limit + arşivleme + davet/claim var, öğrenci self-register mobil akışı tamamlanmadı"
tags: [modul, students, profil, veli-bagi, faz-1]
status: "🟢/🟡"
authority: code
code_refs:
  - src/Modules/Students/**
updated: 2026-11-14
---

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
> İlgili: [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md) · [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`m01_identity.md`](m01_identity.md) · [`m02_teachers.md`](m02_teachers.md) · [`m04_scheduling.md`](m04_scheduling.md) · [`m08_study.md`](m08_study.md) · [`m09_parents.md`](m09_parents.md) · [`mimari_inceleme.md`](mimari_inceleme.md) · [`veri_modeli.md`](veri_modeli.md) · [`00_genel_bakis.md`](00_genel_bakis.md) · [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

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
| **Free plan limiti (5 öğrenci)** | ✅ var (Dilim C) | `students.free_limit_reached` (409); sınır **`IEntitlementDirectory.MaxStudents`**'ten okunur — Premium öğretmende sınır yok (P09 Task 2 / M03-4) |
| **Öğrenci arşivleme / arşivden çıkarma** | ✅ var (Dilim C) | `Archive/Unarchive`; arşivli öğrenci varsayılan listede gizli, limit sayımına dahil |
| **Öğrenci bazlı ücret** (B-07) | ✅ var (Dilim C) | `AgreedRateAmount` + `Currency` link üzerinde |
| **Manuel öğrenciyi gerçek hesaba bağlama (davet/kabul)** | ✅ var (Dilim C) | `Invite` → `Accept`; kabul eden `currentUser` profile bağlanır (`LinkUser`) |
| **Davet kodu ile claim (Ö-C)** | ✅ var (Ö-C) | `InviteStudent` 8 karakterli güvenli `InviteCode` üretir (P15 / N-20: `ISecureCodeGenerator`, 7 gün geçerli); öğrenci `ClaimStudentLinkCommand` ile kodu girip profili devralır (`POST /links/claim`) |
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
| `DateOfBirth` | `DateTime?` | **Doğum tarihi (opsiyonel, Veli V-A 2026-07-19).** DB `date`; yaş türetimi + veli claim eşleşmesi temeli. Yaş-bazlı politika/KVKK bu dilimde YOK. |
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
| `InviteCode` | string? | Öğrencinin claim için gireceği davet kodu (Ö-C). P15 / N-20: 8 karakter, `SecureCode.Alphabet` (karışan `0 O 1 I L` yok), büyük harfe normalize; `varchar(8)`; **bekleyen (`InviteSent`) davetler arasında benzersiz** (filtreli indeks `IX_teacher_student_links_InviteCode_pending`) |
| `InviteExpiresOnUtc` | DateTime? | P15 / N-20: davetin son geçerlilik anı (`InvitePolicy.ExpiryDays` = 7 gün); `null` = davet yok |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |

**Davranış:** `SetRate(amount, currency, ...)`, `Archive(...)` / `Unarchive(...)`, `MarkInviteSent(inviteCode, targetUserId?, expiresOnUtc, now)` → `Result` (→ `TeacherStudentInviteSentDomainEvent`; kod handler'da `ISecureCodeGenerator.NewCode()` ile üretilir — P15 / N-20; eski `GenerateInviteCode()` / `Random.Shared` kaldırıldı), `IsInviteExpired(now)`, `Accept(...)` (→ `TeacherStudentLinkAcceptedDomainEvent`), `Reject(...)`.

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
| Öğrenci ekle | `POST /profiles` | `CreateStudentProfileCommandAuthorizer` · P15 T3 / N-23: `CreateStudentProfileCommandValidator` gövdede profilin sahibini reddeder (kural 8.1) → `400 students.field_not_allowed` | `CreateStudentProfileRequest` | `StudentProfileResponse` |
| Öğrenci güncelle / pasifleştir | `PUT /profiles/{studentId:guid}` | `UpdateStudentProfileCommandAuthorizer` | `UpdateStudentProfileRequest` | `StudentProfileResponse` |
| Öğrenci getir (id) | `GET /profiles/{studentId:guid}` | `StudentProfileQueryAuthorizer` (P15 T3: + yaşayan özel ders bağı olan öğretmen, kural 10) | — | `StudentProfileResponse` |
| Öğrenci getir (userId) | `GET /profiles/by-user/{userId:guid}` | `StudentProfileQueryAuthorizer` | — | `StudentProfileResponse` |
| Öğretmenin öğrencileri | `GET /profiles/by-teacher/{teacherUserId:guid}?includeArchived=false` | `StudentProfileQueryAuthorizer` | — | `IReadOnlyCollection<StudentProfileSummaryResponse>` (link üzerinden) |
| Öğrenciyi arşivle | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/archive` | `TeacherStudentLinkAuthorizer` | — | `204` |
| Arşivden çıkar | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/unarchive` | `TeacherStudentLinkAuthorizer` | — | `204` |
| Öğrenci ücreti belirle (B-07) | `PUT /teachers/{teacherUserId:guid}/students/{studentId:guid}/rate` | `TeacherStudentLinkAuthorizer` | `SetStudentRateRequest` | `204` |
| Öğrenci davet et (B-06) | `POST /teachers/{teacherUserId:guid}/students/{studentId:guid}/invite` | `TeacherStudentLinkAuthorizer` | `InviteStudentRequest` | `204` · P15 T3 / N-22: bağ zaten `Linked` ise `409 students.link_already_active` |
| Daveti kabul et | `POST /links/{linkId:guid}/accept` | `TeacherStudentLinkResponseAuthorizer` (P15 T3: yalnız `Student`/Admin; kod/durum/hedef/süre domain'de — kural 17) | `RespondToStudentInviteRequest(InviteCode)` (**P15 T3 / N-22: zorunlu**) · `currentUser` = kabul eden | `204` · `400 students.invite_code_mismatch` · `409 students.invalid_link_transition` / `students.profile_already_claimed` · `403 students.invite_not_for_user` · `410 students.invite_expired` · `429 shared.too_many_attempts` |
| Daveti reddet | `POST /links/{linkId:guid}/reject` | `TeacherStudentLinkResponseAuthorizer` (P15 T3: yalnız `Student`/Admin) | `RespondToStudentInviteRequest(InviteCode)` (**zorunlu**) | `204` · kabulle aynı hata kodları |
| **Davet kodu ile profili devral (Ö-C)** | `POST /links/claim` | `TeacherStudentLinkResponseAuthorizer` (açık claim: kod bilgisi sahiplik yerine geçer; P15 / N-20: **yalnız `Student` (veya Admin) rolü** → aksi `403 students.claim_requires_student_role`) | `ClaimLinkRequest` (`currentUser` = devralan) | `204` |
| **Veli davet kodu üret (Veli V-D)** | `POST /profiles/{studentId:guid}/parent-invite` | `CreateParentInviteCommandAuthorizer` (öğretmen = `TeacherUserId` **ve** öğrenciyle `Private` kapsam — P15 T2 / N-21; aksi `403 shared.forbidden` / Admin) | `CreateParentInviteRequest(TeacherUserId, ChildDisplayName?)` | `ParentInviteResponse(Id, InviteCode)` · öğrenci yoksa `404 students.student_not_found` |

**`StudentParentInvite` aggregate (Veli V-D):** Öğretmenin bir öğrenci için ürettiği veli davet kodu (P15 / N-20: `InviteCode` 8 karakter, `ISecureCodeGenerator`; bekleyenler arasında benzersiz — `IX_student_parent_invites_InviteCode_pending`). Alanlar: `Id, StudentId, TeacherUserId, InviteCode, ChildDisplayName?, Status (Pending→Claimed), ClaimedByParentUserId?, CreatedOnUtc, ExpiresOnUtc (7 gün), ClaimedOnUtc?`; `IsExpired(now)`. `Claim(parentUserId, now)` yalnız `Pending`'den çalışır (tekrar → `InvalidOperationException`). Tablo `student_parent_invites`. Veli tarafı bu kodu **`IParentInviteDirectory`** (`ResolveAsync`/`MarkClaimedAsync`, Students uygular) ile çözer (P15: `ResolveAsync` kodu normalize eder ve `ParentInviteInfo`'ya **daveti üreten** + **son geçerlilik** bilgisini koyar; süre kararı Parents'tadır); claim, Parents'ta `ParentChildLink` oluşturup **onaylar** (öğretmen kodu = öğretmen onayı, veli kod girer = veli onayı).

**Modüller-arası sözleşme (Shared.Contracts):** M03, öğrenci↔kullanıcı bağının otoritesidir ve
`IStudentDirectory` sözleşmesini `StudentDirectory` ile uygular (2026-07-07):
`GetOwnerUserIdAsync(studentId) → Guid?`, **`GetAgreedLessonRateAsync(teacherUserId, studentId) → AgreedLessonRate?`**
(M07-2, 2026-09-04 — B-07 ücretini `TeacherStudentLink`'ten okur; ücret girilmemişse `null`, çağıran tahmini tutar üretmez)
ve **`IsLinkedTeacherOfStudentAsync(teacherUserId, studentId) → bool`** (M10-2, 2026-09-04 — yaşayan bağ
`Manual`/`InviteSent`/`Linked` ve `IsArchived == false`; `Rejected`/`Disconnected`/arşivli bağ erişim vermez).
Diğer modüller (ör. M04 Scheduling sahiplik doğrulaması, M07 Payments ders ücretlendirmesi) bu sözleşmeyi
tüketerek okur — M03'ün `DbContext`'ine doğrudan erişmeden, proje referansı olmadan (anti-corruption).
Aynı desenin M05'teki karşılığı `ILessonSessionAccessService`.

**İstek/yanıt sözleşmeleri (koddan):**

```
StudentSubjectItem(string Subject, string? TargetLevel)

CreateStudentProfileRequest(Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                            string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                            string? GoalSummary, string? LevelNotes, StudentOrigin Origin,
                            IReadOnlyCollection<StudentSubjectItem> Subjects,
                            TargetExam TargetExam = TargetExam.None, DateTime? DateOfBirth = null)

UpdateStudentProfileRequest(string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                            string? GoalSummary, string? LevelNotes, bool IsActive,
                            IReadOnlyCollection<StudentSubjectItem> Subjects,
                            TargetExam TargetExam = TargetExam.None, DateTime? DateOfBirth = null)  // branşlar tam yeniden yazar

StudentProfileResponse(Guid Id, Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                       string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                       string? GoalSummary, string? LevelNotes, string Origin, bool IsActive,
                       string TargetExam, DateTime? DateOfBirth,
                       IReadOnlyCollection<StudentSubjectResponse> Subjects, DateTime CreatedOnUtc, DateTime UpdatedOnUtc)

StudentProfileSummaryResponse(Guid Id, string FullName, string GradeLevel, string Origin,
                              bool IsActive, DateTime CreatedOnUtc,
                              bool IsArchived, decimal? AgreedRateAmount, string LinkStatus)  // FullName'e göre sıralı

SetStudentRateRequest(decimal AgreedRateAmount, string Currency)   // B-07
InviteStudentRequest(Guid? TargetUserId)                            // B-06; hedef opsiyonel (açık davet)
ClaimLinkRequest(string InviteCode)                                 // Ö-C; öğretmenin verdiği 8 karakterli kod (büyük/küçük harf duyarsız)
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
| `students.invite_expired` | **410** | P15 / N-20: davetin süresi dolmuş (7 gün) |
| `students.cannot_claim_own_invite` | **403** | P15 / N-20: daveti üreten öğretmen kendi kodunu kullanamaz |
| `students.claim_requires_student_role` | **403** | P15 / N-20: claim yalnız öğrenci hesabı |
| `shared.too_many_attempts` | **429** | P15 / N-20: kullanıcı başına saatte 10 başarısız deneme (`IInviteClaimAttemptLimiter`, Redis, fail-open); eşik aşılınca doğru kod da reddedilir |
| `shared.forbidden` | **403** | Bu işlemi yapma / bu kaynağa erişim yetkiniz yok. |
| `students.invalid_origin` | 400 | Öğrenci profili kaynağı ile kimlik bilgileri uyumsuz. |
| `students.invalid_request` | 400 | (Validator) Ad soyad ve sınıf seviyesi zorunlu. |
| `students.duplicate_subject` | **409** | Bu branş zaten ekli. (M03-2; büyük/küçük harf duyarsız) |
| `students.subject_not_found` | **404** | Branş bulunamadı. (M03-2 silme ucu) |
| `students.invalid_subject` | 400 | Branş adı boş olamaz. (M03-2) |
| `students.parent_user_required` | 400 | Veli kayıtlı bir kullanıcı olmalıdır. (M03-1; veli profili yoksa) |
| `students.parent_link_exists` | **409** | Bu veli zaten bu öğrenciye bağlı veya onay bekliyor. (M03-1) |

> **Not:** `StudentProfileQueryAuthorizer`, tekil getirme uçlarında profili önce yükler; bulunamazsa `shared.forbidden` yerine
> `students.profile_not_found` (404) döner (varlık sızdırmadan, sahip değilse yetki reddi).
>
> **Not (PUT):** `UpdateStudentProfileCommandAuthorizer` sahipliği kontrol eder: admin her zaman, öğretmen yalnızca kendi eklediği öğrenciyi (`CreatedByTeacherUserId == currentUserId`) güncelleyebilir. Branşlar PUT ile **tam yeniden yazılır** (merge değil).

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [ ] **`PATCH /profiles/{studentId}/status`** — ayrı pasifleştirme ucu (şimdilik PUT ile `isActive: false` göndererek yapılıyor).
- [x] **Manuel öğrenciyi gerçek hesaba bağlama** — `.../invite` + `/links/{linkId}/accept` ile karşılandı (Dilim C).
- [x] **Davet kodu ile devralma + profil birleştirme** — `.../invite` 8 karakterli güvenli kod üretir (P15 / N-20; 7 gün, rol kontrolü, deneme limiti); `POST /links/claim` ile öğrenci devralır, mevcut self-profil varsa merge (Ö-C).
- [x] **`POST /profiles/{studentId}/link-parent`** — gerçek veli kullanıcısı bağlama (2026-09-04, P06/M03-1). Bkz. §3.4.
- [x] **`POST` / `DELETE /profiles/{studentId}/subjects[/{subjectId}]`** — branş ekleme/çıkarma (2026-09-04, P06/M03-2). Bkz. §3.4.
- [x] **`GET /profiles/by-parent/{parentUserId}`** — velinin **onaylı** çocuklarını listeleme (2026-09-04, P06/M03-3). Bkz. §3.4.

### 3.4 Tamamlayıcı uçlar (P06 Task 9, 2026-09-04)

> ⚠️ **`StudentSubject.Id` `ValueGeneratedNever` olmalıdır (P06 Task 10, 2026-09-04).** Varsayılan
> `ValueGeneratedOnAdd` ile EF, izlenen profilin `Subjects` koleksiyonuna eklenen yeni branşı
> "anahtarı dolu → zaten var" sayıp `INSERT` yerine `UPDATE` üretiyor; gerçek Postgres'te 0 satır
> etkileniyor ve `DbUpdateConcurrencyException` → **500** oluyordu. InMemory sağlayıcısı bu hatayı
> üretmez; A-01'de Teachers için aynı düzeltme yapılmıştı. Regresyon: `RealStudentSubjectIntegrationTests`.
> **Kural:** kimlikleri `IIdGenerator` üreten her çocuk koleksiyonunda anahtar `ValueGeneratedNever` olmalı.

| Uç | Yetki | Davranış |
|----|-------|----------|
| `POST /profiles/{studentId}/link-parent` `{ parentUserId }` | Admin · profili oluşturan öğretmen · profilin sahibi öğrenci | Bağ **Parents**'ta `Pending` açılır; yanıt `{ studentId, parentUserId, status: "Pending" }`. Veli profili yoksa `students.parent_user_required` (400), açık bağ varsa `students.parent_link_exists` (409). |
| `POST /profiles/{studentId}/subjects` `{ subject, targetLevel }` | aynı üçlü | Yanıt `StudentSubjectResponse` (**`id` dahil**). Aynı branş (harf duyarsız) → `students.duplicate_subject` (409). |
| `DELETE /profiles/{studentId}/subjects/{subjectId}` | aynı üçlü | 204. Branş bu profile ait değilse `students.subject_not_found` (404). |
| `GET /profiles/by-parent/{parentUserId}` | Admin · **yalnız o veli** | `ParentChildSummaryResponse[]` (id, ad, sınıf, hedef sınav, aktiflik, oluşturulma). Onaylı bağ listesi `IParentAccessDirectory.ListApprovedStudentIdsAsync` ile Parents'tan okunur. Başka velinin kimliğiyle → `shared.forbidden` (403). |

> **Modül sınırı (önemli):** `link-parent` ucu `StudentProfile.ParentUserId`'yi **doldurmaz**. Bağın sahibi Parents modülüdür;
> talep `IParentChildLinkRequestService` (Shared.Contracts, Parents uygular) üzerinden açılır, onay mevcut
> `POST /api/parents/children/{linkId}/approve` ucundadır ve `ParentUserId` ancak `ParentChildLinkApproved` olayı
> Students'a düştüğünde dolar (bkz. `ParentChildLinkApprovedIntegrationEventHandler`).
>
> ⚠️ **Sözleşme değişikliği:** `StudentSubjectResponse` artık `Id` alanı taşır (silme ucu kimlikle çalışır). Alan
> **eklendi**, mevcut alanlar değişmedi — eski istemciler etkilenmez.

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
   **7.1 (M03-2):** Tek branş ekleme/silme uçları PUT'tan bağımsızdır ve **birleştirici** davranır: `AddSubject` yalnız ekler, `RemoveSubject` yalnız siler. Aynı branş (büyük/küçük harf duyarsız) ikinci kez eklenemez — kural `StudentProfile.AddSubject` içinde, tek yerde durur.
8. **Validator:** `FullName` ve `GradeLevel` zorunlu (hem create hem update için `students.invalid_request`).
   **8.1 (P15 T3 / N-23) — profilin sahibi gövdeden gelmez:** `parentUserId` hiçbir profilde gönderilemez (veli onaylı bağla gelir); `TeacherManaged` profilde `userId`, `SelfRegistered` profilde `createdByTeacherUserId` gönderilemez → `400 students.field_not_allowed` (alan kabul edilip **sessizce yok sayılmaz**). Eskiden öğretmen gövdeye bir öğrenci hesabının kimliğini yazıp profili ona bağlayabiliyor, o öğrenci kendi profilini açamıyordu (`409 user_profile_exists`). Aynı kural domain'de değişmez olarak durur: `StudentProfile` kurucusu uyumsuz alanlarla `ArgumentException` atar (programlama hatasına karşı). Öğretmenin açtığı profil bir hesaba **yalnız davetle** bağlanır (`LinkUser`, kural 17). Mobil zaten `null` gönderiyor (`student_model.dart`, `student_repository_impl.dart`) — kırılma yok.
9. **Create yetkisi (`CreateStudentProfileCommandAuthorizer`):**
   - **Admin** → her zaman.
   - `TeacherManaged` + rol `Teacher` + `currentUserId == CreatedByTeacherUserId` → izin.
   - `SelfRegistered` + rol `Student` + `currentUserId == UserId` → izin.
   - Aksi halde `shared.forbidden`.
10. **Okuma yetkisi (`StudentProfileQueryAuthorizer`):**
    - Tekil getirme: profil yüklenir; yoksa `students.profile_not_found`. **Admin** veya `currentUserId`'nin profilin `UserId` / `CreatedByTeacherUserId` / `ParentUserId` alanlarından biriyle eşleşmesi gerekir. **P15 T3 / N-28:** ayrıca `Teacher` rolündeki kullanıcının öğrenciyle `ITeacherStudentAccess` kapsamı `Private` ise okur — birleşme sonrası kanonik profil öğrencinin kendi açtığıdır (`CreatedByTeacherUserId` boş), bağı taşınan öğretmen öğrenciyi listesinde görüp profilini açabilmeli. Kapsam sorgusu yalnız ucuz kontroller başarısızsa çalışır. **Bilinçli karar:** profil **yazma** yetkileri (kural 11, branş, `link-parent`) genişletilmedi — birleşen profilde öğretmen okur ve ders/ödeme yazar, profili öğrenci yönetir.
    - Liste (`by-teacher`): **admin** veya `Teacher` rolü + `currentUserId == teacherUserId` (kendi listesi).
11. **Güncelleme yetkisi (`UpdateStudentProfileCommandAuthorizer`):**
    - **Admin** → her zaman.
    - `Teacher` rolü + `CreatedByTeacherUserId == currentUserId` → yalnız kendi eklediği öğrenci.
    - Aksi halde `shared.forbidden` (403). Diğer öğretmen 403 alır (sahiplik testi entegrasyon testinde doğrulanmıştır).
12. **Sahiplik üçlüsü:** Bir profile erişebilen taraflar = bağlı öğrenci (UserId) + ekleyen öğretmen (CreatedByTeacherUserId) + bağlı veli (ParentUserId). Bu, öğretmen-öğrenci-veli ilişkisinin yetki temelidir.
13. **🔑 Çoklu öğretmen (Dilim C):** Bir öğrenci **birden fazla öğretmene** bağlanabilir; her bağ ayrı bir `TeacherStudentLink`'tir. Öğretmenin öğrenci listesi `CreatedByTeacherUserId` yerine link tablosu üzerinden yürür. `(TeacherUserId, StudentId)` benzersizdir (aynı öğretmen-öğrenci ikilisi tek link).
14. **Free limit=5 (`students.free_limit_reached`):** Bir öğretmen `TeacherManaged` öğrenci eklerken aktif (reddedilmemiş) link sayısı sınıra ulaştıysa yeni ekleme 409 ile reddedilir. **Arşivli linkler limite dâhildir** (arşiv, kotayı boşaltmaz). Sınırın kaynağı artık Students'ın sabiti değil, **öğretmenin üyelik hakkı**: `IEntitlementDirectory.GetAsync(teacherUserId).MaxStudents` (Free 5, Premium sınırsız — P09 Task 2 / M03-4).
15. **Arşivleme (B-04):** `Archive`/`Unarchive` link'in `IsArchived` bayrağını değiştirir. Arşivli öğrenci varsayılan listede gizlenir; `?includeArchived=true` ile görünür. Reddedilmiş (`Rejected`) linkler listede ve limitte sayılmaz.
16. **Öğrenci bazlı ücret (B-07):** `SetRate(amount, currency)` link üzerinde anlaşılan ders ücretini tutar; ücret öğrenci-öğretmen ikilisine özeldir (öğretmen bazlı sabit ücrete bağlı değil).
17. **Davet/kabul (B-06; P15 T3 / N-22 ile sertleşti):** Öğretmen mevcut link'i `Invite` ile `InviteSent` yapar (hedef kullanıcı opsiyonel); bağ zaten `Linked` ise `409 students.link_already_active` (eskiden kurulmuş bağ yeniden davet edilip ikinci bir claim mevcut sahibin üstüne yazabiliyordu). Yanıt (kabul/ret) yalnız **`InviteSent`** bağda ve **kodla** yapılır — `TeacherStudentLink.CanRespond(actingUserId, inviteCode, now)` sırasıyla: kod eşleşmeli (`students.invite_code_mismatch`; kod **önce** sınanır ki link kimliğini bilip kodu bilmeyen biri bağın durumunu da öğrenemesin), durum `InviteSent` olmalı (`students.invalid_link_transition`), hedefli davette yanıtlayan hedef olmalı (`students.invite_not_for_user`), süre dolmamış olmalı (`students.invite_expired`). Eskiden `Accept()` hiçbir kontrol yapmıyordu: hedefsiz bir bağın kimliğini bilen herkes kabul edip profili kendine yazabiliyor, kurulmuş bağı `Reject` ile düşürüp öğretmenin erişimini kesebiliyordu. Kabul/ret yalnız `Student` (veya Admin) rolüne açıktır; kodla ilgili başarısız denemeler (`mismatch`/`expired`/`not_for_user`) kullanıcı başına deneme sayacına yazılır (N-20 kilidi). `LinkUser` profilin sahibini **bir kez** belirler: başka hesaba bağlı ya da birleşmiş profil devralınamaz (`409 students.profile_already_claimed`), aynı kullanıcıya tekrar bağlamak değişiklik yapmaz; bağlama `StudentProfileLinkedToUserDomainEvent` yayar (§5). Identity'de e-posta/telefonla kullanıcı araması **yapılmaz** (kararı: 2026-07-18).
18. **🔑 Davet kodu ile claim (Ö-C):** `Invite`, link'e 8 karakterli güvenli `InviteCode` yazar (P15 / N-20). Öğrenci `POST /links/claim` ile kodu girer (`ClaimStudentLinkCommand`, `ClaimingUserId = currentUser`). Handler: kod yoksa `students.invite_not_found` (404); link `InviteSent` değilse `students.invite_invalid` (400); aksi halde **link kabulüyle aynı çekirdek** (`StudentInviteAcceptance`, P15 T3): kural 17'deki kontroller + kendi davetini kullanma yasağı, sonra devralma ya da birleştirme (kural 19). Kod bilgisi sahiplik kanıtı yerine geçer (açık claim); hedefli davet yalnız hedefine açılır.
19. **🔑 Tam profil birleştirme (merge, Ö-C/B5; P15 T3 / N-28 ile tamamlandı):** Kabul anında öğrencinin **mevcut bir profili** (`GetByUserIdAsync(actingUserId)`) varsa ve davet edilen profil değilse **kanonik = öğrencinin profili**; aksi halde davet edilen profil devralınır (`LinkUser`). Birleştirme **tek SaveChanges'te** (aynı transaction): (a) öğretmen bağları `StudentMergePolicy.MergeTeacherLinks` ile kanonik kimliğe taşınır — aynı öğretmenin kanonikte de bağı varsa **biri** kalır (`(TeacherUserId, StudentId)` benzersiz): arşivli olan düşer, ikisi de yaşıyorsa güçlü durum (kabul edilen davet = `Linked`) kalır, eşitlikte kanoniktekine; kalan bağ ücreti yoksa düşenin ücretini devralır, düşen satır silinir; (b) **bekleyen veli davetleri** kanonik profile taşınır (birleşmeden önce üretilmiş veli kodu artık kanonik profile bağlar); (c) kanonik profilin onaylı velisi boşsa birleşenin velisi taşınır; (d) manuel profil `MarkMerged` ile pasifleşir ve `StudentProfilesMergedDomainEvent` yayılır. Bağ, birleştirmeden **sonra** kabul edilir — `TeacherStudentLinkAccepted` olayı kanonik kimliği taşır (Reporting'in birleştirme tüketicisi yok). Sonuç: birleşme sonrası kanonik profilde öğretmen kapsamı `Private` (eskiden `None` — N-28). Diğer modüllerin yeniden ataması §5'te.
20. **🔑 Erişim kapsamı (P15 T2 / N-08, N-21, K-13):** Öğretmenin öğrenci üzerindeki yetkisinin tek kaynağı `ITeacherStudentAccess.GetScopeAsync` (`Shared/Contracts`, uygulayan `Infrastructure/TeacherStudentAccess.cs`). `Private` ⇔ bağ `Linked` ve arşivli değil **veya** profil `TeacherManaged`, bu öğretmence açılmış ve bağı yaşıyor (`Manual`/`InviteSent`, arşivli değil); arşivli/`Rejected`/`Disconnected` bağ ya da hiç bağ yoksa `None`; `Class` P22'ye kadar dönmez. `IStudentDirectory.IsLinkedTeacherOfStudentAsync` artık `== Private`'a delege eder. **Veli daveti** (`POST /profiles/{id}/parent-invite`) yalnız `Private` kapsamlı öğretmen (veya Admin) tarafından üretilir — öğrenci kimliğini bilen bağsız öğretmen davet üretip kendi veli hesabıyla çocuğun birincil velisi olamaz. Ders/ödeme/oturum yazma uçları da aynı kapsamı sorar (bkz. [`../architecture/backend.md`](../architecture/backend.md) §8.1). ✅ **N-28 kapandı (P15 T3):** birleşmede bağlar kanonik kimliğe taşınır (kural 19); kanonik profilde kapsam `Private`.

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
                              → kabul eden currentUser profile LinkUser ile bağlanır (birleşmede StudentId = kanonik)
Profil hesaba bağlandı       → StudentProfileLinkedToUserDomainEvent (StudentId, UserId, LinkedOnUtc) — P15 T3 / N-23
                              → Outbox → StudentProfileLinkedToUserIntegrationEvent (Shared.Contracts)
                              → (P15 T4) Parents: KnownStudent.UserId (davetle bağlanan öğrencinin veli onayı yetkisi)
Profiller birleştirildi      → StudentProfilesMergedDomainEvent (FromStudentId=manuel, ToStudentId=self, OnUtc)
                              → Outbox → StudentProfilesMergedIntegrationEvent (Shared.Contracts)
                              → Scheduling / Assignments / Payments / LessonSessions / Study handler'ları:
                                UPDATE ... SET StudentId=ToStudentId WHERE StudentId=FromStudentId
                                (Study.StudyStudent PK=StudentId olduğundan kaynak satır silinir; kanonik korunur)
                              → P15 T3 / N-28: Parents (StudentProfilesMergedParentsHandler) — veli bağları
                                tekilleşir + taşınır, known_students kaynağı silinir, veli paneli özeti toplanır
                              → P15 T3 / N-28: ProgressTracking (StudentProfilesMergedProgressHandler) — hedefler
                                taşınır; topic_masteries / progress_snapshots çakışmayan satır taşınır,
                                çakışanda kanonik kazanır, kaynak silinir
                              (öğretmen bağları + bekleyen veli davetleri Students'ta, birleşmeyle aynı transaction'da)
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
- [x] **Öğrenci detayında branş ekle/sil + veli bağla** — `StudentDetailPage` genel bakış sekmesi (2026-09-04, P06/M03-1/M03-2). ⚠️ Veli, **kullanıcı kimliğiyle** bağlanır: Identity'de e-postayla kullanıcı arama ucu olmadığı için "veli e-postasıyla arama" yapılamadı.
- [ ] **Öğrenci ekleme formu zenginleştirme** — `StudentSubject` çoklu giriş hâlâ tek satır (`_EditStudentSheet` yalnız ilk branşı düzenler); detay ekranındaki ekle/sil bunu telafi ediyor.
- [x] **Öğrenci self-register akışı** (`SelfRegistered`) — backend `POST /profiles` (`Origin=SelfRegistered`) + mobilde ayrı ekran yerine **otomatik profil oluşturma** (`StudentScope` / `StudyHomeCubit` → `createSelfProfile`).
- [ ] **Manuel→gerçek bağlama (mobil ekranlar)** — backend uçları **hazır**: `POST /teachers/{teacherUserId}/students/{studentId}/invite`, `POST /links/{linkId}/accept`, `POST /links/{linkId}/reject` (P15 T3: ikisi de gövdede `inviteCode` ister), `POST /links/claim`. ⚠️ Davet kodu ve link kimliği bugün hiçbir API yanıtında yok — P21 T1 açacak. Mobilde davet formu şu an yalnız arayüz taslağı (repository çağrısı yok), kabul/red/claim ekranı yok — (P06).

---

## 7. Kabul Kriterleri

- [x] Öğretmen, kendi `UserId`'siyle `TeacherManaged` öğrenci ekleyebilir ve listeleyebilir.
- [x] Öğrenci, `SelfRegistered` olarak kendi profilini (backend'de) oluşturabilir.
- [x] Origin ile kimlik alanları tutarsızsa `students.invalid_origin` döner.
- [x] Aynı kullanıcı için ikinci profil 409 ile engellenir.
- [x] Profil erişimi sahiplik üçlüsü (öğrenci/öğretmen/veli) + admin ile sınırlıdır.
- [x] Veli alanı yalnızca gerçek kullanıcı kimliği (`ParentUserId`) ile doldurulur.
- [x] **Profilin sahibi istek gövdesinden belirlenemez** (P15 T3 / N-23) — öğretmen `userId` / herkes `parentUserId` gönderirse `400 students.field_not_allowed`; kurban kendi profilini açabilir (`RealStudentInviteRulesTests`).
- [x] **Davet yalnız kodla, bekleyen davette ve hedefine yanıtlanır** (P15 T3 / N-22) — kodsuz kabul/ret `400`, kurulmuş bağa ret/yeniden davet `409`, hedef dışı `403` (`TeacherStudentLinkTransitionTests`, `RealStudentInviteRulesTests`).
- [x] **Birleşme yarım kalmaz** (P15 T3 / N-28) — gerçek Postgres'te: öğretmen bağı (ücretiyle) kanonikte ve kapsam `Private`, arşivli çakışan bağ düşer, bekleyen veli daveti taşınır, veli bağı tekilleşir, veli paneli toplanır, gelişim verisi taşınır; öğretmen kanonik profili açar ve ders yazar (`RealStudentMergeIntegrationTests`).
- [x] **Öğrenci güncellenebilir / pasifleştirilebilir** — `PUT /profiles/{studentId}`, `IsActive`, branş yeniden yazımı, sahiplik yetki testi.
- [x] **Başka öğretmen başkasının öğrencisini güncelleyemez** (403) — entegrasyon testinde doğrulandı.
- [x] **Bir öğrenci birden fazla öğretmene bağlanabilir** — `TeacherStudentLink` (Dilim C).
- [x] **Free planda öğretmen en fazla 5 öğrenci ekleyebilir** — `students.free_limit_reached` (409), birim testinde doğrulandı. **Premium öğretmen sınıra takılmaz** (P09 Task 2, `Create_TeacherManaged_Premium_Teacher_Should_Not_Hit_Limit`).
- [x] **Öğrenci arşivlenip arşivden çıkarılabilir**; arşivli varsayılan listede gizli, `includeArchived=true` ile görünür — birim testinde doğrulandı.
- [x] **Öğretmen öğrenci bazlı ücret belirleyebilir** (B-07) — link üzerinde `AgreedRateAmount`/`Currency`.
- [x] **Manuel öğrenci, davet/kabul ile gerçek öğrenci hesabına bağlanabilir** (B-06) — kabul eden `currentUser` profile bağlanır; birim testinde doğrulandı.
- [x] **Öğrenci 6 haneli davet koduyla profili devralır** (Ö-C) — `POST /links/claim`; geçersiz/eksik kod `invite_not_found`/`invite_invalid`; birim testinde doğrulandı.
- [x] **Mevcut self-profil varsa claim'de profiller birleşir** (Ö-C/B5) — manuel profil `IsMerged`, `StudentProfilesMergedDomainEvent` yayılır; birim testinde doğrulandı. Modüller-arası `StudentId` yeniden atama Testcontainers entegrasyon testinde doğrulanır (gerçek Postgres gerekli).
- [x] **Öğretmen mevcut veli kullanıcısını bağlayabilir** (2026-09-04, M03-1) — bağ `Pending` açılır; kayıtlı olmayan veli `students.parent_user_required`, açık bağ `students.parent_link_exists`; birim testinde doğrulandı.
- [x] **Branş profil güncellemesinden bağımsız eklenip silinebilir** (2026-09-04, M03-2) — aynı branş harf duyarsız reddedilir (`students.duplicate_subject`); birim testinde doğrulandı.
- [x] **Veli yalnız kendi onaylı çocuklarını listeler** (2026-09-04, M03-3) — başka velinin kimliğiyle sorgu 403; onaysız bağ listeye girmez; birim testinde doğrulandı.
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
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*Öğrenci Profili (Students) Modülü (M03) — Detaylı Tasarım | Güncelleme: 2026-11-14 (P15 Task 3 / N-22, N-23, N-28: kural 8.1 — profilin sahibi gövdeden gelmez (`400 students.field_not_allowed`) + kurucuda değişmez; kural 17 — kabul/ret yalnız `InviteSent`'te, kodla, hedefe, süresinde (`CanRespond`), `Linked` bağa yeniden davet `409`, `LinkUser` bir kez + `StudentProfileLinkedToUserDomainEvent`; kural 18–19 — claim ve link kabulü tek çekirdek (`StudentInviteAcceptance`), birleşmede öğretmen bağları (`StudentMergePolicy`) + bekleyen veli davetleri + veli aynı transaction'da kanonik profile; kural 10 — `Private` kapsamlı öğretmen profili okur; accept/reject uçları gövdede kod ister; §5 Parents + ProgressTracking birleşme tüketicileri) · 2026-11-13 (P15 Task 2 / N-08, N-21: §4 kural 20 — `ITeacherStudentAccess` erişim kapsamının tek kaynağı (`TeacherStudentAccess`), `IsLinkedTeacherOfStudentAsync` ona delege; veli daveti yalnız `Private` kapsamlı öğretmence) · 2026-11-13 (P15 Task 1 / N-20: öğrenci ve veli davet kodları `ISecureCodeGenerator` ile 8 karakter, 7 gün geçerli, bekleyenler arasında benzersiz; claim rol kontrollü, kendi kodunu kullanma yasak, kullanıcı başına deneme kilidi; migration `HardenInviteCodes`). Önceki not — 2026-09-04 (P09 Task 2 / M03-4: öğretmen öğrenci limiti artık `StudentLimits.FreeStudentLimit` sabitinden değil `IEntitlementDirectory.MaxStudents`'ten okunuyor (Free 5, Premium sınırsız); `StudentProfile.MembershipTier` **LEGACY** işaretlendi — karar bu alandan okunmuyor, yalnız geçiş köprüsünün girdisi (P12'de silinecek). Önceki not — 2026-09-04 (P08 Task 4 / M14-4: `IStudentDirectory`'ye `GetDisplayNameAsync` eklendi — öğrenci adı Reporting'in PDF rapor başlığına açıldı; profil yoksa `null` döner, çağıran uydurma ad üretmez) · 2026-09-04 (P08 Task 2 / M10-2: `IStudentDirectory`'ye `IsLinkedTeacherOfStudentAsync` eklendi — öğretmen–öğrenci bağı ProgressTracking'e açıldı) · 2026-09-04 (P06 Task 10 canlı doğrulama: **branş ekleme gerçek Postgres'te 500 veriyordu** — `StudentSubject.Id` `ValueGeneratedNever` yapıldı, migration `StudentSubjectValueGeneratedNever`) · 2026-09-04 (P06 Task 9 / M03-1+M03-2+M03-3: `link-parent` (Pending bağ, `IParentChildLinkRequestService`), branş ekle/sil uçları + `StudentSubjectResponse.Id`, `by-parent` onaylı çocuk listesi; mobil öğrenci detayında branş ekle/sil + veli bağla) · 2026-09-04 (P06 Task 5 / M07-2: `IStudentDirectory`'ye `GetAgreedLessonRateAsync` eklendi — B-07 ücreti Payments'a açıldı) · 2026-09-02 (F-02: self-register kodda; davet/kabul/red/claim uçları hazır, mobil ekranlar eksik) · 2026-07-19 (Veli V-D: `StudentParentInvite` veli davet kodu + `POST /profiles/{id}/parent-invite` + `IParentInviteDirectory` kontratı + `student_parent_invites` migration; Veli V-A: `StudentProfile.DateOfBirth` doğum tarihi alanı — create/update/response + `date` sütun migration; yaş-bazlı politika/KVKK yok; Ö-C: davet kodu `InviteCode` + kod tabanlı claim `POST /links/claim` + tam profil birleştirme merge `StudentProfilesMergedDomainEvent` → modüller-arası `StudentId` yeniden atama; Ö-B: `TargetExam` hedef sınavı S-03.9 — M08 net formülü böleni; Dilim C: `TeacherStudentLink` çoklu öğretmen bağlantısı, free limit=5, arşivleme, öğrenci bazlı ücret B-07, davet/kabul B-06)*
