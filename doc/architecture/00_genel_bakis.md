---
title: "Mimari — Genel Bakış"
summary: "doc/architecture/ klasörünün indeks sayfası: sistem tipi, platformlar, aktörler, katmanlar, veri akışı, ölçeklenebilirlik ve faz hizalaması"
tags: [mimari, genel-bakis, indeks]
authority: derived
updated: 2026-08-20
---

# 🏗️ Mimari — Genel Bakış (Architecture Overview)

> **Bu klasör (`doc/architecture/`), sistemin mimari dokümantasyonudur** — platforma göre bölünmüştür ve
> **koddan doğrulanmış** gerçeği yansıtır. Eski tek-parça `ai_ready_architecture.md` ve `design.md` bu klasöre
> bölündü; `tutormatch_flutter_ui_design.md` içeriği [`mobile_flutter.md`](mobile_flutter.md) altında toplandı.
>
> **Çelişki halinde otorite:** Modül/endpoint gerçeği için [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md);
> kanonik değerler (ad, .NET sürümü, ana renk) için [`../INDEX.md`](../INDEX.md) §0.
>
> **Güncelleme:** 2026-08-20

---

## Bu klasördeki dokümanlar

| Doküman | Kapsam | Ne zaman aç |
|---------|--------|-------------|
| **`00_genel_bakis.md`** (bu dosya) | Sistem geneli: platformlar, aktörler, katmanlar, veri akışı, event sistemi, ölçeklenebilirlik, fazlar | Önce buraya bak |
| [`backend.md`](backend.md) | .NET 9 modüler monolit: çözüm yapısı, Shared/Kernel, modül katmanları, CQRS, Outbox, persistence, Redis, JWT | Backend geliştirirken |
| [`mobile_flutter.md`](mobile_flutter.md) | Flutter mimari + tasarım uygulaması + 20 ekran görsel rehberi (eski `tutormatch`) | Mobil geliştirirken |
| [`web_angular.md`](web_angular.md) | Angular web (planlanan — Faz 4-5) | Web planlaması |
| [`design_system.md`](design_system.md) | Platformlar-arası ortak görsel sistem: renk/tipografi/spacing token'ları + Atomic/CBD ilkeleri | UI token kararı |
| [`widgets.md`](widgets.md) | Ortak widget kataloğu: her paylaşılan bileşenin API + kural + durumu (🟢/🟡/🔴) | Ekran/widget yaparken |
| [`ux_rules.md`](ux_rules.md) | UX kuralları: navigasyon, form davranışı, boş/yükleniyor/hata durumları, geri bildirim | UX/navigasyon kararı |
| [`animations.md`](animations.md) | Animasyon desenleri: süre/eğri token'ları, geçiş/hero kuralları, performans | Animasyon eklerken |
| [`accessibility.md`](accessibility.md) | Erişilebilirlik: kontrast, dokunma hedefi, semantik/etiket, ölçeklenebilir tipografi | A11y kararı |
| [`anti_patterns.md`](anti_patterns.md) | Kaçınılacak desenler: mimari/UI/state kokuları ve doğru alternatifleri | Refactor/review'da |
| [`figma_references.md`](figma_references.md) | Figma referansları: tasarım kaynakları ↔ ekran/komponent eşlemesi | Tasarım eşlerken |

---

## 1. Sistem Tipi

**Modüler Monolit** — tek deploy edilen .NET 9 host (`API.Host`) içinde, her biri kendi domain'ine ve veri şemasına
sahip bağımsız modüller. Modüller arası bağ gevşektir (domain event + Outbox), bu yüzden ileride bir modül
**mikroservise** çıkarılabilir (bkz. §7).

> Neden monolit? Erken dönemde geliştirme hızı, tek transaction sınırı ve operasyonel basitlik için. Modül
> sınırları net tutularak mikroservis evrimi açık bırakılır.

## 2. Platformlar

| Platform | Rol | Teknoloji | Durum |
|----------|-----|-----------|-------|
| **Mobil** | Birincil — günlük operasyonel kullanım (öğretmen/öğrenci/veli) | Flutter | 🟢 Aktif (öğretmen odaklı) |
| **Backend API** | Çekirdek — tüm iş mantığı ve veri | .NET 9 modüler monolit | 🟢 Aktif (M01–M07 🟢) |
| **Web** | İkincil — admin, gelişmiş raporlama, büyük ekran analizi | Angular + Tailwind | 🔴 Planlanan (Faz 4-5) |

Detaylar: [`mobile_flutter.md`](mobile_flutter.md) · [`backend.md`](backend.md) · [`web_angular.md`](web_angular.md).

## 3. Aktörler (Roller)

- 👨‍🏫 **Öğretmen** — takvim-merkezli ders/öğrenci/ödeme yönetimi (Faz 1, 🟢)
- 🎓 **Öğrenci** — bireysel çalışma + gelişim (Faz 2, 🟡)
- 👪 **Veli** — gelişim/ödeme takibi (Faz 2-3, 🔴)
- 🛡️ **Admin** — doğrulama, moderasyon, destek

> Rol yetenekleri, kullanıcı yolculukları ve rol-özel kurallar → [`../roles/`](../roles/00_roller_genel_bakis.md).
> Bir rol genelde birden çok backend modülünü kullanır; teknik karşılıkları → [`../modules/`](../modules/00_genel_bakis.md).

## 4. Yüksek Seviye Mimari

