---
title: "Üyelik Ekranı (Mevcut plan, iptal, referans)"
summary: "M17 üyelik ekranı: etkin seviye ve dönem sonu, abonelik iptali, referans kodu paylaşımı ve kod kullanma; premium kararının mobil tek kaynağı MembershipCubit"
tags: [sayfa, membership, uyelik, premium, referans, m17]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/membership/presentation/pages/membership_page.dart
  - mobile/lib/features/membership/presentation/cubit/membership_cubit.dart
  - mobile/lib/features/membership/domain/membership_contracts.dart
  - mobile/lib/features/membership/data/repositories/membership_repository_impl.dart
  - mobile/lib/features/membership/presentation/widgets/membership_widgets.dart
updated: 2026-11-15
---

# Üyelik

> **Route:** `/membership` · **Feature:** `membership` · **Veri:** 🟢 `/api/membership`
> **Giriş:** `/more` → "Uyelik" satırı (P05'te "Yakında" pasifti, P09 Task 6'da aktifleşti — D-15)
> **State:** `MembershipCubit` — **uygulama kökünde** (`app/app.dart`) tek örnek olarak sağlanır
> **Güncelleme:** 2026-11-15

---

## 1. Neden kökte tek cubit?

K-09 "entitlement tek noktadan" kararının mobil karşılığı: premium kararının **tek** kaynağı
`GET /api/membership/me` yanıtıdır. `PremiumLock` (bkz. [`../architecture/widgets.md`](../architecture/widgets.md))
ve `/more` rozeti bu cubit'i okur; hiçbir ekran kendi tier alanını taşımaz.

Cubit oturum değiştiğinde `AuthCubit` dinleyicisinden tazelenir
(`membershipRoleFor(session.roles)` ile rolün plan kataloğu da çekilir).

## 2. İçerik

| Bölüm | Kaynak | Not |
|-------|--------|-----|
| Durum kartı | `GET /me` | Seviye rozeti, başlık (`Ücretsiz` / `Deneme sürüyor` / `Premium aktif` / `İptal edildi`), dönem sonu, plan kodu, öğrenci limiti |
| Eylem | — | Free → "Premium'a geç" (`/membership/paywall`); Premium → "Planları gör" + "Aboneliği iptal et" |
| Referans kodun | `GET /referral-code` | Kod, kopyala düğmesi (`Clipboard`), ödül ayı, kaç kişi kullandı |
| Kod kullan | `POST /referral/redeem` | Girilen kod **büyük harfe** çevrilip gönderilir; boş kod sunucuya hiç gitmez |

## 3. Kritik davranışlar (sunucu kuralının mobil karşılığı)

- **İptal ≠ anında kapanma.** Sunucu `Cancelled` dönse de `tier` dönem sonuna kadar `Premium`
  kalır. Ekran kilit göstermez; "Erişimin \<tarih\> tarihine kadar sürüyor" der.
- **İptal düğmesi yalnız yürüyen abonelikte** çıkar. Aboneliği olmayan ya da zaten iptal edilmiş
  kullanıcıya gösterilse sunucudan `404 membership.subscription_not_found` alınırdı.
- **Fail-safe.** `/me` okunamazsa kullanıcı Free kabul edilir (premium açılmaz) **ama** hata
  mesajı gösterilir — sessizce yutulmaz.
- Referans ödülü hesap başına **bir kez** ve kendi kodu kullanılamaz; ikisi de sunucuda
  zorunlu, mobil yalnız hata mesajını gösterir.

## 4. Henüz olmayan

| Eksik | Neden | Nerede gelecek |
|-------|-------|----------------|
| Otomatik yenileme anahtarı | `UserSubscription.AutoRenew` domainde var ama ne `GET /me` döner ne de değiştiren bir uç vardır | P09 Task 4 |
| Fatura geçmişi | Ödeme kaydı yok | P09 Task 4 |

---

*Sayfa dokümanı | Güncelleme: 2026-11-15*

## P20 T1 — ortak plan yüzeyleri

Mevcut üyelik/iptal/referans davranışları korunur. Paywall’a gidildiğinde işlevsiz satın alma düğmesi ve henüz sunulmayan Premium vaatleri gösterilmez; yalnız gerçek plan kataloğu, deneme ve referans yer alır. Ortak üyelik rozeti/plan kartı sabit metinleri tr/en ARB’den gelir.
