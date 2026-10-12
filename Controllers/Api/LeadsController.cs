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
public class LeadsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LeadsController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // GET: /api/leads
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<LeadDto>>>> GetLeads([FromQuery] string? status)
    {
        var query = _context.Leads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedTo = l.AssignedTo
            })
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<LeadDto>>.Ok(leads));
    }

    // GET: /api/leads/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> GetLead(int id)
    {
        var l = await _context.Leads.FindAsync(id);
        if (l == null)
        {
            return NotFound(ApiResponse<LeadDto>.Fail($"Lead with ID {id} was not found."));
        }

        var dto = new LeadDto
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            CreatedDate = l.CreatedDate,
            AssignedTo = l.AssignedTo
        };

        return Ok(ApiResponse<LeadDto>.Ok(dto));
    }

    // POST: /api/leads
    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeadDto>>> CreateLead([FromBody] LeadCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<LeadDto>.Fail("Validation failed.", errors));
        }

        if (await _context.Leads.AnyAsync(l => l.LeadCode == dto.LeadCode))
        {
            return Conflict(ApiResponse<LeadDto>.Fail("A lead with this code already exists."));
        }

        var lead = new Lead
        {
            LeadCode = dto.LeadCode,
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Status = dto.Status,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = User.Identity?.Name ?? "API"
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        var resultDto = new LeadDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            AssignedTo = lead.AssignedTo
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Create",
            "Lead",
            lead.LeadId.ToString(),
            newValue: lead.LeadName,
            details: $"Created lead via REST API: {lead.LeadCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, ApiResponse<LeadDto>.Ok(resultDto, "Lead created successfully."));
    }

    // PUT: /api/leads/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> UpdateLead(int id, [FromBody] LeadCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<LeadDto>.Fail("Validation failed.", errors));
        }

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound(ApiResponse<LeadDto>.Fail($"Lead with ID {id} was not found."));
        }

        var oldSnapshot = $"{lead.LeadName} ({lead.Status})";

        lead.LeadName = dto.LeadName;
        lead.Email = dto.Email;
        lead.Phone = dto.Phone;
        lead.CompanyName = dto.CompanyName;
        lead.Source = dto.Source;
        lead.Status = dto.Status;
        lead.Priority = dto.Priority;
        lead.ExpectedValue = dto.ExpectedValue;

        await _context.SaveChangesAsync();

        var resultDto = new LeadDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            AssignedTo = lead.AssignedTo
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Update",
            "Lead",
            lead.LeadId.ToString(),
            oldValue: oldSnapshot,
            newValue: $"{lead.LeadName} ({lead.Status})",
            details: $"Updated lead via REST API: {lead.LeadCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<LeadDto>.Ok(resultDto, "Lead updated successfully."));
    }

    // DELETE: /api/leads/5
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteLead(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound(ApiResponse<string>.Fail($"Lead with ID {id} was not found."));
        }

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Delete",
            "Lead",
            id.ToString(),
            oldValue: lead.LeadName,
            newValue: null,
            details: $"Deleted lead via REST API: {lead.LeadCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<string>.Ok("Lead deleted successfully."));
    }
}
