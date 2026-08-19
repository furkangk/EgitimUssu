# Doküman Temizliği & Bilgi Tabanı Sağlamlaştırma (Faz 1) — Uygulama Planı

> **Agentic worker için:** GEREKLİ ALT-SKILL: Bu planı görev görev uygulamak için superpowers:subagent-driven-development (önerilen) veya superpowers:executing-plans kullan. Adımlar takip için checkbox (`- [ ]`) kullanır.

**Goal:** `doc/` altındaki 73 markdown'ı koddan doğrulanmış, çelişkisiz, biçimsel olarak temiz ve gezilebilir hale getirmek; eski çakışan dokümanları arşive alıp INDEX'i gerçeğe göre yeniden kurmak.

**Architecture:** Sıralı geçişler (0→5), her biri ayrı commit. Ucuz deterministik format düzeltmesi önce; ağır kod-senkron doğrulaması modül modül; INDEX en sonda uzlaştırılır. Doğrulama backend'i çalıştırmadan, kaynağı grep'leyerek ve link kontrolüyle yapılır (Postgres yok).

**Tech Stack:** Markdown, bash (grep/find/link-check one-liner'ları), git. Repo'ya yeni kalıcı bağımlılık **eklenmez**.

## Global Constraints

- Doğruluk hiyerarşisi (çelişkide esas): 1) gerçek kod → 2) `doc/INDEX.md` §0 kanonik gerçekler → 3) PRD v2.1.
- Kanonik gerçekler (INDEX §0): görünen ad **EğitimÜssü**, kod/dosya adı **EgitimUssu** (`EgittimUssu` çift-t YANLIŞ), backend **.NET 9**, ana renk **`0xFF082B4F`**, DB **PostgreSQL modül başına ayrı şema + Redis**, PRD **v2.1**.
- Backend modül klasörleri (gerçek, 15 adet): Assignments, Identity, LessonSessions, Matching, Notifications, Parents, Payments, ProgressTracking, Reporting, Reviews, Scheduling, Settings, Students, Study, Teachers. **Messaging/Membership/Feedback klasörü YOK** → m16/m17/m18 gerçekten 🔴 iskelet.
- Backend'i çalıştırma (Postgres yok); doğrulama = kaynağı grep + link kontrolü.
- Platforma özgü dosyalara dokunma (CLAUDE.md git notu). Sadece `doc/` ve `docs/superpowers/` altında çalış.
- Her düzenlenen dokümanın altındaki `Güncelleme:` tarihini **2026-08-19** yap.
- Durum lejantı: 🟢 tam / 🟡 kısmi / 🔴 iskelet-planlanan.
- Her geçiş kendi commit'i; commit mesajı sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## Ortak yardımcı: kırık link kontrolü

Aşağıdaki komut `doc/` içindeki tüm göreli markdown linklerini çözüp var olmayanları listeler. Birçok görevde "Expected" kanıtı olarak kullanılır:

```bash
# Kırık göreli md linklerini bul (hedef dosya yoksa yazdırır)
find doc -name "*.md" | while read f; do
  grep -oE '\]\(([^)#]+\.md)' "$f" | sed -E 's/^\]\(//' | while read link; do
    target="$(dirname "$f")/$link"
    [ -f "$target" ] || echo "KIRIK: $f -> $link"
  done
done
```

---

## Task 0: Baz çizgisi & format lint

**Files:**
- Modify: `doc/**/*.md` (yalnızca biçim; içerik değişmez)
- Test: kırık link kontrolü one-liner'ı (yukarıda)

**Interfaces:**
- Produces: Biçimsel olarak temiz 73 md; sonraki görevler bunun üstüne içerik düzeltir.

- [ ] **Step 1: Mevcut kırık linkleri ve format sorunlarını envanterle**

