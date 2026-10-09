using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("OpeningBalanceDetails")]
    public class OpeningBalanceDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Opening Balance ID")]
        public int OBId { get; set; }

        [Required]
        [Display(Name = "Chart of Account")]
        public int COAId { get; set; }

        [Required]
        [MaxLength(10)]
        [Column(TypeName = "varchar(10)")]
        [Display(Name = "Transaction Type")]
        public string OBDtlTranType { get; set; } = "DEB"; // "DEB" or "CRE"

        [Column(TypeName = "decimal(18, 4)")]
        [Display(Name = "Opening Balance Amount")]
        public decimal OBDtlOpenBlnc { get; set; } = 0;

        // Navigation Properties
        [ForeignKey(nameof(OBId))]
        public virtual OpeningBalance? OpeningBalance { get; set; }

        [ForeignKey(nameof(COAId))]
        public virtual ChartOfAccount? ChartOfAccount { get; set; }
    }
}
