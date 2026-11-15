import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/paged_list_boundary.dart';
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

  String? _nextCursor;
  bool _loadingMore = false;
  String? _moreError;
  int _generation = 0;

  Future<void> _loadMore() async {
    if (_loadingMore || _loading || _nextCursor == null) return;
    final generation = _generation;
    setState(() {
      _loadingMore = true;
      _moreError = null;
    });
    try {
      final page = await _repo.listPage(
        studentId: _studentId,
        after: _nextCursor,
      );
      if (!mounted || generation != _generation) return;
      setState(() {
        final ids = _items.map((x) => x.id).toSet();
        _items = [..._items, ...page.items.where((x) => !ids.contains(x.id))];
        _nextCursor = page.nextCursor;
        _loadingMore = false;
      });
    } on ApiException catch (error) {
      if (!mounted || generation != _generation) return;
      setState(() {
        _loadingMore = false;
        _moreError = error.message;
      });
    }
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _loadingMore = false;
      _moreError = null;
      _nextCursor = null;
      _error = null;
    });
    try {
      final session = context.read<AuthCubit>().state.session;
      final studentId =
          _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final page = await _repo.listPage(studentId: studentId);
      if (!mounted || generation != _generation) return;
      setState(() {
        _studentId = studentId;
        _items = page.items;
        _nextCursor = page.nextCursor;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted && generation == _generation) {
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
      if (!mounted) return;
      _snack(
        AppLocalizations.of(context).p19list_solutionUploaded,
        success: true,
      );
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
      appBar: AppBar(
        title: Text(AppLocalizations.of(context).p19list_myAssignments),
      ),
      body: _loading
          ? LoadingStateView(
              message: AppLocalizations.of(context).p19list_loading,
            )
          : _error != null
          ? ErrorStateView(message: _error!, onRetry: _load)
          : _content(),
    );
  }

  Widget _content() {
    if (_items.isEmpty) {
      return EmptyStateView(
        title: AppLocalizations.of(context).p19list_noAssignment,
        subtitle: AppLocalizations.of(context).p19list_studentAssignmentHint,
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: PagedListBoundary(
        hasMore: _nextCursor != null,
        isLoading: _loadingMore,
        errorMessage: _moreError,
        onLoadMore: _loadMore,
        builder: (controller) => ListView.separated(
          controller: controller,
                  physics: const AlwaysScrollableScrollPhysics(),
          padding: EdgeInsets.fromLTRB(16, 12, 16, 24),
          itemCount: _items.length,
          separatorBuilder: (_, __) => SizedBox(height: 12),
          itemBuilder: (context, i) => _AssignmentCard(
            item: _items[i],
            busy: _busyId == _items[i].id,
            onComplete: () => _complete(_items[i]),
            onUpload: () => _upload(_items[i]),
          ),
        ),
      ),
    );
  }

  void _snack(String message, {bool success = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: success ? AppColors.accentGreen : AppColors.error,
      ),
    );
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

  bool get _isCompleted =>
      item.status == 'Completed' ||
      item.status == 'Cancelled' ||
      item.status == 'Approved';

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      padding: EdgeInsets.all(16),
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
              if (item.isGraded) ...<Widget>[
                _StudentScoreBadge(score: item.score!),
                SizedBox(width: 6),
              ],
              _StatusPill(status: item.status),
            ],
          ),
          if (item.description.trim().isNotEmpty) ...<Widget>[
            SizedBox(height: 6),
            Text(
              item.description,
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
            ),
          ],
          if (item.teacherFeedback != null &&
              item.teacherFeedback!.trim().isNotEmpty) ...<Widget>[
            SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.06),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                AppLocalizations.of(
                  context,
                ).p19list_teacherFeedback(item.teacherFeedback!.trim()),
                style: Theme.of(
                  context,
                ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
              ),
            ),
          ],
          if (item.attachmentUrl != null &&
              item.attachmentUrl!.trim().isNotEmpty) ...<Widget>[
            SizedBox(height: 8),
            Row(
              children: <Widget>[
                Icon(
                  Icons.attach_file_rounded,
                  size: 16,
                  color: AppColors.primary,
                ),
                SizedBox(width: 4),
                Text(
                  AppLocalizations.of(context).p19list_hasSolution,
                  style: TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ],
          if (!_isCompleted) ...<Widget>[
            SizedBox(height: 12),
            Row(
              children: <Widget>[
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : onUpload,
                    icon: Icon(Icons.upload_file_rounded, size: 18),
                    label: Text(
                      AppLocalizations.of(context).p19list_uploadSolution,
                    ),
                  ),
                ),
                SizedBox(width: 10),
                Expanded(
                  child: FilledButton.icon(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                    ),
                    onPressed: busy ? null : onComplete,
                    icon: busy
                        ? SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              valueColor: AlwaysStoppedAnimation<Color>(
                                Colors.white,
                              ),
                            ),
                          )
                        : Icon(Icons.check_rounded, size: 18),
                    label: Text(AppLocalizations.of(context).p19list_complete),
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
      'Completed' => (
        AppLocalizations.of(context).p19list_completed,
        AppColors.accentGreen,
      ),
      'InProgress' => (
        AppLocalizations.of(context).p19list_inProgress,
        AppColors.accentBlue,
      ),
      'Cancelled' => (
        AppLocalizations.of(context).p19list_cancelled,
        AppColors.textSecondary,
      ),
      'Approved' => (
        AppLocalizations.of(context).p19list_approved,
        AppColors.accentGreen,
      ),
      'ReturnedForRevision' => (
        AppLocalizations.of(context).p19list_revision,
        AppColors.accentOrange,
      ),
      _ => (
        AppLocalizations.of(context).p19list_waiting,
        AppColors.accentOrange,
      ),
    };
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w700,
          fontSize: 12,
        ),
      ),
    );
  }
}

/// M06-4: Öğrenci ödev kartındaki puan rozeti (0–100).
class _StudentScoreBadge extends StatelessWidget {
  const _StudentScoreBadge({required this.score});

  final int score;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.primary.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        AppLocalizations.of(context).p19list_scoreValue(score.toString()),
        style: TextStyle(
          color: AppColors.primary,
          fontWeight: FontWeight.w800,
          fontSize: 12,
        ),
      ),
    );
  }
}
