using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SistemaTrazabilidad.Presentation.Views;

public enum ModuleInputKind
{
    Text,
    Digits,
    Integer,
    Decimal,
    Date,
    Selection
}

public sealed record ModuleInputChoice(string Label, object Value);

public sealed record ModuleInputField(
    string Key,
    string Label,
    ModuleInputKind Kind,
    object? InitialValue = null,
    IReadOnlyList<ModuleInputChoice>? Choices = null,
    bool Required = true);

public static class ModuleInputDialog
{
    public static bool Show(
        Window owner,
        string title,
        IEnumerable<ModuleInputField> fields,
        out Dictionary<string, object?> values)
    {
        values = [];
        var resultValues = new Dictionary<string, object?>();
        var fieldList = fields.ToList();
        var controls = new Dictionary<string, FrameworkElement>();
        var window = new Window
        {
            Title = title,
            Owner = owner,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            MinHeight = 220,
            MaxHeight = 680,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize,
            Background = Brushes.White
        };

        var root = new DockPanel { Margin = new Thickness(22) };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        var content = new StackPanel();
        foreach (var field in fieldList)
        {
            content.Children.Add(new TextBlock
            {
                Text = field.Label,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 10, 0, 5)
            });

            FrameworkElement input = CreateInput(field);
            controls.Add(field.Key, input);
            content.Children.Add(input);
        }

        root.Children.Add(buttonPanel);
        root.Children.Add(new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        window.Content = root;

        var cancelButton = new Button
        {
            Content = "Cancelar",
            MinWidth = 90,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 7, 12, 7)
        };
        cancelButton.Click += (_, _) => window.DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Guardar",
            MinWidth = 90,
            Padding = new Thickness(12, 7, 12, 7),
            Background = new SolidColorBrush(Color.FromRgb(22, 155, 98)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            IsDefault = true
        };
        saveButton.Click += (_, _) =>
        {
            if (TryReadValues(owner, fieldList, controls, out var result))
            {
                resultValues = result;
                window.DialogResult = true;
            }
        };
        buttonPanel.Children.Add(saveButton);

        var accepted = window.ShowDialog() == true;
        if (accepted)
        {
            values = resultValues;
        }

        return accepted;
    }

    private static FrameworkElement CreateInput(ModuleInputField field)
    {
        if (field.Kind == ModuleInputKind.Date)
        {
            return new DatePicker
            {
                SelectedDate = field.InitialValue is DateTime date ? date : DateTime.Today,
                Height = 36
            };
        }

        if (field.Kind == ModuleInputKind.Selection)
        {
            var comboBox = new ComboBox
            {
                Height = 36,
                Padding = new Thickness(8, 4, 8, 4),
                ItemsSource = field.Choices ?? [],
                DisplayMemberPath = nameof(ModuleInputChoice.Label),
                SelectedValuePath = nameof(ModuleInputChoice.Value)
            };

            var initialChoice = field.Choices?.FirstOrDefault(c =>
                Equals(c.Value, field.InitialValue));
            comboBox.SelectedValue = initialChoice?.Value
                ?? field.Choices?.FirstOrDefault()?.Value;
            return comboBox;
        }

        var initialText = field.InitialValue switch
        {
            null => string.Empty,
            DateTime date => date.ToString("d", CultureInfo.CurrentCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => field.InitialValue.ToString() ?? string.Empty
        };
        if (field.Kind == ModuleInputKind.Digits)
        {
            initialText = new string(initialText.Where(character => character is >= '0' and <= '9').ToArray());
        }

        var textBox = new TextBox
        {
            Text = initialText,
            Height = 36,
            Padding = new Thickness(10, 7, 10, 7),
            Style = System.Windows.Application.Current.TryFindResource("InputTextBoxStyle") as Style
        };

        if (field.Kind == ModuleInputKind.Digits)
        {
            NumericTextBoxBehavior.SetDigitsOnly(textBox, true);
        }
        else if (field.Kind is ModuleInputKind.Integer or ModuleInputKind.Decimal)
        {
            NumericTextBoxBehavior.SetUseThousandsSeparator(textBox, true);
        }

        return textBox;
    }

    private static bool TryReadValues(
        Window owner,
        IEnumerable<ModuleInputField> fields,
        IReadOnlyDictionary<string, FrameworkElement> controls,
        out Dictionary<string, object?> values)
    {
        values = [];
        foreach (var field in fields)
        {
            var control = controls[field.Key];
            object? value;

            switch (field.Kind)
            {
                case ModuleInputKind.Text:
                    value = (control as TextBox)?.Text.Trim();
                    if (field.Required && string.IsNullOrWhiteSpace(value as string))
                    {
                        ShowValidationError(owner, field, "es obligatorio.");
                        return false;
                    }
                    break;

                case ModuleInputKind.Digits:
                    var digitsText = (control as TextBox)?.Text.Trim() ?? string.Empty;
                    if ((field.Required && string.IsNullOrWhiteSpace(digitsText)) ||
                        digitsText.Any(character => character is < '0' or > '9'))
                    {
                        ShowValidationError(owner, field, "debe contener solo números.");
                        return false;
                    }
                    value = digitsText;
                    break;

                case ModuleInputKind.Integer:
                    var integerText = (control as TextBox)?.Text.Trim() ?? string.Empty;
                    if (!int.TryParse(
                            integerText,
                            NumberStyles.Integer | NumberStyles.AllowThousands,
                            CultureInfo.CurrentCulture,
                            out var integer))
                    {
                        ShowValidationError(owner, field, "debe ser un número entero.");
                        return false;
                    }
                    value = integer;
                    break;

                case ModuleInputKind.Decimal:
                    var decimalText = (control as TextBox)?.Text.Trim() ?? string.Empty;
                    if (!decimal.TryParse(decimalText, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
                        && !decimal.TryParse(decimalText, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
                    {
                        ShowValidationError(owner, field, "debe ser un número válido.");
                        return false;
                    }
                    value = number;
                    break;

                case ModuleInputKind.Date:
                    value = (control as DatePicker)?.SelectedDate;
                    if (value is null && field.Required)
                    {
                        ShowValidationError(owner, field, "es obligatoria.");
                        return false;
                    }
                    break;

                case ModuleInputKind.Selection:
                    value = (control as ComboBox)?.SelectedValue;
                    if (value is null && field.Required)
                    {
                        ShowValidationError(owner, field, "debe seleccionarse.");
                        return false;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(field.Kind));
            }

            values[field.Key] = value;
        }

        return true;
    }

    private static void ShowValidationError(Window owner, ModuleInputField field, string reason)
    {
        MessageBox.Show(
            owner,
            $"El campo «{field.Label}» {reason}",
            "Revisa los datos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
