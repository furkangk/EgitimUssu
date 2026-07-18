import 'dart:math' as math;

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_timer_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_timer_state.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudyTimerPage extends StatelessWidget {
  const StudyTimerPage({
    super.key,
    required this.studentId,
    this.initialSubject,
    this.initialTopic,
  });

  final String studentId;

  /// "Devam Et" gibi akışlardan gelen ön-doldurulmuş ders/konu.
  final String? initialSubject;
  final String? initialTopic;

  @override
  Widget build(BuildContext context) {
    return BlocProvider<StudyTimerCubit>(
      create: (_) => StudyTimerCubit.create()..restore(studentId),
      child: _StudyTimerView(
        studentId: studentId,
        initialSubject: initialSubject,
        initialTopic: initialTopic,
      ),
    );
  }
}

class _StudyTimerView extends StatefulWidget {
  const _StudyTimerView({
    required this.studentId,
    this.initialSubject,
    this.initialTopic,
  });

  final String studentId;
  final String? initialSubject;
  final String? initialTopic;

  @override
  State<_StudyTimerView> createState() => _StudyTimerViewState();
}

class _StudyTimerViewState extends State<_StudyTimerView> {
  late final _subjectController =
      TextEditingController(text: widget.initialSubject ?? '');
  late final _topicController =
      TextEditingController(text: widget.initialTopic ?? '');

  /// Öğrencinin ders programındaki (düzene kayıtlı) benzersiz dersler —
  /// başlangıç formunda hızlı seçim çipleri olarak sunulur.
  List<String> _scheduledSubjects = const <String>[];

  @override
  void initState() {
    super.initState();
    _loadScheduledSubjects();
  }

  Future<void> _loadScheduledSubjects() async {
    try {
      // Öğrencinin takvimindeki dersler: hem kendi programı hem öğretmen dersleri
      // (yaklaşan pencere) — hızlı seçim çipleri olarak sunulur.
      final now = DateTime.now().toUtc();
      final occurrences = await injector<SchedulingRepository>().getStudentCalendar(
        studentId: widget.studentId,
        startAtUtc: now.subtract(const Duration(days: 1)),
        endAtUtc: now.add(const Duration(days: 14)),
      );
      final sorted = <CalendarOccurrence>[...occurrences]
        ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
      final seen = <String>{};
      final subjects = <String>[];
      for (final o in sorted) {
        final s = o.subject.trim();
        if (s.isEmpty || !seen.add(s.toLowerCase())) continue;
        subjects.add(s);
      }
      if (!mounted) return;
      setState(() => _scheduledSubjects = subjects);
    } catch (_) {
      // Program alınamazsa sessizce geç; elle giriş her zaman mümkün.
    }
  }

  @override
  void dispose() {
    _subjectController.dispose();
    _topicController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Çalışma Kronometresi')),
      body: BlocConsumer<StudyTimerCubit, StudyTimerState>(
        listener: (context, state) {
          if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(state.errorMessage!), backgroundColor: AppColors.error),
            );
          }
        },
        builder: (context, state) {
          final cubit = context.read<StudyTimerCubit>();
          // Seans tamamlandığında özet ekranı gösterilir; kullanıcı kendi
          // isteğiyle "Kapat" diyene kadar açık kalır (otomatik kapanmaz).
          if (state.completedSummary != null) {
            return _CompletionSummary(
              summary: state.completedSummary!,
              netSeconds: state.elapsedSeconds,
              breakSeconds: state.breakSeconds,
              breakCount: state.breakCount,
              onClose: cubit.acknowledgeSummary,
            );
          }
          if (!state.hasActive) {
            return _StartForm(
              subjectController: _subjectController,
              topicController: _topicController,
              scheduledSubjects: _scheduledSubjects,
              isBusy: state.isBusy,
              onStart: () {
                // Ders seçmeden başlatılırsa serbest çalışma olarak kaydedilir.
                final raw = _subjectController.text.trim();
                final subject = raw.isEmpty ? 'Serbest çalışma' : raw;
                cubit.start(
                  widget.studentId,
                  subject,
                  _topicController.text.trim().isEmpty ? null : _topicController.text.trim(),
                );
              },
            );
          }
          return _ActiveTimer(state: state, cubit: cubit);
        },
      ),
    );
  }
}

