using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public ProfileModel(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public string Email { get; private set; } = string.Empty;
    public DateTime JoinedUtc { get; private set; }

    [BindProperty] public ProfileInput Details { get; set; } = new();
    [BindProperty] public PasswordInput Password { get; set; } = new();

    public sealed class ProfileInput
    {
        [Required(ErrorMessage = "Full name is required."), StringLength(120)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
        public string? Phone { get; set; }
    }

    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "Enter your current password."), DataType(DataType.Password)]
        public string Current { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a new password.")]
        [StringLength(256, MinimumLength = 12, ErrorMessage = "Use a password with at least 12 characters.")]
        [DataType(DataType.Password)]
        public string New { get; set; } = string.Empty;

        [Compare(nameof(New), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        public string Confirm { get; set; } = string.Empty;
    }

    private async Task<User?> LoadUserAsync()
    {
        var id = User.GetCustomerId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Customer" && u.IsActive);
        if (user != null) { Email = user.Email; JoinedUtc = user.DateJoined; }
        return user;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await LoadUserAsync();
        if (user == null) return Challenge();
        Details = new ProfileInput { FullName = user.FullName, Phone = user.Phone };
        return Page();
    }

    public async Task<IActionResult> OnPostSaveDetailsAsync()
    {
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Password.")).ToList()) ModelState.Remove(key);
        var user = await LoadUserAsync();
        if (user == null) return Challenge();
        if (!ModelState.IsValid) return Page();

        user.FullName = Details.FullName.Trim();
        user.Phone = Details.Phone?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync();

        // Re-issue the cookie so the new name shows immediately
        await CustomerSignIn.IssueAsync(HttpContext, user);
        TempData["SuccessMessage"] = "Profile updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Details.")).ToList()) ModelState.Remove(key);
        var user = await LoadUserAsync();
        if (user == null) return Challenge();
        Details = new ProfileInput { FullName = user.FullName, Phone = user.Phone };
        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrEmpty(user.PasswordHash)
            || _hasher.VerifyHashedPassword(user, user.PasswordHash, Password.Current) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Password.Current", "Current password is incorrect.");
            return Page();
        }

        user.PasswordHash = _hasher.HashPassword(user, Password.New);
        // Revokes every other signed-in session, the same way the admin password reset does
        user.SecurityVersion++;
        await _db.SaveChangesAsync();

        // Keep THIS session signed in with the new security version
        await CustomerSignIn.IssueAsync(HttpContext, user);
        TempData["SuccessMessage"] = "Password changed. Other devices have been signed out.";
        return RedirectToPage();
    }
}
