import 'package:flutter_bloc/flutter_bloc.dart';

/// Drops late UI updates after disposal. Request/session generation checks
/// remain the caller's responsibility; this does not cancel network work.
mixin SafeEmit<S> on Cubit<S> {
  void safeEmit(S state) {
    if (!isClosed) emit(state);
  }
}
