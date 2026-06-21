import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  static const _primary = Color(0xFF082B4F);
  static const _primaryDark = Color(0xFF061F3A);
  static const _background = Color(0xFFF7F9FC);
  static const _surface = Color(0xFFFFFFFF);
  static const _textPrimary = Color(0xFF111827);
  static const _textSecondary = Color(0xFF6B7280);
  static const _border = Color(0xFFE5E7EB);

  final _formKey = GlobalKey<FormState>();
  final _firstNameController = TextEditingController(text: 'Ayse');
  final _lastNameController = TextEditingController(text: 'Yilmaz');
  final _phoneController = TextEditingController(text: '5551112233');
  final _emailController = TextEditingController(text: 'teacher1@example.com');
  final _passwordController = TextEditingController(text: 'Teacher123!');
  bool _obscurePassword = true;

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 10, 20, 10),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 430),
              child: BlocBuilder<AuthCubit, AuthState>(
                builder: (context, state) {
                  return Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      const _LogoHeader(),
                      const SizedBox(height: 8),
                      _RoleSelectionReturnBanner(
                        onTap: () => context.go('/role-selection'),
                      ),
                      const SizedBox(height: 10),
                      Flexible(
                        child: Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            color: _surface,
                            borderRadius: BorderRadius.circular(24),
                            border: Border.all(color: _border),
                            boxShadow: const <BoxShadow>[
                              BoxShadow(
                                color: Color(0x12082B4F),
                                blurRadius: 24,
                                offset: Offset(0, 12),
                              ),
                            ],
                          ),
                          child: Form(
                            key: _formKey,
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: <Widget>[
                                if (state.errorMessage != null) ...<Widget>[
                                  ErrorStateView(message: state.errorMessage!),
                                  const SizedBox(height: 10),
                                ],
                                Row(
                                  children: <Widget>[
                                    Expanded(
                                      child: _LabeledField(
                                        label: 'Ad',
                                        child: TextFormField(
                                          controller: _firstNameController,
                                          decoration: _inputDecoration(
                                            hintText: 'Adiniz',
                                            icon: Icons.person_outline_rounded,
                                          ),
                                          validator: _requiredValidator,
                                        ),
                                      ),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: _LabeledField(
                                        label: 'Soyad',
                                        child: TextFormField(
                                          controller: _lastNameController,
                                          decoration: _inputDecoration(
                                            hintText: 'Soyadiniz',
                                            icon: Icons.badge_outlined,
                                          ),
                                          validator: _requiredValidator,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 10),
                                _LabeledField(
                                  label: 'Telefon',
                                  child: TextFormField(
                                    controller: _phoneController,
                                    keyboardType: TextInputType.phone,
                                    decoration: _inputDecoration(
                                      hintText: '5xx xxx xx xx',
                                      icon: Icons.phone_outlined,
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 10),
                                _LabeledField(
                                  label: 'E-posta',
                                  child: TextFormField(
                                    controller: _emailController,
                                    keyboardType: TextInputType.emailAddress,
                                    decoration: _inputDecoration(
                                      hintText: 'ornek@mail.com',
                                      icon: Icons.mail_outline_rounded,
                                    ),
                                    validator: (value) {
                                      if (value == null ||
                                          !value.contains('@')) {
                                        return 'Gecerli bir e-posta gir.';
                                      }
                                      return null;
                                    },
                                  ),
                                ),
                                const SizedBox(height: 10),
                                _LabeledField(
                                  label: 'Sifre',
                                  child: TextFormField(
                                    controller: _passwordController,
                                    obscureText: _obscurePassword,
                                    decoration: _inputDecoration(
                                      hintText: '••••••••',
                                      icon: Icons.lock_outline_rounded,
                                      suffixIcon: IconButton(
                                        onPressed: () {
                                          setState(() {
                                            _obscurePassword =
                                                !_obscurePassword;
                                          });
                                        },
                                        icon: Icon(
                                          _obscurePassword
                                              ? Icons.visibility_outlined
                                              : Icons.visibility_off_outlined,
                                        ),
                                      ),
                                    ),
                                    validator: (value) {
                                      if (value == null || value.length < 8) {
                                        return 'Sifre en az 8 karakter olmali.';
                                      }
                                      return null;
                                    },
                                  ),
                                ),
                                const SizedBox(height: 14),
                                Container(
                                  height: 1,
                                  color: const Color(0xFFEFF2F6),
                                ),
                                const SizedBox(height: 14),
                                SizedBox(
                                  width: double.infinity,
                                  child: FilledButton(
                                    onPressed: state.status == AuthStatus.loading
                                        ? null
                                        : () {
                                            if (_formKey.currentState
                                                    ?.validate() ??
                                                false) {
                                              context.read<AuthCubit>().register(
                                                email: _emailController.text
                                                    .trim(),
                                                password: _passwordController
                                                    .text
                                                    .trim(),
                                                firstName:
                                                    _firstNameController.text
                                                        .trim(),
                                                lastName:
                                                    _lastNameController.text
                                                        .trim(),
                                                phoneNumber: _phoneController
                                                    .text
                                                    .trim(),
                                              );
                                            }
                                          },
                                    style: FilledButton.styleFrom(
                                      backgroundColor: _primary,
                                      foregroundColor: Colors.white,
                                      minimumSize: const Size.fromHeight(50),
                                      shape: RoundedRectangleBorder(
                                        borderRadius: BorderRadius.circular(14),
                                      ),
                                      elevation: 0,
                                    ),
                                    child: state.status == AuthStatus.loading
                                        ? const SizedBox(
                                            width: 22,
                                            height: 22,
                                            child: CircularProgressIndicator(
                                              strokeWidth: 2.2,
                                              color: Colors.white,
                                            ),
                                          )
                                        : Text(
                                            'Kayit Ol',
                                            style: theme.textTheme.titleMedium
                                                ?.copyWith(
                                                  color: Colors.white,
                                                  fontWeight: FontWeight.w700,
                                                ),
                                          ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Center(
                        child: RichText(
                          text: TextSpan(
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: _textSecondary,
                              fontWeight: FontWeight.w400,
                            ),
                            children: <InlineSpan>[
                              const TextSpan(text: 'Zaten hesabin var mi? '),
                              WidgetSpan(
                                alignment: PlaceholderAlignment.middle,
                                child: GestureDetector(
                                  onTap: () =>
                                      context.go('/login?role=ogretmen'),
                                  child: Text(
                                    'Giris Yap',
                                    style: theme.textTheme.bodyMedium?.copyWith(
                                      color: _primary,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }

  InputDecoration _inputDecoration({
    required String hintText,
    required IconData icon,
    Widget? suffixIcon,
  }) {
    return InputDecoration(
      hintText: hintText,
      prefixIcon: Icon(icon),
      suffixIcon: suffixIcon,
      filled: true,
      fillColor: const Color(0xFFF9FBFD),
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: _border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: _border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: _primary, width: 1.4),
      ),
    );
  }

  String? _requiredValidator(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Bu alan zorunlu.';
    }
    return null;
  }
}

class _LogoHeader extends StatelessWidget {
  const _LogoHeader();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      children: <Widget>[
        Container(
          width: 60,
          height: 60,
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[
                _RegisterPageState._primary,
                _RegisterPageState._primaryDark,
              ],
            ),
            borderRadius: BorderRadius.circular(24),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: Color(0x19082B4F),
                blurRadius: 22,
                offset: Offset(0, 12),
              ),
            ],
          ),
          child: const Icon(Icons.school_rounded, color: Colors.white, size: 32),
        ),
        const SizedBox(height: 10),
        Text(
          'Kayit Ol',
          textAlign: TextAlign.center,
          style: theme.textTheme.headlineSmall?.copyWith(
            color: _RegisterPageState._textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          'Ogretmen hesabinizi olusturarak ders yonetimine baslayin.',
          textAlign: TextAlign.center,
          style: theme.textTheme.bodyMedium?.copyWith(
            color: _RegisterPageState._textSecondary,
            height: 1.45,
          ),
        ),
      ],
    );
  }
}

class _RoleSelectionReturnBanner extends StatelessWidget {
  const _RoleSelectionReturnBanner({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: const Color(0xFFEAF2FB),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _RegisterPageState._border),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 34,
              height: 34,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(
                Icons.people_alt_outlined,
                color: _RegisterPageState._primary,
                size: 18,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Hesap turunu degistir',
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: _RegisterPageState._textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Rol secim ekranina geri don',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _RegisterPageState._textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.arrow_forward_ios_rounded,
              size: 16,
              color: _RegisterPageState._textSecondary,
            ),
          ],
        ),
      ),
    );
  }
}

class _LabeledField extends StatelessWidget {
  const _LabeledField({required this.label, required this.child});

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
            color: _RegisterPageState._textSecondary,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 6),
        child,
      ],
    );
  }
}
