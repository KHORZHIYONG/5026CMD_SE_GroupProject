using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ResROMS.Pages.Admin;

public class AccountsModel : PageModel
{
    private readonly IAccountManagementService _accounts;
    public AccountsModel(IAccountManagementService accounts) => _accounts = accounts;

    public IReadOnlyList<AccountSummary> Users { get; private set; } = [];
    public CreateUserInput NewUser { get; set; } = new();
    public string? ActiveModal { get; private set; }

    public class CreateUserInput
    {
        [Required, StringLength(120)]
        public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;
        [StringLength(30)]
        public string Phone { get; set; } = string.Empty;
        [Required, RegularExpression("Admin|Kitchen Staff|Waiter|Customer")]
        public string Role { get; set; } = "Waiter";
        [Required, MinLength(12), MaxLength(256), DataType(DataType.Password)]
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class EditUserInput
    {
        [Required, StringLength(120)]
        public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;
        [StringLength(30)]
        public string Phone { get; set; } = string.Empty;
        [Required, RegularExpression("Admin|Kitchen Staff|Waiter|Customer")]
        public string Role { get; set; } = "Waiter";
    }

    public class ResetPasswordInput
    {
        [Required, MinLength(12), MaxLength(256), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => Users = await _accounts.GetUsersAsync();

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private IActionResult Finish(AccountActionResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    private IActionResult InvalidForm()
    {
        TempData["ErrorMessage"] = "Please check the required fields and try again.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddUserAsync([Bind(Prefix = "NewUser")] CreateUserInput input)
    {
        if (!ModelState.IsValid)
        {
            NewUser = input;
            ActiveModal = "addUserModal";
            Users = await _accounts.GetUsersAsync();
            return Page();
        }
        return Finish(await _accounts.CreateAsync(new AccountCreation(
            input.FullName, input.Email, input.Phone, input.Role, input.TemporaryPassword)));
    }

    public async Task<IActionResult> OnPostEditUserAsync(int id, [Bind(Prefix = "EditUser")] EditUserInput input)
    {
        if (!ModelState.IsValid) return InvalidForm();
        return Finish(await _accounts.UpdateAsync(id, ActorId,
            new AccountUpdate(input.FullName, input.Email, input.Phone, input.Role)));
    }

    public async Task<IActionResult> OnPostSetActiveAsync(int id, bool isActive) =>
        Finish(await _accounts.SetActiveAsync(id, ActorId, isActive));

    public async Task<IActionResult> OnPostResetPasswordAsync(int id,
        [Bind(Prefix = "ResetUser")] ResetPasswordInput input)
    {
        if (!ModelState.IsValid) return InvalidForm();
        return Finish(await _accounts.ResetPasswordAsync(id, input.NewPassword));
    }
}
