---
title: "Bilgi Tabanı Dilim D — Q&A + Çıktı Render (Tasarım)"
summary: "/kb-ask: doc/+kaynaklar/ üzerine soru → araştır → md/Marp/mermaid/matplotlib çıktı → doc/_cevaplar/'a geri dosyala; Marp kurulumu burada"
tags: [kb, dilim-d, qa, render, tasarim, spec]
authority: derived
updated: 2026-08-20
---

# Bilgi Tabanı Makinesi — Dilim D: Q&A + Çıktı Render (Tasarım)

> **Tarih:** 2026-08-20 · **Durum:** Ön-yazım (canlı brainstorm yerine kullanıcı talebiyle önceden yazıldı; uygulayan oturum §9 kararlarını gözden geçirebilir) · **Önceki dilimler:** [A — Temel](2026-08-20-bilgi-tabani-dilim-a-design.md) · [B — Obsidian](2026-08-20-bilgi-tabani-dilim-b-design.md) · [C — Ingest](2026-08-20-bilgi-tabani-dilim-c-design.md) (hepsi tamam)
>
> **Kaynak fikir:** LLM bilgi tabanı yönteminin "Q&A + output" katmanı — makinenin son çekirdek dilimi. Wiki büyüdükçe (doc/ + kaynaklar/) karmaşık sorular sorulur; Claude ilgili dokümanları okuyup cevaplar ve çıktıyı (md/slayt/grafik) wiki'ye **geri dosyalar** ki her sorgu bilgi tabanına "eklensin".

## 1. Amaç ve kapsam

Dilim A (frontmatter+health-check), B (Obsidian görünümü), C (ingest) makineyi kurdu. Dilim D **sorgu + çıktı** katmanını ekler: `/kb-ask <soru>` → ilgili dokümanları bul/oku (INDEX + frontmatter + grep; **RAG/arama motoru yok**) → sentezle → istenen formatta çıktı (md raporu / Marp slayt / mermaid diyagram / opsiyonel matplotlib) → `doc/_cevaplar/`'a kaydet (kaynak backlink'leriyle) → index'e ekle. Böylece cevaplar "eklenir" ve gelecekteki sorguları zenginleştirir.

**Substrat:** Ajan = Claude Code. Makine = markdown konvansiyon + slash-komut; **yeni repo bağımlılığı yok**. Marp ve matplotlib kullanıcının/ortamın araçlarıdır (repo build bağımlılığı değil); yoksa çıktı zarifçe düşer (bkz. §5).

**Bu dilimin kapsamı:**
- `/kb-ask` slash-komutu (`.claude/commands/kb-ask.md`).
- `doc/_cevaplar/` — cevap çıktı alanı + `00_cevaplar_index.md`.
- Çıktı formatları: md raporu (varsayılan), Marp slayt (md), mermaid diyagram (natif), matplotlib PNG (opsiyonel, ortamda varsa).
- **Marp etkinleştirme:** `graph`/Dataview gibi `community-plugins.json`'a `marp` eklenir; `_obsidian_kurulum.md` "Marp artık aktif" olarak güncellenir; örnek Marp dosyası.
- INDEX §6.1 kaydı + health-check uyumu + dogfood (gerçek bir soru).

**Kapsam dışı (bilinçli / YAGNI):**
- Ayrı arama motoru / vektör DB / RAG → **YAGNI** (Karpathy de ~küçük ölçekte gereksiz diyor; INDEX + frontmatter + grep + Obsidian arama yeter).
- Otomatik/zamanlanmış sorgu, "health-check candidate question" üretimi → gelecekteki bir "Faz 3" (lint/öneri) işi; bu dilimde yok.
- `matplotlib` kurulumu — ortamda varsa kullanılır; **kurulmaz** (bağımlılık eklenmez).
- Web araştırması — `/kb-ask` **wiki üzerine** sorar; dış bilgi gerekiyorsa `/kb-ingest` ile önce ingest edilir (Dilim C), sonra sorulur.

## 2. Doğruluk ve kaynak gösterme

