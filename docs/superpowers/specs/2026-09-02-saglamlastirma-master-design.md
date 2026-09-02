# EğitimÜssü Sağlamlaştırma Programı — Master Tasarım (Spec)

**Tarih:** 2026-09-02
**Kaynak analiz:** [`doc/denetim/2026-09-02_eksik_analizi.md`](../../../doc/denetim/2026-09-02_eksik_analizi.md) (121 madde)
**Amaç:** EğitimÜssü'nü "çalışan prototip"ten **profesyonel, üretime alınabilir ürüne** taşıyan planların ortak tasarım/karar kaydı. Her plan bu spec'ten argüman alır.

---

## 1. Hedef ve Başarı Tanımı

**Program hedefi:** PRD v2.1'in Faz 0–5 kapsamını, kodda doğrulanmış eksik envanterine göre kapatmak.

**"Profesyonel" ölçütü (her plan bu çıtaya uyar):**

| Ölçüt | Kural |
|-------|-------|
| Yeşil ana dal | Her plan sonunda `dotnet test EgitimUssu.slnx` ve `flutter test` **tam yeşil** (atlanan yalnız Docker gerektiren Testcontainers testleri, o da CI'da koşar). |
| Testsiz kod yok | Her davranış değişikliği önce başarısız test (TDD). Domain kuralları birim, uçtan uca akışlar integration testiyle. |
| Sessiz başarısızlık yok | Yeni her dış bağımlılık (e-posta, push, blob) için: fail-fast konfig doğrulaması **veya** bilinçli fail-open + `LogWarning`. Sessizce yutan `catch` yasak. |
| Sahte veri yok | Mobilde ekrana basılan her değer gerçek API'den gelir; mock yalnız açık `--dart-define` ile. |
| Modül sınırı | Modüller birbirinin DbContext'ini okumaz. Modüller arası okuma yalnız `Shared/Contracts` veya event-beslemeli projeksiyon. `Modules_Should_Not_Reference_Other_Modules` mimari testi bunu zorlar. |
| Yetki varsayılan-deny | Her yeni command/query için authorizer **veya** açık `IAllowAnonymous`. `AuthorizationCoverageValidator` startup'ta zorlar. |
| Doküman aynı turda | Her plan görevinin son adımı, ilgili `doc/modules/mNN_*.md` + `doc/modules/00_genel_bakis.md` + gerekiyorsa `doc/INDEX.md` güncellemesi (kök `CLAUDE.md` kuralı). |
| Sır repoda yok | Yeni hiçbir anahtar/parola `appsettings.json`'a yazılmaz; env/secret store + startup guard. |

---

## 2. Karar Kaydı (ADR-lite)

> **Kod öneki `K-`** kullanılır; analiz dokümanındaki `D-xx` kodları **mobil maddeleri** gösterir, karıştırılmamalıdır.

> Bu kararlar planlara **gömülüdür**. Değiştirmek istersen önce burayı değiştir, sonra ilgili planı.

| # | Karar | Gerekçe | Alternatif (reddedildi) |
|---|-------|---------|--------------------------|
| **K-01** | **E-posta: sağlayıcı-agnostik SMTP.** `IEmailSender` soyutlaması + `SmtpEmailSender` (MailKit) + `LoggingEmailSender` (dev). | Gmail/SES/SendGrid/Resend hepsi SMTP relay verir; sağlayıcı değişimi config değişikliği olur, kod değişmez. | SendGrid/Resend SDK — koda sağlayıcı bağımlılığı sokar. |
| **K-02** | **Push: FCM (Firebase Cloud Messaging)**, hem Android hem iOS için tek kanal. Sunucuda HTTP v1 API, OAuth2 service-account. | `firebase_messaging` zaten `pubspec.yaml`'da; iOS'ta APNs'i FCM proxy'ler. | Doğrudan APNs+FCM ayrı ayrı (iki entegrasyon); OneSignal (üçüncü taraf bağımlılığı). |
| **K-03** | **Dosya depolama: `IFileStorage` soyutlaması + iki implementasyon** — `LocalFileStorage` (dev) ve `S3FileStorage` (AWS SDK, S3-uyumlu: AWS S3 / Cloudflare R2 / MinIO). | Render'da yerel disk kalıcı değil; S3-uyumlu API en yaygın ortak payda. | Azure Blob (aynı iş, daha dar uyumluluk). |
| **K-04** | **Modüller arası okuma (O5): event-beslemeli projeksiyon + `Shared/Contracts` okuma arayüzü.** Senkron çapraz-modül DB okuması yasak. | Mevcut outbox/inbox altyapısı hazır; modül sınırı korunur. | Senkron `IXxxDirectory` her yerde — bağımlılık grafiğini patlatır. |
| **K-05** | **Ödeme sağlayıcı: iyzico** (TR pazarı, TL, 3D Secure, abonelik desteği). Entegrasyon `IPaymentGateway` arkasında; webhook imza doğrulamalı. | Türkiye'de kart kabulü için yerel sağlayıcı gerekli. | Stripe (TR'de kart kabulü sınırlı), yalnız havale (dönüşüm düşer). |
| **K-06** | **Reklam: Google AdMob**, yalnız mobil, yalnız Free kullanıcı; yerleşim `ad-placements` API'siyle sunucudan yönetilir. | Standart; premium'da tamamen kapatılır. | Kendi reklam sunucusu (gereksiz). |
| **K-07** | **Mesajlaşma v1: REST + poll (+push bildirim).** Gerçek-zamanlı (SignalR) v2'ye ertelendi. | Ölçek daha yokken WebSocket operasyon maliyeti taşımaya değmez; push zaten anlık uyarı verir. | SignalR/WebSocket ilk sürümde. |
| **K-08** | **Admin: ayrı `/api/admin/*` uç grubu + mobil değil, Angular web panel.** Faz olarak web'den önce API biter, geçici olarak uçlar Swagger/curl ile kullanılabilir. | Admin işleri masaüstü ekran ister. | Mobil admin ekranı (dar ekranda moderasyon verimsiz). |
| **K-09** | **Üyelik entitlement'ı tek noktadan:** `Shared/Contracts/IEntitlementDirectory` + `MembershipChangedIntegrationEvent`. Modüller limit kontrolünü bu arayüzle yapar (mevcut `MembershipGate` genelleştirilir). | Limitlerin her modülde ayrı hard-code edilmesini engeller. | Her modülde ayrı tier alanı (bugünkü dağınık durum). |
| **K-10** | **Gözlemlenebilirlik: OpenTelemetry** (trace + metrik + log korelasyonu), OTLP exporter; ücretsiz başlangıç için konsol/Jaeger, prod'da yönetilen bir toplayıcı. | Vendor-agnostik standart. | Serilog+Seq (yalnız log), Sentry (yalnız hata). |
| **K-11** | **Web: Angular 20 + standalone components + signals**, admin paneli ilk hedef, sonra public keşif/SEO sayfaları. | `doc/architecture/web_angular.md` planı; admin en yüksek değerli ilk kullanım. | Public site önce (kullanıcı havuzu yokken değersiz). |
| **K-12** | **Türkçe UI metinleri l10n'a taşınır (ARB), varsayılan `tr`, `en` iskelet olarak kalır.** | Hardcoded metin + Türkçe karaktersiz yazımlar marka kalitesini düşürüyor. | Hardcoded devam (teknik borç büyür). |

