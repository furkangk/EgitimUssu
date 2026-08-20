---
title: "Ortak Widget Kataloğu (Shared Widgets)"
summary: "Tüm ekranlarda aynı kuralla kullanılması gereken paylaşılan Flutter bileşenlerinin tek listesi: amaç, API, varyant, kullanım kuralı ve durum"
tags: [mimari, widget, flutter]
authority: code
code_refs:
  - mobile/lib/shared/widgets/**
updated: 2026-08-19
---

# 🧩 Ortak Widget Kataloğu (Shared Widgets)

> **Kapsam:** Tüm ekranlarda **aynı kuralla** kullanılması gereken paylaşılan Flutter bileşenlerinin tek listesi:
> amaç, API, varyant, kullanım kuralı ve **durum**. Atomic Design'da bu katman = **moleküller** (atom/token →
> [`design_system.md`](design_system.md), organizma/ekran → [`mobile_flutter.md`](mobile_flutter.md) §13).
>
> **Otorite:** Bir widget kodda varsa **kod doğruluk kaynağıdır** (sınıf adı, parametreler buradan alınır). Token
> değerleri → [`design_system.md`](design_system.md). Karmaşık widget'ın derin tasarımı kendi dosyasındadır (ör. tab → [`../tab_widget.md`](../tab_widget.md)).
>
> **Güncelleme:** 2026-07-07

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
| AppTextField | `AppTextField` @ `form_fields.dart` | `controller, labelText, hintText?, validator?, keyboardType?, minLines/maxLines, maxLength?, readOnly, onTap?, suffixIcon?` | Üstte `AppFieldLabel` + `TextFormField`; radius 18 (`appInputDecoration`) | 🟢 |
| AppDropdownField | `AppDropdownField<T>` @ `form_fields.dart` | `value, labelText, items, onChanged` | Label + `DropdownButtonFormField` | 🟢 |
| AppDateTimeField | `AppDateTimeField` @ `form_fields.dart` | `controller, labelText, validator, hintText?` | `AppTextField` + takvim ikonu | 🟢 |
| AppFieldLabel | `AppFieldLabel` @ `form_fields.dart` | `text` | Form alanı etiketi (w700) | 🟢 |
| AppCard | _(yok)_ | `child, padding?` | Beyaz zemin + `border` + `softShadow`, radius 16, padding 14-16 | 🔴 |
| AppHeader | `AppPageHeader` @ `app_page_header.dart` | `title, subtitle?, trailing?` | Tüm ana ekranların ortak başlığı: sol başlık (+alt başlık), sağda bildirim zili. Zil her zaman `/notifications`'a gider; rozet global `NotificationsCubit.state.unreadCount`'tan gelir (`context.select`). Renkler `AppColors` token'larından (sabit kodlu **değil**). Tek tanım → tüm sayfalara yansır. Geri/menü varyantı henüz yok | 🟡 |
| AppBottomNav | `AppBottomNav` @ `app_bottom_nav.dart` | `current` (`AppNavTab`) | Tüm ana ekranların ortak alt navigasyon menüsü (master page): 6 sabit sekme (Ana sayfa/Dersler/Öğrenciler/Takvim/Finans/Diğer), ikonlar + etiketler + hedef rotalar tek yerde. Sayfa yalnızca aktif sekmeyi (`AppNavTab`) bildirir; widget `context.go` ile yönlendirir. Aktif primary, pasif gri (`AppColors`). Ana sekmeye uymayan alt sayfalar `AppNavTab.none`. Tek tanım → tüm sayfalara yansır | 🟢 |
| StudentBottomNav | `StudentBottomNav` @ `features/study/presentation/widgets/student_bottom_nav.dart` | `current` (`StudentNavTab`) | Öğrenci paneline özgü alt navigasyon (öğretmen `AppBottomNav` / veli `ParentBottomNav`'dan ayrı): 4 sekme (Ana Sayfa/Ders Programı/İstatistik/Diğer). Görsel dil `AppBottomNav` ile aynı (aktif primary, pasif gri, `context.go`). **Ders Programı/İstatistik/Diğer henüz tasarlanmadı** → `StudentPlaceholderPage` ("yakında")'a gider; gerçek ekranlar sonra bağlanacak | 🟡 |
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

## 3. Durum Görünümleri

| Katalog | Kod (sınıf @ dosya) | API | Kural | Durum |
|---------|---------------------|-----|-------|-------|
| LoadingState | `LoadingStateView` @ `state_views.dart` | `message='Yukleniyor...'` | Ortalı spinner + metin | 🟢 |
| ErrorState | `ErrorStateView` @ `state_views.dart` | `message, onRetry?` | errorContainer kart + "Tekrar dene" | 🟢 |
| EmptyState | `EmptyStateView` @ `state_views.dart` | `title, subtitle` | İkon + başlık + açıklama | 🟢 |

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

*Ortak Widget Kataloğu | Güncelleme: 2026-08-19*
