using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>Premium'a özel (derinlik) öğrenci özellikleri (Ö-D §14.3).</summary>
public enum PremiumFeature
{
    /// <summary>Aylık çalışma/deneme analizi.</summary>
    MonthlyAnalysis,

    /// <summary>Hedef net/puan takibi.</summary>
    TargetTracking,

    /// <summary>Konu bazlı zayıflık analizi.</summary>
    TopicWeakness,

    /// <summary>Streak (seri) dondurma hakkı.</summary>
    StreakFreeze,

    /// <summary>PDF rapor çıktısı.</summary>
    PdfReport
}

/// <summary>
/// Öğrenci Free/Premium kapı mantığı — saf, birim testli. Free = çekirdek alışkanlık
/// (kronometre, test, streak tam, son 30 gün geçmiş, temel haftalık analiz);
/// Premium = derinlik (sınırsız geçmiş + tüm <see cref="PremiumFeature"/> özellikleri).
/// </summary>
public static class MembershipGate
{
    /// <summary>Free için son 30 günlük geçmiş penceresi; Premium için sınırsız (null).</summary>
    public const int FreeHistoryWindowDays = 30;

    /// <summary>
    /// Geçmiş sorgularının alt sınırını belirleyen pencere (gün). Free → 30, Premium → null (sınırsız).
    /// </summary>
    public static int? HistoryWindowDays(MembershipTier tier) => tier switch
    {
        MembershipTier.Premium => null,
        _ => FreeHistoryWindowDays
    };

    /// <summary>Verilen premium özelliğe erişim: Free hepsi false; Premium hepsi true.</summary>
    public static bool Allows(MembershipTier tier, PremiumFeature feature) => tier == MembershipTier.Premium;
}