**Onayına açık kararlar:** K-05 (iyzico), K-06 (AdMob), K-11 (Angular sürümü). Bunlar yalnız ilgili planları (P09, P14) etkiler; öncekiler bloklanmaz.

---

## 3. Plan Seti ve Sıra

| # | Plan | Kapsadığı maddeler | Bağımlılık | Çıktı |
|---|------|--------------------|------------|-------|
| P01 | [`01-onarim`](../plans/2026-09-02-01-onarim.md) | A-01, A-02, A-05, A-06, C-07, C-09, F-01…F-07 | — | Ana dal yeşil, sahte veri kapalı, sırlar env'de, dokümanlar kodla hizalı |
| P02 | [`02-eposta-altyapisi`](../plans/2026-09-02-02-eposta-altyapisi.md) | A-03, D-01, D-02, M01-1 | P01 | Şifre sıfırlama + e-posta doğrulama uçtan uca |
| P03 | [`03-push-bildirim`](../plans/2026-09-02-03-push-bildirim.md) | A-04, M11-1..4, D-11(mobil ekran) | P01 | Gerçek push + in-app bildirim merkezi |
| P04 | [`04-dosya-depolama`](../plans/2026-09-02-04-dosya-depolama.md) | C-02, M06-2, M02-2, D-05, M06-1 | P01 | S3-uyumlu depolama; profil foto + ders kaynağı |
| P05 | [`05-settings-tercihler`](../plans/2026-09-02-05-settings-tercihler.md) | M15-1..4, M11-5, D-14, D-15, D-16, D-17 | P03 | Gerçek ayar modülü + mobil ayar ekranları |
| P06 | [`06-ogretmen-mvp-kapanisi`](../plans/2026-09-02-06-ogretmen-mvp-kapanisi.md) | M02-1/3/4/5, M03-1/2/3, M05-1..4, M06-3/4, M07-1/2, M04-1/2, M08-1, M09-3, D-04 | P02, P03, P04 | Beta'ya hazır öğretmen çekirdeği |
| P07 | [`07-read-model-altyapisi`](../plans/2026-09-02-07-read-model-altyapisi.md) | C-03 (O5), C-05, C-06 | P01 | Projeksiyon deseni + dispatcher pipeline |
| P08 | [`08-gelisim-raporlama`](../plans/2026-09-02-08-gelisim-raporlama.md) | M10-1..4, M09-1, M14-1..5, M07-3 | P07 | Gelişim zaman serisi + rapor + PDF |
| P09 | [`09-uyelik-gelir`](../plans/2026-09-02-09-uyelik-gelir.md) | M17-1..5, M03-4, D-10, D-15 | P07 | Abonelik, entitlement, reklam, kampanya/referans |
| P10 | [`10-mesajlasma`](../plans/2026-09-02-10-mesajlasma.md) | M16-1, M09-2, D-08 | P03, P05 | Öğretmen↔öğrenci/veli sohbet |
| P11 | [`11-esleştirme-yorum`](../plans/2026-09-02-11-eslestirme-yorum.md) | M12-1..6, M13-1..6, D-06, D-07, D-09 | P07, P09 | İlan, keşif, puanlama |
| P12 | [`12-admin-moderasyon`](../plans/2026-09-02-12-admin-moderasyon.md) | C-01, M18-1/2, M01-2/3, M02-1(admin yüzü), D-13 | P11 | Admin API + moderasyon kuyruğu |
| P13 | [`13-operasyon-hijyen`](../plans/2026-09-02-13-operasyon-hijyen.md) | C-04, C-10, E-05, E-06, E-07, D-03, D-18…D-22, M01-4 | P01 (paralel yürür) | Gözlemlenebilirlik, staging, KVKK, l10n, cihaz oturumları |
| P14 | [`14-web-angular`](../plans/2026-09-02-14-web-angular.md) | E-01, D-12(admin ekranı) | P12 | Angular admin paneli + public keşif |