- Cevaplar doğruluk hiyerarşisine (kod > INDEX §0 > PRD; reference dış bilgi) **saygı gösterir** ve **kaynak gösterir**: her cevap dokümanı hangi doc'ları/kaynakları kullandığını "## Kaynaklar" altında backlink'ler. `authority: derived` (başka dokümanlardan türer).
- Cevap kanonik gerçeği **ezmez**; çelişki görürse bunu belirtir (ve gerekiyorsa bir health-check/drift işareti önerir).
- Cevap dokümanı **türev**dir: kod değişirse eskiyebilir; bu yüzden `updated` + kullanılan kaynakların o günkü durumu not edilir ("<tarih> itibarıyla").

## 3. `/kb-ask` iş akışı

`.claude/commands/kb-ask.md`. Girdi `$ARGUMENTS`: soru + opsiyonel `format=md|marp|mermaid|chart` (varsayılan `md`) + opsiyonel kapsam ipucu (ör. `scope=modules`).

1. **Kapsamla (retrieval, arama motoru YOK):** `doc/INDEX.md` + hedef frontmatter (`tags`/`authority`/`summary`) + `grep` ile soruyla ilgili dokümanları bul (`modules/`, `roles/`, `architecture/`, `kaynaklar/` dahil). İlgili en fazla ~10-15 dokümanı seç.
2. **Oku + sentezle:** Seçilen dokümanları oku; soruyu yanıtla; çelişki/eksik varsa belirt.
3. **Render (istenen format):**
   - `md` (varsayılan): yapılandırılmış markdown raporu.
   - `marp`: Marp slayt formatı (frontmatter `marp: true` + `---` slayt ayraçları); Obsidian Marp plugin'iyle önizlenir.
   - `mermaid`: ilişki/akış diyagramı (natif; ek yok).
   - `chart`: veri grafiği — **ortamda matplotlib varsa** `python3` ile PNG üretip `doc/_assets/`'e koy ve cevaba göm; **yoksa** mermaid/tabloya düş ve "matplotlib yok, grafik yerine tablo/mermaid" notu ekle.
