import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_timer_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_timer_state.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudyTimerPage extends StatelessWidget {
  const StudyTimerPage({super.key, required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return BlocProvider<StudyTimerCubit>(
      create: (_) => StudyTimerCubit.create()..restore(studentId),
      child: _StudyTimerView(studentId: studentId),
    );
  }
}

class _StudyTimerView extends StatefulWidget {
  const _StudyTimerView({required this.studentId});

  final String studentId;

  @override
  State<_StudyTimerView> createState() => _StudyTimerViewState();
}

class _StudyTimerViewState extends State<_StudyTimerView> {
  final _subjectController = TextEditingController();
  final _topicController = TextEditingController();

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
          if (state.completedSummary != null) {
            final s = state.completedSummary!;
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(
                    'Seans tamamlandı: ${StudyFormat.minutes(s.effectiveMinutes)} net çalışma'),
                backgroundColor: AppColors.accentGreen,
              ),
            );
            context.read<StudyTimerCubit>().acknowledgeSummary();
          }
        },
        builder: (context, state) {
          final cubit = context.read<StudyTimerCubit>();
          if (!state.hasActive) {
            return _StartForm(
              subjectController: _subjectController,
              topicController: _topicController,
              isBusy: state.isBusy,
              onStart: () {
                final subject = _subjectController.text.trim();
                if (subject.isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Ders alanı zorunlu.')),
                  );
                  return;
                }
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
    required this.isBusy,
    required this.onStart,
  });

  final TextEditingController subjectController;
  final TextEditingController topicController;
  final bool isBusy;
  final VoidCallback onStart;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        const SizedBox(height: 12),
        const Icon(Icons.timer_outlined, size: 64, color: AppColors.primary),
        const SizedBox(height: 12),
        const Text('Ne çalışacaksın?',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
        const SizedBox(height: 24),
        TextField(
          controller: subjectController,
          decoration: const InputDecoration(
            labelText: 'Ders *',
            hintText: 'Örn. Matematik',
            border: OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: 14),
        TextField(
          controller: topicController,
          decoration: const InputDecoration(
            labelText: 'Konu (opsiyonel)',
            hintText: 'Örn. Turev',
            border: OutlineInputBorder(),
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

class _ActiveTimer extends StatelessWidget {
  const _ActiveTimer({required this.state, required this.cubit});

  final StudyTimerState state;
  final StudyTimerCubit cubit;

  @override
  Widget build(BuildContext context) {
    final session = state.session!;
    final running = state.isRunning;
    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 20),
          Text(
            session.topic == null ? session.subject : '${session.subject} · ${session.topic}',
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
          ),
          const SizedBox(height: 8),
          Container(
            margin: const EdgeInsets.symmetric(vertical: 24),
            padding: const EdgeInsets.symmetric(vertical: 40),
            decoration: BoxDecoration(
              color: running ? AppColors.primary : AppColors.primaryLight,
              borderRadius: BorderRadius.circular(24),
            ),
            child: Column(
              children: [
                Text(
                  StudyFormat.stopwatch(state.elapsedSeconds),
                  style: TextStyle(
                    fontSize: 52,
                    fontWeight: FontWeight.w800,
                    color: running ? Colors.white : AppColors.primary,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  running ? 'Çalışıyor' : 'Molada',
                  style: TextStyle(color: running ? Colors.white70 : AppColors.primary),
                ),
                if (session.breakMinutes > 0)
                  Text('Mola: ${StudyFormat.minutes(session.breakMinutes)}',
                      style: TextStyle(
                          color: running ? Colors.white70 : AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: state.isBusy
                      ? null
                      : () => running ? cubit.pause() : cubit.resume(),
                  icon: Icon(running ? Icons.pause : Icons.play_arrow),
                  label: Text(running ? 'Mola' : 'Devam'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(backgroundColor: AppColors.accentGreen),
                  onPressed: state.isBusy ? null : () => cubit.complete(),
                  icon: const Icon(Icons.stop),
                  label: const Text('Bitir'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          TextButton.icon(
            onPressed: state.isBusy ? null : () => cubit.discard(),
            icon: const Icon(Icons.delete_outline, color: AppColors.error),
            label: const Text('Seansı iptal et', style: TextStyle(color: AppColors.error)),
          ),
        ],
      ),
    );
  }
}
