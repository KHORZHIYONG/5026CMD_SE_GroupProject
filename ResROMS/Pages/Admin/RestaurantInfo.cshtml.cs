using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace ResROMS.Pages.Admin
{
    public class RestaurantInfoModel : PageModel
    {
        [BindProperty]
        public GeneralInfoInput Input { get; set; }

        public class GeneralInfoInput
        {
            [Required(ErrorMessage = "Restaurant name is required")]
            [Display(Name = "Restaurant Name")]
            public string Name { get; set; }

            [Phone]
            [Display(Name = "Contact Number")]
            public string Phone { get; set; }

            [EmailAddress]
            [Display(Name = "Support Email")]
            public string Email { get; set; }

            [Required]
            public string Address { get; set; }
        }

        public void OnGet()
        {
            // Mock data - In the future, you will load this from your database
            Input = new GeneralInfoInput
            {
                Name = "ResROMS Bistro",
                Phone = "+60 12-345 6789",
                Email = "hello@resromsbistro.com",
                Address = "123 Culinary Lane, Jelutong, Penang, Malaysia"
            };
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page(); // Show validation errors
            }

            // TODO: Save changes to the database here

            // Show a success message to the user
            TempData["SuccessMessage"] = "Restaurant information updated successfully!";
            return RedirectToPage();
        }
    }
}