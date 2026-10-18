using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ResROMS.Pages.Staff;

public sealed class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    public ProfileModel(AppDbContext db) => _db = db;

    // Email is the login name and is managed by an Administrator,
    // so Staff can only change their own name and phone number here.
    public sealed class ProfileInput
    {
        [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
        [StringLength(30)] public string Phone { get; set; } = string.Empty;
    }

    [BindProperty] public ProfileInput Input { get; set; } = new();
    public User? CurrentUser { get; private set; }

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToPage("/Login");

        CurrentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (CurrentUser == null) return RedirectToPage("/Login");

        Input = new ProfileInput { FullName = CurrentUser.FullName, Phone = CurrentUser.Phone };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return RedirectToPage("/Login");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return RedirectToPage("/Login");

        if (!ModelState.IsValid)
        {
            CurrentUser = user;
            return Page();
        }

        user.FullName = Input.FullName.Trim();
        user.Phone = (Input.Phone ?? string.Empty).Trim();
        await _db.SaveChangesAsync();

        // Refresh the login cookie so the new name shows straight away.
        // The cookie MUST keep SecurityVersion and SessionPolicyVersion, otherwise
        // the validation in Program.cs rejects it and logs the user out.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("SecurityVersion", user.SecurityVersion.ToString()),
            new("SessionPolicyVersion", "2")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });

        TempData["SuccessMessage"] = "Your profile has been updated.";
        return RedirectToPage();
    }
}