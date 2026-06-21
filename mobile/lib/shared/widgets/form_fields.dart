import 'package:flutter/material.dart';

const Color _appFieldText = Color(0xFF10233D);
const Color _appFieldBorder = Color(0xFFE5EAF0);
const Color _appFieldFocus = Color(0xFF062B52);
const Color _appFieldError = Color(0xFFBA1A1A);

InputDecoration appInputDecoration(String hintText, {Widget? suffixIcon}) {
  return InputDecoration(
    hintText: hintText,
    suffixIcon: suffixIcon,
    filled: true,
    fillColor: Colors.white,
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: _appFieldBorder),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: _appFieldFocus),
    ),
    errorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: _appFieldError),
    ),
    focusedErrorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: _appFieldError),
    ),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(18),
      borderSide: const BorderSide(color: _appFieldBorder),
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
        color: _appFieldText,
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
