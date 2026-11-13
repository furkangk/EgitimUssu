import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';

class AppTheme {
  static ThemeData get lightTheme {
    const navy = AppColors.primary;
    const blue = AppColors.secondary;
    const emerald = AppColors.accentGreen;
    const background = AppColors.background;
    const outlineVariant = AppColors.border;

    final colorScheme =
        ColorScheme.fromSeed(
          seedColor: navy,
          brightness: Brightness.light,
        ).copyWith(
          primary: navy,
          secondary: blue,
          tertiary: emerald,
          surface: Colors.white,
          onSurface: AppColors.textPrimary,
          outlineVariant: outlineVariant,
        );

    return ThemeData(
      colorScheme: colorScheme,
      scaffoldBackgroundColor: background,
      appBarTheme: AppBarTheme(
        backgroundColor: background,
        foregroundColor: colorScheme.onSurface,
        elevation: 0,
        centerTitle: false,
      ),
      textTheme: const TextTheme(
        displayMedium: TextStyle(
          fontFamily: 'Plus Jakarta Sans',
          fontSize: 30,
          fontWeight: FontWeight.w700,
          height: 1.2,
          letterSpacing: 0,
        ),
        headlineSmall: TextStyle(
          fontFamily: 'Plus Jakarta Sans',
          fontSize: 24,
          fontWeight: FontWeight.w600,
          height: 1.3,
          letterSpacing: 0,
        ),
        titleLarge: TextStyle(
          fontFamily: 'Plus Jakarta Sans',
          fontSize: 20,
          fontWeight: FontWeight.w600,
          height: 1.4,
          letterSpacing: 0,
        ),
        titleMedium: TextStyle(
          fontFamily: 'Plus Jakarta Sans',
          fontSize: 16,
          fontWeight: FontWeight.w600,
          height: 1.35,
          letterSpacing: 0,
        ),
        bodyLarge: TextStyle(
          fontFamily: 'Inter',
          fontSize: 18,
          fontWeight: FontWeight.w400,
          height: 1.6,
          letterSpacing: 0,
        ),
        bodyMedium: TextStyle(
          fontFamily: 'Inter',
          fontSize: 16,
          fontWeight: FontWeight.w400,
          height: 1.5,
          letterSpacing: 0,
        ),
        bodySmall: TextStyle(
          fontFamily: 'Inter',
          fontSize: 14,
          fontWeight: FontWeight.w400,
          height: 1.5,
          letterSpacing: 0,
        ),
        labelMedium: TextStyle(
          fontFamily: 'Inter',
          fontSize: 12,
          fontWeight: FontWeight.w600,
          height: 1,
          letterSpacing: 0,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 16,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.primary),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: BorderSide(color: colorScheme.error),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: BorderSide(color: colorScheme.error),
        ),
      ),
      cardTheme: CardThemeData(
        color: Colors.white,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: colorScheme.outlineVariant),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: navy,
          foregroundColor: Colors.white,
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      useMaterial3: true,
    );
  }
}
