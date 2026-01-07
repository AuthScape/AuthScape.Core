using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-333333333333")]
    public class DonutChartReport : ReportEntity, IReport
    {
        public DonutChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var dataPoints = new List<DonutDataPoint>
                {
                    new DonutDataPoint { Name = "Direct Sales", Value = 35.5m },
                    new DonutDataPoint { Name = "Partner Sales", Value = 25.2m },
                    new DonutDataPoint { Name = "Online Store", Value = 22.8m },
                    new DonutDataPoint { Name = "Retail", Value = 10.5m },
                    new DonutDataPoint { Name = "Other", Value = 6.0m }
                };

                return new Widget("Sales by Channel")
                {
                    Content = new DonutChartContent()
                    {
                        Title = "Sales by Channel",
                        DataPoints = dataPoints,
                        PieHole = 0.4
                    },
                };
            });
        }
    }
}
