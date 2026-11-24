using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaTrazabilidad.Presentation.Views;

public static class NumericTextBoxBehavior
{
    public static readonly DependencyProperty DigitsOnlyProperty =
        DependencyProperty.RegisterAttached(
            "DigitsOnly",
            typeof(bool),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(false, OnDigitsOnlyChanged));

    public static readonly DependencyProperty UseThousandsSeparatorProperty =
        DependencyProperty.RegisterAttached(
            "UseThousandsSeparator",
            typeof(bool),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(false, OnUseThousandsSeparatorChanged));

    public static bool GetDigitsOnly(DependencyObject element) =>
        (bool)element.GetValue(DigitsOnlyProperty);

    public static void SetDigitsOnly(DependencyObject element, bool value) =>
        element.SetValue(DigitsOnlyProperty, value);

    public static bool GetUseThousandsSeparator(DependencyObject element) =>
        (bool)element.GetValue(UseThousandsSeparatorProperty);

    public static void SetUseThousandsSeparator(DependencyObject element, bool value) =>
        element.SetValue(UseThousandsSeparatorProperty, value);

    private static void OnDigitsOnlyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if (eventArgs.NewValue is true)
        {
            textBox.PreviewTextInput += TextBox_PreviewDigitsOnlyTextInput;
            textBox.PreviewDrop += TextBox_PreviewDigitsOnlyDrop;
            DataObject.AddPastingHandler(textBox, TextBox_DigitsOnlyPasting);
        }
        else
        {
            textBox.PreviewTextInput -= TextBox_PreviewDigitsOnlyTextInput;
            textBox.PreviewDrop -= TextBox_PreviewDigitsOnlyDrop;
            DataObject.RemovePastingHandler(textBox, TextBox_DigitsOnlyPasting);
        }
    }

    private static void OnUseThousandsSeparatorChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if (eventArgs.NewValue is true)
        {
            textBox.PreviewTextInput += TextBox_PreviewTextInput;
            textBox.PreviewDrop += TextBox_PreviewDrop;
            DataObject.AddPastingHandler(textBox, TextBox_Pasting);
            textBox.TextChanged += TextBox_TextChanged;
            FormatTextBox(textBox);
        }
        else
        {
            textBox.PreviewTextInput -= TextBox_PreviewTextInput;
            textBox.PreviewDrop -= TextBox_PreviewDrop;
            DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
            textBox.TextChanged -= TextBox_TextChanged;
        }
    }

    private static void TextBox_PreviewDigitsOnlyTextInput(object sender, TextCompositionEventArgs eventArgs)
    {
        if (sender is TextBox textBox &&
            !IsValidDigitsInsertion(textBox, eventArgs.Text))
        {
            eventArgs.Handled = true;
        }
    }

    private static void TextBox_DigitsOnlyPasting(object sender, DataObjectPastingEventArgs eventArgs)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var pastedText = eventArgs.DataObject.GetData(DataFormats.UnicodeText) as string
            ?? eventArgs.DataObject.GetData(DataFormats.Text) as string;
        if (pastedText is null || !IsValidDigitsInsertion(textBox, pastedText))
        {
            eventArgs.CancelCommand();
        }
    }

    private static void TextBox_PreviewDigitsOnlyDrop(object sender, DragEventArgs eventArgs)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var droppedText = eventArgs.Data.GetData(DataFormats.UnicodeText) as string
            ?? eventArgs.Data.GetData(DataFormats.Text) as string;
        if (droppedText is null || !IsValidDigitsInsertion(textBox, droppedText))
        {
            eventArgs.Handled = true;
        }
    }

    private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs eventArgs)
    {
        if (sender is TextBox textBox &&
            !IsValidInsertion(textBox, eventArgs.Text, CultureInfo.CurrentCulture.NumberFormat))
        {
            eventArgs.Handled = true;
        }
    }

    private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs eventArgs)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var pastedText = eventArgs.DataObject.GetData(DataFormats.UnicodeText) as string
            ?? eventArgs.DataObject.GetData(DataFormats.Text) as string;
        if (pastedText is null ||
            !IsValidInsertion(textBox, pastedText, CultureInfo.CurrentCulture.NumberFormat))
        {
            eventArgs.CancelCommand();
        }
    }

    private static void TextBox_PreviewDrop(object sender, DragEventArgs eventArgs)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var droppedText = eventArgs.Data.GetData(DataFormats.UnicodeText) as string
            ?? eventArgs.Data.GetData(DataFormats.Text) as string;
        if (droppedText is null ||
            !IsValidInsertion(textBox, droppedText, CultureInfo.CurrentCulture.NumberFormat))
        {
            eventArgs.Handled = true;
        }
    }

    private static void TextBox_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (sender is TextBox textBox)
        {
            FormatTextBox(textBox);
        }
    }

    private static void FormatTextBox(TextBox textBox)
    {
        var currentText = textBox.Text;
        var numberFormat = CultureInfo.CurrentCulture.NumberFormat;
        var groupSeparator = numberFormat.NumberGroupSeparator;
        var caretIndex = Math.Clamp(textBox.CaretIndex, 0, currentText.Length);
        var meaningfulCharactersBeforeCaret = CountWithoutGroupSeparators(
            SanitizeNumberText(currentText[..caretIndex], numberFormat),
            groupSeparator);
        var formattedText = FormatNumber(SanitizeNumberText(currentText, numberFormat), numberFormat);
        if (string.Equals(currentText, formattedText, StringComparison.Ordinal))
        {
            return;
        }

        textBox.TextChanged -= TextBox_TextChanged;
        try
        {
            textBox.Text = formattedText;
            textBox.CaretIndex = GetCaretIndex(
                formattedText,
                meaningfulCharactersBeforeCaret,
                groupSeparator);
        }
        finally
        {
            textBox.TextChanged += TextBox_TextChanged;
        }
    }

    private static bool IsValidInsertion(TextBox textBox, string insertedText, NumberFormatInfo numberFormat)
    {
        var selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        var selectionLength = Math.Clamp(textBox.SelectionLength, 0, textBox.Text.Length - selectionStart);
        var candidate = textBox.Text
            .Remove(selectionStart, selectionLength)
            .Insert(selectionStart, insertedText);

        return string.Equals(
            candidate,
            SanitizeNumberText(candidate, numberFormat),
            StringComparison.Ordinal);
    }

    private static bool IsValidDigitsInsertion(TextBox textBox, string insertedText)
    {
        var selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        var selectionLength = Math.Clamp(textBox.SelectionLength, 0, textBox.Text.Length - selectionStart);
        var candidate = textBox.Text
            .Remove(selectionStart, selectionLength)
            .Insert(selectionStart, insertedText);

        return ContainsOnlyDigits(candidate);
    }

    private static string SanitizeNumberText(string text, NumberFormatInfo numberFormat)
    {
        var builder = new StringBuilder(text.Length);
        var groupSeparator = numberFormat.NumberGroupSeparator;
        var decimalSeparator = numberFormat.NumberDecimalSeparator;
        var negativeSign = numberFormat.NegativeSign;
        var hasDecimalSeparator = false;

        for (var index = 0; index < text.Length;)
        {
            var remainingText = text.AsSpan(index);
            if (text[index] is >= '0' and <= '9')
            {
                builder.Append(text[index++]);
            }
            else if (!string.IsNullOrEmpty(negativeSign) &&
                     index == 0 &&
                     remainingText.StartsWith(negativeSign, StringComparison.Ordinal))
            {
                builder.Append(negativeSign);
                index += negativeSign.Length;
            }
            else if (!hasDecimalSeparator &&
                     !string.IsNullOrEmpty(decimalSeparator) &&
                     remainingText.StartsWith(decimalSeparator, StringComparison.Ordinal))
            {
                builder.Append(decimalSeparator);
                hasDecimalSeparator = true;
                index += decimalSeparator.Length;
            }
            else if (!hasDecimalSeparator &&
                     !string.IsNullOrEmpty(groupSeparator) &&
                     remainingText.StartsWith(groupSeparator, StringComparison.Ordinal))
            {
                builder.Append(groupSeparator);
                index += groupSeparator.Length;
            }
            else
            {
                index++;
            }
        }

        return builder.ToString();
    }

    private static string FormatNumber(string text, NumberFormatInfo numberFormat)
    {
        var groupSeparator = numberFormat.NumberGroupSeparator;
        var decimalSeparator = numberFormat.NumberDecimalSeparator;
        var ungroupedText = string.IsNullOrEmpty(groupSeparator)
            ? text
            : text.Replace(groupSeparator, string.Empty, StringComparison.Ordinal);

        if (ungroupedText.Length == 0)
        {
            return string.Empty;
        }

        var isNegative = ungroupedText.StartsWith(numberFormat.NegativeSign, StringComparison.Ordinal);
        var numberText = isNegative
            ? ungroupedText[numberFormat.NegativeSign.Length..]
            : ungroupedText;
        if (numberText.Length == 0)
        {
            return isNegative ? numberFormat.NegativeSign : string.Empty;
        }

        var decimalIndex = numberText.IndexOf(decimalSeparator, StringComparison.Ordinal);
        if (decimalIndex >= 0 &&
            numberText.IndexOf(decimalSeparator, decimalIndex + decimalSeparator.Length, StringComparison.Ordinal) >= 0)
        {
            return text;
        }

        var integerPart = decimalIndex >= 0 ? numberText[..decimalIndex] : numberText;
        var decimalPart = decimalIndex >= 0
            ? numberText[(decimalIndex + decimalSeparator.Length)..]
            : string.Empty;
        if (!ContainsOnlyDigits(integerPart) || !ContainsOnlyDigits(decimalPart))
        {
            return text;
        }

        if (integerPart.Length == 0 && decimalIndex >= 0)
        {
            integerPart = "0";
        }

        return (isNegative ? numberFormat.NegativeSign : string.Empty)
            + AddThousandsSeparators(integerPart, groupSeparator)
            + (decimalIndex >= 0 ? decimalSeparator + decimalPart : string.Empty);
    }

    private static bool ContainsOnlyDigits(string value)
    {
        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string AddThousandsSeparators(string integerPart, string groupSeparator)
    {
        if (integerPart.Length <= 3 || string.IsNullOrEmpty(groupSeparator))
        {
            return integerPart;
        }

        var builder = new StringBuilder(integerPart.Length + integerPart.Length / 3);
        var firstGroupLength = integerPart.Length % 3;
        if (firstGroupLength == 0)
        {
            firstGroupLength = 3;
        }

        builder.Append(integerPart.AsSpan(0, firstGroupLength));
        for (var index = firstGroupLength; index < integerPart.Length; index += 3)
        {
            builder.Append(groupSeparator);
            builder.Append(integerPart.AsSpan(index, 3));
        }

        return builder.ToString();
    }

    private static int CountWithoutGroupSeparators(string text, string groupSeparator)
    {
        if (string.IsNullOrEmpty(groupSeparator))
        {
            return text.Length;
        }

        var count = 0;
        for (var index = 0; index < text.Length;)
        {
            if (text.AsSpan(index).StartsWith(groupSeparator, StringComparison.Ordinal))
            {
                index += groupSeparator.Length;
            }
            else
            {
                count++;
                index++;
            }
        }

        return count;
    }

    private static int GetCaretIndex(string text, int meaningfulCharacterCount, string groupSeparator)
    {
        if (meaningfulCharacterCount == 0 || string.IsNullOrEmpty(groupSeparator))
        {
            return Math.Min(meaningfulCharacterCount, text.Length);
        }

        var count = 0;
        for (var index = 0; index < text.Length;)
        {
            if (text.AsSpan(index).StartsWith(groupSeparator, StringComparison.Ordinal))
            {
                index += groupSeparator.Length;
                continue;
            }

            count++;
            index++;
            if (count == meaningfulCharacterCount)
            {
                return index;
            }
        }

        return text.Length;
    }
}
