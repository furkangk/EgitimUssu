---
description: Dış/ham kaynağı (raw/ dosyası, URL veya yapıştırma) damıtıp doc/kaynaklar/ altında reference makalesi + backlink + index üretir
---

Bir kaynağı bilgi tabanına **ingest** et. Argüman: `$ARGUMENTS` (raw/ dosya yolu, URL veya "paste:<metin>"; opsiyonel `subtype=research|design|decision` ve konu ipucu).

## 1. Kaynağı edin (doc/raw/)
- **URL** ise: WebFetch ile getir → temizlenmiş metni `doc/raw/<slug>.md`'ye yaz; başına `> Kaynak: <URL> · Alındı: <bugün>` satırı.
- **raw/ dosyası** zaten varsa: onu kullan.
- **paste:** ise: metni `doc/raw/<slug>.md`'ye yaz.
- **Görsel (design)** ise: `doc/_assets/<slug>.<ext>`'e koy.
- `<slug>` = kısa kebab-case ad (konudan).

## 2. Damıt
Kaynağı oku → başlık, tek-satır özet (≤160), kilit noktalar (madde), kavram/varlıklar. `subtype`'ı belirle/doğrula (research/design/decision).

## 3. İlişkilendir (backlink)
`doc/INDEX.md` + `grep` ile bu kaynağın hangi `modules/`/`roles/`/`architecture/` dokümanlarını beslediğini bul. Makalede "## İlgili" altında bunlara **tek yönlü** göreli link ver (çekirdek dokümanları DÜZENLEME).

## 4. Makaleyi yaz/güncelle (doc/kaynaklar/<slug>.md)
Aynı `source` için makale varsa **güncelle** (yeniden oluşturma). Frontmatter:
```yaml
---
title: "<başlık>"
summary: "<tek satır>"
tags: [kaynak, <subtype>, <2-3 konu>]
authority: reference
subtype: <research|design|decision>
source: raw/<slug>.md    # veya URL veya repo dosyası
updated: <bugün YYYY-MM-DD>
---
```
Gövde: özet → "## Kilit Noktalar" → (decision ise "## Karar" + "## Gerekçe") → "## İlgili" (backlink'ler) → "## Kaynak" (`[orijinal](../raw/<slug>.md)` veya URL). Gövde sonunda `*Güncelleme: <bugün>*` (frontmatter `updated` ile EŞİT).

## 5. Kaydet (index)
`doc/kaynaklar/00_kaynaklar_index.md`'de ilgili subtype tablosuna satır ekle (`_(henüz yok)_` satırını değiştir/altına ekle).

## 6. Doğrula
`bash doc/_tools/kb_healthcheck.sh doc` çalıştır → yeni makalede FRONTMATTER/DATE/SOURCE bulgusu **olmamalı**, 0 RED. Sorun varsa düzelt.

## Kurallar
- **Otomatik düzeltme yok** — yalnız ekle/güncelle; kanonik gerçeği (INDEX §0) EZME. Reference dış bilgidir.
- Belirsizlikte (subtype, hangi backlink) makul karar ver ve makalede belirt.
- `source` `raw/` yolu değilse (URL/repo dosyası) da olur; health-check URL'i atlar, repo yolunu doğrular.
