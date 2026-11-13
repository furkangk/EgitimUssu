/// D-02: E-postadaki `egitimussu://verify-email?token=…` biçimindeki şema-bazlı
/// derin bağlantılarda **yol boştur**; hedef `host` alanında taşınır
/// (`Uri.parse('egitimussu://verify-email').path == ''`).
///
/// go_router yalnız `uri.path`'e bakarak eşleşme yapar, bu yüzden bağlantı
/// olduğu gibi verilirse hiçbir rota tutmaz ve uygulama sessizce köke düşer.
/// Bu fonksiyon host'u yola taşıyıp uygulama-içi bir rotaya çevirir.
///
/// Uygulama-içi rotalar (zaten yol taşıyanlar) ve bilinmeyen host'lar için
/// `null` döner — çağıran taraf o zaman yönlendirme yapmaz.
String? normalizeDeepLink(Uri uri) {
  if (uri.path.isNotEmpty && uri.path != '/') return null;

  final query = uri.query.isEmpty ? '' : '?${uri.query}';

  switch (uri.host) {
    case 'verify-email':
      return '/verify-email$query';
    case 'password-reset':
      // Token varsa doğrudan "yeni şifre" adımına, yoksa kod isteme adımına.
      return uri.queryParameters.containsKey('token')
          ? '/password-reset/confirm$query'
          : '/password-reset$query';
    default:
      return null;
  }
}
