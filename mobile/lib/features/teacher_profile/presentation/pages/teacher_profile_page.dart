import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class TeacherProfilePage extends StatefulWidget {
  const TeacherProfilePage({super.key});

  @override
  State<TeacherProfilePage> createState() => _TeacherProfilePageState();
}

class _TeacherProfilePageState extends State<TeacherProfilePage> {
  static const _navy = Color(0xFF082B4F);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _red = Color(0xFFFF5A5F);
  static const _slate = Color(0xFF6B7A90);
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EEF7);
  static const _surfaceLow = Color(0xFFEAF3FF);

  late _TeacherProfileData _profile;
  late List<String> _expertise;
  late List<_AvailabilitySlot> _availability;

  bool _profileVisible = true;
  bool _onlineAvailable = true;
  bool _inPersonAvailable = true;

  @override
  void initState() {
    super.initState();
    _profile = _TeacherProfileData.initial();
    _expertise = <String>[
      'LGS Matematik',
      'TYT Problemler',
      'AYT Fonksiyonlar',
      'Geometri',
      'Haftalık takip',
    ];
    _availability = <_AvailabilitySlot>[
      const _AvailabilitySlot(day: 'Pazartesi', start: '18:00', end: '20:00'),
      const _AvailabilitySlot(day: 'Çarşamba', start: '19:00', end: '21:00'),
      const _AvailabilitySlot(day: 'Cumartesi', start: '10:00', end: '13:00'),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    if (session?.fullName.trim().isNotEmpty == true &&
        _profile.fullName == _TeacherProfileData.initial().fullName) {
      _profile = _profile.copyWith(fullName: session!.fullName);
    }

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
          children: <Widget>[
            _TopBar(onBack: () => context.pop()),
            const SizedBox(height: 22),
            _PhotoHeader(profile: _profile, onChangePhoto: _showPhotoOptions),
            const SizedBox(height: 18),
            Row(
              children: const <Widget>[
                Expanded(
                  child: _MetricTile(
                    icon: Icons.star_rounded,
                    label: 'Puan',
                    value: '4.8',
                    helper: '126 yorum',
                    color: _amber,
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: _MetricTile(
                    icon: Icons.groups_rounded,
                    label: 'Aktif öğrenci',
                    value: '18',
                    helper: 'Bu ay',
                    color: _emerald,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 22),
            _VisibilityPanel(
              value: _profileVisible,
              onChanged: (value) => setState(() => _profileVisible = value),
            ),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Kişisel bilgiler'),
            const SizedBox(height: 10),
            _SettingsPanel(
              children: <Widget>[
                _SettingTile(
                  icon: Icons.badge_outlined,
                  label: 'Ad Soyad',
                  value: _profile.fullName,
                  onTap: () => _editText(
                    title: 'Ad Soyad',
                    initialValue: _profile.fullName,
                    icon: Icons.badge_outlined,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(fullName: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.mail_outline_rounded,
                  label: 'E-posta',
                  value: _profile.email,
                  onTap: () => _editText(
                    title: 'E-posta',
                    initialValue: _profile.email,
                    icon: Icons.mail_outline_rounded,
                    keyboardType: TextInputType.emailAddress,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(email: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.phone_outlined,
                  label: 'Telefon',
                  value: _profile.phone,
                  onTap: () => _editText(
                    title: 'Telefon',
                    initialValue: _profile.phone,
                    icon: Icons.phone_outlined,
                    keyboardType: TextInputType.phone,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(phone: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.location_on_outlined,
                  label: 'Konum',
                  value: '${_profile.city}, ${_profile.district}',
                  onTap: _editLocation,
                ),
              ],
            ),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Öğretmen bilgileri'),
            const SizedBox(height: 10),
            _SettingsPanel(
              children: <Widget>[
                _SettingTile(
                  icon: Icons.workspace_premium_outlined,
                  label: 'Başlık',
                  value: _profile.title,
                  onTap: () => _editText(
                    title: 'Başlık',
                    initialValue: _profile.title,
                    icon: Icons.workspace_premium_outlined,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(title: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.school_outlined,
                  label: 'Branş',
                  value: _profile.subject,
                  onTap: () => _editText(
                    title: 'Branş',
                    initialValue: _profile.subject,
                    icon: Icons.school_outlined,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(subject: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.menu_book_outlined,
                  label: 'Eğitim',
                  value: _profile.education,
                  onTap: () => _editText(
                    title: 'Eğitim',
                    initialValue: _profile.education,
                    icon: Icons.menu_book_outlined,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(education: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.timeline_rounded,
                  label: 'Deneyim',
                  value: _profile.experience,
                  onTap: () => _editText(
                    title: 'Deneyim',
                    initialValue: _profile.experience,
                    icon: Icons.timeline_rounded,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(experience: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.payments_outlined,
                  label: 'Saatlik ücret',
                  value: _profile.hourlyRate,
                  onTap: () => _editText(
                    title: 'Saatlik ücret',
                    initialValue: _profile.hourlyRate,
                    icon: Icons.payments_outlined,
                    keyboardType: TextInputType.number,
                    onSaved: (value) {
                      setState(
                        () => _profile = _profile.copyWith(hourlyRate: value),
                      );
                    },
                  ),
                ),
                const _DividerLine(),
                _SettingTile(
                  icon: Icons.sync_alt_rounded,
                  label: 'Ders formatı',
                  value: _profile.lessonFormat,
                  onTap: _editLessonFormat,
                ),
              ],
            ),
            const SizedBox(height: 14),
            _LongTextTile(
              icon: Icons.auto_stories_outlined,
              title: 'Hakkında',
              body: _profile.biography,
              color: _blue,
              onTap: () => _editText(
                title: 'Hakkında',
                initialValue: _profile.biography,
                icon: Icons.auto_stories_outlined,
                minLines: 5,
                maxLines: 7,
                onSaved: (value) {
                  setState(
                    () => _profile = _profile.copyWith(biography: value),
                  );
                },
              ),
            ),
            const SizedBox(height: 14),
            _LongTextTile(
              icon: Icons.family_restroom_rounded,
              title: 'Veliye not',
              body: _profile.parentNote,
              color: _emerald,
              onTap: () => _editText(
                title: 'Veliye not',
                initialValue: _profile.parentNote,
                icon: Icons.family_restroom_rounded,
                minLines: 4,
                maxLines: 6,
                onSaved: (value) {
                  setState(
                    () => _profile = _profile.copyWith(parentNote: value),
                  );
                },
              ),
            ),
            const SizedBox(height: 22),
            _SectionHeader(
              title: 'Uzmanlık alanları',
              actionLabel: 'Düzenle',
              onAction: _editExpertise,
            ),
            const SizedBox(height: 10),
            _ChipPanel(items: _expertise),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Ders tercihleri'),
            const SizedBox(height: 10),
            _PreferencePanel(
              onlineAvailable: _onlineAvailable,
              inPersonAvailable: _inPersonAvailable,
              onOnlineChanged: (value) =>
                  setState(() => _onlineAvailable = value),
              onInPersonChanged: (value) =>
                  setState(() => _inPersonAvailable = value),
            ),
            const SizedBox(height: 22),
            _SectionHeader(
              title: 'Müsaitlik durumu',
              actionLabel: 'Ekle',
              onAction: _editAvailability,
            ),
            const SizedBox(height: 10),
            _AvailabilityPanel(
              items: _availability,
              onEdit: (slot) => _editAvailability(slot: slot),
              onDelete: (slot) {
                setState(() => _availability.remove(slot));
              },
            ),
          ],
        ),
      ),
    );
  }

  void _showPhotoOptions() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const _SheetTitle(title: 'Profil fotoğrafı'),
              const SizedBox(height: 14),
              _ActionRow(
                icon: Icons.photo_camera_outlined,
                title: 'Kamera ile çek',
                subtitle: 'Statik önizleme için fotoğraf değiştirildi sayılır.',
                onTap: () {
                  Navigator.of(context).pop();
                  _showSavedMessage('Fotoğraf güncellendi.');
                },
              ),
              const _DividerLine(),
              _ActionRow(
                icon: Icons.photo_library_outlined,
                title: 'Galeriden seç',
                subtitle:
                    'API bağlantısı olmadan sadece tasarım akışı gösterilir.',
                onTap: () {
                  Navigator.of(context).pop();
                  _showSavedMessage('Fotoğraf güncellendi.');
                },
              ),
            ],
          ),
        );
      },
    );
  }

  void _editText({
    required String title,
    required String initialValue,
    required IconData icon,
    required ValueChanged<String> onSaved,
    TextInputType? keyboardType,
    int minLines = 1,
    int maxLines = 1,
  }) {
    final controller = TextEditingController(text: initialValue);
    final formKey = GlobalKey<FormState>();
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            6,
            20,
            MediaQuery.of(context).viewInsets.bottom + 24,
          ),
          child: Form(
            key: formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _SheetTitle(title: title),
                const SizedBox(height: 14),
                TextFormField(
                  controller: controller,
                  keyboardType: keyboardType,
                  minLines: minLines,
                  maxLines: maxLines,
                  autofocus: true,
                  validator: (value) {
                    if ((value ?? '').trim().isEmpty) {
                      return '$title boş bırakılamaz.';
                    }
                    return null;
                  },
                  decoration: _inputDecoration(title, icon),
                ),
                const SizedBox(height: 18),
                _SheetActions(
                  onCancel: () => Navigator.of(context).pop(),
                  onSave: () {
                    if (!(formKey.currentState?.validate() ?? false)) {
                      return;
                    }
                    onSaved(controller.text.trim());
                    Navigator.of(context).pop();
                    _showSavedMessage('$title güncellendi.');
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  void _editLocation() {
    final cityController = TextEditingController(text: _profile.city);
    final districtController = TextEditingController(text: _profile.district);
    final formKey = GlobalKey<FormState>();
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            6,
            20,
            MediaQuery.of(context).viewInsets.bottom + 24,
          ),
          child: Form(
            key: formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                const _SheetTitle(title: 'Konum'),
                const SizedBox(height: 14),
                TextFormField(
                  controller: cityController,
                  autofocus: true,
                  validator: _requiredValidator('Şehir'),
                  decoration: _inputDecoration(
                    'Şehir',
                    Icons.location_city_outlined,
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: districtController,
                  validator: _requiredValidator('İlçe'),
                  decoration: _inputDecoration(
                    'İlçe',
                    Icons.location_on_outlined,
                  ),
                ),
                const SizedBox(height: 18),
                _SheetActions(
                  onCancel: () => Navigator.of(context).pop(),
                  onSave: () {
                    if (!(formKey.currentState?.validate() ?? false)) {
                      return;
                    }
                    setState(() {
                      _profile = _profile.copyWith(
                        city: cityController.text.trim(),
                        district: districtController.text.trim(),
                      );
                    });
                    Navigator.of(context).pop();
                    _showSavedMessage('Konum güncellendi.');
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  void _editLessonFormat() {
    _showChoiceSheet(
      title: 'Ders formatı',
      values: const <String>['Online', 'Yüz yüze', 'Online + Yüz yüze'],
      selected: _profile.lessonFormat,
      onSelected: (value) {
        setState(() => _profile = _profile.copyWith(lessonFormat: value));
      },
    );
  }

  void _editExpertise() {
    final selected = Set<String>.from(_expertise);
    const options = <String>[
      'LGS Matematik',
      'TYT Problemler',
      'AYT Fonksiyonlar',
      'Geometri',
      'Ders takip sistemi',
      'Sınav koçluğu',
      'Okula destek',
    ];

    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setModalState) {
            return Padding(
              padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const _SheetTitle(title: 'Uzmanlık alanları'),
                  const SizedBox(height: 14),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: options.map((option) {
                      final isSelected = selected.contains(option);
                      return FilterChip(
                        selected: isSelected,
                        label: Text(option),
                        onSelected: (value) {
                          setModalState(() {
                            value
                                ? selected.add(option)
                                : selected.remove(option);
                          });
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 18),
                  _SheetActions(
                    onCancel: () => Navigator.of(context).pop(),
                    onSave: () {
                      setState(() => _expertise = selected.toList());
                      Navigator.of(context).pop();
                      _showSavedMessage('Uzmanlık alanları güncellendi.');
                    },
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  void _editAvailability({_AvailabilitySlot? slot}) {
    final days = <String>[
      'Pazartesi',
      'Salı',
      'Çarşamba',
      'Perşembe',
      'Cuma',
      'Cumartesi',
      'Pazar',
    ];
    final times = <String>[
      '08:00',
      '09:00',
      '10:00',
      '11:00',
      '12:00',
      '13:00',
      '14:00',
      '15:00',
      '16:00',
      '17:00',
      '18:00',
      '19:00',
      '20:00',
      '21:00',
    ];
    var selectedDay = slot?.day ?? 'Pazartesi';
    var start = slot?.start ?? '18:00';
    var end = slot?.end ?? '20:00';

    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setModalState) {
            return Padding(
              padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _SheetTitle(
                    title: slot == null
                        ? 'Müsaitlik ekle'
                        : 'Müsaitlik düzenle',
                  ),
                  const SizedBox(height: 14),
                  Text('Gün', style: _sheetLabelStyle(context)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: days.map((day) {
                      final selected = day == selectedDay;
                      return ChoiceChip(
                        selected: selected,
                        label: Text(day),
                        onSelected: (_) {
                          setModalState(() => selectedDay = day);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 18),
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: _TimeDropdown(
                          label: 'Başlangıç',
                          value: start,
                          values: times,
                          onChanged: (value) {
                            if (value != null) {
                              setModalState(() => start = value);
                            }
                          },
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: _TimeDropdown(
                          label: 'Bitiş',
                          value: end,
                          values: times,
                          onChanged: (value) {
                            if (value != null) {
                              setModalState(() => end = value);
                            }
                          },
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 18),
                  _SheetActions(
                    onCancel: () => Navigator.of(context).pop(),
                    onSave: () {
                      final updated = _AvailabilitySlot(
                        day: selectedDay,
                        start: start,
                        end: end,
                      );
                      setState(() {
                        if (slot == null) {
                          _availability.add(updated);
                        } else {
                          final index = _availability.indexOf(slot);
                          if (index >= 0) {
                            _availability[index] = updated;
                          }
                        }
                      });
                      Navigator.of(context).pop();
                      _showSavedMessage('Müsaitlik güncellendi.');
                    },
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  void _showChoiceSheet({
    required String title,
    required List<String> values,
    required String selected,
    required ValueChanged<String> onSelected,
  }) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _SheetTitle(title: title),
              const SizedBox(height: 12),
              ...values.map(
                (value) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(value),
                  trailing: selected == value
                      ? const Icon(Icons.check_rounded, color: _navy)
                      : null,
                  onTap: () {
                    onSelected(value);
                    Navigator.of(context).pop();
                    _showSavedMessage('$title güncellendi.');
                  },
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  FormFieldValidator<String> _requiredValidator(String label) {
    return (value) {
      if ((value ?? '').trim().isEmpty) {
        return '$label boş bırakılamaz.';
      }
      return null;
    };
  }

  InputDecoration _inputDecoration(String label, IconData icon) {
    return InputDecoration(
      labelText: label,
      prefixIcon: Icon(icon, color: _blue),
      filled: true,
      fillColor: _background,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(16),
        borderSide: const BorderSide(color: _border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(16),
        borderSide: const BorderSide(color: _border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(16),
        borderSide: const BorderSide(color: _navy),
      ),
    );
  }

  TextStyle? _sheetLabelStyle(BuildContext context) {
    return Theme.of(
      context,
    ).textTheme.labelLarge?.copyWith(color: _text, fontWeight: FontWeight.w800);
  }

  void _showSavedMessage(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _IconButtonBox(icon: Icons.arrow_back_rounded, onTap: onBack),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Profil Bilgileri',
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: _TeacherProfilePageState._text,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                'Alanlara dokunarak düzenle',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: _TeacherProfilePageState._slate,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _PhotoHeader extends StatelessWidget {
  const _PhotoHeader({required this.profile, required this.onChangePhoto});

  final _TeacherProfileData profile;
  final VoidCallback onChangePhoto;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Stack(
          clipBehavior: Clip.none,
          children: <Widget>[
            Container(
              width: 126,
              height: 126,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: _TeacherProfilePageState._navy,
                border: Border.all(color: Colors.white, width: 5),
                boxShadow: const <BoxShadow>[
                  BoxShadow(
                    color: Color(0x1F082B4F),
                    blurRadius: 28,
                    offset: Offset(0, 12),
                  ),
                ],
              ),
              clipBehavior: Clip.antiAlias,
              child: Image.network(
                profile.photoUrl,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => Center(
                  child: Text(
                    _initials(profile.fullName),
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
              ),
            ),
            Positioned(
              right: 2,
              bottom: 4,
              child: InkWell(
                borderRadius: BorderRadius.circular(999),
                onTap: onChangePhoto,
                child: Container(
                  width: 42,
                  height: 42,
                  decoration: BoxDecoration(
                    color: _TeacherProfilePageState._navy,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 3),
                  ),
                  child: const Icon(
                    Icons.photo_camera_rounded,
                    color: Colors.white,
                    size: 20,
                  ),
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 14),
        Text(
          profile.fullName,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
            color: _TeacherProfilePageState._text,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          profile.title,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
            color: _TeacherProfilePageState._slate,
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({
    required this.icon,
    required this.label,
    required this.value,
    required this.helper,
    required this.color,
  });

  final IconData icon;
  final String label;
  final String value;
  final String helper;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 112,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: _TeacherProfilePageState._border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _TintedIcon(icon: icon, color: color, size: 36),
          const Spacer(),
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: _TeacherProfilePageState._slate,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 3),
          Row(
            children: <Widget>[
              Text(
                value,
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: _TeacherProfilePageState._text,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  helper,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: color,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _VisibilityPanel extends StatelessWidget {
  const _VisibilityPanel({required this.value, required this.onChanged});

  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _TeacherProfilePageState._border),
      ),
      child: Row(
        children: <Widget>[
          const _TintedIcon(
            icon: Icons.visibility_outlined,
            color: _TeacherProfilePageState._emerald,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'Profil görünürlüğü',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: _TeacherProfilePageState._text,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  'Öğrenci ve veli aramalarında görünür.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _TeacherProfilePageState._slate,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          Switch.adaptive(
            value: value,
            activeThumbColor: _TeacherProfilePageState._navy,
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }
}

class _SettingsPanel extends StatelessWidget {
  const _SettingsPanel({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _TeacherProfilePageState._border),
      ),
      child: Column(children: children),
    );
  }
}

class _SettingTile extends StatelessWidget {
  const _SettingTile({
    required this.icon,
    required this.label,
    required this.value,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
        child: Row(
          children: <Widget>[
            _TintedIcon(icon: icon, color: _TeacherProfilePageState._blue),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    label,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: _TeacherProfilePageState._slate,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    value,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: _TeacherProfilePageState._text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: _TeacherProfilePageState._slate,
            ),
          ],
        ),
      ),
    );
  }
}

class _LongTextTile extends StatelessWidget {
  const _LongTextTile({
    required this.icon,
    required this.title,
    required this.body,
    required this.color,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String body;
  final Color color;
  final VoidCallback onTap;

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
          border: Border.all(color: _TeacherProfilePageState._border),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            _TintedIcon(icon: icon, color: color),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          title,
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: _TeacherProfilePageState._text,
                                fontWeight: FontWeight.w900,
                              ),
                        ),
                      ),
                      const Icon(
                        Icons.chevron_right_rounded,
                        color: _TeacherProfilePageState._slate,
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text(
                    body,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _TeacherProfilePageState._slate,
                      height: 1.45,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: Theme.of(context).textTheme.titleLarge?.copyWith(
        color: _TeacherProfilePageState._text,
        fontWeight: FontWeight.w900,
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.title,
    required this.actionLabel,
    required this.onAction,
  });

  final String title;
  final String actionLabel;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(child: _SectionTitle(title: title)),
        TextButton(onPressed: onAction, child: Text(actionLabel)),
      ],
    );
  }
}

class _ChipPanel extends StatelessWidget {
  const _ChipPanel({required this.items});

  final List<String> items;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _TeacherProfilePageState._border),
      ),
      child: Wrap(
        spacing: 8,
        runSpacing: 8,
        children: items
            .map(
              (item) => Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 9,
                ),
                decoration: BoxDecoration(
                  color: _TeacherProfilePageState._surfaceLow,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  item,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: _TeacherProfilePageState._navy,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            )
            .toList(),
      ),
    );
  }
}

class _PreferencePanel extends StatelessWidget {
  const _PreferencePanel({
    required this.onlineAvailable,
    required this.inPersonAvailable,
    required this.onOnlineChanged,
    required this.onInPersonChanged,
  });

  final bool onlineAvailable;
  final bool inPersonAvailable;
  final ValueChanged<bool> onOnlineChanged;
  final ValueChanged<bool> onInPersonChanged;

  @override
  Widget build(BuildContext context) {
    return _SettingsPanel(
      children: <Widget>[
        _SwitchRow(
          icon: Icons.videocam_outlined,
          title: 'Online ders',
          subtitle: 'Uzaktan ders taleplerinde görünür olur.',
          value: onlineAvailable,
          onChanged: onOnlineChanged,
        ),
        const _DividerLine(),
        _SwitchRow(
          icon: Icons.location_city_outlined,
          title: 'Yüz yüze ders',
          subtitle: 'Konum bazlı aramalarda listelenir.',
          value: inPersonAvailable,
          onChanged: onInPersonChanged,
        ),
      ],
    );
  }
}

class _SwitchRow extends StatelessWidget {
  const _SwitchRow({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
      child: Row(
        children: <Widget>[
          _TintedIcon(icon: icon, color: _TeacherProfilePageState._emerald),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: _TeacherProfilePageState._text,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _TeacherProfilePageState._slate,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          Switch.adaptive(
            value: value,
            activeThumbColor: _TeacherProfilePageState._navy,
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }
}

class _AvailabilityPanel extends StatelessWidget {
  const _AvailabilityPanel({
    required this.items,
    required this.onEdit,
    required this.onDelete,
  });

  final List<_AvailabilitySlot> items;
  final ValueChanged<_AvailabilitySlot> onEdit;
  final ValueChanged<_AvailabilitySlot> onDelete;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _TeacherProfilePageState._border),
      ),
      child: Column(
        children: items.isEmpty
            ? <Widget>[const _EmptyAvailability()]
            : items
                  .map(
                    (item) => Padding(
                      padding: EdgeInsets.only(
                        bottom: item == items.last ? 0 : 10,
                      ),
                      child: Row(
                        children: <Widget>[
                          Container(
                            width: 10,
                            height: 44,
                            decoration: BoxDecoration(
                              color: _TeacherProfilePageState._blue,
                              borderRadius: BorderRadius.circular(999),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: <Widget>[
                                Text(
                                  item.day,
                                  style: Theme.of(context).textTheme.titleSmall
                                      ?.copyWith(
                                        color: _TeacherProfilePageState._text,
                                        fontWeight: FontWeight.w900,
                                      ),
                                ),
                                const SizedBox(height: 3),
                                Text(
                                  '${item.start} - ${item.end}',
                                  style: Theme.of(context).textTheme.bodySmall
                                      ?.copyWith(
                                        color: _TeacherProfilePageState._slate,
                                        fontWeight: FontWeight.w700,
                                      ),
                                ),
                              ],
                            ),
                          ),
                          IconButton(
                            onPressed: () => onEdit(item),
                            icon: const Icon(Icons.edit_outlined),
                          ),
                          IconButton(
                            onPressed: () => onDelete(item),
                            icon: const Icon(Icons.delete_outline_rounded),
                            color: _TeacherProfilePageState._red,
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
      ),
    );
  }
}

class _EmptyAvailability extends StatelessWidget {
  const _EmptyAvailability();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Text(
        'Henüz müsaitlik eklenmedi.',
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
          color: _TeacherProfilePageState._slate,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _TimeDropdown extends StatelessWidget {
  const _TimeDropdown({
    required this.label,
    required this.value,
    required this.values,
    required this.onChanged,
  });

  final String label;
  final String value;
  final List<String> values;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      initialValue: value,
      items: values
          .map(
            (item) => DropdownMenuItem<String>(value: item, child: Text(item)),
          )
          .toList(),
      onChanged: onChanged,
      decoration: InputDecoration(
        labelText: label,
        filled: true,
        fillColor: _TeacherProfilePageState._background,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(16)),
      ),
    );
  }
}

class _SheetActions extends StatelessWidget {
  const _SheetActions({required this.onCancel, required this.onSave});

  final VoidCallback onCancel;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: OutlinedButton(
            onPressed: onCancel,
            child: const Text('Vazgeç'),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: FilledButton(onPressed: onSave, child: const Text('Kaydet')),
        ),
      ],
    );
  }
}

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: _TintedIcon(icon: icon, color: _TeacherProfilePageState._blue),
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right_rounded),
      onTap: onTap,
    );
  }
}

class _SheetTitle extends StatelessWidget {
  const _SheetTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleLarge?.copyWith(
          color: _TeacherProfilePageState._text,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }
}

class _TintedIcon extends StatelessWidget {
  const _TintedIcon({required this.icon, required this.color, this.size = 42});

  final IconData icon;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: size >= 40 ? 22 : 19),
    );
  }
}

class _IconButtonBox extends StatelessWidget {
  const _IconButtonBox({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        width: 46,
        height: 46,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _TeacherProfilePageState._border),
        ),
        child: Icon(icon, color: _TeacherProfilePageState._text),
      ),
    );
  }
}

class _DividerLine extends StatelessWidget {
  const _DividerLine();

  @override
  Widget build(BuildContext context) {
    return const Divider(
      height: 1,
      indent: 68,
      color: _TeacherProfilePageState._border,
    );
  }
}

class _TeacherProfileData {
  const _TeacherProfileData({
    required this.fullName,
    required this.title,
    required this.subject,
    required this.city,
    required this.district,
    required this.phone,
    required this.email,
    required this.education,
    required this.experience,
    required this.hourlyRate,
    required this.lessonFormat,
    required this.photoUrl,
    required this.biography,
    required this.parentNote,
  });

  factory _TeacherProfileData.initial() {
    return const _TeacherProfileData(
      fullName: 'Ahmet Yılmaz',
      title: 'LGS ve TYT Matematik Eğitmeni',
      subject: 'Matematik',
      city: 'İstanbul',
      district: 'Kadıköy',
      phone: '+90 532 410 24 18',
      email: 'ahmet.yilmaz@egitimussu.com',
      education: 'Boğaziçi Üniversitesi Matematik',
      experience: '8 yıl',
      hourlyRate: '850 TRY',
      lessonFormat: 'Online + Yüz yüze',
      photoUrl: 'https://i.pravatar.cc/240?img=12',
      biography:
          'Sınav gruplarında konu eksiğini hızlı tespit eden, düzenli ödev ve takip sistemiyle ilerleyen bir matematik öğretmeniyim.',
      parentNote:
          'Her ders sonunda kısa ders notu, ödev durumu ve bir sonraki hedef veliyle paylaşılır.',
    );
  }

  final String fullName;
  final String title;
  final String subject;
  final String city;
  final String district;
  final String phone;
  final String email;
  final String education;
  final String experience;
  final String hourlyRate;
  final String lessonFormat;
  final String photoUrl;
  final String biography;
  final String parentNote;

  _TeacherProfileData copyWith({
    String? fullName,
    String? title,
    String? subject,
    String? city,
    String? district,
    String? phone,
    String? email,
    String? education,
    String? experience,
    String? hourlyRate,
    String? lessonFormat,
    String? biography,
    String? parentNote,
  }) {
    return _TeacherProfileData(
      fullName: fullName ?? this.fullName,
      title: title ?? this.title,
      subject: subject ?? this.subject,
      city: city ?? this.city,
      district: district ?? this.district,
      phone: phone ?? this.phone,
      email: email ?? this.email,
      education: education ?? this.education,
      experience: experience ?? this.experience,
      hourlyRate: hourlyRate ?? this.hourlyRate,
      lessonFormat: lessonFormat ?? this.lessonFormat,
      photoUrl: photoUrl,
      biography: biography ?? this.biography,
      parentNote: parentNote ?? this.parentNote,
    );
  }
}

class _AvailabilitySlot {
  const _AvailabilitySlot({
    required this.day,
    required this.start,
    required this.end,
  });

  final String day;
  final String start;
  final String end;
}

String _initials(String value) {
  final parts = value
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty)
      .take(2)
      .toList();
  if (parts.isEmpty) {
    return 'Ö';
  }
  return parts.map((part) => part.characters.first).join().toUpperCase();
}
