# Dilim D — Öğretmen Profili Olgunluk · Tasarım Spec'i

**Tarih:** 2026-07-18
**Kaynak:** `doc/roles/ogretmen.md` §10.2 (yanlış yapılandırma #1) · fonksiyonel doküman T-02.3 (çoklu branş) + T-02.12 (sertifika)
**Kapsam:** M02 (Teachers). 2 madde: çoklu branş (`Subjects` koleksiyonu), sertifika (`TeacherCertificate`).

## Mevcut Durum
- `TeacherProfile.Subject` **tek string**. `TeacherProfileCreatedDomainEvent`/`TeacherProfileUpdatedDomainEvent` bu tek `Subject`'i taşır.
- `TeacherAvailabilitySlot` koleksiyonu var; Create/Update komutlarında `AvailabilitySlots` toplu set edilir (`BuildAvailabilitySlots` + `AddRange`) — izlenecek desen budur.
- Sertifika kavramı yok.

## Tasarım

### D.1 — Çoklu branş (T-02.3)
- Yeni child entity `TeacherSubject(Id, TeacherProfileId, Subject)`.
- `TeacherProfile.Subjects` (List) — `AvailabilitySlots` desenini izler.
- **Geriye uyum:** Mevcut tek `Subject` **birincil branş** olarak korunur (domain event + Matching filtreleri kırılmasın). `Subjects` ek branşları tutar. Create/Update, `Subjects` koleksiyonunu alır; boşsa birincil `Subject`'ten bir satır türetilir (migration + handler).
- `Create/UpdateTeacherProfileCommand`/`Request`'e `IReadOnlyCollection<string> Subjects`. `TeacherProfileResponse`'a `IReadOnlyCollection<string> Subjects`.

### D.2 — Sertifika (T-02.12)
- Yeni child entity `TeacherCertificate(Id, TeacherProfileId, Title, Institution, Year, FileUrl?)`.
- `TeacherProfile.Certificates` (List) — `AvailabilitySlots` desenini izler; Create/Update toplu set.
- `Create/UpdateTeacherProfileCommand`/`Request`'e `IReadOnlyCollection<TeacherCertificateRequest> Certificates`. Response'a `IReadOnlyCollection<TeacherCertificateResponse> Certificates`.

## Test Stratejisi (TDD)
- `TeacherProfile.Update` `Subjects`/`Certificates` koleksiyonlarını değiştirir (clear + addrange).
- Boş `Subjects` verilince birincil `Subject`'ten türetme.
- Sertifika alanları round-trip.

## Doküman Bakımı
`doc/modules/m02_teachers.md`, `00_genel_bakis.md`, `veri_modeli.md`, `doc/roles/ogretmen.md` §10.2/§10.3.

## Kabul Kriterleri
- [ ] Öğretmen birden çok branş tanımlayabiliyor; birincil branş korunuyor.
- [ ] Sertifika ekleyip listeleyebiliyor.
- [ ] Mevcut profiller migration sonrası birincil branştan bir `Subjects` satırına sahip.
