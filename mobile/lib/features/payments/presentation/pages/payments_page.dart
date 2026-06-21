import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

class PaymentsPage extends StatefulWidget {
  const PaymentsPage({super.key});

  @override
  State<PaymentsPage> createState() => _PaymentsPageState();
}

class _PaymentsPageState extends State<PaymentsPage> {
  static const _navy = Color(0xFF082B4F);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _red = Color(0xFFFF5A5F);
  static const _blue = Color(0xFF3D8BFF);
  static const _slate = Color(0xFF6B7A90);
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EEF7);

  static final _payments = <_StudentPayment>[
    _StudentPayment(
      studentName: 'Zeynep Demir',
      lessonName: 'Matematik - Trigonometri',
      dueDate: DateTime(2026, 5, 18),
      amount: 2400,
      status: _PaymentStatus.paid,
      accent: _emerald,
      photoUrl: 'https://i.pravatar.cc/120?img=47',
    ),
    _StudentPayment(
      studentName: 'Ali Yılmaz',
      lessonName: 'Fizik - Kuvvet ve Hareket',
      dueDate: DateTime(2026, 5, 22),
      amount: 1800,
      status: _PaymentStatus.pending,
      accent: _blue,
      photoUrl: 'https://i.pravatar.cc/120?img=12',
    ),
    _StudentPayment(
      studentName: 'Merve Kaya',
      lessonName: 'Geometri - Problem Çözümü',
      dueDate: DateTime(2026, 5, 12),
      amount: 2100,
      status: _PaymentStatus.overdue,
      accent: _red,
      photoUrl: 'https://i.pravatar.cc/120?img=32',
    ),
    _StudentPayment(
      studentName: 'Ege Arslan',
      lessonName: 'Kimya - Mol Kavrami',
      dueDate: DateTime(2026, 5, 25),
      amount: 1600,
      status: _PaymentStatus.pending,
      accent: _amber,
      photoUrl: 'https://i.pravatar.cc/120?img=15',
    ),
    _StudentPayment(
      studentName: 'Defne Sahin',
      lessonName: 'Türkçe - Paragraf',
      dueDate: DateTime(2026, 5, 16),
      amount: 1500,
      status: _PaymentStatus.paid,
      accent: _emerald,
      photoUrl: 'https://i.pravatar.cc/120?img=44',
    ),
    _StudentPayment(
      studentName: 'Can Koç',
      lessonName: 'Biyoloji - Hücre',
      dueDate: DateTime(2026, 5, 9),
      amount: 1750,
      status: _PaymentStatus.overdue,
      accent: _red,
      photoUrl: 'https://i.pravatar.cc/120?img=18',
    ),
  ];

  _PaymentFilter _selectedFilter = _PaymentFilter.all;

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    final teacherName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Ahmet Yılmaz';
    final visiblePayments = _payments
        .where((payment) => _selectedFilter.matches(payment.status))
        .toList();

    return Scaffold(
      backgroundColor: _background,
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: _navy,
        foregroundColor: Colors.white,
        onPressed: () => context.push('/payments/new'),
        label: const Text('Ödeme Ekle'),
        icon: const Icon(Icons.add_card_rounded),
      ),
      bottomNavigationBar: _FinanceBottomNav(
        onHomeTap: () => context.go('/dashboard'),
        onLessonsTap: () => context.go('/lesson-sessions'),
        onStudentsTap: () => context.go('/students'),
        onCalendarTap: () => context.go('/scheduling'),
        onMoreTap: () => context.go('/more'),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 116),
          children: <Widget>[
            _FinanceHeader(teacherName: teacherName),
            const SizedBox(height: 22),
            _FinanceSummaryPanel(payments: _payments),
            const SizedBox(height: 18),
            _PaymentTabs(
              selectedFilter: _selectedFilter,
              onChanged: (filter) => setState(() => _selectedFilter = filter),
            ),
            const SizedBox(height: 14),
            _PaymentList(
              payments: visiblePayments,
              selectedFilter: _selectedFilter,
            ),
          ],
        ),
      ),
    );
  }
}

class _FinanceHeader extends StatelessWidget {
  const _FinanceHeader({required this.teacherName});

  final String teacherName;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            teacherName,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: _PaymentsPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        _HeaderIconButton(
          icon: Icons.notifications_none_rounded,
          badgeText: '3',
          onTap: () {},
        ),
      ],
    );
  }
}

class _FinanceSummaryPanel extends StatelessWidget {
  const _FinanceSummaryPanel({required this.payments});

  final List<_StudentPayment> payments;

