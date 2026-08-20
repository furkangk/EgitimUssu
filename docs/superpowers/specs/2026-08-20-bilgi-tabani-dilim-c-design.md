---
title: "Bilgi Tabanı Dilim C — Ingest + Derleme (Tasarım)"
summary: "raw/ ham kaynak + /kb-ingest: dış kaynağı damıtıp kaynaklar/ altında reference makalesi + backlink + index üretir; authority: reference eklenir"
tags: [kb, dilim-c, ingest, tasarim, spec]
authority: derived
updated: 2026-08-20
---

# Bilgi Tabanı Makinesi — Dilim C: Ingest + Derleme (Tasarım)

> **Tarih:** 2026-08-20 · **Durum:** Onaylandı (tasarım) · **Önceki dilimler:** [Dilim A — Temel + Health-check](2026-08-20-bilgi-tabani-dilim-a-design.md) · [Dilim B — Obsidian](2026-08-20-bilgi-tabani-dilim-b-design.md) (ikisi de tamam)
>
> **Kaynak fikir:** LLM bilgi tabanı yönteminin "data ingest → wiki derleme" katmanı. Ham kaynakları `raw/`'a alıp Claude ile damıtılmış, backlink'li wiki makalelerine "derler".

## 1. Amaç ve kapsam

Dilim A frontmatter+health-check, Dilim B Obsidian görünümünü kurdu. Dilim C **yeni bilgi akışını** ekler: dış kaynak (araştırma/tasarım referansı/karar dökümü) → `doc/raw/`'a orijinal → Claude damıtır → `doc/kaynaklar/`'da `authority: reference` makale + ilgili modül/rollere backlink + index kaydı. Wiki'yi Claude yazar/bakım yapar; kullanıcı yalnızca **kaynağı bırakır** (`/kb-ingest`).

**Substrat:** Ajan = Claude Code. Makine = markdown konvansiyon + slash-komut; yeni **repo bağımlılığı yok**. `/kb-ingest` Claude-güdümlüdür (kaynağı oku → damıt → yaz → backlink → index).

**Bu dilimin kapsamı:**
- `doc/raw/` — ham kaynaklar (verbatim); health-check'ten muaf.
- `doc/kaynaklar/` — damıtılmış `reference` makaleleri + `00_kaynaklar_index.md` bölüm indeksi.
- Yeni **`authority: reference`** + `source` + `subtype` frontmatter alanları; Dilim A konvansiyonu + `kb_healthcheck.sh` + Dilim B `graph.json` güncellenir.
- `/kb-ingest` slash-komutu (`.claude/commands/kb-ingest.md`).
- INDEX §6.x kaydı + kabul (mevcut `promp.txt` örnek ingest).

**Kapsam dışı (bilinçli, sonraki dilim / YAGNI):**
- `/kb-ask` Q&A + çıktı render (Marp/matplotlib) → **Dilim D**.
- Ayrı arama motoru → YAGNI (INDEX + grep + Obsidian arama yeter).
- Otomatik web tarama/scraping hattı — `/kb-ingest` tek tek kaynak alır (URL fetch destekli), toplu crawler değil.
- OCR / ağır PDF ayrıştırma — PDF metni okunabildiği kadar; karmaşık PDF'ler için kullanıcı metni yapıştırır.
- Ingest edilen kaynağın **doğruluğunu** denetlemek — reference dış bilgidir; `code` drift kontrolüne tabi değildir (kaynak neyse odur, cite edilir).

## 2. Doğruluk hiyerarşisi ve reference'ın yeri

Dilim A hiyerarşisi korunur: kod > INDEX §0 kanonik > PRD. **`reference` bu hiyerarşinin dışındadır:** dış bilgiyi taşır, üründe/kodla çelişebilir ve bu bir "drift" değildir — reference yalnızca **kaynağını** (source) doğru cite etmelidir. Bir reference makalesi bir ürün kararını **besleyebilir** ama kanonik gerçeği **ezmez**.