Run:
```bash
# Kırık linkler
find doc -name "*.md" | while read f; do grep -oE '\]\(([^)#]+\.md)' "$f" | sed -E 's/^\]\(//' | while read link; do target="$(dirname "$f")/$link"; [ -f "$target" ] || echo "KIRIK: $f -> $link"; done; done
# Kapanmamış kod bloğu (tek sayıda ``` olan dosyalar)
find doc -name "*.md" | while read f; do n=$(grep -c '^```' "$f"); [ $((n%2)) -ne 0 ] && echo "TEK-FENCE: $f ($n)"; done
```
Expected: Sorunlu dosyaların listesi (baz çizgisi). Çıktıyı not al.

- [ ] **Step 2: Her sorunlu dosyada biçimi düzelt**

Yalnızca biçim: kapanmamış ` ``` ` bloklarını kapat, kırık markdown tablolarını hizala (her satırda eşit `|` sayısı), atlanmış başlık seviyelerini düzelt (H1→H2→H3 sıralı), liste girintilerini 2 boşluğa normalize et. **İçerik/anlam değiştirme** — o sonraki görevlerde.

- [ ] **Step 3: Kırık linkleri düzelt (yalnızca hedefi taşınmamış/yanlış yazılmış olanlar)**

Step 1'deki `KIRIK:` satırlarından, hedefi var olan ama yolu yanlış yazılmış olanları düzelt. Hedefi gerçekten olmayan linkler (arşive taşınacak/silinecek) Task 1 ve Task 5'e bırakılır — burada dokunma, listede tut.

- [ ] **Step 4: Doğrula**

