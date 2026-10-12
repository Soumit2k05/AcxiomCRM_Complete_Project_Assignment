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
public class FollowUpsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFollowUpService _followUpService;
    private readonly IAuditService _auditService;

    public FollowUpsController(
        ApplicationDbContext context,
        IFollowUpService followUpService,
        IAuditService auditService)
    {
        _context = context;
        _followUpService = followUpService;
        _auditService = auditService;
    }

    // GET: /api/followups
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<FollowUpDto>>>> GetFollowUps([FromQuery] string? status)
    {
        var query = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        var list = await query
            .OrderBy(f => f.FollowUpDate)
            .Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                FollowUpDate = f.FollowUpDate,
                Subject = f.Subject,
                FollowUpType = f.FollowUpType,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedTo = f.AssignedTo
            })
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<FollowUpDto>>.Ok(list));
    }

    // POST: /api/followups
    [HttpPost]
    public async Task<ActionResult<ApiResponse<FollowUpDto>>> CreateFollowUp([FromBody] FollowUpCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<FollowUpDto>.Fail("Validation failed.", errors));
        }

        var (isValid, errorMessage) = _followUpService.ValidateFollowUpDate(dto.FollowUpDate, "Planned");
        if (!isValid)
        {
            return BadRequest(ApiResponse<FollowUpDto>.Fail(errorMessage!));
        }

        var followUp = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            FollowUpDate = dto.FollowUpDate,
            Subject = dto.Subject,
            FollowUpType = dto.FollowUpType,
            Remarks = dto.Remarks,
            Status = "Planned",
            AssignedTo = dto.AssignedTo ?? User.Identity?.Name ?? "API"
        };

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        var resultDto = new FollowUpDto
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            FollowUpDate = followUp.FollowUpDate,
            Subject = followUp.Subject,
            FollowUpType = followUp.FollowUpType,
            Remarks = followUp.Remarks,
            Status = followUp.Status,
            AssignedTo = followUp.AssignedTo
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Create",
            "FollowUp",
            followUp.FollowUpId.ToString(),
            newValue: followUp.Subject,
            details: "Created follow-up via REST API.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetFollowUps), new { id = followUp.FollowUpId }, ApiResponse<FollowUpDto>.Ok(resultDto, "Follow-up scheduled successfully."));
    }
}
