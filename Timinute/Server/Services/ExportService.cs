using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;

namespace Timinute.Server.Services
{
    public class ExportService : IExportService
    {
        public byte[] ToCsv<T>(IEnumerable<T> data)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteRecords(data);
            writer.Flush();

            return memoryStream.ToArray();
        }

        public byte[] ToExcel<T>(IEnumerable<T> data, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var dataList = data.ToList();
            if (dataList.Count > 0)
            {
                worksheet.Cell(1, 1).InsertTable(dataList);
            }
            else
            {
                var properties = typeof(T).GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = properties[i].Name;
                }
            }

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);

            return memoryStream.ToArray();
        }
    }
}
