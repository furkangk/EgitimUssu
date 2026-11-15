---
title: "Kapsamlı Proje İncelemesi — 2026-11-13 (N serisi)"
summary: "P01–P13 main'e merge edildikten sonra tüm projenin koda karşı incelemesi: mantık hataları, rol bazlı eksik özellikler, profesyonellik açıkları"
tags: [denetim, mantik-hatasi, guvenlik, urun, mobil, operasyon]
authority: derived
updated: 2026-11-15
---

# 🔎 Kapsamlı Proje İncelemesi — 2026-11-13

> **Kapsam:** `main` @ `117aac9` (P01–P13 dalları + `c16d25b` düzeltmeleri merge edilmiş hâli). `src/` (~45k satır, migration hariç) + `mobile/lib/` (~56k satır) + CI/infra + dokümanlar. Statik okuma; hiçbir dosya değiştirilmedi, test koşturulmadı.
> **Yöntem:** Beş paralel inceleme dilimi (zamanlama/ödeme · kimlik/erişim/üyelik · çalışma/ödev/altyapı · mobil · ürün/operasyon). Her dilim önce `2026-09-02_eksik_analizi.md`, `2026-09-05_mimari_aciklik_denetimi.md` ve `mimari_inceleme.md`'yi okudu; **orada zaten listelenen maddeler tekrar edilmedi**. ✅ işaretli olup kodda hâlâ bozuk olanlar ayrıca işaretlendi (§1.6).
> **Kapsam dışı:** Teachers handler'ları (fotoğraf yükleme, arama yanıt alanları — arama ucu kimlik doğrulamalı, anonim değil) · FCM/S3/SMTP iç ayrıntıları · Render panelinde elle girilmiş ortam değişkenleri (yalnız `render.yaml` okundu) · web istemcisi (henüz yok). **Test koşturulmadı**; tüm bulgular statik okumaya dayanıyor.
> **Güven işareti:** ✔ = bu incelemenin sonunda dosya ikinci kez açılarak teyit edildi · ◐ = inceleme kod yolunu `dosya:satır` ile gösterdi, ikinci teyit yapılmadı · ❔ = olası (yarış/çalışma zamanı koşulu gerekiyor, kodla kesin kanıtlanamaz).

---

> **P17 Task 3 (2026-11-15):** N-02/N-12/N-13 kapandı: oluşum anahtarlı oturum ve eşzamanlı türetme, öğrenci/veli profil kapsamı, tek tamamlama olayı ve oturum bazlı katılım makbuzu. Birim + gerçek PostgreSQL + mobil ekran/istek testleri eklendi. Aşağıdaki bulgu satırları ilk denetimin tarihî kanıtını korur.

## 0. Yönetici özeti

Proje mimari olarak olgun: modüler monolit, outbox/inbox, projeksiyonlar, 733 birim + 62 integration testi, KVKK silme akışı, gözlemlenebilirlik altyapısı var. **Ama ürünün "çekirdek iş akışlarında" (tekrarlayan ders → oturum → ücret, veli ↔ öğrenci bağı) ciddi mantık hataları var** ve bunlar mevcut testlerin göremediği yerde: modüller arası sınırda ve mobil ↔ backend sözleşmesinde.

**En kritik 5 mantık hatası**
1. **N-01** Tekrarlayan derste "bu haftayı iptal et" tüm seriyi iptal ediyor (mobil `scope` göndermiyor, backend varsayılanı `All`).
2. **N-02 ✅ (P17 T3)** Haftalık derste 2. haftadan itibaren ders oturumu/notu/ücreti oluşturulamıyor (idempotency anahtarı yalnız `LessonScheduleId`).
3. **N-20** Veli davet kodu 6 haneli `Random.Shared`, süresiz; isabet eden kişi doğrudan **onaylı birincil veli** oluyor.
4. **N-21** Öğrenci profil ID'sini bilen herhangi bir öğretmen hesabı, bağı olmadığı o öğrenci için veli daveti üretip ikinci bir hesapla kendisi kullanabiliyor → çocuğun birincil velisi oluyor.
5. **N-40 ✅ (P19 T1)** Öğrencinin çalışma serisi (streak) yanlış hesaplanıyor: tamamlanan seans, kaydedilmeden önce yapılan günlük toplama girmiyor. Günde tek seans yapan öğrencinin serisi hiç başlamıyor.

Hemen arkasından: **N-41** arka plan işleri kendi kirasını yenileyemediği için 30 sn'lik işler ~5 dk'da bir koşuyor (şifre sıfırlama e-postası dahil) · **N-12 ✅ (P17 T3)** öğrenci/veli ders oturumu listesi düzeltildi · **N-42 ✅ (P19 T2)** outbox tüketici izolasyonu, açık hata anlamı ve inbox tekrar koruması gerçek PostgreSQL'de doğrulandı.

