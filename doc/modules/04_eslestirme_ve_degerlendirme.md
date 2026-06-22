# 🔍 Eşleştirme ve Değerlendirme Modülü — Detaylı Tasarım Dokümanı

> **Öncelik: 4️⃣ (Son)** · **Faz 4 — Eşleştirme & Puanlama** · **Durum: 🔴 İskelet**
>
> **Amaç:** Öğrenci ve öğretmenin birbirini bulması (özel ders eşleştirme) + güven altyapısı olarak
> öğretmen puanlama/yorum sistemi.

> **Tasarım ilkesi (PRD §10.1):** Bu modül **en son** açılır. "Sık yapılan hata: pazar yeri fonksiyonunu
> erken açmak, yönetim tarafını yarım bırakmaktır." Eşleştirmeye yalnızca Faz 1-2'nin gerçek kullanıcılarda
> çalıştığı doğrulandıktan ve her iki tarafta (öğretmen + öğrenci) kullanıcı havuzu oluştuktan sonra geçilir.

---

## 1. İki Alt Sistem

| Alt Sistem | Backend Modülü | PRD | Rol |
|------------|----------------|-----|-----|
| Keşif & eşleştirme | `Matching` | M12 | Öğretmen-öğrenci buluşması |
| Puanlama & yorum | `Reviews` | M13 | Güven / sosyal kanıt altyapısı |

> Puanlama (M13), eşleştirmenin (M12) **güven altyapısıdır**: sosyal kanıt olmadan öğretmen keşfi yeterince güven vermez.

---

## 2. Mevcut Durum (Koddan Doğrulanmış)

- `Matching` modülü iskelet: `MatchingDbContext` + DI + `GET /api/matching/status`. Domain/feature/migration yok.
- `Reviews` modülü iskelet: `ReviewsDbContext` + DI + `GET /api/reviews/status`. Domain/feature/migration yok.
- **Hazır altyapı:** `TeacherProfile` (branş, şehir, ilçe, ücret, ders şekli, uygunluk, `IsVerified`) keşif/filtreleme için gerekli alanları zaten içeriyor (bkz. [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md)).

---

## 3. M12 — Eşleştirme ve Keşif

### 3.1 Yetenekler (PRD)
- Öğretmen listeleme ve arama (herkese açık görünüm).
- Filtreleme: branş, şehir/ilçe, ücret, ders şekli, uygun saatler.
- Öğretmen profil sayfası (puan, yorumlar, geçmiş).
- Talep / mesaj gönderme.
- Profil doğrulama rozeti.
- Premium: profil öne çıkarma (Faz 5).

### 3.2 Önerilen Domain Modeli — `Matching`
- **`MatchRequest` (AggregateRoot):** `StudentUserId`, `TeacherUserId`, `Subject`, `Message`, `Status` (`Pending`, `Accepted`, `Declined`, `Expired`), `CreatedOnUtc`. → `Accept()/Decline()`.
- **`TeacherSearchProjection` (read-model):** `Teachers` modülünden beslenen, aramaya/filtreye optimize edilmiş okuma modeli (branş, şehir, ücret aralığı, ortalama puan, doğrulama rozeti, öne çıkarma bayrağı).

> Modül sınırı kuralı: `Matching` öğretmen verisini doğrudan `Teachers` DB'sinden okumaz; `TeacherProfileCreated/Updated`
> integration event'lerini dinleyerek kendi arama projeksiyonunu günceller (CQRS read-model).

### 3.3 Önerilen API — `/api/matching`
```
GET  /api/matching/teachers?subject=&city=&district=&minRate=&maxRate=&format=&availableDay=
GET  /api/matching/teachers/{teacherUserId}        → herkese açık öğretmen profil sayfası (+ puan/yorum)
POST /api/matching/requests                         → talep/mesaj gönder
GET  /api/matching/teachers/{teacherUserId}/requests → öğretmene gelen talepler
POST /api/matching/requests/{id}/accept             → kabul (→ öğretmen-öğrenci ilişkisi kurulur)
POST /api/matching/requests/{id}/decline            → reddet
```

> **Eşleştirme tamamlanınca:** Talep kabul edildiğinde öğretmen-öğrenci ilişkisi kurulmalı —
> yani `StudentProfile` öğretmene bağlanmalı (bkz. [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md) §5 bağ kurma).

---

## 4. M13 — Öğretmen Puanlama ve Yorum

### 4.1 Temel Kurallar (PRD)
- Yalnızca o öğretmenden **ders almış** öğrenciler yorum yapabilir (sahte yorum önleme).
- Ders tamamlandıktan sonra sistem otomatik yorum daveti gönderir.
- Yorum metni + 1–5 yıldız genel puan.

