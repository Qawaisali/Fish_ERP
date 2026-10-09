using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("SupplierDistributionDetails")]
    public class SupplierDistributionDetail : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Distribution Master")]
        public int SupplierDistributionMasterId { get; set; }

        [Required(ErrorMessage = "Child Supplier is required.")]
        [Display(Name = "Child Supplier")]
        public int ChildSupplierId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; } = 0;

        [MaxLength(300)]
        [Column(TypeName = "nvarchar(300)")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(SupplierDistributionMasterId))]
        public virtual SupplierDistributionMaster? DistributionMaster { get; set; }

        [ForeignKey(nameof(ChildSupplierId))]
        public virtual Vendor? ChildSupplier { get; set; }
    }
}
