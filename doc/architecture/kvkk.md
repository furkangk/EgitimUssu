---
title: "KVKK — Hesap Silme, Anonimleştirme ve Saklama (Teknik)"
summary: "E-07'nin kod tarafı: hesap silme akışı, IPersonalDataAnonymizer sözleşmesi, modül modül hangi verinin ne olduğu ve saklama süresi bakım servisleri"
type: architecture
status: 🟢 uygulandı
authority: code
code_refs:
  - src/Shared/Contracts/PersonalDataContract.cs
  - src/Modules/Identity/Infrastructure/AccountDeletionService.cs
  - src/Modules/*/Infrastructure/*PersonalDataAnonymizer.cs
  - src/Shared/Infrastructure/Persistence/DataRetentionService.cs
tags: [kvkk, gizlilik, hesap-silme, anonimlestirme, saklama, E-07]
updated: 2026-11-14
---

# 🔐 KVKK — Hesap Silme ve Saklama (Teknik)

> **Bu doküman koddur, hukuk değildir.** Hangi verinin neden saklandığı, hukuki sebepler ve
> kullanıcıya gösterilecek metinler [`doc/legal/`](../legal/00_legal_index.md) altındadır. Burada
> yalnız **nasıl uygulandığı** yazılıdır. İkisi çelişirse `doc/legal/` politikadır, burası gerçektir
> — çelişki bir hatadır, düzeltilir.

---

## 1. Akış

```
Kullanıcı: POST /api/identity/account/deletion-request
   ↓
AccountDeletionRequest (Pending, ScheduledForUtc = now + 30 gün)   ← hiçbir veri değişmez
   ↓  kullanıcı isterse: DELETE /api/identity/account/deletion-request → Cancelled
   ↓  30 gün
AccountDeletionService (6 saatte bir, kiralı — P15 T8) → AccountDeletionProcessor.RunAsync()
   ↓
IStudentDirectory.GetStudentIdsForUserAsync(userId)      → PersonalDataSubject(UserId, StudentIds)
   ↓
her IPersonalDataAnonymizer.AnonymizeAsync(subject)      → 11 modül, sırayla
   ↓
UserAccount.Anonymize(vekilEposta, kullanılamazHash)     → Status = Closed
   ↓
AccountDeletionRequest.Complete()  +  SaveChanges
```

### Üç tasarım kararı

**1. Identity en sonda anonimleştirilir.** Identity kaydı akışın çapasıdır: o silindikten sonra bir
modül hata verirse geriye kimliği belli olmayan bir kullanıcının yarım verisi kalır ve tekrar
denenemez. Bu sırayla hata hâlinde talep `Pending` kalır, sonraki tur baştan dener.

**2. Her adım idempotenttir.** Anonimleştiriciler "zaten anonim mi" diye bakar ve `0` döner. Tur
yarıda kesilirse yeniden koşmak güvenlidir — bu, hata toleransının tek mekanizmasıdır.

**3. Modül sınırı korunur.** Identity hiçbir modüle proje referansı vermez; tek bağ
`Shared/Contracts`'teki `IPersonalDataAnonymizer` sözleşmesidir. `Modules_Should_Not_Reference_Other_Modules`
mimari testi bunu zorlar.

---

## 2. Sözleşme

```csharp
namespace EgitimUssu.Shared.Contracts;

public sealed record PersonalDataSubject(Guid UserId, IReadOnlyCollection<Guid> StudentIds);

public interface IPersonalDataAnonymizer
{
    string ModuleName { get; }
    Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken);
}
```

> **Neden iki kimlik?** Öğretmen tarafı `UserId` (Identity), öğrenci tarafı `StudentId`
> (`StudentProfile.Id`) ile referanslanır — kod tabanının bilinen asimetrisi
> ([`veri_modeli.md`](../modules/veri_modeli.md) §2). Study, Assignments, LessonSessions ve
> Reporting yalnız `StudentId` bilir ve modül sınırı yüzünden onu `UserId`'den çözemez; bu yüzden
> çözülmüş liste konuya iliştirilir (`IStudentDirectory.GetStudentIdsForUserAsync`).

> ⚠️ **Yeni bir modül kişisel veri tutmaya başladığında buraya bir implementasyon eklemek
> zorundadır.** Eklenmezse silme talebi **sessizce eksik** kalır ve hiçbir test bunu göremez —
> bu yüzden `RealAccountDeletionTests` gerçek kayıtların gerçekten kaybolduğunu okur.

---

## 3. Modül modül: ne oluyor

| Modül | Ne yapılıyor | Neden |
|-------|--------------|-------|
| **Identity** | Ad/e-posta/telefon üzerine yazılır, oturum ve jetonlar **silinir**, `Status = Closed` | Satır kalır çünkü ders/ödeme kayıtları ona referans verir |
| **Teachers** | Profil kimliksizleşir, **`IsActive = false`**, fotoğraf + sertifika dosyaları depolamadan silinir | Silinmiş bir öğretmenin arama sonuçlarında görünmesi en görünür ihlal olurdu |
| **Students** | Profil alanları (ad, iletişim, hedef, doğum tarihi) temizlenir; veli davetindeki çocuk adı silinir | Profil satırı ders/ödev/ödeme referansları için kalır |
| **Parents** | Veli profili + bağdaki kişisel alanlar temizlenir; çocuk gelişim özeti ve tanınan öğrenci satırları **silinir** | İkincisi türev veridir, saklama amacı kalmaz |
| **Study** | Tüm çalışma verisi **silinir** (seans, deneme, seri, başarım, not, katalog) | Saklama amacı üyelikle sınırlı — ticari/yasal sebep yok |
| **Messaging** | Gönderilen mesajların **gövdesi silinir**, sohbet önizlemesi temizlenir | Sohbet satırı karşı tarafın da verisidir; silinen **içeriktir** |
| **Notifications** | Cihaz jetonları, kullanıcı/veli bildirimleri, ders hatırlatmaları **silinir** | 🔴 Jeton silinmezse silinmiş hesabın telefonuna push gitmeye devam eder |
| **Settings** | Ayar satırı **silinir** | Yalnız o kullanıcı içindir |
| **Assignments** | Ödev/ders notu/kaynak serbest metinleri temizlenir, dosyalar depolamadan silinir | Kayıt iskeleti ders geçmişinin parçası |
| **LessonSessions** | Konu/notlar/bağlantılar temizlenir; **süre, tarih, katılım, durum korunur** | Ders kaydı ücretlendirmenin dayanağı |
| **Payments** | Açıklama ve not temizlenir; **tutar, para birimi, vade, durum korunur** | Ticari kayıt (TTK m.82: 10 yıl) |
| **Reporting** | Raporlar (PDF dosyası dahil) ve projeksiyonlar **silinir** | Tamamı türev veri, kaynaklardan yeniden kurulabilir |

### Vekil e-posta: neden hash değil

🔴 `deleted-{hash(email)}@egitimussu.local` **yeterli değildir**. E-posta adresleri sınırlı ve tahmin
edilebilir bir kümedir; aynı hash bilinen bir adres listesine uygulanarak **eşleştirilebilir**. Bu
anonimleştirme değil takma adlaştırmadır ve takma adlı veri hâlâ kişisel veridir — yani silme talebi
gerçekte yerine getirilmemiş olur.

`PersonalDataPlaceholder.DeletedEmail(Guid surrogateId)` **rastgele** bir kimlik alır
(`IIdGenerator.New()`); silinen adresle hiçbir matematiksel ilişkisi yoktur. Parola özeti de aynı
biçimde rastgele bir değerden üretilir.

---

## 4. Saklama süresi bakımı

`DataRetentionService` (Shared/Infrastructure, **günde bir**, kiralı — P15 T8) süresi dolan **teknik** kayıtları siler:

| Kayıt | Süre | Not |
|-------|------|-----|
| İşlenmiş `outbox_messages` | 90 gün | Tüm modül şemalarında |
| `inbox_messages` (idempotency) | 90 gün | Aynı |

> **İşlenmemiş outbox satırına dokunulmaz.** O henüz yayınlanmamış iştir; eskimiş olması onu çöp
> yapmaz, tam tersine bir arıza işaretidir. Silinseydi olay **sessizce kaybolurdu**.

> Mevzuat periyodik imha için 6 aylık bir **üst sınır** koyar; günlük koşum o sınırın çok altındadır.

### Henüz yazılmayan temizlikler

| Kural | Neden yok | Ne zaman |
|-------|-----------|----------|
| Geri bildirim/şikâyet 2 yıl | **M18 (Feedback) modülü yok** | M18 açılınca (P12) |
| Admin denetim kaydı 5 yıl | **AdminAuditLog yok** | Admin API'siyle (P12, K-08) |
| Oturum/jeton temizliği 90/30 gün | Hesap silmede zaten siliniyor; süresi dolan jetonlar için ayrı tur gerekiyor | Ayrı bir bakım turu |
| Rapor PDF 1 yıl | Hesap silmede siliniyor; süre bazlı tur yok | Ayrı bir bakım turu |
| Log/trace saklama | Toplayıcı seçilmedi (OTLP kapalı) | Toplayıcı devreye girince |

---

## 5. Uçlar

| Uç | Yetki | Davranış |
|----|-------|----------|
| `POST /api/identity/account/deletion-request` | Oturum | Bekleyen talep varsa **onu döndürür** — ikinci istek bekleme süresini sıfırlamaz |
| `DELETE /api/identity/account/deletion-request` | Oturum | Bekleyen talebi iptal eder; yoksa `404 identity.deletion_request_not_found` |
| `GET /api/identity/account/deletion-request` | Oturum | `{ pending, requestedOnUtc, scheduledForUtc }` |

> **Admin görünümü (silme taleplerini listeleme) bu görevde yazılmadı** — `/api/admin/*` uç grubu
> henüz yok (K-08, P12). Talepler o zaman admin panelinden görünecek.

---

## 6. Test kapsamı

| Test | Ne kanıtlıyor |
|------|---------------|
| `tests/Unit/AccountDeletionTests.cs` | 30 gün kuralı, iptal, idempotency, bir modül patlarsa talebin **Pending kalması**, ödeme tutarının korunması, vekil e-postanın türetilmemesi |
| `tests/Unit/DataRetentionTests.cs` | Süresi dolan satırların silinmesi, **işlenmemiş outbox'ın korunması** |
| `tests/Integration/RealAccountDeletionTests.cs` | **Gerçek Postgres'te on şemanın** tek akışta temizlenmesi; birim testi sahte anonimleştiricilerle koşar ve DI'a kaydedilmemiş bir modülü göremez |

---

> İlgili: hukuki metinler → [`../legal/00_legal_index.md`](../legal/00_legal_index.md) ·
> saklama süreleri (politika) → [`../legal/kvkk_saklama_imha_politikasi.md`](../legal/kvkk_saklama_imha_politikasi.md) ·
> kimlik modülü → [`../modules/m01_identity.md`](../modules/m01_identity.md) ·
> backend mimari → [`backend.md`](backend.md)

*KVKK (Teknik) | Güncelleme: 2026-11-14 (P15 Task 8 / A-07: hesap silme ve saklama servisleri iş kirası altında koşar — çok örnekte tur tek örnekte; yeniden başlatmada en çok bir kira süresi (12 sa / 48 sa) gecikme, ayrıntı `backend.md` §7.3) · 2026-09-05 (P13 Task 5 / E-07: ilk sürüm — hesap silme akışı, 11 modül anonimleştiricisi, saklama bakım servisi)*
