import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';

/// P13 Task 6 / D-19 + D-20: Yerelleştirme ve metin kalitesi.
///
/// Üç kural:
/// 1. `app_tr.arb` ve `app_en.arb` **aynı anahtar kümesine** sahip olmalı — biri eksik kalırsa
///    o dilde metin sessizce Türkçe görünür ya da anahtar adı ekrana basılır.
/// 2. Hiçbir Türkçe değer **Türkçe karaktersiz** yazılmamalı ("Cikis yap" değil "Çıkış yap").
/// 3. Aynı kural `lib/**` içindeki **tüm** kullanıcıya görünen dizeler için geçerli — asıl
///    regresyon koruması budur: bugün düzeltilen yazımların yarın geri gelmesini engeller.
///
/// (3) neden gerekli: ekranların ARB'ye taşınması Task 6b'de sürüyor; taşınmamış ekranlarda metin
/// hâlâ `.dart` dosyasında duruyor ve orada bozulabilir.
void main() {
  final projectRoot = Directory.current.path;

  /// Türkçe karaktersiz yazılmış, tek başına anlamı belirsiz olmayan biçimler.
  /// Yalnız **kesin** hatalar: "Devam"/"Kaydet" gibi zaten doğru yazımlar listede yok.
  const brokenSpellings = <String, String>{
    'Cikis': 'Çıkış',
    'cikis': 'çıkış',
    'Odeme': 'Ödeme',
    'odeme': 'ödeme',
    'Ogretmen': 'Öğretmen',
    'ogretmen': 'öğretmen',
    'Ogrenci': 'Öğrenci',
    'ogrenci': 'öğrenci',
    'Ogrenim': 'Öğrenim',
    'Odev': 'Ödev',
    'odev': 'ödev',
    'Calisma': 'Çalışma',
    'calisma': 'çalışma',
    'Gecmis': 'Geçmiş',
    'gecmis': 'geçmiş',
    'Duzenle': 'Düzenle',
    'duzenle': 'düzenle',
    'Iptal': 'İptal',
    'Ozet': 'Özet',
    'ozet': 'özet',
    'Gunluk': 'Günlük',
    'gunluk': 'günlük',
    'Haftalik': 'Haftalık',
    'haftalik': 'haftalık',
    'Aylik': 'Aylık',
    'aylik': 'aylık',
    'Uyelik': 'Üyelik',
    'uyelik': 'üyelik',
    'Hakkimizda': 'Hakkımızda',
    'Yardim': 'Yardım',
    'yardim': 'yardım',
    'Sinif': 'Sınıf',
    'sinif': 'sınıf',
    'Sifre': 'Şifre',
    'sifre': 'şifre',
    'Bugun': 'Bugün',
    'bugun': 'bugün',
    'Basari': 'Başarı',
    'basari': 'başarı',
    'guvenlik': 'güvenlik',
    'ayarlari': 'ayarları',
    'hatirlatma': 'hatırlatma',
    'gorunum': 'görünüm',
    'baglanti': 'bağlantı',
    'Baglanti': 'Bağlantı',
    'gelisim': 'gelişim',
    'Gelisim': 'Gelişim',
    'yukleniyor': 'yükleniyor',
    'yuklenemedi': 'yüklenemedi',
    'secimi': 'seçimi',
    'kaydi': 'kaydı',
    'Kaydi': 'Kaydı',
    'hatasi': 'hatası',
    'yaniti': 'yanıtı',
    'asimina': 'aşımına',
    'ugradi': 'uğradı',
    'baglanilamadi': 'bağlanılamadı',
    'Brans': 'Branş',
    'Musait': 'Müsait',
  };

  /// Bozuk yazımı **kelime başında** arar.
  ///
  /// Sondaki sınır bilerek yok: Türkçe eklemeli bir dildir ve "ogrenci" → "ogrencinin",
  /// "hatirlatma" → "hatirlatmalari" biçiminde uzar; bunlar da yakalanmalı.
  /// Baştaki sınır ise zorunlu — aksi halde "rozet" içindeki "ozet" yanlışlıkla eşleşiyordu.
  bool hasBroken(String text, String broken) =>
      RegExp('\\b${RegExp.escape(broken)}').hasMatch(text);

  Map<String, dynamic> readArb(String locale) {
    final file = File('$projectRoot/lib/l10n/app_$locale.arb');
    expect(file.existsSync(), isTrue, reason: '${file.path} bulunamadı.');
    return jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
  }

  /// Meta anahtarları (`@key`, `@@locale`) dışındaki gerçek çeviri anahtarları.
  Set<String> translationKeys(Map<String, dynamic> arb) =>
      arb.keys.where((key) => !key.startsWith('@')).toSet();

  group('ARB dosyaları', () {
    test('tr ve en aynı anahtar kümesine sahip', () {
      final tr = translationKeys(readArb('tr'));
      final en = translationKeys(readArb('en'));

      expect(
        tr.difference(en),
        isEmpty,
        reason: 'en dosyasında eksik anahtarlar',
      );
      expect(
        en.difference(tr),
        isEmpty,
        reason: 'tr dosyasında eksik anahtarlar',
      );
      expect(tr, isNotEmpty, reason: 'ARB boş olamaz');
    });

    test('Türkçe değerlerde Türkçe karaktersiz yazım yok', () {
      final tr = readArb('tr');
      final violations = <String>[];

      tr.forEach((key, value) {
        if (key.startsWith('@') || value is! String) return;
        for (final entry in brokenSpellings.entries) {
          if (hasBroken(value, entry.key)) {
            violations.add(
              '$key: "$value" → "${entry.key}" yerine "${entry.value}"',
            );
          }
        }
      });

      expect(violations, isEmpty, reason: violations.join('\n'));
    });

    test('anahtar adları <ekran>_<eleman> biçiminde', () {
      final keys = translationKeys(readArb('tr'));
      final invalid = keys.where(
        (key) => !RegExp(r'^[a-z][a-zA-Z0-9]*_[a-zA-Z0-9]+$').hasMatch(key),
      );

      expect(
        invalid,
        isEmpty,
        reason: 'geçersiz anahtar adı: ${invalid.join(", ")}',
      );
    });
  });

  group('Kaynak kodundaki metinler (D-20)', () {
    test('lib/** içinde Türkçe karaktersiz yazım yok', () {
      final violations = <String>[];
      final quoted = RegExp(r"'([^'\n]{3,})'");

      for (final file
          in Directory('$projectRoot/lib')
              .listSync(recursive: true)
              .whereType<File>()
              .where((file) => file.path.endsWith('.dart'))) {
        // Üretilmiş l10n kodu kontrol dışı — kaynağı ARB, o ayrıca denetleniyor.
        if (RegExp(r'/l10n/app_localizations.*\.dart$').hasMatch(file.path)) {
          continue;
        }

        final lines = file.readAsLinesSync();
        for (var index = 0; index < lines.length; index++) {
          final line = lines[index];
          // Rota yolları, API uçları ve import'lar kullanıcıya görünmez.
          if (line.contains('import ') ||
              line.contains('/api/') ||
              line.contains('path:')) {
            continue;
          }

          for (final match in quoted.allMatches(line)) {
            final text = match.group(1)!;
            // Yalnız insan metni: en az bir boşluk ya da baş harfi büyük bir kelime.
            if (!text.contains(' ') &&
                !RegExp(r'^[A-ZÇĞİÖŞÜ]').hasMatch(text)) {
              continue;
            }

            for (final entry in brokenSpellings.entries) {
              if (hasBroken(text, entry.key)) {
                final relative = file.path.replaceFirst('$projectRoot/', '');
                violations.add(
                  '$relative:${index + 1}  "$text"  → "${entry.key}" yerine "${entry.value}"',
                );
                break;
              }
            }
          }
        }
      }

      expect(
        violations,
        isEmpty,
        reason: '${violations.length} bozuk yazım:\n${violations.join('\n')}',
      );
    });
  });

  _runMigratedFeatureScan(projectRoot);
}

