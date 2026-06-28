import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';

class StudentsPage extends StatefulWidget {
  const StudentsPage({super.key});

  @override
  State<StudentsPage> createState() => _StudentsPageState();
}

class _StudentsPageState extends State<StudentsPage> {
  final TextEditingController _searchController = TextEditingController();
  late final StudentsCubit _cubit;
  String _query = '';

  @override
  void initState() {
    super.initState();
    _cubit = StudentsCubit.create();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId != null &&
        _cubit.state.students.isEmpty &&
        !_cubit.state.isLoading) {
      _cubit.load(userId);
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    _cubit.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit c) => c.state.session);
    final teacherName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Öğretmen';

    return BlocProvider<StudentsCubit>.value(
      value: _cubit,
      child: BlocConsumer<StudentsCubit, StudentsState>(
        listener: (context, state) {
          if (state.successMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.successMessage!),
                backgroundColor: AppColors.accentGreen,
                behavior: SnackBarBehavior.floating,
              ),
            );
          }
          if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.errorMessage!),
                backgroundColor: AppColors.accentRed,
                behavior: SnackBarBehavior.floating,
              ),
            );
          }
        },
        builder: (context, state) {
          final filtered = state.students.where((s) {
            final q = _query.trim().toLowerCase();
            if (q.isEmpty) return true;
            return s.fullName.toLowerCase().contains(q) ||
                s.gradeLevel.toLowerCase().contains(q);
          }).toList();

          return Scaffold(
            backgroundColor: AppColors.background,
            body: SafeArea(
              child: RefreshIndicator(
                color: AppColors.primary,
                onRefresh: () {
                  final userId = context
                      .read<AuthCubit>()
                      .state
                      .session
                      ?.userId;
                  if (userId != null) return _cubit.load(userId);
                  return Future<void>.value();
                },
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(16, 10, 16, 116),
                  children: <Widget>[
                    AppPageHeader(title: teacherName),
                    const SizedBox(height: 20),
                    _SearchField(
                      controller: _searchController,
                      onChanged: (v) => setState(() => _query = v),
                    ),
                    const SizedBox(height: 18),
                    Row(
                      children: <Widget>[
                        Text(
                          'Öğrenciler',
                          style: Theme.of(context).textTheme.titleLarge
                              ?.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                        const Spacer(),
                        if (!state.isLoading)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 8,
                            ),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(999),
                              border: Border.all(color: AppColors.skyBorder),
                            ),
                            child: Text(
                              '${filtered.length} kayıt',
                              style: Theme.of(context).textTheme.labelMedium
                                  ?.copyWith(
                                    color: AppColors.primary,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    if (state.isLoading)
                      _ShimmerList()
                    else if (state.errorMessage != null &&
                        state.students.isEmpty)
                      _ErrorCard(
                        message: state.errorMessage!,
                        onRetry: () {
                          final userId = context
                              .read<AuthCubit>()
                              .state
                              .session
                              ?.userId;
                          if (userId != null) _cubit.load(userId);
                        },
                      )
                    else if (filtered.isEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 24),
                        child: EmptyStateView(
                          title: _query.isEmpty
                              ? 'Henüz öğrenci yok'
                              : 'Öğrenci bulunamadı',
                          subtitle: _query.isEmpty
                              ? 'Yeni öğrenci ekle butonuna tıklayarak başla.'
                              : 'Arama sonucuna uygun öğrenci kaydı görünmüyor.',
                        ),
                      )
                    else
                      ...List<Widget>.generate(filtered.length, (index) {
                        final student = filtered[index];
                        return Padding(
                          padding: EdgeInsets.only(
                            bottom: index == filtered.length - 1 ? 0 : 12,
                          ),
                          child: _StudentCard(
                            student: student,
                            accent: _accentForIndex(index),
                            onTap: () =>
                                context.push('/students/${student.id}'),
                          ),
                        );
                      }),
                  ],
                ),
              ),
            ),
            bottomNavigationBar: const AppBottomNav(
              current: AppNavTab.students,
            ),
            floatingActionButton: FloatingActionButton.extended(
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              onPressed: () => _showAddStudentSheet(
                context,
                teacherUserId: session?.userId ?? '',
              ),
              icon: const Icon(Icons.add_rounded),
              label: const Text('Yeni Öğrenci Ekle'),
            ),
          );
        },
      ),
    );
  }

  Color _accentForIndex(int index) {
    const colors = <Color>[
      AppColors.accentGreen,
      AppColors.accentBlue,
      AppColors.amber,
      AppColors.accentTeal,
    ];
    return colors[index % colors.length];
  }

  Future<void> _showAddStudentSheet(
    BuildContext context, {
    required String teacherUserId,
  }) async {
    final profile = await showModalBottomSheet<StudentProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (sheetContext) => _AddStudentSheet(teacherUserId: teacherUserId),
    );

    if (profile == null || !context.mounted) return;
    _cubit.addStudent(profile);
  }

  static String? _validateEmail(String? value) {
    if (value == null || value.trim().isEmpty) return null;
    final email = value.trim();
    if (!email.contains('@') || !email.contains('.')) {
      return 'Geçerli bir e-posta gir.';
    }
    return null;
  }

  static String? _validateGoal(String? value) {
    if (value == null || value.trim().isEmpty) return null;
    if (value.trim().length < 5) return 'Hedef bilgisi daha açık olmalı.';
    return null;
  }

  static String? _validateGradeLevel(String? value) {
    if (value == null || value.trim().isEmpty) return 'Seviye bilgisi zorunlu.';
    if (value.trim().length < 2) return 'Seviye bilgisi çok kısa.';
    return null;
  }

  static String? _validatePhone(String? value) {
    if (value == null || value.trim().isEmpty) return null;
    final digitsOnly = value.replaceAll(RegExp(r'[^0-9]'), '');
    if (digitsOnly.length < 10) return 'Telefon en az 10 rakam olmalı.';
    return null;
  }

  static String? _validateStudentName(String? value) {
    if (value == null || value.trim().isEmpty) return 'Öğrenci adı zorunlu.';
    if (value.trim().split(RegExp(r'\s+')).length < 2) {
      return 'Ad ve soyad birlikte girilmeli.';
    }
    return null;
  }

  static String? _validateSubject(String? value) {
    if (value == null || value.trim().isEmpty) return 'Ders bilgisi zorunlu.';
    if (value.trim().length < 3) return 'Ders bilgisi çok kısa.';
    return null;
  }
}

