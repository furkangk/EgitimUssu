import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class PaymentFormPage extends StatefulWidget {
  const PaymentFormPage({super.key});

  @override
  State<PaymentFormPage> createState() => _PaymentFormPageState();
}

class _PaymentFormPageState extends State<PaymentFormPage> {
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _divider = Color(0xFFE5EEF7);

  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _amountController = TextEditingController(text: '750');
  final _collectedController = TextEditingController(text: '0');
  final _dueDateController = TextEditingController();
  final _notesController = TextEditingController();

  DateTime _selectedDueDate = DateTime.now().add(const Duration(days: 7));
  String _selectedStudent = _studentOptions.first;
  late String _selectedLesson;
  String _currency = 'TRY';
  String _paymentMethod = 'Kredi karti';

  static const _studentOptions = <String>[
    'Zeynep Demir',
    'Ali Yilmaz',
    'Merve Kaya',
    'Ece Aydin',
  ];

  static const _lessonsByStudent = <String, List<String>>{
    'Zeynep Demir': <String>['Matematik', 'Geometri'],
    'Ali Yilmaz': <String>['Fizik', 'Matematik'],
    'Merve Kaya': <String>['Geometri', 'Kimya'],
    'Ece Aydin': <String>['Matematik', 'Turkce'],
  };

  @override
  void initState() {
    super.initState();
    _selectedLesson = _lessonsForStudent(_selectedStudent).first;
    _descriptionController.text = 'Matematik dersi';
    _dueDateController.text = _formatDate(_selectedDueDate);
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _amountController.dispose();
    _collectedController.dispose();
    _dueDateController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final teacherUserId = context.select(
      (AuthCubit cubit) => cubit.state.session?.userId.trim().isNotEmpty == true
          ? cubit.state.session!.userId
          : 'mock-teacher-user',
    );

    return BlocProvider<PaymentsCubit>(
      create: (_) => PaymentsCubit.create(),
      child: Scaffold(
        backgroundColor: _background,
        body: SafeArea(
          child: BlocConsumer<PaymentsCubit, PaymentsState>(
            listener: (context, state) {
              if (state.successMessage != null) {
                ScaffoldMessenger.of(
                  context,
                ).showSnackBar(SnackBar(content: Text(state.successMessage!)));
              }
              if (state.errorMessage != null) {
                ScaffoldMessenger.of(
                  context,
                ).showSnackBar(SnackBar(content: Text(state.errorMessage!)));
              }
            },
            builder: (context, state) {
              return Column(
                children: <Widget>[
                  _TopBar(
                    onBack: () {
                      if (context.canPop()) {
                        context.pop();
                      } else {
                        context.go('/dashboard');
                      }
                    },
                  ),
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 112),
                      children: <Widget>[
                        Form(
                          key: _formKey,
                          child: Column(
                            children: <Widget>[
                              _SelectionFields(
                                selectedStudent: _selectedStudent,
                                selectedLesson: _selectedLesson,
                                studentOptions: _studentOptions,
                                lessonOptions: _lessonsForStudent(
                                  _selectedStudent,
                                ),
                                onStudentChanged: (value) {
                                  if (value == null) {
                                    return;
                                  }
                                  setState(() {
                                    _selectedStudent = value;
                                    _selectedLesson = _lessonsForStudent(
                                      value,
                                    ).first;
                                    _descriptionController.text =
                                        '$_selectedLesson dersi';
                                  });
                                },
                                onLessonChanged: (value) {
                                  if (value == null) {
                                    return;
                                  }
                                  setState(() {
                                    _selectedLesson = value;
                                    _descriptionController.text =
                                        '$value dersi';
                                  });
                                },
                              ),
                              const SizedBox(height: 14),
                              AppTextField(
                                controller: _descriptionController,
                                labelText: 'Aciklama',
                                hintText: 'Odeme aciklamasi',
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
                                      labelText: 'Tahsil',
                                      hintText: 'Alinan tutar',
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
                                      labelText: 'Son odeme',
                                      hintText: 'Tarih sec',
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
                                      onChanged: (value) {
                                        if (value == null) {
                                          return;
                                        }
                                        setState(() => _currency = value);
                                      },
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 14),
                              _PaymentMethodField(
                                value: _paymentMethod,
                                onChanged: (value) {
                                  if (value == null) {
                                    return;
                                  }
                                  setState(() => _paymentMethod = value);
                                },
                              ),
                              const SizedBox(height: 14),
                              AppTextField(
                                controller: _notesController,
                                labelText: 'Not',
                                hintText: 'Odeme notu',
                                minLines: 3,
                                maxLines: 5,
                                maxLength: 160,
                              ),
                            ],
                          ),
                        ),
                      ],
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
                border: Border(top: BorderSide(color: _divider)),
              ),
              padding: EdgeInsets.fromLTRB(
                16,
                12,
                16,
                MediaQuery.of(context).padding.bottom + 12,
              ),
              child: AppPrimaryButton(
                label: 'Odemeyi Kaydet',
                isLoading: state.isSaving,
                onPressed: () => _save(context, teacherUserId),
              ),
            );
          },
        ),
      ),
    );
  }

