// PRD §9 (Free vs. Premium) tablosunun mobil karşılığı.
//
// ⚠️ **Kod gerçeği esastır** (kök `CLAUDE.md`). Bu liste PRD §9'un tamamını değil, sunucunun
// **bugün gerçekten uyguladığı** farkları taşır. Gerekçe: uygulanmayan bir kısıtlamayı paywall'da
// "Premium'a özel" diye satmak, kullanıcıya zaten bedava kullandığı şeyi satmaktır.
//
// Bugün uygulanan kapılar ve yerleri:
// - `Entitlement.MaxStudents`  → `Students/Application/StudentProfileFeatures.cs` (409)
// - `Entitlement.PdfReports`   → `Reporting/Application/StudentReportFeatures.cs` (402)
// - `PremiumFeature.TargetTracking` → `Study/Application/StudyProgressFeatures.cs` (402)
// - `MembershipGate.HistoryWindowDays` → Study geçmiş/test/net sorguları (Free = son 30 gün)
// - `Entitlement.RichNotifications` → `Parents/Infrastructure/ParentNotificationDirectory.cs`
//
// PRD §9'un kalan satırları [premiumRoadmap]'te "yakında" olarak durur; kapı geldiğinde
// (reklamlar P09 Task 5, gelir/boş zaman analizi ve profil öne çıkarma P11-P12) buraya taşınır.

/// Paywall karşılaştırma tablosunun bir satırı.
class PremiumFeatureRow {
  const PremiumFeatureRow({
    required this.title,
    required this.free,
    required this.premium,
  });

  final String title;

  /// Free kullanıcının bugün sahip olduğu değer ("Son 30 gün", "5 öğrenci", "—").
  final String free;

  /// Premium kullanıcının değeri.
  final String premium;
}

/// Rolün bugün gerçekten farklılaşan hakları.
List<PremiumFeatureRow> premiumFeaturesFor(String role, {int? freeMaxStudents}) {
  switch (role) {
    case 'Student':
      return const <PremiumFeatureRow>[
        PremiumFeatureRow(
          title: 'Çalışma ve deneme geçmişi',
          free: 'Son 30 gün',
          premium: 'Sınırsız',
        ),
        PremiumFeatureRow(
          title: 'Hedef net / puan takibi',
          free: '—',
          premium: 'Açık',
        ),
      ];
    case 'Parent':
      return const <PremiumFeatureRow>[
        PremiumFeatureRow(
          title: 'Zengin bildirimler',
          free: 'Temel',
          premium: 'Haftalık özet + ödev/ödeme uyarıları',
        ),
      ];
    default:
      return <PremiumFeatureRow>[
        PremiumFeatureRow(
          title: 'Bağlanabilecek öğrenci',
          free: '${freeMaxStudents ?? 5} öğrenci',
          premium: 'Sınırsız',
        ),
        const PremiumFeatureRow(
          title: 'PDF öğrenci raporu',
          free: '—',
          premium: 'Açık',
        ),
      ];
  }
}

/// PRD §9'un henüz kapısı yazılmamış vaatleri — paywall'da açıkça "yakında" olarak gösterilir.
List<String> premiumRoadmapFor(String role) {
  switch (role) {
    case 'Student':
      return const <String>[
        'Reklamsız kullanım',
        'Motivasyon ve rozet sistemi',
        'Detaylı haftalık/aylık analiz',
      ];
    case 'Parent':
      return const <String>[
        'Reklamsız kullanım',
        'Detaylı gelişim grafikleri',
        'Haftalık veli raporu',
      ];
    default:
      return const <String>[
        'Reklamsız kullanım',
        'Gelir analizi ve geciken ödeme listesi',
        'Boş zaman analizi',
        'İlanda profil öne çıkarma',
      ];
  }
}

/// Rolün insan okunur adı (paywall başlığı için).
String membershipRoleLabel(String role) => switch (role) {
  'Student' => 'Öğrenci',
  'Parent' => 'Veli',
  _ => 'Öğretmen',
};
