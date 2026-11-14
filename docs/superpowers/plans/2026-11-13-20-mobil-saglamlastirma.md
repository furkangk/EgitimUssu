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
- Dokunulan ekran ARB'ye taşınır (K-26); `localization_test.dart` `_migratedFeatures` listesi güncellenir.
- Sahte veri yasağı (2026-09-02 §1): ekrandaki her değer gerçek API'den; demo yalnız `--dart-define=USE_MOCK_FALLBACK=true`.
- Doküman: değişen ekranlar için `doc/pages/*.md`, rota değişikliği için `doc/architecture/mobile_flutter.md` + `ux_rules.md`.

---

### Task 1: Sahte veri ve yer tutucu temizliği (N-60, P-10, N-72 metin)

**Files:**
- Modify: `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart` (`_seedEvents` ve kullanımı silinir; tatil/ödev/ödeme etkinlikleri gerçek kaynaktan gelene kadar **gösterilmez**)
- Modify: `mobile/lib/core/routing/app_router.dart` (`/teacher-panel-preview`, `/account-info-preview` yalnız `kDebugMode`)
- Modify (kullanıcı onaylı 2026-11-14, ekran taslakları O13/O15): `mobile/lib/features/teacher_profile/presentation/pages/teacher_profile_page.dart` (koda gömülü `'4.8'` puan / `'126 yorum'` ~155 ve `'Aktif öğrenci' '18'` ~164 kaldırılır — yorum sistemi M13/P11'e kadar yok, aktif öğrenci sayısı gerçek kaynaktan gelene kadar gösterilmez), `mobile/lib/features/more/presentation/pages/more_page.dart` (`rating: 4.8, reviewCount: 126` ~56 kaldırılır)
- Modify: `mobile/lib/core/network/api_exception.dart`, `mobile/lib/features/students/presentation/cubit/student_detail_cubit.dart`, `.../pages/student_detail_page.dart` (ASCII Türkçe → doğru yazım, ARB)
- Modify: `mobile/test/l10n/localization_test.dart` (tarama `lib/core/**`'u da kapsar)
- Envanter: `grep -rn "StudyDemoBadge\|yakında\|Yakında\|prototip" mobile/lib` → her eşleşme için karar tablosu ILERLEME'ye: (a) P21'de gerçek ekrana bağlanacak → şimdilik **gizle**, (b) özellik yok → kaldır.
- Test: `mobile/test/features/scheduling/scheduling_no_seed_test.dart`, `mobile/test/core/routing/preview_routes_test.dart`

- [ ] **Step 1: Kırmızı testler** — takvim API boş döndüğünde ekranda **hiç** etkinlik yok; release modunda preview rotaları 404/yönlendirme; `localization_test` `api_exception.dart`'taki "hatasi olustu"yu yakalar.
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `doc/pages/scheduling.md`, `dashboard_preview.md`; rapor N-60, P-10 ✅.
  ```bash
  git commit -m "fix(mobile): uretimde sahte takvim etkinlikleri ve onizleme rotalari kaldirildi (N-60, P-10)"
  ```

---

### Task 2: Rol bazlı rota izin tablosu + bildirim rotaları (N-62, N-71 rotalar)

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

- [ ] **Step 1: Kırmızı testler** — veli `/messages` → kalır; veli `/students` → `/parent`; öğrenci `/payments` → `/student-home`; push `/parent/payments` → veli ödeme ekranı; bilinmeyen rota → hata ekranı (çökme yok).
- [ ] **Step 2–4: Kırmızı gör → uygula → yeşil gör.**
- [ ] **Step 5: Doküman + commit** — `mobile_flutter.md` (rota tablosu), `ux_rules.md`, `roles/veli.md`; rapor N-62, N-71 (rota) ✅.
  ```bash
  git commit -m "fix(mobile): rol bazli rota izin tablosu, veli ortak ekranlara girer, push rotalari eslendi (N-62, N-71)"
  ```

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
