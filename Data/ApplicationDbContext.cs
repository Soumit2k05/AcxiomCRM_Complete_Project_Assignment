using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customer unique constraints & indexes
        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.CustomerCode).IsUnique();
            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.Phone).IsUnique();
        });

        // Lead unique constraints & indexes
        builder.Entity<Lead>(entity =>
        {
            entity.HasIndex(l => l.LeadCode).IsUnique();
            entity.Property(l => l.ExpectedValue).HasPrecision(18, 2);

            entity.HasOne(l => l.ConvertedCustomer)
                  .WithMany()
                  .HasForeignKey(l => l.ConvertedCustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Opportunity precision & foreign keys
        builder.Entity<Opportunity>(entity =>
        {
            entity.Property(o => o.Amount).HasPrecision(18, 2);

            entity.HasOne(o => o.Customer)
                  .WithMany(c => c.Opportunities)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.Lead)
                  .WithMany(l => l.Opportunities)
                  .HasForeignKey(o => o.LeadId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // FollowUp relationships
        builder.Entity<FollowUp>(entity =>
        {
            entity.HasOne(f => f.Customer)
                  .WithMany(c => c.FollowUps)
                  .HasForeignKey(f => f.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(f => f.Lead)
                  .WithMany(l => l.FollowUps)
                  .HasForeignKey(f => f.LeadId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Activity relationships
        builder.Entity<Activity>(entity =>
        {
            entity.HasOne(a => a.Customer)
                  .WithMany(c => c.Activities)
                  .HasForeignKey(a => a.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Lead)
                  .WithMany(l => l.Activities)
                  .HasForeignKey(a => a.LeadId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditLog indexing
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.CreatedDate);
            entity.HasIndex(a => a.Action);
            entity.HasIndex(a => a.EntityName);
        });
    }
}
