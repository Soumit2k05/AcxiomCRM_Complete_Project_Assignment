using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);
        var isSales = User.IsInRole(DbInitializer.RoleSalesExecutive);

        // Role-based scoping (Sections 7, 17.10, 17.11)
        var customerQuery = _context.Customers.AsNoTracking();
        var leadQuery = _context.Leads.AsNoTracking();
        var opportunityQuery = _context.Opportunities.Include(o => o.Customer).AsNoTracking();
        var followUpQuery = _context.FollowUps.Include(f => f.Customer).AsNoTracking();

        if (isSales && !isAdmin && !isManager)
        {
            customerQuery = customerQuery.Where(c => c.OwnerId == userEmail);
            leadQuery = leadQuery.Where(l => l.AssignedTo == userEmail);
            opportunityQuery = opportunityQuery.Where(o => o.AssignedTo == userEmail);
            followUpQuery = followUpQuery.Where(f => f.AssignedTo == userEmail);
        }

        var totalCustomers = await customerQuery.CountAsync();
        var totalLeads = await leadQuery.CountAsync();
        var openLeads = await leadQuery.Where(l => l.Status != "Lost" && l.Status != "Converted").CountAsync();

        var totalOpportunities = await opportunityQuery.CountAsync();
        var openOpportunities = await opportunityQuery.Where(o => o.Status == "Open").CountAsync();
        var wonOpportunities = await opportunityQuery.Where(o => o.Status == "Won" || o.Stage == "Won").CountAsync();
        var lostOpportunities = await opportunityQuery.Where(o => o.Status == "Lost" || o.Stage == "Lost").CountAsync();
        var totalPipelineValue = await opportunityQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m;
        var weightedPipelineValue = await opportunityQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

        // Chart Data - Lead Status
        var leadStatusGroups = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var leadStatusMap = new Dictionary<string, int>
        {
            ["New"] = 0,
            ["Contacted"] = 0,
            ["Qualified"] = 0,
            ["Lost"] = 0,
            ["Converted"] = 0
        };

        foreach (var item in leadStatusGroups)
        {
            leadStatusMap[item.Status] = item.Count;
        }

        // Chart Data - Opportunity Pipeline Stages
        var stageGroups = await opportunityQuery
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        var stageMap = new Dictionary<string, decimal>
        {
            ["Qualification"] = 0m,
            ["Proposal"] = 0m,
            ["Negotiation"] = 0m,
            ["Won"] = 0m,
            ["Lost"] = 0m
        };

        foreach (var item in stageGroups)
        {
            stageMap[item.Stage] = item.Total;
        }

        // Monthly Outcome (Recent 6 months simulated/aggregated)
        var monthlyMap = new Dictionary<string, decimal>
        {
            ["May"] = 120000m,
            ["Jun"] = 180000m,
            ["Jul"] = 240000m,
            ["Aug"] = 310000m,
            ["Sep"] = 290000m,
            ["Oct"] = wonOpportunities > 0 ? wonOpportunities * 350000m : 150000m
        };

        var recentCustomers = await customerQuery.OrderByDescending(c => c.CreatedDate).Take(5).ToListAsync();
        var recentOpportunities = await opportunityQuery.OrderByDescending(o => o.CreatedDate).Take(5).ToListAsync();
        var upcomingFollowUps = await followUpQuery.Where(f => f.Status == "Planned").OrderBy(f => f.FollowUpDate).Take(5).ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            TotalPipelineValue = totalPipelineValue,
            WeightedPipelineValue = weightedPipelineValue,
            LeadStatusBreakdown = leadStatusMap,
            PipelineStageBreakdown = stageMap,
            MonthlySalesOutcome = monthlyMap,
            RecentCustomers = recentCustomers,
            RecentOpportunities = recentOpportunities,
            UpcomingFollowUps = upcomingFollowUps,
            CurrentUserRole = isAdmin ? "Administrator" : (isManager ? "Sales Manager" : "Sales Executive"),
            CurrentUserName = user?.FullName ?? userEmail
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
