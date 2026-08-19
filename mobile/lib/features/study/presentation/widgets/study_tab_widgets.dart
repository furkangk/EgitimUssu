import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:flutter/material.dart';

/// Öğrenci çalışma sekmelerinde ortak kullanılan kart/başlık/tile bileşenleri.
/// Tek yerden tanımlı olduğu için tüm sekmelerde tutarlı görünürler.

/// Standart yüzey kartı (surface + skyBorder + AppShadows.soft).
class StudyCard extends StatelessWidget {
  const StudyCard({super.key, required this.child, this.padding});

  final Widget child;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding ?? const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: child,
    );
  }
}

/// Bölüm başlığı (+ opsiyonel sağ aksiyon, ör. "Tümü").
class StudySectionHeader extends StatelessWidget {
  const StudySectionHeader({super.key, required this.title, this.action});

  final String title;
  final StudySectionAction? action;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Text(
          title,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
        ),
        if (action != null)
          InkWell(
            borderRadius: BorderRadius.circular(8),
            onTap: action!.onTap,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
              child: Text(
                action!.label,
                style: const TextStyle(
                    color: AppColors.primary, fontWeight: FontWeight.w700),
              ),
            ),
          ),
      ],
    );
  }
}

/// [StudySectionHeader] sağ aksiyonu.
class StudySectionAction {
  const StudySectionAction({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;
}

/// İkon + değer + etiketten oluşan küçük istatistik kutusu.
class StudyStatTile extends StatelessWidget {
  const StudyStatTile({
    super.key,
    required this.icon,
    required this.color,
    required this.value,
    required this.label,
  });

  final IconData icon;
  final Color color;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 10),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: color, size: 22),
          ),
          const SizedBox(height: 8),
          Text(value,
              textAlign: TextAlign.center,
              style: const TextStyle(
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                  fontSize: 15)),
          const SizedBox(height: 2),
          Text(label,
              textAlign: TextAlign.center,
              style:
                  const TextStyle(color: AppColors.textSecondary, fontSize: 11)),
        ],
      ),
    );
  }
}

/// Backend verisi henüz olmayan bölümler için dürüst "yakında" kartı.
class StudyComingSoonCard extends StatelessWidget {
  const StudyComingSoonCard({
    super.key,
    required this.icon,
    required this.title,
    required this.message,
  });

  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return StudyCard(
      child: Row(
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.tabBackground,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Icon(icon, color: AppColors.textMuted, size: 22),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Flexible(
                      child: Text(title,
                          style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary)),
                    ),
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: AppColors.tabBackground,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: const Text('Yakında',
                          style: TextStyle(
                              color: AppColors.textMuted,
                              fontSize: 10,
                              fontWeight: FontWeight.w700)),
                    ),
                  ],
                ),
                const SizedBox(height: 2),
                Text(message,
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Bir çalışma seansı satırı (geçmiş/son çalışmalar listeleri).
class StudySessionTile extends StatelessWidget {
  const StudySessionTile({
    super.key,
    required this.subject,
    required this.minutes,
    required this.endedAtUtc,
    this.topic,
    this.isManual = false,
  });

  final String subject;
  final String? topic;
  final int minutes;
  final DateTime endedAtUtc;
  final bool isManual;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(isManual ? Icons.edit_rounded : Icons.timer_rounded,
                color: AppColors.primary, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(topic == null ? subject : '$subject · $topic',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary)),
                Text(StudyFormat.date(endedAtUtc),
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
          Text(StudyFormat.minutes(minutes),
              style: const TextStyle(
                  fontWeight: FontWeight.w700, color: AppColors.primary)),
        ],
      ),
    );
  }
}

/// Backend'i olmayan veri/eylemler için dürüst "demo" rozeti.
class StudyDemoBadge extends StatelessWidget {
  const StudyDemoBadge({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.warningSurfaceStrong,
        borderRadius: BorderRadius.circular(999),
      ),
      child: const Text('Demo',
          style: TextStyle(
              color: AppColors.warning, fontSize: 10, fontWeight: FontWeight.w700)),
    );
  }
}

/// Gradient ikon madalyonu (kart başlıkları / hızlı erişim).
class StudyIconChip extends StatelessWidget {
  const StudyIconChip(
      {super.key, required this.icon, required this.color, this.size = 44});

  final IconData icon;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [color.withValues(alpha: 0.18), color.withValues(alpha: 0.08)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: size * 0.5),
    );
  }
}

/// Basılınca hafifçe küçülen dokunma sarmalayıcısı.
class StudyPressable extends StatefulWidget {
  const StudyPressable({super.key, required this.child, required this.onTap});

  final Widget child;
  final VoidCallback onTap;

  @override
  State<StudyPressable> createState() => _StudyPressableState();
}

class _StudyPressableState extends State<StudyPressable> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTapDown: (_) => setState(() => _down = true),
      onTapCancel: () => setState(() => _down = false),
      onTapUp: (_) => setState(() => _down = false),
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _down ? 0.97 : 1.0,
        duration: const Duration(milliseconds: 90),
        child: widget.child,
      ),
    );
  }
}

/// Dashboard hızlı erişim kartı (ikon + etiket).
class StudyQuickAccessCard extends StatelessWidget {
  const StudyQuickAccessCard(
      {super.key,
      required this.icon,
      required this.color,
      required this.label,
      required this.onTap});

  final IconData icon;
  final Color color;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return StudyPressable(
      onTap: onTap,
      child: StudyCard(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            StudyIconChip(icon: icon, color: color),
            const SizedBox(height: 10),
            Text(label,
                style: const TextStyle(
                    fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
          ],
        ),
      ),
    );
  }
}

/// Hedef ilerleme barı (value 0..1, 1 üzerini kırpar).
class StudyProgressBar extends StatelessWidget {
  const StudyProgressBar(
      {super.key, required this.value, this.color, this.trailingLabel});

  final double value;
  final Color? color;
  final String? trailingLabel;

  @override
  Widget build(BuildContext context) {
    final clamped = value.clamp(0.0, 1.0);
    final barColor = color ?? AppColors.accentTeal;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: Container(
            height: 8,
            color: AppColors.tabBackground,
            child: Align(
              alignment: Alignment.centerLeft,
              child: FractionallySizedBox(
                widthFactor: clamped,
                child: Container(color: barColor),
              ),
            ),
          ),
        ),
        if (trailingLabel != null) ...<Widget>[
          const SizedBox(height: 4),
          Text(trailingLabel!,
              style:
                  const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
        ],
      ],
    );
  }
}

/// Kendi (öğrenci) / öğretmen dersi ayrım rozeti.
class StudyOwnershipBadge extends StatelessWidget {
  const StudyOwnershipBadge({super.key, required this.isOwn});

  final bool isOwn;

  @override
  Widget build(BuildContext context) {
    final color = isOwn ? AppColors.accentTeal : AppColors.accentBlue;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(isOwn ? Icons.person_rounded : Icons.school_rounded,
              size: 12, color: color),
          const SizedBox(width: 4),
          Text(isOwn ? 'Kendi' : 'Öğretmen',
              style: TextStyle(
                  color: color, fontSize: 10, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }
}
