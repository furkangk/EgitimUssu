# 📱 Mobil Mimari & UI — Flutter

> **Kapsam:** Flutter uygulamasının (`mobile/`) hem **mimarisi** (katmanlar, state yönetimi, DI, routing, ağ) hem
> **görsel UI rehberi** (tasarım uygulaması + 20 ekran). Eski `tutormatch_flutter_ui_design.md` bu dosyada toplandı.
> Mimari kısımlar **koddan doğrulanmıştır.**
>
> **Otorite:** Görsel token'lar → [`design_system.md`](design_system.md) (çelişkide o esas). Gerçek domain/endpoint →
> [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md). Kanonik değerler → [`../INDEX.md`](../INDEX.md) §0.
> Mevcut app **öğretmen odaklıdır**; öğrenci/veli ekranları planlanandır.
>
> **Güncelleme:** 2026-06-24

---

## İçindekiler

**Mimari:** §1 Yaklaşım · §2 Klasör · §3 Katmanlar · §4 State (Bloc/Cubit) · §5 DI · §6 Routing · §7 Ağ/Config · §8 Feature↔Modül · §9 Rol navigasyonu · §10 Paketler
**Tasarım:** §11 Tasarım sistemi · §12 Ortak widget'lar · §13 Ekran-ekran tasarım (20) · §14 Veri modelleri · §15 Responsive/Erişilebilirlik · §16 Animasyon · §17 Kodlama standartları

---

# BÖLÜM A — MİMARİ

## 1. Genel Yaklaşım

Feature-based **Clean Architecture**. Her özellik (feature) kendi `data / domain / presentation` katmanlarını içerir;
özellikler backend modülleriyle eşleşir (§8). State yönetimi **`flutter_bloc` / Cubit**, DI **`get_it`**, yönlendirme
**`go_router`**, ağ **`dio`** ile yapılır.

> ⚠️ **Önemli düzeltme:** Eski UI dokümanında geçen "Riverpod" önerisi **geçersizdir**. Kanonik ve koddaki gerçek
> state yönetimi **`flutter_bloc` / Cubit**'tir (bkz. [`../INDEX.md`](../INDEX.md) §0).

## 2. Klasör Yapısı (koddan)

```txt
mobile/lib/
├── main.dart                  # giriş: DI kur → runApp(EgitimUssuApp)
├── app/
│   └── app.dart               # MaterialApp.router, BlocProvider<AuthCubit>, tema, tr-TR locale
├── core/
│   ├── config/                # AppConfig (--dart-define ile ortam)
│   ├── constants/             # app_strings.dart
│   ├── di/                    # injector.dart (get_it kayıtları)
│   ├── network/               # api_client.dart (Dio sarmalayıcı), api_exception.dart
│   ├── routing/               # app_router.dart (go_router + redirect)
│   ├── storage/               # token_storage.dart (secure), local_cache.dart (shared prefs)
│   └── theme/                 # app_theme.dart (+ design_system token bağlama)
├── shared/
│   └── widgets/               # app_primary_button, state_views, form_fields … (Atomic)
└── features/<ozellik>/
    ├── data/                  # models/ (DTO + json), repositories/ (impl)
    ├── domain/                # contracts (entity + repository arayüzü)
    └── presentation/          # cubit/ (state), pages/ (ekran), widgets/
```

Mevcut feature'lar: `auth, teacher_profile, students, scheduling, lesson_sessions, assignments, payments, more, dashboard`.

## 3. Katmanlar

| Katman | Sorumluluk | Örnek |
|--------|------------|-------|
| **domain** | Entity + repository **arayüzü** (saf sözleşme) | `auth/domain/repositories/auth_repository.dart` |
| **data** | DTO model (json serileştirme) + repository **impl** (ApiClient kullanır) | `auth/data/repositories/auth_repository_impl.dart` |
| **presentation** | Cubit (state) + pages + widgets | `auth/presentation/cubit/auth_cubit.dart` |

Bağımlılık yönü içe doğrudur: `presentation → domain ← data`. Cubit, repository **arayüzüne** bağımlıdır; somut impl
`get_it` ile enjekte edilir.

