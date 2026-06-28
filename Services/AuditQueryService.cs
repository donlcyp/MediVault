using MediVault.Models;

namespace MediVault.Services;

public class AuditQueryService
{
    public IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        string? userId,
        string? action,
        string? entityType,
        DateTime? date)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(x => x.UserId.Contains(userId));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType);
        }

        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(x => x.Timestamp >= start && x.Timestamp < end);
        }

        return query;
    }

    public string BuildCsv(IEnumerable<AuditLog> logs)
    {
        static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Timestamp,UserId,Action,EntityType,Description,Details,IpAddress");

        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(",",
                Csv(log.Timestamp.ToString("O")),
                Csv(log.UserId),
                Csv(log.Action),
                Csv(log.EntityType),
                Csv(log.Description),
                Csv(log.Details),
                Csv(log.IpAddress)));
        }

        return builder.ToString();
    }
}