class _StartForm extends StatelessWidget {
  const _StartForm({
    required this.subjectController,
    required this.topicController,
    required this.scheduledSubjects,
    required this.isBusy,
    required this.onStart,
  });

  final TextEditingController subjectController;
  final TextEditingController topicController;
  final List<String> scheduledSubjects;
  final bool isBusy;
  final VoidCallback onStart;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 24),
      children: [
        const _StartHero(),
        const SizedBox(height: 24),
        if (scheduledSubjects.isNotEmpty) ...[
          const _SectionLabel(
            icon: Icons.calendar_today_outlined,
            text: 'Programından seç',
          ),
          const SizedBox(height: 12),
          AnimatedBuilder(
            animation: subjectController,
            builder: (context, _) {
              final selected = subjectController.text.trim().toLowerCase();
              return Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final subject in scheduledSubjects)
                    _SubjectChoiceChip(
                      label: subject,
                      selected: subject.toLowerCase() == selected,
                      onTap: () {
                        subjectController.text = subject;
                        subjectController.selection =
                            TextSelection.collapsed(offset: subject.length);
                      },
                    ),
                ],
              );
            },
          ),
          const SizedBox(height: 20),
          const _OrDivider(),
          const SizedBox(height: 20),
        ],
        const _SectionLabel(
          icon: Icons.edit_outlined,
          text: 'Elle gir',
        ),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.soft,
          ),
          child: Column(
            children: [
              AppTextField(
                controller: subjectController,
                labelText: 'Ders *',
                hintText: 'Örn. Matematik',
                textCapitalization: TextCapitalization.sentences,
              ),
              const SizedBox(height: 14),
              AppTextField(
                controller: topicController,
                labelText: 'Konu (opsiyonel)',
                hintText: 'Örn. Türev',
                textCapitalization: TextCapitalization.sentences,
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        AppPrimaryButton(
          label: 'Başlat',
          isLoading: isBusy,
          onPressed: onStart,
        ),
      ],
    );
  }
}

/// Başlangıç ekranının üst gradient kartı: kronometre kimliğini taşıyan
/// premium başlık (navy→teal, arka planda soluk saat filigranı).
class _StartHero extends StatelessWidget {
  const _StartHero();

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(20),
      child: DecoratedBox(
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [AppColors.primary, AppColors.accentTeal],
          ),
          boxShadow: AppShadows.soft,
        ),
        child: Stack(
          children: [
            Positioned(
              right: -22,
              top: -18,
              child: Icon(
                Icons.schedule,
                size: 140,
                color: Colors.white.withValues(alpha: 0.08),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  Container(
                    width: 56,
                    height: 56,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Icon(Icons.timer_outlined, size: 30, color: Colors.white),
                  ),
                  const SizedBox(width: 16),
                  const Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Ne çalışacaksın?',
                          style: TextStyle(
                              fontSize: 19,
                              fontWeight: FontWeight.w800,
                              color: Colors.white),
                        ),
                        SizedBox(height: 4),
                        Text(
                          'Dersini seç, kronometreyi başlat.',
                          style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                              color: Colors.white70),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// İkonlu bölüm başlığı (küçük, muted).
class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 15, color: AppColors.textSecondary),
        const SizedBox(width: 6),
        Text(
          text,
          style: const TextStyle(
              fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.textSecondary),
        ),
      ],
    );
  }
}

/// "veya" ayracı — hızlı seçim ile elle giriş arasını böler.
class _OrDivider extends StatelessWidget {
  const _OrDivider();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const Expanded(child: Divider(color: AppColors.border, height: 1)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Text(
            'veya',
            style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: AppColors.textMuted),
          ),
        ),
        const Expanded(child: Divider(color: AppColors.border, height: 1)),
      ],
    );
  }
}

