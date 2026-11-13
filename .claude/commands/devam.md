---
description: Sağlamlaştırma programında kaldığın yerden devam et — ILERLEME.md'yi oku, sıradaki tek görevi TDD ile yürüt
---

Sağlamlaştırma programında **kaldığın yerden devam et**. Argüman: `$ARGUMENTS`
(boş = sıradaki görev · `P03` = o planın sıradaki görevi · `P06 Task 4` = belirli görev · `plan` = aktif planın tamamı).

Bu komut, `/clear` sonrası sıfır bağlamla çalışacak şekilde tasarlandı: gereken her şey dosyalardan okunur.

## 1. Durumu yükle (konuşma geçmişine güvenme)

1. Oku: `docs/superpowers/ILERLEME.md` → aktif plan, sıradaki görev, dal, öğrenilenler, bloke kararlar.
2. Oku: aktif planın **tamamı** (`docs/superpowers/plans/2026-09-02-NN-*.md`) — özellikle `## Global Constraints` ve yürütülecek görevin `**Files:**` + `**Interfaces:**` blokları.
3. Oku: `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` §1 (çıta), §2 (kararlar K-xx), §4 (ortak sözleşmeler).
4. Çalıştır: `git status --short && git log --oneline -3 && git branch --show-current`
   → ILERLEME.md'deki "Son commit" ve "Dal" ile tutarsızsa **dur ve kullanıcıya sor**; kendi başına düzeltme.

## 2. Başlamadan önce doğrula

5. Görev **bloke mi?** ILERLEME.md "Bloke Eden Kararlar" tablosuna bak. Bloke ise kullanıcıya sor, sıradaki bloke olmayan göreve geçmeyi öner.
6. Dal yoksa aç: `git checkout -b feat/pNN-<kisa-ad>` (plan başına bir dal; plan ortasındaysan mevcut dalda kal).
7. Temiz zemin kontrolü: `dotnet test EgitimUssu.slnx --nologo` → **başarısız 0** olmalı. Değilse önce onu düzelt (P01 dışındaki her plan yeşil zeminde başlar).
   ⚠️ Çıktıyı `| tail` ile kırpma; "Başarısız: N" özet satırını oku.

## 3. Görevi yürüt

8. Görevin adımlarını **sırayla ve harfi harfine** uygula. TDD sırası zorunlu: önce başarısız test → kırmızı gör → minimum implementasyon → yeşil gör.
9. Bir adım plandan sapmayı gerektiriyorsa (plan yanlış/eksik): **planı düzelt**, sapmayı ILERLEME.md "Öğrenilenler"e yaz, sonra devam et. Sessizce farklı bir şey yapma.
10. Varsayılan: **tek görev** yürüt. `$ARGUMENTS` içinde `plan` varsa planın tüm görevlerini sırayla yürüt (her görev sonunda commit).

## 4. Bitir

11. `/gorev-bitir` akışını uygula (bkz. `.claude/commands/gorev-bitir.md`): doğrulama → checkbox → doküman → commit → ILERLEME.md güncelle.
12. Kullanıcıya **kısa** özet ver:
    - Ne yapıldı (2-3 madde), hangi commit
    - Test sonucu (gerçek sayılarla: "156 birim / 17 integration, başarısız 0")
    - Sıradaki görev ne
    - Son satır: **"Bağlamı temizleyebilirsin: `/clear` → sonra `/devam`"**

## Kurallar

- **Kapsam kilidi:** Yalnız o görevin `**Files:**` listesindeki dosyalara dokun. Başka bir eksik görürsen ILERLEME.md "Öğrenilenler"e not düş, o işi yapma.
- **Doğrulama olmadan "bitti" deme:** Her "yeşil" iddiası gerçekten koşturulmuş komut çıktısına dayanmalı.
- **Doküman aynı turda:** Kök `CLAUDE.md` kuralı — kod değiştiyse ilgili `doc/modules/mNN_*.md` + `doc/modules/00_genel_bakis.md` aynı görevde güncellenir.
- **Türkçe yanıt ver.**
