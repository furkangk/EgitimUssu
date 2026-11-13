---
title: "Hukuki Metinler — İndeks ve Durum"
summary: "KVKK metinlerinin listesi, doldurulacak alanlar, hukuk onayı durumu ve metin yazmakla kapanmayan açık riskler"
tags: [kvkk, hukuk, index, E-07, Q5]
status: "🟡 taslak seti hazır — hukuk onayı ve alan doldurma bekliyor"
authority: derived
updated: 2026-09-05
---

# ⚖️ Hukuki Metinler — İndeks ve Durum

> **Neden var:** Q5 ("KVKK metinleri hazır mı?") P13 Task 5'i (hesap silme + saklama süreleri)
> bloke ediyordu. Bu klasör o soruyu **taslak düzeyinde** açar: teknik iş artık yürütülebilir.
> **Metinler hukuk onayından geçmeden yayına alınamaz.**

---

## 1. Belgeler

| Belge | Kime | Ne işe yarar |
|-------|------|--------------|
| [`kvkk_veri_envanteri.md`](kvkk_veri_envanteri.md) | **İç** | Koddan çıkarılmış veri envanteri — diğer üç belgenin dayanağı. Kod değişince **önce burası** güncellenir |
| [`kvkk_aydinlatma_metni.md`](kvkk_aydinlatma_metni.md) | **Kullanıcı** | KVKK m.10 aydınlatma — kayıt ekranında ve `/more` → Gizlilik'te gösterilir |
| [`kvkk_acik_riza_metni.md`](kvkk_acik_riza_metni.md) | **Kullanıcı** | Rıza gerektiren her işlem için ayrı metin + arayüz kuralları |
| [`kvkk_saklama_imha_politikasi.md`](kvkk_saklama_imha_politikasi.md) | **İç + kullanıcı** | Saklama süreleri, hesap silme akışı, anonimleştirme — P13 Task 5'in sayı kaynağı |

**Sırayla okunur:** envanter → aydınlatma → açık rıza → saklama.

---

## 2. Doldurulacak alanlar («…»)

Metinlerdeki `«…»` işaretli her alan doldurulmadan yayına alınamaz.

| # | Alan | Nerede | Kim doldurur |
|---|------|--------|--------------|
| 1 | Veri sorumlusu **ticaret unvanı** | Aydınlatma §1 | Sahip |
| 2 | MERSİS / vergi numarası | Aydınlatma §1 | Sahip |
| 3 | Açık adres | Aydınlatma §1, §8 | Sahip |
| 4 | **KVKK başvuru e-postası** (ör. `kvkk@…`) | Aydınlatma §1, §8 | Sahip — kurulması gerekiyor |
| 5 | KEP adresi | Aydınlatma §1, §8 | Sahip |
| 6 | **VERBİS** kaydı var mı / yükümlülük doğuyor mu | Aydınlatma §1 | Hukuk |
| 7 | Yürürlük tarihi + sürüm | Aydınlatma başlık | Yayın anında |
| 8 | Metinlerin yayımlanacağı web adresi | Aydınlatma §10 | Sahip (web sitesi henüz yok) |
| 9 | **Yurt dışı aktarım dayanağı** (yeterlilik kararı / standart sözleşme / diğer) | Aydınlatma §4 | Hukuk — bkz. §4 R-1 |
| 10 | 5651 s. Kanun kapsamı ve trafik bilgisi saklama süresi | Saklama §2.4 | Hukuk |

---

## 3. Onay durumu

| Adım | Durum |
|------|-------|
| Taslak metinler | ✅ Hazır (2026-09-05) |
| Koddan veri envanteri | ✅ Hazır |
| «…» alanlarının doldurulması | ⬜ Bekliyor |
| **Avukat / veri koruma danışmanı incelemesi** | ⬜ Bekliyor |
| VERBİS yükümlülüğü değerlendirmesi | ⬜ Bekliyor |
| Yurt dışı aktarım dayanağının kurulması | ⬜ Bekliyor |
| Uygulamada gösterim (kayıt ekranı + Ayarlar) | ⬜ Bekliyor — kod işi |
| Rıza kaydı tablosu (`consent_records`) | ⬜ Bekliyor — kod işi |

> ⚠️ **Ben avukat değilim.** Bu metinler KVKK'nın yapısına ve **bu kod tabanının gerçek veri
> envanterine** göre yazılmıştır; genel bir şablon değildir, ama hukuki mütalaa da değildir.
> Yayına almadan önce bir hukukçuya inceletin — özellikle §4'teki üç madde.

