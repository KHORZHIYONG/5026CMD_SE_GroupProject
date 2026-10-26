using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// Singleton policy row; restaurant contact information and opening hours
// remain in their existing dedicated tables.
public sealed class SystemSetting
{
    public int Id { get; set; } = 1;
    public bool CustomerBookingsEnabled { get; set; } = true;
    [Range(0, 1440)] public int MinimumBookingNoticeMinutes { get; set; } = 15;
    [Range(1, 365)] public int MaximumBookingAdvanceDays { get; set; } = 90;
    [Range(1, 16)] public int MaximumShiftHours { get; set; } = 12;
    [Range(8, 80)] public int MaximumWeeklyHours { get; set; } = 60;
    [Range(0, 24)] public int MinimumRestHours { get; set; } = 8;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
