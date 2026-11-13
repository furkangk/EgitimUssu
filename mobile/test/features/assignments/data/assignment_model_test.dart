import 'package:egitim_ussu_mobile/features/assignments/data/models/assignment_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final entry in {
    'Private': 1,
    'Student': 2,
    'StudentAndParent': 3,
  }.entries) {
    test('preserves ${entry.key} note visibility when saving', () {
      final model = FollowUpAssignmentModel.fromJson({
        'lessonSessionId': 'session-id',
        'note': {'summary': 'Not', 'visibility': entry.key},
        'assignments': <dynamic>[],
      });
      expect(model.visibility, entry.key);
      expect(model.toCreatePayload()['visibility'], entry.value);
    });
  }

  test('maps follow-up response and create payload', () {
    final model = FollowUpAssignmentModel.fromJson(<String, dynamic>{
      'lessonSessionId': 'session-id',
      'note': <String, dynamic>{
        'summary': 'Ders ozeti',
        'coveredTopics': 'Problemler',
        'recommendations': 'Tekrar onerisi',
      },
      'assignments': <Map<String, dynamic>>[
        <String, dynamic>{
          'title': '20 soru',
          'description': 'Problemler testi',
          'status': 'Pending',
        },
      ],
    });

    expect(model.summary, 'Ders ozeti');
    expect(model.assignments.single.status, 'Pending');
    expect(model.toCreatePayload()['assignments'], isA<List<dynamic>>());
  });
}
