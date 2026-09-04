import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:flutter/material.dart';

/// Haftanin gunleri — indeks 0 = `dayOfWeek` 1 (Pazartesi), backend ISO-8601 sirasi.
const List<String> availabilityDayNames = <String>[
  'Pazartesi',
  'Salı',
  'Çarşamba',
  'Perşembe',
  'Cuma',
  'Cumartesi',
  'Pazar',
];

/// Alt sayfadaki saat seceneklerinin listesi (06:00–23:30, 30 dakika adim).
final List<String> availabilityTimeOptions = List<String>.generate(
  36,
  (index) {
    final int minutes = 6 * 60 + index * 30;
    return '${(minutes ~/ 60).toString().padLeft(2, '0')}:'
        '${(minutes % 60).toString().padLeft(2, '0')}';
  },
);

/// `HH:mm[:ss]` metnini gun ici dakikaya cevirir; cozulemezse `-1`.
int availabilityMinutesOf(String time) {
  final parts = time.split(':');
  if (parts.length < 2) return -1;
  final hour = int.tryParse(parts[0]);
  final minute = int.tryParse(parts[1]);
  if (hour == null || minute == null) return -1;
  return hour * 60 + minute;
}

/// `18:00:00` → `18:00` (ekranda saniye gosterilmez).
String availabilityTimeLabel(String time) {
  return time.length >= 5 ? time.substring(0, 5) : time;
}

/// [candidate] ile ayni gunde **kesisen** bir aralik var mi?
///
/// Bitisik araliklar (`18:00-20:00` ve `20:00-21:00`) cakisma sayilmaz.
/// [ignore] verilirse (duzenleme akisi) o slot karsilastirma disi birakilir.
bool availabilitySlotOverlaps(
  List<TeacherAvailability> slots,
  TeacherAvailability candidate, {
  TeacherAvailability? ignore,
}) {
  final int start = availabilityMinutesOf(candidate.startTime);
  final int end = availabilityMinutesOf(candidate.endTime);
  for (final slot in slots) {
    if (identical(slot, ignore)) continue;
    if (slot.dayOfWeek != candidate.dayOfWeek) continue;
    final int otherStart = availabilityMinutesOf(slot.startTime);
    final int otherEnd = availabilityMinutesOf(slot.endTime);
    if (start < otherEnd && otherStart < end) return true;
  }
  return false;
}

/// Aday araligin gecerlilik hatasi; sorun yoksa `null`.
///
/// Kural UI'dan bagimsiz tutulur: alt sayfa yalnizca dondugu metni gosterir,
/// boylece dogrulama dropdown surmeden test edilebilir.
String? availabilitySlotError(
  List<TeacherAvailability> existing,
  TeacherAvailability candidate, {
  TeacherAvailability? ignore,
}) {
  if (availabilityMinutesOf(candidate.endTime) <=
      availabilityMinutesOf(candidate.startTime)) {
    return 'Bitiş saati başlangıçtan sonra olmalı.';
  }
  if (availabilitySlotOverlaps(existing, candidate, ignore: ignore)) {
    return 'Bu aralık aynı gündeki başka bir aralıkla çakışıyor.';
  }
  return null;
}

/// D-04 / M02-5: Ogretmen uygunluk duzenleyicisi.
///
/// Haftanin 7 gunu ayri satirlarda listelenir; her gune saat araligi eklenir,
/// silinir ve her aralik **kendi** "Online" / "Yuz yuze" bayragini tasir
/// (profil geneli tercih degil). Kesisen aralik eklenemez.
///
/// Kontrollu (controlled) bir widget'tir: listeyi sahiplenmez, her degisikligi
/// [onChanged] ile yukari verir. Kaydetme, cagiran ekranin isidir ([onSave]).
class AvailabilityEditor extends StatelessWidget {
  const AvailabilityEditor({
    super.key,
    required this.slots,
    required this.onChanged,
    this.onSave,
    this.isSaving = false,
  });