  @override
  Widget build(BuildContext context) {
    final paid = payments
        .where((payment) => payment.status == _PaymentStatus.paid)
        .fold<double>(0, (total, payment) => total + payment.amount);
    final pending = payments
        .where((payment) => payment.status == _PaymentStatus.pending)
        .fold<double>(0, (total, payment) => total + payment.amount);
    final overdue = payments
        .where((payment) => payment.status == _PaymentStatus.overdue)
        .fold<double>(0, (total, payment) => total + payment.amount);

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: _PaymentsPageState._navy,
        borderRadius: BorderRadius.circular(22),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x22082B4F),
            blurRadius: 24,
            offset: Offset(0, 12),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Finans Özeti',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Mayıs 2026 ders ödeme durumu',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.72),
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: <Widget>[
              Expanded(
                child: _SummaryMetric(
                  label: 'Bu ay gelir',
                  value: _money(paid),
                  icon: Icons.trending_up_rounded,
                  color: _PaymentsPageState._emerald,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Bekleyen',
                  value: _money(pending),
                  icon: Icons.schedule_rounded,
                  color: _PaymentsPageState._amber,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _SummaryMetric(
                  label: 'Geciken',
                  value: _money(overdue),
                  icon: Icons.warning_amber_rounded,
                  color: _PaymentsPageState._red,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _SummaryMetric extends StatelessWidget {
  const _SummaryMetric({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(minHeight: 112),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: color, size: 21),
          const SizedBox(height: 10),
          Text(
            label,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.78),
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 10),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w900,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PaymentTabs extends StatelessWidget {
  const _PaymentTabs({
    required this.selectedFilter,
    required this.onChanged,
  });

  final _PaymentFilter selectedFilter;
  final ValueChanged<_PaymentFilter> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: _PaymentsPageState._border),
      ),
      child: Row(
        children: _PaymentFilter.values.map((filter) {
          final selected = filter == selectedFilter;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(14),
              onTap: () => onChanged(filter),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                padding: const EdgeInsets.symmetric(vertical: 11),
                decoration: BoxDecoration(
                  color: selected ? _PaymentsPageState._navy : Colors.white,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    filter.label,
                    maxLines: 1,
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: selected ? Colors.white : _PaymentsPageState._slate,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class _PaymentList extends StatelessWidget {
  const _PaymentList({
    required this.payments,
    required this.selectedFilter,
  });

  final List<_StudentPayment> payments;
  final _PaymentFilter selectedFilter;

  @override
  Widget build(BuildContext context) {
    if (payments.isEmpty) {
      return _EmptyPanel(
        title: '${selectedFilter.label} ödeme yok',
        subtitle: 'Bu durumda görüntülenecek ders ödemesi bulunmuyor.',
      );
    }

    return Column(
      children: payments
          .map(
            (payment) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: _PaymentTile(payment: payment),
            ),
          )
          .toList(),
    );
  }
}

class _PaymentTile extends StatelessWidget {
  const _PaymentTile({required this.payment});

  final _StudentPayment payment;

  @override
  Widget build(BuildContext context) {
    final statusColor = payment.status.color;
    final dueDate = _dateLabel(payment.dueDate);

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _PaymentsPageState._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x0F082B4F),
            blurRadius: 18,
            offset: Offset(0, 8),
          ),
        ],
      ),
      child: Row(
        children: <Widget>[
          _StudentPhoto(
            name: payment.studentName,
            accent: payment.accent,
            photoUrl: payment.photoUrl,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  payment.studentName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: _PaymentsPageState._text,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  payment.lessonName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _PaymentsPageState._slate,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
                Row(
                  children: <Widget>[
                    Icon(
                      Icons.event_rounded,
                      size: 15,
                      color: _PaymentsPageState._slate.withValues(alpha: 0.84),
                    ),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(
                        dueDate,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: _PaymentsPageState._slate,
                              fontWeight: FontWeight.w600,
                            ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                _money(payment.amount),
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: _PaymentsPageState._text,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 8),
              _StatusPill(label: payment.status.label, color: statusColor),
            ],
          ),
        ],
      ),
    );
  }
}

class _StudentPhoto extends StatelessWidget {
  const _StudentPhoto({
    required this.name,
    required this.accent,
    required this.photoUrl,
  });

  final String name;
  final Color accent;
  final String photoUrl;

  @override
  Widget build(BuildContext context) {
    return ClipOval(
      child: SizedBox(
        width: 54,
        height: 54,
        child: Image.network(
          photoUrl,
          fit: BoxFit.cover,
          errorBuilder: (context, error, stackTrace) =>
              _InitialsAvatar(name: name, accent: accent),
        ),
      ),
    );
  }
}

class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.name, required this.accent});

  final String name;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.92),
            accent.withValues(alpha: 0.56),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
          color: color,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({required this.icon, this.badgeText, this.onTap});

  final IconData icon;
  final String? badgeText;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Stack(
        clipBehavior: Clip.none,
        children: <Widget>[
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: _PaymentsPageState._border),
            ),
            child: Icon(icon, color: _PaymentsPageState._text),
          ),
          if (badgeText != null)
            Positioned(
              top: -3,
              right: -3,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: _PaymentsPageState._emerald,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  badgeText!,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _PaymentsPageState._border),
      ),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.receipt_long_outlined,
            color: _PaymentsPageState._navy,
            size: 32,
          ),
          const SizedBox(height: 10),
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _PaymentsPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: _PaymentsPageState._slate),
          ),
        ],
      ),
    );
  }
}

