---
title: "Doküman Haritası (INDEX)"
summary: "doc/ altındaki tüm dokümanların tek listesi ve kanonik gerçekler; hangi konuda hangi dokümana bakılacağını gösterir"
tags: [index, harita, kanonik]
authority: derived
updated: 2026-11-15
---

# 📚 EğitimÜssü — Doküman Haritası (INDEX)

> **Bu dosyanın amacı:** Projedeki tüm dokümanların **tek listesi**. Yapay zekâya tüm md'leri tek tek vermek yerine
> yalnızca bu dosyayı verin; hangi dokümanın ne işe yaradığını ve ne zaman okunması gerektiğini buradan görür.
>
> **Bu dosya her zaman güncel tutulmalıdır** (yeni doküman eklenince/silinince/amacı değişince) — bkz. kökteki `CLAUDE.md` → "Doküman bakımı".
>
> **Son güncelleme:** 2026-11-13 (§6'ya `denetim/2026-11-13_kapsamli_proje_incelemesi.md` eklendi — P01–P13 main'e merge edildikten sonra tüm projenin koda karşı incelemesi: N serisi mantık hataları, ✅ işaretli olup hâlâ bozuk maddeler, rol bazlı eksik özellikler, P serisi profesyonellik açıkları). Önceki: 2026-09-05 (**P13 Task 8 kapanışı** — §1.1'e `doc/architecture/` klasörünün **indekslenmemiş 9 dokümanı** eklendi (`api_versioning` · `environments` · `backup_restore` · `kvkk` + `ux_rules` · `anti_patterns` · `animations` · `accessibility` · `figma_references`); staging profilinde uçtan uca gözlemlenebilirlik ve **yedekten geri yükleme tatbikatı** koşuldu. ⚠️ Bulgu: outbox/handler işi HTTP trace'ine bağlanmıyor — `render.yaml`'a gözlemlenebilirlik anahtarları eklendi, kalan iş P13 Task 9'a açıldı). Önceki: 2026-09-05 (iki rakip analizi **derinleştirildi** — parite dokümanı 35→79 KB: her R/Ü maddesine çözüm taslağı + kabul kriteri + risk, yeni §1.2 olay envanteri / §1.3 desen envanteri / §6 veri modeli etki tablosu, yeni bulgular X-02 ve X-03; rakip analizi 24→36 KB: rakip kartları + gelir modeli haritası + veri kalitesi notları. Word (.doc) ve tarayıcı (.html) kopyaları yeniden üretildi + `_tools/md2doc.py` dönüştürücüsü; §6'ya `denetim/2026-09-05_rakip_paritesi_analizi.md` eklendi — R-01…R-14 parite + Ü-01…Ü-10 üstünlük envanteri; **PRD §9.1–9.2 koda göre düzeltildi** (streak/rozet Free, Free öğrenci limiti 5) ve aynı drift `m17_membership.md` + `roles/ogrenci.md`'de kapatıldı). Önceki: 2026-09-05 (§6'ya `kaynaklar/2026-09-05_rakip_analizi.md` eklendi — 11 rakip ürünün mağaza verisiyle karşılaştırması; PRD §9.2 streak-premium kararı ve Q1/Q3 için dış kanıt). Önceki: 2026-09-04 (**P10 BİTTİ** — mesajlaşma zinciri gerçek Postgres + Redis'te uçtan uca doğrulandı: M16 🔴 → 🟡, `messaging` şeması + 8 uç + yeni mesaj bildirimi + mobil gelen kutusu/sohbet + `RealMessagingChainTests`; `flutter_chat_ui` kaldırıldı (D-18). Doğrulama bir hata çıkardı: kullanıcı başına mesaj kotası `UseAuthentication` sırası yüzünden IP'ye düşüyordu, düzeltildi. **M16 bilerek 🟡** — karşı tarafın adı, veli sohbet başlatma, şikayet (M18/P12) ve cihaza push (Q8) açık). Önceki: 2026-09-04 (P09 Task 7 — **kısmi kapanış**: üyelik zinciri gerçek Postgres'te uçtan uca doğrulandı (`RealMembershipEntitlementTests`); M17 **bilerek 🟡 kaldı** — reklam (Q3) ve ödeme/webhook (Q1) doğrulanamadı, para hâlâ tahsil edilemiyor). Önceki: 2026-09-04 (P09 Task 6: **mobil üyelik ekranı + paywall** — `/membership` ve `/membership/paywall`, ortak `PremiumLock`/`PremiumLockCard`, `/more` "Uyelik" satırı aktif; §5'e `pages/membership.md` + `pages/paywall.md` eklendi. **Satın alma hâlâ yok** — checkout ucu Task 4/Q1'e bloke). Önceki: 2026-09-04 (P09 Task 3: **plan kataloğu + ücretsiz deneme + referans kampanyası** — anonim `GET /api/membership/plans`, `start-trial`/`cancel`, `referral-code`/`referral/redeem`, Admin plan CRUD; fiyat tohumlaması yalnız Development, **Q2 onaylanmadı**). Önceki: 2026-09-04 (P09 Task 2: **K-09 entitlement** — `IEntitlementDirectory` üyelik haklarının tek kaynağı; Study/Students/Reporting/Parents kapıları devredildi, `GET /api/membership/me` açıldı). Önceki: 2026-09-04 (P09 Task 1: **M17 🔴 → 🟡** — Membership modülü açıldı). Önceki: 2026-09-04 (P08 kapanışı: **M10 🟡 → 🟢** ve **M14 🟡 → 🟢**; §5'e `pages/progress_overview.md` + `pages/teacher_reports.md` eklendi). Önceki: 2026-09-03 (P03: M11 durumu 🟡 → 🟢 — gerçek FCM push + bildirim merkezi; §5'e `pages/notifications.md` eklendi). Önceki: 2026-09-02 (§6'ya `denetim/2026-09-02_eksik_analizi.md` eklendi — 121 maddelik eksik envanteri). Önceki: 2026-08-20 (Bilgi Tabanı Dilim A — frontmatter + health-check; Dilim B — Obsidian görünümü; Dilim C — `raw/` + `kaynaklar/` iskeleti + `/kb-ingest`; Dilim D — `/kb-ask` Q&A + `_cevaplar/` + Marp; `/kb-lint` üretken öneri turu (Linting paritesi); Faz 1 tamam — m01–m18 + mimari/sayfa/rol/INDEX koddan doğrulandı)

---

## 0. Kanonik Gerçekler (Tek Doğruluk Kaynağı)

> Çelişki halinde bunlar esastır. Dokümanlarda farklı bir değer görürseniz yanlıştır, düzeltin.

| Konu | Değer |
|------|-------|
| Görünen ad | **EğitimÜssü** |
| ASCII / kod / dosya adı | **EgitimUssu** (`EgittimUssu` çift-t YANLIŞ) |
| Backend | **.NET 9** modüler monolit (`global.json` → SDK 9.0.311) |
| Mobil | **Flutter** (`flutter_bloc`/Cubit, `go_router`, `dio`, `get_it`) |
| Web | Angular (planlandı — Faz 4-5) |
| Ana renk (primary) | **`0xFF082B4F`** |
| Veritabanı | PostgreSQL (modül başına ayrı şema + DbContext), Redis cache |
| PRD sürümü | **v2.1** (promp.txt vizyonu işlendi) |

---

## 1. Üst Düzey Ürün ve Mimari

| Doküman | Ne işe yarar | Otorite |
|---------|--------------|---------|
| [`promp.txt`](promp.txt) | Kullanıcının kendi sözleriyle proje vizyonu (kaynak girdi) | Vizyon kaynağı |
| [`ozel_ders_platformu_PRD_v2.md`](ozel_ders_platformu_PRD.md) | **PRD v2.1**: vizyon, kullanıcılar, M01–M18 modül listesi, 6 fazlı yol haritası, iş modeli, free/premium, reklam/kampanya | Ürün için **esas** |
| [`yol_haritasi.md`](yol_haritasi.md) | **Geliştirme yol haritası**: Faz 0–5, epic→faz eşlemesi, bağımlılıklar, milestone'lar | Planlama |
| [`architecture/00_genel_bakis.md`](architecture/00_genel_bakis.md) | **Mimari genel bakış**: platformlar, aktörler, katmanlar, veri akışı, event/Outbox, ölçeklenebilirlik, fazlar | Mimari için **buradan başla** |

## 1.1 Mimari (`doc/architecture/`) — Platforma Göre, Koddan Doğrulanmış

> Eski tek-parça `ai_ready_architecture.md` + `design.md` + `tutormatch_flutter_ui_design.md` bu klasöre bölündü/birleştirildi.

| Doküman | Kapsam |
|---------|--------|
| [`architecture/00_genel_bakis.md`](architecture/00_genel_bakis.md) | Sistem geneli + bu klasörün haritası |
| [`architecture/backend.md`](architecture/backend.md) | .NET 9 modüler monolit: çözüm yapısı, modül anatomisi, Shared/Kernel, CQRS, Outbox, persistence, JWT |
| [`architecture/mobile_flutter.md`](architecture/mobile_flutter.md) | Flutter mimari (bloc/get_it/go_router/dio) + tasarım uygulaması + 20 ekran görsel rehberi |
| [`architecture/web_angular.md`](architecture/web_angular.md) | Angular web — 🔴 planlanan (Faz 4-5) |
| [`architecture/design_system.md`](architecture/design_system.md) | Platformlar-arası ortak görsel token (renk/tipografi/spacing) + Atomic/CBD — **token tek doğruluk kaynağı** |
| [`architecture/widgets.md`](architecture/widgets.md) | Ortak widget kataloğu: paylaşılan bileşenlerin API + kural + durumu (🟢/🟡/🔴) |
| [`architecture/ux_rules.md`](architecture/ux_rules.md) | Tüm ekranlarda geçerli UX kararları, etkileşim ilkeleri ve navigasyon kuralları; rol bazlı öncelikler |
| [`architecture/anti_patterns.md`](architecture/anti_patterns.md) | Yapılmaması gereken UI/UX ve kod kalıpları — her madde "neden yanlış + doğrusu" ile |
| [`architecture/animations.md`](architecture/animations.md) | Geçiş animasyonları, mikro-etkileşimler, performans kuralları (amaçlı · hızlı · iptal edilebilir) |
| [`architecture/accessibility.md`](architecture/accessibility.md) | Erişilebilirlik kuralları: ekran okuyucu, renk kontrastı, dokunma hedefi, yazı boyutu ölçekleme |
| [`architecture/figma_references.md`](architecture/figma_references.md) | Figma dosya referansları + ekran-component eşlemeleri (⚠️ Figma dosyası henüz yok) |
| [`architecture/api_versioning.md`](architecture/api_versioning.md) | **API sürümleme (C-10)**: yayınlanan sözleşme `v1`, kırıcı değişiklik tanımı, `X-Api-Version` başlığı, uyarı süresi + kaldırma prosedürü |
| [`architecture/environments.md`](architecture/environments.md) | **Ortamlar (E-06)**: Development / Staging / Production farkları, hangi sağlayıcı nerede açık, demo veri tohumlaması, gözlemlenebilirlik anahtarları |
| [`architecture/backup_restore.md`](architecture/backup_restore.md) | **Yedekleme & geri yükleme (E-05)**: prosedür, RPO/RTO, üretimde migration'ın ayrı deploy adımı olması, `Down()` geri alma kuralı |
| [`architecture/kvkk.md`](architecture/kvkk.md) | **KVKK teknik tarafı (E-07)**: hesap silme akışı, `IPersonalDataAnonymizer` sözleşmesi, modül modül hangi veri ne oluyor, saklama bakım servisleri |

## 1.2 Hukuki Metinler (`doc/legal/`) — KVKK

> ⚠️ **Taslak seti.** Hukuk onayından geçmemiştir ve `«…»` alanları doldurulmadan yayına alınamaz.
> Durum + doldurulacak alanlar + metin yazmakla kapanmayan riskler: [`legal/00_legal_index.md`](legal/00_legal_index.md).

| Doküman | Kapsam |
|---------|--------|
| [`legal/00_legal_index.md`](legal/00_legal_index.md) | **Buradan başla.** Belge listesi, doldurulacak `«…»` alanları, onay durumu ve **R-1…R-6 açık riskleri** (yurt dışı aktarım, çocuk verisi, rıza kaydı). Henüz yazılmamış belgeler (kullanıcı sözleşmesi, mesafeli satış, çerez, İYS, ihlal prosedürü) de burada |
| [`legal/kvkk_veri_envanteri.md`](legal/kvkk_veri_envanteri.md) | **İç belge, diğer üçünün dayanağı.** Koddan çıkarılmış kişisel veri envanteri: veri kategorisi → tablo/alan → amaç → hukuki sebep → alıcı. Alıcılar ve **yurt dışına aktarım** tablosu, özel nitelikli veri riski (serbest metin), çocuk verisi açıkları Ç-1…Ç-5. Kod değişince **önce burası** güncellenir |
| [`legal/kvkk_aydinlatma_metni.md`](legal/kvkk_aydinlatma_metni.md) | **Kullanıcıya gösterilecek** KVKK m.10 aydınlatma metni: işlenen veri (rol bazında), amaç + hukuki sebep, aktarım, 18 yaş altı, m.11 hakları ve başvuru usulü. Kayıt ekranında ve Ayarlar → Gizlilik'te gösterilir |
| [`legal/kvkk_acik_riza_metni.md`](legal/kvkk_acik_riza_metni.md) | **Kullanıcıya gösterilecek** rıza metinleri — her amaç için ayrı: profil fotoğrafı, çalışma verisinin öğretmen/veli ile paylaşımı, ticari ileti, reklam. Üç arayüz kuralı (rıza aydınlatmadan ayrı · hizmetin şartı değil · geri alınabilir) + **rıza gerektirmeyen** işlemler listesi + saklanacak rıza kaydı alanları |
| [`legal/kvkk_saklama_imha_politikasi.md`](legal/kvkk_saklama_imha_politikasi.md) | Her veri türü için **saklama süresi + dayanak + imha yöntemi**, hesap silme akışı (30 gün bekleme), anonimleştirme kuralı (**hash ≠ anonimleştirme** uyarısı), periyodik imha takvimi. P13 Task 5'in uygulayacağı sayıların kaynağı |

## 2. Roller (`doc/roles/`) — Rol Perspektifi

> Her rolün yetenekleri, kullanıcı yolculuğu, ekranları ve rol-özel kuralları. **Teknik detay modüllerdedir.**

| Doküman | Kapsam |
|---------|--------|
| [`roles/00_roller_genel_bakis.md`](roles/00_roller_genel_bakis.md) | Rol×yetenek matrisi + roller-arası kurallar (mesajlaşma çiftleri, üyelik, veli=gerçek kişi, bireysel-önce strateji) |
| [`roles/ogretmen.md`](roles/ogretmen.md) | 👨‍🏫 Öğretmen — takvim-merkezli yönetim (Faz 1, 🟢) |
| [`roles/ogrenci.md`](roles/ogrenci.md) | 🎓 Öğrenci — bireysel çalışma + gelişim (Faz 2, 🟡) |
| [`roles/ogrenci_ux.md`](roles/ogrenci_ux.md) | 🎓 Öğrenci Deneyimi (Student UX) — günlük kullanım/motivasyon odaklı UX vizyonu + ekran hedefleri (🟡 vizyon) |
| [`roles/veli.md`](roles/veli.md) | 👪 Veli — gelişim/ödeme takibi (Faz 2-3, 🟡 Faz 2 uygulandı) |
| [`roles/admin.md`](roles/admin.md) | 🛡️ Admin — doğrulama, moderasyon, destek |

## 3. Modüller (`doc/modules/`) — Saf Teknik (Koddan Doğrulanmış)

> Her backend modülünün domain modeli, API, iş kuralları, durum ve eksikleri. **Buradan başla:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md).

