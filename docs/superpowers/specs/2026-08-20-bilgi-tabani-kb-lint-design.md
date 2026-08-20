---
title: "Bilgi Tabanı — /kb-lint Öneri Turu (Tasarım)"
summary: "/kb-lint: wiki üzerinde üretken lint — tutarsızlık + eksik veri imputasyonu (web) + yeni makale adayı + sorulacak soru önerileri; yalnız rapor (doc/_health/), oto-düzeltme yok"
tags: [kb, kb-lint, lint, oneri, tasarim, spec]
authority: derived
updated: 2026-08-20
---

# Bilgi Tabanı Makinesi — /kb-lint: Öneri Turu (Tasarım)

> **Tarih:** 2026-08-20 · **Durum:** Onaylandı (canlı brainstorm; kullanıcı kararlarıyla) · **Önceki:** 4 çekirdek dilim [A](2026-08-20-bilgi-tabani-dilim-a-design.md)·[B](2026-08-20-bilgi-tabani-dilim-b-design.md)·[C](2026-08-20-bilgi-tabani-dilim-c-design.md)·[D](2026-08-20-bilgi-tabani-dilim-d-design.md) (hepsi tamam+push) + check-7 orphan düzeltmesi (tamam)
>
> **Kaynak fikir:** Karpathy "LLM Knowledge Bases" → **Linting** bölümü. Deterministik `/kb-healthcheck` (biçim/kanonik/frontmatter/tarih/orphan) + `--deep` (kod-drift) zaten var; bu, onun **üretken** yarısını ekler: tutarsızlık, eksik veri imputasyonu (web), yeni makale adayı, sorulacak soru önerileri. Makine metne **tam parite** kazanır.

## 1. Amaç ve kapsam

`/kb-lint` wiki'yi (`doc/` + `kaynaklar/`) tarayıp **veri bütünlüğünü artıracak öneriler** üretir ve `doc/_health/`'e dated bir rapor olarak dosyalar. **Hiçbir kaynak dokümanı değiştirmez** — yalnız okur ve rapor yazar; düzeltme/uygulama kullanıcı onayıyla ayrı adımdır (mevcut `/kb-healthcheck` "oto-düzeltme yok" felsefesiyle birebir).

**Substrat:** Ajan = Claude Code. Makine = markdown konvansiyon + slash-komut; **yeni repo bağımlılığı yok, script değişikliği yok** (rapor `authority: derived`, mevcut şema kapsar). `/kb-ask` ile aynı retrieval (INDEX + frontmatter + `grep`; arama motoru YOK).

**Bu işin kapsamı:**
- `/kb-lint` slash-komutu (`.claude/commands/kb-lint.md`).
- 4 öneri boyutu (§3).
- Web araştırması **yalnız** eksik-veri imputasyonu için, `WebSearch`/`WebFetch` araçlarıyla (§4).
- Rapor → `doc/_health/<bugün>-kb-lint.md` (§5).
- Dogfood: gerçek `doc/` üzerinde bir öneri turu.

**Kapsam dışı (bilinçli / YAGNI):**
- Otomatik düzeltme / doküman yazımı — `/kb-lint` yalnız **önerir**; uygulama ayrı (kullanıcı `/kb-ingest`, `/kb-ask` veya elle yapar).
- Ayrı arama motoru / vektör DB — YAGNI (~74 doküman; INDEX+frontmatter+grep yeter).
- Boyut-başına paralel alt-ajan fan-out — wiki büyürse eklenir (§7); bu işte tek-geçiş.
- Zamanlanmış/otomatik lint turu — manuel tetikleme (`/kb-lint`).
- Taslak makale stub üretimi — bu işte yok (yalnız "aday" önerisi); istenirse sonraki iş.

## 2. Doğruluk ve güvenlik

- **Sadece-oku + sadece-rapor:** `/kb-lint` kaynak doc'ları düzenlemez; yalnız `doc/_health/`'e yazar.
- **Kanonik gerçeği ezmez:** Öneriler INDEX §0 kanonik değerlerini (ad, .NET sürümü, ana renk) ezmez; çelişki görürse **kod/INDEX'i doğruluk kaynağı** kabul edip dokümanın düzeltilmesini önerir (tersi değil).
- **Web güvenliği (kullanıcı sert kuralı):** Web araştırması **yalnız** `WebSearch`/`WebFetch` araçlarıyla yapılır. **Bu PC'deki yerel Chrome uzantısına / tarayıcıya / herhangi bir yerel tarayıcı otomasyonuna bağlanmaya ASLA çalışılmaz.** Her web-kaynaklı öneri kaynak **URL**'sini gösterir; öneri "türev/geçici"dir, oto-yazılmaz.
- **Öneri ≠ gerçek:** Rapor `authority: derived`; her öneri bir **eylem önerisi**dir (nerede, ne, neden, önerilen adım), kanonik veri değil.

## 3. Öneri boyutları (4)

`/kb-lint` şu 4 boyutta bulgu üretir; her bulgu: **konum(lar)** (`dosya:satır`) · **ne** · **neden** · **önerilen eylem**.

