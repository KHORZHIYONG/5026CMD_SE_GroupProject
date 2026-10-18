using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Staff Billing page. Payments are saved by IOrderManagementService, the same
// service the Admin Payments page uses, so both sides see the same records.
public sealed class BillingModel : PageModel
{
    private readonly IOrderManagementService _orders;
    public BillingModel(IOrderManagementService orders) => _orders = orders;

    // Orders that are Ready or Completed and have no payment yet
    public IReadOnlyList<OrderSummary> UnpaidOrders { get; private set; } = [];

    // The order shown in the detail panel
    public OrderSummary? SelectedOrder { get; private set; }

    // The payment record for SelectedOrder, if there is one
    public PaymentSummary? ExistingPayment { get; private set; }

    public async Task OnGetAsync(int? orderId)
    {
        var all = await _orders.GetOrdersAsync();
        UnpaidOrders = all
            .Where(o => o.Status is "Ready" or "Completed" && o.PaymentStatus == "Unpaid")
            .OrderBy(o => o.OrderedLocal)
            .ToArray();

        // Default to the first unpaid order when none was picked from the list
        var targetId = orderId ?? UnpaidOrders.Select(o => (int?)o.Id).FirstOrDefault();
        if (targetId.HasValue)
        {
            SelectedOrder = all.FirstOrDefault(o => o.Id == targetId.Value);
            if (SelectedOrder != null)
            {
                var payments = await _orders.GetPaymentsAsync();
                ExistingPayment = payments.FirstOrDefault(p => p.OrderId == SelectedOrder.Id);
            }
        }
    }

    // RECORD a payment. The service checks the order exists, is not cancelled
    // and has no payment yet.
    public async Task<IActionResult> OnPostRecordPaymentAsync(int orderId, string method, string? reference)
    {
        // The service only lets an Admin account be stored as "recorded by",
        // so the staff member's name goes into the reference text instead.
        var staffName = User.Identity?.Name ?? "Staff";
        var note = string.IsNullOrWhiteSpace(reference)
            ? $"Staff: {staffName}"
            : $"{reference.Trim()} (Staff: {staffName})";
        if (note.Length > 100) note = note[..100];

        var result = await _orders.RecordPaymentAsync(orderId, method, note, null);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

        // Billing a Ready order also closes it out, like the original Staff design.
        if (result.Success)
        {
            var summary = (await _orders.GetOrdersAsync()).FirstOrDefault(o => o.Id == orderId);
            if (summary?.Status == "Ready")
                await _orders.UpdateStatusAsync(orderId, "Completed");
        }
        return RedirectToPage(new { orderId });
    }

    // MARK a payment as refunded (record only, no money moves here)
    public async Task<IActionResult> OnPostMarkRefundedAsync(int paymentId, int orderId)
    {
        var result = await _orders.MarkRefundedAsync(paymentId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { orderId });
    }
}