class _FinanceBottomNav extends StatelessWidget {
  const _FinanceBottomNav({
    required this.onHomeTap,
    required this.onLessonsTap,
    required this.onStudentsTap,
    required this.onCalendarTap,
    required this.onMoreTap,
  });

  final VoidCallback onHomeTap;
  final VoidCallback onLessonsTap;
  final VoidCallback onStudentsTap;
  final VoidCallback onCalendarTap;
  final VoidCallback onMoreTap;

  @override
  Widget build(BuildContext context) {
    final items = <_BottomNavItem>[
      _BottomNavItem(Icons.home_rounded, 'Ana sayfa', false, onHomeTap),
      _BottomNavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      _BottomNavItem(Icons.groups_rounded, 'Öğrenciler', false, onStudentsTap),
      _BottomNavItem(
        Icons.calendar_month_rounded,
        'Takvim',
        false,
        onCalendarTap,
      ),
      const _BottomNavItem(
        Icons.account_balance_wallet_rounded,
        'Finans',
        true,
      ),
      _BottomNavItem(Icons.widgets_rounded, 'Diğer', false, onMoreTap),
    ];

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: _PaymentsPageState._border)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: items.map((item) {
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap: item.onTap,
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(
                      item.icon,
                      color: item.selected
                          ? _PaymentsPageState._navy
                          : _PaymentsPageState._slate,
                    ),
                    const SizedBox(height: 4),
                    FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Text(
                        item.label,
                        maxLines: 1,
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: item.selected
                                  ? _PaymentsPageState._navy
                                  : _PaymentsPageState._slate,
                              fontWeight: item.selected
                                  ? FontWeight.w800
                                  : FontWeight.w600,
                              fontSize: 11,
                            ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class _StudentPayment {
  const _StudentPayment({
    required this.studentName,
    required this.lessonName,
    required this.dueDate,
    required this.amount,
    required this.status,
    required this.accent,
    required this.photoUrl,
  });

  final String studentName;
  final String lessonName;
  final DateTime dueDate;
  final double amount;
  final _PaymentStatus status;
  final Color accent;
  final String photoUrl;
}

enum _PaymentFilter {
  all('Tümü'),
  paid('Ödenen'),
  pending('Bekleyen'),
  overdue('Geciken');

  const _PaymentFilter(this.label);

  final String label;

  bool matches(_PaymentStatus status) {
    return switch (this) {
      _PaymentFilter.all => true,
      _PaymentFilter.paid => status == _PaymentStatus.paid,
      _PaymentFilter.pending => status == _PaymentStatus.pending,
      _PaymentFilter.overdue => status == _PaymentStatus.overdue,
    };
  }
}

enum _PaymentStatus {
  paid('Ödendi', _PaymentsPageState._emerald),
  pending('Bekleyen', _PaymentsPageState._amber),
  overdue('Geciken', _PaymentsPageState._red);

  const _PaymentStatus(this.label, this.color);

  final String label;
  final Color color;
}

class _BottomNavItem {
  const _BottomNavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}

String _money(double amount) {
  final formatted = NumberFormat.decimalPattern('tr_TR').format(amount.round());
  return '$formatted TRY';
}

String _dateLabel(DateTime date) {
  const months = <String>[
    'Ocak',
    'Subat',
    'Mart',
    'Nisan',
    'Mayıs',
    'Haziran',
    'Temmuz',
    'Ağustos',
    'Eylul',
    'Ekim',
    'Kasım',
    'Aralik',
  ];

  return '${date.day} ${months[date.month - 1]} ${date.year}';
}

String _initials(String value) {
  final parts = value.trim().split(RegExp(r'\s+'));
  if (parts.isEmpty || parts.first.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.substring(0, 1).toUpperCase();
  }
  return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
      .toUpperCase();
}
