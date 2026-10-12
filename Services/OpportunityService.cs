using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class OpportunityService : IOpportunityService
{
    public (bool IsValid, List<string> Errors) ValidateOpportunity(Opportunity opportunity)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
        {
            errors.Add("Opportunity Name is required.");
        }

        // Section 5.3 & 17.7: Opportunity Amount Must be greater than 0
        if (opportunity.Amount <= 0)
        {
            errors.Add("Opportunity Amount must be greater than 0.");
        }

        // Section 5.3 & 17.7: Probability Must be between 0 and 100 inclusive
        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            errors.Add("Probability must be between 0 and 100.");
        }

        // Section 5.3 & 17.7: Expected Close Date Cannot be in the past for an active Opportunity
        if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            errors.Add("Expected Close Date cannot be in the past.");
        }

        return (errors.Count == 0, errors);
    }

    public decimal CalculateWeightedPipeline(decimal amount, int probability)
    {
        if (amount <= 0 || probability <= 0) return 0m;
        return Math.Round(amount * probability / 100m, 2);
    }
}
