# Veli V-F — Entegre Dashboard Zenginleştirme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Veli panelini dokümanın "temel görünümler"ine ulaştır: (1) **çalışma verisini gerçekten göster** (bugün panelde hep 0 — read-model'e hiç yazılmıyor); ders dağılımı; (2) son ders özeti (konu + öğretmen notu); (3) yaklaşan dersler; (4) öğretmen notları (görünürlük filtreli); (5) ödeme detay listesi. Hepsi V-B gizlilik filtresine ve not görünürlüğüne saygılı.

**Architecture:** Veli paneli read-model'i (ChildProgressSnapshot) rolling haftalık çalışma süresini olaylardan **toplayamaz** (kayan pencere). Bu yüzden çalışma/ders/not verileri okuma anında `Shared.Contracts` "digest" arayüzleriyle çekilir (V-B'deki `IStudentPrivacyDirectory` ile aynı canlı-okuma deseni): `IStudyDigestDirectory` (Study), `IStudentLessonDigestDirectory` (Scheduling/LessonSessions), `IStudentNotesDirectory` (Assignments/M06). Ödeme detayı için Payments'a öğrenci-kapsamlı liste eklenir. **Karar (2026-07-19):** öğretmen notları `LessonNoteVisibility` ∈ {Student, StudentAndParent} olanları görünür. **Bağımlılık:** V-B (gizlilik filtresi + `IsShared`).

**Tech Stack:** .NET 9, EF Core, CQRS, xUnit. Cross-module: birden çok `Shared.Contracts` read arayüzü.

## Global Constraints
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Migration yalnız Payments'ta öğrenci-kapsamlı sorgu için gerekmez (indeks mevcut). Diğer dilimler yeni tablo eklemez → migration yok.
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Okuma canlı `Shared.Contracts` arayüzleriyle; Parents doğrudan başka modül DB'sine erişmez.

## File Structure
- `src/Shared/Contracts/StudyDigestContract.cs`, `StudentLessonDigestContract.cs`, `StudentNotesContract.cs` *(yeni)*.
- İlgili modül Infrastructure'larında implementasyonlar + DI (Study, Scheduling/LessonSessions, Assignments).
- `src/Modules/Payments/Application` + repo: `ListByStudentIdAsync` + parent-kapsamlı sorgu.
- `src/Modules/Parents/Application/ParentFeatures.cs` — `ChildDashboardResponse` genişletme + `GetChildDashboardQueryHandler` yeni arayüzleri çağırır.
- Test: `tests/Unit/ChildDashboardEnrichmentTests.cs`.

---

### Task 1: Çalışma digest'i (panel "0" bug'ını düzelt) + ders dağılımı

**Files:** `src/Shared/Contracts/StudyDigestContract.cs` (yeni), Study Infrastructure impl + DI, `ParentFeatures.cs`, Test.

**Interfaces:**
- Produces:
```csharp
namespace EgitimUssu.Shared.Contracts;

public sealed record StudySubjectMinutes(string Subject, int Minutes);
public sealed record StudyDigest(int WeeklyStudyMinutes, int StreakDays, IReadOnlyCollection<StudySubjectMinutes> SubjectBreakdown);

public interface IStudyDigestDirectory
{
    // studentId için son 7 günün toplam çalışma dk + güncel streak + ders bazlı dağılım.
    Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken cancellationToken);
}
```

