# Tasarım — Doküman Temizliği & Bilgi Tabanı Sağlamlaştırma (Faz 1)

> **Durum:** Tasarım onaylandı (2026-08-19) — uygulama planı yazılıyor.
> **Kaynak fikir:** LLM tabanlı kişisel bilgi tabanı yöntemi (raw → wiki → Q&A → lint). Bu spec yöntemin **temizlik/lint** kısmını hedefler; makine (ingest/arama/görselleştirme) ayrı, sonraki döngüdür.

## 1. Amaç ve kapsam

EğitimÜssü'nün `doc/` altındaki 73 markdown (~125K kelime) dört tür bozukluk taşıyor: içerik/gerçek çelişkisi, biçim/format bozukluğu, dağınık yapı/organizasyon ve kodla senkronsuzluk. Bu alt-projenin amacı **mevcut dokümanları** tutarlı, koddan doğrulanmış ve gezilebilir hale getirmektir.

**Kapsam dışı (bu spec değil, sonraki ayrı döngü):**
- `raw/` ham kaynak ingest klasörü
- Obsidian frontend kurulumu / plugin'ler
- Wiki üstünde arama CLI'si
- Otomatik "health check" tooling'i (tekrarlı çalışan)
- Görselleştirme çıktıları (Marp/matplotlib vb.)
- Dış kaynak (rakip analizi/pazar/makale) ingest'i
- Backend'i çalıştırmak (Postgres yok — doğrulama kaynak kodu **okuyarak** yapılır)

## 2. Doğruluk hiyerarşisi (çelişki çözme kuralı)

CLAUDE.md ile hizalı. Çelişkide dokümanı şuna göre düzelt:
1. **Gerçek kod** (C# modülleri, Flutter ekranları) — birincil doğruluk kaynağı
2. **INDEX §0 kanonik gerçekler** — ad, .NET sürümü, ana renk, DB vb.
3. **PRD v2.1** — yalnızca ürün niyeti/faz için

## 3. İş kırılımı (sıralı geçişler, her biri ayrı commit)

### Geçiş 0 — Baz çizgisi & format lint (deterministik)
73 md üzerinde mekanik markdown onarımı: kırık tablolar, kapanmamış kod blokları, karışık başlık seviyeleri, liste/girinti hataları, kopuk göreli linkler. Repo'ya kalıcı linter **eklenmez** (önce temizlik, sonra makine); tek seferlik mekanik tarama + tek "format" commit'i.

### Geçiş 1 — Eski dev dokümanları birleştir + arşivle
`ogretmen_rolu_fonksiyonel_dokuman_v1.md`, `ogrenci_rolu_fonksiyonel_dokuman_v1.md`, `veli_rolu_fonksiyonel_dokuman_v1.md`, `is_akislari.md` içindeki **hâlâ geçerli** bilgiyi ilgili `roles/` + `modules/`'e taşı → orijinalleri `doc/_arsiv/`'e taşı, başına "tarihi/otorite artık roles/modules'te" notu ekle. Diyagramların (`doc/diagrams/rol_sayfa_mimarisi/`) kaynak referanslarını güncelle. Çifte otorite biter.

### Geçiş 2 — Modül derin kod-senkron (ağır geçiş, m01–m18)
Her modül için gerçek C#'ı oku (endpoint / enum / domain alanları / durum) → `mNN_*.md` + `modules/00_genel_bakis.md` endpoint envanteri + `modules/veri_modeli.md` ER'ini gerçeğe göre düzelt. Modül başına bağımsız doğrulama (2026-06-30 denetimi derinliğinde).

### Geçiş 3 — Mimari & sayfa senkronu
`architecture/*.md`'yi backend/Flutter gerçeğiyle; `pages/*.md`'yi `mobile/lib`'deki gerçek ekranlarla karşılaştır ve düzelt. Var olmayan ekranların "planlanan" olduğu net işaretlensin.

### Geçiş 4 — Roller uzlaştırma
Modül gerçeği oturduktan sonra `roles/` perspektif dokümanlarını hizala (yetenek / akış / durum 🟢🟡🔴).

### Geçiş 5 — INDEX uzlaştırma + global tutarlılık
INDEX'i gerçeğe göre yeniden kur (durum sütunları, linkler, "Güncelleme" tarihleri), son çelişki süpürmesi, tüm göreli linklerin çözüldüğünü doğrula.

## 4. Kalıcı yapı konvansiyonları (temiz kalması için)

- Her dokümanın başında **otorite notu** (bu konuda esas kaynak nedir).
- Tutarlı **durum lejantı**: 🟢 tam / 🟡 kısmi / 🔴 iskelet-planlanan.
- Her dokümanın altında **`Güncelleme: YYYY-MM-DD`** (o günkü tarih).
- Arşiv konvansiyonu: `doc/_arsiv/` + dosya başında neden/ne zaman/nereye taşındığı.
- Göreli linkler (kırık link bırakma).

## 5. Doğrulama (tamamlanma kanıtı)

Backend çalıştırılamadığı için doğrulama = **link kontrolü + kod grep çapraz-kontrolü**:
- Tüm göreli md linkleri çözülüyor (kırık link yok).
- Her modül dokümanındaki endpoint listesi, kodun grep'iyle eşleşiyor.
- INDEX'teki her link ve durum, gerçek dosya/koda karşılık geliyor.
- INDEX §0 kanonik gerçekler ile hiçbir doküman çelişmiyor.

## 6. Çıktılar

- Temizlenmiş `doc/` (73 → düzeltilmiş; bazıları arşive taşınmış).
- `doc/_arsiv/` (eski dev fonksiyonel dokümanlar).
- Gerçeğe göre yeniden kurulmuş `doc/INDEX.md`.
- Her geçiş için ayrı commit (geri alınabilir, izlenebilir).
- Kısa denetim raporu (geçiş başına bulgular) — çalışma artefaktı.

## 7. Riskler / notlar

- **Postgres yok:** kod-senkron yalnızca kaynağı okuyarak; migration/DB doğrulaması yapılmaz.
- **Platform dosyaları:** `_arsiv/` ve doküman değişiklikleri `main`'e commit edilir; platforma özgü dosyalara dokunulmaz (CLAUDE.md git notu).
- **Sonraki döngü:** Faz 2 = makine (raw/ ingest + Obsidian + arama + otomatik health-check). Ayrı spec.
