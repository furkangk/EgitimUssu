---
title: "M18 — Geri Bildirim ve Şikayet"
summary: "Planlanan geri bildirim (bug/öneri) + şikayet/moderasyon modülü; backend klasörü kodda tamamen yok"
tags: [modul, feedback, sikayet, planlanan, faz-1, faz-4]
status: "🔴"
authority: product
updated: 2026-08-19
---

# 🛡️ Geri Bildirim ve Şikayet Modülü (M18) — Detaylı Tasarım Dokümanı

> **PRD: M18 (YENİ)** · **Faz 1+ (geliştirme bug geri bildirimi) → Faz 4 (kötüye kullanım şikayeti & moderasyon)** · **Durum: 🔴 YENİ — kodda HİÇ YOK (tüm domain ⚠️ Önerilen, planlanan)**
>
> **Amaç:** İki ihtiyacı tek modülde karşılamak:
> 1. **Geri bildirim:** Kullanıcılar geliştirme sürecinde **bug** ve **öneri** bildirebilsin (erken fazda kalite için kritik).
> 2. **Şikayet (kötüye kullanım):** Kullanıcı, yorum, mesaj veya ilan **şikayet edilebilsin** ve admin moderasyonuyla işlensin.
>
> Bkz. [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) ("bildirme ve şikayet", "geliştirme buglarını geri bildirim").

> İlgili: yorum şikayeti / `ReviewFlag` → [`m13_reviews.md`](m13_reviews.md) · mesaj şikayeti → [`m16_messaging.md`](m16_messaging.md) · admin moderasyon → [`../roles/admin.md`](../roles/admin.md).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

🔴 **Kodda hiçbir karşılığı yoktur.** Tamamen planlama aşamasındadır:

- **Backend:** `src/Modules/Feedback/` **yok** — `FeedbackDbContext`, `ModuleDefinition`, DI, migration, `/status` endpoint'i **yok**.
- **Mobil:** geri bildirim/şikayet feature'ı **yok** ("Hata bildir", "Şikayet et" akışı yok).
- **Şema:** PostgreSQL'de `feedback` şeması **yok**.
- **İlişki:** M13'te planlanan `ReviewFlag` (yorum şikayeti) ve M16'da planlanan mesaj şikayeti henüz yok; bu modül bunların **ortak hedefi**dir.

> ⚠️ Bu dokümandaki **tüm** içerik **önerilen / planlanan**dır. Bug geri bildirimi (M18-A) Faz 1'den itibaren açılabilir;
> kötüye kullanım şikayeti (M18-B) sosyal/pazar yeri özellikleriyle (Faz 4) birlikte anlam kazanır.

---

## 2. Domain Modeli (⚠️ Önerilen)

**Şema:** `feedback` · **DbContext:** `FeedbackDbContext` · **Route prefix:** `/api/feedback`
**Aggregate'ler:** `FeedbackTicket` (geri bildirim/bug/öneri) ve `AbuseReport` (kötüye kullanım şikayeti).

> Modül sınırı kuralı: `Feedback`, şikayet edilen hedefin (kullanıcı/yorum/mesaj/ilan) sahibi modülün
> tablosuna erişmez; yalnızca `TargetType + TargetId` referansını saklar. Moderasyon kararı, ilgili modüle
> integration event ile bildirilir (örn. yorum kaldırma → M13, mesaj kaldırma → M16).

### 2.1 `FeedbackTicket` (AggregateRoot) — bug / öneri / diğer

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `UserId` | Guid | Bildiren kullanıcı (Identity) |
| `Type` | enum `FeedbackType` | `Bug=1`, `Suggestion=2`, `Question=3`, `Other=4` |
| `Title` | string | Başlık (maks. 150) |
| `Body` | string | Açıklama (maks. 4000) |
| `Status` | enum `TicketStatus` | `Open=1`, `InReview=2`, `Resolved=3`, `Closed=4`, `WontFix=5` |
| `Severity` | enum `Severity`? | `Low=1`, `Medium=2`, `High=3`, `Critical=4` (bug için) |
| `AppVersion` | string? | Uygulama sürümü (meta) |
| `Platform` | enum `ClientPlatform`? | `Android=1`, `iOS=2`, `Web=3` |
| `ScreenContext` | string? | Hangi ekran/rota (meta) |
| `AttachmentUrl` | string? | Ekran görüntüsü/log eki |
| `AdminNote` | string? | Yönetici notu / çözüm açıklaması |
| `AssignedToUserId` | Guid? | Atanan yönetici (varsa) |
| `CreatedOnUtc` / `UpdatedOnUtc` | DateTime | |
| `ResolvedOnUtc` | DateTime? | |

