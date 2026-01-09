using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("CBBF3314-4D6D-492A-93CC-44B95E25AEF7")]
    public class BubbleChartReport : FullReportEntity, IFullReport
    {
        public BubbleChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<BubbleDataPoint>();

                dataPoints.Add(new BubbleDataPoint()
                {
                    Id = "USA",
                    Name = "United States",
                    Size = 3.35m,
                    X = 2,
                    Y = 6,
                });

                dataPoints.Add(new BubbleDataPoint()
                {
                    Id = "CA",
                    Name = "Canada",
                    Size = 8m,
                    X = 6,
                    Y = 3,
                });

                widgets.Add(new WidgetItem("Sample Bubble Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new BubbleChartContent()
                    {
                        dataPoints = dataPoints,
                        HorizontalText = "Life Expectancy",
                        VerticalText = "Fertility Rate",
                        Name = "Region",
                        Title = "This is a test"
                    }
                });

                return widgets;
            });
        }
    }
}
