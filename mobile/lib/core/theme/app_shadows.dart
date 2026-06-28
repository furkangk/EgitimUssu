import 'package:flutter/material.dart';

/// Uygulama genelinde tek, tutarli yumusak golge token'i.
///
/// `doc/architecture/design_system.md` §6'daki "yumusak golge" kuralinin koddaki
/// karsiligi. Daha once her ekran kendi `BoxShadow`'unu (farkli alpha/blur/offset)
/// elle yaziyordu; artik tum kartlar `AppShadows.soft` kullanir. `const` oldugu
/// icin hem `const` hem normal `BoxDecoration` icinde gecerlidir.
abstract final class AppShadows {
  /// Kart/sheet/panel icin standart yumusak golge (primary tonlu, dusuk opaklik).
  static const List<BoxShadow> soft = <BoxShadow>[
    BoxShadow(color: Color(0x12082B4F), blurRadius: 24, offset: Offset(0, 12)),
  ];
}
