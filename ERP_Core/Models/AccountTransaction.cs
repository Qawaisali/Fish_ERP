using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("AccountTransaction")]
    public class AccountTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountTransactionId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Voucher No")]
        public string? VoucherNo { get; set; }


        [Column(TypeName = "decimal(18, 5)")]
        [Display(Name = "Credit")]
        public decimal Credit { get; set; } = 0;

        [Column(TypeName = "decimal(18, 5)")]
        [Display(Name = "Debit")]
        public decimal Debit { get; set; } = 0;

        [Display(Name = "Extra Date")]
        public DateTime? ExtraDate { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Chart of Account Name")]
        public string? ChartofAccountName { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "Invoice No")]
        public string? InvoiceNo { get; set; }

        [MaxLength(1000)]
        [Column(TypeName = "nvarchar(1000)")]
        [Display(Name = "Narration")]
        public string? Narration { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [Display(Name = "Entry Type Name")]
        public string? EntryTypeName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [Display(Name = "Type")]
        public string? Type { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [Display(Name = "Cheque No")]
        public string? ChequeNo { get; set; }

        [Display(Name = "Cheque Date")]
        public DateTime? ChequeDate { get; set; }

        [Display(Name = "Voucher")]
        public int? VoucherId { get; set; }

        [Column(TypeName = "decimal(18, 5)")]
        [Display(Name = "Amount")]
        public decimal? Amount { get; set; }

        public int? JobId { get; set; }

        public int? CompanyId { get; set; }

        public int? UserId { get; set; }

        [Display(Name = "Chart of Account")]
        public int? ChartofAccountId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        public int? PartId { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Account Name")]
        public string? AccountName { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(VoucherId))]
        public virtual Voucher? Voucher { get; set; }

        [ForeignKey(nameof(ChartofAccountId))]
        public virtual ChartOfAccount? ChartOfAccount { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }
    }
}
