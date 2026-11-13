---
title: "Admin Rolü"
summary: "Platform yöneticisi; doğrulama/moderasyon/destek — adanmış panel yok, yetenekler planlanan"
tags: [rol, admin, moderasyon, planlanan]
status: "🔴"
authority: derived
updated: 2026-09-04
---

# 🛡️ Admin Rolü — Detaylı Tasarım Dokümanı

> **Rol:** `UserRole.Admin = 1` · **Faz: 1+** · **Durum: 🔴 Planlanan (özel admin arayüzü yok)**
>
> **Amaç:** Platform güvenini ve sağlığını korumak — öğretmen doğrulama, içerik/yorum/şikayet moderasyonu, destek.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`../modules/m13_reviews.md`](../modules/m13_reviews.md) · [`../modules/m18_feedback.md`](../modules/m18_feedback.md)
> **Güncelleme:** 2026-09-04

---

## 1. Rol Özeti
Admin, son kullanıcı değil **platform yöneticisidir**. Kod tarafında `UserRole.Admin = 1` mevcuttur ([`../modules/m01_identity.md`](../modules/m01_identity.md)) ancak henüz **adanmış bir admin arayüzü/paneli yoktur**. Yetkilendirmede admin genelde "her şeye erişebilen" üst roldür ("Admin VEYA sahibi" deseni): öğretmen profili okuma authorizer'ında ve M15 `SettingsAuthorizer`'da (`_currentUser.Roles.Contains("Admin")`) kodda uygulanmıştır.

## 2. Yetenekler
| Yetenek | İlgili modül | Durum |
|---------|--------------|-------|
| Öğretmen **doğrulama** (`IsVerified` → rozet) | [`m02_teachers`](../modules/m02_teachers.md) | 🟢 `PUT /api/teachers/profiles/{userId}/verification` — yalnız `Admin` rolü; öğretmen kendini doğrulayamaz (P06/M02-1, Y1 kapandı) |
| **Yorum moderasyonu** (şüpheli/olumsuz yorum) | [`m13_reviews`](../modules/m13_reviews.md) | 🔴 Planlanan |
| **Şikayet / kötüye kullanım** moderasyonu | [`m18_feedback`](../modules/m18_feedback.md) | 🔴 Planlanan |
| **Hata/geri bildirim** triyajı | [`m18_feedback`](../modules/m18_feedback.md) | 🔴 Planlanan |
| Kullanıcı durumu (askıya alma/kapatma) | [`m01_identity`](../modules/m01_identity.md) | `UserAccountStatus` mevcut, admin akışı planlanan |
| İçerik/ilan denetimi | [`m12_matching`](../modules/m12_matching.md) | 🔴 Planlanan |
| **Üyelik planı yönetimi** (fiyat / deneme günü / satışta mı) | [`m17_membership`](../modules/m17_membership.md) | 🟡 `POST /api/membership/plans` + `PUT /api/membership/plans/{planId}` — yalnız `Admin` (`AdminOnlyMembershipAuthorizer`); arayüz yok (P09 Task 3). Kod, rol ve periyot **değiştirilemez** (kararlı anahtar) |
| Kampanya yönetimi (kampanya kodu, indirim) | [`m17_membership`](../modules/m17_membership.md) | 🔴 Planlanan — kodda yalnız referans kampanyası var, o da kullanıcı tarafında |

## 3. İş Kuralları
- **Doğrulama yalnız admin:** Öğretmen kendini "doğrulanmış" yapamaz; `IsVerified` yalnızca `PUT /api/teachers/profiles/{userId}/verification` ucuyla ve `Admin` rolüyle değişir ✅ (P06/M02-1; mimari_inceleme **Y1** kapandı).
- **Olumsuz yorum gizlenemez:** Admin yalnızca kural ihlali olan yorumu kaldırır; öğretmen olumsuz yoruma yanıt verebilir ama gizleyemez (M13).
- **Fiyat operasyona aittir:** Plan yazma uçları yalnız Admin'e açıktır; kullanıcı yalnız **kendi** aboneliğini yönetir (hedef kullanıcı gövdeden değil oturumdan gelir). Üretimde plan kataloğu Admin ucundan girilir — tohumlama yalnız `Development`'ta koşar ve fiyatlar henüz **hipotezdir** (Q2).
- **Moderasyon kuyruğu:** M13 `ReviewFlag` + M16 mesaj şikayeti + M18 `AbuseReport` ortak bir moderasyon kuyruğunda toplanır; karar `ModerationAction` olarak ilgili modüllere yayılır.

## 4. Eksikler ve Yapılacaklar
1. Admin yetkisi için **fail-fast authorizer guard** (eksik authorizer'lar — bkz. mimari_inceleme **K3**).
2. ✅ ~~Öğretmen doğrulama akışı + ayrı endpoint (`Admin` rolü)~~ — P06/M02-1 ile eklendi.
3. Moderasyon paneli (yorum/şikayet/mesaj) — büyük olasılıkla **web (Angular)** tarafında.
4. Kullanıcı yönetimi (askıya alma/kapatma) admin akışı.
5. Üyelik/gelir paneli: plan CRUD uçları var ama **arayüzü yok**; kampanya yönetimi ve dönüşüm raporu (M14 ile) eksik.

## 5. İlişkili Dokümanlar
- [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`../modules/m13_reviews.md`](../modules/m13_reviews.md) · [`../modules/m18_feedback.md`](../modules/m18_feedback.md) · [`../modules/m17_membership.md`](../modules/m17_membership.md) · [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)

---

*Admin Rolü — Detaylı Tasarım | Güncelleme: 2026-09-04 (P09 Task 3 / M17-1: üyelik planı yönetimi yeteneği eklendi — `POST`/`PUT /api/membership/plans` yalnız Admin; arayüz hâlâ yok. Önceki not — 2026-08-19 (doküman temizliği: modül gerçeğiyle uzlaştırma — admin paneli/backend yok teyidi, M15 SettingsAuthorizer "Admin VEYA sahibi" deseni eklendi))*
