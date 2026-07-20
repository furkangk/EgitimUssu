# 🎓 Öğrenci Deneyimi (Student UX) — EğitimÜssü

> **Öncelik: 2️⃣** · **Faz 2 — Öğrenci Bireysel Çalışma** · **Durum: 🟡 Vizyon / tasarım hedefi (kısmen uygulandı)**
>
> **Amaç:** Öğrencinin uygulamayı yalnızca öğretmeni olduğu zaman değil, **her gün kendi isteğiyle** açmasını sağlayacak
> bir deneyim oluşturmak. Bu doküman öğrenci tarafı ekranların UX/UI hedefini tarif eder; kodda var olan ekranlar için
> teknik detay → [`ogrenci.md`](ogrenci.md), ekran envanteri → [`../pages/00_pages_index.md`](../pages/00_pages_index.md).
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogrenci.md`](ogrenci.md) · [`../architecture/design_system.md`](../architecture/design_system.md) · [`../architecture/ux_rules.md`](../architecture/ux_rules.md)
> **Güncelleme:** 2026-07-21

---

## 1. Tasarım Felsefesi

Bu uygulama yalnızca ders yönetim sistemi değildir.

Öğrencinin;

- çalışmasını,
- motivasyonunu,
- hedeflerini,
- gelişimini

tek bir yerden takip edebileceği **kişisel eğitim asistanıdır**.

Tüm ekranlar aşağıdaki soruya cevap vermelidir:

> "Bu ekran öğrencinin bugün çalışmasına yardımcı oluyor mu?"

Eğer cevap **hayır**sa ekran yeniden tasarlanmalıdır.

---

## 2. Tasarım İlkeleri

### Günlük kullanım

Her ekran öğrenciyi tekrar uygulamaya getirmelidir. Öğrenci uygulamayı;

- sabah
- ders öncesi
- ders sonrası
- akşam

olmak üzere günde birden fazla kez açmalıdır.

### Hız

Öğrenci istediği işlemi **maksimum 3 dokunuş** ile yapabilmelidir. Örneğin:

```
Ana Sayfa
   ↓
Çalışmaya Başla
   ↓
