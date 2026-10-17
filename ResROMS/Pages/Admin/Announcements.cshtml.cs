using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class AnnouncementsModel : PageModel
{
    private readonly IAnnouncementService _announcements;
    public AnnouncementsModel(IAnnouncementService announcements) => _announcements = announcements;

    public sealed class AnnouncementForm
    {
        [Required, StringLength(140)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(2000)] public string Body { get; set; } = string.Empty;
        [Required] public string Audience { get; set; } = "All";
        [Required] public DateTime StartLocal { get; set; } = ReservationClock.NowLocal;
        public DateTime? EndLocal { get; set; }
    }

    [BindProperty]
    public AnnouncementForm Form { get; set; } = new();
    public IReadOnlyList<AnnouncementView> Announcements { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Announcements = await _announcements.GetForAdminAsync();
        Form.StartLocal = ReservationClock.NowLocal.AddMinutes(5);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid) return await InvalidAsync();
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) ? actor : 0;
        return Finish(await _announcements.CreateAsync(ToInput(), id));
    }

    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (!ModelState.IsValid) return await InvalidAsync();
        return Finish(await _announcements.EditAsync(id, ToInput()));
    }

    public async Task<IActionResult> OnPostPublishAsync(int id) =>
        Finish(await _announcements.SetPublishedAsync(id, true));
    public async Task<IActionResult> OnPostUnpublishAsync(int id) =>
        Finish(await _announcements.SetPublishedAsync(id, false));
    public async Task<IActionResult> OnPostArchiveAsync(int id) =>
        Finish(await _announcements.SetArchivedAsync(id, true));
    public async Task<IActionResult> OnPostRestoreAsync(int id) =>
        Finish(await _announcements.SetArchivedAsync(id, false));
    public async Task<IActionResult> OnPostDeleteAsync(int id) =>
        Finish(await _announcements.DeleteDraftAsync(id));

    private AnnouncementInput ToInput() => new(Form.Title, Form.Body, Form.Audience,
        Form.StartLocal, Form.EndLocal);

    private IActionResult Finish(OperationResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    private async Task<IActionResult> InvalidAsync()
    {
        Announcements = await _announcements.GetForAdminAsync();
        return Page();
    }
}
