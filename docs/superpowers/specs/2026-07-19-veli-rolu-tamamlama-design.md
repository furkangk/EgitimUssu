# Veli Rolü (M09) Tamamlama — Tasarım / Yol Haritası

> **Tarih:** 2026-07-19 · **Kaynak:** `doc/veli_rolu_fonksiyonel_dokuman_v1.md` (v1.0) + kod gerçeği (main)
> **Yöntem:** Öğrenci planlarının deseni — küçük dikey backend dilimleri, TDD, dilim sonu migration + commit + doküman bakımı.
> **Kapsam:** Backend + doküman. Mobil (Flutter), M12 eşleştirme, M13 puanlama, Premium satın alma altyapısı **sonraki fazlara ertelenir**.

---

## 1. Amaç

Veli rolünün fonksiyonel dokümanı ile mevcut kod arasındaki farkı kapatmak: dokümanın **[YENİ]** ve **Kritik** işaretlediği güvenlik/yasal açıkları gidermek, velinin tek retention kanalı olan bildirim motorunu kurmak, ve öğretmen bağlıyken panelin entegre görünümünü zenginleştirmek.

**İlke (dokümandan):** Velinin **kendi verisi yoktur**; panel bir sorgu/görüntüleme katmanıdır. Gördüğü her şey öğrencinin verisidir ve **gizlilik filtresinden** geçer. Değişmez kural: **çocuğun kişisel seans notları veliye asla açılmaz.**

## 2. Mevcut durum (kod gerçeği — main)

Parents modülü (M09) Faz-2 çekirdeği + temel Faz-3 dashboard olarak çalışıyor:

- `ParentProfile` — kimlik (UserId zorunlu) + 5 bildirim tercihi (`NotifyMissedAssignment/WeeklyProgressSummary/LessonReminders/TestResults/Payments`) + kanal (`Push/Email/Both`). **Doğrulama durumu yok, abonelik tier yok.**
- `ParentChildLink` — `Pending→Approved/Rejected/Revoked`, **çoklu çocuk ✅**, onay öğrenci/öğretmen/admin; veli kendi bağını onaylayamaz. `IsPrimaryContact`, `Relationship`, `InviteCode` alanları var (ama yaş/rıza/doğrulama akışı bunları kullanmıyor).
- `GetChildDashboard` / `ListChildren` — `ChildProgressSnapshot` read-model'inden **özet sayıları** (çalışma dk, streak, ders/ödev/ödeme toplamları). Detay yok.
- Yetki: `ParentAuthorizer` self/admin + öğrenci onaylar.

**Kanıtlanan boşluklar:**
- Öğrencide **doğum tarihi/yaş alanı yok** → yaş bazlı politika mümkün değil.
- **KVKK açık rıza** kaydı hiçbir yerde yok.
- Settings'te `UserSetting.ShareStudyDataWithParent` / `PrivacyLevel` **var**, ama Parents dashboard'u bunu **uygulamıyor** (gizlilik filtresi yok).
- Notifications modülünde **veli hedefi hiç yok** — tercih anahtarları var ama tetikleyen handler yok.
- Bağlantı **sessizce** kurulabiliyor (çocuğa/mevcut veliye bildirim yok).

## 3. Dilim ayrışımı

### Faz-2 güvenlik/yasal çekirdek

**V-A — Yaş + KVKK açık rıza + yaş bazlı bağlantı politikası**
- Students: öğrenciye doğum tarihi/yaş alanı; yaş türetimi.
- Parents: bağlantı akışı yaşa göre dallanır (18-: otomatik bağlan + haberdar et; 18+: onay zorunlu). Bağlantıda KVKK açık rıza kaydı (metin + tarih + versiyon).
- Açık ürün kararı: yaş eşiği (18?), rıza metni versiyonlama, 13-17 ara bandı olacak mı.

**V-B — Gizlilik filtresi**
- Parents dashboard `ShareStudyDataWithParent` + `PrivacyLevel`'e uyar (veri katmanında, arayüzde değil). Gizli alanlar "paylaşılmıyor" işaretiyle döner. Kişisel seans notu hiçbir koşulda dönmez.
- Settings → Parents okuma için modüller-arası kontrat (mevcut `IStudentDirectory`/read-model deseni).
- Açık ürün kararı: gizli alan "gizlendi" mi görünsün yoksa hiç mi görünmesin (doküman Karar-7).

