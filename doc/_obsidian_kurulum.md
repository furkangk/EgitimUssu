---
title: "Obsidian Kurulum & Kullanım"
summary: "doc/'u Obsidian vault olarak açma, Dataview kurulumu, graph renk anlamları, görsel konvansiyonu — bilgi tabanı görüntüleyicisi"
tags: [kb, obsidian, kurulum, rehber]
authority: derived
updated: 2026-08-20
---

# 🧭 Obsidian Kurulum & Kullanım

> `doc/` bilgi tabanını Obsidian ile **görüntülemek** için rehber. Wiki'yi Claude yazar/bakım yapar;
> sen Obsidian'da gezinirsin. (Dilim B — bilgi tabanı makinesi.)

## 1. Vault'u aç
Obsidian → **Open folder as vault** → repodaki `doc/` klasörünü seç. `.obsidian` config'i hazır gelir
(graph renkleri, ek klasörü, core plugin'ler).

## 2. Dataview'i kur (panolar için)
**Settings → Community plugins → Browse → "Dataview"** kur ve etkinleştir. Sonra `_dashboards/`
altındaki panolar (modül durumu, kod-doküman envanteri) frontmatter'dan canlı tablo üretir.
Dataview kurulmazsa panolar bozulmaz; sorgu inert kod bloğu görünür.

## 3. Graph renkleri (authority)
Graph düğüm renkleri `authority` alanına göre: 🔵 `code` (koddan doğrulanan) · ⚪ `product` (plan/PRD) ·
🟢 `derived` (türev/rehber) · soluk `archive`. Renk grupları `.obsidian/graph.json`'da; elle ayarlanabilir.

## 4. Görseller (`_assets/`)
Yapıştırdığın/indirdiğin görseller `doc/_assets/`'e kaydedilir (ek klasörü oraya ayarlı). Mevcut
diyagram SVG'leri `doc/diagrams/`'da, mermaid blokları inline (Obsidian natif render eder) — dokunma.

## 5. Neler commit'lenir
Yalnızca paylaşılabilir config (`app/core-plugins/graph/community-plugins/appearance.json`) izlenir;
kişisel oturum durumu (`workspace.json`, plugin verisi, temalar) `.gitignore`'dadır — her cihazda kendi düzenin.

## 6. Slaytlar (Marp) ve Q&A
**Marp** artık aktif: **Settings → Community plugins → "Marp"** kur+etkinleştir → `marp: true` frontmatter'lı dosyalar slayt olarak önizlenir/export edilir. `/kb-ask format=marp` bu formatta çıktı üretir.
**`/kb-ask`** (Dilim D): wiki'ye soru sor → Claude ilgili dokümanları okuyup cevabı `doc/_cevaplar/`'a md/Marp/mermaid/grafik olarak dosyalar.
Dış kaynak eklemek için `/kb-ingest` (Dilim C).

*Güncelleme: 2026-08-20*
