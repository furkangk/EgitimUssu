# 🌐 Web Mimari — Angular + Tailwind CSS

> **Durum: 🔴 PLANLANAN (Faz 4-5).** Henüz kod yoktur (`web/` klasörü mevcut değil). Bu doküman, web başladığında
> izlenecek mimari yönü tanımlar; gerçekleşince koddan doğrulanmış hale güncellenecektir.
>
> **Otorite:** Görsel token'lar → [`design_system.md`](design_system.md). Backend sözleşmesi → [`backend.md`](backend.md)
> ve [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md). Kanonik değerler → [`../INDEX.md`](../INDEX.md) §0.
>
> **Güncelleme:** 2026-06-24

---

## 1. Rol ve Kapsam

Web, **ikincil platform**tur. Mobil (Flutter) günlük operasyonel kullanımı karşılar; web özellikle **büyük ekran**
gerektiren işler içindir:

- **Admin yönetimi** — doğrulama, moderasyon, içerik/sistem yönetimi.
- **Gelişmiş raporlama & analiz** (M14) — öğretmenler için tablet/masaüstü detaylı raporlar.
- **Eşleştirme & ilan** (M12) — öğretmen listeleme + detaylı filtreleme (web tarafında öncelikli).

> Web, mobildeki her ekranı tekrarlamaz; mobille **aynı backend API'yi** tüketir (bkz. [`backend.md`](backend.md)).

## 2. Teknoloji

| Konu | Seçim |
|------|-------|
| Framework | **Angular** |
| Stil | **Tailwind CSS** (utility-first) |
| Mimari | Feature-based + Clean Architecture katmanları (mobille aynı felsefe) |
| Tasarım | Atomic / CBD — [`design_system.md`](design_system.md) token'larını Tailwind config'e bağlar |
| API | Backend REST + JWT (mobille ortak sözleşme) |

## 3. Klasör Yapısı (önerilen)

```txt
src/app/
├── core/            # Guards, Interceptors (JWT), singleton servisler, API katmanı
├── shared/          # Reusable bileşenler (Tailwind tabanlı, Atomic), pipes, direktifler
└── features/        # Modül bazlı özellik setleri
    ├── admin/        # Sistem & içerik yönetimi, moderasyon
    ├── reporting/    # M14: gelişmiş raporlama & analiz (öğretmen)
    └── matching/     # M12: öğretmen listeleme & detaylı filtreleme
```

> Feature isimleri backend modülleriyle hizalanır; her feature kendi `data/domain/presentation` (veya Angular karşılığı
> service/model/component) ayrımını korur.

## 4. UI/UX Stratejisi (Tailwind)

- **Utility-first + breakpoint'ler:** `sm`/`md`/`lg` ile tablet ve masaüstü raporlama düzenleri.
- **Tutarlılık:** Ortak bileşen stilleri `@layer components` altında; [`design_system.md`](design_system.md) renk/tipografi/
  spacing token'ları `tailwind.config` `theme.extend`'e taşınır (ana renk `#082B4F`).
- **Karanlık mod:** Tailwind native dark mode — özellikle öğrenci çalışma/analiz görünümleri için.
- **Erişilebilirlik & responsive:** [`design_system.md`](design_system.md) §1 ilkeleriyle aynı (renk+metin, min tıklama alanı, oranlı grafik).

## 5. Güvenlik

- JWT, HTTP **interceptor** ile `Authorization: Bearer` olarak eklenir; `401`'de oturum yenileme/çıkış.
- Rol bazlı route **guard**'ları (admin alanları yalnızca admin).
- Backend rate limiting ve ProblemDetails hata sözleşmesi web tarafında da ele alınır (bkz. [`backend.md`](backend.md) §8-9).

## 6. Faz Planı

| Faz | İş |
|-----|-----|
| **4** | Web teknoloji kararının netleşmesi, route/component iskeleti, API kontratlarının web uyumlu dokümantasyonu, Matching (M12) web ekranları |
| **5** | Admin + gelişmiş raporlama (M14); eşleştirmenin her iki platformda aktifleşmesi |

Tam yol haritası → [`../yol_haritasi.md`](../yol_haritasi.md); backlog → [`../jira_backlog_from_modules.csv`](../jira_backlog_from_modules.csv) (`faz-4` etiketleri).

---

> İlgili: sistem geneli → [`00_genel_bakis.md`](00_genel_bakis.md) · tasarım sistemi → [`design_system.md`](design_system.md) ·
> backend → [`backend.md`](backend.md)

*Web Mimari (Angular) — Planlanan | Güncelleme: 2026-06-24*
