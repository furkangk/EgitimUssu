---
title: "M09 — Veli Paneli (Parents)"
summary: "Veli paneli read-model modülü olarak uygulanmış; çocuğun bireysel çalışma + ders/ödev/ödeme verisini birleştirir, M08 haftalık dk/streak beslemesi henüz bağlanmadı"
tags: [modul, parents, veli-paneli, read-model, faz-2, faz-3]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Parents/**
updated: 2026-11-15
---

# 👪 M09 — Veli (Parents) Modülü — Detaylı Tasarım Dokümanı

> **Kod Modülü:** `src/Modules/Parents` · **Route Prefix:** `/api/parents` · **Şema:** `parents`
> **PRD Modülü:** M09 Veli Paneli · **Faz:** 2-3 (Faz 2: bireysel çalışma görünümü; Faz 3: öğretmen verisi)
> **Durum:** 🟢 **Uygulandı** (domain + application/CQRS + API + migration + read-model + mobil feature + rol navigasyonu — hepsi kodda, uçtan uca çalışır)
> **Platform:** EğitimÜssü (EgitimUssu) — .NET 9 modüler monolit · PostgreSQL · Redis · Flutter
>
> **Amaç:** Veli, çocuğunun gelişimini **şeffaf** şekilde izlesin. Veli paneli **iki kaynaktan** beslenir:
> (1) çocuğun **bireysel çalışması** (öğretmen gerekmez — M08), (2) öğretmen bağlıysa **ders/ödev/ödeme**
> verisi (M05/M06/M07). Bu modül büyük ölçüde bir **read-model** modülüdür: kendi domain verisi azdır;
> diğer modüllerin verisini veli perspektifinden **okur/birleştirir** (integration event ile beslenen `parents` şeması read-model tabloları).

> ℹ️ **Not:** Aşağıdaki Domain/API/İş Kuralları/Olay Akışı bölümleri artık **kodda uygulanmıştır** ve
> gerçek alan/uç adlarıyla hizalıdır. (Haftalık çalışma dk + streak read-model kolonları hazırdır; **M08 Study → Parents**
> integration event beslemesi henüz bağlanmadığı için bu iki alan şimdilik boş kalır.)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Uygulandı (domain + application + API + infra + read-model)
- **Domain:** `ParentProfile` (AggregateRoot — düz bildirim tercihi alanları + `NotificationChannel`) ve
  `ParentChildLink` (AggregateRoot — onaya dayalı bağ, `ParentChildLinkStatus`) tanımlı; davranışlar + domain olayları mevcut.
- **Application (CQRS):** profil oluştur/oku, bildirim tercihleri güncelle, bağ talep/onay/ret/iptal, bağlı çocuk listesi
  ve birleşik çocuk paneli sorguları — komut/sorgu + handler'lar kodda.
- **API:** `/api/parents` altında profil, bildirim tercihi, bağ akışı ve dashboard uçları (bkz. Bölüm 3) — tümü auth (`AuthenticatedUser`).
- **Infrastructure:** `ParentsDbContext` `DbSet`'leri, **`parents` şemasında `InitialCreate` migration'ı**,
  integration event handler'ları (LessonSessions/Assignments/Payments/Students tüketimi) ve `AddParentsModule(...)` DI kaydı tamam.
- **Read-model tabloları** (`parents` şeması, integration event ile beslenir): `ChildProgressSnapshot` (öğrenci başına
  tamamlanan/planlanan ders + son ders tarihi, toplam/açık/tamamlanan ödev, beklenen/tahsil/kalan ödeme + para birimi,
  haftalık çalışma dk + streak [M08 gelince]), `KnownStudent` (StudentId→UserId eşlemesi, bağ onay yetkisi için).
  Projeksiyon idempotency'si artık modüle özel bir tablo yerine paylaşılan `IdempotentIntegrationEventHandler`
  tabanı + `inbox_messages` tablosu (composite PK `(EventId, Handler)`) ile sağlanır; eski `ProcessedIntegrationEvent`
  entity'si ve `processed_integration_events` tablosu **kaldırıldı** (2026-08-26, bkz. `mimari_inceleme.md` Y4).
- **Mobil veli feature** (`mobile/lib/features/parent/`) ve `Parent` rolü için **rol bazlı navigasyon** kodda (bkz. Bölüm 6).

### 🟢 Bağ noktaları (başka modüllerde)
- `Identity` rolü: `UserRole.Parent = 4` (veli, **gerçek kayıtlı kullanıcı** olmalı — bkz. İş Kuralları 4.1). Register/Login `roleId 4` taşır.
- M03 Students: `StudentProfile.ParentUserId` (`Guid?`) alanı mevcut. **Students modülü artık `ParentChildLinkApprovedDomainEvent`'i
  tüketip** (yeni handler + `StudentProfile.LinkParent` metodu) onaylı bağda `ParentUserId`'yi set eder.

### ⏳ M08 Study beslemesine bağlı
- Haftalık çalışma dk + streak kolonları read-model'de (`ChildProgressSnapshot`) hazır; **M08 Study → Parents** integration event beslemesi henüz bağlanmadı.

---

## 2. Domain Modeli (✅ Uygulandı)

**P18 T1 — eşzamanlılık (K-22):** `ParentChildLink` `IVersioned` uygular; `uint Version { get; private set; }` PostgreSQL’in ürettiği `xmin` (`xid`) sistem kolonuna eşlenir. `ModuleDbContext` tip modele eklenirken concurrency + OnAddOrUpdate konvansiyonunu uygular. Aynı sürümü okuyan iki yazmadan kaybeden `409 shared.concurrency_conflict` alır: “Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.” `AddVersionToken` yalnız model metadatasını kaydeder; yeni kolon/veri göçü yoktur. Outbox hatayı normal backoff ile yeniden teslim eder; yeni scope güncel veriyi yükler, başarısız iş ve inbox aynı transaction’da geri alınır.


> `AggregateRoot<Guid>` / `Entity<Guid>` desenini izler (private ctor, `private set`, enum `1`'den, `Raise(...)`).
> Veli modülünün **kendi ürettiği veri azdır**; çoğu veri diğer modüllerden okunur (read-model). Tablolar `parents` şemasında.

### 2.1 `ParentProfile` (AggregateRoot)

> Bildirim tercihleri **ayrı VO değil**, aggregate üzerinde **düz alanlar** olarak tutulur.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `UserId` | `Guid` | ✓ | **Gerçek** Identity kullanıcısı (`UserRole.Parent`) — null olamaz (İş Kuralları 4.1) |
| `FullName` | `string` | ✓ | Veli adı |
| `ContactPhone` | `string?` | — | |
| `ContactEmail` | `string?` | — | |
| `NotifyMissedAssignment` | `bool` | ✓ | Ödev kaçırma bildirimi (M11) |
| `NotifyWeeklyProgressSummary` | `bool` | ✓ | Haftalık gelişim özeti |
| `NotifyLessonReminders` | `bool` | ✓ | Yaklaşan ders hatırlatması (öğretmen bağlıysa) |
| `NotifyTestResults` | `bool` | ✓ | Yeni deneme sonucu bildirimi |
| `NotifyPayments` | `bool` | ✓ | Ödeme hatırlatması (öğretmen bağlıysa) |
| `NotificationChannel` | `NotificationChannel` (enum) | ✓ | `Push=1` / `Email=2` / `Both=3` |
| `MembershipTier` | `MembershipTier` (Shared.Contracts) | ✓ | ⚠️ **LEGACY (P12'de silinecek).** P09 Task 2'den beri karar bu alandan **okunmaz**: veli bildirim seviyesi `IEntitlementDirectory` üzerinden Membership'ten gelir. Alan yalnız **geçiş köprüsünün** girdisi (`ParentMembershipDirectory` → `EntitlementDirectory`), Admin `PUT /membership-tier` ile yazmaya devam eder. |
| `IsActive` | `bool` | ✓ | |
| `CreatedOnUtc` | `DateTime` | ✓ | |
| `UpdatedOnUtc` | `DateTime` | ✓ | |

```csharp
public enum NotificationChannel { Push = 1, Email = 2, Both = 3 }
```

**Davranışlar:** `UpdateContact(...)`, `UpdateNotificationPreferences(...)`, **`SetMembershipTier(...)`** (Veli V-E). Olay: `ParentProfileCreatedDomainEvent`.

> **Bildirim tercihleri artık fiilen tüketiliyor (Veli V-E, 2026-07-19):** Bu anahtarlar M11 `ParentEventNotificationHandler` + haftalık özet servisi tarafından okunur (`IParentNotificationDirectory` üzerinden). `NotifyMissedAssignment`→yeni ödev, `NotifyLessonReminders`→ders tamamlandı, `NotifyPayments`→ödeme, `NotifyWeeklyProgressSummary`→haftalık özet; bağlantı bildirimi (V-C) koşulsuz. **Tümü Premium kapısına tabidir.**

### 2.2 `ParentChildLink` (AggregateRoot) — Veli–öğrenci bağı (onaylı, çoklu)

`StudentProfile.ParentUserId` tekil/basit senaryoyu karşılar; **çoklu çocuk + onaylı bağ** ayrı aggregate ile yönetilir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `ParentUserId` | `Guid` | ✓ | Veli Identity kullanıcısı |
| `StudentId` | `Guid` | ✓ | M03 `StudentProfile.Id` (mantıksal referans) |
| `ChildDisplayName` | `string?` | — | Panelde gösterilecek çocuk adı |
| `Relationship` | `string?` | — | Yakınlık (anne/baba/vasi) |
| `InviteCode` | `string?` | — | Davet/eşleşme kodu (varsa) |
| `IsPrimaryContact` | `bool` | ✓ | Birincil veli mi |
| `Status` | `ParentChildLinkStatus` | ✓ | `Pending`/`Approved`/`Rejected`/`Revoked` |
| `RequestedOnUtc` | `DateTime` | ✓ | Talep anı |
| `LinkedOnUtc` | `DateTime?` | — | Onay anı |
| `ApprovedByUserId` | `Guid?` | — | Onaylayan (öğrenci/öğretmen/Admin) |
| `UpdatedOnUtc` | `DateTime` | ✓ | |

```csharp
public enum ParentChildLinkStatus { Pending = 1, Approved = 2, Rejected = 3, Revoked = 4 }
```

**Davranışlar:** ctor `Pending` doğurur; `Approve(byUserId, existingPrimary?, nowUtc)` → `Result` — **yalnız `Pending`'den**
`Approved` + `LinkedOnUtc` (P15 / N-26: reddedilmiş/iptal edilmiş bağ yeniden onaylanamaz, onaylı bağın ikinci onayı da
reddedilir → `parents.invalid_link_transition`); `Reject(...)` (`Pending`/`Approved` → `Rejected`); `Revoke(...)` (→ `Revoked`).
Olaylar: `ParentChildLinkRequestedDomainEvent`, `ParentChildLinkApprovedDomainEvent`, `ParentLinkConnectionNoticeDomainEvent`,
**`ParentChildLinkEndedDomainEvent(LinkId, ParentUserId, StudentId, Reason, EndedOnUtc)`** — yalnız **yaşayan** (`Pending`/`Approved`)
bağ reddedilir ya da iptal edilirken (`Reason` = `Rejected`/`Revoked`, `Shared.Contracts.ParentChildLinkEndReason`); eski bir ret
kaydını iptale çekmek olay üretmez. Tüketicisi olmayan eski `ParentChildLinkRejected`/`RevokedDomainEvent` kaldırıldı (P15 T4).
Bir veli **birden çok** çocuğa bağlanabilir (çoklu link).

**Bütünlük (P15 / N-32):** kısmi benzersiz indeks `IX_parent_child_links_StudentId_primary` — `(StudentId) WHERE "IsPrimaryContact"
AND "Status" = 'Approved'` (öğrenci başına tek onaylı birincil veli). Migration `ParentLinkIntegrity` indeksten önce mevcut fazla
birincilleri en erken bağlanan hariç ikincile çevirir. İhlal `ParentRepository.SaveLinkChangesAsync`'te `parents.primary_exists`'e çevrilir.

### 2.3 Read-model tabloları (`parents` şeması — integration event ile beslenir)

Veli paneli, kaynak modüllerin verisini **doğrudan cross-module DB erişimiyle değil**, integration event ile beslenen
`parents` şeması read-model tablolarından okur (modül sınırı kuralı).

| Tablo | İçerik |
|-------|--------|
| `ChildProgressSnapshot` | Öğrenci başına: tamamlanan/planlanan ders sayısı + son ders tarihi; toplam/açık/tamamlanan ödev; beklenen/tahsil/kalan ödeme + para birimi; haftalık çalışma dk + streak (**M08 gelince**) |
| `KnownStudent` | `StudentId → UserId` eşlemesi; bağ onay/ret yetkisi ve veli panelindeki gizlilik ayarı için. Students `StudentProfileCreated` **ve** (P15 T4 / N-27) `StudentProfileLinkedToUser` (davetle bağlanan öğrenci) ile beslenir; bilinen bir kullanıcı hiçbir olayla `null`'a çekilmez. Eskiden ikinci olay tüketilmiyordu: davetle bağlanan öğrenci veli talebini onaylayamıyor, "Gizli" ayarı veli panelinde okunamıyordu |

**Profil birleşmesi (P15 T3 / N-28) — `StudentProfilesMergedParentsHandler`:** Students iki öğrenci profilini birleştirince
(`StudentProfilesMergedDomainEvent`) veli tarafı kanonik kimliğe geçer — eskiden Parents bu olayı tüketmiyordu, birleşme
sonrası veli paneli boşalıyordu. **Veli bağları** (benzersiz indeks yok, çakışma sessiz): aynı velinin iki profildeki yaşayan
(`Pending`/`Approved`) bağlarından biri kalır — onaylı olan, eşitlikte kanoniktekine; diğerleri `Revoked` olur (satır
geçmiş olarak kalır), sonra tüm bağlar kanonik kimliğe taşınır; birden çok onaylı birincil kalırsa en erken bağlanan birincil
kalır (V-C). **`known_students`** (benzersiz): kanonik satır varsa kaynak silinir, yoksa taşınır. **`child_progress_snapshots`**
(benzersiz): kanonikte yoksa taşınır, varsa `ChildProgressSnapshot.Absorb` ile sayaçlar/tutarlar toplanır, son tarihler en yenisi
olur, kaynak silinir. Bekleyen veli davetleri Students'ta, birleşmeyle aynı transaction'da kanonik profile taşınır.

> Projeksiyon idempotency'si artık paylaşılan `IdempotentIntegrationEventHandler` tabanı + `inbox_messages` tablosu
> (composite PK `(EventId, Handler)`) ile sağlanır; eski `ProcessedIntegrationEvent` entity'si ve
> `processed_integration_events` tablosu **kaldırıldı** (2026-08-26). (Not: Notifications modülünün kendi
> `processed_integration_events` tablosu — yalnızca haftalık özet dedup'ı için — ayrı bir mekanizmadır ve korunmuştur;
> Parents bunu paylaşmaz.)

**Integration event handler'ları (Parents tüketir):** LessonSessions (`LessonSessionCreated`/`Completed`),
Assignments (`AssignmentCreated`/`Completed`), Payments (`PaymentRecordCreated`/`Updated`), Students (`StudentProfileCreated`,
`StudentProfileLinkedToUser` — P15 T4, `StudentProfilesMerged` — P15 T3).

### 2.4 Domain Olayları (özet)

| Olay | Tetikleyen | Tüketen |
|------|-----------|---------|
| `ParentProfileCreatedDomainEvent` | `ParentProfile` ctor | — |
| `ParentChildLinkRequestedDomainEvent` | bağ talebi | M11 (öğrenci/öğretmene onay bildirimi) |
| `ParentChildLinkApprovedDomainEvent` | `Approve` | **M03 Students** (`StudentProfile.LinkParent` → `ParentUserId` birincil veli göstergesi; **yetki vermez**), M11 |
| `ParentChildLinkEndedDomainEvent` | `Reject` / `Revoke` (yalnız yaşayan bağ) | **M03 Students** (`StudentProfile.UnlinkParent` — alan o veliyi gösteriyorsa temizlenir; P15 T4 / N-26) |

---

## 3. API Sözleşmesi (✅ Uygulandı) — `/api/parents`

> Tümü auth (`AuthenticatedUser`) gerektirir. Veli **yalnızca onaylı bağlı (`Approved`)** çocuklarının verisini görür ve
> yalnızca **görüntüleme** yetkisine sahiptir (salt-okunur — İş Kuralları 4.4).

### 3.1 Profil & bağlama
```
POST /api/parents/profiles
     body: { userId, fullName, contactPhone?, contactEmail? }     → profil (idempotent; userId gerçek Parent kullanıcısı)
GET  /api/parents/profiles/{userId}                                → 200 profil + tercihler
PUT  /api/parents/{parentUserId}/notification-preferences
     body: { missedAssignment, weeklyProgressSummary, lessonReminders,
             testResults, payments, channel }                      → 200 profil
PUT  /api/parents/{parentUserId}/membership-tier                   → 200 profil (Admin; Veli V-E)
     body: { tier: "Free" | "Premium" }

POST /api/parents/children/link
     body: { parentUserId, studentId, relationship?, childDisplayName?,
             inviteCode?, isPrimaryContact }                       → link (Pending)
POST /api/parents/children/claim-invite                            → link (Approved; Veli V-D — öğretmen davet kodu claim)
     body: { inviteCode }                                             (currentUser = veli; bkz. 4.2.2)
                                                                     409 parents.primary_exists — eşzamanlı başka veli birincil
                                                                     oldu; kod geri verilir, tekrar denemede ikincil bağlanır
POST /api/parents/children/{linkId}/approve                        → 200 (öğrenci/Admin; veli kendi bağını onaylayamaz)
                                                                     409 parents.invalid_link_transition (bağ Pending değil)
                                                                     409 parents.primary_exists (onaylı birincil zaten var)
POST /api/parents/children/{linkId}/reject                         → 200
POST /api/parents/children/{linkId}/revoke                         → 200 (veli/öğrenci/Admin; erişim anında kesilir — 4.2)
GET  /api/parents/{parentUserId}/children                          → 200 bağlı çocuklar (durum + onaylıysa gelişim özeti)
```

### 3.2 Birleşik veli paneli (read-model — çocuk başına)
```
GET /api/parents/{parentUserId}/children/{studentId}/dashboard
    → 200 birleşik panel { study, lessons, assignments, payments }
      yalnız Approved bağda; değilse 403.
```

> Yanıt read-model tablolarından (`ChildProgressSnapshot`) toplanır; kaynak modüllerden veri **doğrudan DB ile değil**,
> integration event ile beslenir (bkz. `00_genel_bakis.md` modül sınırı kuralı). Dashboard **yalnız `Approved` bağda** döner;
> aksi halde `403`.
>
> **Zenginleştirilmiş panel (Veli V-F, 2026-07-19 — uygulandı):** Dashboard artık **canlı `Shared.Contracts` digest** arayüzlerinden beslenir (read-model snapshot yerine):
> - **Çalışma verisi** `IStudyDigestDirectory.GetWeeklyDigestAsync` (Study) — son 7 gün toplam dk + streak + **ders bazlı dağılım** (`SubjectBreakdown`). Bu, `ChildProgressSnapshot.WeeklyStudyMinutes`/`StudyStreakDays` alanlarının **hiç yazılmadığı** bug'ını (panelde çalışma hep 0) giderir → o iki alan artık **kullanımdan kalktı**. Gizlilik kapalıysa digest **hiç çağrılmaz** (V-B; 0/boş döner).
> - **Yaklaşan dersler** `IStudentUpcomingLessonsDirectory` (Scheduling — Planned, gelecekteki ilk N) → `UpcomingLessons`.
> - **Son ders özeti** `IStudentLastLessonDirectory` (LessonSessions — son Completed; konu başlığı) → `LastLesson`. `TeacherNotes` bu özette **null** (veli-görünürlük garantisi yok; notlar aşağıdaki filtreli kanaldan gelir).
> - **Öğretmen notları** `IStudentNotesDirectory` (Assignments/M06 `LessonNote`) — yalnız `Visibility ∈ {Student, StudentAndParent}`; **`Private` asla** → `TeacherNotes`.
> - **Ödeme detay listesi** `IStudentPaymentDigestDirectory` (Payments) — kalem düzeyi (tutar/vade/durum) → `PaymentLines`.
>
> Yanıt: `ChildDashboardResponse(StudentId, ChildDisplayName, LinkStatus, Study{…,SubjectBreakdown}, Lessons, Assignments, Payments, UpcomingLessons[], LastLesson?, TeacherNotes[], PaymentLines[], UpdatedOnUtc)`.

> **Gizlilik filtresi (Veli V-B, 2026-07-19):** Dashboard'un `study` bölümü öğrencinin paylaşım tercihine uyar. Handler,
> `KnownStudent` read-model'i ile `StudentId → UserId` çözer ve `IStudentPrivacyDirectory` (Settings uygular) üzerinden
> `ShareStudyDataWithParent`'ı okur. Paylaşım **kapalıysa** çalışma alanları maskelenir: `WeeklyStudyMinutes=0`,
> `StreakDays=0`, `HasData=false` ve **`IsShared=false`** ("Ayşe bu veriyi paylaşmıyor" — değer sızmadan, şeffaf işaret).
> Ayar kaydı yoksa paylaşım **açık** varsayılır (`IsShared=true` — öğrenci henüz kısıtlamadı). **P15 / N-27 fail-closed:** öğrencinin
> **kullanıcısı** çözülemezse (`KnownStudent` yok ya da `UserId` boş — eşleme henüz gelmedi veya öğrencinin hesabı yok) ayar okunamaz
> ve `Hidden` varsayılır (`IsShared=false`); hesabı olmayan öğrencinin çalışma verisi zaten yoktur. **Değişmez kural:** çocuğun kişisel seans notu hiçbir
> koşulda dashboard'da dönmez (zaten read-model'de yer almaz). `StudySummaryResponse(WeeklyStudyMinutes, StreakDays, HasData, IsShared)`.
> Öğrenci tercihini `PUT /api/settings/users/{userId}/study-sharing` (M15) ile ayarlar.

> **Gizlilik seviyesi matrisi (M15-4, 2026-09-03):** Paylaşım bayrağının yanında `PrivacyLevel` de
> uygulanır — **en kısıtlayıcı kazanır**.
>
> | Seviye | `weeklyStudyMinutes` | `streakDays` | `subjectBreakdown` | `isShared` |
> |--------|----------------------|--------------|--------------------|------------|
> | `Standard` | gerçek | gerçek | gerçek | `true` |
> | `Limited` | gerçek | `0` | boş | `true` |
> | `Hidden` | `0` | `0` | boş | `false` |
>
> `ShareStudyDataWithParent = false` ise seviye ne olursa olsun `Hidden` gibi davranılır.
> Sözleşme: `StudentPrivacy.Level` (`Shared.Contracts.StudyPrivacyLevel`). Seviye
> `PUT /api/settings/users/{userId}` ile ayarlanır. Detay: [`m15_settings.md`](m15_settings.md) §3.2.

---

### 3.1 Dışa açtığı modüller-arası sözleşmeler (Shared.Contracts)

> ⚠️ **`ParentNotificationDirectory` sorgusu SQL'e çevrilebilir olmalıdır (P06 Task 10, 2026-09-04).**
> Sorgu iki varlığı özel bir `record`'a projekte edip **sonra** `x.link.StudentId` ile filtreliyordu;
> EF Core kendi tanımladığımız record'un içini göremediği için gerçek Postgres'te
> `InvalidOperationException: The LINQ expression ... could not be translated` atıyordu. Sonuç ağırdı:
> veli bildirimi arayan **her** outbox mesajı (ders tamamlama → otomatik ücret, ödev oluşturma) kalıcı
> olarak başarısızdı — yani **otomatik ders ücreti ve tüm bildirim zinciri canlıda hiç çalışmıyordu**.
> Düzeltme: filtre **projeksiyondan önce** uygulanır, projeksiyon anonim tipe yapılır.
> Regresyon: `RealParentNotificationDirectoryTests`.

| Sözleşme | Tüketen | Amaç |
|----------|---------|------|
| `IParentAccessDirectory.IsApprovedParentOfStudentAsync` | Payments, Students (profil okuma — P15 T4), ProgressTracking, Reporting, Messaging | Veli bir öğrencinin **onaylı** velisi mi? |
| `IParentAccessDirectory.ListApprovedStudentIdsAsync` | Students | Velinin **onaylı** çocuk kimlikleri (M03-3, 2026-09-04) |
| `IParentChildLinkRequestService.RequestPendingLinkAsync` | Students | Öğretmenin doğrudan veli bağlama talebi (M03-1, 2026-09-04) — `ParentProfile` yoksa `ParentNotRegistered`, açık bağ varsa `AlreadyLinked`, aksi halde `Pending` bağ açılır. İlk onaylı birincil veli yoksa yeni bağ `IsPrimaryContact = true` başlar (V-C tekilliği korunur). |

> **Not (M03-1):** Bu yol, davet kodu akışının (V-D) **yerine geçmez**; ikinci bir giriş kapısıdır. Kod akışında veli kodu
> girerek onayı kendisi verir (`Approved` doğrudan), doğrudan bağlamada ise bağ `Pending` kalır ve
> `POST /children/{linkId}/approve` (öğrenci/öğretmen/Admin) ile onaylanır.

## 4. İş Kuralları

### 4.1 Veli yalnızca gerçek kullanıcı olabilir (PRD kuralı)
- **Öğrenci** platforma manuel (öğretmen tarafından `TeacherManaged`) eklenebilir; ama **VELİ asla manuel/placeholder olamaz** — veli mutlaka **gerçek, kayıtlı bir Identity kullanıcısıdır** (`UserRole.Parent`).
- Dolayısıyla `ParentProfile.UserId` zorunludur (null olamaz) ve veli–çocuk bağı her zaman gerçek bir veli hesabıyla kurulur.

### 4.2 Bağ kurma & onay (KVKK / gizlilik)
- Veli–çocuk bağı **onaya** dayanır: bağ `Pending` doğar, **öğrenci / öğretmen / Admin** onaylar (`Approved`); **veli kendi bağını onaylayamaz**.
- **Reşit olmayan öğrenci:** velinin erişimi velayet gereği varsayılan kabul edilir; yine de bağ kaydı (`Approved`) oluşturulur.
- **Reşit öğrenci:** bağ ve veri paylaşımı **öğrenci onayı** gerektirir; öğrenci dilediğinde bağı reddedebilir/iptal ettirebilir.
- Onaylanan bağ M03'te `StudentProfile.ParentUserId`'yi (birincil veli için) günceller; bağ bitince (ret/iptal) alan o veliyi
  gösteriyorsa temizlenir (`ParentChildLinkEndedDomainEvent`).
- **Erişim bağdan okunur (P15 / N-26):** öğrenci profilini okuma yetkisi `ParentUserId`'ye değil Parents'taki **onaylı bağa**
  (`IParentAccessDirectory.IsApprovedParentOfStudentAsync`, yalnız `Parent` rolünde) bakar — iptal/ret anında kesilir (olayın
  işlenmesini beklemez); birincil olmayan onaylı veli de okuyabilir (`ListStudentsByParent` ile aynı kural). Eskiden alan yalnız
  onayda yazıldığı için bağı iptal edilen veli iletişim/doğum tarihi/hedefleri okumaya devam ediyordu.
- **Yalnız bekleyen bağ onaylanır:** reddedilmiş/iptal edilmiş bağ için veli yeni talep açar (`409 parents.invalid_link_transition`).

### 4.2.1 "Sessizce bağlanma yok" + birincil veli tekilliği (Veli V-C, 2026-07-19 — uygulandı)
- **Şeffaflık olayı:** Bir bağ onaylandığında (`Approve`), `ParentChildLinkApprovedDomainEvent`'e ek olarak
  **`ParentLinkConnectionNoticeDomainEvent`** yayılır. Alıcılar: `StudentId` = çocuk ve (varsa)
  `ExistingPrimaryParentUserId` = mevcut birincil veli — "X hesabı veli olarak bağlandı". Fiili bildirim teslimi
  **V-E bildirim motoruna** aittir (Parents yalnız olayı üretir; olay Outbox'a yazılır).
- **Birincil veli tekilliği:** Bir çocuğun aynı anda **tek birincil velisi** (`IsPrimaryContact=true`) olabilir. İkinci bir bağ
  birincil olacaksa ve zaten onaylı bir birincil veli varsa, **onaylayan kişi mevcut birincil veli değilse** onay reddedilir →
  `parents.primary_exists` (**409**). Kural admin onayında da geçerlidir (veri tutarlılığı — mevcut birincil varken ikinci
  birincil oluşturulamaz). Handler, `ListApprovedLinksForStudentAsync` ile mevcut birincil veliyi çözer ve
  `Approve(approvedByUserId, existingPrimaryParentUserId?, nowUtc)` imzasına geçirir. **P15 / N-32:** bu kontrol eşzamanlı iki
  onayı (ya da iki claim'i) göremez; son söz kısmi benzersiz indekstedir (§2.2) — kaybeden istek `409 parents.primary_exists` alır
  (claim'de davet kodu geri verilir). Gerçek Postgres doğrulaması: `tests/Integration/RealParentAccessRevocationTests.cs`.
- **Öğretmen teyidi** bu dilimde YOK (karar 2026-07-19); doğrulama seviyesi = bildirim şeffaflığı + birincil tekilliği.

### 4.2.2 Öğretmen→veli davet kodu claim (Veli V-D, 2026-07-19 — uygulandı)
- Öğretmen bir öğrenci için veli davet kodu üretir (`POST /api/students/profiles/{studentId}/parent-invite`, M03 `StudentParentInvite`).
- Veli kaydolur ve kodu girer: **`POST /api/parents/children/claim-invite`** (`ClaimParentInviteRequest(InviteCode)`, `currentUser` = veli).
- **P15 / N-20 (2026-11-13) sertleştirme:** kod 8 karakter (`ISecureCodeGenerator`), büyük/küçük harf duyarsız, **7 gün** geçerli; claim **yalnız `Parent` (veya Admin) rolü** (`403 parents.claim_requires_parent_role`); daveti üreten kendi kodunu kullanamaz (`403 parents.cannot_claim_own_invite`); süresi dolmuş kod `410 parents.invite_expired`; bulunamayan/süresi dolmuş her deneme kullanıcı başına sayılır, **saatte 10** başarısız denemeden sonra o pencere boyunca doğru kod da `429 shared.too_many_attempts` (`IInviteClaimAttemptLimiter`, Redis, fail-open). Gerçek Postgres+Redis doğrulaması: `tests/Integration/RealInviteClaimTests.cs`.
  Handler (`ClaimParentInviteCommandHandler`) `IParentInviteDirectory.ResolveAsync` ile kodu çözer; kod geçersiz/kullanılmışsa
  `parents.invite_not_found` (**404**). Zaten aktif bağ varsa `parents.link_exists` (409).
- **Onay modeli:** öğretmenin kod üretmesi = öğretmen onayı, velinin kodu girmesi = veli onayı → bağ doğrudan **`Approved`** oluşturulur
  (`ParentChildLink.Approve` + şeffaflık olayı). İlk veli **birincil** (`IsPrimaryContact=true`), sonraki veli birincil olmaz (V-C tekilliği).
  Claim sonrası davet `Claimed` işaretlenir (`MarkClaimedAsync`); mevcut `ParentChildLinkApprovedIntegrationEventHandler` (Students)
  `StudentProfile.ParentUserId`'yi back-fill eder. **Karar (2026-07-19):** telefon eşleştirme YOK; davet-kodu modeli.

### 4.3 İki veri kaynağı (PRD §M09)
| Kaynak | İçerik | Önkoşul |
|--------|--------|---------|
| **Bireysel çalışma (M08)** | Haftalık süre, konu dağılımı, test performansı, streak | Öğretmen gerekmez |
| **Öğretmen bağlıysa** | Son ders, ödevler, öğretmen notları, ödeme özeti | Öğrenci bir öğretmene bağlı + `IsSharedWithParent` |

### 4.4 Salt-okunur yetki
- Veli **yalnızca görüntüleme** yapar; ders/ödev/ödeme/çalışma verisini **düzenleyemez/silemez**.
- Veli yalnızca **onaylı (`Approved`)** bağlı çocuklarını görür; `Pending`/`Rejected`/`Revoked` bağlarda veri dönmez.

### 4.5 Gizlilik filtreleme
- Her veri kalemi, M08/M05/M06 tarafındaki `IsSharedWithParent` bayrağına göre filtrelenir; öğrenci paylaşmadığı veriyi veli **göremez** (bkz. `m08_study.md` 4.5, `m15_settings.md`).

### 4.6 Ödev kaçırma bildirimi (M11)
- Çocuğun bir ödevi son teslim tarihini geçtiğinde (M06), velinin `MissedAssignmentAlerts` tercihi açıksa M11 üzerinden bildirim gönderilir.

---

## 5. Olay Akışı

### 5.1 Bağ kurma
```
[Veli kayıt olur (Parent rolü)] → POST /parents/profiles
[Çocuğa bağlanma] → POST /parents/children/link (studentId | inviteCode)
     → ParentChildLink(Pending) + ParentChildLinkRequestedDomainEvent → M11 (öğrenci/öğretmene onay isteği)
[Onay] → /approve → ParentChildLink(Approved) + ParentChildLinkApprovedDomainEvent
     → M03 Students handler: StudentProfile.LinkParent → ParentUserId güncelle → M11 (veliye "bağ onaylandı")
[Ret / iptal] → /reject | /revoke → ParentChildLink(Rejected | Revoked) + ParentChildLinkEndedDomainEvent (yalnız yaşayan bağ)
     → profil okuma ANINDA 403 (yetki onaylı bağdan okunur)
     → M03 Students handler: StudentProfile.UnlinkParent → ParentUserId (o veliyse) temizlenir (P15 T4)
```

### 5.2 Veli paneli beslenmesi (read-model)
```
M08 StudySessionCompleted / TestResultRecorded ─┐
M05 LessonSessionCompleted ─────────────────────┤  (Integration Event)
M06 AssignmentDue/Missed ───────────────────────┤ ──► Parents read-model günceller
M07 PaymentRecorded ────────────────────────────┘      → /children/{id}/dashboard güncel veri döner
M03 StudentProfilesMerged ──────────────────────────► bağlar + known_students + özet kanonik StudentId'ye (P15 T3)
```

### 5.3 Ödev kaçırma
```
M06 [ödev teslim tarihi geçti] → AssignmentMissed event
   → (veli bağlı + MissedAssignmentAlerts açık) → M11 Notifications → veliye push/email
```

---

## 6. Mobil Ekranlar (✅ Uygulandı — Flutter `parent` feature)

> Birincil renk `0xFF082B4F`. `Parent` rolü için **ayrı navigasyon uygulandı**: `app_router.dart` redirect'i
> `session.roles` içinde `'Parent'` varsa `/parent`'e yönlendirir (veli öğretmen ekranlarına, öğretmen veli ekranlarına düşerse geri alınır).
> Feature klasörü: `mobile/lib/features/parent/`. Cubit: `ParentCubit`. Repository: `ParentRepository` (get_it'e kayıtlı, mock fallback destekli).
> Alt menü: **`ParentBottomNav`** (Ana Sayfa / Çocuklar / Bildirim / Profil).

- `parent_home_page` — **çocuk seçici** + haftalık KPI kartları + haftalık çalışma çubuk grafiği + ödeme özeti.
- `parent_children_page` — bağlı çocuklar + durum rozetleri + "çocuk bağla" bottom-sheet formu.
- `parent_child_detail_page` — seçili çocuğun detaylı gelişimi (çalışma / ders / ödev / ödeme).
- `parent_notifications_page` — bildirim tercihleri switch'leri + kanal seçimi.
- `parent_profile_page` — profil + çıkış.

> `role_selection_page` 'Veli' kartı artık `/register?role=veli`'ye gider (eski "yakında" snackbar kaldırıldı).
> M08 Study → Parents beslemesi bağlanınca haftalık çalışma dk + streak alanları gerçek veriyle dolacak.

---

## 7. Kabul Kriterleri

### Faz 2 (öğretmensiz)
- [x] Veli **gerçek kullanıcı** olarak kaydolup çocuğuna **onaylı** bağlanabilir.
- [~] Veli, çocuğunun **bireysel çalışma** verilerini (süre, ders dağılımı, test, streak) görebilir — read-model + panel hazır; **M08 verisi beklen(iy)or**.
- [x] Çoklu çocuk desteği: bir veli birden çok çocuğu yönetebilir (çocuk seçici).
- [x] Bildirim tercihleri ayarlanabilir; **salt-okunur** panel + yalnız `Approved` bağ görünürlüğü uygulanır.

### Faz 3 (öğretmen verisi entegre)
- [x] Öğretmen bağlıysa veli; **son ders özeti, ödevler, öğretmen notları, ödeme özeti** görebilir — `GET /{parentUserId}/children/{studentId}/dashboard` → `ChildDashboardResponse` (`LastLesson`, `Assignments`, `TeacherNotes`, `Payments`/`PaymentLines`).
- [~] **Ödev kaçırma** bildirimi (M11) — tercih alanı (`NotifyMissedAssignment`) ve tercihe saygı duyan üretim var, ancak tetikleyici bugün `AssignmentCreatedDomainEvent`; **son teslim yaklaştı/kaçırıldı** olayı henüz yok **(P03)**.
- [ ] Gelişim grafik + rapor (M10/M14) veli panelinde görüntülenir — mobil model (`ChildProgressSummary`) hazır ama `ChildDashboardResponse` henüz `progress` alanı döndürmüyor **(P08)**.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Kalan önkoşul:** Veli panelinde bireysel çalışma verisi görünmesi için **M08 (Study) tamamlanmalı**
> (bkz. [`m08_study.md`](m08_study.md)); read-model kolonları hazır ama besleyici event henüz yok.

1. ⏳ **M08 Study → Parents beslemesi** — bireysel çalışma dk + streak read-model kolonlarını integration event ile doldur (henüz bağlı değil).
2. ✅ **Parents domain'i** — `ParentProfile`, `ParentChildLink` + enum + olaylar.
3. ✅ **Application (CQRS)** — profil/link komutları, onay akışı, dashboard sorguları; read-model.
4. ✅ **Infrastructure** — `ParentsDbContext` `DbSet`'leri, **`parents` şeması `InitialCreate` migration'ı**, integration event handler'ları.
5. ✅ **Veli–çocuk bağlama + onay** akışı (öğrenci/öğretmen/Admin onayı; veli kendi bağını onaylayamaz; KVKK).
6. ✅ **Birleşik dashboard read-model'i** — LessonSessions/Assignments/Payments verisini event ile toplama (doğrudan DB yok); M08 bekliyor.
7. 🟢 **Gizlilik filtreleme** — `ShareStudyDataWithParent` + `PrivacyLevel` matrisi uygulanıyor (P05/M15-4). Kalan: M05/M06/M08 tarafındaki kaynak-modül `IsSharedWithParent` bayrakları.
8. ⏳ **M11 entegrasyonu** — ödev kaçırma, haftalık özet, ders hatırlatma bildirimleri (tercih alanları hazır).
9. ✅ **Mobil `parent` feature** + `Parent` rolü navigasyonu (`/parent`, `ParentBottomNav`, redirect).

---

## 9. İlişkili Dokümanlar

- Veli rolü (bu modülün kullanıcısı) → [`../roles/veli.md`](../roles/veli.md)
- Öğrenci rolü (verinin sahibi, paylaşımı kontrol eder) → [`../roles/ogrenci.md`](../roles/ogrenci.md)
- Öğretmen rolü (ders/ödev/ödeme verisinin kaynağı) → [`../roles/ogretmen.md`](../roles/ogretmen.md)
- Bireysel çalışma verisi (birincil kaynak, önkoşul) → [`m08_study.md`](m08_study.md)
- Öğrenci profili / `ParentUserId` bağı → [`m03_students.md`](m03_students.md)
- Yaklaşan ders / hatırlatma → [`m04_scheduling.md`](m04_scheduling.md)
- Ders oturumu özeti → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Ödev durumu / kaçırma → [`m06_assignments.md`](m06_assignments.md)
- Ödeme özeti → [`m07_payments.md`](m07_payments.md)
- Gelişim takibi (veli grafik kaynağı) → [`m10_progress_tracking.md`](m10_progress_tracking.md)
- Bildirimler → [`m11_notifications.md`](m11_notifications.md)
- Raporlama → [`m14_reporting.md`](m14_reporting.md)
- Bildirim tercihleri / gizlilik / KVKK → [`m15_settings.md`](m15_settings.md)
- Veri modeli → [`veri_modeli.md`](veri_modeli.md)
- Genel durum tablosu → [`00_genel_bakis.md`](00_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*M09 Veli (Parents) Modülü — Detaylı Tasarım | Faz 2-3 | Durum: 🟢 Uygulandı | Güncelleme: 2026-11-15 (P15 Task 4 / N-26, N-27, N-32: `Approve` yalnız `Pending`'den → `Result` + `409 parents.invalid_link_transition`; ret/iptal tek `ParentChildLinkEndedDomainEvent` yayar, Students `ParentUserId`'yi temizler; profil okuma onaylı bağdan; `KnownStudent` `StudentProfileLinkedToUser` ile de beslenir; panel gizliliği kullanıcı bilinmiyorsa fail-closed; kısmi benzersiz birincil veli indeksi + `ParentLinkIntegrity` göçü; §2.2, §2.3, §2.4, §3.1, §3.2, §4.2, §4.2.1, §5.1). Önceki not — 2026-11-14 (P15 Task 3 / N-28: yeni tüketici `StudentProfilesMergedParentsHandler` — profil birleşmesinde veli bağları tekilleşip kanonik kimliğe taşınır, `known_students` kaynağı silinir, veli paneli özeti `ChildProgressSnapshot.Absorb` ile toplanır; §2.3, §5.2). Önceki not — 2026-11-13 (P15 Task 1 / N-20: veli davet kodu claim'i sertleştirildi — güvenli 8 karakterli kod, 7 gün, `Parent` rolü, kendi kodunu kullanma yasağı, kullanıcı başına deneme kilidi; §4.2.2). Önceki not — 2026-09-04 (P09 Task 2 / K-09: `ParentNotificationDirectory` üyelik seviyesini artık `ParentProfile.MembershipTier` alanından değil `IEntitlementDirectory.GetManyAsync` ile Membership'ten okuyor (toplu tarama tek turda); eski alan **geçici köprüye** indirildi — yeni `ParentMembershipDirectory : IMembershipDirectory`, tek tüketicisi `EntitlementDirectory`, P12'de silinecek). Önceki not — 2026-09-04 (P06 Task 10 canlı doğrulama: **`ParentNotificationDirectory` sorgusu gerçek Postgres'te çevrilemiyordu** — tüm outbox zinciri ölüydü; filtre projeksiyondan öne alındı) · 2026-09-04 (P06 Task 9 / M03-1 + M03-3: iki modüller-arası sözleşme — `IParentChildLinkRequestService` (yeni, Pending bağ açar) ve `IParentAccessDirectory.ListApprovedStudentIdsAsync`; bkz. §3.1) · 2026-09-03 (P05/M15-4: gizlilik seviyesi matrisi dashboard'a bağlandı — Standard/Limited/Hidden) · 2026-09-02 (F-06: Faz 3 kabul kriterleri kodla karşılaştırıldı — dashboard ✅, ödev-kaçırma ~ (P03), gelişim ⬜ (P08)). Önceki not — 2026-08-26 (idempotency doküman drift'i giderildi: eski `ProcessedIntegrationEvent`/`processed_integration_events` kaldırıldı, yerine paylaşılan `IdempotentIntegrationEventHandler` + `inbox_messages`; Notifications'ın kendi `processed_integration_events`'i ayrı ve korunmuştur). Önceki not — 2026-08-19 kod-senkron: API 11 endpoint doğrulandı — §3.1'e `POST /children/claim-invite` eklendi. Daha önceki not — Veli V-F: entegre dashboard zenginleştirme — canlı digest'ler `IStudyDigestDirectory`/`IStudentUpcomingLessonsDirectory`/`IStudentLastLessonDirectory`/`IStudentNotesDirectory`/`IStudentPaymentDigestDirectory`; çalışma "hep 0" bug fix; öğretmen notları Student+StudentAndParent; Veli V-E: `ParentProfile.MembershipTier` Free/Premium + `PUT /membership-tier` (Admin) + `IParentNotificationDirectory`; bildirim tercihleri M11 motorunca fiilen tüketiliyor; Veli V-D: öğretmen→veli davet kodu claim `POST /children/claim-invite` → Approved bağ; Veli V-C: "sessizce bağlanma yok" — `ParentLinkConnectionNoticeDomainEvent` + birincil veli tekilliği `parents.primary_exists` 409; Veli V-B: dashboard gizlilik filtresi — `ShareStudyDataWithParent` → çalışma alanları maskelenir + `StudySummaryResponse.IsShared`; `IStudentPrivacyDirectory` kontratı)*

*Güncelleme: 2026-11-15 — P18 Task 1: xmin iyimser eşzamanlılık koruması ve 409 sözleşmesi.*

**Outbox teslimi (P19 T2 / N-42):** Read-model ve profil birleşme tüketicilerinde null yük açık poison hatasıdır; öğrenci–kullanıcı eşlemesinin zaten güncel olması başarılı etkisiz sonuç olarak inbox'a yazılır. Ortak teslim, retry ve hata yalıtımı sözleşmesi: [Backend §7](../architecture/backend.md#7-outbox--event-akışı).

*Güncelleme: 2026-11-15 — P19 Task 2: açık olay tüketim sonuçları ve inbox güvenliği.*


### P19 Task 6 — Haftalık hedeflerin sayfalı okunması (N-53)

`IParentNotificationDirectory.ListApprovedTargetPageAsync(afterLinkId, take, ct)` onaylı veli–öğrenci bağlarını LinkId keyset ile en fazla 200'lük sayfada okur; üyelik `IEntitlementDirectory.GetManyAsync` ile o sayfa için topludur. Sonraki imleç ham son bağ kimliğidir; filtrelenen Free/tercih kapalı bağlar sayfa ilerlemesini bozmaz. Mevcut tek öğrenci ve tam liste okumaları korunur. M11 haftalık özeti her veli–çocuk–yerel hafta için ayrı üretir (205 çocuk/iki sayfa/tekrar gerçek PostgreSQL kanıtı).
