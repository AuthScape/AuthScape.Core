using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-777777777777")]
    public class SteppedAreaChartReport : ReportEntity, IReport
    {
        public SteppedAreaChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var xAxis = new List<string> { "Day", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

                var dataPoints = new List<SteppedAreaDataPoint>
                {
                    new SteppedAreaDataPoint
                    {
                        Label = "Warehouse A",
                        Data = new List<double> { 1500, 1450, 1380, 1520, 1480, 1420, 1400 }
                    },
                    new SteppedAreaDataPoint
                    {
                        Label = "Warehouse B",
                        Data = new List<double> { 800, 750, 820, 780, 850, 810, 790 }
                    }
                };

                return new Widget("Inventory Levels")
                {
                    Content = new SteppedAreaChartContent()
                    {
                        Title = "Weekly Inventory Levels",
                        XAxis = xAxis,
                        DataPoints = dataPoints,
                        IsStacked = false
                    },
                };
            });
        }
    }
}
