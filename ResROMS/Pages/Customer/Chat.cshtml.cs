using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Data;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

/// <summary>
/// JSON endpoint for the floating assistant (protected by the /Customer folder policy).
/// Uses Gemini when Gemini:ApiKey is configured, and falls back to the rule-based engine otherwise
/// or whenever Gemini fails, is rate-limited or returns nothing.
/// </summary>
public sealed class ChatModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ChatModel(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<IActionResult> OnGetAsync(string? q)
    {
        var customerId = User.GetCustomerId();
        var question = (q ?? string.Empty).Trim();
        if (question.Length > 200) question = question[..200];

        if (question.Length > 0 && CustomerGemini.IsConfigured(_config) && CustomerGemini.AllowRequest(customerId))
        {
            var facts = await CustomerGemini.BuildFactsAsync(_db, customerId, HttpContext.RequestAborted);
            var answer = await CustomerGemini.AskAsync(_config, facts, question, HttpContext.RequestAborted);
            if (answer != null) return new JsonResult(new { reply = answer, source = "gemini" });
        }

        var reply = await CustomerChatEngine.ReplyAsync(_db, customerId, question);
        return new JsonResult(new { reply, source = "rules" });
    }
}
