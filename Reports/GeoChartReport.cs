using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-555555555555")]
    public class GeoChartReport : FullReportEntity, IFullReport
    {
        public GeoChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<GeoDataPoint>
                {
                    new GeoDataPoint { Location = "United States", Value = 52000, Label = "USA Sales" },
                    new GeoDataPoint { Location = "Canada", Value = 12500, Label = "Canada Sales" },
                    new GeoDataPoint { Location = "United Kingdom", Value = 18300, Label = "UK Sales" },
                    new GeoDataPoint { Location = "Germany", Value = 15800, Label = "Germany Sales" },
                    new GeoDataPoint { Location = "France", Value = 11200, Label = "France Sales" },
                    new GeoDataPoint { Location = "Australia", Value = 9500, Label = "Australia Sales" },
                    new GeoDataPoint { Location = "Japan", Value = 14200, Label = "Japan Sales" },
                    new GeoDataPoint { Location = "Brazil", Value = 7800, Label = "Brazil Sales" },
                    new GeoDataPoint { Location = "India", Value = 6500, Label = "India Sales" },
                    new GeoDataPoint { Location = "Mexico", Value = 5200, Label = "Mexico Sales" }
                };

                widgets.Add(new WidgetItem("Global Sales Distribution")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new GeoChartContent()
                    {
                        Title = "Sales by Country",
                        DataPoints = dataPoints,
                        Region = "world",
                        DisplayMode = "regions",
                        ValueLabel = "Sales ($)"
                    }
                });

                return widgets;
            });
        }
    }
}
