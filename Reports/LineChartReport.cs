using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-111111111111")]
    public class LineChartReport : ReportEntity, IReport
    {
        public LineChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var xAxis = new List<string> { "Month", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

                var dataPoints = new List<LineChartDataPoint>
                {
                    new LineChartDataPoint
                    {
                        Label = "Sales",
                        Data = new List<double> { 1000, 1170, 660, 1030, 1200, 1350, 1100, 1400, 1250, 1500, 1600, 1750 }
                    },
                    new LineChartDataPoint
                    {
                        Label = "Expenses",
                        Data = new List<double> { 400, 460, 1120, 540, 600, 680, 720, 800, 750, 850, 900, 950 }
                    },
                    new LineChartDataPoint
                    {
                        Label = "Profit",
                        Data = new List<double> { 600, 710, -460, 490, 600, 670, 380, 600, 500, 650, 700, 800 }
                    }
                };

                return new Widget("Monthly Sales Trend")
                {
                    Content = new LineChartContent()
                    {
                        Title = "Monthly Sales Trend",
                        XAxis = xAxis,
                        DataPoints = dataPoints,
                        CurveLines = true
                    },
                };
            });
        }
    }
}
