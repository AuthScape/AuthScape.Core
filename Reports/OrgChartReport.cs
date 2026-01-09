using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-666666666666")]
    public class OrgChartReport : FullReportEntity, IFullReport
    {
        public OrgChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var nodes = new List<OrgDataPoint>
                {
                    new OrgDataPoint { Id = "ceo", Name = "John Smith<br><i>CEO</i>", ParentId = null, Tooltip = "Chief Executive Officer" },
                    new OrgDataPoint { Id = "cto", Name = "Sarah Johnson<br><i>CTO</i>", ParentId = "ceo", Tooltip = "Chief Technology Officer" },
                    new OrgDataPoint { Id = "cfo", Name = "Michael Chen<br><i>CFO</i>", ParentId = "ceo", Tooltip = "Chief Financial Officer" },
                    new OrgDataPoint { Id = "coo", Name = "Emily Davis<br><i>COO</i>", ParentId = "ceo", Tooltip = "Chief Operating Officer" },
                    new OrgDataPoint { Id = "dev_lead", Name = "Alex Wilson<br><i>Dev Lead</i>", ParentId = "cto", Tooltip = "Development Team Lead" },
                    new OrgDataPoint { Id = "qa_lead", Name = "Lisa Brown<br><i>QA Lead</i>", ParentId = "cto", Tooltip = "Quality Assurance Lead" },
                    new OrgDataPoint { Id = "finance_mgr", Name = "David Lee<br><i>Finance Mgr</i>", ParentId = "cfo", Tooltip = "Finance Manager" },
                    new OrgDataPoint { Id = "ops_mgr", Name = "Jennifer White<br><i>Ops Manager</i>", ParentId = "coo", Tooltip = "Operations Manager" },
                    new OrgDataPoint { Id = "dev1", Name = "Tom Harris<br><i>Developer</i>", ParentId = "dev_lead", Tooltip = "Senior Developer" },
                    new OrgDataPoint { Id = "dev2", Name = "Anna Martinez<br><i>Developer</i>", ParentId = "dev_lead", Tooltip = "Developer" }
                };

                widgets.Add(new WidgetItem("Company Organization")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new OrgChartContent()
                    {
                        Title = "Company Organizational Structure",
                        Nodes = nodes,
                        AllowHtml = true,
                        AllowCollapse = true,
                        Size = "medium"
                    }
                });

                return widgets;
            });
        }
    }
}