**Kritik yol:** P01 → P02/P03/P04 (paralelleşebilir) → P06 → *beta* → P07 → P08/P09 → P10 → P11 → P12 → P14.
**P13** baştan sona paralel yürütülebilir; P01'den sonra istenen an başlar.

---

## 4. Ortak Teknik Sözleşmeler

Tüm planlar aşağıdakilere uyar (her planın "Global Constraints" bölümünde tekrarlanır):

- **Namespace kökü:** `EgitimUssu.*`; dosya/klasör/kod tanımlayıcısı `EgitimUssu` (Türkçe karaktersiz). Görünen metin `EğitimÜssü`.
- **Zaman:** her yerde `IClock.UtcNow`; `DateTime.UtcNow` yasak.
- **Kimlik:** `IIdGenerator.New()`; `Guid.NewGuid()` doğrudan çağrılmaz.
- **Sonuç tipi:** `Result` / `Result<T>` + `Error(code, message)`; kod `<modul>.<hata>` biçiminde (`teachers.profile_not_found`).
- **HTTP hata eşlemesi:** `ApiErrorHttpResults.FromError` / `.Forbidden` / `.NotFound`.
- **Modül iskeleti:** `API` (`ModuleDefinition` + `CreateModuleGroup`), `Application` (command/query + handler + validator + authorizer + repository arayüzü), `Domain` (aggregate + event), `Infrastructure` (`ModuleDbContext` türevi + repository + DI + migration).
- **Yeni modül eklerken:** 4 csproj + `EgitimUssu.slnx` girdileri + `src/API.Host/ModuleAssemblies.cs`'e `typeof(XModule).Assembly` + `AddModuleDbContext<XDbContext>(configuration, "X", XDbContext.SchemaName)`.
- **Migration:** `dotnet ef migrations add <Ad> --project src/Modules/<M>/Infrastructure --startup-project src/API.Host --context <M>DbContext`.
- **Test komutları:** `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` (hızlı), `dotnet test EgitimUssu.slnx` (tam), `cd mobile && flutter test`.
- **Mobil katman:** `features/<ad>/{data,domain,presentation}`; DI `mobile/lib/core/di/injector.dart`; rota `mobile/lib/core/routing/app_router.dart`; HTTP `ApiClient`.
- **Commit:** Conventional Commits (`feat(scope):`, `fix(scope):`, `docs:`, `chore:`). Her görev sonunda commit.