**Davranışlar:** `Submit()`, `StartReview()`, `Resolve(note)`, `Close()`, `MarkWontFix(note)`, `Assign(adminUserId)`.
**Event'ler:** `FeedbackTicketSubmittedDomainEvent`, `FeedbackTicketStatusChangedDomainEvent`.
**DB:** tablo `feedback_tickets`; index `(Status, Type, CreatedOnUtc)`, `(UserId)`.

### 2.2 `AbuseReport` (AggregateRoot) — kötüye kullanım şikayeti

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `ReporterUserId` | Guid | Şikayet eden kullanıcı |
| `TargetType` | enum `ReportTargetType` | `User=1`, `Review=2`, `Message=3`, `Listing=4` |
| `TargetId` | Guid | Hedefin kimliği (ilgili modülde) |
| `Reason` | enum `AbuseReason` | `Spam=1`, `Harassment=2`, `InappropriateContent=3`, `Fraud=4`, `FakeReview=5`, `Other=99` |
| `Description` | string? | Serbest açıklama (maks. 2000) |
| `Status` | enum `ReportStatus` | `Pending=1`, `UnderReview=2`, `ActionTaken=3`, `Dismissed=4` |
| `Resolution` | enum `ModerationAction`? | `None=0`, `ContentRemoved=1`, `UserWarned=2`, `UserSuspended=3`, `NoViolation=4` |
| `ResolutionNote` | string? | Moderatör notu |
| `ModeratedByUserId` | Guid? | Kararı veren admin |
| `CreatedOnUtc` / `UpdatedOnUtc` | DateTime | |
| `ResolvedOnUtc` | DateTime? | |

**Davranışlar:** `Submit()`, `StartReview(adminUserId)`, `Resolve(action, note)`, `Dismiss(note)`.
**Event'ler:** `AbuseReportSubmittedDomainEvent`, `AbuseReportResolvedDomainEvent` (içinde `TargetType`+`TargetId`+`ModerationAction` taşır).
**DB:** tablo `abuse_reports`; index `(Status, CreatedOnUtc)`, `(TargetType, TargetId)`, `(ReporterUserId)`.
**Tekillik/anti-spam:** Aynı raporlayan + aynı hedef için açık (`Pending/UnderReview`) tek rapor (UNIQUE kısmi index).

### 2.3 Enum'lar (⚠️ Önerilen)

| Enum | Değerler |
|------|----------|
| `FeedbackType` | `Bug=1`, `Suggestion=2`, `Question=3`, `Other=4` |
| `TicketStatus` | `Open=1`, `InReview=2`, `Resolved=3`, `Closed=4`, `WontFix=5` |
| `Severity` | `Low=1`, `Medium=2`, `High=3`, `Critical=4` |
| `ClientPlatform` | `Android=1`, `iOS=2`, `Web=3` |
| `ReportTargetType` | `User=1`, `Review=2`, `Message=3`, `Listing=4` |
| `AbuseReason` | `Spam=1`, `Harassment=2`, `InappropriateContent=3`, `Fraud=4`, `FakeReview=5`, `Other=99` |
| `ReportStatus` | `Pending=1`, `UnderReview=2`, `ActionTaken=3`, `Dismissed=4` |
| `ModerationAction` | `None=0`, `ContentRemoved=1`, `UserWarned=2`, `UserSuspended=3`, `NoViolation=4` |

---

## 3. API Sözleşmesi (⚠️ Önerilen — `/api/feedback`)

```
# Geri bildirim (bug / öneri) — tüm kullanıcılar
POST /api/feedback/tickets                          → bug/öneri gönder (sürüm/ekran/platform meta ile)
GET  /api/feedback/users/{userId}/tickets           → kullanıcının kendi bildirimleri
GET  /api/feedback/tickets/{ticketId}               → bildirim detayı (sahibi veya admin)

# Şikayet (kötüye kullanım) — tüm kullanıcılar
POST /api/feedback/reports                           → şikayet gönder (TargetType + TargetId + Reason)
GET  /api/feedback/users/{userId}/reports            → kullanıcının gönderdiği şikayetler

# Admin moderasyon
GET  /api/feedback/admin/tickets?status=&type=       → tüm bildirimler (admin kuyruğu)
PUT  /api/feedback/admin/tickets/{ticketId}          → durum/atama/çözüm güncelle
GET  /api/feedback/admin/reports?status=&targetType= → şikayet kuyruğu (admin)
POST /api/feedback/admin/reports/{reportId}/review   → incelemeye al
POST /api/feedback/admin/reports/{reportId}/resolve  → karar (ModerationAction) → ilgili modüle yayılır
POST /api/feedback/admin/reports/{reportId}/dismiss  → reddet
```

