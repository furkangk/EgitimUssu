---
description: doc/ bilgi tabanı health-check — biçim/kanonik/frontmatter (Faz 1) + opsiyonel kod-drift (--deep)
---

`doc/` bilgi tabanının sağlığını denetle. Argüman: `$ARGUMENTS` (`--deep` verilirse Faz 2 de çalışır).

## Faz 1 — deterministik (her zaman)

1. Çalıştır: `bash doc/_tools/kb_healthcheck.sh doc`
2. Çıktıyı severity'ye göre topla (RED/YELLOW/BLUE). Satır formatı: `SEVERITY<TAB>CHECK<TAB>file:line<TAB>message`.

## Faz 2 — kod-drift (yalnız `$ARGUMENTS` içinde `--deep` varsa)

3. `authority: code` olan her `doc/**/*.md`'yi bul:
   `grep -rl '^authority: code' doc --include='*.md'`
4. Bu dokümanlar için **paralel alt-ajan** (Task/Agent) dispatch et — her biri için ayrı bir `general-purpose` ajan, tek mesajda toplu gönder. Her ajana talimat:
   > Şu dokümanı oku: `<yol>`. Frontmatter'daki `code_refs` glob'larındaki gerçek kodu oku. Kodun gerçek endpoint (`Map(Get|Post|Put|Delete|Patch)`), enum değerleri ve domain alanlarını çıkar. Dokümanın iddia ettikleriyle diff'le. SADECE yapısal drift bulgularını döndür; her biri: `severity(RED/YELLOW) | doküman-diyor | kod-diyor | dosya:satır`. Drift yoksa "TEMİZ" döndür.
5. `authority: derived` roller için: iddia edilen modül durumlarını ilgili `modules/mNN` frontmatter `status`'larıyla karşılaştır (tek ajan yeterli).
6. Tüm ajan bulgularını topla, dedup'la.

## Rapor

7. `doc/_health/<bugün YYYY-MM-DD>-healthcheck.md` yaz. Frontmatter:
   ```yaml
   ---
   title: "Health-check <tarih>"
   summary: "doc/ health-check raporu — <RED> kırmızı / <YELLOW> sarı / <BLUE> mavi bulgu"
   tags: [kb, health, rapor]
   authority: derived
   updated: <tarih>
   ---
   ```
   Gövde: en üstte pass/fail + sayaçlar + mod (Faz 1 / --deep); ardından severity sıralı bulgu listesi (🔴 RED, 🟡 YELLOW, 🔵 BLUE), her biri `dosya:satır + ne + beklenen`.
8. Terminal'e kısa özet bas (sayaçlar + ilk birkaç RED bulgu). RED bulgu varsa kullanıcıyı uyar.

## Kurallar
- Beyaz-liste: `EgittimUssu` kural-tanımı satırları ve `.NET 8/10` "çözüldü" notları bulgu değildir (script zaten hariç tutar).
- Bulguları **otomatik düzeltme**; yalnız raporla. Düzeltme kullanıcı onayıyla ayrı adımdır.
