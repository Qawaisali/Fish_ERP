using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("ChartOfAccounts")]
    public class ChartOfAccount : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Account Code")]
        public string? AccountCode { get; set; }

        [Required(ErrorMessage = "Account Name is required.")]
        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Display(Name = "Account Name")]
        public string AccountName { get; set; } = string.Empty;

        [Display(Name = "Parent Account")]
        public int? ParentAccountId { get; set; }

        [Display(Name = "Nature of Account")]
        public int? NatureOfAccountId { get; set; }

        [MaxLength(10)]
        [Column(TypeName = "varchar(10)")]
        [Display(Name = "Currency")]
        public string? Currency { get; set; } = "PKR";

        [Display(Name = "Transable")]
        public bool Transable { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        [Display(Name = "Account Level")]
        public int? LevelNo { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ParentAccountId))]
        public virtual ChartOfAccount? ParentAccount { get; set; }

        public virtual ICollection<ChartOfAccount> SubAccounts { get; set; } = new List<ChartOfAccount>();

        [ForeignKey(nameof(NatureOfAccountId))]
        public virtual NatureofAccount? NatureOfAccount { get; set; }
    }
}
