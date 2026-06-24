# CLAUDE.md — EğitimÜssü

Bu dosya her oturumda otomatik yüklenir. Projede çalışan yapay zekâ aşağıdaki kurallara **kendiliğinden** uyar.

## Proje
EğitimÜssü — öğretmen / öğrenci / veli için özel ders yönetim ve eşleştirme platformu.
- **Backend:** .NET 9 modüler monolit (`src/Modules/`), Clean Architecture + DDD + CQRS + Outbox, PostgreSQL (modül başına ayrı şema), Redis.
- **Mobil:** Flutter (`flutter_bloc`/Cubit, `go_router`, `dio`, `get_it`) — `mobile/`.
- **Web:** Angular (planlandı, henüz yok).

## 📖 Dokümanları okumadan önce: INDEX
Tüm dokümanların haritası → **[`doc/INDEX.md`](doc/INDEX.md)**. Bir konuda bağlam gerekiyorsa önce INDEX'e bak,
sonra ilgili dokümanı aç. Kanonik gerçekler (ad, .NET sürümü, ana renk vb.) INDEX §0'dadır; çelişkide o esastır.

Doküman yapısı:
- **Mimari** (platforma göre, koddan doğrulanmış): `doc/architecture/` — `00_genel_bakis.md`, `backend.md`, `mobile_flutter.md`, `web_angular.md`, `design_system.md`, `widgets.md` (ortak widget kataloğu).
- **Roller** (rol perspektifi): `doc/roles/` — `00_roller_genel_bakis.md`, `ogretmen.md`, `ogrenci.md`, `veli.md`, `admin.md`.
- **Modüller** (saf teknik, her backend modülü): `doc/modules/mNN_<ad>.md` (m01–m18) + çapraz-kesit `00_genel_bakis.md`, `mimari_inceleme.md`, `veri_modeli.md`.
- **Sayfalar** (kodda var olan ekranlar): `doc/pages/`. **Ürün:** `doc/ozel_ders_platformu_PRD_v2.md` (v2.1).

## ✅ Doküman bakımı (KALICI KURAL — kullanıcı söylemese de uygulanır)
Kodda veya üründe bir değişiklik yaptığında, **aynı turda** ilgili dokümanı güncel tut. Bu, ayrı bir görev değil;
her işin parçasıdır. Kullanıcının "dokümanı da güncelle" demesini bekleme.

Tetikleyiciler ve yapılacaklar:
- **Yeni endpoint / domain alanı / enum / davranış** eklediysen → ilgili `doc/modules/mNN_*.md` dosyasını ve gerekiyorsa
  `doc/modules/00_genel_bakis.md` (endpoint envanteri + durum tablosu) ile `doc/modules/veri_modeli.md` (ER) güncelle.
- **Bir rolün yeteneği/akışı değiştiyse** → ilgili `doc/roles/<rol>.md` güncelle.
- **Yeni modül** eklediysen → `doc/modules/mNN_<ad>.md` oluştur (mevcut şablon), `00_genel_bakis.md` indeksi + `doc/INDEX.md` + PRD §6 modül tablosu güncelle.
- **Bir modülün durumu değiştiyse** (🔴 İskelet → 🟡 → 🟢) → `00_genel_bakis.md` modül indeksi + ilgili `mNN_*.md` + `doc/INDEX.md` durum sütunu.
- **Yeni ekran/sayfa** eklediysen → `doc/pages/<ekran>.md`'sini oluştur/güncelle ve `doc/pages/00_pages_index.md`'ye satır ekle. Mevcut ekran değiştiyse ilgili sayfa md'sini güncelle.
- **Yeni doküman ekledin/sildin/amacı değişti** → `doc/INDEX.md` tablosunu güncelle.
- **Mimari yapı/katman/teknoloji değiştiyse** (yeni katman, state mgmt, DI, routing, persistence deseni vb.) → ilgili `doc/architecture/*.md` (backend/mobile_flutter/web_angular) ve gerekiyorsa `design_system.md` güncelle.
- **Ortak widget eklendi/değişti** (`mobile/lib/shared/widgets/`) → `doc/architecture/widgets.md` katalogundaki satırı + durumu (🟢/🟡/🔴) ve gerekiyorsa karmaşık widget'ın kendi md'sini güncelle.
- **Mimari açık kapandıysa** → `doc/modules/mimari_inceleme.md` ilgili maddeyi "✅ Düzeltildi" işaretle.
- Her güncellediğin dokümanın altındaki **"Güncelleme: YYYY-MM-DD"** tarihini o günkü tarihe çek.

Kural: **Kod gerçeği ile doküman çelişirse, dokümanı koda göre düzelt** (kodu doğruluk kaynağı kabul et) ve değişikliği bildir.

## Adlandırma
Görünen metin/başlık: **EğitimÜssü**. Dosya adı / klasör / kod tanımlayıcısı: **EgitimUssu** (Türkçe karaktersiz). `EgittimUssu` (çift t) yanlıştır.

## Git / platform notu
Bu repo Windows + MacBook'ta geliştirilir. Platforma özgü dosyaları (`.vscode/launch.json`, `tasks.json`, `mobile/.metadata`, `pubspec.lock` vb.) `main`'e commit'leme. Detay için oturum hafızasına bakılır.