Sayaç
```

### Motivasyon

Her ekranda öğrencinin ilerlediğini hissettiren bir öğe bulunmalıdır:

- ilerleme çubuğu
- streak
- rozet
- başarı
- grafik
- hedef

### Pozitif dil

| | Örnek |
|---|---|
| ❌ Yanlış | "Bugün çalışmadın." |
| ✅ Doğru | "Bugün 30 dakika daha çalışırsan hedefini tamamlayacaksın." |

---

## 3. Bilgi Hiyerarşisi

Her ekranda önem sırası:

1. **Bugün**
2. **Yapılması gerekenler**
3. **İlerleme**
4. **Geçmiş**

Geçmiş bilgiler hiçbir zaman ilk odakta olmamalıdır.

---

## 4. Öğrenci Navigasyonu

Bottom Navigation kullanılacaktır — **maksimum 5 sekme**:

```
⏱️ Çalış   ·   📊 Performans   ·   📚 Derslerim   ·   🔍 Keşfet   ·   👤 Profil
```

> **Not (kod gerçeği, 2026-07-21 — Ç-06/Task 1, 5-sekme IA yeniden yapılandırması):** `StudentBottomNav`
> (`StudentNavTab`) **5 sekme** ile uygulandı: **Çalış** (`/student-home`) · **Performans** (`/student/performance`)
> · **Derslerim** (`/student/lessons`) · **Keşfet** (`/student/discover`, Faz 4 yer tutucu) · **Profil** (`/student/profile`).
> Eski rotalar geri-uyum için redirect'lenir: `/student/tests`→`/student/performance`, `/student/calendar`→`/student/lessons`.
> Bu görevde (Task 1) yalnızca nav iskeleti değişti — **Performans** sayfasının içeriği hâlâ eski Testler ekranı
> (`student_tests_page.dart`), **Derslerim** sayfasının içeriği hâlâ eski Takvim ekranı (`student_calendar_page.dart`);
> gerçek içerik/isimlendirme dönüşümü sonraki dilimlerde yapılır (spec: `study_student.md`). Eski **Çalışmalarım**
> (`/student/studies`) ve **Diğer** (`/student/more`) sayfaları artık sekme değildir (`current: StudentNavTab.none`);
> route'ları henüz silinmedi, Task 3/6'da retire edilecek. **Profil** artık ayrı sekme (`/student/profile`); eski
> "Diğer hub'ından push" akışı kaldırılıyor.
> Sekme/hub içerikleri (mevcut kod, henüz yeniden adlandırılmadı):
> - **Performans** (`/student/performance`) 🟢: şu an **yalnızca eski Testler ekranının içeriğini** gösterir — deneme gir
>   + net trend grafiği + ders bazlı analiz (trend oku) + son denemeler (`listTests`'ten türetilir). Eski **Çalışmalarım**
>   ekranı (`/student/studies`: sayaç + haftalık grafik + derslere göre süre + son çalışmalar + istatistik seri/hafta/toplam
>   gün) artık sekme değildir — **detabbed/dormant** (`current: StudentNavTab.none`), Task 3/6'da retire edilecek; onun
>   analiz içeriğinin Performans'a taşınması **planlanmıştır ama henüz yapılmamıştır** — bugün Performans'ta görünmez.
> - **Derslerim** (eski Takvim) 🟢: öğrencinin birleşik ders programı — öğretmenin planladığı dersler (salt-okunur, "Öğretmen"
>   rozeti, öncelikli) + öğrencinin kendi oluşturduğu dersler (renkli, düzenle/sil). Ay takvimi (`SfCalendar`) → seçili gün
>   listesi. "Ders ekle" ile tek/tekrarlı (günlük/haftalık/aylık) kişisel ders eklenir; öğretmen dersinin saatine kendi dersi
>   eklenemez (çakışma reddi). Bkz. m04 §2.3.
> - **Keşfet** 🔴 Faz 4 yer tutucu: işlevsel arama yok, "yakında" mesajı (`student_discover_page.dart`).
> - **Diğer** (`student_more_page.dart`, artık sekme değil, geçici olarak erişilebilir) 🟢: hub — Rozetler
>   (`/study/achievements`), **Öğretmenlerim** (`/student/teacher`, push), **Hedefler** (`/student/goals-overview`, push),
>   Hedef & paylaşım (`/study/goals`), Hesap bilgileri (`/account-info`), Çıkış.
> - **Öğretmenlerim** (Diğer'den açılır) 🟡: **yalnızca bağlı öğretmen(ler)i** gösterir. **Öğretmen(ler)im 🟢** gerçek — bağlı öğretmen(ler)
>   güvenli öğrenci-kapsamlı derslerin (`GET /scheduling/students/{id}/lessons`, sahiplik `IStudentDirectory` ile doğrulanır, IDOR koruması)
>   `teacherUserId` kümesinden türetilir, profilleri `GET /api/teachers/profiles/{userId}` ile getirilip bilgi kartı olarak gösterilir
>   (avatar hero + ad/doğrulama + branş/konum/format meta + deneyim/eğitim/ücret istatistik bloğu + "Hakkında"; öğrenci ekranlarıyla tutarlı, birden fazla öğretmen desteklenir). Dersler bu ekranda listelenmez (Derslerim'de).
>   Ödevler/Ders Notları/Mesajlar hâlâ "yakında" (backend yok). (§10: yalnızca öğretmen bağlıysa gösterme — hiç öğretmen yoksa boş durum gösterilir.)
> - **Profil/İstatistik** (artık ayrı sekme, `/student/profile`) 🟢: toplam çalışma/gün/rekor + toplam deneme/net + en çok çalışılan ders + rozet özeti.

---

## 5. Ana Sayfa

Bu uygulamanın **en önemli ekranıdır**. Öğrenci uygulamayı açtığında aşağıdaki soruların cevabını anında görebilmelidir:

- Bugün ne kadar çalıştım?
- Bugünkü hedefim ne?
- Son test sonucum ne?
- Yaklaşan ders var mı?
- Devam etmem gereken çalışma hangisi?

### İçerik Sırası

**Karşılama**

```
Merhaba Furkan 👋
```

**Streak Kartı**

```
🔥 8 gündür çalışıyorsun
```

**Günlük Hedef**

```
██████░░░░
3 / 5 Saat
```

**Devam Et** — En son çalışılan konu, tek dokunuşla devam.

**Yaklaşan Ders** — Saat · Branş · Öğretmen

**Haftalık Grafik** — 7 günlük çalışma süresi (Bar Chart)

**Günlük Görevler** — Checkbox:

- [ ] Matematik
- [ ] Paragraf
- [ ] Fizik

> **Not (kod gerçeği, 2026-07-07):** `student_home_page.dart` bilgi hiyerarşisine (§3) göre yeniden düzenlendi:
> selamlama + **pozitif/güne özel motivasyon alt başlığı** ("Bugün 45dk daha çalışırsan hedefini tamamlayacaksın") →
> **Bugünkü çalışma + Günlük seri** (yan yana eşit özet kartları) → **Hızlı işlemler** (Kronometre birincil
> tam-genişlik CTA + 4 ikincil eylem 2×2 ızgarada) →
> **İlerlemen** (bu hafta + rekor seri) → **Geçmiş** (son deneme + son çalışmalar).
> Henüz uygulanmadı: **Yaklaşan Ders** (öğrenci-kapsamlı ders endpoint'i yok), **Haftalık Grafik** (dashboard
> gün-bazlı veri döndürmüyor; `WeeklySummary.perDay` var ama panoya bağlı değil), **Günlük Görevler** (checkbox — veri yok).

---

## 6. Çalışmaya Başla

Bu ekran uygulamanın **yıldız özelliğidir**. Forest + Pomodoro mantığı kullanılacaktır.

### Başlangıç

- Alanlar: **Ders · Konu · Süre**
- Buton: **Başlat**

### Sayaç

- Ortada büyük timer
- Altında: **Duraklat · Bitir**

### Seans Sonu

Gösterilecekler:

- Toplam süre
- Çalışılan konu
- Günlük toplam
- Kişisel not

---

## 7. Çalışmalarım

Gösterilecek bilgiler:

- Günlük çalışma
- Haftalık çalışma
- Aylık çalışma
- Ders bazlı dağılım

Grafikler: **Pie Chart · Bar Chart · Timeline**

---

## 8. Testler

Test *eklemek* yerine **analiz** ekranı oluşturulacaktır.

Kart:

```
35 Doğru
 4 Yanlış
 1 Boş
