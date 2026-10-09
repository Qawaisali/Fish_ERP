using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("NatureofAccount")]
    public class NatureofAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(100)]
        [Column(TypeName = "varchar(100)")]
        [Display(Name = "Account Name")]
        public string? AccountName { get; set; }

        [StringLength(1)]
        [Column(TypeName = "nchar(1)")]
        [Display(Name = "Debtor Or Creditor")]
        public string? DebtorOrCreditor { get; set; }
    }
}
