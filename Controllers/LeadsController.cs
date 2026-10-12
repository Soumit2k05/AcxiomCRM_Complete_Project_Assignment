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
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILeadService _leadService;
    private readonly IAuditService _auditService;

    public LeadsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILeadService leadService,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _leadService = leadService;
        _auditService = auditService;
    }

    // GET: Leads
    public async Task<IActionResult> Index(string? search, string? status, string? priority, int page = 1, int pageSize = 10)
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Leads.Include(l => l.ConvertedCustomer).AsNoTracking();

        if (!isAdmin && !isManager)
        {
            query = query.Where(l => l.AssignedTo == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l => l.LeadName.Contains(term) ||
                                     l.Email.Contains(term) ||
                                     l.Phone.Contains(term) ||
                                     (l.CompanyName != null && l.CompanyName.Contains(term)) ||
                                     (l.AssignedTo != null && l.AssignedTo.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        var totalItems = await query.CountAsync();
        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentPriority"] = priority;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewData["TotalItems"] = totalItems;

        return View(leads);
    }

    // GET: Leads/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.Opportunities)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (lead.AssignedTo != userEmail)
            {
                return Forbid();
            }
        }

        return View(lead);
    }

    // GET: Leads/Create
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdown();
        var lead = new Lead
        {
            LeadCode = "LEAD-" + DateTime.UtcNow.Ticks.ToString()[^6..],
            Status = "New",
            Priority = "Medium",
            Source = "Website",
            AssignedTo = User.Identity?.Name
        };
        return View(lead);
    }

    // POST: Leads/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        if (await _context.Leads.AnyAsync(l => l.LeadCode == lead.LeadCode))
        {
            ModelState.AddModelError(nameof(lead.LeadCode), "A lead with this code already exists.");
        }

        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError(nameof(lead.ExpectedValue), "Expected value cannot be negative.");
        }

        if (ModelState.IsValid)
        {
            lead.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrEmpty(lead.AssignedTo))
            {
                lead.AssignedTo = User.Identity?.Name;
            }

            _context.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                newValue: $"{lead.LeadName} ({lead.Status})",
                details: $"Created prospective lead: {lead.LeadCode}");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown();
        return View(lead);
    }

    // GET: Leads/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (lead.AssignedTo != userEmail)
            {
                return Forbid();
            }
        }

        await PopulateUsersDropdown();
        return View(lead);
    }

    // POST: Leads/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId) return NotFound();

        var existing = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
        if (existing == null) return NotFound();

        // Validate Status Workflow (Section 4.5 & 17.6)
        if (!_leadService.IsValidStatusTransition(existing.Status, lead.Status, out var transitionError))
        {
            ModelState.AddModelError(nameof(lead.Status), transitionError!);
        }

        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError(nameof(lead.ExpectedValue), "Expected value cannot be negative.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(lead);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    User.Identity?.Name,
                    "Update",
                    "Lead",
                    lead.LeadId.ToString(),
                    oldValue: $"{existing.LeadName} ({existing.Status})",
                    newValue: $"{lead.LeadName} ({lead.Status})",
                    details: $"Updated lead status/details: {lead.LeadCode}");

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Leads.AnyAsync(e => e.LeadId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateUsersDropdown();
        return View(lead);
    }

    // GET: Leads/Convert/5
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "This lead has already been converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.DefaultOppName = $"{lead.LeadName} - Expansion Deal";
        ViewBag.DefaultAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 150000m;
        ViewBag.DefaultCloseDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

        return View(lead);
    }

    // POST: Leads/Convert/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertConfirmed(
        int leadId,
        bool createOpportunity,
        string? opportunityName,
        decimal? opportunityAmount,
        DateTime? expectedCloseDate)
    {
        var result = await _leadService.ConvertLeadAsync(
            leadId,
            User.Identity?.Name ?? "User",
            createOpportunity,
            opportunityName,
            opportunityAmount,
            expectedCloseDate);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Convert), new { id = leadId });
        }

        TempData["SuccessMessage"] = $"Lead successfully converted into Customer '{result.Customer?.CustomerName}'." + 
            (result.Opportunity != null ? $" Opportunity '{result.Opportunity.OpportunityName}' created." : "");

        return RedirectToAction(nameof(Details), "Customers", new { id = result.Customer?.CustomerId });
    }

    // GET: Leads/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FirstOrDefaultAsync(m => m.LeadId == id);
        if (lead == null) return NotFound();

        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            return Forbid();
        }

        return View(lead);
    }

    // POST: Leads/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead != null)
        {
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Delete",
                "Lead",
                id.ToString(),
                oldValue: lead.LeadName,
                newValue: null,
                details: $"Deleted lead #{lead.LeadCode}");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdown()
    {
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.UsersList = new SelectList(users, "Email", "FullName");
    }
}
