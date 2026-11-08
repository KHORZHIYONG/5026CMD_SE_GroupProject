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
            return RedirectToPage("/Customer/Dashboard");

        if (User.IsInRole("Waiter"))
            return RedirectToPage("/Staff/Orders");

        if (User.IsInRole("Kitchen Staff"))
            return RedirectToPage("/Staff/Kitchen");

        return RedirectToPage("/AccessDenied");
    }
}
