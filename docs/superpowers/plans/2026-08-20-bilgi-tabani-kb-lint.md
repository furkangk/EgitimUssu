# Bilgi Tabanı — /kb-lint Öneri Turu Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `/kb-lint` slash-komutu ekle — wiki üzerinde üretken lint (tutarsızlık + eksik-veri imputasyonu (web) + yeni makale adayı + sorulacak soru önerileri) → yalnız rapor `doc/_health/<tarih>-kb-lint.md`. Makine Karpathy "Linting"e tam parite. Health-check yeşil kalsın.

**Architecture:** Ajan = Claude Code. `/kb-lint` Claude-güdümlü (retrieval = INDEX+frontmatter+grep, arama motoru YOK). Web yalnız `WebSearch`/`WebFetch` (yerel tarayıcı/uzantı otomasyonu YASAK). Yalnız-oku + yalnız-rapor; kaynak doc'lar düzenlenmez. **Script değişmez** (rapor `authority: derived`, mevcut şema kapsar).

**Tech Stack:** Markdown + YAML frontmatter, Claude Code slash-komutu, `WebSearch`/`WebFetch` (opsiyonel), git.

## Global Constraints

- **Substrat:** markdown konvansiyon + slash-komut; **yeni repo bağımlılığı YOK, script değişikliği YOK.**
- **Retrieval arama motoru YOK** — INDEX + frontmatter (`tags`/`authority`/`summary`/`status`/`updated`) + `grep` (~74 doküman ölçeğinde yeterli).
- **Yalnız-oku + yalnız-rapor:** `/kb-lint` kaynak dokümanları DÜZENLEMEZ; yalnız `doc/_health/`'e yazar. Oto-düzeltme YOK.
- **Web güvenliği (kullanıcı sert kuralı):** web araştırması yalnız `WebSearch`/`WebFetch`; **bu PC'deki yerel Chrome uzantısı / tarayıcı / tarayıcı otomasyonuna bağlanmaya ASLA çalışma.** Her web-önerisi kaynak URL gösterir. `--no-web` ile web kapatılır.
- **Kanonik gerçeği (INDEX §0) EZME:** çelişkide kod/INDEX doğruluk kaynağıdır; dokümanın düzeltilmesini öner (tersi değil).
- **Rapor Dilim A frontmatter'ı:** `title`/`summary`/`tags`/`authority: derived`/`updated`; gövde sonu `*Güncelleme: <bugün>*` = frontmatter `updated` (DATE kuralı).
- **Health-check yeşil:** `bash doc/_tools/kb_healthcheck.sh doc` → 0 RED / 0 YELLOW, exit 0. Fixture testleri (`test_kb_healthcheck.sh`) 12/12 (script değişmedi).
- Her görev kendi commit'i; mesaj sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## Task 1: `/kb-lint` slash-komutu

**Files:**
- Create: `.claude/commands/kb-lint.md`

**Interfaces:**
- Consumes: Dilim A frontmatter + INDEX (retrieval), Dilim C `kaynaklar/`+`raw/`, `_health/` dizini (Dilim A çıktı alanı), `WebSearch`/`WebFetch` (opsiyonel).
- Produces: `/kb-lint` komutu. Çıktı sözleşmesi: `doc/_health/<bugün YYYY-MM-DD>-kb-lint.md` (Dilim A frontmatter + 4 bölümlü öneri raporu).

- [ ] **Step 1: `.claude/commands/kb-lint.md` dosyasını yaz**

`.claude/commands/kb-lint.md` (aşağıdaki içeriği birebir yaz):
````markdown
---
description: Wiki (doc/+kaynaklar/) üzerinde üretken lint — tutarsızlık + eksik-veri imputasyonu (web) + yeni makale adayı + sorulacak soru önerileri; yalnız rapor doc/_health/'e, oto-düzeltme yok
---

Bilgi tabanı **öneri turu**. Argüman: `$ARGUMENTS` (opsiyonel `scope=` ipucu + opsiyonel `--no-web`). Wiki'yi tarar, veri bütünlüğünü artıracak **öneriler** üretir ve `doc/_health/`'e rapor yazar. **Hiçbir kaynak dokümanı değiştirmez** — düzeltme kullanıcı onayıyla ayrı adımdır.

## 1. Kapsamla (retrieval — arama motoru YOK)
`doc/INDEX.md` + hedef frontmatter (`tags`/`authority`/`summary`/`status`/`updated`) + `grep` ile wiki envanterini çıkar (`modules/`, `roles/`, `architecture/`, `pages/`, `kaynaklar/`, gerekiyorsa `raw/`). `scope=` verilmişse o alana odaklan; yoksa tüm wiki.

