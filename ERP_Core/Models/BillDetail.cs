using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("BillDetails")]
    public class BillDetail : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int BillId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Lot No")]
        public string? LotNo { get; set; }

        [Display(Name = "Fish Variety")]
        public int? FishId { get; set; }

        [Display(Name = "Supplier (Boat / Farmer)")]
        public int? SupplierId { get; set; }

        [Display(Name = "Customer (Buyer)")]
        public int? CustomerId { get; set; }

        [Display(Name = "Crates / Boxes")]
        public int? Crates { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Weight (Kg)")]
        public decimal WeightKg { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rate / Kg")]
        public decimal RatePerKg { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; } = 0;

        [MaxLength(250)]
        [Column(TypeName = "nvarchar(250)")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(BillId))]
        public virtual BillMaster? Bill { get; set; }

        [ForeignKey(nameof(FishId))]
        public virtual Fish? Fish { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public virtual Vendor? Supplier { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Vendor? Customer { get; set; }
    }
}
