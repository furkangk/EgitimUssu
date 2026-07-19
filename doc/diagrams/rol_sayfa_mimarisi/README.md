# 🗺️ Rol Sayfa Mimarisi — Diyagramlar

Her rol için **sayfa yapısı (IA)**, **sayfa içerikleri** ve **sayfalar arası ilişki + veri akışı** diyagramları.

> **Önemli:** Bu diyagramlar yalnızca ilgili **fonksiyonel dokümandan** (`doc/*_rolu_fonksiyonel_dokuman_v1.md`) türetilmiştir — **mevcut Flutter uygulamasını yansıtmaz.** “Olması gereken” tasarımı belgeler; `[YENİ]` maddeler öneridir.
> Kod gerçeğiyle karşılaştırma için bkz. `doc/pages/` ve `doc/roles/`.

| # | Rol | Dosya | Açılış ekranı | Sekme |
|---|-----|-------|---------------|-------|
| 1/3 | 🎓 Öğrenci | [`ogrenci.md`](ogrenci.md) | ⏱️ Çalış (Sayaç) | 5 |
| 2/3 | 👨‍🏫 Öğretmen | [`ogretmen.md`](ogretmen.md) | 📅 Takvim | 5 |
| 3/3 | 👪 Veli | [`veli.md`](veli.md) | 🏠 Özet | 4 |

**Her dosyanın içeriği:**
1. **Sayfa Yapısı** — bilgi mimarisi ağacı (mermaid) + faz bazlı sekme durumu tablosu
2. **Sayfa İçerikleri** — her sekmenin içerik blokları; kaynak yetenek + faz + Free/Premium/⚠️ etiketleri
3. **İlişki + Veri Akışı** — gezinme haritası, veri akış diyagramı ve durum makineleri (mermaid)

**Ortak lejant:** 🟢 Free · 🟣 Premium · ⚠️ PRD çelişki/boşluk · 🔵 Faz-kapılı · 🔒 mahremiyet çekirdeği · **[Y]** [YENİ] öneri

Diyagramlar mermaid ile yazılmıştır; GitHub ve VS Code (Mermaid eklentisi) doğrudan render eder.

## SVG çıktıları (`svg/<rol>/`)

Md içindeki her mermaid bloğu, `@mermaid-js/mermaid-cli` ile SVG olarak da render edildi — role göre ayrı dizinlerde:

| Rol | Dizin | SVG | Dosyalar |
|-----|-------|-----|----------|
| 🎓 Öğrenci | [`svg/ogrenci/`](svg/ogrenci/) | 9 | `01_sayfa_yapisi_ia` · `02_kavramsal_model` · `03_gezinme_haritasi` · `04_ogretmensiz_dongu` · `05_veri_akisi` · `06_durum_seans` · `07_durum_kendi_ders` · `08_durum_hesap` · `09_durum_odev` |
| 👨‍🏫 Öğretmen | [`svg/ogretmen/`](svg/ogretmen/) | 7 | `01_sayfa_yapisi_ia` · `02_gezinme_haritasi` · `03_veri_akisi_ders_tamamlandi` · `04_durum_ders` · `05_durum_baglanti` · `06_durum_odev` · `07_durum_odeme` |
| 👪 Veli | [`svg/veli/`](svg/veli/) | 5 | `01_sayfa_yapisi_ia` · `02_panel_veri_durumu` · `03_gezinme_haritasi` · `04_veri_akisi_panel_sorgusu` · `05_durum_baglanti` |

> SVG'ler md kaynağından türetilmiştir. Md'deki bir diyagram değişirse ilgili SVG'yi yeniden üretmek için:
> `npx -y @mermaid-js/mermaid-cli -i <rol>.md -o svg/<rol>/d.svg` (çıktı `d-1.svg…` olur; anlamlı adlarla yeniden adlandırın).

**Güncelleme:** 2026-07-19
