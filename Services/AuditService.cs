using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string? result = "Success",
        string? details = null,
        string? ipAddress = null)
    {
        try
        {
            var audit = new AuditLog
            {
                UserId = userId ?? "Anonymous",
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Result = result ?? "Success",
                Details = details,
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AuditLogs.AddAsync(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Do not fail user requests if auditing fails; log safely
            _logger.LogError(ex, "Failed to write audit log for {Action} on {EntityName}", action, entityName);
        }
    }
}
