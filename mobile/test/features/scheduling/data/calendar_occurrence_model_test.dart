import 'package:egitim_ussu_mobile/features/scheduling/data/models/study_schedule_models.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _payload({bool? conflictsWithLesson}) {
  return <String, dynamic>{
    'entryId': 'entry-id',
    'source': 'Self',
    'subject': 'Matematik',
    'startAtUtc': '2026-05-12T15:00:00Z',
    'endAtUtc': '2026-05-12T16:00:00Z',
    'isEditable': true,
    if (conflictsWithLesson != null) 'conflictsWithLesson': conflictsWithLesson,
  };
}

void main() {
  test('M04-2: cakisma bayragi yanittan okunur', () {
    final occurrence = CalendarOccurrenceModel.fromJson(
      _payload(conflictsWithLesson: true),
    );

    expect(occurrence.conflictsWithLesson, isTrue);
    expect(occurrence.isSelf, isTrue);
  });

  test('cakisma bayragi yoksa varsayilan false', () {
    expect(
      CalendarOccurrenceModel.fromJson(_payload()).conflictsWithLesson,
      isFalse,
    );
    expect(
      CalendarOccurrenceModel.fromJson(
        _payload(conflictsWithLesson: false),
      ).conflictsWithLesson,
      isFalse,
    );
  });
}
