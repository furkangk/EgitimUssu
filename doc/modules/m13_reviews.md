# ⭐ M13 — Öğretmen Puanlama ve Yorum Modülü (Reviews)

> **PRD Modülü:** M13 Puanlama & Yorum · **Backend Modülü:** `Reviews` · **Route Prefix:** `/api/reviews`
> **Faz:** 4️⃣ (Eşleştirme & Pazar Yeri) · **Öncelik:** Son · **Durum:** 🔴 İskelet
>
> **Amaç:** EğitimÜssü'nün **güven altyapısı**. Yıldız puanı ve yorum sistemi (promp.txt: *"yıldız sistemi ve
> yorum sistemi olmalı ki sistemde ki kişilere güvenilmeli"*) ile öğretmen keşfinin (M12) güvenilirliğini
> sağlar. Yalnızca o öğretmenden **gerçekten ders almış** öğrenciler yorum yapabilir; öğretmen yoruma yanıt
> verebilir ama olumsuz yorumu **gizleyemez**; şüpheli yorumlar bildirilip admin tarafından modere edilir.

> **Tasarım ilkesi (PRD §10.1):** Puanlama (M13), eşleştirmenin (M12) güven altyapısıdır — sosyal kanıt
> olmadan keşif yeterli güven vermez. Bu yüzden M12 ile birlikte Faz 4'te tam açılır. Ancak **Faz 1-2'de
> "öğretmene özel geri bildirim"** olarak erken aktive edilerek veri birikimi erkenden başlatılır (bkz. §4.5).

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

`Reviews` modülü şu anda **iskelet** seviyesindedir. Kodda yalnızca aşağıdakiler mevcuttur:

| Katman | Dosya | İçerik |
|--------|-------|--------|
| API | `src/Modules/Reviews/API/ReviewsModule.cs` | `ModuleDefinition`; `Name = "Reviews"`, `RoutePrefix = "/api/reviews"`, tek endpoint `GET /api/reviews/status` → `{ module, route, state = "placeholder" }` |
| Infrastructure | `src/Modules/Reviews/Infrastructure/ReviewsDbContext.cs` | `ReviewsDbContext : ModuleDbContext`, `SchemaName = "reviews"` (ayrı PostgreSQL şeması) |
| Infrastructure | `src/Modules/Reviews/Infrastructure/DependencyInjection.cs` | `AddReviewsModule(...)` DI kaydı |
| Domain / Application | `AssemblyReference.cs` | Boş — aggregate, command/query, handler **yok** |

**Henüz olmayanlar:** Domain aggregate'leri, CQRS command/query + handler, EF entity konfigürasyonu, migration,
integration event handler (yorum hakkı doğrulaması), mobil/web ekranları, admin moderasyon paneli.

**Hazır altyapı (bu modülü besleyen, kodda mevcut olan zemin):**

- **`LessonSessionCompletedDomainEvent`** (`LessonSessions` modülü) zaten yayılıyor. Bu olay, bir öğretmen-öğrenci
  çifti arasında **tamamlanmış ders** olduğunu kanıtlar — yorum hakkının (`IsVerifiedStudent`) doğum kaynağıdır.
  Bkz. [`m05_lesson_sessions.md`](m05_lesson_sessions.md).
- **`TeacherProfile`** (`Teachers` modülü) öğretmen kimliğini ve doğrulama rozetini sağlar; puan ortalaması bu
  profile bağlanır (M12 arama projeksiyonu bu ortalamayı tüketir). Bkz. [`m02_teachers.md`](m02_teachers.md).
- **`StudentProfile`** (`Students` modülü) yorum yapan öğrenciyi temsil eder.
- **Outbox/Integration event altyapısı** (`Shared/Infrastructure/Messaging`) hazır; `Reviews` `LessonSessionCompleted`'i
  dinleyip "yorum daveti / yorum hakkı" kaydı oluşturabilir.

> **Modül sınırı kuralı:** `Reviews`, ders tamamlanma bilgisini `LessonSessions` DB'sinden **doğrudan okumaz**;
> `LessonSessionCompleted` integration event'ini dinleyip kendi şemasında bir **yorum hakkı (eligibility)**
> kaydı tutar. Yorum gönderiminde bu kayda bakılarak doğrulama yapılır.

---

## 2. Domain Modeli (⚠️ Önerilen — Henüz Kodda Yok)

> Aşağıdaki tablolar **önerilen** tasarımdır. `reviews` PostgreSQL şemasında, modül kendi DbContext'i ile yönetir.

