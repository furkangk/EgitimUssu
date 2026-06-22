# 🎓 Öğrenci Modülü — Detaylı Tasarım Dokümanı

> **Öncelik: 2️⃣** · **Faz 2 — Öğrenci Bireysel Çalışma** · **Durum: 🟡 Kısmen (profil var, bireysel çalışma yok)**
>
> **Amaç:** Öğrenci kendi derslerini/çalışmasını yönetebilsin; öğretmen-öğrenci ilişkisi kurulsun.
> Öğrenci platforma **iki yoldan** girebilir: (a) öğretmen tarafından eklenerek, (b) doğrudan kayıt olarak.

> **Tasarım ilkesi (PRD §2.1):** Öğrenci ve veli, platforma öğretmenden **ÖNCE** girebilir.
> Bireysel çalışma takibi (M08) platformun büyüme motorlarından biridir — öğretmen gerektirmeden tam işlevseldir
> ve eşleştirme modülüne (Faz 4) hazır bir öğrenci havuzu oluşturur.

---

## 1. Kapsam — İki Farklı Öğrenci Senaryosu

| Senaryo | `StudentOrigin` | `UserId` | `CreatedByTeacherUserId` | Açıklama |
|---------|-----------------|----------|--------------------------|----------|
| Öğretmen ekledi | `TeacherManaged` | null (başta) | set | Öğretmenin yönettiği öğrenci. Sonradan öğrenci hesabıyla eşleşebilir. |
| Öğrenci kendi kaydoldu | `SelfRegistered` | set | null | Bağımsız öğrenci; bireysel çalışma için. Sonradan öğretmene bağlanabilir. |

### Öğrencinin yetenek haritası

| Yetenek | Backend Modülü | Mobil Feature | PRD | Durum |
|---------|----------------|---------------|-----|-------|
| Kayıt / giriş (Student rolü) | `Identity` | `auth` | M01 | 🟢 |
| Öğrenci profili | `Students` | `students` | M03 | 🟢 (öğretmen tarafı) / 🟡 (self-register akışı) |
| Bireysel çalışma sayacı | `Study` | _(yok)_ | M08 | 🔴 İskelet |
| Test/sınav performansı | `Study` | _(yok)_ | M08 | 🔴 İskelet |
| Streak / hedef / motivasyon | `Study` | _(yok)_ | M08 | 🔴 İskelet |
| Ders geçmişi & ödevleri görme | `LessonSessions`, `Assignments` | _(öğrenci görünümü yok)_ | M05/M06 | 🟡 |
| Gelişim takibi | `ProgressTracking` | _(yok)_ | M10 | 🔴 İskelet |

---

## 2. Mevcut Durum (Koddan Doğrulanmış)

### ✅ Var olan
- `StudentProfile` domain modeli — `UserId?`, `CreatedByTeacherUserId?`, `ParentUserId?`, `Origin` ile **her iki senaryoyu da destekliyor** (`src/Modules/Students/Domain/StudentsDomainModel.cs`).
- API: `POST /api/students/profiles`, `GET .../by-user/{userId}`, `GET .../by-teacher/{teacherUserId}`, `GET .../{studentId}`.
- Mobil: `students` feature (liste + detay) — ancak bu **öğretmenin** öğrenci yönetim ekranıdır.

### 🔴 Eksik olan (Faz 2'nin çekirdeği)
- **`Study` modülü tamamen iskelet** — sadece `StudyDbContext` + DI + `GET /api/study/status`. Domain, feature, migration **yok**.
- **Öğrenci doğrudan kayıt akışı** (öğretmensiz onboarding) mobilde yok.
- **Öğrenci rolü için mobil deneyim** yok — mevcut mobil app öğretmen odaklı (`students`, `scheduling`, `payments` öğretmen ekranları).
- Öğrencinin **kendi ders geçmişini/ödevlerini** gördüğü ekranlar yok.

---

## 3. Tasarlanması Gereken Domain Modeli — `Study` Modülü (M08)

