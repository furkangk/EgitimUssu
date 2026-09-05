---
title: "Kişisel Veri Saklama ve İmha Politikası"
summary: "Her veri türü için saklama süresi, hukuki dayanağı ve imha yöntemi; hesap silme akışı ve periyodik imha takvimi — P13 Task 5'in uygulayacağı sayılar"
tags: [kvkk, hukuk, saklama, imha, anonimlestirme, E-07]
status: "🟡 taslak — hukuk onayı bekliyor · kod tarafı P13 Task 5'te yazılacak"
authority: derived
updated: 2026-09-05
---

# 🗑️ Kişisel Veri Saklama ve İmha Politikası

> ### ⚠️ Bu belge hakkında
>
> Bu politika **iki okuyucuya** birden yazılmıştır: hukuki dayanağı denetleyecek olana ve süreleri
> koda geçirecek olana. Tablodaki her süre, P13 Task 5'te yazılacak bakım servislerinin girdisidir.
> **Taslaktır; hukuk onayından geçmemiştir.**

---

## 1. İlke

Kişisel veri, **işlenme amacı ortadan kalktığında** silinir, yok edilir veya anonim hâle getirilir.
Amacın ortadan kalkması tek başına yetmez: mevzuatın öngördüğü bir saklama süresi varsa o süre
dolana kadar veri **saklanır ama kullanılmaz**.

İki süre çatışırsa **uzun olan** uygulanır; süre dolduğunda veri, o veriyi zorunlu kılan yükümlülük
kadarıyla **kimliksizleştirilerek** korunur.

---

## 2. Saklama süreleri

### 2.1 Hesap ve kimlik

| Veri | Süre | Dayanak | Süre sonunda |
|------|------|---------|--------------|
| Kullanıcı hesabı (ad, e-posta, telefon) | Üyelik süresi + **hesap kapandıktan sonra 30 gün** | Sözleşme ilişkisi; 30 gün = fikir değiştirme penceresi | **Anonimleştirme** (§4) |
| Parola özeti | Üyelik süresi | Sözleşme | Silme |
| Oturum kayıtları (refresh token) | Süre dolumu / iptalden **90 gün** sonra | Güvenlik — meşru menfaat | Silme |
| Güvenlik jetonları (şifre sıfırlama, e-posta doğrulama) | Kullanım ya da süre dolumundan **30 gün** sonra | Güvenlik | Silme |
| Push cihaz jetonu | Pasifleştikten **90 gün** sonra | Hizmet sunumu | Silme |

### 2.2 Hizmet verisi

| Veri | Süre | Dayanak | Süre sonunda |
|------|------|---------|--------------|
| Ders programı, ders oturumları | Üyelik + **10 yıl** | Sözleşmeden doğan talep zamanaşımı (TBK m.146) | Anonimleştirme |
| Ödev, ders notu, puan | Üyelik + **10 yıl** | Aynı | Anonimleştirme |
| Bireysel çalışma kayıtları, gelişim ölçümleri | Üyelik süresi | Sözleşmenin ifası — sonrasında amaç kalmaz | **Silme** |
| Mesaj içerikleri | Üyelik + **3 yıl** | Uyuşmazlık ve şikâyet incelemesi — meşru menfaat | Silme |
| Üretilmiş raporlar (PDF) | **1 yıl** | Yeniden üretilebilir; saklamanın amacı yok | Silme |
| Yüklenen dosyalar (ödev eki, ders kaynağı, sertifika) | İlgili kaydın süresi | Ana kayda bağlı | Silme (nesne depolamadan da) |
| Profil fotoğrafı | Rıza sürdüğü sürece | Açık rıza | Rıza geri alınınca **derhal silme** |

### 2.3 Ticari ve mali

