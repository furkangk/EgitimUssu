# 🛡️ Admin Rolü — Detaylı Tasarım Dokümanı

> **Rol:** `UserRole.Admin = 1` · **Faz: 1+** · **Durum: 🔴 Planlanan (özel admin arayüzü yok)**
>
> **Amaç:** Platform güvenini ve sağlığını korumak — öğretmen doğrulama, içerik/yorum/şikayet moderasyonu, destek.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`../modules/m13_reviews.md`](../modules/m13_reviews.md) · [`../modules/m18_feedback.md`](../modules/m18_feedback.md)
> **Güncelleme:** 2026-08-19

---

## 1. Rol Özeti
Admin, son kullanıcı değil **platform yöneticisidir**. Kod tarafında `UserRole.Admin = 1` mevcuttur ([`../modules/m01_identity.md`](../modules/m01_identity.md)) ancak henüz **adanmış bir admin arayüzü/paneli yoktur**. Yetkilendirmede admin genelde "her şeye erişebilen" üst roldür ("Admin VEYA sahibi" deseni): öğretmen profili okuma authorizer'ında ve M15 `SettingsAuthorizer`'da (`_currentUser.Roles.Contains("Admin")`) kodda uygulanmıştır.

## 2. Yetenekler
| Yetenek | İlgili modül | Durum |
|---------|--------------|-------|
| Öğretmen **doğrulama** (`IsVerified` → rozet) | [`m02_teachers`](../modules/m02_teachers.md) | ⚠️ Yalnız admin değiştirebilmeli (güvenlik açığı Y1 — bkz. [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)) |
| **Yorum moderasyonu** (şüpheli/olumsuz yorum) | [`m13_reviews`](../modules/m13_reviews.md) | 🔴 Planlanan |
| **Şikayet / kötüye kullanım** moderasyonu | [`m18_feedback`](../modules/m18_feedback.md) | 🔴 Planlanan |
| **Hata/geri bildirim** triyajı | [`m18_feedback`](../modules/m18_feedback.md) | 🔴 Planlanan |
| Kullanıcı durumu (askıya alma/kapatma) | [`m01_identity`](../modules/m01_identity.md) | `UserAccountStatus` mevcut, admin akışı planlanan |
| İçerik/ilan denetimi | [`m12_matching`](../modules/m12_matching.md) | 🔴 Planlanan |

## 3. İş Kuralları
- **Doğrulama yalnız admin:** Öğretmen kendini "doğrulanmış" yapamaz; `IsVerified` yalnızca admin/doğrulama akışıyla değişmeli (bkz. mimari_inceleme **Y1**).
- **Olumsuz yorum gizlenemez:** Admin yalnızca kural ihlali olan yorumu kaldırır; öğretmen olumsuz yoruma yanıt verebilir ama gizleyemez (M13).
- **Moderasyon kuyruğu:** M13 `ReviewFlag` + M16 mesaj şikayeti + M18 `AbuseReport` ortak bir moderasyon kuyruğunda toplanır; karar `ModerationAction` olarak ilgili modüllere yayılır.

## 4. Eksikler ve Yapılacaklar
1. Admin yetkisi için **fail-fast authorizer guard** (eksik authorizer'lar — bkz. mimari_inceleme **K3**).
2. Öğretmen doğrulama akışı + ayrı endpoint (`Admin` rolü).
3. Moderasyon paneli (yorum/şikayet/mesaj) — büyük olasılıkla **web (Angular)** tarafında.
4. Kullanıcı yönetimi (askıya alma/kapatma) admin akışı.

## 5. İlişkili Dokümanlar
- [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`../modules/m13_reviews.md`](../modules/m13_reviews.md) · [`../modules/m18_feedback.md`](../modules/m18_feedback.md) · [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md)

---

*Admin Rolü — Detaylı Tasarım | Güncelleme: 2026-08-19 (doküman temizliği: modül gerçeğiyle uzlaştırma — admin paneli/backend yok teyidi, M15 SettingsAuthorizer "Admin VEYA sahibi" deseni eklendi)*