> Aşağıdaki model PRD §M08'e göre **önerilmiştir**; henüz kodda yoktur.
> Mevcut diğer modüllerin desenini (AggregateRoot + Entity + Enum + DomainEvent) takip eder.

### 3.1 `StudySession` (AggregateRoot) — Çalışma seansı / sayaç
| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | Guid | |
| `StudentId` | Guid | Öğrenci profili |
| `Subject` / `Topic` | string | Çalışılan ders/konu |
| `StartedAtUtc` | DateTime | Sayaç başlangıcı |
| `EndedAtUtc` | DateTime? | Bitiş |
| `EffectiveMinutes` | int | **Mola hariç** net süre |
| `BreakMinutes` | int | Toplam mola süresi |
| `Status` | enum | `Running`, `Paused`, `Completed`, `Discarded` |
| `PersonalNote` | string? | Seans notu |

**Kurallar:** Mola süresi toplam net süreye eklenmez (PRD M08). `Pause()/Resume()/Complete()` davranışları.
**Event:** `StudySessionEndedEvent` (mimari dokümanında zaten öngörülmüş).

### 3.2 `TestResult` (AggregateRoot) — Test/sınav performansı
| Alan | Tip | Açıklama |
|------|-----|----------|
| `StudentId`, `Subject`, `Topic` | — | |
| `TotalQuestions`, `Correct`, `Wrong`, `Blank` | int | |
| `Net` | decimal | Hesaplanan net (örn. `Correct - Wrong/4`) |
| `TakenOnUtc` | DateTime | |

**Kural:** `Correct + Wrong + Blank == TotalQuestions`. Net formülü konfigüre edilebilir olmalı.

### 3.3 `StudyGoal` / `StudyStreak` — Hedef & motivasyon
- `DailyGoalMinutes`, `TargetNet`, `TargetScore` (hedef tanımları).
- Streak: ardışık çalışılan gün sayısı, kişisel rekorlar.

### 3.4 Gizlilik (PRD M08 — "Veli ile Paylaşım")
- Öğrenci belirli verileri veliye/öğretmene karşı gizleyebilmeli (`Visibility` bayrakları).
- Veriler öğretmen bağlıysa öğretmenle, veli bağlıysa veliyle paylaşılır.

---

## 4. Önerilen API Sözleşmesi — `/api/study` (Yeni)

```
POST   /api/study/sessions/start            → sayaç başlat
POST   /api/study/sessions/{id}/pause       → mola
POST   /api/study/sessions/{id}/resume      → devam
POST   /api/study/sessions/{id}/complete    → bitir (özet döner)
GET    /api/study/students/{studentId}/sessions          → seans geçmişi
GET    /api/study/students/{studentId}/weekly-summary    → haftalık özet (süre + konu dağılımı)

POST   /api/study/test-results              → test girişi
GET    /api/study/students/{studentId}/test-results?subject=  → konu bazlı performans

GET    /api/study/students/{studentId}/streak            → streak + günlük hedef durumu
PUT    /api/study/students/{studentId}/goals             → hedef belirle
```

Öğrencinin öğretmen tarafındaki verisini görmesi için (mevcut modüllere **öğrenci görünümü** eklenmeli):
```
GET /api/lesson-sessions/students/{studentId}             → öğrencinin ders geçmişi   (eklenecek)
GET /api/assignments/students/{studentId}                 → öğrencinin ödevleri        (eklenecek)
```

---

## 5. Öğretmen–Öğrenci İlişkisi (İki Tarafın Birleşmesi)

Bu modülün kritik işi, öğretmen tarafıyla (bkz. [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md)) öğrenci tarafını bağlamaktır.

### 5.1 Bağ kurma senaryoları
1. **Öğretmen ekledi → öğrenci sonradan hesap açtı:** Davet kodu / e-posta eşleşmesiyle `StudentProfile.UserId` doldurulur.
2. **Öğrenci bağımsızdı → öğretmene bağlandı:** Eşleştirme (Faz 4) veya öğretmenin davetiyle ilişki kurulur.
3. **Veli bağlama:** `StudentProfile.ParentUserId` set edilir (bkz. [`03_veli_modulu.md`](03_veli_modulu.md)).

