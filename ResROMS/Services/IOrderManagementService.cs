namespace ResROMS.Services;

public sealed record OrderLineInput(int MenuItemId, int Quantity);
public sealed record CreateOrderInput(int? CustomerId, int? TableId, int? ReservationId,
    IReadOnlyList<OrderLineInput> Lines, int? CreatedByUserId);
public sealed record OrderLineSummary(string Name, int Quantity, decimal UnitPrice, decimal Total);
public sealed record OrderSummary(int Id, string TableLabel, string CustomerName,
    string EnteredBy, string Status, DateTime OrderedLocal, decimal Subtotal,
    decimal TotalAmount, string PaymentStatus, IReadOnlyList<OrderLineSummary> Lines);
public sealed record MenuChoice(int Id, string Name, decimal Price, string CategoryName);
public sealed record AccountChoice(int Id, string Name);
public sealed record TableChoice(int Id, string Label);
public sealed record OrderOption(int Id, string Label);
public sealed record PaymentSummary(int Id, int OrderId, decimal Amount, string Method,
    string Status, string? Reference, DateTime RecordedLocal, DateTime? RefundedLocal,
    string RecordedBy);
public sealed record OrderActionResult(bool Success, string Message);

public interface IOrderManagementService
{
    Task<IReadOnlyList<OrderSummary>> GetOrdersAsync();
    Task<IReadOnlyList<PaymentSummary>> GetPaymentsAsync();
    Task<IReadOnlyList<MenuChoice>> GetMenuChoicesAsync();
    Task<IReadOnlyList<AccountChoice>> GetCustomerChoicesAsync();
    Task<IReadOnlyList<TableChoice>> GetTableChoicesAsync();
    Task<IReadOnlyList<OrderOption>> GetUnpaidOrdersAsync();
    Task<OrderActionResult> CreateAsync(CreateOrderInput input);
    Task<OrderActionResult> UpdateStatusAsync(int orderId, string newStatus);
    Task<OrderActionResult> RecordPaymentAsync(int orderId, string method, string? reference, int? adminUserId);
    Task<OrderActionResult> MarkRefundedAsync(int paymentId);
}
