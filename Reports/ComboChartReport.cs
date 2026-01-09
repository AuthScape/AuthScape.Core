using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-222222222222")]
    public class ComboChartReport : FullReportEntity, IFullReport
    {
        public ComboChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var xAxis = new List<string> { "Month", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

                var dataPoints = new List<ComboChartDataPoint>
                {
                    new ComboChartDataPoint
                    {
                        Label = "Revenue",
                        Data = new List<double> { 5000, 6200, 5800, 7100, 7500, 8200 },
                        SeriesType = "bars"
                    },
                    new ComboChartDataPoint
                    {
                        Label = "Expenses",
                        Data = new List<double> { 3500, 4100, 4300, 4800, 5000, 5200 },
                        SeriesType = "bars"
                    },
                    new ComboChartDataPoint
                    {
                        Label = "Profit Margin %",
                        Data = new List<double> { 30, 34, 26, 32, 33, 37 },
                        SeriesType = "line"
                    }
                };

                widgets.Add(new WidgetItem("Revenue vs Expenses with Profit Margin")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new ComboChartContent()
                    {
                        Title = "Revenue vs Expenses with Profit Margin",
                        XAxis = xAxis,
                        DataPoints = dataPoints,
                        DefaultSeriesType = "bars"
                    }
                });

                return widgets;
            });
        }
    }
}
