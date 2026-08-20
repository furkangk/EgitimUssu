---
title: "Kod-Doküman Envanteri"
summary: "authority: code dokümanları + code_refs + updated — en eski güncellenen (drift riski yüksek) üstte; Dataview gerekir"
tags: [kb, pano, dataview, drift]
authority: derived
updated: 2026-08-20
---

# 🔎 Kod-Doküman Envanteri (drift riski)

> ⚠️ **Dataview gerekir.** `authority: code` dokümanları en eski `updated` üstte listeler —
> koddan sapma riski en yüksek olanları önce gösterir. `/kb-healthcheck --deep` ile birlikte kullanılır.

```dataview
TABLE WITHOUT ID file.link AS "Doküman", code_refs AS "Kaynak", status AS "Durum", updated AS "Güncelleme"
WHERE authority = "code"
SORT updated ASC
```

*Güncelleme: 2026-08-20*