  final List<TeacherAvailability> slots;
  final ValueChanged<List<TeacherAvailability>> onChanged;
  final VoidCallback? onSave;
  final bool isSaving;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        for (int day = 1; day <= availabilityDayNames.length; day++)
          Padding(
            padding: EdgeInsets.only(bottom: day == 7 ? 0 : 10),
            child: _DaySection(
              dayOfWeek: day,
              slots: _slotsOfDay(day),
              onAdd: () => _openSlotSheet(context, day),
              onDelete: _deleteSlot,
              onToggleOnline: (slot) => _replaceSlot(
                slot,
                _copySlot(slot, isOnline: !slot.isOnlineAvailable),
              ),
              onToggleInPerson: (slot) => _replaceSlot(
                slot,
                _copySlot(slot, isInPerson: !slot.isInPersonAvailable),
              ),
            ),
          ),
        if (onSave != null) ...<Widget>[
          const SizedBox(height: 14),
          AppPrimaryButton(
            key: const Key('availability-save'),
            label: 'Uygunluğu kaydet',
            isLoading: isSaving,
            onPressed: onSave,
          ),
        ],
      ],
    );
  }

  List<TeacherAvailability> _slotsOfDay(int dayOfWeek) {
    final result = slots.where((slot) => slot.dayOfWeek == dayOfWeek).toList()
      ..sort(
        (a, b) => availabilityMinutesOf(
          a.startTime,
        ).compareTo(availabilityMinutesOf(b.startTime)),
      );
    return result;
  }

  static TeacherAvailability _copySlot(
    TeacherAvailability slot, {
    bool? isOnline,
    bool? isInPerson,
  }) {
    return TeacherAvailability(
      dayOfWeek: slot.dayOfWeek,
      startTime: slot.startTime,
      endTime: slot.endTime,
      isOnlineAvailable: isOnline ?? slot.isOnlineAvailable,
      isInPersonAvailable: isInPerson ?? slot.isInPersonAvailable,
    );
  }

  void _deleteSlot(TeacherAvailability slot) {
    onChanged(
      slots.where((item) => !identical(item, slot)).toList(growable: false),
    );
  }

  void _replaceSlot(TeacherAvailability slot, TeacherAvailability updated) {
    onChanged(
      slots
          .map((item) => identical(item, slot) ? updated : item)
          .toList(growable: false),
    );
  }

  Future<void> _openSlotSheet(BuildContext context, int dayOfWeek) async {
    final added = await showModalBottomSheet<TeacherAvailability>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) => _AvailabilitySlotSheet(
        dayOfWeek: dayOfWeek,
        existing: slots,
      ),
    );
    if (added != null) {
      onChanged(<TeacherAvailability>[...slots, added]);
    }
  }
}

class _DaySection extends StatelessWidget {
  const _DaySection({
    required this.dayOfWeek,
    required this.slots,
    required this.onAdd,
    required this.onDelete,
    required this.onToggleOnline,
    required this.onToggleInPerson,
  });

  final int dayOfWeek;
  final List<TeacherAvailability> slots;
  final VoidCallback onAdd;
  final ValueChanged<TeacherAvailability> onDelete;
  final ValueChanged<TeacherAvailability> onToggleOnline;
  final ValueChanged<TeacherAvailability> onToggleInPerson;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 10, 8, 14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  availabilityDayNames[dayOfWeek - 1],
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w900,
                  ),
                ),
              ),
              IconButton(
                key: Key('availability-add-$dayOfWeek'),
                tooltip: 'Saat aralığı ekle',
                onPressed: onAdd,
                icon: const Icon(Icons.add_circle_outline_rounded),
                color: AppColors.primary,
              ),
            ],
          ),
          if (slots.isEmpty)
            Padding(
              padding: const EdgeInsets.only(left: 2, top: 2),
              child: Text(
                'Uygun saat yok',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: AppColors.textMuted,
                  fontWeight: FontWeight.w700,
                ),
              ),
            )
          else
            for (final slot in slots)
              Padding(
                padding: EdgeInsets.only(
                  top: 6,
                  bottom: identical(slot, slots.last) ? 0 : 4,
                ),
                child: _SlotTile(
                  slot: slot,
                  onDelete: () => onDelete(slot),
                  onToggleOnline: () => onToggleOnline(slot),
                  onToggleInPerson: () => onToggleInPerson(slot),
                ),
              ),
        ],
      ),
    );
  }
}

class _SlotTile extends StatelessWidget {
  const _SlotTile({
    required this.slot,
    required this.onDelete,
    required this.onToggleOnline,
    required this.onToggleInPerson,
  });

  final TeacherAvailability slot;
  final VoidCallback onDelete;
  final VoidCallback onToggleOnline;
  final VoidCallback onToggleInPerson;

