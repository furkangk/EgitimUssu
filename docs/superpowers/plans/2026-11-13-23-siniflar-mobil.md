# P23 — Sınıflar — Mobil — Hafif Plan 🅻

> **Hafif plan:** `/devam` önce **Task 0**'ı yürütür.

**Goal:** P22'nin sınıf özelliklerini üç rolde mobil ekranlara taşımak; öğretmenin ana ekranı bağlamına (okul/özel/ikisi) göre sadeleşsin.

**Spec:** master K-13…K-17. **Dal:** `feat/p23-siniflar-mobil` · **Ön koşul:** P21, P22. Tasarım: `.claude/commands/design.md` (`/design` skill'i) ve `doc/architecture/design_system.md`.

## Global Constraints
- Yeni ekranlar ARB + widget testi + `doc/pages/*.md`.
- Ödemeler sekmesi yalnız öğretmenin en az bir özel bağı varsa veya `TeachingContext ∈ {Private, Both}` ise görünür.

### Task 0: Planı ayrıntılandır (ekran listesi, rota tablosuna eklemeler, cubit'ler)
- [ ] **Step 1:** Kodu oku, görevleri 2026-09-02 plan biçiminde (`Files/Interfaces/Steps`) yaz, `docs(plan): P23 ayrintilandirildi` commit'i at, ILERLEME'yi güncelle. **Kod yazma.**

### Task 1: Öğretmen — sınıf listesi, oluştur, kod paylaş, arşivle
- **Kabul:** sınıf oluşturma 3 alanla (ad, ders, eğitim yılı); kod büyük ve okunaklı + paylaş + yenile.

### Task 2: Sınıf detayı — üyeler, bekleyenler, sınıf panosu
- **Kabul:** risk bayraklı öğrenciler üstte; bekleyen üye yalnız adla.

### Task 3: Toplu ödev oluşturma + teslim takibi
- **Kabul:** ödev oluştur → sınıf özetinde teslim/geç/eksik; tek öğrenciye inip notlama mevcut ekranla.

### Task 4: Duyuru gönderme + öğrenci/veli tarafında duyuru akışı

### Task 5: Öğrenci — "Sınıfa katıl" + "Öğretmenlerim" + paylaşım ayarı
- Doğum tarihi yoksa katılımda sorulur; 18 yaş altında "veli onayı bekleniyor" durumu.
- "Öğretmenlerim": okul sınıfları ve özel öğretmenler tek listede, her birinde ne paylaşıldığı açıkça yazılı.

### Task 6: Veli — çocuğun öğretmenleri + sınıf katılım onayı

### Task 7: Bağlama göre ana ekran + kapanış
- Kayıtta "Nerede ders veriyorsunuz?" sorusu; okul bağlamında ana ekran sınıflar + bugünün ödev teslimleri; özel bağlamda bugünkü dersler + tahsilat.
- Gerçek cihaz turu (öğretmen + 2 öğrenci + 1 veli).