- [ ] **Step 1:** Contract dosyasını yaz.
- [ ] **Step 2:** Study Infrastructure'da `StudyDigestDirectory` impl: Study'nin tamamlanmış seans tablosundan son 7 gün (`nowUtc-7d..nowUtc`) toplam dakika + ders bazlı grup; streak Study'nin `StudyStreak` aggregate'inden. (Study repo/DbContext gerçek isimlerini keşfet; `StudySession`/`StudyStreak`.) DI kaydı.
- [ ] **Step 3:** `ChildDashboardResponse.Study`'yi zenginleştir: `StudySummaryResponse(int WeeklyStudyMinutes, int StreakDays, bool HasData, bool IsShared, IReadOnlyCollection<StudySubjectMinutes> SubjectBreakdown)`. `GetChildDashboardQueryHandler`: `isStudyShared` true ise `IStudyDigestDirectory.GetWeeklyDigestAsync` çağır, değerleri buradan doldur (snapshot yerine); false ise 0/boş + `IsShared=false` (V-B davranışı korunur). `IClock` enjekte et (`nowUtc`).
- [ ] **Step 4: Failing test → impl → PASS** — `ChildDashboardEnrichmentTests`: sahte `IStudyDigestDirectory` (120 dk, streak 5, 2 ders) + shared=true → dashboard 120/5/2-ders; shared=false → 0/boş.
- [ ] **Step 5: Commit** `feat(parents): panel çalışma verisi + ders dağılımı (IStudyDigestDirectory) (Veli V-F)`.

> Not: Bu task, `ChildProgressSnapshot.WeeklyStudyMinutes`/`StudyStreakDays`'in **hiç yazılmadığı** mevcut bug'ını (Parents keşif bulgusu) canlı digest ile giderir; snapshot'taki bu iki alan kullanımdan kalkar (dokümana not düş).

---

### Task 2: Son ders özeti + yaklaşan dersler

**Files:** `src/Shared/Contracts/StudentLessonDigestContract.cs` (yeni), Scheduling + LessonSessions impl + DI, `ParentFeatures.cs`, Test.

**Interfaces:**
- Produces:
```csharp
public sealed record UpcomingLesson(Guid LessonScheduleId, string Subject, DateTime StartAtUtc, DateTime EndAtUtc);
public sealed record LastLessonSummary(Guid LessonSessionId, string TopicTitle, string? TeacherNotes, DateTime? CompletedOnUtc);
public interface IStudentLessonDigestDirectory
{
    Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken cancellationToken);
    Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken cancellationToken);
}
```

- [ ] **Step 1:** Contract. **Step 2:** Impl: `GetUpcomingAsync` Scheduling `LessonSchedule` (studentId, StartAtUtc>=from, Planned) sıralı ilk N; `GetLastCompletedAsync` LessonSessions'tan son tamamlanan (mevcut `ILessonSessionAccessService` yalnız by-id; burada "öğrenci için son tamamlanan" sorgusu eklenir — LessonSessions repo/DbContext). Not: `TeacherNotes` bu özet için öğretmen notu; V-F Task 3 not görünürlüğüyle çelişmemesi için son-ders özetindeki `TeacherNotes` yalnız veli-görünür seviyedeyse doldur (ya da bu alanı Task 3'e bırak). DI kayıtları.
- [ ] **Step 3:** `ChildDashboardResponse`'a `IReadOnlyCollection<UpcomingLesson> UpcomingLessons` + `LastLessonSummary? LastLesson` ekle; handler doldurur.
- [ ] **Step 4: Test → PASS ; Commit** `feat(parents): son ders özeti + yaklaşan dersler (Veli V-F)`.

---

### Task 3: Öğretmen notları (görünürlük filtreli)

**Files:** `src/Shared/Contracts/StudentNotesContract.cs` (yeni), Assignments (M06) impl + DI, `ParentFeatures.cs`, Test.

**Interfaces:**
- Produces:
```csharp
public sealed record ParentVisibleNote(Guid Id, string Content, DateTime CreatedOnUtc);
public interface IStudentNotesDirectory
{
    // Veliye görünür öğretmen notları: LessonNoteVisibility ∈ {Student, StudentAndParent} (karar 2026-07-19).
    Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken cancellationToken);
}
```

- [ ] **Step 1:** Contract. **Step 2:** Impl (Assignments/M06 `LessonNote`): `Where(Visibility == Student || Visibility == StudentAndParent)` (karar: Student + StudentAndParent), `StudentId` filtreli, `CreatedOnUtc` azalan ilk N. **Private notlar asla dönmez.** DI.
- [ ] **Step 3:** `ChildDashboardResponse`'a `IReadOnlyCollection<ParentVisibleNote> TeacherNotes`; handler doldurur.
- [ ] **Step 4: Test → PASS** (Private not dönmediğini doğrula) ; **Commit** `feat(parents): veli-görünür öğretmen notları (Student+StudentAndParent) (Veli V-F)`.

