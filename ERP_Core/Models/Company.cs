namespace ERP_Core.Models
{
    public class Company : BaseEntity
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string Country { get; set; } = "Pakistan";
        public string State { get; set; } = "Sindh";
        public int? CurrencyId { get; set; }
        public string Logo { get; set; } = string.Empty;
        public int? CountryId { get; set; }
        public int? UserId { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public int? CityId { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}
