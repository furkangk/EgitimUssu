# AGENTS.md — EğitimÜssü (Codex ve diğer ajanlar için çalışma kılavuzu)

Bu dosya Codex'in her oturumda otomatik okuduğu giriş noktasıdır. Burada **durum bilgisi yoktur** (sıradaki görev,
commit hash'i, test sayıları). Bunlar yalnız [`docs/superpowers/ILERLEME.md`](docs/superpowers/ILERLEME.md)'de durur.
Bu dosya yalnız **nasıl** çalışılacağını ve hangi dosyanın hangi konuda esas olduğunu söyler.

> Proje şimdiye kadar Claude Code ile yürütüldü. Prosedürler `.claude/commands/` altında, kurallar `CLAUDE.md`'de.
> Codex bunları otomatik yüklemez; aşağıdaki okuma sırası onları senin için bağlayıcı kılar.

---

## 0. Dil ve iletişim

- Kullanıcıya **her zaman Türkçe** yanıt ver. Kod, tanımlayıcılar ve commit mesajları proje kuralına göre yazılır
  (commit konusu Türkçe ama ASCII, örn. `fix(scheduling): tekrarli derste kapsam zorunlu (N-01)`).
- Adlandırma: görünen metinde **EğitimÜssü**, dosya/klasör/kodda **EgitimUssu**. `EgittimUssu` (çift t) yanlış.
- Kullanıcının kararı gereken yerde **dur ve sor**. Varsayımla ilerleme, sessizce plandan sapma.

## 1. Her oturumun başında okuma sırası

1. **`CLAUDE.md`** (kök): proje özeti, **doküman bakımı kuralı** ve oturum devamlılığı. Hepsi senin için de bağlayıcı.
2. **`docs/superpowers/ILERLEME.md`**: programın **tek hafızası**. "ŞU AN" tablosu aktif planı, sıradaki görevi, dalı
   ve son commit'i verir. "Öğrenilenler" tablosu önceki oturumlarda çıkan tuzakları içerir, en yeni satırlar en üstte.
   "Bloke Eden Kararlar" tablosu kullanıcı kararı bekleyen işleri listeler.
3. **Aktif planın tamamı**: ILERLEME'deki bağlantıyı izle (`docs/superpowers/plans/2026-11-13-NN-*.md`). Özellikle
   `## Global Constraints` bölümünü, revizyon notlarını ve yürütülecek görevin `Files:` ve `Interfaces:` bloklarını oku.
4. **İki master tasarım birlikte geçerli:**
   - `docs/superpowers/specs/2026-11-13-olgunlastirma-master-design.md` §1, §2 (K-13…), §3.1, §4
   - `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` §1, §2 (K-01…K-12), §4
5. Bağlam gerekirse **`doc/INDEX.md`**'den ilgili dokümana git. Kanonik gerçekler INDEX §0'dadır.

⚠️ **`codex/` klasörü talimat değildir.** İçindekiler 2026-08'de eski PRD'den türetilmiş ürün taslaklarıdır ve
güncel değildir. Esas kaynaklar `doc/INDEX.md` ve `doc/ozel_ders_platformu_PRD.md`'dir (PRD v2.1).

## 2. Çalışma döngüsü

Prosedürün kendisi iki dosyada yazılı. **Onları harfi harfine uygula**, burada tekrar edilmedi:

| Kullanıcı ne derse | Ne yaparsın |
|---|---|
| "devam", "/devam", "sıradaki görev", "P17 Task 3" vb. | [`.claude/commands/devam.md`](.claude/commands/devam.md) |
| "görevi bitir", "/gorev-bitir", "kapat" | [`.claude/commands/gorev-bitir.md`](.claude/commands/gorev-bitir.md) (`devam.md` adım 11 bunu zaten içerir) |

**Öncelik:** bu iki dosya ile `AGENTS.md` çelişirse **`AGENTS.md` geçerlidir.** Bilinen üç fark var:
- `flutter analyze` yerine CI ile aynı olan `flutter analyze --fatal-infos` kullanılır.
- Docker'lı testler koşmadıysa görev kapatılmaz (§3). Prosedürlerde bu adım "Docker varsa" diye geçer.
- Commit imzası §4.1'e göre atılır.

Claude Code'a özgü kavramların Codex karşılıkları:

