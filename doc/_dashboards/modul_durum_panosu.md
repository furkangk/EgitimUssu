---
title: "Modül Durum Panosu"
summary: "modules/ dokümanlarının status'una göre canlı Dataview tablosu — INDEX §3 modül tablosunun oto-üretilen hali (Dataview gerekir)"
tags: [kb, pano, dataview, modul]
authority: derived
updated: 2026-08-20
---

# 📊 Modül Durum Panosu

> ⚠️ **Dataview gerekir.** Bu pano frontmatter'dan canlı üretilir; Dataview plugin'i
> kurulu değilse aşağıdaki sorgu inert kod bloğu olarak görünür (bozulma yok).

```dataview
TABLE WITHOUT ID file.link AS "Modül", status AS "Durum", updated AS "Güncelleme"
FROM "modules"
WHERE status
SORT status DESC, file.name ASC
```

*Güncelleme: 2026-08-20*