**V-C — Veli doğrulama + bağlantı şeffaflığı**
- `ParentProfile`/`ParentChildLink`'e doğrulama durumu; "sessizce bağlanma yok" → bağlantı kurulunca çocuğa ve mevcut veliye bildirim; birincil veli kısıtı (2. veliyi birincil onaylar).
- Açık ürün kararı: doğrulama katmanı seviyesi (yalnız bildirim mi, öğretmen teyidi de mi).

### Faz-2/3 retention + zenginlik

**V-D — Faz-1 veli bilgisi → claim eşleşmesi**
- Öğretmenin girdiği veli telefonu ile kaydolan veliyi eşleştir (öğrenci claim/merge deseninin veli karşılığı).
- Açık ürün kararı: otomatik eşleşme mi, "bağlanmak ister misin?" onayı mı.

**V-E — Veli bildirim motoru (M11)**
- Notifications'a veli hedefi ekle: haftalık özet + olay bildirimleri (yeni ödev, ödev gecikti, ders tamamlandı özeti, ödeme gecikti); veli tercih anahtarlarına saygılı; ham `StudySession` taramaz, özet tablosundan okur.
- Açık ürün kararı: Free/Premium sınırı — bildirim Free mi (doküman 12.3) yoksa PRD 9.3 Premium mu.

**V-F — Entegre dashboard zenginleştirme**
- Read-model'i genişlet: yaklaşan ders detayı, son ders özeti (konu + öğretmen notu, görünürlük filtreli), öğretmen notları, ödeme detay listesi.
- V-B gizlilik filtresine bağlı.
- Açık ürün kararı: öğretmen notu görünürlük seviyesi (mevcut `LessonNoteVisibility` Private/Student/StudentAndParent kullanılır).

**V-G — "Ödedim" beyanı**
- Payments'a veli beyanı: veli işaretler → öğretmene bildirim → öğretmen teyit → kayıt kapanır. Para transferi değil, mutabakat. PRD "para tahsilatı yapılmaz" kuralı ihlal edilmez.
- Açık ürün kararı: beyan öğretmen teyidine mi bağlı yoksa direkt mi kaydolsun.

### Sonraki fazlar (bu roadmap'te plan yazılmaz — ertelenmiş)
- **V-M12** Veli öğretmen arama + çocuğun öğretmen talebine veli onayı (Faz 4, M12'de veli birincil kullanıcı değil — dokümanın en büyük boşluğu).
- **V-M13** Yorum okuma (yorum yapamaz).
- **V-Premium** Aile paketi + Bölüm 12.3 Free/Premium sınırının uygulanması (Faz 5).

## 4. Sıralama / bağımlılık

```
V-A (yaş+KVKK)  ─┐
V-B (gizlilik)  ─┼─ bağımsız, paralel (temel güvenlik)
V-C (doğrulama) ─┘
V-D (claim) ── V-A'ya hafif bağlı (yaş/kimlik)
V-E (bildirim) ── bağımsız; V-C bağlantı bildirimini paylaşır
V-F (dashboard) ── V-B gizlilik filtresine bağlı
V-G (ödedim) ── bağımsız
```

**Önerilen yazım sırası:** V-A → V-B → V-C → V-E → V-F → V-D → V-G.

## 5. Test / doğrulama (her dilim)
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Şema değişiminde ilgili modülün migration'ı (`dotnet ef migrations add <Ad> --project src/Modules/<Modül>/Infrastructure --startup-project src/API.Host --context <Modül>DbContext`).
- Doküman bakımı: `doc/modules/m09_parents.md` + `doc/roles/veli.md` + `doc/modules/00_genel_bakis.md` + `doc/modules/veri_modeli.md`.
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## 6. Ürün kararları (dilim dilim çözülecek)
Kullanıcı her dilimin implementasyon planı yazılırken ilgili ürün kararını verecek. Bu spec kararların **yerini** işaretler; değerlerini plan yazımında sabitler.

## 7. Kapsam dışı (bu roadmap)
Mobil UI, M12 eşleştirme, M13, Premium satın alma/aile paketi altyapısı, WhatsApp/SMS kanalı. Bunlar ayrı fazlarda ele alınır.
