import 'package:flutter/material.dart';

/// Uygulama renk token'lari — tasarim sisteminin **tek dogruluk kaynagi**
/// olan `doc/architecture/design_system.md` §3 paletini koda baglar.
///
/// Kural: ekranlarda renk **dogrudan** `Color(0x...)` ile yazilmaz; buradaki
/// token'lardan cagrilir. Boylece bir token degisince tum uygulama degisir.
abstract final class AppColors {
  // Birincil
  static const Color primary = Color(0xFF082B4F);
  static const Color primaryDark = Color(0xFF061F3A);
  static const Color primaryLight = Color(0xFFEAF2FB);

  // Vurgu
  static const Color secondary = Color(0xFF3D8BFF);
  static const Color accentBlue = Color(0xFF3D8BFF);
  static const Color accentGreen = Color(0xFF20B486);
  static const Color accentOrange = Color(0xFFFFA726);
  static const Color accentRed = Color(0xFFFF5A5F);
  static const Color accentTeal = Color(0xFF20A4A9);

  // Yuzey
  static const Color background = Color(0xFFF7F9FC);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color card = Color(0xFFFFFFFF);

  // Metin
  static const Color textPrimary = Color(0xFF111827);
  static const Color textSecondary = Color(0xFF6B7280);
  static const Color textMuted = Color(0xFF9CA3AF);

  // Cizgi
  static const Color border = Color(0xFFE5E7EB);
  static const Color divider = Color(0xFFF0F2F5);

  // Ogretmen-paneli ek aksanlari — design_system.md §3 paletiyle tam ortusmeyen,
  // koddaki ekranlarda yaygin kullanilan ek renkler. (Kanonik token'i olmayanlar.)
  static const Color amber = Color(0xFFFFB84D);
  static const Color skyBorder = Color(0xFFD7E7F8);
  static const Color tabBackground = Color(0xFFF3F5F8);
  static const Color purple = Color(0xFF8B5CF6);

  // Semantik durum renkleri — hata/aciliyet (urgency) kartlari ve uyari alanlari.
  // Her durum: accent (metin/ikon) + surface (en acik zemin) + surfaceStrong
  // (avatar/chip zemini) + border. Aciliyet kademeleri: error > warning > info > success.
  static const Color error = Color(0xFFD32F2F);
  static const Color errorSurface = Color(0xFFFFF5F5);
  static const Color errorSurfaceStrong = Color(0xFFFFEBEE);
  static const Color errorBorder = Color(0xFFFFCDD2);

  static const Color warning = Color(0xFFE65100);
  static const Color warningSurface = Color(0xFFFFF9F0);
  static const Color warningSurfaceStrong = Color(0xFFFFF3E0);
  static const Color warningBorder = Color(0xFFFFE0B2);

  static const Color infoSurface = Color(0xFFE8F1FF);
  static const Color successSurface = Color(0xFFE8F8F4);
}
