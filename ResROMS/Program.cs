using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITableManagementService, TableManagementService>();
builder.Services.AddScoped<IAccountManagementService, AccountManagementService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IRestaurantManagementService, RestaurantManagementService>();
builder.Services.AddScoped<ICampaignManagementService, CampaignManagementService>();
builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ResROMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        // Default lifetime. Login.cshtml.cs issues 15-minute Admin and
        // 30-minute non-Admin tickets using AuthenticationProperties.ExpiresUtc.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
            var versionText = context.Principal?.FindFirst("SecurityVersion")?.Value;
            var sessionPolicyVersion = context.Principal?.FindFirst("SessionPolicyVersion")?.Value;

            // Reject cookies issued by the older, 8-hour/Remember-Me login policy.
            if (sessionPolicyVersion != "2")
            {
                await RejectAsync(context);
                return;
            }

            if (!int.TryParse(idText, out var id) || !int.TryParse(versionText, out var version))
            {
                await RejectAsync(context);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash)
                || user.Role != role || user.SecurityVersion != version)
            {
                await RejectAsync(context);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireAuthenticatedUser().RequireRole("Customer"));
});

builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly")
        .AuthorizeFolder("/Customer", "CustomerOnly"));

var app = builder.Build();

// Database schema is managed with EF migrations, not EnsureCreated().
// Only run after Update-Database has been applied.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(db);
    await AdminBootstrap.TryCreateAsync(db,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        app.Configuration, app.Environment,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminBootstrap"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();

// The root URL is a navigation entry point, never the ASP.NET template page.
// Run this AFTER authentication so existing valid login cookies are recognized,
// and BEFORE Razor Pages endpoints so a stale Index view cannot render here.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/")
    {
        // Prevent older home-page responses being reused from the HTTP cache.
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";

        var target = context.User.Identity?.IsAuthenticated != true
            ? "/Login"
            : context.User.IsInRole("Admin")
                ? "/Admin/Dashboard"
                : context.User.IsInRole("Customer")
                    ? "/Customer/Reservations"
                    : "/AccessDenied";

        context.Response.Redirect(target);
        return;
    }

    await next();
});

app.UseAuthorization();

// Avoid keeping sensitive pages in the browser's normal HTTP cache.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Admin") ||
        context.Request.Path.StartsWithSegments("/Customer"))
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    await next();
});

// A lightweight protected request refreshes sliding expiration ONLY while
// an Administrator is actively using a page (see _AdminLayout.cshtml).
app.MapGet("/auth/keepalive", () => Results.NoContent())
    .RequireAuthorization("AdminOnly");

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();

static async Task RejectAsync(CookieValidatePrincipalContext context)
{
    context.RejectPrincipal();
    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
