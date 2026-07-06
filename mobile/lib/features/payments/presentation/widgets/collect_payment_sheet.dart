import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

/// "Tahsil Et" akışının formu. Öğretmen bu tahsilatta **ne kadar** aldığını girer;
/// varsayılan kalan tutardır (tamamı). Daha azını girerse kısmi tahsilat olur.
/// Onaylanırsa bu işlemde tahsil edilecek tutarı (double) döndürür; iptalde `null`.
class CollectPaymentSheet extends StatefulWidget {
  const CollectPaymentSheet({super.key, required this.record});

  final PaymentRecord record;

  static Future<double?> show(BuildContext context, PaymentRecord record) {
    return showModalBottomSheet<double>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => CollectPaymentSheet(record: record),
    );
  }

  @override
  State<CollectPaymentSheet> createState() => _CollectPaymentSheetState();
}

class _CollectPaymentSheetState extends State<CollectPaymentSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _amountController;

  double get _outstanding => widget.record.outstandingAmount;

  @override
  void initState() {
    super.initState();
    _amountController = TextEditingController(text: _asInput(_outstanding));
    _amountController.addListener(_onAmountChanged);
  }

  @override
  void dispose() {
    _amountController.removeListener(_onAmountChanged);
    _amountController.dispose();
    super.dispose();
  }

  void _onAmountChanged() => setState(() {});

  /// Girilen tutar kalanın tamamına eşit mi (tam tahsilat)?
  bool get _isFull {
    final value = double.tryParse(
      _amountController.text.trim().replaceAll(',', '.'),
    );
    return value != null && (value - _outstanding).abs() < 0.001;
  }

  void _fillFull() {
    _amountController.text = _asInput(_outstanding);
    _amountController.selection = TextSelection.collapsed(
      offset: _amountController.text.length,
    );
    _formKey.currentState?.validate();
  }

  @override
  Widget build(BuildContext context) {
    final record = widget.record;
    final bottomInset = MediaQuery.of(context).viewInsets.bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
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
                'Tahsilat Kaydı',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                record.description,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(
                  context,
                ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
              ),
              const SizedBox(height: 16),
              _InfoRow(
                label: 'Beklenen',
                value: _money(record.expectedAmount, record.currency),
              ),
              const SizedBox(height: 8),
              _InfoRow(
                label: 'Tahsil edilen',
                value: _money(record.collectedAmount, record.currency),
              ),
              const SizedBox(height: 8),
              _InfoRow(
                label: 'Kalan',
                value: _money(_outstanding, record.currency),
                emphasize: true,
              ),
              const SizedBox(height: 18),
              // Belirgin, tam-genişlik hızlı seçim: kalanın tamamını doldurur.
              _FullAmountButton(
                amountLabel: _money(_outstanding, record.currency),
                selected: _isFull,
                onTap: _fillFull,
              ),
              const SizedBox(height: 16),
              Row(
                children: <Widget>[
                  Expanded(child: Divider(color: AppColors.border)),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 10),
                    child: Text(
                      'veya kısmi tutar gir',
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  Expanded(child: Divider(color: AppColors.border)),
                ],
              ),
              const SizedBox(height: 12),
              Form(
                key: _formKey,
                child: AppTextField(
                  controller: _amountController,
                  labelText: 'Bu tahsilat (${record.currency})',
                  hintText: 'Alınan tutar',
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  validator: _validateAmount,
                ),
              ),
              const SizedBox(height: 16),
              AppPrimaryButton(label: 'Tahsilatı Kaydet', onPressed: _submit),
            ],
          ),
        ),
      ),
    );
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final amount = double.parse(
      _amountController.text.trim().replaceAll(',', '.'),
    );
    Navigator.of(context).pop(amount);
  }

  String? _validateAmount(String? value) {
    if (value == null || value.trim().isEmpty) return 'Tutar girin.';
    final amount = double.tryParse(value.trim().replaceAll(',', '.'));
    if (amount == null || amount <= 0) return 'Geçerli bir tutar girin.';
    if (amount > _outstanding + 0.001) {
      return 'Kalan tutardan (${_money(_outstanding, widget.record.currency)}) fazla olamaz.';
    }
    return null;
  }

  static String _asInput(double amount) {
    return amount == amount.roundToDouble()
        ? amount.toStringAsFixed(0)
        : amount.toStringAsFixed(2);
  }
}

/// Kalanın tamamını tek dokunuşla dolduran belirgin, tam-genişlik hızlı seçim.
/// Tam tahsilat seçiliyken primary dolgu ile görsel geri bildirim verir.
class _FullAmountButton extends StatelessWidget {
  const _FullAmountButton({
    required this.amountLabel,
    required this.selected,
    required this.onTap,
  });

  final String amountLabel;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fg = selected ? Colors.white : AppColors.primary;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 160),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
          decoration: BoxDecoration(
            color: selected ? AppColors.primary : AppColors.primaryLight,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.skyBorder,
              width: 1.5,
            ),
          ),
          child: Row(
            children: <Widget>[
              Icon(
                selected
                    ? Icons.check_circle_rounded
                    : Icons.done_all_rounded,
                color: fg,
                size: 24,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Tamamını al',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: fg,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    Text(
                      'Kalanın tamamını tahsil et',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: selected
                            ? Colors.white.withValues(alpha: 0.85)
                            : AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              Text(
                amountLabel,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: fg,
                  fontWeight: FontWeight.w900,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.label,
    required this.value,
    this.emphasize = false,
  });

  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
            color: AppColors.textSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
        Text(
          value,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
            color: emphasize ? AppColors.primary : AppColors.textPrimary,
            fontWeight: FontWeight.w900,
          ),
        ),
      ],
    );
  }
}

String _money(double amount, String currency) {
  final formatted = NumberFormat.decimalPattern('tr_TR').format(amount);
  return '$formatted $currency';
}
