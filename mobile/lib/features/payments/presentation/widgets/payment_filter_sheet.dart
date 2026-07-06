import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

/// Gelişmiş ödeme filtreleri (öğrenci + tarih aralığı). Uygulanınca güncel
/// [PaymentFilters]'i döndürür (metin/durum korunur); iptalde `null`.
class PaymentFilterSheet extends StatefulWidget {
  const PaymentFilterSheet({
    super.key,
    required this.filters,
    required this.students,
  });

  final PaymentFilters filters;
  final List<StudentProfile> students;

  static Future<PaymentFilters?> show(
    BuildContext context, {
    required PaymentFilters filters,
    required List<StudentProfile> students,
  }) {
    return showModalBottomSheet<PaymentFilters>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => PaymentFilterSheet(filters: filters, students: students),
    );
  }

  @override
  State<PaymentFilterSheet> createState() => _PaymentFilterSheetState();
}

class _PaymentFilterSheetState extends State<PaymentFilterSheet> {
  String? _studentId;
  String? _studentLabel;
  DateTime? _from;
  DateTime? _to;

  @override
  void initState() {
    super.initState();
    _studentId = widget.filters.studentId;
    _studentLabel = widget.filters.studentLabel;
    _from = widget.filters.dateFromUtc;
    _to = widget.filters.dateToUtc;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
        child: SafeArea(
          top: false,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: AppColors.border,
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Text(
                'Filtrele',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 16),
              const AppFieldLabel(text: 'Öğrenci'),
              const SizedBox(height: 8),
              DropdownButtonFormField<String?>(
                initialValue: _studentId,
                decoration: appInputDecoration('Tüm öğrenciler'),
                items: <DropdownMenuItem<String?>>[
                  const DropdownMenuItem<String?>(
                    value: null,
                    child: Text('Tüm öğrenciler'),
                  ),
                  ...widget.students.map(
                    (s) => DropdownMenuItem<String?>(
                      value: s.id,
                      child: Text(s.fullName, overflow: TextOverflow.ellipsis),
                    ),
                  ),
                ],
                onChanged: (value) => setState(() {
                  _studentId = value;
                  _studentLabel = value == null
                      ? null
                      : widget.students
                            .where((s) => s.id == value)
                            .map((s) => s.fullName)
                            .firstOrNull;
                }),
              ),
              const SizedBox(height: 16),
              const AppFieldLabel(text: 'Vade tarihi aralığı'),
              const SizedBox(height: 8),
              Row(
                children: <Widget>[
                  Expanded(
                    child: _DateBox(
                      label: 'Başlangıç',
                      value: _from,
                      onTap: () => _pickDate(isFrom: true),
                      onClear: _from == null
                          ? null
                          : () => setState(() => _from = null),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _DateBox(
                      label: 'Bitiş',
                      value: _to,
                      onTap: () => _pickDate(isFrom: false),
                      onClear: _to == null
                          ? null
                          : () => setState(() => _to = null),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(context).pop(
                        widget.filters.copyWith(
                          studentId: null,
                          studentLabel: null,
                          dateFromUtc: null,
                          dateToUtc: null,
                        ),
                      ),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.textSecondary,
                        side: const BorderSide(color: AppColors.border),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                      ),
                      child: const Text('Temizle'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: AppPrimaryButton(label: 'Uygula', onPressed: _apply),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _pickDate({required bool isFrom}) async {
    final now = DateTime.now();
    final initial = (isFrom ? _from : _to)?.toLocal() ?? now;
    final picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(now.year - 3),
      lastDate: DateTime(now.year + 3),
    );
    if (picked == null) return;
    setState(() {
      if (isFrom) {
        _from = DateTime.utc(picked.year, picked.month, picked.day);
      } else {
        // Bitiş: günün sonuna kadar dahil olsun.
        _to = DateTime.utc(picked.year, picked.month, picked.day, 23, 59, 59);
      }
    });
  }

  void _apply() {
    Navigator.of(context).pop(
      widget.filters.copyWith(
        studentId: _studentId,
        studentLabel: _studentLabel,
        dateFromUtc: _from,
        dateToUtc: _to,
      ),
    );
  }
}

class _DateBox extends StatelessWidget {
  const _DateBox({
    required this.label,
    required this.value,
    required this.onTap,
    this.onClear,
  });

  final String label;
  final DateTime? value;
  final VoidCallback onTap;
  final VoidCallback? onClear;

  @override
  Widget build(BuildContext context) {
    final text = value == null
        ? label
        : DateFormat('d MMM y', 'tr_TR').format(value!.toLocal());
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: <Widget>[
            const Icon(
              Icons.event_rounded,
              size: 16,
              color: AppColors.textSecondary,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                text,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: value == null
                      ? AppColors.textMuted
                      : AppColors.textPrimary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            if (onClear != null)
              GestureDetector(
                onTap: onClear,
                child: const Icon(
                  Icons.close_rounded,
                  size: 16,
                  color: AppColors.textMuted,
                ),
              ),
          ],
        ),
      ),
    );
  }
}
