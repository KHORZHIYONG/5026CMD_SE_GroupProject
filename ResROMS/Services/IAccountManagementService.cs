namespace ResROMS.Services;

public sealed record AccountSummary(
    int Id, string FullName, string Email, string Phone, string Role,
    bool IsActive, DateTime DateJoined, DateTime? LastLoginAt, bool HasPassword);

public sealed record AccountCreation(string FullName, string Email, string Phone, string Role, string Password);
public sealed record AccountUpdate(string FullName, string Email, string Phone, string Role);
public sealed record AccountActionResult(bool Success, string Message);

public interface IAccountManagementService
{
    Task<IReadOnlyList<AccountSummary>> GetUsersAsync();
    Task<AccountActionResult> CreateAsync(AccountCreation input);
    Task<AccountActionResult> UpdateAsync(int id, int actorId, AccountUpdate input);
    Task<AccountActionResult> SetActiveAsync(int id, int actorId, bool active);
    Task<AccountActionResult> ResetPasswordAsync(int id, string password);
}
