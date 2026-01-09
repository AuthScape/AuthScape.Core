using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-BBBBBBBBBBBB")]
    public class WaterfallChartReport : FullReportEntity, IFullReport
    {
        public WaterfallChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<WaterfallDataPoint>
                {
                    new WaterfallDataPoint { Label = "Starting Balance", Value = 100000, IsTotal = true },
                    new WaterfallDataPoint { Label = "Product Revenue", Value = 85000, IsTotal = false },
                    new WaterfallDataPoint { Label = "Service Revenue", Value = 35000, IsTotal = false },
                    new WaterfallDataPoint { Label = "Gross Revenue", Value = 0, IsTotal = true },
                    new WaterfallDataPoint { Label = "Cost of Goods", Value = -45000, IsTotal = false },
                    new WaterfallDataPoint { Label = "Operating Expenses", Value = -28000, IsTotal = false },
                    new WaterfallDataPoint { Label = "Marketing", Value = -15000, IsTotal = false },
                    new WaterfallDataPoint { Label = "R&D", Value = -12000, IsTotal = false },
                    new WaterfallDataPoint { Label = "Net Profit", Value = 0, IsTotal = true }
                };

                widgets.Add(new WidgetItem("Profit Breakdown")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new WaterfallChartContent()
                    {
                        Title = "Quarterly Profit Breakdown",
                        DataPoints = dataPoints,
                        ShowConnectorLines = true,
                        StartLabel = "Opening",
                        EndLabel = "Closing"
                    }
                });

                return widgets;
            });
        }
    }
}
