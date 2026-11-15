import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/network/cursor_page.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_cubit.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_state.dart';
import 'package:flutter_test/flutter_test.dart';

/// P04 Task 4 (M06-1): Ders kaynakları — listeleme, ekleme, arşivleme.
void main() {
  test('load kaynaklari doldurur', () async {
    final repository = _FakeAssignmentRepository(resources: [_link('r-1'), _file('r-2')]);
    final cubit = LessonResourcesCubit(repository);

    await cubit.load(teacherUserId: 't-1');

    expect(cubit.state.status, LessonResourcesStatus.loaded);
    expect(cubit.state.resources.length, 2);
    expect(repository.lastTeacherUserId, 't-1');
    await cubit.close();
  });

  test('API hatasinda failure durumuna gecer', () async {
    final cubit = LessonResourcesCubit(_FakeAssignmentRepository(failList: true));

    await cubit.load(teacherUserId: 't-1');

    expect(cubit.state.status, LessonResourcesStatus.failure);
    expect(cubit.state.errorMessage, isNotNull);
    await cubit.close();
  });

  test('addResource baglanti kaynagi olusturur ve listeyi tazeler', () async {
    final repository = _FakeAssignmentRepository(resources: [_link('r-1')]);
    final cubit = LessonResourcesCubit(repository);
    await cubit.load(teacherUserId: 't-1', lessonSessionId: 'l-1');

    final ok = await cubit.addResource(title: 'Video', url: 'https://ornek.com');

    expect(ok, isTrue);
    expect(repository.created.single.title, 'Video');
    expect(repository.created.single.url, 'https://ornek.com');
    // lessonSessionId load'dan tasinir
    expect(repository.created.single.lessonSessionId, 'l-1');
    expect(repository.listCallCount, 2);
    await cubit.close();
  });

  test('addResource hatasi durumda mesaja donusur', () async {
    final cubit = LessonResourcesCubit(_FakeAssignmentRepository(failCreate: true));

    final ok = await cubit.addResource(title: 'Video', url: 'https://ornek.com');

    expect(ok, isFalse);
    expect(cubit.state.errorMessage, isNotNull);
    expect(cubit.state.isSaving, isFalse);
    await cubit.close();
  });

  test('archiveResource listeyi iyimser gunceller', () async {
    final repository = _FakeAssignmentRepository(resources: [_link('r-1'), _link('r-2')]);
    final cubit = LessonResourcesCubit(repository);
    await cubit.load(teacherUserId: 't-1');

    final ok = await cubit.archiveResource('r-1');

    expect(ok, isTrue);
    expect(cubit.state.resources.map((r) => r.id), ['r-2']);
    expect(repository.archived, ['r-1']);
    await cubit.close();
  });

  test('arsivleme basarisiz olursa liste geri alinir', () async {
    final repository = _FakeAssignmentRepository(
      resources: [_link('r-1'), _link('r-2')],
      failArchive: true,
    );
    final cubit = LessonResourcesCubit(repository);
    await cubit.load(teacherUserId: 't-1');

    final ok = await cubit.archiveResource('r-1');

    expect(ok, isFalse);
    expect(cubit.state.resources.length, 2);
    expect(cubit.state.errorMessage, isNotNull);
    await cubit.close();
  });

  test('LessonResource.isFile turu ayirir', () {
    expect(_file('r-1').isFile, isTrue);
    expect(_link('r-2').isFile, isFalse);
  });
}

LessonResource _link(String id) => LessonResource(
  id: id,
  teacherUserId: 't-1',
  title: 'Video',
  kind: 'Link',
  url: 'https://ornek.com',
  createdOnUtc: DateTime.utc(2026, 9, 3),
);

LessonResource _file(String id) => LessonResource(
  id: id,
  teacherUserId: 't-1',
  title: 'Konu anlatimi',
  kind: 'File',
  fileName: 'konu.pdf',
  sizeBytes: 2048,
  downloadUrl: '/api/assignments/lesson-resources/$id/file',
  createdOnUtc: DateTime.utc(2026, 9, 3),
);

class _FakeAssignmentRepository implements AssignmentRepository {
  @override
  Future<CursorPage<AssignmentItem>> listPage({String? teacherUserId, String? studentId, String? after, int limit = 50}) async => const CursorPage(items: []);

  _FakeAssignmentRepository({
    this.resources = const <LessonResource>[],
    this.failList = false,
    this.failCreate = false,
    this.failArchive = false,
  });

  final List<LessonResource> resources;
  final bool failList;
  final bool failCreate;
  final bool failArchive;

  final List<({String title, String? url, String? lessonSessionId})> created = [];
  final List<String> archived = [];
  String? lastTeacherUserId;
  int listCallCount = 0;

  @override
  Future<LessonResource> createLessonResource({
    required String title,
    String? description,
    String? url,
    String? filePath,
    String? studentId,
    String? lessonSessionId,
  }) async {
    if (failCreate) {
      throw const ApiException(message: 'Kaynak eklenemedi.');
    }
    created.add((title: title, url: url, lessonSessionId: lessonSessionId));
    return _link('yeni');
  }

  @override
  Future<List<LessonResource>> listLessonResources({
    String? teacherUserId,
    String? studentId,
    String? lessonSessionId,
  }) async {
    listCallCount++;
    if (failList) {
      throw const ApiException(message: 'Ders kaynakları yüklenemedi.');
    }
    lastTeacherUserId = teacherUserId;
    return resources;
  }

  @override
  Future<void> archiveLessonResource(String resourceId) async {
    if (failArchive) {
      throw const ApiException(message: 'Silinemedi.');
    }
    archived.add(resourceId);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
