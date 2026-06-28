using MediVault.Models;

namespace MediVault.ViewModels;

public class AuditLogsViewModel
{
    public List<AuditLog> AuditLogs { get; set; } = [];
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalAuditLogs { get; set; }
    public int FilteredAuditLogs { get; set; }
    public string FilterUser { get; set; } = string.Empty;
    public string FilterAction { get; set; } = string.Empty;
    public string FilterEntityType { get; set; } = string.Empty;
    public string FilterDate { get; set; } = string.Empty;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(FilterUser) ||
        !string.IsNullOrWhiteSpace(FilterAction) ||
        !string.IsNullOrWhiteSpace(FilterEntityType) ||
        !string.IsNullOrWhiteSpace(FilterDate);
}
