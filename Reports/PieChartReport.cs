using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("2437D1D1-DB96-477D-989A-39B4B27275FE")]
    public class PieChartReport : FullReportEntity, IFullReport
    {
        public PieChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<PieChartDataPoint>();

                dataPoints.Add(new PieChartDataPoint()
                {
                    Name = "Sales",
                    Number = 24
                });

                dataPoints.Add(new PieChartDataPoint()
                {
                    Name = "Expenses",
                    Number = 12
                });

                widgets.Add(new WidgetItem("Sample Pie Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new PieChartContent()
                    {
                        DataPoints = dataPoints
                    }
                });

                return widgets;
            });
        }
    }
}
