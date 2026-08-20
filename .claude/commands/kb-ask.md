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