```
┌─────────────────────────── İstemci Katmanı ───────────────────────────┐
│   Mobil (Flutter)  ── birincil          Web (Angular) ── planlanan      │
│   bloc/Cubit · go_router · dio · get_it  Tailwind · feature modülleri   │
└────────────────────────────────┬──────────────────────────────────────┘
                                  │ HTTPS / REST + JWT
┌────────────────────────────────▼──────────────────────────────────────┐
│                       Backend (.NET 9) — API.Host                       │
│  ┌──────────────────────────────────────────────────────────────────┐ │
│  │ Modüller (src/Modules/<Ad>)                                        │ │
│  │  Identity · Teachers · Students · Scheduling · LessonSessions ·    │ │
│  │  Assignments · Payments · Study · Parents · ProgressTracking ·     │ │
│  │  Notifications · Matching · Reviews · Reporting · Settings · …      │ │
│  │  her modül: API / Application / Domain / Infrastructure            │ │
│  └──────────────────────────────────────────────────────────────────┘ │
│  ┌──────────────────────────────────────────────────────────────────┐ │
│  │ Shared (src/Shared): Kernel · Application · Contracts · Infra      │ │
│  └──────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────┬──────────────────────────────────────┘
                                  │
┌────────────────────────────────▼──────────────────────────────────────┐
│  Altyapı:  PostgreSQL (modül başına ayrı şema)  ·  Redis (cache)        │
│            Outbox tablosu (event yayını)  ·  (Gelecek: mesaj brokeri)   │
└─────────────────────────────────────────────────────────────────────────┘
```

## 5. Katmanlar (Özet)

| Katman | Sorumluluk | Detay |
|--------|------------|-------|
| **İstemci** | Sunum, durum yönetimi, API tüketimi | [`mobile_flutter.md`](mobile_flutter.md) / [`web_angular.md`](web_angular.md) |
| **API.Host** | HTTP host, modül kaydı, middleware, auth, health | [`backend.md`](backend.md) §2 |
| **Modül (API/App/Domain/Infra)** | İş mantığı, CQRS, domain modeli, persistence | [`backend.md`](backend.md) §3-4 |
| **Shared** | Kernel (BaseEntity, Result), CQRS arayüzleri, Outbox, Redis, auth | [`backend.md`](backend.md) §5 |
| **Veri** | PostgreSQL (şema/modül) + Redis | [`backend.md`](backend.md) §6 |

## 6. Çapraz-Kesit İlkeler

- **Modül sahipliği:** Her modül kendi verisinin tek sahibidir. **Modüller arası doğrudan DB erişimi yoktur.**
- **İletişim:** Modüller arası iletişim, doğrudan çağrı yerine **domain event → integration event (Outbox)** ile yapılır.
- **Veri izolasyonu:** Her modülün ayrı PostgreSQL **şeması** ve `DbContext`'i vardır (bkz. [`backend.md`](backend.md) §6).
- **CQRS:** Yazma (Command) ve okuma (Query) ayrı handler'larla işlenir; sonuçlar `Result` deseni ile döner.
- **Tutarlılık:** Aynı transaction içinde domain değişikliği + Outbox kaydı yazılır (transactional outbox).

### Standart istek akışı

```
1. İstemci → API.Host (REST + JWT)
2. Endpoint (ModuleDefinition) → Command/Query oluşturur
3. Handler (Application) → domain mantığını çalıştırır
4. Domain → AggregateRoot kuralları + DomainEvent üretir
5. Repository → DbContext ile kalıcılaştırır (modül şeması)
6. DomainEvent → IntegrationEvent map → Outbox tablosuna yazılır (aynı tx)
7. Outbox işleyici → event'i yayınlar (diğer modüller/bildirim tüketir)
8. Sonuç → Result<T> olarak istemciye döner
```

## 7. Ölçeklenebilirlik Yol Haritası

| Aşama | Yaklaşım |
|-------|----------|
| **Bugün** | Modüler monolit; tek host, tek deploy, modül başına şema |
| **Sonraki** | Yük artışında okuma replikaları + Redis cache derinleştirme |
| **Evrim** | Sınırı net modüller (Matching, Notifications, Payments) ayrı **mikroservise** çıkarılabilir; Outbox zaten event tabanlı entegrasyonu hazır tutar |

## 8. Faz Hizalaması

Mimari, ürünün 6 fazlı yol haritasıyla ilerler. Tam plan ve bağımlılıklar → [`../yol_haritasi.md`](../yol_haritasi.md),
ürün gereksinimleri → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) §6.

| Faz | Odak | Mimari etkisi |
|-----|------|---------------|
| **0** | Altyapı + Auth | Shared/Kernel, modül kayıt mekanizması, Identity |
| **1** | Öğretmen MVP | Teachers/Scheduling/LessonSessions/Assignments/Payments + Flutter öğretmen akışı |
| **2** | Öğrenci bireysel | Study/ProgressTracking + Flutter öğrenci akışı |
| **3** | Analiz & bildirim | Reporting/Notifications (gerçek push) |
| **4** | Eşleştirme + Web | Matching + Angular web başlangıcı |
| **5** | Para kazanma | Membership/paywall/reklam + premium |

## 9. AI Kullanım Rehberi

Yeni özellik/koda başlarken:

- **Önce modüle eşle:** Her iş mantığı bir backend modülüne aittir (bkz. [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)).
- **Katman ayrımına uy:** Backend'de API → Application (CQRS) → Domain → Infrastructure; mobilde data → domain → presentation.
- **Gerçeği esas al:** Endpoint/alan adları için idealize taslakları değil, **koddan çıkarılmış envanteri** kullan
  ([`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) §4).
- **Doküman bakımı:** Kod/ürün değişince ilgili mimari + modül + sayfa dokümanını **aynı turda** güncelle (bkz. kökteki `CLAUDE.md`).

---

*Mimari Genel Bakış | Güncelleme: 2026-08-20*
