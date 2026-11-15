# P20 — Mobil Sağlamlaştırma (Dalga 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mobil uygulama kullanıcıya sahte veri göstermesin, her rol kendi ortak ekranlarına (mesajlar, bildirimler, ayarlar, üyelik) girebilsin, çıkışta veri arkada kalmasın, açıkken gelen bildirim ve mesaj görünsün, kapanan ekranlar çökme üretmesin.

**Architecture:** Router'da rol başına izinli rota tablosu (tek kaynak) + `errorBuilder`. Önbellekler kullanıcı-kapsamlı anahtarlarla ve tek bir `CacheStore` üzerinden; çıkışta tümü silinir, kök cubit'ler sıfırlanır. Ön planda `FirebaseMessaging.onMessage` ilgili cubit'leri tazeler; açık sohbet K-07 gereği poll eder. Cubit'ler `SafeEmit` mixin'i kullanır.

**Tech Stack:** Flutter, `flutter_bloc`, `go_router`, `dio`, `get_it`, `shared_preferences`, `flutter_secure_storage`.

**Spec:** [`specs/2026-11-13-olgunlastirma-master-design.md`](../specs/2026-11-13-olgunlastirma-master-design.md) — §1 (ARB kuralı), K-07, K-26. **Kaynak:** rapor §1.4 (N-60, N-62, N-68…N-72), §3 P-10.

**Dal:** `feat/p20-mobil` · **Ön koşul:** P15 T7 (refresh/oturum — N-63/N-67 orada kapandı).

## Global Constraints

- **Commit kapsamı:** her commit bloğundan önce `git add <görevin Files: listesi> <değişen doc dosyaları>`; **`git add -A` / `git add .` yasak** (platforma özgü dosyalar, `pubspec.lock` ve ilgisiz değişiklikler sızmasın).
- Her görevde: `cd mobile && flutter test && flutter analyze --fatal-infos` → 0 hata, 0 sorun.
- Dokunulan ekran ARB'ye taşınır (K-26); `localization_test.dart` tarama kapsamı güncellenir (`_migratedFeatures` veya yalnız dokunulan dosyalar için `_migratedFiles`; dokunulmayan ekranlar taşınmış sayılmaz).
- Sahte veri yasağı (2026-09-02 §1): ekrandaki her değer gerçek API'den; demo yalnız `--dart-define=USE_MOCK_FALLBACK=true`.
- Doküman: değişen ekranlar için `doc/pages/*.md`, rota değişikliği için `doc/architecture/mobile_flutter.md` + `ux_rules.md`.

---

### Task 1: Sahte veri ve yer tutucu temizliği (N-60, P-10, N-72 metin) ✅

