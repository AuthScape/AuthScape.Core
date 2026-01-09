using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("D419A0DD-B291-49DC-8B03-29FD389635D3")]
    public class TimelineChartReport : FullReportEntity, IFullReport
    {
        public TimelineChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<TimelineDataPoint>();

                dataPoints.Add(new TimelineDataPoint()
                {
                    Label = "Sales",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(4),
                });

                dataPoints.Add(new TimelineDataPoint()
                {
                    Label = "Expenses",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(2)
                });

                widgets.Add(new WidgetItem("Sample Timeline Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new TimelineChartContent()
                    {
                        DataPoints = dataPoints
                    }
                });

                return widgets;
            });
        }
    }
}
