namespace Timinute.Server.Services
{
    public interface IExportService
    {
        byte[] ToCsv<T>(IEnumerable<T> data);
        byte[] ToExcel<T>(IEnumerable<T> data, string sheetName);
    }
}