## 3. Konvansiyon uzantısı (`authority: reference`)

Dilim A frontmatter şemasına eklenir (`doc/00_kb_konvansiyon.md` güncellenir):

- **`authority: reference`** — dış/ham kaynaktan damıtılmış, kaynaklı makale. `code_refs` yok; kod-drift denetimine tabi değil.
- **`source`** (reference için zorunlu) — orijinalin yeri: `raw/<dosya>` (repo içi ham kaynak) **veya** bir URL. Health-check: `raw/...` ise dosya varlığı doğrulanır; URL ise atlanır.
- **`subtype`** (reference için zorunlu) — `research` | `design` | `decision`. Etiketleme + graph/pano filtreleme.

Örnek:
```yaml
---
title: "Rakip Platform X — Fiyatlandırma Analizi"
summary: "X platformunun premium/free modeli ve komisyon yapısı; M17 üyelik kararlarını besler"
tags: [kaynak, rakip-analizi, premium]
authority: reference
subtype: research
source: raw/rakip-x-pricing-2026-08.md
updated: 2026-08-20
---
```

## 4. Dizin yapısı

```
doc/
  raw/                         → ham kaynaklar (verbatim; health-check MUAF)
    <slug>.md | .txt | .pdf | görsel
  kaynaklar/                   → damıtılmış reference makaleleri
    00_kaynaklar_index.md      → bölüm indeksi (subtype'a göre tablo)
    <slug>.md                  → authority: reference makale
  _assets/                     → (Dilim B) design subtype görselleri buraya
```

- **`raw/` health-check muafiyeti:** `kb_healthcheck.sh` `md_files()`'a `/raw/` dışlaması eklenir (`_tools/` gibi). raw/ verbatim'dir; frontmatter/format kuralı uygulanmaz.
- **`kaynaklar/*`** Dilim A frontmatter kuralına **uyar** (`reference` authority ile) ve `00_kaynaklar_index.md`'de listelenir → orphan değil.

## 5. `/kb-ingest` iş akışı

