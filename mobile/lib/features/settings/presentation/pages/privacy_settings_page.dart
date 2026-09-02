import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/cubit/settings_cubit.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/widgets/settings_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// D-14 / M15-3 / M15-4: Gizlilik ve guvenlik ayarlari — gizlilik seviyesi ve
/// "diger oturumlari sonlandir" politikasi gercek backend'e yazilir.
class PrivacySettingsPage extends StatelessWidget {
  const PrivacySettingsPage({super.key, this.cubit});

  /// Testlerde hazir bir cubit verilebilir; uretimde DI'dan uretilir.
  final SettingsCubit? cubit;

  @override
  Widget build(BuildContext context) {
    final userId = context.select((AuthCubit c) => c.state.session?.userId);

    if (userId == null) {
      return const SettingsScaffold(
        title: 'Gizlilik ve guvenlik',
        child: EmptyStateView(
          title: 'Oturum bulunamadi',
          subtitle: 'Ayarlarini gormek icin giris yapmalisin.',
        ),
      );
    }

    return BlocProvider<SettingsCubit>(
      create: (_) => (cubit ?? SettingsCubit.create())..load(userId),
      child: const _PrivacySettingsView(),
    );
  }
}

class _PrivacySettingsView extends StatelessWidget {
  const _PrivacySettingsView();

  @override
  Widget build(BuildContext context) {
    return SettingsScaffold(
      title: 'Gizlilik ve guvenlik',
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
              const SettingsSectionTitle('Calisma verisi gizliligi'),
              SettingsPanel(
                children: <Widget>[
                  for (final level in PrivacyLevel.values) ...<Widget>[
                    SettingsChoiceRow(
                      title: level.label,
                      subtitle: level.description,
                      selected: settings.privacyLevel == level,
                      isSaving: state.isSaving,
                      onTap: () => cubit.setPrivacyLevel(level),
                    ),
                    if (level != PrivacyLevel.values.last)
                      const SettingsDivider(),
                  ],
                ],
              ),
              const SizedBox(height: 14),
              const SettingsHint(
                'Ogretmen ve onayli veli yalnizca burada izin verdigin kadarini gorur. '
                'Ders, odev ve odeme bilgileri bu ayardan etkilenmez.',
              ),
              const SizedBox(height: 18),
              const SettingsSectionTitle('Oturum guvenligi'),
              SettingsPanel(
                children: <Widget>[
                  for (final policy in SessionTerminationPolicy.values) ...<Widget>[
                    SettingsChoiceRow(
                      title: policy.label,
                      subtitle: policy.description,
                      selected: settings.sessionTerminationPolicy == policy,
                      isSaving: state.isSaving,
                      onTap: () => cubit.setSessionTerminationPolicy(policy),
                    ),
                    if (policy != SessionTerminationPolicy.values.last)
                      const SettingsDivider(),
                  ],
                ],
              ),
              const SizedBox(height: 14),
              const SettingsHint(
                '"Yalniz son cihazda kal" secildiginde, bir sonraki girisinde diger '
                'cihazlardaki oturumlarin kapanir.',
              ),
            ],
          );
        },
      ),
    );
  }
}
