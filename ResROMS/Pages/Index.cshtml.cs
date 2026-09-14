using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Check if the current user is NOT logged in
            if (!User.Identity.IsAuthenticated)
            {
                // Redirect them to the Login page
                return RedirectToPage("/Login");
            }

            // If they are logged in, load the normal Index page
            return Page();
        }
    }
}