# 👪 M09 — Veli (Parents) Modülü — Detaylı Tasarım Dokümanı

> **Kod Modülü:** `src/Modules/Parents` · **Route Prefix:** `/api/parents` · **Şema:** `parents`
> **PRD Modülü:** M09 Veli Paneli · **Faz:** 2-3 (Faz 2: bireysel çalışma görünümü; Faz 3: öğretmen verisi)
> **Durum:** 🔴 **İskelet** (yalnızca `ParentsDbContext` + DI + `GET /api/parents/status`)
> **Platform:** EğitimÜssü (EgitimUssu) — .NET 9 modüler monolit · PostgreSQL · Redis · Flutter
>
> **Amaç:** Veli, çocuğunun gelişimini **şeffaf** şekilde izlesin. Veli paneli **iki kaynaktan** beslenir:
> (1) çocuğun **bireysel çalışması** (öğretmen gerekmez — M08), (2) öğretmen bağlıysa **ders/ödev/ödeme**
> verisi (M05/M06/M07). Bu modül büyük ölçüde bir **read-model** modülüdür: kendi domain verisi azdır;
> diğer modüllerin verisini veli perspektifinden **okur/birleştirir**.

> ⚠️ Bu dokümandaki Domain/API/İş Kuralları/Olay Akışı bölümleri **henüz kodda yoktur**; PRD §M09 ve
> mevcut modül desenlerine göre **önerilen tasarımdır**. Bölüm 1 koddan doğrulanmıştır.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Var olan (iskelet)
- `ParentsModule : ModuleDefinition` — `Name = "Parents"`, `RoutePrefix = "/api/parents"`, tek endpoint:
  `GET /api/parents/status` → `placeholder` (`src/Modules/Parents/API/ParentsModule.cs`).
- `ParentsDbContext : ModuleDbContext` — `SchemaName = "parents"`; henüz **hiç `DbSet` yok**
  (`src/Modules/Parents/Infrastructure/ParentsDbContext.cs`).
- `AddParentsModule(...)` DI kaydı (`src/Modules/Parents/Infrastructure/DependencyInjection.cs`).

### 🟢 Hazır bağ noktaları (başka modüllerde)
- `Identity` rolü: `UserRole.Parent = 4` (veli, **gerçek kayıtlı kullanıcı** olmalı — bkz. İş Kuralları 4.1).
- M03 Students: `StudentProfile.ParentUserId` (`Guid?`) alanı mevcut — tekil/basit veli bağı için hazır
  (`src/Modules/Students/Domain/StudentsDomainModel.cs`).

### 🔴 Eksik olan
- **Domain yok** — `ParentProfile`, `ParentChildLink` tanımlı değil.
- **Application (CQRS) yok**, **API yok** (sadece `/status`), **migration yok** (`parents` şemasında tablo yok).
- **Birleşik veli dashboard read-model'i yok** — diğer modüllerden veri toplayan sorgular yok.
- **Mobil veli feature yok** ve `Parent` rolü için navigasyon yok.

---

## 2. Domain Modeli (⚠️ Önerilen)

