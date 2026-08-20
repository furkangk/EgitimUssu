# Bilgi Tabanı Makinesi — Dilim D (Q&A + Çıktı Render) Uygulama Planı

> **Agentic worker için:** GEREKLİ ALT-SKILL: superpowers:subagent-driven-development (önerilen) veya superpowers:executing-plans. Adımlar checkbox (`- [ ]`) kullanır.
>
> **Not:** Bu planın spec'i canlı brainstorm yerine önceden yazıldı. Uygulamaya başlamadan spec §9 "Kararlar"ı gözden geçir (özellikle `_cevaplar/` adı + matplotlib opsiyonelliği); kullanıcı farklı isterse ilgili görev metnini uyarla.

**Goal:** `/kb-ask` slash-komutu + `doc/_cevaplar/` çıktı alanı ekle; wiki üzerine soru → araştır → md/Marp/mermaid/(ops.)matplotlib çıktı → geri dosyala. Marp'ı etkinleştir. Health-check yeşil kalsın.

**Architecture:** Ajan = Claude Code. `/kb-ask` Claude-güdümlü (retrieval = INDEX+frontmatter+grep, arama motoru YOK). Çıktı `doc/_cevaplar/`'a `authority: derived` dokümanı olarak dosyalanır. Marp/matplotlib görüntüleyici/render araçları; yoksa graceful degrade. Script değişikliği gerekmez (derived zaten geçerli).

**Tech Stack:** Markdown + YAML frontmatter, Claude Code slash-komutu, Marp (Obsidian plugin, öneri), opsiyonel python3+matplotlib, git.

## Global Constraints

- **Substrat:** markdown konvansiyon + slash-komut; yeni **repo bağımlılığı yok**. Marp = Obsidian plugin (kullanıcı kurar); matplotlib = ortamda **varsa** kullanılır, kurulmaz.
- **Retrieval arama motoru YOK** — INDEX + frontmatter (`tags`/`authority`/`summary`) + `grep`. (~74 doküman ölçeğinde yeterli.)
- **`/kb-ask` yalnız wiki'ye sorar** (web değil); dış bilgi → önce `/kb-ingest` (Dilim C).
- **Cevaplar `authority: derived`**, kaynak gösterir (## Kaynaklar backlink), kanonik gerçeği (INDEX §0) **ezmez**.
- **`/kb-ask` kaynak dokümanları düzenlemez** (yalnız okur); yalnız `doc/_cevaplar/` + `doc/_assets/`'e yazar.
- **Health-check yeşil:** `bash doc/_tools/kb_healthcheck.sh doc` → 0 RED / 0 YELLOW, exit 0. Yeni `.md`'ler Dilim A frontmatter'ı (`summary`/`tags`/`authority`/`updated`; gövde `Güncelleme` = `updated`).
- Her görev kendi commit'i; mesaj sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## Task 1: Marp etkinleştirme

**Files:**
- Modify: `doc/.obsidian/community-plugins.json` (marp ekle)
- Modify: `doc/_obsidian_kurulum.md` ("Sırada ne var" → Marp aktif + /kb-ask)

**Interfaces:**
- Produces: Marp önerilen plugin listesinde; kurulum dokümanı Marp'ı belgeler. (Slayt üretimi Task 2/3'te `/kb-ask format=marp` ile.)

- [ ] **Step 1: `community-plugins.json`'a marp ekle**

`doc/.obsidian/community-plugins.json`:
```json
["dataview", "marp"]
```

- [ ] **Step 2: `_obsidian_kurulum.md` "6. Sırada ne var" bölümünü güncelle**

Mevcut §6 ("Sırada ne var") bloğunu şununla değiştir:
```markdown
## 6. Slaytlar (Marp) ve Q&A
**Marp** artık aktif: **Settings → Community plugins → "Marp"** kur+etkinleştir → `marp: true` frontmatter'lı dosyalar slayt olarak önizlenir/export edilir. `/kb-ask format=marp` bu formatta çıktı üretir.
**`/kb-ask`** (Dilim D): wiki'ye soru sor → Claude ilgili dokümanları okuyup cevabı `doc/_cevaplar/`'a md/Marp/mermaid/grafik olarak dosyalar.
Dış kaynak eklemek için `/kb-ingest` (Dilim C).
```
`updated`/gövde `Güncelleme` = 2026-08-20 (zaten öyle; eşit kalsın).

- [ ] **Step 3: JSON geçerli + health-check + commit**

Run:
```bash
python3 -m json.tool doc/.obsidian/community-plugins.json >/dev/null && echo "JSON OK"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: `JSON OK`, `exit=0`.
```bash
git add doc/.obsidian/community-plugins.json doc/_obsidian_kurulum.md
git commit -m "feat(kb): Dilim D — Marp etkinleştirme (community-plugins + kurulum dokümanı)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `/kb-ask` komutu + `_cevaplar/` alanı

**Files:**
- Create: `doc/_cevaplar/00_cevaplar_index.md`
- Create: `.claude/commands/kb-ask.md`
- Modify: `doc/INDEX.md` (§6.1 satır + tarih)

