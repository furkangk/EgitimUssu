# 🎓 Öğrenci Profili (Students) Modülü (M03) — Detaylı Tasarım Dokümanı

> **PRD: M03 Öğrenci Profili** · **Faz: 1 (öğretmen tarafı 🟢) / öğrenci self-register 🟡** · **Durum: Öğretmenin manuel öğrenci eklemesi uçtan uca çalışır; öğrencinin kendi kaydı domain'de var, akışı tamamlanmadı**
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
| **Öğrenci güncelleme / pasifleştirme ucu** | 🔴 eksik | `IsActive` set/`PUT` endpoint yok (yalnız create'te `true`) |
| **Manuel öğrenciyi gerçek hesaba bağlama (davet/eşleşme)** | 🔴 eksik | Manuel profil sonradan `UserId` ile ilişkilendirilemiyor |
| **`StudentSubject` ekle/sil ucu** | 🔴 eksik | Branşlar yalnız create anında set ediliyor |
| ContactEmail benzersizlik kontrolü | 🟡 kısmi | `ExistsByContactEmailAsync` repo'da var ama create handler'da kullanılmıyor |
| Self-register mobil akışı | 🔴 eksik | Öğrenci `students` feature'ı öğretmen odaklı; öğrenci kendi profil ekranı yok |

> **Özet:** Öğretmen tarafı (manuel ekle/listele/getir) **çalışır durumdadır**. Self-register backend'de hazırdır ama
> **mobil akış ve manuel→gerçek bağlama** tamamlanmamıştır; öğrenci güncelleme/pasifleştirme ucu eksiktir.

---

## 2. Domain Modeli

Kaynak: `src/Modules/Students/Domain/StudentsDomainModel.cs`. Şema: **`students`**.
Tablolar: `student_profiles`, `student_subjects`.

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
| `IsActive` | bool | Aktif/pasif (create'te `true`) |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime | Oluşturma / güncelleme (UTC) |
| `Subjects` | List&lt;`StudentSubject`&gt; | Branş + hedef seviye |

**Davranış:** Yapıcı `StudentProfileCreatedDomainEvent` yayar. (Not: Şu an domain üzerinde `Update`/`Deactivate` gibi davranış **yok** — bkz. §2.4 öneriler.)

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

```
StudentProfileCreatedDomainEvent(Guid StudentProfileId, Guid? UserId,
                                 Guid? CreatedByTeacherUserId, StudentOrigin Origin, DateTime CreatedOnUtc)
```

### 2.4 ⚠️ Önerilen (henüz kodda yok)

| Öneri | Gerekçe |
|-------|---------|
| `StudentProfile.Update(...)` + `Deactivate()` davranışları | Profil düzenleme ve pasifleştirme için domain davranışı; `UpdatedOnUtc` güncellensin |
| `LinkToUserAccount(userId)` davranışı + `StudentLinkedToUserDomainEvent` | Manuel (`TeacherManaged`) öğrenciyi sonradan **gerçek** öğrenci hesabına bağlama (davet/eşleşme akışı) |
| `LinkParent(parentUserId)` / `UnlinkParent()` | Veli bağlama/çözme; veli daima gerçek kullanıcı (kuralı domain'de garanti eder) |
| `StudentProfileUpdatedDomainEvent` | Diğer modüllerin (M04/M06/M09) değişimi yakalaması için |
| `StudentInvitation` (davet token'ı) | Manuel öğrenciye e-posta/SMS daveti gönderip kendi hesabına bağlanmasını sağlamak |

---

## 3. API Sözleşmesi

Tüm uçlar `RoutePrefix = /api/students` altında ve grup **`RequireAuthorization("AuthenticatedUser")`** ile korunur.
`Result<T>` döner; hata kodları `StudentsModule.ToHttpResult` ile HTTP'ye eşlenir.

### 3.1 Mevcut Endpoint'ler ✅

| Yetenek | Method + Route | Yetki kontrolü | İstek | Yanıt |
|---------|----------------|----------------|-------|-------|
| Öğrenci ekle | `POST /profiles` | `CreateStudentProfileCommandAuthorizer` | `CreateStudentProfileRequest` | `StudentProfileResponse` |
| Öğrenci getir (id) | `GET /profiles/{studentId:guid}` | `StudentProfileQueryAuthorizer` | — | `StudentProfileResponse` |
| Öğrenci getir (userId) | `GET /profiles/by-user/{userId:guid}` | `StudentProfileQueryAuthorizer` | — | `StudentProfileResponse` |
| Öğretmenin öğrencileri | `GET /profiles/by-teacher/{teacherUserId:guid}` | `StudentProfileQueryAuthorizer` | — | `IReadOnlyCollection<StudentProfileSummaryResponse>` |

**İstek/yanıt sözleşmeleri (koddan):**

```
StudentSubjectItem(string Subject, string? TargetLevel)

CreateStudentProfileRequest(Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                            string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                            string? GoalSummary, string? LevelNotes, StudentOrigin Origin,
                            IReadOnlyCollection<StudentSubjectItem> Subjects)

StudentProfileResponse(Guid Id, Guid? UserId, Guid? CreatedByTeacherUserId, Guid? ParentUserId,
                       string FullName, string GradeLevel, string? ContactEmail, string? ContactPhone,
                       string? GoalSummary, string? LevelNotes, string Origin, bool IsActive,
                       IReadOnlyCollection<StudentSubjectResponse> Subjects, DateTime CreatedOnUtc, DateTime UpdatedOnUtc)

StudentProfileSummaryResponse(Guid Id, string FullName, string GradeLevel, string Origin,
                              bool IsActive, DateTime CreatedOnUtc)  // FullName'e göre sıralı
```

### 3.2 Hata Kodları → HTTP Eşleme (koddan)

| Hata kodu | HTTP | Mesaj |
|-----------|------|-------|
| `students.user_profile_exists` | **409** | Bu kullanıcı için öğrenci profili zaten var. |
| `students.profile_not_found` | **404** | Öğrenci profili bulunamadı. |
| `shared.forbidden` | **403** | Bu işlemi yapma / bu kaynağa erişim yetkiniz yok. |
| `students.invalid_origin` | 400 | Öğrenci profili kaynağı ile kimlik bilgileri uyumsuz. |
| `students.invalid_request` | 400 | (Validator) Ad soyad ve sınıf seviyesi zorunlu. |

> **Not:** `StudentProfileQueryAuthorizer`, tekil getirme uçlarında profili önce yükler; bulunamazsa `shared.forbidden` yerine
> `students.profile_not_found` (404) döner (varlık sızdırmadan, sahip değilse yetki reddi).

### 3.3 Eksik / Önerilen Endpoint'ler ⚠️

- [ ] **`PUT /profiles/{studentId}`** — profil güncelleme (ad, sınıf, hedef, iletişim, branşlar).
- [ ] **`PATCH /profiles/{studentId}/status`** — pasifleştirme/aktifleştirme (`IsActive`).
- [ ] **`POST /profiles/{studentId}/link-user`** — manuel öğrenciyi gerçek öğrenci hesabına bağlama (davet/eşleşme).
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
6. **Varsayılan aktiflik:** Create'te `IsActive = true` set edilir (pasifleştirme ucu eksik — §3.3).
7. **Branş temizliği:** `Subjects` içinden `Subject` boş/whitespace olanlar **atlanır**; kalanlar `Trim()` edilir.
8. **Validator:** `FullName` ve `GradeLevel` zorunlu (`students.invalid_request`).
9. **Create yetkisi (`CreateStudentProfileCommandAuthorizer`):**
   - **Admin** → her zaman.
   - `TeacherManaged` + rol `Teacher` + `currentUserId == CreatedByTeacherUserId` → izin.
   - `SelfRegistered` + rol `Student` + `currentUserId == UserId` → izin.
   - Aksi halde `shared.forbidden`.
10. **Okuma yetkisi (`StudentProfileQueryAuthorizer`):**
    - Tekil getirme: profil yüklenir; yoksa `students.profile_not_found`. **Admin** veya `currentUserId`'nin profilin `UserId` / `CreatedByTeacherUserId` / `ParentUserId` alanlarından biriyle eşleşmesi gerekir.
    - Liste (`by-teacher`): **admin** veya `Teacher` rolü + `currentUserId == teacherUserId` (kendi listesi).
11. **Sahiplik üçlüsü:** Bir profile erişebilen taraflar = bağlı öğrenci (UserId) + ekleyen öğretmen (CreatedByTeacherUserId) + bağlı veli (ParentUserId). Bu, öğretmen-öğrenci-veli ilişkisinin yetki temelidir.

---

## 5. Olay Akışı (Event-Driven)

```
Öğrenci profili oluşturuldu → StudentProfileCreatedDomainEvent
                              (StudentProfileId, UserId?, CreatedByTeacherUserId?, Origin, CreatedOnUtc)
                              → Outbox → (gelecek) Notifications (M11): öğretmene/veliye bilgilendirme
                              → (gelecek) Matching (M12): SelfRegistered öğrenci için öğretmen önerisi
                              → (öneri) Parents (M09): veli bağlandığında çocuk gelişim akışını başlatma
(öneri) Manuel→gerçek bağlama → StudentLinkedToUserDomainEvent → ders/ödev/ödeme geçmişini hesaba taşıma
```

> Olaylar **Outbox pattern** ile yayılır (`Shared/Infrastructure/Messaging`). Şu an aktif tüketici yok;
> bildirim (M11), eşleştirme (M12) ve veli paneli (M09) için doğal entegrasyon noktasıdır.

---

## 6. Mobil Ekranlar (mevcut + planlanan)

`mobile/lib/features/students/` (flutter_bloc/Cubit) — şu an **öğretmen perspektifli**.

| Route | Sayfa | Durum | Açıklama |
|-------|-------|-------|----------|
| `/students` | `StudentsPage` | ✅ | Öğretmenin öğrenci listesi (`by-teacher`) |
| `/students/:studentId` | `StudentDetailPage` | ✅ | Öğrenci detayı (profil + branşlar) |

### Eksik / planlanan mobil ekranlar ⚠️
- [ ] **Öğrenci ekleme formu zenginleştirme** — `StudentSubject` (branş + hedef seviye) çoklu giriş + **veli bağlama** alanı.
- [ ] **Öğrenci düzenleme / pasifleştirme** ekranı (PUT/PATCH uçlarına bağlı).
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
- [ ] **Öğrenci güncellenebilir / pasifleştirilebilir** (PUT/PATCH uçları).
- [ ] **Manuel öğrenci, gerçek öğrenci hesabına bağlanabilir** (davet/eşleşme akışı).
- [ ] **Self-register mobil akışı** uçtan uca çalışır.

---

## 8. Eksikler ve Yapılacaklar (öncelik sırasıyla)

1. **Öğrenci güncelleme/pasifleştirme** — `StudentProfile.Update(...)`/`Deactivate()` davranışları + `PUT`/`PATCH` uçları + `StudentProfileUpdatedDomainEvent`.
2. **Manuel→gerçek bağlama akışı** — `LinkToUserAccount` + davet (`StudentInvitation`) + `StudentLinkedToUserDomainEvent`; ders/ödev/ödeme geçmişinin korunması.
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

*Öğrenci Profili (Students) Modülü (M03) — Detaylı Tasarım | Güncelleme: 2026-06-24*
