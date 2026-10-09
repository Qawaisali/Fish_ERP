using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ERP_Core.Services.IServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Reporting.NETCore;

namespace ERP_Core.Services
{
    public class ReportService : IReportService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReportService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<FileContentResult> GeneratePdfReportWithParametersAsync<T>(
            List<T> items,
            string reportFileName,
            string dataSourceName,
            string companyLogo,
            string rptTemplate,
            string rptTemplateColor,
            string reportName,
            List<ReportParameter> parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var dataTable = ListToDataTable(items);
                    var reportPath = Path.Combine(_webHostEnvironment.WebRootPath, "Reports", $"{reportFileName}.rdlc");

                    if (!File.Exists(reportPath))
                        throw new Exception($"Report file not found at: {reportPath}");

                    using var localReport = new LocalReport();
                    localReport.EnableExternalImages = true;
                    localReport.ReportPath = reportPath;

                    // Check if data source exists in report
                    localReport.DataSources.Add(new ReportDataSource(dataSourceName, dataTable));

                    string imageFilePath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "Uploads", "Company", "Logo", companyLogo ?? "");
                    string reportColorTemplate = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "ReportTemplates", rptTemplate ?? "", rptTemplateColor ?? "");

                    string imagePath = File.Exists(imageFilePath) ? new Uri(imageFilePath).AbsoluteUri : "";
                    string imageReportColorPath = File.Exists(reportColorTemplate) ? new Uri(reportColorTemplate).AbsoluteUri : "";

                    // Add default parameters
                    var allParameters = new List<ReportParameter>();

                    if (!string.IsNullOrEmpty(reportName))
                        allParameters.Add(new ReportParameter("ReportName", reportName));

                    if (!string.IsNullOrEmpty(imagePath))
                        allParameters.Add(new ReportParameter("ImagePath", imagePath));

                    if (!string.IsNullOrEmpty(imageReportColorPath))
                        allParameters.Add(new ReportParameter("TemplateImagePath", imageReportColorPath));

                    // Add custom parameters
                    if (parameters != null && parameters.Any())
                        allParameters.AddRange(parameters);

                    var definedParams = localReport.GetParameters();
                    var validParams = allParameters.Where(p => definedParams.Any(dp => dp.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                    if (validParams.Any())
                        localReport.SetParameters(validParams);

                    // Render with detailed error
                    byte[] pdf = localReport.Render("PDF");
                    return new FileContentResult(pdf, "application/pdf");
                }
                catch (Exception ex)
                {
                    // Get the real error message with full inner exception tree
                    string errorMessage = ex.Message;
                    var inner = ex.InnerException;
                    while (inner != null)
                    {
                        errorMessage += " | Inner: " + inner.Message;
                        inner = inner.InnerException;
                    }

                    throw new Exception($"RDLC Error: {errorMessage}");
                }
            });
        }

        public static DataTable ListToDataTable<T>(IEnumerable<T> items)
        {
            var dataTable = new DataTable(typeof(T).Name);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                dataTable.Columns.Add(prop.Name, propType);
            }

            if (items != null)
            {
                foreach (var item in items)
                {
                    var values = new object?[properties.Length];
                    for (int i = 0; i < properties.Length; i++)
                    {
                        values[i] = properties[i].GetValue(item, null) ?? DBNull.Value;
                    }
                    dataTable.Rows.Add(values);
                }
            }

            return dataTable;
        }
    }
}
