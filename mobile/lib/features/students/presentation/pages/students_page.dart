import 'package:egitim_ussu_mobile/features/students/data/student_demo_data.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class StudentsPage extends StatefulWidget {
  const StudentsPage({super.key});

  @override
  State<StudentsPage> createState() => _StudentsPageState();
}

class _StudentsPageState extends State<StudentsPage> {
  static const _navy = Color(0xFF082B4F);
  static const _skyBorder = Color(0xFFD7E7F8);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _slate = Color(0xFF6B7A90);
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _divider = Color(0xFFE5EEF7);

  final TextEditingController _searchController = TextEditingController();
  final List<StudentProfile> _students = StudentDemoData.students;
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const teacherName = 'Demo ogretmen';
    final filteredStudents = _students.where((student) {
      final q = _query.trim().toLowerCase();
      if (q.isEmpty) {
        return true;
      }
      return student.fullName.toLowerCase().contains(q) ||
          student.gradeLevel.toLowerCase().contains(q);
    }).toList();

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 116),
          children: <Widget>[
            _PageHeader(teacherName: teacherName),
            const SizedBox(height: 20),
            _SearchField(
              controller: _searchController,
              onChanged: (value) => setState(() => _query = value),
            ),
            const SizedBox(height: 18),
            Row(
              children: <Widget>[
                Text(
                  'Ogrenciler',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: _text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const Spacer(),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 8,
                  ),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(999),
                    border: Border.all(color: _skyBorder),
                  ),
                  child: Text(
                    '${filteredStudents.length} kayit',
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: _navy,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            if (filteredStudents.isEmpty)
              const Padding(
                padding: EdgeInsets.only(top: 24),
                child: EmptyStateView(
                  title: 'Ogrenci bulunamadi',
                  subtitle: 'Arama sonucuna uygun ogrenci kaydi gorunmuyor.',
                ),
              )
            else
              ...List<Widget>.generate(filteredStudents.length, (index) {
                final student = filteredStudents[index];
                return Padding(
                  padding: EdgeInsets.only(
                    bottom: index == filteredStudents.length - 1 ? 0 : 12,
                  ),
                  child: _StudentCard(
                    student: student,
                    lastLessonLabel: _lastLessonLabel(index),
                    accent: _accentForIndex(index),
                    onTap: () => context.push('/students/${student.id}'),
                  ),
                );
              }),
          ],
        ),
      ),
      bottomNavigationBar: _StudentsBottomNav(
        onHomeTap: () => context.go('/dashboard'),
        onLessonsTap: () => context.go('/lesson-sessions'),
        onCalendarTap: () => context.go('/scheduling'),
        onMoreTap: () => context.go('/more'),
        onFinanceTap: () => context.go('/payments'),
      ),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: _navy,
        foregroundColor: Colors.white,
        onPressed: () => _showAddStudentSheet(context),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Yeni Ogrenci Ekle'),
      ),
    );
  }

  Color _accentForIndex(int index) {
    const colors = <Color>[
      _emerald,
      Color(0xFF3D8BFF),
      _amber,
      Color(0xFF20A4A9),
    ];
    return colors[index % colors.length];
  }

  String _lastLessonLabel(int index) {
    const labels = <String>[
      'Bugun 15:30',
      'Dun 18:00',
      '2 gun once',
      'Bugun 11:00',
    ];
    return labels[index % labels.length];
  }

  Future<void> _showAddStudentSheet(BuildContext context) async {
    final created = await showModalBottomSheet<StudentProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) => const FractionallySizedBox(
        heightFactor: 0.94,
        child: _AddStudentSheet(),
      ),
    );

    if (created == null) {
      return;
    }

    setState(() => _students.add(created));
    if (!context.mounted) {
      return;
    }
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Ogrenci eklendi.')));
  }

  static String? _validateEmail(String? value) {
    if (value == null || value.trim().isEmpty) {
      return null;
    }
    final email = value.trim();
    if (!email.contains('@') || !email.contains('.')) {
      return 'Gecerli bir e-posta gir.';
    }
    return null;
  }

  static String? _validateGoal(String? value) {
    if (value == null || value.trim().isEmpty) {
      return null;
    }
    if (value.trim().length < 5) {
      return 'Hedef bilgisi daha acik olmali.';
    }
    return null;
  }

  static String? _validateGradeLevel(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Seviye bilgisi zorunlu.';
    }
    if (value.trim().length < 2) {
      return 'Seviye bilgisi cok kisa.';
    }
    return null;
  }

  static String? _validatePhone(String? value) {
    if (value == null || value.trim().isEmpty) {
      return null;
    }
    final digitsOnly = value.replaceAll(RegExp(r'[^0-9]'), '');
    if (digitsOnly.length < 10) {
      return 'Telefon en az 10 rakam olmali.';
    }
    return null;
  }

  static String? _validateStudentName(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Ogrenci adi zorunlu.';
    }
    if (value.trim().split(RegExp(r'\s+')).length < 2) {
      return 'Ad ve soyad birlikte girilmeli.';
    }
    return null;
  }

  static String? _validateSubject(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Ders bilgisi zorunlu.';
    }
    if (value.trim().length < 3) {
      return 'Ders bilgisi cok kisa.';
    }
    return null;
  }
}