---

## 4. Metin yazmakla **kapanmayan** riskler

Bunlar taslakları onaylatmakla çözülmez; sözleşme, karar veya kod gerektirir.

| # | Risk | Neden ciddi | Ne gerekiyor |
|---|------|-------------|--------------|
| **R-1** | **Altyapının tamamı yurt dışında** (barındırma, veritabanı, önbellek, push, dosya) | Sistem ilk kullanıcı kaydıyla yurt dışına aktarım yapar. KVKK m.9 (7499 s. Kanun'la değişik) bunu yeterlilik kararına / uygun güvencelere bağlar; standart sözleşme kullanılırsa **5 iş günü içinde Kurul'a bildirim** gerekir | Hukuki dayanağın kurulması **veya** yurt içi barındırmaya geçiş. Metinle kapanmaz |
| **R-2** | **Çocuk verisi** — kullanıcıların çoğu reşit değil, ama yaş bilinmiyor ve veli onayı adımı yok | Küçüğün hukuki işlem ehliyeti sınırlıdır; rıza ve sözleşme kural olarak yasal temsilcinin katılımını gerektirir. Bugün sistem yaşı **bilmiyor** (`DateOfBirth` opsiyonel), dolayısıyla kuralı uygulayamıyor | Doğum tarihinin zorunlu hâle gelmesi + 18 yaş altı için veli onayı akışı (envanter §5, Ç-1…Ç-5) |
| **R-3** | **Rıza kaydı yok** | `user_settings`'teki boolean rızanın *şu anki durumunu* tutar; ne zaman, hangi metin sürümüne verildiğini tutmaz. İspat yükü veri sorumlusundadır — bugün ispat edilemez | `consent_records` tablosu (açık rıza metni §7) |
| **R-4** | **Serbest metin alanları özel nitelikli veri taşıyabilir** | "Disleksi tanısı var" yazan bir ders notu, m.6 rejimine giren sağlık verisi üretir | Arayüz uyarıları + veri minimizasyonu; kalan risk kabul edilir ve kayıt altında tutulur |
| **R-5** | **Öğretmen-yönetimli öğrenciye aydınlatma ulaşmıyor** | Hesabı olmayan öğrenciye metin gösterilemiyor; yükümlülük yine veri sorumlusunun | Öğretmen beyanı + e-posta ile iletim (envanter §5, Ç-4) |
| **R-6** | **Yedekler silme talebinin kör noktası** | Silinen veri 30 gün yedeklerde durur | Saklama politikası §2.4'te açıklandı; geri yükleme sonrası silmelerin yeniden uygulanması gerekir |

> **En acili R-1 ve R-2'dir.** R-1 ilk gerçek kullanıcıdan itibaren işler; R-2 ise ürünün hedef
> kitlesinin tanımı gereği neredeyse her kullanıcıda geçerlidir.

---

## 5. Bunlar henüz yok

| Belge | Ne zaman gerekir |
|-------|------------------|
| Kullanıcı Sözleşmesi / Kullanım Koşulları | Yayına çıkmadan önce — KVKK'dan ayrı bir belgedir |
| Mesafeli Satış Sözleşmesi + Ön Bilgilendirme | **Ücretli abonelik açılınca** (P09 Task 4 / Q1) |
| Çerez Aydınlatma Metni | **Web arayüzü gelince** (P14 / Angular) |
| İYS kaydı + ticari ileti onay yönetimi | **Pazarlama iletisi gönderilmeden önce** (6563 s. Kanun) |
| Veri İhlali Müdahale Prosedürü | Yayına çıkmadan önce — ihlal, **72 saat** içinde Kurul'a bildirilir |
| Sağlayıcılarla veri işleyen sözleşmeleri | Her dış hizmet sağlayıcı için |

---

## 6. Metinler değişince

1. Kod değiştiyse **önce** [`kvkk_veri_envanteri.md`](kvkk_veri_envanteri.md) güncellenir.
2. Envanterden etkilenen metinler güncellenir; **sürüm numarası artırılır**.
3. Esaslı değişiklikte kullanıcılara bildirilir; rıza metni değiştiyse **rıza yenilenir**
   (eski onay, yeni metni kapsamaz — açık rıza metni §7).

---

*Hukuki Metinler İndeksi | Güncelleme: 2026-09-05 (ilk sürüm — Q5 taslak düzeyinde açıldı)*
