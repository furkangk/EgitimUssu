import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class ParentChildrenPage extends StatelessWidget {
  const ParentChildrenPage({super.key});

  @override
  Widget build(BuildContext context) {
    final userId = context.select(
      (AuthCubit c) => c.state.session?.userId ?? 'mock-parent-user',
    );
    final fullName = context.select(
      (AuthCubit c) => c.state.session?.fullName ?? 'Veli',
    );

    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..load(userId, fullName: fullName),
      child: Scaffold(
        backgroundColor: AppColors.background,
        bottomNavigationBar: const ParentBottomNav(
          current: ParentNavTab.children,
        ),
        floatingActionButton: Builder(
          builder: (context) => FloatingActionButton.extended(
            backgroundColor: AppColors.primary,
            onPressed: () => _openLinkSheet(context, userId),
            icon: const Icon(Icons.person_add_alt_1_rounded),
            label: const Text('Çocuk bağla'),
          ),
        ),
        body: SafeArea(
          child: Column(
            children: <Widget>[
              const ParentHeader(
                title: 'Çocuklarım',
                subtitle: 'Bağlı çocuklar ve bağ durumları',
              ),
              Expanded(
                child: BlocBuilder<ParentCubit, ParentState>(
                  builder: (context, state) {
                    if (state.isLoading) {
                      return const LoadingStateView();
                    }
                    if (state.status == ParentStatus.error) {
                      return Padding(
                        padding: const EdgeInsets.all(20),
                        child: ErrorStateView(
                          message: state.errorMessage ?? 'Bir hata oluştu.',
                          onRetry: () =>
                              context.read<ParentCubit>().load(userId),
                        ),
                      );
                    }
                    if (state.children.isEmpty) {
                      return const EmptyStateView(
                        title: 'Bağlı çocuk yok',
                        subtitle:
                            'Sağ alttaki butonla çocuğunuzu bağlayabilirsiniz.',
                      );
                    }
                    return RefreshIndicator(
                      color: AppColors.primary,
                      onRefresh: () => context.read<ParentCubit>().refresh(
                        userId,
                        fullName: fullName,
                      ),
                      child: ListView.separated(
                        padding: const EdgeInsets.fromLTRB(16, 8, 16, 90),
                        itemCount: state.children.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 12),
                        itemBuilder: (context, index) =>
                            _ChildLinkCard(link: state.children[index]),
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static Future<void> _openLinkSheet(BuildContext context, String userId) async {
    final cubit = context.read<ParentCubit>();
    final studentIdController = TextEditingController();
    final nameController = TextEditingController();
    final relationshipController = TextEditingController();
    final formKey = GlobalKey<FormState>();

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) {
        return Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            18,
            20,
            MediaQuery.of(sheetContext).viewInsets.bottom + 20,
          ),
          child: Form(
            key: formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'Çocuk bağla',
                  style: Theme.of(sheetContext).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Öğrenci kimliği ile bağ talebi oluşturun. Bağ, öğrenci veya '
                  'öğretmeni tarafından onaylanınca aktif olur.',
                  style: Theme.of(sheetContext).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: studentIdController,
                  decoration: const InputDecoration(
                    labelText: 'Öğrenci kimliği (ID)',
                    prefixIcon: Icon(Icons.badge_outlined),
                  ),
                  validator: (v) => (v == null || v.trim().isEmpty)
                      ? 'Öğrenci kimliği zorunlu.'
                      : null,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: nameController,
                  decoration: const InputDecoration(
                    labelText: 'Çocuğun adı (görünen)',
                    prefixIcon: Icon(Icons.person_outline_rounded),
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: relationshipController,
                  decoration: const InputDecoration(
                    labelText: 'Yakınlık (Anne/Baba/Vasi)',
                    prefixIcon: Icon(Icons.family_restroom_rounded),
                  ),
                ),
                const SizedBox(height: 18),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      minimumSize: const Size.fromHeight(48),
                    ),
                    onPressed: () async {
                      if (!(formKey.currentState?.validate() ?? false)) return;
                      final link = await cubit.requestChildLink(
                        parentUserId: userId,
                        studentId: studentIdController.text.trim(),
                        childDisplayName: nameController.text.trim().isEmpty
                            ? null
                            : nameController.text.trim(),
                        relationship: relationshipController.text.trim().isEmpty
                            ? null
                            : relationshipController.text.trim(),
                      );
                      if (sheetContext.mounted) {
                        Navigator.of(sheetContext).pop();
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            content: Text(
                              link != null
                                  ? 'Bağ talebi oluşturuldu, onay bekleniyor.'
                                  : 'Bağ talebi oluşturulamadı.',
                            ),
                          ),
                        );
                      }
                    },
                    child: const Text('Talep oluştur'),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _ChildLinkCard extends StatelessWidget {
  const _ChildLinkCard({required this.link});

  final ChildLink link;

  @override
  Widget build(BuildContext context) {
    return ParentCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: AppColors.primaryLight,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const Icon(
                  Icons.school_rounded,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      link.displayName,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    if (link.relationship != null)
                      Text(
                        link.relationship!,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                        ),
                      ),
                  ],
                ),
              ),
              ParentStatusBadge(status: link.status),
            ],
          ),
          if (link.isApproved && link.progress != null) ...<Widget>[
            const SizedBox(height: 12),
            const Divider(height: 1, color: AppColors.divider),
            const SizedBox(height: 12),
            Row(
              children: <Widget>[
                _MiniStat(
                  icon: Icons.menu_book_rounded,
                  value: '${link.progress!.completedLessonCount}',
                  label: 'Ders',
                ),
                _MiniStat(
                  icon: Icons.assignment_late_rounded,
                  value: '${link.progress!.openAssignmentCount}',
                  label: 'Açık ödev',
                ),
                _MiniStat(
                  icon: Icons.timer_rounded,
                  value: formatMinutes(link.progress!.weeklyStudyMinutes),
                  label: 'Bu hafta',
                ),
              ],
            ),
          ],
          if (link.isPending) ...<Widget>[
            const SizedBox(height: 10),
            Text(
              'Bağ onaylandığında çocuğunuzun gelişimini görebileceksiniz.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({
    required this.icon,
    required this.value,
    required this.label,
  });

  final IconData icon;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: <Widget>[
          Icon(icon, size: 18, color: AppColors.primary),
          const SizedBox(height: 4),
          Text(
            value,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}
