namespace ERP_Core.Models
{
    public class FinancialYear : BaseEntity
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool CurrentYear { get; set; }
        public bool LockYear { get; set; }
        public int CompanyID { get; set; }
    }
}
