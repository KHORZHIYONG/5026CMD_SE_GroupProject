using System.Globalization;
using ClosedXML.Excel;

namespace ResROMS.Services;

/// <summary>Produces a standards-compliant styled workbook using ClosedXML.</summary>
public static class ReportExcelExporter
{
    public static byte[] Generate(AdminReport report, string dataset, string categoryLabel)
    {
        string title;
        string[] headers;
        int[] widths;
        var rows = new List<object[]>();
        string note;
        switch (dataset)
        {
            case "sales":
                title = "Sales & Operations";
                headers = ["Period", "Net Collected (RM)", "Orders"];
                widths = [24, 25, 18];
                rows.AddRange(report.Trend.Select(r => new object[] { r.Label, r.NetCollected, r.Orders }));
                note = "Net collected = recorded payments minus refunds. Not bank settlement.";
                break;
            case "reservations":
                title = "Reservation Activity";
                headers = ["Period", "Non-cancelled Reservations"];
                widths = [24, 34];
                rows.AddRange(report.Trend.Select(r => new object[] { r.Label, r.Reservations }));
                note = "Non-cancelled reservations; period based on reservation start time in Malaysia.";
                break;
            case "popular":
                title = "Popular Menu Items";
                headers = ["Menu Item ID", "Dish", "Quantity Ordered"];
                widths = [19, 52, 25];
                rows.AddRange(report.PopularItems.Select(r => new object[] { r.MenuItemId, r.Name, r.Quantity }));
                note = "Top 10 by quantities in non-cancelled orders, including unpaid orders. Category: " + categoryLabel;
                break;
            default: throw new ArgumentException("Unsupported report dataset", nameof(dataset));
        }
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Report");
        int count = headers.Length;
        var navy = XLColor.FromHtml("#17283E");
        var orange = XLColor.FromHtml("#E85D04");
        var pale = XLColor.FromHtml("#F0F4F9");
        for (int col = 1; col <= count; col++) sheet.Column(col).Width = widths[col - 1];
        sheet.ShowGridLines = false;
        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, count).Merge();
        sheet.Range(1, 1, 1, count).Style.Fill.BackgroundColor = navy;
        sheet.Range(1, 1, 1, count).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, count).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, count).Style.Font.FontSize = 19;
        sheet.Row(1).Height = 42;
        sheet.Cell(2, 1).Value = $"{report.Range.From:dd MMM yyyy} – {report.Range.To:dd MMM yyyy} | {report.Range.GroupBy} grouping | MYT" +
            (dataset == "popular" ? $" | {categoryLabel}" : "");
        sheet.Range(2, 1, 2, count).Merge();
        sheet.Range(2, 1, 2, count).Style.Fill.BackgroundColor = pale;
        sheet.Range(2, 1, 2, count).Style.Font.FontColor = navy;
        sheet.Row(2).Height = 28;
        for (int i = 0; i < headers.Length; i++) sheet.Cell(4, i + 1).Value = headers[i];
        var heading = sheet.Range(4, 1, 4, count);
        heading.Style.Fill.BackgroundColor = orange;
        heading.Style.Font.Bold = true;
        heading.Style.Font.FontColor = XLColor.White;
        sheet.Row(4).Height = 30;
        for (int r = 0; r < rows.Count; r++)
        {
            int row = r + 5;
            for (int col = 0; col < count; col++)
            {
                var cell = sheet.Cell(row, col + 1);
                // Explicitly assign strings as text; never interpret customer dish names as formulas.
                switch (rows[r][col])
                {
                    case int number: cell.Value = number; break;
                    case decimal d: cell.Value = d; break;
                    case long whole: cell.Value = whole; break;
                    default: cell.Value = Convert.ToString(rows[r][col], CultureInfo.InvariantCulture) ?? ""; break;
                }
                if (r % 2 == 1) cell.Style.Fill.BackgroundColor = pale;
                if (dataset == "sales" && col == 1)
                    cell.Style.NumberFormat.Format = "\"RM \"#,##0.00;[Red](\"RM \"#,##0.00)";
            }
            sheet.Row(row).Height = 23;
        }
        int noteRow = rows.Count + 7;
        sheet.Cell(noteRow, 1).Value = note;
        sheet.Range(noteRow, 1, noteRow, count).Merge();
        sheet.Range(noteRow, 1, noteRow, count).Style.Fill.BackgroundColor = pale;
        sheet.Range(noteRow, 1, noteRow, count).Style.Font.FontColor = navy;
        sheet.Row(noteRow).Height = 31;
        // Only include real data rows in the filter; an empty dataset is valid.
        if (rows.Count > 0) sheet.Range(4, 1, rows.Count + 4, count).SetAutoFilter();
        sheet.SheetView.FreezeRows(4);
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