> `AggregateRoot<Guid>` / `Entity<Guid>` desenini izler (private ctor, `private set`, enum `1`'den, `Raise(...)`).
> Veli modülünün **kendi ürettiği veri azdır**; çoğu veri diğer modüllerden okunur (read-model). Tablolar `parents` şemasında.

### 2.1 `ParentProfile` (AggregateRoot)

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `UserId` | `Guid` | ✓ | **Gerçek** Identity kullanıcısı (`UserRole.Parent`) — null olamaz (İş Kuralları 4.1) |
| `FullName` | `string` | ✓ | Veli adı |
| `ContactPhone` | `string?` | — | |
| `ContactEmail` | `string?` | — | |
| `NotificationPreferences` | `NotificationPreferences` (VO) | ✓ | Bildirim tercihleri (ödev kaçırma, haftalık özet, ders hatırlatma vb.) |
| `IsActive` | `bool` | ✓ | |
| `CreatedOnUtc` | `DateTime` | ✓ | |
| `UpdatedOnUtc` | `DateTime` | ✓ | |

`NotificationPreferences` (value object — öneri):

| Alan | Tip | Açıklama |
|------|-----|----------|
| `MissedAssignmentAlerts` | `bool` | Ödev kaçırma bildirimi (M11) |
| `WeeklyProgressSummary` | `bool` | Haftalık gelişim özeti |
| `LessonReminders` | `bool` | Yaklaşan ders hatırlatması (öğretmen bağlıysa) |
| `TestResultAlerts` | `bool` | Yeni deneme sonucu bildirimi |
| `PaymentReminders` | `bool` | Ödeme hatırlatması (öğretmen bağlıysa) |
| `Channel` | `enum` | `Push`/`Email`/`Both` |

**Davranış:** `UpdateNotificationPreferences(...)`, `UpdateContact(...)`. Olay: `ParentProfileCreatedDomainEvent`.

### 2.2 `ParentChildLink` (AggregateRoot) — Veli–öğrenci bağı (onaylı, çoklu)

`StudentProfile.ParentUserId` tekil/basit senaryoyu karşılar; **çoklu çocuk + onaylı bağ** için ayrı aggregate önerilir.

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|:------:|----------|
| `Id` | `Guid` | ✓ | |
| `ParentUserId` | `Guid` | ✓ | Veli Identity kullanıcısı |
| `StudentId` | `Guid` | ✓ | M03 `StudentProfile.Id` (mantıksal referans) |
| `Status` | `ParentChildLinkStatus` | ✓ | `Pending`/`Approved`/`Rejected`/`Revoked` |
| `Relationship` | `string?` | — | Yakınlık (anne/baba/vasi) |
| `RequestedOnUtc` | `DateTime` | ✓ | Talep anı |
| `LinkedOnUtc` | `DateTime?` | — | Onay anı |
| `ApprovedByUserId` | `Guid?` | — | Onaylayan (öğrenci veya öğretmen) |
| `InviteCode` | `string?` | — | Davet/eşleşme kodu (varsa) |
| `IsPrimaryContact` | `bool` | ✓ | Birincil veli mi |

```csharp
public enum ParentChildLinkStatus { Pending = 1, Approved = 2, Rejected = 3, Revoked = 4 }
```

**Davranışlar:** `RequestLink(...)` → `Pending`; `Approve(byUserId, nowUtc)` → `Approved` + `LinkedOnUtc`;
`Reject(...)`; `Revoke(...)`. Olaylar: `ParentChildLinkRequestedDomainEvent`, `ParentChildLinkApprovedDomainEvent`,
`ParentChildLinkRevokedDomainEvent`. Bir veli **birden çok** çocuğa bağlanabilir (çoklu link).

### 2.3 Read-model projeksiyonları (kendi tablosu yok / opsiyonel cache)

Veli paneli aşağıdaki **özet** verileri diğer modüllerden okur. İsteğe bağlı olarak performans için
`parents` şemasında **denormalize read-model** (örn. `ParentDashboardSnapshot`) tutulabilir; ancak kaynak
modüller her zaman gerçek sahiptir (modül sınırı kuralı — **doğrudan cross-module DB erişimi yok**, integration event / application service ile beslenir).

| Read-model | Kaynak Modül | İçerik |
|------------|--------------|--------|
| Bireysel çalışma özeti | M08 Study | Haftalık süre, ders dağılımı, test performansı, streak (paylaşıma açıksa) |
| Son ders özeti | M05 LessonSessions | Son işlenen ders, konu, öğretmen notu (`IsSharedWithParent`) |
| Ödev durumu | M06 Assignments | Verilen/teslim/kaçırılan ödevler (`IsSharedWithParent`) |
| Ödeme özeti | M07 Payments | Ödenen/bekleyen tutar, son ödeme (öğretmen bağlıysa) |
| Gelişim grafikleri | M10 / M14 | Konu bazlı gelişim, hedef ilerleme, raporlar |

### 2.4 Domain Olayları (özet)

| Olay | Tetikleyen | Tüketen |
|------|-----------|---------|
| `ParentProfileCreatedDomainEvent` | `ParentProfile` ctor | — |
| `ParentChildLinkRequestedDomainEvent` | `RequestLink` | M11 (öğrenci/öğretmene onay bildirimi) |
| `ParentChildLinkApprovedDomainEvent` | `Approve` | M03 (StudentProfile.ParentUserId set), M11 |
| `ParentChildLinkRevokedDomainEvent` | `Revoke` | M03, M11 |

---

## 3. API Sözleşmesi (⚠️ Önerilen) — `/api/parents`

> Tümü auth (`Parent` rolü) gerektirir. Veli **yalnızca onaylı bağlı (`Approved`)** çocuklarının verisini görür ve
> yalnızca **görüntüleme** yetkisine sahiptir (salt-okunur — İş Kuralları 4.4).

### 3.1 Profil & bağlama
```
POST /api/parents/profiles
     body: { userId, fullName, contactPhone?, contactEmail? }     → 201 (userId gerçek Parent kullanıcısı olmalı)
GET  /api/parents/profiles/{userId}                                → 200 profil + tercihler
PUT  /api/parents/{parentUserId}/notification-preferences
     body: NotificationPreferences                                 → 200

POST /api/parents/children/link
     body: { parentUserId, studentId | inviteCode, relationship? } → 201 { linkId, status: Pending }
POST /api/parents/children/{linkId}/approve                        → 200 (öğrenci/öğretmen onayı)
POST /api/parents/children/{linkId}/reject                         → 200
POST /api/parents/children/{linkId}/revoke                         → 200
GET  /api/parents/{parentUserId}/children                          → 200 bağlı çocuk listesi (+ link durumu)
```

### 3.2 Birleşik veli paneli (read-model — çocuk başına)
```
GET /api/parents/children/{studentId}/dashboard
    → 200 { study: haftalık özet+streak, lastLesson?, assignments: {open,missed},
            payment?: özet, progress: özet }                       (paylaşım + bağ durumuna göre filtreli)
GET /api/parents/children/{studentId}/study?from=&to=              → 200 bireysel çalışma (M08'den)
GET /api/parents/children/{studentId}/tests?subject=               → 200 deneme performansı (M08'den)
GET /api/parents/children/{studentId}/lessons                      → 200 ders özeti (M05'ten, öğretmen bağlıysa)
GET /api/parents/children/{studentId}/assignments                  → 200 ödevler (M06'dan)
GET /api/parents/children/{studentId}/payments                     → 200 ödeme özeti (M07'den, öğretmen bağlıysa)
GET /api/parents/children/{studentId}/progress                     → 200 gelişim grafikleri (M10/M14'ten)
```

> Bu endpoint'ler kaynak modüllerden veriyi **doğrudan DB ile değil**, application service / integration event ile toplar
> (bkz. `00_genel_bakis.md` modül sınırı kuralı). Yanıtlar, çocuğun gizlilik bayraklarına (`IsSharedWithParent`) tabidir.

---

## 4. İş Kuralları

### 4.1 Veli yalnızca gerçek kullanıcı olabilir (PRD kuralı)
- **Öğrenci** platforma manuel (öğretmen tarafından `TeacherManaged`) eklenebilir; ama **VELİ asla manuel/placeholder olamaz** — veli mutlaka **gerçek, kayıtlı bir Identity kullanıcısıdır** (`UserRole.Parent`).
- Dolayısıyla `ParentProfile.UserId` zorunludur (null olamaz) ve veli–çocuk bağı her zaman gerçek bir veli hesabıyla kurulur.

### 4.2 Bağ kurma & onay (KVKK / gizlilik)
- Veli–çocuk bağı **onaya** dayanır: bağ `Pending` doğar, **öğrenci ya da öğrencinin öğretmeni** onaylar (`Approved`).
- **Reşit olmayan öğrenci:** velinin erişimi velayet gereği varsayılan kabul edilir; yine de bağ kaydı (`Approved`) oluşturulur.
- **Reşit öğrenci:** bağ ve veri paylaşımı **öğrenci onayı** gerektirir; öğrenci dilediğinde bağı reddedebilir/iptal ettirebilir.
- Onaylanan bağ M03'te `StudentProfile.ParentUserId`'yi (birincil veli için) güncelleyebilir.

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
     → M03 StudentProfile.ParentUserId güncelle (birincil veli) → M11 (veliye "bağ onaylandı")
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

## 6. Mobil Ekranlar (Planlanan — Flutter `parents` feature)

> Birincil renk `0xFF082B4F`. `Parent` rolü için ayrı navigasyon (rol bazlı `redirect`, bkz. `../roles/veli.md`).
> Feature klasörü: `mobile/lib/features/parents/`.

- `parent_onboarding` — veli kaydı + çocuk bağlama (davet kodu / öğrenci e-postası).
- `parent_dashboard` — **çocuk seçici** + haftalık özet kartları:
  - Bu hafta kaç saat çalıştı (M08)
  - Hangi derslere ne kadar zaman ayırdı / test performansı (M08)
  - (Öğretmen bağlıysa) son ders özeti, yaklaşan dersler, ödev durumu (M05/M06)
  - (Öğretmen bağlıysa) ödeme özeti (M07)
- `parent_child_detail` — seçili çocuğun detaylı gelişimi (M10/M14 grafikleri).
- `parent_progress` — konu bazlı gelişim ve hedef ilerleme (M10).
- `parent_notifications` — bildirim tercihleri (`NotificationPreferences`).
- `parent_children` — bağlı çocuklar + bağ durumu + yeni bağ talebi.

---

## 7. Kabul Kriterleri

### Faz 2 (öğretmensiz)
- [ ] Veli **gerçek kullanıcı** olarak kaydolup çocuğuna **onaylı** bağlanabilir.
- [ ] Veli, çocuğunun **bireysel çalışma** verilerini (süre, ders dağılımı, test, streak) — paylaşıma açıksa — görebilir.
- [ ] Çoklu çocuk desteği: bir veli birden çok çocuğu yönetebilir (çocuk seçici).
- [ ] Bildirim tercihleri ayarlanabilir; **gizlilik filtreleme** uygulanır (salt-okunur).

### Faz 3 (öğretmen verisi entegre)
- [ ] Öğretmen bağlıysa veli; **son ders özeti, ödevler, öğretmen notları, ödeme özeti** görebilir.
- [ ] **Ödev kaçırma** bildirimi (M11) tercihe bağlı çalışır.
- [ ] Gelişim grafik + rapor (M10/M14) veli panelinde görüntülenir.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul:** Veli panelinin Faz 2'de değerli olması için **M08 (Study) önce inşa edilmeli**
> (bkz. [`m08_study.md`](m08_study.md)); aksi halde gösterilecek bireysel veri olmaz.
> Faz 3 için M05/M06/M07'nin **`IsSharedWithParent`** bayrakları ve veli-okuma sorguları gerekir.

1. **M08'i tamamla** (Faz 2 önkoşulu).
2. **Parents domain'i** — `ParentProfile`, `ParentChildLink` + enum + olaylar.
3. **Application (CQRS)** — profil/link komutları, onay akışı, dashboard sorguları; **read-model** servis arayüzleri.
4. **Infrastructure** — `ParentsDbContext` `DbSet`'leri, **`parents` şeması migration'ı**, integration event handler'ları.
5. **Veli–çocuk bağlama + onay** akışı (davet kodu / öğretmen onayı / öğrenci onayı; KVKK).
6. **Birleşik dashboard read-model'i** — M08/M05/M06/M07/M10 verisini event/service ile toplama (doğrudan DB yok).
7. **Gizlilik filtreleme** — `IsSharedWithParent` bayraklarına göre veri kısıtlama (M15 ile).
8. **M11 entegrasyonu** — ödev kaçırma, haftalık özet, ders hatırlatma bildirimleri.
9. **Mobil `parents` feature** + `Parent` rolü navigasyonu.

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
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

*M09 Veli (Parents) Modülü — Detaylı Tasarım | Faz 2-3 | Durum: 🔴 İskelet | Güncelleme: 2026-06-24*