/// Programdaki bir dersi tek dokunuşla "Ders" alanına yazan seçim çipi.
class _SubjectChoiceChip extends StatelessWidget {
  const _SubjectChoiceChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final Color fg = selected ? Colors.white : AppColors.textPrimary;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(999),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
          decoration: BoxDecoration(
            color: selected ? AppColors.accentTeal : AppColors.surface,
            borderRadius: BorderRadius.circular(999),
            border: Border.all(
              color: selected ? AppColors.accentTeal : AppColors.border,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                selected ? Icons.check_rounded : Icons.menu_book_outlined,
                size: 15,
                color: fg,
              ),
              const SizedBox(width: 6),
              Text(
                label,
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: fg),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Seans "Bitir" ile tamamlanınca gösterilen çalışma özeti. Otomatik
/// kapanmaz; kullanıcı "Kapat" diyene kadar açık kalır.
class _CompletionSummary extends StatefulWidget {
  const _CompletionSummary({
    required this.summary,
    required this.netSeconds,
    required this.breakSeconds,
    required this.breakCount,
    required this.onClose,
  });

  final StudySession summary;

  /// Kronometrede canlı izlenen net çalışma / mola süreleri (saniye hassas).
  /// Sunucunun dakikaya yuvarlanmış değerine değil, kullanıcının gördüğü
  /// bu değerlere güvenilir.
  final int netSeconds;
  final int breakSeconds;
  final int breakCount;
  final VoidCallback onClose;

  @override
  State<_CompletionSummary> createState() => _CompletionSummaryState();
}

class _CompletionSummaryState extends State<_CompletionSummary>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 650),
  )..forward();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = widget.summary;
    final title = s.topic == null ? s.subject : '${s.subject} · ${s.topic}';
    // Dakikaya yuvarlama: domain (backend) ile aynı (yakın yarıyı yukarı).
    final netMinutes = (widget.netSeconds / 60).round();
    final breakMinutes = (widget.breakSeconds / 60).round();
    final totalMinutes = ((widget.netSeconds + widget.breakSeconds) / 60).round();
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 32, 16, 24),
      children: [
        Center(
          child: ScaleTransition(
            scale: CurvedAnimation(parent: _controller, curve: Curves.elasticOut),
            child: Container(
              width: 96,
              height: 96,
              decoration: const BoxDecoration(
                color: AppColors.successSurface,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.check_rounded, size: 52, color: AppColors.accentGreen),
            ),
          ),
        ),
        const SizedBox(height: 20),
        const Text(
          'Seansı tamamladın! 🎉',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: AppColors.textPrimary),
        ),
        const SizedBox(height: 6),
        const Text(
          'Harika iş — emeğin kaydedildi.',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: AppColors.textSecondary),
        ),
        const SizedBox(height: 24),
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.soft,
          ),
          child: Column(
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.menu_book_outlined, size: 18, color: AppColors.accentTeal),
                  const SizedBox(width: 8),
                  Flexible(
                    child: Text(
                      title,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              const Text(
                'Net çalışma',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.textSecondary),
              ),
              const SizedBox(height: 2),
              Text(
                StudyFormat.minutes(netMinutes),
                style: const TextStyle(
                  fontSize: 34,
                  fontWeight: FontWeight.w800,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(height: 18),
              const Divider(color: AppColors.divider, height: 1),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _StatCard(
                      icon: Icons.coffee_outlined,
                      color: AppColors.accentOrange,
                      label: widget.breakCount == 1 ? '1 mola' : '${widget.breakCount} mola',
                      value: StudyFormat.minutes(breakMinutes),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _StatCard(
                      icon: Icons.timelapse_outlined,
                      color: AppColors.accentTeal,
                      label: 'Toplam süre',
                      value: StudyFormat.minutes(totalMinutes),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        AppPrimaryButton(
          label: 'Kapat',
          onPressed: widget.onClose,
        ),
      ],
    );
  }
}

class _ActiveTimer extends StatelessWidget {
  const _ActiveTimer({required this.state, required this.cubit});

  final StudyTimerState state;
  final StudyTimerCubit cubit;

  @override
  Widget build(BuildContext context) {
    final session = state.session!;
    final running = state.isRunning;
    return SafeArea(
      child: LayoutBuilder(
        builder: (context, constraints) {
          return SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
            child: ConstrainedBox(
              // Uzun ekranda içeriği dikeyde yay (kadran ortada); kısa ekranda
              // taşma yerine kaydır. -28 = dikey padding payı.
              constraints: BoxConstraints(minHeight: constraints.maxHeight - 28),
              child: IntrinsicHeight(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    _SubjectPill(subject: session.subject, topic: session.topic),
                    Center(
                      child: _TimerDial(
                        elapsedSeconds: state.elapsedSeconds,
                        running: running,
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _StatsRow(
                          netSeconds: state.elapsedSeconds,
                          breakSeconds: state.breakSeconds,
                          breakCount: state.breakCount,
                        ),
                        const SizedBox(height: 20),
                        _Controls(state: state, cubit: cubit, running: running),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

/// Aktif seansın ders/konu bilgisini gösteren üst rozet.
class _SubjectPill extends StatelessWidget {
  const _SubjectPill({required this.subject, this.topic});

  final String subject;
  final String? topic;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: AppColors.border),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.menu_book_outlined, size: 18, color: AppColors.accentTeal),
            const SizedBox(width: 8),
            Flexible(
              child: Text(
                topic == null ? subject : '$subject · $topic',
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Büyük dairesel kronometre kadranı: dıştaki halka geçen saniyeyi (0-60)
/// gösterir, çalışırken yumuşak bir nabız efekti verir.
class _TimerDial extends StatefulWidget {
  const _TimerDial({required this.elapsedSeconds, required this.running});

  final int elapsedSeconds;
  final bool running;

  @override
  State<_TimerDial> createState() => _TimerDialState();
}

class _TimerDialState extends State<_TimerDial> with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1800),
  );

  @override
  void initState() {
    super.initState();
    if (widget.running) _pulse.repeat();
  }

  @override
  void didUpdateWidget(covariant _TimerDial old) {
    super.didUpdateWidget(old);
    if (widget.running && !_pulse.isAnimating) {
      _pulse.repeat();
    } else if (!widget.running && _pulse.isAnimating) {
      _pulse.stop();
      _pulse.value = 0;
    }
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final running = widget.running;
    final accent = running ? AppColors.accentTeal : AppColors.textMuted;
    final progress = (widget.elapsedSeconds % 60) / 60;

    return RepaintBoundary(
      child: SizedBox(
        width: 264,
        height: 264,
        child: Stack(
          alignment: Alignment.center,
          children: [
            // Çalışırken dışarı doğru genişleyen nabız halkası.
            if (running)
              AnimatedBuilder(
                animation: _pulse,
                builder: (context, _) {
                  final t = Curves.easeOut.transform(_pulse.value);
                  return Container(
                    width: 220 + 44 * t,
                    height: 220 + 44 * t,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: AppColors.accentTeal.withValues(alpha: 0.18 * (1 - t)),
                    ),
                  );
                },
              ),
            // Saniye ilerleme halkası.
            TweenAnimationBuilder<double>(
              tween: Tween<double>(end: progress),
              duration: const Duration(milliseconds: 400),
              curve: Curves.easeOut,
              builder: (context, value, _) {
                // Dakika başında geri sarmayı gizle: 0'a atlarken anında sıfırla.
                final shown = value > progress + 0.5 ? progress : value;
                return CustomPaint(
                  size: const Size(264, 264),
                  painter: _RingPainter(progress: shown, accent: accent),
                );
              },
            ),
            // Merkez yüzeyi + saat.
            Container(
              width: 208,
              height: 208,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: AppColors.surface,
                boxShadow: AppShadows.soft,
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    StudyFormat.stopwatch(widget.elapsedSeconds),
                    style: TextStyle(
                      fontSize: widget.elapsedSeconds >= 3600 ? 40 : 46,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 1,
                      color: AppColors.textPrimary,
                      fontFeatures: const [FontFeature.tabularFigures()],
                    ),
                  ),
                  const SizedBox(height: 10),
                  _StatusBadge(running: running),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.running});

  final bool running;

  @override
  Widget build(BuildContext context) {
    final color = running ? AppColors.accentTeal : AppColors.accentOrange;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(
            running ? 'Çalışıyor' : 'Molada',
            style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: color),
          ),
        ],
      ),
    );
  }
}

/// Kadranın altındaki iki canlı metrik: net çalışma süresi ve mola
/// (sayısı + toplam süresi). Değerler seans boyunca anlık güncellenir.
class _StatsRow extends StatelessWidget {
  const _StatsRow({
    required this.netSeconds,
    required this.breakSeconds,
    required this.breakCount,
  });

  final int netSeconds;
  final int breakSeconds;
  final int breakCount;

  @override
  Widget build(BuildContext context) {
    final totalMinutes = (netSeconds + breakSeconds) ~/ 60;
    return Row(
      children: [
        Expanded(
          child: _StatCard(
            icon: Icons.timelapse_outlined,
            color: AppColors.accentGreen,
            label: 'Toplam süre',
            value: StudyFormat.minutes(totalMinutes),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _StatCard(
            icon: Icons.coffee_outlined,
            color: AppColors.accentOrange,
            label: breakCount == 1 ? '1 mola' : '$breakCount mola',
            value: StudyFormat.minutes(breakSeconds ~/ 60),
          ),
        ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({
    required this.icon,
    required this.color,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Icon(icon, size: 20, color: color),
          const SizedBox(width: 10),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                label,
                style: const TextStyle(
                    fontSize: 11, fontWeight: FontWeight.w500, color: AppColors.textSecondary),
              ),
              const SizedBox(height: 2),
              Text(
                value,
                style: const TextStyle(
                    fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Controls extends StatelessWidget {
  const _Controls({required this.state, required this.cubit, required this.running});

  final StudyTimerState state;
  final StudyTimerCubit cubit;
  final bool running;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: FilledButton.tonalIcon(
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(52),
                  backgroundColor: AppColors.primaryLight,
                  foregroundColor: AppColors.primary,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                onPressed:
                    state.isBusy ? null : () => running ? cubit.pause() : cubit.resume(),
                icon: Icon(running ? Icons.pause_rounded : Icons.play_arrow_rounded),
                label: Text(running ? 'Mola Ver' : 'Devam Et'),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: FilledButton.icon(
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(52),
                  backgroundColor: AppColors.accentGreen,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                onPressed: state.isBusy ? null : cubit.complete,
                icon: const Icon(Icons.flag_rounded),
                label: const Text('Bitir'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        TextButton.icon(
          onPressed: state.isBusy ? null : cubit.discard,
          icon: const Icon(Icons.delete_outline, size: 18, color: AppColors.error),
          label: const Text('Seansı iptal et', style: TextStyle(color: AppColors.error)),
        ),
      ],
    );
  }
}

/// Kadranın çevresindeki iz + geçen saniye yayını çizen boyacı.
class _RingPainter extends CustomPainter {
  _RingPainter({required this.progress, required this.accent});

  final double progress;
  final Color accent;

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 12.0;
    final center = size.center(Offset.zero);
    final radius = (size.shortestSide - stroke) / 2;

    final track = Paint()
      ..color = AppColors.divider
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke;
    canvas.drawCircle(center, radius, track);

    final arc = Paint()
      ..color = accent
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(
      Rect.fromCircle(center: center, radius: radius),
      -math.pi / 2,
      2 * math.pi * progress.clamp(0.0, 1.0),
      false,
      arc,
    );
  }

  @override
  bool shouldRepaint(covariant _RingPainter old) =>
      old.progress != progress || old.accent != accent;
}