---

### Task 4: Ödeme detay listesi

**Files:** `src/Modules/Payments/Application/*` (öğrenci-kapsamlı liste + `IParentAccessDirectory` [V-G] ile yetki), `IPaymentRecordRepository.ListByStudentIdAsync`, `ParentFeatures.cs` veya doğrudan Payments endpoint, Test.

**Interfaces:**
- Produces: `IPaymentRecordRepository.ListByStudentIdAsync(Guid studentId, CancellationToken)`; veli-görünür ödeme kalemleri (tutar, vade, durum) — panelin `PaymentSummaryResponse`'una ek olarak `IReadOnlyCollection<PaymentLineResponse> PaymentLines`.

- [ ] **Step 1:** Repo `ListByStudentIdAsync` + impl (index `{StudentId, Status, DueDateUtc}` mevcut). **Step 2:** Bir `IStudentPaymentDigestDirectory` (Shared.Contracts, Payments impl) veya Parents'ın mevcut ödeme snapshot'ını satır düzeyine genişletme — **tercih:** yeni `IStudentPaymentDigestDirectory.GetLinesAsync(studentId, take)` (canlı okuma, diğer digest'lerle tutarlı). **Step 3:** `ChildDashboardResponse`'a `PaymentLines`; handler doldurur. **Step 4: Test → PASS ; Commit** `feat(parents): veli ödeme detay listesi (Veli V-F)`.

---

### Task 5: Dokümantasyon
- [ ] `doc/modules/m09_parents.md`: zenginleştirilmiş dashboard (çalışma+dağılım, son ders özeti, yaklaşan dersler, öğretmen notları [Student+StudentAndParent], ödeme detay); çalışma verisinin artık **canlı digest** ile geldiği (snapshot'taki `WeeklyStudyMinutes`/`StudyStreakDays` alanlarının kullanımdan kalktığı) notu; V-B gizlilik + not görünürlüğü etkileşimi.
- [ ] Yeni kontratlar (`IStudyDigestDirectory`, `IStudentLessonDigestDirectory`, `IStudentNotesDirectory`, `IStudentPaymentDigestDirectory`) → `doc/modules/veri_modeli.md` kontrat listesi + ilgili modül md'leri (m08/m04/m05/m06/m07).
- [ ] `doc/roles/veli.md`: V-09.8–09.25 satırlarını "🟢 kodda" güncelle.
- [ ] commit `docs: veli entegre dashboard zenginleştirme (Veli V-F)`.

## Self-Review
- **Spec coverage:** Spec V-F "yaklaşan ders, son ders özeti+not, öğretmen notları, ödeme detay + gizlilik" → Task 1-4. Ayrıca keşif bug'ı (çalışma verisi hiç yansımıyor) Task 1'de düzeltiliyor.
- **Bağımlılık:** V-B (gizlilik `IsShared`); V-G Task 4 ile `IParentAccessDirectory` yetki için (varsa yeniden kullan). Study/LessonSessions/Assignments read-model gerçek isimleri uygulama sırasında keşfedilecek (bu plan kontrat şekillerini sabitler).
- **Placeholder riski:** Bu dilim en geniş; her digest impl'i ilgili modülün gerçek repo/DbContext isimlerini gerektirir (bu planda kontrat şekilleri + handler entegrasyonu kesin; impl adımları "ilgili modül tablosundan sorgula" düzeyinde — uygulayıcı o modülün deseniyle yazar). Gerekirse V-F kendi içinde 4 ayrı küçük plana bölünebilir (her Task bağımsız).
- **Not görünürlüğü:** Öğretmen notu yalnız `Student`+`StudentAndParent`; `Private` asla (karar 2026-07-19). Değişmez kural (kişisel seans notu) ayrıca korunur — o M08 öğrenci notudur, bu contract'lara hiç girmez.