/// P13 Task 6b: ARB'ye taşınmış feature'larda **hardcoded kullanıcı metni kalmadığını**
/// doğrular. Liste her turda bir feature büyür; taşınan ekran bir daha geri kaymaz.
///
/// Tarayıcı kasıtlı olarak ihtiyatlıdır: teknik dize (rota, anahtar, kimlik, dosya yolu),
/// ham `RegExp` deseni ve yalnız enterpolasyondan ibaret biçimlendirme dizeleri elenir.
/// Kodda kalması **doğru** olan dizeler [_contractLiterals] içinde gerekçesiyle listelenir;
/// önizleme/sahte veri blokları kaynakta `// l10n-ignore-start` … `// l10n-ignore-end`
/// işaretiyle kapsam dışına alınır.
///
/// `auth` ve `more` **henüz listede değil**: Task 6 bu ikisini "taşındı" saymıştı ama
/// tarama `auth`ta 38, `more`da 53 kalıntı buldu (`account_info_page`, `device_sessions_page`,
/// rol etiketleri, hata mesajları). Bunlar Task 6b'nin kendi turlarına alındı.
const _migratedFeatures = <String>['dashboard'];

const _migratedFiles = <String>[
  'features/messaging/presentation/pages/chat_page.dart',
  'features/messaging/presentation/pages/messaging_inbox_page.dart',
  'features/messaging/presentation/widgets/messaging_widgets.dart',
  'shared/widgets/stale_data_banner.dart',
  'features/parent/presentation/pages/parent_home_page.dart',
  'core/routing/route_not_found_page.dart',
  'features/scheduling/presentation/pages/scheduling_page.dart',
  'features/teacher_profile/presentation/pages/teacher_profile_page.dart',
  'features/more/presentation/pages/more_page.dart',
  'features/students/presentation/pages/student_detail_page.dart',
  'features/study/presentation/pages/student_home_page.dart',
  'features/study/presentation/pages/student_profile_page.dart',
  'features/study/presentation/pages/student_goals_overview_page.dart',
  'features/study/presentation/pages/student_lesson_detail_page.dart',
  'features/study/presentation/pages/student_tests_page.dart',
  'features/study/presentation/widgets/study_tab_widgets.dart',
  'features/membership/presentation/pages/paywall_page.dart',
  'features/membership/presentation/widgets/membership_widgets.dart',
];

