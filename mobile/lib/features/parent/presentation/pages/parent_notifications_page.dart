import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class ParentNotificationsPage extends StatelessWidget {
  const ParentNotificationsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final userId = context.select(
      (AuthCubit c) => c.state.session?.userId ?? 'mock-parent-user',
    );
    final fullName = context.select(
      (AuthCubit c) => c.state.session?.fullName ?? 'Veli',
    );

    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..load(userId, fullName: fullName),
      child: Scaffold(
        backgroundColor: AppColors.background,
        bottomNavigationBar: const ParentBottomNav(
          current: ParentNavTab.notifications,
        ),
        body: SafeArea(
          child: Column(
            children: <Widget>[
              const ParentHeader(
                title: 'Bildirim tercihleri',
                subtitle: 'Hangi durumlarda haberdar olmak istersiniz?',
              ),
              Expanded(
                child: BlocBuilder<ParentCubit, ParentState>(
                  builder: (context, state) {
                    if (state.isLoading || state.profile == null) {
                      if (state.status == ParentStatus.error) {
                        return Padding(
                          padding: const EdgeInsets.all(20),
                          child: ErrorStateView(
                            message: state.errorMessage ?? 'Bir hata oluştu.',
                            onRetry: () =>
                                context.read<ParentCubit>().load(userId),
                          ),
                        );
                      }
                      return const LoadingStateView();
                    }
                    return _PrefsForm(
                      key: ValueKey<String>(state.profile!.id),
                      userId: userId,
                      profile: state.profile!,
                      saving: state.prefsSaving,
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PrefsForm extends StatefulWidget {
  const _PrefsForm({
    super.key,
    required this.userId,
    required this.profile,
    required this.saving,
  });

  final String userId;
  final ParentProfile profile;
  final bool saving;

  @override
  State<_PrefsForm> createState() => _PrefsFormState();
}

class _PrefsFormState extends State<_PrefsForm> {
  late bool _missedAssignment;
  late bool _weeklyProgressSummary;
  late bool _lessonReminders;
  late bool _testResults;
  late bool _payments;
  late String _channel;

  @override
  void initState() {
    super.initState();
    final p = widget.profile.preferences;
    _missedAssignment = p.missedAssignment;
    _weeklyProgressSummary = p.weeklyProgressSummary;
    _lessonReminders = p.lessonReminders;
    _testResults = p.testResults;
    _payments = p.payments;
    _channel = p.channel;
  }

  void _save() {
    context.read<ParentCubit>().updatePreferences(
      widget.userId,
      ParentNotificationPreferences(
        missedAssignment: _missedAssignment,
        weeklyProgressSummary: _weeklyProgressSummary,
        lessonReminders: _lessonReminders,
        testResults: _testResults,
        payments: _payments,
        channel: _channel,
      ),
    );
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Bildirim tercihleri kaydedildi.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
      children: <Widget>[
        ParentCard(
          title: 'Uyarılar',
          icon: Icons.notifications_active_rounded,
          child: Column(
            children: <Widget>[
              _SwitchRow(
                title: 'Ödev kaçırma',
                subtitle: 'Çocuğunuz ödevini zamanında yüklemezse',
                value: _missedAssignment,
                onChanged: (v) => setState(() => _missedAssignment = v),
              ),
              _SwitchRow(
                title: 'Haftalık gelişim özeti',
                subtitle: 'Her hafta çalışma ve gelişim özeti',
                value: _weeklyProgressSummary,
                onChanged: (v) => setState(() => _weeklyProgressSummary = v),
              ),
              _SwitchRow(
                title: 'Ders hatırlatmaları',
                subtitle: 'Yaklaşan dersler (öğretmen bağlıysa)',
                value: _lessonReminders,
                onChanged: (v) => setState(() => _lessonReminders = v),
              ),
              _SwitchRow(
                title: 'Deneme sonuçları',
                subtitle: 'Yeni deneme/test sonucu geldiğinde',
                value: _testResults,
                onChanged: (v) => setState(() => _testResults = v),
              ),
              _SwitchRow(
                title: 'Ödeme hatırlatmaları',
                subtitle: 'Özel ders ödemeleri (öğretmen bağlıysa)',
                value: _payments,
                onChanged: (v) => setState(() => _payments = v),
                isLast: true,
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        ParentCard(
          title: 'Bildirim kanalı',
          icon: Icons.send_rounded,
          child: Column(
            children: <Widget>[
              for (final option in const <List<String>>[
                <String>['Push', 'Uygulama bildirimi'],
                <String>['Email', 'E-posta'],
                <String>['Both', 'Her ikisi'],
              ])
                RadioListTile<String>(
                  contentPadding: EdgeInsets.zero,
                  activeColor: AppColors.primary,
                  value: option[0],
                  groupValue: _channel,
                  onChanged: (v) => setState(() => _channel = v ?? _channel),
                  title: Text(option[1]),
                ),
            ],
          ),
        ),
        const SizedBox(height: 18),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.primary,
              minimumSize: const Size.fromHeight(50),
            ),
            onPressed: widget.saving ? null : _save,
            child: widget.saving
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2.2,
                      color: Colors.white,
                    ),
                  )
                : const Text('Kaydet'),
          ),
        ),
      ],
    );
  }
}

class _SwitchRow extends StatelessWidget {
  const _SwitchRow({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
    this.isLast = false,
  });

  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            Switch(
              value: value,
              activeColor: AppColors.primary,
              onChanged: onChanged,
            ),
          ],
        ),
        if (!isLast) const Divider(height: 14, color: AppColors.divider),
      ],
    );
  }
}