`.claude/commands/kb-ingest.md` (slash-komut prompt'u). Girdi `$ARGUMENTS`: `raw/` dosya yolu **veya** URL **veya** "paste" + opsiyonel `subtype`/konu ipucu.

1. **Kaynağı edin:**
   - URL ise: WebFetch ile getir, temizlenmiş metni `doc/raw/<slug>.md`'ye yaz (başına `> Kaynak: <URL> · Alındı: <tarih>` notu).
   - Zaten `raw/`'daysa: onu kullan.
   - Yapıştırılmış metin ise: `doc/raw/<slug>.md`'ye yaz.
   - Görsel (design) ise: `doc/_assets/<slug>.<ext>`'e koy (Dilim B ek klasörü); orijinal büyük dosya gerekiyorsa `raw/`'da da tutulabilir.
2. **Damıt:** başlık, tek-satır özet, kilit noktalar (madde), kavram/varlıklar; `subtype` belirle/doğrula (research/design/decision).
3. **İlişkilendir (backlink):** INDEX + grep ile bu kaynağın hangi `modules/`/`roles/`/`architecture/` dokümanlarını **beslediğini** bul. Makalede "## İlgili" altında bunlara **tek yönlü** link ver (Obsidian ters backlink'i otomatik gösterir; çekirdek dokümanlar düzenlenmez → churn yok).
4. **Makaleyi yaz/güncelle:** `doc/kaynaklar/<slug>.md` — frontmatter (§3) + gövde: özet → kilit noktalar → "## İlgili" backlink'ler → "## Kaynak" (`[orijinal](../raw/<slug>.md)` veya URL). **Dedup:** aynı `source` için makale varsa **güncelle** (yeniden oluşturma).
5. **Kaydet:** `00_kaynaklar_index.md`'ye satır (subtype tablosu) + gerekiyorsa INDEX §6.x'e ilk kez ekleme.
6. **Doğrula:** `bash doc/_tools/kb_healthcheck.sh doc` → 0 RED; yeni makalede frontmatter/DATE/orphan temiz.

> `/kb-ingest` **otomatik düzeltme yapmaz**, yalnız ekler/günceller; kanonik gerçeği ezmez. Belirsizlikte (hangi subtype, hangi modüle backlink) makul karar verir ve makalede belirtir.

## 6. Health-check + Dilim A/B uyumu

- **Script (`kb_healthcheck.sh`) değişiklikleri:** (a) `md_files()` `/raw/` hariç; (b) frontmatter authority geçerli-değer setine `reference` eklenir; (c) `authority: reference` için `source` zorunlu; `source` `raw/...` ise dosya varlığı doğrulanır (URL/`http` ise atlanır); (d) yeni fixture: geçerli reference + eksik-source reference. TDD: önce kırmızı fixture, sonra script değişikliği.
- **Konvansiyon (`00_kb_konvansiyon.md`):** authority tablosu + `source`/`subtype` satırları eklenir.
- **Graph (`doc/.obsidian/graph.json`):** `[authority:reference]` için renk (mor) eklenir.
- **Panolar (opsiyonel):** `_dashboards/`'a "Kaynaklar (reference)" panosu eklenebilir — kapsam içi değil, Dilim C'de yalnız `00_kaynaklar_index.md` yeterli; Dataview panosu YAGNI (index var).
- Kabul: `kb_healthcheck.sh doc` → **0 RED**, exit 0; fixture testleri yeşil.

## 7. Kabul kriterleri

- `authority: reference` + `source` + `subtype` konvansiyonu belgelenmiş; health-check reference'ı doğru doğruluyor (eksik source → bulgu; raw/ source yoksa → bulgu; geçerli → temiz).
- `raw/` health-check'ten muaf (raw/ içindeki .md frontmatter'sız olabilir, bulgu vermez).
- `/kb-ingest <kaynak>` çalıştırınca: `raw/`'a orijinal, `kaynaklar/`'a frontmatter'lı damıtılmış makale (backlink + kaynak linki), `00_kaynaklar_index.md`'ye satır oluşur; health-check yeşil kalır.
- **Dogfood:** mevcut `doc/promp.txt` `/kb-ingest` ile işlenip `kaynaklar/`'da bir `reference/decision` makalesine damıtılır (İlgili → PRD/modüller backlink'i) — makinenin ilk gerçek ingest çıktısı.
- Fixture testleri (`test_kb_healthcheck.sh`) reference kurallarını da kapsar ve yeşil.
- Yeni dosyalar INDEX/`00_kaynaklar_index`'te kayıtlı (orphan değil).

## 8. Riskler ve kararlar

- **Script'i genişletmek (Dilim A deliverable'ı):** `kb_healthcheck.sh`'a reference + raw/ muafiyeti eklemek meşru bir uzantı; TDD + mevcut fixture'lar korunarak yapılır (regresyon yok).
- **Backlink tek yönlü:** Churn'ü önlemek için yalnız makale→doküman; ters yön Obsidian backlink paneliyle gelir. (Kullanıcı isterse belirli çekirdek dokümana elle "İlgili kaynaklar" eklenebilir — kapsam dışı.)
- **`reference` kanonik ezmez:** Doğruluk hiyerarşisi net; reference dış bilgidir, drift değildir.
- **Tek akış, subtype ayırır:** research/design/decision ayrı klasör/şablon değil; `subtype` alanı + hafif gövde farkı (decision'da "## Karar / ## Gerekçe"). Basit ve genel.
- **PDF/görsel:** metin okunabildiği kadar; ağır durumlar kullanıcı-yapıştırma ile. YAGNI (OCR yok).

## 9. Sonraki dilim

- **Dilim D — Q&A + çıktı render:** `/kb-ask` (wiki + kaynaklar üzerine soru → araştır → md/**Marp**/matplotlib çıktı → `doc/`'a geri dosyala). Marp kurulumu burada. Reference makaleleri Q&A'in zenginleştirdiği kaynak havuzuna katılır.
