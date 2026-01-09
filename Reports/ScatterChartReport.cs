using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-999999999999")]
    public class ScatterChartReport : FullReportEntity, IFullReport
    {
        public ScatterChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<ScatterChartDataPoint>
                {
                    new ScatterChartDataPoint(8, 12),
                    new ScatterChartDataPoint(4, 5.5m),
                    new ScatterChartDataPoint(11, 14),
                    new ScatterChartDataPoint(4, 4.5m),
                    new ScatterChartDataPoint(3, 3.5m),
                    new ScatterChartDataPoint(6.5m, 7),
                    new ScatterChartDataPoint(9, 11),
                    new ScatterChartDataPoint(7, 8),
                    new ScatterChartDataPoint(5, 6),
                    new ScatterChartDataPoint(10, 13),
                    new ScatterChartDataPoint(6, 7.5m),
                    new ScatterChartDataPoint(8.5m, 10),
                    new ScatterChartDataPoint(2, 2.5m),
                    new ScatterChartDataPoint(7.5m, 9),
                    new ScatterChartDataPoint(5.5m, 6.5m)
                };

                widgets.Add(new WidgetItem("Hours Studied vs Test Score")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new ScatterChartContent()
                    {
                        Title = "Study Time vs Performance",
                        DataPoints = dataPoints,
                        XName = "Hours Studied",
                        YName = "Test Score"
                    }
                });

                return widgets;
            });
        }
    }
}
