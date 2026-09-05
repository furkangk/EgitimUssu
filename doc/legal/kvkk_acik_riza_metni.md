---
title: "KVKK Açık Rıza Metinleri"
summary: "Açık rıza gerektiren her işleme için ayrı ayrı rıza metni + arayüz kuralları; hangi işlem rıza ister hangisi istemez ayrımı"
tags: [kvkk, hukuk, acik-riza, gizlilik, E-07]
status: "🟡 taslak — hukuk onayı bekliyor"
authority: derived
updated: 2026-09-05
---

# ✅ Açık Rıza Metinleri

> ### ⚠️ Bu belge hakkında
>
> - **Taslaktır, hukuk onayından geçmemiştir.** «…» alanları doldurulmalıdır.
> - Kaynak: [`kvkk_veri_envanteri.md`](kvkk_veri_envanteri.md) §6.

---

## 0. Üç kural — arayüz bunlara uymak zorunda

Açık rıza, KVKK m.3'te **"belirli bir konuya ilişkin, bilgilendirmeye dayanan ve özgür iradeyle
açıklanan"** onay olarak tanımlanır. Bu tanım, üç somut arayüz kuralı doğurur:

**1. Rıza, aydınlatmadan ayrı alınır.**
"Aydınlatma metnini okudum ve kişisel verilerimin işlenmesine onay veriyorum" **tek bir kutu**
olamaz. Aydınlatma bilgilendirmedir, onay istemez; rıza ayrı ve ek bir irade beyanıdır.

**2. Rıza, hizmetin şartı yapılamaz.**
Rıza gerektiren işlemler hizmetin **zorunlu parçası değildir**. "Onaylamazsan kayıt olamazsın"
kurgusu rızayı özgür iradeye dayanmaktan çıkarır ve **geçersiz** kılar. Kayıt, aşağıdaki kutuların
hiçbiri işaretlenmeden tamamlanabilmelidir.

**3. Rıza her zaman geri alınabilir ve bu, vermek kadar kolay olmalıdır.**
Her rıza için ayarlarda bir kapatma anahtarı bulunur. Geri alma **ileriye etkilidir**: o ana kadar
yapılan işleme hukuka aykırı hâle gelmez, ama işleme durur ve rızaya bağlı veri silinir.

> ❌ **Önceden işaretli kutu, rıza değildir.** Varsayılan **kapalıdır**.
> ❌ **Toplu kutu, rıza değildir.** Her amaç için ayrı onay alınır.

---

## 1. Profil fotoğrafı

> **Metin (öğretmen profil ekranı, fotoğraf yükleme adımı):**
>
> Profil fotoğrafımın, öğrenci ve veliler tarafından görülebilecek şekilde profilimde
> yayımlanmasına açık rıza veriyorum.
>
> Fotoğraf yüklemek zorunlu değildir; yüklemezseniz profiliniz baş harflerinizle görüntülenir.
> Rızanızı Ayarlar → Gizlilik ve güvenlik'ten geri alabilirsiniz; geri aldığınızda fotoğraf
> **silinir**.

| | |
|---|---|
| Kim | Öğretmen (ve fotoğraf yükleyen her kullanıcı) |
| Ne zaman | Fotoğraf yükleme anında |
| Geri alma | Ayarlar → Gizlilik · fotoğraf `IFileStorage`'tan silinir |
| Verilmezse | Baş harf avatarı gösterilir — hizmet tam çalışır |

---

## 2. Çalışma verisinin öğretmen ile paylaşılması

> **Metin (öğrenci, Ayarlar → Gizlilik):**
>
> Bireysel çalışma kayıtlarımın (çalışma süresi, konu ilerlemesi, deneme sonuçları, çalışma serisi)
> öğretmenimle paylaşılmasına açık rıza veriyorum.
>
> Bu rıza yalnız **kendi başınıza yaptığınız çalışmayı** kapsar. Öğretmeninizin verdiği ödevler,
> ders kayıtları ve ders içi değerlendirmeler dersin bir parçasıdır ve bu ayardan bağımsız olarak
> öğretmeninizle paylaşılır.

| | |
|---|---|
| Kod karşılığı | `settings.user_settings.ShareStudyDataWithTeacher` |
| Varsayılan | **Kapalı** — açık rıza önceden verilmiş sayılamaz. ✅ Kod uyumlu: `UserSetting.Default(...)` iki alanı da `false` kuruyor |
| Geri alma | Aynı ayar · paylaşım anında durur |
| Verilmezse | Çalışma verisi yalnız öğrenciye görünür; ders akışı etkilenmez |

---

## 3. Çalışma verisinin veli ile paylaşılması

> **Metin (öğrenci, Ayarlar → Gizlilik):**
>
> Bireysel çalışma kayıtlarımın velim ile paylaşılmasına açık rıza veriyorum.

| | |
|---|---|
| Kod karşılığı | `settings.user_settings.ShareStudyDataWithParent` |
| Varsayılan | **Kapalı** — ✅ kod uyumlu (`UserSetting.Default`) |
| Verilmezse | Veli, ders/ödev/ödeme özetini görmeye devam eder; **bireysel çalışma** verisini görmez |