| Veri | Süre | Dayanak | Süre sonunda |
|------|------|---------|--------------|
| Ödeme/ücret kayıtları, abonelik | **10 yıl** | TTK m.82 (ticari defter ve belgeler); VUK m.253 asgari 5 yıl — uzun olan uygulanır | Kimliksizleştirilerek saklanır |
| Fatura ve mali belgeler | **10 yıl** | Aynı | Yasal arşiv |

> 💡 **Ticari kayıt silinmez, kimliksizleşir.** Hesabını silen bir kullanıcının ödeme kaydı durur —
> ama "Ayşe Yılmaz" yerine anonim bir kimlikle. Tutar, tarih ve vergi bilgisi korunur; kişi
> bağlantısı kopar.

### 2.4 Teknik ve güvenlik

| Veri | Süre | Dayanak | Süre sonunda |
|------|------|---------|--------------|
| Uygulama log'ları (IP, istek, hata) | **1 yıl** | Güvenlik — meşru menfaat; 5651 s. Kanun kapsamındaysa trafik bilgisi için asgari süre «doğrulanacak» | Silme |
| İzleme kayıtları (OpenTelemetry trace/metrik) | **30 gün** | Arıza tespiti — PII zaten maskeli | Silme |
| İşlenmiş outbox mesajları | **90 gün** | Olay zinciri hata ayıklaması | Silme |
| Inbox (idempotency) kayıtları | **90 gün** | Tekrarlı işlemeyi önleme | Silme |
| Admin işlem/denetim kaydı | **5 yıl** | Hesap verebilirlik — meşru menfaat | Silme |
| Geri bildirim ve şikâyet kayıtları | **2 yıl** | Şikâyet takibi — meşru menfaat | Silme |
| Yedekler | **30 gün** | Felaket kurtarma | Otomatik silme ([`../architecture/backup_restore.md`](../architecture/backup_restore.md)) |

> ⚠️ **Yedek, silme talebinin kör noktasıdır.** Bir kullanıcının verisi bugün silinse bile 30 gün
> boyunca yedeklerde durur. Yedekten seçmeli silme yapılmaz (yedeğin bütünlüğü bozulur); bunun
> yerine **yedekler kendi süresi dolunca silinir** ve bir geri yükleme yapılırsa silme işlemleri
> yeniden uygulanır. Bu, kullanıcıya da anlatılmalıdır.

---

## 3. Hesap silme akışı

```
Kullanıcı "Hesabımı sil" der
   ↓
Talep kaydedilir · hesap KAPATILIR (giriş yapılamaz)
   ↓  30 gün — kullanıcı bu süre içinde iptal edebilir
   ↓
Süre dolunca: her modül kendi kişisel verisini anonimleştirir/siler
   ↓
Kimlik kaydı anonimleştirilir · dosyalar depolamadan silinir
   ↓
Ticari kayıtlar anonim kimlikle KALIR (yasal süre boyunca)
```

**Neden 30 gün?** Silme geri alınamaz; hesabını öfkeyle veya yanlışlıkla silen kullanıcıya dönüş
imkânı bırakır. Süre boyunca hesap **kapalıdır** — veri işlenmez, yalnız saklanır.

**İdempotenttir:** akış iki kez çalışırsa ikinci tur hiçbir şey değiştirmez.

---

## 4. Anonimleştirme kuralı

| Alan | Ne olur |
|------|---------|
| Ad, soyad | `"Silinmiş Kullanıcı"` |
| E-posta | Geri döndürülemez bir vekil değer |
| Telefon | `null` |
| Doğum tarihi | `null` |
| Profil fotoğrafı, yüklenen dosyalar | Depolamadan **silinir** |
| Serbest metin alanları (biyografi, hedef, notlar) | `null` |
| Mesaj içerikleri | Silinir; sohbet "silinmiş kullanıcı" olarak görünür |
| Ders / ödeme kayıtları | **Korunur**, yalnız kimlik bağı kopar |

