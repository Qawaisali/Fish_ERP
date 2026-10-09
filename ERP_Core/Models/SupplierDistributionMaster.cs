using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("SupplierDistributionMasters")]
    public class SupplierDistributionMaster : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Distribution No is required.")]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Distribution No")]
        public string DistributionNo { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Date")]
        public DateTime DistributionDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Parent Supplier is required.")]
        [Display(Name = "Parent Supplier")]
        public int ParentSupplierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; } = 0;

        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Voucher")]
        public int? VoucherId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Voucher No")]
        public string? VoucherNo { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ParentSupplierId))]
        public virtual Vendor? ParentSupplier { get; set; }

        [ForeignKey(nameof(VoucherId))]
        public virtual Voucher? Voucher { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }

        public virtual ICollection<SupplierDistributionDetail> Details { get; set; } = new List<SupplierDistributionDetail>();
    }
}
