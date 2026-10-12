using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FollowUpService(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public (bool IsValid, string? ErrorMessage) ValidateFollowUpDate(DateTime followUpDate, string status)
    {
        if (status == "Planned" && followUpDate.Date < DateTime.UtcNow.Date)
        {
            return (false, "Follow-up date cannot be earlier than today.");
        }
        return (true, null);
    }

    public async Task MarkCompletedAsync(int followUpId, string currentUser, string? completionRemarks)
    {
        var item = await _context.FollowUps.FindAsync(followUpId);
        if (item == null) return;

        var oldStatus = item.Status;
        item.Status = "Completed";
        if (!string.IsNullOrWhiteSpace(completionRemarks))
        {
            item.Remarks = string.IsNullOrEmpty(item.Remarks) 
                ? completionRemarks 
                : $"{item.Remarks} | Completed: {completionRemarks}";
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUser,
            "Update",
            "FollowUp",
            followUpId.ToString(),
            oldValue: oldStatus,
            newValue: "Completed",
            details: $"Marked follow-up '{item.Subject}' as completed.");
    }

    public async Task RescheduleAsync(int followUpId, DateTime newDate, string currentUser, string? reason)
    {
        var item = await _context.FollowUps.FindAsync(followUpId);
        if (item == null) return;

        var oldDate = item.FollowUpDate.ToString("yyyy-MM-dd HH:mm");
        item.FollowUpDate = newDate;
        item.Status = "Planned";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            item.Remarks = string.IsNullOrEmpty(item.Remarks)
                ? $"Rescheduled: {reason}"
                : $"{item.Remarks} | Rescheduled: {reason}";
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUser,
            "Update",
            "FollowUp",
            followUpId.ToString(),
            oldValue: oldDate,
            newValue: newDate.ToString("yyyy-MM-dd HH:mm"),
            details: $"Rescheduled follow-up '{item.Subject}'. Reason: {reason ?? "None"}");
    }
}