> **Yetki:** Bildirim/şikayet gönderme tüm kimliği doğrulanmış kullanıcılara açık. Kuyruk ve moderasyon
> uçları **yalnızca admin** (bkz. [`../roles/admin.md`](../roles/admin.md), [`mimari_inceleme.md`](mimari_inceleme.md) "varsayılan reddet" guard'ı).
> Kullanıcı yalnızca **kendi** bildirim/şikayetlerini listeleyebilir.

---

## 4. İş Kuralları

1. **İki akış, tek modül:** `FeedbackTicket` (geliştirme/ürün geri bildirimi) ile `AbuseReport` (kötüye kullanım) ayrı aggregate'lerdir; karıştırılmaz.
2. **Bug meta zorunluluğu:** `Type=Bug` için `Platform` + `AppVersion` önerilir (tekrar üretim için); `ScreenContext` ve ek (ekran görüntüsü) opsiyonel ama teşvik edilir.
3. **Şikayet hedefi geçerli olmalı:** `TargetType + TargetId` çifti ilgili modülde var olmalı; doğrulama integration event projeksiyonu üzerinden yapılır (doğrudan DB erişimi yok).
4. **Tek aktif şikayet:** Aynı kullanıcı aynı hedefi tekrar tekrar şikayet edemez (açık rapor varken yenisi reddedilir) — anti-spam.
5. **Kendini/sistemini şikayet edememe:** Kullanıcı kendi içeriğini kötüye kullanım olarak şikayet edemez (gerekiyorsa silme/düzenleme ilgili modülde).
6. **Moderasyon sonucu yayılır:** Admin `Resolve(action)` verince `AbuseReportResolvedDomainEvent` yayınlanır; hedef modül aksiyonu uygular:
   - `Review` + `ContentRemoved` → M13 yorumu `Removed` yapar (bkz. [`m13_reviews.md`](m13_reviews.md)).
   - `Message` + `ContentRemoved` → M16 mesajı yumuşak siler (bkz. [`m16_messaging.md`](m16_messaging.md)).
   - `User` + `UserSuspended` → M01 kullanıcı durumunu `Suspended` yapar (bkz. [`m01_identity.md`](m01_identity.md)).
   - `Listing` + `ContentRemoved` → M12 ilanı kaldırır (bkz. [`m12_matching.md`](m12_matching.md)).
7. **`ReviewFlag` köprüsü:** M13'teki "şüpheli yorum bildir" akışı, bu modülde `AbuseReport(TargetType=Review, Reason=FakeReview)` olarak normalize edilebilir; tek moderasyon kuyruğu hedeflenir.
8. **Mesaj şikayeti köprüsü:** M16'daki "mesajı şikayet et", `AbuseReport(TargetType=Message)` üretir.
9. **Bildirim:** Bildirim/şikayet durum değişiminde (örn. `Resolved`) bildirene in-app bilgilendirme gidebilir (bkz. [`m11_notifications.md`](m11_notifications.md)).
10. **Gizlilik:** Raporlayan kimliği şikayet edilen tarafa açıklanmaz; KVKK uyumlu saklama.

---

## 5. Olay Akışı (⚠️ Önerilen)

```
[Bug/öneri]
Kullanıcı → POST /tickets (type, title, body, platform, appVersion, screen)
   → FeedbackTicket.Submit() → FeedbackTicketSubmittedDomainEvent
      → admin kuyruğuna düşer; admin StartReview/Resolve → FeedbackTicketStatusChangedDomainEvent
         → (opsiyonel) bildirene bilgilendirme (M11)

[Şikayet — doğrudan]
Kullanıcı → POST /reports (targetType, targetId, reason)
   → AbuseReport.Submit() → AbuseReportSubmittedDomainEvent

[Şikayet — diğer modülden köprü]
M13 yorum şikayeti / M16 mesaj şikayeti (integration event)
   → Feedback: AbuseReport(TargetType=Review|Message) oluştur

[Moderasyon kararı]
Admin → POST /admin/reports/{id}/resolve (action)
   → AbuseReport.Resolve(action) → AbuseReportResolvedDomainEvent
      → (Outbox) ModerationActionIntegrationEvent (TargetType, TargetId, action)
         → M13: yorumu Removed   | M16: mesajı sil
         → M01: kullanıcıyı Suspended | M12: ilanı kaldır
```

---

## 6. Mobil Ekranlar (Planlanan)

`mobile/lib/features/feedback/`:

- **report-bug / send-feedback** — tür seçimi (bug/öneri), başlık+açıklama, otomatik sürüm/platform/ekran meta, ekran görüntüsü ekleme; genellikle "Daha Fazla/Ayarlar" altından erişilir.
- **my-feedback** — kullanıcının gönderdiği bildirimler ve durumları (Open/InReview/Resolved).
- **report-content** — yorum/mesaj/kullanıcı/ilan yanındaki "Şikayet et" akışı (sebep seçimi + açıklama). M13 yorum kartından ve M16 mesaj menüsünden tetiklenir.
- **admin: moderation-queue** (admin paneli; mobil veya web) — bekleyen şikayet/bildirim listesi, incele, karar ver.

> Kurumsal renk `0xFF082B4F`. "Şikayet et" girişleri M13 ve M16 ekranlarına gömülüdür; ayrı bir modül ekranı gerektirmez.

---

## 7. Kabul Kriterleri (⚠️ Önerilen)

- [ ] Kullanıcı bug/öneri gönderebiliyor (platform + sürüm + ekran meta ile).
- [ ] Kullanıcı kendi bildirim/şikayetlerini listeleyip durumlarını görebiliyor.
- [ ] Kullanıcı/yorum/mesaj/ilan şikayet edilebiliyor; aynı hedefe mükerrer açık şikayet engelleniyor.
- [ ] Admin moderasyon kuyruğunu görebiliyor, incele/karar ver/ret yapabiliyor.
- [ ] Moderasyon kararı integration event ile ilgili modüle yayılıyor (yorum/mesaj kaldırma, kullanıcı askıya alma, ilan kaldırma).
- [ ] M13 `ReviewFlag` ve M16 mesaj şikayeti tek moderasyon kuyruğunda toplanıyor.
- [ ] Raporlayan kimliği gizli tutuluyor; KVKK uyumlu saklama.
- [ ] Bildirim durum değişiminde bildirene (opsiyonel) bilgilendirme gidiyor.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **Modül iskeleti** — `Feedback` modülü, `FeedbackDbContext`, `feedback` şeması, DI + `ModuleDefinition` + ilk migration.
2. **Domain** — `FeedbackTicket` + `AbuseReport` + enum'lar + event'ler.
3. **Bug/öneri akışı (Faz 1+)** — gönderim + kullanıcı listesi + admin kuyruğu (erken kalite için önce bu).
4. **Şikayet akışı (Faz 4)** — gönderim + anti-spam + admin moderasyon.
5. **Hedef doğrulama projeksiyonu** — User/Review/Message/Listing varlığını event'lerle doğrulama.
6. **Moderasyon yayılımı** — `ModerationActionIntegrationEvent` → M13/M16/M01/M12 tüketicileri.
7. **M13 `ReviewFlag` ve M16 mesaj şikayeti köprüleri** — tek kuyruğa normalize et.
8. **Mobil ekranlar** — hata bildir, şikayet et (M13/M16 gömülü), bildirimlerim, admin moderasyon.
9. **Bildirim entegrasyonu (M11)** — durum değişimi bilgilendirmesi.
10. **Admin panel raporları** — şikayet/bug istatistikleri (M14 ile).

---

## 9. İlişkili Dokümanlar

- Yorum şikayeti / `ReviewFlag` köprüsü → [`m13_reviews.md`](m13_reviews.md)
- Mesaj şikayeti köprüsü → [`m16_messaging.md`](m16_messaging.md)
- Kullanıcı askıya alma (moderasyon sonucu) → [`m01_identity.md`](m01_identity.md)
- İlan kaldırma (moderasyon sonucu) → [`m12_matching.md`](m12_matching.md)
- Durum bildirimi → [`m11_notifications.md`](m11_notifications.md)
- Şikayet/bug istatistikleri → [`m14_reporting.md`](m14_reporting.md)
- Gizlilik tercihleri → [`m15_settings.md`](m15_settings.md)
- Premium istismarı bağlamı → [`m17_membership.md`](m17_membership.md)
- Yetki guard'ı → [`mimari_inceleme.md`](mimari_inceleme.md)
- Veri modeli bağlamı → [`veri_modeli.md`](veri_modeli.md)
- Rol perspektifleri → [`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../roles/admin.md`](../roles/admin.md), [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md)
- Ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)
- Genel durum & strateji → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Geri Bildirim ve Şikayet Modülü (M18) — Detaylı Tasarım | Faz 1+ / Faz 4 | Güncelleme: 2026-08-19*
