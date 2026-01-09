using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-AAAAAAAAAAAA")]
    public class CalendarChartReport : FullReportEntity, IFullReport
    {
        public CalendarChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var dataPoints = new List<CalendarDataPoint>();
                var random = new Random(42); // Seeded for consistent demo data
                var startDate = new DateTime(DateTime.Now.Year, 1, 1);

                // Generate activity data for the year
                for (int i = 0; i < 365; i++)
                {
                    var date = startDate.AddDays(i);
                    var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                    // Less activity on weekends
                    var baseActivity = isWeekend ? 2 : 8;
                    var activity = random.Next(0, baseActivity + 5);

                    if (activity > 0)
                    {
                        dataPoints.Add(new CalendarDataPoint
                        {
                            Date = date,
                            Size = activity
                        });
                    }
                }

                widgets.Add(new WidgetItem("Activity Heatmap")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new CalendarChartContent()
                    {
                        Title = "Daily Activity - " + DateTime.Now.Year,
                        DataPoints = dataPoints
                    }
                });

                return widgets;
            });
        }
    }
}
