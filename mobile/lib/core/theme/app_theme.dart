import 'package:flutter/material.dart';

class AppTheme {
  static ThemeData get lightTheme {
    const navy = Color(0xFF0F172A);
    const blue = Color(0xFF3D8BFF);
    const emerald = Color(0xFF10B981);
    const background = Color(0xFFF8F9FF);
    const outlineVariant = Color(0xFFC6C6CD);

    final colorScheme =
        ColorScheme.fromSeed(
          seedColor: navy,
          brightness: Brightness.light,
        ).copyWith(
          primary: navy,
          secondary: blue,
          tertiary: emerald,
          surface: Colors.white,
          onSurface: const Color(0xFF0B1C30),
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
          borderSide: const BorderSide(color: Color(0xFFE5EAF0)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: Color(0xFFE5EAF0)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: Color(0xFF062B52)),
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
