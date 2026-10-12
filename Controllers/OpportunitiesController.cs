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
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpportunityService _opportunityService;
    private readonly IAuditService _auditService;

    public OpportunitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOpportunityService opportunityService,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _opportunityService = opportunityService;
        _auditService = auditService;
    }

    // GET: Opportunities
    public async Task<IActionResult> Index(string? search, string? stage, string? status, int page = 1, int pageSize = 10)
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .AsNoTracking();

        if (!isAdmin && !isManager)
        {
            query = query.Where(o => o.AssignedTo == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.OpportunityName.Contains(term) ||
                                     (o.Customer != null && o.Customer.CustomerName.Contains(term)) ||
                                     (o.Lead != null && o.Lead.LeadName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var totalItems = await query.CountAsync();
        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStage"] = stage;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewData["TotalItems"] = totalItems;

        return View(opportunities);
    }

    // GET: Opportunities/Pipeline
    public async Task<IActionResult> Pipeline()
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Opportunities.Include(o => o.Customer).AsNoTracking();
        if (!isAdmin && !isManager)
        {
            query = query.Where(o => o.AssignedTo == userEmail);
        }

        var opps = await query.ToListAsync();
        return View(opps);
    }

    // GET: Opportunities/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (opportunity.AssignedTo != userEmail)
            {
                return Forbid();
            }
        }

        return View(opportunity);
    }

    // GET: Opportunities/Create
    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        await PopulateDropdowns(customerId, leadId);

        var opp = new Opportunity
        {
            CustomerId = customerId,
            LeadId = leadId,
            Stage = "Qualification",
            Probability = 20,
            Status = "Open",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            AssignedTo = User.Identity?.Name
        };

        return View(opp);
    }

    // POST: Opportunities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // Business Validation via OpportunityService
        var (isValid, errors) = _opportunityService.ValidateOpportunity(opportunity);
        foreach (var err in errors)
        {
            ModelState.AddModelError(string.Empty, err);
        }

        if (ModelState.IsValid)
        {
            opportunity.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrEmpty(opportunity.AssignedTo))
            {
                opportunity.AssignedTo = User.Identity?.Name;
            }

            _context.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                newValue: $"{opportunity.OpportunityName} (₹{opportunity.Amount})",
                details: $"Created opportunity in stage {opportunity.Stage}.");

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId);
        return View(opportunity);
    }

    // GET: Opportunities/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null) return NotFound();

        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (opportunity.AssignedTo != userEmail)
            {
                return Forbid();
            }
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId);
        return View(opportunity);
    }

    // POST: Opportunities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId) return NotFound();

        var existing = await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (existing == null) return NotFound();

        var (isValid, errors) = _opportunityService.ValidateOpportunity(opportunity);
        foreach (var err in errors)
        {
            ModelState.AddModelError(string.Empty, err);
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Sync status with stage
                if (opportunity.Stage == "Won") opportunity.Status = "Won";
                else if (opportunity.Stage == "Lost") opportunity.Status = "Lost";
                else opportunity.Status = "Open";

                _context.Update(opportunity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    User.Identity?.Name,
                    "Update",
                    "Opportunity",
                    opportunity.OpportunityId.ToString(),
                    oldValue: $"{existing.OpportunityName} ({existing.Stage}, ₹{existing.Amount})",
                    newValue: $"{opportunity.OpportunityName} ({opportunity.Stage}, ₹{opportunity.Amount})",
                    details: "Updated opportunity stage and commercial details.");

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Opportunities.AnyAsync(e => e.OpportunityId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId);
        return View(opportunity);
    }

    // GET: Opportunities/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            return Forbid();
        }

        return View(opportunity);
    }

    // POST: Opportunities/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity != null)
        {
            _context.Opportunities.Remove(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Delete",
                "Opportunity",
                id.ToString(),
                oldValue: opportunity.OpportunityName,
                newValue: null,
                details: "Deleted opportunity record.");

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' removed.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);

        var leads = await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync();
        ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);

        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.UsersList = new SelectList(users, "Email", "FullName");
    }
}
