---
title: "İş Akışları Diyagramları — SVG İndeksi"
summary: "Arşivlenen is_akislari.md içindeki 23 Mermaid diyagramının SVG çıktı indeksi ve yeniden üretme komutu"
tags: [diyagram, is-akislari, svg]
authority: derived
updated: 2026-08-19
---

# `is_akislari.md` diyagramları — SVG

[`../../_arsiv/is_akislari.md`](../../_arsiv/is_akislari.md) (⚠️ arşiv, 2026-08-19) içindeki 23 Mermaid diyagramının SVG çıktısı.
mermaid-cli ile üretilmiştir; sıra dokümandaki görünüm sırasıyla aynıdır. Kaynak doküman arşivlendi; güncel otorite `doc/roles/` + `doc/modules/`'tedir.

| SVG | Tür | Diyagram | Bölüm |
|-----|-----|----------|:-----:|
| `diyagram-01.svg` | graph | Sistem haritası — kim hangi modülü kullanıyor | §1 |
| `diyagram-02.svg` | sequence | Ortak omurga — bir istek baştan sona | §2 |
| `diyagram-03.svg` | sequence | Uçtan uca kayıt akışı | §3.1 |
| `diyagram-04.svg` | flowchart | Router yönlendirme mantığı | §3.3 |
| `diyagram-05.svg` | state | Oturum yaşam döngüsü | §3.4 |
| `diyagram-06.svg` | flowchart | Öğretmenin tam iş akışı | §4.1 |
| `diyagram-07.svg` | flowchart | Öğrenci ekleme | §4.3 |
| `diyagram-08.svg` | state | Ders planı durum makinesi | §4.4 |
| `diyagram-09.svg` | sequence | Hatırlatma zinciri (M11) | §4.4 |
| `diyagram-10.svg` | state | Ders oturumu durum makinesi | §4.5 |
| `diyagram-11.svg` | sequence | Ders notu ve ödev | §4.6 |
| `diyagram-12.svg` | state | Ödev durum makinesi | §4.6 |
| `diyagram-13.svg` | state | Ödeme durum makinesi | §4.7 |
| `diyagram-14.svg` | flowchart | Öğrencinin tam iş akışı | §5.1 |
| `diyagram-15.svg` | state | Çalışma seansı durum makinesi | §5.3 |
| `diyagram-16.svg` | sequence | Seans tamamlanınca ne oluyor | §5.4 |
| `diyagram-17.svg` | flowchart | Seri (streak) durum makinesi | §5.5 |
| `diyagram-18.svg` | flowchart | Velinin tam iş akışı | §6.1 |
| `diyagram-19.svg` | sequence | Çocuk bağlama | §6.2 |
| `diyagram-20.svg` | state | Bağ durum makinesi | §6.2 |
| `diyagram-21.svg` | graph | Velinin gelişim panosu | §6.3 |
| `diyagram-22.svg` | sequence | Uçtan uca birleşik akış — üç rol | §7 |
| `diyagram-23.svg` | graph | Modüller arası event haritası | §9 |

## Yeniden üretmek

```bash
# tek tek (her diyagram ayrı .mmd olarak ayıklanıp render edilir)
npx -y @mermaid-js/mermaid-cli@latest -i is_akislari.md -o out.svg -t default -b transparent
```

> Not: mermaid-cli'ın katı parser'ı için kaynak `is_akislari.md`'de iki kalıp düzeltildi
> (artık doğrudan, sanitizasyonsuz render olur):
> `PAR` alias'ı `par` anahtar kelimesiyle çakışıyordu → `PRT` (§6.2, §7);
> §5.4'te mesaj metnindeki `;` ifade ayırıcı olarak yorumlanıyordu → `,`.

**Güncelleme: 2026-08-19** (kaynak `is_akislari.md` → `_arsiv/`'e taşındı)
