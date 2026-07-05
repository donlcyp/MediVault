using Microsoft.AspNetCore.Identity;
using MediVault.Data;

namespace MediVault.Middleware;

public class UserEnabledMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserEnabledMiddleware> _logger;

    public UserEnabledMiddleware(RequestDelegate next, ILogger<UserEnabledMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        // Skip check for unauthenticated users
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Skip check for certain paths (logout, error pages, static files, etc.)
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path != null && (
            path.Contains("/identity/account/logout") ||
            path.Contains("/home/error") ||
            path.Contains("/account/accessdenied") ||
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/images/") ||
            path.StartsWith("/lib/") ||
            path.Contains("/favicon.ico") ||
            path.StartsWith("/_")))
        {
            await _next(context);
            return;
        }

        try
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user != null && !user.IsEnabled)
            {
                _logger.LogWarning("Disabled user {Email} attempted to access {Path}", user.Email, path);
                
                // Sign out the user
                await signInManager.SignOutAsync();
                
                // Redirect to access denied page
                context.Response.Redirect("/Identity/Account/AccessDenied");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user enabled status for {Path}", path);
        }

        await _next(context);
    }
}