### 5.2 Eksik akışlar ⚠️
- [ ] **Davet / eşleşme mekanizması** — Öğretmenin eklediği öğrenciyi gerçek öğrenci hesabına bağlama.
- [ ] `StudentProfile` üzerinde `UserId`/`ParentUserId` bağlama komutları (`Link` davranışları) — şu an domain'de yalnızca constructor var, güncelleme davranışı yok.
- [ ] Veri görünürlüğü/izin matrisi (öğrenci ↔ öğretmen ↔ veli).

---

## 6. Mobil — Öğrenci Deneyimi (Tasarlanacak)

Mevcut mobil app öğretmen odaklıdır. Öğrenci için **rol bazlı ayrı navigasyon** gerekir.

Önerilen öğrenci ekranları:
- `student-onboarding` — öğretmensiz hızlı kayıt (sınıf, hedef branşlar).
- `study-timer` — konu seç → başlat/mola/bitir → seans özeti.
- `study-history` — geçmiş seanslar + haftalık süre grafiği.
- `test-entry` / `test-performance` — test girişi + konu bazlı net grafiği.
- `goals-streak` — günlük hedef, streak, motivasyon.
- `my-lessons` — (öğretmen bağlıysa) ders geçmişi + ödevler.

> Rol bazlı yönlendirme: `app_router.dart`'taki `redirect` mantığı, kullanıcı rolüne (`Teacher`/`Student`) göre
> farklı dashboard'a yönlendirecek şekilde genişletilmeli.

---

## 7. Kabul Kriterleri (Faz 2 Çıktısı)

PRD §Faz 2: "Öğrenci kendi çalışmalarını takip eder, veli çocuğunun gelişimini görür. Öğretmen gerekmez."

- [ ] Öğrenci öğretmensiz kayıt olabilir (`SelfRegistered`).
- [ ] Çalışma sayacı: konu seç, başlat/durdur/bitir, mola desteği.
- [ ] Çalışma seansı kaydı + geçmiş listesi.
- [ ] Haftalık çalışma süresi özeti.
- [ ] Test/sınav girişi + net hesabı + konu bazlı takip.
- [ ] Streak + günlük hedef sistemi.
- [ ] Öğretmen bağlıysa öğrenci kendi ders geçmişini/ödevlerini görebilir.

---

## 8. Eksikler ve Yapılacaklar (Öncelik Sırasıyla)

1. **`Study` modülünü sıfırdan inşa et** — Domain (`StudySession`, `TestResult`, hedef/streak) + CQRS + API + migration.
2. **Öğrenci self-registration akışı** — Identity'de `Student` rolüyle kayıt + `StudentProfile (SelfRegistered)` oluşturma.
3. **Rol bazlı mobil navigasyon** — öğretmen/öğrenci dashboard ayrımı.
4. **Öğretmen-öğrenci bağ kurma** — davet/eşleşme + `StudentProfile.Link*` davranışları.
5. **Öğrenci görünümü endpoint'leri** — ders geçmişi & ödevler (LessonSessions/Assignments modüllerine ekleme).
6. **Mobil öğrenci ekranları** — sayaç, test, geçmiş, hedef.

---

## 9. İlişkili Dokümanlar

- Öğretmen tarafı → [`01_ogretmen_modulu.md`](01_ogretmen_modulu.md)
- Veli paneli (öğrenci verisini tüketir) → [`03_veli_modulu.md`](03_veli_modulu.md)
- Eşleştirme (öğrenci havuzunu kullanır) → [`04_eslestirme_ve_degerlendirme.md`](04_eslestirme_ve_degerlendirme.md)

---

*Öğrenci Modülü — Detaylı Tasarım | Faz 2 | Güncelleme: 2026-06-21*
