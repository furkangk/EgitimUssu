# Bilgi Tabanı Makinesi — Dilim A: Temel + Health-check (Tasarım)

> **Tarih:** 2026-08-20 · **Durum:** Onaylandı (tasarım) · **Önceki döngü:** [Doküman temizliği Faz 1](2026-08-19-dokuman-temizlik-design.md) (tamamlandı)
>
> **Kaynak fikir:** LLM tabanlı kişisel bilgi tabanı yöntemi (raw → wiki → Q&A → lint). Bu spec, o makinenin EğitimÜssü `doc/`'una uygulanan **ilk dilimini** (konvansiyon temeli + otomatik health-check) tasarlar. Diğer dilimler (Obsidian, ingest, Q&A) ayrı spec'lerdir.

## 1. Amaç ve kapsam

Faz 1 temizliği `doc/`'u **elle** koddan doğrulanmış, tutarlı ve gezilebilir hale getirdi. Bu dilim, o işi **kalıcı bir makineye** çevirir: her dokümana makine-okunur metadata (frontmatter) ekler ve `doc/`'un koddan sapmasını (drift), biçim bozukluğunu ve kanonik ihlalleri **otomatik yakalayan** bir health-check kurar.

**Makinenin substratı (tüm makine için geçerli karar):** Ajan = Claude Code. Makine ayrı bir program/framework değil; **skill/slash-komut + `doc/` markdown konvansiyonları**. Obsidian yalnızca görüntüleyici (Dilim B). Yeni dil/runtime bağımlılığı **eklenmez**; yalnızca saf-bash yardımcı script kabul edilir.

**Bu dilimin kapsamı:**
- `doc/` altındaki tüm markdown'lara YAML frontmatter konvansiyonu + migrasyon.
- Frontmatter konvansiyonunu belgeleyen kısa doküman (Dilim B/C/D buna dayanır).
- `/kb-healthcheck` slash-komutu (deterministik Faz 1 + isteğe bağlı LLM drift Faz 2).
- Deterministik kontroller için committed saf-bash script + test fixture'ları.

**Kapsam dışı (bilinçli, sonraki dilimler / ayrı spec):**
- Obsidian vault config, graph, Marp, görsel konvansiyonu → **Dilim B**.
- `raw/` ham kaynak ingest + `/kb-ingest` derleme → **Dilim C**.
- `/kb-ask` Q&A + çıktı render (md/Marp/matplotlib) + geri dosyalama → **Dilim D**.
- Ayrı arama motoru → **YAGNI** (~73 dosya/125K kelimede INDEX + grep yeter; Karpathy de ~küçük ölçekte RAG gereksiz diyor).
- CI/cron pipeline kurulumu (script CI-hazır olur ama pipeline ayrı iş).
- Backend'i çalıştırmak (Postgres yok — doğrulama kaynağı **okuyarak** yapılır).

## 2. Doğruluk hiyerarşisi (Faz 1 ile aynı)

Çelişkide dokümanı şuna göre düzelt: **1) gerçek kod** (`src/Modules/`, `mobile/lib/`) → **2) INDEX §0 kanonik gerçekler** (ad, .NET sürümü, ana renk, DB) → **3) PRD v2.1** (yalnız ürün niyeti/faz). Health-check bu hiyerarşiyi uygular: `authority: code` dokümanlar koda karşı denetlenir.

## 3. Frontmatter konvansiyonu

Her `doc/**/*.md` dosyasının başına YAML frontmatter eklenir. Gövdedeki mevcut insan-okunur bloklar (`> **Güncelleme:**`, `> **Durum:**`) **korunur**; frontmatter **makine-otoritesidir** ve health-check ikisi çeliştiğinde uyarır.

### 3.1 Şema

```yaml
---
title: "M15 — Ayarlar & Güvenlik"
summary: "Kullanıcı bildirim/gizlilik/güvenlik ayarları; study-sharing endpoint + CQRS mevcut, tam CRUD eksik"
tags: [modul, settings, gizlilik, faz-0]
status: "🟡"                # 🟢 tam | 🟡 kısmi | 🔴 iskelet | (yoksa alan atlanır)
authority: code             # code | product | derived | archive
code_refs:
  - src/Modules/Settings/**
updated: 2026-08-19
---
```

| Alan | Zorunlu | Rol |
|------|---------|-----|
| `title` | hayır | İnsan başlığı (Obsidian/INDEX); yoksa H1 kullanılır |
| `summary` | **evet** | Tek satır özet — INDEX oto-üretimi, Obsidian hover, ingest bunu kullanır |
| `tags` | evet (≥1) | Obsidian graph + health-check filtreleme; kebab-case |
| `status` | koşullu | Modül/ekran/rol dokümanlarında 🟢/🟡/🔴; product/archive'da atlanır |
| `authority` | **evet** | Drift davranışını belirler (§3.2) |
| `code_refs` | koşullu | `authority: code` ise ≥1 glob; product/archive'da boş/atlanır |
| `updated` | **evet** | ISO tarih; gövde `Güncelleme:` ile çelişirse Faz 1 uyarır |

