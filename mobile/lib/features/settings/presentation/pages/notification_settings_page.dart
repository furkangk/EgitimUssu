import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/cubit/settings_cubit.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/widgets/settings_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// D-14: Bildirim tercihleri — gerçek `GET`/`PUT /api/settings/users/{userId}`.
/// Her anahtar anında kaydedilir; hata olursa eski değere döner ve SnackBar çıkar.
class NotificationSettingsPage extends StatelessWidget {
  const NotificationSettingsPage({super.key, this.cubit});

  /// Testlerde hazır bir cubit verilebilir; üretimde DI'dan üretilir.
  final SettingsCubit? cubit;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);

    if (userId == null) {
      return const SettingsScaffold(
        title: 'Bildirim ayarlari',
        child: EmptyStateView(
          title: 'Oturum bulunamadi',
          subtitle: 'Ayarlarini gormek icin giris yapmalisin.',
        ),
      );
    }

    return BlocProvider<SettingsCubit>(
      create: (_) => (cubit ?? SettingsCubit.create())..load(userId),
      child: const _NotificationSettingsView(),
    );
  }
}

class _NotificationSettingsView extends StatelessWidget {
  const _NotificationSettingsView();

  @override
  Widget build(BuildContext context) {
    return SettingsScaffold(
      title: 'Bildirim ayarlari',
      child: BlocConsumer<SettingsCubit, SettingsState>(
        listener: settingsErrorListener,
        builder: (context, state) {
          if (state.isLoading) {
            return const LoadingStateView(message: 'Ayarlar yukleniyor...');
          }

          final settings = state.settings;
          if (settings == null) {
            return ErrorStateView(
              message: state.errorMessage ?? 'Ayarlar yuklenemedi.',
              onRetry: context.read<SettingsCubit>().refresh,
            );
          }

          final cubit = context.read<SettingsCubit>();

          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: <Widget>[
              const SettingsSectionTitle('Kanallar'),
              SettingsPanel(
                children: <Widget>[
                  SettingsSwitchRow(
                    icon: Icons.notifications_active_outlined,
                    title: 'Anlik bildirimler',
                    subtitle:
                        'Kapatirsan hicbir tur icin telefonuna bildirim gelmez.',
                    value: settings.pushNotificationsEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setPushNotificationsEnabled,
                  ),
                  const SettingsDivider(),
                  SettingsSwitchRow(
                    icon: Icons.mark_email_unread_outlined,
                    title: 'E-posta bildirimleri',
                    subtitle: 'Ozet ve hatirlatmalari e-posta ile de al.',
                    value: settings.emailNotificationsEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setEmailNotificationsEnabled,
                  ),
                ],
              ),
              const SizedBox(height: 18),
              const SettingsSectionTitle('Turler'),
              SettingsPanel(
                children: <Widget>[
                  SettingsSwitchRow(
                    icon: Icons.event_available_outlined,
                    title: 'Ders hatirlatmalari',
                    subtitle: 'Yaklasan derslerden once bildirim al.',
                    value: settings.upcomingLessonReminderEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setUpcomingLessonReminderEnabled,
                  ),
                  const SettingsDivider(),
                  SettingsSwitchRow(
                    icon: Icons.assignment_outlined,
                    title: 'Odev hatirlatmalari',
                    subtitle: 'Yeni, yaklasan ve kacirilan odevler.',
                    value: settings.homeworkReminderEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setHomeworkReminderEnabled,
                  ),
                  const SettingsDivider(),
                  SettingsSwitchRow(
                    icon: Icons.payments_outlined,
                    title: 'Odeme hatirlatmalari',
                    subtitle: 'Vadesi gelen ve geciken odemeleri kacirma.',
                    value: settings.paymentReminderEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setPaymentReminderEnabled,
                  ),
                  const SettingsDivider(),
                  SettingsSwitchRow(
                    icon: Icons.summarize_outlined,
                    title: 'Haftalik ozet',
                    subtitle: 'Hafta sonunda ders ve calisma ozeti al.',
                    value: settings.weeklySummaryEnabled,
                    isSaving: state.isSaving,
                    onChanged: cubit.setWeeklySummaryEnabled,
                  ),
                ],
              ),
              const SizedBox(height: 14),
              const SettingsHint(
                'Kapattigin bir tur icin bildirim gonderilmez; gecmis bildirimler '
                'bildirim merkezinde kalir.',
              ),
            ],
          );
        },
      ),
    );
  }
}