### 2.1 `TeacherReview` (AggregateRoot) — Öğretmen Değerlendirmesi

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Yorum kimliği |
| `TeacherUserId` | Guid | Değerlendirilen öğretmen (Identity kullanıcı kimliği) |
| `StudentId` | Guid | Yorumu yazan öğrenci profili (`Students`) |
| `StudentUserId` | Guid? | Öğrencinin Identity kimliği (self-registered ise) |
| `OverallRating` | int (1–5) | Genel yıldız puanı |
| `ClarityRating` | int (1–5) | Alt kategori: **anlatım netliği** |
| `PunctualityRating` | int (1–5) | Alt kategori: **dakiklik ve güvenilirlik** |
| `PatienceRating` | int (1–5) | Alt kategori: **sabır ve yaklaşım** |
| `PreparationRating` | int (1–5) | Alt kategori: **ders hazırlığı** |
| `Comment` | string? | Yorum metni (opsiyonel, sadece yıldız da verilebilir) |
| `IsVerifiedStudent` | bool | Bu öğretmenden ders almış mı (LessonSessionCompleted ile doğrulanmış) |
| `Visibility` | enum `ReviewVisibility` | `TeacherOnly=1` (erken/özel geri bildirim), `Public=2` (Faz 4 herkese açık) |
| `Status` | enum `ReviewStatus` | `Published=1`, `Flagged=2`, `Removed=3` |
| `RelatedLessonSessionId` | Guid? | Yorumun dayandığı tamamlanmış ders (kanıt) |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime? | Zaman damgaları |
| `Response` | `ReviewResponse?` | Öğretmenin yanıtı (1:0..1) |

**Davranışlar:**
- `Publish()` → `Status = Published`, `TeacherReviewPublishedDomainEvent` yayılır (→ M12 projeksiyonu ortalamayı günceller).
- `Flag(reason, reporterUserId)` → `Status = Flagged` (içerik gizlenmez, moderasyon kuyruğuna girer).
- `Remove(adminUserId, reason)` → `Status = Removed` (yalnızca admin moderasyonu).
- `AddResponse(text)` → öğretmen yanıtı ekler/günceller.

**Domain Events:** `TeacherReviewSubmittedDomainEvent`, `TeacherReviewPublishedDomainEvent`,
`TeacherReviewFlaggedDomainEvent`, `TeacherReviewRemovedDomainEvent`.

### 2.2 `ReviewResponse` (Entity) — Öğretmen Yanıtı

`TeacherReview`'a bağlı (1:0..1). Olumsuz yorum **gizlenemez**, yalnızca yanıtlanabilir (şeffaflık kuralı).

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Yanıt kimliği |
| `TeacherReviewId` | Guid | Bağlı yorum |
| `TeacherUserId` | Guid | Yanıtlayan öğretmen (yorumun hedefi olmalı) |
| `Text` | string | Yanıt metni |
| `CreatedOnUtc`, `UpdatedOnUtc` | DateTime? | Zaman damgaları |

### 2.3 `ReviewFlag` (Entity) — Şüpheli/Şikayet Bildirimi

Şüpheli veya uygunsuz yorumların admin moderasyonuna taşınması için. Şikayet/moderasyon akışı
[`m18_feedback.md`](m18_feedback.md) ve admin paneli ile ortaktır.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | Flag kimliği |
| `TeacherReviewId` | Guid | Bildirilen yorum |
| `ReportedByUserId` | Guid | Bildiren kullanıcı (öğretmen veya başka kullanıcı) |
| `Reason` | enum `FlagReason` | `Spam`, `Offensive`, `FakeReview`, `Irrelevant`, `Other` |
| `Note` | string? | Açıklama |
| `Status` | enum `FlagStatus` | `Open=1`, `Reviewed=2`, `Dismissed=3`, `Actioned=4` |
| `ResolvedByAdminUserId` | Guid? | Sonuçlandıran admin |
| `CreatedOnUtc`, `ResolvedOnUtc` | DateTime? | Zaman damgaları |

### 2.4 `ReviewEligibility` (Entity / Read kaydı) — Yorum Hakkı

