using System.Text;
using System.Windows;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class AuditDetailWindow : Window
{
    private static readonly (string Prefix, string Title)[] LegacySections =
    [
        ("Lotes:", "Lotes"),
        ("Compras:", "Compras"),
        ("Órdenes:", "Órdenes de producción"),
        ("Movimientos de inventario:", "Movimientos de inventario"),
        ("Registros sanitarios:", "Registros sanitarios"),
        ("Alertas:", "Alertas relacionadas")
    ];

    public AuditDetailWindow(AuditRow row)
    {
        InitializeComponent();

        HeaderTitle.Text = $"{row.Accion} — {row.Entidad}";
        HeaderSubtitle.Text = $"Registro #{row.Registro} · {row.FechaHora} · Usuario: {row.Usuario}";
        DetailText.Text = FormatDetail(row.Detalle);
    }

    private static string FormatDetail(string detail)
    {
        var lines = detail.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (!lines.Any(line => LegacySections.Any(section =>
                line.StartsWith(section.Prefix, StringComparison.OrdinalIgnoreCase))))
        {
            return detail;
        }

        var formatted = new StringBuilder();
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("Insumo eliminado:", StringComparison.OrdinalIgnoreCase))
            {
                formatted.AppendLine(trimmedLine);
                continue;
            }

            if (trimmedLine.StartsWith("Unidad:", StringComparison.OrdinalIgnoreCase))
            {
                formatted.AppendLine();
                formatted.AppendLine("DATOS DEL INSUMO");
                foreach (var field in trimmedLine["Unidad:".Length..].Split("; ", StringSplitOptions.RemoveEmptyEntries))
                {
                    formatted.AppendLine($"- {field.Trim().TrimEnd('.')}");
                }
                continue;
            }

            var section = LegacySections.FirstOrDefault(candidate =>
                trimmedLine.StartsWith(candidate.Prefix, StringComparison.OrdinalIgnoreCase));
            if (section.Prefix is not null)
            {
                var content = trimmedLine[section.Prefix.Length..].Trim();
                if (content.Length == 0)
                {
                    continue;
                }

                formatted.AppendLine();
                formatted.AppendLine(section.Title.ToUpperInvariant());
                foreach (var item in content.Split("; ", StringSplitOptions.RemoveEmptyEntries))
                {
                    formatted.AppendLine($"- {item.Trim()}");
                }
                continue;
            }

            formatted.AppendLine(trimmedLine);
        }

        return formatted.ToString().TrimEnd();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