## 4. State Yönetimi — `flutter_bloc` / Cubit

Her feature için bir `Cubit<XState>`. State sınıfı immutable + `copyWith`. UI `BlocBuilder`/`BlocListener` ile dinler.

```dart
class AuthCubit extends Cubit<AuthState> {
  AuthCubit(this._repository, {Stream<void>? unauthorizedEvents}) : super(const AuthState()) {
    _unauthorizedSubscription = unauthorizedEvents?.listen((_) => expireSession());
  }
  factory AuthCubit.create() => AuthCubit(
        injector<AuthRepository>(),
        unauthorizedEvents: injector<ApiClient>().unauthorizedEvents, // 401 → oturum düşür
      );

  Future<void> login({required String email, required String password}) async {
    emit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.login(email: email, password: password);
      emit(state.copyWith(status: AuthStatus.authenticated, session: session));
    } on ApiException catch (e) {
      emit(state.copyWith(status: AuthStatus.unauthenticated, errorMessage: e.message));
    }
  }
  // logout / register / restoreSession / expireSession …
}
```

`AuthCubit` uygulama kökünde `BlocProvider.value` ile sağlanır; oturum durumu router redirect'ini besler (§6).

## 5. Bağımlılık Enjeksiyonu — `get_it`

Tüm bağımlılıklar `core/di/injector.dart` içinde `configureDependencies()` ile **lazy singleton** kaydedilir;
`main()` çalışmadan önce çağrılır.

```dart
final GetIt injector = GetIt.instance;
Future<void> configureDependencies() async {
  injector
    ..registerLazySingleton<AppConfig>(AppConfig.fromEnvironment)
    ..registerLazySingleton<TokenStorage>(SecureTokenStorage.new)
    ..registerLazySingleton<LocalCache>(() => cache)
    ..registerLazySingleton<Dio>(() => Dio(BaseOptions(baseUrl: injector<AppConfig>().apiBaseUrl, /* timeouts */)))
    ..registerLazySingleton<ApiClient>(() => ApiClient(dio: injector<Dio>(), tokenStorage: injector<TokenStorage>()))
    ..registerLazySingleton<AuthRepository>(() => AuthRepositoryImpl(/* apiClient, tokenStorage, localCache, config */))
    ..registerLazySingleton<StudentRepository>(() => StudentRepositoryImpl(/* … */));
  // … diğer repository'ler
}
```

## 6. Yönlendirme — `go_router`

`AppRouter`, `AuthCubit.stream`'i `refreshListenable` olarak kullanır ve **redirect** ile oturuma göre yönlendirir:

```dart
redirect: (context, state) {
  final status = authCubit.state.status;
  final onAuthScreen = ['/', '/role-selection', '/login', '/register'].contains(state.matchedLocation);
  if (status == AuthStatus.initial || status == AuthStatus.loading) return onAuth/preview ? null : '/';
  if (status == AuthStatus.unauthenticated) return onAuthScreen ? null : '/';
  if (onAuthScreen || state.matchedLocation == '/') return '/dashboard';  // giriş yapılmış
  return null;
}
```

Başlıca rotalar (koddan): `/` (Welcome), `/role-selection`, `/login?role=`, `/register`, `/dashboard`,
`/teacher-profile`, `/more`, `/account-info`, `/students`, `/students/:studentId`, `/scheduling`,
`/lesson-sessions` (+`/detail`, `/detail/note`), `/lesson-notes/new`, `/assignments/:lessonSessionId`,
`/assignments/new`, `/payments`, `/payments/new`. Önizleme: `/teacher-panel-preview`, `/account-info-preview`.

> Not: Mevcut rotalar **öğretmen akışıdır**. Öğrenci/veli rota grupları planlanandır (§9).

## 7. Ağ Katmanı, Config & Depolama

**`ApiClient`** — `Dio` sarmalayıcı. İstekte interceptor access token'ı `Authorization: Bearer` ekler;
`401` gelirse `unauthorizedEvents` yayını ile oturum düşürülür. `post/put/get/getList` metotları `ApiException`'a çevirir.

