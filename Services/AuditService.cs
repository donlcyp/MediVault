using MediVault.Data;
using MediVault.Models;

namespace MediVault.Services;

public class AuditService
{
    private readonly MediVaultContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(MediVaultContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string userId,
        string action,
        string description,
        string entityType = "",
        string details = "")
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
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