// ── Shimmer yükleme ─────────────────────────────────────────────────────────

class _ShimmerList extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Shimmer.fromColors(
      baseColor: const Color(0xFFEEF4FB),
      highlightColor: Colors.white,
      child: Column(
        children: List<Widget>.generate(
          5,
          (_) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Container(
              height: 86,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(22),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ── Hata kartı ──────────────────────────────────────────────────────────────

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.skyBorder),
      ),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.error_outline_rounded,
            size: 36,
            color: AppColors.accentRed,
          ),
          const SizedBox(height: 10),
          Text(
            'Öğrenciler yüklenemedi',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            message,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 14),
          TextButton(onPressed: onRetry, child: const Text('Tekrar Dene')),
        ],
      ),
    );
  }
}

// ── "Öğrenci Ekle" bottom sheet ─────────────────────────────────────────────

class _AddStudentSheet extends StatefulWidget {
  const _AddStudentSheet({required this.teacherUserId});

  final String teacherUserId;

  @override
  State<_AddStudentSheet> createState() => _AddStudentSheetState();
}

class _AddStudentSheetState extends State<_AddStudentSheet> {
  final _manualFormKey = GlobalKey<FormState>();
  final _inviteFormKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _gradeController = TextEditingController();
  final _goalController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _subjectController = TextEditingController(text: 'Matematik');
  final _targetController = TextEditingController(text: 'Temel');
  final _levelNotesController = TextEditingController();
  final _inviteEmailController = TextEditingController();
  final _inviteMessageController = TextEditingController();