---

## 5. Riskler

| Risk | Etki | Azaltma |
|------|------|---------|
| A-01'in kökü Postgres'te farklı davranabilir | Yanlış düzeltme | P01 Task 1'de hem InMemory hem Testcontainers-Postgres testi yazılır |
| Push/e-posta sağlayıcı hesapları hazır değil | P02/P03 bloklanır | Her ikisinde de "no-op + log" implementasyonu varsayılan; gerçek sağlayıcı yalnız config ile devreye girer, kod bloklanmaz |
| Read-model (P07) geç kalırsa P08/P09/P11 tıkanır | Program gecikir | P07 küçük tutuldu: tek genel projeksiyon deseni + 1 örnek uygulama; kapsam büyütmesi yasak |
| Aynı anda çok modül açılması → merge çatışması | Yavaşlama | Her plan ayrı dalda, `superpowers:using-git-worktrees` ile izole; P01 önce merge edilir |
| Docker olmadan integration testleri atlanıyor | Regresyon geç yakalanır | P01 C-07: yerelde `docker compose` ile Postgres+Redis ayağa kaldırma dokümante edilir; CI'da zaten koşuyor |

---

## 5.1 Kapsam Dışı Bırakılanlar (bilinçli karar)

| ID | Madde | Neden şimdi değil | Ne zaman |
|----|-------|-------------------|----------|
| **C-08** | Value object'ler + invariant'ların domaine çekilmesi (DDD olgunluğu) | Tüm modüllere yayılan geniş bir refactor; davranış değiştirmediği için kullanıcıya değer üretmez ve program boyunca sürekli çatışma yaratır. Yeni yazılan kodda (P09–P12 modülleri) **baştan** value object kullanılır; eskiler dokunulduğunda kademeli dönüştürülür. | Ayrı bir teknik-borç planı; P12 sonrası |
| **M01-5** | Telefon doğrulama (SMS/OTP) | SMS sağlayıcı sözleşmesi + maliyet kararı gerekiyor; hiçbir akış buna bağlı değil (kimlik e-posta üzerinden çalışıyor). | Karar verilince ayrı plan |
| **E-02 / E-03 / E-04** | E-posta / nesne depolama / ödeme sağlayıcı sözleşmeleri | Bunlar ayrı iş kalemi değil; sırasıyla **P02**, **P04**, **P09** içinde teknik olarak çözülüyor. Kalan kısım ticari hesap açma (Q1, Q4). | İlgili planla birlikte |

> Bu tablo, "unutuldu mu?" sorusuna cevaptır: unutulmadı, **bilinçli olarak ertelendi**.

---

## 6. Açık Sorular (plan yürütmeden önce cevaplanmalı)

| # | Soru | Bloklayan plan |
|---|------|----------------|
| Q1 | Ödeme sağlayıcı iyzico onaylanıyor mu? Ticari hesap/anlaşma var mı? | P09 |
| Q2 | Fiyatlandırma nihai mi? (`codex/03_paket_fiyatlandirma.md` güncel mi) | P09 |
| Q3 | AdMob hesabı + reklam yerleşim politikası (hangi ekranlarda, hangi sıklıkta)? | P09 |
| Q4 | S3-uyumlu sağlayıcı tercihi: AWS S3 mi, Cloudflare R2 mi? | P04 (varsayılan: R2, ücretsiz çıkış trafiği) |
| Q5 | KVKK metinleri (aydınlatma, açık rıza, saklama süreleri) hazır mı? | P13 |
| Q6 | Beta öğretmen listesi (5–10 kişi) belirlendi mi? | P06 sonrası |

---

*Master Tasarım | Güncelleme: 2026-09-02*
