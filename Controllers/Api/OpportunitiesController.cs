using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[IgnoreAntiforgeryToken]
public class OpportunitiesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IOpportunityService _opportunityService;
    private readonly IAuditService _auditService;

    public OpportunitiesController(
        ApplicationDbContext context,
        IOpportunityService opportunityService,
        IAuditService auditService)
    {
        _context = context;
        _opportunityService = opportunityService;
        _auditService = auditService;
    }

    // GET: /api/opportunities
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<OpportunityDto>>>> GetOpportunities([FromQuery] string? stage)
    {
        var query = _context.Opportunities.Include(o => o.Customer).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        var opps = await query
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                LeadId = o.LeadId,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                WeightedPipeline = Math.Round(o.Amount * o.Probability / 100m, 2),
                AssignedTo = o.AssignedTo
            })
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<OpportunityDto>>.Ok(opps));
    }

    // GET: /api/opportunities/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<OpportunityDto>>> GetOpportunity(int id)
    {
        var o = await _context.Opportunities.Include(x => x.Customer).FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null)
        {
            return NotFound(ApiResponse<OpportunityDto>.Fail($"Opportunity with ID {id} was not found."));
        }

        var dto = new OpportunityDto
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
            LeadId = o.LeadId,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            WeightedPipeline = Math.Round(o.Amount * o.Probability / 100m, 2),
            AssignedTo = o.AssignedTo
        };

        return Ok(ApiResponse<OpportunityDto>.Ok(dto));
    }

    // POST: /api/opportunities
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OpportunityDto>>> CreateOpportunity([FromBody] OpportunityCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<OpportunityDto>.Fail("Validation failed.", errors));
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = "Open",
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = User.Identity?.Name ?? "API"
        };

        var (isValid, validationErrors) = _opportunityService.ValidateOpportunity(opp);
        if (!isValid)
        {
            return BadRequest(ApiResponse<OpportunityDto>.Fail("Business validation failed.", validationErrors));
        }

        _context.Opportunities.Add(opp);
        await _context.SaveChangesAsync();

        var resultDto = new OpportunityDto
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            LeadId = opp.LeadId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            WeightedPipeline = Math.Round(opp.Amount * opp.Probability / 100m, 2),
            AssignedTo = opp.AssignedTo
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Create",
            "Opportunity",
            opp.OpportunityId.ToString(),
            newValue: $"{opp.OpportunityName} (₹{opp.Amount})",
            details: "Created opportunity via REST API.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, ApiResponse<OpportunityDto>.Ok(resultDto, "Opportunity created successfully."));
    }

    // PUT: /api/opportunities/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<OpportunityDto>>> UpdateOpportunity(int id, [FromBody] OpportunityCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<OpportunityDto>.Fail("Validation failed.", errors));
        }

        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null)
        {
            return NotFound(ApiResponse<OpportunityDto>.Fail($"Opportunity with ID {id} was not found."));
        }

        opp.OpportunityName = dto.OpportunityName;
        opp.CustomerId = dto.CustomerId;
        opp.LeadId = dto.LeadId;
        opp.Amount = dto.Amount;
        opp.Stage = dto.Stage;
        opp.Probability = dto.Probability;
        opp.ExpectedCloseDate = dto.ExpectedCloseDate;
        opp.Notes = dto.Notes;

        var (isValid, validationErrors) = _opportunityService.ValidateOpportunity(opp);
        if (!isValid)
        {
            return BadRequest(ApiResponse<OpportunityDto>.Fail("Business validation failed.", validationErrors));
        }

        if (opp.Stage == "Won") opp.Status = "Won";
        else if (opp.Stage == "Lost") opp.Status = "Lost";
        else opp.Status = "Open";

        await _context.SaveChangesAsync();

        var resultDto = new OpportunityDto
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            LeadId = opp.LeadId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            WeightedPipeline = Math.Round(opp.Amount * opp.Probability / 100m, 2),
            AssignedTo = opp.AssignedTo
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Update",
            "Opportunity",
            opp.OpportunityId.ToString(),
            newValue: $"{opp.OpportunityName} ({opp.Stage})",
            details: "Updated opportunity via REST API.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<OpportunityDto>.Ok(resultDto, "Opportunity updated successfully."));
    }
}
