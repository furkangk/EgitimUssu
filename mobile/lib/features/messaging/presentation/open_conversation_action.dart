import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// D-08: "Mesaj gönder" düğmelerinin ortak davranışı — sohbeti aç (get-or-create), sonra git.
///
/// Sunucu aynı çift + öğrenci bağlamı için tek sohbet tuttuğundan bu çağrı ikinci kez
/// basıldığında yeni satır açmaz, var olanı döner.
///
/// [counterpartUserId] öğretmen bir **öğrenciye** yazarken boş bırakılabilir: istemci
/// öğrencinin kullanıcı kimliğini bilmez, sunucu onu [studentId]'den çözer.
Future<void> openConversationAndGo(
  BuildContext context, {
  required String counterpartRole,
  required String studentId,
  String counterpartUserId = '',
  MessagingRepository? repository,
}) async {
  final messenger = ScaffoldMessenger.of(context);
  final repo = repository ?? injector<MessagingRepository>();

  try {
    final detail = await repo.openConversation(
      counterpartUserId: counterpartUserId.isEmpty
          ? '00000000-0000-0000-0000-000000000000'
          : counterpartUserId,
      counterpartRole: counterpartRole,
      studentId: studentId,
    );
    if (!context.mounted) {
      return;
    }
    await context.push('/messages/${detail.id}');
  } on ApiException catch (error) {
    // Sessiz başarısızlık yok: ilişki yoksa (409 `messaging.relationship_required`)
    // kullanıcı nedenini görür.
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(error.message),
          backgroundColor: AppColors.accentRed,
        ),
      );
  }
}
