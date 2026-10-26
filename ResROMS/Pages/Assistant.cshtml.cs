using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages;

// Shared customer/staff/admin information page. POST requests use the protected
// /assistant/ask endpoint in Program.cs, not a write-capable Razor handler.
[Authorize]
public sealed class AssistantModel : PageModel
{
    public void OnGet() { }
}
