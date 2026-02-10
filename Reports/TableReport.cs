using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("E5F6A7B8-C9D0-1E2F-3A4B-5C6D7E8F9A01")]
    public class SampleTableReport : FullReportEntity, IFullReport
    {
        public SampleTableReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var data = new List<object>
                {
                    new { Name = "Alice", Department = "Engineering", Sales = 45000 },
                    new { Name = "Bob", Department = "Marketing", Sales = 32000 },
                    new { Name = "Charlie", Department = "Engineering", Sales = 51000 },
                    new { Name = "Diana", Department = "Sales", Sales = 67000 },
                };

                widgets.Add(new WidgetItem("Sample Table Report")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new TableContent()
                    {
                        Columns = new List<string> { "Name", "Department", "Sales" },
                        Content = data
                    }
                });

                return widgets;
            });
        }
    }
}
