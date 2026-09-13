using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
// using Microsoft.AspNetCore.Authorization; 

namespace ResROMS.Pages.Admin
{
    // [Authorize(Roles = "Administrator")] // You will uncomment this later when authentication is fully set up!
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
            // Future logic: Fetch daily stats (e.g., today's revenue, active reservations) to display on the dashboard
        }
    }
}