  int _selectedTab = 0;
  String _inviteRole = 'Öğrenci';

  @override
  void dispose() {
    _nameController.dispose();
    _gradeController.dispose();
    _goalController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _subjectController.dispose();
    _targetController.dispose();
    _levelNotesController.dispose();
    _inviteEmailController.dispose();
    _inviteMessageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final isInvite = _selectedTab == 1;

    // Durum cubugunun altindan ekranin tamamini kaplar; ust koseleri yuvarlatilir.
    return SizedBox(
      height: media.size.height - media.padding.top,
      child: ClipRRect(
        borderRadius: const BorderRadius.vertical(top: Radius.circular(28)),
        child: Scaffold(
          backgroundColor: AppColors.background,
          // Scaffold klavye acilinca govdeyi kucultur, CTA cubugunu klavyenin
          // ustunde tutar.
          body: Column(
            children: <Widget>[
              _PremiumSheetHeader(
                isInvite: isInvite,
                onClose: () => Navigator.of(context).maybePop(),
              ),
              const SizedBox(height: 18),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: _StudentAddTabs(
                  selectedIndex: _selectedTab,
                  onChanged: (index) => setState(() => _selectedTab = index),
                ),
              ),
              const SizedBox(height: 18),
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 180),
                    switchInCurve: Curves.easeOut,
                    switchOutCurve: Curves.easeIn,
                    child: isInvite
                        ? _InviteStudentForm(
                            key: const ValueKey<String>('invite'),
                            formKey: _inviteFormKey,
                            role: _inviteRole,
                            onRoleChanged: (value) => setState(
                              () => _inviteRole = value ?? _inviteRole,
                            ),
                            emailController: _inviteEmailController,
                            messageController: _inviteMessageController,
                          )
                        : _ManualAddForm(
                            key: const ValueKey<String>('manual'),
                            formKey: _manualFormKey,
                            nameController: _nameController,
                            gradeController: _gradeController,
                            emailController: _emailController,
                            phoneController: _phoneController,
                            subjectController: _subjectController,
                            targetController: _targetController,
                            goalController: _goalController,
                            levelNotesController: _levelNotesController,
                          ),
                  ),
                ),
              ),
            ],
          ),
          bottomNavigationBar: _SubmitBar(
            label: isInvite ? 'Davet Gönder' : 'Öğrenciyi Kaydet',
            icon: isInvite ? Icons.send_rounded : Icons.check_rounded,
            onPressed: isInvite ? _submitInvite : _submitManual,
          ),
        ),
      ),
    );
  }

  void _submitManual() {
    if (!(_manualFormKey.currentState?.validate() ?? false)) return;

    Navigator.of(context).pop(
      StudentProfile(
        id: '',
        fullName: _nameController.text.trim(),
        gradeLevel: _gradeController.text.trim(),
        origin: 'TeacherManaged',
        teacherUserId: widget.teacherUserId,
        contactEmail: _emailController.text.trim().isEmpty
            ? null
            : _emailController.text.trim(),
        contactPhone: _phoneController.text.trim().isEmpty
            ? null
            : _phoneController.text.trim(),
        goalSummary: _goalController.text.trim().isEmpty
            ? null
            : _goalController.text.trim(),
        levelNotes: _levelNotesController.text.trim().isEmpty
            ? null
            : _levelNotesController.text.trim(),
        subjects: <StudentSubjectTarget>[
          StudentSubjectTarget(
            subject: _subjectController.text.trim(),
            targetLevel: _targetController.text.trim().isEmpty
                ? 'Temel'
                : _targetController.text.trim(),
          ),
        ],
      ),
    );
  }

  void _submitInvite() {
    if (!(_inviteFormKey.currentState?.validate() ?? false)) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('${_inviteRole.toLowerCase()} daveti hazırlandı.'),
        behavior: SnackBarBehavior.floating,
      ),
    );
    Navigator.of(context).pop();
  }

  String? _validateInviteEmail(String? value) {
    if (value == null || value.trim().isEmpty) return 'E-posta zorunlu.';
    return _StudentsPageState._validateEmail(value);
  }
}

