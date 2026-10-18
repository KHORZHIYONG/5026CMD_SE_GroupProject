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

        if (User.IsInRole("Waiter") || User.IsInRole("Kitchen Staff"))  //staff - waiter and kitchen staff go to /Staff/Orders
            return RedirectToPage("/Staff/Orders");

        return RedirectToPage("/AccessDenied");
    }
}