**Interfaces:**
- Consumes: Dilim A frontmatter + INDEX (retrieval), Dilim C kaynaklar/.
- Produces: `/kb-ask` komutu + cevap indeksi + INDEX kaydı.

- [ ] **Step 1: `doc/_cevaplar/00_cevaplar_index.md` oluştur**

```markdown
---
title: "Cevaplar İndeksi"
summary: "/kb-ask ile üretilen Q&A çıktılarının (soru + tarih + link) indeksi; her cevap kullandığı kaynaklara backlink verir"
tags: [kb, cevaplar, indeks, qa]
authority: derived
updated: 2026-08-20
---

# 💬 Cevaplar İndeksi

> `/kb-ask` ile wiki üzerine sorulan soruların yanıtları. Her cevap `authority: derived`; kullandığı
> dokümanlara "## Kaynaklar" altında backlink verir ve kanonik gerçeği ezmez. Cevaplar "eklenir" —
> gelecekteki sorguları zenginleştirir.

| Soru | Tarih | Cevap |
|------|-------|-------|
| _(henüz yok)_ | | |

*Güncelleme: 2026-08-20*
```

- [ ] **Step 2: `/kb-ask` slash-komutunu yaz**

`.claude/commands/kb-ask.md`:
````markdown
---
description: Wiki'ye (doc/+kaynaklar/) soru sor → ilgili dokümanları okuyup cevabı doc/_cevaplar/'a md/Marp/mermaid/grafik olarak dosyala
---

Bilgi tabanına **soru sor**. Argüman: `$ARGUMENTS` (soru + opsiyonel `format=md|marp|mermaid|chart` [varsayılan md] + opsiyonel `scope=` ipucu). Yalnız **wiki'ye** sorulur; dış bilgi gerekiyorsa önce `/kb-ingest`.

## 1. Kapsamla (retrieval — arama motoru YOK)
`doc/INDEX.md` + hedef frontmatter (`tags`/`authority`/`summary`) + `grep` ile soruyla ilgili dokümanları bul (`modules/`, `roles/`, `architecture/`, `pages/`, `kaynaklar/`). En fazla ~10-15 ilgili dokümanı seç.

## 2. Oku + sentezle
Seçilen dokümanları oku; soruyu yanıtla. Çelişki/eksik/bayat veri görürsen belirt (ve gerekiyorsa `/kb-healthcheck --deep` öner). Kanonik gerçeği (INDEX §0) EZME.

