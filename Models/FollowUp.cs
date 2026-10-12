using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

[Table("FollowUps")]
public class FollowUp
{
    [Key]
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [StringLength(50)]
    [Display(Name = "Related Type")]
    public string? RelatedType { get; set; } // Customer, Lead, Opportunity

    public int? RelatedId { get; set; }

    [Required(ErrorMessage = "Follow-up date and time is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Follow-Up Date")]
    public DateTime FollowUpDate { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Follow-up type is required.")]
    [StringLength(50)]
    [Display(Name = "Type")]
    public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Task

    [StringLength(1000)]
    [Display(Name = "Remarks / Notes")]
    public string? Remarks { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

    [StringLength(100)]
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer? Customer { get; set; }

    [ForeignKey("LeadId")]
    public virtual Lead? Lead { get; set; }
}
