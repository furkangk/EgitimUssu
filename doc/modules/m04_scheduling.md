# 📅 Takvim & Planlama (M04) — Detaylı Tasarım Dokümanı

> **PRD: M04 Takvim & Planlama** · **Faz: 1 — Öğretmen Çekirdeği (MVP)** · **Durum: 🟢 Yazıldı (çekirdek), ⚠️ genişletme bekliyor**
>
> **Amaç:** Öğretmenin **her gün** açtığı **merkezi ekranı**: takvim. Öğretmen derslerini tek seferlik
> veya tekrarlı olarak planlar; ders türünü (online / yüz yüze / hibrit) seçer; çakışmalar engellenir;
> yaklaşan ders için hatırlatma planlanır. Takvim ileride **dersler + ödevler + tatiller + ödemeler**i
> tek bir görünümde toplayacak şekilde tasarlanmıştır.
>
> İlgili: [`../roles/ogretmen.md`](../roles/ogretmen.md) (takvim öğretmenin merkezi aracı) ·
> [`../roles/ogrenci.md`](../roles/ogrenci.md) (öğrenci kendi planı + öğretmen takvimi) ·
> [`m05_lesson_sessions.md`](m05_lesson_sessions.md) (planlı dersten oturum türetme) ·
> [`m11_notifications.md`](m11_notifications.md) (hatırlatma) · [`00_genel_bakis.md`](00_genel_bakis.md)

---

## 1. Mevcut Durum (Koddan Doğrulanmış)

| Katman | Durum | Kanıt |
|--------|-------|-------|
| Domain (`LessonSchedule`) | ✅ Mevcut | `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` |
| Application (CQRS + handler) | ✅ Mevcut | `src/Modules/Scheduling/Application/LessonScheduleFeatures.cs` |
| API endpoint'leri | ✅ Mevcut (4 endpoint) | `src/Modules/Scheduling/API/SchedulingModule.cs` |
| Infrastructure (DbContext + repo + migration) | ✅ Mevcut | `src/Modules/Scheduling/Infrastructure/*` |
| Çakışma kontrolü (aynı öğretmen) | ✅ **Mevcut** | `HasTeacherConflictAsync` → `scheduling.teacher_conflict` (409) |
| Hatırlatma planlama | ✅ Mevcut | `ILessonScheduleNotificationService.ScheduleReminderAsync` |
| Mobil takvim ekranı | ✅ Mevcut | `mobile/lib/features/scheduling` (`syncfusion_flutter_calendar`) |
| Online ders linki (`MeetingUrl`) | 🔴 **Yok** | Önerilen — bkz. §2.2 |
| Tatil / blackout (`ScheduleException`) | 🔴 **Yok** | Önerilen — bkz. §2.2 |
| Tekrar kuralı (`RecurrenceRule`) **açılımı** | 🔴 **Alan var, mantık yok** | Alan saklanıyor ama somut tekrar üretimi yok |
| Ders güncelleme (`PUT /lessons/{id}`) | 🔴 **Yok** | Yalnızca oluştur + iptal var |
| `Planned → Completed` geçişi | 🔴 **Yok** | Enum değeri var, davranış yok |

> **Önemli düzeltme:** Önceki dokümanda "çakışma kontrolü eksik" yazıyordu; **kodda `HasTeacherConflictAsync`
> ile uygulanmıştır** ve `POST /lessons` sırasında devrededir. Eksik olan, öğrenci tarafındaki **öncelik
> kuralı** (özel ders > bireysel plan) ve görsel uyarıdır.

---

## 2. Domain Modeli

### 2.1 🟢 Mevcut (koddan) — `LessonSchedule` (AggregateRoot<Guid>)

