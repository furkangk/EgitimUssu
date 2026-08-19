import 'package:egitim_ussu_mobile/features/study/presentation/lessons/lesson_detail_permissions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('kendi dersinde tüm ekleme açık', () {
    final p = LessonDetailPermissions.forOwnership(true);
    expect(p.canAddHomework, isTrue);
    expect(p.canAddTopic, isTrue);
    expect(p.canAddNote, isTrue);
    expect(p.canAddTest, isTrue);
  });

  test('öğretmen dersinde ödev/konu ekle kapalı; not/test açık', () {
    final p = LessonDetailPermissions.forOwnership(false);
    expect(p.canAddHomework, isFalse);
    expect(p.canAddTopic, isFalse);
    expect(p.canAddNote, isTrue);
    expect(p.canAddTest, isTrue);
  });
}
