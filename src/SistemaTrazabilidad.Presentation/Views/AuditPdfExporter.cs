using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

internal static class AuditPdfExporter
{
    public static void Export(string filePath, IReadOnlyList<AuditRow> records)
    {
        var document = new Document();
        document.Info.Title = "Reporte de auditoría";
        document.Info.Author = "Sistema de Trazabilidad";

        var normalStyle = document.Styles[StyleNames.Normal]
            ?? throw new InvalidOperationException("No se encontró el estilo normal del documento.");
        normalStyle.Font.Name = "Arial";
        normalStyle.Font.Size = 9;
        normalStyle.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);

        var title = section.AddParagraph("Reporte de auditoría");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Colors.DarkBlue;
        title.Format.SpaceAfter = Unit.FromPoint(6);

        var generatedAt = section.AddParagraph($"Generado: {DateTime.Now:g}");
        generatedAt.Format.Font.Size = 9;
        generatedAt.Format.Font.Color = Colors.Gray;

        var recordCount = section.AddParagraph($"Registros exportados: {records.Count}");
        recordCount.Format.Font.Size = 9;
        recordCount.Format.SpaceAfter = Unit.FromPoint(14);

        foreach (var record in records)
        {
            var heading = section.AddParagraph(
                $"{record.FechaHora} | {record.Accion} | {record.Entidad} #{record.Registro}");
            heading.Format.Font.Size = 11;
            heading.Format.Font.Bold = true;
            heading.Format.Font.Color = Colors.DarkBlue;
            heading.Format.SpaceBefore = Unit.FromPoint(8);
            heading.Format.KeepWithNext = true;

            var metadata = section.AddParagraph(
                $"Usuario: {record.Usuario}    Módulo: {record.Modulo}");
            metadata.Format.Font.Size = 8;
            metadata.Format.Font.Color = Colors.Gray;
            metadata.Format.KeepWithNext = true;

            var detail = section.AddParagraph();
            detail.Format.Font.Size = 9;
            detail.Format.SpaceAfter = Unit.FromPoint(8);
            AppendMultilineText(detail, record.Detalle);
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Auditoría · Página ");
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
