using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface ILeadService
{
    bool IsValidStatusTransition(string currentStatus, string targetStatus, out string? errorMessage);
    
    Task<(bool Success, string Message, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(
        int leadId,
        string currentUser,
        bool createOpportunity = true,
        string? opportunityName = null,
        decimal? opportunityAmount = null,
        DateTime? expectedCloseDate = null);
}
