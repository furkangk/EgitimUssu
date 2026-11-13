import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/email_verification_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/widgets/auth_field_decoration.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// D-02: E-posta doğrulama ekranı.
///
/// E-postadaki `egitimussu://verify-email?token=…` derin bağlantısıyla açıldığında
/// kod alanı hazır dolu gelir; bağlantı açılmazsa kullanıcı kodu elle yapıştırır.
class EmailVerificationPage extends StatefulWidget {
  const EmailVerificationPage({
    super.key,
    this.email = '',
    this.initialToken = '',
  });

  final String email;
  final String initialToken;

  @override
  State<EmailVerificationPage> createState() => _EmailVerificationPageState();
}

class _EmailVerificationPageState extends State<EmailVerificationPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _emailController = TextEditingController(
    text: widget.email,
  );
  late final TextEditingController _tokenController = TextEditingController(
    text: widget.initialToken,
  );
  late final EmailVerificationCubit _cubit = EmailVerificationCubit.create();

  @override
  void dispose() {
    _emailController.dispose();
    _tokenController.dispose();
    _cubit.close();
    super.dispose();
  }

  void _verify() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    _cubit.verify(
      email: _emailController.text.trim(),
      token: _tokenController.text.trim(),
    );
  }

  void _resend() {
    final email = _emailController.text.trim();
    if (!email.contains('@')) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppLocalizations.of(context).auth_verifyEmailEnterFirst)),
      );
      return;
    }
    _cubit.resendCode(email);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return BlocProvider<EmailVerificationCubit>.value(
      value: _cubit,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          backgroundColor: AppColors.background,
          elevation: 0,
          foregroundColor: AppColors.textPrimary,
          title: Text(AppLocalizations.of(context).auth_verifyEmailTitle),
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
                child:
                    BlocConsumer<EmailVerificationCubit, EmailVerificationState>(
                      listener: (context, state) {
                        if (state.status == EmailVerificationStatus.verified) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(AppLocalizations.of(context).auth_verifyEmailConfirmed),
                            ),
                          );
                          context.go('/login');
                        }
                        if (state.status == EmailVerificationStatus.resent) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(
                                AppLocalizations.of(context).auth_verifyEmailCodeResent,
                              ),
                            ),
                          );
                        }
                      },
                      builder: (context, state) {
                        final isVerifying =
                            state.status == EmailVerificationStatus.verifying;
                        final isResending =
                            state.status == EmailVerificationStatus.resending;

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
                                if (state.status ==
                                        EmailVerificationStatus.failure &&
                                    state.message != null) ...<Widget>[
                                  ErrorStateView(message: state.message!),
                                  const SizedBox(height: 14),
                                ],
                                Text(
                                  AppLocalizations.of(context).auth_verifyEmailIntro,
                                  style: theme.textTheme.bodyMedium?.copyWith(
                                    color: AppColors.textSecondary,
                                  ),
                                ),
                                const SizedBox(height: 18),
                                _label(theme, AppLocalizations.of(context).auth_emailAddress),
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
                                const SizedBox(height: 14),
                                _label(theme, AppLocalizations.of(context).auth_verificationCode),
                                const SizedBox(height: 8),
                                TextFormField(
                                  controller: _tokenController,
                                  decoration: authFieldDecoration(
                                    hintText: AppLocalizations.of(context).auth_codeFromEmail,
                                    icon: Icons.verified_outlined,
                                  ),
                                  validator: (value) {
                                    if (value == null || value.trim().isEmpty) {
                                      return AppLocalizations.of(context).auth_verificationCodeRequired;
                                    }
                                    return null;
                                  },
                                ),
                                const SizedBox(height: 18),
                                SizedBox(
                                  width: double.infinity,
                                  child: FilledButton(
                                    onPressed: isVerifying ? null : _verify,
                                    child: Text(
                                      isVerifying
                                          ? AppLocalizations.of(context).auth_verifying
                                          : AppLocalizations.of(context).auth_verify,
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 6),
                                TextButton(
                                  onPressed: isResending ? null : _resend,
                                  child: Text(
                                    isResending
                                        ? AppLocalizations.of(context).auth_sending
                                        : AppLocalizations.of(context).auth_resendCode,
                                  ),
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

  Widget _label(ThemeData theme, String text) => Text(
    text,
    style: theme.textTheme.labelMedium?.copyWith(
      color: AppColors.textSecondary,
      fontWeight: FontWeight.w700,
    ),
  );
}