`LessonSessionCompleted` event'inden türeyen, "bu öğrenci bu öğretmene yorum yapabilir" hakkını tutan kayıt.
Modül sınırını korur (LessonSessions DB'sine sorgu atılmaz).

| Alan | Tip | Açıklama |
|------|-----|----------|
| `TeacherUserId`, `StudentId` | Guid | Çift |
| `CompletedSessionCount` | int | Tamamlanan ders sayısı |
| `FirstCompletedOnUtc`, `LastCompletedOnUtc` | DateTime | İlk/son tamamlanan ders |
| `HasReviewed` | bool | Bu çift için zaten yorum yapıldı mı (tek yorum kuralı) |

### 2.5 Ortalama Puan Hesabı

- Öğretmenin **genel ortalaması** ve alt kategori ortalamaları yalnızca `Status = Published` **ve**
  `Visibility = Public` yorumlardan hesaplanır.
- M12 sıralamasında haksızlığı önlemek için **ağırlıklı (Bayesian) ortalama** önerilir: yorum sayısı azken
  global ortalamaya çekilir, arttıkça gerçek ortalamaya yaklaşır.
- `TeacherReviewPublishedDomainEvent` yayıldığında `Matching` modülünün `TeacherSearchProjection`'ı
  (`AverageRating`, `ReviewCount`) güncellenir — bkz. [`m12_matching.md`](m12_matching.md) §2.4.

---

## 3. API Sözleşmesi (⚠️ Önerilen — Henüz Yok)

Mevcut: yalnızca `GET /api/reviews/status`. Aşağıdaki uçlar **önerilir**. Yazma uçları
`RequireAuthorization("AuthenticatedUser")` ile korunur; admin uçları admin yetkisi ister. `Result<T>` döner;
hatalar HTTP statüsüne eşlenir (`404`, `409`, `403 shared.forbidden`, varsayılan `400`).

### 3.1 Öğrenci / Herkes
```
POST /api/reviews
       → yorum gönder (yalnızca dersi tamamlamış öğrenci — ReviewEligibility doğrulanır)
GET  /api/reviews/teachers/{teacherUserId}
       → öğretmenin herkese açık ortalama puanı + alt kategori ortalamaları + yorum listesi (sayfalı)
GET  /api/reviews/eligibility/{teacherUserId}
       → giriş yapan öğrencinin bu öğretmene yorum hakkı var mı (UI'da "Değerlendir" butonunu açar)
POST /api/reviews/{id}/flag
       → şüpheli/uygunsuz yorum bildir (→ ReviewFlag, moderasyon kuyruğu)
```

### 3.2 Öğretmen
```
POST /api/reviews/{id}/respond
       → yoruma yanıt ver/güncelle (yalnızca yorumun hedefi öğretmen; olumsuz yorum gizlenemez)
GET  /api/reviews/teachers/{teacherUserId}/private-feedback
       → erken/özel geri bildirimler (Visibility = TeacherOnly, yalnızca öğretmen görür) — bkz. §4.5
```

### 3.3 Admin (moderasyon)
```
GET  /api/reviews/flags?status=Open          → bildirilen yorum kuyruğu
POST /api/reviews/{id}/moderate
       → kararı uygula: yayında bırak (Dismiss) | kaldır (Remove) | flag'i sonuçlandır
```

---

## 4. İş Kuralları (Business Rules)

1. **Doğrulanmış öğrenci kuralı (sahte yorum önleme):** Yalnızca o öğretmenden **ders almış** öğrenci yorum
   yapabilir. Hak, `LessonSessionCompletedDomainEvent` ile o öğretmen-öğrenci çifti için **tamamlanmış ders**
   kaydı (`ReviewEligibility`) oluştuğunda doğar. Hak yoksa `403 reviews.not_eligible`.
2. **Tek yorum:** Bir öğrenci bir öğretmen için tek bir genel yorum yapabilir (düzenleyebilir); ikinci yorum
   `409 reviews.already_reviewed`. (`ReviewEligibility.HasReviewed`.)
3. **Puan aralığı:** `OverallRating` ve tüm alt kategoriler 1–5 arası tam sayı olmalı; aksi halde
   `reviews.invalid_rating`.
4. **Olumsuz yorum gizlenemez:** Öğretmen yorumu silemez/gizleyemez; yalnızca `ReviewResponse` ile **yanıtlayabilir**
   (şeffaflık — güven altyapısının temeli).
5. **Yorum kaldırma yalnızca admin:** Bir yorum yalnızca admin moderasyonuyla (`Remove`) kaldırılır; gerekçe
   kaydedilir. Öğretmen veya öğrenci tek taraflı kaldıramaz.
6. **Flag içeriği gizlemez:** `Flag` durumu yorumu moderasyon kuyruğuna alır ama otomatik gizlemez; karar admindedir.
7. **Görünürlük ayrımı:** `Visibility = TeacherOnly` (erken/özel geri bildirim) yorumlar **ortalamaya ve herkese
   açık listeye dahil edilmez**, yalnızca öğretmen görür. `Public` yorumlar ortalamayı ve M12 sıralamasını besler.
8. **Modül sınırı:** Ders tamamlanma kanıtı doğrudan `LessonSessions` DB'sinden okunmaz; `ReviewEligibility`
   integration event'le beslenir.
9. **Yetki:** Yoruma yalnızca hedef öğretmen yanıt verebilir; özel geri bildirimi yalnızca o öğretmen görür;
   moderasyon uçları yalnızca admin (`shared.forbidden`).
10. **Bağlantı M18/Admin:** Şikayet ve moderasyon akışı `m18_feedback.md` ile ortaktır; admin rolünün yetki ve
    panel tanımı [`../roles/admin.md`](../roles/admin.md) içindedir.

---

## 5. Olay Akışı (Event-Driven)

```
[Beslenen — diğer modülden gelen integration event]
LessonSessionCompleted   → Reviews: ReviewEligibility upsert (CompletedSessionCount++, Last/First tarih)
                         → (Faz 4) Notifications: öğrenciye "öğretmenini değerlendir" daveti (m11)

[Üretilen — Reviews'in yaydığı domain/integration event'ler]
TeacherReviewSubmitted   → (iç) doğrulama + Publish veya TeacherOnly olarak kayıt
TeacherReviewPublished   → Matching: TeacherSearchProjection.AverageRating + ReviewCount güncelle (m12)
                         → Teachers/Notifications: öğretmene "yeni yorum" bildirimi (m11)
TeacherReviewFlagged     → Admin moderasyon kuyruğuna düşer (m18 + admin paneli)
TeacherReviewRemoved     → Matching: ortalama yeniden hesapla (yorum ortalamadan çıkarılır)
```

> Olaylar **Outbox pattern** ile güvenilir yayılır. Beslenen tüketici (`LessonSessionCompleted` handler) için
> mevcut desen: `Assignments/Infrastructure/LessonSessionCompletedIntegrationEventHandler.cs`.

---

## 6. Mobil + Web Ekranlar (Planlanan)

### 6.1 Mobil (Flutter — `mobile/lib/features/reviews/`)

| Route (öneri) | Sayfa | Açıklama |
|---------------|-------|----------|
| `/reviews/submit/:teacherUserId` | `ReviewSubmitPage` | Ders sonrası yıldız (genel) + 4 alt kategori + yorum metni (yalnızca yorum hakkı varsa açık) |
| `/teacher/:id` (M12 profil içinde) | `TeacherReviewsSection` | Ortalama + alt kategori çubukları + yorum listesi + öğretmen yanıtları |
| `/reviews/respond/:id` | `ReviewRespondPage` | (Öğretmen) yoruma yanıt |
| `/teacher/private-feedback` | `PrivateFeedbackPage` | (Öğretmen) erken/özel geri bildirimler (Faz 1-2) |
| `/reviews/:id/flag` | `ReviewFlagSheet` | Şüpheli yorum bildir |

- **Tema:** Yıldız ve "Doğrulanmış Öğrenci" rozeti birincil marka rengi `0xFF082B4F` (EğitimÜssü lacivert) ile
  vurgulanır.
- **Durum yönetimi:** `flutter_bloc` (Cubit); ağ `dio`, DI `get_it`, yönlendirme `go_router`.

### 6.2 Web (Angular)

Admin moderasyon paneli web tarafında (Angular `features/admin`): flag kuyruğu, yorum karar ekranı
(yayında bırak / kaldır), kötüye kullanım istatistikleri. Öğretmen tarafında gelişmiş yorum analizi
(zamana göre puan trendi) `teacher-dash` ile bütünleşir (Tailwind CSS, responsive).

---

## 7. Kabul Kriterleri (Faz 4 Çıktısı)

- [ ] Yalnızca dersi tamamlamış (`ReviewEligibility`) öğrenci yorum yapabilir; aksi halde "Değerlendir" kapalı.
- [ ] Genel yıldız (1–5) + 4 alt kategori (anlatım netliği, dakiklik, sabır/yaklaşım, ders hazırlığı) puanlama.
- [ ] Herkese açık öğretmen profilinde ortalama + alt kategori ortalamaları + yorum listesi gösterilir.
- [ ] Doğrulanmış öğrenci rozeti yorumlarda görünür.
- [ ] Öğretmen yoruma yanıt verebilir; **olumsuz yorumu silemez/gizleyemez**.
- [ ] Şüpheli yorum bildirilebilir (flag) → admin moderasyon kuyruğu.
- [ ] Admin yorumu kaldırabilir/yayında bırakabilir (gerekçe kaydıyla).
- [ ] `TeacherReviewPublished` → M12 arama projeksiyonu ortalaması güncellenir.
- [ ] **Erken açılış:** Faz 1-2'de "öğretmene özel geri bildirim" (TeacherOnly) çalışır ve ortalamaya dahil edilmez.

---

## 8. Eksikler ve Yapılacaklar Listesi

> ⚠️ **Önkoşul (PRD §10.1):** Tam (herkese açık) puanlama, M12 ile birlikte **Faz 4'te** açılır ve Faz 1-2-3
> gerçek kullanıcılarda doğrulanmadan başlanmamalıdır. Ancak **erken açılış** (özel geri bildirim, §4.5)
> Faz 1-2'de devreye alınarak veri birikimi erkenden başlatılır.

**Önkoşul doğrulama listesi:**
- [ ] Faz 1 doğrulandı — ders oturumları tamamlanıyor, `LessonSessionCompleted` üretiliyor ([`m05_lesson_sessions.md`](m05_lesson_sessions.md)).
- [ ] Faz 2 doğrulandı — öğrenci tarafı aktif (yorum yapacak gerçek öğrenciler var).
- [ ] Faz 3 doğrulandı — veli paneli gerçek kullanımda.
- [ ] M12 keşif altyapısı hazır (puan, sıralamayı besleyecek tüketici var).

**Yapılacaklar (sıra):**
1. **Erken geri bildirim (Faz 1.9)** — `TeacherReview` + `Visibility = TeacherOnly` ile "öğretmene özel"
   basit değerlendirme; ortalamaya katılmaz, yalnızca öğretmen görür. Veri biriktirmeye başla.
2. **`ReviewEligibility` besleme** — `LessonSessionCompleted` integration event handler + idempotent upsert.
3. **`TeacherReview` domain'i + CQRS** — gönderim + doğrulama (eligibility) + EF konfig + migration (`reviews` şeması).
4. **Ortalama hesabı** — Bayesian ağırlıklı ortalama + `TeacherReviewPublished` event yayını (M12'yi besler).
5. **`ReviewResponse`** — öğretmen yanıtı (olumsuz yorum gizlenemez kuralı).
6. **`ReviewFlag` + admin moderasyon** — flag kuyruğu, karar (kaldır/yayında bırak), gerekçe kaydı (m18 + admin).
7. **Mobil ekranları** — değerlendirme gönder, yorum listesi/yanıt, özel geri bildirim, flag.
8. **Web admin paneli** — moderasyon kuyruğu + karar ekranı (Angular).
9. **Yorum daveti bildirimi** — ders tamamlandıktan sonra otomatik "değerlendir" daveti (m11).

---

## 9. İlişkili Dokümanlar

- **Roller:** [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md) · [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md)
- **Değerlendirilen öğretmen profili + doğrulama rozeti:** [`m02_teachers.md`](m02_teachers.md)
- **Yorum yapan öğrenci profili:** [`m03_students.md`](m03_students.md)
- **Yorum hakkının kaynağı (tamamlanmış ders):** [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- **Güven altyapısını tüketen keşif/sıralama:** [`m12_matching.md`](m12_matching.md)
- **Yorum sonrası iletişim:** [`m16_messaging.md`](m16_messaging.md)
- **Ücretli üyelik (sıralamada premium boost ile birlikte değerlendirilir):** [`m17_membership.md`](m17_membership.md)
- **Şikayet / moderasyon akışı + admin paneli:** [`m18_feedback.md`](m18_feedback.md)
- **Veri modeli (ER + modüller arası referans):** [`veri_modeli.md`](veri_modeli.md)
- **Genel durum & eşleme tablosu:** [`00_genel_bakis.md`](00_genel_bakis.md)
- **Ürün gereksinimleri:** [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md)

---

*EğitimÜssü — M13 Öğretmen Puanlama ve Yorum Modülü · Detaylı Tasarım | Faz 4 | Durum: 🔴 İskelet | Güncelleme: 2026-06-24*