**Files:**
- Modify: `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart` (`_seedEvents` ve kullanımı silinir; tatil/ödev/ödeme etkinlikleri gerçek kaynaktan gelene kadar **gösterilmez**)
- Modify: `mobile/lib/core/routing/app_router.dart` (`/teacher-panel-preview`, `/account-info-preview` yalnız `kDebugMode`)
- Modify (kullanıcı onaylı 2026-11-14, ekran taslakları O13/O15): `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart` (koda gömülü `'4.8'` puan / `'126 yorum'` ~155 ve `'Aktif öğrenci' '18'` ~164 kaldırılır — yorum sistemi M13/P11'e kadar yok, aktif öğrenci sayısı gerçek kaynaktan gelene kadar gösterilmez), `mobile/lib/features/more/presentation/pages/more_page.dart` (`rating: 4.8, reviewCount: 126` ~56 kaldırılır)
- Modify: `mobile/lib/core/network/api_exception.dart`, `mobile/lib/features/students/presentation/cubit/student_detail_cubit.dart`, `.../pages/student_detail_page.dart` (ASCII Türkçe → doğru yazım, ARB)
- Modify: `mobile/test/l10n/localization_test.dart` (tarama `lib/core/**`'u da kapsar)
- Envanter: `grep -rn "StudyDemoBadge\|yakında\|Yakında\|prototip" mobile/lib` → her eşleşme için karar tablosu ILERLEME'ye: (a) P21'de gerçek ekrana bağlanacak → şimdilik **gizle**, (b) özellik yok → kaldır.
- Test: `mobile/test/features/scheduling/scheduling_no_seed_test.dart`, `mobile/test/core/routing/preview_routes_test.dart`

**2026-11-15 kapsam doğrulaması (T1):** Envanter Study ekranlarında da sabit haftalık sayaç, demo hedef/konu puanı ve işlem yapmayan profil satırları gösterdi. Bunlar mevcut T1 envanter yükümlülüğünün parçasıdır; yeni özellik/endpoint eklenmez. `localization_test` zaten `lib/**` tarıyor; eksik olan bozuk yazım sözcükleri ve dokunulan dosyaların ARB geri dönüş korumasıdır. Öğretmen profilinin başlangıç verisi de sunucudan önce sahte bilgi gösterebiliyor; bu görevde gerçek veri gelmeden gösterilmez.

**Ek Files (koddan doğrulandı):**
- Modify: `mobile/lib/features/teacher_profile/data/repositories/teacher_repository_impl.dart`; Test: `mobile/test/features/teacher_profile/teacher_repository_test.dart` (incelemede 404 yanıtının mock kapalıyken de demo profil döndürdüğü doğrulandı; yalnız açık mock tercihi demo döndürebilir, gerçek 404 hata olarak korunur).
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart`, `student_profile_page.dart`, `student_goals_overview_page.dart`, `student_lesson_detail_page.dart`, `student_tests_page.dart` (sahte/işlevsiz parçalar kaldırılır; gerçek verili bölümler korunur, kalan kullanıcı metinleri tr/en ARB).
- Modify: `mobile/lib/features/study/presentation/widgets/study_tab_widgets.dart`, `mobile/lib/features/study/presentation/home/dashboard_stats.dart` (kullanılmayan demo yardımcıları).
- Modify: `mobile/lib/features/membership/presentation/pages/paywall_page.dart`, `mobile/lib/features/membership/presentation/widgets/membership_widgets.dart` (işlevsiz satın alma/gelecek vaatleri gizlenir; mevcut deneme/üyelik korunur; dokunulan metinler ARB).
- Modify: `mobile/lib/l10n/app_tr.arb`, `mobile/lib/l10n/app_en.arb`; gerektiğinde ilgili mevcut ekran widget testlerinin locale kurulumu; yeni `mobile/test/features/study/no_demo_surfaces_test.dart`, `mobile/test/features/teacher_profile/no_fake_profile_test.dart`, `mobile/test/core/network/api_exception_test.dart` (mevcut dosya: eski sözleşme testleri korunur); yeni `mobile/test/features/students/student_detail_messages_test.dart`.
- Docs: `doc/pages/scheduling.md`, `dashboard_preview.md`, `teacher_profile.md`, `more.md`, `students_detail.md`, `study_student.md`, `paywall.md`, `membership.md`, `account_info.md`; `doc/roles/ogrenci.md`, `doc/roles/ogretmen.md`; `doc/architecture/mobile_flutter.md`, `ux_rules.md`, `doc/modules/00_genel_bakis.md`, ilgili M02/M03/M04/M08/M17 dokümanları ve denetim kaydı.

**Ön uyumluluk kontrolü:** T1↔T2 aynı router'ı kullanır: T1 yalnız preview/sahte hesap girişi, rol izin tablosu T2'de kalır. T1↔T3 repository hata mesajı tüketir ama cache/logout sözleşmesini değiştirmez. T1↔T5 Cubit mesajları yerelleştirilir; genel lifecycle/timeout süpürmesi T5'tedir. T1↔T6 ekran davranışları gerçek veriye indirgenir, gerçek cihaz kapanışı T6'da kalır. T2 push rota eşlemesi T4 olay otobüsünden bağımsızdır. T3 reset işlemleri T5 safeEmit ile uyumlu olmalıdır; T4 poll iptali T5 dispose kuralını korur. T1 kendi testi seed/preview/metin gereksinimiyle tutarlı; T2 rota matrisi, T3 cache/logout, T4 poll/push, T5 yaşam döngüsü, T6 cihaz/kapsam testleri kendi dosya ve adımlarıyla tutarlıdır; sonraki görevlerin Files listesi uygulama öncesi ayrıca doğrulanır.

**Interfaces:** Backend/repository sözleşmeleri değişmez. Normal `/account-info` rotası da aynı statik prototipi açtığından preview rotalarıyla birlikte `kDebugMode && enablePreviewRoutes` kapısına alınır; MorePage hesap bilgileri girişi P21 gerçek hesap akışına kadar gizlenir. Preview rotaları derleme modu ile kapatılır; test edilebilir debug tercihi üretim derlemesinde kDebugMode kapısını aşamaz. API mesajlarının mevcut `message` sözleşmesi korunur; istemcinin kendi fallback metinleri yerelleştirilir. Öğrenci detay Cubit mesajları UI'da ARB'ye çevrilecek anlam/anahtar taşır; tüm tüketicileri doğrulanır. Yeni imza gerekiyorsa davranışsız derlenebilir iskelet → gerçek RED → GREEN sırası uygulanır.

**Envanter kararı:** P21 gerçek akışına bağlanacak öğrenci profil düzenleme/veli/gizlilik/abonelik yer tutucuları gizlenir. Veri kaynağı olmayan haftalık sayaç, demo hedef, konu puanı, sahte ilişki ve aylık hedef kartları kaldırılır. API yükleme hatası yakında/demo yerine gerçek hata/yeniden deneme gösterir. P28 satın alma ve kapısı bulunmayan premium vaatleri gösterilmez. Yalnız yorum/dokümantasyon içindeki eşleşmeler işlev sayılmaz; account-info prototipi debug preview rotasının arkasında kalır (gerçek hesap akışı P21).

- [x] **Step 1: Kırmızı testler** — takvim API boş döndüğünde ekranda **hiç** etkinlik yok; gecikmeli gerçek ders yanıtı geldiğinde kullanıcı ekrana dokunmadan ders görünür (seed kaldırma sonrası gerçek veri yolu korunur); release modunda preview rotaları 404/yönlendirme; `localization_test` `api_exception.dart`'taki "hatasi olustu"yu yakalar.
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [x] **Step 5: Doküman + commit** — `doc/pages/scheduling.md`, `dashboard_preview.md`; rapor N-60, P-10 ✅.
  ```bash
  git commit -m "fix(mobile): uretimde sahte takvim etkinlikleri ve onizleme rotalari kaldirildi (N-60, P-10)"
  ```

**Doğrulama (2026-11-15):** Gerçek RED→GREEN (seed/preview/profil/Study/API/metin, gecikmeli gerçek ders, 404 repository ve EN tarama). Son Flutter **385/385**, `flutter analyze --fatal-infos` temiz. Backend **19 mimari / 1312 birim / 214 gerçek Docker integration, 0 başarısız / 0 atlanan**; Release `-warnaserror` 0 uyarı/0 hata. İki inceleme bulgusu giderildi ve dar yeniden inceleme onaylandı. Preview kayıt kapısı debug testinde kapalı/açık ve `kDebugMode` koşulu ile doğrulandı; doğrudan release/profile cihaz koşumu bu görevde yapılmadı. Migration yok.

---

### Task 2: Rol bazlı rota izin tablosu + bildirim rotaları (N-62, N-71 rotalar) ✅

**Files:**
- Create: `mobile/lib/core/routing/route_access.dart` (`Map<String, Set<AppRole>>` + ortak rotalar)
- Modify: `mobile/lib/core/routing/app_router.dart` (redirect tabloya göre; `errorBuilder` → "Sayfa bulunamadı" + ana sayfa düğmesi)
- Modify: `mobile/lib/core/push/**` (push `route` → rol bazlı eşleme; bilinmeyen rota → bildirim merkezi)
- Modify: `mobile/lib/features/parent/presentation/pages/parent_home_page.dart` (mesajlar, bildirimler girişleri)
- Create/Modify: `/parent/assignments`, `/parent/payments` rotaları (mevcut veli ekranlarına eşlenir; yoksa çocuğun ilgili sekmesine)
- Test: `mobile/test/core/routing/role_route_access_test.dart` (her rol × her rota tablosu), `mobile/test/core/push/push_route_mapping_test.dart`

**Interfaces:**
- Ortak (tüm roller): `/messages`, `/messages/:id`, `/notifications`, `/settings/*`, `/account/*`, `/membership`, `/membership/paywall`.
- Veli'ye özel: `/parent/**`. Öğrenci'ye özel: `/student-home/**`, `/study/**`. Öğretmen'e özel: `/dashboard`, `/students/**`, `/scheduling`, `/payments/**`, `/teacher/**`.

**2026-11-15 T2 kapsam doğrulaması:** Push dokunmasını tüketen dosya `mobile/lib/app/app.dart`; Firebase binding rota seçmez. Sunucu ayrıca `/parent/lessons` üretir. Ayrı veli ödeme/ödev listesi yok; mevcut `ParentHomePage` onaylı çocuk seçicisi ve gerçek dashboard verisiyle `overview/assignments/payments/lessons` bölümleri sunacak. Bunlar özet ekranlarıdır; ödeme beyanı ve yeni rol akışları P21'de kalır. `/student/reports` mevcut sözleşmeye göre ortak, `/student/progress` öğrenciye özel kalır. Öğretmen/admin mevcut öğretmen alanını kullanır; çok rollü hesapta mevcut Parent > Teacher > Student önceliği korunur, bilinmeyen rol yalnız ortak ekranlara erişir. İzin tablosu önek yerine kayıtlı tam rota şablonlarını eşler; bilinmeyen URL hata ekranına, bilinmeyen/yetkisiz bildirim hedefi bildirim merkezine gider.

**Ek Files:**
- Create: `mobile/lib/core/push/push_route_mapping.dart`, `mobile/lib/core/routing/route_not_found_page.dart`.
- Modify: `mobile/lib/app/app.dart` (oturum geri yüklenirken gelen bildirimi oturum hazır olunca açar), `mobile/lib/features/notifications/presentation/pages/notifications_page.dart` (aynı güvenli eşleyici; zaten ARB'de).
- Modify: `mobile/lib/l10n/app_tr.arb`, `mobile/lib/l10n/app_en.arb`, `mobile/test/l10n/localization_test.dart` (veli ana ekranı ve hata ekranı ARB).
- Create (test helper): `mobile/test/helpers/fake_parent_repository.dart`.
- Test: `mobile/test/features/parent/parent_home_routes_test.dart`, `mobile/test/core/push/push_navigation_test.dart`; mevcut `mobile/test/core/routing/app_router_test.dart`, `preview_routes_test.dart` regresyonları.
- Docs: `doc/pages/parent.md`, `doc/pages/notifications.md`, `doc/modules/m09_parents.md`, `doc/modules/m11_notifications.md`, `doc/modules/00_genel_bakis.md` ve Step 5 belgeleri; veli sayfası dokümanı olmadığından `parent.md` oluşturulur, `doc/pages/00_pages_index.md` ve `doc/INDEX.md` eklenir.

**Interfaces eki:** `AppRole`, `RouteAccess.roleFor/homeFor/canAccess/redirectFor`, tam şablon izin tablosu; `mapPushRoute(String?, AppRole)` yalnız uygulama içi kayıtlı hedef döndürür. `ParentHomePage(section: ParentHomeSection)` aynı mevcut repository sözleşmesini kullanır; backend ve repository arayüzleri değişmez, sahte imza değişikliği yok. T2/T3 `app.dart` ortak: bu görev yalnız push navigasyonunu değiştirir; cache/reset T3. T2/T4 push ortak: `onMessage`/poll T4. T2/T6 gerçek FCM/cihaz kanıtı T6/P25; burada platform bağımsız akış sınanır.

- [x] **Step 1: Kırmızı testler** — veli `/messages` → kalır; veli `/students` → `/parent`; öğrenci `/payments` → `/student-home`; push `/parent/payments` → veli ödeme ekranı; bilinmeyen rota → hata ekranı (çökme yok).
- [x] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [x] **Step 5: Doküman + commit** — `mobile_flutter.md` (rota tablosu), `ux_rules.md`, `roles/veli.md`; rapor N-62, N-71 (rota) ✅.
  ```bash
  git commit -m "fix(mobile): rol bazli rota izin tablosu, veli ortak ekranlara girer, push rotalari eslendi (N-62, N-71)"
  ```

**Doğrulama (2026-11-15):** Derlenen davranışsız imzalar → gerçek rota/eşleme, veli özet ve sıcak/soğuk push kırmızıları → yeşil. Dinamik rota şablonu regresyonu RED→GREEN. Bağımsız incelemenin tek önemli bulgusu: üç özet rotasında Ana Sayfa seçili olduğu için dönüş etkisiz; üç yeni widget testi 6 yeşil/3 kırmızı → `ParentNavTab.none` ile 9/9. Son `flutter test` **417 geçti**, `flutter analyze --fatal-infos` temiz; analysis_options geri alındı. Release `-warnaserror` **0 uyarı/0 hata**, tool restore başarılı; tam Docker **19 mimari/1312 birim/214 integration, 0 başarısız/0 atlanan**, exit0. NuGet 76 proje/0 advisory; migration yok. Doc health RED0, mevcut1YELLOW+1BLUE. Gerçek FCM/cihaz teslimi doğrulanmadı (Q8/P25); ön plan T4, cache/çıkış T3.

---

### Task 3: Çıkış ve önbellek hijyeni (N-68, N-69, N-70)

**Files:**
- Create: `mobile/lib/core/storage/cache_store.dart` (anahtar biçimi `cache.<userId>.<feature>.<key>`; `clearUser(userId)`, `clearAll()`; okuma `CachedValue<T>{ value, savedAt }`)
- Modify: `mobile/lib/features/payments/data/repositories/payment_repository_impl.dart`, `mobile/lib/features/students/data/repositories/student_repository_impl.dart`, `mobile/lib/features/scheduling/data/repositories/scheduling_repository_impl.dart` (önbelleğe **yalnız ağ hatasında** düş; takvim anahtarı **güne yuvarlanır**)
- Create: `mobile/lib/shared/widgets/stale_data_banner.dart`
- Modify: `mobile/lib/features/auth/data/repositories/auth_repository_impl.dart` (`logout`: sunucu `/api/identity/logout` (hata yutulur ama loglanır) → token sil → `CacheStore.clearAll()`)
- Modify: `mobile/lib/app/app.dart` (kök `NotificationsCubit`, `MembershipCubit` çıkışta `reset()`)
- Test: `mobile/test/core/storage/cache_store_test.dart`, `mobile/test/features/auth/logout_cleanup_test.dart`, repository testleri

- [ ] **Step 1: Kırmızı testler** — 403/500'de önbellek **dönmez**, hata gösterilir; ağ hatasında önbellek + `isStale`; aynı gün iki takvim çağrısı aynı anahtar; çıkış sonrası `SharedPreferences`'ta `cache.` önekli anahtar yok, logout ucu çağrıldı, kök cubit'ler başlangıç durumunda.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `mobile_flutter.md` (önbellek kuralı); rapor N-68/69/70 ✅.
  ```bash
  git commit -m "fix(mobile): onbellek kullanici kapsamli ve yalniz ag hatasinda, cikista tam temizlik (N-68..N-70)"
  ```

---

### Task 4: Ön planda bildirim ve sohbet tazeleme (N-71 `onMessage`)

**Files:**
- Modify: `mobile/lib/core/push/firebase_push_binding.dart` (`FirebaseMessaging.onMessage` → `PushEventBus`)
- Create: `mobile/lib/core/push/push_event_bus.dart`
- Modify: `mobile/lib/features/notifications/**` (rozet + liste tazele), `mobile/lib/features/messaging/presentation/cubit/chat_cubit.dart` (açık sohbette 10 sn poll + push gelince anında tazele; ekrandan çıkınca poll durur), `conversations_cubit.dart`
- Test: `mobile/test/core/push/push_event_bus_test.dart`, `mobile/test/features/messaging/chat_polling_test.dart`

- [ ] **Step 1: Kırmızı testler** — ön planda mesaj push'u → açık sohbet yeni mesajı gösterir, rozet artar; sohbetten çıkınca zamanlayıcı iptal.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `doc/pages/messaging_chat.md`, `notifications.md`; rapor N-71 (`onMessage`) ✅.
  ```bash
  git commit -m "feat(mobile): on planda bildirim ve sohbet tazeleme (N-71)"
  ```

---

### Task 5: Cubit yaşam döngüsü, hata yakalama, yükleme zaman aşımı (N-72)

**Files:**
- Create: `mobile/lib/core/bloc/safe_emit.dart` (`mixin SafeEmit<S> on Cubit<S> { void safeEmit(S s) { if (!isClosed) emit(s); } }`)
- Modify: `chat_cubit`, `conversations_cubit`, `student_report_cubit`, `lesson_resources_cubit`, `progress_cubit`, `teacher_reports_cubit`, `settings_cubit`, `notification_center_cubit`, `password_reset_cubit`, `email_verification_cubit` (async sonrası `safeEmit`)
- Modify: `mobile/lib/features/auth/presentation/cubit/auth_cubit.dart` (`login` genel `catch`; `restoreSession` try/catch → `unauthenticated` + log)
- Modify: `mobile/lib/core/di/injector.dart` / `api_client.dart` (multipart isteklerde `sendTimeout` 120 sn)
- Create: `mobile/test/core/bloc/no_raw_emit_after_await_test.dart` (kaynak taraması: listedeki cubit'lerde `await` sonrası çıplak `emit(` yok)
- Test: `mobile/test/features/auth/auth_cubit_restore_test.dart`

- [ ] **Step 1–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Commit** — rapor N-72 ✅.
  ```bash
  git commit -m "fix(mobile): cubit kapandiktan sonra emit yok, oturum geri yuklemede sonsuz bekleme yok, yukleme zaman asimi (N-72)"
  ```

---

### Task 6: Kapanış

- [ ] **Step 1:** `cd mobile && flutter test --coverage` → başarılı sayı ve kapsam ILERLEME'ye (hedef: öncesinden yüksek).
- [ ] **Step 2:** Gerçek cihazda (Honor 400 Pro, kablosuz ADB) üç rolde giriş → ana ekran → mesajlar → bildirimler → çıkış turu; ekran görüntüsü yolu ILERLEME'ye.
- [ ] **Step 3:** Doküman kapanışı + ILERLEME + commit.
  ```bash
  git commit -m "docs(mobile): P20 kapanisi"
  ```

Güncelleme: 2026-11-15
