# P21 — Üç Rolün Bağ ve Günlük Akışları (Mobil) — Hafif Plan 🅻

> **Hafif plan:** Görevler, kapsadığı maddeler ve kabul kriterleri yazılıdır; adım adım `Files/Interfaces/Steps` **yoktur**. `/devam` bu plana geldiğinde **önce Task 0'ı** yürütür: planı kodu okuyarak 2026-09-02 plan biçiminde ayrıntılandırır, commit'ler, sonra Task 1'e geçer.

**Goal:** Backend'de hazır olan ama mobilde ekranı olmayan akışları tamamlamak: öğretmen öğrencisini ve velisini gerçekten davet edebilsin, veli çocuğunu kodla bağlayabilsin, öğrenci veli bağını onaylayabilsin, takvim ve ödeme mutabakatı mobilde yapılabilsin, hesap silinebilsin.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) §5.3. **Kaynak:** rapor §2.1–2.3, N-61, N-66, §4 (4, 5, 7, 8, 9).
**Dal:** `feat/p21-rol-akislari` · **Ön koşul:** P15 (yeni kod biçimi, erişim), P17 (kapsam API'si), P18 (para/idempotency), P20 (rota tablosu).

## Global Constraints

- Yeni/değişen her ekran: ARB, widget testi, `doc/pages/<ekran>.md` + `00_pages_index.md`.
- Davet kodları K-24 biçiminde gösterilir/girilir (8 karakter, büyük harf, karışmayan alfabe); paylaşım `share_plus` ile.
- Para girişleri `Money.parseTr` (P18 T4); yazma çağrıları `idempotent: true`.
- **Görsel referans (2026-11-14):** [ekran taslakları tuvali](https://claude.ai/artifact/6e7YYdH63gdDy8yeQFf77Q) — 54 ekran, design system'e göre. Çelişkide **özellik ve alanlar koddan**, görsel dil taslaktan alınır; taslağın koddan saptığı yerler (ör. veli "Çocuk bağla" satır içi davet kodu kartı, sohbette karşı tarafın adı) öneridir, Task 0'da kapsama alınıp alınmayacağı yazılır.

---

### Task 0: Planı ayrıntılandır
- [ ] Her görev için ilgili backend uçlarını (`src/Modules/*/API/*Module.cs`) ve mevcut mobil ekranları oku; `Files/Interfaces/Steps` yaz; commit `docs(plan): P21 ayrintilandirildi`.

### Task 1: Öğretmen daveti gerçek uca (N-61, rapor 2.1-1)
- `students_page.dart` `_submitInvite` → öğrenci için `POST .../invite`, veli için `POST .../parent-invite`; kod + paylaş sayfası; bekleyen davetler listesi ve yeniden gönder/iptal.
- **Kabul:** davet üretildi → kod ekranda + paylaşılabilir; sahte snackbar yok; davet süresi görünür.

### Task 2: Öğrenci "Velim" + profil kutucukları (rapor 2.2-1, 2.2-2, §4-4)
- Bekleyen veli taleplerini onayla/reddet; onaylı velileri listele/kaldır; kendi veli davet kodunu üret/paylaş.
- Profil kutucukları gerçek ekranlara: profil düzenle, bildirim tercihleri, gizlilik, Aboneliğim (`/membership`), hesap; `_isPremium` sabiti kaldırılır (`MembershipCubit`'ten okunur).
- **Kabul:** veli talebi öğrenci tarafından onaylanınca veli panosu dolar; profilde "yakında" kalmaz.

### Task 3: Veli — kodla katılım ve hesap girişleri (rapor 2.3-1, 2.3-2, §4-4)
- `parent_children_page.dart` GUID alanı → **davet kodu** alanı (`claim-invite`); bekleyen bağlar için durum açıklaması ("Öğrencinin onayı bekleniyor").
- Veli profilinde: mesajlar, bildirim gelen kutusu, bildirim tercihleri, gizlilik, cihaz oturumları, üyelik, hesap silme.
- **Kabul:** veli kodu girer → bağ **anında onaylı**, panoda çocuk görünür; hiçbir ekranda GUID istenmez.

### Task 4: Öğretmen takvim işlemleri (rapor 2.1-3, §4-7)
- **K-30 (kullanıcı onaylı 2026-11-14):** öğretmen alt menüsü 6 → 5 sekme — Ana sayfa · Program · Öğrenciler · Finans · Diğer; "Program" Takvim ve Dersler'i `Takvim | Dersler` bölümlü seçiciyle birleştirir (taslak O04/O08). Derin bağlantılar `/scheduling` ve `/lesson-sessions` çalışmaya devam eder.
- Tatil bloğu oluştur/listele/sil (P16 T4 çakışma uyarısıyla); öğrenci erteleme talepleri gelen kutusu (kabul/ret, oluşum bazlı — P17 T1); online ders linkine dokununca `url_launcher` ile açılır (kopyala ikincil).
- **Kabul:** tatil bloğu takvimde görünür ve içine ders açmak uyarı verir; talep kabulü yalnız o oluşumu taşır.

### Task 5: Ödeme mutabakatı (rapor 2.1-4, 2.3-3)
- Veli: borçlu kayıtta "Ödedim" beyanı (tutar varsayılan kalan bakiye).
- Öğretmen: bekleyen beyanlar → onay/ret; öğrenci bazlı ders ücreti (`/rate`); arşivlenmiş öğrenciyi geri al.
- **Kabul:** beyan onayı tahsilat hareketi üretir (P18 T2); öğrenci ücreti yeni ders ücret kaydına yansır.

### Task 6: Öğrenci erteleme/iptal talebi (rapor 2.2-3)
- Ders detayında "Erteleme iste" (önerilen yeni zaman + not) ve talep durumu.
- **Kabul:** talep öğretmenin gelen kutusuna düşer; aynı oluşuma ikinci talep engellenir (P17 T5).

### Task 7: Hesap ekranı + hesap silme + rıza (N-66, P-05 kısmi, §4-9)
- `account_info_page.dart` sahte satırları kaldırılır; gerçek veriler (`/api/identity/me`, oturumlar); şifre değiştir; **hesap silme talebi** + bekleme süresi + iptal.
- Backend: `ConsentRecord` (kullanıcı, metin türü, sürüm, zaman, kanal) + `POST /api/identity/consents`; kayıt akışında aydınlatma metni + açık rıza onay kutuları (metin içerikleri `doc/legal/` taslağından, sürüm `v0-taslak`).
- **Kabul:** App Store 5.1.1(v) / Play hesap silme kuralı: uygulama içinden silme başlatılabilir; kayıt sırasında rıza alınmadan hesap açılmaz.

### Task 8: Sohbet adları ve veli sohbet başlatma (rapor 2.3-4)
- `ConversationSummaryResponse`'a karşı tarafın görünen adı (Identity/Students sözleşmesiyle); veli, onaylı çocuğunun özel öğretmenleriyle sohbet başlatabilir.
- **Kabul:** gelen kutusunda isimsiz satır yok.

### Task 9: Kapanış — üç rol senaryosu + doküman çelişkileri
- Mobil entegrasyon testi (fake repository) + gerçek cihaz turu: öğretmen davet → öğrenci katılır → veli kodla bağlanır → öğrenci onaylar → ders → ödedim → onay.
- `roles/veli.md` (ödedim, çalışma durumu tutarlılığı — §4-5), `roles/ogretmen.md` (online link — §4-7), parite dokümanı R-10/R-11 notu (§4-8), `doc/legal/00_legal_index.md` (§4-9).
