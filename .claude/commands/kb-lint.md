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
