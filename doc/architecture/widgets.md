---
title: "Ortak Widget Kataloğu (Shared Widgets)"
summary: "Tüm ekranlarda aynı kuralla kullanılması gereken paylaşılan Flutter bileşenlerinin tek listesi: amaç, API, varyant, kullanım kuralı ve durum"
tags: [mimari, widget, flutter]
authority: code
code_refs:
  - mobile/lib/shared/widgets/**
updated: 2026-09-04
---

# 🧩 Ortak Widget Kataloğu (Shared Widgets)

> **Kapsam:** Tüm ekranlarda **aynı kuralla** kullanılması gereken paylaşılan Flutter bileşenlerinin tek listesi:
> amaç, API, varyant, kullanım kuralı ve **durum**. Atomic Design'da bu katman = **moleküller** (atom/token →
> [`design_system.md`](design_system.md), organizma/ekran → [`mobile_flutter.md`](mobile_flutter.md) §13).
>
> **Otorite:** Bir widget kodda varsa **kod doğruluk kaynağıdır** (sınıf adı, parametreler buradan alınır). Token
> değerleri → [`design_system.md`](design_system.md). Karmaşık widget'ın derin tasarımı kendi dosyasındadır (ör. tab → [`../tab_widget.md`](../tab_widget.md)).
>
> **Güncelleme:** 2026-09-04 (P10 Task 4 / D-08: §2c mesajlaşma bileşenleri — `MessagingHeader`/`ConversationTile`/`MessageBubble`/`MessageComposer`; `flutter_chat_ui` kullanılmadı ve pubspec'ten kaldırıldı). Önceki not — 2026-08-20 (kod-drift düzeltmesi: StudentBottomNav girişi gerçek 4-sekmeye/rotaya güncellendi (🟢), `StudentPlaceholderPage` yok; AppTextField API'sine `textCapitalization` eklendi)

---

## Nasıl kullanılır

- **Yeni ekran yaparken:** Önce bu kataloğa bak — ihtiyacın olan widget 🟢 ise **import et, yeniden yazma**.
- **🔴/🟡 ise:** Buradaki API + kurala göre `mobile/lib/shared/widgets/` altında oluştur/tamamla, sonra durumu güncelle.
- **Tutarlılığı zorlayan şey kod tekrarıdır:** Bir bileşen iki sayfada farklı yazılmaz; tek `shared/widgets/` sürümü kullanılır.

**Durum:** 🟢 kodda mevcut · 🟡 kısmen (varyant/eksik) · 🔴 planlanan (henüz kod yok).
**Konum:** Mevcut olanlar `mobile/lib/shared/widgets/`.

> ⚠️ **Ad uyumu:** Tasarım metinlerinde kısa takma adlar geçer (`AppButton`, `EmptyState`); **koddaki gerçek adlar**
> farklı olabilir (`AppPrimaryButton`, `EmptyStateView`). Kod adları esastır — aşağıda "Kod (sınıf @ dosya)" sütununda.

---

## 1. Temel / Atomik Widget'lar

| Katalog | Kod (sınıf @ dosya) | API (özet) | Kural / Varyant | Durum |
|---------|---------------------|------------|-----------------|-------|
| AppButton | `AppPrimaryButton` @ `app_primary_button.dart` | `label, onPressed, isLoading` | Tam genişlik `FilledButton`; loading'de spinner. **Sadece primary var**; outline/danger/icon/small **yok** | 🟡 |
| AppTextField | `AppTextField` @ `form_fields.dart` | `controller, labelText, hintText?, validator?, keyboardType?, textCapitalization?, minLines/maxLines, maxLength?, readOnly, onTap?, suffixIcon?` | Üstte `AppFieldLabel` + `TextFormField`; radius 18 (`appInputDecoration`) | 🟢 |
| AppDropdownField | `AppDropdownField<T>` @ `form_fields.dart` | `value, labelText, items, onChanged` | Label + `DropdownButtonFormField` | 🟢 |
| AppDateTimeField | `AppDateTimeField` @ `form_fields.dart` | `controller, labelText, validator, hintText?` | `AppTextField` + takvim ikonu | 🟢 |
| AppFieldLabel | `AppFieldLabel` @ `form_fields.dart` | `text` | Form alanı etiketi (w700) | 🟢 |
| AppCard | _(yok)_ | `child, padding?` | Beyaz zemin + `border` + `softShadow`, radius 16, padding 14-16 | 🔴 |
| AppHeader | `AppPageHeader` @ `app_page_header.dart` | `title, subtitle?, trailing?` | Tüm ana ekranların ortak başlığı: sol başlık (+alt başlık), sağda bildirim zili. Zil her zaman `/notifications`'a gider; rozet global `NotificationsCubit.state.unreadCount`'tan gelir (`context.select`). Renkler `AppColors` token'larından (sabit kodlu **değil**). Tek tanım → tüm sayfalara yansır. Geri/menü varyantı henüz yok | 🟡 |
| AppBottomNav | `AppBottomNav` @ `app_bottom_nav.dart` | `current` (`AppNavTab`) | Tüm ana ekranların ortak alt navigasyon menüsü (master page): 6 sabit sekme (Ana sayfa/Dersler/Öğrenciler/Takvim/Finans/Diğer), ikonlar + etiketler + hedef rotalar tek yerde. Sayfa yalnızca aktif sekmeyi (`AppNavTab`) bildirir; widget `context.go` ile yönlendirir. Aktif primary, pasif gri (`AppColors`). Ana sekmeye uymayan alt sayfalar `AppNavTab.none`. Tek tanım → tüm sayfalara yansır | 🟢 |
| StudentBottomNav | `StudentBottomNav` @ `features/study/presentation/widgets/student_bottom_nav.dart` | `current` (`StudentNavTab`) | Öğrenci paneline özgü alt navigasyon (öğretmen `AppBottomNav` / veli `ParentBottomNav`'dan ayrı): 4 sekme — 🏠 Çalışma (`/student-home`) · 📚 Derslerim (`/student/lessons`) · 📊 Performans (`/student/performance`) · 👤 Profil (`/student/profile`). Görsel dil `AppBottomNav` ile aynı (aktif primary, pasif gri, `context.go`). Dördü de **gerçek ekranlara** gider; `StudentPlaceholderPage` kodda **yok** | 🟢 |
| AppAvatar | _(yok)_ | `imageUrl?, size, initials?` | Dairesel; görsel yoksa baş harf | 🔴 |
| AppBadge | _(yok)_ | `text/count, color` | Durum/sayaç rozeti (10-12px) | 🔴 |
| AppSegmentedTab | `EgitimUssuTabBar` (tasarım) @ _(yok)_ | `tabs, selectedIndex, onChanged` | 2-4 sekme; aktif lacivert+beyaz, pasif şeffaf+gri. **Derin tasarım →** [`../tab_widget.md`](../tab_widget.md) | 🔴 |

## 2. Bileşik Widget'lar (Kart / Liste Tile)

| Katalog | Kod | API (özet) | Kural | Durum |
|---------|-----|------------|-------|-------|
| MetricCard | _(yok)_ | `title, value, subtitle?, icon?, trend?, progress?` | KPI/özet kartı; ana değer büyük (24-32) | 🔴 |
| LessonCard | _(yok)_ | `subject, studentName, time, type, status` | Tip badge (Online/Yüz Yüze), durum badge | 🔴 |
| StudentListTile | _(yok)_ | `name, grade, lastLessonText, score, avatarUrl?, onTap` | Skor renk kuralı: ≥85 yeşil / 70-84 turuncu / ≤69 kırmızı | 🔴 |
| AssignmentTile | _(yok)_ | `title, studentName, dueDate, submittedCount, totalCount, status` | Teslim oranı + progress bar | 🔴 |
| PaymentTile | _(yok)_ | `studentName, period, amount, status` | Durum renk: Ödendi yeşil / Bekliyor turuncu / Gecikti kırmızı | 🔴 |
| NotificationTile | _(yok)_ | `icon, title, description, date, type` | Tip renk: Ders mor · Ödev yeşil · Not mavi · Ödeme kırmızı | 🔴 |
| ProfileMenuTile | _(yok)_ | `icon, label, onTap, isDanger?` | Profil menü satırı; danger=kırmızı (Çıkış) | 🔴 |
| SectionHeader | _(yok)_ | `title, actionText?, onActionTap?` | Bölüm başlığı + opsiyonel aksiyon (örnek: [`mobile_flutter.md`](mobile_flutter.md) §17) | 🔴 |

## 2b. Öğrenci Çalışma Sekmesi Ortak Bileşenleri (`study_tab_widgets.dart`)

> Konum: `mobile/lib/features/study/presentation/widgets/study_tab_widgets.dart` (öğrenci 4 sekme — Çalışma/Kronometre/
> Ders Detayı/Derslerim/Performans/Profil — bunlara dayanır). `StudyCard`/`StudySectionHeader`/`StudyStatTile`/
> `StudyComingSoonCard`/`StudySessionTile` zaten burada tanımlıydı; aşağıdakiler Task 2 ile eklendi.

| Katalog | Kod (sınıf @ dosya) | API (özet) | Kural / Varyant | Durum |
|---------|---------------------|------------|-----------------|-------|
| StudyDemoBadge | `StudyDemoBadge` @ `study_tab_widgets.dart` | _(argümansız)_ | Backend'i olmayan veri/eylemler için dürüst "Demo" pill'i (10px, `AppColors.warning` üstünde `warningSurfaceStrong` zemin) | 🟢 |
| StudyIconChip | `StudyIconChip` @ `study_tab_widgets.dart` | `icon, color, size=44` | Gradient ikon madalyonu (kart başlıkları / hızlı erişim); renk `AppColors` token'ından gelir | 🟢 |
| StudyPressable | `StudyPressable` @ `study_tab_widgets.dart` | `child, onTap` | Basılınca `AnimatedScale` ile 0.97'ye küçülen dokunma sarmalayıcısı (90ms) | 🟢 |
| StudyQuickAccessCard | `StudyQuickAccessCard` @ `study_tab_widgets.dart` | `icon, color, label, onTap` | Dashboard hızlı erişim kartı: `StudyPressable` + `StudyCard` + `StudyIconChip` + etiket | 🟢 |
| StudyProgressBar | `StudyProgressBar` @ `study_tab_widgets.dart` | `value (0..1), color?, trailingLabel?` | Hedef ilerleme barı; `value` `0..1` aralığına kırpılır, varsayılan renk `accentTeal`, zemin `tabBackground` | 🟢 |
| StudyOwnershipBadge | `StudyOwnershipBadge` @ `study_tab_widgets.dart` | `isOwn` | Kendi (öğrenci) / öğretmen dersi ayrım rozeti: `👤 Kendi` (teal) / `👨‍🏫 Öğretmen` (accentBlue) | 🟢 |

## 2c. Mesajlaşma Ekranı Ortak Bileşenleri (`messaging_widgets.dart`)

> Konum: `mobile/lib/features/messaging/presentation/widgets/messaging_widgets.dart`
> (P10 Task 4 / D-08). **`flutter_chat_ui` paketi kullanılmadı ve pubspec'ten kaldırıldı** — paket
> kendi teması, kendi girdi alanı ve kendi model tipleriyle geliyor, `AppColors`/`AppShadows`
> token'larına oturmuyordu; kullanılmayan ağır bağımlılık bırakmak da D-18'in ta kendisiydi.

| Katalog | Kod (sınıf @ dosya) | API (özet) | Kural / Varyant | Durum |
|---------|---------------------|------------|-----------------|-------|
| MessagingHeader | `MessagingHeader` @ `messaging_widgets.dart` | `title, subtitle?, trailing?` | Geri düğmeli üst bar (bildirim merkezindekiyle aynı dil); `trailing` engelleme menüsünü taşır | 🟢 |
| ConversationTile | `ConversationTile` @ `messaging_widgets.dart` | `conversation, title, onTap` | Gelen kutusu satırı: baş-harf avatarı + ad + önizleme + göreli zaman + okunmamış rozeti + engel ikonu. Min yükseklik 56 (ux_rules §6), `splashColor: primaryLight` | 🟢 |
| MessageBubble | `MessageBubble` @ `messaging_widgets.dart` | `message, isMine, onRetry` | Kendi mesajı sağda `primary` zeminde, karşı taraf solda `surface`; silinmişte `tabBackground` + italik "Bu mesaj silindi"; altında saat / "Gönderiliyor…" / hata + "Tekrar dene". Okundu ✓✓ **yalnız kendi** mesajında | 🟢 |
| MessageComposer | `MessageComposer` @ `messaging_widgets.dart` | `enabled, onSend, disabledHint?` | 1-5 satır büyüyen alan + 48px yuvarlak gönder düğmesi. `enabled: false` iken alan **hiç çizilmez**, yerine `errorSurface` zeminli açıklama gelir — boşuna yazdırmak yerine neden söylenir | 🟢 |

Yardımcılar: `initialsOf(name)` (baş harfler), `relativeTime(utc)` (bugün→saat, dün→"Dün", <7 gün→gün kısaltması, öncesi→`gg.aa`), `clockTime(utc)`.

## 3. Durum Görünümleri

| Katalog | Kod (sınıf @ dosya) | API | Kural | Durum |
|---------|---------------------|-----|-------|-------|
| LoadingState | `LoadingStateView` @ `state_views.dart` | `message='Yukleniyor...'` | Ortalı spinner + metin | 🟢 |
| ErrorState | `ErrorStateView` @ `state_views.dart` | `message, onRetry?` | errorContainer kart + "Tekrar dene" | 🟢 |
| EmptyState | `EmptyStateView` @ `state_views.dart` | `title, subtitle` | İkon + başlık + açıklama | 🟢 |
| SessionRequired | `SessionRequiredView` @ `session_required_view.dart` | `title?, subtitle?` | Oturum yokken sahte kullanıcı uydurmak yerine kilit ikonu + "Giriş yap" (D-17) | 🟢 |
| PremiumLock | `PremiumLock` @ `premium_lock.dart` | `child, message?` | Premium'da `child` doğrudan; Free'de bulanık + `IgnorePointer` önizleme (`premiumLockPreviewKey`) + kilit kartı. Kararı **kökteki `MembershipCubit`** verir (K-09) — ekran kendi tier'ını hesaplamaz (D-10) | 🟢 |
| PremiumLockCard | `PremiumLockCard` @ `premium_lock.dart` | `message, title?` | Cubit'e bağlı **değil**; sunucudan `402 *_premium_required` alan ekranlar kendi mesajıyla gösterir. "Premium'a geç" → `kPaywallRoute` (`/membership/paywall`) | 🟢 |

## 4. Önerilen Tamamlama Sırası (🔴/🟡)

Ekranların çoğu bunlara dayanır; sıra: **AppCard → AppSegmentedTab → MetricCard →
StudentListTile → LessonCard → AssignmentTile → PaymentTile → NotificationTile → AppAvatar → AppBadge →
ProfileMenuTile → SectionHeader**. (AppHeader ve AppBottomNav 🟢 tamamlandı.) `AppButton`'ı tam varyant setine (outline/danger/icon/small) genişlet.

## 5. Bilinen Tutarlılık Notları

- **`form_fields.dart` artık `AppColors`'a bağlı:** Eski sabit-kodlu renkler (`_appFieldBorder`, `_appFieldFocus`, `_appFieldText`, `_appFieldError`) kaldırıldı; `AppColors.border/primary/textPrimary/accentRed` kullanılıyor.
- **`AppPrimaryButton`** yalnızca primary varyantı sağlıyor; tasarımdaki `AppButton` varyantları (outline/danger/icon/small) eksik (🟡).
- **`AppPageHeader` öncesi** her ana ekran (dashboard, payments, assignments, scheduling, students, lesson_sessions) kendi başlık + bildirim butonunu ayrı tanımlıyordu; bildirim yalnızca dashboard'da çalışıyordu, rozetler sabit "2" idi. Artık tek widget'ta toplandı, buton her yerde `/notifications`'a gidiyor, rozet gerçek okunmamış sayısını gösteriyor ve renkleri `AppColors` token'larından çağırıyor.
- **`AppBottomNav` öncesi** alt navigasyon menüsü 7 sayfada ayrı ayrı kopyalanmıştı (her biri kendi `_XxxBottomNav` + `_BottomNavItem`/`_NavItem` sınıfıyla); etiketler ("Ogrenciler"/"Öğrenciler", "Diger"/"Diğer") ve üst kenarlık (`border`/`divider`) sayfadan sayfaya tutarsızdı, hatta assignments sayfası 5. sekmede "Finans" yerine "Ödevler" gösteriyordu. Artık tek `AppBottomNav` widget'ında toplandı: 6 kanonik sekme, tutarlı etiket/renk, hedef rotalar tek yerde. Sayfa sadece `current: AppNavTab.x` veriyor; assignments gibi ana sekmeye uymayan sayfalar `AppNavTab.none` kullanıyor.
- **Renk migrasyonu:** Tüm sayfa/widget'lardaki yerel renk sabitleri **ve** token'a birebir eşleşen inline `Color(0x…)` literalleri `AppColors`'a taşındı.
- **Gölge migrasyonu:** Tüm ekranlardaki elle yazılmış `BoxShadow`'lar tek `AppShadows.soft` (`core/theme/app_shadows.dart`) token'ına indirgendi; ekran başına ayrı gölge yok.
- **Semantik durum token'ları:** Hata/aciliyet tint'leri `AppColors`'a semantik token oldu (`error`/`errorSurface`/`errorSurfaceStrong`/`errorBorder`, `warning*`, `infoSurface`, `successSurface`); aciliyet-kademeli kartlar (geciken ödev/ödeme) bunları kullanıyor. Geriye yalnızca near-white yüzeyler ve gradient durakları gibi tekil/özel literaller kaldı.
- Yeni widget eklerken renk/spacing **doğrudan değer yazma**, [`design_system.md`](design_system.md) token'larından çağır.

> **Bakım (KALICI KURAL):** `shared/widgets/` altında bir widget eklen/değiştirilince bu katalogdaki **satırı ve durumu**
> aynı turda güncelle (kod adı + API). Karmaşık bir widget kendi md'sini hak ediyorsa onu oluştur ve buradan link ver. Bkz. kökteki `CLAUDE.md`.

---

> İlgili: token'lar → [`design_system.md`](design_system.md) · mobil mimari/ekranlar → [`mobile_flutter.md`](mobile_flutter.md) ·
> tab detay → [`../tab_widget.md`](../tab_widget.md) · sayfalar → [`../pages/00_pages_index.md`](../pages/00_pages_index.md)

### 3.1 Ayar ekranı bileşenleri (P05, `features/settings/presentation/widgets/settings_widgets.dart`)

`settings` feature'ına özel oldukları için `shared/` altına alınmadılar; başka bir feature aynı desene
ihtiyaç duyduğunda taşınmalıdır.

| Bileşen | API | Kural |
|---------|-----|-------|
| `SettingsScaffold` | `title, child` | Geri butonlu sade AppBar + `SafeArea` |
| `SettingsSectionTitle` | `text` | Büyük harf, `textSecondary`, harf aralığı 0.6 |
| `SettingsPanel` | `children` | 18 radius kart + `AppShadows.soft` + `border` |
| `SettingsDivider` | — | 1px `AppColors.divider` |
| `SettingsSwitchRow` | `icon, title, subtitle, value, onChanged, isSaving` | Kaydederken `AbsorbPointer` + %60 opaklık (çift istek yok) |
| `SettingsChoiceRow` | `title, subtitle, selected, onTap, isSaving` | Radyo benzeri tekli seçim |
| `SettingsHint` | `text` | Panel altı açıklama metni |
| `settingsErrorListener` | `BlocConsumer` listener'ı | Kaydetme hatasını SnackBar ile bildirir; yükleme hatası `ErrorStateView`'e bırakılır |

### 3.2 Öğretmen uygunluk düzenleyici (P06, `features/teacher_profile/presentation/widgets/availability_editor.dart`)

`teacher_profile` feature'ına özeldir; başka bir feature haftalık ızgaraya ihtiyaç duyduğunda `shared/`'a taşınmalıdır.

| Bileşen | API | Kural |
|---------|-----|-------|
| `AvailabilityEditor` | `slots, onChanged, onSave, isSaving` | **Kontrollü** widget — listeyi sahiplenmez; 7 gün kartı + gün başına "+" düğmesi |
| `availabilitySlotOverlaps` | `(slots, candidate, {ignore})` | Aynı günde kesişme testi; **bitişik** aralık (`18:00-20:00` + `20:00-21:00`) çakışma değildir |
| `availabilitySlotError` | `(existing, candidate, {ignore})` | Görünen hata metni ya da `null`; kural UI'dan bağımsız olduğu için dropdown sürmeden test edilir |
| `availabilityDayNames` / `availabilityTimeOptions` | sabit | Pazartesi=`dayOfWeek 1` (ISO-8601) · 06:00–23:30, 30 dk adım |

- Slot bazlı "Online" / "Yüz yüze" anahtarları seçiliyken de **ikon + metin** taşır — renk tek başına durum göstergesi olmaz ([`accessibility.md`](accessibility.md)).
- Renk/gölge/radius token'ları `AppColors` + `AppShadows.soft`; hata kutusu `errorSurface`/`errorBorder`/`error` semantik token'ları.

### 3.4 Üyelik / paywall bileşenleri (P09, `features/membership/presentation/widgets/membership_widgets.dart`)

`membership` feature'ına özeldirler; premium kilidi ise **ortak** olduğu için `shared/widgets/premium_lock.dart`'tadır (§3 tablosu).

| Bileşen | API | Kural |
|---------|-----|-------|
| `MembershipScaffold` | `title, child` | Üyelik + paywall ekranlarının ortak kabuğu (`AppColors.background` + `AppBar`) |
| `MembershipPanel` | `child, padding?` | Beyaz, 20 radius, `AppColors.border` kenarlıklı bölüm kartı |
| `MembershipSectionTitle` | `text` | Bölüm başlığı |
| `MembershipTierBadge` | `isPremium` | Free/Premium rozeti; renk `textSecondary` / `amber` |
| `PlanCard` | `plan, canStartTrial, isBusy, onStartTrial` | Fiyat + periyot + deneme rozeti; **satın alma düğmesi `onPressed: null`** (checkout ucu P09 Task 4) |
| `FeatureComparisonTable` | `rows` | PRD §9'dan türeyen üç sütunlu tablo; satırlar `premium_features.dart`'tan gelir ve **yalnız sunucunun uyguladığı farkları** taşır |

---

### 3.3 Gelişim ve rapor grafikleri (P08, `features/progress/...` + `features/reporting/...`)

İlgili feature'lara özeldirler; üçüncü bir feature aynı grafiğe ihtiyaç duyduğunda `shared/`'a taşınmalıdır.
(`WeeklyStudyBarChart` ve `SubjectNetChart` bugün `reporting` ekranlarından **ve** veli çocuk detayından
kullanılıyor — `shared/`'a taşıma adayı.)

| Bileşen | Dosya | API | Kural |
|---------|-------|-----|-------|
| `ProgressSnapshotChart` | `progress/presentation/widgets/progress_snapshot_chart.dart` | `seriesBySubject` | Ders başına `LineChart` çizgisi, y 0–100 sabit; renkler aksan token'larından **deterministik** sırayla; tek noktalı seri de çizilir (nokta görünür) |
| `ReportCard` | `reporting/presentation/widgets/report_charts.dart` | `title, subtitle?, trailing?, child` | 20 radius + `AppShadows.soft` + `skyBorder` — rapor ekranlarının ortak kabuğu |
| `WeeklyStudyBarChart` | aynı dosya | `weeks` | Son **12** hafta (daha uzun seri okunmaz); dokununca dakika + oturum sayısı tooltip'i |
| `SubjectNetChart` | aynı dosya | `performance` | Ders başına **en güncel ay**; % net metni + delta oku; renk tek başına gösterge değil |

- Grafik geçişleri `250 ms` / `Curves.easeOut` ([`animations.md`](animations.md)); `Curves.linear` kullanılmaz.
- Boş seri geldiğinde grafik `SizedBox.shrink()` döner — çağıran ekran boş durum metnini kendisi yazar
  (sunucu gizlilik kapalıyken 403 değil boş liste döndürdüğü için metin iki olasılığı birden söyler).

---

*Ortak Widget Kataloğu | Güncelleme: 2026-09-04 (P09: `PremiumLock` + `PremiumLockCard`) · 2026-09-04 (P08: gelişim/rapor grafikleri) · 2026-09-04 (P06: öğretmen uygunluk düzenleyici) · 2026-09-03 (P05: `SessionRequiredView` + ayar ekranı bileşenleri)*
