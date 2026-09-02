import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:syncfusion_localizations/syncfusion_localizations.dart';

class EgitimUssuApp extends StatefulWidget {
  const EgitimUssuApp({
    super.key,
    this.authCubit,
    this.appRouter,
    this.notificationsCubit,
    this.pushTaps,
  });

  final AuthCubit? authCubit;
  final AppRouter? appRouter;
  final NotificationsCubit? notificationsCubit;

  /// A-04: bildirime dokunma akışı. Verilmezse DI'daki [PushService] kullanılır
  /// (testlerde Firebase'e bağlanmamak için dışarıdan verilebilir).
  final Stream<PushTap>? pushTaps;

  @override
  State<EgitimUssuApp> createState() => _EgitimUssuAppState();
}

class _EgitimUssuAppState extends State<EgitimUssuApp> {
  late final AuthCubit _authCubit;
  late final AppRouter _appRouter;
  late final NotificationsCubit _notificationsCubit;
  StreamSubscription<PushTap>? _pushTapSubscription;

  @override
  void initState() {
    super.initState();
    _authCubit = widget.authCubit ?? AuthCubit.create();
    _appRouter = widget.appRouter ?? AppRouter(authCubit: _authCubit);
    _notificationsCubit =
        widget.notificationsCubit ?? NotificationsCubit.create();
    _authCubit.restoreSession();
    _listenToPushTaps();
  }

  /// Bildirime dokunulunca `data.route` varsa o ekrana gidilir (derin bağlantı).
  void _listenToPushTaps() {
    final taps =
        widget.pushTaps ??
        (injector.isRegistered<PushService>()
            ? injector<PushService>().taps
            : null);

    _pushTapSubscription = taps?.listen((tap) {
      final route = tap.route;
      if (route != null && route.isNotEmpty) {
        _appRouter.router.push(route);
      }
    });
  }

  @override
  void dispose() {
    _pushTapSubscription?.cancel();
    if (widget.appRouter == null) {
      _appRouter.dispose();
    }
    if (widget.authCubit == null) {
      _authCubit.close();
    }
    if (widget.notificationsCubit == null) {
      _notificationsCubit.close();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: <BlocProvider<dynamic>>[
        BlocProvider<AuthCubit>.value(value: _authCubit),
        BlocProvider<NotificationsCubit>.value(value: _notificationsCubit),
      ],
      child: BlocListener<AuthCubit, AuthState>(
        listenWhen: (previous, current) =>
            previous.session?.userId != current.session?.userId,
        listener: (context, state) {
          final userId = state.session?.userId;
          if (userId != null) {
            _notificationsCubit.load(userId);
          }
        },
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
      ),
    );
  }
}
