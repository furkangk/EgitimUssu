import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:syncfusion_localizations/syncfusion_localizations.dart';

class EgitimUssuApp extends StatefulWidget {
  const EgitimUssuApp({super.key, this.authCubit, this.appRouter});

  final AuthCubit? authCubit;
  final AppRouter? appRouter;

  @override
  State<EgitimUssuApp> createState() => _EgitimUssuAppState();
}

class _EgitimUssuAppState extends State<EgitimUssuApp> {
  late final AuthCubit _authCubit;
  late final AppRouter _appRouter;

  @override
  void initState() {
    super.initState();
    _authCubit = widget.authCubit ?? AuthCubit.create();
    _appRouter = widget.appRouter ?? AppRouter(authCubit: _authCubit);
    _authCubit.restoreSession();
  }

  @override
  void dispose() {
    if (widget.appRouter == null) {
      _appRouter.dispose();
    }
    if (widget.authCubit == null) {
      _authCubit.close();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider<AuthCubit>.value(
      value: _authCubit,
      child: MaterialApp.router(
        title: 'EgitimUssu',
        debugShowCheckedModeBanner: false,
        locale: const Locale('tr', 'TR'),
        supportedLocales: const <Locale>[
          Locale('tr', 'TR'),
          Locale('en', 'US'),
        ],
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
          SfGlobalLocalizations.delegate,
        ],
        theme: AppTheme.lightTheme,
        routerConfig: _appRouter.router,
      ),
    );
  }
}
