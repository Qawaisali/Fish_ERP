using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    public enum VendorType
    {
        Supplier = 1,
        Customer = 2
    }

    [Table("Vendors")]
    public class Vendor : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Vendor Type")]
        public VendorType VendorType { get; set; } = VendorType.Supplier;

        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "City / Village")]
        public string? City { get; set; }

        [MaxLength(300)]
        [Column(TypeName = "nvarchar(300)")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [MaxLength(30)]
        [Column(TypeName = "varchar(30)")]
        [Display(Name = "CNIC")]
        public string? Cnic { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "Account No / Bank")]
        public string? AccountNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Opening Balance")]
        public decimal OpeningBalance { get; set; } = 0;

        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        [Display(Name = "Chart of Account")]
        public int? ChartOfAccountId { get; set; }

        [Display(Name = "Parent Supplier")]
        public int? ParentSupplierId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }

        [ForeignKey(nameof(ChartOfAccountId))]
        public virtual ChartOfAccount? ChartOfAccount { get; set; }

        [ForeignKey(nameof(ParentSupplierId))]
        public virtual Vendor? ParentSupplier { get; set; }

        public virtual ICollection<Vendor> SubSuppliers { get; set; } = new List<Vendor>();

        // Helper Accessors
        [NotMapped]
        public bool IsSupplier => VendorType == VendorType.Supplier;

        [NotMapped]
        public bool IsCustomer => VendorType == VendorType.Customer;

        [NotMapped]
        public bool IsSubSupplier => ParentSupplierId.HasValue && ParentSupplierId.Value > 0;
    }
}
