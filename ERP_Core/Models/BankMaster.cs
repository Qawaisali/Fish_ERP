using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("BankMasters")]
    public class BankMaster : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Bank Name is required.")]
        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [Display(Name = "Parent Bank")]
        public int? ParentBankId { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "Bank Account No")]
        public string? AccountNo { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Account Title")]
        public string? AccountName { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        [Display(Name = "IBAN No")]
        public string? IbanNo { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Swift Code")]
        public string? SwiftCode { get; set; }

        [Display(Name = "Account Code")]
        public int? ChartOfAccountId { get; set; }

        [Display(Name = "Bank")]
        public bool IsBank { get; set; } = true;

        [Display(Name = "Cash")]
        public bool IsCash { get; set; } = false;

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ParentBankId))]
        public virtual BankMaster? ParentBank { get; set; }

        public virtual ICollection<BankMaster> SubBanks { get; set; } = new List<BankMaster>();

        [ForeignKey(nameof(ChartOfAccountId))]
        public virtual ChartOfAccount? ChartOfAccount { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }
    }
}
