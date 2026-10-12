using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFollowUpService _followUpService;
    private readonly IAuditService _auditService;

    public FollowUpsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IFollowUpService followUpService,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _followUpService = followUpService;
        _auditService = auditService;
    }

    // GET: FollowUps
    public async Task<IActionResult> Index(string? statusTab, string? search)
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .AsNoTracking();

        if (!isAdmin && !isManager)
        {
            query = query.Where(f => f.AssignedTo == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f => f.Subject.Contains(term) ||
                                     (f.Customer != null && f.Customer.CustomerName.Contains(term)) ||
                                     (f.Lead != null && f.Lead.LeadName.Contains(term)));
        }

        var now = DateTime.UtcNow;

        if (statusTab == "Upcoming")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate >= now);
        }
        else if (statusTab == "Overdue")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate < now);
        }
        else if (!string.IsNullOrWhiteSpace(statusTab) && statusTab != "All")
        {
            query = query.Where(f => f.Status == statusTab);
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();

        ViewData["CurrentTab"] = statusTab ?? "All";
        ViewData["CurrentSearch"] = search;

        return View(followUps);
    }

    // GET: FollowUps/Schedule
    public async Task<IActionResult> Schedule(int? customerId, int? leadId)
    {
        await PopulateDropdowns(customerId, leadId);

        var followUp = new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10), // Tomorrow at 10 AM
            FollowUpType = "Call",
            Status = "Planned",
            AssignedTo = User.Identity?.Name
        };

        return View(followUp);
    }

    // POST: FollowUps/Schedule
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(FollowUp followUp)
    {
        // Business Validation (Section 5.3 & 17.7): Cannot be earlier than today for a new/planned follow-up
        var (isValid, errorMessage) = _followUpService.ValidateFollowUpDate(followUp.FollowUpDate, followUp.Status);
        if (!isValid)
        {
            ModelState.AddModelError(nameof(followUp.FollowUpDate), errorMessage!);
        }

        if (ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(followUp.AssignedTo))
            {
                followUp.AssignedTo = User.Identity?.Name;
            }

            _context.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                newValue: $"{followUp.Subject} ({followUp.FollowUpDate:yyyy-MM-dd})",
                details: $"Scheduled {followUp.FollowUpType} follow-up.");

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(followUp.CustomerId, followUp.LeadId);
        return View(followUp);
    }

    // POST: FollowUps/Complete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? completionRemarks)
    {
        await _followUpService.MarkCompletedAsync(id, User.Identity?.Name ?? "User", completionRemarks);
        TempData["SuccessMessage"] = "Follow-up marked as completed.";
        return RedirectToAction(nameof(Index));
    }

    // POST: FollowUps/Reschedule/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? reason)
    {
        var (isValid, errorMessage) = _followUpService.ValidateFollowUpDate(newDate, "Planned");
        if (!isValid)
        {
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction(nameof(Index));
        }

        await _followUpService.RescheduleAsync(id, newDate, User.Identity?.Name ?? "User", reason);
        TempData["SuccessMessage"] = "Follow-up rescheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: FollowUps/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var followUp = await _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .FirstOrDefaultAsync(m => m.FollowUpId == id);

        if (followUp == null) return NotFound();

        return View(followUp);
    }

    // POST: FollowUps/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp != null)
        {
            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Delete",
                "FollowUp",
                id.ToString(),
                oldValue: followUp.Subject,
                newValue: null,
                details: "Deleted follow-up entry.");

            TempData["SuccessMessage"] = "Follow-up deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);

        var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
        ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);

        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.UsersList = new SelectList(users, "Email", "FullName");
    }
}
