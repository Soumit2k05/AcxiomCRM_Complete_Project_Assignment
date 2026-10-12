using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public const string RoleAdmin = "Admin";
    public const string RoleManager = "Manager";
    public const string RoleSalesExecutive = "SalesExecutive";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Apply EF Core database migrations
        await context.Database.MigrateAsync();

        // 1. Seed Roles
        string[] roles = [RoleAdmin, RoleManager, RoleSalesExecutive];
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName)
                {
                    Description = $"{roleName} role for AcxiomCRM"
                });
            }
        }

        // 2. Seed Default Users with framework-managed password hashing
        var defaultUsers = new[]
        {
            new { Email = "admin@acxiomcrm.local", FullName = "System Administrator", Role = RoleAdmin, Password = "Admin@12345!" },
            new { Email = "manager@acxiomcrm.local", FullName = "Sarah Jenkins", Role = RoleManager, Password = "Manager@12345!" },
            new { Email = "sales@acxiomcrm.local", FullName = "Alex Rivera", Role = RoleSalesExecutive, Password = "Sales@12345!" }
        };

        foreach (var item in defaultUsers)
        {
            var user = await userManager.FindByEmailAsync(item.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = item.Email,
                    Email = item.Email,
                    FullName = item.FullName,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, item.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, item.Role);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogWarning("Failed to seed user {Email}: {Errors}", item.Email, errors);
                }
            }
        }

        // 3. Seed Sample CRM Data if empty
        if (!await context.Customers.AnyAsync())
        {
            var customer1 = new Customer
            {
                CustomerCode = "CUST-1001",
                CustomerName = "Apex Global Technologies",
                Email = "contact@apextech.io",
                Phone = "9876543210",
                CompanyName = "Apex Global Corp",
                Address = "104 Tech Boulevard, Sector 5",
                City = "Bengaluru",
                State = "Karnataka",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                CreatedBy = "admin@acxiomcrm.local",
                OwnerId = "sales@acxiomcrm.local"
            };

            var customer2 = new Customer
            {
                CustomerCode = "CUST-1002",
                CustomerName = "Summit Financial Advisory",
                Email = "info@summitfin.com",
                Phone = "9876543211",
                CompanyName = "Summit Holdings Ltd",
                Address = "50 Financial District, Nariman Point",
                City = "Mumbai",
                State = "Maharashtra",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "manager@acxiomcrm.local",
                OwnerId = "sales@acxiomcrm.local"
            };

            var customer3 = new Customer
            {
                CustomerCode = "CUST-1003",
                CustomerName = "Nexus Cloud Logistics",
                Email = "support@nexuscloud.net",
                Phone = "9876543212",
                CompanyName = "Nexus Logistics Pvt",
                Address = "12 Ring Road",
                City = "Hyderabad",
                State = "Telangana",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                CreatedBy = "admin@acxiomcrm.local",
                OwnerId = "sales@acxiomcrm.local"
            };

            await context.Customers.AddRangeAsync(customer1, customer2, customer3);
            await context.SaveChangesAsync();

            var lead1 = new Lead
            {
                LeadCode = "LEAD-2001",
                LeadName = "Vanguard Innovations",
                Email = "inquiries@vanguard.org",
                Phone = "9876543213",
                CompanyName = "Vanguard Labs",
                Source = "Website",
                Status = "New",
                Priority = "High",
                ExpectedValue = 450000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                AssignedTo = "sales@acxiomcrm.local"
            };

            var lead2 = new Lead
            {
                LeadCode = "LEAD-2002",
                LeadName = "Starlight Retail Systems",
                Email = "operations@starlightretail.com",
                Phone = "9876543214",
                CompanyName = "Starlight Ventures",
                Source = "Referral",
                Status = "Contacted",
                Priority = "Medium",
                ExpectedValue = 280000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-4),
                AssignedTo = "sales@acxiomcrm.local"
            };

            var lead3 = new Lead
            {
                LeadCode = "LEAD-2003",
                LeadName = "Orion Smart Health",
                Email = "health@orionsmart.com",
                Phone = "9876543215",
                CompanyName = "Orion LifeSciences",
                Source = "Partner",
                Status = "Qualified",
                Priority = "High",
                ExpectedValue = 750000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-2),
                AssignedTo = "sales@acxiomcrm.local"
            };

            await context.Leads.AddRangeAsync(lead1, lead2, lead3);
            await context.SaveChangesAsync();

            var opportunities = new[]
            {
                new Opportunity
                {
                    OpportunityName = "Apex Cloud Migration Enterprise Suite",
                    CustomerId = customer1.CustomerId,
                    Amount = 620000.00m,
                    Stage = "Proposal",
                    Probability = 60,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                    Status = "Open",
                    AssignedTo = "sales@acxiomcrm.local",
                    Notes = "Finalizing scope review and compliance verification."
                },
                new Opportunity
                {
                    OpportunityName = "Summit Wealth Management Portal",
                    CustomerId = customer2.CustomerId,
                    Amount = 940000.00m,
                    Stage = "Negotiation",
                    Probability = 80,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
                    Status = "Open",
                    AssignedTo = "sales@acxiomcrm.local",
                    Notes = "Commercial terms currently with procurement."
                },
                new Opportunity
                {
                    OpportunityName = "Nexus Telematics Integration",
                    CustomerId = customer3.CustomerId,
                    Amount = 350000.00m,
                    Stage = "Won",
                    Probability = 100,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(5),
                    Status = "Won",
                    AssignedTo = "sales@acxiomcrm.local",
                    Notes = "Deal successfully closed and signed."
                },
                new Opportunity
                {
                    OpportunityName = "Vanguard AI Pilot Implementation",
                    LeadId = lead1.LeadId,
                    Amount = 180000.00m,
                    Stage = "Qualification",
                    Probability = 30,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                    Status = "Open",
                    AssignedTo = "sales@acxiomcrm.local",
                    Notes = "Discovery call scheduled next week."
                }
            };

            await context.Opportunities.AddRangeAsync(opportunities);
            await context.SaveChangesAsync();

            var followUps = new[]
            {
                new FollowUp
                {
                    CustomerId = customer1.CustomerId,
                    RelatedType = "Customer",
                    RelatedId = customer1.CustomerId,
                    FollowUpDate = DateTime.UtcNow.AddDays(1),
                    Subject = "Security Review Briefing",
                    FollowUpType = "Meeting",
                    Remarks = "Walk through SSO and audit architecture with client security team.",
                    Status = "Planned",
                    AssignedTo = "sales@acxiomcrm.local"
                },
                new FollowUp
                {
                    CustomerId = customer2.CustomerId,
                    RelatedType = "Customer",
                    RelatedId = customer2.CustomerId,
                    FollowUpDate = DateTime.UtcNow.AddDays(3),
                    Subject = "Follow-up on revised service level agreement",
                    FollowUpType = "Call",
                    Remarks = "Confirm approval from leadership.",
                    Status = "Planned",
                    AssignedTo = "sales@acxiomcrm.local"
                }
            };

            await context.FollowUps.AddRangeAsync(followUps);

            var activities = new[]
            {
                new Activity
                {
                    CustomerId = customer1.CustomerId,
                    ActivityType = "Call",
                    Subject = "Introductory Alignment Call",
                    Description = "Discussed high level expectations and migration requirements.",
                    ActivityDate = DateTime.UtcNow.AddDays(-2),
                    AssignedTo = "sales@acxiomcrm.local",
                    Status = "Completed"
                },
                new Activity
                {
                    LeadId = lead2.LeadId,
                    ActivityType = "Email",
                    Subject = "Product Demo Deck Sent",
                    Description = "Shared CRM brochure and enterprise comparison matrix.",
                    ActivityDate = DateTime.UtcNow.AddDays(-1),
                    AssignedTo = "sales@acxiomcrm.local",
                    Status = "Completed"
                }
            };

            await context.Activities.AddRangeAsync(activities);

            var auditLog = new AuditLog
            {
                UserId = "System",
                Action = "Create",
                EntityName = "Database",
                RecordId = "Init",
                Details = "Initialized AcxiomCRM database seed records and security roles.",
                Result = "Success",
                IpAddress = "127.0.0.1",
                CreatedDate = DateTime.UtcNow
            };

            await context.AuditLogs.AddAsync(auditLog);
            await context.SaveChangesAsync();
        }
    }
}
