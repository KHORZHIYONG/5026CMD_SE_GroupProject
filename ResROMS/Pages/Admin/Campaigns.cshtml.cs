using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public sealed class CampaignsModel : PageModel
{
    private readonly ICampaignManagementService _service;
    public CampaignsModel(ICampaignManagementService service) => _service = service;

    public IReadOnlyList<CampaignSummary> Campaigns { get; private set; } = [];
    public int? EditCampaignId { get; private set; }

    [BindProperty]
    public CampaignInput Form { get; set; } = new();

    [BindProperty]
    public int CampaignId { get; set; }

    public async Task OnGetAsync() => Campaigns = await _service.GetAllAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await _service.CreateAsync(Form);
            if (result.Success) return RedirectWithResult(result);
            ModelState.AddModelError("Form.PromoCode", result.Message);
        }
        Campaigns = await _service.GetAllAsync();
        ViewData["ReopenCreateModal"] = true;
        return Page();  // Preserve the posted form and validation errors.
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await _service.EditAsync(CampaignId, Form);
            if (result.Success) return RedirectWithResult(result);
            ModelState.AddModelError("Form.PromoCode", result.Message);
        }
        Campaigns = await _service.GetAllAsync();
        EditCampaignId = CampaignId;
        return Page(); // Preserve every edit field and show its panel on failure.
    }

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