## 2. 4 boyutta analiz et
Her bulgu: **konum(lar)** (`dosya:satır`) · **ne** · **neden** · **önerilen eylem**.
- **Tutarsızlık:** doc'lar arası anlamsal çelişki (iki doc farklı sayı/durum/tarih/ad; rol↔modül çelişkisi; frontmatter `status` gövdeyle uyumsuz). Kanonik gerçekle (kod/INDEX §0) çelişen tarafı işaretle — **kanonik ezilmez**, doküman düzeltilsin.
- **Eksik veri:** boş/zayıf/bayat alan (eksik `summary`, "TODO"/"(henüz yok)" gövde, uzun süredir güncellenmemiş `authority: code`). Web açıksa (aşağı bak) `WebSearch`/`WebFetch` ile **doldurma önerisi + kaynak URL**; kapalıysa yalnız "eksik" işaretle ve gerekiyorsa `/kb-ingest`'e yönlendir.
- **Yeni makale adayı:** kavram kümesi / backlink yoğunluğu / kendi doc'u olmayan tekrar eden konu → hedef klasör + taslak başlık + hangi doc'lardan besleneceği.
- **Sorulacak sorular:** boşluğa dayalı "şunu araştır/sor" önerileri; `/kb-ask` (wiki içi) mi `/kb-ingest` (dış) mı uygun olduğunu belirt.
Boyut boşsa raporda "temiz" yaz.

## 3. Web (yalnız Eksik veri boyutu)
`$ARGUMENTS` içinde `--no-web` YOKSA web açıktır. Web yalnız `WebSearch`/`WebFetch` araçlarıyla yapılır. **KURAL: bu PC'deki yerel Chrome uzantısına / tarayıcıya / herhangi bir yerel tarayıcı otomasyonuna bağlanmaya ASLA çalışma.** Her web-önerisi kaynak URL gösterir ve "öneri (türev)" etiketlidir — oto-yazılmaz. `--no-web` ise web'e hiç çıkma.