// ── Sheet alt bileşenleri ────────────────────────────────────────────────────

/// Tam ekran "Öğrenci Ekle" formunun premium başlığı: primary gradient zemin,
/// sağ üstte kapatma (X) butonu, ikon rozeti + başlık/alt başlık.
class _PremiumSheetHeader extends StatelessWidget {
  const _PremiumSheetHeader({required this.isInvite, required this.onClose});

  final bool isInvite;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(20, 14, 14, 22),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[AppColors.primary, AppColors.primaryDark],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Align(
            alignment: Alignment.centerRight,
            child: Semantics(
              button: true,
              label: 'Kapat',
              child: Material(
                color: Colors.white.withValues(alpha: 0.16),
                shape: const CircleBorder(),
                child: InkWell(
                  customBorder: const CircleBorder(),
                  onTap: onClose,
                  child: const Padding(
                    padding: EdgeInsets.all(8),
                    child: Icon(
                      Icons.close_rounded,
                      color: Colors.white,
                      size: 22,
                    ),
                  ),
                ),
              ),
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: <Widget>[
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.16),
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Icon(
                  isInvite
                      ? Icons.send_rounded
                      : Icons.person_add_alt_1_rounded,
                  color: Colors.white,
                  size: 26,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      isInvite ? 'Davet gönder' : 'Yeni öğrenci ekle',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      isInvite
                          ? 'Öğrenci veya veliyi uygulamaya davet et.'
                          : 'Öğrencinin temel bilgilerini hemen kaydet.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Colors.white.withValues(alpha: 0.72),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Tam ekran formun altina sabitlenen birincil aksiyon cubugu (premium CTA).
class _SubmitBar extends StatelessWidget {
  const _SubmitBar({
    required this.label,
    required this.icon,
    required this.onPressed,
  });

  final String label;
  final IconData icon;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.divider)),
        boxShadow: AppShadows.soft,
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
          child: SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: onPressed,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                minimumSize: const Size.fromHeight(52),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(14),
                ),
                textStyle: Theme.of(
                  context,
                ).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w800),
              ),
              icon: Icon(icon, size: 20),
              label: Text(label),
            ),
          ),
        ),
      ),
    );
  }
}

