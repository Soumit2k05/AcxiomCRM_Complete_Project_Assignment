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
public class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public CustomersController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // GET: /api/customers
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerDto>>>> GetCustomers([FromQuery] string? search)
    {
        var query = _context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(term) ||
                                     c.Email.Contains(term) ||
                                     c.Phone.Contains(term) ||
                                     (c.CompanyName != null && c.CompanyName.Contains(term)));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                CreatedBy = c.CreatedBy,
                OwnerId = c.OwnerId
            })
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<CustomerDto>>.Ok(customers));
    }

    // GET: /api/customers/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetCustomer(int id)
    {
        var c = await _context.Customers.FindAsync(id);
        if (c == null)
        {
            return NotFound(ApiResponse<CustomerDto>.Fail($"Customer with ID {id} was not found."));
        }

        var dto = new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            CreatedBy = c.CreatedBy,
            OwnerId = c.OwnerId
        };

        return Ok(ApiResponse<CustomerDto>.Ok(dto));
    }

    // POST: /api/customers
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> CreateCustomer([FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<CustomerDto>.Fail("Validation failed.", errors));
        }

        // Uniqueness checks (Section 17.5)
        if (await _context.Customers.AnyAsync(c => c.Email == dto.Email))
        {
            return Conflict(ApiResponse<CustomerDto>.Fail("A customer with this email address already exists."));
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
        {
            return Conflict(ApiResponse<CustomerDto>.Fail("A customer with this phone number already exists."));
        }

        if (await _context.Customers.AnyAsync(c => c.CustomerCode == dto.CustomerCode))
        {
            return Conflict(ApiResponse<CustomerDto>.Fail("A customer with this code already exists."));
        }

        var customer = new Customer
        {
            CustomerCode = dto.CustomerCode,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "API",
            OwnerId = User.Identity?.Name ?? "API"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var resultDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            CreatedBy = customer.CreatedBy,
            OwnerId = customer.OwnerId
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Create",
            "Customer",
            customer.CustomerId.ToString(),
            newValue: customer.CustomerName,
            details: $"Created customer via REST API: {customer.CustomerCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, ApiResponse<CustomerDto>.Ok(resultDto, "Customer created successfully."));
    }

    // PUT: /api/customers/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> UpdateCustomer(int id, [FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<CustomerDto>.Fail("Validation failed.", errors));
        }

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(ApiResponse<CustomerDto>.Fail($"Customer with ID {id} was not found."));
        }

        // Uniqueness checks excluding current record
        if (await _context.Customers.AnyAsync(c => c.Email == dto.Email && c.CustomerId != id))
        {
            return Conflict(ApiResponse<CustomerDto>.Fail("Another customer with this email address already exists."));
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone && c.CustomerId != id))
        {
            return Conflict(ApiResponse<CustomerDto>.Fail("Another customer with this phone number already exists."));
        }

        var oldSnapshot = $"{customer.CustomerName} ({customer.Status})";

        customer.CustomerName = dto.CustomerName;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status;
        customer.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var resultDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            CreatedBy = customer.CreatedBy,
            OwnerId = customer.OwnerId
        };

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Update",
            "Customer",
            customer.CustomerId.ToString(),
            oldValue: oldSnapshot,
            newValue: $"{customer.CustomerName} ({customer.Status})",
            details: $"Updated customer via REST API: {customer.CustomerCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<CustomerDto>.Ok(resultDto, "Customer updated successfully."));
    }

    // DELETE: /api/customers/5
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(ApiResponse<string>.Fail($"Customer with ID {id} was not found."));
        }

        // Soft deactivation or deletion according to requirements
        customer.Status = "Inactive";
        customer.ModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            User.Identity?.Name,
            "Delete",
            "Customer",
            id.ToString(),
            oldValue: "Active",
            newValue: "Inactive",
            details: $"Deactivated customer via REST API: {customer.CustomerCode}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<string>.Ok("Customer deactivated successfully."));
    }
}
