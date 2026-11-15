using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages;

/// <summary>Public customer sign-up (lives at /Register, outside the protected /Customer folder).</summary>
public sealed class RegisterModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public RegisterModel(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty] public RegisterInput Input { get; set; } = new();

    public sealed class RegisterInput
    {
        [Required(ErrorMessage = "Please enter your name."), StringLength(120)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email."), EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Please choose a password."), DataType(DataType.Password)]
        [StringLength(256, MinimumLength = 12, ErrorMessage = "Use a password with at least 12 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Customer"))
            return RedirectToPage("/Customer/Dashboard");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            ModelState.AddModelError("Input.Email", "Enter a valid email address.");
            return Page();
        }
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email))
        {
            ModelState.AddModelError("Input.Email", "An account with this email already exists.");
            return Page();
        }

        // Public sign-up can only ever create a Customer account.
        var user = new User
        {
            FullName = Input.FullName.Trim(),
            Email = email,
            Phone = Input.Phone?.Trim() ?? string.Empty,
            Role = "Customer",
            IsActive = true,
            DateJoined = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, Input.Password);

        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique-email index: someone registered the same address at the same moment
            ModelState.AddModelError("Input.Email", "An account with this email already exists.");
            return Page();
        }

        await CustomerSignIn.IssueAsync(HttpContext, user);
        return RedirectToPage("/Customer/Dashboard");
    }
}