/// Kaynakta kalması doğru olan dizeler → gerekçe.
const _contractLiterals = <String, String>{
  'Online':
      'ders biçimi sözleşme değeri (API `lessonFormat`, `LessonDetailPayload.modeLabel`)',
  'InPerson': 'API `lessonFormat` sözleşme değeri',
  'OnlineAndInPerson': 'API `lessonFormat` sözleşme değeri',
  'EEE': 'Syncfusion takvimi gün adı biçim deseni',
  'd MMMM, EEEE': 'Intl DateFormat tarih/gün biçim deseni',
  'MMMM y': 'Intl DateFormat tarih/gün biçim deseni',
  'd MMM y': 'Intl DateFormat tarih/gün biçim deseni',
  'd MMM': 'Intl DateFormat tarih/gün biçim deseni',
  'd MMMM EEEE': 'Intl DateFormat tarih/gün biçim deseni',
  'EEEE': 'Intl DateFormat tarih/gün biçim deseni',
  'd MMMM y EEEE': 'Intl DateFormat tarih/gün biçim deseni',

  'Yuz yuze':
      'aynı sözleşmenin diğer ucu; `lesson_sessions` taşınınca bool/enum olacak (Task 6b sırası #3)',
  'Planned': 'ders durumu sözleşme değeri',
  'Completed': 'ders durumu sözleşme değeri',
  'Cancelled': 'ders durumu sözleşme değeri',
  'Draft': 'ders durumu sözleşme değeri',
  'TRY': 'ISO 4217 para birimi kodu — çeviriye girmez',
  'OG':
      'Veri bulunmadığında kullanılan avatar baş harf glifi; cümle/başlık değil',
};