**En değerli 5 özellik / akış tamamlama**
1. Veli ↔ öğrenci bağ akışının mobilde tamamlanması (onay/ret, davet kodu ile katılma) — şu an bağlar hep `Pending` kalıyor.
2. Öğretmenin öğrenci/veli davet formunun gerçek uca bağlanması (şu an sahte snackbar).
3. Oluşum bazlı ders modeli: her haftanın kendi oturumu, ücreti, hatırlatması; iptal politikası (geç iptal ücreti).
4. Mobilde tatil bloğu, erteleme talebi kabul/ret, veli "ödedim" beyanı onayı (backend hazır, ekran yok).
5. Bire-çok duyuru + veliye paylaşılabilir haftalık özet (WhatsApp'a kaçışı önler).

**En önemli 5 profesyonellik açığı**
1. `render.yaml`'a göre üretimde e-posta/push/depolama sağlayıcısı **Logging/Local** ve bunu engelleyen guard yok — Render panelinde elle ayarlanmadıysa yayına çıkışta şifre sıfırlama kodları log'a düşer, kullanıcı e-posta almaz, dosyalar geçici diske gider.
2. Mağaza yayınına engel: release APK **debug anahtarıyla** imzalı, mobilde **hesap silme yok**, iOS kamera/galeri izin açıklamaları yok, mesajlaşmada şikayet yok.
3. Hata takibi (Sentry/Crashlytics) ve alarm yok; gözlemlenebilirlik üretimde kapalı; Render **free** plan uyuyor → arka plan işleri (hatırlatma, outbox, silme) durur.
4. Hiçbir aggregate'ta **concurrency token yok** (`xmin`/`RowVersion` = 0) → ödeme, abonelik, refresh token'da lost update.
5. Hukuk: KVKK metinleri taslak, kullanım şartları yok, kayıtta rıza kutusu yok, 18 yaş altı için veli onayı mantığı yok; premium mobilde satılacaksa **IAP zorunluluğu** P09 planıyla çelişiyor.

---

## 1. Mantık hataları

### 1.1 Zamanlama · Ders oturumu · Ödeme

| ID | Şiddet | Bulgu | Konum | Senaryo → yanlış sonuç | Öneri | Güven |
|----|--------|-------|-------|------------------------|-------|-------|
| N-01 ◐ (P17 T1–T2 backend ✅ · mobil giriş T5/P21 T4 · tamamlama T4 ✅ · T8 yaşam döngüsü ✅) | 🔴 Kritik | Tekrarlayan ders tek kayıt gibi işleniyor | `Scheduling/Application/LessonScheduleFeatures.cs` (`OccurrenceScopeRules`, `SeriesSplitRules`) · `SchedulingDomainModel.cs` (`SplitSeriesAt`) · mobil kapsam veri katmanı/sheet hazır | Haftalık dersin bu haftası iptal edilince **tüm seri** `Cancelled` olur, geçmiş haftalar dahil takvimden düşer. "Tamamla" tüm seriyi `Completed` yapar; sonraki haftalar artık iptal/ertelenemez. Öğrencinin erteleme talebi kabul edilince tüm seri kayar (`LessonChangeRequestFeatures.cs:160-161`) | Tekrarlı derste `Scope` + `OccurrenceStartAtUtc` zorunlu olsun; `ThisAndFuture` geçmişi koruyan yeni seri açsın; mobil ders detayında "yalnız bu / bu ve sonrakiler / tümü" sorulsun; `Complete` oluşum bazlı çalışsın | ✔ |
| N-02 ✅ (P17 T3) | 🔴 Kritik | Haftalık derste 2. haftadan itibaren oturum üretilemiyor | `LessonSessions/Application/LessonSessionFeatures.cs:37,327-332` (`CreateSessionFromScheduleCommand(Guid LessonScheduleId)`, `GetByScheduleIdAsync`) | 2. hafta "Dersi başlat" → 1. haftanın tamamlanmış oturumu döner, `Start` 409 verir. Ders notu ve **ücret kaydı hiç oluşmaz**. `PlannedStartAtUtc` de serinin ilk tarihi olur | Anahtar `(ScheduleId, OccurrenceStartUtc)` + unique index | ✔ |
| N-03 ✅ (P17 T4) | 🟠 Yüksek | İki ayrı "tamamla" yolu birbirini kilitliyor | `Scheduling/Domain/SchedulingDomainModel.cs:229-236` · `Payments/.../LessonSessionCompletedPaymentHandler.cs:46-48` · `LessonSessionFeatures.cs:335` | Scheduling'deki "Tamamla" kendi olayını yayar; tüketiciler `SourceModule=="LessonSessions"` filtrelediği için **ücret/not/gelişim kaydı oluşmaz**, plan `Completed` olunca oturum türetme `schedule_not_derivable` verir. Ters yönde oturum tamamlanınca plan `Planned` kalır, sonradan iptal edilebilir → rapor aynı dersi hem tamamlanmış hem iptal sayar | **P17 T4:** Scheduling ucu/komutu/olayı kaldırıldı; mobil iki ekran oluşum oturumundan tamamlar. Scheduling inbox tüketicisi tekil/tekrarlı durumu yansıtır; tamamlanmış oluşumu kapsayan iptal/erteleme 409. Gerçek PostgreSQL tekil/tekrarlı senaryoda rapor 1 tamamlandı / 0 iptal; ✅ (P17 T8) yaşam döngüsünde 2 tamamlandı / 1 Single iptal / 300 TRY beklenen | ✔ |
| N-04 ✅ (P16 T2) | 🟠 Yüksek | Tekrar kuralı UTC gününe göre genişletiliyor | `Scheduling/Application/RecurrenceExpander.cs:100-107` (`cursor.DayOfWeek` UTC), `:144` (aylıkta birikimli `AddMonths`) | Salı 01:30 (TR) = Pazartesi 22:30 UTC, kural `BYDAY=TU` → ilk ders üretilmez, sonrakiler TR'de Çarşamba 01:30'a düşer. 31 Ocak aylık ders → 28 Şubat → 28 Mart… kayar | Genişletmeyi lesson'ın `TimeZone` alanıyla yerel saatte yap; ay gününü çapadan sabitle | ✔ (BYDAY) / ◐ (aylık) |
| N-05 ✅ (P16 T3) | 🟠 Yüksek | Aynı oluşuma ikinci istisna → kalıcı 500 | `RecurrenceExpander.cs:164` (`ToDictionary`) · `SchedulingDbContext.cs:76` (unique olmayan index) | Ertelenmiş oluşum tekrar ertelenir/iptal edilirse ikinci istisna yazılır; sonrasında öğretmen ders listesi, dashboard özeti, öğrenci takvimi `ArgumentException` → **500**. (Mobil scope göndermediği için bugün yalnız API'den tetiklenir; N-01 düzeltilince mobilden de tetiklenecek) | Unique index + upsert | ◐ |
| N-06 | 🟠 Yüksek | Hatırlatmalar erteleme/silme/tekrarı izlemiyor | `Notifications/.../LessonScheduleNotificationIntegrationEventHandler.cs:33-37,55-61,69` · `LessonScheduleFeatures.cs:454` | Ders 15:00→18:00 ertelenince hatırlatma 14:45'te eski saatle gider, 17:45'te gitmez. Silinen ders için hatırlatma gider. Haftalık seride yalnız ilk derse hatırlatma var. Metin UTC ISO → kullanıcı 3 saat geri saat görür | Oluşum bazlı hatırlatma; `Rescheduled`/`Deleted` olaylarını işle; saati `Europe/Istanbul`'da biçimle | ✅ (P17 T6) |
| N-07 ✅ (P16 T4) | 🟠 Yüksek | Çakışma kontrolü tekrarı görmüyor, yarışa açık | `LessonScheduleRepository.cs:21-29` · `LessonScheduleFeatures.cs:156-166` · `TimeOffFeatures.cs:73-76` | Eylül'de başlamış haftalık Salı 17:00 serisi varken Kasım'da Salı 17:00'ye yeni ders açılabilir (yalnız serinin ilk satırı karşılaştırılıyor). İki eşzamanlı POST ikisi de geçer. Tatil bloğu ders açmayı engellemiyor | Oluşumları genişleterek kontrol; öğretmen başına advisory lock veya `tstzrange` + `EXCLUDE` | ◐ |
| N-08 ✅ (P15 T2, artığı T9) | 🟠 Yüksek | Yazma uçlarında öğretmen–öğrenci bağı kontrol edilmiyor | `LessonSchedulePolicies.cs:59-62` (yalnız `CanManageTeacher`) · `PaymentPolicies.cs:113-116` · `LessonSessionPolicies.cs:55-58` | Bağı arşivlenmiş eski öğretmen öğrencinin takvimine ders, velinin paneline **borç kaydı** ekleyebilir. Manuel oturuma başka öğretmenin `LessonScheduleId`'si yazılabilir → asıl öğretmenin `from-schedule` isteği o yabancı oturumu döndürür | Üç authorizer'a `IsLinkedTeacherOfStudentAsync`; `LessonScheduleId` sahipliği doğrulansın. **✅ P15 T9:** authorizer'a tabi olmayan otomatik ders ücreti de yaşayan bağ ister (`GetAgreedLessonRateAsync` → `Private` değilse `null`); `link-parent` `Private` ister; arşivin anlamı m03 kural 21 | ✔ (scheduling) / ◐ (diğer) |
| N-09 ✅ (P18 T4; backend T1/T2) | 🟠 Yüksek | Ödeme kaydında lost update ve çift sayım | `Payments/Application/PaymentFeatures.cs:188-201` · `PaymentsDomainModel.cs:112-152` · `Reporting/.../ReportingProjectionHandlers.cs:190` · mobil `payments_cubit.dart:226-258` | Veli beyanı onaylanır (Paid, 500). Öğretmenin eski ekranından gelen tam PUT tahsilatı 0'a, durumu `Pending`'e çeker → rapora −500. Eşzamanlı çift onay iki kez +500 → aylık tahsilat ikiye katlanır. Mobil yeni toplamı istemcide hesaplayıp tam PUT ile yazıyor | `xmin` concurrency token + niyete dayalı komutlar ("tahsilat ekle") | ✔ (token yok) / ❔ (yarış) |
| N-10 | 🟡 Orta · ✅ (P18 T2) | Ödeme durum–tutar tutarsızlığı | `PaymentPolicies.cs:31-46` · `PaymentFeatures.cs:286-288` | `Paid` + tahsilat 0 kabul ediliyor (kayıt "gecikmiş" görünür); `Pending` + tam tahsilat kabul ediliyor; özet iptal edilmiş kayıtları da topluyor; iade kavramı yok | Durumu tutarlardan türet | ◐ |
| N-11 ✅ (P16 T5 gün/ay; P18 T3/T4 para birimi) | 🟡 Orta | "Bugün" ve ay sınırları UTC | `API.Host/TeacherDashboardEndpoints.cs:46,91-92` · `PaymentFeatures.cs:308,314` · `StudentCalendarQuery.cs:63-64` | Dashboard'da 00:00–03:00 (TR) dersleri "bugün" listesinde yok, ertesi günün 00–03 dersleri var. Ayın 1'i 00–03 vadeli ödeme önceki aya düşer. Gecikmiş ödeme toplamı farklı para birimlerini toplayıp ilkinin etiketiyle gösteriyor | Gün/ay sınırını `Europe/Istanbul`'da hesapla; toplamları para birimi bazında dön | ◐ |
| N-12 ✅ (P17 T3) | 🟡 Orta | Öğrenci/veli oturum listesi hep boş, detay hep 403 | `LessonSessionFeatures.cs:106-113` (`studentFilter = currentUserId`) · `LessonSessionPolicies.cs:105,133` | `LessonSession.StudentId` = öğrenci **profil** kimliği (`StudentProfileFeatures.cs:180` yeni Guid), filtre **kullanıcı** kimliğiyle karşılaştırıyor | `IStudentDirectory` ile kullanıcı → profil kimlikleri çözülsün; veliye onaylı bağ kontrolü | ✔ |
| N-13 ✅ (P17 T3) | 🟡 Orta | Oturum çift sayımı | `LessonSessionsDomainModel.cs:185-207` · `ProgressTrackingDomainModel.cs:93` · `LessonSessionsDbContext.cs:51` | Yeniden tamamlama her seferinde olay yayar → `AttendedLessonCount` +1; `Absent` ders de katılım sayılır. `LessonScheduleId` index'i unique değil → çift dokunuş iki oturum + iki ücret | Unique index; tüketicide `LessonSessionId` bazlı idempotency | ◐ |
| N-14 ✅ (P16 T3) | 🟡 Orta | Pencere dışına ertelenen oluşum kayboluyor | `RecurrenceExpander.cs:166-189` | Pazartesi dersi gelecek haftanın Salı'sına ertelenince iki haftalık görünümün hiçbirinde görünmez | Override'ları pencereye göre ayrıca tara | ◐ |
| N-15 ✅ (P17 T5) | 🟡 Orta | "Ücretli iptal" bayrağı işlevsiz; iptal sonrası kayıtlar sahipsiz | `SchedulingDomainModel.cs:216` | `IsChargeable` tüketen yok → geç iptal ücreti hiç kayda dönüşmez. Plan iptal edilince türetilmiş oturum ve ödeme kaydı kalır | `LessonScheduleCancelled` Payments + LessonSessions'ta işlensin | ✅ (P17 T5) — politika + oluşum borcu + bekleyen oturum/ücret/hatırlatma temizliği |
| N-16 ✅ (P18 T3 beyan; P17 T5 diğer) | ⚪ Düşük | Küçükler | `LessonScheduleFeatures.cs:279-297,404-425,454` · `ParentPaymentDeclarationFeatures.cs:97-125` · `LessonChangeRequestFeatures.cs:89-121` | Silme olayı yayılmıyor (projeksiyon sapar) · iptal nedeni doğrulanmıyor (`0` saklanıyor) · aynı ödemeye birden çok bekleyen beyan · gelecekteki ders "tamamlandı" yapılabiliyor · aynı derse sınırsız erteleme talebi, kabul iki ayrı `SaveChanges` | — | ◐ — Scheduling/silme/gelecek tamamlama/talep atomikliği ✅ (P17 T5); veli beyanı tekliği ✅ (P18 T3) |

### 1.2 Kimlik · Erişim · Üyelik (güvenlik)

| ID | Şiddet | Bulgu | Konum | Senaryo → yanlış sonuç | Öneri | Güven |
|----|--------|-------|-------|------------------------|-------|-------|
| N-20 ✅ (P15 T1) | 🔴 Kritik | Davet kodları tahmin edilebilir, süresiz, hedefe bağlı değil | `Students/Domain/StudentsDomainModel.cs:363-364` (`Random.Shared.Next(0, 1_000_000)`) · `StudentParentInviteFeatures.cs:62` · `Parents/Application/ParentFeatures.cs:418-431` · `ParentPolicies.cs:97-99` (claim: yalnız `RequireSelfOrAdmin`) | 10⁶ olasılık, `StudentParentInvite`'ta son kullanma alanı yok, rol kontrolü yok. Veli kodunda isabet **doğrudan `Approved` birincil veli bağı** verir → çocuğun profil, ders, not, ödeme verisi. Öğretmen–öğrenci kodunda isabet → öğretmenin manuel profili saldırganın profiline **merge** edilir, geri alınamaz | ≥128 bit CSPRNG token (ya da kısa kod + 48 saat süre + hesap başına deneme limiti + hedef kullanıcıya bağlama + `Parent` rol kontrolü) | ✔ |
| N-21 ✅ (P15 T2) | 🟠 Yüksek | Bağı olmayan öğretmen veli daveti üretebiliyor | `StudentParentInviteFeatures.cs:85-101` (authorizer: yalnız `Teacher` rolü + `TeacherUserId == ben`) · `:53-58` (handler yalnız profilin varlığına bakıyor) | Öğrenci profil ID'sini bilen herhangi bir öğretmen hesabı (rol kayıtta serbest seçiliyor) davet üretir, ikinci (veli) hesabıyla claim eder → birincil veli olur, `StudentProfile.ParentUserId` kendisine yazılır | Authorizer'a `IsLinkedTeacherOfStudent`; claim için `Parent` rolü; üreten kendi kodunu kullanamasın | ✔ |
| N-22 ✅ (P15 T3) | 🟠 Yüksek | Davet kabul/ret uçlarında durum ve kod kontrolü yok | `TeacherStudentLinkFeatures.cs:137-152,167-178,333-335` · `StudentsDomainModel.cs:353-360,366-370` | Hedefsiz bir bağı link ID'sini bilen `accept` edebilir (`Accept()` durum kontrolü yapmıyor), profilin `UserId`'si ona yazılır. `MarkInviteSent` zaten `Linked` olan bağı tekrar `InviteSent` yapabiliyor → ikinci claim mevcut sahibin üstüne yazar | Kabul yalnız `InviteSent`'te, kodla, hedef kullanıcıya bağlı; `Linked` yeniden davet edilemesin | ✔ (Accept) / ◐ |
| N-23 ✅ (P15 T3) | 🟠 Yüksek | Öğretmen yeni profili rastgele bir kullanıcıya bağlayabiliyor | `StudentsModule.cs:448-468` · `StudentProfilePolicies.cs:43-49` · `StudentProfileFeatures.cs:179-195` | `TeacherManaged` profilde `UserId`/`ParentUserId` istek gövdesinden alınıyor, doğrulanmıyor. Öğretmen bir öğrenci kullanıcısının ID'sini yazarsa o kullanıcının kanonik profili öğretmeninki olur, kendi profilini açamaz (`DuplicateUserProfile`) | `TeacherManaged`'da bu alanları sunucuda null'a zorla | ◐ |
| N-24 ✅ (P15 T6) | 🟠 Yüksek | Üretimde şifre sıfırlama kodları log'a yazılıyor | `render.yaml` (üretim servisinde `Email__Provider` yok) · `appsettings.json:15` (`Logging`) · `EmailOptionsGuard.cs:15-17` (her ortamda kabul) · `LoggingEmailSender.cs:13-17` (gövdeyi `Information` seviyesinde basıyor) | Blueprint'e göre (Render panelinde elle ayarlanmadıysa): log'a erişebilen herkes istediği hesabın şifresini sıfırlayabilir; kullanıcılar e-posta hiç almaz. Asıl bulgu: **bunu engelleyen guard yok**, `Program.cs` yalnız uyarı basıyor | Development/Staging dışında `Logging`/`Local` sağlayıcıyı **fail-fast** reddet; gövdeyi maskele | ✔ (blueprint + guard) |
| N-25 | 🟠 Yüksek | Referans ödülü sınırsız, deneme yeni hesapla tekrarlanabiliyor | `Membership/Application/MembershipFeatures.cs:677-710` · `MembershipDomainModel.cs:203` | Yalnız "kendi kodun olamaz" kontrolü var; davet edenin ödülüne tavan yok, e-posta doğrulaması zorunlu değil (A-19) → N sahte hesap = N ay premium; A↔B karşılıklı kullanım serbest. `HasUsedTrial` kullanıcı ID'sine bağlı; silinen hesabın e-postasıyla yeniden kayıt yeni deneme verir | Ödülü doğrulanmış e-posta + ödeme yapan davetliye bağla, tavan koy | ◐ |
| N-26 ✅ (P15 T4) | 🟡 Orta | Veli bağı iptal/ret edilse de profil erişimi sürüyor | `StudentProfilePolicies.cs:284-287` · `ParentChildLinkApprovedIntegrationEventHandler.cs:30-46` · `ParentsDomainModel.cs:207-212` | `ParentUserId` yalnız onay olayında yazılıyor, Revoked/Rejected tüketilmiyor → iptal edilen veli iletişim/doğum tarihi/hedefleri okumaya devam eder. `Approve()` iptal edilmiş bağı yeniden onaylayabiliyor | Erişimi `IParentAccessDirectory` üzerinden yap veya iptalde `ParentUserId`'yi temizle | ◐ |
| N-27 ✅ (P15 T4) | 🟡 Orta | Davetle bağlanan öğrencinin gizlilik ayarı veli panelinde yok sayılıyor | `ParentReadModelProjections.cs:190-208` · `ParentFeatures.cs:546-555` | `KnownStudent.UserId` yalnız `StudentProfileCreated` ile doluyor; claim/accept sonrası `LinkUser` olay yaymıyor → öğrenci "Gizli" seçse de veli tam veriyi görür; öğrenci veli taleplerini onaylayamaz | `LinkUser` + merge olay yaysın; `UserId` bilinmiyorsa gizlilik `Hidden` (fail-closed) | ◐ |
| N-28 ✅ (P15 T3) | 🟡 Orta | Merge yarım: bağlar eski öğrenci ID'sinde kalıyor | `TeacherStudentLinkFeatures.cs:224-229` | `TeacherStudentLink`, `ParentChildLink`, `KnownStudent`, ProgressTracking ve bekleyen veli davetleri eski `StudentId`'de kalır → kanonik profilde öğretmen erişimi düşer, veli paneli boşalır | Students bağları aynı transaction'da taşısın; Parents + ProgressTracking tüketici eklensin | ◐ |
| N-29 ✅ (P15 T7) | 🟡 Orta | Refresh token "yeniden kullanım" tespiti meşru iptalleri hırsızlık sayıyor | `Identity/Application/IdentityFeatures.cs:176-182` · `DeviceSessionFeatures.cs:130-134,189-194` | "Diğer oturumları kapat" açıkken telefonla giriş → eski tablet refresh yapınca "reuse" sayılır ve **telefonun yeni oturumu da öldürülür** | İptal nedenini sakla; tespiti yalnız rotasyonla eskiyen token'da tetikle | ◐ |
| N-30 ✅ (P15 T7) | 🟡 Orta | Eşzamanlı iki refresh iki geçerli zincir üretiyor | `IdentityFeatures.cs:184-194` · `IdentityDbContext.cs:89-104` | Çalınan token'la yarışan saldırgan tespiti atlatır. Projede concurrency token **0** (`grep` ile doğrulandı) | `UPDATE … WHERE revoked_on_utc IS NULL` + etkilenen satır = 1 kontrolü | ✔ (token yok) / ◐ |
| N-31 ✅ (P15 T7) | 🟡 Orta | Oturum iptali/hesap silme sonrası access token 60 dk geçerli | `API.Host/Program.cs:115-132` (`OnTokenValidated` `sid`/hesap durumu bakmıyor) | Çalınan telefonun oturumu kapatılsa da, KVKK silmesi tamamlansa da token bir saat veri yazabilir | `sid` bazlı kara liste veya bu akışlarda `RevokeAllAccessTokensAsync` | ◐ |
| N-32 ◐ (P15 T4, T7) | ⚪ Düşük | Toplu | `IdentityFeatures.cs:93,126-131` · `RedisTokenBlacklist.cs:111` · `IdentityDbContext.cs:125` · `DistributedRateLimitMiddleware.cs:46` · `ParentsDbContext.cs:64` | Kayıtta `duplicate_email` + girişte zamanlama farkı (kullanıcı numaralandırma) · parola politikası yalnız "≥8 karakter" · iptal damgası saniye çözünürlüğünde · bekleyen silme talebi index'i unique değil (yorum "unique" diyor) → çift talep · IP başına 10/dk sınır `/me`/`/refresh`'i de kapsıyor (okul NAT'ı arkasında kilitlenme) · birincil veli için unique/kısmi index yok (✅ P15 T4 — `IX_parent_child_links_StudentId_primary`) · HSTS yok. **◐ P15 T7:** ✅ girişte zamanlama farkı (yoksa da özet doğrulanır), ✅ iptal damgası ms (`iat_ms`), ✅ bekleyen silme talebi kısmi benzersiz, ✅ `/me`/`/sessions` kullanıcı başına + `/refresh` ayrı IP kovası, ✅ HSTS; **açık → devredildi (P15 T9):** kayıtta `duplicate_email` ile numaralandırma → **P21** (mobil kayıt akışı + backend; doğrulama e-postasıyla "zaten kayıtlıysanız giriş yapın" ürün kararı ister), parola politikası ≥8 (uzunluk + sızdırılmış parola listesi) → **P26** hijyen | — | ◐ (P15 kapsamı bitti) |

### 1.3 Çalışma · Ödev · Gelişim · Raporlama · Bildirim · Mesajlaşma · Altyapı

> Doğru çalışanlar (yeniden bakılmasın): deneme neti `D − Y/bölen` (LGS 3, diğerleri 4, okul denemesi cezasız), sıfıra bölme korumaları, net oranı 0–1 kırpması.

| ID | Şiddet | Bulgu | Konum | Senaryo → yanlış sonuç | Öneri | Güven |
|----|--------|-------|-------|------------------------|-------|-------|
| N-40 | ✅ (P19 T1) | Tamamlanan seans seri (streak) toplamına girmiyor | `Study/Application/StudySessionFeatures.cs:162-178` (`ListCompletedSessionsAsync` `SaveChanges`'tan **önce** DB'yi okuyor) · `:284-285` (manuel seans: `AddSessionAsync` → `RecordCompletedAsync`) | Seans DB'de hâlâ `Running` ya da hiç yok → günlük toplam yalnız önceki seanslar. **Günde tek 90 dk seans yapan öğrencinin serisi hiç başlamaz**; eşiği geçiren seans değil, sonraki sayılır. Birim testi sahte depoda `Seed(session)` ile hatayı gizliyor (`tests/Unit/StudyStreakTests.cs:29,43`). Ek: gün sınırı 04:00 (`StudyPolicies.cs:22`) ama pencere 00–24; geçmiş güne her manuel giriş `TotalStudyDays`'i tekrar artırıyor; kırılan seri okumada sıfırlanmıyor (10 gün önce bırakan "15 gün" görür) | Toplama mevcut seansı ekle; pencereyi 04:00'e hizala; günleri tekilleştir; okumada `last < bugün−1` → 0 | ✔ |
| N-41 ✅ (P15 T8) | 🟠 Yüksek | İş kirası sahibince yenilenemiyor (A-07 ✅ ama bozuk) | `Shared/Infrastructure/.../JobLeaseStore.cs:56-62` (`WHERE ExpiresOnUtc <= now`, sahip kontrolü yok) · `LeasedBackgroundService.cs:37-44` (kira ≥ 5 dk) | Aynı instance bir sonraki turda kendi kirasına takılır → 30 sn'lik işler ~5 dk'da bir koşar: **şifre sıfırlama/doğrulama e-postası ~5 dk gecikir**, push/hatırlatma 5 dk'da bir, saatlik tarayıcılar 2 saatte bir, günlük snapshot 48 saatte bir. `RealJobLeaseTests.cs:35-36` bu davranışı doğru kabul edip kilitliyor | `WHERE ExpiresOnUtc <= now OR OwnerId = EXCLUDED.OwnerId` veya tur sonunda kirayı bırak | ✔ |
| N-42 ✅ (P19 T2) | 🟠 Yüksek | Outbox: bir handler'ın hatası diğer modülleri aç bırakıyor | `Shared/Infrastructure/Messaging/DispatchingEventBus.cs:39-49` (sıralı `foreach`, ilk istisnada kesilir) · `EfOutboxStore.cs:148-161,193-202` | `LessonSessionCompleted` 4 modüle gider; Payments kalıcı hata verirse Reporting'e sıra gelmez; 5 denemeden sonra dead-letter → diğer modüller olayı **hiç** almaz. Ayrıca `return false` "yeniden denenir" diye yorumlanmış (`ProjectionHandler.cs:28-33`, `MessageSentNotificationHandler.cs:47-50`) ama mesaj Processed işaretleniyor → bozuk payload sessizce düşüyor | ✅ Her handler denenir, hatalar adlandırılmış AggregateException olur. Başarılı tüketici inbox ile tekrar uygulanmaz; bozuk yük PoisonMessageException, geçici eksiklik istisna, geçerli etkisiz sonuç başarılı inbox kaydı. Rollback sonrası tracker temizliği; Error tüketici adı/türü. Gerçek PG: dört tüketici, ikinci hata, SQL/staged rollback, tekrar yalnız başarısız tüketici, toparlanma ve dead-letter | ✅ 19/1242/183, başarısız0/atlanan0 |
| N-43 ✅ (P16 T5) | 🟠 Yüksek | Boş zaman analizi yerel uygunluk saatlerini UTC sayıyor | `Reporting/.../FreeSlotCalculator.cs:43-49` | "Pzt 18:00–21:00" şablonu 18:00Z olarak yayılır, dolu aralıklar gerçek UTC → sonuç 3 saat kayık | Slotu İstanbul gününde kur, sonra UTC'ye çevir | ◐ |
| N-44 | 🟠 Yüksek | Ödev durum makinesinde koruma yok | `Assignments/Domain/AssignmentsDomainModel.cs:109-127,153-166` · `AssignmentStudentFeatures.cs:117-142` | Onaylanıp puanlanmış ödeve öğrenci yeni dosya yükleyebilir; **puanlanan eski dosya depodan silinir**, durum `Approved` kalır. `MarkCompleted` `Approved/Cancelled/ReturnedForRevision`'ı ezer (revizyon atlanır). Hiç teslim edilmemiş ödev onaylanabilir. Revizyona dönen ödevde eski `Score` kalır. Teslim anı ve gecikme tutulmuyor | Geçişleri domain'de `Result` ile kısıtla; `SubmittedOnUtc`/`IsLate` ekle | ✅ (P19 T3) |
| N-45 | 🟠 Yüksek | Son tarih tarayıcısında açlık ve yanlış "teslim edilmedi" | `Assignments/Infrastructure/AssignmentDueScanner.cs:66-74` · `AssignmentsDomainModel.cs:203-229` | 24 saatlik "az kaldı" penceresini kaçıran bekleyen ödevde `DueSoonNotifiedOnUtc` hiç dolmaz → `OrderBy(DueDate).Take(200)` hep bu eski satırları alır; 200 biriktiğinde **yeni ödevlere hiç uyarı gitmez**. Zamanında teslim edilip son tarihten sonra revizyona dönen ödev için öğrenci+veliye "teslim edilmedi" gider | Koşul `(DueSoon==null && Due>now) \|\| Missed==null`; "Missed"'i yalnız hiç teslim yoksa üret | ◐ |
| N-46 ✅ (P15 T5) | 🟠 Yüksek | Mesajlaşmada ilişki yalnız sohbet açılırken kontrol ediliyor | `Messaging/Application/MessagingFeatures.cs:333-339,479-492` · `MessagingPolicies.cs:146-147` | Bağı iptal edilen veli / bağı biten öğrenci yazmaya ve geçmişi okumaya devam eder | Gönderme/listelemede `EnsureAllowedAsync` veya bağ iptalinde sohbeti salt-okunur yap | ◐ |
| N-47 | 🟡 Orta | Düzenleme/silme olay üretmiyor | `StudyDomainModel.cs:248-265,381-422,500-503` · `StudyTestFeatures.cs:154-204` · `StudySessionFeatures.cs:486-505` | Silinen/düzeltilen test ve seans `TopicMastery`'de, Reporting projeksiyonlarında ve `MockExam.TotalNet`'te kalır (`ReportingProjections.cs:169-171`'deki yorum yanlış) | `Edited/Deleted` olayları + tüketicide yeniden hesap | ◐ |
| N-48 | 🟡 Orta | Olay sırası bozulunca hayalet kayıt / ters trend | `TeacherDashboardProjectionHandlers.cs:79-117` · `ProgressTrackingDomainModel.cs:74-84` | `Scheduled` backoff'a düşüp `Cancelled` önce işlenirse, sonra gelen `Scheduled` iptal edilmiş dersi panele ve hatırlatmalara geri ekler. Trend `TakenOnUtc`'ye değil işlenme sırasına göre → geriye dönük girilen eski test trendi tersine çevirir | İptal için tombstone/sürüm; trendi tarihe göre hesapla | ◐ İptal sırası ✅ (P17 T7); trend ayağı P19’da açık |
| N-49 | 🟡 Orta | Öğretmen panelinde "bugünkü ders" yanlış | `TeacherDashboardProjectionHandlers.cs:282` · `TeacherDashboardProjection.cs:29-33,51-59` | Payload'da RRULE yok → tekrar eden ders yalnız ilk tarihinde sayılır; gün UTC; geçmiş ders satırları hiç budanmıyor, her istekte tümü yükleniyor | Oluşum bazlı satır, yerel gün, budama | ✅ (P17 T7) — oluşum anahtarı, yerel gün, kayan pencere ve günlük budama |
| N-50 ✅ (P16 T5) | 🟡 Orta | Saat dilimi tutarsızlığı: Study yerel, diğerleri UTC | `ReportingProjections.cs:13-16` · `ProgressSnapshotService.cs:62` · `StudentReportFeatures.cs:326` · `AssignmentDueNotificationHandlers.cs:56` · `PaymentOverdueNotificationHandler.cs:54` | Pazartesi 01:30 seansı Study özetinde bu haftaya, Reporting grafiğinde geçen haftaya düşer. Bildirimde 23:59 son teslim **"20:59"** görünür | `StudyLocalTime`'ı Shared'a taşı, tüm kovaları/metinleri ona bağla | ◐ |
| N-51 | 🟡 Orta | Push kuyruğu tıkanabiliyor, bildirimler bayatlıyor | `UserNotificationRepository.cs:44` · `NotificationDispatching.cs:170-218` | Geçici hatalı bildirimde deneme sayacı/son kullanma yok; hep en eski 100 seçilir → takılırsa yeni push gitmez; FCM düzelince saatler önceki "ders başlıyor" gider; tur başına ~200 sorgu (N+1) | `PushAttempts`/`ExpiresOnUtc`; tercih/token'ları toplu oku | ◐ |
| N-52 | 🟡 Orta | Silinen mesajın metni bildirimde kalıyor | `MessageSentNotificationHandler.cs:53-58` | `c16d25b` yalnız sohbet önizlemesini temizledi; `UserNotification.Body` mesaj metnini taşımaya devam ediyor | Gövdede sabit metin veya silmede bildirimi temizle | ◐ |
| N-53 | 🟡 Orta | Haftalık veli özeti yalnız ilk çocuk için | `ParentWeeklySummaryService.cs:64-67,79` | İki çocuklu veli ikinci çocuğun özetini hiç almaz; tablo sayfalamasız okunuyor | `(veli, öğrenci)` bazında üret | ◐ |
| N-54 | 🟡 Orta | Sayfalama/indeks eksikleri | `AssignmentRepository.cs:35-61` · `StudyRepository.cs:31-53,96-122` · `NotificationsDbContext.cs:55` | Ödev ve premium seans/test/not listeleri tüm geçmişi döndürüyor; hatırlatma sorgusu `TeacherUserId` önekli indeksi kullanamıyor → 30 sn'de bir tam tablo taraması | Keyset sayfalama; `(Status, RemindAtUtc)` kısmi indeks | ◐ |
| N-55 ◐ (idempotency ✅ (P18 T5)) | ⚪ Düşük | Toplu | `ExamPenalty.cs:5,25` · `MessagingFeatures.cs:540-546` · `IdempotencyMiddleware.cs:34-41` | YDS'de yanlış doğruyu götürmez ama bölen 4 uygulanıyor · okundu işareti ekranda görülmemiş yeni mesajı da okundu sayıyor · ~~idempotency anahtarı gövdeyi karşılaştırmıyor~~ → **✅ (P18 T5):** ham SHA-256 processing/completed kaydında; farklı gövde422, aynı gövde ilk yanıt/409, gerçek Redis ve tahsilat HTTP kanıtlı · `TopicGoal.TargetDate` geçse de hedef `Missed` olmuyor | — | ◐ |

### 1.4 Mobil (Flutter)

| ID | Şiddet | Bulgu | Konum | Senaryo → yanlış sonuç | Öneri | Güven |
|----|--------|-------|-------|------------------------|-------|-------|
| N-60 | 🟠 Yüksek | Öğretmen takviminde üretimde sahte etkinlikler | `scheduling/presentation/pages/scheduling_page.dart:43,2023-2056` (`_seedEvents`, mock bayrağından bağımsız) | Her öğretmen bu hafta Perşembe "Tatil / Musait Degil" (tüm gün), "Ödev Hatırlatması" (`student-2`), "Ödeme Hatırlatması" (`student-1`) görür | Seed'i sil; gerçek tatil bloğu ucuna bağla | ✔ |
| N-61 | 🟠 Yüksek | Öğrenci/veli davet formu sahte başarı veriyor | `students/presentation/pages/students_page.dart:508-517` | "Davet Gönder" API çağırmadan "öğrenci daveti hazırlandı" deyip kapanır | Backend'deki `/invite` + `/parent-invite`'a bağla veya gizle | ✔ |
| N-62 | 🟠 Yüksek | Veli mesajlara, bildirimlere, ayarlara giremiyor | `core/routing/app_router.dart:135-138` (`isParent && !onParentArea → /parent`) · `parent_home_page.dart:50` | Veli "Mesajlar"a basınca ana sayfaya atılır. Aynı kural `/notifications`, `/settings/*`, `/account/sessions`, `/membership`'i de kapatır; `/messages/{id}` push'u da ana sayfaya düşer | Rol başına izinli ortak rota listesi | ✔ |
| N-63 ✅ (P15 T7) | 🟠 Yüksek | Ağ hatasında oturum siliniyor | `core/network/token_refresh_interceptor.dart:38-45` · `auth_repository_impl.dart:165` | Refresh'teki timeout/çevrimdışılık da `_onUnauthorized()`'a gider. Token 60 dk → bir saat sonra metroda uygulamayı açan kullanıcı açıklamasız çıkışa atılır | Yalnız refresh 400/401 dönerse çıkış; ağ hatasını ekrana ilet | ✔ |
| N-64 ✅ (P18 T4) | 🟠 Yüksek | Para `double`, ayrıştırma hatalı | `payments/.../payment_form_page.dart:361-362,474` · `collect_payment_sheet.dart:192` | "750,50" reddedilir; "1.500" **1,5 TL** olarak kaydedilir | TR `NumberFormat` ile ayrıştır, kuruş (`int`) taşı | ◐ |
| N-65 ✅ (P18 T4) | 🟠 Yüksek | `Idempotency-Key` hiç gönderilmiyor | `mobile/lib` içinde 0 eşleşme (backend'de `IdempotencyMiddleware` var) | Ödeme oluşturma 30 sn zaman aşımına düşer, kullanıcı tekrar basar → çift kayıt. Sohbette "tekrar dene" çift mesaj | create/collect/send çağrılarına UUID anahtar | ◐ |
| N-66 | 🟠 Yüksek | `account_info_page` sahte, hesap silme yok | `more/.../account_info_page.dart:21,64-98,120-146,664-668` | "Son değişiklik 18 gün önce", "İki aşamalı: Kapalı", "Üyelik tarihi 12 Mayıs 2026" herkese aynı; "Hesabı kapat" → "Bu prototipte işlem yapılmaz". `deletion-request` mobilde hiç çağrılmıyor → **App Store 5.1.1(v) / Play hesap silme kuralı** | Sahte satırları kaldır, silme ucuna bağla | ✔ (silme yok) / ◐ |
| N-67 ✅ (P15 T7) | 🟡 Orta | Eşzamanlı 401'de çoklu refresh | `token_refresh_interceptor.dart:33` | `QueuedInterceptor` sıraya koyuyor ama kuyruktaki her 401 yeniden refresh ediyor → dashboard'ın 5 paralel çağrısı 5 rotasyon; N-29 ile birleşince oturum düşmesi riski | İsteğin token'ı depodakinden farklıysa refresh etmeden yeni token'la tekrar dene | ◐ |
| N-68 | 🟡 Orta | Hata durumunda eski önbellek sessizce gösteriliyor | `payment_repository_impl.dart:59-63,82-86` · `student_repository_impl.dart:209-215` | 403/500 dahil her hatada önbellek dönülüyor; "eski veri" işareti yok | Yalnız ağ hatasında önbelleğe düş + banner | ◐ |
| N-69 | 🟡 Orta | Takvim önbellek anahtarı `DateTime.now()` içeriyor | `scheduling_repository_impl.dart:197-203,233-239` | Her çağrı yeni SharedPreferences anahtarı yazar → çevrimdışı yedek hiç isabet etmez, depolama sınırsız büyür | Anahtarı güne yuvarla | ◐ |
| N-70 | 🟡 Orta | Çıkış eksik temizliyor | `auth_repository_impl.dart:60-63` · `app.dart:105-113` | Öğrenci adı/telefon/tutar düz SharedPreferences'ta kalıyor (KVKK); `/api/identity/logout` çağrılmıyor → refresh token sunucuda geçerli; kök Notifications/Membership cubit'leri sıfırlanmıyor | Sunucu logout + tüm önbellek + kök cubit sıfırlama | ◐ |
| N-71 | 🟡 Orta | Push rotaları uygulamada yok; ön planda push yok | Backend `/parent/assignments`, `/parent/payments` gönderiyor (`ParentEventNotificationHandler.cs:114-116`); router'da yok, `errorBuilder` yok · `firebase_push_binding.dart:73-90` (`onMessage` yok) · `chat_cubit.dart` (polling/soket yok) | Push'a dokunan veli hata ekranı görür; uygulama açıkken gelen bildirim kaybolur; sohbette karşı tarafın yanıtı görünmez | Rotalar + `errorBuilder` + `onMessage` ile cubit tazeleme | ◐ |
| N-72 | ⚪ Düşük | Toplu | çeşitli | 10 cubit'te dispose sonrası `emit` (`isClosed` yok) · bildirim merkezinde sayfalama yok (ilk 20) · gönderim zaman aşımı 15 sn (dosya yükleme kesilir) · `api_exception.dart:34,51-52`'de ASCII Türkçe ("hatasi olustu") — D-20 ✅ ama en sık görülen hata metni bozuk | — | ◐ |

### 1.5 Çapraz kesit: eşzamanlılık ✅ (P18 T1)

**İlk denetim kanıtı (2026-11-13):** `src/` içinde `IsRowVersion` / `IsConcurrencyToken` / `UseXminAsConcurrencyToken` / `[Timestamp]` / `[ConcurrencyCheck]` **sıfırdı** ✔; son yazan kazanıyordu. N-09 (ödeme), N-30 (refresh), N-25 (referans) somut sonuçlarıydı.

**✅ (P18 T1, 2026-11-15):** `PaymentRecord`, `UserSubscription`, `StudyStreak`, `StudyTopic`, `TopicMastery`, `Assignment`, `LessonSchedule`, `TeacherStudentLink`, `StudentProfile`, `ParentChildLink` `IVersioned` uygular. Ortak konvansiyon `uint Version` → `xmin/xid` + concurrency/OnAddOrUpdate eşler; sekiz migration yalnız metadata, Up/Down boş. Kaybeden sunucu yazması `409 shared.concurrency_conflict` döner. Gerçek PostgreSQL iki context/iki HTTP isteği yarışını, kaybeden outbox rollback’ini ve tüketicinin güncel scope ile tek etkili retry’sini kanıtladı. **N-09 backend ✅ (P18 T2):** eski ekranın PUT’u tahsilata/duruma dokunmaz; yeni tahsilat/iade hareket kimliğiyle raporda tekilleşir, paralel yazma kaybedeni 409 alır ve çocuk/outbox geri alınır. P18 T4 ile mobil yeni tahsilat/iptal komutlarını kullanır; PUT yalnız ayrıntı gönderir; If-Match bu adımda eklenmedi. N-30 zaten P15 T7’de koşullu rotasyonla kapandı; N-25 P28 kapsamıdır.

### 1.6 ✅ işaretli ama kodda hâlâ eksik/bozuk

| Kaynak madde | Durum | Gerçek |
|--------------|-------|--------|
| A-15 (refresh reuse tespiti) | ✅ | Meşru iptalleri de hırsızlık sayıyor → N-29 → **✅ (P15 T7)**: iptal nedeni saklanır, tespit yalnız rotasyonla eskiyen token'da |
| A-06 (token iptali) | ✅ | Cihaz iptali, M15-3, KVKK tamamlanması access token'ı iptal etmiyor → N-31 → **✅ (P15 T7)**: `sid` kara listesi + hesap durumu kontrolü |
| A-07 (çok-instance kirası) | ✅ | Kira sahibince yenilenemiyor → işler ~5 dk'da bir koşuyor (N-41); ayrıca `AccountDeletionService` ve `DataRetentionService` düz `BackgroundService`, kirasız (`AccountDeletionService.cs:170`) ◐ → **✅ (P15 T8)**: sahip yeniler, iki servis kiraya alındı |
| M15-3 (diğer oturumları kapat) | ✅ | A-15 ile birleşince yeni oturumu da öldürüyor → N-29 → **✅ (P15 T7)** |
| M03-1 (veli bağ onayı) | ✅ | Davetle bağlanan öğrencide fiilen yalnız Admin onaylayabiliyor → N-27 (✅ backend P15 T4); mobilde onay ekranı hiç yok (P21 T2) |
| D-08 (mesajlaşma, üç rol) | ✅ | Veli `/messages`'a giremiyor → N-62 |
| D-14 (hesap bilgileri) | ✅ | `account_info_page` sahte veri → N-66 |
| D-20 (Türkçe yazım 188→0) | ✅ | `api_exception.dart` ASCII Türkçe, `localization_test` yakalamıyor → N-72 |
| E-07 (KVKK hesap silme) | ✅ | Backend var, mobilde arayüz yok → N-66 |
| B-06 (öğretmen davet) `roles/ogretmen.md` | ✅ | Mobil form sahte → N-61 |
| K2 filtresi (oturum listesi) | ✅ | Öğrenci/veli için yanlış kimlik → N-12 |
| `AuthorizationCoverageTests` | yeşil | Yalnız authorizer'ın **varlığına** bakıyor, sahiplik kontrolüne bakmıyor; N-21/N-22'deki authorizer'lar testi geçiyor; integration event handler'larını ve modüller arası yazma portlarını kapsamıyor; yüklenemeyen assembly'yi sessizce atlıyor (`:123`) → **✅ (P15 T9)**: yüklenemeyen/eksik/kısmen yüklenen assembly ve host'a kayıtlı ama taranmamış modül artık **test hatası**; sahiplik `RealCrossTenantAccessTests` (iki öğretmen/öğrenci/veli, gerçek Postgres: yazamaz + okuyamaz, iptal edilen veli göremez) ile; event handler/port kuralı senaryo testlerinde (otomatik ücret: aynı paket). Kapsama testinin kendisi bilinçli olarak varlık denetimi kalır (sınırı sınıf açıklamasında) |

---

## 2. Kullanıcıya değer katacak özellikler (rol bazlı)

> **Ana tespit:** En büyük açık "yeni özellik" değil, **backend'de hazır olan akışların mobilde karşılığının olmaması**. Aşağıdaki "S" eforlu maddelerin çoğu yalnız ekran işi.

### 2.1 Öğretmen

| # | Özellik | Neden | Kod durumu | Efor |
|---|---------|-------|------------|------|
| 1 | Davetin gerçekten çalışması (öğrenci + veli) | Öğrenci/veli kazanımının tek kanalı | Backend var (`/invite`, `/links/claim`, `/parent-invite`), mobil sahte (N-61) | S |
| 2 | Oluşum bazlı ders + iptal politikası ("X saatten az kala iptal ücretli") | Özel ders öğretmeninin en çok yaşadığı anlaşmazlık | `IsChargeable` alanı var, tüketen yok (N-15) | M |
| 3 | Tatil bloğu, ders erteleme, öğrenci erteleme talebine kabul/ret | Takvimin günlük gerçekliği | Backend var, mobil yok | M |
| 4 | Velinin "ödedim" beyanını onay/ret, öğrenci bazlı ücret, arşivden çıkarma | Ödeme mutabakatı yarım | Backend var, mobil yok | M |
| 5 | Tahsilat hareketleri (kısmi ödeme geçmişi, iade, makbuz) + paket/peşin ders kredisi | "10 derslik paket" Türkiye'de yaygın satış modeli | Yok | M |
| 6 | Bire-çok duyuru + veliye özet paylaşımı (`share_plus`) | Öğretmeni WhatsApp grubuna kaçıran boşluk | Yok | S–M |
| 7 | iCal / Google Takvim aboneliği | Öğretmen kendi takvimini kullanmaya devam eder | Yok | S |

### 2.2 Öğrenci

| # | Özellik | Neden | Kod durumu | Efor |
|---|---------|-------|------------|------|
| 1 | "Velim": veli bağını onay/ret + kendi kimliğini paylaşma | Veli akışının kilidi (onay yetkisi yalnız öğrenci + Admin'de) | Backend var, mobil yer tutucu (`student_profile_page.dart:249`) | S |
| 2 | Profildeki "yakında" kutucuklarının gerçek ekranlara bağlanması | Özellik zaten var, kullanıcı "yakında" görüyor | `/settings/privacy`, `/membership` var; `_isPremium = false` sabit (`:46`) | S |
| 3 | Ders erteleme/iptal talebi gönderme | Doküman 🟢 diyor, kullanıcı kullanamıyor | Backend var, mobil yok | S |
| 4 | Zamanlanmış yerel çalışma hatırlatıcısı | Streak'i ayakta tutar | Yok (`flutter_local_notifications` da yok) | S |
| 5 | Günlük görev + rozetler, haftalık lig | Her gün açma sebebi | Yok | M |
| 6 | Seri dondurma ("dünü kurtar") + haftalık hedef ilerleme bildirimi | Seri kırılınca bırakmayı azaltır | `StreakFreeze` enum'da var, özellik yok | S–M |
| 7 | Deneme sonucundan ÖSYM katsayılarıyla puan/sıralama tahmini | Sınav öğrencisi için en çok merak edilen sayı | `MockExam.SetEstimatedRank` var, hiç çağrılmıyor | M |
| 8 | Ödevde geç teslim rozeti, teslim sürüm geçmişi; mesaja fotoğraf eki (soru sorma) | Öğretmenle iletişimin doğal biçimi | Yok (bkz. N-44) | M |

### 2.3 Veli

| # | Özellik | Neden | Kod durumu | Efor |
|---|---------|-------|------------|------|
| 1 | Davet koduyla katılma (claim-invite) — GUID yazdırmak yerine | Şu an "Öğrenci kimliği (ID)" alanına GUID isteniyor (`parent_children_page.dart:140`), bağ hep `Pending` kalıyor | Backend var, mobil yok | S |
| 2 | Mesajlar, bildirim gelen kutusu, ayarlar, üyelik, hesap silme erişimi | Şu an veli profilinde yalnız "çıkış" var | Router engelliyor (N-62) | S |
| 3 | "Ödedim" beyanı | `veli.md`'de 🟢, mobilde yok | Backend var | S |
| 4 | Sohbet başlatma + karşı tarafın adı | Gelen kutusu isimsiz | `ConversationSummaryResponse`'ta ad yok | S–M |
| 5 | Girişsiz haftalık özet linki | En ucuz kazanım/tutundurma | Yok | M |
| 6 | Vade öncesi ödeme hatırlatması + aylık ekstre (PDF) | Ödeme disiplinini artırır | Yok | M |

### 2.4 Admin

| # | Özellik | Neden | Kod durumu | Efor |
|---|---------|-------|------------|------|
| 1 | **İlk admini oluşturma yolu** | `/register` Admin'i reddediyor, rol atama yalnız Admin'e açık, seed'de Admin yok (`DemoDataSeeder.cs:165`) → **sistemde hiç admin oluşturulamıyor** | Yok | S |
| 2 | Asgari admin arayüzü (öğretmen doğrulama kuyruğu, plan CRUD, kullanıcı arama) | Uçlar var, kullanılamıyor | Backend kısmen | M |
| 3 | Kullanıcı askıya alma | Kötüye kullanıma müdahale | `UserAccountStatus.Suspended` var, ucu yok | S–M |
| 4 | Şikayet/moderasyon kuyruğu | Çocukların olduğu mesajlaşmada zorunlu; Apple 1.2 | M18 yok | M–L |
| 5 | Kayıt → aktivasyon → deneme → premium hunisi | İş metrikleri | Yok | M |

### 2.5 Güvenlik/hesap (tüm roller)

Kullanıcı saat dilimi + **sessiz saatler** (tarayıcılar UTC'de saat başı koşuyor; gece 02:00'de push gidebiliyor) · öğrenci ve veliye de ders hatırlatması · 2FA (en azından Admin + öğretmen) · Google/Apple ile giriş · yeni cihazdan giriş ve şifre değişikliği e-postası · kayıt/şifre sıfırlama/davet uçlarında CAPTCHA · Android App Links + iOS Universal Links (şu an sıfırlama token'ı özel `egitimussu://` şemasıyla geliyor; aynı şemayı kaydeden başka bir uygulama yakalayabilir).

---

## 3. Profesyonel ürün olma açıkları

| ID | Öncelik | Açık | Kanıt | Öneri |
|----|---------|------|-------|-------|
| P-01 ✅ (P15 T6) | 🔴 Yüksek | Üretim blueprint'inde e-posta/push/depolama sahte sağlayıcıda | `render.yaml` üretim bloğunda `Email__/Push__/Storage__Provider` yok → `appsettings.json` varsayılanları `Logging/Logging/Local`; guard'lar ortam ayırt etmiyor ✔ | Production'da Logging/Local'ı reddeden guard; blueprint'te açık tanım (bkz. N-24) |
| P-02 | 🔴 Yüksek | Barındırma: free plan, yedeksiz, bölge belirsiz | `render.yaml:10,138` `plan: free`; zamanlanmış yedek yok (`backup_restore.md` §7); `region` yok | Uyuyan servis → hatırlatma/outbox/silme işleri durur. Ücretli plan + Frankfurt + managed backup + günlük cron |
| P-03 | 🔴 Yüksek | Hata takibi/alarm yok | Sentry/Crashlytics `src`+`mobile` 0 eşleşme ✔; `Observability__Enabled=false` (Q9); `FlutterError.onError` yok | Sentry (.NET + Flutter), Grafana Cloud OTLP free, uptime ping |
| P-04 | 🔴 Yüksek | Mobil yayın hazır değil | `build.gradle.kts:36-38` debug imza ✔; AAB yok, obfuscation yok, `1.0.0+1` sabit, iOS build yok; `latest` etiketli prerelease'e debug imzalı APK gidiyor; varsayılan Flutter ikonu, başlatıcı adı `egitim_ussu_mobile`; iOS `NSCameraUsageDescription`/`NSPhotoLibraryUsageDescription` yok (kamera açılınca iOS uygulamayı kapatır) | Upload keystore + Play App Signing, AAB + `--obfuscate`, build numarası CI'dan, ikon/splash, iOS izin metinleri |
| P-05 | 🔴 Yüksek | Hukuk/uyum | `kvkk_aydinlatma_metni.md` 14 boş `«…»`; kullanım şartları yok; mobilde KVKK/rıza metni 0 eşleşme ✔; `DateOfBirth` opsiyonel → 18 yaş altı için veli rızası uygulanamıyor; yurt dışı aktarım (Render/Firebase/Upstash) R-1…R-6 açık | Hukuk onayı, kayıtta rıza kutusu + Identity'de rıza kaydı, yaş alanı + veli onayı akışı |
| P-06 | 🔴 Yüksek | Gelir modeli mağaza kurallarıyla çelişiyor | P09 Task 4 iyzico öngörüyor; mobilde dijital premium satışı App Store 3.1.1 / Play Billing gereği **IAP** ister (parite dokümanı R-09 bunu zaten yazıyor, plan güncellenmemiş) | Premium için StoreKit 2 / Play Billing (veya RevenueCat); iyzico yalnız birebir ders ücreti tahsilatı (3.1.3(d) istisnası) |
| P-07 | 🟠 Orta | CI/CD eksikleri | Var: `-warnaserror`, testler, NuGet zafiyet taraması, migration drift. Yok: Dependabot/Renovate (`.github/` altında yalnız `workflows`) ✔, CodeQL/secret scanning, Docker imaj build+tarama, deploy/staging terfisi, backend kapsam ölçümü, PR şablonu, CODEOWNERS. 369 commit'te 0 PR merge'ü — tüm merge'ler yerel | Dependabot + CodeQL + PR akışı + branch koruması |
| P-08 | 🟠 Orta | Test kapsamı | Mobil satır kapsamı %17 (eşik yalnız uyarı); `lesson_form_sheet` (1.426 satır, RRULE) testsiz; router rol yönlendirmesi testsiz; veli ekranları testsiz. Backend'de N-01/N-02/N-12 gibi modüller-arası hatalar 733 birim testine rağmen kaçmış → **senaryo bazlı uçtan uca test** eksik | "Haftalık ders → 3. hafta oturum + ücret" gibi akış testleri; mobilde router rol testleri |
| P-09 | 🟠 Orta | Dockerfile | Non-root `USER` yok, `HEALTHCHECK` yok; `COPY src/` restore'dan önce → katman önbelleği çalışmıyor; `LangVersion preview` üretime gidiyor (`Directory.Build.props`) | `USER $APP_UID`, csproj'ları önce kopyala, `LangVersion` sabitle |
| P-10 | 🟠 Orta | Kullanıcıya görünen demo/yer tutucu içerik | `StudyDemoBadge`, öğrenci ekranlarında "yakında" metinleri, `/teacher-panel-preview` + `/account-info-preview` girişsiz açık ve sahte veri gösteriyor; mock repository kodu üretim binary'sinde | Yayın öncesi "demo sıfır" taraması; preview rotalarını `kDebugMode` arkasına al |
| P-11 | 🟠 Orta | l10n / erişilebilirlik | Dil `tr` sabit (EN ARB devreye girmiyor); ARB kullanan dosya 10/126, ~1.500 sabit literal; `Semantics` 4 yerde; 32 `IconButton`'ın 11'inde tooltip; 189 sabit `fontSize` | Task 6b'yi sürdür; tooltip/Semantics turu |
| P-12 | 🟠 Orta | Flavor/ortam ayrımı yok | Tek `applicationId` (yanında TODO), staging URL tanımsız | dev/staging/prod flavor'ları |
| P-13 | ⚪ Düşük | Migration tasarımı | `ModuleDbContext` tabanındaki `outbox_messages`, `inbox_messages`, `job_leases`, `outgoing_emails`, `module_states` her modül şemasında kopya → her altyapı değişikliği 15 migration × 2 dosya; `src` altında 58.265 satır migration (%75'i `*.Designer.cs`), kodun 1,3 katı | Outbox/inbox atomiklik için modül şemasında kalmalı; ama `job_leases`, `outgoing_emails` (yalnız Identity kullanıyor) ve hiç kullanılmayan `module_states` tek `infra` şemasına taşınmalı, eski migration'lar squash edilip yeni baseline alınmalı |
| P-14 | ⚪ Düşük | Repo hijyeni | Kökte README/LICENSE/CHANGELOG/SECURITY yok ✔; tek etiket `latest` ✔; kökte ve `.config/` altında iki `dotnet-tools.json` (10.0.7 vs 9.0.17); platforma özgü 22 dosya izleniyor ✔ (`.vscode/*`, `mobile/.metadata`, `mobile/windows/**`); `doc/.obsidian/` commit'li; `.doc`/`.html` kopyaları; `doc/` + `docs/` + `codex/` + `doc/promp.txt` dağınıklığı | README + sürüm etiketleri; kök `dotnet-tools.json`'ı sil; Flutter sürümünü sabitle (`pubspec.lock` oynamasının asıl nedeni) |

---

## 4. Doküman ↔ kod çelişkileri

1. **`docs/superpowers/ILERLEME.md` bayat** — "P01–P13 dallarının hepsi PR/merge bekliyor", dal `feat/p13-operasyon-hijyen`, son commit `820b75c` diyor; oysa `dfc158f` + `117aac9` (2026-11-13) ile hepsi `main`'e merge edildi ve `c16d25b` kaydedilmedi.
2. **PRD §9.1** öğretmen paketinde aylık kazanç, geciken ödeme, hatırlatma, analizler için "Free ❌" diyor; kodda öğretmen kapıları yalnız `MaxStudents` + `PdfReport`.
3. **PRD §9.2** `MonthlyAnalysis`, `TopicWeakness`, `StreakFreeze` enum'da var, hiçbir modül kullanmıyor; `weak-spots` ucu kapısız; streak dondurma özelliği yok.
4. **Üyelik girişi** `ogrenci.md`/`veli.md`'de "Diğer → Üyelik" deniyor; bağlantı yalnız öğretmenin `more_page.dart:100`'ünde var.
5. **`veli.md`** "ödedim" beyanı 🟢 (mobilde yok); aynı dosyada çalışma görünümü hem 🔴 hem 🟢.
6. **M11 🟢** ama FCM gerçek cihazda hiç doğrulanmadı (Q8) ve üretimde `Push:Provider=Logging`.
7. **`ogretmen.md` §9** "[ ] Online ders linki" işaretsiz, aynı dosyada B-10 ✅ (`MeetingUrl`).
8. **Parite analizi** R-11 "arama yok" diyor (backend'de filtreli arama var); R-10 "paket kurulu" diyor (`flutter_local_notifications` pubspec'te yok).
9. **`doc/legal/00_legal_index.md`** aydınlatma metninin "kayıt ekranında ve /more → Gizlilik'te gösterildiğini" söylüyor; mobilde hiç yok.
10. **P09 planı** iyzico ile premium öngörüyor; parite dokümanı R-09 IAP'nin zorunlu olduğunu yazıyor.

---

## 5. Önerilen sıra

> **Bu sıra plana dönüştürüldü (2026-11-13):** [Olgunlaştırma master tasarım](../../docs/superpowers/specs/2026-11-13-olgunlastirma-master-design.md) — P15–P29, her N-/P- maddesinin hangi plan/göreve düştüğü §5 izlenebilirlik tablosunda. Aşağıdaki tablo özet olarak korunuyor.

| Dalga | İçerik | Gerekçe |
|-------|--------|---------|
| **0 — Hemen (güvenlik)** | N-20, N-21, N-22, N-23 (davet/bağ güvenliği) · N-24 + P-01 (üretim sağlayıcı guard'ı) · N-08 (yazma uçlarında bağ kontrolü) | Veri sızıntısı ve hesap ele geçirme; küçük, yerel değişiklikler |
| **1 — Çekirdek akış doğruluğu** | N-01, N-02, N-03, N-04, N-05, N-12, N-15 · N-40 (streak), N-41 (kira), N-42 (outbox), N-44, N-45 (ödev) · N-60, N-61 · senaryo bazlı akış testleri (P-08) | Öğretmenin günlük kullanımı (haftalık ders → oturum → ücret) ve öğrencinin ana motivasyonu (seri) bugün bozuk |
| **2 — Veri bütünlüğü + saat dilimi** | `xmin` concurrency token (§1.5) · N-09, N-13, N-29, N-30, N-31 · N-63, N-65, N-67 · tek `Europe/Istanbul` zaman yardımcısı (N-04, N-06, N-11, N-43, N-49, N-50) · N-46 | Para/oturum tutarlılığı; saat dilimi hataları 6 ayrı modüle yayılmış, tek seferde çözülmeli |
| **3 — Mobilde eksik ekranlar** | §2'deki "S" eforlu maddeler (veli bağ akışı, davet, erteleme, ödedim, veli rotaları N-62, hesap silme N-66) | Backend hazır; en yüksek değer/efor oranı |
| **4 — Yayın hazırlığı** | P-02…P-06, P-09, P-10, P-12 · ilk admin (§2.4-1) · şikayet (M18) | Mağaza + hukuk + operasyon kapıları |
| **5 — Büyüme** | Duyuru, özet paylaşımı, paket/kredi, iCal, oyunlaştırma, Matching/Reviews (P11) | Rakip paritesi |

---
Güncelleme: 2026-11-15 (P17 T2: N-01 backend seri bölme ayağı ✅ — `ThisAndFuture` geçmişi eski seride bırakır, yeni seri cutoff'tan başlar, `COUNT` kalan sayıya bölünür, gelecek istisnalar saat farkıyla taşınır ve iptal korunur; split outbox olayı gerçek PostgreSQL'de) · 2026-11-15 (P17 T1: N-01 ◐ — tekrarlı derste iptal/erteleme kapsamı zorunlu (`scope_required`), anahtar orijinal oluşum (`occurrence_not_found`), erteleme talebi oluşum taşır ve kabul yalnız onu taşır; mobil veri katmanı + kapsam sayfası hazır, ekran girişi P17 T5 / P21 T4; "Tamamla" oluşum bazlı P17 T4) · 2026-11-15 (P16 T5: N-43 ✅ — boş zaman şablonu yerel güne kurulur → UTC; N-50 ✅ — Reporting kovaları, anlık görüntü ve bildirim metinleri Türkiye saati; N-11 ◐ — "bugün"/ay sınırları yerel ve panel gecikmiş toplamı para birimi bazında, ödeme özeti kırılımının para birimi boyutu P18 T3; N-06 metin ayağı yerel, hatırlatma anı P17 T6) · 2026-11-14 (P16 T4: N-07 ✅ — çakışma kontrolü oluşum genişletmeli (12 hafta, istisnalar uygulanmış), tatil bloğu `409 scheduling.time_off_conflict` + `allowTimeOffOverride`, yazımlar öğretmen başına `pg_advisory_xact_lock` altında tek transaction; tatil oluştururken çakışan oluşumlar listelenir) · 2026-11-14 (P16 T3: N-05 ✅ — oluşum istisnası `(SeriesLessonScheduleId, OriginalStartAtUtc)` benzersiz + upsert, genişletici tekrar satırlara dayanıklı; N-14 ✅ — pencereye ertelenen oluşum listelenir, bayat istisna diriltilmez) · 2026-11-14 (P16 T2: N-04 ✅ — tekrar kuralı serinin yerel diliminde genişletilir, `BYDAY` yerel gün, aylık ay günü çapadan sabit) · 2026-11-14 (P15 T9: §1.2 kapanışı — N-08 artığı ✅ (otomatik ders ücreti + `link-parent` yaşayan bağ ister), N-32'nin iki açık maddesi P21/P26'ya devredildi; §1.6 A-15/A-06/M15-3 ✅ (T7), `AuthorizationCoverageTests` ✅ + çapraz-kiracı paketi) · 2026-11-14 (P15 T8: N-41 ✅, A-07 ✅ — kira sahibince yenilenir, hesap silme ve saklama servisleri kiralı) · 2026-11-14 (P15 T7: N-29 ✅, N-30 ✅, N-31 ✅, N-63 ✅, N-67 ✅, N-32 ◐ — iptal nedeni, koşullu rotasyon, sid/hesap durumu, mobil tek uçuş) · 2026-11-14 (P15 T6: N-24 ✅, P-01 ✅ — guard + blueprint; üretim servisi Q7/Q4 cevaplanınca açılır) · 2026-11-14 (P15 T5: N-46 ✅)

*Güncelleme: 2026-11-15 — P17 Task 4 / N-03, K-21: tek oturum tamamlama yolu ve oluşum durum senkronizasyonu.*

Güncelleme: 2026-11-15 — P17 T6: N-06 kapandı; oluşum/alıcı bazlı 7 gün, erteleme/silme/seri bölme, TR metin ve zaman aşımı.

*Güncelleme: 2026-11-15 — P17 T8 kapanış zinciri doğrulaması; N-01 mobil erteleme P21 T4, N-16 veli beyanı P18 T3 ve N-48 trend P19 açık kalır.*

*Güncelleme: 2026-11-15 — P18 T1: §1.5 kritik kayıtlarda xmin/409 altyapısı; N-09 niyet komutları T2’de açık.*

*Güncelleme: 2026-11-15 — P18 T2: N-10 kapandı; N-09 backend kapandı, mobil tahsilat geçişi T4’te açık.*

*Güncelleme: 2026-11-15 — P18 T3: N-16 beyan tekliği kapandı; N-11 backend para birimi boyutu kapandı, mobil Currency tüketimi/toplam gösterimi P18 T4 açık.*

**P18 T4 kapanış kanıtı (2026-11-15):** N-09 mobil toplam PUT’u kaldırıldı; bayat ekran yalnız tahsilat deltasını `/collections`'a gönderir ve sunucunun 300 tahsil/200 kalan yanıtını uygular. N-11 ödeme özeti/donut/aylık grafik seçilen para birimiyle, öğrenci detay metrikleri ayrı ISO kodlarıyla gösterilir; para birimleri arasında fold toplam yoktur. N-64 PaymentRecord/özet/aylık modelleri int kuruş, TR ayrıştırma ve decimal JSON sınırı tek Money yardımcısındadır; form `1.500` için 1500 ana birim gönderir. N-65 ödeme oluşturma/tahsilat/iptal, ders oluşturma, ödev teslimi ve mesaj gönderimi UUID anahtar taşır; otomatik/manuel ağ tekrarında aynı anahtar ve orijinal gövde korunur, başarıdan sonraki yeni işlem farklıdır. Koruma uygulama belleği süresince geçerlidir; yeniden başlatma kurtarması eklenmedi, sunucu gövde özeti P18 T5'tir. İlk tahsilat create'te korunur; mobil iade/beyan ekranı bu görevde eklenmedi.

*Güncelleme: 2026-11-15 — P18 T4: N-09 mobil, N-11 mobil, N-64 ve N-65 tamamlandı.*

*Güncelleme: 2026-11-15 — P18 T5: N-55 idempotency ayağı kapandı; sınav/okundu/hedef ayakları P19 T6’da açık.*

*Güncelleme: 2026-11-15 — P19 Task 1: N-40, mevcut seans + 04:00 gün sınırı + geçmiş gün tekilliği + kırılmış seri + idempotent onarım.*

*Güncelleme: 2026-11-15 — P19 Task 2: N-42 kapandı; handler izolasyonu, açık tüketim sonuçları ve dead-letter tüketici adı.*

*Güncelleme: 2026-11-15 — P19 Task 3: N-44 durum/sürüm/yerel geç teslim kapandı; tarayıcı N-45 ve profilId erişimi P21 açık.*
