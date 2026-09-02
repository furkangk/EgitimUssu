import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/password_reset_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/widgets/auth_field_decoration.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// D-01 · Adım 2: E-postadaki kod ile yeni şifreyi belirleme ekranı.
///
/// Kod, e-postadaki `egitimussu://password-reset?token=...` derin bağlantısından
/// otomatik gelebilir ya da elle yapıştırılır.
class PasswordResetConfirmPage extends StatefulWidget {
  const PasswordResetConfirmPage({
    super.key,
    this.email = '',
    this.initialToken = '',
  });

  final String email;
  final String initialToken;

  @override
  State<PasswordResetConfirmPage> createState() =>
      _PasswordResetConfirmPageState();
}

class _PasswordResetConfirmPageState extends State<PasswordResetConfirmPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _emailController = TextEditingController(
    text: widget.email,
  );
  late final TextEditingController _tokenController = TextEditingController(
    text: widget.initialToken,
  );
  final _passwordController = TextEditingController();
  final _passwordAgainController = TextEditingController();
  late final PasswordResetCubit _cubit = PasswordResetCubit.create();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _emailController.dispose();
    _tokenController.dispose();
    _passwordController.dispose();
    _passwordAgainController.dispose();
    _cubit.close();
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    _cubit.confirmReset(
      email: _emailController.text.trim(),
      token: _tokenController.text.trim(),
      newPassword: _passwordController.text,
    );
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
          title: const Text('Yeni şifre belirle'),
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
                    if (state.status == PasswordResetStatus.done) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'Şifren güncellendi, giriş yapabilirsin.',
                          ),
                        ),
                      );
                      context.go('/login');
                    }
                  },
                  builder: (context, state) {
                    final isBusy =
                        state.status == PasswordResetStatus.confirming;

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
                              'E-postana gönderdiğimiz sıfırlama kodunu ve yeni '
                              'şifreni gir.',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: AppColors.textSecondary,
                              ),
                            ),
                            const SizedBox(height: 18),
                            _label(theme, 'E-posta adresin'),
                            const SizedBox(height: 8),
                            TextFormField(
                              controller: _emailController,
                              keyboardType: TextInputType.emailAddress,
                              decoration: authFieldDecoration(
                                hintText: 'ornek@mail.com',
                                icon: Icons.mail_outline_rounded,
                              ),
                              validator: (value) {
                                if (value == null || !value.contains('@')) {
                                  return 'Geçerli bir e-posta gir.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            _label(theme, 'Sıfırlama kodu'),
                            const SizedBox(height: 8),
                            TextFormField(
                              controller: _tokenController,
                              decoration: authFieldDecoration(
                                hintText: 'E-postadaki kod',
                                icon: Icons.key_outlined,
                              ),
                              validator: (value) {
                                if (value == null || value.trim().isEmpty) {
                                  return 'Sıfırlama kodunu gir.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            _label(theme, 'Yeni şifre'),
                            const SizedBox(height: 8),
                            TextFormField(
                              controller: _passwordController,
                              obscureText: _obscurePassword,
                              decoration: authFieldDecoration(
                                hintText: '••••••••',
                                icon: Icons.lock_outline_rounded,
                                suffixIcon: IconButton(
                                  onPressed: () => setState(
                                    () =>
                                        _obscurePassword = !_obscurePassword,
                                  ),
                                  icon: Icon(
                                    _obscurePassword
                                        ? Icons.visibility_outlined
                                        : Icons.visibility_off_outlined,
                                  ),
                                ),
                              ),
                              validator: (value) {
                                if (value == null || value.length < 8) {
                                  return 'Şifre en az 8 karakter olmalı.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            _label(theme, 'Yeni şifre (tekrar)'),
                            const SizedBox(height: 8),
                            TextFormField(
                              controller: _passwordAgainController,
                              obscureText: _obscurePassword,
                              decoration: authFieldDecoration(
                                hintText: '••••••••',
                                icon: Icons.lock_outline_rounded,
                              ),
                              validator: (value) {
                                if (value != _passwordController.text) {
                                  return 'Şifreler eşleşmiyor.';
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
                                      ? 'Güncelleniyor...'
                                      : 'Şifremi güncelle',
                                ),
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
