using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public sealed class PaymentsModel : PageModel
{
    private readonly IOrderManagementService _orders;
    public PaymentsModel(IOrderManagementService orders) => _orders = orders;
    public IReadOnlyList<PaymentSummary> Payments { get; private set; } = [];
    public IReadOnlyList<OrderOption> UnpaidOrders { get; private set; } = [];
    [BindProperty] public RecordPaymentForm NewPayment { get; set; } = new();

    public sealed class RecordPaymentForm
    {
        public int OrderId { get; set; }
        public string Method { get; set; } = "Cash";
        public string? Reference { get; set; }
    }

    public async Task OnGetAsync()
    {
        Payments = await _orders.GetPaymentsAsync();
        UnpaidOrders = await _orders.GetUnpaidOrdersAsync();
    }

    public async Task<IActionResult> OnPostRecordAsync()
    {
        var adminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : (int?)null;
        if (!adminId.HasValue)
        {
            TempData["ErrorMessage"] = "Administrator account could not be verified. Sign in again.";
            return RedirectToPage();
        }
        var result = await _orders.RecordPaymentAsync(NewPayment.OrderId,
            NewPayment.Method, NewPayment.Reference, adminId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefundAsync(int paymentId)
    {
        var result = await _orders.MarkRefundedAsync(paymentId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
