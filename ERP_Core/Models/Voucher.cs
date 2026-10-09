using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    public enum VoucherType
    {
        JV = 1, // Journal Voucher
        CP = 2, // Cash Payment Voucher (CPV)
        CR = 3, // Cash Receipt Voucher (CRV)
        BP = 4, // Bank Payment Voucher (BPV)
        BR = 5  // Bank Receipt Voucher (BRV)
    }

    [Table("Voucher")]
    public class Voucher
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VoucherId { get; set; }

        [Required(ErrorMessage = "Voucher No is required.")]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Voucher No")]
        public string VoucherNo { get; set; } = string.Empty;

        [Display(Name = "Voucher Date")]
        public DateTime VoucherDate { get; set; } = DateTime.Now;

        [Display(Name = "Voucher Type")]
        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string VoucherType { get; set; } = "CP";

        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        [Display(Name = "Amount In Words")]
        public string? AmountInWords { get; set; }

        [Column(TypeName = "decimal(18, 5)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; } = 0;

        public int? UserId { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? Username { get; set; }

        public int? CompanyId { get; set; }

        public int? DeleteStatus { get; set; } = 0;

        public int? VendorId { get; set; }

        public int? InvoiceId { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? InvoiceNo { get; set; }

        [MaxLength(1000)]
        [Column(TypeName = "nvarchar(1000)")]
        public string? Narration { get; set; }

        public int? CurrencyId { get; set; }

        [Column(TypeName = "decimal(18, 5)")]
        [Display(Name = "FC Rate")]
        public decimal? FCRate { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Approval Status")]
        public string? ApprovalStatus { get; set; } = "Approved";

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? InvoiceRefNo { get; set; }

        public int? FinancialYearID { get; set; }

        public DateTime? CreatedOn { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }

        [ForeignKey(nameof(FinancialYearID))]
        public virtual FinancialYear? FinancialYear { get; set; }

        [ForeignKey(nameof(VendorId))]
        public virtual Vendor? Vendor { get; set; }

        public virtual ICollection<AccountTransaction> AccountTransactions { get; set; } = new List<AccountTransaction>();
    }
}