class _StudentAddTabs extends StatelessWidget {
  const _StudentAddTabs({required this.selectedIndex, required this.onChanged});

  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    const labels = <String>['Manuel Ekle', 'Davet Gönder'];
    return Container(
      height: 42,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.tabBackground,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: List<Widget>.generate(labels.length, (index) {
          final selected = selectedIndex == index;
          return Expanded(
            child: GestureDetector(
              onTap: () => onChanged(index),
              behavior: HitTestBehavior.opaque,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary : Colors.transparent,
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Text(
                  labels[index],
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: selected ? Colors.white : AppColors.textSecondary,
                    fontWeight: selected ? FontWeight.w800 : FontWeight.w600,
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

class _ManualAddForm extends StatelessWidget {
  const _ManualAddForm({
    super.key,
    required this.formKey,
    required this.nameController,
    required this.gradeController,
    required this.emailController,
    required this.phoneController,
    required this.subjectController,
    required this.targetController,
    required this.goalController,
    required this.levelNotesController,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController nameController;
  final TextEditingController gradeController;
  final TextEditingController emailController;
  final TextEditingController phoneController;
  final TextEditingController subjectController;
  final TextEditingController targetController;
  final TextEditingController goalController;
  final TextEditingController levelNotesController;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          AppTextField(
            controller: nameController,
            labelText: 'Öğrenci adı soyadı',
            textCapitalization: TextCapitalization.words,
            validator: _StudentsPageState._validateStudentName,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: gradeController,
            labelText: 'Sınıf / seviye',
            hintText: '9. Sınıf',
            validator: _StudentsPageState._validateGradeLevel,
          ),
          const SizedBox(height: 12),
          Row(
            children: <Widget>[
              Expanded(
                child: AppTextField(
                  controller: subjectController,
                  labelText: 'Ders',
                  validator: _StudentsPageState._validateSubject,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: AppTextField(
                  controller: targetController,
                  labelText: 'Hedef seviye',
                  hintText: 'Temel',
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: emailController,
            labelText: 'İletişim e-postası',
            keyboardType: TextInputType.emailAddress,
            validator: _StudentsPageState._validateEmail,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: phoneController,
            labelText: 'İletişim telefonu',
            keyboardType: TextInputType.phone,
            validator: _StudentsPageState._validatePhone,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: goalController,
            labelText: 'Hedef',
            hintText: 'LGS matematik netini 15+ yapmak',
            maxLength: 120,
            validator: _StudentsPageState._validateGoal,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: levelNotesController,
            labelText: 'Seviye notu',
            hintText: 'Temel konularda tekrar gerekiyor',
            minLines: 2,
            maxLines: 3,
          ),
        ],
      ),
    );
  }
}

class _InviteStudentForm extends StatelessWidget {
  const _InviteStudentForm({
    super.key,
    required this.formKey,
    required this.role,
    required this.onRoleChanged,
    required this.emailController,
    required this.messageController,
  });

  final GlobalKey<FormState> formKey;
  final String role;
  final ValueChanged<String?> onRoleChanged;
  final TextEditingController emailController;
  final TextEditingController messageController;

  @override
  Widget build(BuildContext context) {
    final sheetState = context.findAncestorStateOfType<_AddStudentSheetState>();
    return Form(
      key: formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const AppFieldLabel(text: 'Davet tipi'),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            initialValue: role,
            decoration: appInputDecoration('Davet tipi seç'),
            items: const <DropdownMenuItem<String>>[
              DropdownMenuItem<String>(
                value: 'Öğrenci',
                child: Text('Öğrenci daveti'),
              ),
              DropdownMenuItem<String>(
                value: 'Veli',
                child: Text('Veli daveti'),
              ),
            ],
            onChanged: onRoleChanged,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: emailController,
            labelText: 'E-posta',
            keyboardType: TextInputType.emailAddress,
            validator: sheetState?._validateInviteEmail,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: messageController,
            labelText: 'Davet notu',
            hintText: 'Merhaba, ders takibi için EğitimÜssü davetin hazır.',
            minLines: 3,
            maxLines: 4,
            maxLength: 180,
          ),
        ],
      ),
    );
  }
}

// ── Sayfa bileşenleri ────────────────────────────────────────────────────────

class _SearchField extends StatelessWidget {
  const _SearchField({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      onChanged: onChanged,
      decoration: InputDecoration(
        hintText: 'Öğrenci ara',
        prefixIcon: const Icon(Icons.search_rounded),
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 16,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.skyBorder),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.skyBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.primary),
        ),
      ),
    );
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({required this.student, required this.accent, this.onTap});

  final StudentProfile student;
  final Color accent;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final isActive = student.isActive;

    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: AppColors.skyBorder),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          children: <Widget>[
            _StudentAvatar(name: student.fullName, accent: accent),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    student.fullName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    student.gradeLevel,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                      fontWeight: FontWeight.w400,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 5,
                  ),
                  decoration: BoxDecoration(
                    color:
                        (isActive
                                ? AppColors.accentGreen
                                : AppColors.textSecondary)
                            .withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    isActive ? 'Aktif' : 'Pasif',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: isActive
                          ? AppColors.accentGreen
                          : AppColors.textSecondary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(width: 8),
            Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary.withValues(alpha: 0.88),
            ),
          ],
        ),
      ),
    );
  }
}

class _StudentAvatar extends StatelessWidget {
  const _StudentAvatar({required this.name, required this.accent});

  final String name;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 54,
      height: 54,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.9),
            accent.withValues(alpha: 0.6),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }

  String _initials(String value) {
    final parts = value.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty) return '??';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }
}

// ── Alt navigasyon ───────────────────────────────────────────────────────────
