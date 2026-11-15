---
title: "Ödeme Takibi Ekranı"
summary: "Öğretmenin tüm ödeme kayıtlarını görüntülediği, sunucu taraflı sayfalama/filtre ve tahsilat formuyla PaymentsCubit'e bağlı ekran"
tags: [sayfa, payments, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/payments/presentation/pages/payments_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-11-15
---

# Ödeme Takibi (`/payments`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payments_page.dart`
> **State:** `PaymentsCubit` (BlocProvider.value) · **Veri:** 🟡 Gerçek API + demo fallback · **Güncelleme:** 2026-11-15

## Amaç
Öğretmenin tüm ödeme kayıtlarını görüntüleme: açıklama, vade, tutar, durum (Ödendi/Bekleyen/Kısmi/Geciken/İptal) + **tam veya kısmi tahsilat** girişi (tahsilat formu).

## Veri / API — sunucu tarafı sayfalama + filtre (2026-07-06)
- `PaymentsCubit.load(teacherUserId)` → paralel: `GET .../summary` (aggregate: özet paneli + grafikler) + `GET .../records/search?skip=0&take=20` (ilk sayfa).
- `PaymentsCubit.loadMore()` → sonraki sayfa (`skip=records.length`); sonsuz kaydırma (`_scrollController`, dip − 400px).
- `PaymentsCubit.applyFilters(filters)` → ilk sayfayı yeni filtrelerle yeniden çeker (özet **değişmez**; filtresizdir).
- **Filtreler** (`PaymentFilters`): metin arama (`q`, 350ms debounce), durum sekmesi (`status`: `null`/`Paid`/`Open`/`Overdue`), öğrenci (`studentId`), vade tarih aralığı. Sekme dışı (öğrenci + tarih) filtreler `_FilterButton` → `PaymentFilterSheet` (bottom sheet) ile; aktif olanlar çip olarak gösterilir.
- `state.totalCount` = filtreyle eşleşen **toplam** kayıt; sekme altında "N kayıt". `hasMore = records.length < totalCount`.
- Aggregate'ler (özet + grafikler) yalnız `getSummary`'den; tahsilat/iptal sonrası `_refreshSummary()` ile güncellenir. Bu yüzden 100+ kayıtta tüm veri client'a çekilmez.
- `PaymentsCubit.collect(record, int amountNow)` → `POST /api/payments/records/{id}/collections` `{amount, method: 1, occurredOnUtc, note}`. `amountNow` kuruş, JSON `amount` ana birimdir. Varsayılan yöntem nakittir; yöntem seçicisi bu sheet'te yoktur.
  - `CollectPaymentSheet` yalnız bu tahsilatın kuruş tutarını döndürür; varsayılan kalan bakiyedir. Türkçe gruplama/ondalık desteklenir, sıfır/negatif/bozuk giriş ve ekranda görünen bakiyeyi aşan tutar reddedilir. Cubit toplam/status hesaplamaz ve clamp yapmaz; güncel toplamı sunucu yanıtından alır. Sunucu yarış veya değişmiş bakiyeyi reddederse hata gösterilir, kayıt tahminle değiştirilmez.
  - `isSaving` aynı anda ikinci mutasyonu engeller. Tıklanan kayda özel spinner ve hata sonrası tekrar etkinleşme korunur. POST anahtarı, timeout'ta aynı anahtarla tek otomatik tekrar ve aynı içerikli manuel tekrar vardır; ilk hareket zamanı da korunur. Başarıyla biten sonraki işlem yeni UUID alır.
- `PaymentsCubit.create(record)` → `POST /api/payments/records` (PaymentFormPage'den dönüşte `load` yeniden çağrılır)
- **Kart → düzenleme (2026-07-06):** Ödeme kartına dokununca (`_PaymentTile` `InkWell` ile tıklanabilir + sağda `chevron_right` ok → `_onEdit`) `context.push('/payments/edit', extra: record)` ile `PaymentFormPage` **düzenleme modunda** açılır; dönüşte liste yeniden yüklenir. Kaydet `PaymentsCubit.update(record)` → `PUT /api/payments/records/{id}`.
- **İptal (2026-07-06):** Düzenleme formundaki iptal ikonu → **onay dialogu** → `PaymentsCubit.cancel(record)` → `POST /api/payments/records/{id}/cancel`. Kayıt **silinmez**; listede "İptal" olarak kalır. Başarıda listeye döner. (Hard delete yok.)
- 🟡 Gerçek API (yukarıdaki uçlar) çağrılır; erişilemezse offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

## Status string → UI mapping
| Backend string | Etiket    | Renk         |
|----------------|-----------|--------------|
| `Paid`         | Ödendi    | emerald      |
| `Cancelled`    | İptal     | gri (muted)  |
| `Pending`      | Bekleyen  | amber        |
| `PartiallyPaid`| Kısmi     | blue         |
| `isOverdue=true` | Geciken | red          |

> **Düzeltme (2026-07-06):** `Cancelled` kayıtlar önceden hiçbir yerde ele alınmadığından UI'da yanlışlıkla **"Bekleyen"** (amber) görünüyor, **"Bekleyen" sekmesinde** listeleniyor ve iptal edilmiş olmasına rağmen **"Tahsil Et"** butonu gösteriyordu. Artık ayrı **"İptal"** rozeti (gri) alır, "Bekleyen" filtresinin dışında tutulur ve "Tahsil Et" butonu gösterilmez.
> **P18 T4:** Özet paneli ve iki grafik yalnız seçilen para biriminin `CurrencySummaries`/`MonthlyBreakdown.Currency` değerlerini kullanır. TRY varsa ilk seçim TRY, yoksa ilk mevcut para birimidir. Seçim kayıt listesini filtrelemez; her kart kendi ISO koduyla iki ondalık gösterilir. Para birimleri arasında tutar toplamı yoktur.

## Dar ekran ve form güvenliği (2026-10-16)
- Tahsilat ve filtre alt sayfaları kaydırılabilir; 320×568 ekranda, klavye açıkken ve büyütülmüş yazıda kaydet/uygula eylemine erişilir.
- Büyük tutarlar ve uzun öğrenci adları yatay taşma yapmaz; güncellenen öğrenci listesinde seçili öğrenci yoksa mevcut filtre seçimi korunur.
- Ters tarih aralığı formda açıklanır ve API'ye gönderilmez; tahsilat alanı sonlu olmayan sayıları reddeder.
- Seçilen tarihler kullanıcının yerel gün başlangıcı/sonu üzerinden UTC'ye çevrilir; bitiş etiketi saat dilimi nedeniyle ertesi güne kaymaz. Tarih seçici ±3 yıl dışındaki eski filtreleri açarken başlangıç seçimini geçerli aralığa sınırlar.
- Regresyon: `test/features/payments/presentation/widgets/payment_sheet_layout_test.dart`.

## Sayfa düzeni & performans (2026-07-06)
- Gövde `CustomScrollView` + **lazy `SliverList`** + sunucu **sayfalama** (sonsuz kaydırma) — 100+ kayıt tek seferde çekilmez/build edilmez.
- Sıra: başlık → `_FinanceSummaryPanel` (mavi özet, `getSummary`'den) → **katlanabilir `_StatsSection`** (grafikler) → **arama çubuğu + `_FilterButton`** → filtre sekmeleri (+ "N kayıt") → aktif filtre çipleri → kayıt listesi → (yükleniyor) alt spinner.
- İlk yüklemede özet null ise `_ShimmerSummary`; sonraki sayfa yüklenirken liste altında spinner.

## State akışı
- `isLoading=true` → shimmer özet + shimmer liste
- `errorMessage!=null && records.isEmpty` → hata kartı + "Tekrar Dene"
- `records.isEmpty` (başarılı ama boş) → `_EmptyPanel`
- Yüklü → 4-tab filtre (Tümü/Ödenen/Bekleyen/Geciken) + kayıt kartları
- `successMessage` / `errorMessage` → `SnackBar` (BlocConsumer listener)

## Grafikler (2026-07-06) — `_StatsSection` (varsayılan KAPALI)
İki grafik kartı, mavi özet altındaki **katlanabilir "İstatistikler"** bölümündedir; **varsayılan kapalı** (tek dokunuşla açılır, `AnimatedSize`). Veriler **sunucu özetinden** gelir (`state.summary`), böylece sayfalama açıkken de tüm kayıtlara ihtiyaç yok. Ev-stili **hafif özel grafikler** (bkz. `ParentWeeklyBars`).
- `MonthlyCollectionCard(points: summary.monthsForCurrency(currency), currency: currency)` — son 6 ayın **beklenen vs tahsil edilen** çubuk grafiği (arka plan beklenen, dolu tahsil edilen).
- `PaymentDistributionCard(summary: summary.forCurrency(currency), currency: currency)` — özet toplamlarından tutar bazında **donut** (tahsil edilen/bekleyen/geciken) + ortada **tahsilat oranı %** (`_DonutPainter`). Dosya: `presentation/widgets/finance_charts.dart`.

## Ana bileşenler
- `_FinanceSummaryPanel` — tahsil edilen / bekleyen / geciken tutarlarını sunucu özetinin seçilen para biriminden alır
- `_PaymentTile` — **iki bölümlü ferah kart, tek-tip yükseklik**: üstte avatar + başlık/vade + durum rozeti · ince ayraç · altta tutar (etiketli: Kalan/Tahsil edilen/Tutar) + etiketli tonal **"Tahsil Et"** aksiyonu (`_CollectAction`, yalnız Paid/İptal dışı). Alt bölüm tüm kartlarda aynı yapıda olduğundan "Tahsil Et"i olan/olmayan kartlar aynı boyutta. **Kart gövdesine dokunma → düzenleme**; "Tahsil Et" → `CollectPaymentSheet`.
- `CollectPaymentSheet` (`presentation/widgets/collect_payment_sheet.dart`) — tahsilat formu: beklenen/tahsil edilen/kalan özeti + **belirgin tam-genişlik "Tamamını al" butonu** (`_FullAmountButton`) + "veya kısmi tutar gir" ayıracı + tutar girişi; alınan tutarı int kuruş olarak döndürür
- `_SearchField` + `_FilterButton` — metin araması + gelişmiş filtre (öğrenci/tarih) düğmesi (aktif sayı rozetli)
- `PaymentFilterSheet` (`presentation/widgets/payment_filter_sheet.dart`) — öğrenci dropdown + vade tarih aralığı seçimi; güncel `PaymentFilters` döndürür
- `_ActiveFilterChips` — aktif öğrenci/tarih filtrelerini çip olarak gösterir (× ile temizle)
- `_PaymentTabs` — 4 durum sekmesi (Tümü/Ödenen/Bekleyen=`Open`/Geciken) → sunucu `status` filtresi + sonuç sayısı
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`
- `_ShimmerSummary` / `_ShimmerList` — yükleme iskeletleri

## P18 T4 para / ARB sözleşmesi

`PaymentRecord`, `PaymentCurrencySummary` ve `PaymentMonthlyPoint` tutarları int kuruştur; ayrıştırma/JSON dönüşümü/iki ondalıklı gösterim `core/money/money.dart` içindedir. Eski aylık cache para birimlerini birleştirdiği için özet cache anahtarı `payments.summary.v2.<teacher>` olmuştur. Ana ekran, tahsilat sheet'i ve grafik metinleri tr/en ARB'den gelir; filtre sheet'inin önceki metinleri bu görevde değiştirilmedi.

Regresyonlar: `payment_form_parsing_test.dart` (TRY→USD özet ve iki grafik, form gövdesi, sheet kuruş sonucu), `payment_currency_test.dart` (JSON Currency+kuruş), `payment_collection_request_test.dart` (bayat toplamdan delta komutu, iptal ucu, manuel retry zamanı).

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.10 · Form: [`payment_form.md`](payment_form.md) · Modül: M07

*Güncelleme: 2026-11-15 — P18 T4 mobil para ve idempotency.*
