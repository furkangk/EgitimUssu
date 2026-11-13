---
description: Görev kapanış ritüeli — doğrula, plan checkbox'larını işaretle, dokümanı güncelle, commit et, ILERLEME.md'yi tazele (sonra /clear)
---

Bir görevi **kapat** ve oturumu temizlenmeye hazır hale getir. Argüman: `$ARGUMENTS` (boş = aktif görev).

Amaç: `/clear` sonrası hiçbir bilgi kaybolmasın. Konuşmada kalan her şey silinecek — **kalması gereken her şey dosyaya yazılmalı**.

## 1. Doğrula (iddia değil, kanıt)

1. Backend: `dotnet test EgitimUssu.slnx --nologo`
   → Özet satırlarını oku (`Başarısız: N`). `| tail` ile kırpma; çıkış kodu pipe'ta maskelenir.
2. Mobil değiştiyse: `cd mobile && flutter test && flutter analyze`
3. Docker varsa tam doğrulama: `./scripts/test-with-docker.sh` (atlanan test 0 olmalı).
4. **Herhangi biri kırmızıysa kapanış YAPMA.** Kullanıcıya durumu söyle, düzeltmeyi öner.

## 2. İzleri bırak

5. **Plan checkbox'ları:** Yürütülen görevin `- [ ] **Step N:**` satırlarını `- [x]` yap. Görev tamamen bittiyse başlığına ` ✅` ekle.
6. **Doküman (CLAUDE.md kuralı):** Kod/davranış değiştiyse aynı turda:
   - ilgili `doc/modules/mNN_*.md` (+ endpoint envanteri, durum, "Güncelleme: YYYY-MM-DD")
   - gerekiyorsa `doc/modules/00_genel_bakis.md`, `doc/modules/veri_modeli.md`, `doc/INDEX.md`, `doc/pages/*`, `doc/roles/*`
   - `doc/denetim/2026-09-02_eksik_analizi.md` → kapanan madde ID'lerine `✅ (PNN)` işareti
7. **Commit:** Planın o görevindeki commit komutunu kullan (Conventional Commits). Tek görev = tek commit.

## 3. ILERLEME.md'yi güncelle (en kritik adım)

`docs/superpowers/ILERLEME.md` içinde:

8. **ŞU AN** tablosu: aktif plan, **sıradaki görev**, dal, durum, son commit hash'i (`git log --oneline -1`).
9. **Plan Durumu** tablosu: ilgili satırın `Görev` sayacını ilerlet (`3/7`), durumunu güncelle (🔄/✅).
10. **Tamamlanan Görevler** tablosuna satır ekle: `| YYYY-MM-DD | PNN Task N — <kısa ad> | <commit> | <test özeti> |`
11. **Öğrenilenler:** Bu görevde ortaya çıkan, bir sonraki oturumun bilmesi gereken şeyler (plandan sapma, sürpriz davranış, geçici çözüm). Kalıcı kural olacaksa `CLAUDE.md`'ye, mimari karar ise master tasarım §2'ye taşı ve buraya sadece atıf bırak.
12. **Bloke Eden Kararlar:** Cevaplanan soru varsa ✅ işaretle; yeni blokaj çıktıysa satır ekle.
13. Alt satırdaki `Son güncelleme:` tarihini bugüne çek.

## 4. Kullanıcıya rapor

14. Kısa özet: ne yapıldı · commit · **gerçek test sayıları** · sıradaki görev.
15. Kapanış cümlesi: **"Bağlamı temizleyebilirsin: `/clear` → yeni oturumda `/devam`"**

## Kural

Bu ritüel **atlanamaz**. `/clear` öncesi ILERLEME.md güncel değilse bir sonraki oturum kör başlar — programın tek hafızası bu dosyadır.
