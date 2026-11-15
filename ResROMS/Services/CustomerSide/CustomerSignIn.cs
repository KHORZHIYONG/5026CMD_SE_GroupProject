using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ResROMS.Models;

namespace ResROMS.Services.CustomerSide;

/// <summary>
/// Issues the same cookie that Pages/Login.cshtml.cs issues (same claims, SessionPolicyVersion "2",
/// 30-minute session ticket for non-Admin users), so Register/Profile fit the existing auth rules.
/// </summary>
public static class CustomerSignIn
{
    public static Task IssueAsync(HttpContext http, User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("SecurityVersion", user.SecurityVersion.ToString()),
            new Claim("SessionPolicyVersion", "2")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });
    }
}

public static class CustomerClaims
{
    /// <summary>Id of the signed-in user (0 if missing).</summary>
    public static int GetCustomerId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}

public sealed record FoodImageView(string? Url, string Alt, bool Small = false);

public static class CustomerUi
{
    /// <summary>Returns the image URL if it is a plain http(s) address, otherwise null (so no script/data URLs are rendered).</summary>
    public static string? ImageOrNull(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;

    public static string Money(decimal amount) => "RM " + amount.ToString("F2");

    public static string Local(DateTime utc) =>
        ReservationClock.ToLocal(utc).ToString("ddd d MMM, h:mm tt");

    public static string Badge(string status) => status switch
    {
        "New" or "Pending" => "text-bg-warning",
        "Cooking" => "text-bg-info",
        "Confirmed" or "Ready" => "text-bg-success",
        "Completed" or "Paid" => "text-bg-primary",
        "Cancelled" or "Refunded" => "text-bg-secondary",
        "Unpaid" => "text-bg-light border",
        _ => "text-bg-light"
    };

    public static string OrderLabel(string status) => status switch
    {
        "New" => "Received",
        "Cooking" => "Being prepared",
        _ => status
    };
}
