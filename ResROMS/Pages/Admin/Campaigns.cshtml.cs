using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public sealed class CampaignsModel : PageModel
{
    private readonly ICampaignManagementService _service;
    public CampaignsModel(ICampaignManagementService service) => _service = service;

    public IReadOnlyList<CampaignSummary> Campaigns { get; private set; } = [];

    [BindProperty]
    public CampaignInput Form { get; set; } = new();

    [BindProperty]
    public int CampaignId { get; set; }

    public async Task OnGetAsync() => Campaigns = await _service.GetAllAsync();

    public async Task<IActionResult> OnPostCreateAsync() =>
        RedirectWithResult(await _service.CreateAsync(Form));

    public async Task<IActionResult> OnPostEditAsync() =>
        RedirectWithResult(await _service.EditAsync(CampaignId, Form));

    public async Task<IActionResult> OnPostPauseAsync() =>
        RedirectWithResult(await _service.SetEnabledAsync(CampaignId, false));

    public async Task<IActionResult> OnPostResumeAsync() =>
        RedirectWithResult(await _service.SetEnabledAsync(CampaignId, true));

    public async Task<IActionResult> OnPostDeleteAsync() =>
        RedirectWithResult(await _service.DeleteAsync(CampaignId));

    private IActionResult RedirectWithResult(CampaignResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
