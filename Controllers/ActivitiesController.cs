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
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public ActivitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    // GET: Activities
    public async Task<IActionResult> Index(string? type, string? status, string? search)
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .AsNoTracking();

        if (!isAdmin && !isManager)
        {
            query = query.Where(a => a.AssignedTo == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Subject.Contains(term) ||
                                     (a.Description != null && a.Description.Contains(term)) ||
                                     (a.Customer != null && a.Customer.CustomerName.Contains(term)) ||
                                     (a.Lead != null && a.Lead.LeadName.Contains(term)));
        }

        var activities = await query.OrderByDescending(a => a.ActivityDate).ToListAsync();

        ViewData["CurrentType"] = type;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentSearch"] = search;

        return View(activities);
    }

    // GET: Activities/Create
    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        await PopulateDropdowns(customerId, leadId);

        var activity = new Activity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityType = "Call",
            Status = "Completed",
            ActivityDate = DateTime.UtcNow,
            AssignedTo = User.Identity?.Name
        };

        return View(activity);
    }

    // POST: Activities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(activity.AssignedTo))
            {
                activity.AssignedTo = User.Identity?.Name;
            }

            _context.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                newValue: $"{activity.ActivityType}: {activity.Subject}",
                details: "Logged interaction activity.");

            TempData["SuccessMessage"] = "Activity logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(activity.CustomerId, activity.LeadId);
        return View(activity);
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
