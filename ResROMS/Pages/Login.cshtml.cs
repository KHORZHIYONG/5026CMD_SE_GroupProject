using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ResROMS.Pages;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public LoginModel(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin")) return RedirectToPage("/Admin/Dashboard");
            if (User.IsInRole("Customer")) return RedirectToPage("/Customer/Dashboard");
            if (User.IsInRole("Waiter")) return RedirectToPage("/Staff/Orders");
            if (User.IsInRole("Kitchen Staff")) return RedirectToPage("/Staff/Kitchen");  //if staff already logged in, go straight to orders page
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password, or the account is disabled.");
            return Page();
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, Input.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password, or the account is disabled.");
            return Page();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, Input.Password);
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("SecurityVersion", user.SecurityVersion.ToString()),
            new Claim("SessionPolicyVersion", "2")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                // Session-only: do not issue a persistent "Remember me" cookie.
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(
                    user.Role == "Admin" ? 15 : 30)
            });

        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return user.Role switch
        {
            "Admin" => RedirectToPage("/Admin/Dashboard"),
            "Customer" => RedirectToPage("/Customer/Dashboard"),
            "Waiter" => RedirectToPage("/Staff/Orders"),
            "Kitchen Staff" => RedirectToPage("/Staff/Kitchen"),  //after login, staff goes to orders page
            _ => RedirectToPage("/AccessDenied")
        };
    }
}
