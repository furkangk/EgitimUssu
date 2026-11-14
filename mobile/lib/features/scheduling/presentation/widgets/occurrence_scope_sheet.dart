import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';

/// P17 T1 / N-01 (K-20): tekrarlı derste iptal/erteleme öncesi kapsam seçimi —
/// "Yalnız bu ders" (varsayılan) / "Bu ve sonrakiler" / "Tüm seri".
///
/// Eskiden kapsam sorulmuyordu ve sunucu isteği "tüm seri" sayıyordu: haftalık dersin
/// bu haftası iptal edilince bütün seri iptal oluyordu. Seçim `null` dönerse kullanıcı
/// vazgeçmiştir. Ekran girişi: iptal P17 T5, erteleme P21 T4.
Future<OccurrenceScope?> showOccurrenceScopeSheet(BuildContext context) {
  return showModalBottomSheet<OccurrenceScope>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.white,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
    ),
    builder: (_) => const OccurrenceScopeSheet(),
  );
}

class OccurrenceScopeSheet extends StatefulWidget {
  const OccurrenceScopeSheet({super.key});

  @override
  State<OccurrenceScopeSheet> createState() => _OccurrenceScopeSheetState();
}

class _OccurrenceScopeSheetState extends State<OccurrenceScopeSheet> {
  OccurrenceScope _scope = OccurrenceScope.single;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final options = <(OccurrenceScope, String, String)>[
      (
        OccurrenceScope.single,
        l10n.scheduling_scopeSingle,
        l10n.scheduling_scopeSingleHint,
      ),
      (
        OccurrenceScope.thisAndFuture,
        l10n.scheduling_scopeThisAndFuture,
        l10n.scheduling_scopeThisAndFutureHint,
      ),
      (
        OccurrenceScope.all,
        l10n.scheduling_scopeAll,
        l10n.scheduling_scopeAllHint,
      ),
    ];

    return SafeArea(
      child: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                l10n.scheduling_scopeTitle,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(height: 8),
              RadioGroup<OccurrenceScope>(
                groupValue: _scope,
                onChanged: (value) => setState(() => _scope = value ?? _scope),
                child: Column(
                  children: <Widget>[
                    for (final (scope, title, hint) in options)
                      RadioListTile<OccurrenceScope>(
                        key: ValueKey<OccurrenceScope>(scope),
                        contentPadding: EdgeInsets.zero,
                        activeColor: AppColors.primary,
                        value: scope,
                        title: Text(title),
                        subtitle: Text(hint),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: Text(l10n.common_cancel),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.primary,
                      ),
                      onPressed: () => Navigator.of(context).pop(_scope),
                      child: Text(l10n.common_continue),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
