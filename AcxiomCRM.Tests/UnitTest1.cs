using Xunit;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Tests;

public class BusinessRuleValidationTests
{
    private readonly OpportunityService _opportunityService = new();
    private readonly FollowUpService _followUpService = new(null!, null!);
    private readonly LeadService _leadService = new(null!, null!, null!);

    [Fact]
    public void OpportunityAmount_WhenZeroOrNegative_IsRejected()
    {
        var opp = new Opportunity
        {
            OpportunityName = "Cloud Migration",
            Amount = 0m,
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            Status = "Open"
        };

        var (isValid, errors) = _opportunityService.ValidateOpportunity(opp);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Opportunity Amount must be greater than 0"));
    }

    [Fact]
    public void OpportunityAmount_WhenPositive_IsAccepted()
    {
        var opp = new Opportunity
        {
            OpportunityName = "Cloud Migration",
            Amount = 150000m,
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            Status = "Open"
        };

        var (isValid, errors) = _opportunityService.ValidateOpportunity(opp);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void OpportunityProbability_WhenOutsideRange_IsRejected()
    {
        var oppHigh = new Opportunity
        {
            OpportunityName = "License Deal",
            Amount = 10000m,
            Probability = 105,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            Status = "Open"
        };

        var (isValidHigh, errorsHigh) = _opportunityService.ValidateOpportunity(oppHigh);
        Assert.False(isValidHigh);
        Assert.Contains(errorsHigh, e => e.Contains("Probability must be between 0 and 100"));

        var oppLow = new Opportunity
        {
            OpportunityName = "License Deal",
            Amount = 10000m,
            Probability = -5,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            Status = "Open"
        };

        var (isValidLow, errorsLow) = _opportunityService.ValidateOpportunity(oppLow);
        Assert.False(isValidLow);
        Assert.Contains(errorsLow, e => e.Contains("Probability must be between 0 and 100"));
    }

    [Fact]
    public void OpportunityExpectedCloseDate_WhenInPastForActiveOpportunity_IsRejected()
    {
        var opp = new Opportunity
        {
            OpportunityName = "Expansion",
            Amount = 50000m,
            Probability = 40,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-2), // In the past
            Status = "Open"
        };

        var (isValid, errors) = _opportunityService.ValidateOpportunity(opp);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Expected Close Date cannot be in the past"));
    }

    [Fact]
    public void WeightedPipeline_IsCalculatedCorrectly()
    {
        var opp = new Opportunity
        {
            Amount = 250000m,
            Probability = 40
        };

        // 250,000 * 40 / 100 = 100,000
        Assert.Equal(100000.00m, opp.WeightedPipeline);
        Assert.Equal(100000.00m, _opportunityService.CalculateWeightedPipeline(250000m, 40));
    }

    [Fact]
    public void FollowUpDate_WhenEarlierThanToday_IsRejected()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var (isValid, errorMessage) = _followUpService.ValidateFollowUpDate(pastDate, "Planned");

        Assert.False(isValid);
        Assert.Equal("Follow-up date cannot be earlier than today.", errorMessage);
    }

    [Fact]
    public void FollowUpDate_WhenTodayOrFuture_IsAccepted()
    {
        var futureDate = DateTime.UtcNow.AddDays(2);
        var (isValid, errorMessage) = _followUpService.ValidateFollowUpDate(futureDate, "Planned");

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void LeadStatusWorkflow_ValidTransitions_AreAllowed()
    {
        Assert.True(_leadService.IsValidStatusTransition("New", "Contacted", out _));
        Assert.True(_leadService.IsValidStatusTransition("Contacted", "Qualified", out _));
        Assert.True(_leadService.IsValidStatusTransition("Qualified", "Converted", out _));
    }

    [Fact]
    public void LeadStatusWorkflow_FromConverted_CannotRevert()
    {
        var canRevert = _leadService.IsValidStatusTransition("Converted", "New", out var errorMessage);

        Assert.False(canRevert);
        Assert.NotNull(errorMessage);
        Assert.Contains("converted lead is finalized", errorMessage);
    }

    [Fact]
    public void RoleConstants_AreAccuratelyConfigured()
    {
        Assert.Equal("Admin", AcxiomCRM.Data.DbInitializer.RoleAdmin);
        Assert.Equal("Manager", AcxiomCRM.Data.DbInitializer.RoleManager);
        Assert.Equal("SalesExecutive", AcxiomCRM.Data.DbInitializer.RoleSalesExecutive);
    }

    [Fact]
    public void ApiResponse_ProducesValidPayload()
    {
        var response = AcxiomCRM.DTOs.ApiResponse<string>.Ok("TestData", "Operation Successful");

        Assert.True(response.Success);
        Assert.Equal("TestData", response.Data);
        Assert.Equal("Operation Successful", response.Message);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void CustomerEntity_Instantiation_HasDefaultActiveStatus()
    {
        var customer = new Customer
        {
            CustomerName = "Acxiom Test Corp",
            Email = "corp@acxiom.local",
            Phone = "9876543210"
        };

        Assert.Equal("Active", customer.Status);
        Assert.True(customer.CreatedDate <= DateTime.UtcNow);
    }
}
