namespace ResROMS.Services;

// Read-only AI. The caller MUST derive isAdmin from the authenticated server principal,
// never from posted form fields.
public sealed record RestaurantChatTurn(string Role, string Text);
public sealed record RestaurantChatReply(bool Success, string Text, bool NeedsConfiguration = false);

public interface IRestaurantAiChatService
{
    Task<RestaurantChatReply> ReplyAsync(
        string message,
        IReadOnlyList<RestaurantChatTurn> history,
        bool isAdmin,
        int periodDays,
        CancellationToken cancellationToken = default);
}
