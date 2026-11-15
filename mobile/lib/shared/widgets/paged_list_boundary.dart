import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';

/// Kısa ilk sayfada da görünümü doldurur; hata sonrası yalnız kullanıcı tekrar dener.
class PagedListBoundary extends StatefulWidget {
  const PagedListBoundary({
    super.key,
    required this.builder,
    required this.hasMore,
    required this.isLoading,
    required this.onLoadMore,
    this.errorMessage,
  });
  final Widget Function(ScrollController) builder;
  final bool hasMore;
  final bool isLoading;
  final VoidCallback onLoadMore;
  final String? errorMessage;
  @override
  State<PagedListBoundary> createState() => _PagedListBoundaryState();
}

class _PagedListBoundaryState extends State<PagedListBoundary> {
  final _controller = ScrollController();
  @override
  void initState() {
    super.initState();
    _controller.addListener(_check);
  }

  void _check() {
    if (mounted &&
        widget.hasMore &&
        !widget.isLoading &&
        widget.errorMessage == null &&
        _controller.hasClients &&
        _controller.position.extentAfter < 240) {
      widget.onLoadMore();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    WidgetsBinding.instance.addPostFrameCallback((_) => _check());
    return Stack(
      children: [
        Positioned.fill(child: widget.builder(_controller)),
        if (widget.isLoading)
          const Align(
            alignment: Alignment.bottomCenter,
            child: LinearProgressIndicator(),
          ),
        if (widget.errorMessage != null)
          Align(
            alignment: Alignment.bottomCenter,
            child: Material(
              child: TextButton(
                onPressed: widget.onLoadMore,
                child: Text(AppLocalizations.of(context).pagination_retry),
              ),
            ),
          ),
      ],
    );
  }
}
