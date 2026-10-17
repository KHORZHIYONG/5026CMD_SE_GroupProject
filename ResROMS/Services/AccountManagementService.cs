using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using System.Net.Mail;

namespace ResROMS.Services;

public sealed class AccountManagementService : IAccountManagementService
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.Ordinal)
    { "Admin", "Kitchen Staff", "Waiter", "Customer" };

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ILogger<AccountManagementService> _logger;

    public AccountManagementService(AppDbContext db, IPasswordHasher<User> hasher,
        ILogger<AccountManagementService> logger)
    {
        _db = db;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AccountSummary>> GetUsersAsync() =>
        await _db.Users.AsNoTracking().OrderBy(u => u.FullName)
            .Select(u => new AccountSummary(u.Id, u.FullName, u.Email, u.Phone,
                u.Role, u.IsActive, u.DateJoined, u.LastLoginAt,
                u.PasswordHash != null))
            .ToListAsync();

    public async Task<AccountActionResult> CreateAsync(AccountCreation input)
    {
        var name = input.FullName.Trim();
        var email = NormalizeEmail(input.Email);
        var phone = input.Phone.Trim();
        var error = Validate(name, email, phone, input.Role);
        if (error is not null) return Fail(error);
        if (!ValidPassword(input.Password)) return Fail("Use a password with at least 12 characters.");
        if (await EmailExistsAsync(email)) return Fail("An account with that email already exists.");

        var user = new User
        {
            FullName = name, Email = email, Phone = phone, Role = input.Role,
            IsActive = true, DateJoined = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, input.Password);
        _db.Users.Add(user);
        return await SaveAsync($"Account for '{name}' created.");
    }

    public async Task<AccountActionResult> UpdateAsync(int id, int actorId, AccountUpdate input)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return Fail("Account not found.");

        var name = input.FullName.Trim();
        var email = NormalizeEmail(input.Email);
        var phone = input.Phone.Trim();
        var error = Validate(name, email, phone, input.Role);
        if (error is not null) return Fail(error);
        if (await EmailExistsAsync(email, id)) return Fail("An account with that email already exists.");
        if (id == actorId && input.Role != "Admin")
            return Fail("You cannot remove your own Administrator permissions.");
        if (user.Role == "Admin" && input.Role != "Admin" && !await HasOtherActiveAdminAsync(id))
            return Fail("At least one active Administrator must remain.");

        bool roleChanged = user.Role != input.Role;
        bool emailChanged = user.Email != email;
        user.FullName = name;
        user.Email = email;
        user.Phone = phone;
        user.Role = input.Role;
        if (roleChanged || emailChanged) user.SecurityVersion++;
        return await SaveAsync($"Account for '{name}' updated.");
    }

    public async Task<AccountActionResult> SetActiveAsync(int id, int actorId, bool active)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return Fail("Account not found.");
        if (!active && id == actorId)
            return Fail("You cannot deactivate your own account.");
        if (!active && user.Role == "Admin" && !await HasOtherActiveAdminAsync(id))
            return Fail("At least one active Administrator must remain.");

        if (user.IsActive != active)
        {
            user.IsActive = active;
            user.SecurityVersion++;
        }
        return await SaveAsync($"Account '{user.FullName}' {(active ? "activated" : "deactivated")}.");
    }

    public async Task<AccountActionResult> ResetPasswordAsync(int id, string password)
    {
        if (!ValidPassword(password)) return Fail("Use a password with at least 12 characters.");
        var user = await _db.Users.FindAsync(id);
        if (user is null) return Fail("Account not found.");
        user.PasswordHash = _hasher.HashPassword(user, password);
        user.SecurityVersion++;
        return await SaveAsync($"Password updated for '{user.FullName}'. All previous sessions are revoked.");
    }

    private async Task<bool> HasOtherActiveAdminAsync(int id) =>
        await _db.Users.AnyAsync(u => u.Id != id && u.IsActive
            && u.Role == "Admin" && u.PasswordHash != null);

    private async Task<bool> EmailExistsAsync(string email, int? excludeId = null) =>
        await _db.Users.AnyAsync(u => u.Email.ToLower() == email &&
            (!excludeId.HasValue || u.Id != excludeId.Value));

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool ValidPassword(string? password) => password is { Length: >= 12 and <= 256 };

    private static string? Validate(string name, string email, string phone, string role)
    {
        if (name.Length is < 1 or > 120) return "Name must contain 1–120 characters.";
        if (email.Length is < 3 or > 254 || !MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            return "Enter a valid email address.";
        if (phone.Length > 30) return "Phone number cannot exceed 30 characters.";
        if (!ValidRoles.Contains(role)) return "Choose a valid account role.";
        return null;
    }

    private async Task<AccountActionResult> SaveAsync(string message)
    {
        try
        {
            await _db.SaveChangesAsync();
            return new(true, message);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not save account changes");
            return Fail("Could not save account details. Check for a duplicate email address.");
        }
    }

    private static AccountActionResult Fail(string message) => new(false, message);
}
