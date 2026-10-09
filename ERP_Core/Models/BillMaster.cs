using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("BillMasters")]
    public class BillMaster : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Bill No is required.")]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Bill No")]
        public string BillNo { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Bill Date")]
        public DateTime BillDate { get; set; } = DateTime.Today;

        [Display(Name = "Supplier (Farmer / Boat)")]
        public int? SupplierId { get; set; }

        [Display(Name = "Customer (Buyer)")]
        public int? CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Commission %")]
        public decimal CommissionPercent { get; set; } = 6.0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Commission Amount")]
        public decimal CommissionAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Labour Expense")]
        public decimal LabourExpense { get; set; } = 0;

        [Column("LoadingExpense", TypeName = "decimal(18,2)")]
        [Display(Name = "Ice Expense")]
        public decimal IceExpense { get; set; } = 0;

        [NotMapped]
        public decimal LoadingExpense { get => IceExpense; set => IceExpense = value; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Farmer Ice Expense")]
        public decimal FarmerIceExpense { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Freight Expense")]
        public decimal FreightExpense { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Other Expense")]
        public decimal OtherExpense { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Expense")]
        public decimal TotalExpense { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Gross Amount / Sale")]
        public decimal TotalGrossAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Deposit / Martey")]
        public decimal Deposit { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Net Payable")]
        public decimal NetPayable { get; set; } = 0;

        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Due"; // Due, Paid, Partial, Draft

        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(SupplierId))]
        public virtual Vendor? Supplier { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Vendor? Customer { get; set; }

        public virtual ICollection<BillDetail> Details { get; set; } = new List<BillDetail>();
    }
}