class _AddStudentSheet extends StatefulWidget {
  const _AddStudentSheet();

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
  final _targetController = TextEditingController(text: 'Faz 1');
  final _levelNotesController = TextEditingController();
  final _inviteEmailController = TextEditingController();
  final _inviteMessageController = TextEditingController();

  int _selectedTab = 0;
  String _inviteRole = 'Ogrenci';

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
    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 14,
          bottom: MediaQuery.of(context).viewInsets.bottom + 20,
        ),
        child: LayoutBuilder(
          builder: (context, constraints) {
            return SingleChildScrollView(
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: Column(
                  children: <Widget>[
                    Container(
                      width: 48,
                      height: 5,
                      decoration: BoxDecoration(
                        color: _StudentsPageState._divider,
                        borderRadius: BorderRadius.circular(999),
                      ),
                    ),
                    const SizedBox(height: 16),
                    _SheetHeader(selectedTab: _selectedTab),
                    const SizedBox(height: 16),
                    _StudentAddTabs(
                      selectedIndex: _selectedTab,
                      onChanged: (index) =>
                          setState(() => _selectedTab = index),
                    ),
                    const SizedBox(height: 18),
                    AnimatedSwitcher(
                      duration: const Duration(milliseconds: 180),
                      switchInCurve: Curves.easeOut,
                      switchOutCurve: Curves.easeIn,
                      child: _selectedTab == 0
                          ? _ManualAddForm(
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
                              onSubmit: _submitManual,
                            )
                          : _InviteStudentForm(
                              key: const ValueKey<String>('invite'),
                              formKey: _inviteFormKey,
                              role: _inviteRole,
                              onRoleChanged: (value) => setState(
                                () => _inviteRole = value ?? _inviteRole,
                              ),
                              emailController: _inviteEmailController,
                              messageController: _inviteMessageController,
                              onSubmit: _submitInvite,
                            ),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  void _submitManual() {
    if (!(_manualFormKey.currentState?.validate() ?? false)) {
      return;
    }

    Navigator.of(context).pop(
      StudentProfile(
        id: 'student-${DateTime.now().millisecondsSinceEpoch}',
        fullName: _nameController.text.trim(),
        gradeLevel: _gradeController.text.trim(),
        origin: 'Manuel kayit',
        teacherUserId: 'demo-teacher',
        contactEmail: _emailController.text.trim(),
        contactPhone: _phoneController.text.trim(),
        goalSummary: _goalController.text.trim(),
        levelNotes: _levelNotesController.text.trim(),
        subjects: <StudentSubjectTarget>[
          StudentSubjectTarget(
            subject: _subjectController.text.trim(),
            targetLevel: _targetController.text.trim().isEmpty
                ? 'Faz 1'
                : _targetController.text.trim(),
          ),
        ],
      ),
    );
  }

  void _submitInvite() {
    if (!(_inviteFormKey.currentState?.validate() ?? false)) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('${_inviteRole.toLowerCase()} daveti hazirlandi.'),
      ),
    );
    Navigator.of(context).pop();
  }

  String? _validateInviteEmail(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'E-posta zorunlu.';
    }
    return _StudentsPageState._validateEmail(value);
  }
}

class _SheetHeader extends StatelessWidget {
  const _SheetHeader({required this.selectedTab});

  final int selectedTab;

  @override
  Widget build(BuildContext context) {
    final isInvite = selectedTab == 1;
    return Row(
      children: <Widget>[
        Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: _StudentsPageState._navy,
            borderRadius: BorderRadius.circular(16),
          ),
          child: Icon(
            isInvite ? Icons.send_rounded : Icons.person_add_alt_1_rounded,
            color: Colors.white,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                isInvite ? 'Davet gonder' : 'Yeni ogrenci ekle',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: _StudentsPageState._text,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                isInvite
                    ? 'Ogrenci veya veliyi uygulamaya davet et.'
                    : 'Ogrencinin temel bilgilerini hemen kaydet.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: _StudentsPageState._slate,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _StudentAddTabs extends StatelessWidget {
  const _StudentAddTabs({required this.selectedIndex, required this.onChanged});

  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    const labels = <String>['Manuel Ekle', 'Davet Gonder'];
    return Container(
      height: 42,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: const Color(0xFFF3F5F8),
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
                  color: selected
                      ? _StudentsPageState._navy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Text(
                  labels[index],
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: selected ? Colors.white : _StudentsPageState._slate,
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
    required this.onSubmit,
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
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          AppTextField(
            controller: nameController,
            labelText: 'Ogrenci adi soyadi',
            textCapitalization: TextCapitalization.words,
            validator: _StudentsPageState._validateStudentName,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: gradeController,
            labelText: 'Sinif / seviye',
            hintText: '9. Sinif',
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
                  hintText: 'Faz 1',
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: emailController,
            labelText: 'Iletisim e-postasi',
            keyboardType: TextInputType.emailAddress,
            validator: _StudentsPageState._validateEmail,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: phoneController,
            labelText: 'Iletisim telefonu',
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
          const SizedBox(height: 18),
          AppPrimaryButton(label: 'Ogrenciyi Kaydet', onPressed: onSubmit),
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
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final String role;
  final ValueChanged<String?> onRoleChanged;
  final TextEditingController emailController;
  final TextEditingController messageController;
  final VoidCallback onSubmit;

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
            decoration: appInputDecoration('Davet tipi sec'),
            items: const <DropdownMenuItem<String>>[
              DropdownMenuItem<String>(
                value: 'Ogrenci',
                child: Text('Ogrenci daveti'),
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
            hintText: 'Merhaba, ders takibi icin EgitimUssu davetin hazir.',
            minLines: 3,
            maxLines: 4,
            maxLength: 180,
          ),
          const SizedBox(height: 18),
          AppPrimaryButton(label: 'Davet Gonder', onPressed: onSubmit),
        ],
      ),
    );
  }
}

class _PageHeader extends StatelessWidget {
  const _PageHeader({required this.teacherName});

  final String teacherName;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            teacherName,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: _StudentsPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const _HeaderIconButton(),
      ],
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton();

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: <Widget>[
        Container(
          width: 46,
          height: 46,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: _StudentsPageState._skyBorder),
          ),
          child: const Icon(
            Icons.notifications_none_rounded,
            color: _StudentsPageState._text,
          ),
        ),
        Positioned(
          top: -3,
          right: -3,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: _StudentsPageState._emerald,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              '2',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

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
        hintText: 'Ogrenci ara',
        prefixIcon: const Icon(Icons.search_rounded),
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 16,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: _StudentsPageState._skyBorder),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: _StudentsPageState._skyBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: _StudentsPageState._navy),
        ),
      ),
    );
  }
}