- **`$ARGUMENTS`**: kullanıcının komutla birlikte yazdığı metin. Boşsa sıradaki görev.
- **`/clear`**: görev bitince **dur**. Kullanıcı yeni oturum açar (Codex CLI'de `/new` ya da yeniden başlatma) ve
  "devam" der. Tek oturumda tek görev yapılır; kullanıcı açıkça `plan` demedikçe sonraki göreve geçme.
- **advisor / hafıza dizini**: Codex'te yok. Kararsız kaldığında kullanıcıya sor. Kalıcı bilgiyi ILERLEME.md'ye yaz.

### 2.1 Tutarlılık kontrolü toleransı (`devam.md` adım 4)

ILERLEME'deki "Son commit" satırından sonra HEAD'de **yalnız `docs(...)` ve `merge:` commit'leri** varsa durum
tutarlıdır, devam et. Arada **kod commit'i** varsa, dal ILERLEME'deki daldan farklıysa ya da çalışma ağacında §4.3
dışında commit'lenmemiş değişiklik varsa **dur ve kullanıcıya sor**.

### 2.2 Görev yürütürken değişmez kurallar

- **Önce `Files:` bloğunu koddan doğrula.** Bu programda neredeyse her görevin dosya listesi eksik çıktı (ILERLEME
  #221–#227). Bir arayüz değişince **tüm test sahteleri** de değişir; `grep` ile `: IArayuzAdi` uygulayanları bul.
  Eksikse önce planı düzelt, sapmayı "Öğrenilenler"e yaz, sonra uygula.
- **TDD sırası zorunlu:** önce imza adımı (derlenen ama davranışı olmayan iskelet), sonra kırmızı test (kırmızıyı
  gerçekten gör), sonra en az uygulama, sonra yeşil.
- **Kapsam kilidi:** yalnız görevin dosyalarına dokun. Gördüğün başka eksikleri düzeltme; "Öğrenilenler"e not düş.
- **Doküman aynı turda:** kod değiştiyse ilgili `doc/modules/mNN_*.md`, `doc/modules/00_genel_bakis.md`, gerekirse
  `veri_modeli.md`, `doc/pages/*`, `doc/roles/*` ve `doc/architecture/*` aynı commit'te güncellenir. Kapanan N-/P-
  maddeleri `doc/denetim/2026-11-13_kapsamli_proje_incelemesi.md`'de `✅ (PNN Tn)` ile işaretlenir. Dokunduğun her
  dokümanın "Güncelleme: YYYY-MM-DD" tarihini bugüne çek. Kod ile doküman çelişirse dokümanı koda göre düzelt.
- **Kanıtsız "bitti" yok:** her "yeşil" iddiası o oturumda gerçekten koşturulmuş bir komut çıktısına dayanır.

## 3. Doğrulama komutları (CI ile aynı)

```bash
# Backend: CI derlemesi uyarıları hata sayar
dotnet tool restore
dotnet build EgitimUssu.slnx --configuration Release -warnaserror
dotnet test EgitimUssu.slnx --nologo        # özetteki "Başarısız: N" ve "Atlanan: N" satırlarını oku

# Migration eklendiyse: drift betiği --no-build kullanır, build'den SONRA çalıştır
bash scripts/check-migration-drift.sh
bash scripts/check-migration-down.sh       # Down() metotlarını denetler

# Mobil değiştiyse (repo kökünden; parantez alt kabukta çalıştırır, sonra kökte kalınır)
(cd mobile && flutter pub get && flutter test && flutter analyze --fatal-infos)
git checkout -- mobile/analysis_options.yaml   # her analyze sonrası, bkz. §4.3
```

- Çıktıyı `| tail` ile kırpma. Pipe çıkış kodunu maskeler; özet satırını tam çıktıdan oku.
- **`Real*` integration testleri Docker ister.** Testcontainers gerçek Postgres ve Redis ayağa kaldırır. Docker yoksa
  ya da sandbox Docker'a erişimi engelliyorsa bu testler `Skip.IfNot(fixture.Available, ...)` ile **atlanır**. InMemory
  testleri yine koşar ve sonuç yeşil görünür (`tests/Integration/ContainerFixture.cs`). **Belirti: özette
  `Atlanan: N > 0`.** Bu durumda sonucu **"Docker testleri doğrulanmadı"** diye bildir ve görevi kapatma. Gerekirse
  kullanıcıdan Docker'ı başlatmasını ya da Codex'i Docker ve ağ erişimiyle açmasını iste. `dotnet restore` ve
  `flutter pub get` de ağ ister.
- Tam paket: `bash scripts/test-with-docker.sh`.
- **Bilinen aralıklı testler** (kod sorunu sanma, yeniden koş, sahipleri P26): `DispatcherCacheTests` (#194),
  `TeacherWorkflowIntegrationTests` (#205), `RealMessagingChainTests` hız sınırı testi (#210). Tekrarlıyorsa bildir.
- `-warnaserror --no-incremental` ile XML belgelerindeki kısmi `<param>` uyarısı (CS1573) yakalanır. Bir metoda
  parametre eklediğinde ve metodun `<param>` etiketleri varsa yenisini de yaz.

## 4. Git kuralları

### 4.1 Commit

- **`git add -A` ve `git add .` yasak.** Yalnız görevin dosyalarını ve doküman dosyalarını yol yol ekle.
  Commit öncesi `git diff --cached --stat` ile kontrol et.
- Tek görev, tek commit. Mesaj için planda o görev için yazılmış komutu kullan (Conventional Commits).
  ILERLEME güncellemesi ayrı bir `docs(ilerleme): ...` commit'idir.
- Codex commit'lerine `Co-Authored-By: Claude …` satırı **ekleme**. Geçmiş commit'lerdeki bu satır Claude Code'a
  aittir. Kullanıcı başka bir imza isterse onu kullan.
- Yerel git kimliğini (`user.name`/`user.email`) **değiştirme**. Repo yerel kimlikle ve `origin` uzak adıyla
  çalışacak şekilde ayarlı.

### 4.2 Dal, merge, push

- Plan başına bir dal: `feat/pNN-<kisa-ad>`. Plan ortasındaysan ILERLEME'deki dalda kal.
- **`main`'e merge ve `git push` yalnız kullanıcı açıkça isteyince** yapılır. Merge `--no-ff` ile ve
  `merge: PNN <ad> → main` mesajıyla atılır. Push öncesi `git diff --stat origin/main..main` çıktısında §4.3
  dosyalarının olmadığını kontrol et.
- Geçmişi yeniden yazma: `--force`, `reset --hard`, `rebase` ve `--no-verify` yok.

### 4.3 Commit'lenmeyen dosyalar (repo Windows ve macOS'ta geliştiriliyor)

Bu dosyalar platforma göre değişir. **Hiçbir zaman** commit'leme, `main`'e taşıma:

- `.vscode/launch.json`, `.vscode/tasks.json`
- `mobile/.metadata`, `mobile/pubspec.lock` (paket ekleyince lock da değişir: `git restore --staged mobile/pubspec.lock`)
- `mobile/ios/Flutter/*.xcconfig`
- `mobile/macos/Flutter/GeneratedPluginRegistrant.swift`, `mobile/windows/flutter/generated_plugin*` (Flutter
  bunları satır sonu farkıyla yeniden üretir; çalışma ağacında değişmiş görünmeleri normaldir)
- **`mobile/analysis_options.yaml`**: kullanıcı kararıyla geri alınır. `flutter analyze` (Flutter 3.47) her koşuda bu
  dosyaya 8 satırlık bir `analyzer.exclude` bloğu ekliyor. Her analyze sonrası
  `git checkout -- mobile/analysis_options.yaml` çalıştır. Dışlama olmadan da analyze temiz geçer.
- Üretilen yerelleştirme dosyaları `mobile/lib/l10n/app_localizations*.dart` gitignore'dadır. Yalnız
  `app_tr.arb` ve `app_en.arb` commit'lenir.

macOS dosyaları (`mobile/macos/*`, `mobile/ios/Podfile` vb.) repoda kalmalıdır. Windows'ta silinmiş görünürlerse
silmeyi commit'leme.

## 5. Kurulu mimari sözleşmeler (dokunmadan önce bil)

Ayrıntı ve gerekçeleri ILERLEME "Öğrenilenler" #221–#227'de.

- **Yerel takvim (K-19):** gün, hafta ve ay kararları yalnız `ILocalCalendar` (`src/Shared/Kernel/ILocalCalendar.cs`)
  ile verilir. UTC'den `.Date`, `DateOnly.FromDateTime(`, `UtcNow.Day` gibi kalıplar yasak.
  `tests/Architecture/LocalTimeRuleTests.cs` tüm `src/`'yi tarar. İzin listesi dosya adı, satırın **birebir metni** ve
  gerekçe tutar; satır değişirse giriş bayatlar ve test bilerek kırılır.
- **Tekrar genişletme:** `RecurrenceExpander.Expand(..., TimeZoneInfo zone, ...)` saf ve statik bir fonksiyondur.
  Dilim `calendar.SeriesZone(lesson.TimeZone)` ile verilir. İstisnalı aşırı yükleme serinin **tüm** istisnalarını ister.
- **İstisnalar:** tek yazma yolu upsert'tir (`AddExceptionAsync` kaldırıldı). `(seri, oluşum)` benzersizdir.
- **Çakışma ve kilit:** `TeacherScheduleConflicts` oluşumları genişleterek bakar (ufuk `Scheduling:ConflictHorizonWeeks`).
  Yazma yolları `ITeacherScheduleLock.RunExclusiveAsync` içinde, aynı transaction'da çalışır. İş birimi yeniden
  denenebilir yazılır.
- **Oluşum kapsamı:** `OccurrenceScopeRules` (`src/Modules/Scheduling/Application/LessonScheduleFeatures.cs`). Tekrarlı
  derste kapsam zorunludur. `Single` ve `ThisAndFuture` için anahtar serinin orijinal oluşumu olmalıdır.
- **Hata kodları:** `modul.kod` biçimindedir. HTTP durumu modülün `*Module.cs` dosyasında eşlenir; eşlenmezse 400'e düşer.
- **Outbox olayları:** var olan olay kaydına alan **ekleme**. Eski şemayla yazılmış mesajlar bozulur. Gereken veriyi
  sözleşmeden (`I…Directory`) oku.
- **Integration test saati:** `WithLocalNow(...)` (`tests/Integration/RealLocalDayBoundaryTests.cs`) yalnız takvimi
  sabitler. `IClock`'u sabitleme, JWT süresi bozulur.
- **Migration:** modül başına ayrı şema ve DbContext vardır.
  `dotnet ef migrations add <Ad> --project src/Modules/<Modul>/Infrastructure/<proje>.csproj --startup-project src/API.Host/EgitimUssu.API.Host.csproj`
  (bağlantı dizesi için `scripts/add-migration-all.sh` içindeki tasarım zamanı değişkenine bak). Veri göçü `Up`'ta SQL
  ile yazılır. Göç kanıtı ayrı bir veritabanında alınır (#223).
- **Mobil:** Cubit, `go_router`, `dio` ve `get_it` kullanılır. Bir görevin dokunduğu her ekranın metinleri ARB'ye
  taşınır (K-26, tr ve en birlikte). Repository arayüzü değişirse `mobile/test/helpers/` altındaki sahteler de değişir.

## 6. Bu makineye özgü notlar (Windows + Git Bash)

- `.sh` betiklerini `bash scripts/...` ile çalıştır. PowerShell'de `&&` yoktur, `;` kullanılır.
- Git Bash'te `python` yok. Tek tırnak ve parantez içeren `sed` komutları bozuluyor; dosya düzenlemesini düzenleme
  aracıyla yap.
- macOS'ta bu iki madde geçerli değildir; komutlar aynıdır.

## 7. Oturum sonu kontrol listesi

- [ ] Doğrulama gerçekten koşturuldu, sayılar not edildi (ya da "doğrulanmadı" diye açıkça yazıldı)
- [ ] Plan checkbox'ları işaretlendi, görev bittiyse başlığına ✅ eklendi
- [ ] Doküman aynı commit'te güncellendi
- [ ] Görev commit'i atıldı, §4.3 dosyaları dışarıda kaldı
- [ ] ILERLEME.md güncellendi ("ŞU AN", Plan Durumu, Tamamlanan Görevler, Öğrenilenler, `Son güncelleme`) ve
      `docs(ilerleme)` commit'i atıldı
- [ ] Kullanıcıya kısa Türkçe özet verildi: ne yapıldı, commit, gerçek test sayıları, sıradaki görev. Son satır:
      "Yeni oturum aç ve 'devam' yaz."
