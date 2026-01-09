using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("94D281D1-2E5E-4CB8-8F9C-D956044DF3FD")]
    public class CandleStickChartReport : FullReportEntity, IFullReport
    {
        public CandleStickChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<CandleStickChartDataPoint>();

                dataPoints.Add(new CandleStickChartDataPoint()
                {
                    Label = "Mon",
                    Data = new List<double>() { 5, 10, 15, 20 }
                });

                dataPoints.Add(new CandleStickChartDataPoint()
                {
                    Label = "Tues",
                    Data = new List<double>() { 12, 15, 12, 20 }
                });

                dataPoints.Add(new CandleStickChartDataPoint()
                {
                    Label = "Wed",
                    Data = new List<double>() { 12, 15, 12, 20 }
                });

                dataPoints.Add(new CandleStickChartDataPoint()
                {
                    Label = "Thur",
                    Data = new List<double>() { 12, 15, 12, 20 }
                });

                widgets.Add(new WidgetItem("Sample CandleStick Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new BarCandleStickContent()
                    {
                        dataPoints = dataPoints,
                        XAxis = new List<string>() { "Year", "2013", "2014", "2015", "2018" }
                    }
                });

                return widgets;
            });
        }
    }
}
