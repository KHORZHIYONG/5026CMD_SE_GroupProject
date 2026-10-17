using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResROMS.Models;

namespace ResROMS.Data;

/// <summary>
/// One-time, development-only initial administrator creation. No default passwords.
/// Stores only a password hash in SQL Server. Configure using User Secrets.
/// </summary>
public static class AdminBootstrap
{
    public static async Task TryCreateAsync(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (!environment.IsDevelopment()) return;

        // Never bootstrap twice, even if the first administrator is deactivated.
        if (await db.Users.AnyAsync(u => u.PasswordHash != null)) return;

        var name = configuration["BootstrapAdmin:FullName"]?.Trim();
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No credentialed users exist. Set BootstrapAdmin values in Visual Studio User Secrets before attempting to sign in.");
            return;
        }
        if (name.Length > 120 || email.Length > 254 || !email.Contains('@') || password.Length < 12)
            throw new InvalidOperationException("BootstrapAdmin settings are invalid; use a valid email and password with at least 12 characters.");
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email))
            throw new InvalidOperationException("The bootstrap email already belongs to a legacy user. Choose a NEW email in User Secrets. Existing users are never promoted automatically.");

        var admin = new User
        {
            FullName = name,
            Email = email,
            Role = "Admin",
            IsActive = true,
            DateJoined = DateTime.UtcNow
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Initial development administrator created. Remove BootstrapAdmin credentials from User Secrets after verifying login.");
    }
}