> 🔴 **Kritik teknik uyarı — hash anonimleştirme değildir.**
> "E-postayı hash'leyip `deleted-{hash}@…` yazmak" ilk bakışta yeterli görünür, **değildir**:
> e-posta adresleri sınırlı ve tahmin edilebilir bir kümedir; aynı hash fonksiyonu bilinen bir
> adres listesine uygulanarak eşleştirme yapılabilir. Bu, anonimleştirme değil
> **takma adlaştırmadır** ve takma adlı veri **hâlâ kişisel veridir** — yani silme talebi
> gerçekte yerine getirilmemiş olur.
>
> **Doğru yol:** e-posta yerine ilişkisiz rastgele bir değer yazılır (`deleted-{rastgele}@…`), ya da
> gizli bir tuzla (salt) hash alınır **ve o tuz imha edilir**. Vekil değer, silinen hesaba geri
> götürmemelidir. → P13 Task 5'te böyle uygulanacak.

---

## 5. Periyodik imha

| | |
|---|---|
| **Sıklık** | **6 ayda bir** — Kişisel Verilerin Silinmesi, Yok Edilmesi veya Anonim Hale Getirilmesi Hakkında Yönetmelik periyodik imha süresinin 6 ayı aşmamasını öngörür |
| **Uygulama** | Süresi dolan kayıtları tarayan bakım servisleri (P13 Task 5) |
| **Kayıt** | Her imha turu log'lanır: ne zaman, hangi tür, kaç kayıt |

> Bakım servisleri **günlük** koşar; 6 ay bu işin *üst sınırıdır*, hedefi değil. Günlük koşan bir
> silme, süresi dolan veriyi en fazla bir gün fazladan tutar.

---

## 6. İmha yöntemleri

| Ortam | Yöntem |
|-------|--------|
| Veritabanı kaydı | `DELETE` ya da kişisel alanların anonim değerle üzerine yazılması |
| Nesne depolama (dosya) | `IFileStorage.DeleteAsync` — sağlayıcıdan kalıcı silme |
| Log / izleme | Saklama süresi dolunca toplayıcı tarafında otomatik silme |
| Yedek | Yedek dosyasının kendisinin süresi dolunca silinmesi (§2.4 uyarısı) |

---

## 7. Kodda karşılığı — P13 Task 5'e girdi

| Kural | Nerede uygulanacak | Durum |
|-------|--------------------|-------|
| 30 gün bekleme + iptal | `AccountDeletionService` | 🔴 Yazılacak |
| Modül başına anonimleştirme | `IPersonalDataAnonymizer` (Identity, Students, Teachers, Parents, Study, Messaging, Assignments, Payments, LessonSessions, Reporting) | 🔴 Yazılacak |
| Rastgele vekil e-posta (§4) | `AccountDeletionService` | 🔴 Yazılacak |
| Outbox/inbox 90 gün | Bakım servisi | 🔴 Yazılacak |
| Geri bildirim 2 yıl | `FeedbackRetentionService` (M18 — henüz yok) | 🔴 Yazılacak |
| Admin denetim kaydı 5 yıl | Bakım servisi (M18/P12) | 🔴 Yazılacak |
| Oturum/jeton temizliği 90/30 gün | Bakım servisi | 🔴 Yazılacak |
| Rapor PDF 1 yıl | Bakım servisi | 🔴 Yazılacak |
| Yedek 30 gün | `scripts/backup-db.sh` (`BACKUP_RETENTION_DAYS`) | 🟢 **Var** |
| Log/trace saklama | Toplayıcı yapılandırması | 🔴 Toplayıcı seçilmedi |

---

> İlgili: [aydınlatma metni](kvkk_aydinlatma_metni.md) · [açık rıza metinleri](kvkk_acik_riza_metni.md) ·
> [veri envanteri (iç belge)](kvkk_veri_envanteri.md) · [yedekleme](../architecture/backup_restore.md)

*Saklama ve İmha Politikası | Taslak sürüm 1.0 | Güncelleme: 2026-09-05 (ilk sürüm — Q5'i açmak için; hukuk onayı bekliyor)*
