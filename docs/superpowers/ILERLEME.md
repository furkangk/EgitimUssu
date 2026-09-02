# 🧭 İLERLEME — Sağlamlaştırma Programı Devam Noktası

> **Bu dosya oturumlar arası tek hafızadır.** Konuşma geçmişi `/clear` ile silinse bile "nerede kaldık" bilgisi burada durur.
> Her görev bittiğinde **bu dosya güncellenir**, sonra oturum temizlenir. Yeni oturumda `/devam` yaz.
>
> Kaynaklar: [master tasarım](specs/2026-09-02-saglamlastirma-master-design.md) · [planlar](plans/) · [eksik analizi](../../doc/denetim/2026-09-02_eksik_analizi.md)

---

## 🎯 ŞU AN

| Alan | Değer |
|------|-------|
| **Aktif plan** | `P01 — Onarım` (`plans/2026-09-02-01-onarim.md`) |
| **Sıradaki görev** | **Task 2** — A-02: Derlenmeyen mobil testleri onar |
| **Dal** | `feat/p01-onarim` |
| **Durum** | 🔄 Task 1 bitti (1/7) |
| **Son commit** | `a835eba` (fix(teachers): A-01) |
| **Çalışma ağacı** | Temiz (program dokümanları `6580acb` ile commit'lendi) |

---

## 📋 Plan Durumu

| Plan | Görev | Durum | Not |
|------|-------|-------|-----|
| P01 Onarım | 1/7 | 🔄 Devam ediyor | Ana dalı yeşile alır — **önce bu** · dal: `feat/p01-onarim` |
| P02 E-posta altyapısı | 0/7 | ⚪ Bekliyor | P01 sonrası |
| P03 Push bildirim | 0/7 | ⚪ Bekliyor | P01 sonrası (P02 ile paralel olabilir) |
| P04 Dosya depolama | 0/5 | ⚪ Bekliyor | P01 sonrası (paralel olabilir) · **Q4 kararı gerek** |
| P05 Ayarlar | 0/6 | ⚪ Bekliyor | P03 sonrası |
| P06 Öğretmen MVP | 0/10 | ⚪ Bekliyor | P02+P03+P04 sonrası → **beta noktası** |
| P07 Read-model | 0/5 | ⚪ Bekliyor | P01 sonrası |
| P08 Gelişim & raporlama | 0/6 | ⚪ Bekliyor | P07 sonrası |
| P09 Üyelik & gelir | 0/7 | ⚪ Bekliyor | P07 sonrası · **Q1–Q3 kararı gerek** |
| P10 Mesajlaşma | 0/5 | ⚪ Bekliyor | P03+P05 sonrası |
| P11 Eşleştirme & yorum | 0/6 | ⚪ Bekliyor | P07+P09 sonrası |
| P12 Admin & moderasyon | 0/6 | ⚪ Bekliyor | P11 sonrası |
| P13 Operasyon & hijyen | 0/8 | ⚪ Bekliyor | P01 sonrası — **paralel yürüyebilir** |
| P14 Web (Angular) | 0/5 | ⚪ Bekliyor | P12 sonrası |

**Lejant:** ✅ Bitti · 🔄 Devam ediyor · ⏳ Sırada · ⚪ Bekliyor · 🚫 Bloke

---

## ✅ Tamamlanan Görevler (kronolojik)

> Her satır bir görev. Kolonlar: tarih · plan/görev · commit · doğrulama sonucu.

| Tarih | Plan / Görev | Commit | Doğrulama |
|-------|--------------|--------|-----------|
| 2026-09-02 | P01 / Task 1 — A-01 öğretmen profil güncelleme 500'ü | `a835eba` | `dotnet test EgitimUssu.slnx`: 158 birim + 4 mimari + 13 integration, **başarısız 0** (5 atlandı: Docker yok) |

---

## 🧠 Yol Boyunca Öğrenilenler

> Bir sonraki oturumun bilmesi gereken, planlarda yazmayan şeyler. **Kısa tut**; kalıcı kural olacaksa `CLAUDE.md`'ye taşı, mimari karar ise master tasarım §2'ye ekle.

| # | Not | Kaynak görev |
|---|-----|--------------|
| 1 | Testcontainers testleri Docker olmadan atlanıyor; tam doğrulama için `./scripts/test-with-docker.sh` (P01 Task 5'te oluşturulacak). | Denetim 2026-09-02 |
| 2 | `dotnet test ... \| tail` çıkış kodunu maskeler — başarısız test görünmez. Sonucu daima **özet satırından** oku ("Başarısız: N"). | Denetim 2026-09-02 |
| 3 | **EF + istemcide üretilen Id = gizli tuzak.** Guid PK'lar `IIdGenerator` ile istemcide atanıyor ama EF konvansiyonu onları `ValueGenerated.OnAdd` sayıyor. Sonuç: izlenen bir aggregate'in koleksiyonuna eklenen **yeni** çocuk, `Added` yerine **`Modified`** olarak izleniyor → var olmayan satır UPDATE ediliyor → `DbUpdateConcurrencyException`. Çözüm: çocuk entity konfigürasyonunda `builder.Property(e => e.Id).ValueGeneratedNever()`. **A-01'in asıl kök nedeni buydu; plandaki merge düzeltmesi tek başına yetmedi.** | P01/Task 1 |
| 4 | Aynı tuzak **diğer modüllerde de** olabilir (aynı Id deseni her yerde). Kapsam kilidi gereği bu görevde yalnız Teachers düzeltildi — modül-genelinde tarama ayrı bir iş olarak değerlendirilmeli (öneri: P13 hijyen planına madde). | P01/Task 1 |
| 5 | `tests/Unit` projesi modül **Infrastructure** katmanlarını referanslamıyordu; EF davranışını birim testinde zorlamak için `Microsoft.EntityFrameworkCore.InMemory` + ilgili `*.Infrastructure.csproj` referansı eklendi. Benzer testler için aynı yol izlenir. | P01/Task 1 |
| 6 | Integration'da gerçek-DB deseni: `Skip.IfNot(fixture.Available, …)` + `RealInfrastructure.Use(fixture)` + `WebApplicationFactory<Program>` → `CreateAsyncScope()` ile DbContext. (Planlardaki `RealInfrastructure.CreateTeachersContextAsync()` gibi yardımcılar **yok**.) | P01/Task 1 |

---

## ⛔ Bloke Eden Kararlar

| Soru | Etkilediği plan | Durum |
|------|-----------------|-------|
| Q1 — iyzico ticari hesabı? (karar K-05) | P09 Task 4 | ❓ Cevap bekliyor |
| Q2 — Nihai fiyat listesi? | P09 Task 3 | ❓ Cevap bekliyor |
| Q3 — AdMob hesabı + yerleşim politikası? (karar K-06) | P09 Task 5 | ❓ Cevap bekliyor |
| Q4 — S3 mi Cloudflare R2 mı? (karar K-03) | P04 Task 5 | ❓ Cevap bekliyor (varsayılan: R2) |
| Q5 — KVKK metinleri hazır mı? | P13 Task 5 | ❓ Cevap bekliyor |
| Q6 — Beta öğretmen listesi (5–10 kişi)? | P06 sonrası | ❓ Cevap bekliyor |
| K-11 — Angular 20 + signals onayı? | P14 | ❓ Cevap bekliyor |

---

## 🔁 Oturum Döngüsü (nasıl çalışıyor)

```
/devam            → ILERLEME.md okunur, sıradaki TEK görev yürütülür (TDD adımlarıyla)
   ↓
/gorev-bitir      → testler koşar, plan checkbox'ları [x], doküman, commit, bu dosya güncellenir
   ↓
/clear            → konuşma bağlamı temizlenir
   ↓
/devam            → yeni oturum bu dosyadan devam eder
```

`/devam P03` → belirli bir planın sıradaki görevi · `/devam P06 Task 4` → belirli görev · `/devam plan` → aktif planın tamamı (uzun oturum).

---

*İlerleme defteri | Son güncelleme: 2026-09-02 (P01 Task 1 tamamlandı)*
