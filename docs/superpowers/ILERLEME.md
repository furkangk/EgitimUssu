# 🧭 İLERLEME — Sağlamlaştırma Programı Devam Noktası

> **Bu dosya oturumlar arası tek hafızadır.** Konuşma geçmişi `/clear` ile silinse bile "nerede kaldık" bilgisi burada durur.
> Her görev bittiğinde **bu dosya güncellenir**, sonra oturum temizlenir. Yeni oturumda `/devam` yaz.
>
> Kaynaklar: [master tasarım](specs/2026-09-02-saglamlastirma-master-design.md) · [planlar](plans/) · [eksik analizi](../../doc/denetim/2026-09-02_eksik_analizi.md)

---

## 🎯 ŞU AN

| Alan | Değer |
|------|-------|
| **Aktif plan** | `P01 — Onarım` (`plans/2026-09-02-01-onarim.md`) |
| **Sıradaki görev** | **Task 5** — C-07: Gerçek-DB testlerini yerelde koşulabilir yap |
| **Dal** | `feat/p01-onarim` |
| **Durum** | 🔄 Task 4 bitti (4/7) |
| **Son commit** | `b655d30` (fix(config): A-06) |
| **Çalışma ağacı** | Temiz (yalnız izlenmeyen `.claude/worktrees/` duruyor) |

---

## 📋 Plan Durumu

| Plan | Görev | Durum | Not |
|------|-------|-------|-----|
| P01 Onarım | 4/7 | 🔄 Devam ediyor | Ana dalı yeşile alır — **önce bu** · dal: `feat/p01-onarim` |
| P02 E-posta altyapısı | 0/7 | ⚪ Bekliyor | P01 sonrası |
| P03 Push bildirim | 0/7 | ⚪ Bekliyor | P01 sonrası (P02 ile paralel olabilir) |
| P04 Dosya depolama | 0/5 | ⚪ Bekliyor | P01 sonrası (paralel olabilir) · **Q4 kararı gerek** |
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

*İlerleme defteri | Son güncelleme: 2026-09-02 (P01 Task 4 tamamlandı)*
