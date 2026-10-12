using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
[IgnoreAntiforgeryToken]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReportsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /api/reports/pipeline
    [HttpGet("pipeline")]
    public async Task<ActionResult<ApiResponse<PipelineReportDto>>> GetPipelineReport()
    {
        var userEmail = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Opportunities.Include(o => o.Customer).AsNoTracking();

        if (!isAdmin && !isManager)
        {
            query = query.Where(o => o.AssignedTo == userEmail);
        }

        var openOpps = await query.Where(o => o.Status == "Open").ToListAsync();

        var totalPipeline = openOpps.Sum(o => o.Amount);
        var weightedPipeline = openOpps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));

        var stageBreakdown = openOpps
            .GroupBy(o => o.Stage)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var topOpportunities = openOpps
            .OrderByDescending(o => o.Amount)
            .Take(5)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.CustomerName,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                WeightedPipeline = Math.Round(o.Amount * o.Probability / 100m, 2),
                AssignedTo = o.AssignedTo
            })
            .ToList();

        var dto = new PipelineReportDto
        {
            TotalPipelineValue = totalPipeline,
            WeightedPipelineValue = weightedPipeline,
            TotalOpenOpportunities = openOpps.Count,
            StageBreakdown = stageBreakdown,
            TopOpportunities = topOpportunities
        };

        return Ok(ApiResponse<PipelineReportDto>.Ok(dto));
    }
}
