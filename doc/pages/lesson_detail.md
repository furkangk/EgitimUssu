---
title: "Ders Detayı Ekranı"
summary: "Tek dersin detayı (Ders Notu/Ödevler/Kaynaklar/Ödeme sekmeleri); tamamlama, düzenleme, ders kaynakları ve ders başlat/iptal gerçek backend'e bağlı, not/ödev sekmeleri demo/UI"
tags: [sayfa, lesson-sessions, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-11-15
---

# Ders Detayı (`/lesson-sessions/detail`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart`
> **State:** Stateful (tab) · **Veri:** Kısmen gerçek (tamamlama/düzenleme/kaynaklar gerçek; not & ödev sekmeleri demo) · **Güncelleme:** 2026-11-15

## Amaç
Tek dersin detayı; sekmeler: **Ders Notu / Ödevler / Kaynaklar / Ödeme**. Ders meta verisi, ekler, ilgili ödevler, ödeme durumu.

## Kaynaklar sekmesi (2026-09-03, P04/M06-1)
`LessonResourcesSection` (`features/assignments/presentation/widgets/`) — **gerçek API**:
- Liste: `GET /api/assignments/lesson-resources?teacherUserId={self}&lessonSessionId={lessonId}`
- Ekleme: "Kaynak ekle" → dosya (`file_picker`, maks. 10 MB) veya bağlantı → `POST .../lesson-resources` (multipart)
- Silme: çöp kutusu → onay diyaloğu → `DELETE .../lesson-resources/{id}` (**yumuşak silme**, iyimser liste güncellemesi; sunucu reddederse geri alınır)
- Açma: bağlantı `url_launcher` ile dış tarayıcıda; dosya yetkili uçtan indirilip geçici dizine yazılır ve yolu SnackBar ile bildirilir.

> Aynı bölüm öğrenci tarafında (`student_lesson_detail_page.dart`) `studentId` ile salt-okunur çalışır: öğrenci yalnız kendisine özel **veya** tüm öğrencilere açık (`studentId = null`) kaynakları görür.

## Route
- Veri `route.extra` ile `LessonDetailPayload` olarak gelir: `studentName`, `subject`, `dateLabel`, `timeLabel`, `modeLabel`, `accent`, `lessonId?`, `lessonStatus?`, `meetingUrl?`, **`lesson?`** (tam `LessonSchedule` — kalıcı düzenleme için).
- Tek sayfa/route'tur (`/lesson-sessions/detail`); hem **Dersler listesi** hem **Ana sayfa** ders kartları aynı sayfayı açar. İkisi de artık tam payload (`lessonId` + `lesson`) gönderir → tamamlama butonu, kalıcı düzenleme ve doğru "Bekliyor/Tamamlandı" durumu **her iki girişte birebir aynı** çalışır. (Önceden ana sayfa ince payload gönderiyordu; `DashboardTodayLesson`/`DashboardUpcomingLesson.lesson` alanı eklenerek giderildi.)

## Davranış
- **Online + `meetingUrl` doluysa** "Toplantıya Katıl" kartı görünür; tıklayınca link panoya kopyalanır (`Clipboard`; URL'yi açmak için ileride `url_launcher` eklenebilir). `meetingUrl` hem dersler listesinden hem **ana sayfa ders kartlarından** taşınır (dashboard: `DashboardTodayLesson`/`DashboardUpcomingLesson` → `meetingUrl`).
- **Dersi Düzenle:** Payload'da tam `lesson` (LessonSchedule) varsa — yani dersler listesinden açıldıysa — ortak **`LessonFormSheet` edit modu** açılır ve `SchedulingCubit.updateLesson` ile **kalıcı** olarak günceller (`PUT /api/scheduling/lessons/{id}`). Edit modunda öğrenci salt-okunur, konu serbest metin, tekrar seçimi gizli; tarih/saat seçicileri + format + online toplantı linki vardır. Kaydedince detay kartı güncel derse göre yenilenir.
- **Kozmetik düzenleme (fallback):** `lesson` yoksa (demo veya dashboard kartından açılan detay) eski sheet kullanılır; yalnızca detay kartını (`_editedPayload`) günceller, kalıcı değildir. Bu sheet'te de tarih/saat seçicileri ve segmented format var (eski `DropdownButtonFormField` kaldırılmıştı — `_dependents.isEmpty` hatası giderildi).
- **Durum rozeti:** Başlık kartında (`_HeroCard`) dersin durumu **her zaman** gösterilir: **Planlandı** (mavi) · **Bekliyor** (amber — başlama saati geçmiş planlı ders) · **Tamamlandı** (yeşil) · **İptal edildi** (kırmızı) · **Taslak** (gri). Durum bilinmiyorsa (payload'da `lessonStatus` yoksa) rozet gizlenir.
- **Dersi Tamamla:** `lessonId != null && status == 'Planned'` ise aksiyon kartlarının altında yeşil "Dersi Tamamla" butonu görünür; `SchedulingCubit.completeLesson` → `POST /api/scheduling/lessons/{id}/complete`. Tamamlanınca durum `Completed` olur ve başlık rozeti **Tamamlandı**'ya döner. Aynı hızlı tamamlama dersler listesinde kart üzerinden de yapılabilir (bkz. [`lesson_sessions_list.md`](lesson_sessions_list.md)).
- **Dersi Başlat / Dersi İptal Et (2026-09-04, P06/M05-1 + M05-2):** `lessonId != null && status == 'Planned'` iken "Dersi Tamamla"nın altında iki kenarlıklı buton çıkar. Ekran bir **`LessonSchedule`** taşıdığı, ders **oturumu** kimliği taşımadığı için akış **iki adımlıdır**: önce `POST /api/lesson-sessions/from-schedule` gövdesinde `{lessonScheduleId, occurrenceStartAtUtc: lesson.occurrenceKeyUtc}` ile oturum türetilir (aynı **oluşum** için idempotent; ertelenmiş saatte de orijinal anahtar gönderilir), dönen `id` ile `POST /api/lesson-sessions/{id}/start` veya `/cancel` çağrılır. İptalde önce **neden** sorulur (opsiyonel serbest metin). Başlatıldıktan sonra "● Ders devam ediyor" göstergesi çıkar ve "Dersi Başlat" gizlenir. Sunucudan **409** gelirse "Bu ders zaten başlamış veya iptal edilmiş." mesajı gösterilir. Bu işlem `_didChange` işaretler → geri dönüşte kaynak ekran tazelenir.
- **Kaynağı tazeleme:** Detayda kalıcı bir değişiklik (tamamlama veya kalıcı düzenleme) olursa `_didChange` işaretlenir ve geri dönülürken `PopScope` ile pop sonucu `true` döndürülür. Kaynak ekranlar (dersler listesi → `SchedulingCubit.loadForCalendar`, ana sayfa → `DashboardCubit.load`) bu sonucu bekleyip **otomatik yenilenir**. Değişiklik yoksa (yalnızca görüntüleme) yenileme yapılmaz.

## Yerelleştirme (P17 T3)
Detay ekranının başlıkları, düğmeleri, durumları, diyalogları ve mevcut demo içerik metinleri `lessonDetail_*` ARB anahtarlarından Türkçe/İngilizce okunur. Tarih biçimleme `intl.DateFormat` ile uygulamanın dilini izler. Demo not/ödev/ödeme içeriğinin gerçek veriye taşınması P20 kapsamındadır.

## Veri / API
- Ders notu/ödev/ödeme sekmeleri ⚠️ demo/UI. **Tamamlama ve düzenleme** ise `SchedulingCubit` üzerinden gerçek backend'e bağlı (`lesson`/`lessonId` doluysa): `POST /api/scheduling/lessons/{id}/complete`, `PUT /api/scheduling/lessons/{id}`.
- **Ders oturumu yaşam döngüsü** doğrudan `LessonSessionRepository` ile çağrılır (cubit yok, sayfa yerel `_sessionBusy`/`_sessionStatus` durumu tutar): `createSessionFromSchedule` → `startSession` / `cancelSession`.

## İlgili
- Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05/M06)