| M | Dosya | Durum | M | Dosya | Durum |
|---|-------|-------|---|-------|-------|
| M01 | [`m01_identity`](modules/m01_identity.md) | 🟢 | M10 | [`m10_progress_tracking`](modules/m10_progress_tracking.md) | 🟢 |
| M02 | [`m02_teachers`](modules/m02_teachers.md) | 🟢 | M11 | [`m11_notifications`](modules/m11_notifications.md) | 🟢 |
| M03 | [`m03_students`](modules/m03_students.md) | 🟢/🟡 | M12 | [`m12_matching`](modules/m12_matching.md) | 🔴 |
| M04 | [`m04_scheduling`](modules/m04_scheduling.md) | 🟢 | M13 | [`m13_reviews`](modules/m13_reviews.md) | 🔴 |
| M05 | [`m05_lesson_sessions`](modules/m05_lesson_sessions.md) | 🟢 | M14 | [`m14_reporting`](modules/m14_reporting.md) | 🟢 |
| M06 | [`m06_assignments`](modules/m06_assignments.md) | 🟢 | M15 | [`m15_settings`](modules/m15_settings.md) | 🟢 |
| M07 | [`m07_payments`](modules/m07_payments.md) | 🟢 | M16 | [`m16_messaging`](modules/m16_messaging.md) | 🟡 uçtan uca doğrulandı (P10); ad/veli-başlatma/şikayet/push yok |
| M08 | [`m08_study`](modules/m08_study.md) | 🟢 | M17 | [`m17_membership`](modules/m17_membership.md) | 🟡 domain+entitlement+plan/kampanya+mobil paywall, uçtan uca doğrulanmış; ödeme+reklam yok |
| M09 | [`m09_parents`](modules/m09_parents.md) | 🟢 | M18 | [`m18_feedback`](modules/m18_feedback.md) | 🔴 yeni |