`src/Modules/Scheduling/Domain/SchedulingDomainModel.cs`

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | Ders planı kimliği |
| `TeacherUserId` | `Guid` | Dersi planlayan öğretmenin kullanıcı kimliği |
| `StudentId` | `Guid` | Hedef öğrenci profil kimliği |
| `Subject` | `string` | Konu / branş |
| `LessonFormat` | enum `ScheduledLessonFormat` | `InPerson=1`, `Online=2`, `Hybrid=3` |
| `StartAtUtc` | `DateTime` | Başlangıç (UTC) |
| `EndAtUtc` | `DateTime` | Bitiş (UTC) |
| `TimeZone` | `string` | IANA zaman dilimi (gösterim için) |
| `RecurrenceRule` | `string?` | Tekrar kuralı (RRULE benzeri metin) — **yalnızca saklanır** |
| `Status` | enum `LessonScheduleStatus` | `Draft=1`, `Planned=2`, `Cancelled=3`, `Completed=4` |
| `ReminderOffsetMinutes` | `int` | Hatırlatmanın dersten kaç dk önce gönderileceği |
| `LocationLabel` | `string?` | Konum etiketi (yüz yüze için adres/etiket) |
| `Notes` | `string?` | Serbest not (iptal notu da buraya eklenir) |
| `CreatedOnUtc` | `DateTime` | Oluşturma |
| `UpdatedOnUtc` | `DateTime` | Son güncelleme (iptalde güncellenir) |

**Davranışlar (kodda):**
- **Constructor** → durum dışarıdan verilir; `CreateLessonScheduleCommandHandler` her zaman `LessonScheduleStatus.Planned` ile çağırır. Oluşturmada `LessonScheduledDomainEvent` yayılır.
- `Cancel(cancellationNote, updatedOnUtc)` → `Status = Cancelled`, not eklenir (varsa mevcut nota satır eklenir), `LessonScheduleCancelledDomainEvent` yayılır.

**Enum'lar (koddan birebir):**
```
ScheduledLessonFormat : InPerson = 1, Online = 2, Hybrid = 3
LessonScheduleStatus  : Draft = 1, Planned = 2, Cancelled = 3, Completed = 4
```

**Domain Event'ler (koddan birebir):**
| Event | Alanlar |
|-------|---------|
| `LessonScheduledDomainEvent` | `LessonScheduleId, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, CreatedOnUtc` |
| `LessonScheduleCancelledDomainEvent` | `LessonScheduleId, TeacherUserId, StudentId, CancelledOnUtc` |

### 2.2 ⚠️ Önerilen (henüz kodda yok)

Aşağıdakiler `promp.txt` ve [`../roles/ogretmen.md`](../roles/ogretmen.md) hedeflerinden türetilmiştir; **kodda yoktur**.

#### A) `LessonSchedule`'a eklenecek alanlar
| Alan (öneri) | Tip | Gerekçe |
|--------------|-----|---------|
| `MeetingUrl` | `string?` | **Online ders linki.** `LessonFormat = Online/Hybrid` olduğunda öğrenci bu linkle derse katılır (`promp.txt`: "Online için link girer öğrenciler o linkden derse giriş yapar"). |
| `MeetingProvider` | `string?` | Zoom/Meet/Teams/diğer (görüntüleme/ikon için). |
| `SeriesId` | `Guid?` | Tekrarlı seriye ait örnek (instance) ise serinin kimliği — açılım için. |
| `CancelledOnUtc` | `DateTime?` | İptal zamanını ayrı tutmak (şu an yalnızca `UpdatedOnUtc`). |

#### B) `ScheduleException` / `Holiday` (yeni Aggregate — tatil / blackout)
Öğretmenin **müsait olmadığı** gün/aralıkları işaretler. Takvimde "tatil" bloğu olarak görünür; bu aralıkta ders planlama uyarı verir.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `Id` | `Guid` | |
| `TeacherUserId` | `Guid` | Sahibi |
| `Title` | `string` | "Resmî tatil", "İzin", "Yurt dışı" vb. |
| `StartAtUtc`, `EndAtUtc` | `DateTime` | Blackout aralığı |
| `IsAllDay` | `bool` | Tüm gün mü |
| `Kind` | enum (öneri) | `Holiday`, `PersonalBlock`, `Break` |
| `Notes` | `string?` | |

