import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/paged_list_boundary.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

class StudyHistoryPage extends StatefulWidget {
  const StudyHistoryPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<StudyHistoryPage> createState() => _StudyHistoryPageState();
}

class _StudyHistoryPageState extends State<StudyHistoryPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  List<StudySession> _sessions = [];
  List<TestResult> _tests = [];
  WeeklySummary? _weekly;
  bool _loading = true;
  String? _error;

  String? _sessionCursor;
  String? _testCursor;
  bool _sessionsMore = false;
  bool _testsMore = false;
  String? _sessionsError;
  String? _testsError;
  int _generation = 0;

  Future<void> _loadMore(bool sessions) async {
    final cursor = sessions ? _sessionCursor : _testCursor;
    if (_loading || (sessions ? _sessionsMore : _testsMore) || cursor == null) {
      return;
    }
    final generation = _generation;
    setState(() {
      if (sessions) {
        _sessionsMore = true;
        _sessionsError = null;
      } else {
        _testsMore = true;
        _testsError = null;
      }
    });
    try {
      if (sessions) {
        final page = await _repo.listSessionsPage(
          widget.studentId,
          after: cursor,
        );
        if (!mounted || generation != _generation) return;
        setState(() {
          final ids = _sessions.map((x) => x.id).toSet();
          _sessions = [
            ..._sessions,
            ...page.items.where(
              (x) => x.status == 'Completed' && !ids.contains(x.id),
            ),
          ];
          _sessionCursor = page.nextCursor;
          _sessionsMore = false;
        });
      } else {
        final page = await _repo.listTestsPage(widget.studentId, after: cursor);
        if (!mounted || generation != _generation) return;
        setState(() {
          final ids = _tests.map((x) => x.id).toSet();
          _tests = [..._tests, ...page.items.where((x) => !ids.contains(x.id))];
          _testCursor = page.nextCursor;
          _testsMore = false;
        });
      }
    } on ApiException catch (error) {
      if (!mounted || generation != _generation) return;
      setState(() {
        if (sessions) {
          _sessionsMore = false;
          _sessionsError = error.message;
        } else {
          _testsMore = false;
          _testsError = error.message;
        }
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
      _sessionsMore = false;
      _testsMore = false;
      _sessionsError = null;
      _testsError = null;
      _error = null;
    });
    try {
      final sessions = await _repo.listSessionsPage(widget.studentId);
      final tests = await _repo.listTestsPage(widget.studentId);
      final weekly = await _repo.getWeeklySummary(widget.studentId);
      if (!mounted || generation != _generation) return;
      setState(() {
        _sessionCursor = sessions.nextCursor;
        _testCursor = tests.nextCursor;
        _sessions = sessions.items
            .where((s) => s.status == 'Completed')
            .toList();
        _tests = tests.items;
        _weekly = weekly;
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

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 3,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: Text(AppLocalizations.of(context).p19list_history),
          bottom: TabBar(
            tabs: [
              Tab(text: AppLocalizations.of(context).p19list_sessions),
              Tab(text: AppLocalizations.of(context).p19list_tests),
              Tab(text: AppLocalizations.of(context).p19list_weekly),
            ],
          ),
        ),
        floatingActionButton: FloatingActionButton.extended(
          onPressed: _openManualSheet,
          icon: Icon(Icons.add),
          label: Text(AppLocalizations.of(context).p19list_manualSession),
        ),
        body: _loading
            ? LoadingStateView()
            : _error != null
            ? ErrorStateView(message: _error!, onRetry: _load)
            : TabBarView(children: [_sessionsTab(), _testsTab(), _weeklyTab()]),
      ),
    );
  }

  Widget _sessionsTab() {
    if (_sessions.isEmpty && _sessionCursor == null) {
      return EmptyStateView(
        title: AppLocalizations.of(context).p19list_noSessions,
        subtitle: AppLocalizations.of(context).p19list_startSessionHint,
      );
    }
    return PagedListBoundary(
      hasMore: _sessionCursor != null,
      isLoading: _sessionsMore,
      errorMessage: _sessionsError,
      onLoadMore: () => _loadMore(true),
      builder: (controller) => ListView.builder(
        controller: controller,
                  physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.all(16),
        itemCount: _sessions.length,
        itemBuilder: (context, i) {
          final s = _sessions[i];
          return Container(
            margin: EdgeInsets.only(bottom: 10),
            padding: EdgeInsets.all(14),
            decoration: _cardDeco,
            child: Row(
              children: [
                CircleAvatar(
                  backgroundColor: AppColors.primaryLight,
                  child: Icon(
                    s.source == 'Manual' ? Icons.edit : Icons.timer,
                    color: AppColors.primary,
                    size: 20,
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        s.topic == null
                            ? s.subject
                            : '${s.subject} · ${s.topic}',
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      Text(
                        _dateLabel(s.endedAtUtc ?? s.startedAtUtc),
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
                Text(
                  StudyFormat.minutes(s.effectiveMinutes),
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _testsTab() {
    if (_tests.isEmpty && _testCursor == null) {
      return EmptyStateView(
        title: AppLocalizations.of(context).p19list_noTests,
        subtitle: AppLocalizations.of(context).p19list_startTestHint,
      );
    }
    return PagedListBoundary(
      hasMore: _testCursor != null,
      isLoading: _testsMore,
      errorMessage: _testsError,
      onLoadMore: () => _loadMore(false),
      builder: (controller) => ListView.builder(
        controller: controller,
                  physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.all(16),
        itemCount: _tests.length,
        itemBuilder: (context, i) {
          final t = _tests[i];
          return Container(
            margin: EdgeInsets.only(bottom: 10),
            padding: EdgeInsets.all(14),
            decoration: _cardDeco,
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        t.testName ?? t.subject,
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      Text(
                        AppLocalizations.of(context).p19list_testCounts(
                          t.subject,
                          t.correct.toString(),
                          t.wrong.toString(),
                          t.blank.toString(),
                        ),
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                      Text(
                        _dateLabel(t.takenOnUtc),
                        style: TextStyle(
                          color: AppColors.textMuted,
                          fontSize: 11,
                        ),
                      ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      StudyFormat.net(t.net),
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        color: AppColors.primary,
                        fontSize: 18,
                      ),
                    ),
                    Text(
                      AppLocalizations.of(context).p19list_net,
                      style: TextStyle(
                        color: AppColors.textMuted,
                        fontSize: 11,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _weeklyTab() {
    final w = _weekly;
    if (w == null || w.perDay.isEmpty) {
      return EmptyStateView(
        title: AppLocalizations.of(context).p19list_noData,
        subtitle: AppLocalizations.of(context).p19list_noStudyThisWeek,
      );
    }
    final maxMinutes = w.perDay.fold<int>(
      1,
      (m, d) => d.effectiveMinutes > m ? d.effectiveMinutes : m,
    );
    final dayLabels = List.generate(
      7,
      (i) => DateFormat.E(
        Localizations.localeOf(context).toString(),
      ).format(DateTime(2026, 11, 9 + i)),
    );
    return ListView(
      padding: EdgeInsets.all(16),
      children: [
        Container(
          padding: EdgeInsets.all(16),
          decoration: _cardDeco,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                AppLocalizations.of(context).p19list_weekTotal(
                  StudyFormat.minutes(w.totalEffectiveMinutes),
                ),
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              Text(
                AppLocalizations.of(
                  context,
                ).p19list_sessionCount(w.sessionCount.toString()),
                style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
              ),
              SizedBox(height: 16),
              SizedBox(
                height: 140,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: List.generate(w.perDay.length, (i) {
                    final d = w.perDay[i];
                    final h = (d.effectiveMinutes / maxMinutes) * 110;
                    return Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          Text(
                            d.effectiveMinutes > 0
                                ? '${d.effectiveMinutes}'
                                : '',
                            style: TextStyle(
                              fontSize: 10,
                              color: AppColors.textMuted,
                            ),
                          ),
                          SizedBox(height: 4),
                          Container(
                            margin: EdgeInsets.symmetric(horizontal: 4),
                            height: h < 4 ? 4 : h,
                            decoration: BoxDecoration(
                              color: d.effectiveMinutes > 0
                                  ? AppColors.primary
                                  : AppColors.divider,
                              borderRadius: BorderRadius.circular(6),
                            ),
                          ),
                          SizedBox(height: 6),
                          Text(
                            i < dayLabels.length ? dayLabels[i] : '',
                            style: TextStyle(
                              fontSize: 11,
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    );
                  }),
                ),
              ),
            ],
          ),
        ),
        SizedBox(height: 16),
        if (w.perSubject.isNotEmpty) ...[
          Text(
            AppLocalizations.of(context).p19list_subjectDistribution,
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          SizedBox(height: 8),
          ...w.perSubject.map(
            (s) => Padding(
              padding: EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      s.subject,
                      style: TextStyle(color: AppColors.textPrimary),
                    ),
                  ),
                  Text(
                    StudyFormat.minutes(s.effectiveMinutes),
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }

  Future<void> _openManualSheet() async {
    final subject = TextEditingController();
    final topic = TextEditingController();
    final minutes = TextEditingController();
    var busy = false;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheet) {
            return Padding(
              padding: EdgeInsets.only(
                left: 16,
                right: 16,
                top: 16,
                bottom: MediaQuery.of(context).viewInsets.bottom + 16,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    AppLocalizations.of(context).p19list_addManual,
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                  ),
                  SizedBox(height: 12),
                  TextField(
                    controller: subject,
                    decoration: InputDecoration(
                      labelText: AppLocalizations.of(
                        context,
                      ).p19list_subjectRequired,
                      border: OutlineInputBorder(),
                    ),
                  ),
                  SizedBox(height: 10),
                  TextField(
                    controller: topic,
                    decoration: InputDecoration(
                      labelText: AppLocalizations.of(
                        context,
                      ).p19list_topicOptional,
                      border: OutlineInputBorder(),
                    ),
                  ),
                  SizedBox(height: 10),
                  TextField(
                    controller: minutes,
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(
                      labelText: AppLocalizations.of(
                        context,
                      ).p19list_minutesRequired,
                      border: OutlineInputBorder(),
                    ),
                  ),
                  SizedBox(height: 16),
                  AppPrimaryButton(
                    label: AppLocalizations.of(context).p19list_add,
                    isLoading: busy,
                    onPressed: () async {
                      final subj = subject.text.trim();
                      final mins = int.tryParse(minutes.text.trim()) ?? 0;
                      if (subj.isEmpty || mins <= 0) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            content: Text(
                              AppLocalizations.of(
                                context,
                              ).p19list_subjectMinutesRequired,
                            ),
                          ),
                        );
                        return;
                      }
                      setSheet(() => busy = true);
                      try {
                        await _repo.createManualSession(
                          widget.studentId,
                          subject: subj,
                          topic: topic.text.trim().isEmpty
                              ? null
                              : topic.text.trim(),
                          effectiveMinutes: mins,
                          studiedOnUtc: DateTime.now().toUtc(),
                        );
                        if (sheetContext.mounted) {
                          Navigator.of(sheetContext).pop();
                        }
                        await _load();
                      } on ApiException catch (e) {
                        setSheet(() => busy = false);
                        if (sheetContext.mounted) {
                          ScaffoldMessenger.of(
                            context,
                          ).showSnackBar(SnackBar(content: Text(e.message)));
                        }
                      }
                    },
                  ),
                ],
              ),
            );
          },
        );
      },
    );
    subject.dispose();
    topic.dispose();
    minutes.dispose();
  }

  static final BoxDecoration _cardDeco = BoxDecoration(
    color: AppColors.surface,
    borderRadius: BorderRadius.circular(14),
    border: Border.all(color: AppColors.border),
  );

  String _dateLabel(DateTime utc) {
    return DateFormat.yMd(
      Localizations.localeOf(context).toString(),
    ).add_Hm().format(utc.toLocal());
  }
}
