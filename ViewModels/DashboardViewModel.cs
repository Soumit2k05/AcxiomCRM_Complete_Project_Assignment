using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }

    // Chart.js Data
    public Dictionary<string, int> LeadStatusBreakdown { get; set; } = new();
    public Dictionary<string, decimal> PipelineStageBreakdown { get; set; } = new();
    public Dictionary<string, decimal> MonthlySalesOutcome { get; set; } = new();

    // Recent items for rapid overview
    public List<Customer> RecentCustomers { get; set; } = new();
    public List<Opportunity> RecentOpportunities { get; set; } = new();
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();

    public string CurrentUserRole { get; set; } = string.Empty;
    public string CurrentUserName { get; set; } = string.Empty;
}
