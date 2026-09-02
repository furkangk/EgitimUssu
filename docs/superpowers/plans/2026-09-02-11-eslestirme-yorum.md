# P11 — Eşleştirme/İlan (M12) ve Puanlama/Yorum (M13) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Platformun pazar yeri tarafını açmak: iki taraflı ilan, arama/keşif (konum + puan + premium sıralaması), herkese açık öğretmen profili, talep→kabul akışı ve doğrulanmış öğrenci yorumları (alt kategori puanları, öğretmen yanıtı, flag).

**Architecture:** M12 ve M13 iskeletten çıkarılır. M12'nin arama yüzeyi **projeksiyondur** (`TeacherSearchProjection`, P07 deseni): Teachers/Reviews/Membership event'lerinden beslenir, doğrudan DB okuması yoktur. Sıralama skoru sunucuda hesaplanır: `konum yakınlığı × yıldız × premium öne çıkarma` — "Öne Çıkan" sonuçlar **şeffaf etiketle** işaretlenir. M13 yorum uygunluğu `ReviewEligibility` ile tamamlanmış ders üzerinden doğrulanır (`LessonSessionCompleted` event'i). Yorum yayınlanınca `TeacherReviewPublished` event'i M12 projeksiyonundaki ortalamayı günceller.

**Tech Stack:** .NET 9, EF Core (+ `earthdistance`/haversine hesabı uygulama tarafında), xUnit; Flutter.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md`

## Global Constraints

- **Önkoşul:** P07 (projeksiyon), P09 (entitlement — premium öne çıkarma), P06 (öğretmen doğrulama + arama ucu) tamamlanmış olmalı.
- **PRD sırası:** PRD §10.1 — eşleştirme, Faz 1–3 gerçek kullanıcılarda doğrulandıktan sonra açılır. Bu plan yürütülmeden önce beta geri bildirimi alınmış olmalı.
- **Şeffaflık:** Ücretli öne çıkarma sonuçta **görünür etiketle** işaretlenir (`isPromoted: true` + UI rozeti). Gizli sıralama manipülasyonu yok.
- **Yorum bütünlüğü:** Öğretmen olumsuz yorumu silemez/gizleyemez; yalnız yanıt verebilir. Silme yalnız Admin moderasyonu (P12).
- **Doğrulanmış yorum:** Yalnız `Completed` ders oturumu olan öğrenci yorum yapabilir; her ders için en fazla bir yorum.
- **Idempotency:** Tüm projeksiyon handler'ları `inbox_messages` guard'ını kullanır.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: M13 — Yorum domain'i ve uçları

**Files:**
- Modify: `src/Modules/Reviews/Domain/*` (yeni `ReviewsDomainModel.cs`)
- Create: `src/Modules/Reviews/Application/ReviewFeatures.cs`, `ReviewPolicies.cs`
- Create: `src/Modules/Reviews/Infrastructure/ReviewRepository.cs`, `ReviewEligibilityHandler.cs`
- Modify: `src/Modules/Reviews/Infrastructure/{ReviewsDbContext,DependencyInjection}.cs` + migration
- Modify: `src/Modules/Reviews/API/ReviewsModule.cs`
- Test: `tests/Unit/ReviewTests.cs`, `tests/Unit/ReviewEligibilityTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Review : AggregateRoot<Guid>` — `Guid TeacherUserId`, `Guid StudentId`, `Guid LessonSessionId`, `int OverallRating` (1–5), `int ClarityRating`, `int PunctualityRating`, `int PatienceRating`, `int PreparationRating`, `string? Comment`, `ReviewVisibility Visibility` (`Public = 1, TeacherOnly = 2`), `string? TeacherReply`, `DateTime? TeacherRepliedOnUtc`, `bool IsRemoved`, `string? RemovalReason`, `DateTime CreatedOnUtc`.
  - `sealed class ReviewEligibility` — `Guid StudentId`, `Guid TeacherUserId`, `Guid LessonSessionId`, `bool IsUsed`, `DateTime CompletedOnUtc` (`LessonSessionCompleted` event'inden üretilir).
  - `enum ReviewFlagReason { Spam = 1, Abusive = 2, Fake = 3, Other = 9 }` + `sealed class ReviewFlag`.
  - Uçlar:
    - `GET /api/reviews/eligibility?teacherUserId=` (öğrenci) → değerlendirilebilir dersler
    - `POST /api/reviews` (öğrenci; `lessonSessionId` zorunlu)
    - `GET /api/reviews/teachers/{teacherUserId}?skip=&take=` (herkese açık yorumlar + ortalamalar)
    - `POST /api/reviews/{reviewId}/reply` (öğretmen)
    - `POST /api/reviews/{reviewId}/flag` (herhangi bir kimlikli kullanıcı)
  - Domain event: `TeacherReviewPublishedDomainEvent(Guid TeacherUserId, Guid ReviewId, int OverallRating, DateTime OccurredOnUtc)`.

- [ ] **Step 1: Testleri yaz (kırmızı)**
```csharp
[Fact] public void Rating_Out_Of_Range_Should_Throw() { }                          // 0 ve 6
[Fact] public void Review_Requires_Eligibility() { }                                // tamamlanmamış ders → reviews.not_eligible
[Fact] public void Second_Review_For_Same_Session_Should_Fail() { }                 // reviews.already_reviewed
[Fact] public void TeacherOnly_Review_Should_Not_Affect_Public_Average() { }
[Fact] public void Teacher_Cannot_Remove_Negative_Review() { }                      // yalnız reply
[Fact] public void Reply_Twice_Should_Overwrite_With_Timestamp() { }
```
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ReviewTests|FullyQualifiedName~ReviewEligibilityTests"`
- [ ] **Step 3: Domain + DbContext + migration**
  Tablolar: `reviews` (unique `(LessonSessionId)`), `review_eligibilities` (unique `(LessonSessionId)`), `review_flags`.
  Run: `dotnet ef migrations add InitialCreate --project src/Modules/Reviews/Infrastructure --startup-project src/API.Host --context ReviewsDbContext`
  > `DependencyInjection`'a `AddModuleDbContext<ReviewsDbContext>(...)` **artık eklenir** (K4 notu: entity var).
- [ ] **Step 4: `ReviewEligibilityHandler`** — `LessonSessionCompletedIntegrationEvent` tüketir (idempotent taban), uygunluk satırı yazar.
- [ ] **Step 5: Command/query + authorizer + endpoint'ler.**
- [ ] **Step 6: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 7: Doküman + commit**

`doc/modules/m13_reviews.md` (🔴 → 🟡), `doc/modules/00_genel_bakis.md`, `doc/INDEX.md`, `doc/modules/veri_modeli.md`.
```bash
git add src/Modules/Reviews tests doc
git commit -m "feat(reviews): dogrulanmis ogrenci yorumu (M13-1) + alt kategori puanlari (M13-2) + ogretmen yaniti (M13-3) + flag (M13-4) + TeacherOnly erken acilis (M13-6)"
```

---

### Task 2: M12 — İlan domain'i (iki taraflı)

**Files:**
- Modify: `src/Modules/Matching/Domain/*` (`MatchingDomainModel.cs`)
- Create: `src/Modules/Matching/Application/ListingFeatures.cs`, `MatchingPolicies.cs`
- Create: `src/Modules/Matching/Infrastructure/{ListingRepository,MatchingDbContext ek}` + migration
- Modify: `src/Modules/Matching/API/MatchingModule.cs`
- Test: `tests/Unit/ListingTests.cs`

**Interfaces:**
- Produces:
  - `enum ListingKind { TeacherOffer = 1, StudentRequest = 2 }`
  - `enum ListingStatus { Draft = 1, Published = 2, Paused = 3, Closed = 4 }`
  - `sealed class Listing : AggregateRoot<Guid>` — `Guid OwnerUserId`, `ListingKind Kind`, `ListingStatus Status`, `string Title`, `string? Description`, `string Subject`, `string City`, `string District`, `double? Latitude`, `double? Longitude`, `TeacherLessonFormatContract LessonFormat`, `decimal? HourlyRateMin`, `decimal? HourlyRateMax`, `string Currency`, `IReadOnlyList<DayOfWeek> AvailableDays`, `DateTime CreatedOnUtc`, `DateTime? PublishedOnUtc`, `bool IsPromoted`, `DateTime? PromotedUntilUtc`; metotlar `Publish(DateTime)`, `Pause(DateTime)`, `Close(DateTime)`, `Promote(DateTime until)`.
  - Uçlar: `POST /api/matching/listings`, `PUT /api/matching/listings/{id}`, `POST /api/matching/listings/{id}/publish|pause|close`, `GET /api/matching/listings/mine`.

- [ ] **Step 1: Testleri yaz (kırmızı)** — yayınlanmamış ilan aramada çıkmaz; ücret aralığı `min <= max`; kapalı ilan tekrar yayınlanamaz; `Promote` yalnız premium sahibinde (entitlement).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ListingTests"`
- [ ] **Step 3: Domain + DbContext + migration** (`listings`, index `(Kind, Status, City, Subject)`).
  Run: `dotnet ef migrations add InitialCreate --project src/Modules/Matching/Infrastructure --startup-project src/API.Host --context MatchingDbContext`
- [ ] **Step 4: Command/handler/authorizer + uçlar.**
- [ ] **Step 5: Yeşil gör + commit**

```bash
dotnet test EgitimUssu.slnx
git add src/Modules/Matching tests doc
git commit -m "feat(matching): iki tarafli ilan domain + uclari (M12-1/M12-2)"
```

---

### Task 3: M12 — Arama projeksiyonu ve sıralama

**Files:**
- Create: `src/Modules/Matching/Domain/TeacherSearchProjection.cs`
- Create: `src/Modules/Matching/Infrastructure/TeacherSearchProjectionHandlers.cs`
- Create: `src/Modules/Matching/Application/SearchFeatures.cs`
- Create: `src/Modules/Matching/Application/RankingScore.cs`
- Modify: `src/Modules/Matching/API/MatchingModule.cs`
- Modify: `src/Modules/Teachers/**` (yeni event'ler: profil güncellendi/doğrulandı/pasifleşti)
- Test: `tests/Unit/RankingScoreTests.cs`, `tests/Unit/TeacherSearchProjectionTests.cs`

**Interfaces:**
- Produces:
  - `sealed class TeacherSearchProjection : ProjectionEntity` — `Guid TeacherUserId` (PK), `string FullName`, `IReadOnlyList<string> Subjects`, `string City`, `string District`, `double? Latitude`, `double? Longitude`, `TeacherLessonFormatContract LessonFormat`, `decimal HourlyRateAmount`, `string Currency`, `bool IsVerified`, `bool IsActive`, `decimal AverageRating`, `int ReviewCount`, `bool IsPromoted`, `DateTime? PromotedUntilUtc`.
  - ```csharp
    public static class RankingScore
    {
        /// <summary>0..1 arası bileşik skor: %40 mesafe, %35 puan, %15 doğrulama, %10 öne çıkarma.</summary>
        public static double Compute(double? distanceKm, decimal averageRating, int reviewCount, bool isVerified, bool isPromoted);
    }
    ```
  - `GET /api/matching/search?subject=&city=&district=&lat=&lng=&maxDistanceKm=&minRate=&maxRate=&format=&days=&onlyVerified=&skip=&take=` → `PagedResult<TeacherSearchResultResponse>` (her öğede `isPromoted`, `distanceKm`, `averageRating`, `reviewCount`).

- [ ] **Step 1: Skor testlerini yaz (kırmızı)**
```csharp
[Fact] public void Closer_Teacher_Should_Score_Higher_All_Else_Equal() { }
[Fact] public void Higher_Rating_Should_Score_Higher() { }
[Fact] public void Low_Review_Count_Should_Dampen_Rating_Weight() { }   // 1 yorumlu 5.0 ≠ 50 yorumlu 5.0
[Fact] public void Promoted_Should_Add_Bounded_Bonus() { }              // öne çıkarma tek başına 1. sıraya taşımaz
[Fact] public void Unknown_Distance_Should_Use_Neutral_Value() { }
```
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~RankingScoreTests"`
- [ ] **Step 3: `RankingScore`'u yaz** (saf fonksiyon, DB'siz — bu yüzden kolay test edilir).
- [ ] **Step 4: Projeksiyon + handler'lar**
  Beslenen event'ler: `TeacherProfileCreated/Updated`, `TeacherVerified` (P06), `TeacherProfileDeactivated`, `TeacherReviewPublished` (Task 1), `MembershipChanged` (P09 — premium öne çıkarma hakkı), `ListingPublished/Closed`.
  Run: `dotnet ef migrations add AddTeacherSearchProjection --project src/Modules/Matching/Infrastructure --startup-project src/API.Host --context MatchingDbContext`
- [ ] **Step 5: Arama sorgusu** — filtreler SQL'de, mesafe ve skor uygulama tarafında (haversine); `take` en fazla 50.
- [ ] **Step 6: P06'daki geçici arama ucunu devret** — `GET /api/teachers/profiles` handler'ı artık projeksiyonu okur; dış sözleşme korunur, `doc/modules/m02_teachers.md`'ye not düşülür.
- [ ] **Step 7: Yeşil gör + commit**

```bash
dotnet test EgitimUssu.slnx
git add src/Modules tests doc
git commit -m "feat(matching): arama projeksiyonu + bilesik siralama (M12-3/M12-4/M12-9)"
```

---

### Task 4: M12 — Public profil + talep akışı

**Files:**
- Modify: `src/Modules/Matching/Domain/MatchingDomainModel.cs` (`MatchRequest`)
- Modify: `src/Modules/Matching/Application/*`, `src/Modules/Matching/API/MatchingModule.cs`
- Modify: `src/Modules/Students/**` (kabulde öğretmen-öğrenci bağı kurulması — event tüketimi)
- Modify: `src/Modules/Scheduling/**` (kabulde ilk dersin programa eklenmesi — opsiyonel alan)
- Test: `tests/Unit/MatchRequestTests.cs`

**Interfaces:**
- Produces:
  - `enum MatchRequestStatus { Pending = 1, Accepted = 2, Rejected = 3, Cancelled = 4, Expired = 5 }`
  - `sealed class MatchRequest : AggregateRoot<Guid>` — `Guid FromUserId`, `Guid ToUserId`, `Guid? ListingId`, `string Message`, `MatchRequestStatus Status`, `DateTime CreatedOnUtc`, `DateTime ExpiresOnUtc` (14 gün), `DateTime? RespondedOnUtc`.
  - `GET /api/matching/teachers/{teacherUserId}/public-profile` (**anonim erişilebilir**, `IAllowAnonymous`) — ad, branşlar, şehir/ilçe, ücret, format, doğrulama rozeti, ortalama puan + yorumlar (M13'ten), aktif ilanlar. **Telefon/e-posta gösterilmez.**
  - `POST /api/matching/requests` · `POST /api/matching/requests/{id}/accept|reject|cancel` · `GET /api/matching/requests?direction=incoming|outgoing`
  - Kabulde: `MatchAcceptedIntegrationEvent` → Students bağ kurar (`TeacherStudent` daveti otomatik `Accepted`), Notifications iki tarafa bildirir.

- [ ] **Step 1: Testleri yaz (kırmızı)** — kendine talep gönderilemez; aynı hedefe açık talep varken ikinci talep `matching.duplicate_request`; süresi geçmiş talep kabul edilemez; kabulde event üretiliyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~MatchRequestTests"`
- [ ] **Step 3: Domain + migration + handler + uçlar.**
- [ ] **Step 4: Kabul akışının Students tarafını yaz** (idempotent tüketici).
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

```bash
git add src/Modules tests doc
git commit -m "feat(matching): public profil + talep/kabul akisi (M12-5/M12-6/M12-8)"
```

---

### Task 5: Mobil — keşif, public profil, yorum ekranları

**Files:**
- Create: `mobile/lib/features/discovery/**` (arama + filtre + sonuç listesi + public profil)
- Create: `mobile/lib/features/reviews/**` (yorum yaz + yorum listesi)
- Modify: `mobile/lib/features/study/presentation/pages/student_teacher_page.dart` (gerçek keşfe bağla)
- Modify: `mobile/lib/core/routing/app_router.dart` (`/discover`, `/teachers/:userId`, `/reviews/new`)
- Test: `mobile/test/features/discovery/discovery_cubit_test.dart`, `mobile/test/features/reviews/review_form_test.dart`
- Create: `doc/pages/discovery_search.md`, `doc/pages/teacher_public_profile.md`, `doc/pages/review_form.md`

**Interfaces:**
- `DiscoveryRepository.search({...filtreler, skip, take})`, `publicProfile(teacherUserId)`, `sendRequest({toUserId, listingId, message})`.
- `ReviewsRepository.eligibility(teacherUserId)`, `create({...})`, `listForTeacher(teacherUserId)`.

- [ ] **Step 1: Cubit testlerini yaz (kırmızı)** — filtre değişince yeni arama; sayfalama; "Öne Çıkan" rozetinin `isPromoted` ile gösterilmesi; uygunluk yoksa "Değerlendir" butonu kapalı.
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/features/discovery test/features/reviews`
- [ ] **Step 3: Repository + cubit'leri yaz.**
- [ ] **Step 4: Arama ekranı** — üstte arama + filtre çekmecesi (branş, şehir, ücret aralığı, format, gün, yalnız doğrulanmış, mesafe), sonuç kartında ad/branş/puan/ücret/mesafe + "Öne Çıkan" rozeti.
- [ ] **Step 5: Public profil ekranı** — özet, branşlar, uygunluk, puan dağılımı, yorumlar, "Ders talebi gönder".
- [ ] **Step 6: Yorum formu** — 1 genel + 4 alt kategori yıldız, opsiyonel yorum metni, "Yalnız öğretmene özel" seçeneği.
- [ ] **Step 7: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 8: Doküman + commit**

```bash
git add mobile doc
git commit -m "feat(mobile): kesif/arama + public profil + yorum ekranlari (D-06/D-07/D-09)"
```

---

### Task 6: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test` → yeşil.
- [ ] **Step 2: Uçtan uca** — Öğretmen ilan yayınlar → öğrenci arar, bulur → talep gönderir → öğretmen kabul eder → bağ kurulur → ders planlanır → tamamlanır → öğrenci yorum yapar → yorum public profilde ve arama sıralamasında görünür.
- [ ] **Step 3: Projeksiyon yeniden inşa** — `TeacherSearchProjection` tablosunu boşalt, yeniden inşa et, arama sonuçları aynı çıksın.
- [ ] **Step 4: Dokümanlar** — `doc/modules/m12_matching.md` + `m13_reviews.md` (🔴 → 🟢), `00_genel_bakis.md`, `INDEX.md`, `veri_modeli.md`, `doc/roles/*.md`, `doc/yol_haritasi.md` Faz 4, `doc/denetim/2026-09-02_eksik_analizi.md` M12-*, M13-* → `✅ (P11)`.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: P11 eslestirme ve yorum kapanisi (M12-*/M13-*)"
```

---

## Kabul Kriterleri

- [ ] Öğretmen ilan yayınlayabiliyor; öğrenci "ders arıyorum" ilanı açabiliyor
- [ ] Arama filtreleri (branş, şehir, ücret, format, gün, doğrulanmış, mesafe) doğru çalışıyor
- [ ] Sıralama bileşik skorla yapılıyor; öne çıkarma **etiketli** ve tek başına 1. sıraya taşımıyor
- [ ] Public öğretmen profili anonim erişilebilir ve iletişim bilgisi sızdırmıyor
- [ ] Talep gönder → kabul → öğretmen-öğrenci bağı otomatik kuruluyor
- [ ] Yalnız dersi tamamlamış öğrenci yorum yapabiliyor; ders başına tek yorum
- [ ] Öğretmen olumsuz yorumu silemiyor, yalnız yanıtlayabiliyor
- [ ] Yorum yayınlanınca arama ortalaması güncelleniyor (idempotent)
- [ ] `TeacherSearchProjection` sıfırdan yeniden inşa edilebiliyor
- [ ] Tam test paketi (Docker'lı) yeşil
