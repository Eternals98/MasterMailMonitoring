using ClosedXML.Excel;
using MailMonitor.Application.Abstractions.Reporting;
using MailMonitor.Domain.Entities.Reporting;
using Microsoft.Extensions.Logging;

namespace MailMonitor.Infrastructure.Reporting
{
    public sealed class ExcelEmailStatisticsExporter : IEmailStatisticsExporter
    {
        private readonly ILogger<ExcelEmailStatisticsExporter> _logger;

        public ExcelEmailStatisticsExporter(ILogger<ExcelEmailStatisticsExporter> logger)
        {
            _logger = logger;
        }

        public Task ExportAsync(IEnumerable<EmailProcessStatistic> statistics, string outputPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var data = statistics?.ToList() ?? new List<EmailProcessStatistic>();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Estadísticas");

            var headers = new[]
            {
                "Fecha",
                "Empresa",
                "Usuario",
                "Procesado",
                "Asunto",
                "Adjuntos",
                "Motivo Ignorado",
                "Buzón",
                "Carpeta de Almacenamiento",
                "Adjuntos Guardados",
                "Message Id"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
                worksheet.Cell(1, column + 1).Style.Font.Bold = true;
                worksheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF8");
            }

            var row = 2;
            foreach (var statistic in data)
            {
                cancellationToken.ThrowIfCancellationRequested();

                worksheet.Cell(row, 1).Value = statistic.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd HH:mm";
                worksheet.Cell(row, 2).Value = statistic.CompanyName;
                worksheet.Cell(row, 3).Value = statistic.UserMail;
                worksheet.Cell(row, 4).Value = statistic.Processed ? "Sí" : "No";
                worksheet.Cell(row, 5).Value = statistic.Subject;
                worksheet.Cell(row, 6).Value = statistic.AttachmentsCount;
                worksheet.Cell(row, 7).Value = statistic.ReasonIgnored;
                worksheet.Cell(row, 8).Value = statistic.Mailbox;
                worksheet.Cell(row, 9).Value = statistic.StorageFolder;
                worksheet.Cell(row, 10).Value = string.Join(", ", statistic.StoredAttachments ?? Enumerable.Empty<string>());
                worksheet.Cell(row, 11).Value = statistic.MessageId ?? string.Empty;

                row++;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.RangeUsed()?.SetAutoFilter();

            workbook.SaveAs(outputPath);
            _logger.LogInformation("Excel email statistics report created at {Path} with {RowCount} rows.", outputPath, data.Count);

            return Task.CompletedTask;
        }
    }
}