**Çapraz-kesit:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) (indeks + tech stack + endpoint envanteri) · [`modules/mimari_inceleme.md`](modules/mimari_inceleme.md) (hata/güvenlik/öncelik) · [`modules/veri_modeli.md`](modules/veri_modeli.md) (ER şeması).

## 4. Mobil UI / Tasarım Sistemi

| Doküman | Ne işe yarar |
|---------|--------------|
| [`architecture/mobile_flutter.md`](architecture/mobile_flutter.md) | Flutter mimari + UI rehberi: renk/tipografi/spacing, ortak widget'lar, **20 ekran** (§13). ⚠️ §14 veri modeli idealize — gerçeği modüllerde |
| [`architecture/design_system.md`](architecture/design_system.md) | Ortak görsel token + Atomic/CBD (Flutter & Angular) — token tek doğruluk kaynağı |
| [`architecture/widgets.md`](architecture/widgets.md) | **Ortak widget kataloğu**: paylaşılan bileşenlerin API + kural + durumu (🟢/🟡/🔴) |
| [`tab_widget.md`](tab_widget.md) | Tab/Segment widget tasarımı + Flutter kodu (katalogda `AppSegmentedTab`) |

## 5. Sayfa (Ekran) Dokümanları (`doc/pages/`)

> Kodda **var olan** her Flutter ekranı için birer md. İndeks → [`pages/00_pages_index.md`](pages/00_pages_index.md). Öğretmen, **öğrenci** (4-sekme çalışma paneli), **veli** (parent paneli), **üyelik/paywall** ve **mesajlaşma** ekranları kodda mevcuttur; yalnızca ilan/değerlendirme (M12/M13) ekranları **planlanandır**.
>
> P08 ile gelen ekran dokümanları: [`pages/progress_overview.md`](pages/progress_overview.md) (M10 gelişim + snapshot çizgi grafiği) ve [`pages/teacher_reports.md`](pages/teacher_reports.md) (M14 öğretmen aylık raporu · öğrenci analizi + PDF · veli gelişim bölümü).
>
> P09 ile gelen ekran dokümanları: [`pages/membership.md`](pages/membership.md) (M17 üyelik durumu · iptal · referans kodu) ve [`pages/paywall.md`](pages/paywall.md) (rol bazlı Free/Premium karşılaştırması · plan kartları · ücretsiz deneme; **satın alma pasif**).

