import 'package:egitim_ussu_mobile/features/assignments/data/models/assignment_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
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
