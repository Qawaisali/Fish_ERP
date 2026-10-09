using Microsoft.AspNetCore.Mvc;
using Microsoft.Reporting.NETCore;

namespace ERP_Core.Services.IServices
{
    public interface IReportService
    {
        Task<FileContentResult> GeneratePdfReportWithParametersAsync<T>(
        List<T> items,
        string reportFileName,
        string dataSourceName,
        string companyLogo,
        string rptTemplate,
        string rptTemplateColor,
        string reportName,
        List<ReportParameter> parameters);
    }
}
