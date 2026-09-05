import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/password_reset_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/widgets/auth_field_decoration.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// D-01 · Adım 1: Şifre sıfırlama kodu isteme ekranı.
///
/// Sunucu, e-posta kayıtlı olmasa da 200 döner (enumeration engeli); bu yüzden
/// başarı metni "kod gönderildi" değil, "kayıtlıysa gönderildi" der.
class PasswordResetRequestPage extends StatefulWidget {
  const PasswordResetRequestPage({super.key, this.initialEmail = ''});

  final String initialEmail;

  @override
  State<PasswordResetRequestPage> createState() =>
      _PasswordResetRequestPageState();
}

class _PasswordResetRequestPageState extends State<PasswordResetRequestPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _emailController = TextEditingController(
    text: widget.initialEmail,
  );
  late final PasswordResetCubit _cubit = PasswordResetCubit.create();

  @override
  void dispose() {
    _emailController.dispose();
    _cubit.close();
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    _cubit.requestReset(_emailController.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return BlocProvider<PasswordResetCubit>.value(
      value: _cubit,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          backgroundColor: AppColors.background,
          elevation: 0,
          foregroundColor: AppColors.textPrimary,
          title: Text(AppLocalizations.of(context).auth_forgotPassword),
          leading: IconButton(
            icon: const Icon(Icons.arrow_back_rounded),
            onPressed: () => context.go('/login'),
          ),
        ),
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 430),
                child: BlocConsumer<PasswordResetCubit, PasswordResetState>(
                  listener: (context, state) {
                    if (state.status == PasswordResetStatus.sent) {
                      context.push(
                        '/password-reset/confirm',
                        extra: _emailController.text.trim(),
                      );
                    }
                  },
                  builder: (context, state) {
                    final isBusy = state.status == PasswordResetStatus.sending;

                    return Container(
                      padding: const EdgeInsets.all(18),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: BorderRadius.circular(24),
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.soft,
                      ),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            if (state.status == PasswordResetStatus.failure &&
                                state.message != null) ...<Widget>[
                              ErrorStateView(message: state.message!),
                              const SizedBox(height: 14),
                            ],
                            Text(
                              AppLocalizations.of(context).auth_resetRequestIntro,
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: AppColors.textSecondary,
                              ),
                            ),
                            const SizedBox(height: 18),
                            Text(
                              AppLocalizations.of(context).auth_emailAddress,
                              style: theme.textTheme.labelMedium?.copyWith(
                                color: AppColors.textSecondary,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 8),
                            TextFormField(
                              controller: _emailController,
                              keyboardType: TextInputType.emailAddress,
                              decoration: authFieldDecoration(
                                hintText: AppLocalizations.of(context).common_emailHint,
                                icon: Icons.mail_outline_rounded,
                              ),
                              validator: (value) {
                                if (value == null || !value.contains('@')) {
                                  return AppLocalizations.of(context).common_invalidEmail;
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 18),
                            SizedBox(
                              width: double.infinity,
                              child: FilledButton(
                                onPressed: isBusy ? null : _submit,
                                child: Text(
                                  isBusy
                                      ? AppLocalizations.of(context).auth_sending
                                      : AppLocalizations.of(context).auth_sendResetCode,
                                ),
                              ),
                            ),
                            const SizedBox(height: 6),
                            TextButton(
                              onPressed: () => context.push(
                                '/password-reset/confirm',
                                extra: _emailController.text.trim(),
                              ),
                              child: Text(AppLocalizations.of(context).auth_haveCodeAlready),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