#### C) `LessonScheduleSeries` (tekrarlı ders açılımı)
`RecurrenceRule` şu an yalnızca metin olarak saklanıyor; **somut örnekler üretilmiyor**. Öneri: bir seri oluşturulduğunda kural çözümlenip ufuk (örn. 8–12 hafta) kadar `LessonSchedule` örneği materyalize edilir. Her örnek `SeriesId` taşır; "tek örneği iptal et" / "tüm seriyi iptal et" ayrımı yapılabilir.

> **Tasarım kararı:** Materyalize yaklaşım (örnekleri önceden üret) çakışma kontrolü, hatırlatma ve takvim
> sorgularını basitleştirir. Alternatif (uçuş anında genişletme) daha az satır ama daha karmaşık sorgu demektir.

---

## 3. API Sözleşmesi

> Tüm endpoint'ler `group.RequireAuthorization("AuthenticatedUser")` ile korunur ve `Result<T>` döner.
> Hata kodu → HTTP eşlemesi `SchedulingModule.ToHttpResult` içinde yapılır.

### 3.1 ✅ Mevcut Endpoint'ler

| Yetenek | Method + Route | İstek / Yanıt | Notlar |
|---------|----------------|---------------|--------|
| Ders planla | `POST /api/scheduling/lessons` | `CreateLessonScheduleRequest` → `LessonScheduleResponse` | Çakışma/aralık kontrolü uygulanır; durum `Planned` set edilir; hatırlatma planlanır |
| Ders iptal | `POST /api/scheduling/lessons/{lessonId}/cancel` | `CancelLessonScheduleRequest { CancellationNote? }` → `LessonScheduleResponse` | `Cancel()` → event + hatırlatma iptali |
| Ders getir | `GET /api/scheduling/lessons/{lessonId}` | → `LessonScheduleResponse` | Yoksa `404 scheduling.lesson_not_found` |
| Takvim (aralık) | `GET /api/scheduling/teachers/{teacherUserId}/lessons?startAtUtc=&endAtUtc=` | → `LessonScheduleResponse[]` | **Tarih aralığı filtresi MEVCUT**; sonuç `StartAtUtc` artan sıralı |

**`CreateLessonScheduleRequest` (koddan):** `TeacherUserId, StudentId, Subject, LessonFormat, StartAtUtc, EndAtUtc, TimeZone, RecurrenceRule?, ReminderOffsetMinutes, LocationLabel?, Notes?`

**`LessonScheduleResponse` (koddan):** `Id, TeacherUserId, StudentId, Subject, LessonFormat (string), StartAtUtc, EndAtUtc, TimeZone, RecurrenceRule?, Status (string), ReminderOffsetMinutes, LocationLabel?, Notes?, CreatedOnUtc, UpdatedOnUtc`
> Not: Enum'lar yanıtta **string** olarak döner (`LessonFormat.ToString()`, `Status.ToString()`).

**Hata kodu → HTTP eşlemesi (koddan):**
| Kod | HTTP | Anlam |
|-----|------|-------|
| `scheduling.teacher_conflict` | `409` | Öğretmenin bu aralıkta başka dersi var |
| `scheduling.invalid_range` | `400` | `EndAtUtc <= StartAtUtc` |
| `scheduling.lesson_not_found` | `404` | Ders planı yok |
| `shared.forbidden` | `403` | Yetki yok |
| (varsayılan) | `400` | Diğer doğrulama hataları |

### 3.2 ⚠️ Eksik / Önerilen Endpoint'ler