## 3. Render (istenen format)
- **md** (varsayılan): yapılandırılmış markdown rapor.
- **marp**: frontmatter'a `marp: true` ekle + gövdede `---` slayt ayraçları (Obsidian Marp plugin'iyle önizlenir).
- **mermaid**: ilişki/akış diyagramı (` ```mermaid ` bloğu; natif render).
- **chart**: `python3 -c "import matplotlib" 2>/dev/null` başarılıysa küçük bir python3 script'iyle PNG üret → `doc/_assets/<slug>.png` → cevaba `![](../_assets/<slug>.png)` ile göm. Başarısızsa mermaid/tabloya düş + "matplotlib yok" notu.

## 4. Geri dosyala (doc/_cevaplar/<slug>.md)
Frontmatter:
```yaml
---
title: "<soru başlığı>"
summary: "<tek satır: sorunun kısa cevabı>"
tags: [cevap, <2-3 konu>]
authority: derived
question: "<soru metni>"
updated: <bugün YYYY-MM-DD>
---
```
(format=marp ise frontmatter'a ayrıca `marp: true`.) Gövde: cevap → (render çıktısı gömülü) → "## Kaynaklar" (kullanılan doc'lara göreli backlink) → sonda `*Güncelleme: <bugün>*` (frontmatter `updated` ile EŞİT).

## 5. Kaydet (index)
`doc/_cevaplar/00_cevaplar_index.md` tablosuna satır ekle (`_(henüz yok)_`'u değiştir/altına): `| <soru> | <bugün> | [<slug>](<slug>.md) |`.

## 6. Doğrula
`bash doc/_tools/kb_healthcheck.sh doc` → yeni cevapta FRONTMATTER/DATE bulgusu **yok**, 0 RED. Sorun varsa düzelt.

## Kurallar
- **Kaynak dokümanları DÜZENLEME** — yalnız oku; yalnız `_cevaplar/` + `_assets/`'e yaz.
- Cevap **türev**dir ("<tarih> itibarıyla"); kod değişirse eskiyebilir. Kaynak göster.
- matplotlib **kurma** — yoksa mermaid/tabloya düş.
````

- [ ] **Step 3: INDEX §6.1'e `_cevaplar/` ekle**

`doc/INDEX.md` §6.1 tablosuna (`raw/` satırından sonra) ekle:
```markdown
| [`_cevaplar/`](_cevaplar/00_cevaplar_index.md) | **Q&A** (Dilim D): `/kb-ask` ile wiki'ye sorulan soruların cevapları (md/Marp/mermaid/grafik) + kaynak backlink. İndeks: `00_cevaplar_index.md` |
```
§6.1 başlığını `(Dilim A + B + C + D)` yap. `updated`/`Son güncelleme` = 2026-08-20 (Dilim D notu ekle; DATE çelişkisi olmasın).

- [ ] **Step 4: Health-check yeşil + commit**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep -E "RED|	YELLOW	" || echo "0 RED/0 YELLOW"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: `0 RED/0 YELLOW`, `exit=0` (`00_cevaplar_index` INDEX'te; komut dosyası doc/ dışında).
```bash
git add doc/_cevaplar .claude/commands/kb-ask.md doc/INDEX.md
git commit -m "feat(kb): Dilim D — /kb-ask komutu + _cevaplar/ alanı + INDEX kaydı

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Dogfood — gerçek soru + kabul

**Files:**
- Create: `doc/_cevaplar/uyelik-premium-etkisi.md` (örnek cevap)
- Modify: `doc/_cevaplar/00_cevaplar_index.md` (satır ekle)

**Interfaces:**
- Consumes: Task 2 (komut + iskelet).
- Produces: Makinenin ilk Q&A çıktısı; `/kb-ask` akışının kanıtı.

- [ ] **Step 1: `/kb-ask` akışıyla gerçek soruyu yanıtla**

Soru: **"Üyelik/premium hangi modülleri ve rolleri etkiliyor?"** `/kb-ask` Adım 1-4'ü uygula:
- Retrieval: `grep -rl -i "premium\|membership\|üyelik\|MembershipTier" doc` + INDEX ile ilgili dokümanları bul (beklenen: `modules/m17_membership`, `modules/m08_study` (Ö-D Free/Premium), `modules/m14_reporting` (premium analiz), `roles/*` premium yetenekleri, `kaynaklar/proje-vizyonu-promp` (vizyon)).
- Oku + sentezle: hangi modüller (m17 çekirdek, m08 kapılar, m14 premium raporlar) + roller (öğretmen/öğrenci/veli premium setleri) etkileniyor.
- `doc/_cevaplar/uyelik-premium-etkisi.md` oluştur: frontmatter (authority: derived, `question`, tags: [cevap, premium, uyelik], updated: 2026-08-20) + gövde: kısa cevap → "## Modüller" → "## Roller" → "## Kaynaklar" (backlink'ler: `../modules/m17_membership.md`, `../modules/m08_study.md`, `../modules/m14_reporting.md`, `../kaynaklar/proje-vizyonu-promp.md`, ilgili `../roles/*`) → `*Güncelleme: 2026-08-20*`. "2026-08-20 itibarıyla" notu.

> Not: bu cevap türevdir; kanonik gerçeği ezmez (m17 🔴 planlanan olduğunu belirt — premium çekirdeği bugün yalnız m08'de).

- [ ] **Step 2: `00_cevaplar_index.md`'ye satır ekle**

`_(henüz yok)_` satırını şununla değiştir:
```markdown
| Üyelik/premium hangi modülleri ve rolleri etkiliyor? | 2026-08-20 | [uyelik-premium-etkisi](uyelik-premium-etkisi.md) |
```

- [ ] **Step 3: Health-check + backlink doğrulaması**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "_cevaplar" || echo "(cevaplar temiz veya sadece BLUE)"
bash doc/_tools/kb_healthcheck.sh doc | grep -E "RED|	YELLOW	" || echo "0 RED/0 YELLOW"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: cevapta FRONTMATTER/DATE bulgusu **yok**; `0 RED/0 YELLOW`; `exit=0`. Cevap `00_cevaplar_index`'te → BLUE ORPHAN olabilir (check-7 sınırı, non-blocking). Backlink hedefleri (m17/m08/m14/kaynaklar/roller) var → link kontrolü temiz.

- [ ] **Step 4: Commit**

```bash
git add doc/_cevaplar
git commit -m "docs(kb): Dilim D — dogfood: üyelik/premium etkisi Q&A çıktısı

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Tamamlanma kanıtı (tüm plan)

- `bash doc/_tools/kb_healthcheck.sh doc` → **0 RED / 0 YELLOW, exit 0**; fixture testleri (`test_kb_healthcheck.sh`) hâlâ yeşil (script değişmedi).
- `/kb-ask` komutu (`.claude/commands/kb-ask.md`) + `doc/_cevaplar/00_cevaplar_index.md` mevcut; INDEX §6.1'de kayıtlı (Dilim A+B+C+D).
- Marp `community-plugins.json`'da; `_obsidian_kurulum.md` Marp'ı + `/kb-ask`'i belgeler.
- **Dogfood:** `doc/_cevaplar/uyelik-premium-etkisi.md` — gerçek soruya cevap, m17/m08/m14/kaynaklar/roller backlink'leri çözülüyor, kanonik gerçeği ezmiyor (m17 🔴 notu), health-check temiz.
- Cevaplar `authority: derived`, kaynak gösterir; `/kb-ask` çekirdek dokümanları düzenlemedi.
- Kapsam dışı (arama motoru = YAGNI; matplotlib kurulumu; web araştırması) bu planda **yok**.
- **Makinenin 4 çekirdek dilimi (A-D) tamam.** Opsiyonel sonraki işler: check-7 orphan iyileştirmesi, lint/öneri turu (spec §8).
```
