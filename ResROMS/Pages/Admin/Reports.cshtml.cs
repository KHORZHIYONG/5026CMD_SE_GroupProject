using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class ReportsModel : PageModel
{
    private readonly IAdminReportingService _reporting;
    private readonly AppDbContext _db;
    public ReportsModel(IAdminReportingService reporting, AppDbContext db)
    {
        _reporting = reporting;
        _db = db;
    }

    public AdminReport? Data { get; private set; }
    public string FromText { get; private set; } = "";
    public string ToText { get; private set; } = "";
    public string Grouping { get; private set; } = "Daily";
    public string? ErrorMessage { get; private set; }
    public int? CategoryId { get; private set; }
    public IReadOnlyList<Category> Categories { get; private set; } = [];

    public async Task OnGetAsync(string? from, string? to, string? grouping, int? categoryId,
        CancellationToken cancellationToken)
    {
        Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        CategoryId = categoryId;
        if (!TryRange(from, to, grouping, categoryId, out var range, out var error))
        {
            ErrorMessage = error;
            FromText = from ?? "";
            ToText = to ?? "";
            Grouping = grouping ?? "Daily";
            return;
        }
        FromText = range.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ToText = range.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Grouping = range.GroupBy;
        // Category affects Popular Menu Items only; other report totals stay complete.
        if (range.CategoryId is int id && Categories.All(c => c.Id != id))
        {
            ErrorMessage = "The selected menu category does not exist.";
            return;
        }
        Data = await _reporting.GetReportAsync(range, cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(string? from, string? to,
        string? grouping, int? categoryId, string? dataset, string? format,
        CancellationToken cancellationToken)
    {
        if (!TryRange(from, to, grouping, categoryId, out var range, out var error))
            return BadRequest(error);
        if (dataset is not ("sales" or "reservations" or "popular"))
            return BadRequest("Unknown export type.");
        if (format is not (null or "csv" or "xlsx"))
            return BadRequest("Unknown export format.");
        if (range.CategoryId is int id && !await _db.Categories.AnyAsync(c => c.Id == id, cancellationToken))
            return BadRequest("Unknown category.");

        var data = await _reporting.GetReportAsync(range, cancellationToken);
        string categoryLabel = range.CategoryId.HasValue
            ? await _db.Categories.Where(c => c.Id == range.CategoryId.Value)
                .Select(c => c.Name).FirstAsync(cancellationToken)
            : "All categories";
        var filename = $"ResROMS_{dataset}_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}";

        // True .xlsx exports have styling, filter arrows, frozen headers, formatted
        // numeric cells and sensible column widths. CSV remains available as a
        // simple interchange format for spreadsheets and third-party applications.
        if (format == "xlsx")
        {
            var bytes = ReportExcelExporter.Generate(data, dataset!, categoryLabel);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                filename + ".xlsx");
        }

        var rows = new List<string>();
        switch (dataset)
        {
            case "sales":
                rows.Add(Csv("Period", "Net Collected (RM)", "Non-cancelled Orders"));
                rows.AddRange(data.Trend.Select(t => Csv(t.Label,
                    t.NetCollected.ToString("F2", CultureInfo.InvariantCulture),
                    t.Orders.ToString(CultureInfo.InvariantCulture))));
                break;
            case "reservations":
                rows.Add(Csv("Period", "Non-cancelled Reservations"));
                rows.AddRange(data.Trend.Select(t => Csv(t.Label,
                    t.Reservations.ToString(CultureInfo.InvariantCulture))));
                break;
            case "popular":
                rows.Add(Csv("Menu Item ID", "Item", "Quantity Ordered"));
                rows.AddRange(data.PopularItems.Select(i => Csv(
                    i.MenuItemId.ToString(CultureInfo.InvariantCulture), i.Name,
                    i.Quantity.ToString(CultureInfo.InvariantCulture))));
                break;
        }
        // Formula-like strings in menu item names are escaped for CSV.
        var csv = "\uFEFF" + string.Join("\r\n", rows) + "\r\n";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", filename + ".csv");
    }

    private static string Csv(params string[] values) => string.Join(",", values.Select(v =>
    {
        v = v.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (v.Length > 0 && (v[0] is '=' or '+' or '-' or '@' or '\t')) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }));

    private static bool TryRange(string? from, string? to, string? grouping, int? categoryId,
        out ReportRange range, out string error)
    {
        var today = DateOnly.FromDateTime(ReservationClock.NowLocal);
        range = new ReportRange(today.AddDays(-29), today, "Daily", categoryId);
        error = string.Empty;
        if (categoryId is <= 0)
        {
            error = "Select a valid menu category.";
            return false;
        }
        var group = grouping ?? "Daily";
        if (group is not ("Daily" or "Weekly" or "Monthly"))
        {
            error = "Choose Daily, Weekly or Monthly grouping.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
        {
            range = range with { GroupBy = group };
            return true;
        }
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var first)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var last)
            || first.Year < 2000 || last.Year > 2100 || last < first
            || last.DayNumber - first.DayNumber > 365)
        {
            error = "Choose valid dates from 2000–2100, in order, no more than 366 calendar days inclusive.";
            return false;
        }
        range = new ReportRange(first, last, group, categoryId);
        return true;
    }
}
