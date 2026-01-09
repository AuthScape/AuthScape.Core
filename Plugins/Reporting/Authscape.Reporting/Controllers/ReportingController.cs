using Authscape.Reporting.Models;
using Authscape.Reporting.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;

namespace Authscape.Reporting.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportingController : Controller
    {
        readonly IReportService reportService;
        readonly AppSettings appSettings;
        readonly DatabaseContext databaseContext;

        public ReportingController(IReportService reportService, IOptions<AppSettings> appSettings, DatabaseContext databaseContext)
        {
            this.reportService = reportService;
            this.appSettings = appSettings.Value;
            this.databaseContext = databaseContext;
        }

        [HttpPost]
        public async Task<IActionResult> Post(ReportRequest request) // runs a report
        {
            // Check if this is a full report (multi-widget)
            if (reportService.IsFullReport(AppDomain.CurrentDomain, request.id))
            {
                var fullResponse = await reportService.RunFullReport(
                    AppDomain.CurrentDomain,
                    request.id,
                    request.payLoad,
                    new[] { databaseContext }
                );

                var fullReportData = new FullReportData();
                foreach (var widget in fullResponse.Widgets)
                {
                    fullReportData.Widgets.Add(reportService.ProcessWidget(widget));
                }

                return Ok(fullReportData);
            }

            // Existing single-widget logic (unchanged)
            var response = await reportService.RunReport(AppDomain.CurrentDomain, request.id, request.payLoad, new[] { databaseContext });

            return Ok(new ReportData()
            {
                Columns = response.Columns,
                Content = response.Data,
                ReportType = response.ReportType,
                Options = response.Options
            });
        }
    }

    public class ReportRequest
    {
        public Guid id { get; set; }
        public string payLoad { get; set; }
    }
}
