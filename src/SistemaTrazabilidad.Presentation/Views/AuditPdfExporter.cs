using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using SistemaTrazabilidad.Presentation.ViewModels;
using System.Globalization;

namespace SistemaTrazabilidad.Presentation.Views;

internal static class AuditPdfExporter
{
    private static readonly Color AccentColor = Color.FromRgb(8, 107, 193);
    private static readonly Color LightAccentColor = Color.FromRgb(232, 244, 253);
    private static readonly Color MutedColor = Color.FromRgb(100, 116, 139);
    private static readonly Color BorderColor = Color.FromRgb(214, 226, 238);

    public static void Export(string filePath, IReadOnlyList<AuditRow> records)
    {
        var document = new Document();
        document.Info.Title = "Reporte de auditoría";
        document.Info.Author = "Sistema de Trazabilidad";
        document.Info.Subject = "Registro de movimientos del sistema";

        var normalStyle = document.Styles[StyleNames.Normal]
            ?? throw new InvalidOperationException("No se encontró el estilo normal del documento.");
        normalStyle.Font.Name = "Arial";
        normalStyle.Font.Size = 9;
        normalStyle.Font.Color = Color.FromRgb(30, 41, 59);
        normalStyle.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.9);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.9);

        var pageHeader = section.Headers.Primary.AddParagraph("SISTEMA DE TRAZABILIDAD");
        pageHeader.Format.Font.Size = 8;
        pageHeader.Format.Font.Bold = true;
        pageHeader.Format.Font.Color = AccentColor;
        pageHeader.Format.Borders.Bottom.Width = Unit.FromPoint(0.7);
        pageHeader.Format.Borders.Bottom.Color = BorderColor;
        pageHeader.Format.SpaceAfter = Unit.FromPoint(5);

        var eyebrow = section.AddParagraph("REGISTRO DE ACTIVIDAD");
        eyebrow.Format.Font.Size = 8;
        eyebrow.Format.Font.Bold = true;
        eyebrow.Format.Font.Color = AccentColor;
        eyebrow.Format.SpaceAfter = Unit.FromPoint(3);

        var title = section.AddParagraph("Reporte de auditoría");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(30, 41, 59);
        title.Format.SpaceAfter = Unit.FromPoint(3);

        var subtitle = section.AddParagraph(
            "Detalle de los movimientos incluidos con los filtros aplicados en el sistema.");
        subtitle.Format.Font.Size = 9;
        subtitle.Format.Font.Color = MutedColor;
        subtitle.Format.SpaceAfter = Unit.FromPoint(14);

        var summary = section.AddTable();
        summary.Borders.Width = Unit.FromPoint(0);
        summary.AddColumn(Unit.FromCentimeter(8.7));
        summary.AddColumn(Unit.FromCentimeter(8.7));
        var summaryRow = summary.AddRow();
        summaryRow.Shading.Color = LightAccentColor;
        summaryRow.Cells[0].Format.LeftIndent = Unit.FromPoint(10);
        summaryRow.Cells[0].Format.SpaceBefore = Unit.FromPoint(7);
        summaryRow.Cells[0].Format.SpaceAfter = Unit.FromPoint(7);
        summaryRow.Cells[0].AddParagraph("REGISTROS INCLUIDOS").Format.Font.Size = 8;
        var count = summaryRow.Cells[0].AddParagraph(records.Count.ToString(CultureInfo.InvariantCulture));
        count.Format.Font.Size = 15;
        count.Format.Font.Bold = true;
        count.Format.Font.Color = AccentColor;
        summaryRow.Cells[1].Format.LeftIndent = Unit.FromPoint(10);
        summaryRow.Cells[1].Format.SpaceBefore = Unit.FromPoint(7);
        summaryRow.Cells[1].Format.SpaceAfter = Unit.FromPoint(7);
        summaryRow.Cells[1].AddParagraph("FECHA DE EXPORTACIÓN").Format.Font.Size = 8;
        var generatedAt = summaryRow.Cells[1].AddParagraph(
            DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        generatedAt.Format.Font.Size = 11;
        generatedAt.Format.Font.Bold = true;
        generatedAt.Format.Font.Color = Color.FromRgb(30, 41, 59);

        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(5);

        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var heading = section.AddParagraph();
            heading.AddFormattedText($"EVENTO {index + 1:000}  ·  {record.Accion}", TextFormat.Bold);
            heading.AddText($"    {record.Entidad} #{record.Registro}");
            heading.Format.Font.Size = 10;
            heading.Format.Font.Bold = true;
            heading.Format.Font.Color = AccentColor;
            heading.Format.Shading.Color = LightAccentColor;
            heading.Format.Borders.Left.Width = Unit.FromPoint(2.5);
            heading.Format.Borders.Left.Color = AccentColor;
            heading.Format.LeftIndent = Unit.FromPoint(8);
            heading.Format.SpaceBefore = Unit.FromPoint(12);
            heading.Format.SpaceAfter = Unit.FromPoint(5);

            var date = section.AddParagraph($"FECHA Y HORA   {record.FechaHora}");
            date.Format.Font.Size = 8;
            date.Format.Font.Color = MutedColor;
            date.Format.LeftIndent = Unit.FromPoint(8);
            date.Format.KeepWithNext = true;

            var metadata = section.AddParagraph();
            metadata.AddFormattedText("USUARIO   ", TextFormat.Bold);
            metadata.AddText(record.Usuario);
            metadata.AddText("      ");
            metadata.AddFormattedText("MÓDULO   ", TextFormat.Bold);
            metadata.AddText(record.Modulo);
            metadata.Format.Font.Size = 8;
            metadata.Format.Font.Color = MutedColor;
            metadata.Format.Shading.Color = Color.FromRgb(246, 248, 251);
            metadata.Format.LeftIndent = Unit.FromPoint(8);
            metadata.Format.SpaceBefore = Unit.FromPoint(2);
            metadata.Format.SpaceAfter = Unit.FromPoint(6);
            metadata.Format.KeepWithNext = true;

            var detailLabel = section.AddParagraph("DETALLE");
            detailLabel.Format.Font.Size = 7;
            detailLabel.Format.Font.Bold = true;
            detailLabel.Format.Font.Color = AccentColor;
            detailLabel.Format.LeftIndent = Unit.FromPoint(8);
            detailLabel.Format.SpaceAfter = Unit.FromPoint(2);
            detailLabel.Format.KeepWithNext = true;

            var detail = section.AddParagraph();
            detail.Format.Font.Size = 9;
            detail.Format.LeftIndent = Unit.FromPoint(8);
            detail.Format.RightIndent = Unit.FromPoint(4);
            detail.Format.SpaceAfter = Unit.FromPoint(8);
            AppendMultilineText(detail, record.Detalle);

            var separator = section.AddParagraph();
            separator.Format.Borders.Bottom.Width = Unit.FromPoint(0.5);
            separator.Format.Borders.Bottom.Color = BorderColor;
            separator.Format.SpaceAfter = Unit.FromPoint(2);
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = MutedColor;
        footer.Format.Borders.Top.Width = Unit.FromPoint(0.7);
        footer.Format.Borders.Top.Color = BorderColor;
        footer.AddText("AUDITORÍA  ·  PÁGINA ");
        footer.AddPageField();
        footer.AddText(" de ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer()
        {
            Document = document
        };
        renderer.RenderDocument();
        renderer.Save(filePath);
    }

    private static void AppendMultilineText(Paragraph paragraph, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                paragraph.AddLineBreak();
            }

            paragraph.AddText(lines[index]);
        }
    }
}
