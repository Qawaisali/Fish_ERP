using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERP_Core.Models
{
    [Table("Fishes")]
    public class Fish : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Display(Name = "Fish Code")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "Fish Name is required.")]
        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "Fish Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Fish Type is required.")]
        [MaxLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        [Display(Name = "Fish Type")]
        public string Type { get; set; } = "Sea Fish";

        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        [Display(Name = "Unit of Measure")]
        public string Unit { get; set; } = "Kg";

        [Display(Name = "Product Category")]
        public int? CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public virtual ProductCategory? Category { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }
    }
}