  Future<void> _pickDueDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDueDate,
      firstDate: DateTime.now().subtract(const Duration(days: 30)),
      lastDate: DateTime(DateTime.now().year + 2),
    );
    if (picked == null) {
      return;
    }
    setState(() {
      _selectedDueDate = picked;
      _dueDateController.text = _formatDate(picked);
    });
  }

  void _save(BuildContext context, String teacherUserId) {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final expected = double.parse(_amountController.text.trim());
    final collected = double.parse(_collectedController.text.trim());
    final outstanding = expected - collected;

    context.read<PaymentsCubit>().create(
      PaymentRecord(
        id: '',
        teacherUserId: teacherUserId,
        studentId: _selectedStudent,
        description: _descriptionController.text.trim(),
        currency: _currency,
        expectedAmount: expected,
        collectedAmount: collected,
        outstandingAmount: outstanding < 0 ? 0 : outstanding,
        status: collected >= expected
            ? 'Paid'
            : collected > 0
            ? 'PartiallyPaid'
            : 'Pending',
        dueDateUtc: _selectedDueDate.toUtc(),
        collectedOnUtc: collected > 0 ? DateTime.now().toUtc() : null,
        notes: _combinedNotes(),
      ),
    );
  }

  String _combinedNotes() {
    final note = _notesController.text.trim();
    final methodText = 'Odeme yontemi: $_paymentMethod';
    return note.isEmpty ? methodText : '$methodText\n$note';
  }

  String? _required(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Bu alan zorunlu.';
    }
    return null;
  }

  String? _validateAmount(String? value) {
    final required = _required(value);
    if (required != null) {
      return required;
    }
    final amount = double.tryParse(value!.trim());
    if (amount == null || amount < 0) {
      return 'Gecerli tutar gir.';
    }
    return null;
  }

  List<String> _lessonsForStudent(String student) {
    return _lessonsByStudent[student] ?? const <String>['Matematik'];
  }

  String _formatDate(DateTime date) {
    const months = <String>[
      'Ocak',
      'Subat',
      'Mart',
      'Nisan',
      'Mayis',
      'Haziran',
      'Temmuz',
      'Agustos',
      'Eylul',
      'Ekim',
      'Kasim',
      'Aralik',
    ];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }
}

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
            color: _PaymentFormPageState._text,
          ),
          Expanded(
            child: Text(
              'Odeme Ekle',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: _PaymentFormPageState._text,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SelectionFields extends StatelessWidget {
  const _SelectionFields({
    required this.selectedStudent,
    required this.selectedLesson,
    required this.studentOptions,
    required this.lessonOptions,
    required this.onStudentChanged,
    required this.onLessonChanged,
  });

  final String selectedStudent;
  final String selectedLesson;
  final List<String> studentOptions;
  final List<String> lessonOptions;
  final ValueChanged<String?> onStudentChanged;
  final ValueChanged<String?> onLessonChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const AppFieldLabel(text: 'Ogrenci'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: selectedStudent,
          decoration: appInputDecoration('Ogrenci sec'),
          items: studentOptions
              .map(
                (student) => DropdownMenuItem<String>(
                  value: student,
                  child: Text(student),
                ),
              )
              .toList(),
          onChanged: onStudentChanged,
        ),
        const SizedBox(height: 14),
        const AppFieldLabel(text: 'Ders'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: ValueKey<String>('lesson-$selectedStudent'),
          initialValue: selectedLesson,
          decoration: appInputDecoration('Ders sec'),
          items: lessonOptions
              .map(
                (lesson) => DropdownMenuItem<String>(
                  value: lesson,
                  child: Text(lesson),
                ),
              )
              .toList(),
          onChanged: onLessonChanged,
        ),
      ],
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
        const AppFieldLabel(text: 'Odeme yontemi'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: value,
          decoration: appInputDecoration('Odeme yontemi sec'),
          items: const <DropdownMenuItem<String>>[
            DropdownMenuItem<String>(
              value: 'Kredi karti',
              child: Text('Kredi karti'),
            ),
            DropdownMenuItem<String>(value: 'IBAN', child: Text('IBAN')),
            DropdownMenuItem<String>(value: 'Elden', child: Text('Elden')),
          ],
          onChanged: onChanged,
        ),
      ],
    );
  }
}
