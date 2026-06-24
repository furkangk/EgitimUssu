# 🧩 Ortak Widget Kataloğu (Shared Widgets)

> **Kapsam:** Tüm ekranlarda **aynı kuralla** kullanılması gereken paylaşılan Flutter bileşenlerinin tek listesi:
> amaç, API, varyant, kullanım kuralı ve **durum**. Atomic Design'da bu katman = **moleküller** (atom/token →
> [`design_system.md`](design_system.md), organizma/ekran → [`mobile_flutter.md`](mobile_flutter.md) §13).
>
> **Otorite:** Bir widget kodda varsa **kod doğruluk kaynağıdır** (sınıf adı, parametreler buradan alınır). Token
> değerleri → [`design_system.md`](design_system.md). Karmaşık widget'ın derin tasarımı kendi dosyasındadır (ör. tab → [`../tab_widget.md`](../tab_widget.md)).
>
> **Güncelleme:** 2026-06-24

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
| AppHeader | _(yok)_ | `title, leading?, actions?` | Varyant: geri / bildirim / menü / sadece-başlık | 🔴 |
| AppBottomNav | _(yok)_ | `items, currentIndex, onTap` | **Rol bazlı** item seti (bkz. [`mobile_flutter.md`](mobile_flutter.md) §9); aktif primary, pasif gri | 🔴 |
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

## 3. Durum Görünümleri

| Katalog | Kod (sınıf @ dosya) | API | Kural | Durum |
|---------|---------------------|-----|-------|-------|
| LoadingState | `LoadingStateView` @ `state_views.dart` | `message='Yukleniyor...'` | Ortalı spinner + metin | 🟢 |
| ErrorState | `ErrorStateView` @ `state_views.dart` | `message, onRetry?` | errorContainer kart + "Tekrar dene" | 🟢 |
| EmptyState | `EmptyStateView` @ `state_views.dart` | `title, subtitle` | İkon + başlık + açıklama | 🟢 |

## 4. Önerilen Tamamlama Sırası (🔴/🟡)

Ekranların çoğu bunlara dayanır; sıra: **AppCard → AppHeader → AppBottomNav → AppSegmentedTab → MetricCard →
StudentListTile → LessonCard → AssignmentTile → PaymentTile → NotificationTile → AppAvatar → AppBadge →
ProfileMenuTile → SectionHeader**. `AppButton`'ı tam varyant setine (outline/danger/icon/small) genişlet.

## 5. Bilinen Tutarlılık Notları

- **`form_fields.dart` token'a bağlı değil:** Renkleri sabit kodlu (`_appFieldBorder=0xFFE5EAF0`, `_appFieldFocus=0xFF062B52`).
  Bunlar [`design_system.md`](design_system.md) token'larından (`border=#E5E7EB` vb.) küçük farkla ayrışıyor → ileride
  `AppColors`/tema üzerinden alınmalı.
- **`AppPrimaryButton`** yalnızca primary varyantı sağlıyor; tasarımdaki `AppButton` varyantları (outline/danger/icon/small) eksik (🟡).
- Yeni widget eklerken renk/spacing **doğrudan değer yazma**, [`design_system.md`](design_system.md) token'larından çağır.

> **Bakım (KALICI KURAL):** `shared/widgets/` altında bir widget eklen/değiştirilince bu katalogdaki **satırı ve durumu**
> aynı turda güncelle (kod adı + API). Karmaşık bir widget kendi md'sini hak ediyorsa onu oluştur ve buradan link ver. Bkz. kökteki `CLAUDE.md`.

---

> İlgili: token'lar → [`design_system.md`](design_system.md) · mobil mimari/ekranlar → [`mobile_flutter.md`](mobile_flutter.md) ·
> tab detay → [`../tab_widget.md`](../tab_widget.md) · sayfalar → [`../pages/00_pages_index.md`](../pages/00_pages_index.md)

*Ortak Widget Kataloğu | Güncelleme: 2026-06-24*
