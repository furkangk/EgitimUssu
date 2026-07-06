# Ödeme Takibi (`/payments`)

> **Feature:** `payments` · **Dosya:** `mobile/lib/features/payments/presentation/pages/payments_page.dart`
> **State:** `PaymentsCubit` (BlocProvider.value) · **Veri:** ✅ Gerçek API · **Güncelleme:** 2026-07-06

## Amaç
Öğretmenin tüm ödeme kayıtlarını görüntüleme: açıklama, vade, tutar, durum (Ödendi/Bekleyen/Kısmi/Geciken/İptal) + **tam veya kısmi tahsilat** girişi (tahsilat formu).

## Veri / API — sunucu tarafı sayfalama + filtre (2026-07-06)
- `PaymentsCubit.load(teacherUserId)` → paralel: `GET .../summary` (aggregate: özet paneli + grafikler) + `GET .../records/search?skip=0&take=20` (ilk sayfa).
- `PaymentsCubit.loadMore()` → sonraki sayfa (`skip=records.length`); sonsuz kaydırma (`_scrollController`, dip − 400px).
- `PaymentsCubit.applyFilters(filters)` → ilk sayfayı yeni filtrelerle yeniden çeker (özet **değişmez**; filtresizdir).
- **Filtreler** (`PaymentFilters`): metin arama (`q`, 350ms debounce), durum sekmesi (`status`: `null`/`Paid`/`Open`/`Overdue`), öğrenci (`studentId`), vade tarih aralığı. Sekme dışı (öğrenci + tarih) filtreler `_FilterButton` → `PaymentFilterSheet` (bottom sheet) ile; aktif olanlar çip olarak gösterilir.
- `state.totalCount` = filtreyle eşleşen **toplam** kayıt; sekme altında "N kayıt". `hasMore = records.length < totalCount`.
- Aggregate'ler (özet + grafikler) yalnız `getSummary`'den; tahsilat/iptal sonrası `_refreshSummary()` ile güncellenir. Bu yüzden 100+ kayıtta tüm veri client'a çekilmez.
- `PaymentsCubit.collect(record, amountNow)` → `PUT /api/payments/records/{id}`
  - **Tahsilat formu (2026-07-06):** "Tahsil Et" artık tek tıkla tamamını almaz; `CollectPaymentSheet` (bottom sheet) açar. Öğretmen bu işlemde alınan tutarı girer (varsayılan = **kalan**; "Tamamını al" ile doldurulabilir). Cubit yeni tahsilatı hesaplar: `newCollected = min(collectedAmount + amountNow, expectedAmount)`; tamamı alınırsa `status=Paid`, kısmiyse `status=PartiallyPaid`. `collectedOnUtc=now`. Kalandan fazla girişe form izin vermez; cubit de beklenen tutarda **clamp**'ler.
  - ⚠️ **Bug düzeltmesi (2026-07-06, `collect`'in öncülü `markPaid`):** "Tahsil Et" 3 ayrı sorundan çalışmıyordu:
    1. **Enum eşleme:** `PaymentRecordModel._statusToApi`/`_itemTypeToApi` backend enum'larıyla uyuşmuyordu (Paid↔PartiallyPaid takas; Cancelled→Overdue; `MonthlyPackage`/`ManualAdjustment` isim uyuşmazlığı → LessonFee'ye düşüyordu). Sonuç: gerçek API'de "Tahsil Et" ödemeyi **PartiallyPaid** kaydedip kart "Ödendi"ye geçmiyordu. Eşleme backend değerlerine göre düzeltildi.
    2. **Görsel geri bildirim yok:** Tıklamada buton sessizce devre dışı kalıyor, spinner yoktu; backend erişilemezse Dio ~15 sn asılı kaldığından kullanıcıya "hiçbir şey olmuyor" gibi görünüyordu. Artık tıklanan kayda özel yükleniyor göstergesi (`PaymentsState.savingRecordId` → butonda `CircularProgressIndicator` + "İşleniyor…") var.
    3. **Sağlamlık:** `collect`/`load`/`create` yalnız `ApiException` yakalıyordu; başka bir hata `isSaving`/`isLoading`'i kalıcı bırakıp butonu/ekranı kilitliyordu. Genel `catch` eklendi (durum sıfırlanır + hata mesajı).
    - Testler: `test/features/payments/data/payment_model_test.dart`, `test/features/payments/data/payment_repository_mock_fallback_test.dart`, `test/features/payments/presentation/payments_cubit_test.dart`.
