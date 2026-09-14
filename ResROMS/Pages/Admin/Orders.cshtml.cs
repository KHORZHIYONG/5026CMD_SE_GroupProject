using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages.Admin
{
    public class OrdersModel : PageModel
    {
        public class OrderDto
        {
            public int Id { get; set; }
            public string TableNumber { get; set; } = string.Empty;
            public string WaiterName { get; set; } = string.Empty;
            public List<string> Items { get; set; } = new();
            public string Status { get; set; } = "New"; // New, Cooking, Ready, Completed
            public DateTime OrderTime { get; set; }
            public decimal TotalAmount { get; set; }
        }

        // Using a static list so the data persists while you test the UI!
        public static List<OrderDto> LiveOrders { get; set; } = new();

        public void OnGet()
        {
            // Only seed data if the board is empty
            if (!LiveOrders.Any())
            {
                LiveOrders.AddRange(new List<OrderDto>
                {
                    new OrderDto { Id = 1001, TableNumber = "T-02", WaiterName = "Alex R.", Items = new List<string> { "2x Truffle Fries", "1x Ribeye Steak" }, Status = "New", OrderTime = DateTime.Now.AddMinutes(-5), TotalAmount = 53.00m },
                    new OrderDto { Id = 1002, TableNumber = "T-05", WaiterName = "Sarah J.", Items = new List<string> { "1x Mushroom Risotto", "2x Coke" }, Status = "New", OrderTime = DateTime.Now.AddMinutes(-2), TotalAmount = 26.50m },
                    new OrderDto { Id = 1003, TableNumber = "P-01", WaiterName = "Emily C.", Items = new List<string> { "4x Lava Cake", "4x Coffee" }, Status = "Cooking", OrderTime = DateTime.Now.AddMinutes(-15), TotalAmount = 56.00m },
                    new OrderDto { Id = 1004, TableNumber = "T-01", WaiterName = "Alex R.", Items = new List<string> { "1x Old Fashioned" }, Status = "Ready", OrderTime = DateTime.Now.AddMinutes(-8), TotalAmount = 14.50m }
                });
            }
        }

        public IActionResult OnPostUpdateStatus(int orderId, string newStatus)
        {
            var order = LiveOrders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                order.Status = newStatus;
                TempData["SuccessMessage"] = $"Order #{orderId} moved to {newStatus}!";
            }
            return RedirectToPage();
        }
    }
}