| Yetenek | Öneri | Gerekçe |
|---------|-------|---------|
| Ders güncelle | `PUT /api/scheduling/lessons/{lessonId}` | Saat/konu/format/`MeetingUrl`/not değişikliği. Şu an yalnızca oluştur+iptal var. Güncellemede çakışma yeniden kontrol edilmeli + hatırlatma yeniden planlanmalı |
| Tamamlandı işaretle | `POST /api/scheduling/lessons/{lessonId}/complete` | `Planned → Completed` geçişi (enum'da `Completed=4` var, davranış yok). Ya da bu, M05 oturum tamamlama ile köprülenir (bkz. §5) |
| Tatil ekle/listele | `POST /api/scheduling/holidays`, `GET /api/scheduling/teachers/{id}/holidays` | `ScheduleException` için |
| Seri oluştur | `POST /api/scheduling/lessons/series` | `RecurrenceRule` açılımı + `SeriesId` üretimi |
| Seri iptal | `POST /api/scheduling/lessons/series/{seriesId}/cancel?scope=instance|all` | Tek örnek / tüm seri |
| Öğrenci takvimi | `GET /api/scheduling/students/{studentId}/lessons?from=&to=` | Öğrenci kendi özel ders takvimini görsün (m08 birleşik takvim için) |

---

## 4. İş Kuralları

1. **Geçerli aralık (🟢 kodda):** `EndAtUtc > StartAtUtc` zorunlu; aksi halde `scheduling.invalid_range`.
2. **Çakışma engeli (🟢 kodda):** Aynı `TeacherUserId` için zaman aralığı çakışan yeni ders oluşturulamaz → `scheduling.teacher_conflict` (409). Çakışma `HasTeacherConflictAsync` ile sorgulanır.
3. **Oluşturmada durum (🟢 kodda):** Yeni ders her zaman `Planned` ile başlar (handler `Draft` kabul etmez; `Draft` ileride taslak akışı için ayrılmıştır).
4. **İptal davranışı (🟢 kodda):** `Cancel()` durumu `Cancelled` yapar, iptal notunu mevcut nota satır olarak ekler, event yayar, hatırlatmayı iptal eder.
5. **Hatırlatma (🟢 kodda):** Oluşturmada `ReminderOffsetMinutes` ile hatırlatma planlanır (m11); iptalde geri alınır.
6. **⚠️ Öğrenci tarafı öncelik kuralı:** Öğrencinin **kendi bireysel planı** (m08 Study) ile öğretmenle yapılan **özel ders** çakışırsa **özel ders önceliklidir**; öğrenci uyarılır (`promp.txt`: "öncelik özel dersindir öğrenciyi çakışma olduğunda uyarır"). Bu kural m08'de uygulanmalı; Scheduling yetkili kaynaktır.
7. **⚠️ Online ders linki:** `LessonFormat = Online/Hybrid` ise `MeetingUrl` istenmeli; öğrenci linkle katılır.
8. **⚠️ Tatil çakışması:** Bir `ScheduleException` aralığına ders planlanırken uyarı verilmeli (sert engel değil, esnek uyarı önerilir).
9. **⚠️ Güncellemede yeniden kontrol:** `PUT` ile saat değişirse çakışma yeniden değerlendirilmeli ve hatırlatma yeniden planlanmalı.
10. **Sahiplik (yetki):** Öğretmen yalnızca kendi derslerini görüp düzenleyebilir (`LessonSchedulePolicies.cs`); ihlalde `shared.forbidden`.

---

## 5. Olay Akışı (Event-Driven)

```
POST /lessons (Planned)
   → LessonScheduledDomainEvent
       → m11 Notifications: ReminderOffsetMinutes ile hatırlatma planlanır (ILessonScheduleNotificationService)
       → (öneri) m08 Study: öğrencinin birleşik takvimine "özel ders" olarak yansıtılır
       → (öneri) m09 Parents: veliye "yeni ders planlandı" bildirimi

POST /lessons/{id}/cancel
   → LessonScheduleCancelledDomainEvent
       → m11 Notifications: hatırlatma iptal
       → (öneri) m08/m09: öğrenci/veli bilgilendirme

(öneri) Planlı ders → ders günü
   → M05 LessonSession türetilir (LessonScheduleId ile bağ) — bkz. m05_lesson_sessions.md §5
   → Oturum tamamlanınca (öneri) LessonSchedule.Status = Completed güncellenir
```

> Olaylar **Outbox** ile güvenilir yayılır (`Shared/Infrastructure/Messaging`).

---

## 6. Mobil Ekranlar

### ✅ Mevcut
| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/scheduling` | `SchedulingPage` | `syncfusion_flutter_calendar` ile gün/hafta/ay görünümü; ders ekle/iptal |

> Durum yönetimi `flutter_bloc` (Cubit). `mobile/lib/features/scheduling`.

### ⚠️ Planlanan
- **Birleşik takvim katmanları:** dersler + ödevler (m06 son teslim) + tatiller (`ScheduleException`) + ödeme vadeleri (m07) aynı takvimde renk kodlu.
- **Çakışma görsel uyarısı:** kayıt öncesi/eş zamanlı 409 dönüşünde kullanıcıya net uyarı.
- **Online ders kartı:** `MeetingUrl` "Derse Katıl" butonu (öğretmen + öğrenci tarafı).
- **Tekrarlı ders formu:** "her hafta Salı 18:00" gibi kural seçici + seri önizleme.
- **Tatil/izin ekleme** ekranı.
- **Öğrenci takvimi görünümü** (m08 ile birleşik).

---

## 7. Kabul Kriterleri

- [x] Öğretmen tek seferlik ders planlayabilir (konu, taraflar, format, saat, zaman dilimi).
- [x] Geçersiz aralık reddedilir (`scheduling.invalid_range`).
- [x] Aynı öğretmende çakışan ders engellenir (`scheduling.teacher_conflict`).
- [x] Ders iptal edilebilir ve hatırlatma geri alınır.
- [x] Tarih aralığıyla takvim listesi alınabilir.
- [ ] ⚠️ Ders güncelleme (`PUT /lessons/{id}`) + güncellemede çakışma/hatırlatma yeniden değerlendirme.
- [ ] ⚠️ Online ders linki (`MeetingUrl`) uçtan uca (öğretmen girer, öğrenci katılır).
- [ ] ⚠️ Tekrarlı ders **açılımı** (seri materyalizasyonu + tekil/tüm seri iptali).
- [ ] ⚠️ Tatil / blackout (`ScheduleException`) ve planlamada uyarı.
- [ ] ⚠️ Öğrenci tarafı öncelik kuralı (özel ders > bireysel plan, m08).
- [ ] ⚠️ `Planned → Completed` geçişi (M05 ile köprü).

---

## 8. Eksikler ve Yapılacaklar

> Öncelik sırasıyla:

1. **Ders güncelleme + yeniden çakışma kontrolü** (`PUT /lessons/{id}`).
2. **`MeetingUrl` / online ders linki** — domain alanı + DTO + mobil "Derse Katıl".
3. **Tekrarlı ders açılımı** — `RecurrenceRule` çözümleyici + `SeriesId` + seri iptali.
4. **`ScheduleException` (tatil/blackout)** — aggregate + endpoint + takvim katmanı + planlama uyarısı.
5. **Öğrenci öncelik kuralı (m08 entegrasyonu)** — çakışmada özel ders önceliği + öğrenci uyarısı.
6. **`Planned → Completed` geçişi** — M05 oturum tamamlanınca planı kapatma köprüsü.
7. **Birleşik takvim** (dersler+ödev+tatil+ödeme) — backend toplayıcı veya mobil çoklu kaynak.

---

## 9. İlişkili Dokümanlar

- Rol bağlamı → [`../roles/ogretmen.md`](../roles/ogretmen.md) · [`../roles/ogrenci.md`](../roles/ogrenci.md) · [`../roles/veli.md`](../roles/veli.md)
- Planlı dersten oturum → [`m05_lesson_sessions.md`](m05_lesson_sessions.md)
- Hatırlatma → [`m11_notifications.md`](m11_notifications.md)
- Öğrenci bireysel plan + öncelik → [`m08_study.md`](m08_study.md)
- Veli takip → [`m09_parents.md`](m09_parents.md)
- Veri modeli (ER) → [`veri_modeli.md`](veri_modeli.md) · Mimari → [`mimari_inceleme.md`](mimari_inceleme.md)
- Genel bakış → [`00_genel_bakis.md`](00_genel_bakis.md) · PRD → [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD_v2.md) · UI → [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md)

---

*Takvim & Planlama (M04) — Detaylı Tasarım | Güncelleme: 2026-06-24*
