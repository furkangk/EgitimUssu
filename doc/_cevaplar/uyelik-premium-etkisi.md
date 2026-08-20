---
title: "Üyelik/premium hangi modülleri ve rolleri etkiliyor?"
summary: "Premium çekirdeği bugün yalnız M08 Study'de (MembershipTier + MembershipGate); tam gelir modülü M17 🔴 planlanan; entitlement açılınca M03/M14/M16 + öğretmen/öğrenci/veli premium setleri bağlanır"
tags: [cevap, premium, uyelik]
authority: derived
question: "Üyelik/premium hangi modülleri ve rolleri etkiliyor?"
updated: 2026-08-20
---

# 💬 Üyelik/premium hangi modülleri ve rolleri etkiliyor?

> **2026-08-20 itibarıyla** bilgi tabanı (doc/ + kaynaklar/) üzerinden türetilmiş cevap (`authority: derived`).
> Kaynak dokümanların o günkü durumunu yansıtır; kod değişirse eskiyebilir.

## Kısa cevap

Üyelik/premium **kavramsal olarak tüm rolleri ve pek çok modülü** etkiler, ama **bugün kodda yalnızca tek bir çekirdek** vardır:

- **Tam gelir/üyelik modülü M17 (Membership) 🔴 planlanan** — `src/Modules/Membership/` kodda **hiç yok** (şema, DbContext, endpoint, ödeme entegrasyonu yok). Tüm domain (`SubscriptionPlan`, `UserSubscription`, `Campaign`, `ReferralCode`, `AdPlacement`, entitlement projeksiyonu) **önerilen**.
- **Bugün gerçekten zorlanan tek premium kapısı M08 Study'dedir** (Ö-D, 2026-07-19): öğrencinin `MembershipTier` (`Free`/`Premium`) M03 `StudentProfile`'da hafifçe tutulur, Study bunu `Shared/Contracts` `IMembershipDirectory` üzerinden okur ve `MembershipGate` ile kapıları uygular.

Yani premium **mimari niyet olarak yaygın**, **kod gerçeği olarak M08'e sınırlı**.

## Modüller

| Modül | Premium etkisi | Durum |
|-------|----------------|-------|
| **M08 Study** | **Çalışan çekirdek.** Free: çalışma/deneme geçmişi + net-trend **son 30 güne** kısılı (`MembershipGate.ClampFrom`); hedef net/puan (`TargetNet`/`TargetScore`) Free'de ⛔ → **HTTP 402** `study.premium_required`. Premium: sınırsız geçmiş + derinlik. Kronometre/streak/rozet/haftalık özet her ikisinde de açık ("Free geniş, Premium yalnız derinlik"). | 🟢 kodda |
| **M17 Membership** | **Gelir çekirdeği (planlanan).** Plan/abonelik/kampanya/referans/reklam + entitlement projeksiyonu (`UserSubscriptionChangedIntegrationEvent`). Kısıtlama tek noktadan buradan yayılacak. | 🔴 kodda yok |
| **M03 Students** | `MembershipTier` bugün burada saklanır (`StudentProfile`). Öğretmenin **Free = en fazla 5 aktif öğrenci bağı**; Premium sınırsız (entitlement M17'ye taşınacak). | 🟡/planlanan kapı |
| **M14 Reporting** | Premium analiz/PDF rapor çıktılarının hedefi (aylık özet, performans analizi). Modül 🔴 iskelet; anlamlı rapor için modüller-arası okuma (O5) + M17 entitlement gerekir. | 🔴 iskelet |
| **M16 Messaging** | Mesaj eki/hız limitleri entitlement'a bağlanacak (free kısıt, premium serbest). | planlanan |
| **M12 Matching** | Öğretmen premium: **profil öne çıkarma** / sıralama sinyali (ücretli üyelik ilanı öne çıkarır). | planlanan |
| **M11 Notifications** | Premium hatırlatma kanalları (WhatsApp/SMS). | planlanan |
| **M18 Feedback / M15 Settings** | Premium istismarı (şikayet) ve tercih/gizlilik bağlamı. | planlanan |

> Reklam politikası (`AdPlacement`): **premium kullanıcı reklam görmez** (`ShownToTiers = Free`); gösterim mobil SDK'da, backend yalnız yerleşim konfigürasyonu verir — hepsi M17 kapsamında (planlanan).

## Roller

Üyelik (free/premium) **her üç rolde de** vardır ([`roles/00_roller_genel_bakis`](../roles/00_roller_genel_bakis.md) §4.5): ücretsiz → reklam + limit; ücretli → reklamsız + sınırsız + ekstra. Rol bazlı premium setleri (PRD §9, M17 §4.7):

- **Öğretmen:** aylık kazanç/gelir analizi, geciken ödeme listesi, otomatik ödeme hesaplama, **PDF öğrenci raporu**, performans/boş zaman analizi, **profil öne çıkarma**, **sınırsız öğrenci** (Free = 5 bağ), WhatsApp/SMS hatırlatma.
- **Öğrenci:** sınırsız çalışma/deneme geçmişi, haftalık/aylık analiz, hedef net/puan takibi, streak dondurma, konu zayıflık analizi, gelişmiş sayaç. *(Bugün M08'de zorlanan: geçmiş 30 gün + hedef net/puan 402.)*
- **Veli:** detaylı gelişim grafikleri, haftalık rapor, çalışma süresi geçmişi, bildirimler.
- **Admin:** plan/kampanya tanımı + gelir/dönüşüm raporu (M17 + M14). Admin → Premium (tam erişim) sayılır.

## Kaynaklar

- [`../modules/m17_membership.md`](../modules/m17_membership.md) — üyelik/gelir modülü (🔴 planlanan; domain, entitlement, reklam, kampanya/referans)
- [`../modules/m08_study.md`](../modules/m08_study.md) §4.7 — bugün çalışan Free/Premium kapıları (`MembershipTier` + `MembershipGate`, 402)
- [`../modules/m14_reporting.md`](../modules/m14_reporting.md) — premium analiz/PDF rapor hedefi (🔴 iskelet)
- [`../roles/00_roller_genel_bakis.md`](../roles/00_roller_genel_bakis.md) §4.5 — üyelik tüm rollerde
- [`../roles/ogretmen.md`](../roles/ogretmen.md), [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md) — rol bazlı premium yetenekler
- [`../kaynaklar/proje-vizyonu-promp.md`](../kaynaklar/proje-vizyonu-promp.md) — premium/ücretsiz gelir modeli + büyüme kampanyaları (vizyon)

> **Kanonik gerçek uyarısı:** M17 🔴 **planlanan**dır (kodda yok); premium çekirdeği bugün yalnız M08'de. Bu cevap kanonik gerçeği (INDEX §0) ezmez; türevdir.

*Güncelleme: 2026-08-20*