4. **Geri dosyala:** `doc/_cevaplar/<slug>.md` — frontmatter (authority: derived, `question`, tags, updated) + gövde: cevap → (render çıktısı gömülü) → "## Kaynaklar" (kullanılan doc'lara backlink) → sonda `*Güncelleme: <bugün>*`.
5. **Kaydet:** `doc/_cevaplar/00_cevaplar_index.md`'ye satır (soru + tarih + link).
6. **Doğrula:** `bash doc/_tools/kb_healthcheck.sh doc` → yeni cevapta FRONTMATTER/DATE bulgusu yok, 0 RED.

> `/kb-ask` **kaynak dokümanları düzenlemez** (yalnız okur); yalnız `_cevaplar/` + `_assets/`'e yazar. Kanonik gerçeği ezmez.

## 4. Dizin yapısı

```
doc/
  _cevaplar/                   → /kb-ask çıktıları (authority: derived)
    00_cevaplar_index.md       → soru/tarih/link indeksi
    <slug>.md                  → cevap (md/Marp) + gömülü render
  _assets/                     → (Dilim B) matplotlib PNG'leri buraya
```

- `_cevaplar/*` Dilim A frontmatter'ına uyar; `00_cevaplar_index` INDEX §6.1'de → orphan değil (tekil cevaplar section-index'te; check-7 sınırı gereği BLUE olabilir — non-blocking).
- Marp dosyaları: frontmatter'da hem Dilim A alanları (summary/tags/authority/updated) hem `marp: true` bir arada; gövdedeki `---` slayt ayraçları health-check'i bozmaz (fence `^```` sayar, `---`'ı değil).

## 5. Render araçları ve graceful degrade

- **md / Marp / mermaid:** saf metin/markdown → **her zaman** çalışır, bağımlılık yok. Marp'ı yalnızca **görüntülemek** için Obsidian Marp plugin'i gerekir (kurulmazsa Marp md'si normal md gibi görünür — bozulma yok).
- **matplotlib (chart):** `python3 -c "import matplotlib"` başarılıysa PNG üretilir; değilse mermaid/tabloya düşülür + not. **matplotlib kurulmaz** (bağımlılık eklenmez); ortam kararına bırakılır.
- Bu, "yeni repo bağımlılığı yok" kısıtını korur ve çıktıyı her ortamda kullanılabilir tutar.

## 6. Health-check + Dilim A/B/C uyumu

- `_cevaplar/*.md` Dilim A frontmatter kuralına uyar (`authority: derived`); `00_cevaplar_index` INDEX'te kayıtlı.
- Marp `---` ayraçları fence kontrolünü tetiklemez (yalnız ` ``` ` sayılır). Marp frontmatter'ı Dilim A alanlarını da içerir → FRONTMATTER temiz.
- matplotlib PNG'leri `_assets/`'te (md değil) → taranmaz.
- Kabul: `kb_healthcheck.sh doc` → **0 RED / 0 YELLOW**, exit 0.
- Script değişikliği **gerekmez** (Dilim C'deki authority seti `derived`'ı zaten kapsıyor; yeni authority yok).

## 7. Kabul kriterleri

- `/kb-ask <soru>` çalıştırınca: ilgili dokümanlar bulunur/okunur, `doc/_cevaplar/<slug>.md` cevabı (kaynak backlink'leriyle) + index satırı oluşur; health-check yeşil kalır.
- `format=marp` Marp-geçerli md üretir; `format=mermaid` natif render'lanan diyagram; `format=chart` matplotlib varsa PNG, yoksa mermaid/tablo + not.
- Marp `community-plugins.json`'da; `_obsidian_kurulum.md` Marp'ı "aktif" olarak belgeler (örnek dosyayla).
- **Dogfood:** gerçek bir soru (ör. "Üyelik/premium hangi modülleri ve rolleri etkiliyor?") `/kb-ask` ile yanıtlanıp `_cevaplar/`'a dosyalanır — makinenin ilk Q&A çıktısı; kaynak backlink'leri (m08/m17/roles) çözülür.
- Cevap dokümanları kaynak gösterir, kanonik gerçeği ezmez.

## 8. Sonraki adımlar (Dilim D sonrası — opsiyonel)

Makinenin 4 çekirdek dilimi (A-D) tamamlanınca kalan fikirler (ayrı, opsiyonel):
- **check-7 orphan iyileştirmesi** — section-index'leri de tara (tüm KB'de 0 BLUE).
- **Lint/öneri turu** — `/kb-healthcheck`'e "eksik veri imputasyonu, yeni makale adayı, tutarsızlık" önerileri (Karpathy "health check"in ileri hali).
- **Synthetic data / fine-tune** — çok ileride (Karpathy'nin "further explorations"ı); kapsam dışı.

## 9. Kararlar (uygulayan oturum gözden geçirebilir)

Bu spec canlı brainstorm yerine önceden yazıldığından, aşağıdaki kararlar **varsayılan** olarak alındı; uygulama öncesi kullanıcı/oturum değiştirebilir:

1. **Çıktı alanı adı `doc/_cevaplar/`** (Türkçe, `_health`/`_dashboards` deseniyle uyumlu). Alternatif: `_answers`/`_qa`.
2. **matplotlib opsiyonel + graceful degrade** (kurulmaz). Alternatif: chart'ı tamamen mermaid/tabloya indirip matplotlib'i hiç kullanmamak (daha katı "sıfır bağımlılık").
3. **Retrieval = INDEX+frontmatter+grep** (arama motoru yok). ~74 doküman ölçeğinde yeterli; ölçek büyürse yeniden değerlendirilir.
4. **Marp bu dilimde etkinleştirilir** (spec'lerde D'ye ertelenmişti). `community-plugins.json`'a eklenir; plugin id kullanıcı Obsidian'ında ada göre kurulur (Dataview gibi öneri listesi).
5. **`/kb-ask` yalnız wiki'ye sorar** (web değil); dış bilgi → önce `/kb-ingest`.
