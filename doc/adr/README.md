# Mimari Karar Kayıtları (ADR)

> **ADR (Architecture Decision Record):** Önemli bir mimari kararı, *neden* alındığını ve *hangi alternatiflerin* elendiğini kalıcı olarak belgeleyen kısa kayıt. Amaç: kararın bağlamını geleceğe taşımak, "neden böyle yapmışız?" sorusunu tek bir yerden yanıtlamak.

## Kurallar
- Her ADR **değiştirilmez** (immutable). Bir karar değişirse yeni bir ADR yazılır ve eskisi `Geçersiz kılındı (Superseded by ADR-XXXX)` olarak işaretlenir.
- Dosya adı: `NNNN-kisa-baslik.md` (4 haneli sıra no).
- Yeni ADR eklendiğinde bu README tablosu ve [`doc/INDEX.md`](../INDEX.md) güncellenir (CLAUDE.md doküman bakım kuralı).
- Şablon: [`0000-template.md`](0000-template.md).

## Durum etiketleri
`Önerildi (Proposed)` · `Kabul edildi (Accepted)` · `Reddedildi (Rejected)` · `Geçersiz kılındı (Superseded)` · `Kullanımdan kalktı (Deprecated)`

## Kayıtlar

| ADR | Başlık | Durum | İlgili denetim bulgusu |
|-----|--------|-------|------------------------|
| [0001](0001-asenkron-mesajlasma-ve-outbox.md) | Asenkron mesajlaşma & transactional outbox stratejisi | Önerildi | K3, K5, Y1, M6 |
| [0002](0002-kaynak-tabanli-yetkilendirme.md) | Kaynak-tabanlı yetkilendirme & izin modeli | Önerildi | K1, K2 |
| [0003](0003-api-versiyonlama-ve-contract-first.md) | API versiyonlama & contract-first istemci üretimi | Önerildi | Mobil drift, M6 |
| [0004](0004-redis-kullanim-stratejisi.md) | Redis kullanım stratejisi (dağıtık cache & rate limiting) | Önerildi | Y4, ölü altyapı |
| [0005](0005-kvkk-pii-koruma-ve-audit.md) | KVKK/PII koruma & denetim (audit) stratejisi | Önerildi | Güvenlik/uyum |

> Bu beş ADR, 2026-06-30 kapsamlı denetiminin ([`doc/denetim/2026-06-30_kapsamli_kod_denetimi.md`](../denetim/2026-06-30_kapsamli_kod_denetimi.md)) stratejik önerilerinden türetilmiştir. Hepsi **taslak/önerildi** durumundadır; ekip kararıyla `Kabul edildi`ye çekilmelidir.
