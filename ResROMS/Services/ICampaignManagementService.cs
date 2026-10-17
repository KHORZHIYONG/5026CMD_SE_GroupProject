using System.ComponentModel.DataAnnotations;

namespace ResROMS.Services;

public sealed class CampaignInput
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(24)] public string? PromoCode { get; set; }
    [Required] public string Type { get; set; } = "Discount";
    [Required] public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; }
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateTime StartDate { get; set; } = ReservationClock.NowLocal.Date;
    [DataType(DataType.Date)] public DateTime EndDate { get; set; } = ReservationClock.NowLocal.Date.AddDays(30);
}

public sealed record CampaignSummary(
    int Id, string Name, string? PromoCode, string Type, string DiscountType,
    decimal DiscountValue, string Description, DateTime StartDate, DateTime EndDate,
    bool IsEnabled, int UsageCount, string Status);

public sealed record CampaignResult(bool Success, string Message);

public interface ICampaignManagementService
{
    Task<IReadOnlyList<CampaignSummary>> GetAllAsync();
    Task<CampaignResult> CreateAsync(CampaignInput input);
    Task<CampaignResult> EditAsync(int id, CampaignInput input);
    Task<CampaignResult> SetEnabledAsync(int id, bool enabled);
    Task<CampaignResult> DeleteAsync(int id);
}