## 4. Rapor yaz (doc/_health/<bugün>-kb-lint.md)
Frontmatter:
```yaml
---
title: "KB-Lint <bugün YYYY-MM-DD>"
summary: "<tek satır: N tutarsızlık / M eksik / K yeni-makale adayı / L soru önerisi>"
tags: [kb, lint, oneri, rapor]
authority: derived
updated: <bugün YYYY-MM-DD>
---
```
Gövde: en üstte özet sayaçlar (boyut başına) + mod (web açık/kapalı); ardından 4 bölüm (**## Tutarsızlık**, **## Eksik veri**, **## Yeni makale adayı**, **## Sorulacak sorular**), her bölümde bulgu listesi (`konum · ne · neden · önerilen eylem`; web önerisinde kaynak URL). Sonda `*Güncelleme: <bugün>*` (frontmatter `updated` ile EŞİT).

## 5. Doğrula + özet
`bash doc/_tools/kb_healthcheck.sh doc` → yeni raporda FRONTMATTER/DATE bulgusu **yok**, 0 RED. Terminal'e kısa özet bas (boyut başına sayaç + rapor yolu).

## Kurallar
- **Kaynak dokümanları DÜZENLEME** — yalnız oku; yalnız `doc/_health/`'e yaz. Oto-düzeltme YOK (uygulama ayrı, kullanıcı onayıyla).
- **Kanonik gerçeği (INDEX §0) EZME**; çelişkide kod/INDEX doğruluk kaynağı.
- **Web yalnız `WebSearch`/`WebFetch`**; yerel tarayıcı/uzantı otomasyonu YASAK; her web-önerisi kaynak URL gösterir.
- Öneriler türevdir ("<tarih> itibarıyla"); wiki değişince eskiyebilir.
````

- [ ] **Step 2: Komut geçerliliği + health-check + commit**

Run:
```bash
test -f .claude/commands/kb-lint.md && head -3 .claude/commands/kb-lint.md
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: dosya var + `---`/`description:` başlığı görünür; `exit=0` (komut dosyası `doc/` dışında, health-check'i etkilemez).
```bash
git add .claude/commands/kb-lint.md
git commit -m "feat(kb): /kb-lint öneri turu komutu (tutarsızlık+eksik+aday+soru, yalnız rapor)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Dogfood — gerçek öneri turu + INDEX notu

**Files:**
- Create: `doc/_health/2026-08-20-kb-lint.md` (gerçek rapor)
- Modify: `doc/INDEX.md` (§6.1 `_health/` satırına lint notu + tarih)

**Interfaces:**
- Consumes: Task 1 (`/kb-lint` komutu).
- Produces: Makinenin ilk öneri raporu; `/kb-lint` akışının kanıtı + INDEX'te lint kaydı.

- [ ] **Step 1: `/kb-lint`'i gerçek `doc/` üzerinde çalıştır (Adım 1-4)**

`/kb-lint` (web açık) Adım 1-4'ü uygula:
- Retrieval: INDEX + frontmatter + `grep` ile wiki envanteri (~74 doküman).
- 4 boyutu analiz et (gerçek bulgular — uydurma yok; her bulgu `dosya:satır` ile doğrulanabilir):
  - **Tutarsızlık:** ör. `authority: code` doc'ların frontmatter `status`'u ile INDEX/00_genel_bakis durum tablosu; rol doc'ları ↔ modül doc'ları premium/faz ifadeleri.
  - **Eksik veri:** eksik/zayıf `summary`, "(henüz yok)"/TODO gövde, uzun süredir güncellenmemiş `authority: code` (ör. `updated` eski). Web açık: en fazla 1-2 gerçek imputasyon önerisi + kaynak URL (yerel tarayıcı/uzantıya bağlanma).
  - **Yeni makale adayı:** kavram kümesi (ör. tekrar eden ama kendi doc'u olmayan konu — `kaynaklar/` + `modules/` çapraz).
  - **Sorulacak sorular:** boşluğa dayalı 2-3 `/kb-ask`/`/kb-ingest` beslemesi.
- `doc/_health/2026-08-20-kb-lint.md` oluştur: Dilim A frontmatter (`authority: derived`, tags: [kb, lint, oneri, rapor], updated: 2026-08-20) + gövde (özet sayaçlar + mod + 4 bölüm) + sonda `*Güncelleme: 2026-08-20*`.

> Not: rapor türevdir; kanonik gerçeği ezmez. Boş boyut = "temiz".

- [ ] **Step 2: INDEX §6.1 `_health/` satırına lint notu ekle**

`doc/INDEX.md` §6.1'de `_health/` satırını güncelle (health-check + kb-lint çıktıları birlikte):
```markdown
| [`_health/`](_health/) | Health-check + **kb-lint** rapor çıktıları (`YYYY-MM-DD-healthcheck.md` / `YYYY-MM-DD-kb-lint.md`) — pass/fail + severity'li bulgular + kod-drift; `/kb-lint` üretken öneriler (tutarsızlık/eksik/aday/soru) |
```
`Son güncelleme` satırına `/kb-lint öneri turu` notu ekle (DATE çelişkisi olmasın; 2026-08-20 kalsın).

- [ ] **Step 3: Health-check + doğrulama**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "kb-lint" || echo "(kb-lint raporu temiz)"
bash doc/_tools/kb_healthcheck.sh doc | grep -E "	RED	|	YELLOW	" || echo "0 RED/0 YELLOW"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
bash doc/_tools/test_kb_healthcheck.sh | grep -cE "^PASS"; bash doc/_tools/test_kb_healthcheck.sh | grep "^FAIL" || echo "0 FAIL"
```
Expected: kb-lint raporunda FRONTMATTER/DATE bulgusu **yok** (grep boş → "temiz" veya yalnız BLUE); `0 RED/0 YELLOW`; `exit=0`; fixtures `12` PASS / `0 FAIL`. (Rapor `_health/`'te → orphan-muaf, check-7 atlar.)

- [ ] **Step 4: Commit**

```bash
git add doc/_health/2026-08-20-kb-lint.md doc/INDEX.md
git commit -m "docs(kb): dogfood — ilk /kb-lint öneri raporu + INDEX _health/ notu

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Tamamlanma kanıtı (tüm plan)

- `/kb-lint` komutu (`.claude/commands/kb-lint.md`) mevcut; 4 boyut + web (`WebSearch`/`WebFetch`, yerel tarayıcı/uzantı YASAK) + yalnız-rapor kuralları belgelenmiş.
- **Dogfood:** `doc/_health/2026-08-20-kb-lint.md` — gerçek, `dosya:satır` ile doğrulanabilir öneriler (uydurma yok); kanonik gerçeği ezmiyor.
- `bash doc/_tools/kb_healthcheck.sh doc` → **0 RED / 0 YELLOW, exit 0**; fixture testleri **12/12** (script değişmedi).
- INDEX §6.1 `_health/` satırı health-check + kb-lint çıktılarını belgeliyor.
- Kapsam dışı (oto-düzeltme; arama motoru; fan-out; taslak stub; zamanlanmış tur) bu planda **yok**.
- **Makine Karpathy "Linting"e tam parite:** deterministik `/kb-healthcheck` (+`--deep`) + üretken `/kb-lint`. Çekirdek dilimler A-D + check-7 + kb-lint tamam.
