using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("OpeningBalances")]
    public class OpeningBalance : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "OB Code")]
        public string OBCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "OB Date")]
        public DateTime OBDate { get; set; } = DateTime.Today;

        [Display(Name = "Company")]
        public int CompanyId { get; set; }

        [Display(Name = "Financial Year")]
        public int FinancialYearId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }

        [ForeignKey(nameof(FinancialYearId))]
        public virtual FinancialYear? FinancialYear { get; set; }

        public virtual ICollection<OpeningBalanceDetail> Details { get; set; } = new List<OpeningBalanceDetail>();
    }
}
