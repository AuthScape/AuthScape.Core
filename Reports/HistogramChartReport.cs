using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("E6DACE5B-0CA2-4C30-9D36-CC1964E408E0")]
    public class HistogramChartReport : FullReportEntity, IFullReport
    {
        public HistogramChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<HistogramsDataPoint>();

                dataPoints.Add(new HistogramsDataPoint()
                {
                    Name = "Acrocanthosaurus",
                    Number = 12.2m
                });

                dataPoints.Add(new HistogramsDataPoint()
                {
                    Name = "Albertosaurus",
                    Number = 9.1m
                });

                dataPoints.Add(new HistogramsDataPoint()
                {
                    Name = "Allosaurus",
                    Number = 12.2m
                });

                widgets.Add(new WidgetItem("Sample Histogram Chart")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new HistogramsContent()
                    {
                        NameText = "Dinosaur",
                        NumberText = "Length",
                        DataPoints = dataPoints
                    }
                });

                return widgets;
            });
        }
    }
}
