import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/push/firebase_push_binding.dart';
import 'package:flutter/widgets.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // A-04: Firebase yoksa uygulama yine açılır; uyarı log'lanır ve push devre dışı kalır.
  final firebaseAvailable = await FirebasePushBinding.initializeFirebase();
  await configureDependencies(firebaseAvailable: firebaseAvailable);
  runApp(const EgitimUssuApp());
}
