using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("ProductCategories")]
    public class ProductCategory : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Category Code")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [MaxLength(150)]
        [Column(TypeName = "nvarchar(150)")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Purchase Account")]
        public int? PurchaseAccountId { get; set; }

        [Display(Name = "Sale Account")]
        public int? SaleAccountId { get; set; }

        [Display(Name = "Purchase Return Account")]
        public int? PurchaseReturnAccountId { get; set; }

        [Display(Name = "Sale Return Account")]
        public int? SaleReturnAccountId { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        // Navigation Properties (Linked to Transable Chart of Accounts)
        [ForeignKey(nameof(PurchaseAccountId))]
        public virtual ChartOfAccount? PurchaseAccount { get; set; }

        [ForeignKey(nameof(SaleAccountId))]
        public virtual ChartOfAccount? SaleAccount { get; set; }

        [ForeignKey(nameof(PurchaseReturnAccountId))]
        public virtual ChartOfAccount? PurchaseReturnAccount { get; set; }

        [ForeignKey(nameof(SaleReturnAccountId))]
        public virtual ChartOfAccount? SaleReturnAccount { get; set; }
    }
}
