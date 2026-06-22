# 👪 Veli Modülü — Detaylı Tasarım Dokümanı

> **Öncelik: 3️⃣** · **Faz 2-3 — Veli Paneli** · **Durum: 🔴 İskelet**
>
> **Amaç:** Veli, çocuğunun gelişimini **şeffaf** şekilde görüntülesin. Veli platforma çocuğun
> bireysel çalışma verisiyle (öğretmen gerekmeden) veya öğretmen bağlıysa ders verisiyle dahil olur.

> **Tasarım ilkesi (PRD §M09):** Veli paneli **iki farklı veri kaynağından** beslenir:
> (1) Bireysel çalışma (öğretmen gerekmez), (2) Öğretmen bağlıysa ders/ödev/ödeme verisi.

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

- `Parents` modülü **tamamen iskelet**: yalnızca `ParentsDbContext` + DI + `GET /api/parents/status`.
  Domain modeli, feature (CQRS), migration ve mobil ekran **yok**.
- Bağ noktası hazır: `StudentProfile.ParentUserId` (Guid?) alanı zaten domain'de mevcut
  (`src/Modules/Students/Domain/StudentsDomainModel.cs`) — veli bir öğrenciye buradan bağlanır.
- `Identity` rolü hazır: `UserRole.Parent = 4`.

---

## 2. İki Veri Kaynağı (PRD §M09)

| Veri Kaynağı | İçerik | Önkoşul | Bağımlı Modül |
|--------------|--------|---------|---------------|
| **Bireysel çalışma** | Haftalık çalışma süreleri, konu dağılımı, test performansı, streak | Öğretmen gerekmez | `Study` (M08) |
| **Öğretmen bağlıysa** | Son ders özeti, verilen ödevler, öğretmen notları, ödeme özeti | Öğrenci bir öğretmene bağlı | `LessonSessions`, `Assignments`, `Payments` |

> Bu nedenle Veli modülü, **kendi domain verisini üretmez**; çoğunlukla diğer modüllerin verisini
> veli perspektifinden **okuyan/birleştiren** bir okuma (read-model) modülüdür.

---

## 3. Tasarlanması Gereken Domain Modeli — `Parents` Modülü

> Henüz kodda yok; aşağıdaki model PRD'ye göre **önerilmiştir**.

### 3.1 `ParentProfile` (AggregateRoot)
| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `UserId` | Guid | Identity kullanıcısı (`Parent` rolü) |
| `FullName` | string | |
| `ContactPhone`, `ContactEmail` | string? | |
| `NotificationPreferences` | value object | Bildirim tercihleri (PRD M09: 2.10) |

### 3.2 `ParentChildLink` (Entity) — Veli–öğrenci bağı
| Alan | Tip | Açıklama |
|------|-----|----------|
| `ParentUserId` | Guid | |
| `StudentId` | Guid | |
| `Status` | enum | `Pending`, `Approved`, `Rejected` (onaylı bağ) |
| `LinkedOnUtc` | DateTime | |

**Kural:** Bağ kurulması öğrenci/öğretmen onayı gerektirebilir (KVKK + gizlilik). Bir veli birden çok çocuğa bağlanabilir.

> Not: `StudentProfile.ParentUserId` mevcut tekil alan basit senaryoyu karşılar; çoklu/onaylı bağ için
> ayrı bir `ParentChildLink` tablosu önerilir.

---

## 4. Önerilen API Sözleşmesi — `/api/parents` (Yeni)

```
POST /api/parents/profiles                          → veli profili oluştur
GET  /api/parents/profiles/{userId}                 → profil getir

POST /api/parents/children/link                     → çocuğa bağlanma talebi (öğrenci/öğretmen onayı)
GET  /api/parents/{parentUserId}/children           → bağlı çocuklar

# Birleşik panel (read-model) — çocuk başına
GET  /api/parents/children/{studentId}/dashboard    → haftalık çalışma + son ders + ödev + (varsa) ödeme özeti
GET  /api/parents/children/{studentId}/study        → bireysel çalışma verisi (Study'den)
GET  /api/parents/children/{studentId}/lessons      → öğretmen bağlıysa ders özeti (LessonSessions'tan)

PUT  /api/parents/{parentUserId}/notification-preferences  → bildirim tercihleri
```