### 3.2 `authority` değerleri ve drift davranışı

- **`code`** — Kod doğruluk kaynağı. `code_refs`'teki gerçek koda karşı endpoint/enum/domain drift denetlenir (Faz 2). Örn: `modules/mNN_*`, `architecture/backend.md`, `architecture/mobile_flutter.md`, `architecture/widgets.md`, `pages/*`.
- **`product`** — Ürün niyeti/plan; kod karşılığı yok, drift denetlenmez. Örn: `ozel_ders_platformu_PRD_v2.md`, `yol_haritasi.md`, `architecture/web_angular.md` (🔴 planlanan).
- **`derived`** — Başka dokümanlardan türer; dolaylı tutarlılık denetlenir. Örn: `roles/*` (iddia edilen modül durumları, modül frontmatter'larıyla tutarlı mı), `_health/*` raporları, `INDEX.md`.
- **`archive`** — `doc/_arsiv/*`; tüm drift/lint atlanır (yalnız kırık link + fence kontrol edilir).

### 3.3 `code_refs` — aile kalıpları

| Doküman ailesi | `code_refs` kalıbı |
|----------------|--------------------|
| `modules/mNN_<ad>.md` | `src/Modules/<Ad>/**` |
| `modules/00_genel_bakis.md` | `src/Modules/*/API/*Module.cs` |
| `modules/veri_modeli.md` | `src/Modules/*/Domain/**` |
| `architecture/backend.md` | `src/**` (yapı düzeyinde) |
| `architecture/mobile_flutter.md` | `mobile/lib/**` |
| `architecture/widgets.md` | `mobile/lib/shared/widgets/**` |
| `pages/*.md` | `mobile/lib/features/**/presentation/pages/*.dart` + `mobile/lib/core/routing/app_router.dart` |
| `roles/*.md` | (authority: derived — `code_refs` boş; ilgili modül dokümanlarına dolaylı) |
| `PRD`, `yol_haritasi`, `INDEX` | (authority: product/derived — `code_refs` boş) |

## 4. `/kb-healthcheck` komutu

**Form:** `.claude/commands/kb-healthcheck.md` (slash-komut; orkestrasyon prompt'u) + `doc/_tools/kb_healthcheck.sh` (deterministik Faz 1; saf bash). Komut: script'i çalıştır → çıktıyı oku → (isteğe bağlı) Faz 2 drift → rapor yaz.

**Kademeli maliyet:**
- `/kb-healthcheck` → yalnız **Faz 1** (deterministik, saniyeler, ucuz, loop/CI'ye uygun).
- `/kb-healthcheck --deep` → **Faz 1 + Faz 2** (tam kod-senkron denetimi; Faz 1'de elle yaptığımızın otomasyonu).

### 4.1 Faz 1 — deterministik (bash)

`kb_healthcheck.sh` şunları tarar ve makine-okunur (severity-etiketli) satırlar üretir:

1. Kırık göreli md link (hedef dosya yok).
2. Kapanmamış kod bloğu (tek sayıda ` ``` `).
3. Kanonik süpürme: `EgittimUssu` çift-t (kural tanım satırı hariç), yanlış `.NET [0-8]` sürümü, `0xFF082B4F` dışı `primary`/ana-renk şüphesi.
4. Frontmatter şema geçerliliği: zorunlu alanlar (`summary`, `tags`, `authority`, `updated`) var mı; `authority: code` ise `code_refs` dolu mu.
5. `code_refs` var-olmayan dosya/glob'a işaret ediyor mu.
6. Gövde `Güncelleme:` ↔ frontmatter `updated` çelişkisi.
7. Öksüz doküman: `find doc` sonucunda olup `INDEX.md`'de linki olmayan dosya.
8. Frontmatter `status` ↔ INDEX modül durum tablosu çelişkisi.

> Script yeni bağımlılık kullanmaz (grep/sed/find/awk). Çıktı formatı: `SEVERITY\tKONTROL\tdosya:satır\tmesaj` (komutun ayrıştırması için).

### 4.2 Faz 2 — LLM drift (fan-out, `--deep`)

- `authority: code` olan **her doküman için paralel bir alt-ajan** dispatch edilir (fan-out). Her ajan: dokümanı + `code_refs`'teki gerçek kodu okur → endpoint/enum/domain envanterini çıkarır → dokümanın iddiasıyla diff'ler → **yapısal drift bulgusu** döndürür (dosya, tür, doküman-diyor, kod-diyor, severity).
- `authority: derived` (roller) → dolaylı: iddia edilen modül durumları, ilgili modül frontmatter `status`'larıyla tutarlı mı.
- `product`/`archive` → Faz 2 atlanır.
- Komut tüm bulguları toplar, dedup'lar, severity sıralar.

### 4.3 Rapor çıktısı

`doc/_health/YYYY-MM-DD-healthcheck.md` (commit'lenen artefakt, `authority: derived`) + terminal özeti.

- En üstte: pass/fail + sayaçlar (🔴/🟡/🔵), taranan dosya sayısı, mod (Faz 1 / `--deep`).
- Severity sıralı bulgu listesi:
  - 🔴 **kırık/yanlış** — kırık link, kapanmamış fence, kanonik ihlal, kaldırılmış endpoint hâlâ belgeli, yanlış enum değeri.
  - 🟡 **bayat/uyumsuz** — tarih çelişkisi, status uyumsuzluğu, eksik frontmatter alanı, `code_refs` kırık.
  - 🔵 **öneri** — öksüz doküman, eksik backlink, yeni makale/bağlantı adayı.
- Her bulgu: `dosya:satır` + ne + beklenen (Faz 1 "Tamamlanma kanıtı" formatı).

## 5. Doğrulama / kabul kriterleri

- **Yeşil taban (asıl kabul testi):** Faz 1 temizliği yeni bitti → `/kb-healthcheck` mevcut `doc/`'ta çalışınca Faz 1 kontrolleri **0 gerçek bulgu** vermeli (elle doğruladığımız durumu üretmeli; yalnız kural-tanımı istisnaları — `EgittimUssu` tanım satırı, D4 "çözüldü" notu — beyaz-listede).
- **Kırmızı fixture:** `doc/_tools/fixtures/` altında bilerek bozulmuş küçük örnekler (bir kırık link, bir kapanmamış fence, bir `EgittimUssu`, bir eksik frontmatter). Script her birini yakalamalı (TDD: önce kırmızı fixture, sonra script).
- **Faz 2 gerçek değeri:** `--deep` mevcut `doc/`'ta çalışınca ya "temiz" demeli ya da **gerçek** kalan drift bulmalı; bulursa aynı turda düzeltilir (aracın ilk somut getirisi).
- **Frontmatter tamlığı:** 73 dosyanın hepsinde geçerli frontmatter; `authority: code` olanların hepsinde çözülen `code_refs`.
- **Regresyon yok:** Migrasyon yalnız frontmatter ekler; gövde içeriği/anlamı değişmez (blockquote korunur).

## 6. Teslimatlar ve uygulama sırası

```
doc/
  <73 md>              → her birine YAML frontmatter (§3)
  00_kb_konvansiyon.md → frontmatter konvansiyonu + authority kuralları (Dilim B/C/D temeli)
  _tools/
    kb_healthcheck.sh  → deterministik Faz 1 (saf bash)
    fixtures/          → kırmızı test örnekleri
  _health/
    .gitkeep           → rapor çıktı klasörü
.claude/commands/
    kb-healthcheck.md  → slash-komut (Faz 1 sarar + Faz 2 fan-out)
```

**Uygulama sırası (plan bunu görev görev açar):**
1. `doc/00_kb_konvansiyon.md` — frontmatter şeması + authority + `code_refs` aile kalıpları.
2. `kb_healthcheck.sh` + `fixtures/` — deterministik Faz 1 (TDD: kırmızı fixture → script → yeşil).
3. Frontmatter migrasyonu — aile başına geçiş: `modules/` → `architecture/` → `pages/` → `roles/` → `product/index/arsiv`. Her dosyada `summary`+`tags`+`authority`+`code_refs`+`updated`.
4. `.claude/commands/kb-healthcheck.md` — script orkestrasyonu + Faz 2 fan-out prompt'u + rapor yazımı.
5. Doğrulama — `/kb-healthcheck` yeşil taban; `/kb-healthcheck --deep` çalıştır, çıkan gerçek drift'i düzelt; `_health/` raporunu commit'le.

## 7. Riskler ve kararlar

- **Migrasyon hacmi (73 dosya: 69 + 4 arşiv):** `summary`/`code_refs` dosya-başı yargı gerektirir; aile başına LLM geçişiyle (paralelleştirilebilir) yönetilir. En büyük iş kalemi bu.
- **Faz 2 maliyeti:** `--deep` fan-out token-yoğun; bu yüzden varsayılan komut yalnız Faz 1'dir, derinlik opt-in.
- **Beyaz-liste bakımı:** Kanonik süpürmede meşru istisnalar (kural tanımı, "çözüldü" notları) script'te açık beyaz-listede tutulur; yanlış-pozitifi önler.
- **`authority: derived` roller kontrolü:** Kod yerine modül frontmatter'ına dayanır → modül frontmatter'ları doğru olmalı (sıra 3'te modules önce migrate edilir).

## 8. Sonraki dilimler (bağlam)

- **Dilim B — Obsidian görünümü:** vault config + graph + Marp + görsel konvansiyonu (bu dilimin `tags`/`summary`'sinden beslenir).
- **Dilim C — Ingest + derleme:** `raw/` + `/kb-ingest` (bu dilimin konvansiyonuna dayanır).
- **Dilim D — Q&A + çıktı render:** `/kb-ask` + Marp/matplotlib + geri dosyalama.
