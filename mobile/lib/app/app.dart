import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/push/push_route_mapping.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/core/routing/route_access.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/stale_data_banner.dart';
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
    this.membershipCubit,
    this.pushTaps,
  });

  final AuthCubit? authCubit;
  final AppRouter? appRouter;
  final NotificationsCubit? notificationsCubit;

  /// D-10: Premium kilidinin tek doğruluk kaynağı; uygulama kökünde tek örnek yaşar.
  final MembershipCubit? membershipCubit;

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
  late final MembershipCubit _membershipCubit;
  StreamSubscription<PushTap>? _pushTapSubscription;
  PushTap? _pendingPushTap;
  bool _pushNavigationScheduled = false;

  @override
  void initState() {
    super.initState();
    _authCubit = widget.authCubit ?? AuthCubit.create();
    _appRouter = widget.appRouter ?? AppRouter(authCubit: _authCubit);
    _notificationsCubit =
        widget.notificationsCubit ?? NotificationsCubit.create();
    _membershipCubit = widget.membershipCubit ?? MembershipCubit.create();
    _authCubit.restoreSession();
    _listenToPushTaps();
  }

  /// Cold-start taps wait until authentication and the router are ready.
  void _listenToPushTaps() {
    final taps =
        widget.pushTaps ??
        (injector.isRegistered<PushService>()
            ? injector<PushService>().taps
            : null);

    _pushTapSubscription = taps?.listen((tap) {
      _pendingPushTap = tap;
      _openPendingPush();
    });
  }

  void _openPendingPush() {
    if (_pendingPushTap == null ||
        _pushNavigationScheduled ||
        _authCubit.state.status != AuthStatus.authenticated) {
      return;
    }
    _pushNavigationScheduled = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _pushNavigationScheduled = false;
      if (!mounted || _authCubit.state.status != AuthStatus.authenticated) {
        return;
      }
      final tap = _pendingPushTap;
      if (tap == null) return;
      _pendingPushTap = null;
      final role = RouteAccess.roleFor(
        _authCubit.state.session?.roles ?? const [],
      );
      final target = mapPushRoute(tap.route, role);
      if (_appRouter.router.state.uri.toString() != target) {
        _appRouter.router.push(target);
      }
    });
    WidgetsBinding.instance.ensureVisualUpdate();
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
    if (widget.membershipCubit == null) {
      _membershipCubit.close();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: <BlocProvider<dynamic>>[
        BlocProvider<AuthCubit>.value(value: _authCubit),
        BlocProvider<NotificationsCubit>.value(value: _notificationsCubit),
        BlocProvider<MembershipCubit>.value(value: _membershipCubit),
      ],
      child: BlocListener<AuthCubit, AuthState>(
        listenWhen: (previous, current) =>
            previous.session?.userId != current.session?.userId,
        listener: (context, state) {
          _notificationsCubit.reset();
          _membershipCubit.reset();
          final session = state.session;
          if (session != null) {
            _openPendingPush();
            _notificationsCubit.load(session.userId);
            // D-10: Oturum değişince üyelik ve rolün plan kataloğu tazelenir;
            // `PremiumLock` kararını buradan okur.
            _membershipCubit.load(role: membershipRoleFor(session.roles));
          } else {
            _pendingPushTap = null;
          }
        },
        child: MaterialApp.router(
          // D-19: Başlık da ARB'den gelir — görünen ad her zaman "EğitimÜssü".
          onGenerateTitle: (context) => AppLocalizations.of(context).app_title,
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
            AppLocalizations.delegate,
          ],
          builder: (context, child) {
            if (!injector.isRegistered<CacheStore>()) return child!;
            return ValueListenableBuilder<DateTime?>(
              valueListenable: injector<CacheStore>().staleSince,
              builder: (context, savedAt, _) => Column(
                children: [
                  if (savedAt != null) StaleDataBanner(savedAt: savedAt),
                  Expanded(child: child!),
                ],
              ),
            );
          },
          theme: AppTheme.lightTheme,
          routerConfig: _appRouter.router,
        ),
      ),
    );
  }
}
