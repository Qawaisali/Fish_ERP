using ERP_Core.Data;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using ERP_Core.Models;
using Microsoft.EntityFrameworkCore;
using Dapper;

namespace ERP_Core.DataAccessLayer
{
    public class CompanyDAL
    {
        private readonly ERPDbContext _context;

        public CompanyDAL(ERPDbContext context)
        {
            _context = context;
        }

        public async Task<Company?> SelectDataByIDAsync(int id)
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();

            const string spName = "sp_GetCompanyById";

            return await connection.QueryFirstOrDefaultAsync<Company>(
                spName,
                new { CompanyId = id },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<DataTable> GetCompanyData(int companyId)
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();

            const string query = @"
            SELECT CompanyId, CompanyName, Phone, Address, TrnNumber,
                   EmailId, Web, Logo, CODE 
            FROM Company 
            WHERE CompanyId = @CompanyId";

            var result = connection.Query(query, new { CompanyId = companyId });

            return ToDataTable(result);
        }

        public async Task<Company?> SelectDataByIDAsync(int id, DbConnection connection, IDbTransaction transaction)
        {
            const string spName = "sp_GetCompanyById";

            return await connection.QueryFirstOrDefaultAsync<Company>(
                spName,
                new { CompanyId = id },
                commandType: CommandType.StoredProcedure,
                transaction: transaction
            );
        }

        private static DataTable ToDataTable(IEnumerable<dynamic> data)
        {
            var dt = new DataTable();

            foreach (var item in data)
            {
                var dict = (IDictionary<string, object>)item;
                if (dt.Columns.Count == 0)
                {
                    foreach (var key in dict.Keys)
                        dt.Columns.Add(key);
                }

                var row = dt.NewRow();
                foreach (var key in dict.Keys)
                    row[key] = dict[key] ?? DBNull.Value;
                dt.Rows.Add(row);
            }
            return dt;
        }
    }
}
