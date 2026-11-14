---
title: "KVKK Kişisel Veri Envanteri"
summary: "EğitimÜssü'nün işlediği kişisel verilerin koddan çıkarılmış envanteri: veri kategorisi → tablo/alan → amaç → hukuki sebep → alıcı → saklama; aydınlatma ve açık rıza metinlerinin dayanağı"
tags: [kvkk, hukuk, veri-envanteri, gizlilik, E-07]
status: "🟡 taslak — hukuk onayı bekliyor"
authority: code
code_refs:
  - src/Modules/*/Domain/**
  - src/Shared/Infrastructure/Email/**
  - src/Shared/Infrastructure/Push/**
  - src/Shared/Infrastructure/Storage/**
  - src/Shared/Infrastructure/Observability/**
updated: 2026-11-14
---

# 🗂️ Kişisel Veri Envanteri (KVKK)

> **Bu belge ne değildir:** Kullanıcıya gösterilecek bir metin değildir. Bu, aydınlatma metninin,
> açık rıza metinlerinin ve saklama/imha politikasının **dayanağıdır** — hepsi buradan türetilir.
> Kod değiştiğinde **önce burası** güncellenir.
>
> **Yöntem:** Alanlar `src/Modules/*/Domain/` altındaki aggregate'lerden okunmuştur. "Planlanan"
> işaretli satırlar kodda **yoktur**; envantere, aydınlatma metni yayına girdikten sonra sessizce
> eklenmesinler diye yazılmıştır.

---

## 1. Veri sahibi grupları

| Grup | Kim | Özellik |
|------|-----|---------|
| **Öğretmen** | Kendi hesabıyla kayıt olur | Yetişkin varsayılır; profili **herkese açık** olabilir (arama/keşif) |
| **Öğrenci (kendi hesaplı)** | Kendi hesabıyla kayıt olur / davet kodu ile hesabını açar | **Çoğunlukla reşit değil** → §5 |
| **Öğrenci (öğretmen-yönetimli)** | Hesabı yoktur; öğretmen kaydı açar | Veri sahibi yine **öğrencidir**, kaydı açan değil |
| **Veli** | Kendi hesabıyla kayıt olur, çocuğuna bağlanır | Çocuğun verisine **onaylı bağ** üzerinden erişir |

> ⚠️ **Öğretmen-yönetimli öğrenci, aydınlatmanın en kırılgan noktasıdır.** Veriyi öğretmen giriyor,
> ama veri sahibi öğrenci (ve reşit değilse velisi). Aydınlatma yükümlülüğü veri sorumlusunundur ve
> "öğretmen girdi" demek bu yükümlülüğü kaldırmaz. Uygulama tarafı: §5.

---

## 2. Veri kategorileri ve kaynak alanlar

### 2.1 Kimlik

| Alan | Nerede | Kim için | Zorunlu mu |
|------|--------|----------|------------|
| Ad, soyad | `identity.user_accounts.FirstName/LastName` | Hesabı olan herkes | Evet |
| Görünen ad | `teachers.teacher_profiles.FullName`, `students.student_profiles.FullName`, `parents.parent_profiles.FullName` | Profil sahibi | Evet |
| **Doğum tarihi** | `students.student_profiles.DateOfBirth` | Öğrenci | **Hayır (opsiyonel)** |
| Çocuğun görünen adı | `parents.parent_child_links.ChildDisplayName`, `students.student_parent_invites.ChildDisplayName` | Veli daveti | Hayır |
| Yakınlık derecesi | `parents.parent_child_links.Relationship` | Veli | Hayır |

> ⚠️ **`DateOfBirth` opsiyoneldir → yaş bilinmiyor → "reşit mi" sorusu cevaplanamıyor.** Bu, §5'teki
> çocuk verisi kuralının bugün **teknik olarak uygulanamaz** olduğu anlamına gelir. Kapatılması gereken
> bir açıktır, metin sorunu değildir.

### 2.2 İletişim

| Alan | Nerede | Not |
|------|--------|-----|
| E-posta | `identity.user_accounts.Email` + `NormalizedEmail` | Giriş kimliğidir; benzersiz indeksli |
| Telefon | `identity.user_accounts.PhoneNumber` | Opsiyonel; **doğrulanmıyor** (SMS/OTP kapsam dışı) |
| İletişim e-postası / telefonu | `students.student_profiles.ContactEmail/ContactPhone`, `parents.parent_profiles.ContactEmail/ContactPhone` | Öğretmenin girdiği irtibat bilgisi olabilir |

### 2.3 İşlem güvenliği

| Alan | Nerede | Not |
|------|--------|-----|
| Parola özeti | `identity.user_accounts.PasswordHash` | **Parolanın kendisi saklanmaz** |
| Oturum (refresh token) özeti | `identity.refresh_token_sessions.RefreshTokenHash` | Token'ın kendisi saklanmaz |
| Cihaz adı | `identity.refresh_token_sessions.DeviceName` | Kullanıcının oturumlarını ayırt etmesi için |
| Güvenlik jetonu özeti | `identity.user_security_tokens.TokenHash` | Şifre sıfırlama / e-posta doğrulama |
| Push cihaz jetonu | `notifications.device_tokens.Token` + `Platform` | FCM jetonu — cihaza özgüdür |
| İstek log'ları | Uygulama log'u / OpenTelemetry | IP, yol, süre, `TraceId`. **PII maskelenir** → §4 |

### 2.4 Müşteri işlem (hizmetin kendisi)

| Veri | Nerede |
|------|--------|
| Ders programı ve oturumları | `scheduling.lesson_schedules`, `lesson_sessions.lesson_sessions` (konu, süre, katılım durumu, öğretmen notu, toplantı/kayıt bağlantısı) |
| Ödev ve geri bildirim | `assignments.assignments` (başlık, açıklama, puan, öğretmen geri bildirimi, ek dosya) |
| Ders notu | `assignments.lesson_notes`, `study.study_notes` |
| Bireysel çalışma | `study.study_sessions`, `study_streaks`, `student_achievements`, `test_results`, `mock_exams` |
| Gelişim | `progress_tracking.topic_masteries`, `progress_snapshots` |
| Mesajlar | `messaging.messages.Body` + `conversations.LastMessagePreview` |
| Raporlar | `reporting.*` projeksiyonları, `student_reports` (PDF) |
| Veli görünümü | `parents.child_progress_snapshots` |

### 2.5 Finans

| Veri | Nerede | Not |
|------|--------|-----|
| Ücret kaydı | `payments.payment_records` (tutar, para birimi, vade, tahsilat tarihi, açıklama) | Öğretmen–öğrenci arasındaki **takip kaydı** |
| Veli ödeme beyanı | `payments.parent_payment_declarations` | Veli "ödedim" beyanı + not |
| Abonelik | `membership.user_subscriptions` (durum, dönem, sağlayıcı abonelik kimliği) | |
| Ders ücreti | `teachers.teacher_profiles.HourlyRateAmount` | |
| **Kart / banka bilgisi** | **YOK** | Ödeme sağlayıcı entegrasyonu (iyzico, K-05) **kodda yoktur** (Q1). Devreye girdiğinde kart verisi **sağlayıcıda** kalır, EğitimÜssü'nde saklanmaz — envanter o gün güncellenir. |

### 2.6 Mesleki deneyim (yalnız öğretmen)

`teachers.teacher_profiles`: `Subject`, `City`, `District`, `Biography`, `Headline`, `EducationLevel`,
`ExperienceYears`, `LessonFormat` · `teacher_subjects` · `teacher_certificates` (başlık, kurum, yıl, dosya) ·
`teacher_availability_slots` (haftalık müsaitlik).

> Öğretmen profili **arama sonuçlarında görünür**. Bu, "yayınlanan veri" kategorisidir ve öğretmene
> ayrıca bildirilir (aydınlatma metni §3).

### 2.7 Görsel

| Alan | Nerede |
|------|--------|
| Profil fotoğrafı | `teachers.teacher_profiles.ProfilePhotoKey` → `IFileStorage` (Local / S3-uyumlu) |
| Ödev eki | `assignments.assignments.AttachmentStorageKey` |
| Ders kaynağı | `assignments.lesson_resources` |
| Sertifika dosyası | `teachers.teacher_certificates.FileUrl` |

### 2.8 Tercih

`settings.user_settings`: bildirim tercihleri, `ShareStudyDataWithTeacher`, `ShareStudyDataWithParent`,
`PrivacyLevel`, `SessionTerminationPolicy`.

> `ShareStudyDataWith*` **rıza niteliğindedir**: öğrencinin çalışma verisinin öğretmene/veliye
> görünürlüğünü kullanıcı belirler. ✅ **Varsayılan ikisinde de kapalı** (`UserSetting.Default(...)`
> → `false, false`) — açık rızanın önceden verilmiş sayılamayacağı kuralıyla uyumlu. Açık kalan
> soru varsayılan değil, **ayarın küçük öğrencide kime ait olduğudur** → §5 (Ç-5).

### 2.9 Pazarlama — **bugün yok**

Ticari elektronik ileti gönderilmiyor, reklam kimliği işlenmiyor. AdMob (K-06) **kodda yoktur** (Q3).
Devreye girerse: reklam kimliği + açık rıza + 6563 sayılı Kanun / İYS yükümlülüğü doğar.

---

## 3. Özel nitelikli kişisel veri (KVKK m.6)

**Kasıtlı olarak toplanmıyor.** Ne kayıt formunda ne profilde sağlık, din, etnik köken, biyometrik
veya benzeri bir alan vardır.

> ⚠️ **Ama serbest metin alanları bu veriyi taşıyabilir.** `GoalSummary`, `LevelNotes`,
> `TeacherNotes`, `CoveredContent`, `Biography`, `study_notes.Body`, `messages.Body` — bir öğretmenin
> "disleksi tanısı var", "ilaç kullanıyor" yazması özel nitelikli **sağlık verisi** üretir ve m.6
> rejimi (kural olarak açık rıza) devreye girer.
>
> **Alınan tavır:** (a) hiçbir arayüz bu bilgiyi **istemez**; (b) serbest metin alanlarının yanında
> "sağlık bilgisi girmeyin" uyarısı gösterilir; (c) girilirse bu veri, aynı erişim kısıtlarıyla
> korunur ve talep üzerine silinir. Bu bir kabul değil, **azaltma**dır — kalan risk kayıt altındadır.

---

## 4. Alıcılar ve yurt dışına aktarım

| Alıcı | Ne alır | Nerede | Durum |
|-------|---------|--------|-------|
| Öğretmen | Bağlı olduğu öğrencinin ders/ödev/çalışma verisi (öğrencinin paylaşım tercihi kadarı) | — | 🟢 Aktif |
| Veli | **Onaylı bağ** kurduğu çocuğun özet verisi | — | 🟢 Aktif |
| Diğer kullanıcılar | Öğretmen profilinin **yayın alanları** | — | 🟢 Aktif |
| Barındırma (Render) | Tüm veri (sunucu + veritabanı) | **Yurt dışı** | 🟢 Aktif |
| Önbellek (Upstash Redis) | Oturum/kota sayaçları — profil verisi değil | **Yurt dışı** | 🟢 Aktif |
| Google FCM | Push jetonu + bildirim başlığı/gövdesi | **Yurt dışı** | 🟢 Kod hazır (Q8) |
| E-posta (SMTP sağlayıcı) | Alıcı adresi + e-posta içeriği | Sağlayıcıya bağlı | 🟡 Kod hazır (Q7) |
| Nesne depolama (S3 / Cloudflare R2) | Yüklenen dosyalar | **Yurt dışı** | 🟡 Kod hazır (Q4) |
| Gözlemlenebilirlik toplayıcısı (OTLP) | Trace/metrik/log — **PII maskeli** | Seçime bağlı | 🟡 Varsayılan **kapalı** |
| Ödeme sağlayıcı (iyzico) | Ödeme işlemi verisi | Türkiye | 🔴 Kodda yok (Q1) |
| Reklam (AdMob) | Reklam kimliği | **Yurt dışı** | 🔴 Kodda yok (Q3) |
| Yetkili kamu kurum/kuruluşları | Mevzuatın gerektirdiği kadarı | Türkiye | Talep hâlinde |

> 🔴 **En önemli uyum maddesi budur: altyapının tamamı yurt dışındadır.** Barındırma, veritabanı,
> önbellek, push ve nesne depolama Türkiye dışında çalışır — yani sistem, ilk kullanıcı kaydıyla
> birlikte **yurt dışına aktarım** yapar. KVKK m.9 (7499 sayılı Kanun'la değişik) bunu bir
> yeterlilik kararına, uygun güvencelere (ör. **standart sözleşme**) veya istisnalara bağlar; standart
> sözleşme kullanılıyorsa **imzadan itibaren 5 iş günü içinde Kurul'a bildirim** gerekir.
>
> **Bu, hukuk danışmanıyla kapatılması gereken tek başına en kritik açıktır** ve metin yazarak
> kapanmaz; sözleşme + bildirim işidir. Alternatif: yurt içi barındırmaya geçmek.

### PII log'a yazılmaz

`SensitiveDataMask` (P13 Task 1) `email`, `phone`, `token`, `password` içeren sorgu parametrelerini
`***` ile maskeler; her log satırı `TraceId` taşır. Doğrulandı: `?token=SECRET123` çağrısında ham
değer log'da **0 kez** geçti.

**E-posta günlüğü (P15 T6, 2026-11-14):** göndermeyen `Logging` e-posta sağlayıcısı (yalnız Development/Staging)
alıcıyı maskeli (`a***@g***.com`) yazar, gövdeyi (sıfırlama/doğrulama kodu) **hiçbir ortamda** yazmaz;
üretimde bu sağlayıcı açılışı durdurur. Açık kalan tek yer: `SmtpEmailSender` gönderim **hatasında** alıcıyı ham yazar.

---

## 5. Çocuk verisi — açık uçlar

Platformun birincil kullanıcısı **ortaokul/lise öğrencisidir**; yani işlenen verinin büyük kısmı
**reşit olmayan** kişilere aittir. Türk hukukunda küçüklerin hukuki işlem ehliyeti sınırlıdır: üyelik
sözleşmesi ve rıza, kural olarak **yasal temsilcinin** (veli) katılımını gerektirir.

| # | Açık | Bugünkü durum | Yapılması gereken |
|---|------|---------------|-------------------|
| Ç-1 | Yaş bilinmiyor | `DateOfBirth` **opsiyonel** | Kendi hesabını açan öğrenciden doğum tarihi **zorunlu** istensin |
| Ç-2 | Veli onayı yok | 18 yaş altı kayıtta veli onayı adımı **yok** | Yaş sınırının altındaysa kayıt, veli onayına bağlansın |
| Ç-3 | Bağı öğrenci onaylıyor | `ParentChildLink`'i **öğrenci** (ya da Admin) onaylar | Gizlilik için doğru, ehliyet için tartışmalı: **küçükte** onay velinin olmalı |
| Ç-4 | Öğretmen-yönetimli öğrenciye aydınlatma ulaşmıyor | Hesabı olmayan öğrenciye metin gösterilemiyor | Öğretmen, kaydı açarken öğrenci/velisini bilgilendirdiğini **beyan etsin**; metin bağlantısı e-posta ile de iletilsin |
| Ç-5 | Paylaşım ayarının sahibi | Varsayılan ✅ **kapalı** (doğru); ama ayarı **yalnız öğrenci** yönetir | Küçük öğrencide anahtarı kimin çevireceği kararlaştırılsın (öğrenci / veli / ikisi) |

> Ç-1…Ç-5 **kod işidir, metin işi değildir.** Aydınlatma metni bunları yazmakla uyumlu hâle gelmez;
> P13 Task 5 ve sonrasında kapatılmaları gerekir.

---

## 6. Hukuki sebep haritası (KVKK m.5)

| Amaç | İşlenen veri | Hukuki sebep |
|------|--------------|--------------|
| Hesap açma, kimlik doğrulama, oturum yönetimi | Kimlik, iletişim, işlem güvenliği | m.5/2-c **sözleşmenin kurulması/ifası** |
| Ders planlama, ödev, çalışma takibi, gelişim raporu | Müşteri işlem | m.5/2-c |
| Öğretmen–öğrenci/veli mesajlaşması | Mesaj içeriği | m.5/2-c |
| Ücret ve abonelik takibi | Finans | m.5/2-c; fatura/defter için m.5/2-a **kanunda açıkça öngörülme** |
| Hizmete ilişkin bildirim (ders hatırlatma, ödev, ödeme) | İletişim, push jetonu | m.5/2-c — **açık rıza gerekmez**, tercih ekranından kapatılabilir |
| Güvenlik, kötüye kullanım ve hata tespiti | İşlem güvenliği, log | m.5/2-f **meşru menfaat** |
| Yasal saklama, kamu talebi | İlgili kayıt | m.5/2-a / m.5/2-ç **hukuki yükümlülük** |
| Öğretmen profilinin **arama sonuçlarında yayını** | Profil alanları | m.5/2-c (öğretmen için hizmetin özü) |
| Profil fotoğrafı | Görsel | **m.5/1 açık rıza** (hizmet için zorunlu değil) |
| Çalışma verisinin öğretmen/veli ile paylaşımı | Çalışma verisi | **m.5/1 açık rıza** (`ShareStudyDataWith*`) |
| Ticari elektronik ileti (pazarlama) | İletişim | **m.5/1 açık rıza** + 6563 s. Kanun / İYS — *bugün yok* |
| Kişiselleştirilmiş reklam | Reklam kimliği | **m.5/1 açık rıza** — *bugün yok* |

> **Ayrım önemli:** ders hatırlatma bildirimi **sözleşmenin ifasıdır**, pazarlama bildirimi **açık rıza**
> ister. İkisini tek bir "bildirim izni" altında toplamak, rızayı da hizmeti de sakatlar.

---

## 7. Bu envanter ne zaman güncellenir

| Tetikleyici | Ne yapılır |
|-------------|-----------|
| Yeni kişisel veri alanı | §2'ye satır + amaç/hukuki sebep + saklama süresi |
| Yeni dış servis / alıcı | §4'e satır; **yurt dışıysa** aktarım dayanağı yazılır |
| Yeni amaç | §6'ya satır; açık rıza gerekiyorsa metin eklenir |
| Modül 🔴 → 🟡/🟢 | İlgili "kodda yok" satırları gerçeğe çekilir |

---

> İlgili: [aydınlatma metni](kvkk_aydinlatma_metni.md) · [açık rıza metinleri](kvkk_acik_riza_metni.md) ·
> [saklama ve imha politikası](kvkk_saklama_imha_politikasi.md) · [özet + doldurulacak alanlar](00_legal_index.md)

*Kişisel Veri Envanteri | Güncelleme: 2026-11-14 (P15 T6: e-posta günlüğünde alıcı maskeli, gövde yazılmaz) · 2026-09-05 (ilk sürüm — koddan çıkarıldı; Q5'i açmak için)*
