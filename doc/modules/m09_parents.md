---
title: "M09 — Veli Paneli (Parents)"
summary: "Veli paneli read-model modülü olarak uygulanmış; çocuğun bireysel çalışma + ders/ödev/ödeme verisini birleştirir, M08 haftalık dk/streak beslemesi henüz bağlanmadı"
tags: [modul, parents, veli-paneli, read-model, faz-2, faz-3]
status: "🟢"
authority: code
code_refs:
  - src/Modules/Parents/**
updated: 2026-08-19
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
  haftalık çalışma dk + streak [M08 gelince]), `KnownStudent` (StudentId→UserId eşlemesi, bağ onay yetkisi için),
  `ProcessedIntegrationEvent` (idempotency / çift-sayım koruması).
- **Mobil veli feature** (`mobile/lib/features/parent/`) ve `Parent` rolü için **rol bazlı navigasyon** kodda (bkz. Bölüm 6).

### 🟢 Bağ noktaları (başka modüllerde)
- `Identity` rolü: `UserRole.Parent = 4` (veli, **gerçek kayıtlı kullanıcı** olmalı — bkz. İş Kuralları 4.1). Register/Login `roleId 4` taşır.
- M03 Students: `StudentProfile.ParentUserId` (`Guid?`) alanı mevcut. **Students modülü artık `ParentChildLinkApprovedDomainEvent`'i
  tüketip** (yeni handler + `StudentProfile.LinkParent` metodu) onaylı bağda `ParentUserId`'yi set eder.

### ⏳ M08 Study beslemesine bağlı
- Haftalık çalışma dk + streak kolonları read-model'de (`ChildProgressSnapshot`) hazır; **M08 Study → Parents** integration event beslemesi henüz bağlanmadı.

---

## 2. Domain Modeli (✅ Uygulandı)

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
| `MembershipTier` | `MembershipTier` (Shared.Contracts) | ✓ | **Free/Premium (Veli V-E).** Veli bildirimleri yalnız `Premium`'a gider (M11 `ParentEventNotificationHandler` Premium kapısı). Varsayılan `Free`; Admin `PUT /membership-tier` ile set eder (satın alma altyapısı sonraki faz). |
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

**Davranışlar:** ctor `Pending` doğurur; `Approve(byUserId, nowUtc)` → `Approved` + `LinkedOnUtc`;
`Reject(...)`; `Revoke(...)`. Olaylar: `ParentChildLinkRequestedDomainEvent`, `ParentChildLinkApprovedDomainEvent`,
`ParentChildLinkRejectedDomainEvent`, `ParentChildLinkRevokedDomainEvent`. Bir veli **birden çok** çocuğa bağlanabilir (çoklu link).

### 2.3 Read-model tabloları (`parents` şeması — integration event ile beslenir)

Veli paneli, kaynak modüllerin verisini **doğrudan cross-module DB erişimiyle değil**, integration event ile beslenen
`parents` şeması read-model tablolarından okur (modül sınırı kuralı).

| Tablo | İçerik |
|-------|--------|
| `ChildProgressSnapshot` | Öğrenci başına: tamamlanan/planlanan ders sayısı + son ders tarihi; toplam/açık/tamamlanan ödev; beklenen/tahsil/kalan ödeme + para birimi; haftalık çalışma dk + streak (**M08 gelince**) |
| `KnownStudent` | `StudentId → UserId` eşlemesi (Students `StudentProfileCreated` ile beslenir); bağ onay yetkisi kontrolü için |
| `ProcessedIntegrationEvent` | İşlenmiş event kimlikleri — idempotency / çift-sayım koruması |

**Integration event handler'ları (Parents tüketir):** LessonSessions (`LessonSessionCreated`/`Completed`),
Assignments (`AssignmentCreated`/`Completed`), Payments (`PaymentRecordCreated`/`Updated`), Students (`StudentProfileCreated`).

### 2.4 Domain Olayları (özet)

| Olay | Tetikleyen | Tüketen |
|------|-----------|---------|
| `ParentProfileCreatedDomainEvent` | `ParentProfile` ctor | — |
| `ParentChildLinkRequestedDomainEvent` | bağ talebi | M11 (öğrenci/öğretmene onay bildirimi) |
| `ParentChildLinkApprovedDomainEvent` | `Approve` | **M03 Students** (`StudentProfile.LinkParent` → `ParentUserId` set), M11 |
| `ParentChildLinkRejectedDomainEvent` | `Reject` | M11 |
| `ParentChildLinkRevokedDomainEvent` | `Revoke` | M03, M11 |

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
POST /api/parents/children/{linkId}/approve                        → 200 (öğrenci/öğretmen/Admin; veli kendi bağını onaylayamaz)
POST /api/parents/children/{linkId}/reject                         → 200
POST /api/parents/children/{linkId}/revoke                         → 200
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
> Ayar kaydı yoksa paylaşım **açık** varsayılır (`IsShared=true`). **Değişmez kural:** çocuğun kişisel seans notu hiçbir
> koşulda dashboard'da dönmez (zaten read-model'de yer almaz). `StudySummaryResponse(WeeklyStudyMinutes, StreakDays, HasData, IsShared)`.
> Öğrenci tercihini `PUT /api/settings/users/{userId}/study-sharing` (M15) ile ayarlar.

---

## 4. İş Kuralları

### 4.1 Veli yalnızca gerçek kullanıcı olabilir (PRD kuralı)
- **Öğrenci** platforma manuel (öğretmen tarafından `TeacherManaged`) eklenebilir; ama **VELİ asla manuel/placeholder olamaz** — veli mutlaka **gerçek, kayıtlı bir Identity kullanıcısıdır** (`UserRole.Parent`).
- Dolayısıyla `ParentProfile.UserId` zorunludur (null olamaz) ve veli–çocuk bağı her zaman gerçek bir veli hesabıyla kurulur.

### 4.2 Bağ kurma & onay (KVKK / gizlilik)
- Veli–çocuk bağı **onaya** dayanır: bağ `Pending` doğar, **öğrenci / öğretmen / Admin** onaylar (`Approved`); **veli kendi bağını onaylayamaz**.
- **Reşit olmayan öğrenci:** velinin erişimi velayet gereği varsayılan kabul edilir; yine de bağ kaydı (`Approved`) oluşturulur.
- **Reşit öğrenci:** bağ ve veri paylaşımı **öğrenci onayı** gerektirir; öğrenci dilediğinde bağı reddedebilir/iptal ettirebilir.
- Onaylanan bağ M03'te `StudentProfile.ParentUserId`'yi (birincil veli için) güncelleyebilir.

### 4.2.1 "Sessizce bağlanma yok" + birincil veli tekilliği (Veli V-C, 2026-07-19 — uygulandı)
- **Şeffaflık olayı:** Bir bağ onaylandığında (`Approve`), `ParentChildLinkApprovedDomainEvent`'e ek olarak
  **`ParentLinkConnectionNoticeDomainEvent`** yayılır. Alıcılar: `StudentId` = çocuk ve (varsa)
  `ExistingPrimaryParentUserId` = mevcut birincil veli — "X hesabı veli olarak bağlandı". Fiili bildirim teslimi
  **V-E bildirim motoruna** aittir (Parents yalnız olayı üretir; olay Outbox'a yazılır).
- **Birincil veli tekilliği:** Bir çocuğun aynı anda **tek birincil velisi** (`IsPrimaryContact=true`) olabilir. İkinci bir bağ
  birincil olacaksa ve zaten onaylı bir birincil veli varsa, **onaylayan kişi mevcut birincil veli değilse** onay reddedilir →
  `parents.primary_exists` (**409**). Kural admin onayında da geçerlidir (veri tutarlılığı — mevcut birincil varken ikinci
  birincil oluşturulamaz). Handler, `ListApprovedLinksForStudentAsync` ile mevcut birincil veliyi çözer ve
  `Approve(approvedByUserId, existingPrimaryParentUserId?, nowUtc)` imzasına geçirir.
- **Öğretmen teyidi** bu dilimde YOK (karar 2026-07-19); doğrulama seviyesi = bildirim şeffaflığı + birincil tekilliği.

### 4.2.2 Öğretmen→veli davet kodu claim (Veli V-D, 2026-07-19 — uygulandı)
- Öğretmen bir öğrenci için veli davet kodu üretir (`POST /api/students/profiles/{studentId}/parent-invite`, M03 `StudentParentInvite`).
- Veli kaydolur ve kodu girer: **`POST /api/parents/children/claim-invite`** (`ClaimParentInviteRequest(InviteCode)`, `currentUser` = veli).
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
```

### 5.2 Veli paneli beslenmesi (read-model)
```
M08 StudySessionCompleted / TestResultRecorded ─┐
M05 LessonSessionCompleted ─────────────────────┤  (Integration Event)
M06 AssignmentDue/Missed ───────────────────────┤ ──► Parents read-model günceller
M07 PaymentRecorded ────────────────────────────┘      → /children/{id}/dashboard güncel veri döner
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
- [ ] Öğretmen bağlıysa veli; **son ders özeti, ödevler, öğretmen notları, ödeme özeti** görebilir.
- [ ] **Ödev kaçırma** bildirimi (M11) tercihe bağlı çalışır.
- [ ] Gelişim grafik + rapor (M10/M14) veli panelinde görüntülenir.

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
7. ⏳ **Gizlilik filtreleme** — `IsSharedWithParent` bayraklarına göre veri kısıtlama (M15 ile) — kaynak modül bayrakları geldikçe.
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

*M09 Veli (Parents) Modülü — Detaylı Tasarım | Faz 2-3 | Durum: 🟢 Uygulandı | Güncelleme: 2026-08-19 (kod-senkron: API 11 endpoint doğrulandı — §3.1'e `POST /children/claim-invite` eklendi). Önceki not — Veli V-F: entegre dashboard zenginleştirme — canlı digest'ler `IStudyDigestDirectory`/`IStudentUpcomingLessonsDirectory`/`IStudentLastLessonDirectory`/`IStudentNotesDirectory`/`IStudentPaymentDigestDirectory`; çalışma "hep 0" bug fix; öğretmen notları Student+StudentAndParent; Veli V-E: `ParentProfile.MembershipTier` Free/Premium + `PUT /membership-tier` (Admin) + `IParentNotificationDirectory`; bildirim tercihleri M11 motorunca fiilen tüketiliyor; Veli V-D: öğretmen→veli davet kodu claim `POST /children/claim-invite` → Approved bağ; Veli V-C: "sessizce bağlanma yok" — `ParentLinkConnectionNoticeDomainEvent` + birincil veli tekilliği `parents.primary_exists` 409; Veli V-B: dashboard gizlilik filtresi — `ShareStudyDataWithParent` → çalışma alanları maskelenir + `StudySummaryResponse.IsShared`; `IStudentPrivacyDirectory` kontratı)*
