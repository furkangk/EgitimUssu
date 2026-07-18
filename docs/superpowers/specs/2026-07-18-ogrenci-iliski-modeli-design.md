# Dilim C — Öğrenci İlişki Modeli · Tasarım Spec'i

**Tarih:** 2026-07-18
**Kaynak:** `doc/roles/ogretmen.md` §10 · fonksiyonel doküman §15 (B-04/B-06/B-07) + §12.4 (bağlantı durum makinesi)
**Onaylanan kararlar (hafıza `ogretmen-veri-modeli-kararlari`):** bir öğrenci **birden fazla öğretmene** bağlanabilir; free limit = **toplam** öğrenci; kesin sayı = **5**.
**Kapsam:** M03 (Students) çekirdek refactor + M07 (Payments) ücret çözümleme dokunuşu. 4 alan: `TeacherStudentLink` bağlantı tablosu (çoklu öğretmen), B-07 öğrenci bazlı ücret, B-04 arşivleme + free limit, B-06 davet/bağlanma.

> **Not (kapsam):** Bu dilimin en büyüğüdür ve 4 alt-özellik içerir. Tek plan olarak yazıldı ama task'lar bağımsız teslim edilebilir; gerekirse B-06 (davet) ayrı bir plana çıkarılabilir.

## Mevcut Durum
- `StudentProfile`: `UserId?`, `CreatedByTeacherUserId?` (tek öğretmen varsayımı), `ParentUserId?`, `IsActive`, `Origin (TeacherManaged|SelfRegistered)`.
- `ListStudentsByTeacherQuery` → `CreatedByTeacherUserId` ile filtreler.
- `IStudentDirectory.GetOwnerUserIdAsync(studentId)` → öğrencinin kendi `UserId`'si (Scheduling/Study IDOR koruması). **Çoklu öğretmen bunu değiştirmez.**
- Ücret: `TeacherProfile.HourlyRateAmount` veya her `PaymentRecord`'da tekil. Öğrenciye özel ücret yok.
- Free limit: yok.

## Tasarım

### C.1 — `TeacherStudentLink` bağlantı tablosu (çoklu öğretmen)
- Yeni aggregate (Students modülü, `students` şeması): `TeacherStudentLink`
  - `Id, TeacherUserId, StudentId, AgreedRateAmount? (decimal), Currency (string, "TRY"), Status, IsArchived (bool), CreatedOnUtc, UpdatedOnUtc`.
- `enum TeacherStudentLinkStatus { Manual = 1, InviteSent = 2, Linked = 3, Rejected = 4, Disconnected = 5 }`.
- Benzersizlik: `(TeacherUserId, StudentId)` unique index.
- **Geriye dönük uyum:** Migration, mevcut `student_profiles.CreatedByTeacherUserId` dolu satırlar için `Manual` link'leri backfill eder (migration içinde raw SQL `INSERT ... SELECT`).

### C.2 — Free limit = 5 (toplam)
- Öğretmenin toplam link sayısı (arşiv dahil) ≥ 5 iken yeni öğrenci/link reddedilir: `students.free_limit_reached`.
- Sabit `FreeStudentLimit = 5`. Premium bypass (M17) henüz yok → şimdilik herkese uygulanır; kod içinde açık uzatma noktası (`// TODO(M17): premium sınırsız`).

### C.3 — Manuel öğrenci oluşturma → link
- `CreateStudentProfileCommand` (Origin=TeacherManaged) işlenirken, profille birlikte `Manual` bir `TeacherStudentLink` oluşturulur (aynı transaction). Limit kontrolü burada.

### C.4 — Listeleme (link üzerinden) + arşiv filtresi
- `ListStudentsByTeacherQuery`'ye `bool IncludeArchived = false`. Sorgu link tablosundan yürür (teacher → linkler → profiller). Yanıt özetine `bool IsArchived`, `decimal? AgreedRateAmount`, `string LinkStatus` eklenir.

### C.5 — Arşivleme (B-04)
- `POST /api/students/teachers/{teacherUserId}/students/{studentId}/archive` ve `/unarchive` → link.`IsArchived` toggle. Arşiv **limiti boşaltmaz** (toplam sayım).

### C.6 — Öğrenci bazlı ücret (B-07)
- `PUT /api/students/teachers/{teacherUserId}/students/{studentId}/rate` (body: `AgreedRateAmount`, `Currency`) → link.`AgreedRateAmount`.
- Ücret çözümleme (ders > öğrenci > profil) istemci/ödeme oluşturma tarafında kalır; link'in `AgreedRateAmount`'ı listede/özette sunulur ki istemci ön-doldursun. (Payments domain'i değişmez; yalnız veri sunulur.)

### C.7 — Davet / bağlanma (B-06) — Faz 2
- `POST /api/students/teachers/{teacherUserId}/students/{studentId}/invite` (body: `Email` veya `Phone`) → kayıtlı kullanıcı bulunursa link `InviteSent` + `InviteTargetUserId` set + `TeacherStudentInviteSentDomainEvent`. Bulunamazsa `students.user_not_found`.
- `POST /api/students/links/{linkId}/accept` (öğrenci) → `Linked`; profil `UserId` bağlanır. `POST /links/{linkId}/reject` → `Rejected`.
- Parents modülündeki `children/link` + `approve/reject` deseni birebir örnek alınır.

## Test Stratejisi (TDD)
- `TeacherStudentLink` domain: durum geçişleri (`InviteSent→Linked/Rejected`), arşiv toggle, rate set.
- Free limit: 5. link varken 6.'yı reddet; arşivli link de sayılır.
- Create student → link oluşur; limit dolu ise profil de oluşmaz (atomiklik).
- List: arşivli hariç varsayılan; `includeArchived=true` dahil.
- Authorizer: başka öğretmenin linkine dokunulamaz.

## Doküman Bakımı
`doc/modules/m03_students.md`, `00_genel_bakis.md` (endpoint + M03 durum), `veri_modeli.md` (yeni tablo + ER), `doc/roles/ogretmen.md` §10 (B-04/B-06/B-07), `doc/roles/00_roller_genel_bakis.md` (çoklu öğretmen kuralı).

## Kabul Kriterleri
- [ ] Bir öğrenci birden fazla öğretmene bağlanabiliyor (link tablosu).
- [ ] 6. öğrenci eklenirken free limit uyarısı; arşivleme limiti boşaltmıyor.
- [ ] Manuel öğrenci oluşunca `Manual` link kuruluyor; mevcut veriler backfill'leniyor.
- [ ] Öğrenci bazlı ücret set/gösterim çalışıyor.
- [ ] Davet gönder → öğrenci kabul/red → link durumu doğru.
- [ ] Başka öğretmenin öğrencisine/linkine erişim reddediliyor.