> P10 ile gelen ekran dokümanları: [`pages/messaging_inbox.md`](pages/messaging_inbox.md) (M16 gelen kutusu · okunmamış rozeti) ve [`pages/messaging_chat.md`](pages/messaging_chat.md) (sohbet · keyset sayfalama · iyimser gönderim · engelleme; **şikayet P12, gerçek zamanlı v2**).

> P19 Task 7: [`pages/assignments.md`](pages/assignments.md) öğretmen/öğrenci ödev listelerini belgeler; ödev, çalışma geçmişi/notlar ve bildirim merkezi cursor ile sonsuz kaydırır.

## 5.1 Rol Sayfa Mimarisi Diyagramları (`doc/diagrams/rol_sayfa_mimarisi/`)

> Her rol için **fonksiyonel dokümandan türetilen** (uygulamadan değil) sayfa yapısı (IA), içerik ve ilişki/veri-akışı diyagramları (mermaid). Kaynak fonksiyonel dokümanlar **arşivlendi** (2026-08-19) → `doc/_arsiv/*_rolu_fonksiyonel_dokuman_v1.md`; güncel otorite `doc/roles/`'tedir. İndeks → [`rol_sayfa_mimarisi/README.md`](diagrams/rol_sayfa_mimarisi/README.md).

| Rol | Dosya | Kaynak fonksiyonel doküman (⚠️ arşiv) |
|-----|-------|----------------------------|
| 🎓 Öğrenci | [`ogrenci`](diagrams/rol_sayfa_mimarisi/ogrenci.md) | [`_arsiv/ogrenci_rolu_fonksiyonel_dokuman_v1`](_arsiv/ogrenci_rolu_fonksiyonel_dokuman_v1.md) |
| 👨‍🏫 Öğretmen | [`ogretmen`](diagrams/rol_sayfa_mimarisi/ogretmen.md) | [`_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1`](_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md) |
| 👪 Veli | [`veli`](diagrams/rol_sayfa_mimarisi/veli.md) | [`_arsiv/veli_rolu_fonksiyonel_dokuman_v1`](_arsiv/veli_rolu_fonksiyonel_dokuman_v1.md) |

