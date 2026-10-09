using ERP_Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ERP_Core.Data
{
    public class ERPDbContext : IdentityDbContext<ApplicationUser>
    {
        public ERPDbContext(DbContextOptions<ERPDbContext> options) : base(options)
        {
        }

        public DbSet<ApplicationUser> CApplicationUsers { get; set; }
        public DbSet<Role> CRoles { get; set; }
        public DbSet<FinancialYear> CFinancialYears { get; set; }
        public DbSet<Company> CCompanies { get; set; }
        public DbSet<Modules> CModules { get; set; }
        public DbSet<RoleModulesPermission> CRoleModulesPermissions { get; set; }
        public DbSet<Department> CDepartments { get; set; }
        public DbSet<Designation> CDesignations { get; set; }
        public DbSet<NatureofAccount> NatureofAccounts { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<AccountSetup> AccountSetups { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<OpeningBalance> OpeningBalances { get; set; }
        public DbSet<OpeningBalanceDetail> OpeningBalanceDetails { get; set; }
        public DbSet<BankMaster> BankMasters { get; set; }
        public DbSet<Fish> Fishes { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<BillMaster> BillMasters { get; set; }
        public DbSet<BillDetail> BillDetails { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<AccountTransaction> AccountTransactions { get; set; }
        public DbSet<SupplierDistributionMaster> SupplierDistributionMasters { get; set; }
        public DbSet<SupplierDistributionDetail> SupplierDistributionDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure EmpId as unique property for employee identity
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(u => u.EmpId).IsUnique();
            });

            builder.Entity<AccountSetup>(entity =>
            {
                entity.ToTable("AccountSetup");
                entity.HasKey(e => e.AccountSetupId);
            });

            builder.Entity<NatureofAccount>(entity =>
            {
                entity.ToTable("NatureofAccount");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AccountName)
                    .HasMaxLength(100)
                    .IsUnicode(false);
                entity.Property(e => e.DebtorOrCreditor)
                    .HasMaxLength(1)
                    .IsFixedLength();
            });

            builder.Entity<ChartOfAccount>(entity =>
            {
                entity.ToTable("ChartOfAccounts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AccountCode).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.AccountName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Currency).HasMaxLength(10).IsUnicode(false);

                entity.HasOne(e => e.ParentAccount)
                    .WithMany(p => p.SubAccounts)
                    .HasForeignKey(e => e.ParentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.NatureOfAccount)
                    .WithMany()
                    .HasForeignKey(e => e.NatureOfAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Vendor>(entity =>
            {
                entity.ToTable("Vendors");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.VendorType).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.Phone).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.Address).HasMaxLength(300);
                entity.Property(e => e.Cnic).HasMaxLength(30).IsUnicode(false);
                entity.Property(e => e.AccountNo).HasMaxLength(100);
                entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
                entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false);

                entity.HasOne(e => e.ChartOfAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ChartOfAccountId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ParentSupplier)
                    .WithMany(p => p.SubSuppliers)
                    .HasForeignKey(e => e.ParentSupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OpeningBalance>(entity =>
            {
                entity.ToTable("OpeningBalances");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OBCode).HasMaxLength(50).IsUnicode(false).IsRequired();

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FinancialYear)
                    .WithMany()
                    .HasForeignKey(e => e.FinancialYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Details)
                    .WithOne(d => d.OpeningBalance)
                    .HasForeignKey(d => d.OBId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OpeningBalanceDetail>(entity =>
            {
                entity.ToTable("OpeningBalanceDetails");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OBDtlTranType).HasMaxLength(10).IsUnicode(false).IsRequired();
                entity.Property(e => e.OBDtlOpenBlnc).HasPrecision(18, 4);

                entity.HasOne(e => e.OpeningBalance)
                    .WithMany(m => m.Details)
                    .HasForeignKey(e => e.OBId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChartOfAccount)
                    .WithMany()
                    .HasForeignKey(e => e.COAId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<BankMaster>(entity =>
            {
                entity.ToTable("BankMasters");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BankName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.AccountNo).HasMaxLength(100);
                entity.Property(e => e.AccountName).HasMaxLength(200);
                entity.Property(e => e.IbanNo).HasMaxLength(100).IsUnicode(false);
                entity.Property(e => e.SwiftCode).HasMaxLength(50).IsUnicode(false);

                entity.HasOne(e => e.ParentBank)
                    .WithMany(p => p.SubBanks)
                    .HasForeignKey(e => e.ParentBankId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ChartOfAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ChartOfAccountId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Fish>(entity =>
            {
                entity.ToTable("Fishes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Unit).HasMaxLength(20).IsUnicode(false);

                entity.HasOne(e => e.Category)
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ProductCategory>(entity =>
            {
                entity.ToTable("ProductCategories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.Name).HasMaxLength(150).IsRequired();

                entity.HasOne(e => e.PurchaseAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PurchaseAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SaleAccount)
                    .WithMany()
                    .HasForeignKey(e => e.SaleAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PurchaseReturnAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PurchaseReturnAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SaleReturnAccount)
                    .WithMany()
                    .HasForeignKey(e => e.SaleReturnAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<BillMaster>(entity =>
            {
                entity.ToTable("BillMasters");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).HasMaxLength(50).IsUnicode(false).IsRequired();
                entity.Property(e => e.Deposit).HasPrecision(18, 2);
                entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false);
                entity.Property(e => e.Remarks).HasMaxLength(500);

                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Details)
                    .WithOne(d => d.Bill)
                    .HasForeignKey(d => d.BillId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<BillDetail>(entity =>
            {
                entity.ToTable("BillDetails");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LotNo).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.Remarks).HasMaxLength(250);

                entity.HasOne(e => e.Fish)
                    .WithMany()
                    .HasForeignKey(e => e.FishId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Voucher>(entity =>
            {
                entity.ToTable("Voucher");
                entity.HasKey(e => e.VoucherId);
                entity.Property(e => e.VoucherNo).HasMaxLength(50).IsUnicode(false).IsRequired();
                entity.Property(e => e.VoucherType).HasMaxLength(20).IsUnicode(false);
                entity.Property(e => e.Amount).HasPrecision(18, 5);
                entity.Property(e => e.FCRate).HasPrecision(18, 5);
                entity.Property(e => e.ApprovalStatus).HasMaxLength(50).IsUnicode(false);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FinancialYear)
                    .WithMany()
                    .HasForeignKey(e => e.FinancialYearID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Vendor)
                    .WithMany()
                    .HasForeignKey(e => e.VendorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.AccountTransactions)
                    .WithOne(t => t.Voucher)
                    .HasForeignKey(t => t.VoucherId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AccountTransaction>(entity =>
            {
                entity.ToTable("AccountTransaction");
                entity.HasKey(e => e.AccountTransactionId);
                entity.Property(e => e.VoucherNo).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.Type).HasMaxLength(50);
                entity.Property(e => e.Credit).HasPrecision(18, 5);
                entity.Property(e => e.Debit).HasPrecision(18, 5);
                entity.Property(e => e.Amount).HasPrecision(18, 5);
                entity.Property(e => e.Code).HasMaxLength(50).IsUnicode(false);

                entity.HasOne(e => e.Voucher)
                    .WithMany(v => v.AccountTransactions)
                    .HasForeignKey(e => e.VoucherId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChartOfAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ChartofAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SupplierDistributionMaster>(entity =>
            {
                entity.ToTable("SupplierDistributionMasters");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DistributionNo).HasMaxLength(50).IsUnicode(false).IsRequired();
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.Remarks).HasMaxLength(500);

                entity.HasOne(e => e.ParentSupplier)
                    .WithMany()
                    .HasForeignKey(e => e.ParentSupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Voucher)
                    .WithMany()
                    .HasForeignKey(e => e.VoucherId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Details)
                    .WithOne(d => d.DistributionMaster)
                    .HasForeignKey(d => d.SupplierDistributionMasterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SupplierDistributionDetail>(entity =>
            {
                entity.ToTable("SupplierDistributionDetails");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Remarks).HasMaxLength(300);

                entity.HasOne(e => e.DistributionMaster)
                    .WithMany(m => m.Details)
                    .HasForeignKey(e => e.SupplierDistributionMasterId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChildSupplier)
                    .WithMany()
                    .HasForeignKey(e => e.ChildSupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, LogLevel.Information);

            base.OnConfiguring(optionsBuilder);
        }
    }
}
