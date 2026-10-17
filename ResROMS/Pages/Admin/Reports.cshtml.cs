using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class ReportsModel : PageModel
{
    private readonly IAdminReportingService _reporting;
    public ReportsModel(IAdminReportingService reporting) => _reporting = reporting;

    public AdminReport? Data { get; private set; }
    public string FromText { get; private set; } = "";
    public string ToText { get; private set; } = "";
    public string Grouping { get; private set; } = "Daily";
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(string? from, string? to, string? grouping,
        CancellationToken cancellationToken)
    {
        if (!TryRange(from, to, grouping, out var range, out var error))
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
        Data = await _reporting.GetReportAsync(range, cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(string? from, string? to,
        string? grouping, string? dataset, CancellationToken cancellationToken)
    {
        if (!TryRange(from, to, grouping, out var range, out var error))
            return BadRequest(error);
        if (dataset is not ("sales" or "reservations" or "popular"))
            return BadRequest("Unknown export type.");

        var data = await _reporting.GetReportAsync(range, cancellationToken);
        var rows = new List<string>();
        switch (dataset)
        {
            case "sales":
                rows.Add(Csv("Period", "Net Collected (RM)", "Non-cancelled Orders"));
                rows.AddRange(data.Trend.Select(t => Csv(t.Label, t.NetCollected.ToString("F2", CultureInfo.InvariantCulture), t.Orders.ToString(CultureInfo.InvariantCulture))));
                break;
            case "reservations":
                rows.Add(Csv("Period", "Non-cancelled Reservations"));
                rows.AddRange(data.Trend.Select(t => Csv(t.Label, t.Reservations.ToString(CultureInfo.InvariantCulture))));
                break;
            case "popular":
                rows.Add(Csv("Menu Item ID", "Item", "Quantity Ordered", "Order Value (RM)"));
                rows.AddRange(data.PopularItems.Select(i => Csv(i.MenuItemId.ToString(CultureInfo.InvariantCulture), i.Name,
                    i.Quantity.ToString(CultureInfo.InvariantCulture), i.OrderValue.ToString("F2", CultureInfo.InvariantCulture))));
                break;
        }
        // Excel-friendly BOM. Escape formula-like CSV values to guard against
        // spreadsheet formula injection from editable menu item names.
        var csv = "\uFEFF" + string.Join("\r\n", rows) + "\r\n";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8",
            $"ResROMS_{dataset}_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}.csv");
    }

    private static string Csv(params string[] values) => string.Join(",", values.Select(v =>
    {
        v = v.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (v.Length > 0 && (v[0] is '=' or '+' or '-' or '@' or '\t')) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }));

    private static bool TryRange(string? from, string? to, string? grouping,
        out ReportRange range, out string error)
    {
        var today = DateOnly.FromDateTime(ReservationClock.NowLocal);
        range = new ReportRange(today.AddDays(-29), today, "Daily");
        error = string.Empty;
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
        range = new ReportRange(first, last, group);
        return true;
    }
}
