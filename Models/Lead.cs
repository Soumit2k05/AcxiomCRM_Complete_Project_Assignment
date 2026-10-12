using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

[Table("Leads")]
public class Lead
{
    [Key]
    public int LeadId { get; set; }

    [Required(ErrorMessage = "Lead code is required.")]
    [StringLength(50)]
    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead name is required.")]
    [StringLength(150, ErrorMessage = "Lead name cannot exceed 150 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^(\+?[0-9]{1,4}[ -]?)?[0-9]{10}$", ErrorMessage = "Enter a valid 10-digit phone number.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead source is required.")]
    [StringLength(50)]
    public string Source { get; set; } = "Website"; // Website, Referral, Cold Call, Partner, Event

    [Required(ErrorMessage = "Lead status is required.")]
    [StringLength(50)]
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

    [StringLength(50)]
    public string? Priority { get; set; } = "Medium"; // Low, Medium, High

    [Range(0, 100000000, ErrorMessage = "Expected value must be a non-negative number within allowable range.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Expected Value")]
    public decimal ExpectedValue { get; set; } = 0.00m;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [Display(Name = "Converted Customer ID")]
    public int? ConvertedCustomerId { get; set; }

    // Navigation properties
    [ForeignKey("ConvertedCustomerId")]
    public virtual Customer? ConvertedCustomer { get; set; }

    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