1. **Tutarsızlık (inconsistency):** Dokümanlar arası anlamsal çelişki — deterministik health-check'in yakalamadığı türden (ör. iki doc farklı sayı/durum/tarih/ad veriyor; bir rol doc'u modül doc'uyla çelişiyor; frontmatter `status` gövdeyle uyumsuz). Kanonik gerçekle (kod/INDEX) çelişen tarafı işaretler.
2. **Eksik veri imputasyonu (missing-data):** Boş/zayıf/bayat alanlar (ör. eksik `summary`, "TODO"/"(henüz yok)" gövde, uzun süredir güncellenmemiş `authority: code` doc). Web izinliyse `WebSearch`/`WebFetch` ile **doldurma önerisi** + kaynak URL. Web kapalıysa (`--no-web`) yalnız "eksik" işaretler ve gerekiyorsa `/kb-ingest`'e yönlendirir.
3. **Yeni makale adayı (new-article candidates):** Kavram kümesi / backlink yoğunluğu / tekrar eden ama kendi doc'u olmayan konu → "burada ayrı bir makale/başlık olmalı" önerisi (hedef klasör + taslak başlık + hangi doc'lardan besleneceği).
4. **Sorulacak sorular (further questions):** Wiki'deki boşluklara dayalı "şunu araştır/sor" önerileri — `/kb-ask` (wiki içi) veya `/kb-ingest` (dış kaynak) için besleme; hangi aracın uygun olduğunu belirtir.

> Boyutlar bağımsızdır; bir tur hepsini kapsar. Boş boyut varsa raporda "temiz" yazılır.

## 4. Retrieval + web

- **Retrieval (arama motoru YOK):** `/kb-ask` ile aynı — `doc/INDEX.md` + hedef frontmatter (`tags`/`authority`/`summary`/`status`/`updated`) + `grep`. Tutarsızlık/eksik/küme analizi için ilgili doküman kümesini (gerekiyorsa `kaynaklar/` + `raw/` envanteri dahil) okur.
- **Web (yalnız boyut 2):** `WebSearch`/`WebFetch`. Kısıt: yalnız bu iki araç; **yerel tarayıcı/uzantı otomasyonu yok** (§2). Web önerileri kaynak URL'li ve "öneri" etiketli.

## 5. Rapor (çıktı)

`doc/_health/<bugün YYYY-MM-DD>-kb-lint.md` — mevcut health-check raporlarıyla aynı dizin (INDEX §6.1'de kayıtlı, orphan-muaf, dated-snapshot deseni). Yeni top-level dizin **açılmaz**.

Frontmatter (Dilim A):
```yaml
---
title: "KB-Lint <tarih>"
summary: "<tek satır: N tutarsızlık / M eksik / K yeni-makale adayı / L soru önerisi>"
tags: [kb, lint, oneri, rapor]
authority: derived
updated: <bugün YYYY-MM-DD>
---
```
Gövde: en üstte özet sayaçlar (boyut başına) + mod (web açık/kapalı); ardından **4 bölüm** (Tutarsızlık / Eksik veri / Yeni makale adayı / Sorulacak sorular), her bölümde bulgu listesi (`konum · ne · neden · önerilen eylem`; web önerisinde kaynak URL). Sonda `*Güncelleme: <bugün>*` (frontmatter `updated` ile EŞİT — DATE kuralı). Boş bölüm = "temiz".

> Rapor `_health/` orphan-muafiyetindedir (check-7 `_health/*`'ı atlar); Dilim A frontmatter + DATE eşitliği sağlandığından health-check temiz kalır. **Script değişmez.**

## 6. Kabul kriterleri

- `/kb-lint` çalışınca: 4 boyutta bulgu üretilir; `doc/_health/<bugün>-kb-lint.md` raporu (özet sayaçlar + 4 bölüm) oluşur; **hiçbir kaynak doc değişmez**.
- Web açıkken imputasyon önerileri kaynak URL gösterir; `--no-web` ile web'e hiç çıkılmaz. **Yerel tarayıcı/uzantı otomasyonu hiçbir modda denenmez.**
- Öneriler kanonik gerçeği (INDEX §0) ezmez; çelişkide kod/INDEX'i doğruluk kaynağı sayar.
- `bash doc/_tools/kb_healthcheck.sh doc` → rapor sonrası **0 RED / 0 YELLOW**, exit 0 (rapor Dilim A frontmatter + DATE eşit). Fixture testleri değişmez (script değişmedi) — 12/12.
- **Dogfood:** gerçek `doc/` üzerinde bir tur; rapor gerçek, uygulanabilir öneriler içerir; makine "tam parite"ye ulaşır.

## 7. Sonraki adımlar (opsiyonel)

- **Boyut/küme fan-out:** wiki büyüyünce boyut başına (veya doküman kümesi başına) paralel alt-ajan (`--deep` deseni).
- **Taslak stub üretimi:** yeni-makale adayları için `_taslak/` iskeleti (frontmatter + TODO).
- **Öneri → uygulama akışı:** onaylı önerileri yarı-otomatik uygulama (yine kullanıcı kapısıyla).
- **Synthetic data / finetune:** çok ileride (Karpathy "further explorations"); kapsam dışı.

## 8. Kararlar (kullanıcı onaylı)

1. **Kapsam = 4 boyutun tümü** (tutarsızlık + eksik-veri imputasyonu + yeni makale adayı + sorulacak soru).
2. **Web = izinli**, yalnız `WebSearch`/`WebFetch`; **yerel Chrome uzantısı / tarayıcı otomasyonuna bağlanma YASAK** (kullanıcı sert kuralı). `--no-web` ile kapatılabilir.
3. **Çıktı = yalnız rapor** (`doc/_health/<tarih>-kb-lint.md`); oto-düzeltme yok. `_health/` yeniden kullanılır (yeni dizin yok).
4. **Yaklaşım = saf slash-komut, Claude-güdümlü, tek geçiş**; script değişmez; fan-out ertelendi (§7).
