using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToPage("/Login");

        if (User.IsInRole("Admin"))
            return RedirectToPage("/Admin/Dashboard");

        if (User.IsInRole("Customer"))
            return RedirectToPage("/Customer/Reservations");

        // Staff module is not implemented yet; don't show the ASP.NET demo page.
        return RedirectToPage("/AccessDenied");
    }
}
