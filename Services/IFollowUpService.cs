using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IFollowUpService
{
    (bool IsValid, string? ErrorMessage) ValidateFollowUpDate(DateTime followUpDate, string status);
    Task MarkCompletedAsync(int followUpId, string currentUser, string? completionRemarks);
    Task RescheduleAsync(int followUpId, DateTime newDate, string currentUser, string? reason);
}
