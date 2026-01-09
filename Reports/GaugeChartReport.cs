using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("973D18A2-FF6F-4826-8F62-852C005E7644")]
    public class GaugeChartReport : FullReportEntity, IFullReport
    {
        public GaugeChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<GaugeDataPoint>();

                dataPoints.Add(new GaugeDataPoint()
                {
                    Label = "Sales",
                    value = 24
                });

                dataPoints.Add(new GaugeDataPoint()
                {
                    Label = "Expenses",
                    value = 12
                });

                widgets.Add(new WidgetItem("Sample Gauge Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new GaugeChartContent()
                    {
                        DataPoints = dataPoints
                    }
                });

                return widgets;
            });
        }
    }
}
