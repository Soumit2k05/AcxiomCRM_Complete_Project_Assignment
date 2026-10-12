using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Reports (Report Hub)
    public IActionResult Index()
    {
        return View();
    }

    // 1. Pipeline Report (Stage-wise and Owner-wise pipeline amount)
    public async Task<IActionResult> PipelineReport(string? export)
    {
        var userEmail = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Opportunities.Include(o => o.Customer).AsNoTracking();
        if (!isAdmin && !isManager)
        {
            query = query.Where(o => o.AssignedTo == userEmail);
        }

        var opps = await query.ToListAsync();

        if (export == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("OpportunityId,Name,Customer,Stage,Probability,Amount,WeightedPipeline,CloseDate,Status,AssignedTo");
            foreach (var o in opps)
            {
                csv.AppendLine($"{o.OpportunityId},\"{o.OpportunityName}\",\"{o.Customer?.CustomerName}\",{o.Stage},{o.Probability},{o.Amount},{o.WeightedPipeline},{o.ExpectedCloseDate:yyyy-MM-dd},{o.Status},{o.AssignedTo}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"PipelineReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return View(opps);
    }

    // 2. Customer Report
    public async Task<IActionResult> CustomerReport(string? status, string? export)
    {
        var userEmail = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Customers.AsNoTracking();
        if (!isAdmin && !isManager)
        {
            query = query.Where(c => c.OwnerId == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();

        if (export == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("CustomerId,CustomerCode,Name,Email,Phone,Company,City,State,Status,CreatedDate,Owner");
            foreach (var c in customers)
            {
                csv.AppendLine($"{c.CustomerId},{c.CustomerCode},\"{c.CustomerName}\",{c.Email},{c.Phone},\"{c.CompanyName}\",{c.City},{c.State},{c.Status},{c.CreatedDate:yyyy-MM-dd},{c.OwnerId}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"CustomerReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return View(customers);
    }

    // 3. Lead Conversion Report
    public async Task<IActionResult> ConversionReport(string? export)
    {
        var userEmail = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Leads.Include(l => l.ConvertedCustomer).AsNoTracking();
        if (!isAdmin && !isManager)
        {
            query = query.Where(l => l.AssignedTo == userEmail);
        }

        var leads = await query.ToListAsync();

        if (export == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("LeadId,LeadCode,Name,Source,Status,Priority,ExpectedValue,CreatedDate,ConvertedCustomer,AssignedTo");
            foreach (var l in leads)
            {
                csv.AppendLine($"{l.LeadId},{l.LeadCode},\"{l.LeadName}\",{l.Source},{l.Status},{l.Priority},{l.ExpectedValue},{l.CreatedDate:yyyy-MM-dd},\"{l.ConvertedCustomer?.CustomerName}\",{l.AssignedTo}");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"LeadConversionReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return View(leads);
    }

    // 4. Follow-Up Report
    public async Task<IActionResult> FollowUpReport(string? export)
    {
        var userEmail = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsNoTracking();
        if (!isAdmin && !isManager)
        {
            query = query.Where(f => f.AssignedTo == userEmail);
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();

        if (export == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("FollowUpId,Subject,Type,FollowUpDate,Status,Customer/Lead,AssignedTo,Remarks");
            foreach (var f in followUps)
            {
                var target = f.Customer?.CustomerName ?? f.Lead?.LeadName ?? "N/A";
                csv.AppendLine($"{f.FollowUpId},\"{f.Subject}\",{f.FollowUpType},{f.FollowUpDate:yyyy-MM-dd HH:mm},{f.Status},\"{target}\",{f.AssignedTo},\"{f.Remarks}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"FollowUpReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return View(followUps);
    }

    // 5. User Activity Report (Admin/Manager only)
    [Authorize(Roles = $"{DbInitializer.RoleAdmin},{DbInitializer.RoleManager}")]
    public async Task<IActionResult> UserActivityReport(string? export)
    {
        var activities = await _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync();

        if (export == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("ActivityId,ActivityType,Subject,Date,Status,AssignedTo,Target");
            foreach (var a in activities)
            {
                var target = a.Customer?.CustomerName ?? a.Lead?.LeadName ?? "N/A";
                csv.AppendLine($"{a.ActivityId},{a.ActivityType},\"{a.Subject}\",{a.ActivityDate:yyyy-MM-dd HH:mm},{a.Status},{a.AssignedTo},\"{target}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"UserActivityReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return View(activities);
    }
}