  @override
  Widget build(BuildContext context) {
    final String suffix = '${slot.dayOfWeek}-${slot.startTime}';
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 8, 4, 10),
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 4,
                height: 22,
                decoration: BoxDecoration(
                  color: AppColors.accentBlue,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${availabilityTimeLabel(slot.startTime)} - '
                  '${availabilityTimeLabel(slot.endTime)}',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              IconButton(
                key: Key('availability-delete-$suffix'),
                tooltip: 'Aralığı sil',
                onPressed: onDelete,
                icon: const Icon(Icons.delete_outline_rounded),
                color: AppColors.accentRed,
              ),
            ],
          ),
          const SizedBox(height: 4),
          Row(
            children: <Widget>[
              _ModeToggle(
                key: Key('availability-online-$suffix'),
                label: 'Online',
                icon: Icons.videocam_outlined,
                selected: slot.isOnlineAvailable,
                onTap: onToggleOnline,
              ),
              const SizedBox(width: 8),
              _ModeToggle(
                key: Key('availability-inperson-$suffix'),
                label: 'Yüz yüze',
                icon: Icons.location_on_outlined,
                selected: slot.isInPersonAvailable,
                onTap: onToggleInPerson,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Slot bazli ders sekli anahtari. Secili degilken de metin/ikon tasir —
/// renk **tek basina** durum gostergesi olmaz (erisilebilirlik kurali).
class _ModeToggle extends StatelessWidget {
  const _ModeToggle({
    super.key,
    required this.label,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      toggled: selected,
      button: true,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            color: selected ? AppColors.primary : AppColors.surface,
            borderRadius: BorderRadius.circular(999),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.border,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(
                selected ? Icons.check_rounded : icon,
                size: 16,
                color: selected ? AppColors.surface : AppColors.textSecondary,
              ),
              const SizedBox(width: 6),
              Text(
                label,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: selected ? AppColors.surface : AppColors.textSecondary,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Saat araligi ekleme alt sayfasi. Dogrulama burada yapilir: gecersiz aralik
/// veya cakisma varsa sayfa kapanmaz, hata metni gosterilir.
class _AvailabilitySlotSheet extends StatefulWidget {
  const _AvailabilitySlotSheet({required this.dayOfWeek, required this.existing});

  final int dayOfWeek;
  final List<TeacherAvailability> existing;

  @override
  State<_AvailabilitySlotSheet> createState() => _AvailabilitySlotSheetState();
}

class _AvailabilitySlotSheetState extends State<_AvailabilitySlotSheet> {
  String _start = '18:00';
  String _end = '20:00';
  bool _online = true;
  bool _inPerson = true;
  String? _error;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(
        20,
        6,
        20,
        24 + MediaQuery.of(context).viewInsets.bottom,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            '${availabilityDayNames[widget.dayOfWeek - 1]} — saat aralığı ekle',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: <Widget>[
              Expanded(
                child: _TimeField(
                  fieldKey: const Key('availability-start'),
                  label: 'Başlangıç',
                  value: _start,
                  onChanged: (value) => setState(() => _start = value),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _TimeField(
                  fieldKey: const Key('availability-end'),
                  label: 'Bitiş',
                  value: _end,
                  onChanged: (value) => setState(() => _end = value),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: <Widget>[
              _ModeToggle(
                key: const Key('availability-sheet-online'),
                label: 'Online',
                icon: Icons.videocam_outlined,
                selected: _online,
                onTap: () => setState(() => _online = !_online),
              ),
              const SizedBox(width: 8),
              _ModeToggle(
                key: const Key('availability-sheet-inperson'),
                label: 'Yüz yüze',
                icon: Icons.location_on_outlined,
                selected: _inPerson,
                onTap: () => setState(() => _inPerson = !_inPerson),
              ),
            ],
          ),
          if (_error != null) ...<Widget>[
            const SizedBox(height: 14),
            Container(
              key: const Key('availability-error'),
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.errorSurface,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: AppColors.errorBorder),
              ),
              child: Text(
                _error!,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: AppColors.error,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
          const SizedBox(height: 18),
          AppPrimaryButton(
            key: const Key('availability-confirm'),
            label: 'Ekle',
            onPressed: _confirm,
          ),
        ],
      ),
    );
  }

  void _confirm() {
    final candidate = TeacherAvailability(
      dayOfWeek: widget.dayOfWeek,
      startTime: '$_start:00',
      endTime: '$_end:00',
      isOnlineAvailable: _online,
      isInPersonAvailable: _inPerson,
    );

    final error = availabilitySlotError(widget.existing, candidate);
    if (error != null) {
      setState(() => _error = error);
      return;
    }

    Navigator.of(context).pop(candidate);
  }
}

class _TimeField extends StatelessWidget {
  const _TimeField({
    required this.fieldKey,
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final Key fieldKey;
  final String label;
  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String>(
      key: fieldKey,
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: availabilityTimeOptions
          .map(
            (time) => DropdownMenuItem<String>(value: time, child: Text(time)),
          )
          .toList(),
      onChanged: (selected) {
        if (selected != null) onChanged(selected);
      },
    );
  }
}
