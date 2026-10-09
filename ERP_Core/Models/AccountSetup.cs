using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("AccountSetup")]
    public class AccountSetup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountSetupId { get; set; }
        public int? SupplierAccID { get; set; }
        public int? CustomerAccID { get; set; }
        public int? VatInputAccID { get; set; }
        public int? VatOutputAccID { get; set; }
        public int? CompanyId { get; set; }
        public int? BankChargesId { get; set; }
    }
}