### 4.2 Alt Kategori Puanlama
Anlatım netliği · Dakiklik ve güvenilirlik · Sabır ve yaklaşım · Ders hazırlığı.

### 4.3 Önerilen Domain Modeli — `Reviews`
- **`TeacherReview` (AggregateRoot):** `TeacherUserId`, `StudentId`, `OverallRating` (1-5), alt kategori puanları, `Comment`, `IsVerifiedStudent`, `Status` (`Published`, `Flagged`, `Removed`), `CreatedOnUtc`.
- **`TeacherResponse` (Entity):** öğretmenin yoruma yanıtı (olumsuz yorum **gizlenemez**, yalnızca yanıtlanabilir).
- **`ReviewFlag` (Entity):** şüpheli yorum bildirimi → admin moderasyonu.

**Kural (doğrulanmış öğrenci):** Yorum hakkı, `LessonSessionCompletedDomainEvent` ile o öğretmen-öğrenci çifti
arasında **tamamlanmış ders kaydı** olduğunda doğar (bkz. [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md) §6 olay akışı).

### 4.4 Önerilen API — `/api/reviews`
```
POST /api/reviews                                   → yorum gönder (yalnızca dersi tamamlamış öğrenci)
GET  /api/reviews/teachers/{teacherUserId}          → ortalama puan + yorum listesi
POST /api/reviews/{id}/respond                      → öğretmen yanıtı
POST /api/reviews/{id}/flag                         → şüpheli bildir
# Admin moderasyon
GET  /api/reviews/flagged                           → bildirilen yorumlar (admin)
POST /api/reviews/{id}/moderate                     → yayınla/kaldır (admin)
```

### 4.5 Erken Açılış Stratejisi (PRD M13)
> Puanlama Faz 4'te herkese açılır. Ancak **Faz 1-2'de "öğretmene özel geri bildirim"** olarak erken aktive
> edilebilir — öğrenci değerlendirme gönderir, **yalnızca öğretmen görür**. Bu, veri birikimini Faz 4'ten önce başlatır.
> (Faz 1 iş kalemi 1.9 — "öğretmene özel geri bildirim (puanlama ön versiyonu)" ile uyumlu.)

---

## 5. Mobil — Eşleştirme & Değerlendirme (Tasarlanacak)

- `teacher-discovery` — arama + filtre (branş, şehir, ücret, ders şekli, uygunluk).
- `teacher-public-profile` — herkese açık profil + puan/yorum + "talep gönder".
- `match-requests` — (öğretmen) gelen talepler; (öğrenci) gönderdiği talepler.
- `review-submit` — ders sonrası yıldız + alt kategori + yorum.
- `review-respond` — öğretmenin yoruma yanıtı.

> `design.md`'ye göre eşleştirme (M12) ve gelişmiş raporlama, **web (Angular)** tarafında da öncelikli olacak;
> mobil + web her iki platformda da aktif edilecek.

---

## 6. Kabul Kriterleri (Faz 4 Çıktısı)

- [ ] Öğrenci, öğretmenleri arayıp filtreleyebilir.
- [ ] Herkese açık öğretmen profil sayfası (puan + yorum + geçmiş).
- [ ] Talep/mesaj gönderme; öğretmen kabul/ret → ilişki kurulur.
- [ ] Profil doğrulama rozeti.
- [ ] Yalnızca dersi tamamlamış öğrenci yorum yapabilir.
- [ ] Yıldız + alt kategori puanlama; öğretmen yanıtı.
- [ ] Doğrulanmış öğrenci rozeti + şüpheli yorum bildirme + admin moderasyon paneli.

---

## 7. Eksikler ve Yapılacaklar

> ⚠️ **Önkoşul:** Faz 1-2-3 gerçek kullanıcılarda doğrulanmadan ve öğretmen+öğrenci havuzu oluşmadan başlanmamalı (PRD §10.1).

1. **Erken geri bildirim (Faz 1.9)** — "öğretmene özel" basit değerlendirme ile veri biriktirmeye başla.
2. **`Reviews` domain'i** — `TeacherReview`, yanıt, flag + moderasyon.
3. **`Matching` arama read-model'i** — `Teachers` event'lerinden beslenen projeksiyon.
4. **Eşleştirme talep akışı** — talep → kabul → öğretmen-öğrenci ilişkisi kurma.
5. **Mobil + web keşif/değerlendirme ekranları.**
6. **Premium (Faz 5):** profil öne çıkarma, doğrulama rozeti vurgusu.

---

## 8. İlişkili Dokümanlar

- Eşleştirilecek öğretmen profili → [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md)
- Eşleştirilecek öğrenci havuzu → [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md)
- Genel durum & strateji → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Eşleştirme ve Değerlendirme Modülü — Detaylı Tasarım | Faz 4 | Güncelleme: 2026-06-21*
