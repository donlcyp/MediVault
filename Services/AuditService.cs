using System.Security.Claims;
using MediVault.Data;
using MediVault.Models;
using Microsoft.AspNetCore.Identity;

namespace MediVault.Services;

public class AuditService
{
    private readonly MediVaultContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditService(
        MediVaultContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public async Task LogAsync(
        string userId,
        string action,
        string description,
        string entityType = "",
        string details = "",
        string? userEmail = null)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var resolvedEmail = userEmail;

        if (string.IsNullOrWhiteSpace(resolvedEmail))
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(principal);
                resolvedEmail = currentUser?.Email ?? principal.FindFirstValue(ClaimTypes.Email);
            }
        }

        resolvedEmail ??= userId;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserEmail = resolvedEmail,
            Action = action,
            Description = description,
            EntityType = entityType,
            Details = details,
            IpAddress = ip,
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }
}