> ⚠️ **Varsayılan doğru, sahibi belirsiz.** Kod her iki paylaşımı da kapalı başlatıyor — hukuken
> doğru olan bu. Cevaplanmamış olan şu: **reşit olmayan** bir öğrencide bu anahtarı kim çevirir?
> Velinin yasal temsil yetkisi vardır; öte yandan çalışma verisinin gizliliği öğrencinin platforma
> güvenini belirler. Bugün ayar yalnız öğrencinindir. Bkz. envanter §5 (Ç-5) — ürün + hukuk kararı.

---

## 4. Ticari elektronik ileti (kampanya / duyuru) — *bugün gönderilmiyor*

> **Metin (kayıt ekranı ve Ayarlar → Bildirimler):**
>
> EğitimÜssü'nün kampanya, indirim ve yeni özellik duyurularını e-posta ve/veya uygulama bildirimi
> yoluyla göndermesine, bu amaçla iletişim bilgilerimin işlenmesine açık rıza veriyorum.
>
> Bu izni vermeseniz de ders hatırlatması, ödev bildirimi ve ödeme uyarısı gibi **hizmete ilişkin**
> bildirimleri almaya devam edersiniz.

| | |
|---|---|
| Durum | 🔴 **Şu an kodda yok** — pazarlama iletisi gönderilmiyor |
| Ek yükümlülük | 6563 s. Elektronik Ticaretin Düzenlenmesi Hakkında Kanun → **İYS** kaydı ve onay yönetimi |
| Geri alma | Her iletide "çıkış" bağlantısı + Ayarlar → Bildirimler |

> **Devreye alınmadan önce yapılacaklar:** İYS kaydı, onay kaydının İYS'ye yüklenmesi, her iletide
> ret hakkının gösterilmesi. Bunlar tamamlanmadan tek bir pazarlama iletisi bile gönderilmemelidir.

---

## 5. Kişiselleştirilmiş reklam — *bugün gösterilmiyor*

> **Metin (ilk açılışta, reklam devreye girerse):**
>
> Bana ilgi alanlarıma göre kişiselleştirilmiş reklam gösterilmesi amacıyla reklam kimliğimin
> işlenmesine ve reklam sağlayıcısıyla paylaşılmasına açık rıza veriyorum.
>
> İzin vermezseniz uygulamayı kullanmaya devam edersiniz; reklamlar **kişiselleştirilmemiş** olarak
> gösterilir.

| | |
|---|---|
| Durum | 🔴 **Şu an kodda yok** (K-06 / Q3) |
| Not | Reklam kimliği yurt dışına aktarılır → aydınlatma metni §4 buna göre güncellenir |

---

## 6. Rıza **gerektirmeyen** işlemler — kutu koymayın

Aşağıdakiler sözleşmenin ifası ya da hukuki yükümlülük kapsamındadır. Bunlar için onay istemek
yanlıştır: kullanıcıya "reddedebilirim" izlenimi verir, oysa reddedilirse hizmet zaten sunulamaz.

| İşlem | Neden rıza gerekmez |
|-------|---------------------|
| Hesap açma, kimlik doğrulama, oturum yönetimi | Sözleşmenin kurulması — m.5/2-c |
| Ders planı, ödev, ders notu, gelişim raporu | Sözleşmenin ifası — m.5/2-c |
| Öğretmen–öğrenci/veli mesajlaşması | Sözleşmenin ifası — m.5/2-c |
| **Ders/ödev/ödeme hatırlatma bildirimi** | Sözleşmenin ifası — m.5/2-c *(tercihlerden kapatılabilir; bu bir rıza değil, ayar)* |
| Ücret ve abonelik kayıtları | Sözleşmenin ifası + hukuki yükümlülük |
| Güvenlik ve hata kayıtları | Meşru menfaat — m.5/2-f |
| Yasal saklama, yetkili kurum talebi | Hukuki yükümlülük — m.5/2-ç |

---

## 7. Rıza kaydı — neyin saklanacağı

Bir rızanın varlığını **ispat yükü veri sorumlusundadır**. "Kullanıcı ayarı açmıştı" demek yetmez;
hangi metne, ne zaman onay verildiği kayıt altında olmalıdır.

Her rıza için saklanacak asgari alanlar:

| Alan | Neden |
|------|-------|
| Kullanıcı kimliği | Kimin verdiği |
| Rıza türü | Hangi amaç (fotoğraf / öğretmenle paylaşım / veliyle paylaşım / ileti / reklam) |
| **Metin sürümü** | Hangi metne onay verildiği — metin değişince eski onay yeni metni kapsamaz |
| Verildiği an (UTC) | Ne zaman |
| Geri alındığı an (UTC) | Geri alınmışsa ne zaman |
| Kanal | Mobil / web — hangi arayüzden |

> ⚠️ **Bugün böyle bir kayıt yok.** `user_settings`'teki boolean, rızanın **şu anki durumunu** tutar;
> geçmişini ve hangi metne verildiğini tutmaz. Metin sürümü değiştiğinde eski onayların geçerliliği
> gösterilemez. Bu, P13 Task 5 kapsamında açılacak bir tablo işidir (`consent_records` benzeri) —
> metin yazarak kapanmaz.

---

> İlgili: [aydınlatma metni](kvkk_aydinlatma_metni.md) · [saklama ve imha politikası](kvkk_saklama_imha_politikasi.md) ·
> [veri envanteri (iç belge)](kvkk_veri_envanteri.md)

*Açık Rıza Metinleri | Taslak sürüm 1.0 | Güncelleme: 2026-09-05 (ilk sürüm — Q5'i açmak için; hukuk onayı bekliyor)*