class _StudentCard extends StatelessWidget {
  const _StudentCard({
    required this.student,
    required this.lastLessonLabel,
    required this.accent,
    this.onTap,
  });

  final StudentProfile student;
  final String lastLessonLabel;
  final Color accent;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: _StudentsPageState._skyBorder),
          boxShadow: const <BoxShadow>[
            BoxShadow(
              color: Color(0x12082B4F),
              blurRadius: 24,
              offset: Offset(0, 10),
            ),
          ],
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
                      color: _StudentsPageState._text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    student.gradeLevel,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _StudentsPageState._slate,
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
                Text(
                  'Son ders',
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: _StudentsPageState._slate,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  lastLessonLabel,
                  textAlign: TextAlign.end,
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: _StudentsPageState._navy,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
            const SizedBox(width: 10),
            Icon(
              Icons.chevron_right_rounded,
              color: _StudentsPageState._slate.withValues(alpha: 0.88),
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
    if (parts.isEmpty) {
      return '??';
    }
    if (parts.length == 1) {
      return parts.first.substring(0, 1).toUpperCase();
    }
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }
}

class _StudentsBottomNav extends StatelessWidget {
  const _StudentsBottomNav({
    this.onHomeTap,
    this.onLessonsTap,
    this.onCalendarTap,
    this.onMoreTap,
    this.onFinanceTap,
  });

  final VoidCallback? onHomeTap;
  final VoidCallback? onLessonsTap;
  final VoidCallback? onCalendarTap;
  final VoidCallback? onMoreTap;
  final VoidCallback? onFinanceTap;

  @override
  Widget build(BuildContext context) {
    final items = <_BottomNavItem>[
      _BottomNavItem(Icons.home_rounded, 'Ana sayfa', false, onHomeTap),
      _BottomNavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      const _BottomNavItem(Icons.groups_rounded, 'Ogrenciler', true),
      _BottomNavItem(
        Icons.calendar_month_rounded,
        'Takvim',
        false,
        onCalendarTap,
      ),
      _BottomNavItem(
        Icons.account_balance_wallet_rounded,
        'Finans',
        false,
        onFinanceTap,
      ),
      _BottomNavItem(Icons.widgets_rounded, 'Diger', false, onMoreTap),
    ];

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: _StudentsPageState._divider)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: items
            .map(
              (item) => Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(18),
                  onTap: item.onTap,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Icon(
                          item.icon,
                          color: item.selected
                              ? _StudentsPageState._navy
                              : _StudentsPageState._slate,
                        ),
                        const SizedBox(height: 4),
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            item.label,
                            maxLines: 1,
                            style: Theme.of(context).textTheme.labelMedium
                                ?.copyWith(
                                  color: item.selected
                                      ? _StudentsPageState._navy
                                      : _StudentsPageState._slate,
                                  fontWeight: item.selected
                                      ? FontWeight.w800
                                      : FontWeight.w600,
                                  fontSize: 11,
                                ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            )
            .toList(),
      ),
    );
  }
}

class _BottomNavItem {
  const _BottomNavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}