## 6. Türev / Operasyon Dosyaları

| Dosya | Ne işe yarar |
|-------|--------------|
| [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv) | Jira backlog (Epic/Story/Task). Her görevde `faz-N` etiketi → [`yol_haritasi.md`](yol_haritasi.md) ile hizalı. Modül docs değişince güncellenmeli |
| [`denetim/2026-06-30_kapsamli_kod_denetimi.md`](denetim/2026-06-30_kapsamli_kod_denetimi.md) | **Kapsamlı kod denetimi (2026-06-30)**: mimari/güvenlik/DDD/persistence/mobil/operasyon bulguları, skor tablosu, önceliklendirilmiş yol haritası. Anlık denetim artefaktı |
| [`denetim/2026-09-02_eksik_analizi.md`](denetim/2026-09-02_eksik_analizi.md) | **Eksik/yapılmamış işler analizi (2026-09-02)**: backend 15 modül + mobil 41 ekran koda karşı taranıp çıkarılmış 121 maddelik eksik envanteri (ID + kanıt + etki + efor + karar sütunu). Plan çıkarmadan önce karar sütunu doldurulur |
| [`denetim/2026-11-13_kapsamli_proje_incelemesi.md`](denetim/2026-11-13_kapsamli_proje_incelemesi.md) | **Kapsamlı proje incelemesi (2026-11-13, N serisi)**: P01–P13 main'e merge edildikten sonra tüm projenin koda karşı incelemesi — **mantık hataları** (N-01…N-72, şiddet + `dosya:satır` + senaryo + güven işareti), ✅ işaretli olup hâlâ bozuk maddeler (§1.6), **rol bazlı eksik özellikler** (§2), **profesyonellik açıkları** P-01…P-14 (§3), doküman↔kod çelişkileri (§4) ve önerilen dalga sırası (§5). Önceki denetimlerdeki maddeler tekrar edilmedi. **Plana dönüştürüldü:** [`docs/superpowers/specs/2026-11-13-olgunlastirma-master-design.md`](../docs/superpowers/specs/2026-11-13-olgunlastirma-master-design.md) (Olgunlaştırma programı P15–P29, okul öğretmeni dahil) |
| [`denetim/2026-09-05_rakip_paritesi_analizi.md`](denetim/2026-09-05_rakip_paritesi_analizi.md) | **Rakip paritesi ve farklılaşma analizi (2026-09-05)**: rakiplerde olup bizde olmayan **R-01…R-14**, hiçbir rakipte olmayıp bizde olabilecek **Ü-01…Ü-10** ve doküman≠kod çelişkileri **X-01…X-03**. Her madde kod gerçeğine karşı doğrulanmış; `2026-09-02_eksik_analizi.md` şemasına **çözüm taslağı** (modül/entity/olay/uç/ekran), **kabul kriteri** ve **risk** alanları eklenmiş. Ayrıca: mevcut domain olayları envanteri (§1.2), yerleşik desenler (§1.3), veri modeli/olay etki tablosu (§6), 6 dalgalı sıralama (§7), karar özeti (§9). ⚠️ §3: PRD §9.1–9.2 ile kod çelişiyordu, **PRD düzeltildi**. 📄 Aynı klasörde `.doc` (Word) ve `.html` (tarayıcı) kopyaları |
| [`kaynaklar/2026-09-05_rakip_analizi.md`](kaynaklar/2026-09-05_rakip_analizi.md) | **Rakip analizi (2026-09-05)**: Doping Hafıza · Pakodemy · Net Up · DUS Club · VeliCep + gerçek rakip kuşağı (Tilki, Özel Ders Alanı, Öğretsen). Mağaza verisiyle doğrulanmış özellik/fiyat matrisi + 9 maddelik çıkarım listesi (streak paywall'ı, IAP vs iyzico, reklam kararı). Rakip kartları ürün/fiyat/konumlandırma kırılımında, gelir modeli haritası, mağaza kuralları (Apple 3.1.1 vs 3.1.3(d)) ve veri kalitesi notlarıyla. **Ürün kararı değil, girdi** — uygulama tarafı `denetim/2026-09-05_rakip_paritesi_analizi.md`'de. 📄 Aynı klasörde `.doc` (Word) ve `.html` (tarayıcı) kopyaları |
| [`_arsiv/`](_arsiv/) | **Arşiv (⚠️ tarihî):** PRD v2.0'dan türetilen eski fonksiyonel/iş-akışı dokümanları (`*_rolu_fonksiyonel_dokuman_v1.md`, `is_akislari.md`). Geçmiş referans; güncel otorite `doc/roles/` + `doc/modules/`. 2026-08-19'da birleştirilip arşivlendi |

## 6.1 Bilgi Tabanı Makinesi (Dilim A + B + C + D)

> Dokümanları koddan doğrulanmış, gezilebilir tutan health-check makinesi. Her `doc/**/*.md` başında makine-okunur frontmatter bulunur. Tasarım/plan: `docs/superpowers/specs|plans/2026-08-20-bilgi-tabani-dilim-a*`.

| Dosya | Ne işe yarar |
|-------|--------------|
| [`00_kb_konvansiyon.md`](00_kb_konvansiyon.md) | **Frontmatter konvansiyonu**: şema (summary/tags/status/authority/code_refs/updated) + authority kuralları + code_refs aile kalıpları. Tüm doc/ bunu izler |
| `_tools/kb_healthcheck.sh` | Deterministik health-check (saf bash): kırık link/fence/kanonik/frontmatter/code_refs/tarih/öksüz. `/kb-healthcheck` slash-komutu bunu sarar (+ `--deep` LLM kod-drift) |
| `_tools/md2doc.py` | **Markdown → Word / HTML** dönüştürücü. `python3 doc/_tools/md2doc.py <dosya.md> [--format doc\|docx\|rtf\|html]` → aynı klasöre yazar. `doc/docx/rtf` macOS `textutil` ile üretilir (pandoc gerekmez); `html` **bağımsız web belgesi** verir (duyarlı, açık/koyu tema, kaydırılabilir tablo, baskı stili). Paylaşılacak analiz/rapor dokümanlarının ofis ve tarayıcı kopyası içindir; **çıktılar üretilmiş dosyadır**, doğruluk kaynağı her zaman `.md` |
| [`_health/`](_health/) | Health-check + **kb-lint** rapor çıktıları (`YYYY-MM-DD-healthcheck.md` / `YYYY-MM-DD-kb-lint.md`) — pass/fail + severity'li bulgular + kod-drift; `/kb-lint` üretken öneriler (tutarsızlık/eksik/aday/soru) |
| [`_obsidian_kurulum.md`](_obsidian_kurulum.md) | **Obsidian görünümü** (Dilim B): vault'u açma, Dataview kurulumu, graph renk anlamları, `_assets` görsel konvansiyonu |
| [`_dashboards/`](_dashboards/) | Dataview panoları (frontmatter'dan canlı): `modul_durum_panosu.md` (INDEX §3'ün oto-üretilen hali) + `kod_dokuman_envanteri.md` (drift riski) |
| `.obsidian/`, `_assets/` | Obsidian vault config (authority-renkli graph, ek klasörü) + görsel/ek klasörü. Kişisel dosyalar `.gitignore`'da |
| [`kaynaklar/`](kaynaklar/00_kaynaklar_index.md) | **Ingest** (Dilim C): `/kb-ingest` ile dış/ham kaynaklardan damıtılmış `reference` makaleleri (research/design/decision) + backlink. İndeks: `00_kaynaklar_index.md` |
| `raw/` | Ham kaynaklar (verbatim: kırpılmış makale/PDF/görsel/döküm). Health-check **muaf**; orijinal `source` referansı buraya bakar |
| [`_cevaplar/`](_cevaplar/00_cevaplar_index.md) | **Q&A** (Dilim D): `/kb-ask` ile wiki'ye sorulan soruların cevapları (md/Marp/mermaid/grafik) + kaynak backlink. İndeks: `00_cevaplar_index.md` |

---

## 7. Hangi Soru → Hangi Doküman?

- **"Rakiplerle aynı seviyeye nasıl gelirim / nasıl öne geçerim?"** → [`denetim/2026-09-05_rakip_paritesi_analizi.md`](denetim/2026-09-05_rakip_paritesi_analizi.md)
- **"Rakipler kim, pazarda ne var, biz nerede duruyoruz?"** → [`kaynaklar/2026-09-05_rakip_analizi.md`](kaynaklar/2026-09-05_rakip_analizi.md)
- **"Ürün ne yapıyor, hangi modül hangi fazda?"** → [`PRD v2.1`](ozel_ders_platformu_PRD.md) §6 + [`modules/00_genel_bakis`](modules/00_genel_bakis.md)
- **"Kişisel veriyi nasıl işliyoruz / KVKK metinleri nerede / hesap silme nasıl işler?"** → [`legal/00_legal_index.md`](legal/00_legal_index.md) (envanter → aydınlatma → açık rıza → saklama)
- **"Staging nerede, demo hesaplar ne, hangi sağlayıcı hangi ortamda açık?"** → [`architecture/environments.md`](architecture/environments.md)
- **"Veritabanını nasıl yedeklerim / geri yüklerim, migration nasıl geri alınır?"** → [`architecture/backup_restore.md`](architecture/backup_restore.md)
- **"API sözleşmesini değiştirebilir miyim, `X-Api-Version` ne anlama geliyor?"** → [`architecture/api_versioning.md`](architecture/api_versioning.md)
- **"Bu rol neler yapabilir / akışı nedir?"** → [`roles/`](roles/00_roller_genel_bakis.md)
- **"Gerçekte hangi endpoint/domain var?"** → [`modules/00_genel_bakis`](modules/00_genel_bakis.md) §4 + ilgili `mNN_*`
- **"Tablolar nasıl ilişkili?"** → [`modules/veri_modeli`](modules/veri_modeli.md)
- **"Sistem mimarisi / katmanlar / event akışı nasıl?"** → [`architecture/00_genel_bakis`](architecture/00_genel_bakis.md) (backend → `architecture/backend.md`, mobil → `architecture/mobile_flutter.md`)
- **"Bu ekran nasıl görünmeli / ne yapıyor?"** → [`architecture/mobile_flutter`](architecture/mobile_flutter.md) §13 + [`pages/`](pages/00_pages_index.md)
- **"Hangi açıkları düzeltmeliyim?"** → [`modules/mimari_inceleme`](modules/mimari_inceleme.md)
- **"Hangi sırayla / hangi fazda geliştireyim?"** → [`yol_haritasi.md`](yol_haritasi.md) + [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv)

*Güncelleme: 2026-11-15 — P19 Task 7.*
