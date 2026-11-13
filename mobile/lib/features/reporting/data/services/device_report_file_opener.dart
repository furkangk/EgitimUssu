import 'dart:io';
import 'dart:typed_data';

import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:open_filex/open_filex.dart';

/// M14-4: PDF'i geçici dizine yazıp cihazın sistem görüntüleyicisiyle açar.
///
/// `url_launcher` bir `file://` adresini Android/iOS'ta güvenilir biçimde açamaz
/// (Android `content://` bekler); `open_filex` platform başına doğru mekanizmayı
/// kullanır. Gerekçe `doc/architecture/mobile_flutter.md` §5'te kayıtlıdır.
class DeviceReportFileOpener implements ReportFileOpener {
  const DeviceReportFileOpener();

  @override
  Future<String> open({
    required String fileName,
    required Uint8List bytes,
  }) async {
    final file = File('${Directory.systemTemp.path}/$fileName');
    await file.writeAsBytes(bytes);

    final result = await OpenFilex.open(file.path);
    if (result.type != ResultType.done) {
      // Sessizce yutma: görüntüleyici yoksa kullanıcı dosyanın nerede olduğunu bilmeli.
      throw StateError('Rapor açılamadı (${result.message}). Dosya: ${file.path}');
    }
    return file.path;
  }
}
