# 🧭 İLERLEME — Sağlamlaştırma Programı Devam Noktası

> **Bu dosya oturumlar arası tek hafızadır.** Konuşma geçmişi `/clear` ile silinse bile "nerede kaldık" bilgisi burada durur.
> Her görev bittiğinde **bu dosya güncellenir**, sonra oturum temizlenir. Yeni oturumda `/devam` yaz.
>
> Kaynaklar: [master tasarım](specs/2026-09-02-saglamlastirma-master-design.md) · [planlar](plans/) · [eksik analizi](../../doc/denetim/2026-09-02_eksik_analizi.md)

---

## 🎯 ŞU AN

| Alan | Değer |
|------|-------|
| **Aktif plan** | `P04 — Dosya Depolama` **2/5** (`plans/2026-09-02-04-dosya-depolama.md`) — P03 ✅ 7/7 bitti |
| **Sıradaki görev** | **P04 Task 3** — Öğretmen profil fotoğrafı (M02-2 + D-05) |
| **Dal** | `feat/p04-depolama` — `feat/p03-push` üzerine açıldı. **P01, P02, P03 dallarının üçü de PR/merge bekliyor.** |
| **Durum** | 🔄 P04 sürüyor · P01 + P02 + P03 dalları push/merge bekliyor |
| **Son commit** | `221f561` (feat(assignments): ödev teslimini ortak IFileStorage'a taşı — C-02) |
| **Çalışma ağacı** | Temiz (yalnız izlenmeyen `.claude/worktrees/` duruyor) |

## 📋 Plan Durumu

| Plan | Görev | Durum | Not |
|------|-------|-------|-----|
| P01 Onarım | 7/7 | ✅ Bitti | Ana dal yeşil: test paketi + CI `-warnaserror` derlemesi · dal: `feat/p01-onarim` (merge bekliyor) |
| P02 E-posta altyapısı | 7/7 | ✅ Bitti | A-03 + M01-1 + D-01 + D-02 kapandı · dal: `feat/p02-eposta` (merge bekliyor) |
| P03 Push bildirim | 7/7 | ✅ Bitti | Task 1-6 bitti (cihaz token · `IPushSender`/FCM · gerçek gönderim · in-app bildirim merkezi · mobil push zinciri · mobil bildirim merkezi; **Y5 kapandı, M11 🟢**). Gerçek cihaz doğrulaması **Q8'e bağlı** · dal: `feat/p03-push` |
| P04 Dosya depolama | 2/5 | 🔄 Devam ediyor | Task 1-2 bitti (`IFileStorage` + Local/S3 + guard · ödev teslimi göç edildi) · dal: `feat/p04-depolama` · **Q4 kararı Task 5'te gerek** |
| P05 Ayarlar | 0/6 | ⚪ Bekliyor | P03 sonrası |
| P06 Öğretmen MVP | 0/10 | ⚪ Bekliyor | P02+P03+P04 sonrası → **beta noktası** |
| P07 Read-model | 0/5 | ⚪ Bekliyor | P01 sonrası |
| P08 Gelişim & raporlama | 0/6 | ⚪ Bekliyor | P07 sonrası |
| P09 Üyelik & gelir | 0/7 | ⚪ Bekliyor | P07 sonrası · **Q1–Q3 kararı gerek** |
| P10 Mesajlaşma | 0/5 | ⚪ Bekliyor | P03+P05 sonrası |
| P11 Eşleştirme & yorum | 0/6 | ⚪ Bekliyor | P07+P09 sonrası |
| P12 Admin & moderasyon | 0/6 | ⚪ Bekliyor | P11 sonrası |
| P13 Operasyon & hijyen | 0/8 | ⚪ Bekliyor | P01 sonrası — **paralel yürüyebilir** |
| P14 Web (Angular) | 0/5 | ⚪ Bekliyor | P12 sonrası |

**Lejant:** ✅ Bitti · 🔄 Devam ediyor · ⏳ Sırada · ⚪ Bekliyor · 🚫 Bloke

---

## ✅ Tamamlanan Görevler (kronolojik)

> Her satır bir görev. Kolonlar: tarih · plan/görev · commit · doğrulama sonucu.

| Tarih | Plan / Görev | Commit | Doğrulama |
|-------|--------------|--------|-----------|
| 2026-09-02 | P01 / Task 1 — A-01 öğretmen profil güncelleme 500'ü | `a835eba` | `dotnet test EgitimUssu.slnx`: 158 birim + 4 mimari + 13 integration, **başarısız 0** (5 atlandı: Docker yok) |
| 2026-09-02 | P01 / Task 2 — A-02 derlenmeyen 5 mobil test dosyası | `5fa58ea` | `flutter test`: **47 başarılı, başarısız 0** (önce 41 +, 5 dosya yüklenemiyordu) · `flutter analyze`: 5 info, hepsi `lib/` içinde önceden var olan (bu görevin dosyaları temiz) · backend yeşil kaldı |
| 2026-09-02 | P01 / Task 3 — A-05 mock fallback varsayılanı kapatıldı | `fa875e7` | `flutter test`: **48 başarılı, başarısız 0** · `flutter analyze`: aynı 5 önceden var olan info, yeni yok · `dotnet test EgitimUssu.slnx`: 158 birim + 4 mimari + 13 integration, **başarısız 0** (5 atlandı: Docker yok) |
| 2026-09-02 | P01 / Task 4 — A-06 Postgres sırrı config'ten çıkarıldı + `ConnectionStringGuard` | `b655d30` | `dotnet test EgitimUssu.slnx`: **167 birim + 4 mimari + 13 integration, başarısız 0** (5 atlandı: Docker yok) · Development'ta `dotnet run` → `Now listening on: http://localhost:5000` · Production'da dize yokken fail-fast (`exit=134`, guard mesajı) |
| 2026-09-02 | P01 / Task 5 — C-07 gerçek-DB testleri yerelde koşulabilir (`scripts/test-with-docker.sh`) | `53cb0ba` | `./scripts/test-with-docker.sh`: 167 birim + 4 mimari + **18 integration, Atlanan 0, Başarısız 0** (Testcontainers gerçekten koştu) |
| 2026-09-02 | P01 / Task 6 — F-01…F-07 doküman driftleri koda göre düzeltildi | `3ca961d` | `bash doc/_tools/kb_healthcheck.sh` → **temiz** (12 dosyanın frontmatter `updated:` alanı gövdeyle eşitlendi) |
| 2026-09-02 | P01 / Task 7 — kapanış: boş `src/Modules/docs` silindi, CI `-warnaserror` derlemesi yeşile alındı, denetim envanteri işaretlendi | `3755896` | `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** (önce 13 hata) · tam paket: 167+4+18, **Başarısız 0, Atlanan 0** · `flutter test`: **48/48** · `flutter analyze`: 5 önceden var olan info |
| 2026-09-02 | P02 / Task 1 — K-01 `IEmailSender` soyutlaması + `LoggingEmailSender` + `EmailOptionsGuard` | `ee00755` | `dotnet test EgitimUssu.slnx`: **175 birim + 4 mimari + 18 integration, Başarısız 0, Atlanan 0** (öncesi 167 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · Development'ta `dotnet run` → `Now listening on: http://localhost:5296` · Production + `Email__Provider=Smtp` (Host yok) → fail-fast (`exit=134`, "Email:Smtp:Host zorunludur") · Production + `Provider=Logging` → `LogLevel:Warning` uyarısı basıldı, uygulama açıldı · `doc/_tools/kb_healthcheck.sh` temiz |
| 2026-09-02 | P02 / Task 2 — `SmtpEmailSender` (MailKit 4.17) + sağlayıcı seçen DI fabrikası | `236e76c` | `dotnet test EgitimUssu.slnx`: **179 birim + 4 mimari + 18 integration, Başarısız 0, Atlanan 0** · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** |
| 2026-09-02 | P02 / Task 3 — A-03 gerçek e-posta gönderimi (`EmailTemplates` + `EmailIdentityNotificationService`) **+ plan dışı EF düzeltmesi** | `6fde493` | `dotnet test`: **182 birim + 4 + 18, Başarısız 0** · Canlı API (`Provider=Logging`): register/reset/verify hepsi **200** ve 3 e-posta log'landı; kayıtsız e-postada **200 ama e-posta yok** (enumeration engeli korunuyor) · `kb_healthcheck` temiz |
| 2026-09-02 | P02 / Task 4 — `GET /api/identity/me` (M01-1) | `e44812c` | `dotnet test`: 186 birim + 4 mimari + **19 integration, Başarısız 0** (yeni: kimliksiz **401**, kimlikli **200** + `fullName`/`roles` doğrulaması) · `-warnaserror`: 0/0 |
| 2026-09-02 | P02 / Task 5 — Mobil "Şifremi unuttum" akışı (D-01) | `70204eb` | `flutter test`: **51 başarılı, başarısız 0** (öncesi 48) · `flutter analyze`: aynı 5 önceden var olan info · `kb_healthcheck` temiz |
| 2026-09-02 | P02 / Task 6 — Mobil e-posta doğrulama ekranı + `egitimussu://` derin bağlantı (D-02) | `5efdd30` | `flutter test`: **57 başarılı, başarısız 0** · `flutter analyze`: 5 önceden var olan info · **iOS Simulator:** ham `egitimussu://verify-email?token=ABC123` URL'i doğrulama ekranını açtı ve kod alanı `ABC123` doldu (ekran görüntüsü) |
| 2026-09-02 | P02 / Task 7 — Kapanış: SMTP yolu canlı doğrulama + denetim envanteri + dokümanlar | `9f9b5e2` | Yerel SMTP sunucusuna **gerçek gönderim**: MailKit bağlandı, UTF-8 konu + `multipart/alternative` + URL-kodlu `egitimussu://` bağlantı teslim edildi; yakalanan tokenla `password-reset/confirm` **200**, eski şifre **400**, yeni şifre **200**, `GET /me` doğru · `dotnet test EgitimUssu.slnx`: **186 + 4 + 19, Başarısız 0, Atlanan 0** · `flutter test`: **57/57** · `-warnaserror`: 0 uyarı / 0 hata · `kb_healthcheck` temiz |
| 2026-09-03 | P03 / Task 1 — M11-2 `DeviceToken` aggregate + kayıt/silme uçları | `5e5d2ce` | `dotnet test EgitimUssu.slnx`: **195 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 186 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · `dotnet ef migrations add AddDeviceTokens` üretildi (`device_tokens`, Token UNIQUE) · **Canlı API:** kimliksiz kayıt **401**, kayıt **200**, aynı token tekrar **200** (yeni satır yok), başkasının adına **403**, boş token **400**, silme **200**, bilinmeyen token silme **200**, kimliksiz silme **401** · `kb_healthcheck` temiz |
| 2026-09-03 | P03 / Task 2 — `IPushSender` + `FcmPushSender` (FCM HTTP v1) + `PushOptionsGuard` | `49d8ad8` | `dotnet test EgitimUssu.slnx`: **206 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 195 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · `kb_healthcheck` temiz |
| 2026-09-03 | P03 / Task 3 — A-04/Y5 hatırlatmalar gerçekten gönderiliyor + geçersiz token kapatılıyor | `1097ac7` | `dotnet test EgitimUssu.slnx`: **212 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 206 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · `dotnet ef migrations add AddReminderDeliveryTracking` üretildi · `kb_healthcheck` temiz |
| 2026-09-03 | P03 / Task 4 — M11-3/M11-4 `UserNotification` + bildirim merkezi (liste/rozet/okundu) + dispatcher push turu | `dcb6df1` | `dotnet test EgitimUssu.slnx`: **229 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 212 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · `dotnet ef migrations add AddUserNotifications` üretildi · `kb_healthcheck` temiz |
| 2026-09-03 | P03 / Task 5 — A-04/D-11 mobil push: izin + cihaz token kaydı + dokunma → derin bağlantı | `fb26cd8` | `flutter test`: **69 başarılı, başarısız 0** (öncesi 57) · `flutter analyze`: **4 info** (öncesi 5 — `injector.dart` import sıralaması düzeldi) · `kb_healthcheck` temiz. **Gerçek cihaz doğrulaması yapılamadı** (Firebase projesi yok — Q8) |
| 2026-09-03 | P03 / Task 6 — D-11 mobil bildirim merkezi (Tümü/Okunmamış, okundu, derin bağlantı, gerçek rozet) | `a3e9427` | `flutter test`: **79 başarılı, başarısız 0** (öncesi 69) · `flutter analyze`: **4 info** (değişmedi) · `kb_healthcheck` temiz · `doc/pages/notifications.md` oluşturuldu |
| 2026-09-03 | P03 / Task 7 — Kapanış: denetim envanteri (A-04/M11-1..4/D-11 ✅), INDEX/m11 durumu 🟢, kabul kriterleri | `c0d9138` | `dotnet test EgitimUssu.slnx`: **229 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** · `-warnaserror`: **0 uyarı / 0 hata** · `flutter test`: **79/79** · `flutter analyze`: 4 info · `kb_healthcheck` temiz. **Uçtan uca gerçek cihaz senaryosu yapılamadı (Q8)** |
| 2026-09-03 | P04 / Task 1 — C-02 `IFileStorage` + `LocalFileStorage`/`S3FileStorage` + `FileValidation` + `StorageOptionsGuard` | `ba5969c` | `dotnet test EgitimUssu.slnx`: **263 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 229 birim) · `dotnet build … -warnaserror --no-incremental`: **0 uyarı / 0 hata** · `kb_healthcheck` temiz |
| 2026-09-03 | P04 / Task 2 — C-02 ödev teslimi ortak `IFileStorage`'a taşındı + göç script'i | `221f561` | `dotnet test EgitimUssu.slnx`: **271 birim + 4 mimari + 19 integration, Başarısız 0, Atlanan 0** (öncesi 263 birim) · `-warnaserror`: **0 uyarı / 0 hata** · `dotnet ef migrations add AddAssignmentAttachmentStorage` üretildi · `scripts/migrate-assignment-files.sh --dry-run` sahte eski dizinle koşturuldu (dosya + doğru `UPDATE` SQL'i üretti) · `kb_healthcheck` temiz |

---

## 🧠 Yol Boyunca Öğrenilenler

> Bir sonraki oturumun bilmesi gereken, planlarda yazmayan şeyler. **Kısa tut**; kalıcı kural olacaksa `CLAUDE.md`'ye taşı, mimari karar ise master tasarım §2'ye ekle.

| # | Not | Kaynak görev |
|---|-----|--------------|
| 1 | Testcontainers testleri Docker olmadan atlanıyor; tam doğrulama için `./scripts/test-with-docker.sh` (P01 Task 5'te oluşturulacak). | Denetim 2026-09-02 |
| 2 | `dotnet test ... \| tail` çıkış kodunu maskeler — başarısız test görünmez. Sonucu daima **özet satırından** oku ("Başarısız: N"). | Denetim 2026-09-02 |
| 3 | **EF + istemcide üretilen Id = gizli tuzak.** Guid PK'lar `IIdGenerator` ile istemcide atanıyor ama EF konvansiyonu onları `ValueGenerated.OnAdd` sayıyor. Sonuç: izlenen bir aggregate'in koleksiyonuna eklenen **yeni** çocuk, `Added` yerine **`Modified`** olarak izleniyor → var olmayan satır UPDATE ediliyor → `DbUpdateConcurrencyException`. Çözüm: çocuk entity konfigürasyonunda `builder.Property(e => e.Id).ValueGeneratedNever()`. **A-01'in asıl kök nedeni buydu; plandaki merge düzeltmesi tek başına yetmedi.** | P01/Task 1 |
| 4 | Aynı tuzak **diğer modüllerde de** olabilir (aynı Id deseni her yerde). Kapsam kilidi gereği bu görevde yalnız Teachers düzeltildi — modül-genelinde tarama ayrı bir iş olarak değerlendirilmeli (öneri: P13 hijyen planına madde). | P01/Task 1 |
| 5 | `tests/Unit` projesi modül **Infrastructure** katmanlarını referanslamıyordu; EF davranışını birim testinde zorlamak için `Microsoft.EntityFrameworkCore.InMemory` + ilgili `*.Infrastructure.csproj` referansı eklendi. Benzer testler için aynı yol izlenir. | P01/Task 1 |
| 6 | Integration'da gerçek-DB deseni: `Skip.IfNot(fixture.Available, …)` + `RealInfrastructure.Use(fixture)` + `WebApplicationFactory<Program>` → `CreateAsyncScope()` ile DbContext. (Planlardaki `RealInfrastructure.CreateTeachersContextAsync()` gibi yardımcılar **yok**.) | P01/Task 1 |
| 7 | **A-02'nin kökü tek değil, ikiydi.** Plan yalnız auth sahtesini öngörüyordu; 5 dosyanın 2'si (`dashboard_cubit_test`, `scheduling_page_test`) aslında `_FakeSchedulingRepository`'nin Ç-06'da eklenen 5 öğrenci metodunu uygulamamasından kırılıyordu. İkinci ortak sahte (`FakeSchedulingRepository`) yazıldı; plan Task 2 bu sapmayla güncellendi. | P01/Task 2 |
| 8 | **Kalıcı kural oldu:** test sahteleri artık `mobile/test/helpers/` altında tek noktada. Yeni bir repository arayüzü için test dosyası içine yerel `_Fake*` yazma — `doc/architecture/mobile_flutter.md` §17.1. | P01/Task 2 |
| 9 | `flutter analyze` zaten **5 info** üretiyor (2× `directives_ordering` + 3× `deprecated_member_use` `parent_notifications_page.dart`'ta). Bunlar P01 öncesinden var; "No issues found!" beklentisi gerçekçi değil, temizlik P13 hijyen planına aday. | P01/Task 2 |
| 10 | Plan başlığı "A-05 … **+ görünür işaret**" diyordu ama hiçbir adım/`Files:` girdisi mock rozetini kapsamıyordu; denetimde de bu bir **alternatif** öneriydi ("varsayılanı false yap **ya da** rozet göster"). Varsayılan kapatıldı, başlık teslim edilenle hizalandı. Rozet istenirse ayrı iş (aday: P13). | P01/Task 3 |
| 11 | Mock varsayılanı kapanınca **hiçbir test kırılmadı** (48/48 yeşil) — testler mock fallback'e dayanmıyor. Ama artık geliştirmede backend kapalıyken ekranlar boş/hatalı gelir; el ile deneme yaparken `--dart-define=USE_MOCK_FALLBACK=true` gerekir (`mobile/README.md`). | P01/Task 3 |
| 12 | **Plan Task 4 Step 3'te gerçek bir hata vardı:** `WeakPasswords` listesindeki `"password="` alt dizesi, normalize edilmiş **her** bağlantı dizesinde eşleşir → guard üretimdeki *güçlü* parolaları da reddeder, uygulama hiç açılmazdı. Düzeltme: parola **değeri** ayrıştırılıp zayıf-parola kümesiyle karşılaştırılıyor. Regresyon testi eklendi (`EnsureValid_Should_Allow_Strong_Password_In_Production`). **Ders:** plandaki hazır kod blokları kopyalanmadan önce mantıken okunmalı. | P01/Task 4 |
| 13 | `ConnectionStringGuard`, `JwtSigningKeyGuard`'ın `Validate` (neden döner) + `EnsureValid` (fırlatır) ikilisini birebir izler. Health-check fırlatan bir API'yi `try/catch` ile saramazdı; **yeni guard'lar bu deseni izlesin.** | P01/Task 4 |
| 14 | `render.yaml` planın öngördüğü değişikliğe **ihtiyaç duymadı**: `ConnectionStrings__Postgres` zaten `fromDatabase` ile managed DB'ye bağlıydı; `sync: false`'a çevirmek çalışan yapılandırmayı bozardı. Dosya değiştirilmedi. | P01/Task 4 |
| 15 | ⚠️ **CI'nin derleme adımı bugün kırmızı olmalı:** `dotnet build EgitimUssu.slnx -warnaserror` (backend-ci.yml'de var) `tests/Unit`'te **18 adet önceden var olan `CS8602`** (olası null başvuru) üretiyor — `ClaimParentInviteTests`, `ChildDashboardEnrichmentTests`, `StudentCalendarQueryTests`, `StudentPrivacyFilterTests`. Bu görevin değişiklikleri olmadan da üretiliyor (stash ile doğrulandı), yani A-06 kaynaklı değil. `dotnet test` `-warnaserror` kullanmadığı için yerelde görünmüyor. **Aday: P01 Task 7 (kapanış hijyeni) veya P13.** | P01/Task 4 |
| 16 | `src/Shared/Infrastructure/Design/DesignTimeDbContextFactoryBase.cs:18` hâlâ `Password=postgres` içeren bir varsayılan taşıyor. Yalnız `dotnet ef` tasarım-zamanı aracını besliyor (uygulama çalışma zamanına girmiyor) ve Task 4'ün `Files:` listesinde değil — kapsam kilidi gereği dokunulmadı. **Aday: P13 hijyen.** | P01/Task 4 |
| 17 | Bu repoda bazı dosyalar **CRLF** satır sonu kullanıyor (`appsettings.json`, `ConfigurationHealthCheck.cs`, `mimari_inceleme.md`). Python `read_text`/`write_text` ile düzenleme satır sonlarını LF'e çevirip diff'i şişiriyor — ikili (`read_bytes`/`write_bytes`) düzenleme yapılmalı. | P01/Task 4 |
| 18 | Bu makinede **Docker Compose eklentisi yok** (colima + salt `docker` CLI). Testcontainers zaten yalnız çalışan bir Docker daemon'a ihtiyaç duyuyor; `scripts/test-with-docker.sh` Compose'u algılayıp yoksa yığın adımını atlıyor. Docker'ı başlatmak için: `colima start`. | P01/Task 5 |
| 19 | Doküman drift'i işaretlerken plan "hepsini `[x]` yap" diyordu ama kod doğrulaması bazılarının **kısmen** doğru olduğunu gösterdi. Kalıcı yaklaşım: tam → `[x]`, kısmi → `[~]` + kapatacak plan etiketi (`(P03)` gibi), yok → `[ ]` + gerekçe. Doküman böylece "yalancı yeşil" olmuyor. | P01/Task 6 |
| 20 | `doc/_tools/kb_healthcheck.sh`, frontmatter `updated:` ile gövdedeki `Güncelleme:` tarihinin **eşit** olmasını istiyor. Bir doc'un alt bilgisini güncellerken frontmatter'ı da güncelle, yoksa YELLOW DATE uyarısı çıkar. | P01/Task 6 |
| 21 | Öğrenilenler #15'teki CI kırmızısı **Task 7'de kapatıldı**: `-warnaserror` 13 hata veriyordu (3× CS1573 eksik XML `<param>`, 1× CS8604 nullable `LessonFormat`, 9× CS8602 `result.Value`). Dikkat: `dotnet build` **incremental** olduğunda hatalar görünmüyor — CI'yi taklit etmek için **`--no-incremental`** şart. | P01/Task 7 |
| 23 | **`dotnet run` yerelde `Properties/launchSettings.json` profilini uygular ve `ASPNETCORE_ENVIRONMENT` env değişkenini ezer.** Üretim davranışını (guard/uyarı) yerelde denemek için **`--no-launch-profile`** şart; yoksa uygulama sessizce Development'ta koşar ve testin bir anlamı kalmaz. | P02/Task 1 |
| 24 | Yeni guard `Validate`/`EnsureValid` ikilisiyle yazıldı (#13) ama **`ConfigurationHealthCheck`'e bağlanmadı** — `Health/ConfigurationHealthCheck.cs` Task 1'in `Files:` listesinde yok (kapsam kilidi). `/health/ready` bugün e-posta yapılandırmasını raporlamıyor. Aday: P02 kapanışı veya P13. | P02/Task 1 |
| 22 | `flutter analyze`'daki 5 info hâlâ duruyor ve CI **onu koşmuyor** (`build-android.yml` yalnız `pub get` + APK). Ana dalın yeşilliği bundan etkilenmiyor; temizlik P13'e kaldı. | P01/Task 7 |
| 25 | **MailKit sahtesi yazarken iki tuzak:** (1) `MailTransport` taban sınıfı ctor'da `IProtocolLogger` ister → `new NullProtocolLogger()`; (2) sahtedeki `Connected`/`Authenticated`/`Disconnected` alanları `MailService`'in **event**'leriyle çakışıp `CS0108` üretir — `dotnet test` bunu göstermez ama CI'nin `-warnaserror` derlemesi kırılır. Adları `ConnectCalled`/… yap. | P02/Task 2 |
| 26 | **EF `ValueGeneratedNever()` tuzağı Identity'de de vardı** (Öğrenilenler #3/#4'ün doğrulanması): `UserSecurityToken`, `RefreshTokenSession`, `UserRoleMembership`. Sonuç: `password-reset/request` ve `email-verification/request` uçları **500** dönüyordu — e-postadan bağımsız, önceden var olan bir hata. **Diğer modüller hâlâ taranmadı; P13'e madde olmalı.** | P02/Task 3 |
| 27 | **Güvenlik tokenları base64'tür** (`+`, `/`, `=` içerir). Derin bağlantıya ham gömülürse `+` karşı tarafta boşluğa dönüşür. `Uri.EscapeDataString` ile kodlanır; elle yapıştırma için metin gövdede ham kod da bulunmalı. | P02/Task 3 |
| 28 | **Şema-bazlı derin bağlantıda yol boştur:** `Uri.parse('egitimussu://verify-email').path == ''`, hedef `host`'tadır. go_router yalnız `uri.path`'e baktığı için bağlantı sessizce köke düşer. Çözüm: `core/routing/deep_links.dart` → `normalizeDeepLink(Uri)` (host → yol), router `redirect`'inin **ilk** adımı. Yeni derin bağlantı eklerken bu haritaya satır eklenmeli. | P02/Task 6 |
| 29 | `app_router.dart`'ın `redirect`'i beyaz liste dışındaki her yolu köke/role atar. Yeni bir oturumsuz-erişilebilir ekran eklerken **rota tanımı tek başına yetmez**; `onAuthScreen` / `onAccountRecoveryScreen` listelerine de eklenmelidir. | P02/Task 5-6 |
| 30 | **`flutter build ios` sonrası `mobile/build/` analiz sonucunu 5 → 13.104 issue'ya şişiriyor** (eklenti kaynakları oraya kopyalanıyor). Manuel cihaz denemesinden sonra `flutter clean && flutter pub get` çalıştır, yoksa "analyze temiz" iddiası kurulamaz. | P02/Task 7 |
| 31 | **Gerçek SMTP hesabı olmadan da SMTP yolu doğrulanabilir:** yerel bir SMTP sunucusu (127.0.0.1:2525, STARTTLS yok, auth yok) + `Email__Smtp__UseStartTls=false`. Development'ta guard `Username` istemez. MailKit'in ürettiği tam MIME gövdesi böylece görülebilir. | P02/Task 7 |
| 32 | Bu makinede **Android emülatörü yok** (yalnız iOS Simulator) ve **sentetik tıklama izni yok** (`osascript` → hata -25204). iOS'un "…ile Açılsın mı?" onay diyaloğu otomatik geçilemiyor. Soğuk açılışı taklit etmenin yolu: `flutter run -d <sim> --route "<ham-derin-bağlantı>"` — motorun `defaultRouteName`'e koyduğu değerin aynısı. | P02/Task 6 |
| 33 | macOS'ta `flutter pub get` iki **izlenmeyen** SwiftPM artefaktı üretiyor: `mobile/macos/Runner.xcodeproj/project.xcworkspace/xcshareddata/swiftpm/Package.resolved` ve `mobile/macos/Runner.xcworkspace/…/swiftpm/Package.resolved`. `mobile/.gitignore` yalnız `.swiftpm/` (nokta ile) deseni taşıyor, bu yollar tutmuyor → `git add -A` ile kazara commit'lenebilir. **Aday: P13 hijyen** — `.gitignore`'a `**/xcshareddata/swiftpm/` eklenmeli. | P02/Task 7 |
| 34 | **`AuthorizationCoverageValidator` sessiz bir güvenlik ağı:** yeni bir command/query için authorizer kaydı unutulursa uygulama startup'ta patlar; `dotnet test` yeşilse kayıtlar tamamdır. Ayrı bir kontrol yazmaya gerek yok. | P03/Task 1 |
| 35 | Yeni EF entity eklerken `ValueGeneratedNever()` **baştan** yazılmalı (Öğrenilenler #3/#26). Id `IIdGenerator` ile istemcide üretildiği için bu satır olmadan ilk güncelleme senaryosunda `DbUpdateConcurrencyException` çıkar. `DeviceToken`'da proaktif eklendi. | P03/Task 1 |
| 36 | Canlı uç denerken **kullanıcı bir kez oluşturulunca InMemory DB'de kalır**; aynı e-postayla ikinci kayıt `identity.duplicate_email` (409) döner ve `accessToken` alınamaz → tüm sonraki çağrılar 401 görünür. Denemeyi tekrarlarken e-postayı değiştir. | P03/Task 1 |
| 37 | Bash'te `UID` bir kabuk **salt-okunur değişkeni**; `UID=$(...)` zsh'de "bad math expression" ile patlar. Script değişkenlerinde `USERID` gibi adlar kullan. | P03/Task 1 |
| 38 | **`Google.Apis.Auth` 1.76'da `GoogleCredential.FromFile`/`FromJson` `[Obsolete]`.** `dotnet test` bunu göstermez ama CI'nin `-warnaserror` derlemesi CS0618 ile kırılır. Doğru yol: `CredentialFactory.FromFile<ServiceAccountCredential>(path)` / `FromJson<ServiceAccountCredential>(json)` → `.ToGoogleCredential().CreateScoped(scope)`. Plandaki kod bloğu eskiydi. | P03/Task 2 |
| 39 | **`LessonReminder`'da genel bir `UserId` yok** — alıcı `TeacherUserId`. Kendi-ders (Ç-06) hatırlatmalarında bu alan `Guid.Empty`, dolayısıyla **öğrenciye push gitmiyor**. `StudentId → UserId` eşlemesi çapraz-modül okuma; **P07 read-model** geldiğinde kapatılmalı (m11 + mimari_inceleme Y5'e not düşüldü). | P03/Task 3 |
| 40 | **Olay tüketicisinde push göndermek yasak** (senkron ağ çağrısı + inbox transaction'ını uzatır). Desen: tüketici `UserNotification` **yazar**, `NotificationDispatcher` turu `PushedOnUtc == null` olanları toplu gönderir. Yeni bildirim üreten her akış bu deseni izlemeli. | P03/Task 4 |
| 41 | Bildirim merkezi uçları `ParentNotification`'ı **kaldırmadı**; veli listesi (`GET /parents/{id}/notifications`) geriye dönük uyumluluk için duruyor. Aynı olay iki kayıt üretiyor (veli-özel + genel). Birleştirme, veli mobil ekranı `UserNotification`'a geçince yapılmalı (aday: P05/P06). | P03/Task 4 |
| 42 | **`Firebase.initializeApp()` koşulsuz çağrılırsa, platform dosyaları (`google-services.json`/`GoogleService-Info.plist`) yokken uygulama hiç açılmaz.** `FirebasePushBinding.initializeFirebase()` bunu `try/catch` ile sarar, uyarı log'lar ve `false` döner — istemcide **bilinçli fail-open**, backend'de fail-fast. | P03/Task 5 |
| 43 | **Push servisi Firebase'i import etmemeli.** `PushService` token/izin/olay kaynaklarını parametre olarak alır; gerçek FCM bağlantısı ayrı dosyadadır. 12 birim testi platform kanalı olmadan koşuyor. Yeni platform servisleri bu deseni izlesin. | P03/Task 5 |
| 44 | `mobile/pubspec.lock` **tracked** ama `CLAUDE.md` onu `main`'e commit'lemeyi yasaklıyor. Bağımlılık eklendiğinde dosya değişir; commit öncesi `git restore --staged mobile/pubspec.lock` gerekir, yoksa `git add -A` onu içeri alır. Çalışma ağacında kirli kalır — bu **beklenen** durumdur. | P03/Task 5 |
| 45 | **Zil rozeti `AppPageHeader`'da tek noktada.** Sayı `NotificationsState.unreadCount`'tan gelir; önceden bekleyen ders hatırlatmalarından türetiliyordu (yalnız öğretmende doğruydu), şimdi `GET /users/{id}/unread-count`. Rozetle ilgili değişiklik bu tek dosyadan yapılır. | P03/Task 6 |
| 46 | Bir domain arayüzü (`NotificationRepository`) büyüyünce **test dosyalarındaki yerel sahteler derlenmez olur**. Öğrenilenler #8'in maliyeti burada görüldü: 2 test dosyası kırıldı, sahte `test/helpers/fake_notification_repository.dart`'a taşındı. Yeni sahte yazmadan önce `test/helpers/`'a bak. | P03/Task 6 |
| 47 | ⚠️ **`.gitignore`'da kök-bağlantısız desen tehlikeli.** `storage/` satırı macOS'un **büyük/küçük harf duyarsız** dosya sisteminde `src/Shared/Infrastructure/Storage/`'ı da eşledi; P04 Task 1 commit'i (`ba5969c`) depolama kaynaklarını **hiç içermedi** ve bu ancak `git mv` "not under version control" dediğinde fark edildi. Yeni yoksayma deseni yazarken **kök-bağlantılı** yaz (`/storage/`) ve commit sonrası `git ls-files <klasör>` ile doğrula. | P04/Task 2 |
| 48 | **Altyapı portları `Shared.Kernel`'e aittir, `Shared.Infrastructure`'a değil.** Modüllerin Application katmanı Infrastructure'ı referanslamıyor; `IFileStorage`/`FileValidation` orada kalsaydı handler onları göremezdi. `IClock`/`ICurrentUser`/`IIdGenerator` deseni: **port Kernel'de, implementasyon Infrastructure'da.** | P04/Task 2 |

---

## ⛔ Bloke Eden Kararlar

| Soru | Etkilediği plan | Durum |
|------|-----------------|-------|
| Q1 — iyzico ticari hesabı? (karar K-05) | P09 Task 4 | ❓ Cevap bekliyor |
| Q2 — Nihai fiyat listesi? | P09 Task 3 | ❓ Cevap bekliyor |
| Q3 — AdMob hesabı + yerleşim politikası? (karar K-06) | P09 Task 5 | ❓ Cevap bekliyor |
| Q4 — S3 mi Cloudflare R2 mı? (karar K-03) | P04 Task 5 | ❓ Cevap bekliyor (varsayılan: R2) |
| Q5 — KVKK metinleri hazır mı? | P13 Task 5 | ❓ Cevap bekliyor |
| Q6 — Beta öğretmen listesi (5–10 kişi)? | P06 sonrası | ❓ Cevap bekliyor |
| Q7 — **Gerçek SMTP sağlayıcı hesabı** (host/kullanıcı/parola)? | P02 kapanış doğrulaması (tek seferlik gerçek gönderim) | ❓ Cevap bekliyor — kod hazır, yalnız kimlik bilgisi eksik |
| Q8 — **Firebase projesi** (Android+iOS uygulaması, `google-services.json`/`GoogleService-Info.plist`, service-account JSON)? | P03 Task 5 Step 1 + Task 7 canlı doğrulama | ❓ Cevap bekliyor — kod hazır, kurulum `mobile/README_PUSH.md`'de |
| K-11 — Angular 20 + signals onayı? | P14 | ❓ Cevap bekliyor |

---

## 🔁 Oturum Döngüsü (nasıl çalışıyor)

```
/devam            → ILERLEME.md okunur, sıradaki TEK görev yürütülür (TDD adımlarıyla)
   ↓
/gorev-bitir      → testler koşar, plan checkbox'ları [x], doküman, commit, bu dosya güncellenir
   ↓
/clear            → konuşma bağlamı temizlenir
   ↓
/devam            → yeni oturum bu dosyadan devam eder
```

`/devam P03` → belirli bir planın sıradaki görevi · `/devam P06 Task 4` → belirli görev · `/devam plan` → aktif planın tamamı (uzun oturum).

---

*İlerleme defteri | Son güncelleme: 2026-09-03 (P04 Task 2 bitti — 2/5; dal `feat/p04-depolama`)*
