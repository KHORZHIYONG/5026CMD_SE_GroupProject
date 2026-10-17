namespace ResROMS.Services;

public sealed record RestaurantDetails(string Name, string Phone, string Email, string Address);
public sealed record WeeklyHours(int DayOfWeek, bool IsClosed, TimeOnly OpeningTime, TimeOnly ClosingTime);
public sealed record RestaurantSaveResult(bool Success, string Message);

public interface IRestaurantManagementService
{
    Task<RestaurantDetails> GetDetailsAsync();
    Task<IReadOnlyList<WeeklyHours>> GetHoursAsync();
    Task<RestaurantSaveResult> SaveDetailsAsync(RestaurantDetails details);
    Task<RestaurantSaveResult> SaveHoursAsync(IReadOnlyList<WeeklyHours> schedule);
}
