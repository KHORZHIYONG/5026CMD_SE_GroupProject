using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class DashboardModel : PageModel
{
    private readonly IAdminReportingService _reporting;
    public DashboardModel(IAdminReportingService reporting) => _reporting = reporting;
    public AdminDashboard Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Data = await _reporting.GetDashboardAsync(cancellationToken);
}
