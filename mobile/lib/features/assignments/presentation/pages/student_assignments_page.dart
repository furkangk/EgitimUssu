import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Öğrencinin kendi ödevleri. Öğretmenin verdiği ödevleri listeler; öğrenci çözümünü (dosya)
/// yükler ve ödevi tamamlandı olarak işaretler. Sunucu sahiplik filtresini zorlar.
class StudentAssignmentsPage extends StatefulWidget {
  const StudentAssignmentsPage({super.key});

  @override
  State<StudentAssignmentsPage> createState() => _StudentAssignmentsPageState();
}

class _StudentAssignmentsPageState extends State<StudentAssignmentsPage> {
  final AssignmentRepository _repo = injector<AssignmentRepository>();

  bool _loading = true;
  String? _error;
  String? _studentId;
  List<AssignmentItem> _items = <AssignmentItem>[];
  String? _busyId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = context.read<AuthCubit>().state.session;
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final items = await _repo.listByStudent(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _items = items;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          _loading = false;
        });
      }
    }
  }

  Future<void> _complete(AssignmentItem item) async {
    if (item.id == null) return;
    setState(() => _busyId = item.id);
    try {
      await _repo.completeAssignment(item.id!);
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _upload(AssignmentItem item) async {
    if (item.id == null) return;
    final picked = await FilePicker.platform.pickFiles();
    final path = picked?.files.single.path;
    if (path == null) return;
    setState(() => _busyId = item.id);
    try {
      await _repo.submitWork(item.id!, path);
      _snack('Çözüm yüklendi.', success: true);
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Ödevlerim')),
      body: _loading
          ? const LoadingStateView(message: 'Yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
    );
  }

  Widget _content() {
    if (_items.isEmpty) {
      return const EmptyStateView(
        title: 'Ödev yok',
        subtitle: 'Öğretmenin sana ödev verdiğinde burada görünür.',
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        itemCount: _items.length,
        separatorBuilder: (_, __) => const SizedBox(height: 12),
        itemBuilder: (context, i) => _AssignmentCard(
          item: _items[i],
          busy: _busyId == _items[i].id,
          onComplete: () => _complete(_items[i]),
          onUpload: () => _upload(_items[i]),
        ),
      ),
    );
  }

  void _snack(String message, {bool success = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: success ? AppColors.accentGreen : AppColors.error,
    ));
  }
}

class _AssignmentCard extends StatelessWidget {
  const _AssignmentCard({
    required this.item,
    required this.busy,
    required this.onComplete,
    required this.onUpload,
  });

  final AssignmentItem item;
  final bool busy;
  final VoidCallback onComplete;
  final VoidCallback onUpload;

  bool get _isCompleted => item.status == 'Completed' || item.status == 'Cancelled';

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  item.title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w800,
                      ),
                ),
              ),
              _StatusPill(status: item.status),
            ],
          ),
          if (item.description.trim().isNotEmpty) ...<Widget>[
            const SizedBox(height: 6),
            Text(
              item.description,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
            ),
          ],
          if (item.attachmentUrl != null &&
              item.attachmentUrl!.trim().isNotEmpty) ...<Widget>[
            const SizedBox(height: 8),
            Row(
              children: const <Widget>[
                Icon(Icons.attach_file_rounded,
                    size: 16, color: AppColors.primary),
                SizedBox(width: 4),
                Text(
                  'Çözüm yüklendi',
                  style: TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ],
          if (!_isCompleted) ...<Widget>[
            const SizedBox(height: 12),
            Row(
              children: <Widget>[
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : onUpload,
                    icon: const Icon(Icons.upload_file_rounded, size: 18),
                    label: const Text('Çözüm yükle'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton.icon(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                    ),
                    onPressed: busy ? null : onComplete,
                    icon: busy
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              valueColor:
                                  AlwaysStoppedAnimation<Color>(Colors.white),
                            ),
                          )
                        : const Icon(Icons.check_rounded, size: 18),
                    label: const Text('Tamamla'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.status});

  final String? status;

  @override
  Widget build(BuildContext context) {
    final (String label, Color color) = switch (status) {
      'Completed' => ('Tamamlandı', AppColors.accentGreen),
      'InProgress' => ('Devam', AppColors.accentBlue),
      'Cancelled' => ('İptal', AppColors.textSecondary),
      _ => ('Bekliyor', AppColors.accentOrange),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 12),
      ),
    );
  }
}
