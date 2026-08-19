import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';

InputDecoration appInputDecoration(String hintText, {Widget? suffixIcon}) {
  return InputDecoration(
    hintText: hintText,
    suffixIcon: suffixIcon,
    filled: true,
    fillColor: Colors.white,
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: AppColors.border),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: AppColors.primary),
    ),
    errorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: AppColors.accentRed),
    ),
    focusedErrorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: AppColors.accentRed),
    ),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: AppColors.border),
    ),
  );
}

class AppFieldLabel extends StatelessWidget {
  const AppFieldLabel({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: Theme.of(context).textTheme.labelLarge?.copyWith(
        color: AppColors.textPrimary,
        fontWeight: FontWeight.w700,
      ),
    );
  }
}

class AppTextField extends StatelessWidget {
  const AppTextField({
    super.key,
    required this.controller,
    required this.labelText,
    this.hintText,
    this.validator,
    this.keyboardType,
    this.textCapitalization = TextCapitalization.none,
    this.minLines,
    this.maxLines = 1,
    this.maxLength,
    this.readOnly = false,
    this.onTap,
    this.suffixIcon,
  });

  final TextEditingController controller;
  final String labelText;
  final String? hintText;
  final FormFieldValidator<String>? validator;
  final TextInputType? keyboardType;
  final TextCapitalization textCapitalization;
  final int? minLines;
  final int? maxLines;
  final int? maxLength;
  final bool readOnly;
  final VoidCallback? onTap;
  final Widget? suffixIcon;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        AppFieldLabel(text: labelText),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          decoration: appInputDecoration(
            hintText ?? labelText,
            suffixIcon: suffixIcon,
          ),
          keyboardType: keyboardType,
          textCapitalization: textCapitalization,
          minLines: minLines,
          maxLines: maxLines,
          maxLength: maxLength,
          readOnly: readOnly,
          onTap: onTap,
          validator: validator,
        ),
      ],
    );
  }
}

class AppDropdownField<T> extends StatelessWidget {
  const AppDropdownField({
    super.key,
    required this.value,
    required this.labelText,
    required this.items,
    required this.onChanged,
  });

  final T value;
  final String labelText;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T?> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        AppFieldLabel(text: labelText),
        const SizedBox(height: 8),
        DropdownButtonFormField<T>(
          initialValue: value,
          decoration: appInputDecoration(labelText),
          items: items,
          onChanged: onChanged,
        ),
      ],
    );
  }
}

class AppDateTimeField extends StatelessWidget {
  const AppDateTimeField({
    super.key,
    required this.controller,
    required this.labelText,
    required this.validator,
    this.hintText,
  });

  final TextEditingController controller;
  final String labelText;
  final String? hintText;
  final FormFieldValidator<String> validator;

  @override
  Widget build(BuildContext context) {
    return AppTextField(
      controller: controller,
      labelText: labelText,
      hintText: hintText,
      validator: validator,
      keyboardType: TextInputType.datetime,
      suffixIcon: const Icon(Icons.event_outlined),
    );
  }
}