Run: Step 1'deki iki komut.
Expected: `TEK-FENCE` çıktısı boş; `KIRIK` çıktısı yalnızca "hedefi gerçekten yok" olanlar (Task 1/5'e devredilenler).

- [ ] **Step 5: Commit**

```bash
git add doc && git commit -m "docs(temizlik): Geçiş 0 — biçim/format lint (fence/tablo/başlık/link)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 1: Eski dev dokümanları birleştir + arşivle

**Files:**
- Create: `doc/_arsiv/` (klasör) + taşınan 4 dosya
- Modify: ilgili `doc/roles/*.md`, `doc/modules/*.md`, `doc/diagrams/rol_sayfa_mimarisi/*.md`, `doc/INDEX.md`
- Kaynak (taşınacak): `doc/ogretmen_rolu_fonksiyonel_dokuman_v1.md`, `doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md`, `doc/veli_rolu_fonksiyonel_dokuman_v1.md`, `doc/is_akislari.md`

**Interfaces:**
- Consumes: Task 0 çıktısı (temiz biçim).
- Produces: Tek otorite (`roles/` + `modules/`); `doc/_arsiv/` konvansiyonu; INDEX'ten eski dosya referansları temizlenmiş.

- [ ] **Step 1: Çakışma/benzersiz-bilgi farkını çıkar**

Her eski dosya için ilgili yeni dosyalarla karşılaştır ve "eskide olup yenide olmayan hâlâ geçerli bilgi"yi listele:
```bash
# Örnek: öğretmen fonksiyonel v1 vs roles/ogretmen.md + modüller
wc -l doc/ogretmen_rolu_fonksiyonel_dokuman_v1.md doc/roles/ogretmen.md
```
Eşleştirme: `ogretmen_rolu_*` → `roles/ogretmen.md` (+ ilgili `modules/m02,m04,m05,m06`); `ogrenci_rolu_*` → `roles/ogrenci.md` + `roles/ogrenci_ux.md` (+ `modules/m08,m10`); `veli_rolu_*` → `roles/veli.md` (+ `modules/m09`); `is_akislari.md` → ilgili `modules/*` iş kuralları + `roles/00_roller_genel_bakis.md`.

- [ ] **Step 2: Benzersiz geçerli bilgiyi hedef dosyalara taşı**

Step 1'de bulunan hâlâ geçerli bilgiyi ilgili `roles/`/`modules/` dokümanına ekle (doğruluk hiyerarşisine uyarak; kodla çelişeni ekleme, düzeltilmiş halini ekle). Her hedef dosyanın `Güncelleme:` tarihini 2026-08-19 yap.

- [ ] **Step 3: Arşiv klasörü + taşıma**

```bash
mkdir -p doc/_arsiv
git mv doc/ogretmen_rolu_fonksiyonel_dokuman_v1.md doc/_arsiv/
git mv doc/ogrenci_rolu_fonksiyonel_dokuman_v1.md doc/_arsiv/
git mv doc/veli_rolu_fonksiyonel_dokuman_v1.md doc/_arsiv/
git mv doc/is_akislari.md doc/_arsiv/
```
Her taşınan dosyanın başına şu notu ekle:
```markdown
> ⚠️ **ARŞİV (2026-08-19):** Bu doküman tarihîdir. Geçerli otorite `doc/roles/` + `doc/modules/`'tedir. Buradaki bilgi yalnızca geçmiş referans içindir; çelişkide roles/modules esastır.
```

- [ ] **Step 4: Diyagram kaynak referanslarını + INDEX'i güncelle**

`doc/diagrams/rol_sayfa_mimarisi/*.md` içinde `..._rolu_fonksiyonel_dokuman_v1.md`'ye giden linkleri `_arsiv/` yoluna güncelle (veya roles/'e yönlendir). `doc/INDEX.md` §1/§5.1'de bu 4 dosyanın satırlarını "arşivlendi" olarak güncelle.

- [ ] **Step 5: Doğrula**

Run: Ortak kırık link kontrolü.
Expected: Bu 4 dosyaya giden `KIRIK` link kalmadı (hepsi `_arsiv/` veya roles/'e çözülüyor).

- [ ] **Step 6: Commit**

```bash
git add doc && git commit -m "docs(temizlik): Geçiş 1 — eski dev fonksiyonel dokümanları birleştir + _arsiv

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Modül derin kod-senkron (m01–m18)

**Files:**
- Modify: `doc/modules/mNN_*.md` (her modül), `doc/modules/00_genel_bakis.md` (endpoint envanteri + durum), `doc/modules/veri_modeli.md` (ER)
- Kaynak: `src/Modules/<Ad>/API/*Module.cs`, `.../Domain/**`, `.../Application/**`

**Interfaces:**
- Consumes: Task 1 (tek otorite modüllerde).
- Produces: Koddan doğrulanmış modül dokümanları; sonraki görevler (roller/INDEX) buradaki gerçeğe dayanır.

**Modül→klasör eşlemesi:** m01=Identity, m02=Teachers, m03=Students, m04=Scheduling, m05=LessonSessions, m06=Assignments, m07=Payments, m08=Study, m09=Parents, m10=ProgressTracking, m11=Notifications, m12=Matching, m13=Reviews, m14=Reporting, m15=Settings. **m16=Messaging, m17=Membership, m18=Feedback → backend klasörü YOK.**

Aşağıdaki prosedürü **her modül için sırayla** uygula (m01→m18). Her modül kendi doğrulama+düzeltme döngüsüdür.

- [ ] **Step 1: Modülün gerçek endpoint'lerini çıkar**

Run (örnek m06=Assignments):
```bash
grep -rInE 'Map(Get|Post|Put|Delete|Patch)\(' src/Modules/Assignments/API/
```
Expected: Gerçek route listesi. Bunu `mNN_*.md`'deki endpoint tablosuyla karşılaştır.

- [ ] **Step 2: Domain alanları + enum'ları çıkar**

Run (örnek):
```bash
find src/Modules/Assignments/Domain -name "*.cs" | grep -v '/obj/\|/bin/'
grep -rInE 'public enum |public .* \{ get;' src/Modules/Assignments/Domain/ | grep -v '/obj/\|/bin/'
```
Expected: Gerçek entity/alan/enum listesi. Dokümandaki domain modeli ve `veri_modeli.md` ER'iyle karşılaştır.

- [ ] **Step 3: Farkları modül dokümanına yansıt**

`mNN_*.md`'de: eksik/yanlış endpoint'leri düzelt, kaldırılmış olanı sil, enum değerlerini gerçeğe eşitle, domain alanlarını güncelle, durumu (🟢/🟡/🔴) koda göre ayarla. `Güncelleme:` = 2026-08-19.

- [ ] **Step 4: Çapraz-kesit dosyaları güncelle**

`00_genel_bakis.md` endpoint envanterine bu modülün düzeltilmiş satırlarını işle; `veri_modeli.md` ER'inde bu modülün tabloları/ilişkilerini gerçeğe göre düzelt.

- [ ] **Step 5: m16/m17/m18 özel durumu**

Bu üç modülde backend klasörü olmadığını doğrula:
```bash
ls src/Modules/Messaging src/Modules/Membership src/Modules/Feedback 2>&1
```
Expected: "No such file or directory". Dokümanlarında durumu 🔴 iskelet/planlanan olarak netleştir; "gerçekte kod yok" notu ekle; olmayan endpoint'leri "planlanan" işaretle.

- [ ] **Step 6: Modül döngüsünü tekrarla**

Step 1–4'ü m01…m15 için, Step 5'i m16/m17/m18 için tamamla. Her ~3-4 modülde bir ara commit uygundur (izlenebilirlik):
```bash
git add doc/modules && git commit -m "docs(temizlik): Geçiş 2 — mXX-mYY kod-senkron

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

- [ ] **Step 7: Geçiş kapanış doğrulaması + commit**

Run: Ortak kırık link kontrolü + `grep -rn "EgittimUssu" doc/modules` (çift-t yanlışı).
Expected: Kırık link yok; `EgittimUssu` sonucu boş.
```bash
git add doc/modules && git commit -m "docs(temizlik): Geçiş 2 — modül kod-senkron tamam

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Mimari & sayfa senkronu

**Files:**
- Modify: `doc/architecture/*.md`, `doc/pages/*.md`, `doc/pages/00_pages_index.md`, `doc/tab_widget.md`, `doc/architecture/widgets.md`
- Kaynak: `src/` (backend), `mobile/lib/features/**/presentation/pages/*.dart`, `mobile/lib/shared/widgets/`

**Interfaces:**
- Consumes: Task 2 (modül gerçeği).
- Produces: Koddan doğrulanmış mimari + sayfa dokümanları.

- [ ] **Step 1: Gerçek ekran envanterini çıkar**

Run:
```bash
find mobile/lib -name "*_page.dart" -o -name "*_screen.dart" | grep -v '/obj/' | sort
```
Expected: Gerçek ekran listesi. `doc/pages/00_pages_index.md` ve tekil `pages/*.md` ile karşılaştır: kodda olmayan ekran dokümanlarını "planlanan" işaretle; kodda olup dokümanı olmayan ekranlar için satır ekle (veya en azından index'e not düş).

- [ ] **Step 2: Ortak widget envanterini çıkar**

Run:
```bash
find mobile/lib/shared/widgets -name "*.dart" | sort
```
Expected: Gerçek widget listesi. `doc/architecture/widgets.md` katalogunu (API + durum 🟢/🟡/🔴) gerçeğe eşitle; `tab_widget.md`'deki `AppSegmentedTab` referansını doğrula.

- [ ] **Step 3: Mimari dokümanları koddan doğrula**

`architecture/mobile_flutter.md` (bloc/get_it/go_router/dio + §13 ekran rehberi), `architecture/backend.md` (çözüm/modül anatomisi), `architecture/web_angular.md` (🔴 planlanan olduğu net) dosyalarını gerçekle karşılaştır; sapmaları düzelt. Idealize/planlanan bölümleri açıkça etiketle (örn. mobile_flutter §14 "idealize veri modeli").

- [ ] **Step 4: Tarih + doğrula**

Düzenlenen her dosyada `Güncelleme:` = 2026-08-19. Run: Ortak kırık link kontrolü.
Expected: Kırık link yok.

- [ ] **Step 5: Commit**

```bash
git add doc/architecture doc/pages doc/tab_widget.md && git commit -m "docs(temizlik): Geçiş 3 — mimari & sayfa kod-senkron

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Roller uzlaştırma

**Files:**
- Modify: `doc/roles/*.md` (00_roller_genel_bakis, ogretmen, ogrenci, ogrenci_ux, veli, admin)

**Interfaces:**
- Consumes: Task 2 (modül gerçeği) + Task 3 (ekran gerçeği).
- Produces: Modül/ekran gerçeğiyle hizalı rol perspektif dokümanları.

- [ ] **Step 1: Her rol dokümanını modül/ekran gerçeğiyle karşılaştır**

Her `roles/*.md` içindeki yetenek/akış iddialarını Task 2'de doğrulanan modül durumları ve Task 3'te doğrulanan ekranlarla eşleştir. Rol×yetenek matrisindeki (00_roller_genel_bakis) durumları düzelt.

- [ ] **Step 2: Faz/durum etiketlerini eşitle**

Rol dokümanlarındaki 🟢/🟡/🔴 ve faz bilgisini modül gerçeğine ve PRD v2.1 fazlarına göre hizala (çelişkide kod > INDEX §0 > PRD).

- [ ] **Step 3: Tarih + doğrula**

`Güncelleme:` = 2026-08-19. Run: Ortak kırık link kontrolü.
Expected: Kırık link yok.

- [ ] **Step 4: Commit**

```bash
git add doc/roles && git commit -m "docs(temizlik): Geçiş 4 — roller modül/ekran gerçeğiyle uzlaştırma

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: INDEX uzlaştırma + global tutarlılık

**Files:**
- Modify: `doc/INDEX.md`, gerektiğinde herhangi bir `doc/**/*.md` (son çelişki süpürmesi)

**Interfaces:**
- Consumes: Task 0–4 (tüm dokümanlar gerçeğe göre düzeltilmiş).
- Produces: Gerçeğe birebir karşılık gelen INDEX + sıfır kırık link + kanonik gerçeklerle tam tutarlılık.

- [ ] **Step 1: INDEX'i gerçek dosya ağacına göre yeniden kur**

Run:
```bash
find doc -name "*.md" | sort
```
INDEX'teki her satırı bu ağaçla karşılaştır: olmayan dosyalara link kaldır/düzelt, yeni dosyaları (örn. `_arsiv/`) ekle, modül durum tablosunu (§3) Task 2'deki gerçek durumlarla eşitle, `_arsiv`'e taşınanları §1/§5.1'de işaretle. `Son güncelleme:` = 2026-08-19.

- [ ] **Step 2: Kanonik gerçek çelişki süpürmesi**

Run:
```bash
grep -rn "EgittimUssu" doc          # çift-t yanlışı — boş olmalı
grep -rniE "\.NET [0-8]([^0-9]|$)" doc   # yanlış .NET sürümü şüphesi
grep -rn "0xFF082B4F" doc | head     # ana renk kullanımı
```
Expected: `EgittimUssu` boş; yanlış .NET sürümü referansı yok; ad/renk/DB değerleri INDEX §0 ile tutarlı. Bulunan çelişkileri düzelt.

- [ ] **Step 3: Nihai kırık link + fence kontrolü**

Run:
```bash
find doc -name "*.md" | while read f; do grep -oE '\]\(([^)#]+\.md)' "$f" | sed -E 's/^\]\(//' | while read link; do target="$(dirname "$f")/$link"; [ -f "$target" ] || echo "KIRIK: $f -> $link"; done; done
find doc -name "*.md" | while read f; do n=$(grep -c '^```' "$f"); [ $((n%2)) -ne 0 ] && echo "TEK-FENCE: $f"; done
```
Expected: Her iki çıktı da **boş**.

- [ ] **Step 4: Commit**

```bash
git add doc && git commit -m "docs(temizlik): Geçiş 5 — INDEX uzlaştırma + global tutarlılık

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Tamamlanma kanıtı (tüm plan)

- `find doc -name "*.md"` kırık link taraması: **0 sonuç**.
- Tek-fence (kapanmamış kod bloğu) taraması: **0 sonuç**.
- `grep -rn "EgittimUssu" doc`: **0 sonuç**.
- Her modül dokümanının endpoint listesi ilgili `src/Modules/<Ad>/API/*Module.cs` grep'iyle eşleşiyor.
- `doc/INDEX.md` her linki gerçek dosyaya çözülüyor; modül durum tablosu koddaki gerçekle uyumlu.
- Eski 4 dev doküman `doc/_arsiv/`'de, arşiv notu ile; çifte otorite yok.
- Kapsam dışı (Faz 2 makine: raw/ ingest, Obsidian, arama CLI, otomatik health-check) bu planda **yok** — ayrı spec.
```
