using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = $"{DbInitializer.RoleAdmin},{DbInitializer.RoleManager}")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: AuditLogs
    public async Task<IActionResult> Index(
        string? userFilter,
        string? moduleFilter,
        string? actionFilter,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1,
        int pageSize = 15)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            query = query.Where(a => a.UserId != null && a.UserId.Contains(userFilter.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(moduleFilter))
        {
            query = query.Where(a => a.EntityName == moduleFilter);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).ToUniversalTime();
            query = query.Where(a => a.CreatedDate < endOfDay);
        }

        var totalItems = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentUserFilter"] = userFilter;
        ViewData["CurrentModuleFilter"] = moduleFilter;
        ViewData["CurrentActionFilter"] = actionFilter;
        ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
        ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewData["TotalItems"] = totalItems;

        // Distinct lists for dropdown filters
        ViewBag.Modules = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();
        ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();

        return View(logs);
    }

    // GET: AuditLogs/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var log = await _context.AuditLogs.FindAsync(id);
        if (log == null) return NotFound();

        return View(log);
    }
}
