import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';

class PaymentFormPage extends StatefulWidget {
  const PaymentFormPage({super.key});

  @override
  State<PaymentFormPage> createState() => _PaymentFormPageState();
}

class _PaymentFormPageState extends State<PaymentFormPage> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _amountController = TextEditingController(text: '750');
  final _collectedController = TextEditingController(text: '0');
  final _dueDateController = TextEditingController();
  final _notesController = TextEditingController();

  late final PaymentsCubit _paymentsCubit;
  late final StudentsCubit _studentsCubit;

  DateTime _selectedDueDate = DateTime.now().add(const Duration(days: 7));
  StudentProfile? _selectedProfile;
  String? _selectedSubject;
  String _currency = 'TRY';
  String _paymentMethod = 'Nakit';

  @override
  void initState() {
    super.initState();
    _paymentsCubit = PaymentsCubit.create();
    _studentsCubit = StudentsCubit.create();
    _dueDateController.text = _formatDate(_selectedDueDate);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final userId = context.read<AuthCubit>().state.session?.userId;
    if (userId != null &&
        _studentsCubit.state.students.isEmpty &&
        !_studentsCubit.state.isLoading) {
      _studentsCubit.load(userId);
    }
  }

  @override
  void dispose() {
    _paymentsCubit.close();
    _studentsCubit.close();
    _descriptionController.dispose();
    _amountController.dispose();
    _collectedController.dispose();
    _dueDateController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: [
        BlocProvider<PaymentsCubit>.value(value: _paymentsCubit),
        BlocProvider<StudentsCubit>.value(value: _studentsCubit),
      ],
      child: BlocListener<StudentsCubit, StudentsState>(
        listener: (context, state) {
          if (_selectedProfile == null && state.students.isNotEmpty) {
            setState(() {
              _selectedProfile = state.students.first;
              _selectedSubject =
                  _selectedProfile!.subjects.firstOrNull?.subject;
              _syncDescription();
            });
          }
        },
        child: Scaffold(
          backgroundColor: AppColors.background,
          body: SafeArea(
            child: BlocConsumer<PaymentsCubit, PaymentsState>(
              listener: (context, state) {
                if (state.successMessage != null) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text(state.successMessage!),
                      backgroundColor: AppColors.accentGreen,
                      behavior: SnackBarBehavior.floating,
                    ),
                  );
                  if (context.mounted && context.canPop()) context.pop();
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
              builder: (context, payState) {
                return Column(
                  children: <Widget>[
                    _TopBar(
                      onBack: () => context.canPop()
                          ? context.pop()
                          : context.go('/payments'),
                    ),
                    Expanded(
                      child: BlocBuilder<StudentsCubit, StudentsState>(
                        builder: (context, studState) {
                          return ListView(
                            padding: const EdgeInsets.fromLTRB(16, 10, 16, 112),
                            children: <Widget>[
                              Form(
                                key: _formKey,
                                child: Column(
                                  children: <Widget>[
                                    _StudentSection(
                                      isLoading: studState.isLoading,
                                      students: studState.students,
                                      selectedProfile: _selectedProfile,
                                      selectedSubject: _selectedSubject,
                                      onStudentChanged: _onStudentChanged,
                                      onSubjectChanged: _onSubjectChanged,
                                    ),
                                    const SizedBox(height: 14),
                                    AppTextField(
                                      controller: _descriptionController,
                                      labelText: 'Açıklama',
                                      hintText: 'Ödeme açıklaması',
                                      textCapitalization:
                                          TextCapitalization.sentences,
                                      validator: _required,
                                    ),
                                    const SizedBox(height: 14),
                                    Row(
                                      children: <Widget>[
                                        Expanded(
                                          child: AppTextField(
                                            controller: _amountController,
                                            labelText: 'Tutar',
                                            hintText: 'Beklenen tutar',
                                            keyboardType: TextInputType.number,
                                            validator: _validateAmount,
                                          ),
                                        ),
                                        const SizedBox(width: 10),
                                        Expanded(
                                          child: AppTextField(
                                            controller: _collectedController,
                                            labelText: 'Tahsil edildi',
                                            hintText: 'Alınan tutar',
                                            keyboardType: TextInputType.number,
                                            validator: _validateAmount,
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 14),
                                    Row(
                                      children: <Widget>[
                                        Expanded(
                                          child: AppTextField(
                                            controller: _dueDateController,
                                            labelText: 'Son ödeme tarihi',
                                            hintText: 'Tarih seç',
                                            readOnly: true,
                                            onTap: _pickDueDate,
                                            suffixIcon: const Icon(
                                              Icons.event_rounded,
                                            ),
                                            validator: _required,
                                          ),
                                        ),
                                        const SizedBox(width: 10),
                                        Expanded(
                                          child: _CurrencyField(
                                            value: _currency,
                                            onChanged: (v) {
                                              if (v != null) {
                                                setState(() => _currency = v);
                                              }
                                            },
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 14),
                                    _PaymentMethodField(
                                      value: _paymentMethod,
                                      onChanged: (v) {
                                        if (v != null) {
                                          setState(() => _paymentMethod = v);
                                        }
                                      },
                                    ),
                                    const SizedBox(height: 14),
                                    AppTextField(
                                      controller: _notesController,
                                      labelText: 'Not',
                                      hintText: 'Ödeme notu (isteğe bağlı)',
                                      minLines: 3,
                                      maxLines: 5,
                                      maxLength: 160,
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          );
                        },
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
          bottomNavigationBar: BlocBuilder<PaymentsCubit, PaymentsState>(
            builder: (context, state) {
              return Container(
                decoration: const BoxDecoration(
                  color: Colors.white,
                  border: Border(top: BorderSide(color: AppColors.divider)),
                ),
                padding: EdgeInsets.fromLTRB(
                  16,
                  12,
                  16,
                  MediaQuery.of(context).padding.bottom + 12,
                ),
                child: AppPrimaryButton(
                  label: 'Ödemeyi Kaydet',
                  isLoading: state.isSaving,
                  onPressed: _save,
                ),
              );
            },
          ),
        ),
      ),
    );
  }

  void _onStudentChanged(StudentProfile? profile) {
    if (profile == null) return;
    setState(() {
      _selectedProfile = profile;
      _selectedSubject = profile.subjects.firstOrNull?.subject;
      _syncDescription();
    });
  }

  void _onSubjectChanged(String? subject) {
    if (subject == null) return;
    setState(() {
      _selectedSubject = subject;
      _syncDescription();
    });
  }

  void _syncDescription() {
    if (_selectedProfile == null) return;
    final subject =
        _selectedSubject ?? _selectedProfile!.subjects.firstOrNull?.subject;
    final name = _selectedProfile!.fullName;
    _descriptionController.text = subject != null
        ? '$subject dersi — $name'
        : '$name dersi';
  }

  Future<void> _pickDueDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDueDate,
      firstDate: DateTime.now().subtract(const Duration(days: 30)),
      lastDate: DateTime(DateTime.now().year + 2),
    );
    if (picked == null) return;
    setState(() {
      _selectedDueDate = picked;
      _dueDateController.text = _formatDate(picked);
    });
  }

  void _save() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_selectedProfile == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Lütfen bir öğrenci seçin.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }
    final teacherUserId = context.read<AuthCubit>().state.session?.userId ?? '';
    final expected = double.parse(_amountController.text.trim());
    final collected = double.parse(_collectedController.text.trim());
    final outstanding = (expected - collected)
        .clamp(0, double.infinity)
        .toDouble();

    _paymentsCubit.create(
      PaymentRecord(
        id: '',
        teacherUserId: teacherUserId,
        studentId: _selectedProfile!.id,
        description: _descriptionController.text.trim(),
        currency: _currency,
        expectedAmount: expected,
        collectedAmount: collected,
        outstandingAmount: outstanding,
        status: collected >= expected
            ? 'Paid'
            : collected > 0
            ? 'PartiallyPaid'
            : 'Pending',
        dueDateUtc: _selectedDueDate.toUtc(),
        collectedOnUtc: collected > 0 ? DateTime.now().toUtc() : null,
        notes: _buildNotes(),
      ),
    );
  }

  String _buildNotes() {
    final note = _notesController.text.trim();
    final method = 'Ödeme yöntemi: $_paymentMethod';
    return note.isEmpty ? method : '$method\n$note';
  }

  static String? _required(String? value) {
    if (value == null || value.trim().isEmpty) return 'Bu alan zorunlu.';
    return null;
  }

  static String? _validateAmount(String? value) {
    final req = _required(value);
    if (req != null) return req;
    final amount = double.tryParse(value!.trim());
    if (amount == null || amount < 0) return 'Geçerli bir tutar girin.';
    return null;
  }

  static String _formatDate(DateTime date) {
    const months = <String>[
      'Ocak',
      'Şubat',
      'Mart',
      'Nisan',
      'Mayıs',
      'Haziran',
      'Temmuz',
      'Ağustos',
      'Eylül',
      'Ekim',
      'Kasım',
      'Aralık',
    ];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }
}

// ── Öğrenci + ders seçimi ────────────────────────────────────────────────────

class _StudentSection extends StatelessWidget {
  const _StudentSection({
    required this.isLoading,
    required this.students,
    required this.selectedProfile,
    required this.selectedSubject,
    required this.onStudentChanged,
    required this.onSubjectChanged,
  });

  final bool isLoading;
  final List<StudentProfile> students;
  final StudentProfile? selectedProfile;
  final String? selectedSubject;
  final ValueChanged<StudentProfile?> onStudentChanged;
  final ValueChanged<String?> onSubjectChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const AppFieldLabel(text: 'Öğrenci'),
        const SizedBox(height: 8),
        if (isLoading)
          _StudentShimmer()
        else if (students.isEmpty)
          _NoStudentsHint()
        else
          DropdownButtonFormField<StudentProfile>(
            key: ValueKey<String?>('student-${selectedProfile?.id}'),
            initialValue: selectedProfile,
            decoration: appInputDecoration('Öğrenci seçin'),
            items: students
                .map(
                  (s) => DropdownMenuItem<StudentProfile>(
                    value: s,
                    child: Text(s.fullName, overflow: TextOverflow.ellipsis),
                  ),
                )
                .toList(),
            onChanged: onStudentChanged,
            validator: (v) => v == null ? 'Öğrenci seçimi zorunlu.' : null,
          ),
        if (selectedProfile?.subjects.isNotEmpty == true) ...<Widget>[
          const SizedBox(height: 14),
          const AppFieldLabel(text: 'Ders'),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            key: ValueKey<String>('subj-${selectedProfile!.id}'),
            initialValue: selectedSubject,
            decoration: appInputDecoration('Ders seçin'),
            items: selectedProfile!.subjects
                .map(
                  (s) => DropdownMenuItem<String>(
                    value: s.subject,
                    child: Text(s.subject),
                  ),
                )
                .toList(),
            onChanged: onSubjectChanged,
          ),
        ],
      ],
    );
  }
}

class _StudentShimmer extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Shimmer.fromColors(
      baseColor: const Color(0xFFEEF4FB),
      highlightColor: Colors.white,
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
        ),
      ),
    );
  }
}

class _NoStudentsHint extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.skyBorder),
      ),
      child: Row(
        children: <Widget>[
          const Icon(
            Icons.info_outline_rounded,
            size: 18,
            color: AppColors.textSecondary,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              'Kayıtlı öğrenci bulunamadı. Önce öğrenci ekleyin.',
              style: Theme.of(
                context,
              ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
            ),
          ),
        ],
      ),
    );
  }
}

