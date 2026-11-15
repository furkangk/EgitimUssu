import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class RouteNotFoundPage extends StatelessWidget {
  const RouteNotFoundPage({super.key, required this.home});
  final String home;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(l10n.route_notFound),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => context.go(home),
                child: Text(l10n.route_backHome),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
