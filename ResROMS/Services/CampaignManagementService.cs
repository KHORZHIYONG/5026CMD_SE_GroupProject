using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using System.Text.RegularExpressions;

namespace ResROMS.Services;

public sealed class CampaignManagementService : ICampaignManagementService
{
    private static readonly HashSet<string> Types = ["Discount", "Happy Hour", "Seasonal"];
    private static readonly HashSet<string> DiscountTypes = ["None", "Percentage", "FixedAmount"];
    private readonly AppDbContext _db;
    private readonly ILogger<CampaignManagementService> _logger;

    public CampaignManagementService(AppDbContext db, ILogger<CampaignManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CampaignSummary>> GetAllAsync()
    {
        var today = ReservationClock.NowLocal.Date;
        var rows = await _db.Campaigns.AsNoTracking()
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Take(300).ToListAsync();
        return rows.Select(c => new CampaignSummary(c.Id, c.Name, c.PromoCode, c.Type,
            c.DiscountType, c.DiscountValue, c.Description, c.StartDate, c.EndDate,
            c.IsEnabled, c.UsageCount,
            !c.IsEnabled ? "Paused" : today < c.StartDate ? "Scheduled" :
            today > c.EndDate ? "Ended" : "Active")).ToList();
    }

    public async Task<CampaignResult> CreateAsync(CampaignInput input)
    {
        var validation = Validate(input);
        if (validation is not null) return Fail(validation);
        var code = NormalizeCode(input.PromoCode);
        if (code is not null && await _db.Campaigns.AnyAsync(c => c.PromoCode == code))
            return Fail("This promotion code is already used by another campaign.");

        var now = DateTime.UtcNow;
        var campaign = new Campaign { CreatedUtc = now, UpdatedUtc = now };
        Apply(campaign, input, code);
        _db.Campaigns.Add(campaign);
        try
        {
            await _db.SaveChangesAsync();
            return Ok($"Campaign '{campaign.Name}' created successfully.");
        }
        catch (DbUpdateException ex) { return SaveFailure(ex); }
    }

    public async Task<CampaignResult> EditAsync(int id, CampaignInput input)
    {
        var validation = Validate(input);
        if (validation is not null) return Fail(validation);
        var campaign = await _db.Campaigns.FindAsync(id);
        if (campaign is null) return Fail("Campaign not found.");
        var code = NormalizeCode(input.PromoCode);
        if (code is not null && await _db.Campaigns.AnyAsync(c => c.Id != id && c.PromoCode == code))
            return Fail("This promotion code is already used by another campaign.");

        Apply(campaign, input, code);
        campaign.UpdatedUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync();
            return Ok($"Campaign '{campaign.Name}' updated.");
        }
        catch (DbUpdateException ex) { return SaveFailure(ex); }
    }

    public async Task<CampaignResult> SetEnabledAsync(int id, bool enabled)
    {
        var campaign = await _db.Campaigns.FindAsync(id);
        if (campaign is null) return Fail("Campaign not found.");
        campaign.IsEnabled = enabled;
        campaign.UpdatedUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync();
            return Ok(enabled ? "Campaign resumed." : "Campaign paused.");
        }
        catch (DbUpdateException ex) { return SaveFailure(ex); }
    }

    public async Task<CampaignResult> DeleteAsync(int id)
    {
        var campaign = await _db.Campaigns.FindAsync(id);
        if (campaign is null) return Fail("Campaign not found.");
        // Do not erase redeemed campaigns; reports and future order links need history.
        if (campaign.UsageCount != 0)
            return Fail("A redeemed campaign cannot be deleted. Pause it instead.");
        _db.Campaigns.Remove(campaign);
        try
        {
            await _db.SaveChangesAsync();
            return Ok("Unused campaign deleted.");
        }
        catch (DbUpdateException ex) { return SaveFailure(ex); }
    }

    private static string? Validate(CampaignInput? input)
    {
        if (input is null) return "Campaign details are required.";
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120)
            return "Campaign name must be between 1 and 120 characters.";
        if (!Types.Contains(input.Type)) return "Choose a supported campaign type.";
        if (!DiscountTypes.Contains(input.DiscountType)) return "Choose a supported discount type.";
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 1000)
            return "Description is required (maximum 1000 characters).";
        if (input.StartDate == default || input.EndDate == default || input.EndDate.Date < input.StartDate.Date)
            return "End date cannot be earlier than start date.";
        if (input.DiscountType == "None" && input.DiscountValue != 0)
            return "Select a discount type or set the discount value to zero.";
        if (input.DiscountType == "Percentage" && (input.DiscountValue <= 0 || input.DiscountValue > 100))
            return "Percentage discounts must be greater than 0 and at most 100.";
        if (input.DiscountType == "FixedAmount" && (input.DiscountValue <= 0 || input.DiscountValue > 999999.99m))
            return "Fixed discounts must be between RM 0.01 and RM 999999.99.";
        if (decimal.Round(input.DiscountValue, 2) != input.DiscountValue)
            return "Discount values support at most two decimal places.";
        var code = NormalizeCode(input.PromoCode);
        if (code is not null && !Regex.IsMatch(code, "^[A-Z0-9-]{3,24}$", RegexOptions.CultureInvariant))
            return "Promotion codes must be 3–24 letters, numbers, or hyphens.";
        return null;
    }

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static void Apply(Campaign entity, CampaignInput input, string? code)
    {
        entity.Name = input.Name.Trim();
        entity.PromoCode = code;
        entity.Type = input.Type;
        entity.DiscountType = input.DiscountType;
        entity.DiscountValue = input.DiscountValue;
        entity.Description = input.Description.Trim();
        entity.StartDate = input.StartDate.Date;
        entity.EndDate = input.EndDate.Date;
    }

    private CampaignResult SaveFailure(DbUpdateException ex)
    {
        _logger.LogWarning(ex, "Unable to save campaign");
        if (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
            return Fail("This promotion code is already in use. Please use a different code.");
        return Fail("Campaign could not be saved. Please retry, or check database constraints.");
    }

    private static CampaignResult Ok(string message) => new(true, message);
    private static CampaignResult Fail(string message) => new(false, message);
}