```dart
_dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) async {
  final token = await _tokenStorage.readAccessToken();
  if (token != null && token.isNotEmpty) options.headers['Authorization'] = 'Bearer $token';
  handler.next(options);
}));
```

**`AppConfig`** — `--dart-define` ile ortam değişkenleri:

| Değişken | Varsayılan | Açıklama |
|----------|------------|----------|
| `API_BASE_URL` | `http://10.0.2.2:8080` | Backend taban URL (Android emülatör localhost) |
| `APP_ENV` | `development` | `development` / `beta` / `production` |
| `USE_MOCK_FALLBACK` | `true` | Geliştirmede mock'a düşme |
| `MOCK_FALLBACK_FEATURES` | `*` | Hangi feature'lar mock'a düşer |

`isMockFallbackEnabled(feature)` production-benzeri ortamda kapanır; geliştirmede backend hazır olmayan feature'lar mock veri döndürür.

**Depolama:** `SecureTokenStorage` (token'lar — secure storage), `SharedPrefsLocalCache` (`LocalCache` — basit önbellek/offline).

## 8. Feature ↔ Backend Modül Eşlemesi

| Mobil feature | Backend modül | Not |
|---------------|---------------|-----|
| `auth` | Identity (M01) | giriş/kayıt/rol/oturum |
| `teacher_profile` | Teachers (M02) | profil |
| `students` | Students (M03) | öğretmenin öğrenci yönetimi |
| `scheduling` | Scheduling (M04) | takvim (syncfusion) |
| `lesson_sessions` | LessonSessions (M05) | oturum + not |
| `assignments` | Assignments (M06) | ödev/takip |
| `payments` | Payments (M07) | ödeme liste/form |
| `more` | Settings (M15) | ayarlar/hesap |
| `dashboard` | (çapraz) | öğretmen ana ekranı |
| _(planlanan)_ | Study/Parents/ProgressTracking/Matching/Reviews/Reporting/Messaging/Membership/Feedback | öğrenci/veli/yeni özellik ekranları |

## 9. Rol Bazlı Navigasyon (Planlanan)

Mevcut app öğretmen odaklı. `app_router.dart` redirect'i ileride role göre farklı kabuğa yönlendirecek:

- **Öğretmen:** Ana Sayfa · Dersler · Öğrenciler · Takvim · Finans · Diğer
- **Öğrenci:** Ana Sayfa · Çalışma (kronometre) · Dersler · Gelişim · Profil
- **Veli:** Ana Sayfa · Raporlar · Bildirimler · Profil

Yeni feature klasörleri (planlanan): `study` (M08), `progress` (M10), `parent` (M09), `messaging` (M16),
`listings` (M12), `reviews` (M13), `membership` (M17 — paywall + reklam yerleşimi), `feedback` (M18).
Detay: [`../roles/`](../roles/00_roller_genel_bakis.md).

## 10. Paketler

```yaml
dependencies:
  flutter_bloc: ^8        # state (Cubit)  ← kanonik
  go_router: ^14          # routing
  dio: ^5                 # ağ
  get_it: ^7              # DI
  flutter_secure_storage  # token
  shared_preferences      # local cache
  syncfusion_flutter_calendar + syncfusion_localizations  # takvim
  google_fonts            # Inter
  fl_chart                # grafik (donut/line) — öğrenci/veli ekranları
  table_calendar          # (alternatif takvim)
  percent_indicator       # circular progress
  intl                    # tarih/locale
  file_picker             # dosya ekleme (ders notu)
  cached_network_image    # avatar/görsel
  flutter_animate         # mikro animasyon
```

> Sürümleri `mobile/pubspec.yaml` belirler (burada anlamsal/yön gösterici). `pubspec.lock` `main`'e commit edilmez (bkz. `CLAUDE.md`).

---

# BÖLÜM B — GÖRSEL UI REHBERİ

## 11. Tasarım Sistemi (Flutter Bağlama)

> Token **değerleri** [`design_system.md`](design_system.md)'dendir; çelişkide o esastır. Aşağıdaki Dart sınıfları
> o token'ların Flutter karşılığıdır (`core/theme/`).

```dart
class AppColors {
  static const Color primary = Color(0xFF082B4F);
  static const Color primaryDark = Color(0xFF061F3A);
  static const Color primaryLight = Color(0xFFEAF2FB);
  static const Color secondary = Color(0xFF3D8BFF);
  static const Color accentOrange = Color(0xFFFFA726);
  static const Color accentGreen = Color(0xFF20B486);
  static const Color accentRed = Color(0xFFFF5A5F);
  static const Color accentTeal = Color(0xFF20A4A9);
  static const Color background = Color(0xFFF7F9FC);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color textPrimary = Color(0xFF111827);
  static const Color textSecondary = Color(0xFF6B7280);
  static const Color textMuted = Color(0xFF9CA3AF);
  static const Color border = Color(0xFFE5E7EB);
  static const Color divider = Color(0xFFF0F2F5);
}

class AppTextStyles {
  static const h1 = TextStyle(fontSize: 28, fontWeight: FontWeight.w800, height: 1.2);
  static const h2 = TextStyle(fontSize: 22, fontWeight: FontWeight.w700, height: 1.25);
  static const h3 = TextStyle(fontSize: 18, fontWeight: FontWeight.w700, height: 1.3);
  static const title = TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.35);
  static const body = TextStyle(fontSize: 14, fontWeight: FontWeight.w500, height: 1.45);
  static const caption = TextStyle(fontSize: 12, fontWeight: FontWeight.w500, height: 1.35);
  static const small = TextStyle(fontSize: 10, fontWeight: FontWeight.w500, height: 1.3);
}

class AppSpacing { static const xs=4.0, sm=8.0, md=12.0, lg=16.0, xl=20.0, xxl=24.0, xxxl=32.0; }
class AppRadius  { static const sm=8.0, md=12.0, lg=16.0, xl=20.0, pill=999.0; }

final softShadow = [BoxShadow(color: Colors.black.withOpacity(0.04), blurRadius: 14, offset: const Offset(0, 6))];
```

**Layout kuralları:** `SafeArea`, yatay padding sabit 16, geniş ekranda içerik maks. 430-480 px. Genel şablon:
`Scaffold(backgroundColor: background, appBar: AppHeader, body: SafeArea(Padding(symmetric(horizontal:16))), bottomNavigationBar: AppBottomNav)`.

## 12. Ortak Widget'lar (`shared/widgets`)

> **Tam liste, API ve durum (🟢/🟡/🔴) için → [`widgets.md`](widgets.md) (Ortak Widget Kataloğu).** Aşağıda özet.

Sürdürülebilirlik için tüm ekranlar şu component'leri kullanır:

- **AppButton** — varyantlar: primary (koyu lacivert, ~48px, radius 12), secondary outline, danger, icon, small.
- **AppCard** — beyaz zemin, hafif border + `softShadow`, radius 16, padding 14-16.
- **AppHeader** — geri/bildirim/menü/sadece-başlık varyantları.
- **AppBottomNav** — rol bazlı item seti (§9); aktif item primary, pasif gri.
- **MetricCard** — başlık + ana değer + alt açıklama (+ ikon/trend/progress). KPI/özet kartları.
- **AppSegmentedTab** — segment kontrolü (aktif: koyu lacivert+beyaz; pasif: açık gri+gri). Detay → [`../tab_widget.md`](../tab_widget.md).
- Liste tile'ları: `StudentListTile`, `LessonCard`, `AssignmentTile`, `PaymentTile`, `NotificationTile`, `ProfileMenuTile`.
- Durum görünümleri: `EmptyState`, `LoadingState`, `ErrorState` (`state_views.dart`), form alanları (`form_fields.dart`).

**Geliştirme sırası:** AppTheme → AppColors → AppTextStyles → AppSpacing → AppButton → AppCard → AppHeader →
AppBottomNav → AppTextField → AppAvatar → AppBadge → AppSegmentedTab → MetricCard → LessonCard → StudentListTile →
AssignmentTile → PaymentTile → NotificationTile → ProfileMenuTile → EmptyState/LoadingState/ErrorState.

## 13. Ekran-Ekran Tasarım

> Aşağıdaki ekranlar, ilgili sayfa dokümanlarıyla ([`../pages/`](../pages/00_pages_index.md)) eşleşir.
> Veri alanları idealize olabilir; gerçek alanlar için modül dokümanlarına bakın (§14).

### 13.1 Splash / Welcome
**Amaç:** Karşılama + marka + giriş/kayıt. **UI:** üstte logo+ad, slogan ("Özel ders süreçlerinizi tek bir yerde
yönetin."), ortada 3D illüstrasyon, altta `Giriş Yap` / `Kayıt Ol`. **Flutter:** `Column`+`Spacer` ile dengeli; SVG logo, asset illüstrasyon. → `pages/auth_welcome.md`

### 13.2 Hesap Türü Seçimi
**Amaç:** Rol belirleme. **UI:** başlık "Hesap türünü seçin", 3 `RoleSelectionCard` (Öğretmen/Öğrenci/Veli; avatar+başlık+açıklama+ok), altta giriş bağlantısı.
`RoleSelectionCard(title, description, imagePath, onTap)`. → `pages/auth_role_selection.md`

### 13.3 Öğretmen Paneli (Dashboard)
**Amaç:** Günlük operasyon tek ekranda. **Bölümler:** header (sağda bildirim+badge), günlük özet kartları
(Streak `14 Gün` turuncu / Bugünün Dersleri `2` koyu lacivert), yaklaşan ders kartı (öğrenci avatar + ders + online badge),
hızlı işlemler (Ders Planla / Ödev Ver / Not Ekle / Ödeme Ekle), son aktiviteler listesi, bottom nav.
**Flutter:** `CustomScrollView`; KPI `Row`+`Expanded`; quick actions `GridView.count`; aktiviteler `ListView.separated(shrinkWrap)`. → `pages/dashboard.md`

### 13.4 Öğrenci Listesi
**Amaç:** Öğretmenin öğrencileri araması/görmesi. **UI:** header, arama input, liste, altta `+ Yeni Öğrenci Ekle`, bottom nav.
**Kart (`StudentListTile`):** avatar, ad, sınıf, son ders, sağda skor (≥85 yeşil / 70-84 turuncu / ≤69 kırmızı).
`StudentListTile(name, grade, lastLessonText, score, avatarUrl, onTap)`. → `pages/students_list.md`

### 13.5 Öğrenci Detay
**Amaç:** Bir öğrencinin ders/ödev/ödeme/performansı. **UI:** header (geri+menü), profil, segment tab
(Genel/Dersler/Ödevler/Ödemeler), metrik kartları (Ders Saati `36`, Ortalama `92`, Devam `%95`), yakın dersler listesi, `Ders Planla`. → `pages/students_detail.md`

### 13.6 Takvim
**Amaç:** Dersleri tarih bazlı gösterme + planlama. **UI:** geri, aylık takvim (seçili gün koyu lacivert daire),
seçili günün ders listesi (saat + ders/öğrenci + tip + renkli nokta), altta `+ Ders Planla`.
**Paket:** kodda **`syncfusion_flutter_calendar`** (doküman alternatifi `table_calendar`). → `pages/scheduling.md`

### 13.7 Ders Planla
**Amaç:** Yeni ders oluşturma. **Form:** Öğrenci, Ders, Tarih, Saat aralığı, Ders şekli, Tekrar, Not.
**Validasyon:** öğrenci/ders/tarih zorunlu; başlangıç < bitiş; aynı saatte çakışma uyarısı. Alt buton `Kaydet`.

### 13.8 Ders Notu
**Amaç:** İşlenen ders + ödev notu. **UI:** header, ders bilgi alanı, ders notu input, ödev input, dosya ekleme, `Kaydet`.
**Flutter:** çok satırlı `TextFormField(maxLines:5)`, `file_picker`; dosya kartı (ikon+ad+boyut+chevron). → `pages/lesson_note_form.md`

### 13.9 Ödevler (Öğretmen)
**Amaç:** Verilen ödevler + teslim durumu. **UI:** header, segment tab (Verilenler/Teslim Edilenler), liste, `+ Yeni Ödev Ver`.
**Kart:** başlık, öğrenci, teslim tarihi, teslim oranı (`2/5`), progress bar; durum rengi (yeşil/turuncu/kırmızı). → `pages/assignment_follow_up.md`

### 13.10 Ödeme Takibi
**Amaç:** Alacak/tahsil/bekleyen takibi. **UI:** header+filtre, 3 özet kart (Toplam Alacak ₺12.500 / Tahsil ₺7.500 / Bekleyen ₺5.000),
segment (Tümü/Tahsil/Bekleyen), liste (avatar+ad+ay+tutar+durum), `+ Ödeme Ekle`. Durum: Ödendi yeşil / Bekliyor turuncu / Gecikti kırmızı. → `pages/payments_list.md`

### 13.11 Öğrenci Paneli
**Amaç:** Öğrencinin günlük çalışma/ders/gelişimi. **UI:** karşılama ("Merhaba, Ali 👋"), streak kartı,
bugünkü çalışma süresi (koyu lacivert büyük kart + progress/circular), hızlı işlemler (Çalışma Odası/Test Çöz/Ödevlerim/Derslerim), yaklaşan ders. _(planlanan)_

### 13.12 Çalışma Odası
**Amaç:** Odaklı çalışma sayacı. **UI:** ders/konu kartı, büyük sayaç (`01:15:24`), kırmızı `Durdur`, günlük hedef (progress + %).
**Flutter:** `Timer.periodic`; arka plan için lifecycle yönetimi; local cache + backend sync. _(planlanan, M08)_

### 13.13 Gelişim Analizi
**Amaç:** Çalışma süresi + test başarısı + ders performansı. **UI:** segment (Haftalık/Aylık), ders dağılımı donut,
test başarı kartı (`%82`), haftalık line chart. **Paket:** `fl_chart` (`PieChart`/`LineChart`). _(planlanan, M10)_

### 13.14 Veli Paneli
**Amaç:** Velinin sade takibi. **UI:** karşılama, çocuk kartı, 3 metrik (Bu Hafta Çalışma `08:45`, Ders Saati `03`, Ortalama `%88`), haftalık bar chart. _(planlanan, M09)_

### 13.15 Veli — Öğrenci Detay
**Amaç:** Daha detaylı inceleme. **UI:** profil, segment (Genel Bakış/Dersler/Ödevler/Ödemeler), çalışma süresi kartı (haftalık karşılaştırma `%35 ↑`), ders dağılımı donut. _(planlanan)_

### 13.16 Dersler (Öğrenci/Veli)
**Amaç:** Yaklaşan/geçmiş dersler. **UI:** segment (Yaklaşan/Geçmiş), ders kartı (ad, tarih/saat, tip badge Online/Yüz Yüze, durum badge Tamamlandı/Planlandı). → `pages/lesson_sessions_list.md`

### 13.17 Ödev Durumu (Öğrenci)
**Amaç:** Aktif/tamamlanan ödevler. **UI:** segment (Aktif/Tamamlananlar), ödev kartı (başlık, veriliş, teslim, durum). Durum: Devam Ediyor mor/lacivert / Teslim Edildi yeşil / Gecikti kırmızı. _(planlanan)_

### 13.18 Test Performansı
**Amaç:** Test sonuçları + trend. **UI:** filtre (Aylık), ortalama başarı kartı (`%82`, `%6 ↑`), son testler listesi (ad, tarih, %). _(planlanan)_

### 13.19 Bildirimler
**Amaç:** Ders/ödev/not/ödeme hatırlatmaları. **UI:** segment (Tümü/Okunmamış/Önemli), bildirim kartı (ikon+başlık+açıklama+tarih).
Tipler: Ders mor/pembe · Ödev yeşil · Not mavi · Ödeme kırmızı.

### 13.20 Profil
**Amaç:** Hesap/güvenlik/bildirim/yardım/çıkış. **UI:** avatar+ad+rol, menü (Kişisel Bilgiler, Şifre Değiştir,
Bildirim Ayarları, Gizlilik, Yardım & Destek, Çıkış Yap — kırmızı), bottom nav. → `pages/more.md`, `pages/account_info.md`

## 14. Veri Modelleri (UI — idealize)

> ⚠️ Bu modeller **UI çizimine yönelik basitleştirilmiş** örneklerdir; gerçek backend domain modelleriyle birebir
> aynı **değildir** (ör. `Student.averageScore`/`attendanceRate` gerçek `StudentProfile`'da yoktur). **Gerçek için** →
> [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) ve [`../modules/veri_modeli.md`](../modules/veri_modeli.md).

```dart
enum UserRole { teacher, student, parent }
enum LessonType { online, faceToFace }       enum LessonStatus { planned, completed, cancelled }
enum AssignmentStatus { active, submitted, completed, late }   enum PaymentStatus { paid, pending, overdue }

class Student { final String id, fullName, grade; final String? avatarUrl; final int averageScore, attendanceRate; final DateTime? lastLessonDate; }
class Lesson  { final String id, studentId, studentName, subject, topic; final DateTime startTime, endTime; final LessonType type; final LessonStatus status; final String? note; }
class Assignment { final String id, title, studentId, studentName; final DateTime assignedAt, dueDate; final AssignmentStatus status; final int submittedCount, totalCount; }
class Payment { final String id, studentId, studentName; final double amount; final String period; final PaymentStatus status; final DateTime? paidAt; }
class StudySession { final String id, studentId, subject, topic; final DateTime startedAt; final DateTime? endedAt; final Duration duration; }
```

## 15. Responsive & Erişilebilirlik

- Metin taşmaları `ellipsis`; KPI kartlarında `Expanded`; grafik alanları oranlı (sabit yükseklik değil); alt buton `SafeArea`.
- Tıklanabilir alan min `44×44`; renk tek başına durum göstergesi değil (metin/badge ile); font scaling; görsellere semantic label.

## 16. Animasyon

Hafif mikro etkileşimler: sayfa geçişi fade+slide, kart scale, progress/circular dolum animasyonu, liste staggered fade-in,
form hatası için sade error text. Paketler: `flutter_animate`, `animations`.

## 17. Kodlama Standartları

- Büyük ekranları küçük widget'lara böl; sayfa dosyasında iş mantığı olmasın (Cubit'e taşı); her kart ayrı widget.
- Sabit string'ler localization'a (`app_strings.dart`); renkler `AppColors`, metin `AppTextStyles` üzerinden — **doğrudan değer yazma**.
- Dosya adı: `*_page.dart`, `*_cubit.dart`, `*_state.dart`, `*_tile.dart`, `*_card.dart`, `*_repository_impl.dart`.

```dart
class SectionHeader extends StatelessWidget {        // örnek reusable widget
  final String title; final String? actionText; final VoidCallback? onActionTap;
  const SectionHeader({super.key, required this.title, this.actionText, this.onActionTap});
  @override Widget build(BuildContext c) => Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
    Text(title, style: AppTextStyles.title),
    if (actionText != null) GestureDetector(onTap: onActionTap,
      child: Text(actionText!, style: AppTextStyles.caption.copyWith(color: AppColors.primary, fontWeight: FontWeight.w700))),
  ]);
}
```

---

> İlgili: sistem geneli → [`00_genel_bakis.md`](00_genel_bakis.md) · token'lar → [`design_system.md`](design_system.md) ·
> sayfalar → [`../pages/00_pages_index.md`](../pages/00_pages_index.md) · tab widget → [`../tab_widget.md`](../tab_widget.md) ·
> backend (API gerçeği) → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)

*Mobil Mimari & UI (Flutter) | Güncelleme: 2026-06-24*
