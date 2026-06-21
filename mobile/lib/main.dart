import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:flutter/widgets.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await configureDependencies();
  runApp(const EgitimUssuApp());
}
