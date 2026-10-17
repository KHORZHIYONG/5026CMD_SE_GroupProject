using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages;

[Authorize]
public sealed class AnnouncementsModel : PageModel
{
    private readonly IAnnouncementService _announcements;
    public AnnouncementsModel(IAnnouncementService announcements) => _announcements = announcements;
    public IReadOnlyList<AnnouncementView> Posts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        string role = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Customer") ? "Customer" :
            User.IsInRole("Staff") || User.IsInRole("Waiter") || User.IsInRole("Kitchen Staff") ? "Staff" : "Unknown";
        Posts = await _announcements.GetLiveForRoleAsync(role);
    }
}