void _runMigratedFeatureScan(String projectRoot) {
  /// Enterpolasyonu ayıklar. Ayıklanamayan (dengesiz `${`) dizeler `null` döner.
  String? stripInterpolation(String text) {
    final stripped = text
        .replaceAll(RegExp(r'\$\{[^{}]*\}'), '')
        .replaceAll(RegExp(r'\$[a-zA-Z_][a-zA-Z0-9_]*'), '');
    if (stripped.contains(r'$') ||
        stripped.contains('{') ||
        stripped.contains('}')) {
      return null;
    }
    return stripped;
  }

  bool looksLikeUserText(String raw) {
    final content = stripInterpolation(raw);
    if (content == null) return false;

    final text = content.trim();
    // Harf içermeyen biçimlendirme parçaları ("  -  ", ":", "%").
    if (!RegExp('[A-Za-zÇĞİÖŞÜçğıöşü]').hasMatch(text)) return false;
    // Rota (`/api/...`, `/login?role=...`), URL ve e-posta kullanıcıya metin sunmaz.
    // Ham dizeye bakılır: sayaçlardan sonra kalan bölü işareti rota değildir.
    if (raw.startsWith('/') || raw.contains('://')) return false;
    if (RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(text)) return false;
    // Enterpolasyon + kelime = derlenmiş kullanıcı metni ("$count gün"): uzunluğa bakma.
    if (content != raw) return true;
    if (text.length < 2) return false;
    // Boşluksuz teknik dizeler: tanımlayıcı, anahtar, kimlik, dosya yolu.
    if (!RegExp(r'\s').hasMatch(text)) {
      // Tek kelimelik **başlık** da olabilir ("Dersler") — küçük harfle başlamıyorsa
      // ve yol/ayraç içermiyorsa gerçek metin sayılır.
      return !RegExp(r'^[a-z0-9]').hasMatch(text) &&
          !text.contains('/') &&
          !text.contains(':') &&
          !text.contains('_');
    }
    return true;
  }

  test('metin taraması enterpolasyon sonrası bölü işaretini rota sanmaz', () {
    expect(looksLikeUserText(r'$earned / $total rozet kazanıldı'), isTrue);
  });
  test('metin taraması kısa Ay başlığını da yakalar', () {
    expect(looksLikeUserText('Ay'), isTrue);
  });
  test('metin taraması gerçek rota URL ve teknik anahtarı dışarıda tutar', () {
    for (final value in [
      r'/students/$id',
      'https://example.com/profile',
      'studentId',
    ]) {
      expect(looksLikeUserText(value), isFalse, reason: value);
    }
  });
  group('Taşınmış feature\'larda hardcoded metin (Task 6b)', () {
    for (final feature in <String>[..._migratedFeatures, ..._migratedFiles]) {
      test('$feature: kullanıcı metni yalnız ARB\'den geliyor', () {
        final target = feature.endsWith('.dart')
            ? File('$projectRoot/lib/$feature')
            : Directory('$projectRoot/lib/features/$feature');
        expect(
          target.existsSync(),
          isTrue,
          reason: '${target.path} bulunamadı.',
        );

        final violations = <String>[];
        // Ham dize (`r'...'`) hariç, tek satırlık dize sabitleri.
        final literal = RegExp(r"""(?<!r)(?:'([^'\n\\]*)'|"([^"\n\\]*)")""");

        final files = target is File
            ? <File>[target]
            : (target as Directory)
                  .listSync(recursive: true)
                  .whereType<File>()
                  .where((file) => file.path.endsWith('.dart'));
        for (final file in files) {
          final lines = file.readAsLinesSync();
          var ignoring = false;

          for (var index = 0; index < lines.length; index++) {
            final line = lines[index];
            final trimmed = line.trimLeft();

            if (trimmed.startsWith('// l10n-ignore-start')) {
              ignoring = true;
              continue;
            }
            if (trimmed.startsWith('// l10n-ignore-end')) {
              ignoring = false;
              continue;
            }
            if (ignoring) continue;
            if (trimmed.startsWith('//') || trimmed.startsWith('*')) continue;
            if (trimmed.startsWith('import ') ||
                trimmed.startsWith('export ')) {
              continue;
            }
            // `DateFormat('d MMMM y')` gibi biçim desenleri metin değil, kalıptır.
            if (line.contains('DateFormat(')) continue;

            // Ham desenin kapanış tırnağı sonraki dizenin açılışı sanılmasın.
            final scanLine = line.replaceAllMapped(
              RegExp(r"""r(?:'[^'\n]*'|"[^"\n]*")"""),
              (match) => ' ' * match.group(0)!.length,
            );
            for (final match in literal.allMatches(scanLine)) {
              // Switch'in solundaki sabitler sözleşme/etiket eşleme anahtarıdır;
              // sağdaki görüntü metni yine taranır.
              if (trimmed.startsWith("'") &&
                  line.contains(' => ') &&
                  match.end <= line.indexOf(' => ')) {
                continue;
              }
              final raw = match.group(1) ?? match.group(2)!;
              if (_contractLiterals.containsKey(raw)) continue;
              if (!looksLikeUserText(raw)) continue;

              final relative = file.path.replaceFirst('$projectRoot/', '');
              violations.add('$relative:${index + 1}  "$raw"');
            }
          }
        }

        expect(
          violations,
          isEmpty,
          reason:
              '${violations.length} hardcoded metin — ARB\'ye taşı ya da gerekçesiyle '
              'sözleşme listesine ekle:\n${violations.join('\n')}',
        );
      });
    }
  });
}
