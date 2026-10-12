namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string? result = "Success",
        string? details = null,
        string? ipAddress = null);
}