> Bu endpoint'ler diğer modüllerden veri toplar. Modül sınırı kuralı gereği (her modül kendi verisine sahiptir),
> veriye **doğrudan DB erişimiyle değil**, application service / integration event ile ulaşılmalıdır
> (bkz. [`../ai_ready_architecture.md`](../ai_ready_architecture.md) — "No direct cross-module DB access").

---

## 5. Görünürlük ve İzin (PRD M08 + M09)

- Öğrenci, bireysel çalışma verilerinin **hangilerini** veliyle paylaşacağını kontrol edebilmeli (gizlilik bayrakları — `Study` modülünde).
- Veli–çocuk bağı **onaya** dayalı olmalı (özellikle büyük yaş grubu öğrenciler için).
- Veli yalnızca **görüntüleme** yetkisine sahiptir; ders/ödev/ödeme verisini düzenleyemez.
- **KVKK:** Reşit olmayan öğrenciler için veli erişimi varsayılan; reşit öğrencilerde öğrenci onayı esas.

---

## 6. Mobil — Veli Deneyimi (Tasarlanacak)

Önerilen ekranlar:
- `parent-onboarding` — veli kaydı + çocuk bağlama (davet kodu / öğrenci e-postası).
- `parent-dashboard` — çocuk seçici + haftalık özet kartları:
  - Bu hafta kaç saat çalıştı
  - Hangi derslere ne kadar zaman ayırdı
  - Test performansı özeti
  - (Öğretmen bağlıysa) yaklaşan dersler, öğretmen mesajları
- `parent-child-detail` — seçili çocuğun detaylı gelişimi.
- `parent-notifications` — bildirim tercihleri.

---

## 7. Kabul Kriterleri

### Faz 2 (öğretmensiz)
- [ ] Veli profili oluşturup çocuğuna bağlanabilir.
- [ ] Veli, çocuğunun bireysel çalışma verilerini (süre, konu dağılımı, test, streak) görebilir.
- [ ] İzin bazlı görünürlük + bildirim tercihleri.

### Faz 3 (öğretmen verisi entegre)
- [ ] Öğretmen bağlıysa veli; son ders özeti, ödevler, öğretmen notları ve ödeme özetini görebilir.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

> ⚠️ **Önkoşul:** Veli panelinin değerli olması için **`Study` modülü (M08) önce inşa edilmeli**
> (bkz. [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md)). Aksi halde gösterecek bireysel veri olmaz.

1. **`Study` modülünü tamamla** (önkoşul).
2. **`Parents` modülü domain'i** — `ParentProfile`, `ParentChildLink`.
3. **Veli–çocuk bağlama akışı** + onay mekanizması.
4. **Birleşik veli dashboard read-model'i** — diğer modüllerden veri toplama (event/service ile).
5. **Görünürlük/izin matrisi** — öğrenci ↔ veli ↔ öğretmen.
6. **Mobil veli ekranları** + rol bazlı navigasyon (`Parent` rolü).
7. **Veli bildirim tercihleri** (Notifications modülüyle entegrasyon).

---

## 9. İlişkili Dokümanlar

- Bireysel çalışma verisinin kaynağı → [`02_ogrenci_modulu.md`](02_ogrenci_modulu.md)
- Öğretmen verisi (ders/ödev/ödeme) → [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md)
- Genel durum → [`00_genel_bakis.md`](00_genel_bakis.md)

---

*Veli Modülü — Detaylı Tasarım | Faz 2-3 | Güncelleme: 2026-06-21*