34 Net
```

Altında konu bazlı başarı:

```
Problemler   ████████
Fonksiyonlar ████
Geometri     ██
```

Trend: `↑ +5 Net` / `↓ -2 Net`

---

## 9. Hedefler

Gösterilecekler:

- Günlük hedef
- Haftalık hedef
- Aylık hedef
- Üniversite hedefi

Her hedef **Progress Bar** ile gösterilecektir.

> **Not (kod gerçeği, 2026-07-08):** "Hedefler" artık alt menüde ayrı sekme değil; **Diğer hub'ından** açılır
> (`/student/goals-overview`, pushed, AppBar'lı). Yerini alt menüde **Takvim** sekmesi aldı (§9a).

---

## 9a. Takvim (2026-07-08)

Öğrencinin **kişisel eğitim asistanı** vizyonunun (§1) merkez parçası: öğrenci bir öğretmeni olmasa bile kendi
çalışma programını kurar ve her gün buradan yönetir.

- **Birleşik görünüm:** Öğretmenin planladığı özel dersler + öğrencinin kendi oluşturduğu dersler bir arada.
- **Öncelik:** Öğretmen dersleri salt-okunur ve "Öğretmen" rozetiyle öne çıkar; öğrenci o slota kendi dersini ekleyemez.
- **Kendi programı:** "Ders ekle" (tam ekran form) ile ders adı, saat, hatırlatma ve tek/tekrarlı (günlük·haftalık·aylık,
  bitiş tarihli) seçim. Örn. *her Pazartesi 15:00–16:00 Matematik*.
- **Etkileşim:** Gerçek ay takvimi (`SfCalendar` ay görünümü, günlerde nokta göstergesi — öğretmen takvim sayfasıyla aynı bileşen) → seçilen günün listesi. Kendi dersinde düzenle/sil.
- **Kronometre bağlantısı (§6):** Kronometre ders seçicisi takvimden beslenir; ders seçmeden başlatınca **serbest çalışma** kaydedilir.

Teknik: `GET /scheduling/students/{id}/calendar` (tekrarlar backend'de genişletilir) — bkz. [`../modules/m04_scheduling.md`](../modules/m04_scheduling.md) §2.3.

---

## 10. Öğretmenlerim

Bu sekme **yalnızca öğretmen bağlıysa** görünmelidir. Öğretmen(ler), öğrencinin derslerindeki
`teacherUserId` kümesinden türetilir; hiç öğretmen yoksa boş durum kartı gösterilir.

İçerik yalnızca bağlı öğretmen(ler)in bilgi kartıdır; dersler bu ekranda değil **Takvim** ekranında listelenir.

- **Öğretmen bilgi kartı 🟢** — açılır-kapanır gradient hero kartı: varsayılan yalnızca başlık (navy→mavi degrade band + avatar + ad + doğrulama rozeti + başlık + branş pill'i + sağda aç/kapat oku); oka/başlığa dokununca gövde açılır (deneyim/eğitim/ücret istatistik satırı + konum/format meta + "Hakkında" biyografi) (`GET /api/teachers/profiles/{userId}`). Birden fazla öğretmen desteklenir.
- Ders notu (yakında)
- Verilen ödev + teslim tarihi (yakında)
- Mesajlar (yakında)

---

## 11. Profil

**İstatistik odaklı** olacaktır. Kartlar:

- Toplam çalışma
- Toplam test
- Toplam net
- En çok çalışılan ders
- Seri gün
- Rozetler

---

## 12. Oyunlaştırma

Öğrenci motive edilmelidir. Kullanılacak sistemler:

### Rozet

- İlk çalışma
- İlk 10 saat
- 100 saat
- 500 saat

### Streak

Günlük seri: **7 · 30 · 100 gün**

### Kişisel Rekor

- En uzun çalışma
- En yüksek net
- En iyi hafta

---

## 13. AI Koç (Premium)

AI öğrencinin verilerini analiz eder. Örnek:

> "Problemlerde son iki haftadır düşüş var. Yarın **45 dakika problem**, **30 dakika paragraf** öneriyorum."

AI **hiçbir zaman yargılayıcı** konuşmamalıdır.

---

## 14. UX Kuralları

- ✔ Büyük dokunma alanları
- ✔ Tek elle kullanım
- ✔ Az metin
- ✔ Çok görsel
- ✔ Renk yerine ikon desteği
- ✔ Her sayfada CTA
- ✔ Boş ekranlar motive edici olmalı
- ✔ Animasyonlar kısa
- ✔ Gereksiz popup kullanılmamalı

---

## 15. Tasarım Hissi

Arayüz aşağıdaki uygulamalardan ilham almalıdır:

- Duolingo
- Forest
- Notion Calendar
- Todoist
- Habitica
- TickTick

**Hedef:** Minimal · Modern · Premium · Sade · Motivasyon veren · Hızlı · Profesyonel

---

## 16. AI Tasarım Kuralları

AI yeni ekran tasarlarken aşağıdaki kurallara uymalıdır:

- Önce UX düşün, sonra UI tasarla.
- Gereksiz bileşen ekleme.
- Her ekranda tek ana amaç olsun.
- Kullanıcı dikkatini dağıtma.
- Mobil öncelikli tasarla.
- Material 3 prensiplerine uy.
- Component tabanlı düşün, reusable widget kullan.
- Responsive tasarla.
- Dark Mode destekle.
- Accessibility kurallarına uy.
- Premium hissiyat oluştur.

---

*Öğrenci Deneyimi (Student UX) — Vizyon & Tasarım Hedefi | Güncelleme: 2026-07-21*
