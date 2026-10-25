using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class SystemSettingsModel : PageModel
{
    private readonly IAdminSettingsService _settings;
    public SystemSettingsModel(IAdminSettingsService settings) => _settings = settings;

    public sealed class SettingsForm
    {
        public bool CustomerBookingsEnabled { get; set; } = true;
        [Range(0, 1440, ErrorMessage = "Choose 0–1,440 minutes.")]
        public int MinimumBookingNoticeMinutes { get; set; } = 15;
        [Range(1, 365, ErrorMessage = "Choose 1–365 days.")]
        public int MaximumBookingAdvanceDays { get; set; } = 90;
        [Range(1, 16)] public int MaximumShiftHours { get; set; } = 12;
        [Range(8, 80)] public int MaximumWeeklyHours { get; set; } = 60;
        [Range(0, 24)] public int MinimumRestHours { get; set; } = 8;
    }

    [BindProperty]
    public SettingsForm Form { get; set; } = new();

    public async Task OnGetAsync()
    {
        var value = await _settings.GetAsync();
        Form = new SettingsForm
        {
            CustomerBookingsEnabled = value.CustomerBookingsEnabled,
            MinimumBookingNoticeMinutes = value.MinimumBookingNoticeMinutes,
            MaximumBookingAdvanceDays = value.MaximumBookingAdvanceDays,
            MaximumShiftHours = value.MaximumShiftHours,
            MaximumWeeklyHours = value.MaximumWeeklyHours,
            MinimumRestHours = value.MinimumRestHours
        };
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await _settings.SaveAsync(new SystemSettingsInput(
            Form.CustomerBookingsEnabled, Form.MinimumBookingNoticeMinutes,
            Form.MaximumBookingAdvanceDays, Form.MaximumShiftHours,
            Form.MaximumWeeklyHours, Form.MinimumRestHours));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToPage();
    }
}
