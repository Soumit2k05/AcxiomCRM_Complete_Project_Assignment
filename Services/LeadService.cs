using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<LeadService> _logger;

    public LeadService(ApplicationDbContext context, IAuditService auditService, ILogger<LeadService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public bool IsValidStatusTransition(string currentStatus, string targetStatus, out string? errorMessage)
    {
        errorMessage = null;

        if (string.Equals(currentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Terminal status: Converted
        if (string.Equals(currentStatus, "Converted", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "A converted lead is finalized and its status cannot be changed.";
            return false;
        }

        var validTransitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["New"] = ["Contacted", "Unqualified", "Lost"],
            ["Contacted"] = ["Qualified", "Unqualified", "Lost", "New"],
            ["Qualified"] = ["Converted", "Lost", "Contacted"],
            ["Unqualified"] = ["Contacted", "Lost"],
            ["Lost"] = ["Contacted", "New"]
        };

        if (validTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(targetStatus, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        errorMessage = $"Invalid status transition from '{currentStatus}' to '{targetStatus}'.";
        return false;
    }

    public async Task<(bool Success, string Message, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(
        int leadId,
        string currentUser,
        bool createOpportunity = true,
        string? opportunityName = null,
        decimal? opportunityAmount = null,
        DateTime? expectedCloseDate = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null)
            {
                return (false, $"Lead #{leadId} not found.", null, null);
            }

            if (lead.Status == "Converted")
            {
                return (false, "Lead is already converted.", null, null);
            }

            // 1. Check or create Customer
            var customerCode = "CUST-" + DateTime.UtcNow.Ticks.ToString()[^6..];
            
            // Check duplicate email or phone
            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == lead.Email || c.Phone == lead.Phone);
            Customer customer;

            if (existingCustomer != null)
            {
                customer = existingCustomer;
            }
            else
            {
                customer = new Customer
                {
                    CustomerCode = customerCode,
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUser,
                    OwnerId = lead.AssignedTo ?? currentUser
                };

                await _context.Customers.AddAsync(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    currentUser,
                    "Create",
                    "Customer",
                    customer.CustomerId.ToString(),
                    newValue: customer.CustomerName,
                    details: $"Customer generated from Lead conversion #{lead.LeadCode}");
            }

            // 2. Optionally create Opportunity
            Opportunity? opportunity = null;
            if (createOpportunity)
            {
                var oppAmount = opportunityAmount ?? (lead.ExpectedValue > 0 ? lead.ExpectedValue : 100000m);
                var oppClose = expectedCloseDate ?? DateTime.UtcNow.AddDays(30);

                if (oppAmount <= 0)
                {
                    await transaction.RollbackAsync();
                    return (false, "Opportunity Amount must be greater than 0.", null, null);
                }

                if (oppClose.Date < DateTime.UtcNow.Date)
                {
                    await transaction.RollbackAsync();
                    return (false, "Expected close date cannot be in the past for active opportunities.", null, null);
                }

                opportunity = new Opportunity
                {
                    OpportunityName = !string.IsNullOrWhiteSpace(opportunityName) ? opportunityName : $"{lead.LeadName} - Deal",
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = oppAmount,
                    Stage = "Qualification",
                    Probability = 25,
                    ExpectedCloseDate = oppClose,
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo ?? currentUser,
                    Notes = $"Converted from Lead #{lead.LeadCode}"
                };

                await _context.Opportunities.AddAsync(opportunity);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    currentUser,
                    "Create",
                    "Opportunity",
                    opportunity.OpportunityId.ToString(),
                    newValue: opportunity.OpportunityName,
                    details: $"Opportunity created during conversion of Lead #{lead.LeadCode}");
            }

            // 3. Update Lead Status
            var oldStatus = lead.Status;
            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUser,
                "Conversion",
                "Lead",
                lead.LeadId.ToString(),
                oldValue: oldStatus,
                newValue: "Converted",
                details: $"Converted to Customer #{customer.CustomerId} (Code: {customer.CustomerCode}) and Opportunity #{opportunity?.OpportunityId}");

            await transaction.CommitAsync();
            return (true, "Lead successfully converted to Customer and Opportunity.", customer, opportunity);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed while converting lead #{LeadId}", leadId);
            return (false, "Conversion failed due to a system error: " + ex.Message, null, null);
        }
    }
}
