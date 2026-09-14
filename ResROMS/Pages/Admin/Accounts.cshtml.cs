using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages.Admin
{
    public class AccountsModel : PageModel
    {
        public class UserDto
        {
            public int Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = "Staff"; // Admin, Kitchen Staff, Waiter, Customer
            public bool IsActive { get; set; } = true;
            public DateTime DateJoined { get; set; }
            public string LastLogin { get; set; } = string.Empty;
        }

        public class CreateUserDto
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = "Waiter";
            public string TemporaryPassword { get; set; } = string.Empty;
        }

        public List<UserDto> Users { get; set; } = new();

        [BindProperty]
        public CreateUserDto NewUser { get; set; } = new();

        public void OnGet()
        {
            // Sample User Data
            Users = new List<UserDto>
            {
                new UserDto {
                    Id = 1,
                    FullName = "Sarah Jenkins",
                    Email = "sarah.admin@resroms.com",
                    Phone = "+1 555-0142",
                    Role = "Admin",
                    IsActive = true,
                    DateJoined = new DateTime(2025, 1, 15),
                    LastLogin = "Today, 08:30 AM"
                },
                new UserDto {
                    Id = 2,
                    FullName = "Marco Rossi",
                    Email = "chef.marco@resroms.com",
                    Phone = "+1 555-0188",
                    Role = "Kitchen Staff",
                    IsActive = true,
                    DateJoined = new DateTime(2025, 3, 10),
                    LastLogin = "Today, 10:15 AM"
                },
                new UserDto {
                    Id = 3,
                    FullName = "Alex Rivera",
                    Email = "alex.r@resroms.com",
                    Phone = "+1 555-0199",
                    Role = "Waiter",
                    IsActive = true,
                    DateJoined = new DateTime(2025, 6, 01),
                    LastLogin = "Yesterday, 09:45 PM"
                },
                new UserDto {
                    Id = 4,
                    FullName = "Emily Chen",
                    Email = "emily.waiter@resroms.com",
                    Phone = "+1 555-0211",
                    Role = "Waiter",
                    IsActive = false,
                    DateJoined = new DateTime(2025, 7, 20),
                    LastLogin = "3 weeks ago"
                },
                new UserDto {
                    Id = 5,
                    FullName = "David Miller",
                    Email = "dmiller@gmail.com",
                    Phone = "+1 555-0344",
                    Role = "Customer",
                    IsActive = true,
                    DateJoined = new DateTime(2026, 2, 12),
                    LastLogin = "Today, 01:10 PM"
                }
            };
        }

        public IActionResult OnPostAddUser()
        {
            TempData["SuccessMessage"] = $"Account for '{NewUser.FullName}' ({NewUser.Role}) created successfully!";
            return RedirectToPage();
        }
    }
}