- `PaymentsCubit.create(record)` → `POST /api/payments/records` (PaymentFormPage'den dönüşte `load` yeniden çağrılır)
- **Kart → düzenleme (2026-07-06):** Ödeme kartına dokununca (`_PaymentTile` `InkWell` ile tıklanabilir + sağda `chevron_right` ok → `_onEdit`) `context.push('/payments/edit', extra: record)` ile `PaymentFormPage` **düzenleme modunda** açılır; dönüşte liste yeniden yüklenir. Kaydet `PaymentsCubit.update(record)` → `PUT /api/payments/records/{id}`.
- **İptal (2026-07-06):** Düzenleme formundaki iptal ikonu → **onay dialogu** → `PaymentsCubit.cancel(record)` → `PUT /api/payments/records/{id}` (`Status=Cancelled`). Kayıt **silinmez**; listede "İptal" olarak kalır. Başarıda listeye döner. (Hard delete yok.)
- Offline fallback: lokal cache → demo veri (`AppConfig.isMockFallbackEnabled`)

## Status string → UI mapping
| Backend string | Etiket    | Renk         |
|----------------|-----------|--------------|
| `Paid`         | Ödendi    | emerald      |
| `Cancelled`    | İptal     | gri (muted)  |
| `Pending`      | Bekleyen  | amber        |
| `PartiallyPaid`| Kısmi     | blue         |
| `isOverdue=true` | Geciken | red          |

> **Düzeltme (2026-07-06):** `Cancelled` kayıtlar önceden hiçbir yerde ele alınmadığından UI'da yanlışlıkla **"Bekleyen"** (amber) görünüyor, **"Bekleyen" sekmesinde** listeleniyor ve iptal edilmiş olmasına rağmen **"Tahsil Et"** butonu gösteriyordu. Artık ayrı **"İptal"** rozeti (gri) alır, "Bekleyen" filtresinin dışında tutulur ve "Tahsil Et" butonu gösterilmez.
> **`_FinanceSummaryPanel` "Tahsil edilen"** metriği artık **tüm** kayıtların `collectedAmount` toplamıdır (kısmi tahsilatlar dahil; önceden yalnızca `Paid` kayıtlar sayılıyordu). Backend `CollectedAmountTotal` semantiğiyle örtüşür.

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
- `MonthlyCollectionCard(points: summary.monthlyBreakdown)` — son 6 ayın **beklenen vs tahsil edilen** çubuk grafiği (arka plan beklenen, dolu tahsil edilen).
- `PaymentDistributionCard(summary)` — özet toplamlarından tutar bazında **donut** (tahsil edilen/bekleyen/geciken) + ortada **tahsilat oranı %** (`_DonutPainter`). Dosya: `presentation/widgets/finance_charts.dart`.

## Ana bileşenler
- `_FinanceSummaryPanel` — tahsil edilen / bekleyen / geciken tutarları `state.records`'dan hesaplar
- `_PaymentTile` — **iki bölümlü ferah kart, tek-tip yükseklik**: üstte avatar + başlık/vade + durum rozeti · ince ayraç · altta tutar (etiketli: Kalan/Tahsil edilen/Tutar) + etiketli tonal **"Tahsil Et"** aksiyonu (`_CollectAction`, yalnız Paid/İptal dışı). Alt bölüm tüm kartlarda aynı yapıda olduğundan "Tahsil Et"i olan/olmayan kartlar aynı boyutta. **Kart gövdesine dokunma → düzenleme**; "Tahsil Et" → `CollectPaymentSheet`.
- `CollectPaymentSheet` (`presentation/widgets/collect_payment_sheet.dart`) — tahsilat formu: beklenen/tahsil edilen/kalan özeti + **belirgin tam-genişlik "Tamamını al" butonu** (`_FullAmountButton`) + "veya kısmi tutar gir" ayıracı + tutar girişi; alınan tutarı döndürür
- `_SearchField` + `_FilterButton` — metin araması + gelişmiş filtre (öğrenci/tarih) düğmesi (aktif sayı rozetli)
- `PaymentFilterSheet` (`presentation/widgets/payment_filter_sheet.dart`) — öğrenci dropdown + vade tarih aralığı seçimi; güncel `PaymentFilters` döndürür
- `_ActiveFilterChips` — aktif öğrenci/tarih filtrelerini çip olarak gösterir (× ile temizle)
- `_PaymentTabs` — 4 durum sekmesi (Tümü/Ödenen/Bekleyen=`Open`/Geciken) → sunucu `status` filtresi + sonuç sayısı
- `RefreshIndicator` — pull-to-refresh → `_cubit.load(userId)`
- `_ShimmerSummary` / `_ShimmerList` — yükleme iskeletleri

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.10 · Form: [`payment_form.md`](payment_form.md) · Modül: M07
