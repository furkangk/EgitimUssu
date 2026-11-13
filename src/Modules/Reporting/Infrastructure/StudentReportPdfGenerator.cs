using System.Globalization;
using EgitimUssu.Modules.Reporting.Application;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// M14-4: QuestPDF ile öğrenci gelişim raporunu üretir. Metnin kendisi
/// <see cref="StudentReportComposer"/>'dan gelir (saf ve testli); burada yalnız <b>düzen</b> vardır.
/// <para>
/// Lisans <see cref="LicenseType.Community"/> olarak <b>statik kurucuda</b> ayarlanır: sınıf hangi
/// yoldan kurulursa kurulsun (DI, birim testi) QuestPDF lisans istisnası atmaz.
/// </para>
/// </summary>
public sealed class StudentReportPdfGenerator : IStudentReportPdfGenerator
{
    static StudentReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(StudentReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily(Fonts.Calibri));

                page.Header().Column(header =>
                {
                    header.Item().Text(StudentReportComposer.DocumentTitle).FontSize(18).SemiBold();
                    foreach (var line in StudentReportComposer.HeaderLines(data))
                    {
                        header.Item().PaddingTop(2).Text(line);
                    }
                });

                page.Content().PaddingVertical(16).Column(content =>
                {
                    content.Spacing(14);
                    content.Item().Row(row =>
                    {
                        row.Spacing(10);
                        foreach (var summary in StudentReportComposer.SummaryLines(data))
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(box =>
                            {
                                box.Item().Text(summary.Label).FontSize(9).FontColor(Colors.Grey.Darken1);
                                box.Item().PaddingTop(2).Text(summary.Value).FontSize(14).SemiBold();
                            });
                        }
                    });

                    content.Item().Text("Ders bazlı performans").FontSize(13).SemiBold();

                    if (data.Subjects.Count == 0)
                    {
                        // Gizlilik seviyesi kırılımı kapatmış ya da dönemde veri yok olabilir; boş bir
                        // tablo yerine sebebi belirsiz bırakmayan tek satır yazılır.
                        content.Item().Text("Bu dönem için paylaşılan ders bazlı veri bulunmuyor.")
                            .FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Ders");
                                header.Cell().Element(HeaderCell).Text("Hâkimiyet");
                                header.Cell().Element(HeaderCell).Text("Ortalama net");
                                header.Cell().Element(HeaderCell).Text("Test");
                            });

                            foreach (var subject in data.Subjects)
                            {
                                // Delta 0 ise "(+0)" yazmak gürültü; yalnız gerçek değişim gösterilir.
                                var delta = subject.Delta == 0
                                    ? string.Empty
                                    : $" ({subject.Delta.ToString("+#;-#;0", CultureInfo.InvariantCulture)})";

                                table.Cell().Element(BodyCell).Text(subject.Subject);
                                table.Cell().Element(BodyCell).Text($"{subject.MasteryScore}{delta}");
                                table.Cell().Element(BodyCell).Text(StudentReportComposer.FormatNet(subject.AverageNet));
                                table.Cell().Element(BodyCell)
                                    .Text(subject.TestCount.ToString(CultureInfo.InvariantCulture));
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(data.Note))
                    {
                        content.Item().Column(note =>
                        {
                            note.Item().Text("Öğretmen notu").FontSize(13).SemiBold();
                            note.Item().PaddingTop(4).Text(data.Note);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("EğitimÜssü · sayfa ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Darken1)
        .PaddingVertical(4)
        .DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(4);
}
