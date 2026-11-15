---
title: "Paywall Ekranı (Rol bazlı Free/Premium karşılaştırması + planlar)"
summary: "M17 paywall: rolün plan kataloğu, ilk-ay-ücretsiz denemesi, referans kampanyası ve PRD §9 karşılaştırma tablosu; satın alma girişi P28 gerçek akışına kadar gizli"
tags: [sayfa, membership, paywall, premium, fiyatlandirma, m17]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/membership/presentation/pages/paywall_page.dart
  - mobile/lib/features/membership/domain/premium_features.dart
  - mobile/lib/features/membership/presentation/widgets/membership_widgets.dart
  - mobile/lib/shared/widgets/premium_lock.dart
updated: 2026-11-15
---

# Paywall

> **Route:** `/membership/paywall` · **Feature:** `membership` · **Veri:** 🟡 (katalog + deneme gerçek, **satın alma yok**)
> **Giriş:** `/membership` durum kartı · her `PremiumLockCard`'ın "Premium'a geç" düğmesi
> **Güncelleme:** 2026-11-15

---

## 1. İçerik

| Bölüm | Kaynak | Not |
|-------|--------|-----|
| Başlık kartı | `GET /me` | Seviye rozeti + role göre değişen vaat metni |
| Karşılaştırma tablosu | `premium_features.dart` (statik ürün metni) | Aşağıya bak — **yalnız uygulanan farklar** |
| Planlar | `GET /plans?role=` (**anonim** uç) | Aylık önce; fiyat, periyot, "İlk ay ücretsiz" rozeti |
| Kampanyalar | statik | İlk ay ücretsiz + arkadaşını getir (ikisi de bugün çalışıyor) |

Rol, oturumun rollerinden `membershipRoleFor()` ile çözülür ve **`AppRouter`'ın rol
yönlendirmesiyle birebir aynıdır** (veli > yalnız-öğrenci > öğretmen): kullanıcı hangi paneli
görüyorsa o rolün planlarını görür.

## 2. ⚠️ Tablo neden PRD §9'un tamamı değil?

Kök `CLAUDE.md` kuralı: **kod gerçeği ile doküman çelişirse kod esastır.** Sunucunun bugün
gerçekten uyguladığı kapılar şunlardır:

| Hak | Nerede uygulanıyor | Yanıt |
|-----|--------------------|-------|
| `Entitlement.MaxStudents` | `Students/Application/StudentProfileFeatures.cs` | `409 students.free_limit_reached` |
| `Entitlement.PdfReports` | `Reporting/Application/StudentReportFeatures.cs` | `402 reporting.premium_required` |
| `PremiumFeature.TargetTracking` | `Study/Application/StudyProgressFeatures.cs` | `402 study.premium_required` |
| `MembershipGate.HistoryWindowDays` | Study geçmiş/test/net sorguları | Sessiz kısma (Free = son 30 gün) |
| `Entitlement.RichNotifications` | `Parents/Infrastructure/ParentNotificationDirectory.cs` | Sunucu tarafı filtre |

PRD §9'un kalan satırları (reklamsız kullanım, gelir/boş zaman analizi, streak, motivasyon,
profil öne çıkarma) **kodda kısıtlanmıyor**. Bunları tabloda "Premium'a özel" diye göstermek,
kullanıcıya zaten bedava kullandığı şeyi satmak olurdu; P20 T1 ile bu vaatlerin ayrı gelecek özellikler bölümü de kaldırılmıştır. Yalnız bugün çalışan haklar listelenir.

İki yerde PRD'den bilinçli sapma var, **ikisi de kod gerçeği yönünde**:

- PRD öğrenci için "Geçmiş çalışma kayıtları ❌ Free" diyor; kodda Free **son 30 günü** görür.
- PRD öğretmen için "maks. 5–10 öğrenci" diyor; kodda Free limiti `Entitlement.MaxStudents`'tan
  (bugün 5) canlı okunur.

## 3. Satın alma neden görünmüyor?

`POST /api/membership/checkout` ucu yoktur. P20 T1 ile işlevsiz "Satın al (yakında)" düğmesi tamamen gizlenmiştir. Mobil ödeme P28 gerçek satın alma akışına aittir; kullanıcı kararıyla şu an uygulanmaz. Katalog, ücretsiz deneme ve referans akışları korunur.

Bugün Premium olmanın **çalışan** iki yolu var ve ikisi de bu ekranda: **ücretsiz deneme**
(`POST /subscriptions/start-trial`, kullanıcı başına bir kez) ve **referans ödülü**
([`membership.md`](membership.md)).

---

*Sayfa dokümanı | Güncelleme: 2026-11-15*

Paywall sayfasının ve ortak plan kartlarının sabit metinleri tr/en ARB’dedir (P20 T1).
