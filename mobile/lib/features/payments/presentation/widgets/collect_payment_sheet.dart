import 'package:egitim_ussu_mobile/core/money/money.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';

/// "Tahsil Et" akışının formu. Öğretmen bu tahsilatta **ne kadar** aldığını girer;
/// varsayılan kalan tutardır (tamamı). Daha azını girerse kısmi tahsilat olur.
/// Onaylanırsa bu işlemde tahsil edilecek tutarı (double) döndürür; iptalde `null`.
class CollectPaymentSheet extends StatefulWidget {
  const CollectPaymentSheet({super.key, required this.record});

  final PaymentRecord record;

  static Future<int?> show(BuildContext context, PaymentRecord record) {
    return showModalBottomSheet<int>(
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

  int get _outstanding => widget.record.outstandingAmount;

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
    try {
      return Money.parseTr(_amountController.text).minor == _outstanding;
    } on FormatException {
      return false;
    }
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
          child: SingleChildScrollView(
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
                  AppLocalizations.of(context).finance_tahsilatKaydi,
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
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 16),
                _InfoRow(
                  label: AppLocalizations.of(context).finance_beklenen,
                  value: _money(record.expectedAmount, record.currency),
                ),
                const SizedBox(height: 8),
                _InfoRow(
                  label: AppLocalizations.of(context).finance_tahsilEdilen,
                  value: _money(record.collectedAmount, record.currency),
                ),
                const SizedBox(height: 8),
                _InfoRow(
                  label: AppLocalizations.of(context).finance_kalan,
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
                    Flexible(
                      flex: 3,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        child: Text(
                          AppLocalizations.of(
                            context,
                          ).finance_veyaKismiTutarGir,
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.labelMedium
                              ?.copyWith(
                                color: AppColors.textSecondary,
                                fontWeight: FontWeight.w600,
                              ),
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
                    labelText: AppLocalizations.of(
                      context,
                    ).finance_collectionCurrency(record.currency),
                    hintText: AppLocalizations.of(context).finance_alinanTutar,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    validator: _validateAmount,
                  ),
                ),
                const SizedBox(height: 16),
                AppPrimaryButton(
                  label: AppLocalizations.of(context).finance_tahsilatiKaydet,
                  onPressed: _submit,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final amount = Money.parseTr(_amountController.text).minor;
    Navigator.of(context).pop(amount);
  }

  String? _validateAmount(String? value) {
    if (value == null || value.trim().isEmpty) {
      return AppLocalizations.of(context).finance_tutarGirin;
    }
    final int amount;
    try {
      amount = Money.parseTr(value).minor;
    } on FormatException {
      return AppLocalizations.of(context).finance_gecerliBirTutarGirin;
    }
    if (amount <= 0) {
      return AppLocalizations.of(context).finance_gecerliBirTutarGirin;
    }
    if (amount > _outstanding) {
      return AppLocalizations.of(context).finance_kalanTutardanFazlaOlamaz;
    }
    return null;
  }

  static String _asInput(int amount) =>
      Money(amount).formatTr(withCurrency: false);
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
                selected ? Icons.check_circle_rounded : Icons.done_all_rounded,
                color: fg,
                size: 24,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      AppLocalizations.of(context).finance_tamaminiAl,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: fg,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    Text(
                      AppLocalizations.of(
                        context,
                      ).finance_kalaninTamaminiTahsilEt,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: selected
                            ? Colors.white.withValues(alpha: 0.85)
                            : AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 6),
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
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      spacing: 12,
      runSpacing: 4,
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

String _money(int amount, String currency) =>
    Money(amount, currency).formatTr();
