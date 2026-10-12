using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IOpportunityService
{
    (bool IsValid, List<string> Errors) ValidateOpportunity(Opportunity opportunity);
    decimal CalculateWeightedPipeline(decimal amount, int probability);
}