// ── Diğer bileşenler ─────────────────────────────────────────────────────────

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 4, 16, 4),
      child: Row(
        children: <Widget>[
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back_rounded),
            color: AppColors.textPrimary,
          ),
          Expanded(
            child: Text(
              'Ödeme Ekle',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CurrencyField extends StatelessWidget {
  const _CurrencyField({required this.value, required this.onChanged});

  final String value;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const AppFieldLabel(text: 'Para birimi'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: value,
          decoration: appInputDecoration('Para birimi'),
          items: const <DropdownMenuItem<String>>[
            DropdownMenuItem<String>(value: 'TRY', child: Text('TRY')),
            DropdownMenuItem<String>(value: 'USD', child: Text('USD')),
            DropdownMenuItem<String>(value: 'EUR', child: Text('EUR')),
          ],
          onChanged: onChanged,
        ),
      ],
    );
  }
}

class _PaymentMethodField extends StatelessWidget {
  const _PaymentMethodField({required this.value, required this.onChanged});

  final String value;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const AppFieldLabel(text: 'Ödeme yöntemi'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: value,
          decoration: appInputDecoration('Ödeme yöntemi seçin'),
          items: const <DropdownMenuItem<String>>[
            DropdownMenuItem<String>(value: 'Nakit', child: Text('Nakit')),
            DropdownMenuItem<String>(
              value: 'Banka transferi',
              child: Text('Banka transferi'),
            ),
            DropdownMenuItem<String>(
              value: 'Kredi kartı',
              child: Text('Kredi kartı'),
            ),
          ],
          onChanged: onChanged,
        ),
      ],
    );
  }
}
